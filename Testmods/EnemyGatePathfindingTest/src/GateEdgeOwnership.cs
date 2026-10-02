using System;
using System.Collections.Generic;

namespace EnemyGatePathfindingTest
{
    // Built alongside the existing masks. An overlapping edge has no unique gate.
    internal sealed class GateEdgeOwnership
    {
        private readonly Dictionary<long, int> owners = new Dictionary<long, int>();
        internal void Record(int tile, int direction, int gateId)
        {
            long key = (long)tile * 8 + direction;
            if (owners.TryGetValue(key, out int previous) && previous != gateId)
                owners[key] = -1;
            else if (!owners.ContainsKey(key)) owners.Add(key, gateId);
        }
        internal int Resolve(int tile, int direction) =>
            owners.TryGetValue((long)tile * 8 + direction, out int owner) ? owner : 0;
    }

    // All edges count; concrete blocked edges retain first/last in the existing aggregate.
    internal sealed class AssassinRouteProbe
    {
        internal readonly RouteTilePolicySnapshot Snapshot;
        internal long Ground, Climb, BlockedGround, BlockedClimb, Unknown;
        internal AssassinRouteProbe(RouteTilePolicySnapshot snapshot) { Snapshot = snapshot; }
        internal bool Observe(int player, int tile, int direction, bool climb, out int gateId)
        {
            gateId = 0;
            if (climb) Climb++; else Ground++;
            if (player <= 0 || player >= Snapshot.DirectionMasks.Length ||
                tile < 0 || direction < 0 || direction > 7 ||
                (Snapshot.DirectionMasks[player] != null && tile >= Snapshot.DirectionMasks[player].Length))
            { Unknown++; return false; }
            if (Snapshot.IsDirectionAllowed(player, tile, direction)) return false;
            if (climb) BlockedClimb++; else BlockedGround++;
            gateId = Snapshot.EdgeOwners?[player]?.Resolve(tile, direction) ?? 0;
            return true;
        }
    }
}
