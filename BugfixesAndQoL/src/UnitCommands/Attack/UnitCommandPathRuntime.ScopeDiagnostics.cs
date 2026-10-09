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
        internal void LogSynchronousAttackCandidate(
            AttackCommandScope scope, int unitId, GameUnit* unit)
        {
            if (scope.DetailLogs >= 24) return;
            scope.DetailLogs++;
            string signature = $"sync:{scope.MapEpoch}:{scope.TribeId}:{scope.Command}:" +
                $"{scope.TargetValue1}:{scope.TargetValue2}:{GetAttackCandidateSignature(unit)}";
            if (lastAttackCommandCandidates.TryGetValue(unitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastAttackCommandCandidates[unitId] = signature;
            LogCommandDiagnostic(
                $"stage=attack-command-candidate phase=sync unit={unitId} " +
                $"type={unit->r_UnitChimp} global={unit->r_GlobalId} player={unit->r_ControllableForPlayerId} " +
                $"tribe={unit->r_TribeId}/{scope.TribeId} aiState={unit->r_AIState} " +
                $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope.Command} " +
                $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                $"contextUnit={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId} " +
                $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY})");
        }

        internal void LogAttackScopeDecision(
            string stage,
            int unitId,
            GameUnit* unit,
            int vanillaResult,
            string reason,
            RouteProbeSummary summary)
        {
            AttackCommandScope scope = activeAttackCommand;
            string signature =
                $"{stage}:{mapEpoch}:{scope?.TribeId}:{scope?.Command}:{scope?.TargetValue1}:" +
                $"{scope?.TargetValue2}:{unit->r_AIState}:{unit->r_AttackMoveToTargetTileX}:" +
                $"{unit->r_AttackMoveToTargetTileY}:{reason}:{summary.RouteFound}";
            if (scope != null && scope.LastDecisionByUnit.TryGetValue(unitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            if (scope != null)
                scope.LastDecisionByUnit[unitId] = signature;

            string buildingPair = string.Empty;
            if (scope != null && IsBuildingAttackCommand(scope.Command) &&
                TryGetUnitAttackMoveTile(unit, out int approachTileId) &&
                TryGetPublishedBuildingFootprint(
                    scope.PublishedBuildingApproaches,
                    approachTileId,
                    out int footprintTileId))
            {
                buildingPair = $" buildingPair={approachTileId}->{footprintTileId}";
            }

            LogCommandDiagnostic(
                $"stage={stage} unit={unitId} type={unit->r_UnitChimp} " +
                $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId}/{scope?.TribeId} " +
                $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope?.Command} " +
                $"target={scope?.TargetValue1}/{scope?.TargetValue2} " +
                $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}) " +
                $"vanillaMode={vanillaResult} reason={reason}{buildingPair} " +
                summary.ToLogFields());
        }

    }
}
