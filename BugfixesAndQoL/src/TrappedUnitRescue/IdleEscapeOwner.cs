using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    internal enum IdleEscapeState { Rejected, InProgress, Completed, Superseded, Expired, AwaitResources }
    internal enum IdleEscapeInspection { Changed, Waiting, Arrived }
    internal enum IdleEscapeMove { Other, NativeRefused, NativeSucceeded }
    internal sealed class IdleEscapeRequest
    {
        internal readonly long Session, Generation, OrderSerial;
        internal readonly UnitSnapshot Before;
        internal readonly RescueCandidate Candidate;
        internal readonly bool InitialCrewAliasProof;
        internal IdleEscapeRequest(long session,long generation,long orderSerial,UnitSnapshot before,RescueCandidate candidate,bool initialCrewAliasProof)
        {Session=session;Generation=generation;OrderSerial=orderSerial;Before=before;Candidate=candidate;InitialCrewAliasProof=initialCrewAliasProof;}
    }
    internal sealed class IdleEscapeTicket
    {
        internal readonly IdleEscapeRequest Request;
        internal readonly uint[] Semantic;
        internal IdleEscapeTicket(IdleEscapeRequest request,uint[] semantic)
        {Request=request??throw new ArgumentNullException(nameof(request));Semantic=semantic==null?new uint[0]:(uint[])semantic.Clone();}
        internal int UnitId=>Request.Before.Id;
    }
    internal interface IIdleEscapeWorld
    {
        bool TryCapture(IdleEscapeRequest request,out IdleEscapeTicket ticket,out string reason);
        IdleEscapeMove Move(IdleEscapeTicket ticket,Func<bool> stillOwned);
        bool HasPublishedRoute(IdleEscapeTicket ticket);
        bool CanFallback(IdleEscapeTicket ticket);
        bool RelocateAndNormalize(IdleEscapeTicket ticket);
        IdleEscapeInspection Inspect(IdleEscapeTicket ticket);
    }
    // A new, bounded escape instruction, never a read-only route query. It owns no
    // worker arrival barrier and never restores an earlier AI state or order.
    internal sealed class IdleEscapeOwner
    {
        private sealed class Receipt { internal IdleEscapeTicket Ticket;internal uint Started;internal LinkedListNode<Receipt> Node; }
        private readonly IIdleEscapeWorld world;
        private readonly int capacity,perTick;
        private readonly uint deadline;
        private readonly Dictionary<int,Receipt> receipts=new Dictionary<int,Receipt>();
        private readonly LinkedList<Receipt> schedule=new LinkedList<Receipt>();
        private bool executing;
        private bool hasAdvanced;
        private uint lastAdvanced;
        internal bool IsExecutingNativeCall=>executing;
        internal long CompletedCount {get;private set;}
        internal int Count=>receipts.Count;
        internal IdleEscapeState LastState {get;private set;}=IdleEscapeState.Rejected;
        internal IdleEscapeOwner(IIdleEscapeWorld world,int capacity=64,int perTick=8,uint deadlineTicks=600)
        {
            if(capacity<1 || perTick<1 || deadlineTicks<1 || deadlineTicks>=0x80000000u)throw new ArgumentOutOfRangeException();
            this.world=world??throw new ArgumentNullException(nameof(world));this.capacity=capacity;this.perTick=perTick;deadline=deadlineTicks;
        }
        internal bool Owns(IdleEscapeTicket ticket)=>ticket!=null && receipts.TryGetValue(ticket.UnitId,out var receipt) && ReferenceEquals(receipt.Ticket,ticket);
        internal bool Contains(int unitId)=>receipts.ContainsKey(unitId);
        // A new proved trap may retire its own existing observation before TryStart.
        // This query itself never releases it or permits a duplicate instruction.
        internal bool HasCapacityFor(int unitId)=>!executing && (receipts.Count<capacity || receipts.ContainsKey(unitId));
        internal void Release(int unitId)
        {if(receipts.TryGetValue(unitId,out var receipt)){if(receipt.Node!=null)schedule.Remove(receipt.Node);receipts.Remove(unitId);}}
        internal void Clear(){receipts.Clear();schedule.Clear();hasAdvanced=false;}
        internal bool TryStart(IdleEscapeRequest request,uint tick,out string reason)
        {
            LastState=IdleEscapeState.Rejected;reason="idle-escape-rejected";
            if(executing || request?.Before==null)return false;
            if(receipts.ContainsKey(request.Before.Id)){reason="idle-escape-already-owned";return false;}
            if(receipts.Count>=capacity){LastState=IdleEscapeState.AwaitResources;reason="idle-escape-capacity";return false;}
            IdleEscapeTicket ticket;
            try {if(!world.TryCapture(request,out ticket,out reason) || ticket==null || !ReferenceEquals(ticket.Request,request))return false;}
            catch {reason="idle-escape-capture-failed";return false;}
            var receipt=new Receipt{Ticket=ticket,Started=tick};receipts.Add(ticket.UnitId,receipt);
            // Reservation precedes every native call, including synchronous callbacks.
            executing=true;
            try
            {
                var move=world.Move(ticket,()=>Owns(ticket));
                if(!Owns(ticket)){LastState=IdleEscapeState.Superseded;reason="idle-escape-authority-lost";return false;}
                if(move==IdleEscapeMove.NativeSucceeded && world.HasPublishedRoute(ticket))
                {
                    var state=world.Inspect(ticket);
                    if(!Owns(ticket)){LastState=IdleEscapeState.Superseded;reason="idle-escape-authority-lost";return false;}
                    if(state==IdleEscapeInspection.Arrived){Release(ticket.UnitId);CompletedCount++;LastState=IdleEscapeState.Completed;reason="idle-escape-arrived";return true;}
                    if(state==IdleEscapeInspection.Waiting){receipt.Node=schedule.AddLast(receipt);LastState=IdleEscapeState.InProgress;reason="idle-escape-native-route-in-progress";return true;}
                }
                else if(move==IdleEscapeMove.NativeRefused && world.CanFallback(ticket) && Owns(ticket) &&
                    world.RelocateAndNormalize(ticket) && Owns(ticket) && world.Inspect(ticket)==IdleEscapeInspection.Arrived && Owns(ticket))
                {Release(ticket.UnitId);CompletedCount++;LastState=IdleEscapeState.Completed;reason="idle-escape-native-refusal-local-arrival";return true;}
                Release(ticket.UnitId);reason="idle-escape-not-proven";return false;
            }
            catch {Release(ticket.UnitId);reason="idle-escape-call-failed";return false;}
            finally {executing=false;}
        }
        internal int Advance(uint tick)
        {
            if(executing || hasAdvanced && lastAdvanced==tick)return 0;
            hasAdvanced=true;lastAdvanced=tick;
            int processed=0,available=Math.Min(perTick,schedule.Count);
            while(processed<available && schedule.Count!=0)
            {
                var receipt=schedule.First.Value;schedule.RemoveFirst();receipt.Node=null;var ticket=receipt.Ticket;
                if(!Owns(ticket))continue;
                processed++;
                if(unchecked(tick-receipt.Started)>=deadline)
                {Release(ticket.UnitId);LastState=IdleEscapeState.Expired;continue;}
                IdleEscapeInspection state;
                try {state=world.Inspect(ticket);}catch{state=IdleEscapeInspection.Changed;}
                if(!Owns(ticket))continue;
                if(state==IdleEscapeInspection.Waiting)receipt.Node=schedule.AddLast(receipt);
                else {Release(ticket.UnitId);if(state==IdleEscapeInspection.Arrived)CompletedCount++;LastState=state==IdleEscapeInspection.Arrived?IdleEscapeState.Completed:IdleEscapeState.Superseded;}
            }
            return processed;
        }
    }
}
