using System;

namespace CrenelatedWallTest
{
    internal sealed class EndpointAssignment
    {
        internal readonly int Owner, Row, Neighbor;
        internal EndpointAssignment(int owner, int row, int neighbor) { Owner = owner; Row = row; Neighbor = neighbor; }
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
                (uint)tile >= (uint)logic.Length || row * 8 + 7 >= deltas.Length ||
                tile >= damage.Length || tile >= structure.Length || tile >= owner.Length ||
                (logic[tile] & 0x400300) != 0x300 || damage[tile] != 0 || structure[tile] != 0 ||
                (owner[tile] & 7) + 1 != endpoint.Owner) return false;
            int count = 0, neighbor = -1;
            for (int d = 0; d < 8; d += 2)
            {
                long id = (long)tile + deltas[row * 8 + d];
                if (id >= 0 && id < logic.Length && (logic[(int)id] & 0x200) != 0) { count++; neighbor = (int)id; }
            }
            return count == 1 && neighbor == endpoint.Neighbor &&
                (neighbor == (long)tile + deltas[row * 8 + direction] || neighbor == (long)tile + deltas[row * 8 + ((direction + 4) & 7)]);
        }
    }
}
