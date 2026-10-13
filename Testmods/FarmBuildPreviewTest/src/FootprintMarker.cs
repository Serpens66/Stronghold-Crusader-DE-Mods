using System;
using SHCDESE.Interop;

namespace FarmBuildPreviewTest
{
    internal readonly struct MarkResult
    {
        internal readonly int Cells, Eligible, Changed;
        internal readonly GroundCounts Counts;
        internal int Coverage => Counts.Coverage;
        internal MarkResult(int cells, GroundCounts counts, int eligible, int changed)
        { Cells = cells; Counts = counts; Eligible = eligible; Changed = changed; }
    }

    internal static class FootprintMarker
    {
        internal static unsafe MarkResult Apply(ulong imageBase, IntPtr manager, int x, int y, int scale, eMappers mapper = eMappers.MAPPER_WHEATFARM, bool blocked = true)
        {
            if (scale != FarmPreviewPolicy.Scale(mapper) || scale == 0) throw new ArgumentException("Unsupported farm footprint.");
            int count = scale * scale;
            Span<int> indices = stackalloc int[count];
            Span<int> logic = stackalloc int[count];
            Span<byte> terrain = stackalloc byte[count];
            Span<int> preview = stackalloc int[count];
            var view = new GameTileManagerView((ulong)manager.ToInt64());
            int eligibleTiles = 0;
            // Validate the entire footprint before the first write. Native XY and packed indices
            // are different spaces; use the exact native validity mask and row-prefix table.
            for (int i = 0; i < count; i++)
            {
                long tileX = (long)x + i % scale, tileY = (long)y + i / scale;
                if ((ulong)tileX >= GameTileManagerView.PackedMapAxisCapacity ||
                    (ulong)tileY >= GameTileManagerView.PackedMapAxisCapacity)
                    throw new InvalidOperationException("Blocked preview has an out-of-grid footprint.");
                if (*(byte*)(imageBase + PreviewNativeContract.ValidMapMaskRva + (ulong)(tileY * 800 + tileX)) == 0)
                    throw new InvalidOperationException("Blocked preview crosses the native map mask.");
                int index = checked(*(int*)(imageBase + PreviewNativeContract.PackedRowRva + (ulong)tileY * 12) + (int)tileX);
                if ((uint)index >= (uint)view.ConstructionGrid.Length || (uint)index >= (uint)view.LogicGrid.Length ||
                    (uint)index >= (uint)view.Logic2Grid.Length)
                    throw new InvalidOperationException("Packed footprint index exceeds native layers.");
                indices[i] = index;
                logic[i] = view.LogicGrid[index];
                terrain[i] = view.Logic2Grid[index];
                preview[i] = view.ConstructionGrid[index];

            }
            int changed = FarmPreviewPolicy.Mark(mapper, blocked, logic, terrain, preview, out GroundCounts counts);
            for (int i = 0; i < count; i++)
                if (view.ConstructionGrid[indices[i]] == FarmPreviewPolicy.DarkPreview && FarmPreviewPolicy.Missing(mapper, counts, logic[i], terrain[i])) eligibleTiles++;
            if (changed == 0) return new MarkResult(count, counts, eligibleTiles, 0);
            for (int i = 0; i < count; i++)
                if (preview[i] == FarmPreviewPolicy.RedPreview && view.ConstructionGrid[indices[i]] == FarmPreviewPolicy.DarkPreview)
                    view.ConstructionGrid[indices[i]] = FarmPreviewPolicy.RedPreview;
            return new MarkResult(count, counts, eligibleTiles, changed);
        }
    }
}
