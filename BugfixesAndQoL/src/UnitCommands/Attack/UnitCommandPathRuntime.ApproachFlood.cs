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
        internal void ObserveAttackApproachFloodBuilder(
            IntPtr pathManager,
            int tribeId,
            int targetContext,
            uint targetX,
            uint targetY,
            int requestedResults,
            int sourceRegion,
            int movementClass)
        {
            IEnemyGatePathPolicy gate = BeginEnemyGateSearch(
                movementClass, EnemyGateSearchKind.Attack, out object gateScope);
            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("Attack", movementClass);
            bool bridgeCompleted = false;
            try
            {
                ObserveAttackApproachFloodBuilderWithMoat(
                    pathManager, tribeId, targetContext, targetX, targetY,
                    requestedResults, sourceRegion, movementClass);
                bridgeCompleted = true;
            }
            finally
            {
                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, bridgeCompleted, 0);
                EndEnemyGateSearch(gate, gateScope, EnemyGateSearchKind.Attack, true, false);
            }
        }

        internal void ObserveAttackApproachFloodBuilderWithMoat(
            IntPtr pathManager, int tribeId, int targetContext, uint targetX, uint targetY,
            int requestedResults, int sourceRegion, int movementClass)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            AttackApproachDiagnosticScope scope = null;
            AttackCommandScope fastCommand = null;
            bool nativeFloodCompleted = false;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!disposed && pathManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == mapEpoch && command.TribeId == tribeId &&
                    command.Command == TribeAICommand.AttackUnit)
                {
                    if (command.Options.RequiredOnly)
                    {
                        fastCommand = command;
                    }
                    else
                    {
                        EnsureAttackCommandCandidates(command);
                        ResolveAttackApproachRepresentative(
                            command, out int unitId, out int playerId, out eChimps unitType);
                        scope = new AttackApproachDiagnosticScope(
                            command,
                            AttackApproachKind.UnitFlood,
                            command.Sequence,
                            command.Command,
                            tribeId,
                            targetContext,
                            unchecked((int)targetX),
                            unchecked((int)targetY),
                            requestedResults,
                            sourceRegion,
                            movementClass,
                            unitId,
                            playerId,
                            unitType,
                            CaptureAttackApproachState(pathManager));
                        activeAttackApproachDiagnostic = scope;
                    }
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                TryLogDiagnosticFailure("attack-approach-unit-pre", ex);
                scope = null;
            }

            try
            {
                // DBC60 builds its depth-limited queue before extracting 50..500
                // results. Request the full native pool only near forbidden moat;
                // this does not repeat its flood for individual units.
                bool expand = IsBoundUnitAttackFlood(scope, targetX, targetY);
                if (expand)
                {
                    expand = false;
                    for (int dy = -2; dy <= 2 && !expand; dy++)
                    for (int dx = -2; dx <= 2 && !expand; dx++)
                    {
                        int x = (int)targetX + dx, y = (int)targetY + dy;
                        if ((uint)x < MapWidth && (uint)y < MapWidth)
                            expand = IsForbiddenFormationMoat(scope.PlayerId, x, y);
                    }
                }
                AttackCommandScope measuredCommand = scope?.OwnerCommand;
                long floodStarted = Stopwatch.GetTimestamp();
                try
                {
                    RunAttackApproachFloodWithVanillaLadderFix(
                        pathManager,
                        tribeId,
                        targetContext,
                        targetX,
                        targetY,
                        expand ? Math.Max(requestedResults, VanillaAttackFloodResultCapacity / 2) : requestedResults,
                        sourceRegion,
                        movementClass);
                }
                finally
                {
                    if (measuredCommand != null)
                    {
                        measuredCommand.UnitFloodCalls++;
                        measuredCommand.UnitFloodTicks += Stopwatch.GetTimestamp() - floodStarted;
                    }
                }
                nativeFloodCompleted = true;

                // Fast first accepts the unmodified native result. Only an empty result
                // justifies capturing the tribe group and retrying the bounded moat case.
                if (scope == null && fastCommand != null &&
                    CaptureAttackApproachState(pathManager).UsableResultCount == 0)
                {
                    EnsureAttackCommandCandidates(fastCommand);
                    ResolveAttackApproachRepresentative(
                        fastCommand, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        fastCommand, AttackApproachKind.UnitFlood, fastCommand.Sequence,
                        fastCommand.Command, tribeId, targetContext, unchecked((int)targetX),
                        unchecked((int)targetY), requestedResults, sourceRegion, movementClass,
                        unitId, playerId, unitType, CaptureAttackApproachState(pathManager));
                    activeAttackApproachDiagnostic = scope;
                    bool retry = IsBoundUnitAttackFlood(scope, targetX, targetY);
                    if (retry)
                    {
                        retry = false;
                        for (int dy = -2; dy <= 2 && !retry; dy++)
                        for (int dx = -2; dx <= 2 && !retry; dx++)
                        {
                            int x = (int)targetX + dx, y = (int)targetY + dy;
                            if ((uint)x < MapWidth && (uint)y < MapWidth)
                                retry = IsForbiddenFormationMoat(scope.PlayerId, x, y);
                        }
                    }
                    if (retry)
                        originalAttackApproachFloodBuilder(
                            pathManager, tribeId, targetContext, targetX, targetY,
                            Math.Max(requestedResults, VanillaAttackFloodResultCapacity / 2),
                            sourceRegion, movementClass);
                }
            }
            finally
            {
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(pathManager);
                        if (nativeFloodCompleted && scope.After.Generation != scope.Before.Generation &&
                            IsBoundUnitAttackFlood(scope, targetX, targetY))
                            FilterAttackOutput(pathManager, scope.PlayerId, scope.CommandSequence);
                        scope.After = CaptureAttackApproachState(pathManager);
                        PublishUnitAttackApproachTiles(scope.OwnerCommand, pathManager);
                        LogAttackApproachDiagnostic(scope);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-unit-post", ex);
                    }
                }
                activeAttackApproachDiagnostic = previous;
            }
        }

        internal bool IsBoundUnitAttackFlood(AttackApproachDiagnosticScope scope, uint x, uint y)
        {
            if (scope == null || scope.OwnerCommand == null || scope.OwnerCommand.MapEpoch != mapEpoch ||
                scope.Command != TribeAICommand.AttackUnit || scope.TargetContext != scope.OwnerCommand.TargetValue1 ||
                (uint)scope.TargetX != x || (uint)scope.TargetY != y ||
                scope.UnitId <= 0 ||
                !APIShared.UnitAccess.TryGetById(scope.UnitId, out GameUnit* source, out _) ||
                source == null || !CanDigMoat(source) || source->r_TribeId != scope.TribeId ||
                source->r_ControllableForPlayerId != scope.PlayerId) return false;
            return TryGetHostileLivingUnitAtTile(scope.PlayerId, (int)x, (int)y,
                scope.OwnerCommand.TargetValue1, scope.OwnerCommand.TargetValue2, out _, out _);
        }

        internal void FilterAttackOutput(IntPtr manager, int player, int sequence)
        {
            if (manager != nativePathManager || manager == IntPtr.Zero ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(player)) return;
            int removed = WeightedMoatRoutePlanner.FilterNativeAttackCandidates(
                (int*)((byte*)manager + PathManagerFloodResultTileOffset), VanillaAttackFloodResultCapacity,
                tile => !IsValidTileId(tile) || (IsCompletedMoatTile(tile) &&
                    ResolveCompletedMoatRelationship(player, tile) != CompletedMoatRelationship.Friendly));
            if (removed > 0)
                LogCommandDiagnostic($"stage=attack-slot-filter commandSeq={sequence} removed={removed} source=native-pool");
        }

        internal readonly struct AttackRegionFallbackDecision
        {
            internal AttackRegionFallbackDecision(
                bool allowed,
                string reason,
                RouteProbeSummary summary,
                int approachX,
                int approachY)
            {
                Allowed = allowed;
                Reason = reason;
                Summary = summary;
                ApproachX = approachX;
                ApproachY = approachY;
            }

            public bool Allowed { get; }
            public string Reason { get; }
            public RouteProbeSummary Summary { get; }
            public int ApproachX { get; }
            public int ApproachY { get; }

            public static AttackRegionFallbackDecision Allow(
                RouteProbeSummary summary,
                int approachX,
                int approachY,
                string reason = "required-friendly-moat-route") =>
                new AttackRegionFallbackDecision(
                    true, reason, summary, approachX, approachY);

            public static AttackRegionFallbackDecision Reject(
                string reason,
                RouteProbeSummary summary = default,
                int approachX = -1,
                int approachY = -1) =>
                new AttackRegionFallbackDecision(false, reason, summary, approachX, approachY);
        }

        internal readonly struct AttackApproachState
        {
            public AttackApproachState(
                int generation,
                int depth,
                int queueHead,
                int queueTail,
                int resultCount,
                int usableResultCount,
                int malformedResultCount,
                int firstResultTile,
                int firstCompanionTile,
                int firstScore)
            {
                Generation = generation;
                Depth = depth;
                QueueHead = queueHead;
                QueueTail = queueTail;
                ResultCount = resultCount;
                UsableResultCount = usableResultCount;
                MalformedResultCount = malformedResultCount;
                FirstResultTile = firstResultTile;
                FirstCompanionTile = firstCompanionTile;
                FirstScore = firstScore;
            }

            public int Generation { get; }
            public int Depth { get; }
            public int QueueHead { get; }
            public int QueueTail { get; }
            public int ResultCount { get; }
            public int UsableResultCount { get; }
            public int MalformedResultCount { get; }
            public int FirstResultTile { get; }
            public int FirstCompanionTile { get; }
            public int FirstScore { get; }

            public string ToLogFields() =>
                $"generation={Generation} depth={Depth} queue={QueueHead}->{QueueTail} " +
                $"results={ResultCount} usable={UsableResultCount} malformed={MalformedResultCount} " +
                $"first={FirstResultTile}/{FirstCompanionTile}/{FirstScore}";
        }

    }
}
