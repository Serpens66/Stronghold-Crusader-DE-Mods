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
        internal void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (disposed)
                return;

            // TEMP_GATE_ROUTE_ACCEPTANCE: existing Pre/Post pair only.
            ObserveTemporaryAssassinOrderPhase(args.Phase == EventHookPhase.Pre, "group-move",
                args.TribeId, (int)args.MoveType, args.TileX, args.TileY, args.ReturnValue);

            if (args.Phase == EventHookPhase.Pre)
            {
                PushManualCommandContext();
                ClearUnitMoveFrames();
                RemoveTrackedAttacksForTribe(args.TribeId, "move-command");
                RemoveTrackedMoatMovesForTribe(args.TribeId, "new-move-command");
                activeMoveCommand = new MoveCommandScope(
                    ++moveCommandSequence,
                    args.TribeId,
                    args.TileX,
                    args.TileY,
                    args.IsPatrolPath != 0,
                    args.IsNewOrder,
                    args.MoveType,
                    activeAttackCommand?.Sequence ?? 0,
                    activeAttackCommand?.Command ?? TribeAICommand.Unknown0,
                    MovementOptionsSnapshot.Capture(settings));
                if (!activeMoveCommand.Options.RequiredOnly ||
                    settings.EnableMoveFormationEnhancements)
                    CaptureMoveCommandGroupSummary(activeMoveCommand);
                PrepareNativeManualGroup(args);
                ObserveNativeWaypointQueueAtCommand(
                    activeMoveCommand, "pre", args.TileX, args.TileY);
                try
                {
                    LogCommandDiagnostic(
                        $"stage=move-command commandSeq={activeMoveCommand.Sequence} " +
                        $"tribe={args.TribeId} target=({args.TileX},{args.TileY}) " +
                        $"phase=pre patrol={args.IsPatrolPath} newOrder={args.IsNewOrder} " +
                        $"moveType={args.MoveType} activeUnits={activeMoveCommand.ActiveUnitsAtDispatch} " +
                        $"diggers={activeMoveCommand.DiggersAtDispatch} " +
                        $"onMoat={activeMoveCommand.UnitsOnMoatAtDispatch} " +
                        $"playerMask=0x{activeMoveCommand.PlayerMaskAtDispatch:X} " +
                        $"parentAttack={activeMoveCommand.ParentAttackCommandSequence}/" +
                        $"{activeMoveCommand.ParentAttackCommand}");
                }
                catch
                {
                    // Diagnostics must not escape into the synchronous command event.
                }
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                ClearUnitMoveFrames();
                MoveCommandScope command = activeMoveCommand;
                try
                {
                    ObserveNativeWaypointQueueAtCommand(
                        command, "post", args.TileX, args.TileY);
                    QualifyPendingCommandDiagnostics(command);
                    string lastBuilderResult = command != null && command.BuilderCalls > 0
                        ? command.LastBuilderResult.ToString()
                        : "none";
                    string lastVanillaBuilderResult = command != null &&
                        command.VanillaBuilderCalls > 0
                            ? command.LastVanillaBuilderResult.ToString()
                            : "none";
                    if (command != null)
                    {
                        command.ElapsedMilliseconds =
                            (Stopwatch.GetTimestamp() - command.StartTimestamp) * 1000.0 /
                            Stopwatch.Frequency;
                    }
                    LogCommandDiagnostic(
                        $"stage=move-command-result commandSeq={command?.Sequence ?? 0} " +
                        $"tribe={args.TribeId} " +
                        $"target=({args.TileX},{args.TileY}) patrol={args.IsPatrolPath} " +
                        $"newOrder={args.IsNewOrder} moveType={args.MoveType} return={args.ReturnValue} " +
                        $"activeUnits={command?.ActiveUnitsAtDispatch ?? 0} " +
                        $"diggers={command?.DiggersAtDispatch ?? 0} " +
                        $"onMoat={command?.UnitsOnMoatAtDispatch ?? 0} " +
                        $"playerMask=0x{(command?.PlayerMaskAtDispatch ?? 0):X} " +
                        $"parentAttack={command?.ParentAttackCommandSequence ?? 0}/" +
                        $"{command?.ParentAttackCommand ?? TribeAICommand.Unknown0} " +
                        $"plannerCalls={command?.CentralPlannerCalls ?? 0} " +
                        $"nativeModeEntries={nativeModeEntries} preBuilderFailures={preBuilderFailures} preBuilderRecovered={preBuilderRecovered} " +
                        $"preBuilderRejected={FormatRecoveryRejections()} " +
                        $"cursorTopologyBuilds={cursorTopologyBuilds} cursorTopologyUpdates={cursorTopologyUpdates} " +
                        $"unitMoveCalls={command?.UnitMoveCalls ?? 0} " +
                        $"unitMoveCompleted={command?.UnitMoveCompleted ?? 0} " +
                        $"unitMovePositive={command?.UnitMovePositive ?? 0} " +
                        $"unitMoveWithoutBuilder={command?.UnitMoveWithoutBuilder ?? 0} " +
                        $"unitMoveAlreadyArrived={command?.UnitMoveAlreadyArrived ?? 0} " +
                        $"searchRunsTotal={weightedMoatRoutePlanner.SearchRuns} searchNodesTotal={weightedMoatRoutePlanner.SearchNodes} " +
                        $"cachedSearchFields={weightedMoatRoutePlanner.CachedSearchFields} " +
                        $"cachedFieldHitsTotal={weightedMoatRoutePlanner.CachedFieldHits} nativeRegionQueriesTotal={nativeGroundQueries} " +
                        $"nativeRegionCacheHitsTotal={nativeGroundCacheHits} " +
                        $"unitMoveAbandoned={command?.UnitMoveAbandoned ?? 0} " +
                        $"builderIntermediateTargets={command?.BuilderIntermediateTargets ?? 0} " +
                        $"floodCalls={command?.FloodCalls ?? 0} " +
                        $"floodVanillaPositive={command?.FloodVanillaPositive ?? 0} " +
                        $"floodBypasses={command?.FloodFillBypasses ?? 0} " +
                        $"modeCalls={command?.ModeCalls ?? 0} regionCalls={command?.RegionCalls ?? 0} " +
                        $"builderCalls={command?.BuilderCalls ?? 0} " +
                        $"vanillaBuilderCalls={command?.VanillaBuilderCalls ?? 0} " +
                        $"fallbackBuilderCalls={command?.FallbackBuilderCalls ?? 0} " +
                        $"contractRejections={command?.FallbackContractRejections ?? 0} " +
                        $"contractReasons={FormatContractRejectionReasons(command)} " +
                        $"fallbackRollbacks={command?.FallbackRollbacks ?? 0} " +
                        $"positiveBuilders={command?.PositiveBuilderCalls ?? 0} " +
                        $"weightedUnits={command?.WeightedUnitIds.Count ?? 0} " +
                        $"weightedDecisions={command?.WeightedDecisions ?? 0} " +
                        $"weightedPublished={command?.WeightedPublished ?? 0} " +
                        $"weightedSearchMs={(command?.WeightedSearchMilliseconds ?? 0):F3} " +
                        $"weightedMaxSearchMs={(command?.WeightedMaximumSearchMilliseconds ?? 0):F3} " +
                        $"routeMode={(int)(command?.Options.RouteMode ?? CurrentOptions.RouteMode)} " +
                        $"groundChecks={command?.Required.GroundChecks ?? 0} groundHits={command?.Required.GroundHits ?? 0} samePclHits={command?.Required.SamePclHits ?? 0} topologyExclusions={command?.Required.TopologyExclusions ?? 0} " +
                        $"groundDecisionCacheHits={command?.Required.GroundDecisionCacheHits ?? 0} decisionCacheHits={command?.Required.DecisionCacheHits ?? 0} " +
                        $"exactGroundSearches={command?.Required.ExactGroundSearches ?? 0} exactGroundFieldCacheHits={command?.Required.ExactGroundFieldCacheHits ?? 0} exactGroundNodes={command?.Required.ExactGroundNodes ?? 0} " +
                        $"groundProofMs={(command?.Required.GroundMilliseconds ?? 0):F3} exactGroundMs={(command?.Required.ExactGroundMilliseconds ?? 0):F3} " +
                        $"requiredSearches={command?.Required.Searches ?? 0} requiredQualified={command?.Required.Qualified ?? 0} requiredSearchMs={(command?.Required.SearchMilliseconds ?? 0):F3} requiredPublished={command?.Required.Published ?? 0} " +
                        $"requiredPublishAuditMs={(command?.Required.PublicationMilliseconds ?? 0):F3} requiredRejected={command?.Required.Rejected ?? 0} requiredReasons={FormatCounts(command?.Required.RejectionReasons)} " +
                        $"commandPreBuilderFailures={command?.PreBuilderFailures ?? 0} commandPreBuilderRecovered={command?.PreBuilderRecovered ?? 0} " +
                        $"commandPreBuilderRejected={FormatCounts(command?.PreBuilderRejectionReasons)} " +
                        $"trackersStarted={command?.Required.TrackersStarted ?? 0} trackersSuppressed={command?.Required.TrackersSuppressed ?? 0} " +
                        $"targetedSearches={command?.TargetedRouteSearches ?? 0} " +
                        $"targetedSearchPasses={command?.TargetedRouteSearchPasses ?? 0} " +
                        $"targetedCacheHits={command?.TargetedRouteCacheHits ?? 0} " +
                        $"targetedExpanded={command?.TargetedRouteExpandedNodes ?? 0} " +
                        $"targetedSearchMs={(command?.TargetedRouteSearchMilliseconds ?? 0):F3} " +
                        $"targetedMaxSearchMs={(command?.TargetedRouteMaximumSearchMilliseconds ?? 0):F3} " +
                        $"elapsedMs={(command?.ElapsedMilliseconds ?? 0):F3} " +
                        $"lastVanillaBuilderResult={lastVanillaBuilderResult} " +
                        $"lastBuilderResult={lastBuilderResult}");
                    LogQueuedMoveHereOutcome(command, args.ReturnValue);
                    FlushCommandDiagnostics(command);
                }
                catch
                {
                    // Diagnostics must not escape into the synchronous command event.
                }
                try
                {
                    CaptureDeferredFastMoveScope(command);
                }
                catch (Exception ex)
                {
                    // This scope is optional and must fail closed without affecting Vanilla.
                    ClearDeferredFastMoveScope();
                    TryLogDiagnosticFailure("deferred-fast-move-scope", ex);
                }
                RestoreManualCommandContext();
            }
        }

        internal void ObserveTribeTargetOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (disposed)
                return;

            try
            {
                // TEMP_GATE_ROUTE_ACCEPTANCE: existing Pre/Post pair only.
            ObserveTemporaryAssassinOrderPhase(args.Phase == EventHookPhase.Pre, "group-target",
                args.TribeId, (int)args.AICommand, args.TargetValue1, args.TargetValue2, args.ReturnValue);

            if (args.Phase == EventHookPhase.Pre)
                {
                    BeginDirectFillCommand(args);
                    RemoveTrackedAttacksForTribe(args.TribeId, "new-target-command");
                    RemoveTrackedMoatMovesForTribe(args.TribeId, "new-target-command");
                    if (IsAttackCommand(args.AICommand))
                    {
                        activeAttackCommand = new AttackCommandScope(
                            activeAttackCommand,
                            ++attackCommandSequence,
                            mapEpoch,
                            args.TribeId,
                            args.AICommand,
                            args.TargetValue1,
                            args.TargetValue2,
                            MovementOptionsSnapshot.Capture(settings));
                        if (!activeAttackCommand.Options.RequiredOnly)
                        {
                            CaptureAttackCommandCandidates(activeAttackCommand);
                            LogAttackCommandCandidates(activeAttackCommand, "pre");
                        }
                    }
                }

                LogCommandDiagnostic(
                    $"stage=target-command phase={args.Phase.ToString().ToLowerInvariant()} " +
                    $"tribe={args.TribeId} aiCommand={args.AICommand} " +
                    $"target1={args.TargetValue1} target2={args.TargetValue2} a6={args.a6} " +
                    $"return={args.ReturnValue}");

                if (args.Phase == EventHookPhase.Post && IsAttackCommand(args.AICommand))
                {
                    AttackCommandScope scope = activeAttackCommand;
                    if (scope != null && scope.Matches(args, mapEpoch))
                    {
                        LogAttackCommandCandidates(scope, "post");
                        if (args.ReturnValue > 0)
                        {
                            if (!scope.Options.RequiredOnly || scope.CandidatesCaptured)
                                TrackUnitsUpdatedByAttackCommand(args, scope);
                            RemoveSynchronousAttackTrackers(scope, "command-dispatched");
                        }
                        else
                            RemoveSynchronousAttackTrackers(scope, "command-rejected");
                        long dispatchFinished = Stopwatch.GetTimestamp();
                        scope.DispatchElapsedTicks = dispatchFinished - scope.StartedTimestamp;
                        double exclusiveQualificationMilliseconds = Math.Max(
                            0.0, scope.QualificationMilliseconds - scope.FloodQualificationMilliseconds);
                        double exclusiveWeightedMilliseconds = Math.Max(
                            0.0, scope.WeightedPhaseMilliseconds - scope.WeightedAuditMilliseconds);
                        double exclusiveRequiredMilliseconds = scope.Required.ExclusiveGroundMilliseconds +
                            scope.Required.ExclusiveSearchMilliseconds +
                            scope.Required.ExclusivePublicationMilliseconds;
                        double accountedMilliseconds = scope.UnitFloodMilliseconds +
                            exclusiveQualificationMilliseconds + scope.NativeBuilderMilliseconds +
                            scope.AuditMilliseconds + exclusiveWeightedMilliseconds +
                            exclusiveRequiredMilliseconds;
                        scope.ResidualMilliseconds = Math.Max(
                            0.0, scope.DispatchMilliseconds - accountedMilliseconds);
                        LogCommandDiagnostic(
                            $"stage=attack-command-summary commandSeq={scope.Sequence} elapsedMs={scope.DispatchMilliseconds:F3} " +
                            $"tribe={scope.TribeId} command={scope.Command} " +
                            $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                            $"return={args.ReturnValue} weightedUnits={scope.WeightedUnitIds.Count} " +
                            $"weightedDecisions={scope.WeightedDecisions} " +
                            $"weightedPublished={scope.WeightedPublished} " +
                            $"weightedSearchMs={scope.WeightedSearchMilliseconds:F3} " +
                            $"weightedMaxSearchMs={scope.WeightedMaximumSearchMilliseconds:F3} " +
                            $"unitFloodCalls={scope.UnitFloodCalls} unitFloodMs={scope.UnitFloodMilliseconds:F3} " +
                            $"qualificationCalls={scope.QualificationCalls} qualificationMs={scope.QualificationMilliseconds:F3} qualificationInsideFloodMs={scope.FloodQualificationMilliseconds:F3} " +
                            $"nativeBuilderCalls={scope.NativeBuilderCalls} nativeBuilderMs={scope.NativeBuilderMilliseconds:F3} " +
                            $"auditCalls={scope.AuditCalls} auditMs={scope.AuditMilliseconds:F3} " +
                            $"weightedPhaseMs={scope.WeightedPhaseMilliseconds:F3} weightedAuditMs={scope.WeightedAuditMilliseconds:F3} residualMs={scope.ResidualMilliseconds:F3} " +
                            $"routeMode={(int)scope.Options.RouteMode} " +
                            $"groundChecks={scope.Required.GroundChecks} groundHits={scope.Required.GroundHits} samePclHits={scope.Required.SamePclHits} topologyExclusions={scope.Required.TopologyExclusions} " +
                            $"groundDecisionCacheHits={scope.Required.GroundDecisionCacheHits} decisionCacheHits={scope.Required.DecisionCacheHits} " +
                            $"exactGroundSearches={scope.Required.ExactGroundSearches} exactGroundFieldCacheHits={scope.Required.ExactGroundFieldCacheHits} exactGroundNodes={scope.Required.ExactGroundNodes} " +
                            $"groundProofMs={scope.Required.GroundMilliseconds:F3} exactGroundMs={scope.Required.ExactGroundMilliseconds:F3} groundExclusiveMs={scope.Required.ExclusiveGroundMilliseconds:F3} " +
                            $"requiredSearches={scope.Required.Searches} requiredSearchMs={scope.Required.SearchMilliseconds:F3} requiredSearchExclusiveMs={scope.Required.ExclusiveSearchMilliseconds:F3} requiredPublished={scope.Required.Published} " +
                            $"requiredPublishAuditMs={scope.Required.PublicationMilliseconds:F3} requiredPublishExclusiveMs={scope.Required.ExclusivePublicationMilliseconds:F3} requiredRejected={scope.Required.Rejected} requiredReasons={FormatCounts(scope.Required.RejectionReasons)} " +
                            $"trackersStarted={scope.Required.TrackersStarted} trackersSuppressed={scope.Required.TrackersSuppressed}");
                        FlushAttackDiagnostics(scope);
                    }
                }
            }
            catch
            {
                // Target-order observation must never affect attack command dispatch.
            }
            finally
            {
                if (args.Phase == EventHookPhase.Post)
                    EndDirectFillCommand(args);
                if (args.Phase == EventHookPhase.Post && IsAttackCommand(args.AICommand))
                {
                    AttackCommandScope scope = activeAttackCommand;
                    if (scope != null)
                        activeAttackCommand = scope.Previous;
                    if (pendingPlan != null && pendingPlan.AttackMovementQualified)
                        pendingPlan = null;
                }
            }
        }

    }
}
