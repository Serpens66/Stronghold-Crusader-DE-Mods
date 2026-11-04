using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.GateRouting
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
            string directionMaskDiagnostics = null, GateEdgeOwnership[] edgeOwners = null, Dictionary<int, GateIdentity> gateIdentities = null, GateEdgeOwnership diagnosticGateEdges = null, int[] missingRequiredGateMasks = null, HashSet<int> maskedGates = null)
        {
            RulesVersion = APIShared.GateAccessRules.Version;
            MissingRequiredGateMasks = missingRequiredGateMasks == null ? Array.Empty<int>() : (int[])missingRequiredGateMasks.Clone();
            MaskedGates = maskedGates == null ? new HashSet<int>() : new HashSet<int>(maskedGates);
            DirectionMasks = directionMasks ?? new byte[9][];
            EdgeOwners = edgeOwners;
            DiagnosticGateEdges = diagnosticGateEdges;
            GateIdentities = gateIdentities == null ? new Dictionary<int, GateIdentity>() : new Dictionary<int, GateIdentity>(gateIdentities);
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

        // 1 where the leaving directions of a tile differ between players; built once per immutable
        // snapshot on first use (a race builds equal arrays; one of them is kept).
        private byte[] playerSensitive;
        internal bool IsPlayerNeutral(int tileId)
        {
            if (NonEmptyPlayerMaskCount == 0 || tileId < 0) return true;
            byte[] sensitive = playerSensitive ?? BuildPlayerSensitivity();
            return tileId >= sensitive.Length || sensitive[tileId] == 0;
        }
        private byte[] BuildPlayerSensitivity()
        {
            int length = 0;
            for (int player = 1; player < DirectionMasks.Length; player++)
                if (DirectionMasks[player] != null) length = Math.Max(length, DirectionMasks[player].Length);
            var sensitive = new byte[length];
            for (int tile = 0; tile < length; tile++)
            {
                int first = -1;
                for (int player = 1; player < DirectionMasks.Length; player++)
                {
                    byte[] masks = DirectionMasks[player];
                    int value = masks == null || tile >= masks.Length ? 0xFF : masks[tile];
                    if (first < 0) first = value;
                    else if (value != first) { sensitive[tile] = 1; break; }
                }
            }
            System.Threading.Interlocked.CompareExchange(ref playerSensitive, sensitive, null);
            return playerSensitive;
        }
        internal GateEdgeOwnership[] EdgeOwners { get; }
        // TEMP_GATE_ROUTE_ACCEPTANCE: player-independent geometry; never consulted by functional policy.
        internal GateEdgeOwnership DiagnosticGateEdges { get; }
        internal readonly Dictionary<int, GateIdentity> GateIdentities;
        internal readonly struct GateIdentity
        {
            internal GateIdentity(uint global, int owner, int capturer) { Global = global; Owner = owner; Capturer = capturer; }
            internal readonly uint Global;
            internal readonly int Owner, Capturer;
        }
        internal long RulesVersion { get; }
        internal int[] MissingRequiredGateMasks { get; }
        private readonly HashSet<int> MaskedGates;
        internal string DescribeGateMask(int gateId, uint globalId, int owner, int capturer)
        {
            if (!GateIdentities.TryGetValue(gateId, out GateIdentity identity) || identity.Global != globalId || identity.Owner != owner || identity.Capturer != capturer)
                return "unknown-identity";
            return MaskedGates.Contains(gateId) ? "present-for-restricted-players" :
                Array.IndexOf(MissingRequiredGateMasks, gateId) >= 0 ? "missing-required-mask" : "not-required";
        }
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

namespace BugfixesAndQoL.UnitCommands
{
    // Whether a tile's leaving directions are equal for every player in the currently published gate
    // route policy (one byte per tile and snapshot). Set by the gate routing runtime; null without it.
    // Traversal providers use it to share player-neutral tiles across player masks. Declared beside
    // the snapshot so GateRouting compiles on its own; not part of the functional policy adapter.
    internal static class GatePolicyNeutrality
    {
        internal static System.Func<int, bool> Current;
    }
}
