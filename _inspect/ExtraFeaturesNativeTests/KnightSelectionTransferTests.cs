using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraFeatures
{
    // Exercises the production queue. The fake replaces only native identity/readback and
    // the asynchronous selection boundary; it does not implement the queue's state machine.
    internal static class KnightSelectionTransferTests
    {
        internal static void Run(Action<bool, string> check)
        {
            PreviousThreeCannotWakeDuringLaterTwo(check);
            FreshSelectionAndSerializedBatches(check);
            SyntheticGestureSourceEntitlement(check);
            InvalidMembersDoNotBlockSurvivors(check);
            WaitingHasPerEntryDeadline(check);
            SubmissionFailuresDoNotReplay(check);
            ReadFailuresAreIsolated(check);
            ContextAndIdleCleanup(check);
        }

        private static KnightSelectionIdentity Identity(int id, int globalId = 0,
            long session = 1, int player = 1, int type = 1) =>
            new KnightSelectionIdentity(session, player, id, globalId == 0 ? 1000 + id : globalId, type);

        private static KnightSelectionTransferQueue Queue()
        {
            var queue = new KnightSelectionTransferQueue();
            queue.SetContext(1, 1);
            return queue;
        }

        private static void PreviousThreeCannotWakeDuringLaterTwo(Action<bool, string> check)
        {
            var queue = Queue();
            var world = new World();
            KnightSelectionIdentity[] swords = { Identity(11), Identity(12), Identity(13) };
            foreach (var identity in swords)
            {
                world.Set(identity, KnightSelectionUnitState.NeedsInit);
                queue.Enqueue(identity);
            }
            queue.Advance(true, world);
            check(queue.HasWork && world.Requests.Count == 0,
                "last completed transformation keeps selection work alive while replacements need init");
            foreach (var identity in swords) world.Set(identity, KnightSelectionUnitState.Ready);
            queue.Advance(true, world);
            check(world.LastIs(11, 12, 13), "prior dismount batch selects all three ready replacements");
            world.CommitLast();
            Advance(queue, world, 3);
            check(!queue.HasWork, "acknowledged prior batch fully retires without needing another transformation");

            // Runtime has already decided source entitlement at completion. The queue receives
            // only eligible replacements, never a list of sources selected ten seconds ago.
            world.Current.Clear(); // the selected two swords have now been replaced
            var knights = new[] { Identity(21), Identity(22) };
            foreach (var identity in knights)
            {
                world.Set(identity, KnightSelectionUnitState.NeedsInit);
                queue.Enqueue(identity);
            }
            queue.Advance(true, world);
            check(world.Requests.Count == 1, "later batch cannot revive the old three-swords request");
            foreach (var identity in knights) world.Set(identity, KnightSelectionUnitState.Ready);
            queue.Advance(true, world);
            check(world.Requests.Count == 2 && world.LastIs(21, 22),
                "later two replacements are selected without the deliberately unselected third sword");
        }

        private static void FreshSelectionAndSerializedBatches(Action<bool, string> check)
        {
            var queue = Queue();
            var world = new World();
            var first = Identity(31);
            var second = Identity(32);
            world.Set(first, KnightSelectionUnitState.Ready);
            world.Set(second, KnightSelectionUnitState.NeedsInit);
            world.Current.Add(71);
            world.Current.Add(71);
            queue.Enqueue(first);
            queue.Enqueue(first);
            queue.Enqueue(second);
            queue.Advance(false, world);
            check(world.Reads == 0 && world.Requests.Count == 0,
                "command-side or otherwise ineligible advance consumes no engine phase");
            queue.Advance(true, world);
            check(world.LastIs(31, 71), "ready member publishes once without waiting for another initializing member");
            world.CommitLast();
            world.Current.Clear();
            world.Current.Add(72);
            world.Set(first, KnightSelectionUnitState.Ready); // deliberately deselected before readback
            world.Set(second, KnightSelectionUnitState.Ready);
            queue.Advance(false, world);
            queue.Advance(true, world);
            queue.Advance(true, world);
            check(world.Requests.Count == 1, "new ready batch cannot overwrite either of the first two subsequent phases");
            queue.Advance(true, world);
            check(world.Requests.Count == 2 && world.LastIs(32, 72),
                "after three subsequent engine phases next batch uses fresh selection and never cached unrelated troops");
            world.CommitLast();
            Advance(queue, world, 3);
            check(!queue.HasWork && world.Reports.Count == 1 && world.Reports[0].UnitId == 31,
                "competing selection reports an unconfirmed member once and successful later batch drains cleanly");
        }

        private static void InvalidMembersDoNotBlockSurvivors(Action<bool, string> check)
        {
            var queue = Queue();
            var world = new World();
            var dead = Identity(41);
            var reused = Identity(42);
            var survivor = Identity(43);
            world.Set(dead, KnightSelectionUnitState.Invalid);
            world.Set(Identity(42, 9999), KnightSelectionUnitState.Ready);
            world.Set(survivor, KnightSelectionUnitState.Ready);
            queue.Enqueue(dead);
            queue.Enqueue(reused);
            queue.Enqueue(survivor);
            queue.Advance(true, world);
            check(world.LastIs(43), "dead or reused slots never select another occupant or block a valid replacement");
            check(world.Reports.Count(x => x.UnitId == 41) == 1 && world.Reports.Count(x => x.UnitId == 42) == 1,
                "invalid transfer identities are individually reported once");
            world.CommitLast();
            Advance(queue, world, 3);
            check(!queue.HasWork && world.Requests.Count == 1, "invalid entries do not trigger retries after survivors commit");
        }

        private static void SyntheticGestureSourceEntitlement(Action<bool, string> check)
        {
            var queue = Queue();
            var world = new World();
            var replacement = Identity(101);
            var sourceIncluded = Identity(102);
            var sourceExcluded = Identity(103);
            world.Set(replacement, KnightSelectionUnitState.Ready);
            world.Set(sourceIncluded, KnightSelectionUnitState.Ready);
            world.Set(sourceExcluded, KnightSelectionUnitState.Ready);
            world.Current.Add(sourceIncluded.UnitId);
            queue.Enqueue(replacement);
            queue.Advance(true, world);
            check(queue.IsSourceSelected(sourceIncluded, false),
                "synthetic gesture preserves entitlement of an included source despite transient native deselection");
            check(!queue.IsSourceSelected(sourceExcluded, true),
                "synthetic same-type transient mark never grants entitlement to an excluded source");
            var changedIdentities = new[]
            {
                Identity(sourceIncluded.UnitId, sourceIncluded.GlobalId + 1),
                Identity(sourceIncluded.UnitId + 100, sourceIncluded.GlobalId),
                Identity(sourceIncluded.UnitId, sourceIncluded.GlobalId, player: 2),
                Identity(sourceIncluded.UnitId, sourceIncluded.GlobalId, type: 2),
                Identity(sourceIncluded.UnitId, sourceIncluded.GlobalId, session: 2)
            };
            check(changedIdentities.All(identity => !queue.IsSourceSelected(identity, true)),
                "gesture entitlement requires exact slot, global identity, owner, type and mission");
            Advance(queue, world, 2);
            check(queue.IsSourceSelected(sourceIncluded, false),
                "gesture entitlement remains authoritative for both intermediate engine phases");
            queue.Advance(true, world);
            check(!queue.HasWork && !queue.IsSourceSelected(sourceIncluded, false) &&
                  queue.IsSourceSelected(sourceExcluded, true),
                "after three subsequent engine phases entitlement follows current native selection again");

            world.Current.Clear();
            world.Current.Add(104);
            queue.Enqueue(sourceExcluded);
            queue.Advance(true, world);
            check(world.LastIs(103, 104),
                "temporary entitlement snapshot never adds former source or replacement to a later request");
            queue.SetContext(1, 2);
            var otherPlayerSource = Identity(105, player: 2);
            check(!queue.HasWork && queue.IsSourceSelected(otherPlayerSource, true) &&
                  !queue.IsSourceSelected(otherPlayerSource, false),
                "player context boundary restores native entitlement immediately without waiting for another engine tick");
            queue.Clear();
            check(queue.IsSourceSelected(sourceIncluded, true) && !queue.IsSourceSelected(sourceIncluded, false),
                "clear immediately removes synthetic entitlement and restores native source decisions");

            queue = Queue();
            world = new World { ThrowIdentity = true };
            world.Set(replacement, KnightSelectionUnitState.Ready);
            world.Current.Add(sourceIncluded.UnitId);
            queue.Enqueue(replacement);
            queue.Advance(true, world);
            Advance(queue, world, 4);
            check(!queue.HasWork && world.Requests.Count == 0 && world.Reports.Count == 1,
                "identity-capture failure drops the request once without setter submission or replay");
        }

        private static void WaitingHasPerEntryDeadline(Action<bool, string> check)
        {
            var queue = Queue();
            var world = new World();
            var first = Identity(51);
            var later = Identity(52);
            world.Set(first, KnightSelectionUnitState.NeedsInit);
            queue.Enqueue(first);
            Advance(queue, world, 50);
            world.Set(later, KnightSelectionUnitState.NeedsInit);
            queue.Enqueue(later);
            for (int i = 0; i < 150; i++) queue.Advance(false, world);
            Advance(queue, world, 49);
            check(world.Reports.Count == 0 && queue.HasWork, "ineligible advances never age either replacement deadline");
            queue.Advance(true, world);
            check(world.Reports.Count(x => x.UnitId == 51) == 1 &&
                  world.Reports.All(x => x.UnitId != 52) && queue.HasWork,
                "100-attempt deadline expires only its own entry and does not reset or age a later entry prematurely");
            Advance(queue, world, 49);
            check(world.Reports.All(x => x.UnitId != 52), "later entry retains its independent full initialization budget");
            queue.Advance(true, world);
            check(!queue.HasWork && world.Reports.Count(x => x.UnitId == 52) == 1,
                "each permanently initializing entry expires once after 100 eligible attempts");

            queue = Queue();
            world = new World();
            var selected = Identity(53);
            var waiting = Identity(54);
            world.Set(selected, KnightSelectionUnitState.Ready);
            world.Set(waiting, KnightSelectionUnitState.NeedsInit);
            queue.Enqueue(selected);
            queue.Enqueue(waiting);
            queue.Advance(true, world);
            Advance(queue, world, 99);
            check(world.Reports.Count(x => x.UnitId == 54) == 1 && !queue.HasWork,
                "waiting entries continue to age while a preceding gesture awaits readback");
            check(world.Reports.Count(x => x.UnitId == 53) == 1 && world.Requests.Count == 1,
                "unconfirmed gesture reports once after its bounded phases and is never replayed");
        }

        private static void SubmissionFailuresDoNotReplay(Action<bool, string> check)
        {
            foreach (bool throws in new[] { false, true })
            {
                var queue = Queue();
                var world = new World { Reject = !throws, Throw = throws };
                var identity = Identity(61);
                world.Set(identity, KnightSelectionUnitState.Ready);
                queue.Enqueue(identity);
                queue.Advance(true, world);
                Advance(queue, world, 5);
                check(!queue.HasWork && world.Requests.Count == 1 && world.Reports.Count == 1,
                    "rejected or throwing selection submission is reported once without replay");
                world.Reject = false;
                world.Throw = false;
                world.Current.Add(81);
                var next = Identity(62);
                world.Set(next, KnightSelectionUnitState.Ready);
                queue.Enqueue(next);
                queue.Advance(true, world);
                check(world.LastIs(62, 81), "submission failure leaves no stale members for the next request");
            }
        }

        private static void ContextAndIdleCleanup(Action<bool, string> check)
        {
            var queue = Queue();
            var world = new World();
            Advance(queue, world, 5);
            check(!queue.HasWork && world.Reads == 0 && world.Captures == 0,
                "idle queue never scans units or selection");
            var identity = Identity(91);
            world.Set(identity, KnightSelectionUnitState.NeedsInit);
            queue.Enqueue(identity);
            queue.SetContext(1, 1);
            check(queue.HasWork, "same context preserves current waiting work");
            queue.SetContext(2, 1);
            check(!queue.HasWork, "new mission clears former mission work");
            queue.Enqueue(identity);
            check(!queue.HasWork, "former mission identity is rejected in a new mission");
            var current = Identity(92, session: 2);
            world.Set(current, KnightSelectionUnitState.Ready);
            queue.Enqueue(current);
            queue.Advance(true, world);
            queue.SetContext(2, 2);
            check(!queue.HasWork, "editor player switch clears in-flight work");
            queue.SetContext(1, 1);
            queue.Enqueue(identity);
            queue.Clear();
            int reads = world.Reads;
            Advance(queue, world, 5);
            check(!queue.HasWork && world.Reads == reads,
                "mission end or feature disable clears all pending work and prevents later readback");
        }

        private static void ReadFailuresAreIsolated(Action<bool, string> check)
        {
            var queue = Queue();
            var world = new World();
            var broken = Identity(111);
            var healthy = Identity(112);
            world.Set(broken, KnightSelectionUnitState.Ready);
            world.Set(healthy, KnightSelectionUnitState.Ready);
            world.ThrowReads.Add(broken.UnitId);
            queue.Enqueue(broken);
            queue.Enqueue(healthy);
            queue.Advance(true, world);
            check(world.LastIs(112) && world.Reports.Count(x => x.UnitId == 111) == 1,
                "one throwing identity read is reported and removed without blocking another ready replacement");
            world.CommitLast();
            Advance(queue, world, 3);
            check(!queue.HasWork && world.Requests.Count == 1 && world.Reports.Count == 1,
                "failed waiting read never reappears while successful transfer confirms normally");

            queue = Queue();
            world = new World();
            world.Set(healthy, KnightSelectionUnitState.Ready);
            queue.Enqueue(healthy);
            queue.Advance(true, world);
            world.ThrowReads.Add(healthy.UnitId);
            Advance(queue, world, 6);
            check(!queue.HasWork && world.Requests.Count == 1 && world.Reports.Count == 1,
                "throwing gesture readback retires and reports once without replaying the submitted selection");
        }

        private static void Advance(KnightSelectionTransferQueue queue, World world, int count)
        {
            for (int i = 0; i < count; i++) queue.Advance(true, world);
        }

        private sealed class World : IKnightSelectionTransferWorld
        {
            private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
            internal readonly List<int> Current = new List<int>();
            internal readonly List<int[]> Requests = new List<int[]>();
            internal readonly List<KnightSelectionIdentity> Reports = new List<KnightSelectionIdentity>();
            internal readonly HashSet<int> ThrowReads = new HashSet<int>();
            internal int Reads, Captures;
            internal bool Reject, Throw, ThrowIdentity;

            internal void Set(KnightSelectionIdentity identity, KnightSelectionUnitState state) =>
                entries[identity.UnitId] = new Entry { Identity = identity, State = state };

            public KnightSelectionUnitState Read(KnightSelectionIdentity identity)
            {
                Reads++;
                if (ThrowReads.Contains(identity.UnitId))
                    throw new InvalidOperationException("fake identity read failure");
                if (!entries.TryGetValue(identity.UnitId, out Entry entry) ||
                    entry.Identity.SessionId != identity.SessionId || entry.Identity.PlayerId != identity.PlayerId ||
                    entry.Identity.GlobalId != identity.GlobalId || entry.Identity.UnitType != identity.UnitType)
                    return KnightSelectionUnitState.Invalid;
                return entry.State;
            }

            public void CaptureCurrentSelection(List<int> ids)
            {
                Captures++;
                ids.AddRange(Current);
            }

            public KnightSelectionIdentity CaptureIdentity(int unitId)
            {
                if (ThrowIdentity) throw new InvalidOperationException("fake identity capture failure");
                return entries.TryGetValue(unitId, out Entry entry) ? entry.Identity : Identity(unitId);
            }

            public bool TrySetSelection(List<int> ids)
            {
                Requests.Add(ids.ToArray());
                if (Throw) throw new InvalidOperationException("fake selection boundary failure");
                return !Reject;
            }

            public void Report(KnightSelectionIdentity identity, string reason) => Reports.Add(identity);

            internal bool LastIs(params int[] ids) => Requests.Count != 0 &&
                Requests[Requests.Count - 1].OrderBy(x => x).SequenceEqual(ids.OrderBy(x => x));

            internal void CommitLast()
            {
                Current.Clear();
                Current.AddRange(Requests[Requests.Count - 1]);
                foreach (int id in Current)
                    if (entries.TryGetValue(id, out Entry entry)) entry.State = KnightSelectionUnitState.Selected;
            }

            private sealed class Entry
            {
                internal KnightSelectionIdentity Identity;
                internal KnightSelectionUnitState State;
            }
        }
    }
}
