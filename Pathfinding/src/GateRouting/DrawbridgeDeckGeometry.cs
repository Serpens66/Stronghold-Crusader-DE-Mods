using System;
using System.Collections.Generic;

namespace Pathfinding.GateRouting
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

    // Abstraction over the packed tile grids, so the closure is testable without the game.
    internal interface IDeckGrid
    {
        byte GetDirections(int tile);
        void SetDirections(int tile, byte value);
        bool TryGetCoordinates(int tile, out int x, out int y);
        bool TryGetTile(int x, int y, out int tile);
    }

    // Remembers the first original value of every edited direction byte and restores
    // exactly those bytes. Nothing else may write the grid inside a closure window
    // (audited callers of 0xC4BF0 only read directions between closing and restoring).
    internal sealed class DirectionJournal
    {
        private readonly int[] stamp;
        private readonly byte[] original;
        private readonly List<int> touched = new List<int>(256);
        private int generation = 1;
        internal DirectionJournal(int capacity) { stamp = new int[capacity]; original = new byte[capacity]; }
        internal int Count => touched.Count;
        internal void Write(IDeckGrid grid, int tile, byte value)
        {
            if ((uint)tile >= (uint)stamp.Length) throw new ArgumentOutOfRangeException(nameof(tile));
            if (stamp[tile] != generation) { stamp[tile] = generation; original[tile] = grid.GetDirections(tile); touched.Add(tile); }
            grid.SetDirections(tile, value);
        }
        internal void Restore(IDeckGrid grid)
        {
            for (int i = touched.Count - 1; i >= 0; i--) grid.SetDirections(touched[i], original[touched[i]]);
            touched.Clear();
            if (++generation == int.MaxValue) { Array.Clear(stamp, 0, stamp.Length); generation = 1; }
        }
    }

    internal static class DeckClosureCore
    {
        private static readonly int[] SideOffsets = { 0, 1, -1 };

        // Emulates the connectivity of a raised deck: every edge between a deck cell and a
        // non-deck neighbour is removed in both directions. Edges between deck cells stay,
        // so the deck forms its own island instead of component zero.
        internal static int CutDeck(IDeckGrid grid, IReadOnlyList<int> deck, Func<int, bool> isDeck, DirectionJournal journal)
        {
            int removed = 0;
            for (int i = 0; i < deck.Count; i++)
            {
                int cell = deck[i];
                if (!grid.TryGetCoordinates(cell, out int x, out int y)) continue;
                for (int d = 0; d < 8; d++)
                {
                    if (!grid.TryGetTile(x + DirectionBits.Dx[d], y + DirectionBits.Dy[d], out int neighbour) || isDeck(neighbour)) continue;
                    byte own = grid.GetDirections(cell);
                    if ((own & DirectionBits.Bit[d]) != 0) { journal.Write(grid, cell, (byte)(own & ~DirectionBits.Bit[d])); removed++; }
                    int back = DirectionBits.Opposite(d);
                    byte other = grid.GetDirections(neighbour);
                    if ((other & DirectionBits.Bit[back]) != 0) { journal.Write(grid, neighbour, (byte)(other & ~DirectionBits.Bit[back])); removed++; }
                }
            }
            return removed;
        }

        // Re-adds the gate aperture edge between the deck tile in front of the gate and the
        // gate's own edge tile, with the values Vanilla computes for an open gate. The deck
        // island thereby joins the gate corridor component.
        internal static int Link(IDeckGrid grid, int outside, int inside, bool outsideToInside, bool insideToOutside, DirectionJournal journal)
        {
            if (!grid.TryGetCoordinates(outside, out int ox, out int oy) || !grid.TryGetCoordinates(inside, out int ix, out int iy)) return 0;
            int d = DirectionBits.FromDelta(ix - ox, iy - oy);
            if (d < 0) return 0;
            int added = 0;
            if (outsideToInside) { journal.Write(grid, outside, (byte)(grid.GetDirections(outside) | DirectionBits.Bit[d])); added++; }
            if (insideToOutside) { journal.Write(grid, inside, (byte)(grid.GetDirections(inside) | DirectionBits.Bit[DirectionBits.Opposite(d)])); added++; }
            return added;
        }

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

        // Follow live, reciprocal edges within the footprint and across its boundary.
        // An abutment can itself belong to a land region separated by the planning cut.
        internal static bool CollectLandingRegions(IDeckGrid grid, int outside, int inside, int corridor,
            Func<int, bool> onBridge, Func<int, bool> isDeck,
            Func<int, bool> otherConnection, Func<int, int> component, Span<int> footprint, Span<int> regions)
        {
            int count = 1, regionCount = 0;
            bool reachedDeck = false;
            footprint[0] = outside;
            for (int index = 0; index < count; index++)
            {
                int tile = footprint[index];
                reachedDeck |= isDeck(tile);
                if (!grid.TryGetCoordinates(tile, out int x, out int y)) return false;
                byte directions = grid.GetDirections(tile);
                for (int d = 0; d < 8; d++)
                {
                    if ((directions & DirectionBits.Bit[d]) == 0 ||
                        !grid.TryGetTile(x + DirectionBits.Dx[d], y + DirectionBits.Dy[d], out int next) ||
                        (grid.GetDirections(next) & DirectionBits.Bit[DirectionBits.Opposite(d)]) == 0) continue;

                    int region = component(next);
                    if (region == 0) continue;
                    if (onBridge(next))
                    {
                        if (footprint.Slice(0, count).IndexOf(next) >= 0) continue;
                        if (count == footprint.Length) return false;
                        footprint[count++] = next;
                    }
                    else if (otherConnection(next))
                    {
                        // A different bridge or gate requires its own access decision.
                        if (next != inside) return false;
                        continue;
                    }

                    if (region == corridor || regions.Slice(0, regionCount).IndexOf(region) >= 0) continue;
                    if (regionCount == regions.Length) return false;
                    regions[regionCount++] = region;
                }
            }
            return reachedDeck;
        }

        internal static bool TryFitPortalRegions(ReadOnlySpan<int> first, ReadOnlySpan<int> second, int corridor,
            out int a, out int b, out int c)
        {
            foreach (int entry in first[1] == 0 ? first.Slice(0, 1) : first)
            {
                foreach (int exit in second[1] == 0 ? second.Slice(0, 1) : second)
                {
                    int extra = 0;
                    if (CollectExtraRegion(first, entry, exit, corridor, ref extra) &&
                        CollectExtraRegion(second, entry, exit, corridor, ref extra) &&
                        (extra == 0 || corridor == entry || corridor == exit))
                    {
                        a = entry;
                        b = exit;
                        c = extra == 0 ? corridor : extra;
                        return true;
                    }
                }
            }
            a = b = c = 0;
            return false;
        }

        private static bool CollectExtraRegion(ReadOnlySpan<int> regions, int a, int b, int c, ref int extra)
        {
            foreach (int region in regions)
            {
                if (region == 0 || region == a || region == b || region == c) continue;
                if (extra != 0 && extra != region) return false;
                extra = region;
            }
            return true;
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
