using System;
using System.Collections.Generic;
using SHCDESE.Interop;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    internal enum RescueMutationSource { Building, AivBuilding, AiWallTile, HumanWallLine, MoatDig, MoatFill, EditorBrush, Demolition, EditorDelete }
    internal sealed class RescueMutationFootprintRequest
    {
        internal RescueMutationSource Source;
        internal eStructs Structure;
        internal eMappers Mapper;
        internal int X, Y, EndX, EndY, Size, Direction, BrushWidth, ReplacedBuildingId;
        // Set only when the caller has proved that preliminary cleanup preserves BFS bits 0x30.
        internal bool CleanupPreservesBrushFlags;
        // 6FE90 operationFlags=0x40000000 uses centered 69850 offsets instead of E3360.
        internal bool CenteredSquareBrush;
    }
    internal readonly struct RescueFootprintOffset
    {
        internal readonly int X, Y;
        internal RescueFootprintOffset(int x, int y) { X = x; Y = y; }
    }
    internal readonly struct RescueBrushOrigin : IEquatable<RescueBrushOrigin>
    {
        private readonly int x, y;
        internal RescueBrushOrigin(int x, int y) { this.x = x; this.y = y; }
        public bool Equals(RescueBrushOrigin other) => x == other.x && y == other.y;
        public override bool Equals(object other) => other is RescueBrushOrigin origin && Equals(origin);
        public override int GetHashCode() => unchecked(x * 397 ^ y);
    }
    internal delegate bool RescueFootprintNeighbor(RescueTile tile, int direction, out RescueTile next);
    internal sealed class RescueFootprintContext
    {
        internal Func<int, int, int> TileAt, RawTileAt;
        internal Func<int, uint> Flags;
        internal RescueFootprintNeighbor Neighbor;
        // Hash-bound tables only; this must not invoke native scratch-mutating enumerators.
        internal Func<int, int> TableInt;
        internal Func<int, int, RescueFootprintOffset> SquareOffset;
        // Complete, immutable linked-removal closure including subsidiary fields/plots.
        internal Func<int, IList<RescueTile>> CompleteRemovalFootprint;
        // All removals reachable from BOTH cleanup passes, including linked/auxiliary buildings.
        internal Func<int, int, int, IList<RescueTile>> CompleteCleanupFootprint;
        // Opt-in only for one synchronous immutable predicate view. Never reuse across mutations.
        internal Dictionary<RescueBrushOrigin, List<RescueTile>> BrushCache;
        internal int BrushCacheHits, BrushSearches, BrushNodes;
    }
    internal readonly struct RescueFootprintBudget
    {
        internal readonly int MaximumTiles, MaximumWork, TileCapacity;
        internal RescueFootprintBudget(int maximumTiles, int maximumWork, int tileCapacity = 320800)
        {
            if (maximumTiles < 1 || maximumWork < 1 || tileCapacity < 1) throw new ArgumentOutOfRangeException(nameof(maximumTiles));
            MaximumTiles = maximumTiles; MaximumWork = maximumWork; TileCapacity = tileCapacity;
        }
    }

    // A conservative observation envelope, never evidence that any geometry actually changed.
    // RVAs/strides below belong to the audited FBCB9319 native image; context admission owns its hash.
    internal static class RescueMutationFootprints
    {
        internal static bool TryPlan(RescueMutationFootprintRequest request, RescueFootprintContext context,
            RescueFootprintBudget budget, out List<RescueTile> tiles, out string reason)
        {
            tiles = new List<RescueTile>(); reason = null;
            if (request == null || context == null || context.TileAt == null || budget.MaximumTiles < 1 || budget.MaximumWork < 1 || budget.TileCapacity < 1)
            { reason = "invalid-footprint-input"; return false; }
            try
            {
                var plan = new Planner(context, budget);
                plan.Build(request);
                tiles.AddRange(plan.Tiles.Values);
                tiles.Sort((a, b) => a.TileId.CompareTo(b.TileId));
                return true;
            }
            catch (FootprintRejected ex) { reason = ex.Message; return false; }
            catch (Exception) { reason = "footprint-reader-failed"; return false; }
        }
        private sealed class FootprintRejected : Exception { internal FootprintRejected(string reason) : base(reason) { } }
        private sealed class Planner
        {
            internal readonly Dictionary<int, RescueTile> Tiles = new Dictionary<int, RescueTile>();
            private readonly RescueFootprintContext context;
            private readonly RescueFootprintBudget budget;
            private int work;
            internal Planner(RescueFootprintContext context, RescueFootprintBudget budget) { this.context = context; this.budget = budget; }
            private void Spend() { if (++work > budget.MaximumWork) Reject("footprint-work-budget"); }
            private static void Reject(string reason) { throw new FootprintRejected(reason); }
            private void Add(int x, int y)
            {
                Spend();
                int id = context.TileAt(x, y);
                if (id < 0) return; // Clip to active map; raw BFS still includes inactive neighbours.
                if (id >= budget.TileCapacity) Reject("footprint-invalid-tile");
                if (Tiles.TryGetValue(id, out var old))
                { if (old.X != x || old.Y != y) Reject("footprint-coordinate-alias"); return; }
                if (Tiles.Count == budget.MaximumTiles) Reject("footprint-tile-budget");
                Tiles.Add(id, new RescueTile(x, y, id));
            }
            private void Rectangle(int x, int y, int width, int height)
            {
                if (width < 1 || height < 1 || (long)width * height > budget.MaximumWork - work) Reject("footprint-work-budget");
                for (int dy = 0; dy < height; dy++) for (int dx = 0; dx < width; dx++) Add(checked(x + dx), checked(y + dy));
            }
            private List<RescueFootprintOffset> Square(int x, int y, int size, bool nativeCleanupSpill = false)
            {
                if (size < 1 || size > (nativeCleanupSpill ? 14 : 13) || nativeCleanupSpill && size != 14 || context.SquareOffset == null) Reject("footprint-square-contract");
                var offsets = new List<RescueFootprintOffset>();
                var seen = new HashSet<long>();
                for (int i = 0; i < size * size; i++)
                {
                    Spend(); var offset = context.SquareOffset(size, i);
                    if (!nativeCleanupSpill && (offset.X < 0 || offset.X >= size || offset.Y < 0 || offset.Y >= size || !seen.Add(((long)offset.X << 32) | (uint)offset.Y))) Reject("footprint-square-contract");
                    if (nativeCleanupSpill && ((long)x + offset.X < 0 || (long)x + offset.X >= 800 || (long)y + offset.Y < 0 || (long)y + offset.Y >= 800)) Reject("footprint-native-spill-coordinate-contract");
                    offsets.Add(offset); Add(checked(x + offset.X), checked(y + offset.Y));
                }
                return offsets;
            }
            private int Table(int rva)
            { if (context.TableInt == null) Reject("footprint-table-unavailable"); Spend(); return context.TableInt(rva); }
            private RescueFootprintOffset Offset(int rva) { return new RescueFootprintOffset(Table(rva), Table(rva + 4)); }
            private void TableTiles(int x, int y, int rva, int count, int stride)
            { for (int i = 0; i < count; i++) { var p = Offset(rva + i * stride); Add(checked(x + p.X), checked(y + p.Y)); } }
            private void Child(int x, int y, int rva, int size)
            { var p = Offset(rva); Square(checked(x + p.X), checked(y + p.Y), size); }
            private void Removal(int id)
            {
                if (id == 0) return;
                if (id < 0 || context.CompleteRemovalFootprint == null) Reject("footprint-removal-closure-unavailable");
                Spend(); var closure = context.CompleteRemovalFootprint(id);
                if (closure == null || closure.Count > budget.MaximumWork - work) Reject("footprint-removal-closure-unavailable");
                foreach (var t in closure)
                { if (context.TileAt(t.X, t.Y) != t.TileId) Reject("footprint-removal-tile-invalid"); Add(t.X, t.Y); }
            }
            private void Cleanup(RescueMutationFootprintRequest r)
            {
                if (!r.CleanupPreservesBrushFlags || context.CompleteCleanupFootprint == null) Reject("footprint-aiv-cleanup-contract-required");
                Spend(); var closure = context.CompleteCleanupFootprint(r.X, r.Y, r.Size);
                if (closure == null || closure.Count > budget.MaximumWork - work) Reject("footprint-cleanup-closure-unavailable");
                foreach (var t in closure)
                { if (context.TileAt(t.X, t.Y) != t.TileId) Reject("footprint-removal-tile-invalid"); Add(t.X, t.Y); }
                Square(r.X, r.Y, r.Size);
                if (r.Size <= 11) { int halo = r.Size == 11 ? 1 : 2; int side = r.Size == 11 ? 13 : r.Size + 4; Square(checked(r.X - halo), checked(r.Y - halo), side, side == 14); }
            }
            private void Brush(int x, int y, int width, bool centeredSquare = false)
            {
                if (width == 0) return; // Native count-table entry0 is zero: no surface writes.
                if (width < 1 || width > 9) Reject("footprint-brush-contract");
                int count = Table(0x2D1C38 + width * 4);
                if (count < 1 || count > 545) Reject("footprint-brush-count-contract");
                if (centeredSquare)
                {
                    // Native 6FE90 still iterates the brush count, which can exceed widthÂ².
                    // Read its actual 69850 table, including those extra entries.
                    if (context.SquareOffset == null) Reject("footprint-square-contract");
                    for (int i = 0; i < count; i++)
                    { Spend(); var p = context.SquareOffset(width, i); Add(checked(x + p.X - width / 2), checked(y + p.Y - width / 2)); }
                    return;
                }
                if (context.RawTileAt == null || context.Flags == null || context.Neighbor == null) Reject("footprint-brush-reader-unavailable");
                var origin = new RescueBrushOrigin(x, y);
                if (context.BrushCache != null && context.BrushCache.TryGetValue(origin, out var cached) && cached.Count >= count)
                {
                    context.BrushCacheHits++;
                    for (int i = 0; i < count; i++) Add(cached[i].X, cached[i].Y);
                    return;
                }
                context.BrushSearches++;
                int seed = context.TileAt(x, y);
                if (seed < 0) Reject("footprint-brush-seed-inactive");
                var queue = new List<RescueTile> { new RescueTile(x, y, seed) };
                var seen = new Dictionary<int, RescueTile> { [seed] = queue[0] };
                Add(x, y);
                int discovered = 1;
                for (int i = 0; i < queue.Count && discovered < count; i++)
                {
                    Spend(); var tile = queue[i];
                    if (tile.TileId < 0 || tile.TileId >= budget.TileCapacity) Reject("footprint-brush-invalid-node");
                    if ((context.Flags(tile.TileId) & 0x30U) != 0) continue;
                    for (int direction = 0; direction < 8 && discovered < count; direction += 2)
                    {
                        Spend();
                        if (!context.Neighbor(tile, direction, out var next) || next.TileId < 0 || next.TileId >= budget.TileCapacity || context.RawTileAt(next.X, next.Y) != next.TileId)
                            Reject("footprint-brush-invalid-neighbour");
                        if (seen.TryGetValue(next.TileId, out var previous))
                        { if (previous.X != next.X || previous.Y != next.Y) Reject("footprint-brush-coordinate-alias"); continue; }
                        seen.Add(next.TileId, next); queue.Add(next); discovered++; Add(next.X, next.Y);
                    }
                }
                // E3360 returns zero/zero for missing ordinals; do not silently omit such native writes.
                if (discovered != count) Reject("footprint-brush-search-exhausted");
                context.BrushNodes += queue.Count;
                if (context.BrushCache != null) context.BrushCache[origin] = queue;
            }
            private void BrushSquare(int x, int y, int size, int width)
            { foreach (var p in Square(x, y, size)) Brush(checked(x + p.X), checked(y + p.Y), width); }
            internal void Build(RescueMutationFootprintRequest r)
            {
                // Native source coordinates are bounded before any derived footprint arithmetic.
                // 800 is the audited native XY capacity, not the current active-map size.
                if (r.X < 0 || r.X >= 800 || r.Y < 0 || r.Y >= 800) Reject("footprint-source-coordinate-contract");
                switch (r.Source)
                {
                    case RescueMutationSource.EditorDelete: Reject("intentional-unit-deletion"); break;
                    case RescueMutationSource.EditorBrush: Brush(r.X, r.Y, r.BrushWidth, r.CenteredSquareBrush); return;
                    case RescueMutationSource.Demolition:
                        if (r.ReplacedBuildingId <= 0) Reject("footprint-removal-identity-required"); Removal(r.ReplacedBuildingId); return;
                    case RescueMutationSource.MoatDig:
                    case RescueMutationSource.MoatFill:
                    case RescueMutationSource.AiWallTile: Rectangle(checked(r.X - 1), checked(r.Y - 1), 3, 3); return;
                    case RescueMutationSource.HumanWallLine:
                        if (r.EndX < 0 || r.EndX >= 800 || r.EndY < 0 || r.EndY >= 800) Reject("footprint-source-coordinate-contract");
                        int left = Math.Min(r.X, r.EndX), top = Math.Min(r.Y, r.EndY);
                        Rectangle(checked(left - 1), checked(top - 1), checked(Math.Abs(r.EndX - r.X) + 3), checked(Math.Abs(r.EndY - r.Y) + 3)); return;
                    case RescueMutationSource.AivBuilding:
                        // Cleanup can remove linked compounds not visible in its local rectangle.
                        Cleanup(r);
                        break;
                    case RescueMutationSource.Building: break;
                    default: Reject("unsupported-mutation-source"); break;
                }
                if (!Enum.IsDefined(typeof(eStructs), r.Structure) || r.Structure < eStructs.STRUCT_HOVEL || r.Structure > eStructs.STRUCT_DOCK)
                    Reject("unsupported-construction-structure");
                int orientation = r.Direction == 15 ? 0 : r.Direction / 2;
                bool directional = r.Structure == eStructs.STRUCT_BARRACKS_WOOD || r.Structure == eStructs.STRUCT_BARRACKS_STONE ||
                    r.Structure == eStructs.STRUCT_BEDOUIN_STOCKADE || r.Structure == eStructs.STRUCT_ENGINEERS_GUILD ||
                    r.Structure == eStructs.STRUCT_TUNNELLERS_GUILD || r.Structure == eStructs.STRUCT_OIL_SMELTER ||
                    r.Structure >= eStructs.STRUCT_KEEP_ONE && r.Structure <= eStructs.STRUCT_KEEP_FIVE;
                // Other creator families use this common argument as a sprite/path selector (e.g.81).
                if (directional && r.Direction != 15 && (r.Direction < 0 || r.Direction > 6 || (r.Direction & 1) != 0)) Reject("footprint-direction-contract");
                Removal(r.ReplacedBuildingId);
                Square(r.X, r.Y, r.Structure == eStructs.STRUCT_DRAWBRIDGE ? 5 : r.Size);
                switch (r.Structure)
                {
                    case eStructs.STRUCT_BARRACKS_WOOD:
                    case eStructs.STRUCT_BARRACKS_STONE:
                    case eStructs.STRUCT_BEDOUIN_STOCKADE:
                        for (int i = 0; i < 3; i++) Child(r.X, r.Y, 0x2D2E90 + orientation * 24 + i * 8, 5); break;
                    case eStructs.STRUCT_GOODS_YARD: Goods(r.X, r.Y); break;
                    case eStructs.STRUCT_QUARRY: Quarry(r); break;
                    case eStructs.STRUCT_ENGINEERS_GUILD:
                    case eStructs.STRUCT_TUNNELLERS_GUILD: Child(r.X, r.Y, 0x2D2E98 + orientation * 24, 5); break;
                    case eStructs.STRUCT_OIL_SMELTER: Child(r.X, r.Y, 0x2D2EF0 + orientation * 8, 4); break;
                    case eStructs.STRUCT_WHEATFARM:
                        Farm(r, 9, 0x2D37C0, 36, 8, 2); break;
                    case eStructs.STRUCT_HOPSFARM:
                        Farm(r, 9, 0x2D3C40, 24, 8, 2); break;
                    case eStructs.STRUCT_APPLEFARM:
                        Farm(r, 10, 0x2D3DC0, 8, 8, 1); break;
                    case eStructs.STRUCT_CATTLEFARM:
                        Farm(r, 10, 0x2D3E00, 27, 12, 4); break;
                    case eStructs.STRUCT_KEEP_ONE:
                    case eStructs.STRUCT_KEEP_TWO:
                    case eStructs.STRUCT_KEEP_THREE:
                    case eStructs.STRUCT_KEEP_FOUR:
                    case eStructs.STRUCT_KEEP_FIVE: Keep(r, orientation); break;
                    case eStructs.STRUCT_POND:
                    case eStructs.STRUCT_CESS_PIT:
                        // Union of the optional brush and both mapper-specific child pairs.
                        BrushSquare(r.X, r.Y, r.Size, 2);
                        Child(r.X, r.Y, 0x2D2FE0, r.Size); Child(r.X, r.Y, 0x2D2FE8, r.Size);
                        Child(r.X, r.Y, 0x2D3020, r.Size); Child(r.X, r.Y, 0x2D3028, r.Size); break;
                    // Remaining audited creator families only register the effective main square.
                }
            }
            private void Goods(int x, int y)
            { for (int i = 0; i < 4; i++) Child(x, y, 0x2D2FB8 + i * 8, 2); TableTiles(x, y, 0x2D3060, 9, 8); }
            private void Farm(RescueMutationFootprintRequest r, int preparation, int table, int count, int stride, int variants)
            {
                Square(r.X, r.Y, preparation);
                for (int variant = 0; variant < variants; variant++) TableTiles(r.X, r.Y, table + variant * count * stride, count, stride);
                BrushSquare(r.X, r.Y, 3, 2);
            }
            private void Keep(RescueMutationFootprintRequest r, int orientation)
            {
                int index = ((int)r.Structure - (int)eStructs.STRUCT_KEEP_ONE) * 4 + orientation;
                TableTiles(r.X, r.Y, 0x2D30F0 + index * 24, 3, 8);
                var camp = Offset(0x2D3210 + index * 8);
                int x = checked(r.X + camp.X), y = checked(r.Y + camp.Y);
                // Width six is a superset of width three in the identical BFS discovery order.
                BrushSquare(r.X, r.Y, r.Size, 6); BrushSquare(x, y, 7, 6);
                var goods = Offset(0x2D3270 + index * 8);
                x = checked(r.X + goods.X); y = checked(r.Y + goods.Y);
                Square(x, y, 5); Goods(x, y);
            }
            private void Quarry(RescueMutationFootprintRequest r)
            {
                int[] tables = { 0, 0x2E3FA0, 0x2E3FC0, 0x2E4000, 0x2E4060, 0x2E40E0, 0x2E4180, 0x2E4240, 0x2E4320, 0x2E4420, 0x2E4540, 0x2E4680, 0x2E47E0, 0x2E4960 };
                int width = r.Size, shift = 0, expansion = 0;
                for (int attempt = 0; attempt < 11; attempt++)
                {
                    int extra;
                    if (width + 2 <= 13) { width += 2; shift++; extra = 0; }
                    else extra = ++expansion;
                    for (int i = 0; i < width * 4; i++)
                    {
                        var p = Offset(tables[width] + i * 8); int x = p.X, y = p.Y;
                        switch (i / width) { case 0: y -= extra; break; case 1: x += extra; break; case 2: y += extra; break; case 3: x -= extra; break; }
                        if (x < width && y < width) { if (x < 0) x--; else if (y < 0) y--; }
                        Square(checked(r.X - shift + x), checked(r.Y - shift + y), 2);
                    }
                }
            }
        }
    }
}
