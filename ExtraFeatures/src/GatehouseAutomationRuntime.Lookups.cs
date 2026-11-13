using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed unsafe partial class GatehouseAutomationRuntime
    {
        private static readonly HashSet<int> NoManualGateIds = new HashSet<int>();
        private HashSet<int> manualGateIdsSnapshot = NoManualGateIds;

        private static int gateQueryMapGeneration;
        [ThreadStatic] private static int gateQueryRunDepth;
        [ThreadStatic] private static int cachedMapGeneration;
        [ThreadStatic] private static int cachedBuildingId;
        [ThreadStatic] private static uint cachedGlobalId;
        [ThreadStatic] private static PathConnectionRecord* cachedPathConnection;

        // Each native building pass queries a gate's candidates consecutively. The active
        // record remains valid in that loop. A nested run can change it before the outer resumes.
        private static void BeginGateQueryRun()
        {
            gateQueryRunDepth++;
            cachedPathConnection = null;
        }

        private static void EndGateQueryRun()
        {
            cachedPathConnection = null;
            gateQueryRunDepth--;
        }

        private static void InvalidateGateQueryCache() => Interlocked.Increment(ref gateQueryMapGeneration);

        private bool HasLivePathConnectionForQuery(int buildingId, GameBuilding* building)
        {
            if (building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE)
                return true;
            if (gateQueryRunDepth != 1 || !mapActive || editorSessionActive)
                return TryGetLivePathConnection(buildingId, building, out _);

            int mapGeneration = Volatile.Read(ref gateQueryMapGeneration);
            uint globalId = building->r_GlobalId;
            if (cachedPathConnection != null && cachedMapGeneration == mapGeneration &&
                cachedBuildingId == buildingId && cachedGlobalId == globalId &&
                IsCurrentPathConnection(cachedPathConnection, buildingId, globalId))
            {
                return true;
            }

            if (!TryGetLivePathConnection(buildingId, building, out PathConnectionRecord* record))
            {
                cachedPathConnection = null;
                return false;
            }

            cachedMapGeneration = mapGeneration;
            cachedBuildingId = buildingId;
            cachedGlobalId = globalId;
            cachedPathConnection = record;
            return true;
        }

        // Call only while holding stateLock. Published sets are never mutated.
        private void PublishManualGateIdsLocked() =>
            Volatile.Write(ref manualGateIdsSnapshot, manualOnlyGateGlobalIds.Count == 0
                ? NoManualGateIds
                : new HashSet<int>(manualOnlyGateGlobalIds));

        private bool IsManualOnlyBuilding(int buildingId)
        {
            if (!automationReady || !mapActive || loadedMapStatePending ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                return false;
            }

            HashSet<int> ids = Volatile.Read(ref manualGateIdsSnapshot);
            return ids.Count != 0 &&
                TryGetLiveGatehouseBuilding(buildingId, out GameBuilding* building) &&
                ids.Contains((int)building->r_GlobalId) &&
                (building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE ||
                    TryGetLivePathConnection(buildingId, building, out _));
        }

        private static bool TryGetLiveGatehouse(int buildingId, out GameBuilding* building, out PathConnectionRecord* gatehouse)
        {
            gatehouse = null;
            return TryGetLiveGatehouseBuilding(buildingId, out building) &&
                (building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE ||
                    TryGetLivePathConnection(buildingId, building, out gatehouse));
        }

        private static bool TryGetLivePathConnection(int buildingId, GameBuilding* building, out PathConnectionRecord* record) =>
            GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(buildingId, out record) &&
            IsCurrentPathConnection(record, buildingId, building->r_GlobalId);

        private static bool IsCurrentPathConnection(PathConnectionRecord* record, int buildingId, uint globalId) =>
            record != null && record->r_IsActive != 0 && record->r_BuildingId == buildingId &&
            record->r_SubjectGlobalId == globalId;

        // Building-array checks of TryGetLiveGatehouse without the path-record lookup.
        private static bool TryGetLiveGatehouseBuilding(int buildingId, out GameBuilding* building)
        {
            building = null;
            if (buildingId <= 0 || !global::APIShared.BuildingAccess.TryGetById(buildingId, out building, out _) ||
                building == null || building->r_AliveState != AliveState.IsAlive || building->r_GlobalId == 0 ||
                building->r_GlobalId > int.MaxValue ||
                (building->r_BuildingType != eStructs.STRUCT_DRAWBRIDGE && !IsGatehouseType(building->r_BuildingType)))
            {
                building = null;
                return false;
            }
            return true;
        }

        private static bool IsGatehouseType(eStructs type) =>
            type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER ||
            type == eStructs.STRUCT_GATE_WOOD || type == eStructs.STRUCT_GATE_POSTERN ||
            type == eStructs.STRUCT_GATEHOUSE;
    }
}
