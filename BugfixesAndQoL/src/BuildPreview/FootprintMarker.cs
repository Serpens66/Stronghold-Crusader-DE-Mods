using System;
using SHCDESE.Interop;

namespace BugfixesAndQoL.BuildPreview
{
    internal readonly struct MarkResult
    {
        internal readonly int Cells, Changed;
        internal readonly GroundCounts Counts;
        internal int Coverage => Counts.Coverage;
        internal MarkResult(int cells, GroundCounts counts, int changed)
        { Cells = cells; Counts = counts; Changed = changed; }
    }

    internal static class FootprintMarker
    {
        internal static unsafe bool TryApply(ulong imageBase, IntPtr manager, int x, int y, int scale, out MarkResult result, eMappers mapper = eMappers.MAPPER_WHEATFARM, bool blocked = true)
        {
            result = default;
            if (scale != FarmPreviewPolicy.Scale(mapper) || scale == 0) throw new ArgumentException("Unsupported farm footprint.");
            int count = scale * scale;
            Span<int> indices = stackalloc int[count];
            Span<int> logic = stackalloc int[count];
            Span<byte> terrain = stackalloc byte[count];
            Span<int> preview = stackalloc int[count];
            var view = new GameTileManagerView((ulong)manager.ToInt64());
            // Validate the entire footprint before the first write. Native XY and packed indices
            // are different spaces; use the exact native validity mask and row-prefix table.
            for (int i = 0; i < count; i++)
            {
                long tileX = (long)x + i % scale, tileY = (long)y + i / scale;
                if ((ulong)tileX >= GameTileManagerView.PackedMapAxisCapacity ||
                    (ulong)tileY >= GameTileManagerView.PackedMapAxisCapacity)
                    return false;
                if (*(byte*)(imageBase + PreviewNativeContract.ValidMapMaskRva + (ulong)(tileY * 800 + tileX)) == 0)
                    return false;
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
            result = new MarkResult(count, counts, changed);
            if (changed == 0) return true;
            for (int i = 0; i < count; i++)
                if (preview[i] == FarmPreviewPolicy.RedPreview && view.ConstructionGrid[indices[i]] == FarmPreviewPolicy.DarkPreview)
                    view.ConstructionGrid[indices[i]] = FarmPreviewPolicy.RedPreview;
            return true;
        }
    }
}
