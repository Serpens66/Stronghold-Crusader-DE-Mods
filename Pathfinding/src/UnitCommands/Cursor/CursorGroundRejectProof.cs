using System;
using APIShared;

namespace Pathfinding.UnitCommands
{
    // A closed directed set excluding the target is a negative reachability proof.
    // Search stamps only propose a set; closure is checked independently of budget,
    // queue exhaustion, reconstruction success, and the search's return value.
    internal sealed unsafe partial class CursorGroundRejectProof
    {
        internal const int TileCount = 320800;
        internal readonly ulong[] Tiles = new ulong[(TileCount + 63) / 64];
        internal int Player, Target, Mode, Profile;
        internal IEnemyGatePathPolicy Provider;
        internal IEnemyGateRoutePolicySnapshot Policy;
        internal bool Contains(int tile) => (uint)tile < TileCount && (Tiles[tile >> 6] & (1UL << (tile & 63))) != 0;
        internal void Reset() { Provider = null; Policy = null; }

        internal bool Capture(short* stamps, int generation, int start, int target,
            byte* connections, byte* directionBits, int* neighborOffsets, ushort* rows,
            IEnemyGateRoutePolicySnapshot policy)
        {
            if (generation < 1 || generation > 32000) { return false; }
            if ((uint)start >= TileCount || (uint)target >= TileCount || stamps == null || connections == null ||
                directionBits == null || neighborOffsets == null || rows == null) { return false; }
            Array.Clear(Tiles, 0, Tiles.Length);
            for (int tile = 0; tile < TileCount; tile++)
                if (stamps[tile] == generation) Tiles[tile >> 6] |= 1UL << (tile & 63);
            if (!Contains(start)) { return false; }
            if (Contains(target)) { return false; }
            for (int tile = 0; tile < TileCount; tile++)
            {
                if (!Contains(tile)) continue;
                int row = rows[tile];
                if ((uint)row >= 800) { return false; }
                int mask = connections[tile];
                for (int direction = 0; direction < 8; direction++)
                {
                    if ((mask & directionBits[direction]) == 0) continue;
                    int next = tile + neighborOffsets[row * 8 + direction];
                    if ((uint)next >= TileCount) { return false; }
                    // Interior edges need no policy callback. The unfiltered graph is
                    // a safe superset when no exact, immutable gate policy is available.
                    if (!Contains(next) && (policy == null || policy.IsDirectionAllowed(tile, direction))) { return false; }
                }
            }
            if (policy != null && !policy.IsCurrent) { return false; }
            return true;
        }
    }
}
