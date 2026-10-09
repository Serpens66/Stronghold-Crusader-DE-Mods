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
        internal void TrackUnitsUpdatedByAttackCommand(
            TribeIssueOrderWithTargetEventArgs args,
            AttackCommandScope scope)
        {
            EnsureAttackCommandCandidates(scope);
            int trackedCount = 0;
            foreach (int unitId in scope.CandidateUnitIds)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != args.TribeId ||
                    !MatchesCompletedAttackTargetContext(unit, scope))
                {
                    continue;
                }

                GetOrCreateAttackTracker(scope, unitId);
                trackedCount++;
            }

            LogCommandDiagnostic(
                $"stage=attack-track-start tribe={args.TribeId} command={args.AICommand} " +
                $"target1={args.TargetValue1} target2={args.TargetValue2} units={trackedCount}");
        }

        internal void CaptureAttackCommandCandidates(AttackCommandScope scope)
        {
            if (scope == null || scope.CandidatesCaptured)
                return;
            scope.CandidatesCaptured = true;
            if (!TryCaptureOrderedActiveGroupUnits(
                    nativeTribeManager, scope.TribeId, out int[] unitIds))
                return;
            foreach (int unitId in unitIds)
            {
                if (APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) &&
                    unit != null && APIShared.UnitAccess.IsReallyAlive(unit) &&
                    unit->r_TribeId == scope.TribeId)
                {
                    scope.CandidateUnitIds.Add(unitId);
                    if (scope.PreCandidateSignatures.Count < 24)
                        scope.PreCandidateSignatures[unitId] = GetAttackCandidateSignature(unit);
                }
            }
        }

        internal void EnsureAttackCommandCandidates(AttackCommandScope scope)
        {
            if (scope != null && !scope.CandidatesCaptured)
                CaptureAttackCommandCandidates(scope);
        }

        internal void LogAttackCommandCandidates(AttackCommandScope scope, string phase)
        {
            if (phase == "pre")
            {
                LogCommandDiagnostic(
                    $"stage=attack-command-candidate phase=pre tribe={scope.TribeId} " +
                    $"command={scope.Command} target={scope.TargetValue1}/{scope.TargetValue2} " +
                    $"tribeUnits={scope.CandidateUnitIds.Count}");
                return;
            }

            int logged = 0;
            foreach (int unitId in scope.CandidateUnitIds)
            {
                if (logged >= 24) break;
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null)
                    continue;

                string candidateState = GetAttackCandidateSignature(unit);
                if (scope.PreCandidateSignatures.TryGetValue(unitId, out string preState) &&
                    string.Equals(preState, candidateState, StringComparison.Ordinal) &&
                    !MatchesAttackTargetContext(
                        unit, scope.Command, scope.TargetValue1, scope.TargetValue2))
                {
                    continue;
                }
                string signature =
                    $"{phase}:{scope.MapEpoch}:{scope.TribeId}:{scope.Command}:" +
                    $"{scope.TargetValue1}:{scope.TargetValue2}:{candidateState}";
                if (lastAttackCommandCandidates.TryGetValue(unitId, out string previous) &&
                    string.Equals(previous, signature, StringComparison.Ordinal))
                {
                    continue;
                }

                lastAttackCommandCandidates[unitId] = signature;
                logged++;
                LogCommandDiagnostic(
                    $"stage=attack-command-candidate phase={phase} unit={unitId} " +
                    $"type={unit->r_UnitChimp} global={unit->r_GlobalId} player={unit->r_ControllableForPlayerId} " +
                    $"tribe={unit->r_TribeId}/{scope.TribeId} aiState={unit->r_AIState} " +
                    $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope.Command} " +
                    $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                    $"contextUnit={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId} " +
                    $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                    $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                    $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY})");
            }

            if (logged == 0 && phase == "post")
            {
                LogCommandDiagnostic(
                    $"stage=attack-command-candidate phase=post tribe={scope.TribeId} " +
                    $"command={scope.Command} target={scope.TargetValue1}/{scope.TargetValue2} " +
                    $"changedUnits=0 candidates={scope.CandidateUnitIds.Count}");
                foreach (int unitId in scope.CandidateUnitIds)
                {
                    if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                        unit == null)
                    {
                        continue;
                    }

                    LogCommandDiagnostic(
                        $"stage=attack-command-candidate phase=post-unchanged " +
                        $"commandSeq={scope.Sequence} unit={unitId} type={unit->r_UnitChimp} " +
                        $"alive={unit->r_AliveState} global={unit->r_GlobalId} " +
                        $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId}/{scope.TribeId} " +
                        $"aiState={unit->r_AIState} " +
                        $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope.Command} " +
                        $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"targetTile=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}) " +
                        $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2}) " +
                        $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}) " +
                        $"contextUnit={unit->r_AI_ContextTargetUnitId}/" +
                        $"{unit->r_AI_ContextTargetUnitGlobalId} " +
                        $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                        $"path={unit->r_PathPlanRelated1}/{unit->r_PathPlanStateBitFlags}/" +
                        $"{unit->r_MovementSubstep}/{unit->r_CurrentPathPlanIndex}/" +
                        $"{unit->r_PathPlanLength}");
                }
            }
        }

        internal static string GetAttackCandidateSignature(GameUnit* unit) =>
            $"{unit->r_AIState}:{unit->r_AI_LastIssuedTribeCommand}:" +
            $"{unit->r_AI_ContextTargetUnitId}:{unit->r_AI_ContextTargetUnitGlobalId}:" +
            $"{unit->r_AI_ContextTargetBuildingTileId}:" +
            $"{unit->r_AttackMoveToTargetTileX}:{unit->r_AttackMoveToTargetTileY}:" +
            $"{unit->r_TargetTilePositionX}:{unit->r_TargetTilePositionY}";

        internal AttackUnitTracker GetOrCreateAttackTracker(AttackCommandScope scope, int unitId)
        {
            if (trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker) &&
                tracker.MapEpoch == scope.MapEpoch && tracker.TribeId == scope.TribeId &&
                tracker.Command == scope.Command && tracker.TargetValue1 == scope.TargetValue1 &&
                tracker.TargetValue2 == scope.TargetValue2)
            {
                tracker.ReplacePublishedBuildingApproaches(scope.PublishedBuildingApproaches);
                scope.SynchronousTrackerUnitIds.Add(unitId);
                return tracker;
            }

            tracker = new AttackUnitTracker(
                scope.MapEpoch,
                unitId,
                scope.TribeId,
                scope.Command,
                scope.TargetValue1,
                scope.TargetValue2);
            tracker.ReplacePublishedBuildingApproaches(scope.PublishedBuildingApproaches);
            trackedAttackUnits[unitId] = tracker;
            scope.SynchronousTrackerUnitIds.Add(unitId);
            return tracker;
        }

        internal void RemoveSynchronousAttackTrackers(AttackCommandScope scope, string reason)
        {
            foreach (int unitId in scope.SynchronousTrackerUnitIds)
            {
                if (trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker))
                    EndTrackedAttack(unitId, tracker, reason);
            }
        }

        internal void ObserveTrackedAttackStates(int tick)
        {
            if (disposed)
                return;

            ClearUnitMoveFrames();

            // The Script Extender raises no Post event when another subscriber handles a
            // command through SkipOriginalFunction (the integrated Shift queue deliberately does this). These
            // scopes are synchronous by contract and must never leak into the next game tick.
            if (activeMoveCommand != null)
            {
                try
                {
                    LogDetailedInfo(
                        "Bugfixes and QoL friendly-moat-movement " +
                        $"stage=move-command-incomplete commandSeq={activeMoveCommand.Sequence} " +
                        $"tribe={activeMoveCommand.TribeId} " +
                        $"target=({activeMoveCommand.TargetX},{activeMoveCommand.TargetY}) " +
                        "reason=no-post-event-before-next-tick.");
                }
                catch
                {
                    // Cleanup remains mandatory even if diagnostics fail.
                }
                activeMoveCommand = null;
                activePlan = null;
                pendingPlan = null;
            }
            if (activeAttackCommand != null)
            {
                try
                {
                    LogDetailedInfo(
                        "Bugfixes and QoL friendly-moat-movement " +
                        $"stage=target-command-incomplete commandSeq={activeAttackCommand.Sequence} " +
                        $"tribe={activeAttackCommand.TribeId} command={activeAttackCommand.Command} " +
                        $"target={activeAttackCommand.TargetValue1}/" +
                        $"{activeAttackCommand.TargetValue2} " +
                        "reason=no-post-event-before-next-tick.");
                }
                catch
                {
                    // Cleanup remains mandatory even if diagnostics fail.
                }
                activeAttackCommand = null;
                if (pendingPlan != null && pendingPlan.AttackMovementQualified)
                    pendingPlan = null;
            }
            ClearIncompleteDirectFillScopeAtTick();

            // A work-target handoff is strictly synchronous (0x6AF60 -> 0x196280). If the
            // expected planner/builder was never entered, do not let it affect a later tick.
            if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                pendingPlan = null;

            ObserveNativeWaypointQueues(tick);

            if (trackedAttackUnits.Count != 0)
            {
                try
                {
                    List<int> unitIds = new List<int>(trackedAttackUnits.Keys);
                    foreach (int unitId in unitIds)
                    {
                        if (!trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker))
                            continue;
                        if (tracker.MapEpoch != mapEpoch)
                        {
                            EndTrackedAttack(unitId, tracker, "map-changed");
                            continue;
                        }
                        if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                            unit == null || !APIShared.UnitAccess.IsReallyAlive(unit))
                        {
                            EndTrackedAttack(unitId, tracker, "unit-dead-or-invalid");
                            continue;
                        }

                        TribeAICommand currentCommand =
                            (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
                        if (unit->r_TribeId != tracker.TribeId)
                        {
                            EndTrackedAttack(unitId, tracker, "command-ended-or-replaced");
                            continue;
                        }
                        if (!MatchesTrackedAttackTargetContext(unit, tracker, currentCommand))
                        {
                            EndTrackedAttack(unitId, tracker, "target-changed");
                            continue;
                        }

                        if (tracker.BuilderObserved && tracker.LastPlannerTargetX >= 0 &&
                            unit->r_CurrentTilePositionX == tracker.LastPlannerTargetX &&
                            unit->r_CurrentTilePositionY == tracker.LastPlannerTargetY)
                        {
                            EndTrackedAttack(unitId, tracker, "approach-reached");
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogFailure("attack-state-tick", ex);
                    }
                    catch
                    {
                        // Read-only attack diagnostics must never escape into the simulation tick.
                    }
                }
            }

            ObserveTrackedMoatMoveStates(tick);
        }

        internal void MarkTrackedAttackPipeline(
            int unitId,
            AttackPipelineStage stage,
            int targetX,
            int targetY,
            bool vanillaModeDetected)
        {
            try
            {
                if (!trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker))
                    return;

                switch (stage)
                {
                    case AttackPipelineStage.Mode:
                        tracker.ModeObserved = true;
                        tracker.VanillaModeDetected |= vanillaModeDetected;
                        break;
                    case AttackPipelineStage.Planner:
                        tracker.PlannerObserved = true;
                        tracker.LastPlannerTargetX = targetX;
                        tracker.LastPlannerTargetY = targetY;
                        break;
                    case AttackPipelineStage.Builder:
                        tracker.BuilderObserved = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    LogFailure("attack-pipeline-marker", ex);
                }
                catch
                {
                    // A diagnostic marker must never escape across a native callback.
                }
            }
        }

        internal void RemoveTrackedAttacksForTribe(int tribeId, string reason)
        {
            if (trackedAttackUnits.Count == 0)
                return;

            List<int> unitIds = new List<int>(trackedAttackUnits.Keys);
            foreach (int unitId in unitIds)
            {
                if (trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker) &&
                    tracker.TribeId == tribeId)
                {
                    EndTrackedAttack(unitId, tracker, reason);
                }
            }
        }

        internal void EndTrackedAttack(int unitId, AttackUnitTracker tracker, string reason)
        {
            trackedAttackUnits.Remove(unitId);
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-attack-state-end unit={unitId} command={tracker.Command} " +
                $"reason={reason} mode={tracker.ModeObserved} planner={tracker.PlannerObserved} " +
                $"builder={tracker.BuilderObserved}.");
        }

        internal bool MatchesAttackTargetContext(
            GameUnit* unit,
            TribeAICommand command,
            int targetValue1,
            int targetValue2)
        {
            if (command == TribeAICommand.AttackUnit)
            {
                return unit->r_AI_ContextTargetUnitId == targetValue1 &&
                    unit->r_AI_ContextTargetUnitGlobalId == unchecked((uint)targetValue2);
            }

            if (!IsBuildingAttackCommand(command) ||
                unit->r_AI_ContextTargetBuildingTileId > (uint)int.MaxValue)
            {
                return false;
            }

            int footprintTileId = (int)unit->r_AI_ContextTargetBuildingTileId;
            return TryValidateHostileBuildingTarget(
                    targetValue1,
                    unchecked((uint)targetValue2),
                    unit->r_ControllableForPlayerId,
                    out GameBuilding* building) &&
                IsExactBuildingContextTile(targetValue1, building, footprintTileId);
        }

        internal bool MatchesCompletedAttackTargetContext(
            GameUnit* unit, AttackCommandScope scope)
        {
            if (unit == null || scope == null)
                return false;
            if (scope.Command == TribeAICommand.AttackUnit)
            {
                return (TribeAICommand)unit->r_AI_LastIssuedTribeCommand == scope.Command &&
                    MatchesAttackTargetContext(
                        unit, scope.Command, scope.TargetValue1, scope.TargetValue2);
            }

            if (!IsBuildingAttackCommand(scope.Command) ||
                !MatchesAttackTargetContext(
                    unit, scope.Command, scope.TargetValue1, scope.TargetValue2))
            {
                return false;
            }

            return TryGetUnitAttackMoveTile(unit, out int approachTileId) &&
                HasPublishedBuildingApproachPair(
                    scope.PublishedBuildingApproaches,
                    approachTileId,
                    unchecked((int)unit->r_AI_ContextTargetBuildingTileId));
        }

        internal bool MatchesTrackedAttackTargetContext(
            GameUnit* unit, AttackUnitTracker tracker, TribeAICommand currentCommand)
        {
            if (tracker.Command == TribeAICommand.AttackUnit)
            {
                return currentCommand == tracker.Command &&
                    MatchesAttackTargetContext(
                        unit, tracker.Command, tracker.TargetValue1, tracker.TargetValue2);
            }

            if (!IsBuildingAttackCommand(tracker.Command) ||
                !MatchesAttackTargetContext(
                    unit, tracker.Command, tracker.TargetValue1, tracker.TargetValue2) ||
                !TryGetUnitAttackMoveTile(unit, out int approachTileId))
            {
                return false;
            }

            return HasPublishedBuildingApproachPair(
                tracker.PublishedBuildingApproaches,
                approachTileId,
                unchecked((int)unit->r_AI_ContextTargetBuildingTileId));
        }

        internal static bool IsAttackCommand(TribeAICommand command) =>
            command == TribeAICommand.AttackUnit ||
            command == TribeAICommand.AttackBuilding ||
            command == TribeAICommand.ForceAttackBuilding;

        internal static bool IsBuildingAttackCommand(TribeAICommand command) =>
            command == TribeAICommand.AttackBuilding ||
            command == TribeAICommand.ForceAttackBuilding;

        internal enum AttackPipelineStage
        {
            Mode,
            Planner,
            Builder
        }

        internal enum AttackApproachKind
        {
            UnitFlood,
            BuildingApproach,
            BuildingCandidateConsumer
        }

        internal sealed class AttackCommandScope
        {
            internal const int MaximumDiagnosticDetailsPerStage = 3;
            internal readonly Dictionary<string, int> retainedDiagnosticsByStage =
                new Dictionary<string, int>(StringComparer.Ordinal);

            internal readonly MovementOptionsSnapshot Options;
            internal readonly RequiredRouteMetrics Required = new RequiredRouteMetrics();
            internal readonly RequiredRouteCache RequiredCache = new RequiredRouteCache();
            public AttackCommandScope(
                AttackCommandScope previous,
                int sequence,
                int mapEpoch,
                int tribeId,
                TribeAICommand command,
                int targetValue1,
                int targetValue2,
                MovementOptionsSnapshot currentOptions)
            {
                Previous = previous;
                Options = previous?.Options ?? currentOptions;
                Sequence = sequence;
                MapEpoch = mapEpoch;
                TribeId = tribeId;
                Command = command;
                TargetValue1 = targetValue1;
                TargetValue2 = targetValue2;
            }

            public AttackCommandScope Previous { get; }
            public int Sequence { get; }
            public int MapEpoch { get; }
            public int TribeId { get; }
            public TribeAICommand Command { get; }
            public int TargetValue1 { get; }
            public int TargetValue2 { get; }
            public int DetailLogs { get; set; }
            public long StartedTimestamp { get; } = Stopwatch.GetTimestamp();
            public HashSet<int> CandidateUnitIds { get; } = new HashSet<int>();
            public bool CandidatesCaptured { get; set; }
            public Dictionary<int, string> PreCandidateSignatures { get; } =
                new Dictionary<int, string>();
            public HashSet<int> SynchronousTrackerUnitIds { get; } = new HashSet<int>();
            public Dictionary<int, string> LastDecisionByUnit { get; } =
                new Dictionary<int, string>();
            public HashSet<string> AttackApproachDiagnosticSignatures { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public Dictionary<int, HashSet<int>> PublishedBuildingApproaches { get; } =
                new Dictionary<int, HashSet<int>>();
            public HashSet<LadderRegionTransition> PositiveLadderRegionTransitions { get; } =
                new HashSet<LadderRegionTransition>();
            public HashSet<int> PublishedUnitAttackApproaches { get; } = new HashSet<int>();
            public int WeightedDecisions { get; set; }
            public int WeightedPublished { get; set; }
            public double WeightedSearchMilliseconds { get; set; }
            public double WeightedMaximumSearchMilliseconds { get; set; }
            public HashSet<int> WeightedUnitIds { get; } = new HashSet<int>();
            public long DispatchElapsedTicks { get; set; }
            public long LogFlushTicks { get; set; }
            public long UnitFloodTicks { get; set; }
            public long QualificationTicks { get; set; }
            public long FloodQualificationTicks { get; set; }
            public long NativeBuilderTicks { get; set; }
            public long AuditTicks { get; set; }
            public long WeightedPhaseTicks { get; set; }
            public long WeightedAuditTicks { get; set; }
            public int UnitFloodCalls { get; set; }
            public int QualificationCalls { get; set; }
            public int NativeBuilderCalls { get; set; }
            public int AuditCalls { get; set; }
            public double ResidualMilliseconds { get; set; }
            public int DiagnosticMessages { get; internal set; }
            public int DiagnosticCharacters { get; internal set; }
            public List<string> Diagnostics { get; } = new List<string>();
            public Dictionary<string, int> SuppressedDiagnostics { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public double DispatchMilliseconds => TicksToMilliseconds(DispatchElapsedTicks);
            public double LogFlushMilliseconds => TicksToMilliseconds(LogFlushTicks);
            public double UnitFloodMilliseconds => TicksToMilliseconds(UnitFloodTicks);
            public double QualificationMilliseconds => TicksToMilliseconds(QualificationTicks);
            public double FloodQualificationMilliseconds => TicksToMilliseconds(FloodQualificationTicks);
            public double NativeBuilderMilliseconds => TicksToMilliseconds(NativeBuilderTicks);
            public double AuditMilliseconds => TicksToMilliseconds(AuditTicks);
            public double WeightedPhaseMilliseconds => TicksToMilliseconds(WeightedPhaseTicks);
            public double WeightedAuditMilliseconds => TicksToMilliseconds(WeightedAuditTicks);

            public void BufferDiagnostic(string message)
            {
                message = message ?? string.Empty;
                DiagnosticMessages++;
                DiagnosticCharacters += message.Length;
                string stage = GetDiagnosticStage(message);
                retainedDiagnosticsByStage.TryGetValue(stage, out int retained);
                if (retained < MaximumDiagnosticDetailsPerStage)
                {
                    retainedDiagnosticsByStage[stage] = retained + 1;
                    Diagnostics.Add(message);
                    return;
                }

                SuppressedDiagnostics.TryGetValue(stage, out int suppressed);
                SuppressedDiagnostics[stage] = suppressed + 1;
            }

            internal static string GetDiagnosticStage(string message)
            {
                const string marker = "stage=";
                int start = message.IndexOf(marker, StringComparison.Ordinal);
                if (start < 0)
                    return "other";
                start += marker.Length;
                int end = message.IndexOf(' ', start);
                return end < 0 ? message.Substring(start) : message.Substring(start, end - start);
            }

            internal static double TicksToMilliseconds(long ticks) =>
                ticks * 1000.0 / Stopwatch.Frequency;

            public bool Matches(TribeIssueOrderWithTargetEventArgs args, int currentMapEpoch) =>
                MapEpoch == currentMapEpoch && TribeId == args.TribeId &&
                Command == args.AICommand && TargetValue1 == args.TargetValue1 &&
                TargetValue2 == args.TargetValue2;
        }

        internal sealed class AttackUnitTracker
        {
            public AttackUnitTracker(
                int mapEpoch,
                int unitId,
                int tribeId,
                TribeAICommand command,
                int targetValue1,
                int targetValue2)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                TribeId = tribeId;
                Command = command;
                TargetValue1 = targetValue1;
                TargetValue2 = targetValue2;
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public int TribeId { get; }
            public TribeAICommand Command { get; }
            public int TargetValue1 { get; }
            public int TargetValue2 { get; }
            public bool ModeObserved { get; set; }
            public bool PlannerObserved { get; set; }
            public bool BuilderObserved { get; set; }
            public bool VanillaModeDetected { get; set; }
            public int LastPlannerTargetX { get; set; } = -1;
            public int LastPlannerTargetY { get; set; } = -1;
            public Dictionary<int, HashSet<int>> PublishedBuildingApproaches { get; } =
                new Dictionary<int, HashSet<int>>();

            public void ReplacePublishedBuildingApproaches(
                Dictionary<int, HashSet<int>> approaches)
            {
                PublishedBuildingApproaches.Clear();
                if (approaches == null)
                    return;
                foreach (KeyValuePair<int, HashSet<int>> pair in approaches)
                    PublishedBuildingApproaches[pair.Key] = new HashSet<int>(pair.Value);
            }
        }

    }
}
