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
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.Diagnostics.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
#if SERP_DEVDIAG
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
#endif

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogModeContext(PlanScope plan, GameUnit* unit, int vanillaResult)
        {
#if SERP_DEVDIAG
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
#endif
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal static void RecordBuilderResult(MoveCommandScope command, int result)
        {
#if SERP_DEVDIAG

            if (command == null)
                return;

            command.LastBuilderResult = result;
            if (result > 0)
                command.PositiveBuilderCalls++;
#endif
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal static void RecordVanillaBuilderResult(
            MoveCommandScope command, int result)
        {
#if SERP_DEVDIAG

            if (command == null)
                return;

            command.VanillaBuilderCalls++;
            command.LastVanillaBuilderResult = result;
#endif
        }
    }
}
