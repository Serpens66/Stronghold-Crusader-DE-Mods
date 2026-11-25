using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest.Tests
{
    internal static class CandidateQueueFixture
    {
        private static int checks;
        private static RescueIdentity Identity(int id, uint global = 1, long order = 1, long session = 7, long world = 9) =>
            new RescueIdentity(session, world, id, global, order);
        internal static void Run()
        {
            checks = 0;
            var queue = new RescueCandidateQueue<string>(2, 2, 8, 2);
            Require(queue.Offer(Identity(1), 1, "bridge", "old", 10) == RescueOfferResult.Inactive, "inactive capture");
            queue.Reset(7, 9);
            Require(queue.Offer(Identity(0), 1, "bridge", "old", 10) == RescueOfferResult.InvalidIdentity, "reserved slot");
            Require(queue.Offer(Identity(10000), 1, "bridge", "old", 10) == RescueOfferResult.InvalidIdentity, "native upper bound");
            Require(queue.Offer(Identity(1, 0), 1, "bridge", "old", 10) == RescueOfferResult.InvalidIdentity, "missing global identity");
            Require(queue.Offer(Identity(1), 1, "bridge", "old", 10) == RescueOfferResult.Added, "initial capture");
            Require(queue.Offer(Identity(1), 1, "bridge", "replacement", 11) == RescueOfferResult.Duplicate, "duplicate invocation");
            string proof = null;
            var completed = queue.Drain(11, 10, 10, item => { proof = item.Snapshot; return RescueQueueDecision.Complete; });
            Require(proof == "old" && completed.Completed == 1 && queue.Count == 0 && queue.PendingCount == 0, "dedup preserves original historical proof");

            queue.Offer(Identity(1), 1, "bridge", "first geometry", 10);
            Require(queue.Offer(Identity(1), 2, "wall", "second geometry", 10) == RescueOfferResult.ConflictingMutation,
                "a second mutation is not a stronger first proof");
            Require(queue.Count == 0 && queue.PendingCount == 0 && queue.ConflictCount == 1, "conflicting proof retired immediately");
            queue.Offer(Identity(1), 3, "bridge", "slot A", 12);
            queue.Offer(Identity(1, 2), 4, "wall", "slot B", 12);
            queue.Drain(12, 3, 3, item => { proof = item.Snapshot; return RescueQueueDecision.Complete; });
            Require(proof == "slot B", "reused slot cannot execute old global identity");

            queue.Offer(Identity(1, 2, 3), 5, "bridge", "new order", 13);
            Require(queue.Offer(Identity(1, 2, 2), 6, "wall", "old order", 13) == RescueOfferResult.InvalidIdentity, "late old order does not evict newer order");
            Require(!queue.InvalidateOrder(1, 2, 3), "same order remains current");
            Require(queue.InvalidateOrder(1, 2, 4) && queue.Count == 0 && queue.PendingCount == 0, "new command retires pending proof");

            queue.Offer(Identity(8), 7, "bridge", "eight", 20);
            queue.Offer(Identity(3), 8, "wall", "three", 20);
            Require(queue.Offer(Identity(2), 9, "wall", "overflow", 20) == RescueOfferResult.CapacityExceeded && queue.OverflowCount == 1, "visible overload");
            var visited = new List<int>();
            var one = queue.Drain(20, 100, 1, item => { visited.Add(item.Identity.UnitId); return RescueQueueDecision.Complete; });
            Require(one.Attempted == 1 && queue.Count == 1, "attempt budget");
            queue.Drain(20, 1, 100, item => { visited.Add(item.Identity.UnitId); return RescueQueueDecision.Complete; });
            Require(visited.Count == 2 && visited[0] == 8 && visited[1] == 3, "stable FIFO across budgets");
            int emptyCalls = 0;
            var empty = queue.Drain(20, 100, 100, item => { emptyCalls++; return RescueQueueDecision.Complete; });
            Require(empty.Visited == 0 && emptyCalls == 0, "empty queue performs no lookup/evaluation");

            queue.Offer(Identity(1), 10, "bridge", "retry", 30);
            var retry = queue.Drain(30, 100, 100, item => RescueQueueDecision.Retry);
            Require(retry.Attempted == 1 && retry.Retried == 1 && queue.Count == 1, "retry executes at most once per drain");
            var notDue = queue.Drain(31, 100, 100, item => throw new Exception("must not run"));
            Require(notDue.Attempted == 0 && queue.Count == 1, "retry cadence uses native ticks");
            var exhausted = queue.Drain(32, 100, 100, item => RescueQueueDecision.Retry);
            Require(exhausted.Attempted == 1 && exhausted.Expired == 1 && !queue.HasWork, "maximum attempts terminates retries");

            queue.Offer(Identity(1), 11, "bridge", "expiry", 40);
            var expired = queue.Drain(48, 10, 10, item => throw new Exception("expired must not run"));
            Require(expired.Expired == 1 && expired.Attempted == 0, "proof expires before evaluator");
            queue.Offer(Identity(1), 12, "bridge", "clock backwards", 40);
            Require(queue.Drain(39, 10, 10, item => RescueQueueDecision.Complete).Expired == 1, "backwards/ambiguous native time rejects proof");
            queue.Offer(Identity(1), 13, "bridge", "wrap", int.MaxValue);
            Require(queue.Drain(int.MinValue, 10, 10, item => RescueQueueDecision.Retry).Retried == 1, "signed tick wrap is forward time");
            Require(queue.Drain(unchecked(int.MinValue + 1), 10, 10, item => RescueQueueDecision.Complete).Attempted == 0, "wrapped retry not early");
            Require(queue.Drain(unchecked(int.MinValue + 2), 10, 10, item => RescueQueueDecision.Complete).Completed == 1, "wrapped retry due");

            queue.Offer(Identity(1), 14, "bridge", "old world", 50);
            queue.Reset(7, 10);
            Require(!queue.HasWork && queue.Offer(Identity(2), 15, "wall", "stale world", 50) == RescueOfferResult.Inactive,
                "same-session native reload invalidates old world");
            queue.Reset(7, 9);
            queue.Offer(Identity(1), 16, "bridge", "callback reset", 50);
            queue.Offer(Identity(2), 16, "bridge", "same batch", 50);
            int callbacks = 0;
            queue.Drain(50, 10, 10, item => { callbacks++; queue.Reset(8, 11); return RescueQueueDecision.Retry; });
            Require(callbacks == 1 && queue.Count == 0 && queue.PendingCount == 0, "world retirement during callback cannot resurrect queue");

            queue.Reset(7, 9);
            queue.Offer(Identity(1), 17, "bridge", "nested drain", 60);
            queue.Drain(60, 10, 10, item => {
                Require(queue.Drain(60, 10, 10, nested => throw new Exception("reentrant")).Attempted == 0, "no reentrant evaluator");
                queue.InvalidateUnit(1); return RescueQueueDecision.Retry;
            });
            Require(queue.Count == 0 && queue.PendingCount == 0, "self-invalidated callback cannot retry");
            queue.Offer(Identity(1), 18, "bridge", "failure", 61);
            queue.Offer(Identity(2), 18, "bridge", "healthy", 61);
            var failure = queue.Drain(61, 10, 10, item => item.Identity.UnitId == 1 ? throw new InvalidOperationException("isolated") : RescueQueueDecision.Complete);
            Require(failure.Failed == 1 && failure.Rejected == 1 && failure.Completed == 1, "one evaluator failure does not skip healthy candidate");

            queue.Offer(Identity(1), 19, "bridge", "callback offers", 62);
            int offers = 0;
            queue.Drain(62, 10, 10, item => { offers++; queue.Offer(Identity(2), 19, "bridge", "next pass", 62); return RescueQueueDecision.Complete; });
            Require(offers == 1 && queue.Count == 1, "callback-created work waits until next drain");
            queue.Reset(7, 9);
            for (int i = 0; i < 10000; i++)
            {
                queue.Offer(Identity(1, (uint)(i + 1)), i + 1, "replace", "bounded", 63);
                Require(queue.Count == 1 && queue.PendingCount == 1, "slot replacement retains no FIFO tombstones");
            }
            Require(queue.InvalidateUnit(1) && queue.Count == 0 && queue.PendingCount == 0, "explicit death/invalidation releases proof");
            Console.WriteLine("PASS: candidate queue checks=" + checks + "; production pure queue, no native calls.");
        }
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
