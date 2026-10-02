using System;

namespace EnemyBridgePathTest
{
    // Native 645C0 uses the 25-cell mapper at 2D1A30, indexed by orientation / 2.
    // Only its 15 nonzero cells with a moat record receive the closure flag.
    internal static class DrawbridgeClosurePolicy
    {
        internal static bool IsClosureCell(int orientation, int cellIndex)
        {
            if ((uint)orientation > 7 || (uint)cellIndex >= 25) return false;
            return ((orientation / 2) & 1) == 0
                ? cellIndex >= 5 && cellIndex < 20
                : cellIndex % 5 >= 1 && cellIndex % 5 <= 3;
        }

        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        // Coordinates are resolved through the existing packed-tile API, never by
        // rectangular tile-ID arithmetic. Non-footprint land is not isolated.
        internal static int BlockCell(byte[] masks, int tile, int x, int y,
            Func<int, int, int> getTileId, GateEdgeOwnership ownership, int gateId, int bridgeId)
        {
            if ((uint)tile >= (uint)masks.Length) return 0;
            int changed = 0;
            for (int direction = 0; direction < 8; direction++)
            {
                int nextX = x + Dx[direction], nextY = y + Dy[direction];
                if ((uint)nextX >= 800 || (uint)nextY >= 800) continue;
                int neighbor = getTileId(nextX, nextY);
                if ((uint)neighbor >= (uint)masks.Length) continue;
                changed += Clear(masks, tile, direction, ownership, gateId, bridgeId);
                changed += Clear(masks, neighbor, (direction + 4) & 7, ownership, gateId, bridgeId);
            }
            return changed;
        }

        private static int Clear(byte[] masks, int tile, int direction,
            GateEdgeOwnership ownership, int gateId, int bridgeId)
        {
            int bit = 1 << direction;
            int changed = (masks[tile] & bit) != 0 ? 1 : 0;
            masks[tile] = unchecked((byte)(masks[tile] & ~bit));
            ownership.Record(tile, direction, gateId, bridgeId);
            return changed;
        }
    }
}
