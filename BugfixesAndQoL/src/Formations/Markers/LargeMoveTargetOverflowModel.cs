using System;

namespace BugfixesAndQoL
{
    internal readonly struct LargeMoveTargetOverflowRecord
    {
        public LargeMoveTargetOverflowRecord(
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int tileId,
            int flags,
            int next)
        {
            Category = category;
            SpriteId = spriteId;
            Layer = layer;
            VerticalOffset = verticalOffset;
            TileId = tileId;
            Flags = flags;
            Next = next;
        }

        public int Category { get; }
        public int SpriteId { get; }
        public int Layer { get; }
        public int VerticalOffset { get; }
        public int TileId { get; }
        public int Flags { get; }
        public int Next { get; }
    }

    internal sealed class LargeMoveTargetOverflowBuffer
    {
        private readonly int[] headByTile =
            new int[LargeMoveTargetOverflowModel.NativeTileCount];
        private readonly LargeMoveTargetOverflowRecord[] records =
            new LargeMoveTargetOverflowRecord[LargeMoveTargetOverflowModel.MaximumOverflowMarkers + 1];
        private readonly int[] touchedTiles =
            new int[LargeMoveTargetOverflowModel.MaximumOverflowMarkers];
        private int touchedTileCount;

        public int Count { get; private set; }

        public int GetHead(int tileId) =>
            (uint)tileId < LargeMoveTargetOverflowModel.NativeTileCount
                ? headByTile[tileId]
                : 0;

        public LargeMoveTargetOverflowRecord GetRecord(int index) => records[index];

        public bool TryAdd(
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int tileId,
            int flags)
        {
            if ((uint)tileId >= LargeMoveTargetOverflowModel.NativeTileCount)
                return false;

            int head = headByTile[tileId];
            for (int index = head; index != 0; index = records[index].Next)
            {
                LargeMoveTargetOverflowRecord record = records[index];
                if (record.Category == category && record.SpriteId == spriteId)
                    return true;
            }

            if (Count >= LargeMoveTargetOverflowModel.MaximumOverflowMarkers)
                return false;

            if (head == 0)
                touchedTiles[touchedTileCount++] = tileId;
            int recordIndex = ++Count;
            records[recordIndex] = new LargeMoveTargetOverflowRecord(
                category,
                spriteId,
                layer,
                verticalOffset,
                tileId,
                flags,
                head);
            headByTile[tileId] = recordIndex;
            return true;
        }

        public void Clear()
        {
            for (int index = 0; index < touchedTileCount; index++)
                headByTile[touchedTiles[index]] = 0;
            touchedTileCount = 0;
            Count = 0;
        }
    }

    internal static class LargeMoveTargetOverflowModel
    {
        public const int NativeDrawCapacity = 0xFA;
        public const int NativeUsableDrawRecords = NativeDrawCapacity - 1;
        public const int FirstSyntheticIdentity = NativeDrawCapacity;
        public const int NativeMode8IdentityCapacity = 4250;
        public const int MaximumOverflowMarkers =
            NativeMode8IdentityCapacity - FirstSyntheticIdentity;
        public const int NativeTileCount = 320800;

        public static bool IsVanillaMoveTargetMarker(
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int flags)
        {
            return category == 0x6B && spriteId >= 0x52 && spriteId <= 0x59 &&
                layer == 0xC && verticalOffset == 6 && (flags & 0x1FFFF) == 2;
        }

        public static bool IsRejectedByFullVanillaList(int drawCountBeforeOriginal) =>
            drawCountBeforeOriginal >= NativeDrawCapacity;

        public static int GetOverflowCount(int requestedMoveMarkers) =>
            Math.Max(0, requestedMoveMarkers - NativeUsableDrawRecords);
    }
}
