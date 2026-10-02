using System;
using System.Collections.Generic;

namespace EnemyBridgePathTest
{
    // Built alongside the existing masks. An overlapping edge has no unique gate.
    internal sealed class GateEdgeOwnership
    {
        private readonly Dictionary<long, int> owners = new Dictionary<long, int>();
        private readonly Dictionary<long, int> bridges = new Dictionary<long, int>();
        internal void Record(int tile, int direction, int gateId, int bridgeId = 0)
        {
            long key = (long)tile * 8 + direction;
            if (owners.TryGetValue(key, out int previous) && previous != gateId)
                owners[key] = -1;
            else if (!owners.ContainsKey(key)) owners.Add(key, gateId);
            if (bridges.TryGetValue(key, out int previousBridge) && previousBridge != bridgeId)
                bridges[key] = -1;
            else if (!bridges.ContainsKey(key)) bridges.Add(key, bridgeId);
        }
        internal int Resolve(int tile, int direction) =>
            owners.TryGetValue((long)tile * 8 + direction, out int owner) ? owner : 0;
        internal int ResolveBridge(int tile, int direction) =>
            bridges.TryGetValue((long)tile * 8 + direction, out int bridge) ? bridge : 0;
    }
}
