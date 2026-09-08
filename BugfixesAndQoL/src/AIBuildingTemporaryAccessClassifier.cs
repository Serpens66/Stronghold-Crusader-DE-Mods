// Feature: Classify disconnected AI buildings using Vanilla's native gate-portal topology.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BugfixesAndQoL
{
    internal readonly struct AIBuildingAccessDiagnostic
    {
        internal AIBuildingAccessDiagnostic(
            GateBlockageEvaluationKind kind,
            int tick,
            int buildingPcl,
            int keepPcl,
            string details)
        {
            Kind = kind;
            Tick = tick;
            BuildingPcl = buildingPcl;
            KeepPcl = keepPcl;
            Details = details ?? string.Empty;
        }

        internal GateBlockageEvaluationKind Kind { get; }
        internal int Tick { get; }
        internal int BuildingPcl { get; }
        internal int KeepPcl { get; }
        internal string Details { get; }
        internal bool IsReachableUnderImprovedCheck =>
            Kind != GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates;

        internal static AIBuildingAccessDiagnostic Unavailable(int tick, string reason) =>
            new AIBuildingAccessDiagnostic(
                GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates,
                tick,
                0,
                0,
                "failureReason=" + (reason ?? "unknown"));
    }

    internal sealed unsafe class AIBuildingTemporaryAccessClassifier
    {
        internal const int NativePathManagerRva = 0x60AD660;
        internal const int MaximumPortalRecordCount = 200;
        internal const int PortalRecordStrideDwords = 0x81;
        internal const int PortalStateOffsetDwords = 0x809;
        internal const int PortalKindOffsetDwords = 0x80A;
        internal const int PortalBuildingIdOffsetDwords = 0x80C;
        internal const int PortalActiveOffsetDwords = 0x80F;
        internal const int PortalFirstPclOffsetDwords = 0x816;
        internal const int PortalSecondPclOffsetDwords = 0x817;
        internal const int PortalOwnerOffsetDwords = 0x882;
        internal const int PortalThirdPclOffsetDwords = 0x883;

        private readonly ManualLogSource log;
        private readonly int* nativePathManager;
        private readonly Dictionary<int, PortalTopologySnapshot> portalTopologyCache =
            new Dictionary<int, PortalTopologySnapshot>();
        private readonly Dictionary<ClassificationKey, AIBuildingAccessDiagnostic> classificationCache =
            new Dictionary<ClassificationKey, AIBuildingAccessDiagnostic>();
        private int cacheTick = int.MinValue;
        private bool failureLogged;

        internal AIBuildingTemporaryAccessClassifier(ManualLogSource log, IntPtr nativePathManager)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.nativePathManager = (int*)nativePathManager.ToPointer();
        }

        internal bool TryClassify(
            int buildingId,
            int expectedPlayerId,
            out AIBuildingAccessDiagnostic diagnostic)
        {
            if (!TryCaptureTick(out int tick))
            {
                diagnostic = AIBuildingAccessDiagnostic.Unavailable(int.MinValue, "game-tick-unavailable");
                return false;
            }
            diagnostic = AIBuildingAccessDiagnostic.Unavailable(tick, "classification-not-started");

            try
            {
                BeginTick(tick);
                if (nativePathManager == null)
                    return Fail(tick, "native-path-manager-unavailable", out diagnostic);

                GameBuildingManagerAPI buildingsApi = GameBuildingManagerAPI.Instance;
                if (!buildingsApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building == null)
                {
                    return Fail(tick, "building-not-found", out diagnostic);
                }
                if (building->r_AliveState != AliveState.IsAlive || building->r_GlobalId == 0)
                    return Fail(tick, "building-not-living", out diagnostic);

                int playerId = building->r_PlayerIdOwner;
                if (playerId != expectedPlayerId || playerId == 0 ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                {
                    return Fail(tick, "building-owner-context-mismatch", out diagnostic);
                }

                if (!TryGetEntryPcl(building, out int buildingPcl))
                    return Fail(tick, "building-entry-pcl-unavailable", out diagnostic);
                if (!TryGetKeepPcl(playerId, out int keepPcl))
                    return Fail(tick, "keep-pcl-unavailable", out diagnostic);
                if (!TryGetPortalTopology(playerId, out PortalTopologySnapshot topology, out string failure))
                    return Fail(tick, failure, out diagnostic);

                var key = new ClassificationKey(
                    buildingId,
                    building->r_GlobalId,
                    playerId,
                    buildingPcl,
                    keepPcl,
                    topology.Signature);
                if (classificationCache.TryGetValue(key, out diagnostic))
                    return true;

                GateBlockageEvaluation evaluation = TemporaryGateBlockagePolicy.Evaluate(
                    buildingPcl,
                    keepPcl,
                    topology.Portals);
                diagnostic = new AIBuildingAccessDiagnostic(
                    evaluation.Kind,
                    tick,
                    buildingPcl,
                    keepPcl,
                    BuildDiagnosticDetails(topology, evaluation));
                classificationCache.Add(key, diagnostic);
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = AIBuildingAccessDiagnostic.Unavailable(
                    tick,
                    "exception-" + ex.GetType().Name);
                if (!failureLogged)
                {
                    failureLogged = true;
                    log.LogError(
                        $"[{TimestampNow()}] Bugfixes and QoL improved AI building-access classification failed; " +
                        $"this demolition uses vanilla behavior: {ex}");
                }
                return false;
            }
        }

        private void BeginTick(int tick)
        {
            if (tick == cacheTick)
                return;

            portalTopologyCache.Clear();
            classificationCache.Clear();
            cacheTick = tick;
        }

        private static bool TryGetEntryPcl(GameBuilding* building, out int pcl)
        {
            // Despite their public names, Vanilla writes the selected entrance coordinates
            // to these two fields immediately before returning accessibility result 2.
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int x = building->r_TilePositionXEnd;
            int y = building->r_TilePositionYEnd;
            if (!tiles.IsTileInsideMapBounds(x, y))
            {
                pcl = 0;
                return false;
            }
            return TryGetPcl(tiles.GetTileId(x, y), out pcl);
        }

        private static bool TryGetKeepPcl(int playerId, out int pcl)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    playerId,
                    out GamePlayerResources* resources) || resources == null)
            {
                pcl = 0;
                return false;
            }
            return TryGetPcl(checked((int)resources->r_KeepTileId), out pcl);
        }

        private static bool TryGetPcl(int tileId, out int pcl)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pcls = tiles.TileManager.PathConnectionGrid;
            if (!tiles.IsValidTileId(tileId) || (uint)tileId >= (uint)pcls.Length)
            {
                pcl = 0;
                return false;
            }

            pcl = pcls[tileId];
            return pcl > 0;
        }

        private bool TryGetPortalTopology(
            int playerId,
            out PortalTopologySnapshot snapshot,
            out string failure)
        {
            if (portalTopologyCache.TryGetValue(playerId, out snapshot))
            {
                failure = string.Empty;
                return true;
            }

            int count = nativePathManager[0];
            if (count < 1 || count > MaximumPortalRecordCount)
            {
                snapshot = null;
                failure = "native-portal-count-out-of-range";
                return false;
            }

            var portals = new List<PclPortalConnection>();
            GameBuildingManagerAPI buildingsApi = GameBuildingManagerAPI.Instance;
            GamePlayerManagerAPI playersApi = GamePlayerManagerAPI.Instance;
            for (int portalId = 1; portalId < count; portalId++)
            {
                int offset = portalId * PortalRecordStrideDwords;
                if (nativePathManager[offset + PortalStateOffsetDwords] != 1 ||
                    nativePathManager[offset + PortalActiveOffsetDwords] == 0 ||
                    nativePathManager[offset + PortalKindOffsetDwords] == 1)
                {
                    continue;
                }

                int portalBuildingId = nativePathManager[offset + PortalBuildingIdOffsetDwords];
                int portalOwnerId = nativePathManager[offset + PortalOwnerOffsetDwords];
                if (!buildingsApi.IsValidId(portalBuildingId) ||
                    !buildingsApi.TryGetBuildingById(portalBuildingId, out GameBuilding* portalBuilding) ||
                    portalBuilding == null || portalBuilding->r_AliveState != AliveState.IsAlive ||
                    portalBuilding->r_GlobalId == 0 || portalBuilding->r_PlayerIdOwner != portalOwnerId ||
                    !TemporaryGateBlockagePolicy.IsGateOrDrawbridge(portalBuilding->r_BuildingType))
                {
                    continue;
                }

                bool friendly = TemporaryGateBlockagePolicy.IsFriendlyPortalOwner(
                    playerId,
                    portalOwnerId,
                    playersApi.IsPlayerIdValid,
                    playersApi.IsPlayerAlliedTo);
                if (!friendly)
                    continue;

                int first = nativePathManager[offset + PortalFirstPclOffsetDwords];
                int second = nativePathManager[offset + PortalSecondPclOffsetDwords];
                int third = nativePathManager[offset + PortalThirdPclOffsetDwords];
                if (!IsValidPcl(first) || !IsValidPcl(second) ||
                    (third != 0 && !IsValidPcl(third)))
                {
                    snapshot = null;
                    failure = "friendly-portal-pcl-out-of-range";
                    return false;
                }

                portals.Add(new PclPortalConnection(
                    first,
                    second,
                    third,
                    portalOwnerId,
                    portalBuildingId,
                    portalBuilding->r_GlobalId));
            }

            portals.Sort(ComparePortals);
            snapshot = new PortalTopologySnapshot(portals, BuildTopologySignature(portals));
            portalTopologyCache.Add(playerId, snapshot);
            failure = string.Empty;
            return true;
        }

        private static bool IsValidPcl(int pcl) => pcl > 0 && pcl <= ushort.MaxValue;

        private static int ComparePortals(PclPortalConnection left, PclPortalConnection right)
        {
            int comparison = left.GlobalId.CompareTo(right.GlobalId);
            if (comparison != 0)
                return comparison;
            comparison = left.BuildingId.CompareTo(right.BuildingId);
            if (comparison != 0)
                return comparison;
            comparison = left.First.CompareTo(right.First);
            if (comparison != 0)
                return comparison;
            comparison = left.Second.CompareTo(right.Second);
            return comparison != 0 ? comparison : left.Third.CompareTo(right.Third);
        }

        private static string BuildTopologySignature(IReadOnlyList<PclPortalConnection> portals)
        {
            var builder = new StringBuilder(portals.Count * 24);
            foreach (PclPortalConnection portal in portals)
            {
                builder.Append(portal.GlobalId).Append(':')
                    .Append(portal.First).Append(':')
                    .Append(portal.Second).Append(':')
                    .Append(portal.Third).Append(';');
            }
            return builder.ToString();
        }

        private static string BuildDiagnosticDetails(
            PortalTopologySnapshot topology,
            GateBlockageEvaluation evaluation)
        {
            var builder = new StringBuilder(160);
            builder.Append("friendlyPortalCount=").Append(topology.Portals.Count)
                .Append(", usedPortalPath=[");
            for (int index = 0; index < evaluation.UsedPortalIndices.Length; index++)
            {
                if (index != 0)
                    builder.Append("->");
                int portalIndex = evaluation.UsedPortalIndices[index];
                if ((uint)portalIndex >= (uint)topology.Portals.Count)
                {
                    builder.Append("invalid-index-").Append(portalIndex);
                    continue;
                }
                PclPortalConnection portal = topology.Portals[portalIndex];
                builder.Append("gate#").Append(portal.BuildingId)
                    .Append("/owner#").Append(portal.OwnerId)
                    .Append('(').Append(portal.First).Append(',')
                    .Append(portal.Second).Append(',').Append(portal.Third).Append(')');
            }
            return builder.Append(']').ToString();
        }

        private static bool Fail(int tick, string reason, out AIBuildingAccessDiagnostic diagnostic)
        {
            diagnostic = AIBuildingAccessDiagnostic.Unavailable(tick, reason);
            return false;
        }

        private static bool TryCaptureTick(out int tick)
        {
            try
            {
                tick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
                return true;
            }
            catch
            {
                tick = int.MinValue;
                return false;
            }
        }

        private sealed class PortalTopologySnapshot
        {
            internal PortalTopologySnapshot(List<PclPortalConnection> portals, string signature)
            {
                Portals = portals;
                Signature = signature;
            }

            internal List<PclPortalConnection> Portals { get; }
            internal string Signature { get; }
        }

        private readonly struct ClassificationKey : IEquatable<ClassificationKey>
        {
            internal ClassificationKey(
                int buildingId,
                uint buildingGlobalId,
                int playerId,
                int buildingPcl,
                int keepPcl,
                string topologySignature)
            {
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                PlayerId = playerId;
                BuildingPcl = buildingPcl;
                KeepPcl = keepPcl;
                TopologySignature = topologySignature ?? string.Empty;
            }

            private int BuildingId { get; }
            private uint BuildingGlobalId { get; }
            private int PlayerId { get; }
            private int BuildingPcl { get; }
            private int KeepPcl { get; }
            private string TopologySignature { get; }

            public bool Equals(ClassificationKey other) =>
                BuildingId == other.BuildingId &&
                BuildingGlobalId == other.BuildingGlobalId &&
                PlayerId == other.PlayerId &&
                BuildingPcl == other.BuildingPcl &&
                KeepPcl == other.KeepPcl &&
                string.Equals(TopologySignature, other.TopologySignature, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is ClassificationKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = BuildingId;
                    hash = hash * 397 ^ (int)BuildingGlobalId;
                    hash = hash * 397 ^ PlayerId;
                    hash = hash * 397 ^ BuildingPcl;
                    hash = hash * 397 ^ KeepPcl;
                    return hash * 397 ^ StringComparer.Ordinal.GetHashCode(TopologySignature);
                }
            }
        }

        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
