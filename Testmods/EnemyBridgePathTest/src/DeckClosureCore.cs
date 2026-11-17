using System;
using System.Collections.Generic;

namespace EnemyBridgePathTest
{
    // Direction byte layout of the native edge grid (RVA 0x51890D0, FBCB9319), taken from
    // the flood in 0xE49D0: bit -> neighbour offset. Opposite direction = (d + 4) % 8.
    internal static class DirectionBits
    {
        internal static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        internal static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        internal static readonly byte[] Bit = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };
        internal static int Opposite(int direction) => (direction + 4) & 7;
        internal static int FromDelta(int dx, int dy)
        {
            for (int d = 0; d < 8; d++) if (Dx[d] == dx && Dy[d] == dy) return d;
            return -1;
        }
    }

    // Read-only view of the packed tile grids (independent copy of the geometry Pathfinding's
    // drawbridge closure uses; the acceptance compares the game against this oracle).
    internal interface IDeckGrid
    {
        byte GetDirections(int tile);
        bool TryGetCoordinates(int tile, out int x, out int y);
        bool TryGetTile(int x, int y, out int tile);
    }

    internal static class DeckClosureCore
    {
        private static readonly int[] SideOffsets = { 0, 1, -1 };

        // From the tile in front of the gate, follow the passage axis away from the gate to the
        // first deck cell. A 5x5 drawbridge usually has an abutment row (footprint, no deck)
        // between gate and deck; then 'abutment' is that row's axis tile, otherwise -1.
        internal static bool FindNearDeck(IDeckGrid grid, int outside, int inside, Func<int, bool> isDeck, Func<int, bool> onBridge,
            out int abutment, out int nearDeck, int maxSteps = 8)
        {
            abutment = nearDeck = -1;
            if (isDeck(outside)) { nearDeck = outside; return true; }
            if (!grid.TryGetCoordinates(outside, out int x, out int y) || !grid.TryGetCoordinates(inside, out int ix, out int iy)) return false;
            int dx = x - ix, dy = y - iy;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return false;
            int current = outside;
            for (int step = 0; step < maxSteps; step++)
            {
                if (!onBridge(current) || !grid.TryGetTile(x + dx, y + dy, out int next)) return false;
                if (isDeck(next)) { abutment = current; nearDeck = next; return true; }
                x += dx; y += dy; current = next;
            }
            return false;
        }

        // Landing tiles beyond the far end of the bridge footprint, centre first. The walk starts
        // in front of the gate and follows the gate passage axis away from the gate.
        internal static int FindLanding(IDeckGrid grid, int outside, int inside, Func<int, bool> onBridge, Func<int, int> component, int maxDeckLength = 8) =>
            FindLanding(grid, outside, inside, onBridge, component, out _, maxDeckLength);

        internal static int FindLanding(IDeckGrid grid, int outside, int inside, Func<int, bool> onBridge, Func<int, int> component, out int landingTile, int maxDeckLength = 8)
        {
            landingTile = -1;
            if (!grid.TryGetCoordinates(outside, out int x, out int y) || !grid.TryGetCoordinates(inside, out int ix, out int iy)) return 0;
            int dx = x - ix, dy = y - iy;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return 0;
            for (int step = 0; step < maxDeckLength; step++)
            {
                int nx = x + dx, ny = y + dy;
                if (!grid.TryGetTile(nx, ny, out int next)) return 0;
                if (onBridge(next)) { x = nx; y = ny; continue; }
                foreach (int side in SideOffsets)
                    if (grid.TryGetTile(nx + side * dy, ny + side * dx, out int landing) && !onBridge(landing))
                    {
                        int value = component(landing);
                        if (value > 0) { landingTile = landing; return value; }
                    }
                return 0;
            }
            return 0;
        }
    }
}
