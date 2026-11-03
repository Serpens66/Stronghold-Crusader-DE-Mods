extern alias BugfixesRuntime;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Live audit of anchor routes, remembered per group order. Every route the core answers is checked edge
    // by edge with LiveEdge before it is published (TryFinishFastEncoded), because masks can lag behind the
    // map. The members of one order walk the same corridors, so with groups of ~600 the same edges were
    // checked hundreds of times (about 30 % of a 255 ms order tick on test_stresstest5, 02.11.2026).
    // LiveEdge reads only map state; inside one order (one 0x11B520 call on the simulation thread) that
    // changes only through reported tile, moat or direction writes (placementRevision), route rule changes
    // (RouteTileRules.Version), the topology marks below and the closed-gate sets, and each of them starts a
    // new generation. Only allowed edges are kept: a failed edge ends the route and marks the area anyway.
    // DEVDIAG builds verify every hit against a fresh LiveEdge (anchor-breakdown edgeMemo mismatches).
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        // Per node and policy (index node * 2 + ground): bits 0..7 allowed directions, 8..15 wet, 16..23 structure.
        private int[] edgeMemoStamps, edgeMemoBits;
        private int edgeMemoGeneration, edgeMemoEpoch, edgeMemoSeenEpoch = -1, edgeMemoPlayer = -1;
        private long edgeMemoFrame = -1, edgeMemoRevision = -1, edgeMemoRules = -1;
        private long edgeMemoHits, edgeMemoMisses;
#if SERP_DEVDIAG
        private long edgeMemoMismatches;
#endif

        // Any change the memo cannot see through placementRevision or the rule version.
        private void InvalidateEdgeMemo() => edgeMemoEpoch++;

        private bool AnchorLiveEdgeMemo(int player, int from, int to, int direction, bool ground, out bool wet, out bool structure)
        {
            int nodes = MapWidth * MapWidth;
            if (anchorFrames.Count == 0 || (uint)from >= (uint)nodes || (uint)direction > 7)
                return AnchorLiveEdge(player, from, to, direction, ground, out wet, out structure);
            long frame = anchorFrames[anchorFrames.Count - 1].Stamp, revision = runtime.placementRevision,
                rules = APIShared.Pathfinding.RouteTileRules.Version;
            if (edgeMemoStamps == null || edgeMemoStamps.Length != nodes * 2)
            { edgeMemoStamps = new int[nodes * 2]; edgeMemoBits = new int[nodes * 2]; edgeMemoGeneration = 0; edgeMemoFrame = -1; }
            if (frame != edgeMemoFrame || player != edgeMemoPlayer || revision != edgeMemoRevision ||
                rules != edgeMemoRules || edgeMemoEpoch != edgeMemoSeenEpoch)
            {
                if (++edgeMemoGeneration == int.MaxValue) { System.Array.Clear(edgeMemoStamps, 0, edgeMemoStamps.Length); edgeMemoGeneration = 1; }
                edgeMemoFrame = frame; edgeMemoPlayer = player; edgeMemoRevision = revision; edgeMemoRules = rules; edgeMemoSeenEpoch = edgeMemoEpoch;
            }
            int slot = from * 2 + (ground ? 1 : 0);
            int bits = edgeMemoStamps[slot] == edgeMemoGeneration ? edgeMemoBits[slot] : 0;
            if ((bits >> direction & 1) != 0)
            {
                wet = (bits >> (8 + direction) & 1) != 0; structure = (bits >> (16 + direction) & 1) != 0;
                edgeMemoHits++;
#if SERP_DEVDIAG
                if (!AnchorLiveEdge(player, from, to, direction, ground, out bool liveWet, out bool liveStructure) ||
                    liveWet != wet || liveStructure != structure) edgeMemoMismatches++;
#endif
                return true;
            }
            edgeMemoMisses++;
            bool allowed = AnchorLiveEdge(player, from, to, direction, ground, out wet, out structure);
            if (allowed)
            {
                edgeMemoStamps[slot] = edgeMemoGeneration;
                edgeMemoBits[slot] = bits | 1 << direction | (wet ? 1 << (8 + direction) : 0) | (structure ? 1 << (16 + direction) : 0);
            }
            return allowed;
        }

        private string FormatEdgeMemo()
        {
            string text = $"edgeMemo(hits={edgeMemoHits} misses={edgeMemoMisses}";
#if SERP_DEVDIAG
            text += $" mismatches={edgeMemoMismatches}";
#endif
#if SERP_DEVDIAG
            edgeMemoMismatches = 0;
#endif
            edgeMemoHits = edgeMemoMisses = 0;
            return text + ")";
        }
    }
}
