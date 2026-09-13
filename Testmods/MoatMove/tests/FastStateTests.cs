using System;
using System.IO;
using System.Linq;

namespace MoatMove
{
    public static class FastStateTests
    {
        private static int assertions;
        private static void Check(bool success, string reason)
        { assertions++; if (!success) throw new Exception("Fast state: " + reason); }
        public static void Run()
        {
            var queue = new FastCommandQueue();
            var members = Enumerable.Range(1, 680).Select(i => new FastUnitIdentity(i, (uint)(10000 + i))).ToArray();
            var first = queue.Enqueue(1, 1, 300, 12, 13, 0, -255, 17, members);
            var second = queue.Enqueue(1, 2, 301, 14, 15, 0, 1, 18, members.Take(100));
            Check(first.Members.Count == 580 && second.Members.Count == 100, "partial replacement loses remainder");
            queue.Supersede(new[] { new FastUnitIdentity(101, 999) });
            Check(first.Members.Count == 580, "reused ID cancels previous identity");
            queue.Supersede(members.Take(100));
            Check(queue.Commands.Count == 1 && queue.Commands[0] == first, "stop crosses cohort boundary");
            byte[] saved = queue.Save();
            var restored = new FastCommandQueue(); restored.Load(saved);
            Check(saved.SequenceEqual(restored.Save()), "save roundtrip changes order/flags/identity");
            Check(restored.Commands[0].MoveFlags == -255, "signed move flags narrowed");
            byte[] broken = saved.Concat(new byte[] { 4 }).ToArray();
            bool rejected = false;
            try { restored.Load(broken); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && saved.SequenceEqual(restored.Save()), "invalid save partially replaces live state");
            restored.Clear(); byte[] empty = restored.Save(); queue.Load(empty);
            Check(queue.Commands.Count == 0 && empty.Length > 0, "empty save resurrects previous commands");

            first = queue.Enqueue(1, 1, 300, 12, 13, 0, -255, 17, members);
            queue.AppendWaypoint(1, 1, 300, 20, 21, 2, -1, 18, members);
            Check(queue.Commands.Count == 2 && first.Members.Count == 680, "Shift replaces pending first move");
            var waypoint = queue.Commands[1];
            Check(queue.HasPredecessor(waypoint.Members, waypoint.Sequence), "waypoint overtakes first move");
            restored.Load(queue.Save());
            Check(queue.Save().SequenceEqual(restored.Save()) && restored.Commands[1].IsWaypoint,
                "pending waypoint not restored exactly");
            queue.AppendWaypoint(1, 1, 300, 22, 23, 2, -1, 19, members.Take(100));
            Check(waypoint.Members.Count == 580 && queue.Commands.Count == 3,
                "saturated waypoint replacement loses unrelated members");
            queue.Remove(first);
            Check(!queue.HasPredecessor(waypoint.Members, waypoint.Sequence), "completed first move blocks waypoint");
            queue.Supersede(members.Take(100));
            Check(queue.Commands.Count == 1 && queue.Commands[0].Members.Count == 580,
                "Stop fails to cancel only the selected pending waypoints");
            queue.Clear();

            bool allow = true;
            bool Edge(int a, int b, int d, out bool wet, out bool structure)
            { wet = structure = false; return allow; }
            var pool = new FastRoutePool(20, 20, 2 * 20 * 20 * 9, _ => Edge);
            using (var a = pool.Acquire(new FastFieldKey(1, 0, true)))
            using (var b = pool.Acquire(new FastFieldKey(1, 1, false)))
            {
                Check(pool.Acquire(new FastFieldKey(1, 2, true)) == null, "pool evicts pending frontier");
                a.Field.Advance(399, int.MaxValue);
                using (var shared = pool.Acquire(new FastFieldKey(1, 0, true)))
                    Check(ReferenceEquals(a.Field, shared.Field), "same destination rebuilds field");
                pool.Invalidate(2, new[] { 0 });
                Check(a.Field.Status(399) == FastRouteStatus.Found, "foreign player invalidates field");
                allow = false; pool.Invalidate(1, new[] { 0 });
                Check(a.Field.Status(399) == FastRouteStatus.Pending, "changed topology retains stale proof");
                Check(a.Field.Advance(399, int.MaxValue) == FastRouteStatus.NoRoute, "reset frontier has old parents");
            }
            Check(pool.BufferBytes <= 7200, "pool exceeds buffer budget");
            using (var isolated = pool.Acquire(new FastFieldKey(1, 0, true)))
            {
                Check(isolated.Field.Advance(399, int.MaxValue) == FastRouteStatus.NoRoute, "isolated ground result");
                pool.Invalidate(1, new[] { 399 });
                Check(isolated.Field.Status(399) == FastRouteStatus.NoRoute, "unrelated local topology discards proof");
                pool.Invalidate(1, new[] { 1 });
                Check(isolated.Field.Status(399) == FastRouteStatus.Pending, "blocked frontier dependency omitted");
            }

            bool open = true;
            var cache = new FastTraversalCache(4, 4,
                (int a, int b, int d, bool ground, out bool wet, out bool structure) =>
                { wet = !ground; structure = false; return open; });
            Check(cache.Edge(0, 1, 2, false, out _, out _), "initial edge");
            cache.Mark(0); Check(cache.Refresh().Count == 0 && cache.Revision == 0, "unchanged notification invalidates field");
            open = false; cache.Mark(0);
            Check(cache.Refresh().Count == 1 && cache.Revision == 1, "real change not observed");
            Check(!cache.Edge(0, 1, 2, false, out _, out _), "stale edge survives change");

            allow = true;
            var field = new FastRouteField(20, 20, Edge); field.Reset(399);
            Check(field.Advance(0, int.MaxValue) == FastRouteStatus.Found, "packed test route");
            field.GetPath(0, out int[] path); var packed = new byte[(path.Length - 1 + 1) / 2];
            Check(field.WritePacked(0, packed, out int count) == FastRouteStatus.Found && count == path.Length - 1, "packed count");
            int position = 0; int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
            for (int i = 0; i < count; i++)
            {
                int direction = (packed[i / 2] >> ((i % 2) * 4)) & 15;
                position += dx[direction] + dy[direction] * 20;
                Check(position == path[i + 1], "packed nibble/endpoints");
            }
            field.ResetRoots(new[] { 0, 399 }); field.Advance(20, int.MaxValue);
            Check(field.Distance(20) == 1, "multi-root distances");

            var random = new Random(86143);
            var expectedOwners = new System.Collections.Generic.Dictionary<FastUnitIdentity, int>();
            queue.Clear();
            for (int operation = 0; operation < 1000; operation++)
            {
                var cohort = members.Where(_ => random.Next(17) == 0).ToArray();
                if (operation % 4 == 0)
                {
                    queue.Supersede(cohort);
                    foreach (var member in cohort) expectedOwners.Remove(member);
                }
                else
                {
                    int target = random.Next(800);
                    queue.Enqueue(1, 1, 300, target, 10, 0, -127, operation, cohort);
                    foreach (var member in cohort) expectedOwners[member] = target;
                }
                var actualOwners = queue.Commands.SelectMany(command => command.Members.Select(member => (member, command.X)))
                    .ToDictionary(pair => pair.member, pair => pair.X);
                Check(actualOwners.Count == expectedOwners.Count && expectedOwners.All(pair => actualOwners[pair.Key] == pair.Value),
                    "random overlapping moves/Stop lost a member or resurrected an old order");
                if (operation % 23 == 0)
                { byte[] roundtrip = queue.Save(); restored.Load(roundtrip); Check(roundtrip.SequenceEqual(restored.Save()), "random save ordering"); }
            }
            Console.WriteLine("PASS: new Fast ownership/pool/topology/packed/save assertions=" + assertions);
        }
    }
}
