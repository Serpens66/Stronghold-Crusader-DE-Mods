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
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.Selection.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override bool TryQualifySelectedGroupCursorRouteTraversal(
            AttackCursorPairScope template, int[] ids, string token,
            ref AttackCursorPairScope boundScope, out CursorGroupRouteSummary group)
        {
            group = default; group.SelectionSignature = token;
            EnsureCursorTopology(template.PlayerId, false);
            cursorSources.Clear(); cursorSourceCounts.Clear();
            foreach (int id in ids)
            {
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null ||
                    !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_ControllableForPlayerId != template.PlayerId) continue;
                group.SelectedUnits++;
                bool canDig = runtime.CanDigMoat(unit);
                if (canDig) group.DiggerUnits++;
                int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y), node = runtime.CursorNode(template.PlayerId, tile);
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

        internal bool TryProbeDirectCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out RouteProbeSummary summary)
        {
            normalReachable = false; friendlyMoatSeparated = false; summary = default;
            if (scope == null || (uint)scope.TargetX >= MapWidth || (uint)scope.TargetY >= MapWidth ||
                !IsValidTileId(scope.TargetTileId) || runtime.movementTargetAvailability[scope.TargetY * MapWidth + scope.TargetX] == 0 ||
                (runtime.tileFlags[scope.TargetTileId] & MovementBlockedLowTileFlagMask) != 0)
            { runtime.RecordCursorDecision("invalid-move-target", scope); return false; }
            if (!runtime.ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, scope.TargetTileId, out summary)) return false;
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
            if (scope == null || !runtime.CursorScopeMatchesTargetTile(scope, scope.TargetTileId))
            { runtime.RecordCursorDecision("invalid-attack-target", scope); return false; }
            // 8C5F0 passes the actual target unit tile to E2CA0. Reachability is not
            // a melee approach search; weapon and command eligibility stay native.
            if (!runtime.ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, scope.TargetTileId, out summary)) return false;
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
            if (!runtime.ExtensionsEnabled || scope == null) return false;
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
    }
}
