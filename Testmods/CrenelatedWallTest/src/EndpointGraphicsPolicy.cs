using System;

namespace CrenelatedWallTest
{
    // A low managed crenel the graphics hook draws as a straight piece, so the renderer's special
    // branch never ORs the high bit into it (65EF0). Holds the axis neighbours (tile ids, any owner)
    // seen when the height was decided; a different neighbourhood makes the assignment stale.
    internal sealed class EndpointAssignment
    {
        internal readonly int Owner, Row, Count, N0, N1, N2, N3;
        internal EndpointAssignment(int owner, int row, int[] neighbors, int count)
        {
            if (count < 0 || count > 4 || (neighbors?.Length ?? 0) < count) throw new ArgumentOutOfRangeException(nameof(count));
            Owner = owner; Row = row; Count = count;
            N0 = count > 0 ? neighbors[0] : -1; N1 = count > 1 ? neighbors[1] : -1;
            N2 = count > 2 ? neighbors[2] : -1; N3 = count > 3 ? neighbors[3] : -1;
        }
        internal bool Has(int id) => id == N0 || id == N1 || id == N2 || id == N3;
    }
    internal static class EndpointGraphicsPolicy
    {
        // Pure validation of an existing logical assignment. Never chooses a height.
        internal static bool Matches(EndpointAssignment endpoint, int tile, int row, int direction,
            ReadOnlySpan<int> logic, ReadOnlySpan<int> deltas, ReadOnlySpan<byte> damage,
            ReadOnlySpan<ushort> structure, ReadOnlySpan<byte> owner)
        {
            if (endpoint == null || endpoint.Row != row || row < 0 || row >= 800 ||
                (direction != 0 && direction != 2 && direction != 4 && direction != 6) ||
                !global::APIShared.IndexBounds.IsValidIndex(tile, logic.Length) || !global::APIShared.IndexBounds.IsValidIndex(row * 8 + 7, deltas.Length) ||
                !global::APIShared.IndexBounds.IsValidIndex(tile, damage.Length) || !global::APIShared.IndexBounds.IsValidIndex(tile, structure.Length) || !global::APIShared.IndexBounds.IsValidIndex(tile, owner.Length) ||
                (logic[tile] & 0x400300) != 0x300 || damage[tile] != 0 || structure[tile] != 0 ||
                (owner[tile] & 7) + 1 != endpoint.Owner) return false;
            // Native directions 0/2/4/6 are the axis neighbours; 0/4 and 2/6 are the two axes.
            int count = 0, along = 0, across = 0;
            for (int d = 0; d < 8; d += 2)
            {
                long id = (long)tile + deltas[row * 8 + d];
                if (id < 0 || id >= logic.Length || (logic[(int)id] & 0x200) == 0) continue;
                if (!endpoint.Has((int)id)) return false;
                count++;
                if (((d - direction) & 3) == 0) along++; else across++;
            }
            if (count != endpoint.Count) return false;
            // Straight sprite along the axis with more crenel neighbours; a tie (diagonal or isolated
            // piece, corner, cross, block inside) takes the axis of direction 0, which the renderer
            // asks first (65DD6). An endpoint answers on its neighbour's axis, as before.
            return along > across || (along == across && (direction & 2) == 0);
        }
    }
}
