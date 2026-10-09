using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal int AllowBuildingCursorThroughCompletedMoat(
            IntPtr buildingManager,
            int buildingId,
            int unitId)
        {
            int vanillaResult = CallBuildingCursorWithRegions(buildingManager, buildingId, unitId);
            if (disposed)
                return vanillaResult;

            try
            {
                string reason = "vanilla-positive";
                int effectiveResult = vanillaResult;
                int playerId = -1;
                int targetX = -1;
                int targetY = -1;
                int targetTileId = -1;
                BuildingCursorTarget target = default;
                CursorGroupRouteSummary group = default;
                bool groupEvaluated = false;
                uint rawHoverBuildingTileId = 0;
                uint rawMouseTileId2 = 0;
                uint rawMouseTileId = 0;
                int rawMouseX = -1;
                int rawMouseY = -1;

                if (vanillaResult == 0)
                {
                    if (buildingId <= 0 || unitId <= 0 ||
                        !APIShared.UnitAccess.TryGetById(
                            unitId, out GameUnit* unit, out _) || unit == null ||
                        !APIShared.UnitAccess.IsReallyAlive(unit))
                    {
                        reason = "invalid-unit-or-building-id";
                    }
                    else
                    {
                        playerId = unit->r_ControllableForPlayerId;
                        GameCursorManager* cursorManager =
                            GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
                        uint rawBuildingId = cursorManager != null
                            ? cursorManager->r_HoverOverBuildingId : 0;
                        rawHoverBuildingTileId = cursorManager != null
                            ? cursorManager->r_HoverOverBuildingTileId : 0;
                        rawMouseTileId2 = cursorManager != null
                            ? cursorManager->r_MouseTileId2 : 0;
                        rawMouseTileId = cursorManager != null
                            ? cursorManager->r_MouseTileId : 0;
                        rawMouseX = cursorManager != null
                            ? unchecked((int)cursorManager->r_MouseTileX) : -1;
                        rawMouseY = cursorManager != null
                            ? unchecked((int)cursorManager->r_MouseTileY) : -1;
                        if (rawBuildingId != unchecked((uint)buildingId) ||
                            !TryResolveHostileLivingBuildingFromRawCursor(
                                playerId,
                                rawBuildingId,
                                rawHoverBuildingTileId,
                                rawMouseTileId2,
                                rawMouseTileId,
                                rawMouseX,
                                rawMouseY,
                                out targetX,
                                out targetY,
                                out targetTileId,
                                out target) ||
                            target.BuildingId != buildingId)
                        {
                            reason = "hover-building-not-exact-hostile-target";
                        }
                        else
                        {
                            int startX = unit->r_CurrentTilePositionX;
                            int startY = unit->r_CurrentTilePositionY;
                            int startTileId = startX >= 0 && startX < MapWidth &&
                                startY >= 0 && startY < MapWidth
                                ? GameTileManagerAPI.Instance.GetTileId(startX, startY)
                                : -1;
                            AttackCursorPairScope template = IsValidTileId(startTileId)
                                ? new AttackCursorPairScope(
                                    mapEpoch,
                                    unitId,
                                    playerId,
                                    startX,
                                    startY,
                                    startTileId,
                                    targetX,
                                    targetY,
                                    targetTileId,
                                    CursorPairFallbackKind.BuildingApproach,
                                    target.BuildingId,
                                    target.GlobalId,
                                    target.OwnerId,
                                    target.BuildingType,
                                    target.HoverTileId)
                                : null;
                            groupEvaluated = TryQualifySelectedGroupCursorRoute(
                                template, out _, out group);
                            if (groupEvaluated && group.AllowFallback &&
                                (ManualCommandsEnabled || group.DiggerUnits > 0))
                            {
                                effectiveResult = 1;
                                reason = "required-friendly-moat-building-route";
                            }
                            else
                            {
                                reason = groupEvaluated
                                    ? "no-legal-moat-relevant-group-route"
                                    : "group-route-not-evaluable";
                            }
                        }
                    }
                }

                // Diagnostic deduplication uses numeric fields; format only a new,
                // bounded detail entry, not every building hover frame.
                ulong key;
                unchecked
                {
                    key = (uint)buildingId;
                    key = key * 1099511628211UL ^ target.GlobalId;
                    key = key * 1099511628211UL ^ (uint)unitId;
                    key = key * 1099511628211UL ^ (uint)targetTileId;
                    key = key * 1099511628211UL ^ (uint)cursorSelectionRevision;
                    key = key * 1099511628211UL ^ (uint)vanillaResult;
                    key = key * 1099511628211UL ^ (uint)effectiveResult;
                }
                if (loggedBuildingCursorReachabilityDecisions.Count < 64 && loggedBuildingCursorReachabilityDecisions.Add(key))
                {
                    LogDetailedInfo(
                        $"Bugfixes and QoL stage=friendly-moat-movement-building-cursor-reachability " +
                        $"building={buildingId}/{target.GlobalId} type={target.BuildingType} " +
                        $"owner={target.OwnerId} unit={unitId} player={playerId} " +
                        $"hoverRaw=buildingTile:{rawHoverBuildingTileId}/" +
                        $"mouse2:{rawMouseTileId2}/mouse:{rawMouseTileId}/" +
                        $"xy:({rawMouseX},{rawMouseY}) " +
                        $"hoverResolved=({targetX},{targetY})/{targetTileId} " +
                        $"hoverTileSource={FormatBuildingHoverTileSource(target.HoverTileSource)} " +
                        $"groupEvaluated={groupEvaluated} selected={group.SelectedUnits} " +
                        $"diggers={group.DiggerUnits} legal={group.LegallyReachableUnits} " +
                        $"friendlyMoatSeparated={group.FriendlyMoatSeparatedUnits} " +
                        $"vanilla={vanillaResult} effective={effectiveResult} reason={reason}.");
                }

                return effectiveResult;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("building-cursor-reachability", ex);
                return vanillaResult;
            }
        }

        internal long ObserveCursorTilePairFallbackSelection(
            IntPtr selectionState, long vanillaResult)
        {
            pendingAttackCursorPair = null;
            if (vanillaResult != 0)
                return vanillaResult;
            if (disposed || selectionState == IntPtr.Zero)
                return vanillaResult;
            if (activeBuildingCursorConnectivity != null)
                return 1;

            try
            {
                bool hasVanillaDiggerSelection = selectionCanDigMoat(selectionState) != 0;

                int unitId = getRepresentativeSelectedUnit(selectionState, 1);
                int nextUnitId = *(int*)nativeUnitManager;
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                int playerId = -1;
                int startX = -1;
                int startY = -1;
                int startTileId = -1;
                int targetTileId = -1;
                GameCursorManager* cursorManager =
                    GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
                uint rawHoverBuildingId = cursorManager != null
                    ? cursorManager->r_HoverOverBuildingId : 0;
                uint rawHoverUnitId = cursorManager != null
                    ? cursorManager->r_HoverOverUnitId : 0;
                uint rawHoverBuildingTileId = cursorManager != null
                    ? cursorManager->r_HoverOverBuildingTileId : 0;
                uint rawMouseTileId2 = cursorManager != null
                    ? cursorManager->r_MouseTileId2 : 0;
                uint rawHoveringOverWall = cursorManager != null
                    ? cursorManager->r_HoveringOverWall : 0;
                uint rawMouseTileId = cursorManager != null
                    ? cursorManager->r_MouseTileId : 0;
                int rawMouseX = cursorManager != null
                    ? unchecked((int)cursorManager->r_MouseTileX) : -1;
                int rawMouseY = cursorManager != null
                    ? unchecked((int)cursorManager->r_MouseTileY) : -1;

                BuildingCursorTarget buildingTarget = default;
                bool hostileBuildingTarget = false;
                bool wallTarget = false;
                bool validTarget = targetX >= 0 && targetX < MapWidth &&
                    targetY >= 0 && targetY < MapWidth;
                GameUnit* unit = null;
                bool validUnit = unitId > 0 && unitId < nextUnitId &&
                    APIShared.UnitAccess.TryGetById(unitId, out unit, out _) && unit != null;
                int representativePlayerId = validUnit ? unit->r_ControllableForPlayerId : -1;
                if (hasVanillaDiggerSelection && (!validUnit || !CanDigMoat(unit)) &&
                    TryGetSelectedVanillaDigger(
                        unitId, representativePlayerId, out int diggerUnitId, out GameUnit* diggerUnit))
                {
                    unitId = diggerUnitId;
                    unit = diggerUnit;
                    validUnit = true;
                }
                if (validUnit)
                {
                    playerId = unit->r_ControllableForPlayerId;
                    startX = unit->r_CurrentTilePositionX;
                    startY = unit->r_CurrentTilePositionY;
                    startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                }
                if (validTarget)
                    targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);

                // Sprite overhangs can leave the dispatcher's global target at (0,0), although
                // Vanilla still reports the exact building ID. Bind such hovers to a verified
                // StructureGrid tile; the building approach probe still enumerates the footprint.
                if (validUnit && (!validTarget || !IsValidTileId(targetTileId)) &&
                    TryResolveHostileLivingBuildingFromRawCursor(
                        playerId,
                        rawHoverBuildingId,
                        rawHoverBuildingTileId,
                        rawMouseTileId2,
                        rawMouseTileId,
                        rawMouseX,
                        rawMouseY,
                        out int recoveredTargetX,
                        out int recoveredTargetY,
                        out int recoveredTargetTileId,
                        out BuildingCursorTarget recoveredBuilding))
                {
                    targetX = recoveredTargetX;
                    targetY = recoveredTargetY;
                    targetTileId = recoveredTargetTileId;
                    buildingTarget = recoveredBuilding;
                    hostileBuildingTarget = true;
                    validTarget = true;
                }

                int cursorPairTargetTileId = targetTileId;
                bool validPair = validUnit && validTarget &&
                    IsValidTileId(startTileId) && IsValidTileId(targetTileId);
                CursorPairFallbackKind fallbackKind = CursorPairFallbackKind.DirectTile;
                bool hostileUnitTarget = false;
                int hostileUnitId = -1;
                uint hostileUnitGlobalId = 0;
                bool occupiedByLivingUnit = false;
                if (validPair)
                {
                    // Vanilla's sprite hit-test owns entity identity. The tile below a unit
                    // sprite may be adjacent to the unit and is only the cursor-call binding.
                    hostileUnitTarget = TryResolveHostileLivingUnitFromRawCursor(
                        playerId,
                        rawHoverUnitId,
                        out hostileUnitId,
                        out hostileUnitGlobalId,
                        out int resolvedUnitX,
                        out int resolvedUnitY,
                        out int resolvedUnitTileId);
                    if (hostileUnitTarget)
                    {
                        occupiedByLivingUnit = true;
                        targetX = resolvedUnitX;
                        targetY = resolvedUnitY;
                        targetTileId = resolvedUnitTileId;
                    }
                    else
                    {
                        int tileUnitId = GameTileManagerAPI.Instance.GetTileUnitId(targetTileId);
                        hostileUnitTarget = tileUnitId > 0 && TryGetHostileLivingUnitAtTile(
                            playerId,
                            targetX,
                            targetY,
                            tileUnitId,
                            -1,
                            out hostileUnitId,
                            out occupiedByLivingUnit);
                        if (hostileUnitTarget &&
                            APIShared.UnitAccess.TryGetById(
                                hostileUnitId, out GameUnit* hostileUnit, out _) && hostileUnit != null)
                        {
                            hostileUnitGlobalId = hostileUnit->r_GlobalId;
                        }
                    }
                    if (!hostileBuildingTarget)
                    {
                        hostileBuildingTarget = TryGetHostileLivingBuildingForCursor(
                            playerId, targetTileId, out buildingTarget, out wallTarget);
                    }
                    // Walls are not reliably represented as living GameBuilding records.
                    // The cursor's dedicated wall field is the authoritative raw signal.
                    wallTarget |= rawHoveringOverWall != 0;
                    if (hostileBuildingTarget)
                        fallbackKind = CursorPairFallbackKind.BuildingApproach;
                    else if (hostileUnitTarget)
                        fallbackKind = CursorPairFallbackKind.UnitApproach;
                }

                AttackCursorPairScope candidateScope = validPair &&
                    (!occupiedByLivingUnit || hostileUnitTarget || hostileBuildingTarget ||
                     (tileFlags[targetTileId] & CursorSpecialStructureTileFlagMask) != 0)
                    ? new AttackCursorPairScope(
                        mapEpoch, unitId, playerId, startX, startY, startTileId,
                        targetX, targetY, targetTileId, fallbackKind,
                        buildingTarget.BuildingId, buildingTarget.GlobalId,
                        buildingTarget.OwnerId, buildingTarget.BuildingType,
                        buildingTarget.HoverTileId)
                    : null;
                if (candidateScope != null && hostileUnitTarget)
                {
                    candidateScope.TargetUnitId = hostileUnitId;
                    candidateScope.TargetUnitGlobalId = hostileUnitGlobalId;
                    candidateScope.CursorPairTargetTileId = cursorPairTargetTileId;
                }
                CursorGroupRouteSummary groupRoute = default;
                bool groupRouteEvaluated = false;
                bool ownerRoute;
                bool dedicatedBuildingReachability =
                    fallbackKind == CursorPairFallbackKind.BuildingApproach;
                if (vanillaResult == 0 && candidateScope != null &&
                    (ManualCommandsEnabled || hasVanillaDiggerSelection) &&
                    !dedicatedBuildingReachability)
                {
                    groupRouteEvaluated = TryQualifySelectedGroupCursorRoute(
                        candidateScope, out AttackCursorPairScope groupScope, out groupRoute);
                    ownerRoute = groupRouteEvaluated && groupRoute.AllowFallback;
                    if (groupRouteEvaluated) RecordCursorDecision(ownerRoute ? "region-connected" : "no-region-connection", candidateScope);
                    if (ownerRoute)
                        candidateScope = groupScope;
                }
                else if (dedicatedBuildingReachability)
                {
                    // B70C0 owns normal-building approach enumeration. Arming E2CA0 here is
                    // reentrant: B70C0 calls this selection helper again and replaces the scope.
                    ownerRoute = false;
                }
                else
                {
                    ownerRoute = false;
                }
                bool functionalArmed = candidateScope != null && ownerRoute &&
                    (ManualCommandsEnabled || hasVanillaDiggerSelection);
                if (candidateScope != null && (ManualCommandsEnabled || hasVanillaDiggerSelection) && !dedicatedBuildingReachability)
                    pendingAttackCursorPair = candidateScope;

                // A positive Vanilla answer is authoritative. Only a Vanilla rejection may be
                // lifted, and only after the route has already passed the owner-aware moat probe.
                if (vanillaResult == 0 && functionalArmed)
                    return 1;
            }
            catch (Exception ex)
            {
                pendingAttackCursorPair = null;
                LogFailure("cursor-selection", ex);
            }

            return vanillaResult;
        }

        internal int AllowAttackCursorTilePairThroughCompletedMoat(
            IntPtr pathManager, int targetTileId, int selectedUnitTileId, byte useCache)
        {
            AttackCursorPairScope scope = pendingAttackCursorPair;
            pendingAttackCursorPair = null;
            // E2CA0 may call D9C40 with 400000 nodes. A bound cursor must be answered
            // BEFORE calling it, while genuine attack/work consumers retain Vanilla.
            if (!disposed && activeAttackApproachDiagnostic == null && pathManager == nativePathManager)
            {
                try
                {
                    if (TryAnswerBuildingCursorPair(targetTileId, selectedUnitTileId, useCache, out int buildingResult))
                        return buildingResult;
                    if (scope != null && scope.MapEpoch == mapEpoch && useCache == 1 &&
                        scope.FallbackKind != CursorPairFallbackKind.BuildingApproach &&
                        CursorStartMatchesBoundSelection(scope, selectedUnitTileId) && CursorScopeMatchesTargetTile(scope, targetTileId) &&
                        TryQualifySelectedGroupCursorRoute(scope, out _, out CursorGroupRouteSummary group))
                    {
                        RecordCursorDecision(group.AllowFallback ? "region-connected" : "no-region-connection", scope);
                        return group.AllowFallback ? 1 : 0;
                    }
                }
                catch (Exception ex) { LogFailure("cursor-region-pair", ex); return 0; }
            }
            try { RecordCursorDecision("native-consumer-or-unbound", scope); } catch { /* Diagnostics must not interrupt native consumers. */ }
            int vanillaResult = originalCursorTilePairReachability(pathManager, targetTileId, selectedUnitTileId, useCache);
            if (activeAttackApproachDiagnostic != null)
                DiagnoseAttackApproachTilePair(activeAttackApproachDiagnostic, targetTileId, selectedUnitTileId, useCache, vanillaResult);
            return vanillaResult;
        }

        internal int AllowCursorRegionThroughCompletedMoat(IntPtr pathManager, int unitId)
        {
            // E9D90 probes structure exits; a boolean override violates its contract.
            return originalCursorRegionPrecheck(pathManager, unitId);
        }





        internal int AllowCursorReachabilityThroughCompletedMoat(
            IntPtr pathManager, int unitId, int targetX, int targetY)
        {
            // E9FF0 also writes exit coordinates. Leave both result and outputs native.
            return originalCursorReachability(pathManager, unitId, targetX, targetY);
        }

        internal enum BuildingHoverTileSource
        {
            None,
            BuildingTile,
            MouseTile2,
            MouseTile,
            NearestFootprint
        }

        internal enum CursorPairFallbackKind
        {
            UnitApproach,
            BuildingApproach,
            DirectTile
        }

        internal sealed class AttackCursorPairScope
        {
            public AttackCursorPairScope(
                int mapEpoch,
                int unitId,
                int playerId,
                int startX,
                int startY,
                int startTileId,
                int targetX,
                int targetY,
                int targetTileId,
                CursorPairFallbackKind fallbackKind,
                int buildingId = 0,
                uint buildingGlobalId = 0,
                int buildingOwnerId = -1,
                eStructs buildingType = eStructs.STRUCT_NULL,
                int hoverBuildingTileId = -1)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                PlayerId = playerId;
                StartX = startX;
                StartY = startY;
                StartTileId = startTileId;
                TargetX = targetX;
                TargetY = targetY;
                TargetTileId = targetTileId;
                FallbackKind = fallbackKind;
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                BuildingOwnerId = buildingOwnerId;
                BuildingType = buildingType;
                HoverBuildingTileId = hoverBuildingTileId;
                CursorPairTargetTileId = targetTileId;
            }

            public void SetSource(SelectedCursorUnitSnapshot unit)
            {
                UnitId = unit.UnitId; StartX = unit.StartX; StartY = unit.StartY; StartTileId = unit.StartTileId;
            }

            public int MapEpoch { get; }
            public int UnitId { get; internal set; }
            public int PlayerId { get; }
            public int StartX { get; internal set; }
            public int StartY { get; internal set; }
            public int StartTileId { get; internal set; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int TargetTileId { get; }
            public CursorPairFallbackKind FallbackKind { get; }
            public int BuildingId { get; }
            public uint BuildingGlobalId { get; }
            public int BuildingOwnerId { get; }
            public eStructs BuildingType { get; }
            public int HoverBuildingTileId { get; }
            public int CursorPairTargetTileId { get; set; }
            public bool GroupCursorAuthorized { get; set; }
            public string GroupSelectionSignature { get; set; }
            public int TargetUnitId { get; set; } = -1;
            public uint TargetUnitGlobalId { get; set; }
        }

        internal struct BuildingCursorTarget
        {
            public int BuildingId;
            public uint GlobalId;
            public int OwnerId;
            public eStructs BuildingType;
            public int HoverTileId;
            public BuildingHoverTileSource HoverTileSource;
        }

    }
}
