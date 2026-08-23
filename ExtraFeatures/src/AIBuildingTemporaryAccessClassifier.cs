// Feature: Classify AI building access while treating closed friendly gates as virtual links.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Zhuqiaomon.Memory;

namespace ExtraFeatures
{
    internal readonly struct AIBuildingAccessDiagnostic
    {
        internal AIBuildingAccessDiagnostic(
            GateBlockageEvaluationKind kind,
            int tick,
            int topologyHash,
            string details)
        {
            Kind = kind;
            Tick = tick;
            TopologyHash = topologyHash;
            Details = details ?? string.Empty;
        }

        internal GateBlockageEvaluationKind Kind { get; }
        internal int Tick { get; }
        internal int TopologyHash { get; }
        internal string Details { get; }
        internal bool IsOnlyTemporarilyBlocked =>
            Kind == GateBlockageEvaluationKind.TemporaryViaClosedFriendlyGate;
    }

    internal sealed unsafe class AIBuildingTemporaryAccessClassifier
    {
        private readonly ManualLogSource log;
        private readonly bool supportedDll;
        private readonly Dictionary<ReachabilityKey, bool> reachabilityCache =
            new Dictionary<ReachabilityKey, bool>();
        private readonly Dictionary<ClassificationKey, AIBuildingAccessDiagnostic> classificationCache =
            new Dictionary<ClassificationKey, AIBuildingAccessDiagnostic>();
        private int cacheTick = int.MinValue;
        private bool failureLogged;

        internal AIBuildingTemporaryAccessClassifier(ManualLogSource log, bool supportedDll)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.supportedDll = supportedDll;
        }

        internal bool TryClassify(int buildingId, out AIBuildingAccessDiagnostic diagnostic)
        {
            diagnostic = default;
            if (!supportedDll)
                return false;

            try
            {
                GameBuildingManagerAPI buildingsApi = GameBuildingManagerAPI.Instance;
                if (!buildingsApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building == null || building->r_AliveState != AliveState.IsAlive ||
                    building->r_GlobalId == 0 || building->r_PlayerIdOwner == 0 ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(building->r_PlayerIdOwner))
                {
                    return false;
                }

                int playerId = building->r_PlayerIdOwner;
                if (!TryFindKeep(playerId, out GameBuilding* keep) || keep == null)
                    return false;

                List<int> buildingPcls = CollectAccessPcls(building);
                List<int> keepPcls = CollectAccessPcls(keep);
                if (buildingPcls.Count == 0 || keepPcls.Count == 0)
                    return false;

                int tick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
                if (tick != cacheTick)
                {
                    reachabilityCache.Clear();
                    classificationCache.Clear();
                    cacheTick = tick;
                }

                if (!TryCollectClosedFriendlyGates(playerId, out List<PclGateConnection> gates, out int topologyHash))
                    return false;

                ClassificationKey key = new ClassificationKey(
                    buildingId,
                    building->r_GlobalId,
                    keep->r_GlobalId,
                    playerId,
                    topologyHash);
                if (classificationCache.TryGetValue(key, out diagnostic))
                    return true;

                GateBlockageEvaluation evaluation = TemporaryGateBlockagePolicy.Evaluate(
                    buildingPcls,
                    keepPcls,
                    gates,
                    (source, destination) => IsNormallyReachable(playerId, source, destination));
                diagnostic = new AIBuildingAccessDiagnostic(
                    evaluation.Kind,
                    tick,
                    topologyHash,
                    BuildDiagnosticDetails(buildingPcls, keepPcls, gates, evaluation.UsedGateIndices));
                classificationCache[key] = diagnostic;
                return true;
            }
            catch (Exception ex)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    log.LogError(
                        $"[{TimestampNow()}] Extra Features temporary AI building-access classification failed; " +
                        $"this demolition uses vanilla behavior: {ex}");
                }
                return false;
            }
        }

        private static bool TryFindKeep(int playerId, out GameBuilding* keep)
        {
            keep = null;
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding candidate = ref buildings[index];
                if (candidate.r_AliveState == AliveState.IsAlive &&
                    candidate.r_PlayerIdOwner == playerId &&
                    candidate.r_GlobalId != 0 &&
                    IsKeep(candidate.r_BuildingType))
                {
                    return GameBuildingManagerAPI.Instance.TryGetBuildingById(index + 1, out keep) && keep != null;
                }
            }
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
            List<int> result = new List<int>();
            HashSet<int> seen = new HashSet<int>();
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

        private static bool TryCollectClosedFriendlyGates(
            int playerId,
            out List<PclGateConnection> gates,
            out int topologyHash)
        {
            gates = new List<PclGateConnection>();
            topologyHash = 17;
            GameBuildingManagerAPI buildingsApi = GameBuildingManagerAPI.Instance;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pcls = tiles.TileManager.PathConnectionGrid;
            SimpleNativeArray<GameGatehouseEntry> entries = buildingsApi.GetGatehouseArray();

            for (int index = 0; index < entries.Length; index++)
            {
                GameGatehouseEntry* gate = entries.GetValuePointer(index);
                if (gate == null || gate->r_BuildingId == 0 || gate->r_IsOpen != 0 ||
                    gate->r_BuildingId > int.MaxValue)
                {
                    continue;
                }

                int gateBuildingId = (int)gate->r_BuildingId;
                if (!buildingsApi.TryGetBuildingById(gateBuildingId, out GameBuilding* gateBuilding) ||
                    gateBuilding == null || gateBuilding->r_AliveState != AliveState.IsAlive ||
                    gateBuilding->r_PlayerIdOwner != playerId || gateBuilding->r_GlobalId == 0 ||
                    gate->r_GlobalId != gateBuilding->r_GlobalId)
                {
                    continue;
                }

                int entryTile = checked((int)gate->r_EntryDoorTileId);
                int exitTile = checked((int)gate->r_ExitDoorTileId);
                if (!tiles.IsValidTileId(entryTile) || !tiles.IsValidTileId(exitTile) ||
                    (uint)entryTile >= (uint)pcls.Length || (uint)exitTile >= (uint)pcls.Length)
                {
                    return false;
                }

                int entryPcl = pcls[entryTile];
                int exitPcl = pcls[exitTile];
                if (entryPcl <= 0 || exitPcl <= 0 || entryPcl == exitPcl)
                    return false;

                gates.Add(new PclGateConnection(
                    entryPcl,
                    exitPcl,
                    gateBuildingId,
                    gateBuilding->r_GlobalId));
                unchecked
                {
                    topologyHash = topologyHash * 31 + (int)gateBuilding->r_GlobalId;
                    topologyHash = topologyHash * 31 + entryPcl;
                    topologyHash = topologyHash * 31 + exitPcl;
                }
            }
            return true;
        }

        private static string BuildDiagnosticDetails(
            IReadOnlyList<int> buildingPcls,
            IReadOnlyList<int> keepPcls,
            IReadOnlyList<PclGateConnection> gates,
            IReadOnlyList<int> usedGateIndices)
        {
            StringBuilder builder = new StringBuilder(256);
            builder.Append("buildingPcls=");
            AppendIntList(builder, buildingPcls);
            builder.Append(", keepPcls=");
            AppendIntList(builder, keepPcls);
            builder.Append(", closedFriendlyGateLinks=");
            builder.Append(gates.Count);
            builder.Append(", gates=[");
            int shownGates = Math.Min(gates.Count, 16);
            for (int index = 0; index < shownGates; index++)
            {
                if (index != 0)
                    builder.Append(';');
                PclGateConnection gate = gates[index];
                builder.Append("buildingId=").Append(gate.BuildingId)
                    .Append("/globalId=").Append(gate.GlobalId)
                    .Append("/state=closed")
                    .Append("/entryExitPcls=").Append(gate.First).Append("<->").Append(gate.Second);
            }
            if (gates.Count > shownGates)
                builder.Append(";...");
            builder.Append("], usedGatePath=[");
            for (int pathIndex = 0; pathIndex < usedGateIndices.Count; pathIndex++)
            {
                if (pathIndex != 0)
                    builder.Append("->");
                int gateIndex = usedGateIndices[pathIndex];
                if ((uint)gateIndex >= (uint)gates.Count)
                {
                    builder.Append("invalid-index-").Append(gateIndex);
                    continue;
                }
                PclGateConnection gate = gates[gateIndex];
                builder.Append("gate#").Append(gate.BuildingId)
                    .Append('(').Append(gate.First).Append("<->").Append(gate.Second).Append(')');
            }
            builder.Append(']');
            return builder.ToString();
        }

        private static void AppendIntList(StringBuilder builder, IReadOnlyList<int> values)
        {
            builder.Append('[');
            int shownValues = Math.Min(values.Count, 24);
            for (int index = 0; index < shownValues; index++)
            {
                if (index != 0)
                    builder.Append(',');
                builder.Append(values[index]);
            }
            if (values.Count > shownValues)
                builder.Append(",...");
            builder.Append(']');
        }

        private bool IsNormallyReachable(int playerId, int sourcePcl, int destinationPcl)
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
                PlayerId == other.PlayerId && SourcePcl == other.SourcePcl && DestinationPcl == other.DestinationPcl;
            public override bool Equals(object obj) => obj is ReachabilityKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return ((PlayerId * 397) ^ SourcePcl) * 397 ^ DestinationPcl; }
            }
        }

        private readonly struct ClassificationKey : IEquatable<ClassificationKey>
        {
            internal ClassificationKey(int buildingId, uint buildingGlobalId, uint keepGlobalId, int playerId, int topologyHash)
            {
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                KeepGlobalId = keepGlobalId;
                PlayerId = playerId;
                TopologyHash = topologyHash;
            }

            private int BuildingId { get; }
            private uint BuildingGlobalId { get; }
            private uint KeepGlobalId { get; }
            private int PlayerId { get; }
            private int TopologyHash { get; }
            public bool Equals(ClassificationKey other) =>
                BuildingId == other.BuildingId && BuildingGlobalId == other.BuildingGlobalId &&
                KeepGlobalId == other.KeepGlobalId && PlayerId == other.PlayerId &&
                TopologyHash == other.TopologyHash;
            public override bool Equals(object obj) => obj is ClassificationKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = BuildingId;
                    hash = hash * 397 ^ (int)BuildingGlobalId;
                    hash = hash * 397 ^ (int)KeepGlobalId;
                    hash = hash * 397 ^ PlayerId;
                    return hash * 397 ^ TopologyHash;
                }
            }
        }

        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
