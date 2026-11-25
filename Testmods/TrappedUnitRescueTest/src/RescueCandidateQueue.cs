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
    internal enum RescueQueueDecision { Complete, Retry, Reject }

    internal readonly struct RescuePendingCandidate<T>
    {
        internal readonly string Source;
        internal readonly T Snapshot;
        internal readonly int Age, Attempts, RetryIn;
        internal RescuePendingCandidate(string source,T snapshot,int age,int attempts,int retryIn)
        {Source=source;Snapshot=snapshot;Age=age;Attempts=attempts;RetryIn=retryIn;}
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
        internal int DueTick { get; private set; }
        internal int Attempts { get; private set; }
        internal bool IsRestored { get; private set; }
        internal RescueQueuedCandidate(RescueIdentity identity, long mutationId, string source, T snapshot, int tick, long sequence)
        { Identity = identity; MutationId = mutationId; Source = source; Snapshot = snapshot; CapturedTick = DueTick = tick; Sequence = sequence; }
        internal void BeginAttempt() { Attempts++; }
        internal void RetryAt(int tick) { DueTick = tick; }
        internal void RestoreAttempts(int attempts) { Attempts=attempts; }
        internal void MarkRestored() { IsRestored=true; }
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
        private readonly Dictionary<int, RescueQueuedCandidate<T>> byUnit = new Dictionary<int, RescueQueuedCandidate<T>>();
        private readonly int capacity, maximumAttempts, lifetimeTicks, retryTicks, maximumUnitId;
        private long session, world, epoch, sequence;
        private bool draining;
        internal int Count => byUnit.Count;
        internal int PendingCount => pending.Count;
        internal bool HasWork => byUnit.Count != 0;
        internal bool ContainsUnit(int unitId)=>byUnit.ContainsKey(unitId);
        internal bool TryTakeUnit(int unitId,out RescueQueuedCandidate<T> candidate)
        {
            if(!byUnit.TryGetValue(unitId,out candidate))return false;
            Remove(candidate);return true;
        }
        internal long OverflowCount { get; private set; }
        internal long ConflictCount { get; private set; }

        // The default ID limit is the audited 10000-slot native allocation minus reserved slot0.
        internal RescueCandidateQueue(int capacity = 512, int maximumAttempts = 4, int lifetimeTicks = 80, int retryTicks = 1, int maximumUnitId = 9999)
        {
            if (capacity < 1 || maximumAttempts < 1 || lifetimeTicks < 1 || retryTicks < 1 || retryTicks > lifetimeTicks || maximumUnitId < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity; this.maximumAttempts = maximumAttempts; this.lifetimeTicks = lifetimeTicks; this.retryTicks = retryTicks; this.maximumUnitId = maximumUnitId;
        }

        // A new native world must call Reset even when the mission SessionId was retained.
        internal void Reset(long sessionId, long worldGeneration)
        {
            pending.Clear(); byUnit.Clear(); session = sessionId; world = worldGeneration; epoch++;
        }

        // Read-only save capture under the caller's engine monitor. Expired proofs are
        // omitted without mutating the live queue. No process-local identities are saved.
        internal bool TryCopyPending(int tick,out RescuePendingCandidate<T>[] records)
        {
            records=null;if(draining)return false;
            var result=new List<RescuePendingCandidate<T>>(byUnit.Count);
            foreach(var candidate in pending)
            {
                if(!TryForward(candidate.CapturedTick,tick,out int age) || age>=lifetimeTicks || candidate.Attempts>=maximumAttempts)continue;
                int delay=Reached(candidate.DueTick,tick)?0:unchecked(candidate.DueTick-tick);
                if(delay<0 || delay>retryTicks)return false;
                result.Add(new RescuePendingCandidate<T>(candidate.Source,candidate.Snapshot,age,candidate.Attempts,delay));
            }
            records=result.ToArray();return true;
        }

        internal bool OfferRestored(RescueIdentity identity,long mutationId,RescuePendingCandidate<T> record,int tick)
        {
            if(record.Age<0 || record.Age>=lifetimeTicks || record.Attempts<0 || record.Attempts>=maximumAttempts ||
                record.RetryIn<0 || record.RetryIn>retryTicks)return false;
            if(Offer(identity,mutationId,record.Source,record.Snapshot,unchecked(tick-record.Age))!=RescueOfferResult.Added)return false;
            var candidate=byUnit[identity.UnitId];candidate.RestoreAttempts(record.Attempts);candidate.MarkRestored();
            candidate.RetryAt(unchecked(tick+record.RetryIn));return true;
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
                    pending.RemoveFirst(); candidate.Node = null; visited++;
                    if (!IsCurrent(candidate)) continue;
                    if (!TryForward(candidate.CapturedTick, tick, out int age) || age >= lifetimeTicks || candidate.Attempts >= maximumAttempts)
                    { Remove(candidate); expired++; continue; }
                    if (!Reached(candidate.DueTick, tick)) { AddPending(candidate); continue; }
                    candidate.BeginAttempt(); attempted++;
                    RescueQueueDecision decision;
                    try { decision = evaluate(candidate); }
                    catch { decision = RescueQueueDecision.Reject; failed++; }
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
                if (byUnit.Count == 0) pending.Clear();
            }
            return new RescueQueueDrainResult(visited, attempted, complete, rejected, expired, retried, failed);
        }

        private bool IsCurrent(RescueQueuedCandidate<T> candidate) =>
            byUnit.TryGetValue(candidate.Identity.UnitId, out var current) && ReferenceEquals(current, candidate);
        private void Remove(RescueQueuedCandidate<T> candidate)
        {
            if (!IsCurrent(candidate)) return;
            byUnit.Remove(candidate.Identity.UnitId);
            if (candidate.Node != null) { pending.Remove(candidate.Node); candidate.Node = null; }
        }
        private void AddPending(RescueQueuedCandidate<T> candidate) { candidate.Node = pending.AddLast(candidate); }
        private static bool Reached(int due, int tick) => unchecked(tick - due) >= 0;
        private static bool TryForward(int from, int to, out int elapsed)
        { uint distance = unchecked((uint)(to - from)); elapsed = distance <= int.MaxValue ? (int)distance : 0; return distance <= int.MaxValue; }
    }
}
