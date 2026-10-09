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
        internal void LogCursorDecision(string message)
        {
            LogDetailedInfo($"Bugfixes and QoL friendly-moat-movement {message}.");
        }

        internal void LogDetailedInfo(string message)
        {
            if (DetailedDiagnosticsEnabled)
                APIShared.Internal.DebugLogHelper.LogDebug(log, message);
        }

        internal void LogPositiveCursorDecision(ref int lastLoggedGeneration, string message)
        {
            if (lastLoggedGeneration == gridGeneration)
                return;

            lastLoggedGeneration = gridGeneration;
            LogCursorDecision(message);
        }

        internal void LogCursorOwnerBlockDecision(ref int lastLoggedGeneration, string message)
        {
            if (lastLoggedGeneration == gridGeneration)
                return;

            lastLoggedGeneration = gridGeneration;
            LogCursorDecision(message);
        }

        // Commands and work selections have aggregate counters; avoid formatting a
        // full per-unit trace in those hot paths. Standalone diagnostics remain available.
        internal bool ShouldLogUnitPipeline => DetailedDiagnosticsEnabled &&
            activeMoveCommand == null && activeAttackCommand == null &&
            activeMoatWorkSelection == null &&
            !((activePlan ?? pendingPlan)?.MoatWorkMovement ?? false);

        internal void LogMovementContext(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        internal void LogBuilderDecision(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        internal void LogPipelineDiagnostic(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        internal void LogCommandDiagnostic(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        internal void BufferOrLogCommandDiagnostic(string message)
        {
            if (!DetailedDiagnosticsEnabled)
                return;

            MoveCommandScope command = activeMoveCommand;
            if (command != null)
            {
                command.BufferDiagnostic(message);
                return;
            }

            AttackCommandScope attack = activeAttackCommand;
            if (attack != null)
            {
                attack.BufferDiagnostic(message);
                return;
            }

            if (CurrentOptions.RequiredOnly)
            {
                int tick = CaptureCurrentGameTick();
                if (requiredBackgroundDiagnosticTick != tick)
                {
                    FlushRequiredBackgroundDiagnosticSummary();
                    requiredBackgroundDiagnosticTick = tick;
                    requiredBackgroundDiagnostics.Clear();
                    requiredBackgroundSuppressedDiagnostics.Clear();
                }
                string stage = AttackCommandScope.GetDiagnosticStage(message ?? string.Empty);
                requiredBackgroundDiagnostics.TryGetValue(stage, out int retained);
                if (retained >= 3)
                {
                    requiredBackgroundSuppressedDiagnostics.TryGetValue(stage, out int suppressed);
                    requiredBackgroundSuppressedDiagnostics[stage] = suppressed + 1;
                    return;
                }
                requiredBackgroundDiagnostics[stage] = retained + 1;
            }

            LogDetailedInfo($"Bugfixes and QoL friendly-moat-movement {message}.");
        }

        internal void FlushRequiredBackgroundDiagnosticSummary()
        {
            if (!DetailedDiagnosticsEnabled)
                return;

            if (requiredBackgroundSuppressedDiagnostics.Count == 0)
                return;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-background-diagnostics tick={requiredBackgroundDiagnosticTick} " +
                $"suppressed={FormatCounts(requiredBackgroundSuppressedDiagnostics)}.");
        }

        internal void FlushAttackDiagnostics(AttackCommandScope command)
        {
            if (!DetailedDiagnosticsEnabled || command == null)
                return;
            long started = Stopwatch.GetTimestamp();
            foreach (string message in command.Diagnostics)
                LogDetailedInfo($"Bugfixes and QoL friendly-moat-movement {message}.");
            command.LogFlushTicks = Stopwatch.GetTimestamp() - started;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-attack-command-performance commandSeq={command.Sequence} " +
                $"dispatchMs={command.DispatchMilliseconds:F3} logFlushMs={command.LogFlushMilliseconds:F3} " +
                $"observerTotalMs={(command.DispatchMilliseconds + command.LogFlushMilliseconds):F3} " +
                $"diagnostics={command.DiagnosticMessages}/{command.Diagnostics.Count} " +
                $"diagnosticChars={command.DiagnosticCharacters} suppressed={FormatCounts(command.SuppressedDiagnostics)}.");
        }

        internal static string FormatCounts(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return "none";
            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.Ordinal);
            var values = new List<string>(keys.Count);
            foreach (string key in keys)
                values.Add(key + ":" + counts[key]);
            return string.Join(",", values);
        }

        internal static void MarkCommandMoatRelevant(
            MoveCommandScope command, RouteProbeSummary summary)
        {
            if (command != null &&
                (summary.FriendlyMoatTiles > 0 || summary.EnemyMoatTiles > 0))
            {
                command.MoatRelevant = true;
            }
        }

        internal void QualifyPendingCommandDiagnostics(MoveCommandScope command)
        {
            if (command == null || command.BuilderReached || pendingPlan == null ||
                command.DiggersAtDispatch == 0)
                return;

            // A Patrol leg can leave MoveHere after mode selection but before the builder.
            // Probe once in Post so precisely that early exit remains diagnosable.
            try
            {
                if (TryGetCachedRequiredFriendlyRouteForPlan(
                        pendingPlan, out RouteProbeSummary summary))
                {
                    MarkCommandMoatRelevant(command, summary);
                }
            }
            catch (Exception ex)
            {
                LogFailure("command-diagnostic-route", ex);
            }
        }

        internal void FlushCommandDiagnostics(MoveCommandScope command)
        {
            if (!DetailedDiagnosticsEnabled)
                return;

            bool queueRelevant = command != null &&
                ((command.HasQueuePreSnapshot && command.QueuePreSnapshot.Count > 0) ||
                 (command.HasQueuePostSnapshot && command.QueuePostSnapshot.Count > 0));
            bool slow = command != null && command.ElapsedMilliseconds >= 50.0;
            bool earlyFailure = command != null && command.DiggersAtDispatch > 0 &&
                command.UnitMoveCalls == 0;
            if (command == null || (!command.MoatRelevant && !queueRelevant && !slow && !earlyFailure))
                return;

            if (slow && !command.MoatRelevant && !queueRelevant && !earlyFailure)
            {
                LogDetailedInfo(
                    $"Bugfixes and QoL stage=friendly-moat-movement-move-command-performance commandSeq={command.Sequence} " +
                    $"tribe={command.TribeId} units={command.ActiveUnitsAtDispatch} " +
                    $"diggers={command.DiggersAtDispatch} elapsedMs={command.ElapsedMilliseconds:F3} " +
                    $"targetedSearches={command.TargetedRouteSearches} " +
                    $"targetedCacheHits={command.TargetedRouteCacheHits} " +
                    $"targetedExpanded={command.TargetedRouteExpandedNodes} " +
                    $"targetedSearchMs={command.TargetedRouteSearchMilliseconds:F3} " +
                    $"targetedMaxSearchMs={command.TargetedRouteMaximumSearchMilliseconds:F3} " +
                    $"weightedSearchMs={command.WeightedSearchMilliseconds:F3} " +
                    $"weightedMaxSearchMs={command.WeightedMaximumSearchMilliseconds:F3} " +
                    "moatIntervention=False.");
                return;
            }

            foreach (string message in command.Diagnostics)
                LogDetailedInfo($"Bugfixes and QoL friendly-moat-movement {message}.");
            if (command.SuppressedDiagnostics.Count != 0)
            {
                LogDetailedInfo(
                    $"Bugfixes and QoL stage=friendly-moat-movement-move-command-diagnostics commandSeq={command.Sequence} " +
                    $"diagnostics={command.DiagnosticMessages}/{command.Diagnostics.Count} " +
                    $"diagnosticChars={command.DiagnosticCharacters} " +
                    $"suppressed={FormatCounts(command.SuppressedDiagnostics)}.");
            }
        }

        internal void LogQueuedMoveHereOutcome(MoveCommandScope command, long returnValue)
        {
            if (command == null || command.IsNewOrder)
                return;

            string before = command.HasQueuePreSnapshot
                ? command.QueuePreSnapshot.ToString()
                : "unavailable";
            string after = command.HasQueuePostSnapshot
                ? command.QueuePostSnapshot.ToString()
                : "unavailable";
            bool consumerAlreadyAdvanced = command.HasQueuePreSnapshot &&
                command.QueuePreSnapshot.Count > 0 &&
                command.QueuePreSnapshot.Index > 0 &&
                command.QueuePreSnapshot.Index < command.QueuePreSnapshot.Count;
            string stage = returnValue != 0
                ? "queue-waypoint-accepted"
                : consumerAlreadyAdvanced
                    ? "queue-waypoint-skipped"
                    : "movehere-continuation-rejected";
            string skipProbe = returnValue == 0
                ? DescribeRejectedQueueWaypoint(command)
                : "not-required";
            LogCommandDiagnostic(
                $"stage={stage} commandSeq={command.Sequence} tribe={command.TribeId} " +
                $"target=({command.TargetX},{command.TargetY}) return={returnValue} " +
                $"consumerAlreadyAdvanced={consumerAlreadyAdvanced} " +
                $"queueBefore=[{before}] queueAfter=[{after}] " +
                $"regionCalls={command.RegionCalls} floodCalls={command.FloodCalls} " +
                $"modeCalls={command.ModeCalls} builderCalls={command.BuilderCalls} " +
                $"earlyRegionCalls={command.EarlyRegionCalls} " +
                $"earlyRegionBypasses={command.EarlyRegionBypasses} " +
                $"skipProbe=[{skipProbe}]");
        }

        internal string DescribeRejectedQueueWaypoint(MoveCommandScope command)
        {
            int alive = 0;
            int diggers = 0;
            int requiredFriendly = 0;
            RouteProbeSummary observed = default;
            foreach (int unitId in command.ActiveUnitIdsAtDispatch)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != command.TribeId)
                {
                    continue;
                }

                alive++;
                if (!CanDigMoat(unit))
                    continue;
                diggers++;
                var plan = new PlanScope(unitId, command.TargetX, command.TargetY)
                {
                    PlayerId = unit->r_ControllableForPlayerId
                };
                if (TryGetCachedRequiredFriendlyRouteForPlan(
                        plan, out RouteProbeSummary summary))
                {
                    requiredFriendly++;
                    observed.MergeObservations(summary);
                }
            }

            return $"alive={alive} diggers={diggers} " +
                $"cachedRequiredFriendly={requiredFriendly} {observed.ToLogFields()}";
        }

        internal void LogModeContext(PlanScope plan, GameUnit* unit, int vanillaResult)
        {
            if (!ShouldLogUnitPipeline)
                return;
            int startX = unit->r_CurrentTilePositionX;
            int startY = unit->r_CurrentTilePositionY;
            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            int startRegion = IsValidTileId(startTileId) ? pathRegionGrid[startTileId] : 0;
            uint startFlags = IsValidTileId(startTileId) ? tileFlags[startTileId] : 0;

            bool targetInBounds = plan.TargetX >= 0 && plan.TargetX < MapWidth &&
                plan.TargetY >= 0 && plan.TargetY < MapWidth;
            int targetCell = targetInBounds ? (plan.TargetY * MapWidth) + plan.TargetX : -1;
            int targetTileId = targetInBounds
                ? GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY)
                : -1;
            int targetRegion = IsValidTileId(targetTileId) ? pathRegionGrid[targetTileId] : 0;
            uint targetFlags = IsValidTileId(targetTileId) ? tileFlags[targetTileId] : 0;
            int targetAvailability = targetCell >= 0 ? movementTargetAvailability[targetCell] : -1;
            string source = ReferenceEquals(activePlan, plan) ? "central-planner" : "movehere-direct";

            LogCommandDiagnostic(
                $"stage=mode-context unit={plan.UnitId} player={plan.PlayerId} source={source} " +
                $"start=({startX},{startY}) target=({plan.TargetX},{plan.TargetY}) " +
                $"sameTile={startX == plan.TargetX && startY == plan.TargetY} " +
                $"startTile={startTileId} startRegion={startRegion} startFlags=0x{startFlags:X8} " +
                $"targetTile={targetTileId} targetRegion={targetRegion} " +
                $"targetFlags=0x{targetFlags:X8} targetAvailability={targetAvailability} " +
                $"vanillaMode={vanillaResult} effectiveMode=1");
        }

        internal static void RecordBuilderResult(MoveCommandScope command, int result)
        {
            if (command == null)
                return;

            command.LastBuilderResult = result;
            if (result > 0)
                command.PositiveBuilderCalls++;
        }

        internal static void RecordVanillaBuilderResult(
            MoveCommandScope command, int result)
        {
            if (command == null)
                return;

            command.VanillaBuilderCalls++;
            command.LastVanillaBuilderResult = result;
        }

        internal void LogRejectedPlannerRoute(PlanScope plan, RouteProbeSummary summary)
        {
            if (plan == null)
                return;

            int targetAvailability = -1;
            int targetRegion = 0;
            bool targetInBounds = plan.TargetX >= 0 && plan.TargetX < MapWidth &&
                plan.TargetY >= 0 && plan.TargetY < MapWidth;
            if (targetInBounds)
            {
                int targetCell = (plan.TargetY * MapWidth) + plan.TargetX;
                targetAvailability = movementTargetAvailability[targetCell];
                int targetTileId = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
                if (IsValidTileId(targetTileId))
                    targetRegion = pathRegionGrid[targetTileId];
            }

            bool moatRelevant = summary.FriendlyMoatTiles > 0 ||
                summary.EnemyMoatTiles > 0;
            if (!moatRelevant)
                return;

            string reason = !targetInBounds
                ? "target-out-of-bounds"
                : targetAvailability == 0
                    ? "target-unavailable-or-occupied"
                    : summary.EnemyMoatTiles > 0 && summary.FriendlyMoatTiles == 0
                        ? "enemy-moat-only"
                        : "no-qualified-friendly-moat-route";

            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-planner-owner-rejected unit={plan.UnitId} player={plan.PlayerId} " +
                $"target=({plan.TargetX},{plan.TargetY}) targetAvailability={targetAvailability} " +
                $"targetRegion={targetRegion} reason={reason} {summary.ToLogFields()}.");
        }

        internal void LogFailure(string stage, Exception ex)
        {
            if (callbackFailureReported)
                return;
            callbackFailureReported = true;
            APIShared.Internal.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL friendly-moat-movement {stage} callback failed once; Vanilla behavior remains active: {ex}");
        }

        internal void TryLogDiagnosticFailure(string stage, Exception ex)
        {
            try
            {
                if (!reportedDiagnosticFailureStages.Add(stage))
                    return;
                APIShared.Internal.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL friendly-moat-movement read-only diagnostic stage={stage} failed; " +
                    $"Vanilla behavior remains unchanged: {ex}");
            }
            catch
            {
                // Never let diagnostic error reporting escape into a native callback.
            }
        }

    }
}
