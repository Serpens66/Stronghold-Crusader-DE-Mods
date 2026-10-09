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
        internal int AllowBuilderAfterFailedRegionSearch(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY)
        {
            int vanillaResult = originalRegionReachability(pathManager, movementClass, targetRegion, startX, startY);
            // 11B520's original moat branch chooses 118E00 on zero. Preserve
            // the flood's native outputs, then choose its per-unit fallback for
            // this qualified manual group. Unit planners retain the native answer.
            if (IsNativeManualGroupFlood(pathManager, movementClass, targetRegion, startX, startY))
                return 0;
            // Baseline semantics: argument 2 is the player filter here, despite this legacy
            // delegate parameter name used by the older movement diagnostics.
            if (TryAllowDigWorkRegionSearch(
                    pathManager, movementClass, targetRegion, startX, startY,
                    vanillaResult, out int workResult))
            {
                return workResult;
            }
            if (TryAllowUnitMoveRegion(pathManager, movementClass, targetRegion, startX, startY, vanillaResult, out int unitResult))
                return unitResult;
            if (TryAllowEarlyMoveHereGroupRegion(
                    pathManager, movementClass, targetRegion, startX, startY,
                    vanillaResult, out int moveHereResult))
            {
                return moveHereResult;
            }
            PlanScope plan = activePlan ?? pendingPlan;
            bool scoped = activeMoveCommand != null ||
                (plan != null && plan.FriendlyRouteQualified);
            if (disposed || !scoped)
                return vanillaResult;

            if (plan == null ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) ||
                unit == null || !CanDigMoat(unit))
            {
                return vanillaResult;
            }

            if (activeMoveCommand != null)
                activeMoveCommand.RegionCalls++;

            try
            {
                bool bypass = vanillaResult == 0 && plan.FriendlyRouteQualified &&
                    plan.ModeObserved && *moatPathMode == 1 &&
                    targetRegion > 0 && targetRegion <= MaximumRegionId;
                if (!bypass)
                    return vanillaResult;

                if (ShouldLogUnitPipeline)
                    LogMovementContext(
                        $"stage=region movementClass={movementClass} start=({startX},{startY}) " +
                        $"targetRegion={targetRegion} vanilla=0 effective={targetRegion}");
                return targetRegion;
            }
            catch (Exception ex)
            {
                LogFailure("region", ex);
                return vanillaResult;
            }
        }

        internal bool TryAllowEarlyMoveHereGroupRegion(
            IntPtr pathManager,
            int playerId,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            out int effectiveResult)
        {
            effectiveResult = vanillaResult;
            MoveCommandScope command = activeMoveCommand;
            if (vanillaResult != 0 || command == null || disposed ||
                activePlan != null || pendingPlan != null ||
                pathManager != nativePathManager ||
                startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth ||
                targetRegion <= 0 || targetRegion > MaximumRegionId)
            {
                return false;
            }

            try
            {
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                    return false;
                EnsureMoveCommandGroupSummary(command);
                int[] unitIds = command.ActiveUnitIdsAtDispatch;
                if (unitIds.Length == 0 ||
                    !GroupContainsCurrentPosition(unitIds, playerId, startX, startY))
                {
                    return false;
                }

                command.RegionCalls++;
                EarlyGroupRegionDecision decision = EvaluateEarlyMoveHereGroupRegion(
                    command, unitIds, playerId, targetRegion, null, "E7C40");
                try
                {
                    LogEarlyMoveHereGroupRegionDecision(
                        command, decision, playerId, null, targetRegion, "E7C40");
                }
                catch
                {
                    // Diagnostics must not undo an otherwise valid scoped decision.
                }
                if (!decision.Allowed)
                    return false;

                command.EarlyRegionBypasses++;
                effectiveResult = targetRegion;
                return true;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("movehere-group-region-search", ex);
                effectiveResult = vanillaResult;
                return false;
            }
        }

        internal EarlyGroupRegionDecision EvaluateEarlyMoveHereGroupRegion(
            MoveCommandScope command,
            int[] unitIds,
            int playerId,
            int targetRegion,
            int? sourceRegion,
            string helper)
        {
            command.EarlyRegionCalls++;
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(
                command.TargetX, command.TargetY);
            if (!IsValidTileId(targetTileId) || pathRegionGrid[targetTileId] != targetRegion)
            {
                return new EarlyGroupRegionDecision(
                    false, 0, "command-target-region-mismatch", new RouteProbeSummary(playerId));
            }

            string key = $"{helper}:{playerId}:{sourceRegion?.ToString() ?? "none"}:" +
                $"{targetRegion}:{command.TargetX}:{command.TargetY}";
            if (command.EarlyRegionDecisions.TryGetValue(
                    key, out EarlyGroupRegionDecision cached))
            {
                return cached;
            }

            RouteProbeSummary observed = new RouteProbeSummary(playerId);
            int diggers = 0;
            foreach (int unitId in unitIds)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != command.TribeId ||
                    unit->r_ControllableForPlayerId != playerId || !CanDigMoat(unit))
                {
                    continue;
                }

                int startTileId = GameTileManagerAPI.Instance.GetTileId(
                    unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                if (!IsValidTileId(startTileId))
                    continue;
                int unitSourceRegion = pathRegionGrid[startTileId];
                if (sourceRegion.HasValue && unitSourceRegion != sourceRegion.Value)
                    continue;

                diggers++;
                var plan = new PlanScope(unitId, command.TargetX, command.TargetY)
                {
                    PlayerId = playerId,
                    VanillaFailureProven = RequiredOnlyMode
                };
                if (TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out RouteProbeSummary summary))
                {
                    observed.MergeObservations(summary);
                    var allowed = new EarlyGroupRegionDecision(
                        true, unitId, "required-friendly-moat-route", summary);
                    command.EarlyRegionDecisions[key] = allowed;
                    MarkCommandMoatRelevant(command, summary);
                    return allowed;
                }
                observed.MergeObservations(summary);
            }

            var rejected = new EarlyGroupRegionDecision(
                false,
                0,
                diggers == 0 ? "no-matching-digger" : "no-required-friendly-moat-route",
                observed);
            command.EarlyRegionDecisions[key] = rejected;
            return rejected;
        }

        internal bool GroupContainsCurrentPosition(
            int[] unitIds, int playerId, int startX, int startY)
        {
            foreach (int unitId in unitIds)
            {
                if (APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) &&
                    unit != null && APIShared.UnitAccess.IsReallyAlive(unit) &&
                    unit->r_ControllableForPlayerId == playerId &&
                    unit->r_CurrentTilePositionX == startX &&
                    unit->r_CurrentTilePositionY == startY)
                {
                    return true;
                }
            }
            return false;
        }

        internal void LogEarlyMoveHereGroupRegionDecision(
            MoveCommandScope command,
            EarlyGroupRegionDecision decision,
            int playerId,
            int? sourceRegion,
            int targetRegion,
            string helper)
        {
            string signature = $"early-group-region:{helper}:{playerId}:" +
                $"{sourceRegion?.ToString() ?? "none"}:{targetRegion}:" +
                $"{decision.Allowed}:{decision.UnitId}:{decision.Reason}";
            if (!command.EarlyRegionLogSignatures.Add(signature))
                return;
            LogCommandDiagnostic(
                $"stage=movehere-group-region commandSeq={command.Sequence} " +
                $"tribe={command.TribeId} player={playerId} helper={helper} " +
                $"regions={sourceRegion?.ToString() ?? "coordinates"}->{targetRegion} " +
                $"target=({command.TargetX},{command.TargetY}) vanilla=0 " +
                $"effective={(decision.Allowed ? targetRegion : 0)} " +
                $"qualifyingUnit={decision.UnitId} reason={decision.Reason} " +
                decision.Summary.ToLogFields());
        }

        internal int AllowTribeFloodFillForMoveOrder(IntPtr tribeManager, int tribeId, int floodFillStamp)
        {
            int vanillaResult = originalTribeFloodFillMembership(tribeManager, tribeId, floodFillStamp);
            if (disposed)
                return vanillaResult;

            try
            {
                MoveCommandScope command = activeMoveCommand;
                if (vanillaResult == 0)
                    EnsureMoveCommandGroupSummary(command);
                bool managerValid = tribeManager != IntPtr.Zero;
                bool matchingTribe = command != null && tribeId == command.TribeId;
                bool stampValid = floodFillStamp > 0 && floodFillStamp <= MaximumFloodFillStamp;
                bool bypass = vanillaResult == 0 && managerValid && matchingTribe && stampValid &&
                    command.DiggersAtDispatch > 0 &&
                    TryQualifyMoveCommandFloodBypass(command);
                if (command != null)
                {
                    command.FloodCalls++;
                    if (vanillaResult != 0)
                        command.FloodVanillaPositive++;
                }
                if (!bypass)
                    return vanillaResult;

                command.FloodFillBypasses++;
                try
                {
                    if (ShouldLogUnitPipeline)
                        LogMovementContext(
                            $"stage=tribe-flood-fill tribe={tribeId} stamp={floodFillStamp} vanilla=0 effective=1");
                }
                catch
                {
                    // Diagnostics must not undo an otherwise valid scoped bypass.
                }
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("tribe-flood-fill", ex);
                return vanillaResult;
            }
        }

        internal int SelectOwnerSafeGroupMoatMode(IntPtr tribeManager, int tribeId)
        {
            int vanillaResult = originalFirstGroupUnitOnCompletedMoat(tribeManager, tribeId);
            if (disposed)
                return vanillaResult;

            MoveCommandScope command = activeMoveCommand;
            if (command?.NativeCommonFallback == true && tribeManager == nativeTribeManager &&
                command.TribeId == tribeId && GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* manualTribe) &&
                manualTribe != null && Array.IndexOf(command.ActiveUnitIdsAtDispatch, manualTribe->r_LeaderUnitId) >= 0)
                return manualTribe->r_LeaderUnitId;
            if (!TraversalEnabled) return vanillaResult;
            if (command == null || command.TribeId != tribeId ||
                tribeManager == IntPtr.Zero || tribeManager != nativeTribeManager ||
                tribeId < 0 || tribeId >= MaximumTribeCount || getGroupUnitId == null)
            {
                return vanillaResult;
            }

            try
            {
                byte* tribeRecord = (byte*)tribeManager.ToPointer() +
                    (tribeId * TribeRecordSize);
                int leadUnitId = *(short*)(tribeRecord + TribeLeadUnitIdOffset);
                int unitCount = *(short*)(tribeRecord + TribeUnitCountOffset);
                if (leadUnitId <= 0 || unitCount <= 0 || unitCount > MaximumUnitCount)
                    return vanillaResult;

                // This helper returns a unit ID, not a boolean success result. Fast may
                // bypass the group scan only when Vanilla already selected the leader;
                // a later moat member would otherwise leave 11B520 on its ground branch.
                if (RequiredOnlyMode && vanillaResult == leadUnitId)
                {
                    fastVanillaBypasses++;
                    return vanillaResult;
                }

                EnsureMoveCommandGroupSummary(command);

                // A ground leader and a later moat member still need qualification.
                // Returning here used to strand the whole tribe on an isolated PCL.
                command.MoatRelevant |= vanillaResult > 0;

                int activeUnitsOnMoat = 0;
                int activeUnitsOffMoat = 0;
                int diggerUnits = 0;
                int qualifyingDiggerUnitsOnMoat = 0;
                int qualifyingUnitId = 0;
                bool leadUnitObserved = false;
                RouteProbeSummary qualifyingRoute = default;
                var probedDiggerStarts = new HashSet<int>();
                for (int ordinal = 0; ordinal < unitCount; ordinal++)
                {
                    int unitId = getGroupUnitId(tribeManager, tribeId, ordinal);
                    if (unitId <= 0 ||
                        !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                        unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                        *(ushort*)((byte*)unit + UnitGroupInactiveStateOffset) != 0)
                    {
                        continue;
                    }

                    int startX = unit->r_CurrentTilePositionX;
                    int startY = unit->r_CurrentTilePositionY;
                    if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                        continue;
                    int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                    if (!IsValidTileId(startTileId))
                        continue;
                    if (unitId == leadUnitId)
                        leadUnitObserved = true;

                    bool onCompletedMoat =
                        (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
                    if (onCompletedMoat)
                        activeUnitsOnMoat++;
                    else
                        activeUnitsOffMoat++;

                    if (!CanDigMoat(unit))
                        continue;
                    diggerUnits++;
                    // Qualify a required crossing before choosing a native group branch;
                    // both island starters and actual moat starters can supply evidence.
                    if (qualifyingUnitId != 0)
                        continue;
                    int startRegion = pathRegionGrid[startTileId];
                    int startKey = startRegion > 0 ? startRegion : -startTileId - 1;
                    if (!probedDiggerStarts.Add(startKey))
                        continue;

                    PlanScope probe = new PlanScope(unitId, command.TargetX, command.TargetY)
                    {
                        PlayerId = unit->r_ControllableForPlayerId,
                        VanillaFailureProven = RequiredOnlyMode && vanillaResult == 0
                    };
                    if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                            probe, out RouteProbeSummary route))
                    {
                        continue;
                    }

                    if (onCompletedMoat)
                        qualifyingDiggerUnitsOnMoat++;
                    qualifyingUnitId = unitId;
                    qualifyingRoute = route;
                }

                bool mixedPositions = activeUnitsOnMoat > 0 && activeUnitsOffMoat > 0;
                int targetTileId = command.TargetX >= 0 && command.TargetX < MapWidth &&
                    command.TargetY >= 0 && command.TargetY < MapWidth
                        ? GameTileManagerAPI.Instance.GetTileId(
                            command.TargetX, command.TargetY)
                        : 0;
                int targetRegion = IsValidTileId(targetTileId)
                    ? pathRegionGrid[targetTileId]
                    : 0;
                bool targetIsFriendlyCompletedMoat = IsValidTileId(targetTileId) &&
                    IsCompletedMoatTile(targetTileId) && qualifyingUnitId > 0 &&
                    APIShared.UnitAccess.TryGetById(
                        qualifyingUnitId, out GameUnit* qualifyingUnit, out _) &&
                    qualifyingUnit != null &&
                    ResolveCompletedMoatRelationship(
                        qualifyingUnit->r_ControllableForPlayerId, targetTileId) ==
                        CompletedMoatRelationship.Friendly;
                bool forceSharedMoatMode = vanillaResult != leadUnitId && leadUnitObserved &&
                    ((targetRegion > 0 && targetRegion <= MaximumRegionId) ||
                     targetIsFriendlyCompletedMoat) &&
                    qualifyingUnitId > 0;
                bool normalize = vanillaResult > 0 && mixedPositions &&
                    qualifyingUnitId > 0;
                if (vanillaResult > 0 || forceSharedMoatMode)
                {
                    command.MoatRelevant = true;
                    MarkCommandMoatRelevant(command, qualifyingRoute);
                }
                int effectiveResult = forceSharedMoatMode
                    ? leadUnitId
                    : normalize ? 0 : vanillaResult;
                string decision = forceSharedMoatMode
                    ? "forced=shared-friendly-moat"
                    : normalize ? "normalized=ground-per-unit" : "vanilla";
                string diagnostic =
                    $"stage=group-moat-mode tribe={tribeId} target=({command.TargetX},{command.TargetY}) " +
                    $"targetRegion={targetRegion} " +
                    $"lead={leadUnitId} vanillaFirstMoat={vanillaResult} onMoat={activeUnitsOnMoat} " +
                    $"offMoat={activeUnitsOffMoat} diggers={diggerUnits} " +
                    $"qualifyingDiggersOnMoat={qualifyingDiggerUnitsOnMoat} " +
                    $"qualifyingUnit={qualifyingUnitId} " +
                    $"effective={effectiveResult} decision={decision}";
                if (!string.Equals(
                        command.LastGroupMoatModeDiagnostic, diagnostic, StringComparison.Ordinal))
                {
                    command.LastGroupMoatModeDiagnostic = diagnostic;
                    LogCommandDiagnostic(diagnostic);
                }

                // A ground leader with a later moat member needs the shared moat branch.
                // A moat leader in a mixed group uses Vanilla's per-unit common-target
                // branch. Never synthesize E2610: DF720 consumes its real portal state.
                // The later per-unit mode and builder hooks retain the capability/owner filter.
                return effectiveResult;
            }
            catch (Exception ex)
            {
                LogFailure("group-moat-mode", ex);
                return vanillaResult;
            }
        }

        internal readonly struct EarlyGroupRegionDecision
        {
            public EarlyGroupRegionDecision(
                bool allowed, int unitId, string reason, RouteProbeSummary summary)
            {
                Allowed = allowed;
                UnitId = unitId;
                Reason = reason;
                Summary = summary;
            }

            public bool Allowed { get; }
            public int UnitId { get; }
            public string Reason { get; }
            public RouteProbeSummary Summary { get; }
        }

    }
}
