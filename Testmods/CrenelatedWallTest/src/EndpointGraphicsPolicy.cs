using System;

namespace CrenelatedWallTest
{
    // A low managed crenel the graphics hook draws as a straight piece, so the renderer's special
    // branch never ORs the high bit into it (65EF0). Only the owner and row are stored: the answer
    // depends solely on synchronized tile state, never on a neighbourhood remembered on this peer.
    // Camera rotation and the flatten toggle re-run the renderer over all tiles on one peer only, so
    // a stale local snapshot here would raise the crenel on that peer alone (desync).
    internal sealed class EndpointAssignment
    {
        internal readonly int Owner, Row;
        internal EndpointAssignment(int owner, int row) { Owner = owner; Row = row; }
    }
    internal static class EndpointGraphicsPolicy
    {
        // Pure validation of an existing logical assignment. Never chooses a height; raising a crenel
        // stays the job of WallRuntime.Normalize, which also refreshes the paths.
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
            int along = 0, across = 0;
            for (int d = 0; d < 8; d += 2)
            {
                long id = (long)tile + deltas[row * 8 + d];
                if (id < 0 || id >= logic.Length || (logic[(int)id] & 0x200) == 0) continue;
                if (((d - direction) & 3) == 0) along++; else across++;
            }
            // Straight sprite along the axis with more live crenel neighbours; a tie (diagonal or
            // isolated piece, corner, cross, block inside) takes the axis of direction 0, which the
            // renderer asks first (65DD6). An endpoint answers on its neighbour's axis.
            return along > across || (along == across && (direction & 2) == 0);
        }
    }
}
