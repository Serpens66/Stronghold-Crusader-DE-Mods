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
        internal void StartOrRefreshMoatMoveTracker(
            PlanScope plan, RouteProbeSummary summary, int builderResult)
        {
            try
            {
                if (!APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit))
                {
                    return;
                }

                if (trackedMoatMoves.TryGetValue(
                        plan.UnitId, out MoatMoveTracker existing) &&
                    existing.MapEpoch == mapEpoch &&
                    existing.TargetX == plan.TargetX &&
                    existing.TargetY == plan.TargetY)
                {
                    existing.TribeId = unit->r_TribeId;
                    if (plan.MoatWorkTargetTileId > 0)
                        existing.WorkTargetMoatTileId = plan.MoatWorkTargetTileId;
                    return;
                }

                bool requiredOnly = activeMoveCommand?.Options.RequiredOnly ??
                    activeAttackCommand?.Options.RequiredOnly ?? CurrentOptions.RequiredOnly;
                bool explicitCommand = activeMoveCommand != null || activeAttackCommand != null;
                requiredBackgroundTrackedUnitIds.Remove(plan.UnitId);
                trackedMoatMoves.Remove(plan.UnitId);
                RequiredRouteMetrics requiredMetrics = requiredOnly
                    ? activeMoveCommand?.Required ?? activeAttackCommand?.Required
                    : null;
                if (requiredOnly)
                {
                    bool selected = explicitCommand
                        ? requiredMetrics?.TryTrackUnit(plan.UnitId) ?? false
                        : requiredBackgroundTrackedUnitIds.Contains(plan.UnitId) ||
                          requiredBackgroundTrackedUnitIds.Count < 8;
                    if (!selected)
                        return;
                    if (!explicitCommand) requiredBackgroundTrackedUnitIds.Add(plan.UnitId);
                }
                ResolveCommandDiagnosticContext(
                    plan.UnitId, unit, out TribeAICommand command, out string commandContext,
                    out int commandSequence);
                var tracker = new MoatMoveTracker(
                    mapEpoch,
                    plan.UnitId,
                    unit->r_TribeId,
                    unit->r_UnitChimp,
                    unit->r_ControllableForPlayerId,
                    plan.TargetX,
                    plan.TargetY,
                    builderResult,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY,
                    unit->r_CurrentPathPlanIndex,
                    IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)),
                    ReadUnitMoatPathConsumptionMode(unit),
                    CaptureCurrentGameTick());
                tracker.WeightedCommand = command;
                tracker.WeightedCommandContext = commandContext;
                tracker.WeightedCommandSequence = commandSequence;
                tracker.RequiredOnlyAtPublication = requiredOnly;
                tracker.WorkTargetMoatTileId = plan.MoatWorkTargetTileId;
                trackedMoatMoves[plan.UnitId] = tracker;
                LogCommandDiagnostic(
                    $"stage=move-track-start unit={plan.UnitId} type={unit->r_UnitChimp} " +
                    $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId} " +
                    $"target=({plan.TargetX},{plan.TargetY}) command={command} " +
                    $"commandContext={commandContext} canDig={CanDigMoat(unit)} " +
                    $"builderResult={builderResult} " +
                    summary.ToLogFields());
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("move-track-start", ex);
            }
        }

        internal void StartOrRefreshWeightedShadowTracker(
            BuilderWeightedScope shadow,
            int builderResult,
            WeightedMoatRouteSummary nativeSummary,
            bool nativeValid,
            string decision)
        {
            if (!APIShared.UnitAccess.TryGetById(shadow.UnitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit))
            {
                return;
            }

            trackedMoatMoves.TryGetValue(shadow.UnitId, out MoatMoveTracker tracker);
            bool sameTrackedRoute = tracker != null &&
                tracker.MapEpoch == mapEpoch && tracker.TribeId == shadow.TribeId &&
                tracker.TargetX == shadow.TargetX && tracker.TargetY == shadow.TargetY &&
                tracker.InitialX == shadow.StartX && tracker.InitialY == shadow.StartY;
            bool weightedPathPublished = string.Equals(
                decision, "weighted-path-published", StringComparison.Ordinal);
            // Runtime tracking is retained only for paths changed by this mod. A native moat
            // path that needed no fallback is Vanilla behavior and must not create per-tick work.
            if (!weightedPathPublished && !sameTrackedRoute)
                return;
            int workTargetMoatTileId = sameTrackedRoute
                ? tracker.WorkTargetMoatTileId
                : 0;
            if (!sameTrackedRoute)
            {
                requiredBackgroundTrackedUnitIds.Remove(shadow.UnitId);
                tracker = new MoatMoveTracker(
                    mapEpoch, shadow.UnitId, shadow.TribeId, shadow.UnitType, shadow.PlayerId,
                    shadow.TargetX, shadow.TargetY,
                    builderResult, shadow.StartX, shadow.StartY,
                    unit->r_CurrentPathPlanIndex,
                    IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)),
                    ReadUnitMoatPathConsumptionMode(unit),
                    CaptureCurrentGameTick());
                // The builder can refresh the movement tracker after 0x6AF60 selected a
                // work moat. Preserve that identity so owner observations remain classifiable.
                tracker.WorkTargetMoatTileId = workTargetMoatTileId;
                trackedMoatMoves[shadow.UnitId] = tracker;
            }

            tracker.HasWeightedShadow = true;
            tracker.WeightedPlayerId = shadow.PlayerId;
            tracker.WeightedUnitType = shadow.UnitType;
            tracker.WeightedCommand = shadow.Command;
            tracker.WeightedCommandContext = shadow.CommandContext;
            tracker.WeightedCommandSequence = shadow.CommandSequence;
            tracker.AllowReservedTarget = shadow.AllowReservedTarget;
            tracker.PlanningCostProfile = shadow.CostProfile;
            tracker.NativeRouteSummary = nativeSummary;
            tracker.NativeRouteValid = nativeValid;
            tracker.NativeEstimatedTicks = nativeSummary.EstimatedTicks;
            tracker.ShadowEstimatedTicks = shadow.Candidate.EstimatedTicks;
            tracker.ShadowDecision = decision;
            tracker.BuilderResult = builderResult;
            tracker.WeightedPathPublished = weightedPathPublished;
            tracker.PublishedLengthChecked = false;
            tracker.PublishedLengthVerified = false;
            tracker.ObservedPublishedPathSize = -1;
            tracker.PublishedRouteSummary = tracker.WeightedPathPublished
                ? shadow.Candidate
                : nativeSummary;
            tracker.RuntimeCadenceCaptured = false;
            tracker.RuntimeCadenceChanged = false;
            tracker.RuntimeCadenceRebased = false;
            tracker.RuntimeCostProfile = default;
            tracker.RuntimeShadowMatchesPublishedCostProfile = false;
            tracker.RuntimeNativeEstimatedTicks = long.MaxValue;
            tracker.RuntimeShadowEstimatedTicks = long.MaxValue;
            tracker.RuntimeShadowDecision = null;
            tracker.LastRuntimeCadenceRejection = null;
            tracker.Calibratable = !tracker.CombatInterrupted &&
                shadow.Calibratable && nativeValid && shadow.CandidateFound;
            tracker.ShadowMatchesPublishedCostProfile = shadow.CandidateFound &&
                (tracker.WeightedPathPublished ||
                 nativeValid && nativeSummary.RouteLength == shadow.Candidate.RouteLength &&
                 nativeSummary.GroundEdges == shadow.Candidate.GroundEdges &&
                 nativeSummary.MoatEdges == shadow.Candidate.MoatEdges);
            tracker.CalibrationReason = tracker.Calibratable
                ? "isolated-unretargeted"
                : tracker.CombatInterrupted
                    ? "combat-interrupted"
                    : "group-or-route-unvalidated";
        }

        internal void ObserveTrackedMoatMoveStates(int tick)
        {
            if (trackedMoatMoves.Count == 0)
                return;

            try
            {
                var consumerContracts = new Dictionary<int, int[]>();
                List<int> unitIds = new List<int>(trackedMoatMoves.Keys);
                foreach (int unitId in unitIds)
                {
                    if (!trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker))
                        continue;
                    if (tracker.MapEpoch != mapEpoch)
                    {
                        EndTrackedMoatMove(unitId, tracker, "map-changed");
                        continue;
                    }
                    if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                        unit == null || !APIShared.UnitAccess.IsReallyAlive(unit))
                    {
                        EndTrackedMoatMove(unitId, tracker, "unit-dead-or-invalid");
                        continue;
                    }
                    if (tracker.CombatInterrupted &&
                        (unit->r_TargetTilePositionX2 != tracker.TargetX ||
                         unit->r_TargetTilePositionY2 != tracker.TargetY))
                    {
                        EndTrackedMoatMove(unitId, tracker, "saved-target-changed");
                        continue;
                    }
                    if (unit->r_TribeId != tracker.TribeId)
                    {
                        bool savedTargetStillMatches =
                            unit->r_TargetTilePositionX2 == tracker.TargetX &&
                            unit->r_TargetTilePositionY2 == tracker.TargetY;
                        if (!savedTargetStillMatches)
                        {
                            EndTrackedMoatMove(
                                unitId, tracker, "tribe-changed-and-saved-target-changed");
                            continue;
                        }

                        tracker.TribeId = unit->r_TribeId;
                        tracker.Calibratable = false;
                        tracker.CalibrationReason = "combat-interrupted";
                        if (!tracker.CombatInterrupted)
                        {
                            tracker.CombatInterrupted = true;
                            LogMoveMilestone(
                                tick, unitId, unit, tracker,
                                (TribeAICommand)unit->r_AI_LastIssuedTribeCommand,
                                "combat-interrupted");
                        }
                    }

                    bool reachedRequestedTarget =
                        unit->r_CurrentTilePositionX == tracker.TargetX &&
                        unit->r_CurrentTilePositionY == tracker.TargetY;
                    bool pathConsumed = unit->r_PathPlanLength <= 0 ||
                        unit->r_CurrentPathPlanIndex >= unit->r_PathPlanLength;
                    bool settledOnCurrentTile =
                        unit->r_TargetTilePositionX == unit->r_CurrentTilePositionX &&
                        unit->r_TargetTilePositionY == unit->r_CurrentTilePositionY &&
                        unit->r_NextTilePositionX2 == unit->r_CurrentTilePositionX &&
                        unit->r_NextTilePositionY2 == unit->r_CurrentTilePositionY;
                    bool currentMoat = IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId));
                    int currentTileId = unchecked((int)unit->r_CurrentPositionTileId);
                    ushort currentConsumerMode = ReadUnitMoatPathConsumptionMode(unit);
                    tracker.LastConsumerMode = currentConsumerMode;
                    if (currentConsumerMode < tracker.MinimumConsumerMode)
                        tracker.MinimumConsumerMode = currentConsumerMode;
                    if (currentConsumerMode > tracker.MaximumConsumerMode)
                        tracker.MaximumConsumerMode = currentConsumerMode;
                    tracker.ConsumerModeObservedNonZero |= currentConsumerMode != 0;
                    if (currentMoat)
                        ObserveActualMoatOwnership(tracker, currentTileId, unit, tick);
                    TribeAICommand command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
                    bool tileChanged = unit->r_CurrentTilePositionX != tracker.LastX ||
                        unit->r_CurrentTilePositionY != tracker.LastY;
                    bool progressed = tileChanged ||
                        unit->r_CurrentPathPlanIndex != tracker.LastPathPosition;
                    int transitionTicks = -1;
                    bool firstObservation = tracker.FirstObservedTick < 0;
                    if (firstObservation)
                    {
                        tracker.FirstObservedTick = tick;
                        if (tracker.LastTileTransitionTick < 0)
                            tracker.LastTileTransitionTick = tick;
                    }
                    if (tracker.WeightedPathPublished && !tracker.PublishedLengthChecked)
                    {
                        tracker.PublishedLengthChecked = true;
                        tracker.ObservedPublishedPathSize = unchecked((int)unit->r_PathPlanLength);
                        tracker.PublishedLengthVerified =
                            tracker.ObservedPublishedPathSize == tracker.BuilderResult;
                        if (!consumerContracts.TryGetValue(
                                tracker.WeightedCommandSequence, out int[] contractCounts))
                        {
                            contractCounts = new int[3];
                            consumerContracts.Add(tracker.WeightedCommandSequence, contractCounts);
                        }
                        contractCounts[0]++;
                        contractCounts[tracker.PublishedLengthVerified ? 1 : 2]++;
                        if (!tracker.PublishedLengthVerified)
                        {
                            if (contractCounts[2] <= 3)
                            {
                                APIShared.Internal.DebugLogHelper.LogWarning(
                                    log,
                                    $"Bugfixes and QoL stage=friendly-moat-movement-weighted-path-consumer-contract-invalid " +
                                    $"unit={unitId} commandSeq={tracker.WeightedCommandSequence} " +
                                    $"expectedLength={tracker.BuilderResult} " +
                                    $"observedLength={tracker.ObservedPublishedPathSize} " +
                                    $"pathPosition={unit->r_CurrentPathPlanIndex}.");
                            }
                            tracker.Calibratable = false;
                            tracker.CalibrationReason = "published-length-not-consumed";
                        }
                    }
                    tracker.LastObservedTick = tick;
                    if (tileChanged)
                    {
                        int dx = Math.Abs(unit->r_CurrentTilePositionX - tracker.LastX);
                        int dy = Math.Abs(unit->r_CurrentTilePositionY - tracker.LastY);
                        if (dx > 1 || dy > 1)
                        {
                            tracker.Calibratable = false;
                            tracker.CalibrationReason = "non-adjacent-tile-change";
                        }
                        if (tracker.TileTransitionCount == 0)
                        {
                            tracker.FirstTileTransitionTick = tick;
                            tracker.FirstTransitionTimingUnavailable =
                                firstObservation && tracker.TrackingStartTick < 0;
                        }
                        tracker.TileTransitionCount++;
                        if ((!firstObservation || tracker.TrackingStartTick >= 0) &&
                            tracker.LastTileTransitionTick >= 0)
                        {
                            transitionTicks = tick - tracker.LastTileTransitionTick;
                            if (transitionTicks >= 0)
                            {
                                tracker.TimedTileTransitionCount++;
                                tracker.TileTransitionTicks += transitionTicks;
                                tracker.MaximumTileTransitionTicks = Math.Max(
                                    tracker.MaximumTileTransitionTicks, transitionTicks);
                            }
                        }

                        if (dx <= 1 && dy <= 1 && dx + dy > 0)
                        {
                            bool moatTransition = tracker.WasOnMoat || currentMoat;
                            if (moatTransition)
                            {
                                tracker.ActualMoatTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedMoatTransitions++;
                                    tracker.ActualMoatTransitionTicks += transitionTicks;
                                    tracker.MinimumMoatTransitionTicks = Math.Min(
                                        tracker.MinimumMoatTransitionTicks, transitionTicks);
                                    tracker.MaximumMoatTransitionTicks = Math.Max(
                                        tracker.MaximumMoatTransitionTicks, transitionTicks);
                                }
                            }
                            else
                            {
                                tracker.ActualGroundTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedGroundTransitions++;
                                    tracker.ActualGroundTransitionTicks += transitionTicks;
                                    tracker.MinimumGroundTransitionTicks = Math.Min(
                                        tracker.MinimumGroundTransitionTicks, transitionTicks);
                                    tracker.MaximumGroundTransitionTicks = Math.Max(
                                        tracker.MaximumGroundTransitionTicks, transitionTicks);
                                }
                            }
                            if (dx == 1 && dy == 1)
                            {
                                tracker.ActualDiagonalTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedDiagonalTransitions++;
                                    tracker.ActualDiagonalTransitionTicks += transitionTicks;
                                }
                            }
                            else
                            {
                                tracker.ActualCardinalTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedCardinalTransitions++;
                                    tracker.ActualCardinalTransitionTicks += transitionTicks;
                                }
                            }

                            int direction = EncodeDirectionDelta(
                                unit->r_CurrentTilePositionX - tracker.LastX,
                                unit->r_CurrentTilePositionY - tracker.LastY);
                            if (direction >= 0)
                            {
                                if (tracker.LastActualDirection >= 0 &&
                                    tracker.LastActualDirection != direction)
                                {
                                    tracker.ActualDirectionChanges++;
                                }
                                tracker.LastActualDirection = direction;
                                tracker.ActualRouteFingerprint = unchecked(
                                    (tracker.ActualRouteFingerprint ^ (byte)direction) *
                                    RouteFingerprintPrime);
                            }
                        }
                        tracker.LastTileTransitionTick = tick;
                    }
                    if (tracker.LastProgressTick < 0 || progressed)
                        tracker.LastProgressTick = tick;

                    if (progressed && tracker.PostCombatRepathEntered &&
                        !tracker.MovementResumedAfterCombat)
                    {
                        tracker.MovementResumedAfterCombat = true;
                        LogMoveMilestone(
                            tick, unitId, unit, tracker, command, "movement-resumed");
                    }

                    if (!tracker.MovementStarted &&
                        (unit->r_CurrentTilePositionX != tracker.InitialX ||
                         unit->r_CurrentTilePositionY != tracker.InitialY))
                    {
                        tracker.MovementStarted = true;
                    }

                    if (currentMoat && !tracker.MoatEntered)
                    {
                        tracker.MoatEntered = true;
                    }
                    if (tracker.MoatEntered && !tracker.MoatExited &&
                        tracker.WasOnMoat && !currentMoat)
                    {
                        tracker.MoatExited = true;
                    }

                    if (reachedRequestedTarget && pathConsumed && settledOnCurrentTile)
                    {
                        EndTrackedMoatMove(unitId, tracker, "path-completed-at-target");
                        continue;
                    }

                    tracker.LastX = unit->r_CurrentTilePositionX;
                    tracker.LastY = unit->r_CurrentTilePositionY;
                    tracker.LastPathPosition = unit->r_CurrentPathPlanIndex;
                    tracker.WasOnMoat = currentMoat;
                }
                foreach (KeyValuePair<int, int[]> entry in consumerContracts)
                {
                    LogDetailedInfo(
                        $"Bugfixes and QoL stage=friendly-moat-movement-weighted-path-consumer-contract-summary " +
                        $"tick={tick} commandSeq={entry.Key} checked={entry.Value[0]} " +
                        $"valid={entry.Value[1]} invalid={entry.Value[2]}.");
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("move-state-tick", ex);
            }
        }

        internal static int CaptureCurrentGameTick()
        {
            try
            {
                return GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
            }
            catch
            {
                return -1;
            }
        }

        internal bool IsCompletedMoatTile(int tileId) =>
            IsValidTileId(tileId) && (tileFlags[tileId] & CompletedMoatTileFlag) != 0;

        internal void ObserveActualMoatOwnership(
            MoatMoveTracker tracker, int tileId, GameUnit* unit, int tick)
        {
            if (!IsCompletedMoatTile(tileId) || !tracker.ActualMoatTileIds.Add(tileId))
                return;

            if (!TryReadCompletedMoatOwner(tileId, out int moatId, out int ownerId))
            {
                tracker.ActualInvalidMoatOwnerTiles++;
                tracker.ActualMoatOwnerAtFirstObservation[tileId] = -1;
                return;
            }

            tracker.ActualMoatOwnerAtFirstObservation[tileId] = ownerId;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(tracker.PlayerId))
            {
                tracker.ActualInvalidMoatOwnerTiles++;
                return;
            }

            if ((uint)ownerId < 32)
                tracker.ActualMoatOwnerMask |= 1u << ownerId;
            if (ownerId == tracker.PlayerId)
                tracker.ActualOwnMoatTiles++;
            else if (playerApi.IsPlayerAlliedTo(tracker.PlayerId, ownerId))
                tracker.ActualAlliedMoatTiles++;
            else
            {
                tracker.ActualEnemyMoatTiles++;
                bool matchesWorkTarget = tileId == tracker.WorkTargetMoatTileId;
                if (matchesWorkTarget)
                    tracker.ActualEnemyMoatTilesMatchingWorkTarget++;
                else
                    tracker.ActualEnemyMoatTilesOutsideWorkTarget++;
                var position = GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
                LogDetailedInfo(
                    $"Bugfixes and QoL stage=friendly-moat-movement-owner-safety-observation tick={tick} " +
                    $"unit={tracker.UnitId} player={tracker.PlayerId} " +
                    $"command={tracker.WeightedCommand} " +
                    $"commandContext={tracker.WeightedCommandContext ?? "unresolved"} " +
                    $"tile={tileId}/({position.X},{position.Y}) moat={moatId} owner={ownerId} " +
                    $"workTargetTile={tracker.WorkTargetMoatTileId} " +
                    $"matchesWorkTarget={matchesWorkTarget} " +
                    $"requestedTarget=({tracker.TargetX},{tracker.TargetY}) " +
                    $"path={unit->r_CurrentPathPlanIndex}/{unit->r_PathPlanLength}.");
            }
        }

        internal bool TryReadCompletedMoatOwner(int tileId, out int moatId, out int ownerId)
        {
            moatId = 0;
            ownerId = -1;
            if (!IsCompletedMoatTile(tileId) || getMoatIdAtTile == null)
                return false;

            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero)
                return false;
            moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (!IsValidMoatRecordId(moatId, moatCount))
                return false;

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            ownerId = moatRecord[MoatOwnerOffset];
            return GamePlayerManagerAPI.Instance.IsPlayerIdValid(ownerId);
        }

        internal void LogMoveMilestone(
            int tick,
            int unitId,
            GameUnit* unit,
            MoatMoveTracker tracker,
            TribeAICommand command,
            string milestone)
        {
            string cadenceSnapshot = tracker.RequiredOnlyAtPublication
                ? "skipped-required-only"
                : TryCaptureWeightedMovementCostProfile(
                    unit, out WeightedMovementCostProfile profile, out string rejectionReason)
                        ? FormatCostProfile(profile)
                        : $"unavailable/{rejectionReason ?? "unknown"}";
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-move-milestone event={milestone} tick={tick} " +
                $"unit={unitId} type={unit->r_UnitChimp} " +
                $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId} " +
                $"runtimeCommand={command}({(uint)command}) " +
                $"commandSeq={tracker.WeightedCommandSequence} " +
                $"plannedCommand={tracker.WeightedCommand}({(uint)tracker.WeightedCommand}) " +
                $"commandContext={tracker.WeightedCommandContext ?? "unresolved"} " +
                $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                $"requestedTarget=({tracker.TargetX},{tracker.TargetY}) " +
                $"path={unit->r_CurrentPathPlanIndex}/{unit->r_PathPlanLength} " +
                $"moatConsumerMode={ReadUnitMoatPathConsumptionMode(unit)} " +
                $"cadence={cadenceSnapshot} " +
                $"builderResult={tracker.BuilderResult}.");
        }

        internal void MarkPostCombatRepathEntered(
            int unitId,
            GameUnit* unit,
            PlanScope plan,
            bool requiredFriendlyMoat)
        {
            if (!trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker) ||
                tracker.MapEpoch != mapEpoch ||
                tracker.TargetX != plan.TargetX || tracker.TargetY != plan.TargetY)
            {
                return;
            }

            tracker.TribeId = unit->r_TribeId;
            if (tracker.PostCombatRepathEntered)
                return;

            tracker.PostCombatRepathEntered = true;
            tracker.PostCombatRequiredFriendlyMoat = requiredFriendlyMoat;
            LogMoveMilestone(
                CaptureCurrentGameTick(), unitId, unit, tracker,
                (TribeAICommand)unit->r_AI_LastIssuedTribeCommand,
                requiredFriendlyMoat
                    ? "post-combat-repath-entered-required-moat"
                    : "post-combat-repath-entered-ground-or-faster-moat");
        }

        internal static ushort ReadUnitMoatPathConsumptionMode(GameUnit* unit)
        {
            if (unit == null)
                return 0;

            // Native indexes from the unit-manager base and stores this ushort at +0x9C8.
            // The Script Extender pointer begins at manager + unitId*0x490 + 0x65C.
            return *(ushort*)((byte*)unit + UnitMoatPathConsumptionModeOffset);
        }

        internal void RemoveTrackedMoatMovesForTribe(int tribeId, string reason)
        {
            if (trackedMoatMoves.Count == 0)
                return;

            List<int> unitIds = new List<int>(trackedMoatMoves.Keys);
            foreach (int unitId in unitIds)
            {
                if (trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker) &&
                    tracker.TribeId == tribeId)
                {
                    EndTrackedMoatMove(unitId, tracker, reason);
                }
            }
        }

        internal void EndTrackedMoatMove(int unitId, MoatMoveTracker tracker, string reason)
        {
            trackedMoatMoves.Remove(unitId);
            requiredBackgroundTrackedUnitIds.Remove(unitId);
            bool completedAtTarget = string.Equals(
                reason, "path-completed-at-target", StringComparison.Ordinal);
            if (!completedAtTarget && tracker.Calibratable)
            {
                tracker.Calibratable = false;
                tracker.CalibrationReason = $"incomplete-{reason}";
            }
            int measurementStartTick = tracker.TrackingStartTick >= 0
                ? tracker.TrackingStartTick
                : tracker.FirstObservedTick;
            int actualTicks = measurementStartTick >= 0 &&
                tracker.LastObservedTick >= measurementStartTick
                    ? tracker.LastObservedTick - measurementStartTick
                    : -1;
            int firstTransitionWaitTicks = measurementStartTick >= 0 &&
                !tracker.FirstTransitionTimingUnavailable &&
                tracker.FirstTileTransitionTick >= measurementStartTick
                    ? tracker.FirstTileTransitionTick - measurementStartTick
                    : -1;
            int finalSettleTicks = tracker.LastTileTransitionTick >= 0 &&
                tracker.LastObservedTick >= tracker.LastTileTransitionTick
                    ? tracker.LastObservedTick - tracker.LastTileTransitionTick
                    : -1;
            bool actualMatchesNativeFingerprint = tracker.NativeRouteValid &&
                tracker.TileTransitionCount == tracker.NativeRouteSummary.RouteLength &&
                tracker.ActualRouteFingerprint == tracker.NativeRouteSummary.RouteFingerprint;
            bool actualMatchesPublishedFingerprint =
                tracker.PublishedRouteSummary.Found &&
                tracker.TileTransitionCount == tracker.PublishedRouteSummary.RouteLength &&
                tracker.ActualRouteFingerprint == tracker.PublishedRouteSummary.RouteFingerprint;
            if (tracker.WeightedPathPublished && !actualMatchesPublishedFingerprint)
            {
                tracker.Calibratable = false;
                tracker.CalibrationReason =
                    tracker.PublishedRouteSummary.MoatEdges > 0 &&
                    tracker.ActualMoatTransitions == 0
                        ? "published-path-rejected-before-moat"
                        : "published-path-not-consumed";
            }
            bool ownerSafeActualRoute = tracker.ActualEnemyMoatTilesOutsideWorkTarget == 0 &&
                tracker.ActualInvalidMoatOwnerTiles == 0;
            int ownerChangedTiles = 0;
            int noLongerCompletedMoatTiles = 0;
            foreach (KeyValuePair<int, int> observed in tracker.ActualMoatOwnerAtFirstObservation)
            {
                if (!IsCompletedMoatTile(observed.Key))
                {
                    noLongerCompletedMoatTiles++;
                    continue;
                }
                if (!TryReadCompletedMoatOwner(observed.Key, out _, out int finalOwner) ||
                    finalOwner != observed.Value)
                {
                    ownerChangedTiles++;
                }
            }
            bool weightedPublicationVerified = tracker.WeightedPathPublished &&
                completedAtTarget && actualMatchesPublishedFingerprint &&
                tracker.PublishedLengthVerified &&
                tracker.ConsumerModeObservedNonZero && ownerSafeActualRoute;
            long nativeCalibrationDelta = tracker.Calibratable &&
                !tracker.WeightedPathPublished && actualTicks >= 0 &&
                tracker.NativeEstimatedTicks > 0
                    ? actualTicks - tracker.NativeEstimatedTicks
                    : long.MinValue;
            long shadowCalibrationDelta = tracker.Calibratable &&
                tracker.ShadowMatchesPublishedCostProfile && actualTicks >= 0 &&
                tracker.ShadowEstimatedTicks > 0
                    ? actualTicks - tracker.ShadowEstimatedTicks
                    : long.MinValue;
            long runtimeNativeCalibrationDelta = tracker.Calibratable &&
                tracker.RuntimeCadenceCaptured && !tracker.RuntimeCadenceChanged &&
                actualTicks >= 0 && tracker.RuntimeNativeEstimatedTicks > 0 &&
                tracker.RuntimeNativeEstimatedTicks != long.MaxValue
                    ? actualTicks - tracker.RuntimeNativeEstimatedTicks
                    : long.MinValue;
            long runtimeShadowCalibrationDelta = tracker.Calibratable &&
                tracker.RuntimeCadenceCaptured && !tracker.RuntimeCadenceChanged &&
                tracker.RuntimeShadowMatchesPublishedCostProfile && actualTicks >= 0 &&
                tracker.RuntimeShadowEstimatedTicks > 0 &&
                tracker.RuntimeShadowEstimatedTicks != long.MaxValue
                    ? actualTicks - tracker.RuntimeShadowEstimatedTicks
                    : long.MinValue;
            string shadowCalibrationReason = !tracker.Calibratable
                ? tracker.CalibrationReason ?? "context-unvalidated"
                : tracker.ShadowMatchesPublishedCostProfile
                    ? "matching-published-cost-profile"
                    : "published-cost-profile-differs";
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-move-state-end unit={unitId} type={tracker.UnitType} " +
                $"player={tracker.PlayerId} tribe={tracker.TribeId} " +
                $"commandSeq={tracker.WeightedCommandSequence} " +
                $"plannedCommand={tracker.WeightedCommand}({(uint)tracker.WeightedCommand}) " +
                $"commandContext={tracker.WeightedCommandContext ?? "unresolved"} " +
                $"target=({tracker.TargetX},{tracker.TargetY}) " +
                $"reason={reason} weightedShadow={tracker.HasWeightedShadow} " +
                $"combatInterrupted={tracker.CombatInterrupted} " +
                $"postCombatRepath={tracker.PostCombatRepathEntered} " +
                $"postCombatRequiredFriendlyMoat={tracker.PostCombatRequiredFriendlyMoat} " +
                $"movementResumedAfterCombat={tracker.MovementResumedAfterCombat} " +
                $"decision={tracker.ShadowDecision ?? "none"} " +
                $"actualTicks={actualTicks} " +
                $"nativeEstimatedTicks={tracker.NativeEstimatedTicks} " +
                $"shadowEstimatedTicks={tracker.ShadowEstimatedTicks} " +
                $"nativeCalibrationDeltaTicks={(nativeCalibrationDelta == long.MinValue ? "n/a" : nativeCalibrationDelta.ToString())} " +
                $"shadowCalibrationDeltaTicks={(shadowCalibrationDelta == long.MinValue ? "n/a" : shadowCalibrationDelta.ToString())} " +
                $"runtimeCadenceCaptured={tracker.RuntimeCadenceCaptured} " +
                $"runtimeCadenceChanged={tracker.RuntimeCadenceChanged} " +
                $"runtimeCadenceRebased={tracker.RuntimeCadenceRebased} " +
                $"runtimeDecision={tracker.RuntimeShadowDecision ?? "none"} " +
                $"runtimeNativeEstimatedTicks={(tracker.RuntimeNativeEstimatedTicks == long.MaxValue ? "n/a" : tracker.RuntimeNativeEstimatedTicks.ToString())} " +
                $"runtimeShadowEstimatedTicks={(tracker.RuntimeShadowEstimatedTicks == long.MaxValue ? "n/a" : tracker.RuntimeShadowEstimatedTicks.ToString())} " +
                $"runtimeNativeCalibrationDeltaTicks={(runtimeNativeCalibrationDelta == long.MinValue ? "n/a" : runtimeNativeCalibrationDelta.ToString())} " +
                $"runtimeShadowCalibrationDeltaTicks={(runtimeShadowCalibrationDelta == long.MinValue ? "n/a" : runtimeShadowCalibrationDelta.ToString())} " +
                $"tileTransitions={tracker.TileTransitionCount} " +
                $"actualGround={tracker.ActualGroundTransitions}/timed={tracker.TimedGroundTransitions}/ticks={tracker.ActualGroundTransitionTicks}/" +
                $"min={(tracker.TimedGroundTransitions > 0 ? tracker.MinimumGroundTransitionTicks : -1)}/" +
                $"max={(tracker.TimedGroundTransitions > 0 ? tracker.MaximumGroundTransitionTicks : -1)} " +
                $"actualMoat={tracker.ActualMoatTransitions}/timed={tracker.TimedMoatTransitions}/ticks={tracker.ActualMoatTransitionTicks}/" +
                $"min={(tracker.TimedMoatTransitions > 0 ? tracker.MinimumMoatTransitionTicks : -1)}/" +
                $"max={(tracker.TimedMoatTransitions > 0 ? tracker.MaximumMoatTransitionTicks : -1)} " +
                $"actualMoatTiles={tracker.ActualMoatTileIds.Count} " +
                $"actualMoatOwners=own:{tracker.ActualOwnMoatTiles}/allied:{tracker.ActualAlliedMoatTiles}/" +
                $"enemy:{tracker.ActualEnemyMoatTiles}/invalid:{tracker.ActualInvalidMoatOwnerTiles}/" +
                $"mask:0x{tracker.ActualMoatOwnerMask:X} " +
                $"enemyMoatClassification=workTarget:{tracker.ActualEnemyMoatTilesMatchingWorkTarget}/" +
                $"traversed:{tracker.ActualEnemyMoatTilesOutsideWorkTarget} " +
                $"workTargetMoatTile={tracker.WorkTargetMoatTileId} " +
                $"ownerRecheck=changed:{ownerChangedTiles}/noLongerCompleted:{noLongerCompletedMoatTiles} " +
                $"ownerSafetyViolation={tracker.ActualEnemyMoatTilesOutsideWorkTarget > 0 || tracker.ActualInvalidMoatOwnerTiles > 0} " +
                $"consumerMode=last:{tracker.LastConsumerMode}/min:{tracker.MinimumConsumerMode}/" +
                $"max:{tracker.MaximumConsumerMode}/nonZero:{tracker.ConsumerModeObservedNonZero} " +
                $"publishedLength=expected:{tracker.BuilderResult}/" +
                $"observed:{tracker.ObservedPublishedPathSize}/" +
                $"checked:{tracker.PublishedLengthChecked}/valid:{tracker.PublishedLengthVerified} " +
                $"weightedPublicationVerified={weightedPublicationVerified} " +
                $"actualCardinal={tracker.ActualCardinalTransitions}/timed={tracker.TimedCardinalTransitions}/ticks={tracker.ActualCardinalTransitionTicks} " +
                $"actualDiagonal={tracker.ActualDiagonalTransitions}/timed={tracker.TimedDiagonalTransitions}/ticks={tracker.ActualDiagonalTransitionTicks} " +
                $"actualDirectionChanges={tracker.ActualDirectionChanges} " +
                $"actualFingerprint=0x{tracker.ActualRouteFingerprint:X16} " +
                $"nativeFingerprint=0x{tracker.NativeRouteSummary.RouteFingerprint:X16} " +
                $"actualMatchesNativeFingerprint={actualMatchesNativeFingerprint} " +
                $"publishedFingerprint=0x{tracker.PublishedRouteSummary.RouteFingerprint:X16} " +
                $"actualMatchesPublishedFingerprint={actualMatchesPublishedFingerprint} " +
                $"firstTransitionWaitTicks={firstTransitionWaitTicks} " +
                $"firstTransitionTimingUnavailable={tracker.FirstTransitionTimingUnavailable} " +
                $"finalSettleTicks={finalSettleTicks} " +
                $"timedTransitions={tracker.TimedTileTransitionCount} " +
                $"averageTransitionTicks={(tracker.TimedTileTransitionCount > 0 ? tracker.TileTransitionTicks / tracker.TimedTileTransitionCount : -1)} " +
                $"maximumTransitionTicks={tracker.MaximumTileTransitionTicks} " +
                $"calibratable={tracker.Calibratable} " +
                $"shadowMatchesPublishedCostProfile={tracker.ShadowMatchesPublishedCostProfile} " +
                $"shadowCalibrationReason={shadowCalibrationReason} " +
                $"calibrationReason={tracker.CalibrationReason ?? "not-instrumented"}.");
        }

        internal sealed class MoatMoveTracker
        {
            public MoatMoveTracker(
                int mapEpoch,
                int unitId,
                int tribeId,
                eChimps unitType,
                int playerId,
                int targetX,
                int targetY,
                int builderResult,
                int initialX,
                int initialY,
                int initialPathPosition,
                bool startedOnMoat,
                ushort initialConsumerMode,
                int trackingStartTick)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                TribeId = tribeId;
                UnitType = unitType;
                PlayerId = playerId;
                TargetX = targetX;
                TargetY = targetY;
                BuilderResult = builderResult;
                InitialX = initialX;
                InitialY = initialY;
                LastX = initialX;
                LastY = initialY;
                LastPathPosition = initialPathPosition;
                StartedOnMoat = startedOnMoat;
                LastConsumerMode = initialConsumerMode;
                MinimumConsumerMode = initialConsumerMode;
                MaximumConsumerMode = initialConsumerMode;
                ConsumerModeObservedNonZero = initialConsumerMode != 0;
                TrackingStartTick = trackingStartTick;
                WasOnMoat = startedOnMoat;
                ActualRouteFingerprint = RouteFingerprintOffsetBasis;
                MinimumGroundTransitionTicks = int.MaxValue;
                MinimumMoatTransitionTicks = int.MaxValue;
                RuntimeNativeEstimatedTicks = long.MaxValue;
                RuntimeShadowEstimatedTicks = long.MaxValue;
                LastTileTransitionTick = trackingStartTick;
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public int TribeId { get; set; }
            public eChimps UnitType { get; }
            public int PlayerId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int BuilderResult { get; set; }
            public int InitialX { get; }
            public int InitialY { get; }
            public bool StartedOnMoat { get; }
            public int TrackingStartTick { get; }
            public int LastX { get; set; }
            public int LastY { get; set; }
            public int LastPathPosition { get; set; }
            public int LastProgressTick { get; set; } = -1;
            public bool MovementStarted { get; set; }
            public bool MoatEntered { get; set; }
            public bool MoatExited { get; set; }
            public bool WasOnMoat { get; set; }
            public bool StallReported { get; set; }
            public bool CombatInterrupted { get; set; }
            public bool PostCombatRepathEntered { get; set; }
            public bool PostCombatRequiredFriendlyMoat { get; set; }
            public bool MovementResumedAfterCombat { get; set; }
            public bool RequiredOnlyAtPublication { get; set; }
            public bool HasWeightedShadow { get; set; }
            public int WeightedPlayerId { get; set; }
            public eChimps WeightedUnitType { get; set; }
            public TribeAICommand WeightedCommand { get; set; }
            public string WeightedCommandContext { get; set; }
            public int WeightedCommandSequence { get; set; }
            public bool AllowReservedTarget { get; set; }
            public WeightedMovementCostProfile PlanningCostProfile { get; set; }
            public WeightedMoatRouteSummary NativeRouteSummary { get; set; }
            public bool NativeRouteValid { get; set; }
            public bool Calibratable { get; set; }
            public bool ShadowMatchesPublishedCostProfile { get; set; }
            public bool RuntimeCadenceCaptured { get; set; }
            public bool RuntimeCadenceChanged { get; set; }
            public bool RuntimeCadenceRebased { get; set; }
            public WeightedMovementCostProfile RuntimeCostProfile { get; set; }
            public bool RuntimeShadowMatchesPublishedCostProfile { get; set; }
            public long RuntimeNativeEstimatedTicks { get; set; }
            public long RuntimeShadowEstimatedTicks { get; set; }
            public string RuntimeShadowDecision { get; set; }
            public string LastRuntimeCadenceRejection { get; set; }
            public int FirstObservedTick { get; set; } = -1;
            public int LastObservedTick { get; set; } = -1;
            public int LastTileTransitionTick { get; set; } = -1;
            public int FirstTileTransitionTick { get; set; } = -1;
            public bool FirstTransitionTimingUnavailable { get; set; }
            public int TileTransitionCount { get; set; }
            public int TimedTileTransitionCount { get; set; }
            public long TileTransitionTicks { get; set; }
            public int MaximumTileTransitionTicks { get; set; }
            public int ActualGroundTransitions { get; set; }
            public int TimedGroundTransitions { get; set; }
            public long ActualGroundTransitionTicks { get; set; }
            public int MinimumGroundTransitionTicks { get; set; }
            public int MaximumGroundTransitionTicks { get; set; }
            public int ActualMoatTransitions { get; set; }
            public int TimedMoatTransitions { get; set; }
            public long ActualMoatTransitionTicks { get; set; }
            public int MinimumMoatTransitionTicks { get; set; }
            public int MaximumMoatTransitionTicks { get; set; }
            public int ActualCardinalTransitions { get; set; }
            public int TimedCardinalTransitions { get; set; }
            public long ActualCardinalTransitionTicks { get; set; }
            public int ActualDiagonalTransitions { get; set; }
            public int TimedDiagonalTransitions { get; set; }
            public long ActualDiagonalTransitionTicks { get; set; }
            public int ActualDirectionChanges { get; set; }
            public int LastActualDirection { get; set; } = -1;
            public ulong ActualRouteFingerprint { get; set; }
            public long NativeEstimatedTicks { get; set; }
            public long ShadowEstimatedTicks { get; set; }
            public string ShadowDecision { get; set; }
            public bool WeightedPathPublished { get; set; }
            public bool PublishedLengthChecked { get; set; }
            public bool PublishedLengthVerified { get; set; }
            public int ObservedPublishedPathSize { get; set; } = -1;
            public WeightedMoatRouteSummary PublishedRouteSummary { get; set; }
            public string CalibrationReason { get; set; }
            public ushort LastConsumerMode { get; set; }
            public ushort MinimumConsumerMode { get; set; }
            public ushort MaximumConsumerMode { get; set; }
            public bool ConsumerModeObservedNonZero { get; set; }
            public uint ActualMoatOwnerMask { get; set; }
            public int ActualOwnMoatTiles { get; set; }
            public int ActualAlliedMoatTiles { get; set; }
            public int ActualEnemyMoatTiles { get; set; }
            public int ActualInvalidMoatOwnerTiles { get; set; }
            public int ActualEnemyMoatTilesMatchingWorkTarget { get; set; }
            public int ActualEnemyMoatTilesOutsideWorkTarget { get; set; }
            public int WorkTargetMoatTileId { get; set; }
            public HashSet<int> ActualMoatTileIds { get; } = new HashSet<int>();
            public Dictionary<int, int> ActualMoatOwnerAtFirstObservation { get; } =
                new Dictionary<int, int>();
        }

    }
}
