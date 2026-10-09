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
        internal bool TryQualifySelectedGroupCursorRoute(
            AttackCursorPairScope template,
            out AttackCursorPairScope boundScope,
            out CursorGroupRouteSummary group)
        {
            boundScope = null; group = default;
            if (!ExtensionsEnabled || template == null || !TryCaptureSelectedGroup(template.PlayerId, out int[] ids, out string token)) return false;
            group.SelectionSignature = token;
            if (ManualCommandsEnabled && TryQualifyNativeSelection(template, ids, token, out boundScope, out group) && group.AllowFallback)
                return true;
            if (!TraversalEnabled) return group.SelectedUnits > 0;
            group = default; group.SelectionSignature = token;
            EnsureCursorTopology(template.PlayerId, false);
            cursorSources.Clear(); cursorSourceCounts.Clear();
            foreach (int id in ids)
            {
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null ||
                    !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_ControllableForPlayerId != template.PlayerId) continue;
                group.SelectedUnits++;
                bool canDig = CanDigMoat(unit);
                if (canDig) group.DiggerUnits++;
                int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y), node = CursorNode(template.PlayerId, tile);
                if (node < 0) continue;
                int key = node * 2 + (canDig ? 1 : 0);
                if (cursorSources.ContainsKey(key)) { cursorSourceCounts[key]++; continue; }
                cursorSources.Add(key, new SelectedCursorUnitSnapshot(id, x, y, tile, canDig));
                cursorSourceCounts[key] = 1;
            }
            AttackCursorPairScope probe = null;
            foreach (var entry in cursorSources)
            {
                var member = entry.Value;
                if (probe == null) probe = CreateCursorScopeForSnapshot(template, member);
                else probe.SetSource(member);
                if (!TryQualifyCursorScope(probe, out _, out _, out RouteProbeSummary summary)) continue;
                if (!member.CanDig && !summary.ReachedWithoutMoat) continue;
                group.ObservedRoute.MergeObservations(summary);
                group.LegallyReachableUnits += cursorSourceCounts[entry.Key];
                if (summary.ReachedWithMoat && !summary.ReachedWithoutMoat) group.FriendlyMoatSeparatedUnits += cursorSourceCounts[entry.Key];
                if (boundScope != null) continue;
                boundScope = CreateCursorScopeForSnapshot(template, member);
                group.RepresentativeUnitId = member.UnitId; group.RepresentativeStartX = member.StartX;
                group.RepresentativeStartY = member.StartY; group.RepresentativeStartTileId = member.StartTileId;
                group.RepresentativeCanDig = member.CanDig;
            }
            group.AllowFallback = boundScope != null;
            if (boundScope != null) { boundScope.GroupCursorAuthorized = true; boundScope.GroupSelectionSignature = token; }
            return group.SelectedUnits > 0;
        }

        internal static AttackCursorPairScope CreateCursorScopeForSnapshot(
            AttackCursorPairScope template, SelectedCursorUnitSnapshot unit)
        {
            AttackCursorPairScope scope = new AttackCursorPairScope(
                template.MapEpoch,
                unit.UnitId,
                template.PlayerId,
                unit.StartX,
                unit.StartY,
                unit.StartTileId,
                template.TargetX,
                template.TargetY,
                template.TargetTileId,
                template.FallbackKind,
                template.BuildingId,
                template.BuildingGlobalId,
                template.BuildingOwnerId,
                template.BuildingType,
                template.HoverBuildingTileId);
            scope.TargetUnitId = template.TargetUnitId;
            scope.TargetUnitGlobalId = template.TargetUnitGlobalId;
            scope.CursorPairTargetTileId = template.CursorPairTargetTileId;
            return scope;
        }

        internal bool TryCaptureSelectedGroup(
            int playerId, out int[] selectedUnitIds, out string signature)
        { return CaptureCursorSelection(playerId, out selectedUnitIds, out signature); }

        internal bool TryProbeDirectCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out RouteProbeSummary summary)
        {
            normalReachable = false; friendlyMoatSeparated = false; summary = default;
            if (scope == null || (uint)scope.TargetX >= MapWidth || (uint)scope.TargetY >= MapWidth ||
                !IsValidTileId(scope.TargetTileId) || movementTargetAvailability[scope.TargetY * MapWidth + scope.TargetX] == 0 ||
                (tileFlags[scope.TargetTileId] & MovementBlockedLowTileFlagMask) != 0)
            { RecordCursorDecision("invalid-move-target", scope); return false; }
            if (!ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, scope.TargetTileId, out summary)) return false;
            normalReachable = summary.ReachedWithoutMoat;
            friendlyMoatSeparated = summary.ReachedWithMoat && !normalReachable;
            return true;
        }

        internal bool TryProbeUnitApproachCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out RouteProbeSummary summary)
        {
            normalReachable = false; friendlyMoatSeparated = false; summary = default;
            if (scope == null || !CursorScopeMatchesTargetTile(scope, scope.TargetTileId))
            { RecordCursorDecision("invalid-attack-target", scope); return false; }
            // 8C5F0 passes the actual target unit tile to E2CA0. Reachability is not
            // a melee approach search; weapon and command eligibility stay native.
            if (!ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, scope.TargetTileId, out summary)) return false;
            normalReachable = summary.ReachedWithoutMoat;
            friendlyMoatSeparated = summary.ReachedWithMoat;
            return true;
        }

        internal bool TryQualifyCursorScope(
            AttackCursorPairScope scope,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
            approachX = -1; approachY = -1; summary = default;
            if (!ExtensionsEnabled || scope == null) return false;
            bool normal, friendly;
            if (scope.FallbackKind == CursorPairFallbackKind.DirectTile)
            {
                approachX = scope.TargetX; approachY = scope.TargetY;
                return TryProbeDirectCursorRoute(scope, out normal, out friendly, out summary) && (normal || friendly);
            }
            if (scope.FallbackKind == CursorPairFallbackKind.UnitApproach)
                return TryProbeUnitApproachCursorRoute(scope, out normal, out friendly, out summary) && (normal || friendly);
            return TryProbeBuildingApproachCursorRoute(scope, out normal, out friendly, out approachX, out approachY, out summary) && (normal || friendly);
        }

        internal bool CursorScopeMatchesTargetTile(AttackCursorPairScope scope, int targetTileId)
        {
            if (scope.FallbackKind == CursorPairFallbackKind.UnitApproach)
            {
                return (scope.CursorPairTargetTileId == targetTileId ||
                        scope.TargetTileId == targetTileId) &&
                    scope.TargetUnitId > 0 &&
                    TryGetHostileLivingUnitAtTile(
                        scope.PlayerId,
                        scope.TargetX,
                        scope.TargetY,
                        scope.TargetUnitId,
                        -1,
                        out _,
                        out _) &&
                    APIShared.UnitAccess.TryGetById(
                        scope.TargetUnitId, out GameUnit* targetUnit, out _) &&
                    targetUnit != null && targetUnit->r_GlobalId == scope.TargetUnitGlobalId &&
                    targetUnit->r_CurrentTilePositionX == scope.TargetX && targetUnit->r_CurrentTilePositionY == scope.TargetY;
            }
            return scope.TargetTileId == targetTileId;
        }

        internal bool CursorStartMatchesBoundSelection(
            AttackCursorPairScope scope, int actualSelectedUnitTileId)
        {
            if (scope.GroupCursorAuthorized)
            {
                return !string.IsNullOrEmpty(scope.GroupSelectionSignature) &&
                    TryCaptureSelectedGroup(
                        scope.PlayerId, out _, out string currentSelectionSignature) &&
                    string.Equals(
                        scope.GroupSelectionSignature,
                        currentSelectionSignature,
                        StringComparison.Ordinal);
            }

            if (scope.StartTileId == actualSelectedUnitTileId)
                return true;

            // E2CA0 can receive another representative member of a mixed selection.
            // The cursor is group-wide, while the later movement hooks filter every unit.
            return TryGetSelectedVanillaDigger(
                       scope.UnitId, scope.PlayerId, out int selectedDiggerId, out GameUnit* selectedDigger) &&
                   selectedDiggerId == scope.UnitId && selectedDigger != null &&
                   GameTileManagerAPI.Instance.GetTileId(
                       selectedDigger->r_CurrentTilePositionX,
                       selectedDigger->r_CurrentTilePositionY) == scope.StartTileId;
        }

        internal bool TryGetHostileLivingUnitAtTile(
            int playerId,
            int targetX,
            int targetY,
            int requiredUnitId,
            int requiredGlobalId,
            out int targetUnitId,
            out bool occupiedByLivingUnit)
        {
            targetUnitId = -1;
            occupiedByLivingUnit = false;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || targetX < 0 || targetX >= MapWidth ||
                targetY < 0 || targetY >= MapWidth)
            {
                return false;
            }

            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int unitId = requiredUnitId > 0 ? requiredUnitId : 1; unitId <= (requiredUnitId > 0 ? requiredUnitId : units.Length); unitId++)
            {
                if (requiredUnitId > 0 && unitId != requiredUnitId)
                    continue;
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* target, out _) ||
                    target == null || !APIShared.UnitAccess.IsReallyAlive(target) ||
                    target->r_CurrentTilePositionX != targetX ||
                    target->r_CurrentTilePositionY != targetY)
                {
                    continue;
                }
                if (requiredGlobalId >= 0 &&
                    target->r_GlobalId != unchecked((uint)requiredGlobalId))
                {
                    continue;
                }

                occupiedByLivingUnit = true;

                int targetPlayerId = target->r_ControllableForPlayerId;
                if (!playerApi.IsPlayerIdValid(targetPlayerId) || targetPlayerId == playerId ||
                    playerApi.IsPlayerAlliedTo(playerId, targetPlayerId))
                {
                    continue;
                }

                targetUnitId = unitId;
                return true;
            }

            return false;
        }

        internal readonly struct SelectedCursorUnitSnapshot
        {
            public SelectedCursorUnitSnapshot(
                int unitId, int startX, int startY, int startTileId, bool canDig)
            {
                UnitId = unitId;
                StartX = startX;
                StartY = startY;
                StartTileId = startTileId;
                CanDig = canDig;
            }

            public int UnitId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int StartTileId { get; }
            public bool CanDig { get; }
        }

    }
}
