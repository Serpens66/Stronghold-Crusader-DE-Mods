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
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.RegionEligibility.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override int SelectOwnerSafeGroupMoatModeTraversal(
            IntPtr tribeManager, int tribeId, int vanillaResult, MoveCommandScope command)
        {
            if (command == null || command.TribeId != tribeId ||
                tribeManager == IntPtr.Zero || tribeManager != runtime.nativeTribeManager ||
                tribeId < 0 || tribeId >= MaximumTribeCount || runtime.getGroupUnitId == null)
            {
                return vanillaResult;
            }

            try
            {
                byte* tribeRecord = (byte*)tribeManager.ToPointer() +
                    (tribeId * TribeRecordSize);
                int leadUnitId = *(short*)(tribeRecord + TribeLeadUnitIdOffset);
                int unitCount = *(short*)(tribeRecord + TribeUnitCountOffset);
                if (leadUnitId <= 0 || unitCount <= 0 || unitCount > MaximumUnitCount)
                    return vanillaResult;

                // This helper returns a unit ID, not a boolean success result. Fast may
                // bypass the group scan only when Vanilla already selected the leader;
                // a later moat member would otherwise leave 11B520 on its ground branch.
                if (vanillaResult == leadUnitId)
                {
                    fastVanillaBypasses++;
                    return vanillaResult;
                }

                runtime.EnsureMoveCommandGroupSummary(command);

                // A ground leader and a later moat member still need qualification.
                // Returning here used to strand the whole tribe on an isolated PCL.
                command.MoatRelevant |= vanillaResult > 0;

                int activeUnitsOnMoat = 0;
                int activeUnitsOffMoat = 0;
                int diggerUnits = 0;
                int qualifyingDiggerUnitsOnMoat = 0;
                int qualifyingUnitId = 0;
                bool leadUnitObserved = false;
                RouteProbeSummary qualifyingRoute = default;
                var probedDiggerStarts = new HashSet<int>();
                for (int ordinal = 0; ordinal < unitCount; ordinal++)
                {
                    int unitId = runtime.getGroupUnitId(tribeManager, tribeId, ordinal);
                    if (unitId <= 0 ||
                        !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                        unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                        *(ushort*)((byte*)unit + UnitGroupInactiveStateOffset) != 0)
                    {
                        continue;
                    }

                    int startX = unit->r_CurrentTilePositionX;
                    int startY = unit->r_CurrentTilePositionY;
                    if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                        continue;
                    int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                    if (!IsValidTileId(startTileId))
                        continue;
                    if (unitId == leadUnitId)
                        leadUnitObserved = true;

                    bool onCompletedMoat =
                        (runtime.tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
                    if (onCompletedMoat)
                        activeUnitsOnMoat++;
                    else
                        activeUnitsOffMoat++;

                    if (!runtime.CanDigMoat(unit))
                        continue;
                    diggerUnits++;
                    // Qualify a required crossing before choosing a native group branch;
                    // both island starters and actual moat starters can supply evidence.
                    if (qualifyingUnitId != 0)
                        continue;
                    int startRegion = runtime.pathRegionGrid[startTileId];
                    int startKey = startRegion > 0 ? startRegion : -startTileId - 1;
                    if (!probedDiggerStarts.Add(startKey))
                        continue;

                    PlanScope probe = new PlanScope(unitId, command.TargetX, command.TargetY)
                    {
                        PlayerId = unit->r_ControllableForPlayerId,
                        VanillaFailureProven = vanillaResult == 0
                    };
                    if (!runtime.TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                            probe, out RouteProbeSummary route))
                    {
                        continue;
                    }

                    if (onCompletedMoat)
                        qualifyingDiggerUnitsOnMoat++;
                    qualifyingUnitId = unitId;
                    qualifyingRoute = route;
                }

                bool mixedPositions = activeUnitsOnMoat > 0 && activeUnitsOffMoat > 0;
                int targetTileId = command.TargetX >= 0 && command.TargetX < MapWidth &&
                    command.TargetY >= 0 && command.TargetY < MapWidth
                        ? GameTileManagerAPI.Instance.GetTileId(
                            command.TargetX, command.TargetY)
                        : 0;
                int targetRegion = IsValidTileId(targetTileId)
                    ? runtime.pathRegionGrid[targetTileId]
                    : 0;
                bool targetIsFriendlyCompletedMoat = IsValidTileId(targetTileId) &&
                    runtime.IsCompletedMoatTile(targetTileId) && qualifyingUnitId > 0 &&
                    APIShared.UnitAccess.TryGetById(
                        qualifyingUnitId, out GameUnit* qualifyingUnit, out _) &&
                    qualifyingUnit != null &&
                    runtime.ResolveCompletedMoatRelationship(
                        qualifyingUnit->r_ControllableForPlayerId, targetTileId) ==
                        CompletedMoatRelationship.Friendly;
                bool forceSharedMoatMode = vanillaResult != leadUnitId && leadUnitObserved &&
                    ((targetRegion > 0 && targetRegion <= MaximumRegionId) ||
                     targetIsFriendlyCompletedMoat) &&
                    qualifyingUnitId > 0;
                bool normalize = vanillaResult > 0 && mixedPositions &&
                    qualifyingUnitId > 0;
                if (vanillaResult > 0 || forceSharedMoatMode)
                {
                    command.MoatRelevant = true;
                    MarkCommandMoatRelevant(command, qualifyingRoute);
                }
                int effectiveResult = forceSharedMoatMode
                    ? leadUnitId
                    : normalize ? 0 : vanillaResult;
                string decision = forceSharedMoatMode
                    ? "forced=shared-friendly-moat"
                    : normalize ? "normalized=ground-per-unit" : "vanilla";
                string diagnostic =
                    $"stage=group-moat-mode tribe={tribeId} target=({command.TargetX},{command.TargetY}) " +
                    $"targetRegion={targetRegion} " +
                    $"lead={leadUnitId} vanillaFirstMoat={vanillaResult} onMoat={activeUnitsOnMoat} " +
                    $"offMoat={activeUnitsOffMoat} diggers={diggerUnits} " +
                    $"qualifyingDiggersOnMoat={qualifyingDiggerUnitsOnMoat} " +
                    $"qualifyingUnit={qualifyingUnitId} " +
                    $"effective={effectiveResult} decision={decision}";
                if (!string.Equals(
                        command.LastGroupMoatModeDiagnostic, diagnostic, StringComparison.Ordinal))
                {
                    command.LastGroupMoatModeDiagnostic = diagnostic;
                    runtime.LogCommandDiagnostic(diagnostic);
                }

                // A ground leader with a later moat member needs the shared moat branch.
                // A moat leader in a mixed group uses Vanilla's per-unit common-target
                // branch. Never synthesize E2610: DF720 consumes its real portal state.
                // The later per-unit mode and builder hooks retain the capability/owner filter.
                return effectiveResult;
            }
            catch (Exception ex)
            {
                runtime.LogFailure("group-moat-mode", ex);
                return vanillaResult;
            }
        }
    }
}
