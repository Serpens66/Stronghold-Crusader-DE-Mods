using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BetterPathfindingTest.Core;
using BugfixesAndQoL.UnitCommands;

internal static partial class Program
{
    private static void AdvancedContracts()
    {
        RawPredicateContracts();
        var controlled = UpdateComparison.Controlled(new WorkBudget());
        Check((bool)controlled["passed"], "Controlled closure and shorter path outside old route, including prepared field invalidation.");
        var map = Grid(3, 3);
        using (var nav = new AreaNavigator(map, new WorkBudget()))
        {
            var candidates = nav.EvaluateCandidates(new RouteQuery { Start = 0, Target = 8, Terminals = new[] { new TerminalEdge(7, 8, true) } }, new[] { 2, 8 });
            Check(candidates.Length == 2 && candidates[0].Status == SearchStatus.Found && candidates[1].Status == SearchStatus.Found, "Candidate-specific terminal overlays do not leak into other targets.");
        }
        var budget = new WorkBudget(8L * 1024 * 1024, 0);
        bool timed = false; try { using (var nav = new AreaNavigator(map, budget)) { } } catch (SearchBudgetException) { timed = true; }
        Check(timed, "Expired budget never produces unreachable.");
        var cancel = new CancellationTokenSource(); cancel.Cancel();
        bool cancelled = false; try { ReferenceSearch.Find(map, new RouteQuery { Start = 0, Target = 8 }, new WorkBudget(cancellation: cancel.Token)); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Lifecycle cancellation propagates without fabricated reachability.");
        Check(PublicationRules.Improves(41, 0, 1, 0, new long[] { 1 }, new long[] { 7 }, new[] { 1 }), "Exactly 40 tick improvement accepted.");
        Check(!PublicationRules.Improves(40, 0, 1, 0, new long[] { 1 }, new long[] { 7 }, new[] { 1 }), "Exactly 39 tick improvement rejected.");
        Check(PublicationRules.Improves(42, 0, 1, 0, new long[] { 1 }, new long[] { 7 }, new[] { 1 }), "41 tick improvement accepted.");
        for (int phase = 0; phase <= 28; phase += 4) for (int speed = -1; speed < 4; speed++) for (int bonus = 0; bonus < 3; bonus++)
        {
            var seed = new UnitSeed { Speed = speed, Speed2 = speed + 4, Phase = phase, Bonus = bonus, Additional = 2, Extra = 1, OnMoat = phase != 0 };
            if (!seed.TryProfile(out long g, out long m, out int progress)) continue;
            Check(WeightedMovementCostProfile.TryCreate(seed.Speed, seed.Speed2, seed.Bonus, seed.Additional, seed.Extra, seed.Phase, seed.OnMoat, out WeightedMovementCostProfile original, out _), "Captured profile must be accepted by actual Precise.");
            Check(g == original.GroundEdgeFixedCost && m == original.GetEdgeFixedCost(true) && progress == original.CadenceProgress, "Captured profile exact costs.");
        }
        // Diagonal crossings at BOTH hierarchy boundaries; asymmetric arcs and unequal wet costs.
        foreach (int x in new[] { 31, 127 })
        {
            var cross = Grid(x + 3, 3, (a, b, d) => d == 1 || d == 3 || d == 2 ? (1U << d) | (b % (x + 3) == x + 1 ? 1U << (d + 8) : 0) : 0);
            using (var nav = new AreaNavigator(cross, new WorkBudget()))
                Compare(nav, cross, new RouteQuery { Start = x, Target = 2 * (x + 3) + x + 2, GroundCost = 2, MoatCost = 11, RequireMoat = true });
        }
        Directory.CreateDirectory(Evidence);
        var queries = new[] { new RouteQuery { Start = 0, Target = 8, RequireMoat = true, Bounds = new[] { new CostBound(2, 1, 99), new CostBound(1, 4, 100) }, Terminals = new[] { new TerminalEdge(7, 8, true) } } };
        string file = Path.Combine(Evidence, "codec.bpf.gz"); SnapshotCodec.WriteQueries(file, map, queries);
        var loaded = SnapshotCodec.ReadQueries(file, out GridSnapshot restored);
        Check(restored.Fingerprint() == map.Fingerprint() && loaded.Length == 1 && loaded[0].Bounds.Length == 2 && loaded[0].Terminals[0].Moat, "Bounded query roundtrip retains all constraints.");
    }
    private static unsafe void RawPredicateContracts()
    {
        const int width = 16, height = 12; var random = new Random(925837);
        var raw = new CapturedMap { Width = width, Height = height, MapSize = width, Available = new byte[width * height], Rows = new int[height * 3], Flags = new uint[width * height], Heights = new byte[width * height], NativeEdges = new byte[width * height], MoatOwner = new byte[width * height], Structures = new ushort[width * height], Organisms = new ushort[width * height], BuildingTypes = new ushort[5], StructureHeights = new short[5], LowWalls = new byte[5], VegetationStates = new short[20], Players = new[] { 1, 2 } };
        for (int y = 0; y < height; y++) raw.Rows[y * 3] = y * width;
        for (int i = 0; i < raw.VegetationStates.Length; i++) raw.VegetationStates[i] = (short)i;
        // Audited C07C0 input classes; this checks the machine-contract translation.
        raw.BuildingTypes[1] = 0x28; raw.BuildingTypes[2] = 0x2D; raw.BuildingTypes[3] = 0x4A;
        raw.StructureHeights[1] = 64; raw.StructureHeights[2] = 128; raw.StructureHeights[3] = 296; raw.LowWalls[2] = 1;
        raw.Allied[1 * 9 + 2] = true;
        uint[] flags = { 0, 0x40000000U, 0x100U, 0x800U, 0x1000U, 0x10000000U, 0x40001000U, 0x10900U, 0x0A5014B1U };
        for (int n = 0; n < raw.Flags.Length; n++)
        { raw.Available[n] = 1; raw.Flags[n] = flags[random.Next(flags.Length)]; raw.NativeEdges[n] = (byte)random.Next(256); raw.Heights[n] = (byte)(random.Next(12) * 8); raw.MoatOwner[n] = (byte)random.Next(4); raw.Structures[n] = (ushort)random.Next(4); raw.Organisms[n] = (ushort)random.Next(20); }
        var directions = new byte[] { 1, 2, 4, 8, 16, 32, 64, 128 };
        var records = new byte[raw.BuildingTypes.Length * WeightedMoatRoutePlanner.NativeBuildingRecordStride];
        for (int id = 1; id < raw.BuildingTypes.Length; id++) { byte[] type = BitConverter.GetBytes(raw.BuildingTypes[id]); Array.Copy(type, 0, records, id * WeightedMoatRoutePlanner.NativeBuildingRecordStride, 2); }
        foreach (int player in raw.Players) foreach (bool groundOnly in new[] { false, true })
        {
            GridSnapshot graph = raw.Build(player, groundOnly); var query = new RouteQuery();
            fixed (int* rows = raw.Rows) fixed (uint* tileFlags = raw.Flags) fixed (ushort* structures = raw.Structures) fixed (byte* heights = raw.Heights, native = raw.NativeEdges, masks = directions, buildings = records)
            {
                var original = new WeightedMoatRoutePlanner(rows, tileFlags, structures, heights, native, masks, buildings,
                    (p, t) => raw.MoatOwner[t] == 0 ? CompletedMoatRelationship.Invalid : raw.MoatOwner[t] == p || raw.Allied[p * 9 + raw.MoatOwner[t]] ? CompletedMoatRelationship.Friendly : CompletedMoatRelationship.Enemy,
                    t => raw.VegetationStates[raw.Organisms[t]] > 4 && raw.VegetationStates[raw.Organisms[t]] != 15);
                original.AllowAdditionalMoatEntry = () => true;
                for (int n = 0; n < graph.Count; n++) for (int d = 0; d < 8; d++)
                {
                    int to = graph.Neighbor(n, d); if (to < 0) continue;
                    bool expected = original.TryGetEdge(player, n % width, n / width, n, to % width, to / width, to, d, false, false, groundOnly ? MoatTraversalPolicy.GroundOnly : MoatTraversalPolicy.FriendlyOnly, out bool wet, out bool structure);
                    bool actual = graph.Edge(n, d, query, out _, out bool awet, out bool astruct);
                    Check(expected == actual && (!actual || wet == awet && structure == astruct), "Raw Precise directed edge predicate differs at " + n + "/" + d + " player=" + player + " ground=" + groundOnly);
                }
            }
        }
        string file = Path.Combine(Evidence, "raw-codec.gz"); Directory.CreateDirectory(Evidence); SnapshotCodec.WriteCaptured(file, raw); CapturedMap reread = SnapshotCodec.ReadCaptured(file);
        Check(reread.Build(1).Fingerprint() == raw.Build(1).Fingerprint(), "Raw replay rebuilds the same directed graph.");
    }
}
