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
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.Tracking.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void StartOrRefreshMoatMoveTracker(
            PlanScope plan, RouteProbeSummary summary, int builderResult)
        {
#if SERP_DEVDIAG
            try
            {
                if (!APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit))
                {
                    return;
                }

                if (runtime.trackedMoatMoves.TryGetValue(
                        plan.UnitId, out MoatMoveTracker existing) &&
                    existing.MapEpoch == runtime.mapEpoch &&
                    existing.TargetX == plan.TargetX &&
                    existing.TargetY == plan.TargetY)
                {
                    existing.TribeId = unit->r_TribeId;
                    if (plan.MoatWorkTargetTileId > 0)
                        existing.WorkTargetMoatTileId = plan.MoatWorkTargetTileId;
                    return;
                }

                bool requiredOnly = activeMoveCommand?.Options.RequiredOnly ??
                    activeAttackCommand?.Options.RequiredOnly ?? runtime.CurrentOptions.RequiredOnly;
                bool explicitCommand = activeMoveCommand != null || activeAttackCommand != null;
                runtime.requiredBackgroundTrackedUnitIds.Remove(plan.UnitId);
                runtime.trackedMoatMoves.Remove(plan.UnitId);
                RequiredRouteMetrics requiredMetrics = requiredOnly
                    ? activeMoveCommand?.Required ?? activeAttackCommand?.Required
                    : null;
                if (requiredOnly)
                {
                    bool selected = explicitCommand
                        ? requiredMetrics?.TryTrackUnit(plan.UnitId) ?? false
                        : runtime.requiredBackgroundTrackedUnitIds.Contains(plan.UnitId) ||
                          runtime.requiredBackgroundTrackedUnitIds.Count < 8;
                    if (!selected)
                        return;
                    if (!explicitCommand) runtime.requiredBackgroundTrackedUnitIds.Add(plan.UnitId);
                }
                runtime.ResolveCommandDiagnosticContext(
                    plan.UnitId, unit, out TribeAICommand command, out string commandContext,
                    out int commandSequence);
                var tracker = new MoatMoveTracker(
                    runtime.mapEpoch,
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
                    runtime.IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)),
                    ReadUnitMoatPathConsumptionMode(unit),
                    CaptureCurrentGameTick());
                tracker.WeightedCommand = command;
                tracker.WeightedCommandContext = commandContext;
                tracker.WeightedCommandSequence = commandSequence;
                tracker.RequiredOnlyAtPublication = requiredOnly;
                tracker.WorkTargetMoatTileId = plan.MoatWorkTargetTileId;
                runtime.trackedMoatMoves[plan.UnitId] = tracker;
                runtime.LogCommandDiagnostic(
                    $"stage=move-track-start unit={plan.UnitId} type={unit->r_UnitChimp} " +
                    $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId} " +
                    $"target=({plan.TargetX},{plan.TargetY}) command={command} " +
                    $"commandContext={commandContext} canDig={runtime.CanDigMoat(unit)} " +
                    $"builderResult={builderResult} " +
                    summary.ToLogFields());
            }
            catch (Exception ex)
            {
                runtime.TryLogDiagnosticFailure("move-track-start", ex);
            }
#endif
        }
    }
}
