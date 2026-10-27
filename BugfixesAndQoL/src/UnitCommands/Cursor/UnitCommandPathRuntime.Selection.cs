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
            if (ManualCommandsEnabled && TryQualifySameRegionSelection(template, ids, token, out boundScope, out group))
                return true;
            // This newly supported hover must not start a full path probe for every member.
            // Other cursor kinds keep their existing exact and traversal-specific checks.
            if (template.FriendlyGroundMove) return group.SelectedUnits > 0;
            if (ManualCommandsEnabled && TryQualifyNativeSelection(template, ids, token, out boundScope, out group) && group.AllowFallback)
                return true;
            if (!TraversalEnabled) return group.SelectedUnits > 0;
            return Traversal.TryQualifySelectedGroupCursorRouteTraversal(template, ids, token, ref boundScope, out group);
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
            scope.FriendlyGroundMove = template.FriendlyGroundMove;
            scope.OrdinaryGroundMove = template.OrdinaryGroundMove;
            scope.FriendlyUnitId = template.FriendlyUnitId;
            scope.FriendlyUnitGlobalId = template.FriendlyUnitGlobalId;
            return scope;
        }

        internal bool TryCaptureSelectedGroup(
            int playerId, out int[] selectedUnitIds, out string signature)
        { return CaptureCursorSelection(playerId, out selectedUnitIds, out signature); }

        internal bool CursorScopeMatchesTargetTile(AttackCursorPairScope scope, int targetTileId)
        {
            if (scope.FriendlyGroundMove &&
                (!APIShared.UnitAccess.TryGetById(scope.FriendlyUnitId, out GameUnit* friendly, out _) ||
                 friendly == null || !APIShared.UnitAccess.IsReallyAlive(friendly) ||
                 friendly->r_GlobalId != scope.FriendlyUnitGlobalId ||
                 friendly->r_ControllableForPlayerId != scope.PlayerId)) return false;
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

        private bool TryQualifySameRegionSelection(AttackCursorPairScope template, int[] ids, string token,
            out AttackCursorPairScope bound, out CursorGroupRouteSummary group)
        {
            bound = null; group = default; group.SelectionSignature = token;
            int target = template.TargetTileId;
            if (!template.OrdinaryGroundMove || template.FallbackKind != CursorPairFallbackKind.DirectTile || !cursorGroundContractReady ||
                !IsValidTileId(target) || IsCompletedMoatTile(target) ||
                (tileFlags[target] & CursorSpecialStructureTileFlagMask) != 0 ||
                unchecked((short)pathRegionGrid[target]) <= 0) return false;
            foreach (int id in ids)
            {
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null ||
                    !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_ControllableForPlayerId != template.PlayerId)
                    continue;
                group.SelectedUnits++;
                if (CanDigMoat(unit)) group.DiggerUnits++;
                int type = (int)unit->r_UnitChimp;
                if (unit->N000000DB == 0 || type == (int)SHCDESE.Interop.eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                    type == (int)SHCDESE.Interop.eChimps.CHIMP_TYPE_TREBUCHET ||
                    type == (int)SHCDESE.Interop.eChimps.CHIMP_TYPE_MANGONEL ||
                    type == (int)SHCDESE.Interop.eChimps.CHIMP_TYPE_BALLISTA ||
                    TryRejectCursorNativeTarget(unit, template.TargetX, template.TargetY)) continue;
                int current = unchecked((int)unit->r_CurrentPositionTileId);
                if ((uint)unit->r_CurrentTilePositionX >= MapWidth || (uint)unit->r_CurrentTilePositionY >= MapWidth ||
                    !IsValidTileId(current) || IsCompletedMoatTile(current) ||
                    (tileFlags[current] & CursorSpecialStructureTileFlagMask) != 0 ||
                    current != GameTileManagerAPI.Instance.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY) ||
                    pathRegionGrid[current] != pathRegionGrid[target]) continue;
                GetNativeMovementStart(unit, out int x, out int y);
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int source = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (!IsValidTileId(source) || IsCompletedMoatTile(source) ||
                    (tileFlags[source] & CursorSpecialStructureTileFlagMask) != 0 ||
                    pathRegionGrid[source] != pathRegionGrid[target]) continue;
                // E2CA0 itself accepts equal PCLs without a tile search. Only positive,
                // ordinary regions are used here; this never authorizes dispatch.
                var snapshot = new SelectedCursorUnitSnapshot(id, x, y, source, CanDigMoat(unit));
                bound = CreateCursorScopeForSnapshot(template, snapshot);
                bound.GroupCursorAuthorized = true; bound.GroupSelectionSignature = token;
                group.AllowFallback = true; group.LegallyReachableUnits = 1;
                group.RepresentativeUnitId = id; group.RepresentativeStartX = x;
                group.RepresentativeStartY = y; group.RepresentativeStartTileId = source;
                group.RepresentativeCanDig = snapshot.CanDig;
                Shared.DevDiag.Count("manual.cursor.sameRegionWitness");
                return true;
            }
            return false;
        }

        private bool TryGetOwnCursorUnit(int player, uint rawId, int targetTile, out int id, out uint globalId)
        {
            id = rawId > 0 && rawId <= ManualRegionAdmissionBatch.Capacity ? (int)rawId :
                GameTileManagerAPI.Instance.GetTileUnitId(targetTile);
            globalId = 0;
            if (id <= 0 || !APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null ||
                !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_ControllableForPlayerId != player) return false;
            globalId = unit->r_GlobalId;
            return globalId != 0;
        }

        private bool TryBindFriendlyGroundCursor(int player, uint rawId, int targetTile, int callSite,
            int nativePermission, int commandMode, bool otherInteraction, out int id, out uint globalId)
        {
            id = 0; globalId = 0;
            bool accepted = callSite == 0x8F325 && nativePermission != 0 &&
                commandMode == APIShared.Internal.GroundMovePreviewEligibility.OrdinaryMoveCommandMode && !otherInteraction &&
                IsValidTileId(targetTile) && !IsCompletedMoatTile(targetTile) &&
                (tileFlags[targetTile] & CursorSpecialStructureTileFlagMask) == 0 &&
                TryGetOwnCursorUnit(player, rawId, targetTile, out id, out globalId);
            Shared.DevDiag.Count(accepted ? "manual.cursor.friendlyCandidate" : "manual.cursor.friendlyRejected");
            return accepted;
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
