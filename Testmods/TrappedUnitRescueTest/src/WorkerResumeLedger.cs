using System;
using System.Collections.Generic;
using System.IO;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    internal enum ResumeReceiptKind : byte { None=0, Worker=1, Command=2 }
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
        internal readonly WorkerBindingSnapshot Bindings;
        internal readonly ResumeReceiptKind Kind;
        internal readonly CommandJourneySnapshot Command;
        internal WorkerResumeTicket(int unitId,uint unitGlobalId,eChimps unitType,int aiState,ushort owner,
            int tribeId,uint tribeGlobalId,ushort requestedX,ushort requestedY,WorkerJourneySnapshot job,WorkerBindingSnapshot bindings=null)
        {UnitId=unitId;UnitGlobalId=unitGlobalId;UnitType=unitType;AIState=aiState;Owner=owner;
            TribeId=tribeId;TribeGlobalId=tribeGlobalId;RequestedX=requestedX;RequestedY=requestedY;Job=job;Bindings=bindings;Kind=ResumeReceiptKind.Worker;}
        private WorkerResumeTicket(int unitId,uint unitGlobalId,eChimps unitType,int aiState,ushort owner,
            int tribeId,uint tribeGlobalId,ushort requestedX,ushort requestedY,CommandJourneySnapshot command)
        {UnitId=unitId;UnitGlobalId=unitGlobalId;UnitType=unitType;AIState=aiState;Owner=owner;
            TribeId=tribeId;TribeGlobalId=tribeGlobalId;RequestedX=requestedX;RequestedY=requestedY;Command=command;
            Bindings=WorkerBindingSnapshot.None;Kind=ResumeReceiptKind.Command;}
        internal static WorkerResumeTicket ForCommand(int unitId,uint unitGlobalId,eChimps unitType,int aiState,ushort owner,
            int tribeId,uint tribeGlobalId,ushort requestedX,ushort requestedY,CommandJourneySnapshot command)
            =>new WorkerResumeTicket(unitId,unitGlobalId,unitType,aiState,owner,tribeId,tribeGlobalId,requestedX,requestedY,command);
        internal bool TryGetValue(int offset,out uint value)
        {
            if(Kind==ResumeReceiptKind.Worker && Job!=null)return Job.TryGetValue(offset,out value);
            if(Kind==ResumeReceiptKind.Command && Command!=null)return Command.TryGetValue(offset,out value);
            value=0;return false;
        }
        internal bool IsValid
        {
            get
            {
                if(UnitId<=0 || UnitId>NativeRelocation.MaximumUnitId || TribeId<0 || TribeId>ushort.MaxValue || Bindings==null)return false;
                if(Kind==ResumeReceiptKind.Worker)
                {if(Job==null || Command!=null || Job.Rule.UnitType!=UnitType || Job.Rule.AIState!=AIState || !Job.Rule.HasOrdinarySchema || !Bindings.MatchesSchema(Job))return false;}
                else if(Kind==ResumeReceiptKind.Command)
                {if(Command==null || Job!=null || Command.UnitType!=UnitType || Command.AIState!=AIState || !Bindings.IsEmpty ||
                    !CommandJourneySnapshot.IsSupportedPhase(UnitType,AIState) ||
                    !Command.TryGetValue(0x930,out uint tribe) || tribe!=(uint)TribeId)return false;}
                else return false;
                return TryGetValue(0x6F0,out uint global) && global==UnitGlobalId &&
                    TryGetValue(0x6EE,out uint owner) && owner==Owner &&
                    (TribeId==0 || TryGetValue(0x93C,out uint tribeStamp) && tribeStamp==TribeGlobalId);
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
        private const ushort CodecVersion=3;
        private sealed class Entry
        {
            internal WorkerResumeTicket Ticket;
            internal long Serial, Due;
            internal int Retries;
            internal bool Parked, Waiting;
        }
        private readonly Entry[] entries=new Entry[Capacity];
        private readonly Dictionary<int,int> slotByUnit=new Dictionary<int,int>();
        private long generation=1, serial;
        private int count, active, cursor, dirtyRemaining,legacyCursor,legacyCount;
        private bool draining;
        internal int Count=>count;
        internal bool CanReserveSlot=>!draining && count<Capacity;
        internal int LegacyCivilianCount=>legacyCount;
        internal static bool IsLegacyCivilian(WorkerResumeTicket ticket)=>ticket!=null && ticket.Kind==ResumeReceiptKind.Worker &&
            CivilianRemovalPolicy.IsRemovableType(ticket.UnitType);
        // Includes Waiting entries: unlike retry Drain, migration must not depend
        // on a later construction event to wake an historical arrival barrier.
        internal int DrainLegacyCivilian(int visitBudget,int attemptBudget,Func<WorkerResumeTicket,WorkerLegacyRetirement> retire)
        {
            if(draining || visitBudget<=0 || attemptBudget<=0 || legacyCount==0 || retire==null)return 0;
            int attempted=0;draining=true;
            try
            {
                for(int visited=0;visited<Math.Min(visitBudget,Capacity) && attempted<attemptBudget;visited++)
                {
                    int slot=legacyCursor;legacyCursor=(legacyCursor+1)%Capacity;var entry=entries[slot];
                    if(entry==null || !entry.Parked || !IsLegacyCivilian(entry.Ticket))continue;
                    attempted++;WorkerLegacyRetirement result;
                    try{result=retire(entry.Ticket);}catch{result=WorkerLegacyRetirement.Deferred;}
                    if(result==WorkerLegacyRetirement.Marked || result==WorkerLegacyRetirement.Released)Remove(slot);
                }
            }
            finally{draining=false;}
            return attempted;
        }

        // Lifecycle inspection only; tickets and their snapshots are immutable.
        internal WorkerResumeTicket[] CopyTickets()
        {
            var result=new WorkerResumeTicket[count];int at=0;
            foreach(var entry in entries)if(entry!=null)result[at++]=entry.Ticket;
            return result;
        }
        internal bool TryGetParkedByUnit(int unitId,out WorkerResumeTicket ticket)
        {
            ticket=null;
            if(slotByUnit.TryGetValue(unitId,out int slot) && entries[slot].Parked){ticket=entries[slot].Ticket;return true;}
            return false;
        }
        internal bool TryGetOwnedByUnit(int unitId,out WorkerResumeTicket ticket)
        {
            ticket=null;
            if(!slotByUnit.TryGetValue(unitId,out int slot))return false;
            ticket=entries[slot].Ticket;return true;
        }
        internal bool HandoffOwned(WorkerResumeTicket expected,WorkerResumeTicket replacement,long tick)
        {
            if(draining || expected==null || replacement==null || !replacement.IsValid || tick<0 ||
                expected.UnitId!=replacement.UnitId || expected.UnitGlobalId!=replacement.UnitGlobalId ||
                !slotByUnit.TryGetValue(expected.UnitId,out int slot) || !ReferenceEquals(entries[slot].Ticket,expected))return false;
            var entry=entries[slot];
            if(!entry.Parked || entry.Waiting)active++;
            entry.Parked=true;entry.Waiting=false;entry.Retries=0;entry.Due=tick;legacyCount+=(IsLegacyCivilian(replacement)?1:0)-(IsLegacyCivilian(entry.Ticket)?1:0);entry.Ticket=replacement;return true;
        }
        internal bool ReleaseOwned(WorkerResumeTicket expected)
        {
            if(draining || expected==null || !slotByUnit.TryGetValue(expected.UnitId,out int slot) || !ReferenceEquals(entries[slot].Ticket,expected))return false;
            Remove(slot);return true;
        }
        internal bool ReplaceParked(WorkerResumeTicket expected,WorkerResumeTicket replacement,long tick)
        {
            if(draining || expected==null || replacement==null || !replacement.IsValid || tick<0 ||
                expected.UnitId!=replacement.UnitId || expected.UnitGlobalId!=replacement.UnitGlobalId)return false;
            if(slotByUnit.TryGetValue(expected.UnitId,out int slot))
            {
                var entry=entries[slot];
                if(entry.Parked && ReferenceEquals(entry.Ticket,expected))
                {
                    if(entry.Waiting)active++;
                    legacyCount+=(IsLegacyCivilian(replacement)?1:0)-(IsLegacyCivilian(entry.Ticket)?1:0);entry.Ticket=replacement;entry.Retries=0;entry.Waiting=false;entry.Due=tick;return true;
                }
            }
            return false;
        }
        internal bool ReleaseParked(WorkerResumeTicket expected)
        {
            if(draining || expected==null)return false;
            if(slotByUnit.TryGetValue(expected.UnitId,out int slot) && entries[slot].Parked && ReferenceEquals(entries[slot].Ticket,expected))
            {Remove(slot);return true;}
            return false;
        }
        internal int ReservedCount { get {int n=0;foreach(var e in entries)if(e!=null && !e.Parked)n++;return n;} }
        internal bool TryReserve(WorkerResumeTicket ticket,out WorkerResumeToken token)
        {
            token=default;
            if(draining || ticket==null || !ticket.IsValid || count==Capacity || slotByUnit.ContainsKey(ticket.UnitId))return false;
            int free=-1;
            for(int i=0;i<entries.Length;i++)
            {
                if(entries[i]==null){if(free<0)free=i;}
            }
            var entry=new Entry{Ticket=ticket,Serial=++serial};entries[free]=entry;count++;
            slotByUnit.Add(ticket.UnitId,free);if(IsLegacyCivilian(ticket))legacyCount++;
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
        {var entry=entries[slot];if(entry.Parked && !entry.Waiting)active--;slotByUnit.Remove(entry.Ticket.UnitId);entries[slot]=null;count--;if(IsLegacyCivilian(entry.Ticket))legacyCount--;}
        internal void MarkWorldMutation(){if(count!=0)dirtyRemaining=Capacity;}
        internal void Reset()
        {
            if(draining)throw new InvalidOperationException("Cannot reset during a resume callback.");
            Array.Clear(entries,0,entries.Length);slotByUnit.Clear();count=active=cursor=dirtyRemaining=legacyCursor=legacyCount=0;generation++;
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
                    writer.Write((byte)t.Kind);
                    var bindings=t.Bindings;writer.Write((int)bindings.Required);writer.Write(bindings.WorkplaceId);writer.Write(bindings.WorkplaceGlobal);
                    writer.Write(bindings.StoreId);writer.Write(bindings.StoreGlobal);writer.Write(bindings.VegetationId);writer.Write(bindings.VegetationGlobal);
                    var values=t.Kind==ResumeReceiptKind.Worker?t.Job.CopyValues():t.Command.CopyValues();writer.Write(values.Length);
                    for(int i=0;i<values.Length;i++)
                    {var field=t.Kind==ResumeReceiptKind.Worker?t.Job.Rule.Field(i):t.Command.Field(i);writer.Write(field.NativeOffset);writer.Write((byte)field.Width);writer.Write(values[i]);}
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
                        int id=reader.ReadInt32();uint global=reader.ReadUInt32();int typeCode=reader.ReadInt32();
                        if(typeCode<0 || typeCode>ushort.MaxValue)return false;
                        var type=(eChimps)typeCode;int state=reader.ReadInt32();
                        ushort owner=reader.ReadUInt16();int tribe=reader.ReadInt32();uint tribeGlobal=reader.ReadUInt32();
                        ushort x=reader.ReadUInt16(),y=reader.ReadUInt16();int retries=reader.ReadInt32();byte wait=reader.ReadByte();
                        if(retries<0 || retries>MaximumRetries || wait>1 || (retries==MaximumRetries && wait==0))return false;
                        var kind=(ResumeReceiptKind)reader.ReadByte();if(kind!=ResumeReceiptKind.Worker && kind!=ResumeReceiptKind.Command)return false;
                        var required=(WorkerExternalBindings)reader.ReadInt32();int home=reader.ReadInt32();uint homeGlobal=reader.ReadUInt32();
                        int store=reader.ReadInt32();uint storeGlobal=reader.ReadUInt32();int vegetation=reader.ReadInt32();uint vegetationGlobal=reader.ReadUInt32();
                        var bindings=new WorkerBindingSnapshot(required,home,homeGlobal,store,storeGlobal,vegetation,vegetationGlobal);
                        int fields=reader.ReadInt32();if(fields<1 || fields>64)return false;
                        var offsets=new int[fields];var widths=new int[fields];var values=new uint[fields];
                        for(int i=0;i<fields;i++){offsets[i]=reader.ReadInt32();widths[i]=reader.ReadByte();values[i]=reader.ReadUInt32();}
                        WorkerResumeTicket ticket;
                        if(kind==ResumeReceiptKind.Worker)
                        {
                            if(!WorkerJourneySnapshot.TryRestoreKnownSnapshot(type,state,offsets,widths,values,out var job))return false;
                            ticket=new WorkerResumeTicket(id,global,type,state,owner,tribe,tribeGlobal,x,y,job,bindings);
                        }
                        else
                        {
                            if(!bindings.IsEmpty || !CommandJourneySnapshot.TryRestoreKnownSnapshot(type,state,offsets,widths,values,out var command))return false;
                            ticket=WorkerResumeTicket.ForCommand(id,global,type,state,owner,tribe,tribeGlobal,x,y,command);
                        }
                        if(!restored.TryReserve(ticket,out var token) || !restored.CommitPark(token,tick))return false;
                        var entry=restored.Find(token);entry.Retries=retries;
                        if(wait!=0){entry.Waiting=true;restored.active--;}
                    }
                    if(stream.Position!=stream.Length)return false;
                }
            }
            catch(IOException){return false;} catch(ArgumentException){return false;} catch(OverflowException){return false;}
            Reset();Array.Copy(restored.entries,entries,Capacity);count=restored.count;active=restored.active;legacyCount=restored.legacyCount;serial=restored.serial;
            foreach(var pair in restored.slotByUnit)slotByUnit.Add(pair.Key,pair.Value);
            // Dormant receipts survive saves; only a later real mutation arms them.
            return true;
        }
    }
}
