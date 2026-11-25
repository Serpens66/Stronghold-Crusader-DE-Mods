using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest
{
    internal enum IdleEscapeState { Rejected, InProgress, Completed, Superseded, Expired }
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
        private sealed class Receipt { internal IdleEscapeTicket Ticket;internal uint Started; }
        private readonly IIdleEscapeWorld world;
        private readonly int capacity,perTick;
        private readonly uint deadline;
        private readonly Dictionary<int,Receipt> receipts=new Dictionary<int,Receipt>();
        private readonly Queue<Receipt> schedule=new Queue<Receipt>();
        private bool executing;
        internal int Count=>receipts.Count;
        internal IdleEscapeState LastState {get;private set;}=IdleEscapeState.Rejected;
        internal IdleEscapeOwner(IIdleEscapeWorld world,int capacity=64,int perTick=8,uint deadlineTicks=600)
        {
            if(capacity<1 || perTick<1 || deadlineTicks<1 || deadlineTicks>=0x80000000u)throw new ArgumentOutOfRangeException();
            this.world=world??throw new ArgumentNullException(nameof(world));this.capacity=capacity;this.perTick=perTick;deadline=deadlineTicks;
        }
        internal bool Owns(IdleEscapeTicket ticket)=>ticket!=null && receipts.TryGetValue(ticket.UnitId,out var receipt) && ReferenceEquals(receipt.Ticket,ticket);
        internal bool Contains(int unitId)=>receipts.ContainsKey(unitId);
        internal void Release(int unitId){receipts.Remove(unitId);}
        internal void Clear(){receipts.Clear();schedule.Clear();}
        internal bool TryStart(IdleEscapeRequest request,uint tick,out string reason)
        {
            LastState=IdleEscapeState.Rejected;reason="idle-escape-rejected";
            if(executing || request?.Before==null)return false;
            if(receipts.ContainsKey(request.Before.Id)){reason="idle-escape-already-owned";return false;}
            if(receipts.Count>=capacity){reason="idle-escape-capacity";return false;}
            if(!world.TryCapture(request,out var ticket,out reason) || ticket==null || !ReferenceEquals(ticket.Request,request))return false;
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
                    if(state==IdleEscapeInspection.Arrived){Release(ticket.UnitId);LastState=IdleEscapeState.Completed;reason="idle-escape-arrived";return true;}
                    if(state==IdleEscapeInspection.Waiting){schedule.Enqueue(receipt);LastState=IdleEscapeState.InProgress;reason="idle-escape-native-route-in-progress";return true;}
                }
                else if(move==IdleEscapeMove.NativeRefused && world.CanFallback(ticket) && Owns(ticket) &&
                    world.RelocateAndNormalize(ticket) && Owns(ticket) && world.Inspect(ticket)==IdleEscapeInspection.Arrived)
                {Release(ticket.UnitId);LastState=IdleEscapeState.Completed;reason="idle-escape-native-refusal-local-arrival";return true;}
                Release(ticket.UnitId);reason="idle-escape-not-proven";return false;
            }
            catch {Release(ticket.UnitId);reason="idle-escape-call-failed";return false;}
            finally {executing=false;}
        }
        internal int Advance(uint tick)
        {
            if(executing)return 0;
            int processed=0,available=Math.Min(perTick,schedule.Count);
            while(processed<available)
            {
                var receipt=schedule.Dequeue();var ticket=receipt.Ticket;
                if(!Owns(ticket))continue;
                processed++;
                if(unchecked(tick-receipt.Started)>=deadline)
                {Release(ticket.UnitId);LastState=IdleEscapeState.Expired;continue;}
                IdleEscapeInspection state;
                try {state=world.Inspect(ticket);}catch{state=IdleEscapeInspection.Changed;}
                if(state==IdleEscapeInspection.Waiting)schedule.Enqueue(receipt);
                else {Release(ticket.UnitId);LastState=state==IdleEscapeInspection.Arrived?IdleEscapeState.Completed:IdleEscapeState.Superseded;}
            }
            return processed;
        }
    }
}
