using System;
using System.IO;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    internal enum WorkerResumeDecision { Release, Retry, WaitForGeometry, Invalidated }
    internal readonly struct WorkerResumeToken
    {
        internal readonly long Generation;
        internal readonly long Serial;
        internal readonly int Slot;
        internal WorkerResumeToken(long generation,long serial,int slot){Generation=generation;Serial=serial;Slot=slot;}
    }
    internal sealed class WorkerResumeTicket
    {
        internal readonly int UnitId, AIState, TribeId;
        internal readonly uint UnitGlobalId, TribeGlobalId;
        internal readonly eChimps UnitType;
        internal readonly ushort Owner, RequestedX, RequestedY;
        internal readonly WorkerJourneySnapshot Job;
        internal WorkerResumeTicket(int unitId,uint unitGlobalId,eChimps unitType,int aiState,ushort owner,
            int tribeId,uint tribeGlobalId,ushort requestedX,ushort requestedY,WorkerJourneySnapshot job)
        {UnitId=unitId;UnitGlobalId=unitGlobalId;UnitType=unitType;AIState=aiState;Owner=owner;
            TribeId=tribeId;TribeGlobalId=tribeGlobalId;RequestedX=requestedX;RequestedY=requestedY;Job=job;}
        internal bool IsValid
        {
            get
            {
                if(UnitId<=0 || TribeId<0 || Job==null || Job.Rule.UnitType!=UnitType || Job.Rule.AIState!=AIState ||
                    !Job.Rule.CanResumeOrdinary)return false;
                return Job.TryGetValue(0x6F0,out uint global) && global==UnitGlobalId &&
                    Job.TryGetValue(0x6EE,out uint owner) && owner==Owner;
            }
        }
    }

    // Owns receipts, not native units. A terminal callback decision is an acknowledgement
    // that the caller already resolved the native parked ownership (including new orders).
    // Never discard a parked receipt merely because time elapsed or a callback threw.
    internal sealed class WorkerResumeLedger
    {
        internal const int Capacity=512;
        private const int MaximumRetries=6;
        private const uint Signature=0x5752534C;
        private const ushort CodecVersion=1;
        private sealed class Entry
        {
            internal WorkerResumeTicket Ticket;
            internal long Serial, Due;
            internal int Retries;
            internal bool Parked, Waiting;
        }
        private readonly Entry[] entries=new Entry[Capacity];
        private long generation=1, serial;
        private int count, active, cursor, dirtyRemaining;
        private bool draining;
        internal int Count=>count;
        // Lifecycle inspection only; tickets and their snapshots are immutable.
        internal WorkerResumeTicket[] CopyTickets()
        {
            var result=new WorkerResumeTicket[count];int at=0;
            foreach(var entry in entries)if(entry!=null)result[at++]=entry.Ticket;
            return result;
        }
        internal int ReservedCount { get {int n=0;foreach(var e in entries)if(e!=null && !e.Parked)n++;return n;} }
        internal bool TryReserve(WorkerResumeTicket ticket,out WorkerResumeToken token)
        {
            token=default;
            if(draining || ticket==null || !ticket.IsValid || count==Capacity)return false;
            int free=-1;
            for(int i=0;i<entries.Length;i++)
            {
                if(entries[i]==null){if(free<0)free=i;}
                else if(entries[i].Ticket.UnitId==ticket.UnitId)return false;
            }
            var entry=new Entry{Ticket=ticket,Serial=++serial};entries[free]=entry;count++;
            token=new WorkerResumeToken(generation,entry.Serial,free);return true;
        }
        private Entry Find(WorkerResumeToken token)=>token.Generation==generation && token.Slot>=0 && token.Slot<Capacity &&
            entries[token.Slot]!=null && entries[token.Slot].Serial==token.Serial ? entries[token.Slot] : null;
        internal bool CommitPark(WorkerResumeToken token,long tick)
        {
            var entry=Find(token);if(draining || entry==null || entry.Parked || tick<0)return false;
            entry.Parked=true;entry.Due=tick;active++;return true;
        }
        internal bool Release(WorkerResumeToken token)
        {
            var entry=Find(token);if(draining || entry==null)return false;
            Remove(token.Slot);return true;
        }
        private void Remove(int slot)
        {var entry=entries[slot];if(entry.Parked && !entry.Waiting)active--;entries[slot]=null;count--;}
        internal void MarkWorldMutation(){if(count!=0)dirtyRemaining=Capacity;}
        internal void Reset()
        {
            if(draining)throw new InvalidOperationException("Cannot reset during a resume callback.");
            Array.Clear(entries,0,entries.Length);count=active=cursor=dirtyRemaining=0;generation++;
        }
        internal int Drain(long tick,int visitBudget,int nativeAttempts,Func<WorkerResumeTicket,WorkerResumeDecision> callback)
        {
            if(draining || tick<0 || visitBudget<=0 || nativeAttempts<=0 || callback==null || (active==0 && dirtyRemaining==0))return 0;
            int attempted=0;draining=true;
            try
            {
                int visits=Math.Min(visitBudget,Capacity);
                while(visits-->0 && attempted<nativeAttempts)
                {
                    int slot=cursor;cursor=(cursor+1)%Capacity;
                    bool dirty=dirtyRemaining>0;if(dirty)dirtyRemaining--;
                    var entry=entries[slot];if(entry==null || !entry.Parked)continue;
                    if(entry.Waiting)
                    {
                        if(!dirty)continue;
                        entry.Waiting=false;entry.Retries=0;entry.Due=tick;active++;
                    }
                    if(entry.Due>tick)continue;
                    attempted++;
                    WorkerResumeDecision decision;
                    try {decision=callback(entry.Ticket);} catch {decision=WorkerResumeDecision.WaitForGeometry;}
                    if(decision==WorkerResumeDecision.Release || decision==WorkerResumeDecision.Invalidated)Remove(slot);
                    else if(decision==WorkerResumeDecision.Retry && ++entry.Retries<MaximumRetries)
                        entry.Due=tick>long.MaxValue-(1L<<entry.Retries)?long.MaxValue:tick+(1L<<entry.Retries);
                    else {entry.Waiting=true;active--;}
                }
            }
            finally {draining=false;}
            return attempted;
        }
        internal bool TrySerialize(out byte[] data)
        {
            data=null;if(draining || ReservedCount!=0)return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(Signature);writer.Write(CodecVersion);writer.Write(count);
                foreach(var entry in entries)
                {
                    if(entry==null)continue;var t=entry.Ticket;
                    writer.Write(t.UnitId);writer.Write(t.UnitGlobalId);writer.Write((int)t.UnitType);writer.Write(t.AIState);
                    writer.Write(t.Owner);writer.Write(t.TribeId);writer.Write(t.TribeGlobalId);writer.Write(t.RequestedX);writer.Write(t.RequestedY);
                    writer.Write(entry.Retries);writer.Write(entry.Waiting);
                    var values=t.Job.CopyValues();writer.Write(values.Length);
                    for(int i=0;i<values.Length;i++)
                    {var field=t.Job.Rule.Field(i);writer.Write(field.NativeOffset);writer.Write((byte)field.Width);writer.Write(values[i]);}
                }
                writer.Flush();data=stream.ToArray();return true;
            }
        }
        // Atomic parse: invalid data leaves the existing ledger untouched. Live world/job,
        // flags5 and original-order validation after loading remains mandatory at the caller.
        // Retry deadlines are deliberately rebound to this world's tick, never serialized.
        internal bool TryRestore(byte[] data,long tick)
        {
            if(draining || data==null || data.Length>Capacity*1024+16 || tick<0)return false;
            var restored=new WorkerResumeLedger();
            try
            {
                using(var stream=new MemoryStream(data,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadUInt32()!=Signature || reader.ReadUInt16()!=CodecVersion)return false;
                    int n=reader.ReadInt32();if(n<0 || n>Capacity)return false;
                    for(int record=0;record<n;record++)
                    {
                        int id=reader.ReadInt32();uint global=reader.ReadUInt32();var type=(eChimps)reader.ReadInt32();int state=reader.ReadInt32();
                        ushort owner=reader.ReadUInt16();int tribe=reader.ReadInt32();uint tribeGlobal=reader.ReadUInt32();
                        ushort x=reader.ReadUInt16(),y=reader.ReadUInt16();int retries=reader.ReadInt32();byte wait=reader.ReadByte();
                        if(retries<0 || retries>MaximumRetries || wait>1 || (retries==MaximumRetries && wait==0))return false;
                        if(!WorkerTravelPolicy.TryDescribe(type,state,out var rule) || !rule.CanResumeOrdinary)return false;
                        int fields=reader.ReadInt32();if(fields!=rule.FieldCount)return false;
                        var offsets=new int[fields];var widths=new int[fields];var values=new uint[fields];
                        for(int i=0;i<fields;i++){offsets[i]=reader.ReadInt32();widths[i]=reader.ReadByte();values[i]=reader.ReadUInt32();}
                        if(!WorkerJourneySnapshot.TryRestoreKnownSnapshot(type,state,offsets,widths,values,out var job))return false;
                        var ticket=new WorkerResumeTicket(id,global,type,state,owner,tribe,tribeGlobal,x,y,job);
                        if(!restored.TryReserve(ticket,out var token) || !restored.CommitPark(token,tick))return false;
                        var entry=restored.Find(token);entry.Retries=retries;
                        if(wait!=0){entry.Waiting=true;restored.active--;}
                    }
                    if(stream.Position!=stream.Length)return false;
                }
            }
            catch(IOException){return false;} catch(ArgumentException){return false;} catch(OverflowException){return false;}
            Reset();Array.Copy(restored.entries,entries,Capacity);count=restored.count;active=restored.active;serial=restored.serial;
            // Dormant receipts survive saves; only a later real mutation arms them.
            return true;
        }
    }
}
