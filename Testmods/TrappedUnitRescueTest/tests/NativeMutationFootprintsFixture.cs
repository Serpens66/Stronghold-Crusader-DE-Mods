using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.WorldMutations;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class NativeMutationFootprintsFixture
    {
        private static int checks;
        internal static void Run()
        {
            checks = 0;
            var memory = new Memory();
            var adapter = new NativeMutationFootprints(memory, new RescueFootprintBudget(2000, 100000));
            var context = adapter.ContextForOfflineFixture();
            Require(context.RawTileAt(12, 9) == Memory.Tile(12, 9) && context.RawTileAt(12, 9) != 9 * 800 + 12, "native packed rows do not use full-grid ids");
            memory.Inactive.Add(Memory.Tile(12, 9));
            Require(context.TileAt(12, 9) < 0 && context.RawTileAt(12, 9) >= 0, "raw border lookup is independent of active map mask");
            memory.Inactive.Clear();
            var center = new RescueTile(30, 40, Memory.Tile(30, 40));
            int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 };
            for (int i = 0; i < 4; i++)
            {
                Require(context.Neighbor(center, i * 2, out var next) && next.X == 30 + dx[i] && next.Y == 40 + dy[i] && next.TileId == Memory.Tile(next.X, next.Y), "native neighbour delta and Y stride " + i);
            }
            Require(context.SquareOffset(5, 7).X == 1 && context.SquareOffset(5, 7).Y == 2, "read real offset table order instead of reconstructing row-major");
            Require(context.TableInt(0x2D1C38 + 6 * 4) == 85, "actual brush count table");
            bool refused = false;
            try { context.TableInt(0x48F71B0); } catch (InvalidOperationException) { refused = true; }
            Require(refused, "table callback refuses non-table RVAs");
            // A local empty cleanup must not scan any building slots.
            var empty = context.CompleteCleanupFootprint(30, 30, 5);
            Require(empty.Count == 81 && memory.BuildingReads == 0, "empty cleanup remains local");
            memory.AddBuilding(1, eStructs.STRUCT_HOVEL, 30, 30, 2, 123);
            memory.AddBuilding(2, eStructs.STRUCT_GOODS_YARD, 60, 60, 2, 123);
            memory.Layer[Memory.Tile(30, 30)] = 1;
            context = adapter.ContextForOfflineFixture();
            var closure = context.CompleteCleanupFootprint(30, 30, 5);
            Require(Contains(closure, 60, 60) && Contains(closure, 62, 64), "compound key closure reaches distant child and extra goods tiles");
            int indexedReads = memory.BuildingReads;
            context.CompleteCleanupFootprint(30, 30, 5);
            Require(memory.BuildingReads == indexedReads, "building index reused only within one invocation context");
            // Farm preparation has no BuildingLayer owner. Native B8E00 still finds the farm.
            memory.AddBuilding(3, eStructs.STRUCT_HOPSFARM, 10, 10, 3, 0);
            for (int i = 0; i < 24; i++) memory.Words[Memory.Record(3) + 0x1C8 + i * 4] = Memory.Tile(10 + i % 6, 10 + i / 6);
            memory.Flags[Memory.Tile(17, 17)] = 4;
            context = adapter.ContextForOfflineFixture();
            closure = context.CompleteCleanupFootprint(17, 17, 1);
            Require(Contains(closure, 10, 10) && Contains(closure, 18, 18), "farm preparation lookup outside main building square");
            // B8ED0 reads all36 fields, even for Hops'24 actual removal plot entries.
            memory.Flags.Clear(); memory.Flags[Memory.Tile(80, 80)] = 0x04000000;
            memory.Words[Memory.Record(3) + 0x1C8 + 35 * 4] = Memory.Tile(80, 80);
            context = adapter.ContextForOfflineFixture();
            closure = context.CompleteCleanupFootprint(80, 80, 1);
            Require(Contains(closure, 10, 10), "native farm lookup includes unused array tail");
            memory.AddBuilding(4, eStructs.STRUCT_APPLEFARM, 45, 10, 3, 0);
            for (int i = 0; i < 8; i++) memory.Words[Memory.Record(4) + 0x1C8 + i * 4] = Memory.Tile(45 + i, 13);
            context = adapter.ContextForOfflineFixture();
            closure = context.CompleteRemovalFootprint(4);
            Require(Contains(closure, 55, 20) && !Contains(closure, 56, 20), "terminal Apple cleanup is11, distinct from creator prep10");
            refused = false;
            try { new NativeMutationFootprints(memory, new RescueFootprintBudget(10, 100000)).ContextForOfflineFixture().CompleteRemovalFootprint(4); }
            catch (InvalidOperationException ex) { refused = ex.Message == "footprint-removal-tile-budget"; }
            Require(refused, "closure overload fails before returning partial tiles");
            refused = false;
            try { new NativeMutationFootprints(memory, new RescueFootprintBudget(2000, 100000), maximumBuildingReads: 1).ContextForOfflineFixture().CompleteRemovalFootprint(1); }
            catch (InvalidOperationException ex) { refused = ex.Message == "footprint-building-read-budget"; }
            Require(refused, "compound discovery bounded building budget");
            Require(adapter.TryPlan(Mutation(WorldMutationSource.BuildStructure, NativeMutationFootprints.TileManagerRva, 1, 30, 30,
                (int)eMappers.MAPPER_DRAWBRIDGE, 5, 0, 0), out var mapped, out int owner, out string reason) && owner == 1 && mapped.Count == 25,
                "effective building arguments map without native calls: " + reason);
            Require(adapter.TryPlan(Mutation(WorldMutationSource.EditorTileType, NativeMutationFootprints.TileManagerRva, 99, Memory.Tile(30, 30), 30, 3, 0, 2, 1),
                out mapped, out owner, out reason) && owner == 0 && mapped.Count == 13, "editor uses a5 width rather than misleading argument1: " + reason);
            Require(!adapter.TryPlan(Mutation(WorldMutationSource.EditorHeightEven, NativeMutationFootprints.TileManagerRva, Memory.Tile(30, 30), 31, 3),
                out mapped, out owner, out reason) && mapped.Count == 0 && reason == "editor-center-y-contract", "editor packed/Y mismatch rejected");
            int task = NativeMutationFootprints.TileManagerRva + 0x1F3EE30 + 7 * 16;
            memory.Words[NativeMutationFootprints.TileManagerRva + 0x2038E30] = 64000;
            memory.Words[task] = Memory.Tile(40, 40); memory.Words[task + 4] = 40; memory.Words[task + 6] = 40; memory.Words[task + 12] = 2; memory.Words[task + 13] = 3;
            Require(adapter.TryPlan(Mutation(WorldMutationSource.MoatDig, NativeMutationFootprints.TileManagerRva, 7), out mapped, out owner, out reason) &&
                owner == 2 && mapped.Count == 9 && Contains(mapped, 39, 39), "moat task slot maps packed identity and owner: " + reason);
            Require(adapter.TryPlan(Mutation(WorldMutationSource.BuildingRemoval, NativeMutationFootprints.BuildingManagerRva, 4), out mapped, out owner, out reason) &&
                owner == 1 && Contains(mapped, 55, 20), "terminal removal resolves live record and owner: " + reason);
            Require(!adapter.TryPlan(Mutation(WorldMutationSource.BuildStructure, NativeMutationFootprints.TileManagerRva + 1, 1, 30, 30,
                (int)eMappers.MAPPER_DRAWBRIDGE, 5, 0, 0), out mapped, out owner, out reason) && mapped.Count == 0 && reason == "mutation-manager-address-mismatch", "foreign pointer rejected before reads");
            for (int progress = 0; progress < 256; progress++)
            {
                int incremented = (progress + 1) % 256, decremented = (progress + 255) % 256;
                Require(RescueCleanupProjection.MoatCanChangeGeometry(false, (byte)progress) == (incremented >= 4 && incremented < 128), "signed Dig threshold " + progress);
                Require(RescueCleanupProjection.MoatCanChangeGeometry(true, (byte)progress) == (decremented == 0 || decremented >= 128), "signed Fill threshold " + progress);
            }
            foreach (int progress in new[] { 0, 1, 2, 127, 128, 255 })
            {
                memory.Words[task + 13] = progress;
                int reads = memory.ReadCount;
                Require(adapter.TryPlan(Mutation(WorldMutationSource.MoatDig, NativeMutationFootprints.TileManagerRva, 7), out mapped, out owner, out reason) &&
                    mapped.Count == 0 && memory.ReadCount - reads == 3, "Dig nonterminal path performs exactlythree scalar reads and no terrain discovery");
            }
            memory.Words[task + 13] = 0;
            Require(adapter.TryPlan(Mutation(WorldMutationSource.MoatFill, NativeMutationFootprints.TileManagerRva, 7), out mapped, out owner, out reason) && mapped.Count == 9,
                "Chore41 progresszero remains terminal after signed decrement");
            // The size14 block is live adjacent data, not an initialized square. Native duplicates
            // and an arbitrary valid far offset must be preserved, with no invented14×14 area.
            int spill = 0x8DDCC0 + 14 * 169 * 24;
            for (int i = 0; i < 196; i++) { memory.Words[spill + i * 24] = 0; memory.Words[spill + i * 24 + 4] = 0; }
            memory.Words[spill + 195 * 24] = 40; memory.Words[spill + 195 * 24 + 4] = 20;
            context = adapter.ContextForOfflineFixture();
            closure = context.CompleteCleanupFootprint(30, 30, 10);
            Require(Contains(closure, 68, 48) && !Contains(closure, 41, 41), "size14 reads exact196live records including distant tail");
            memory.Words[spill + 195 * 24] = 50;
            Require(context.SquareOffset(14, 195).X == 40, "live spill snapshot retained within qualified invocation");
            context = adapter.ContextForOfflineFixture();
            Require(context.SquareOffset(14, 195).X == 50, "later invocation refreshes mutable live spill");
            var workBudget = new RescueFootprintBudget(2048, 131072);
            var measured = new NativeMutationFootprints(memory, workBudget);
            Require(measured.TryPlanAivFieldsForOfflineFixture(eMappers.MAPPER_DRAWBRIDGE, 0, new[] { Memory.Tile(30, 30) }, out mapped, out var bridgeMetrics, out reason), "AIV bridge cleanup mapping " + reason);
            Require(measured.TryPlanAivFieldsForOfflineFixture(eMappers.MAPPER_MOAT, 0, new[] { Memory.Tile(70, 70) }, out mapped, out var moatMetrics, out reason) && mapped.Count == 9,
                "AIV Moat and high-ground KillingPit alternative share local envelope " + reason);
            Require(measured.TryPlanAivFieldsForOfflineFixture(eMappers.MAPPER_STAIR6, 0, new[] { Memory.Tile(70, 70) }, out mapped, out _, out reason) && mapped.Count == 25,
                "single-tile AIV stair frame includes earlier cleanup " + reason);
            Require(measured.TryPlanAivFieldsForOfflineFixture(eMappers.MAPPER_MOAT, 0, Array.Empty<int>(), out mapped, out _, out reason) && mapped.Count == 0,
                "empty ordered AIV frame performs no terrain work");
            Require(adapter.TryPlan(Mutation(WorldMutationSource.EditorHeightEven, NativeMutationFootprints.TileManagerRva, Memory.Tile(30, 30), 30, 0),
                out mapped, out owner, out reason) && mapped.Count == 0, "native zero-count brush performs no terrain work");
            Require(measured.TryPlanAivFieldsForOfflineFixture(eMappers.MAPPER_WHEATFARM, 0, new[] { Memory.Tile(70, 30) }, out mapped, out var farmMetrics, out reason), "AIV farm post-cleanup BFS predicate admitted " + reason);
            // Mark a remote building for terminal cleanup before a keep replacement.
            memory.Words[Memory.Record(4) + 0xD0] = 3;
            Require(measured.TryPlanAivFieldsForOfflineFixture(eMappers.MAPPER_KEEP3, 6, new[] { Memory.Tile(40, 40) }, out mapped, out var keepMetrics, out reason) && Contains(mapped, 55, 20),
                "keep includes remote preexistingAlive3 cleanup and allorientations " + reason);
            Require(keepMetrics.BuildingSlots == 3999 && keepMetrics.BrushCacheHits > 0, "replacement scans capacityonce and reuses exactbrushes");
            int lowWork = 1, highWork = 131072;
            while (lowWork < highWork)
            {
                int candidate = lowWork + (highWork - lowWork) / 2;
                bool admitted = new NativeMutationFootprints(memory, new RescueFootprintBudget(2048, candidate)).TryPlanAivFieldsForOfflineFixture(
                    eMappers.MAPPER_KEEP3, 6, new[] { Memory.Tile(40, 40) }, out _, out _, out _);
                if (admitted) highWork = candidate; else lowWork = candidate + 1;
            }
            Console.WriteLine("Largest keep minimum fixed work budget=" + lowWork + "; observed union tiles=" + mapped.Count);
            Require(lowWork > 32768 && lowWork <= 131072, "measured real keep table coverage fits chosen finite budget");
            foreach (var keepType in new[] { eMappers.MAPPER_KEEP1, eMappers.MAPPER_KEEP2, eMappers.MAPPER_KEEP3 })
                for (int rotation = 0; rotation < 8; rotation += 2)
                    Require(measured.TryPlanAivFieldsForOfflineFixture(keepType, rotation, new[] { Memory.Tile(40, 40) }, out mapped, out _, out reason),
                        "normal keep/all Planner input rotations " + keepType + ": " + reason);
            foreach (var legacy in new[] { eMappers.MAPPER_KEEP4, eMappers.MAPPER_KEEP5 })
                Require(!adapter.TryPlan(Mutation(WorldMutationSource.BuildStructure, NativeMutationFootprints.TileManagerRva, 1, 40, 40, (int)legacy, 7, 0, 0),
                    out mapped, out owner, out reason) && mapped.Count == 0 && reason == "unsupported-effective-native-mapper", "legacy SDK-only keep has nativeNull mapping");
            Console.WriteLine("Footprint cost: bridge reads=" + bridgeMetrics.NativeReads + "; moat reads=" + moatMetrics.NativeReads + "; farm reads=" + farmMetrics.NativeReads +
                "; keep reads=" + keepMetrics.NativeReads + "; keep buildingSlots=" + keepMetrics.BuildingSlots + "; keep brushSearches=" + keepMetrics.BrushSearches + "; keep cacheHits=" + keepMetrics.BrushCacheHits + "; keep nodes=" + keepMetrics.BrushNodes);
            Console.WriteLine("NativeMutationFootprintsFixture: " + checks + " checks passed.");
        }
        private static WorldMutationContext Mutation(WorldMutationSource source, params long[] args)
        { return (WorldMutationContext)Activator.CreateInstance(typeof(WorldMutationContext), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { source, 1L, 0L, 1L, 7L, 9L, args }, null); }
        private static bool Contains(IList<RescueTile> cells, int x, int y)
        { foreach (var cell in cells) if (cell.X == x && cell.Y == y) return true; return false; }
        private static void Require(bool condition, string detail)
        { checks++; if (!condition) throw new Exception("NativeMutationFootprintsFixture: " + detail); }
        private sealed class Memory : IRescueFootprintMemory
        {
            internal readonly Dictionary<int, int> Words = new Dictionary<int, int>();
            internal readonly Dictionary<int, int> Layer = new Dictionary<int, int>();
            internal readonly Dictionary<int, int> Flags = new Dictionary<int, int>();
            internal readonly HashSet<int> Inactive = new HashSet<int>();
            internal int BuildingReads;
            internal int ReadCount;
            internal static int Row(int y) => y * 100 + y * y;
            internal static int Tile(int x, int y) => Row(y) + x;
            internal static int Record(int id) => 0x64CCC0C + id * 0x32C;
            internal Memory()
            {
                Words[NativeMutationFootprints.BuildingManagerRva + 0x50] = 1;
                int[] count = { 0, 1, 5, 13, 25, 41, 85, 145, 313, 545 };
                for (int i = 0; i < count.Length; i++) Words[0x2D1C38 + i * 4] = count[i];
                int[] points = { 2, 0, 2, 1, 2, 2, 2, 3, 2, 4, 0, 2, 1, 2, 3, 2, 4, 2 };
                for (int i = 0; i < points.Length; i++) Words[0x2D3060 + i * 4] = points[i];
                for (int size = 1; size <= 13; size++) for (int i = 0; i < size * size; i++)
                { Words[0x8DDCC0 + (size * 169 + i) * 24] = i / size; Words[0x8DDCC4 + (size * 169 + i) * 24] = i % size; }
                Words[0x2D2E54] = -1; Words[0x2D2E54 + 2 * 8] = 0; Words[0x2D2E54 + 4 * 8] = 1; Words[0x2D2E54 + 6 * 8] = 0;
                // Exact static table contents independently read from the audited canonical PE.
                int[] doors = { 2,7,3,7,4,7,7,4,7,3,7,2,4,-1,3,-1,2,-1,-1,2,-1,3,-1,4,2,7,3,7,4,7,7,4,7,3,7,2,4,-1,3,-1,2,-1,-1,2,-1,3,-1,4,4,11,5,11,6,11,11,6,11,5,11,4,6,-1,5,-1,4,-1,-1,4,-1,5,-1,6 };
                int[] camps = { 0,8,8,0,0,-8,-8,0,0,8,8,0,0,-8,-8,0,2,12,12,2,2,-12,-12,2 };
                int[] goods = { 7,2,2,-5,-5,0,0,7,7,2,2,-5,-5,0,0,7,11,6,6,-5,-5,0,0,11 };
                for (int i = 0; i < doors.Length; i++) Words[0x2D30F0 + i * 4] = doors[i];
                for (int i = 0; i < camps.Length; i++) Words[0x2D3210 + i * 4] = camps[i];
                for (int i = 0; i < goods.Length; i++) Words[0x2D3270 + i * 4] = goods[i];
            }
            internal void AddBuilding(int id, eStructs type, int x, int y, int size, int compound)
            {
                int rva = Record(id);
                Words[rva + 0xD0] = 2; Words[rva + 0xD2] = (int)type; Words[rva + 0xD6] = 1; Words[rva + 0xD8] = id + 100;
                Words[rva + 0xEE] = x; Words[rva + 0xF0] = y; Words[rva + 0xF8] = size; Words[rva + 0x2A8] = compound;
                Words[NativeMutationFootprints.BuildingManagerRva + 0x50] = id + 1;
            }
            public int Int32(int rva)
            {
                ReadCount++;
                if (Words.TryGetValue(rva, out int value)) return value;
                if (rva >= 0x402FF2C && rva < 0x402FF2C + 800 * 12 && (rva - 0x402FF2C) % 12 == 0) return Row((rva - 0x402FF2C) / 12);
                if (rva >= NativeMutationFootprints.TileManagerRva && rva < NativeMutationFootprints.TileManagerRva + 800 * 32)
                {
                    int index = rva - NativeMutationFootprints.TileManagerRva, y = index / 32, direction = index % 32 / 4;
                    return direction == 0 ? Row(y - 1) - Row(y) : direction == 2 ? 1 : direction == 4 ? Row(y + 1) - Row(y) : direction == 6 ? -1 : throw new Exception("unexpected diagonal read");
                }
                if (rva >= 0x48F71B0 && rva < 0x48F71B0 + 320800 * 4) return Flags.TryGetValue((rva - 0x48F71B0) / 4, out value) ? value : 0;
                return 0;
            }
            public short Int16(int rva)
            {
                ReadCount++;
                if (rva >= 0x64CCC0C && rva < 0x64CCC0C + 4000 * 0x32C) BuildingReads++;
                if (Words.TryGetValue(rva, out int value)) return checked((short)value);
                if (rva >= 0x4B6AA50 && rva < 0x4B6AA50 + 320800 * 2) return Layer.TryGetValue((rva - 0x4B6AA50) / 2, out value) ? checked((short)value) : (short)0;
                if (rva >= 0x3AAE2A4 && rva < 0x3AAE2A4 + 320800 * 2)
                { int tile = (rva - 0x3AAE2A4) / 2; for (short y = 0; y < 800; y++) if (tile >= Row(y) && tile < Row(y) + 100) return y; throw new Exception("invalid packed tile"); }
                return 0;
            }
            public byte Byte(int rva)
            {
                ReadCount++;
                if (rva >= 0x3A11EA4 && rva < 0x3A11EA4 + 800 * 800)
                { int xy = rva - 0x3A11EA4, x = xy % 800, y = xy / 800; return x < 100 && y < 100 && !Inactive.Contains(Tile(x, y)) ? (byte)1 : (byte)0; }
                return Words.TryGetValue(rva, out int value) ? checked((byte)value) : (byte)0;
            }
        }
    }
}
