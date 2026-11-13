using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Pathfinding.Engine
{
    public static unsafe class HazardScannerFixture
    {
        // Hash-bound native record layout audited in FIRE_EFFECTS/PLAGUE_HEIGHT.
        private const int Slots = 6000, Stride = 0xE8, State = 0x3C, Type = 0x3E, Tile = 0x60,
            SubX = 0x4C, SubY = 0x4E, Direction = 0xB0, Steps = 0xB2;
        private static int assertions;
        private static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
        private static int Node(int x, int y) => y * RouteTileRules.MapSize + x;
        private static void Short(byte* records, int slot, int offset, int value) => *(short*)(records + slot * Stride + offset) = (short)value;
        private static void Fire(byte* records, int slot, int tile, int state = 2)
        { Short(records, slot, State, state); Short(records, slot, Type, 9); *(int*)(records + slot * Stride + Tile) = tile; }
        private static void Cloud(byte* records, int slot, int x, int y, int direction = 0, int steps = 0, int state = 2)
        {
            Short(records, slot, State, state); Short(records, slot, Type, (int)ProjectileType.Disease);
            Short(records, slot, SubX, x); Short(records, slot, SubY, y);
            Short(records, slot, Direction, direction); Short(records, slot, Steps, steps);
        }
        private static void BindFire(FireRouteRules scanner, byte* memory) => typeof(FireRouteRules)
            .GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scanner, Pointer.Box(memory, typeof(byte*)));
        private static void RuleAt(int x, int y, bool expected)
        {
            for (int player = 1; player <= 8; player++)
            {
                var effect = RouteTileRules.GetEffect(x, y, player, out int penalty);
                Check(expected ? effect == RouteTileEffect.Penalty && penalty == 254 : effect == RouteTileEffect.None,
                    $"unexpected rule at {x},{y} player {player}: {effect}/{penalty}");
            }
        }
        private static void Nodes(HazardTileSet bits, IEnumerable<int> expected, string phase)
        {
            var set = new HashSet<int>(expected);
            Check(bits.Count == set.Count, phase + " count " + bits.Count + " expected " + set.Count);
            foreach (int node in set) Check(bits.Contains(node), phase + " missing " + node);
        }
        private static IEnumerable<int> Square(int x, int y, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if ((uint)(x + dx) < 800 && (uint)(y + dy) < 800) yield return Node(x + dx, y + dy);
        }
        public static void Run()
        {
            byte* records = (byte*)NativeMemory.AllocZeroed((nuint)(Slots * Stride));
            try
            {
                FireCases(records);
                NativeMemory.Clear(records, (nuint)(Slots * Stride));
                PlagueCases(records);
                ModelCases();
                Console.WriteLine($"PASS: {assertions} production hazard scanner/model/journal assertions; fake game boundaries, real rules and owner-policy union.");
            }
            finally { NativeMemory.Free(records); }
        }
        private static void FireCases(byte* records)
        {
            var api = GameTileManagerAPI.Instance; api.Clear(); api.Rectangle(10, 10, 30, 30);
            var bits = new HazardTileSet(); var fire = new FireRouteRules(bits); BindFire(fire, records);
            var policy = new RouteReplanPolicy(3, 50, 10, 2400);
            Check(fire.TryRegister("tests.hazard.scanner", policy, out _), "fire registration");
            Fire(records, 0, api.GetTileId(10, 10)); // slot zero is not an effect
            Fire(records, 1, api.GetTileId(15, 15)); Fire(records, 2, api.GetTileId(15, 15));
            Fire(records, 3, api.GetTileId(16, 15), 1); Fire(records, 4, api.GetTileId(17, 15), 3);
            Fire(records, 5, -1); Fire(records, 6, GamePathingManagerAPI.Instance.GridLength);
            Fire(records, 7, api.GetTileId(18, 15)); Short(records, 7, Type, (int)ProjectileType.Disease);
            Fire(records, Slots - 1, api.GetTileId(20, 20));
            fire.Tick(3); Check(bits.Count == 0, "off-cadence scan changed fire");
            long cursor = RouteTileRules.Version;
            fire.Tick(4); Nodes(bits, new[] { Node(15, 15), Node(20, 20) }, "initial fire");
            RuleAt(15, 15, true); RuleAt(20, 20, true); RuleAt(16, 15, false);
            Check(ReferenceEquals(RouteTileRules.GetReplanPolicy(15, 15, 1), policy), "fire policy lost");
            var journal = new int[64]; Check(RouteTileRules.ReadChanges(ref cursor, journal) == 2, "fire additions not journaled once per tile");
            long version = RouteTileRules.Version; fire.Tick(8); Check(RouteTileRules.Version == version, "unchanged fire generated journal changes");
            Short(records, 1, State, 0); fire.Tick(12); RuleAt(15, 15, true);
            Short(records, 2, State, 3); fire.Tick(16); RuleAt(15, 15, false);
            Fire(records, 2, api.GetTileId(22, 21)); fire.Tick(20); RuleAt(22, 21, true); RuleAt(15, 15, false);
            fire.Reset(); Check(bits.Count == 0 && fire.Burning == 0, "fire reset left hazard bits"); RuleAt(22, 21, false);
            // Reset must reread the new map's path-grid size instead of retaining a prior count.
            api.Clear(); api.Rectangle(50, 50, 51, 51); GamePathingManagerAPI.Instance.GridLength = 2;
            NativeMemory.Clear(records, (nuint)(Slots * Stride)); Fire(records, 1, 3); fire.Tick(24);
            Check(bits.Count == 0, "new map retained old fire tile count");
            fire.Reset(); GamePathingManagerAPI.Instance.GridLength = PlagueDamageModel.NativeTileCount;
            api.Clear(); api.Rectangle(10, 10, 30, 30);
            Fire(records, 1, api.GetTileId(15, 15));
            var sequence = new SchedulerOrderFixture(fire, bits); sequence.Tick(28);
            Check(sequence.AnchorVersion < RouteTileRules.Version && sequence.ReplanVersion == sequence.AnchorVersion && sequence.EscapeTiles == 1,
                "production scheduling block did not scan after anchor/replan and before escape");
            sequence.Tick(29); Check(sequence.AnchorVersion == RouteTileRules.Version && sequence.ReadChanges == 1,
                "next production scheduling block did not expose preceding scanner journal");
            fire.Reset();
        }
        private static void PlagueCases(byte* records)
        {
            var api = GameTileManagerAPI.Instance; api.Clear(); api.Rectangle(20, 20, 160, 160);
            var terrain = new Terrain(); var bits = new HazardTileSet(); var near = new HazardTileSet();
            var client = new ModApiClient();
            var plague = new PlagueRouteRules(bits, near, new PlagueDamageModel(terrain), client); plague.Bind(records);
            var policy = new RouteReplanPolicy(3, 50, 10, 3600);
            Check(plague.TryRegister("tests.hazard.scanner", policy, out _), "plague registration");
            Cloud(records, 25, 80 * 8, 80 * 8, state: 1); plague.Tick(4); Check(bits.Count == 0, "new cloud caused damage rules");
            Short(records, 25, State, 2); plague.Tick(5); Check(bits.Count == 0, "off-cadence plague scan");
            plague.Tick(8); Nodes(bits, Square(80, 80, 2), "single cloud"); Nodes(near, Square(80, 80, 3), "cloud margin");
            RuleAt(80, 80, true); Check(plague.Clouds == 1 && !plague.PolicyActive, "Vanilla cloud counters");
            Short(records, 25, 0x28, 1017); plague.Tick(12); Check(bits.Count == 25, "fading state-2 cloud stopped contributing");
            Cloud(records, 26, 81 * 8, 80 * 8); plague.Tick(16);
            Nodes(bits, Square(80, 80, 2).Concat(Square(81, 80, 2)), "overlap");
            Short(records, 25, State, 3); plague.Tick(20); Nodes(bits, Square(81, 80, 2), "one removed overlapping cloud");
            Short(records, 26, State, 0); plague.Tick(24); Check(bits.Count == 0 && near.Count == 0 && plague.Clouds == 0, "cloud removal retained rules");
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
            for (int d = 0; d < 8; d++)
            {
                int subX = 80 * 8 + (dx[d] > 0 ? 7 : 0), subY = 80 * 8 + (dy[d] > 0 ? 7 : 0);
                Cloud(records, 25, subX, subY, d, 2); plague.Tick(28 + d * 12);
                Nodes(bits, Square(80, 80, 2).Concat(Square(80 + dx[d], 80 + dy[d], 2)), "predictable direction " + d);
                Short(records, 25, Steps, 1); plague.Tick(32 + d * 12);
                Nodes(bits, Square(80, 80, 2), "direction redraw step " + d);
                Short(records, 25, Steps, 0); plague.Tick(36 + d * 12); Nodes(bits, Square(80, 80, 2), "unknown next direction " + d);
            }
            Cloud(records, 25, 80 * 8 + 7, 80 * 8, 8, 2); plague.Tick(128); Nodes(bits, Square(80, 80, 2), "invalid direction");
            Cloud(records, 25, 120 * 8, 120 * 8); plague.Tick(132); Nodes(bits, Square(120, 120, 2), "reused cloud slot moved"); RuleAt(80, 80, false);
            // Read the real unioned owner policy through the fake capability boundary.
            int protectedTile = api.GetTileId(121, 120); terrain.FlagsData[protectedTile] = PlagueDamageModel.WallFlag;
            plague.Tick(136); Check(!bits.Contains(Node(121, 120)), "Vanilla wall immunity");
            Check(client.Policies.TrySet("walls", true, PlagueSurfaceCategory.Walls, 0), "owner wall policy");
            Check(client.Policies.TrySet("tower", true, PlagueSurfaceCategory.Towers, 20), "owner tower policy");
            plague.Tick(140); Check(bits.Contains(Node(121, 120)) && plague.PolicyActive, "union policy did not reach scanner");
            terrain.HeightData[protectedTile] = 1; plague.Tick(144); Check(!bits.Contains(Node(121, 120)), "wall tolerance equality boundary");
            client.Available = false; terrain.HeightData[protectedTile] = 0; plague.Tick(148);
            Check(!bits.Contains(Node(121, 120)) && !plague.PolicyActive, "unavailable policy did not retain Vanilla");
            client.Available = true; client.ThrowMissing = true; plague.Tick(152); client.ThrowMissing = false; plague.Tick(156);
            Check(!bits.Contains(Node(121, 120)) && !plague.PolicyActive, "missing policy reader was not safely cached unavailable");
            plague.Reset(); Check(bits.Count == 0 && near.Count == 0 && plague.Clouds == 0, "plague reset");
            // Each corner is a separately controlled map, so native tile ids stay in their real range.
            foreach (var corner in new[] { (0, 0), (799, 0), (0, 799), (799, 799) })
            {
                api.Clear(); api.Rectangle(Math.Max(0, corner.Item1 - 3), Math.Max(0, corner.Item2 - 3), Math.Min(799, corner.Item1 + 3), Math.Min(799, corner.Item2 + 3));
                Cloud(records, 25, corner.Item1 * 8, corner.Item2 * 8); plague.Tick(160);
                Nodes(bits, Square(corner.Item1, corner.Item2, 2), "corner damage"); Nodes(near, Square(corner.Item1, corner.Item2, 3), "corner margin");
                plague.Reset();
            }
            Cloud(records, 25, -8, 0); plague.Tick(164); Check(bits.Count == 0 && near.Count == 0, "negative cloud coordinates");
            Cloud(records, 25, 800 * 8, 0); plague.Tick(168); Check(bits.Count == 0 && near.Count == 0, "outside cloud coordinates");
            plague.Reset();
            ScannerLoad(records);
        }
        private static void ModelCases()
        {
            var terrain = new Terrain(); var model = new PlagueDamageModel(terrain);
            const int cloud = 0, target = 1; terrain.HeightData[cloud] = 40;
            // Every actual native-stub category, with the building identity taking priority over flags.
            foreach (var category in NativeCategoryContract.BuildingCategories)
            {
                terrain.BuildingData[target] = 1; terrain.Types[1] = (category.Type, 0);
                terrain.FlagsData[target] = PlagueDamageModel.WallFlag;
                Check(model.TryGetLane(target, out var lane) && (int)lane == category.Lane + 1, "building category differs from native table: " + category.Type);
            }
            terrain.BuildingData[target] = 0;
            for (int mask = 0; mask < 16; mask++)
            {
                var client = new ModApiClient();
                Check(client.Policies.TrySet("test", true, (PlagueSurfaceCategory)mask, 20), "category union rejected mask");
                var view = PlagueHeightPolicy.FromPacked(client.Policies.Current);
                var rules = new PlagueHeightRules { Walls = view.WallsTolerance + 1, Gates = view.GatesTolerance + 1, Towers = view.TowersTolerance + 1, Keeps = view.KeepsTolerance + 1 };
                foreach (var surface in new[] { (PlagueDamageModel.WallFlag, 0, 0), (0u, 45, 1), (0u, 74, 2), (0u, 40, 3) })
                {
                    terrain.FlagsData[target] = surface.Item1; terrain.BuildingData[target] = surface.Item2 == 0 ? 0 : 1;
                    terrain.Types[1] = ((eStructs)surface.Item2, 0);
                    // These categories use raw height in this synthetic standing-plane boundary.
                    terrain.HeightData[target] = 60;
                    Check(model.WouldDamage(cloud, target, 1, rules) == ((mask & (1 << surface.Item3)) != 0), "inclusive policy category boundary mask=" + mask);
                    terrain.HeightData[target] = 61;
                    Check(!model.WouldDamage(cloud, target, 1, rules), "outside policy tolerance mask=" + mask);
                }
            }
            terrain.BuildingData[target] = 0; terrain.FlagsData[target] = 0; terrain.HeightData[cloud] = 0;
            terrain.HeightData[target] = 119; Check(model.WouldDamage(cloud, target, 1, default), "Vanilla 119 neighbor height");
            terrain.HeightData[target] = 120; Check(!model.WouldDamage(cloud, target, 1, default), "Vanilla strict 120 neighbor height");
            Check(model.WouldDamage(cloud, target, 0, default), "center incorrectly used neighbor height gate");
            terrain.FlagsData[target] = PlagueDamageModel.StairsFlag | PlagueDamageModel.WallFlag | PlagueDamageModel.BuildingFlag;
            Check(model.Plane(target) == 132, "stairs plane priority");
            terrain.FlagsData[target] = PlagueDamageModel.WallFlag; terrain.BuildingData[target] = 1;
            Check(model.Plane(target) == 160, "building-backed wall plane");
            terrain.BuildingData[target] = -1; Check(model.Plane(target) == -1 && !model.TryGetLane(target, out _), "invalid signed building id");
            terrain.BuildingData[target] = 4000; Check(model.Plane(target) == -1 && !model.TryGetLane(target, out _), "outside building id");
            Check(!model.WouldDamage(-1, target, 0, default) && !model.WouldDamage(cloud, PlagueDamageModel.NativeTileCount, 0, default), "invalid native tile limits");
        }
        private static void ScannerLoad(byte* records)
        {
            var api = GameTileManagerAPI.Instance; api.Clear(); api.Rectangle(50, 50, 150, 150);
            var bits = new HazardTileSet(); var near = new HazardTileSet(); var terrain = new Terrain();
            var fire = new FireRouteRules(bits); BindFire(fire, records); Check(fire.TryRegister("tests.hazard.load", null, out _), "load fire registration");
            foreach (int count in new[] { 1, 225, 5999 })
            {
                NativeMemory.Clear(records, (nuint)(Slots * Stride));
                for (int slot = 1; slot <= count; slot++) Fire(records, slot, api.GetTileId(50 + slot % 100, 50 + slot / 100));
                Measure("fire", count, () => fire.Tick(4));
                Check(bits.Count == count, "load fire scan omitted a slot"); fire.Reset();
            }
            var plague = new PlagueRouteRules(bits, near, new PlagueDamageModel(terrain), null); plague.Bind(records);
            Check(plague.TryRegister("tests.hazard.load", null, out _), "load plague registration");
            foreach (int count in new[] { 1, 225, 1000 })
            {
                NativeMemory.Clear(records, (nuint)(Slots * Stride));
                // Complete overlap isolates scanning cost from output-set growth.
                for (int slot = 1; slot <= count; slot++) Cloud(records, slot, 100 * 8, 100 * 8);
                Measure("plague-overlap", count, () => plague.Tick(4));
                Check(bits.Count == 25 && plague.Clouds == count, "load plague scan omitted overlapping slots"); plague.Reset();
            }
            void Measure(string kind, int count, Action scan)
            {
                for (int i = 0; i < 5; i++) scan();
                long[] times = new long[20]; long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < times.Length; i++) { long start = Stopwatch.GetTimestamp(); scan(); times[i] = Stopwatch.GetTimestamp() - start; }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before; Array.Sort(times);
                Console.WriteLine($"HAZARD OFFLINE kind={kind} effects={count} scans={times.Length} medianMs={times[times.Length/2]*1000.0/Stopwatch.Frequency:F4} maxMs={times[^1]*1000.0/Stopwatch.Frequency:F4} allocatedBytes={allocated} (CLR fixture; not game tick/FPS)");
            }
        }
        private sealed class Terrain : IPlagueTerrain
        {
            internal readonly uint[] FlagsData = new uint[PlagueDamageModel.NativeTileCount];
            internal readonly int[] HeightData = new int[PlagueDamageModel.NativeTileCount], BuildingData = new int[PlagueDamageModel.NativeTileCount];
            internal readonly Dictionary<int, (eStructs Type, int Height)> Types = new Dictionary<int, (eStructs, int)>();
            public uint Flags(int tile) => FlagsData[tile]; public int RawHeight(int tile) => HeightData[tile];
            public int DefaultHeight(int tile) => HeightData[tile]; public int BuildingId(int tile) => BuildingData[tile];
            public bool TryGetBuilding(int id, out eStructs type, out int height)
            { bool found = Types.TryGetValue(id, out var result); type = result.Type; height = result.Height; return found; }
        }
    }
    internal partial class SchedulerOrderFixture
    {
        internal sealed class Runtime { public bool EngineEnabled = true; }
        private readonly Runtime runtime = new Runtime();
        private readonly FireRouteRules fireRules; private readonly PlagueRouteRules plagueRules = null;
        private readonly HazardTileSet tiles; private readonly bool fireRulesActive = true, plagueRulesActive = false;
        private int plagueCloudCount; private bool plaguePolicyActive;
        public long AnchorVersion, ReplanVersion; public int EscapeTiles, ReadChanges;
        private long cursor = RouteTileRules.Version; private readonly int[] changes = new int[64];
        public SchedulerOrderFixture(FireRouteRules fire, HazardTileSet tiles) { fireRules = fire; this.tiles = tiles; }
        private bool AnchorPlayerScanDue(int tick) => false; private int ScanAnchorPlayers() => 0;
        private void RequestAnchorMasksForPlayers(int players) { }
        private void AnchorTick(int tick) { AnchorVersion = RouteTileRules.Version; ReadChanges = RouteTileRules.ReadChanges(ref cursor, changes); }
        private void ReplanTick(int tick) { ReplanVersion = AnchorVersion; }
        private void FireEscapeTick(int tick) { EscapeTiles = tiles.Count; }
        private void ProbeRouteRules() { } private void ProbeMoatCost() { } private void ProbeCursorTopology() { }
    }
}
namespace SHCDESE.API
{
    internal struct Vector { public ushort X, Y; }
    internal sealed class GameTileManagerAPI
    {
        public static readonly GameTileManagerAPI Instance = new GameTileManagerAPI();
        private readonly Dictionary<(int, int), int> ids = new Dictionary<(int, int), int>();
        private readonly List<Vector> positions = new List<Vector>();
        public void Clear() { ids.Clear(); positions.Clear(); }
        public void Rectangle(int minX, int minY, int maxX, int maxY)
        { for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) { ids.Add((x, y), positions.Count); positions.Add(new Vector { X = (ushort)x, Y = (ushort)y }); } }
        public int GetTileId(int x, int y) => ids.TryGetValue((x, y), out int id) ? id : -1;
        public Vector GetTileVectorFromId(int tile) => (uint)tile < positions.Count ? positions[tile] : new Vector { X = 800, Y = 800 };
        public int GetTileDefaultHeight(int tile) => 0;
    }
    internal sealed class GamePathingManagerAPI
    {
        public static readonly GamePathingManagerAPI Instance = new GamePathingManagerAPI();
        public int GridLength = Pathfinding.Engine.PlagueDamageModel.NativeTileCount;
        public ReadOnlySpan<short> GetPathComponentGrid() => new short[GridLength];
    }
}
namespace SHCDESE.Interop { internal struct GameBuilding { public eStructs r_BuildingType; public int r_HeightElevation; } }
namespace BepInEx.Logging { internal sealed class ManualLogSource { } }
namespace Shared
{
    internal static class DebugLogHelper { public static void LogInfo(object log, string text) { } }
    internal static class NativePatternResolver
    {
        public struct Result { public int Rva; }
        public static Result ResolveUnique(ReadOnlySpan<byte> memory, string pattern, int rva, bool required, string label, object log) => throw new NotSupportedException("Binding is outside this fake game boundary");
        public static int ResolveRelativeTarget(ReadOnlySpan<byte> memory, int operand, int next) => throw new NotSupportedException();
    }
}
namespace APIShared
{
    internal sealed class ModApiClient
    {
        public readonly PlagueHeightPolicies Policies = new PlagueHeightPolicies(_ => { });
        public bool Available = true, ThrowMissing;
        public bool TryGetPlagueHeightPolicy(out PlagueHeightPolicy policy, out string diagnostic)
        { if (ThrowMissing) throw new MissingMethodException("fixture policy unavailable"); policy = PlagueHeightPolicy.FromPacked(Policies.Current); diagnostic = null; return Available; }
    }
    internal static unsafe class BuildingAccess
    { public static bool TryGetById(int id, out GameBuilding* building, out string reason) { building = null; reason = null; return false; } }
}
