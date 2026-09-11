using System;

namespace EnemyGatePathfindingTest
{
    // Immutable input for the native direction adapters. Geometry is resolved while
    // publishing the snapshot; route queries only index the selected byte mask.
    internal sealed class RouteTilePolicySnapshot
    {
        internal static readonly RouteTilePolicySnapshot Empty =
            new RouteTilePolicySnapshot(new byte[9][], 0);

        internal RouteTilePolicySnapshot(
            byte[][] directionMasks,
            ulong topologyFingerprint,
            int maskedDirectedEdges = 0,
            int ambiguousPassages = 0,
            string directionMaskDiagnostics = null)
        {
            DirectionMasks = directionMasks ?? new byte[9][];
            TopologyFingerprint = topologyFingerprint;
            MaskedDirectedEdges = maskedDirectedEdges;
            AmbiguousPassages = ambiguousPassages;
            DirectionMaskDiagnostics = directionMaskDiagnostics ?? "none";
            int nonEmpty = 0;
            for (int player = 1; player < DirectionMasks.Length; player++)
                if (DirectionMasks[player] != null) nonEmpty++;
            NonEmptyPlayerMaskCount = nonEmpty;
        }

        // Each byte contains the eight Vanilla directions that remain legal when
        // leaving the tile. A null player entry is the all-0xFF fast path.
        internal byte[][] DirectionMasks { get; }
        internal int NonEmptyPlayerMaskCount { get; }
        internal int MaskedDirectedEdges { get; }
        internal int AmbiguousPassages { get; }
        internal string DirectionMaskDiagnostics { get; }
        internal ulong TopologyFingerprint { get; }

        internal bool IsDirectionAllowed(int playerId, int tileId, int direction)
        {
            if (direction < 0 || direction > 7 || playerId <= 0 ||
                playerId >= DirectionMasks.Length || tileId < 0)
                return true;
            byte[] masks = DirectionMasks[playerId];
            return masks == null || tileId >= masks.Length ||
                (masks[tileId] & (1 << direction)) != 0;
        }
    }
}
