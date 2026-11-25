using System;
using System.Collections.Generic;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class MutationFootprintsFixture
    {
        private static int checks;
        internal static void Run()
        {
            checks = 0;
            var table = new Dictionary<int, int>();
            int[] counts = { 0, 1, 5, 13, 25, 41, 85, 145, 313, 545 };
            for (int i = 0; i < counts.Length; i++) table.Add(0x2D1C38 + i * 4, counts[i]);
            // Native fixture tables use nontrivial offsets so missing children/variants are visible.
            Pairs(table, 0x2D2E90, new[] { 5, 0, 0, 5, 5, 5, 0, -5, 5, 0, 5, -5, -5, 0, 0, -5, -5, -5, 0, 5, -5, 0, -5, 5 });
            var context = Context(table);
            var budget = new RescueFootprintBudget(12000, 500000);
            var request = new RescueMutationFootprintRequest { Source = RescueMutationSource.Building, Structure = eStructs.STRUCT_BARRACKS_STONE, X = 50, Y = 50, Size = 5 };
            for (int rotation = 0; rotation < 4; rotation++)
            {
                request.Direction = rotation * 2;
                Require(RescueMutationFootprints.TryPlan(request, context, budget, out var cells, out var reason), "barracks rotation " + reason);
                Require(cells.Count == 100, "three disjoint children plus root");
                for (int child = 0; child < 3; child++)
                {
                    int rva = 0x2D2E90 + rotation * 24 + child * 8;
                    Require(Contains(cells, 50 + table[rva] + 4, 50 + table[rva + 4] + 4), "rotated child far corner");
                }
            }
            request.Direction = 1;
            Require(!RescueMutationFootprints.TryPlan(request, context, budget, out var rejected, out var failure) && failure == "footprint-direction-contract" && rejected.Count == 0, "unknown rotation fails closed");
            request.Direction = 15; request.X = 0; request.Y = 0;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var clipped, out failure) && clipped.Count == 100, "default direction and active corner");
            request.X = 198; request.Y = 198;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out clipped, out failure) && clipped.Count == 4, "map clips far corner");
            request.Source = RescueMutationSource.HumanWallLine; request.X = 20; request.Y = 20; request.EndX = 24; request.EndY = 23;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var line, out failure) && line.Count == 42 && Contains(line, 19, 19) && Contains(line, 25, 24), "partial line rectangle plus update halo");
            Require(!RescueMutationFootprints.TryPlan(request, context, new RescueFootprintBudget(10, 1000), out rejected, out failure) && rejected.Count == 0 && failure == "footprint-tile-budget", "overload never returns partial envelope");
            request.EndX = int.MinValue;
            Require(!RescueMutationFootprints.TryPlan(request, context, budget, out rejected, out failure) && failure == "footprint-source-coordinate-contract", "line input cannot wrap into a small rectangle");
            request.Source = RescueMutationSource.EditorBrush; request.X = 50; request.Y = 50; request.BrushWidth = 3;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var brush, out failure) && brush.Count == 13, "native thirteen-cell brush");
            Require(Contains(brush, 50, 48) && Contains(brush, 52, 50) && !Contains(brush, 48, 48), "BFS diamond differs from width square");
            // A blocked north neighbour is still discovered but cannot expand; later ordinals go elsewhere.
            context.Flags = id => id == 49 * 200 + 50 ? 0x20U : 0;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out brush, out failure) && brush.Count == 13 && Contains(brush, 50, 49) && !Contains(brush, 50, 48), "expansion guard does not suppress discovering barrier");
            context.Flags = id => 0x30U;
            Require(!RescueMutationFootprints.TryPlan(request, context, budget, out rejected, out failure) && failure == "footprint-brush-search-exhausted", "native missing-ordinal fallback cannot be silently omitted");
            context.Flags = id => 0;
            Require(!RescueMutationFootprints.TryPlan(request, context, new RescueFootprintBudget(100, 10), out rejected, out failure) && failure == "footprint-work-budget", "BFS fixed work budget");
            request.Source = RescueMutationSource.EditorDelete;
            Require(!RescueMutationFootprints.TryPlan(request, context, budget, out rejected, out failure) && failure == "intentional-unit-deletion", "editor delete is not rescue");
            request.Source = RescueMutationSource.AivBuilding;
            Require(!RescueMutationFootprints.TryPlan(request, context, budget, out rejected, out failure) && failure == "footprint-aiv-cleanup-contract-required", "cleanup needs complete removal and flags contracts");
            request.Source = RescueMutationSource.Demolition; request.ReplacedBuildingId = 42;
            context.CompleteRemovalFootprint = id => new[] { new RescueTile(2, 3, 602), new RescueTile(99, 100, 20099) };
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var compound, out failure) && compound.Count == 2 && Contains(compound, 99, 100), "removal closure includes distant linked plot");
            request.Source = (RescueMutationSource)999;
            Require(!RescueMutationFootprints.TryPlan(request, context, budget, out rejected, out failure) && failure == "unsupported-mutation-source", "no unknown-source square fallback");
            request.Source = RescueMutationSource.Building; request.Structure = eStructs.STRUCT_WHEATFARM; request.X = 50; request.Y = 50; request.Size = 4; request.ReplacedBuildingId = 0;
            for (int variant = 0; variant < 2; variant++) for (int i = 0; i < 36; i++)
                Pairs(table, 0x2D37C0 + (variant * 36 + i) * 8, new[] { variant == 0 ? i % 6 : 15 + i % 6, i / 6 });
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var farm, out failure) && Contains(farm, 58, 58) && Contains(farm, 70, 55), "farm preparation and both table variants");
            request.Structure = eStructs.STRUCT_KEEP_THREE; request.Size = 7;
            for (int orientation = 0; orientation < 4; orientation++)
            {
                int index = 8 + orientation;
                Pairs(table, 0x2D30F0 + index * 24, new[] { -1, 2, 2, -1, 7, 2 });
                Pairs(table, 0x2D3210 + index * 8, new[] { orientation * 2, 10 });
                Pairs(table, 0x2D3270 + index * 8, new[] { 15, orientation * 2 });
            }
            Pairs(table, 0x2D2FB8, new[] { 0, 0, 3, 0, 0, 3, 3, 3 });
            Pairs(table, 0x2D3060, new[] { 2, 0, 2, 1, 2, 2, 2, 3, 2, 4, 0, 2, 1, 2, 3, 2, 4, 2 });
            for (int orientation = 0; orientation < 4; orientation++)
            {
                request.Direction = orientation * 2;
                Require(RescueMutationFootprints.TryPlan(request, context, budget, out var keep, out failure), "keep including BFS halos " + failure);
                Require(Contains(keep, 50 + orientation * 2 + 6, 66) && Contains(keep, 69, 54 + orientation * 2), "actual keep orientation chooses camp and goods");
                Require(Contains(keep, 50, 44), "keep width six BFS extends beyond main square");
            }
            request.Source = RescueMutationSource.AivBuilding; request.Structure = eStructs.STRUCT_HOVEL;
            request.Size = 11; request.CleanupPreservesBrushFlags = true;
            context.CompleteCleanupFootprint = (x, y, size) => new[] { new RescueTile(10, 10, 2010) };
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var cleanup, out failure) && Contains(cleanup, 49, 49) && Contains(cleanup, 61, 61) && Contains(cleanup, 10, 10), "size eleven cleanup special thirteen square and linked closure");
            Require(!Contains(cleanup, 48, 48) && !Contains(cleanup, 62, 62), "size eleven does not use generic two tile cleanup halo");
            request.Size = 5;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out cleanup, out failure) && Contains(cleanup, 48, 48) && Contains(cleanup, 56, 56), "ordinary cleanup two tile halo");
            request.Source = RescueMutationSource.Building; request.Structure = eStructs.STRUCT_QUARRY; request.Size = 12;
            // Width twelve cannot grow to fourteen. All eleven attempts therefore advance
            // the outward offset, independently of the actual placement rejection flags.
            int[] perimeter = { 0, 0x2E3FA0, 0x2E3FC0, 0x2E4000, 0x2E4060, 0x2E40E0, 0x2E4180, 0x2E4240, 0x2E4320, 0x2E4420, 0x2E4540, 0x2E4680, 0x2E47E0, 0x2E4960 };
            for (int width = 1; width <= 13; width++)
                for (int i = 0; i < width; i++)
                {
                    Pairs(table, perimeter[width] + i * 8, new[] { i, -1 });
                    Pairs(table, perimeter[width] + (width + i) * 8, new[] { width, i });
                    Pairs(table, perimeter[width] + (2 * width + i) * 8, new[] { i, width });
                    Pairs(table, perimeter[width] + (3 * width + i) * 8, new[] { -1, i });
                }
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var quarry, out failure), "dynamic quarry search union " + failure);
            Require(Contains(quarry, 50, 37) && Contains(quarry, 37, 50) && Contains(quarry, 74, 50) && Contains(quarry, 50, 74), "quarry includes eleventh failed-search expansion and full two tile candidate");
            request.Size = 5;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out quarry, out failure) && Contains(quarry, 50, 37) && !Contains(quarry, 50, 36), "quarry four growth shifts plus seven outward searches");
            request.Structure = (eStructs)65535;
            Require(!RescueMutationFootprints.TryPlan(request, context, budget, out rejected, out failure) && failure == "unsupported-construction-structure", "unknown structure has no square fallback");
            Pairs(table, 0x2D2EF0, new[] { 0, 4 });
            Pairs(table, 0x2D2FE0, new[] { -5, 0, 5, 0 }); Pairs(table, 0x2D3020, new[] { 0, -5, 0, 5 });
            for (int i = 0; i < 48; i++) Pairs(table, 0x2D3C40 + i * 8, new[] { i % 6, i / 6 });
            for (int i = 0; i < 8; i++) Pairs(table, 0x2D3DC0 + i * 8, new[] { i, 8 });
            for (int i = 0; i < 108; i++) Pairs(table, 0x2D3E00 + i * 12, new[] { i % 9, i / 9 });
            request.Direction = 0; request.Size = 5;
            var families = new[] {
                eStructs.STRUCT_ARMOURY, eStructs.STRUCT_HOVEL, eStructs.STRUCT_BARRACKS_WOOD,
                eStructs.STRUCT_GOODS_YARD, eStructs.STRUCT_QUARRY, eStructs.STRUCT_ENGINEERS_GUILD,
                eStructs.STRUCT_TUNNELLERS_GUILD, eStructs.STRUCT_OIL_SMELTER, eStructs.STRUCT_WHEATFARM,
                eStructs.STRUCT_HOPSFARM, eStructs.STRUCT_APPLEFARM, eStructs.STRUCT_CATTLEFARM,
                eStructs.STRUCT_RUINS, eStructs.STRUCT_KEEP_THREE, eStructs.STRUCT_GATE_MAIN,
                eStructs.STRUCT_GATE_INNER, eStructs.STRUCT_GATE_WOOD, eStructs.STRUCT_DRAWBRIDGE,
                eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA, eStructs.STRUCT_MAYPOLE, eStructs.STRUCT_GARDEN,
                eStructs.STRUCT_KILLING_PIT, eStructs.STRUCT_PITCH_DITCH, eStructs.STRUCT_TOWER1,
                eStructs.STRUCT_CESS_PIT, eStructs.STRUCT_POND, eStructs.STRUCT_DOCK };
            foreach (var family in families)
            {
                request.Structure = family;
                Require(RescueMutationFootprints.TryPlan(request, context, budget, out var familyCells, out failure) && familyCells.Count != 0, "audited creator dispatch " + family + ": " + failure);
            }
            request.Structure = eStructs.STRUCT_GATE_MAIN; request.Direction = 81;
            Require(RescueMutationFootprints.TryPlan(request, context, budget, out var gateSelector, out failure) && gateSelector.Count == 25,
                "non-directional creator retains native path selector81");
            Console.WriteLine("MutationFootprintsFixture: " + checks + " checks passed.");
        }
        private static RescueFootprintContext Context(Dictionary<int, int> table)
        {
            return new RescueFootprintContext
            {
                TileAt = (x, y) => x >= 0 && y >= 0 && x < 200 && y < 200 ? y * 200 + x : -1,
                RawTileAt = (x, y) => x >= 0 && y >= 0 && x < 200 && y < 200 ? y * 200 + x : -1,
                Flags = id => 0,
                SquareOffset = (size, index) => new RescueFootprintOffset(index % size, index / size),
                TableInt = rva => table[rva],
                Neighbor = (RescueTile t, int dir, out RescueTile next) =>
                {
                    int x = t.X + (dir == 2 ? 1 : dir == 6 ? -1 : 0), y = t.Y + (dir == 4 ? 1 : dir == 0 ? -1 : 0);
                    next = new RescueTile(x, y, y * 200 + x); return x >= 0 && y >= 0 && x < 200 && y < 200;
                }
            };
        }
        private static void Pairs(Dictionary<int, int> table, int rva, int[] values)
        { for (int i = 0; i < values.Length; i++) table[rva + i * 4] = values[i]; }
        private static bool Contains(List<RescueTile> cells, int x, int y)
        { return cells.Exists(t => t.X == x && t.Y == y); }
        private static void Require(bool condition, string detail)
        { checks++; if (!condition) throw new Exception("MutationFootprintsFixture: " + detail); }
    }
}
