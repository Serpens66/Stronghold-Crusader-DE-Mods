using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using GroupPathProbe.Core;

// Phase 1 contracts and measurement for the MoatMove anchor core (MoatMoveAnchor.dll).
// One task per group (as one command in MoatMove), queries in group order.
internal static class AnchorCoreGate
{
    private static int comparisons;
    private const int Margin = 8, Player = 1;
    private static RouteResult[] Solve(AnchorCoreInterop core, GroupLoad load, int threads, out int[] tasks)
    {
        tasks = new int[load.Groups];
        for (int g = 0; g < load.Groups; g++)
        {
            int first = load.GroupOf.IndexOf(g); RouteQuery q = load.Queries[first];
            int[] starts = Enumerable.Range(0, load.Queries.Count).Where(i => load.GroupOf[i] == g).Select(i => load.Queries[i].Start).ToArray();
            tasks[g] = core.CreateTask(Player, load.Anchors[g], q.GroundCost, q.MoatCost, starts);
        }
        core.Step(int.MaxValue, threads);
        var results = new RouteResult[load.Queries.Count];
        for (int i = 0; i < results.Length; i++) results[i] = core.Query(tasks[load.GroupOf[i]], load.Queries[i].Start, load.Queries[i].Target, Margin);
        return results;
    }
    // Same tasks as Solve, answered with one batch per group (caller order within the group).
    private static RouteResult[] SolveBatch(AnchorCoreInterop core, GroupLoad load, int threads)
    {
        var tasks = new int[load.Groups];
        for (int g = 0; g < load.Groups; g++)
        {
            int first = load.GroupOf.IndexOf(g); RouteQuery q = load.Queries[first];
            int[] starts = Enumerable.Range(0, load.Queries.Count).Where(i => load.GroupOf[i] == g).Select(i => load.Queries[i].Start).ToArray();
            tasks[g] = core.CreateTask(Player, load.Anchors[g], q.GroundCost, q.MoatCost, starts);
        }
        core.Step(int.MaxValue, threads);
        var results = new RouteResult[load.Queries.Count];
        for (int g = 0; g < load.Groups; g++)
        {
            int[] members = Enumerable.Range(0, load.Queries.Count).Where(i => load.GroupOf[i] == g).ToArray();
            RouteResult[] answers = core.QueryBatch(tasks[g], members.Select(i => load.Queries[i].Start).ToArray(), members.Select(i => load.Queries[i].Target).ToArray(), Margin, threads);
            for (int k = 0; k < members.Length; k++) results[members[k]] = answers[k];
        }
        return results;
    }
    private static void Same(RouteResult[] a, RouteResult[] b, string name)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i].Status != b[i].Status || a[i].Reason != b[i].Reason || a[i].Cost != b[i].Cost || !a[i].Directions.SequenceEqual(b[i].Directions)) throw new InvalidOperationException(name + ": result depends on threads/mask chunking; case=" + i);
    }
    // Legal, never below the optimum, same reachability as the independent reference.
    private static void Check(GroupLoad load, RouteResult[] results)
    {
        for (int i = 0; i < results.Length; i++)
        {
            RouteQuery q = load.Queries[i]; RouteResult r = results[i];
            RouteResult reference = ReferenceSearch.Find(load.Map, q, new WorkBudget(256L * 1024 * 1024, 30)); comparisons++;
            if (r.Status == SearchStatus.Inconclusive && r.Reason == "over-2000-steps") continue;
            if (r.Status != reference.Status) throw new InvalidOperationException(load.Name + ": status mismatch case=" + i + " " + r.Status + "/" + reference.Status + " " + r.Reason);
            if (r.Status != SearchStatus.Found) continue;
            RouteResult described = load.Map.Describe(q, r.Directions);
            if (described.Status != SearchStatus.Found || described.Cost != r.Cost || described.Ground != r.Ground || described.Moat != r.Moat) throw new InvalidOperationException(load.Name + ": illegal path/cost case=" + i);
            if (r.Cost < reference.Cost) throw new InvalidOperationException(load.Name + ": below optimum case=" + i);
        }
    }
    private static GroupLoad RandomLoad(Random random, int sample)
    {
        var profiles = new[] { (1L, 7L), (2L, 3L), (5L, 5L) };
        var validity = new bool[23 * 19]; for (int i = 0; i < validity.Length; i++) validity[i] = sample == 0 || random.Next(100) < 85;
        GridSnapshot map = GroupGate.Grid(23, 19, (a, b, d) => random.Next(100) < 67 ? (1U << d) | (random.Next(100) < 25 ? 1U << (8 + d) : 0U) : 0U, n => validity[n]);
        var nodes = Enumerable.Range(0, map.Count).Where(map.IsValid).ToArray();
        var load = new GroupLoad { Name = "core-random", Map = map };
        for (int group = 0; group < 3; group++)
        {
            var (ground, moat) = profiles[(sample + group) % profiles.Length];
            int size = 1 + random.Next(group == 2 ? 90 : 20); load.Anchors.Add(-1);
            for (int i = 0; i < size; i++) { load.Queries.Add(new RouteQuery { Start = nodes[random.Next(nodes.Length)], Target = nodes[random.Next(nodes.Length)], GroundCost = ground, MoatCost = moat }); load.GroupOf.Add(group); }
            load.Anchors[group] = load.Queries[load.Queries.Count - 1].Target;
        }
        return load;
    }
    internal static void Contracts()
    {
        AnchorCoreInterop.Load();
        int recommended = AnchorCoreInterop.mma_recommended_threads();
        if (recommended < 1 || recommended > 8) throw new InvalidOperationException("Recommended thread count outside 1..8: " + recommended);
        var random = new Random(918273);
        // Random directed grids: chunked mask + 1 thread equals one-piece mask + 4 threads.
        for (int sample = 0; sample < 24; sample++)
        {
            GroupLoad load = RandomLoad(random, sample);
            RouteResult[] a, b;
            using (var core = new AnchorCoreInterop(load.Map.Width, load.Map.Height, 128L * 1024 * 1024)) { core.WriteWholeMask(Player, load.Map, 37); a = Solve(core, load, 1, out _); }
            using (var core = new AnchorCoreInterop(load.Map.Width, load.Map.Height, 128L * 1024 * 1024)) { core.WriteWholeMask(Player, load.Map, load.Map.Count); b = Solve(core, load, 4, out _); }
            Same(a, b, load.Name + "#" + sample); Check(load, a);
            // Single-unit routes are exactly optimal.
            using (var core = new AnchorCoreInterop(load.Map.Width, load.Map.Height, 128L * 1024 * 1024))
            {
                core.WriteWholeMask(Player, load.Map, load.Map.Count);
                var single = load.Queries.Select(q => core.SingleRoute(Player, q.Target, q.GroundCost, q.MoatCost, q.Start)).ToArray();
                Check(load, single);
                for (int i = 0; i < single.Length; i++)
                    if (single[i].Status == SearchStatus.Found && single[i].Cost != ReferenceSearch.Find(load.Map, load.Queries[i], new WorkBudget(256L * 1024 * 1024, 30)).Cost)
                        throw new InvalidOperationException("Single route above optimum: sample=" + sample + " case=" + i);
            }
            // Batch answers equal single queries in caller order, for one and several threads.
            foreach (int threads in new[] { 1, 4 })
            {
                RouteResult[] batch;
                using (var core = new AnchorCoreInterop(load.Map.Width, load.Map.Height, 128L * 1024 * 1024)) { core.WriteWholeMask(Player, load.Map, load.Map.Count); batch = SolveBatch(core, load, threads); }
                Same(a, batch, load.Name + "#" + sample + "-batch" + threads);
            }
        }
        // Pending until the mask is complete; stale after a later change; restart recovers.
        {
            // Wall at x=20 with a single gap 50 rows below the route; later the wall disappears.
            GridSnapshot open = GroupGate.Grid(41, 61);
            GridSnapshot walled = GroupGate.Grid(41, 61, null, n => n % 41 != 20 || n / 41 == 60);
            var load = new GroupLoad { Name = "core-lifecycle", Map = walled }; load.Anchors.Add(10 * 41 + 39);
            load.Queries.Add(new RouteQuery { Start = 10 * 41 + 1, Target = 10 * 41 + 39, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0);
            using var core = new AnchorCoreInterop(41, 61, 128L * 1024 * 1024);
            core.BeginMask(Player); core.WriteMask(Player, walled, 0, walled.Count - 1);
            int task = core.CreateTask(Player, load.Anchors[0], 1, 7, new[] { load.Queries[0].Start });
            if (core.Step(8, 2) != 0 || core.TaskStatus(task) != AnchorCoreInterop.Pending || core.Query(task, load.Queries[0].Start, load.Queries[0].Target, Margin).Reason != "pending") throw new InvalidOperationException("Incomplete mask did not keep the task pending.");
            core.WriteMask(Player, walled, walled.Count - 1, 1);
            if (core.Step(8, 2) != 1 || core.TaskStatus(task) != AnchorCoreInterop.Ready) throw new InvalidOperationException("Complete mask did not compute the task.");
            RouteResult before = core.Query(task, load.Queries[0].Start, load.Queries[0].Target, Margin); Check(load, new[] { before });
            // Open the wall far from the old route: the old answer becomes stale.
            var changedNodes = new List<int>();
            for (int n = 0; n < open.Count; n++) if (walled.Word(n) != open.Word(n)) changedNodes.Add(n);
            GridSnapshot opened = open;
            if (core.UpdateMask(Player, opened, changedNodes) == 0 || core.TaskStatus(task) != AnchorCoreInterop.Stale || core.Query(task, load.Queries[0].Start, load.Queries[0].Target, Margin).Reason != "stale") throw new InvalidOperationException("Mask change did not mark the task stale.");
            core.Restart(task); core.Step(8, 2);
            var reopened = new GroupLoad { Name = "core-lifecycle-opened", Map = opened }; reopened.Anchors.Add(load.Anchors[0]); reopened.Queries.Add(load.Queries[0]); reopened.GroupOf.Add(0);
            RouteResult after = core.Query(task, load.Queries[0].Start, load.Queries[0].Target, Margin); Check(reopened, new[] { after });
            if (after.Cost >= before.Cost) throw new InvalidOperationException("Opened shortcut was not used after restart.");
            core.Release(task);
            if (core.TaskStatus(task) != AnchorCoreInterop.Invalid) throw new InvalidOperationException("Released task still answers.");
        }
        // Far slots behind a wall: one shared sub-anchor, at most 2 above the optimum.
        {
            GridSnapshot map = GroupGate.Grid(60, 60, null, n => n % 60 != 34 || n / 60 == 59);
            var load = new GroupLoad { Name = "core-far-slots", Map = map }; load.Anchors.Add(30 * 60 + 30);
            foreach (int slot in new[] { 30 * 60 + 30, 30 * 60 + 31, 30 * 60 + 36, 30 * 60 + 37 }) { load.Queries.Add(new RouteQuery { Start = 10 * 60 + 50 + load.Queries.Count, Target = slot, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0); }
            using var core = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024); core.WriteWholeMask(Player, map, 500);
            RouteResult[] r = Solve(core, load, 2, out _); Check(load, r);
            if (core.Stats()[AnchorCoreInterop.Stat("subAnchors")] != 1) throw new InvalidOperationException("Far slots did not share one sub-anchor.");
            using (var batchCore = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024)) { batchCore.WriteWholeMask(Player, map, 500); Same(r, SolveBatch(batchCore, load, 4), "core-far-slots-batch"); if (batchCore.Stats()[AnchorCoreInterop.Stat("subAnchors")] != 1) throw new InvalidOperationException("Batch far slots did not share one sub-anchor."); }
            for (int i = 2; i < 4; i++) { var reference = ReferenceSearch.Find(map, load.Queries[i], new WorkBudget(64L * 1024 * 1024, 10)); if (r[i].Cost > reference.Cost + 2) throw new InvalidOperationException("Far-slot unit still detours; case=" + i); }
        }
        // Group split by a closed wall (AI members behind walls): the main field cannot reach the
        // right-hand starts. A sub-anchor for right-hand slots targets only right-hand starts, so
        // it stops near the slots instead of exhausting the right component; answers stay exact.
        {
            GridSnapshot map = GroupGate.Grid(60, 60, null, n => n % 60 != 30);
            var load = new GroupLoad { Name = "core-split-group", Map = map }; load.Anchors.Add(30 * 60 + 10);
            foreach (var (start, slot) in new[] { (26 * 60 + 8, 30 * 60 + 10), (26 * 60 + 9, 30 * 60 + 11), (26 * 60 + 44, 30 * 60 + 45), (26 * 60 + 45, 30 * 60 + 46), (26 * 60 + 46, 30 * 60 + 45) })
            { load.Queries.Add(new RouteQuery { Start = start, Target = slot, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0); }
            using var core = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024); core.WriteWholeMask(Player, map, 500);
            RouteResult[] r = Solve(core, load, 2, out _); Check(load, r);
            long[] stats = core.Stats();
            if (stats[AnchorCoreInterop.Stat("subAnchors")] != 1) throw new InvalidOperationException("Right-hand slots did not share one sub-anchor.");
            using (var batchCore = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024)) { batchCore.WriteWholeMask(Player, map, 500); Same(r, SolveBatch(batchCore, load, 4), "core-split-group-batch"); }
            // Main field: the whole left component (30 x 60 nodes). A sub-anchor that also targeted the
            // unreachable left-hand starts would settle the whole right component (29 x 60) as well.
            if (stats[AnchorCoreInterop.Stat("settleEvents")] >= 30 * 60 + 29 * 60) throw new InvalidOperationException("Sub-anchor exhausted the right component: settles=" + stats[AnchorCoreInterop.Stat("settleEvents")]);
        }
        // Anchor, starts and slots in three closed components (an AI group ordered into an
        // enclosure, pf-iter-3): the first slot's exhausted sub-anchor proves every start
        // unreachable for the slots it settled, so the other slots build no sub-anchor of their own.
        {
            GridSnapshot map = GroupGate.Grid(60, 60, null, n => n % 60 != 20 && n % 60 != 40);
            var load = new GroupLoad { Name = "core-unreachable-slots", Map = map }; load.Anchors.Add(30 * 60 + 10);
            for (int i = 0; i < 8; i++) { load.Queries.Add(new RouteQuery { Start = (20 + i) * 60 + 30, Target = 30 * 60 + 45 + i, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0); }
            using var core = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024); core.WriteWholeMask(Player, map, 500);
            RouteResult[] r = Solve(core, load, 2, out _); Check(load, r);
            if (r.Any(x => x.Status != SearchStatus.Unreachable)) throw new InvalidOperationException("A slot in a closed component was not answered unreachable.");
            if (core.Stats()[AnchorCoreInterop.Stat("subAnchors")] != 1) throw new InvalidOperationException("Unreachable slots built one sub-anchor each: " + core.Stats()[AnchorCoreInterop.Stat("subAnchors")]);
            using (var batchCore = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024)) { batchCore.WriteWholeMask(Player, map, 500); Same(r, SolveBatch(batchCore, load, 4), "core-unreachable-slots-batch"); }
        }
        // Ground-only profiles use the ground directions (bits 8..15, Vanilla's DA590 rules),
        // moat profiles the friendly directions (DAFD0 rules with the height correction): an
        // edge present only as ground direction (a keep or tower step) serves ground units only.
        {
            const uint West = 1U << 6, WestGround = 1U << 14;
            var words = new uint[5];
            for (int n = 1; n < 5; n++) words[n] = West | WestGround;
            words[2] = WestGround;
            using var core = new AnchorCoreInterop(5, 1, 64L * 1024 * 1024); core.BeginMask(Player); core.WriteWords(Player, 0, words);
            int groundTask = core.CreateTask(Player, 0, 1, 0, new[] { 4 }), moatTask = core.CreateTask(Player, 0, 1, 7, new[] { 4 });
            core.Step(8, 1);
            RouteResult ground = core.Query(groundTask, 4, 0, Margin), moat = core.Query(moatTask, 4, 0, Margin);
            if (ground.Status != SearchStatus.Found || ground.Cost != 4) throw new InvalidOperationException("Ground profile ignored a ground-only direction: " + ground.Status + "/" + ground.Reason);
            if (moat.Status == SearchStatus.Found) throw new InvalidOperationException("Moat profile used a ground-only direction.");
        }
        // Field cache: a second task with the same player, anchor and profile whose starts the first
        // field settled reuses that field and answers exactly like a fresh core; a mask change or
        // an unsettled start computes a new field.
        {
            GridSnapshot map = GroupGate.Grid(60, 60, null, n => n % 60 != 30 || n / 60 == 50);
            int anchorNode = 10 * 60 + 10, near = 12 * 60 + 15, far = 55 * 60 + 50;
            using var core = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024); core.WriteWholeMask(Player, map, 500);
            int first = core.CreateTask(Player, anchorNode, 1, 7, new[] { near, far }); core.Step(8, 1);
            int second = core.CreateTask(Player, anchorNode, 1, 7, new[] { near }); core.Step(8, 1);
            if (core.Stats()[AnchorCoreInterop.Stat("fieldReuses")] != 1) throw new InvalidOperationException("A covered task did not reuse the cached field.");
            using var fresh = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024); fresh.WriteWholeMask(Player, map, 500);
            int alone = fresh.CreateTask(Player, anchorNode, 1, 7, new[] { near }); fresh.Step(8, 1);
            foreach (int slot in new[] { anchorNode, anchorNode + 1, anchorNode + 61 })
            {
                RouteResult cached = core.Query(second, near, slot, Margin), own = fresh.Query(alone, near, slot, Margin);
                if (cached.Status != own.Status || cached.Cost != own.Cost) throw new InvalidOperationException($"Reused field answers differently: slot={slot} {cached.Status}/{cached.Cost} vs {own.Status}/{own.Cost}");
            }
            int third = core.CreateTask(Player, anchorNode, 1, 7, new[] { 59 * 60 + 59 }); core.Step(8, 1);
            if (core.Stats()[AnchorCoreInterop.Stat("fieldReuses")] != 1) throw new InvalidOperationException("A task with an unsettled start reused the cached field.");
            core.UpdateMask(Player, GroupGate.Grid(60, 60, null, n => n % 60 != 30), new[] { 50 * 60 + 30 });
            int fourth = core.CreateTask(Player, anchorNode, 1, 7, new[] { near }); core.Step(8, 1);
            if (core.Stats()[AnchorCoreInterop.Stat("fieldReuses")] != 1) throw new InvalidOperationException("A field of an older mask revision was reused.");
            core.Release(first); core.Release(second); core.Release(third); core.Release(fourth);
        }
        // Entry penalties (APIShared route tile rules): a penalized column is crossed at its one
        // unpenalized tile, which the unpenalized route (lowest direction first) does not use.
        {
            GridSnapshot map = GroupGate.Grid(20, 5);
            int start = 2 * 20 + 0, slot = 2 * 20 + 19;
            using var core = new AnchorCoreInterop(20, 5, 64L * 1024 * 1024); core.WriteWholeMask(Player, map, 100);
            int plain = core.CreateTask(Player, slot, 1, 7, new[] { start }); core.Step(8, 1);
            int CrossingRow(RouteResult route)
            {
                int node = start;
                foreach (byte d in route.Directions) { node += GridSnapshot.Dy[d] * 20 + GridSnapshot.Dx[d]; if (node % 20 == 10) return node / 20; }
                return -1;
            }
            RouteResult straight = core.Query(plain, start, slot, Margin);
            if (straight.Status != SearchStatus.Found || CrossingRow(straight) == 4) throw new InvalidOperationException("Unpenalized route already crosses at the open row.");
            var column = new[] { 0 * 20 + 10, 1 * 20 + 10, 2 * 20 + 10, 3 * 20 + 10 };
            if (core.UpdatePenalties(Player, column, new byte[] { 50, 50, 50, 50 }) != 4 || core.TaskStatus(plain) != AnchorCoreInterop.Stale)
                throw new InvalidOperationException("A penalty change did not make the task stale.");
            int penalized = core.CreateTask(Player, slot, 1, 7, new[] { start }); core.Step(8, 1);
            RouteResult around = core.Query(penalized, start, slot, Margin);
            if (around.Status != SearchStatus.Found || CrossingRow(around) != 4) throw new InvalidOperationException("The route crosses a penalized tile: row " + CrossingRow(around));
            using (var batchCore = new AnchorCoreInterop(20, 5, 64L * 1024 * 1024))
            {
                batchCore.WriteWholeMask(Player, map, 100); batchCore.UpdatePenalties(Player, column, new byte[] { 50, 50, 50, 50 });
                int batchTask = batchCore.CreateTask(Player, slot, 1, 7, new[] { start }); batchCore.Step(8, 1);
                RouteResult batched = batchCore.QueryBatch(batchTask, new[] { start }, new[] { slot }, Margin, 2)[0];
                if (batched.Status != SearchStatus.Found || !batched.Directions.SequenceEqual(around.Directions)) throw new InvalidOperationException("Batch route differs under penalties.");
            }
        }
        // Single routes search a corridor: on open ground ties dive towards the start instead of
        // settling the diamond of equally short routes (about distance^2 / 4 nodes), so a straight
        // trip settles a few nodes per step.
        {
            GridSnapshot map = GroupGate.Grid(60, 60);
            using var core = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024); core.WriteWholeMask(Player, map, 500);
            RouteResult route = core.SingleRoute(Player, 30 * 60 + 10, 1, 7, 30 * 60 + 40);
            long settles = core.Stats()[AnchorCoreInterop.Stat("singleSettles")];
            if (route.Status != SearchStatus.Found || route.Cost != 30 || settles > 4 * 30)
                throw new InvalidOperationException($"Single route is not a corridor search: status={route.Status} cost={route.Cost} settles={settles}");
        }
        // A start enclosed by blocked cells is proven unreachable from its own small component, without
        // the backward A* settling the target's whole open component.
        {
            static bool Ring(int n) { int x = n % 60, y = n / 60; return Math.Max(Math.Abs(x - 10), Math.Abs(y - 10)) == 1; }
            GridSnapshot map = GroupGate.Grid(60, 60, null, n => !Ring(n));
            using var core = new AnchorCoreInterop(60, 60, 128L * 1024 * 1024); core.WriteWholeMask(Player, map, 500);
            RouteResult route = core.SingleRoute(Player, 50 * 60 + 50, 1, 7, 10 * 60 + 10);
            long settles = core.Stats()[AnchorCoreInterop.Stat("singleSettles")];
            if (route.Status != SearchStatus.Unreachable || settles > 0)
                throw new InvalidOperationException($"Enclosed start is not proven unreachable cheaply: status={route.Status} settles={settles}");
            RouteResult open = core.SingleRoute(Player, 50 * 60 + 50, 1, 7, 20 * 60 + 20);
            if (open.Status != SearchStatus.Found || open.Cost != 30)
                throw new InvalidOperationException($"Open start after the enclosure proof: status={open.Status} cost={open.Cost}");
        }
        // Directed snake longer than 2,000 steps: never returned as a route.
        {
            GridSnapshot map = GroupGate.Grid(800, 4, (a, b, d) => ((a / 800) % 2 == 0 ? d == 2 : d == 6) && a / 800 == b / 800 || d == 4 && a % 800 == ((a / 800) % 2 == 0 ? 799 : 0) ? 1U << d : 0);
            using var core = new AnchorCoreInterop(800, 4, 128L * 1024 * 1024); core.WriteWholeMask(Player, map, 4096);
            int task = core.CreateTask(Player, 2400, 1, 7, new[] { 0 }); core.Step(1, 1);
            if (core.Query(task, 0, 2400, Margin).Reason != "over-2000-steps") throw new InvalidOperationException("Over-limit route was returned.");
        }
        // A budget that cannot hold the worker fields fails cleanly, without a crash.
        {
            GridSnapshot map = GroupGate.Grid(800, 800);
            using var core = new AnchorCoreInterop(800, 800, 64L * 1024 * 1024); core.WriteWholeMask(Player, map, 65536);
            int task = core.CreateTask(Player, 400 * 800 + 400, 1, 7, new[] { 10 });
            int code = core.StepCode(1, 8);
            if (code != AnchorCoreInterop.NoMemory && core.TaskStatus(task) != AnchorCoreInterop.Failed) throw new InvalidOperationException("Exhausted budget was not reported.");
        }
        Console.WriteLine("PASS anchor core contracts independentComparisons=" + comparisons + " recommendedThreads=" + recommended);
    }
    private static double Percentile(List<long> values, double p) { if (values.Count == 0) return 0; values.Sort(); return values[Math.Min(values.Count - 1, (int)Math.Ceiling(p * values.Count) - 1)]; }
    private static object Measure(GroupLoad load, int recommended)
    {
        var rows = new Dictionary<string, object>(); RouteResult[] reference = null; double maskMs = 0;
        foreach (int threads in new[] { 1, recommended })
        {
            using var core = new AnchorCoreInterop(load.Map.Width, load.Map.Height);
            var watch = Stopwatch.StartNew(); core.WriteWholeMask(Player, load.Map, 16384); maskMs = watch.Elapsed.TotalMilliseconds;
            core.Stats(true);
            var results = Solve(core, load, threads, out _); long[] stats = core.Stats();
            if (reference == null) reference = results; else Same(reference, results, load.Name + " threads=" + threads);
            double stepMs = stats[AnchorCoreInterop.Stat("stepNs")] / 1e6, queryMs = stats[AnchorCoreInterop.Stat("queryNs")] / 1e6, subMs = stats[AnchorCoreInterop.Stat("subNs")] / 1e6;
            var named = new Dictionary<string, long>(); for (int i = 0; i < stats.Length; i++) named[AnchorCoreInterop.StatNames[i]] = stats[i];
            rows["threads-" + threads] = new { stepMs, queryMs, subAnchorMs = subMs, queryMicrosecondsPerUnit = (queryMs - subMs) * 1000 / load.Queries.Count, stats = named };
            Console.WriteLine("  core threads=" + threads + " step=" + stepMs.ToString("F1") + " ms queries=" + queryMs.ToString("F1") + " ms (sub-anchors " + subMs.ToString("F1") + " ms, " + stats[AnchorCoreInterop.Stat("subAnchors")] + ") via=" + stats[AnchorCoreInterop.Stat("viaChosen")] + " peakMiB=" + stats[AnchorCoreInterop.Stat("peakBytes")] / 1048576);
        }
        // Batch queries (one call per group, parallel pass plus ordered deferred pass): identical
        // answers, timed like the single queries above.
        foreach (int threads in new[] { 1, recommended })
        {
            using var core = new AnchorCoreInterop(load.Map.Width, load.Map.Height);
            core.WriteWholeMask(Player, load.Map, 16384); core.Stats(true);
            var results = SolveBatch(core, load, threads); long[] stats = core.Stats();
            Same(reference, results, load.Name + " batch threads=" + threads);
            double queryMs = stats[AnchorCoreInterop.Stat("queryNs")] / 1e6, subMs = stats[AnchorCoreInterop.Stat("subNs")] / 1e6;
            rows["batch-threads-" + threads] = new { queryMs, subAnchorMs = subMs, queryMicrosecondsPerUnit = queryMs * 1000 / load.Queries.Count };
            Console.WriteLine("  batch threads=" + threads + " queries=" + queryMs.ToString("F1") + " ms (sub-anchors " + subMs.ToString("F1") + " ms)");
        }
        var oracle = new PreciseOracle(load.Map); var extra = new List<long>(); var relative = new List<long>(); int mismatch = 0, worse = 0;
        var pw = Stopwatch.StartNew(); double next = 5;
        for (int i = 0; i < load.Queries.Count; i++)
        {
            RouteQuery q = load.Queries[i]; RouteResult p = oracle.Find(q), r = reference[i];
            if (pw.Elapsed.TotalSeconds >= next) { Console.WriteLine("  PROGRESS Precise " + (i + 1) + "/" + load.Queries.Count); next = pw.Elapsed.TotalSeconds + 5; }
            if (p.Status != SearchStatus.Found || r.Status != SearchStatus.Found) throw new InvalidOperationException(load.Name + ": positive case not found case=" + i + " precise=" + p.Status + " core=" + r.Status + " " + r.Reason);
            RouteResult described = load.Map.Describe(q, r.Directions);
            if (described.Status != SearchStatus.Found || described.Cost != r.Cost) throw new InvalidOperationException(load.Name + ": illegal path case=" + i);
            if (r.Cost < p.Cost) throw new InvalidOperationException(load.Name + ": below optimum case=" + i);
            extra.Add(r.Cost - p.Cost); relative.Add((r.Cost - p.Cost) * 10000 / Math.Max(1, p.Cost)); if (r.Cost > p.Cost) worse++;
            if ((r.Moat > 0) != (p.Moat > 0)) mismatch++;
        }
        Console.WriteLine("  Precise total=" + pw.ElapsedMilliseconds + " ms");
        return new
        {
            kind = "anchor-core", name = load.Name, units = load.Queries.Count, groups = load.Groups, maskWriteMs = maskMs, results = rows,
            preciseMs = pw.Elapsed.TotalMilliseconds,
            quality = new { worseThanOptimum = worse, extraCostMean = extra.Average(), extraCostP95 = Percentile(extra, 0.95), extraCostMax = extra.Max(), extraPercentP95 = Percentile(relative, 0.95) / 100.0, extraPercentMax = relative.Max() / 100.0, moatDecisionMismatches = mismatch },
            scope = "one task per group, queries in group order, margin " + Margin + "; every path legal and compared with Precise; identical for 1 and recommended threads"
        };
    }
    internal static int Run(string capture)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string output = Path.Combine(root, "results", "core-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(output);
        string nativeDir = Path.GetFullPath(Path.Combine(root, "../MoatMove/.local/anchor"));
        File.Copy(Path.Combine(nativeDir, "build.json"), Path.Combine(output, "native-build.json"));
        using (JsonDocument proof = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "native-build.json"))))
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(nativeDir, "MoatMoveAnchor.dll")))), proof.RootElement.GetProperty("dllSha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Anchor core binary/proof mismatch");
        var rows = new List<object>(); int exit = 0;
        try
        {
            Contracts(); rows.Add(new { kind = "contracts", passed = true, independentComparisons = comparisons });
            int recommended = AnchorCoreInterop.mma_recommended_threads();
            CapturedMap raw = SnapshotCodec.ReadCaptured(capture);
            var loads = GroupWorkloads.Captured(raw.Build(raw.Players[0]), raw.Units); loads.Add(GroupWorkloads.MoatMix(GroupWorkloads.MoatMap()));
            foreach (GroupLoad load in loads.Where(l => l.Queries.Count >= 1024))
            {
                Console.WriteLine("BEGIN " + load.Name + " units=" + load.Queries.Count + " groups=" + load.Groups);
                rows.Add(Measure(load, recommended)); File.WriteAllText(Path.Combine(output, "progress.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception ex) { rows.Add(new { kind = "incomplete", error = ex.ToString() }); Console.WriteLine(ex); exit = 1; }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
        Console.WriteLine("ANCHOR CORE exit=" + exit + "; evidence=" + output); return exit;
    }
}
