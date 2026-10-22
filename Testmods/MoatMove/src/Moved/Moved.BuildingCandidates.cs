extern alias BugfixesRuntime;
using BepInEx.Logging;
using APIShared;
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
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.BuildingCandidates.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override void BuildingApproachBuilderTraversal(
            IntPtr pathManager, int tribeId, int buildingId, int requestedResults,
            int sourceRegion, int movementClass)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            BuildingApproachPerformanceScope previousPerformance =
                activeBuildingApproachPerformance;
            AttackApproachDiagnosticScope scope = null;
            BuildingApproachPerformanceScope performance = null;
            long started = 0;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!runtime.disposed && pathManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == runtime.mapEpoch && command.TribeId == tribeId &&
                    IsBuildingAttackCommand(command.Command) &&
                    !command.Options.RequiredOnly)
                {
                    runtime.EnsureAttackCommandCandidates(command);
                    ResolveAttackApproachRepresentative(
                        command, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        command,
                        AttackApproachKind.BuildingApproach,
                        command.Sequence,
                        command.Command,
                        tribeId,
                        buildingId,
                        -1,
                        -1,
                        requestedResults,
                        sourceRegion,
                        movementClass,
                        unitId,
                        playerId,
                        unitType,
                        CaptureAttackApproachState(
                            pathManager,
                            requirePairedResult: true,
                            requireReachableScore: false));
                    performance = new BuildingApproachPerformanceScope(
                        command.Sequence, buildingId);
                    activeAttackApproachDiagnostic = scope;
                    activeBuildingApproachPerformance = performance;
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                activeBuildingApproachPerformance = previousPerformance;
                runtime.TryLogDiagnosticFailure("attack-approach-building-pre", ex);
                scope = null;
            }

            try
            {
                started = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
                runtime.RunBuildingApproachBuilderWithVanillaLadderFix(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, movementClass);
            }
            finally
            {
                if (performance != null && started != 0)
                    performance.TotalElapsedTicks = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - started;
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(
                            pathManager,
                            requirePairedResult: true,
                            requireReachableScore: false);
                        LogAttackApproachDiagnostic(scope);
                        LogBuildingApproachPerformance(performance);
                    }
                    catch (Exception ex)
                    {
                        runtime.TryLogDiagnosticFailure("attack-approach-building-post", ex);
                    }
                }
                activeBuildingApproachPerformance = previousPerformance;
                activeAttackApproachDiagnostic = previous;
            }
        }

        internal override void BuildingCandidateConsumerTraversal(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            BuildingConsumerPerformanceScope previousPerformance =
                activeBuildingConsumerPerformance;
            AttackApproachDiagnosticScope scope = null;
            BuildingConsumerPerformanceScope performance = null;
            BuildingApproachCandidate[] vanillaCandidates = Array.Empty<BuildingApproachCandidate>();
            AttackCommandScope fastCommand = null;
            AttackApproachState fastBefore = default;
            bool vanillaCompleted = false;
            long vanillaStarted = 0;
            long vanillaElapsedTicks = 0;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!runtime.disposed && tribeManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == runtime.mapEpoch && command.TribeId == tribeId &&
                    IsBuildingAttackCommand(command.Command))
                {
                    vanillaCandidates = CaptureBuildingApproachCandidates(runtime.nativePathManager);
                    if (command.Options.RequiredOnly)
                    {
                        fastCommand = command;
                        fastBefore = CaptureAttackApproachState(
                            runtime.nativePathManager,
                            requirePairedResult: true,
                            requireReachableScore: false);
                    }
                    else
                    {
                        runtime.EnsureAttackCommandCandidates(command);
                        ResolveAttackApproachRepresentative(
                            command, out int unitId, out int playerId, out eChimps unitType);
                        scope = new AttackApproachDiagnosticScope(
                            command,
                            AttackApproachKind.BuildingCandidateConsumer,
                            command.Sequence,
                            command.Command,
                            tribeId,
                            command.TargetValue1,
                            -1,
                            -1,
                            -1,
                            -1,
                            -1,
                            unitId,
                            playerId,
                            unitType,
                            CaptureAttackApproachState(
                                runtime.nativePathManager,
                                requirePairedResult: true,
                                requireReachableScore: false))
                        {
                            ConsumerVariant = builderVariant
                        };
                        performance = new BuildingConsumerPerformanceScope(
                            command.Sequence, command.TargetValue1, vanillaCandidates.Length);
                        activeAttackApproachDiagnostic = scope;
                    }
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                runtime.TryLogDiagnosticFailure("attack-approach-building-consumer-pre", ex);
                scope = null;
            }

            try
            {
                vanillaStarted = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
                runtime.originalBuildingCandidateConsumer(tribeManager, tribeId, builderVariant);
                vanillaCompleted = true;
            }
            finally
            {
                if (vanillaStarted != 0)
                {
                    vanillaElapsedTicks = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - vanillaStarted;
                    if (performance != null)
                        performance.VanillaElapsedTicks = vanillaElapsedTicks;
                }
                if (scope == null && vanillaCompleted && fastCommand != null &&
                    CaptureAttackApproachState(runtime.nativePathManager, requirePairedResult: true)
                        .UsableResultCount == 0)
                {
                    runtime.EnsureAttackCommandCandidates(fastCommand);
                    ResolveAttackApproachRepresentative(
                        fastCommand, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        fastCommand, AttackApproachKind.BuildingCandidateConsumer,
                        fastCommand.Sequence, fastCommand.Command, tribeId,
                        fastCommand.TargetValue1, -1, -1, -1, -1, -1,
                        unitId, playerId, unitType, fastBefore)
                    {
                        ConsumerVariant = builderVariant
                    };
                    performance = new BuildingConsumerPerformanceScope(
                        fastCommand.Sequence, fastCommand.TargetValue1,
                        vanillaCandidates.Length)
                    {
                        VanillaElapsedTicks = vanillaElapsedTicks
                    };
                    activeAttackApproachDiagnostic = scope;
                }
                if (scope != null)
                {
                    try
                    {
                        AttackApproachState vanillaAfter =
                            CaptureAttackApproachState(runtime.nativePathManager, requirePairedResult: true);
                        BuildingConsumerFallbackResult fallback;
                        LadderBuildingCandidateRestoreResult ladderRestore;
                        long fallbackStarted = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
                        activeBuildingConsumerPerformance = performance;
                        try
                        {
                            fallback = vanillaCompleted
                                ? TryApplyBuildingConsumerFallback(
                                    scope, tribeManager, vanillaCandidates, vanillaAfter)
                                : BuildingConsumerFallbackResult.NotAttempted("vanilla-threw");
                        }
                        finally
                        {
                            if (performance != null)
                            {
                                performance.FallbackElapsedTicks =
                                    (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - fallbackStarted;
                            }
                            activeBuildingConsumerPerformance = previousPerformance;
                        }
                        ladderRestore = vanillaCompleted
                            ? runtime.RestoreVanillaLadderBuildingCandidates(
                                scope.OwnerCommand, tribeManager, vanillaCandidates)
                            : new LadderBuildingCandidateRestoreResult(
                                "vanilla-threw", 0, 0, 0, 0);
                        PublishBuildingApproachPairs(scope.OwnerCommand, runtime.nativePathManager);
                        scope.After = CaptureAttackApproachState(
                            runtime.nativePathManager,
                            requirePairedResult: true,
                            requireReachableScore: false);
                        LogBuildingConsumerCandidates(
                            scope, vanillaCandidates, vanillaAfter, fallback, ladderRestore);
                        LogBuildingConsumerPerformance(performance, fallback);
                        LogAttackApproachDiagnostic(scope);
                    }
                    catch (Exception ex)
                    {
                        runtime.TryLogDiagnosticFailure("attack-approach-building-consumer-post", ex);
                    }
                }
                else if (vanillaCompleted && fastCommand != null)
                {
                    try
                    {
                        LadderBuildingCandidateRestoreResult ladderRestore =
                            runtime.RestoreVanillaLadderBuildingCandidates(
                                fastCommand, tribeManager, vanillaCandidates);
                        if (ladderRestore.RestoredPairs > 0)
                        {
                            PublishBuildingApproachPairs(fastCommand, runtime.nativePathManager);
                            runtime.LogCommandDiagnostic(
                                $"stage=building-consumer-ladder commandSeq={fastCommand.Sequence} " +
                                $"building={fastCommand.TargetValue1}/{fastCommand.TargetValue2} " +
                                $"proofs={ladderRestore.ProvenTransitions} " +
                                $"afterMoatPairs={ladderRestore.AfterMoatPairs} " +
                                $"eligible={ladderRestore.EligiblePairs} " +
                                $"ladderRestored={ladderRestore.RestoredPairs} " +
                                $"reason={ladderRestore.Reason}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        runtime.TryLogDiagnosticFailure(
                            "attack-approach-building-consumer-ladder-fast", ex);
                    }
                }
                activeBuildingConsumerPerformance = previousPerformance;
                activeAttackApproachDiagnostic = previous;
            }
        }

        internal bool BuildingCandidateEdge(int player,int from,int to,int d,bool endpoint,bool reserved,
            out bool moat,out bool structure)
        {
            bool valid=runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(player,from%MapWidth,from/MapWidth,
                GameTileManagerAPI.Instance.GetTileId(from%MapWidth,from/MapWidth),to%MapWidth,to/MapWidth,
                GameTileManagerAPI.Instance.GetTileId(to%MapWidth,to/MapWidth),d,endpoint,reserved,
                MoatTraversalPolicy.FriendlyOnly,out MoatTraversalEdgeKind kind,out structure);
            moat=kind!=MoatTraversalEdgeKind.Ground; return valid;
        }
    }
}
