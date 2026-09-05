// Feature: Classify AI building access with friendly gatehouses treated as always passable.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RedBird.Core.Memory;

namespace BugfixesAndQoL
{
    internal readonly struct AIBuildingAccessDiagnostic
    {
        internal AIBuildingAccessDiagnostic(
            GateBlockageEvaluationKind kind,
            int tick,
            bool hasDirectPclPath,
            bool hasPathWithFriendlyGates,
            bool? nativePlayerAwareReachable,
            string details)
        {
            Kind = kind;
            Tick = tick;
            HasDirectPclPath = hasDirectPclPath;
            HasPathWithFriendlyGates = hasPathWithFriendlyGates;
            NativePlayerAwareReachable = nativePlayerAwareReachable;
            Details = details ?? string.Empty;
        }

        internal GateBlockageEvaluationKind Kind { get; }
        internal int Tick { get; }
        internal bool HasDirectPclPath { get; }
        internal bool HasPathWithFriendlyGates { get; }
        internal bool? NativePlayerAwareReachable { get; }
        internal string Details { get; }
        internal bool IsReachableUnderImprovedCheck =>
            Kind != GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates;

        internal static AIBuildingAccessDiagnostic Unavailable(int tick, string reason) =>
            new AIBuildingAccessDiagnostic(
                GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates,
                tick,
                hasDirectPclPath: false,
                hasPathWithFriendlyGates: false,
                nativePlayerAwareReachable: null,
                details: "failureReason=" + (reason ?? "unknown"));
    }

    internal sealed unsafe class AIBuildingTemporaryAccessClassifier
    {
        private readonly ManualLogSource log;
        private readonly Dictionary<int, KeepSnapshot> keepCache = new Dictionary<int, KeepSnapshot>();
        private readonly Dictionary<int, GateTopologySnapshot> gateTopologyCache =
            new Dictionary<int, GateTopologySnapshot>();
        private readonly Dictionary<ReachabilityKey, bool> reachabilityCache =
            new Dictionary<ReachabilityKey, bool>();
        private readonly Dictionary<ClassificationKey, AIBuildingAccessDiagnostic> classificationCache =
            new Dictionary<ClassificationKey, AIBuildingAccessDiagnostic>();
        private int cacheTick = int.MinValue;
        private bool failureLogged;

        internal AIBuildingTemporaryAccessClassifier(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal bool TryClassify(int buildingId, out AIBuildingAccessDiagnostic diagnostic)
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
                GameBuildingManagerAPI buildingsApi = GameBuildingManagerAPI.Instance;
                if (!buildingsApi.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null)
                    return Fail(tick, "building-not-found", out diagnostic);
                if (building->r_AliveState != AliveState.IsAlive || building->r_GlobalId == 0)
                    return Fail(tick, "building-not-living", out diagnostic);
                if (building->r_PlayerIdOwner == 0 ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(building->r_PlayerIdOwner))
                {
                    return Fail(tick, "building-not-ai-owned", out diagnostic);
                }

                int playerId = building->r_PlayerIdOwner;
                if (!TryGetKeepSnapshot(playerId, out KeepSnapshot keep, out string keepFailure))
                    return Fail(tick, keepFailure, out diagnostic);

                List<int> buildingPcls = CollectAccessPcls(building);
                if (buildingPcls.Count == 0)
                    return Fail(tick, "building-has-no-valid-access-pcl", out diagnostic);

                if (!TryGetGateTopology(playerId, out GateTopologySnapshot topology, out string topologyFailure))
                    return Fail(tick, topologyFailure, out diagnostic);

                string buildingPclKey = BuildPclKey(buildingPcls);
                ClassificationKey key = new ClassificationKey(
                    buildingId,
                    building->r_GlobalId,
                    keep.GlobalId,
                    playerId,
                    buildingPclKey,
                    keep.PclKey);
                if (classificationCache.TryGetValue(key, out diagnostic))
                    return true;

                GateBlockageEvaluation evaluation = TemporaryGateBlockagePolicy.Evaluate(
                    buildingPcls,
                    keep.AccessPcls,
                    topology.Gates,
                    (source, destination) => IsNativePlayerAwareReachable(
                        playerId,
                        source,
                        destination));
                diagnostic = new AIBuildingAccessDiagnostic(
                    evaluation.Kind,
                    tick,
                    evaluation.HasDirectPclPath,
                    evaluation.HasPathWithFriendlyGates,
                    evaluation.NativePlayerAwareReachable,
                    BuildDiagnosticDetails(
                        buildingPcls,
                        keep.AccessPcls,
                        topology,
                        evaluation));
                classificationCache[key] = diagnostic;
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

            keepCache.Clear();
            gateTopologyCache.Clear();
            reachabilityCache.Clear();
            classificationCache.Clear();
            cacheTick = tick;
        }

        private bool TryGetKeepSnapshot(int playerId, out KeepSnapshot snapshot, out string failure)
        {
            if (keepCache.TryGetValue(playerId, out snapshot))
            {
                failure = string.Empty;
                return true;
            }

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding candidate = ref buildings[spanIndex];
                if (candidate.r_AliveState != AliveState.IsAlive ||
                    candidate.r_PlayerIdOwner != playerId ||
                    candidate.r_GlobalId == 0 ||
                    !IsKeep(candidate.r_BuildingType))
                {
                    continue;
                }

                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(spanIndex + 1, out GameBuilding* keep) ||
                    keep == null)
                {
                    snapshot = null;
                    failure = "keep-pointer-unavailable";
                    return false;
                }

                List<int> accessPcls = CollectAccessPcls(keep);
                if (accessPcls.Count == 0)
                {
                    snapshot = null;
                    failure = "keep-has-no-valid-access-pcl";
                    return false;
                }

                snapshot = new KeepSnapshot(keep->r_GlobalId, accessPcls, BuildPclKey(accessPcls));
                keepCache.Add(playerId, snapshot);
                failure = string.Empty;
                return true;
            }

            snapshot = null;
            failure = "keep-not-found";
            return false;
        }

        private static bool IsKeep(eStructs type) =>
            type == eStructs.STRUCT_KEEP_ONE ||
            type == eStructs.STRUCT_KEEP_TWO ||
            type == eStructs.STRUCT_KEEP_THREE ||
            type == eStructs.STRUCT_KEEP_FOUR ||
            type == eStructs.STRUCT_KEEP_FIVE;

        private static List<int> CollectAccessPcls(GameBuilding* building)
        {
            var result = new List<int>();
            var seen = new HashSet<int>();
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pcls = tiles.TileManager.PathConnectionGrid;

            int preferredTile = checked((int)building->r_TileIdOriginBottomRightInnerOne);
            AddPclForTile(preferredTile, requireWalkable: false, tiles, pcls, result, seen);

            for (int y = building->r_TilePositionYBegin - 1; y <= building->r_TilePositionYEnd + 1; y++)
            {
                for (int x = building->r_TilePositionXBegin - 1; x <= building->r_TilePositionXEnd + 1; x++)
                {
                    if ((x >= building->r_TilePositionXBegin && x <= building->r_TilePositionXEnd &&
                         y >= building->r_TilePositionYBegin && y <= building->r_TilePositionYEnd) ||
                        !tiles.IsTileInsideMapBounds(x, y))
                    {
                        continue;
                    }

                    AddPclForTile(tiles.GetTileId(x, y), requireWalkable: true, tiles, pcls, result, seen);
                }
            }
            result.Sort();
            return result;
        }

        private static void AddPclForTile(
            int tileId,
            bool requireWalkable,
            GameTileManagerAPI tiles,
            Span<ushort> pcls,
            List<int> result,
            HashSet<int> seen)
        {
            if (!tiles.IsValidTileId(tileId) || (uint)tileId >= (uint)pcls.Length ||
                (requireWalkable && !tiles.IsTileWalkableAndUnoccupied(tileId)))
            {
                return;
            }

            int pcl = pcls[tileId];
            if (pcl > 0 && seen.Add(pcl))
                result.Add(pcl);
        }

        private bool TryGetGateTopology(
            int playerId,
            out GateTopologySnapshot snapshot,
            out string failure)
        {
            if (gateTopologyCache.TryGetValue(playerId, out snapshot))
            {
                failure = string.Empty;
                return true;
            }

            var gates = new List<PclGateConnection>();
            int skippedNoOpGates = 0;
            GameBuildingManagerAPI buildingsApi = GameBuildingManagerAPI.Instance;
            GamePlayerManagerAPI playersApi = GamePlayerManagerAPI.Instance;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pcls = tiles.TileManager.PathConnectionGrid;
            SimpleNativeArray<GameGatehouseEntry> entries = buildingsApi.GetGatehouseArray();

            for (int index = 0; index < entries.Length; index++)
            {
                GameGatehouseEntry* gate = entries.GetValuePointer(index);
                if (gate == null || gate->r_BuildingId == 0 || gate->r_BuildingId > int.MaxValue)
                    continue;

                int gateBuildingId = (int)gate->r_BuildingId;
                if (!buildingsApi.IsValidId(gateBuildingId) ||
                    !buildingsApi.TryGetBuildingById(gateBuildingId, out GameBuilding* gateBuilding) ||
                    gateBuilding == null || gateBuilding->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                int gateOwnerId = gateBuilding->r_PlayerIdOwner;
                if (!playersApi.IsPlayerIdValid(gateOwnerId) ||
                    !playersApi.IsPlayerAlliedTo(playerId, gateOwnerId))
                {
                    continue;
                }

                if (gateBuilding->r_GlobalId == 0 || gate->r_GlobalId != gateBuilding->r_GlobalId)
                {
                    snapshot = null;
                    failure = "friendly-gate-global-id-mismatch";
                    return false;
                }

                int entryTile = checked((int)gate->r_EntryDoorTileId);
                int exitTile = checked((int)gate->r_ExitDoorTileId);
                if (!tiles.IsValidTileId(entryTile) || !tiles.IsValidTileId(exitTile) ||
                    (uint)entryTile >= (uint)pcls.Length || (uint)exitTile >= (uint)pcls.Length)
                {
                    snapshot = null;
                    failure = "friendly-gate-entry-exit-tile-invalid";
                    return false;
                }

                int entryPcl = pcls[entryTile];
                int exitPcl = pcls[exitTile];
                if (entryPcl <= 0 || exitPcl <= 0)
                {
                    snapshot = null;
                    failure = "friendly-gate-entry-exit-pcl-invalid";
                    return false;
                }
                if (entryPcl == exitPcl)
                {
                    skippedNoOpGates++;
                    continue;
                }

                gates.Add(new PclGateConnection(
                    entryPcl,
                    exitPcl,
                    ownerId: gateOwnerId,
                    buildingId: gateBuildingId,
                    globalId: gateBuilding->r_GlobalId));
            }

            gates.Sort(CompareGates);
            snapshot = new GateTopologySnapshot(
                gates,
                skippedNoOpGates);
            gateTopologyCache.Add(playerId, snapshot);
            failure = string.Empty;
            return true;
        }

        private static int CompareGates(PclGateConnection left, PclGateConnection right)
        {
            int comparison = left.GlobalId.CompareTo(right.GlobalId);
            if (comparison != 0)
                return comparison;
            comparison = left.BuildingId.CompareTo(right.BuildingId);
            if (comparison != 0)
                return comparison;
            comparison = left.OwnerId.CompareTo(right.OwnerId);
            if (comparison != 0)
                return comparison;
            comparison = left.First.CompareTo(right.First);
            if (comparison != 0)
                return comparison;
            return left.Second.CompareTo(right.Second);
        }

        private static string BuildPclKey(IReadOnlyList<int> pcls)
        {
            var builder = new StringBuilder(pcls.Count * 6);
            foreach (int pcl in pcls)
                builder.Append(pcl).Append(',');
            return builder.ToString();
        }

        private static string BuildDiagnosticDetails(
            IReadOnlyList<int> buildingPcls,
            IReadOnlyList<int> keepPcls,
            GateTopologySnapshot topology,
            GateBlockageEvaluation evaluation)
        {
            var builder = new StringBuilder(160);
            builder.Append("buildingPclCount=").Append(buildingPcls.Count)
                .Append(", keepPclCount=").Append(keepPcls.Count)
                .Append(", friendlyGateLinks=").Append(topology.Gates.Count)
                .Append(", skippedNoOpFriendlyGates=").Append(topology.SkippedNoOpGates)
                .Append(", usedGatePath=[");
            for (int pathIndex = 0; pathIndex < evaluation.UsedGateIndices.Length; pathIndex++)
            {
                if (pathIndex != 0)
                    builder.Append("->");
                int gateIndex = evaluation.UsedGateIndices[pathIndex];
                if ((uint)gateIndex >= (uint)topology.Gates.Count)
                {
                    builder.Append("invalid-index-").Append(gateIndex);
                    continue;
                }
                PclGateConnection gate = topology.Gates[gateIndex];
                builder.Append("gate#").Append(gate.BuildingId)
                    .Append("/owner#").Append(gate.OwnerId)
                    .Append('(').Append(gate.First).Append("<->").Append(gate.Second).Append(')');
            }
            builder.Append(']');
            return builder.ToString();
        }

        private bool IsNativePlayerAwareReachable(
            int playerId,
            int sourcePcl,
            int destinationPcl)
        {
            if (sourcePcl == destinationPcl)
                return true;

            ReachabilityKey key = new ReachabilityKey(playerId, sourcePcl, destinationPcl);
            if (reachabilityCache.TryGetValue(key, out bool reachable))
                return reachable;

            reachable = GamePlayerManagerAPI.Instance.GetNextReachablePCLToDestinationForPlayer(
                playerId,
                destinationPcl,
                sourcePcl,
                0) != 0;
            reachabilityCache[key] = reachable;
            return reachable;
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

        private sealed class KeepSnapshot
        {
            internal KeepSnapshot(uint globalId, List<int> accessPcls, string pclKey)
            {
                GlobalId = globalId;
                AccessPcls = accessPcls;
                PclKey = pclKey;
            }

            internal uint GlobalId { get; }
            internal List<int> AccessPcls { get; }
            internal string PclKey { get; }
        }

        private sealed class GateTopologySnapshot
        {
            internal GateTopologySnapshot(List<PclGateConnection> gates, int skippedNoOpGates)
            {
                Gates = gates;
                SkippedNoOpGates = skippedNoOpGates;
            }

            internal List<PclGateConnection> Gates { get; }
            internal int SkippedNoOpGates { get; }
        }

        private readonly struct ReachabilityKey : IEquatable<ReachabilityKey>
        {
            internal ReachabilityKey(int playerId, int sourcePcl, int destinationPcl)
            {
                PlayerId = playerId;
                SourcePcl = sourcePcl;
                DestinationPcl = destinationPcl;
            }

            private int PlayerId { get; }
            private int SourcePcl { get; }
            private int DestinationPcl { get; }
            public bool Equals(ReachabilityKey other) =>
                PlayerId == other.PlayerId && SourcePcl == other.SourcePcl &&
                DestinationPcl == other.DestinationPcl;
            public override bool Equals(object obj) => obj is ReachabilityKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((PlayerId * 397) ^ SourcePcl) * 397 ^ DestinationPcl;
                }
            }
        }

        private readonly struct ClassificationKey : IEquatable<ClassificationKey>
        {
            internal ClassificationKey(
                int buildingId,
                uint buildingGlobalId,
                uint keepGlobalId,
                int playerId,
                string buildingPclKey,
                string keepPclKey)
            {
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                KeepGlobalId = keepGlobalId;
                PlayerId = playerId;
                BuildingPclKey = buildingPclKey ?? string.Empty;
                KeepPclKey = keepPclKey ?? string.Empty;
            }

            private int BuildingId { get; }
            private uint BuildingGlobalId { get; }
            private uint KeepGlobalId { get; }
            private int PlayerId { get; }
            private string BuildingPclKey { get; }
            private string KeepPclKey { get; }
            public bool Equals(ClassificationKey other) =>
                BuildingId == other.BuildingId && BuildingGlobalId == other.BuildingGlobalId &&
                KeepGlobalId == other.KeepGlobalId && PlayerId == other.PlayerId &&
                string.Equals(BuildingPclKey, other.BuildingPclKey, StringComparison.Ordinal) &&
                string.Equals(KeepPclKey, other.KeepPclKey, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is ClassificationKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = BuildingId;
                    hash = hash * 397 ^ (int)BuildingGlobalId;
                    hash = hash * 397 ^ (int)KeepGlobalId;
                    hash = hash * 397 ^ PlayerId;
                    hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(BuildingPclKey);
                    return hash * 397 ^ StringComparer.Ordinal.GetHashCode(KeepPclKey);
                }
            }
        }

        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
