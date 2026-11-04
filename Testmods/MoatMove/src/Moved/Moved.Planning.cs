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
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.Planning.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal bool TryQualifyAttackMovementPlan(
            int unitId,
            GameUnit* unit,
            int vanillaResult,
            out PlanScope plan,
            out RouteProbeSummary summary,
            out string rejectionReason)
        {
            AttackCommandScope command = activeAttackCommand;
            long started = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
            attackQualificationTimingDepth++;
            try
            {
                return TryQualifyAttackMovementPlanCore(
                    unitId, unit, vanillaResult, out plan, out summary, out rejectionReason);
            }
            finally
            {
                attackQualificationTimingDepth--;
                if (command != null)
                {
                    long elapsed = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - started;
                    command.QualificationCalls++;
                    command.QualificationTicks += elapsed;
                    if (activeAttackApproachDiagnostic?.Kind == AttackApproachKind.UnitFlood)
                        command.FloodQualificationTicks += elapsed;
                }
            }
        }

        internal bool TryQualifyAttackMovementPlanCore(
            int unitId,
            GameUnit* unit,
            int vanillaResult,
            out PlanScope plan,
            out RouteProbeSummary summary,
            out string rejectionReason)
        {
            plan = null;
            summary = default;
            rejectionReason = "no-active-attack-command";
            AttackCommandScope scope = activeAttackCommand;
            if (scope == null || scope.MapEpoch != runtime.mapEpoch || !IsAttackCommand(scope.Command))
                return false;
            runtime.EnsureAttackCommandCandidates(scope);
            if (!runtime.CanDigMoat(unit))
            {
                rejectionReason = "unit-cannot-dig-moat";
                return false;
            }
            if (!scope.CandidateUnitIds.Contains(unitId))
            {
                rejectionReason = "unit-not-command-candidate";
                return false;
            }
            try
            {
                LogSynchronousAttackCandidate(scope, unitId, unit);
            }
            catch
            {
                // Candidate diagnostics must not reject an otherwise valid attack scope.
            }
            if (!APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_TribeId != scope.TribeId)
            {
                rejectionReason = "unit-or-tribe-mismatch";
                return false;
            }
            if (!MatchesSynchronousAttackMovementContext(unit, scope, out string contextReason))
            {
                rejectionReason = contextReason;
                return false;
            }

            int targetX = unit->r_AttackMoveToTargetTileX;
            int targetY = unit->r_AttackMoveToTargetTileY;
            if (targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth)
            {
                rejectionReason = "invalid-attack-move-target";
                return false;
            }

            plan = new PlanScope(unitId, targetX, targetY)
            {
                PlayerId = unit->r_ControllableForPlayerId,
                AttackMovementQualified = true
            };
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            bool nativeUnitApproach = scope.Command == TribeAICommand.AttackUnit &&
                scope.PublishedUnitAttackApproaches.Contains(targetTileId);
            bool routeQualified = nativeUnitApproach
                ? TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
                    plan,
                    targetTileId,
                    false,
                    out summary,
                    out _)
                : runtime.TryFindRequiredFriendlyCompletedMoatRouteForPlan(plan, out summary);
            if (!routeQualified)
            {
                plan = null;
                rejectionReason = nativeUnitApproach
                    ? "native-unit-approach-owner-route-rejected"
                    : "no-required-friendly-moat-route";
                return false;
            }

            plan.FriendlyRouteQualified = true;
            rejectionReason = nativeUnitApproach
                ? "native-unit-approach-endpoint"
                : "friendly-moat-required";
#if SERP_DEVDIAG
            runtime.GetOrCreateAttackTracker(scope, unitId);
#endif

            return true;
        }

        internal bool MatchesSynchronousAttackMovementContext(
            GameUnit* unit, AttackCommandScope scope, out string rejectionReason)
        {
            rejectionReason = "command-or-target-context-mismatch";
            if (scope.Command == TribeAICommand.AttackUnit)
            {
                return (TribeAICommand)unit->r_AI_LastIssuedTribeCommand == scope.Command &&
                    runtime.MatchesAttackTargetContext(
                        unit, scope.Command, scope.TargetValue1, scope.TargetValue2);
            }

            if (!IsBuildingAttackCommand(scope.Command))
                return false;
            if (!runtime.TryValidateHostileBuildingTarget(
                    scope.TargetValue1,
                    unchecked((uint)scope.TargetValue2),
                    unit->r_ControllableForPlayerId,
                    out GameBuilding* building))
            {
                rejectionReason = "building-target-context-mismatch";
                return false;
            }
            if (!TryGetUnitAttackMoveTile(unit, out int approachTileId) ||
                !TryGetPublishedBuildingFootprint(
                    scope.PublishedBuildingApproaches,
                    approachTileId,
                    out int footprintTileId) ||
                !(footprintTileId == 0
                    ? runtime.IsLegalBuildingCandidate(scope, building, unit->r_ControllableForPlayerId,
                        new BuildingApproachCandidate(approachTileId, 0, 0))
                    : runtime.IsValidBuildingApproachPair(scope.TargetValue1, building, approachTileId, footprintTileId)))
            {
                rejectionReason = "building-approach-context-mismatch";
                return false;
            }

            return true;
        }
    }
}
