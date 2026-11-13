using static Pathfinding.UnitCommands.UnitCommandPathRuntime;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Pathfinding.Engine
{
    // Moves of one soldier (FireEscape) as Vanilla group commands: IssueMoveHereCommand runs 0x11B520,
    // which admits the member, sets the military walking state (r_AIState 101) and publishes the route
    // through 0x196280, so the unit walks with its animation. 0x196280 alone (MoveToTile) only publishes
    // the route: a standing soldier glides. A group command moves the whole tribe, so the soldier gets a
    // tribe of its own for the case (Create, Unassign, Assign as Pathfinding's shift queue and
    // FastMovementScheduler.TryBindFastDispatchGroup) and rejoins its group at the end, if that still
    // exists with the same global id and owner; the emptied own tribe is deleted. A soldier that is
    // already alone in its tribe keeps it. Simulation tick only: every peer runs the same commands.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        // Own receives the commands; Split: created for this case (deleted once empty). Original (0: none)
        // and OriginalGlobal identify the group to rejoin.
        private struct SoldierTribe { internal int Own, Original; internal uint OwnGlobal, OriginalGlobal; internal bool Split; }
        private long soldierTribeSplits, soldierTribeRejoins, soldierTribeReleased, soldierTribeFailures, soldierTribeNotMoved, soldierMoveMarksCleared;
        private static bool soldierMoveMarkUnavailableLogged;
        // An ordinary player move (8C5F0 -> 196100 -> 11B520(patrol 0, newOrder 1, flags 1)); 196100's
        // extra +0x56C "fast" flag is not part of the direct API. The move type is only tested for != 0
        // (destination marker counter +0x78), so DefaultInSync and Fast behave alike here.
        private const int SoldierMoveNewOrder = 1;
        private const TribeMoveType SoldierMoveType = TribeMoveType.DefaultInSync;
        // r_AIState of an admitted member after 11B520: 101 walking, 122 exit route after a failed 196280
        // (no Extender enum names these states). 11B520 returns 1 also when it admitted no member.
        private const int MilitaryWalkState = 101, MilitaryExitState = 122;

        private static bool IsMoveCommanded(GameUnit* unit) => unit->r_AIState == MilitaryWalkState || unit->r_AIState == MilitaryExitState;

        private static bool TryGetSoldier(int id, uint global, out GameUnit* unit) =>
            APIShared.UnitAccess.TryGetById(id, out unit, out _) && unit != null && unit->r_GlobalId == global && APIShared.UnitAccess.IsReallyAlive(unit);

        private static bool TryGetLiveTribe(int tribeId, uint global, int player, out GameTribe* tribe)
        {
            tribe = null;
            return tribeId > 0 && tribeId < MaximumTribeCount && GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out tribe) && tribe != null &&
                tribe->r_AliveState == AliveState.IsAlive && (global == 0 || tribe->r_GlobalId == global) && tribe->r_PlayerIdOwner == player;
        }

        private bool TryIsolateSoldier(int id, uint global, int player, out SoldierTribe result)
        {
            result = default;
            if (!TryGetSoldier(id, global, out GameUnit* unit)) return false;
            var tribes = GameTribeManagerAPI.Instance;
            int original = unit->r_TribeId;
            GameTribe* group = null;
            if (original > 0 && !TryGetLiveTribe(original, 0, player, out group)) { soldierTribeFailures++; return false; }
            if (group != null && group->r_UnitsInGroup == 1)
            { result = new SoldierTribe { Own = original, OwnGlobal = group->r_GlobalId }; return true; }
            long created = tribes.Create(player, false);
            if (created <= 0 || created >= MaximumTribeCount) { soldierTribeFailures++; return false; }
            int own = (int)created;
            if (!TryGetLiveTribe(own, 0, player, out GameTribe* fresh) || fresh->r_UnitsInGroup != 0) { soldierTribeFailures++; return false; }
            var split = new SoldierTribe { Own = own, OwnGlobal = fresh->r_GlobalId, Original = original, OriginalGlobal = group != null ? group->r_GlobalId : 0, Split = true };
            if (group != null) fresh->r_TribeStance = group->r_TribeStance;
            bool moved = (original <= 0 || tribes.UnassignUnit(original, id)) && TryGetSoldier(id, global, out unit) && unit->r_TribeId == 0 &&
                tribes.AssignUnit(own, id) && TryGetSoldier(id, global, out unit) && unit->r_TribeId == own;
            if (!moved)
            {
                // Roll back: the soldier stays in (or returns to) its group.
                if (TryGetSoldier(id, global, out unit))
                {
                    if (unit->r_TribeId == own) tribes.UnassignUnit(own, id);
                    if (unit->r_TribeId == 0 && original > 0) tribes.AssignUnit(original, id);
                }
                DeleteOwnTribe(split);
                soldierTribeFailures++; return false;
            }
            soldierTribeSplits++;
            result = split; return true;
        }

        // A Vanilla move command to the soldier's own tribe; moveX/moveY is the move target 0x11B520 gave it.
        private bool TryMoveSoldier(int id, uint global, SoldierTribe tribe, int x, int y, out int moveX, out int moveY)
        {
            moveX = moveY = -1;
            if (!TryGetSoldier(id, global, out GameUnit* unit) || unit->r_TribeId != tribe.Own ||
                !TryGetLiveTribe(tribe.Own, tribe.OwnGlobal, unit->r_ControllableForPlayerId, out _)) return false;
            ClearSoldierMoveMark(id);
            if (!GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe.Own, x, y, false, SoldierMoveNewOrder, SoldierMoveType)) return false;
            if (!TryGetSoldier(id, global, out unit)) return false;
            // A member 11B520 did not admit keeps its old order and gets no route.
            if (!IsMoveCommanded(unit)) { soldierTribeNotMoved++; return false; }
            moveX = unit->r_TargetTilePositionX2; moveY = unit->r_TargetTilePositionY2;
            return true;
        }

        // 0x11B520 skips a member whose AI exclusion mark (GameUnit+0x428) is set. Only the AI's own group
        // orders set it, and an AI soldier keeps it in the tribe this case orders, so the move would leave
        // it behind (notMoved). Vanilla's AI clears the mark right before its own plain group moves
        // (0x126020); this clears it for the one ordered soldier. Player soldiers never carry it. The mark
        // is not restored on rejoin: the AI rewrites all marks of its group before its next own order.
        // Simulation tick on every peer, like the move itself. Without the APIShared helper (other native
        // image or layout) the move runs as before.
        private void ClearSoldierMoveMark(int id)
        {
            if (APIShared.Commands.GroupMoveExclusion.TryClear(id, out bool wasMarked))
            { if (wasMarked) soldierMoveMarksCleared++; return; }
            if (APIShared.Commands.GroupMoveExclusion.IsAvailable || soldierMoveMarkUnavailableLogged) return;
            soldierMoveMarkUnavailableLogged = true;
            Shared.DebugLogHelper.LogError(log, "Pathfinding soldier moves cannot clear the AI group-move mark; AI soldiers may stay behind: " +
                APIShared.Commands.GroupMoveExclusion.UnavailabilityReason);
        }

        // Ends a case: back into the original group (if it still exists). An AI soldier whose group is gone
        // leaves its own tribe: Vanilla's repair (0x118660) gives units without a valid tribe a typed AI
        // tribe, whereas the AI never orders a tribe it did not create. Removing the last member deletes the
        // own tribe natively (0x123EA0); DeleteOwnTribe covers what remains.
        private void RejoinSoldier(int id, uint global, SoldierTribe tribe)
        {
            if (!tribe.Split) return;
            var tribes = GameTribeManagerAPI.Instance;
            if (TryGetSoldier(id, global, out GameUnit* unit) && unit->r_TribeId == tribe.Own)
            {
                int player = unit->r_ControllableForPlayerId;
                if (tribe.Original > 0 && TryGetLiveTribe(tribe.Original, tribe.OriginalGlobal, player, out _))
                {
                    if (tribes.UnassignUnit(tribe.Own, id) && TryGetSoldier(id, global, out unit) && unit->r_TribeId == 0 &&
                        tribes.AssignUnit(tribe.Original, id) && TryGetSoldier(id, global, out unit) && unit->r_TribeId == tribe.Original) soldierTribeRejoins++;
                    else
                    {
                        if (TryGetSoldier(id, global, out unit) && unit->r_TribeId == 0) tribes.AssignUnit(tribe.Own, id);
                        soldierTribeFailures++;
                    }
                }
                else if (GamePlayerManagerAPI.Instance.IsAIPlayer(player) && tribes.UnassignUnit(tribe.Own, id)) soldierTribeReleased++;
            }
            DeleteOwnTribe(tribe);
        }

        // Only the tribe this case created, and only once it is empty (a new player order moves the
        // soldier into another tribe and leaves it empty).
        private static void DeleteOwnTribe(SoldierTribe tribe)
        {
            if (tribe.Split && GameTribeManagerAPI.Instance.TryGetTribeById(tribe.Own, out GameTribe* own) && own != null &&
                own->r_AliveState == AliveState.IsAlive && own->r_GlobalId == tribe.OwnGlobal && own->r_UnitsInGroup == 0)
                GameTribeManagerAPI.Instance.DeleteTribeSafe(tribe.Own);
        }

        private string FormatSoldierTribes() =>
            $"tribes(splits={soldierTribeSplits} rejoins={soldierTribeRejoins} released={soldierTribeReleased} failures={soldierTribeFailures} notMoved={soldierTribeNotMoved} aiMarksCleared={soldierMoveMarksCleared})";

        private void ResetSoldierTribeMetrics() =>
            soldierTribeSplits = soldierTribeRejoins = soldierTribeReleased = soldierTribeFailures = soldierTribeNotMoved = soldierMoveMarksCleared = 0;
    }
}
