using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest
{
    internal readonly struct RescueIdentity : IEquatable<RescueIdentity>
    {
        internal readonly long SessionId, WorldGeneration, OrderSerial;
        internal readonly int UnitId;
        internal readonly uint GlobalId;
        internal RescueIdentity(long sessionId, long worldGeneration, int unitId, uint globalId, long orderSerial)
        { SessionId = sessionId; WorldGeneration = worldGeneration; UnitId = unitId; GlobalId = globalId; OrderSerial = orderSerial; }
        public bool Equals(RescueIdentity other) => SessionId == other.SessionId && WorldGeneration == other.WorldGeneration &&
            UnitId == other.UnitId && GlobalId == other.GlobalId && OrderSerial == other.OrderSerial;
        public override bool Equals(object other) => other is RescueIdentity value && Equals(value);
        public override int GetHashCode() => UnitId ^ (int)GlobalId ^ SessionId.GetHashCode() ^ WorldGeneration.GetHashCode() ^ OrderSerial.GetHashCode();
    }

    internal enum RescueOfferResult { Added, Duplicate, ConflictingMutation, CapacityExceeded, Inactive, InvalidIdentity }
    internal enum RescueQueueDecision { Complete, Retry, Reject, Observe, AwaitResources }
    internal enum RescueQueueLifetimeMode { AbsoluteSourceAge, ActiveWindow }

    internal readonly struct RescuePendingCandidate<T>
    {
        internal readonly string Source;
        internal readonly T Snapshot;
        // Age is always the original source age, never a refreshed admission time.
        internal readonly int Age, Attempts, RetryIn;
        internal readonly RescueQueueLifetimeMode LifetimeMode;
        internal readonly int ActiveAge; // -1: no admitted non-resource-wait evaluation yet.
        internal readonly bool AwaitingResources;
        internal RescuePendingCandidate(string source,T snapshot,int age,int attempts,int retryIn)
            :this(source,snapshot,age,attempts,retryIn,RescueQueueLifetimeMode.AbsoluteSourceAge,-1,false){}
        internal RescuePendingCandidate(string source,T snapshot,int age,int attempts,int retryIn,
            RescueQueueLifetimeMode lifetimeMode,int activeAge,bool awaitingResources)
        {Source=source;Snapshot=snapshot;Age=age;Attempts=attempts;RetryIn=retryIn;
            LifetimeMode=lifetimeMode;ActiveAge=activeAge;AwaitingResources=awaitingResources;}
    }

    internal sealed class RescueQueuedCandidate<T>
    {
        internal readonly RescueIdentity Identity;
        internal readonly long MutationId;
        internal readonly string Source;
        internal readonly T Snapshot;
        internal readonly int CapturedTick;
        internal readonly long Sequence;
        internal LinkedListNode<RescueQueuedCandidate<T>> Node;
        internal bool Preferred;
        // SourceCapturedTick never changes. ActiveAge is accumulated native time
        // since admitted non-Await evaluation, excluding declared resource waits.
        // DueTick only schedules the next bounded check, not a new source lifetime.
        internal int SourceCapturedTick=>CapturedTick;
        internal int ActiveAge { get; private set; }=-1;
        internal bool AwaitingResources { get; private set; }
        private int activeUpdatedTick;
        internal int DueTick { get; private set; }
        internal int Attempts { get; private set; }
        internal bool IsRestored { get; private set; }
        internal RescueQueuedCandidate(RescueIdentity identity, long mutationId, string source, T snapshot, int tick, long sequence)
        { Identity = identity; MutationId = mutationId; Source = source; Snapshot = snapshot; CapturedTick = DueTick = activeUpdatedTick = tick; Sequence = sequence; }
        internal void BeginAttempt() { Attempts++; }
        internal void CancelObservationAttempt() { Attempts--; }
        internal void RetryAt(int tick) { DueTick = tick; }
        internal void RestoreAttempts(int attempts) { Attempts=attempts; }
        internal void MarkRestored() { IsRestored=true; }
        internal bool TryActiveAge(int tick,out int age)
        {
            age=ActiveAge;
            uint elapsed=unchecked((uint)(tick-activeUpdatedTick));
            if(elapsed>int.MaxValue)return false;
            if(ActiveAge<0 || AwaitingResources)return true;
            if(ActiveAge>int.MaxValue-(int)elapsed)return false;
            age=ActiveAge+(int)elapsed;return true;
        }
        internal void BeginEvaluation(int tick,int activeAge)
        {ActiveAge=activeAge<0?0:activeAge;activeUpdatedTick=tick;AwaitingResources=false;}
        internal void AwaitResources(int tick,bool hadActiveWindow)
        {if(!hadActiveWindow)ActiveAge=-1;activeUpdatedTick=tick;AwaitingResources=true;}
        internal void RestoreActiveWindow(int age,bool awaiting,int tick)
        {ActiveAge=age;AwaitingResources=awaiting;activeUpdatedTick=tick;}
    }

    internal readonly struct RescueQueueDrainResult
    {
        internal readonly int Visited, Attempted, Completed, Rejected, Expired, Retried, Failed;
        internal RescueQueueDrainResult(int visited, int attempted, int completed, int rejected, int expired, int retried, int failed)
        { Visited = visited; Attempted = attempted; Completed = completed; Rejected = rejected; Expired = expired; Retried = retried; Failed = failed; }
    }

    // Simulation-owned queue; callers serialize access in their admitted native context.
    // It stores immutable identity/proof references, never native pointers. No timer or full-unit scan.
    internal sealed class RescueCandidateQueue<T>
    {
        private readonly LinkedList<RescueQueuedCandidate<T>> pending = new LinkedList<RescueQueuedCandidate<T>>();
        private LinkedListNode<RescueQueuedCandidate<T>> preferredTail;
        private readonly Dictionary<int, RescueQueuedCandidate<T>> byUnit = new Dictionary<int, RescueQueuedCandidate<T>>();
        private readonly int capacity, maximumAttempts, lifetimeTicks, retryTicks, maximumUnitId;
        private readonly RescueQueueLifetimeMode lifetimeMode;
        private long session, world, epoch, sequence;
        private bool draining;
        internal int Count => byUnit.Count;
        internal int PendingCount => pending.Count;
        internal bool HasWork => byUnit.Count != 0;
        internal bool ContainsUnit(int unitId)=>byUnit.ContainsKey(unitId);
        internal bool TryGetSnapshot(int unitId,out T snapshot)
        {snapshot=default;if(!byUnit.TryGetValue(unitId,out var candidate))return false;snapshot=candidate.Snapshot;return true;}
        internal bool TryTakeUnit(int unitId,out RescueQueuedCandidate<T> candidate)
        {
            if(!byUnit.TryGetValue(unitId,out candidate))return false;
            Remove(candidate);return true;
        }
        internal long OverflowCount { get; private set; }
        internal long ConflictCount { get; private set; }

        // The default ID limit is the audited 10000-slot native allocation minus reserved slot0.
        internal RescueCandidateQueue(int capacity = 512, int maximumAttempts = 4, int lifetimeTicks = 80, int retryTicks = 1, int maximumUnitId = 9999, RescueQueueLifetimeMode lifetimeMode = RescueQueueLifetimeMode.AbsoluteSourceAge)
        {
            if (capacity < 1 || maximumAttempts < 1 || lifetimeTicks < 1 || retryTicks < 1 || retryTicks > lifetimeTicks || maximumUnitId < 1 ||
                lifetimeMode!=RescueQueueLifetimeMode.AbsoluteSourceAge && lifetimeMode!=RescueQueueLifetimeMode.ActiveWindow)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity; this.maximumAttempts = maximumAttempts; this.lifetimeTicks = lifetimeTicks; this.retryTicks = retryTicks; this.maximumUnitId = maximumUnitId; this.lifetimeMode=lifetimeMode;
        }

        // A new native world must call Reset even when the mission SessionId was retained.
        internal void Reset(long sessionId, long worldGeneration)
        {
            pending.Clear(); preferredTail=null; byUnit.Clear(); session = sessionId; world = worldGeneration; epoch++;
        }

        // Invalidation only, including the currently evaluated entry. This is not
        // a save capture: it grants no age/retry/attempt or persistence authority.
        internal T[] CopyOwnedSnapshots()
        {
            var snapshots=new T[byUnit.Count];int index=0;
            foreach(var candidate in byUnit.Values)snapshots[index++]=candidate.Snapshot;
            return snapshots;
        }

        // Read-only save capture under the caller's engine monitor. Expired proofs are
        // omitted without mutating the live queue. No process-local identities are saved.
        internal bool TryCopyPending(int tick,out RescuePendingCandidate<T>[] records)
        {
            records=null;if(draining)return false;
            var result=new List<RescuePendingCandidate<T>>(byUnit.Count);
            foreach(var candidate in pending)
            {
                if(!TryForward(candidate.CapturedTick,tick,out int age) || candidate.Attempts>=maximumAttempts)continue;
                int activeAge=-1;
                if(lifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge?age>=lifetimeTicks:
                    !candidate.TryActiveAge(tick,out activeAge) || activeAge>=lifetimeTicks)continue;
                int delay=Reached(candidate.DueTick,tick)?0:unchecked(candidate.DueTick-tick);
                if(delay<0 || delay>retryTicks)return false;
                result.Add(lifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge?
                    new RescuePendingCandidate<T>(candidate.Source,candidate.Snapshot,age,candidate.Attempts,delay):
                    new RescuePendingCandidate<T>(candidate.Source,candidate.Snapshot,age,candidate.Attempts,delay,lifetimeMode,activeAge,candidate.AwaitingResources));
            }
            records=result.ToArray();return true;
        }

        internal bool OfferRestored(RescueIdentity identity,long mutationId,RescuePendingCandidate<T> record,int tick,bool preferFirst=false)
        {
            if(record.Age<0 || record.Attempts<0 || record.Attempts>=maximumAttempts || record.RetryIn<0 || record.RetryIn>retryTicks)return false;
            int activeAge=-1;bool awaiting=false;
            if(record.LifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge)
            {
                if(record.Age>=lifetimeTicks)return false;
                // Old records can never gain a fresh lifetime by selecting a new queue mode.
                if(lifetimeMode==RescueQueueLifetimeMode.ActiveWindow)activeAge=record.Age;
            }
            else if(record.LifetimeMode==RescueQueueLifetimeMode.ActiveWindow && lifetimeMode==RescueQueueLifetimeMode.ActiveWindow)
            {
                if(record.ActiveAge< -1 || record.ActiveAge>=lifetimeTicks || record.ActiveAge>record.Age ||
                    record.ActiveAge<0 && record.Attempts!=0)return false;
                activeAge=record.ActiveAge;awaiting=record.AwaitingResources;
            }
            else return false;
            if(Offer(identity,mutationId,record.Source,record.Snapshot,unchecked(tick-record.Age))!=RescueOfferResult.Added)return false;
            var candidate=byUnit[identity.UnitId];candidate.RestoreAttempts(record.Attempts);candidate.MarkRestored();
            if(lifetimeMode==RescueQueueLifetimeMode.ActiveWindow)candidate.RestoreActiveWindow(activeAge,awaiting,tick);
            candidate.RetryAt(unchecked(tick+record.RetryIn));
            if(preferFirst && candidate.Node!=null)
            {
                // Priority is FIFO among already ready stationary receipts; new
                // arrivals must never push an older active window backwards.
                // Sequence still excludes every new entry from a running drain.
                UnlinkPending(candidate);candidate.Preferred=true;AddPending(candidate);
            }
            return true;
        }

        internal RescueOfferResult Offer(RescueIdentity identity, long mutationId, string source, T snapshot, int tick)
        {
            if (session == 0 || world == 0 || identity.SessionId != session || identity.WorldGeneration != world)
                return RescueOfferResult.Inactive;
            if (identity.UnitId < 1 || identity.UnitId > maximumUnitId || identity.GlobalId == 0 || mutationId <= 0 || string.IsNullOrEmpty(source))
                return RescueOfferResult.InvalidIdentity;
            if (byUnit.TryGetValue(identity.UnitId, out var previous))
            {
                if (previous.Identity.GlobalId == identity.GlobalId && identity.OrderSerial < previous.Identity.OrderSerial)
                    return RescueOfferResult.InvalidIdentity;
                if (previous.Identity.Equals(identity))
                {
                    // Restored causes belong to the save, not this process's batch counter.
                    if (previous.MutationId == mutationId && !previous.IsRestored) return RescueOfferResult.Duplicate;
                    // A second geometry change invalidates the old proof. Neither snapshot is
                    // silently promoted to a stronger combined historical reachability claim.
                    Remove(previous); ConflictCount++; return RescueOfferResult.ConflictingMutation;
                }
                Remove(previous); // Slot reuse or a newer captured order retires the old receipt.
            }
            if (byUnit.Count >= capacity) { OverflowCount++; return RescueOfferResult.CapacityExceeded; }
            if (sequence == long.MaxValue) return RescueOfferResult.InvalidIdentity;
            var candidate = new RescueQueuedCandidate<T>(identity, mutationId, source, snapshot, tick, ++sequence);
            byUnit.Add(identity.UnitId, candidate); AddPending(candidate);
            return RescueOfferResult.Added;
        }

        internal bool InvalidateUnit(int unitId)
        {
            if (!byUnit.TryGetValue(unitId, out var candidate)) return false;
            Remove(candidate); return true;
        }

        internal bool InvalidateOrder(int unitId, uint globalId, long currentOrderSerial)
        {
            if (!byUnit.TryGetValue(unitId, out var candidate) ||
                candidate.Identity.GlobalId == globalId && candidate.Identity.OrderSerial == currentOrderSerial) return false;
            Remove(candidate); return true;
        }

        internal RescueQueueDrainResult Drain(int tick, int visitBudget, int attemptBudget,
            Func<RescueQueuedCandidate<T>, RescueQueueDecision> evaluate)
        {
            if (visitBudget < 0 || attemptBudget < 0) throw new ArgumentOutOfRangeException(nameof(visitBudget));
            if (evaluate == null) throw new ArgumentNullException(nameof(evaluate));
            if (draining || byUnit.Count == 0 || visitBudget == 0 || attemptBudget == 0) return default;
            int visited = 0, attempted = 0, complete = 0, rejected = 0, expired = 0, retried = 0, failed = 0;
            int available = pending.Count;
            long observedEpoch = epoch, observedSequence = sequence;
            draining = true;
            try
            {
                // Work offered by a callback cannot reenter this pass. Every visited queue
                // node counts; cancellation removes its node immediately without tombstones.
                while (available-- > 0 && visited < visitBudget && attempted < attemptBudget && pending.Count != 0)
                {
                    var candidate = pending.First.Value;
                    if (candidate.Sequence > observedSequence) break;
                    UnlinkPending(candidate); visited++;
                    if (!IsCurrent(candidate)) continue;
                    int activeAge=-1;
                    if (!TryForward(candidate.CapturedTick, tick, out int age) || candidate.Attempts >= maximumAttempts ||
                        (lifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge?age>=lifetimeTicks:
                            !candidate.TryActiveAge(tick,out activeAge) || activeAge>=lifetimeTicks))
                    { Remove(candidate); expired++; continue; }
                    if (!Reached(candidate.DueTick, tick)) { AddPending(candidate); continue; }
                    // Priority protects the first eligible admission only. A
                    // Retry/Observe/Await then uses the ordinary fair FIFO; a
                    // resource wait must not monopolize the ready segment.
                    candidate.Preferred=false;
                    bool hadActiveWindow=candidate.ActiveAge>=0;
                    if(lifetimeMode==RescueQueueLifetimeMode.ActiveWindow)candidate.BeginEvaluation(tick,activeAge);
                    candidate.BeginAttempt(); attempted++;
                    RescueQueueDecision decision;
                    try { decision = evaluate(candidate); }
                    catch { decision = RescueQueueDecision.Reject; failed++; }
                    // Observe/Await performed only a state/resource check, not a native attempt.
                    // Await alone suspends the active window; Observe does not.
                    // Keep callback-visible attempt numbering for existing Retry users;
                    // undo it only while this exact candidate is still owned here.
                    if(decision==RescueQueueDecision.Observe || decision==RescueQueueDecision.AwaitResources)
                    {
                        attempted--;
                        if(epoch!=observedEpoch)break;
                        if(!IsCurrent(candidate))continue;
                        candidate.CancelObservationAttempt();
                        if(decision==RescueQueueDecision.AwaitResources && lifetimeMode==RescueQueueLifetimeMode.ActiveWindow)
                            candidate.AwaitResources(tick,hadActiveWindow);
                        candidate.RetryAt(unchecked(tick+retryTicks));AddPending(candidate);
                        continue;
                    }
                    // A callback may end/load the world or invalidate this very order.
                    if (epoch != observedEpoch) break;
                    if (!IsCurrent(candidate)) continue;
                    if (decision == RescueQueueDecision.Retry && candidate.Attempts < maximumAttempts)
                    {
                        candidate.RetryAt(unchecked(tick + retryTicks)); AddPending(candidate); retried++;
                    }
                    else
                    {
                        Remove(candidate);
                        if (decision == RescueQueueDecision.Complete) complete++;
                        else if (decision == RescueQueueDecision.Retry) expired++;
                        else rejected++;
                    }
                }
            }
            finally
            {
                draining = false;
                if (byUnit.Count == 0) { pending.Clear();preferredTail=null; }
            }
            return new RescueQueueDrainResult(visited, attempted, complete, rejected, expired, retried, failed);
        }

        private bool IsCurrent(RescueQueuedCandidate<T> candidate) =>
            byUnit.TryGetValue(candidate.Identity.UnitId, out var current) && ReferenceEquals(current, candidate);
        private void Remove(RescueQueuedCandidate<T> candidate)
        {
            if (!IsCurrent(candidate)) return;
            byUnit.Remove(candidate.Identity.UnitId);
            UnlinkPending(candidate);
        }
        private void UnlinkPending(RescueQueuedCandidate<T> candidate)
        {
            var node=candidate.Node;if(node==null)return;
            if(ReferenceEquals(node,preferredTail))
                preferredTail=node.Previous!=null && node.Previous.Value.Preferred?node.Previous:null;
            pending.Remove(node);candidate.Node=null;
        }
        private void AddPending(RescueQueuedCandidate<T> candidate)
        {
            if(!candidate.Preferred){candidate.Node=pending.AddLast(candidate);return;}
            candidate.Node=preferredTail==null?pending.AddFirst(candidate):pending.AddAfter(preferredTail,candidate);
            preferredTail=candidate.Node;
        }
        private static bool Reached(int due, int tick) => unchecked(tick - due) >= 0;
        private static bool TryForward(int from, int to, out int elapsed)
        { uint distance = unchecked((uint)(to - from)); elapsed = distance <= int.MaxValue ? (int)distance : 0; return distance <= int.MaxValue; }
    }
}
