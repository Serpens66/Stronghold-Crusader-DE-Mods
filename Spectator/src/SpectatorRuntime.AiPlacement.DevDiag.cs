#if SERP_DEVDIAG
using APIShared;
using APIShared.Units.AiMilitary;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static int aiPlacementDiagnostics;
        // Bounded per-session evidence; Shift placement of many units must still be fully visible.
        private const int AiResultLogLimit = 600, AiFollowUpLimit = 400;
        // Simulation-tick offsets after the first tick that observed an Applied admission. The AI
        // pass frequency is not statically known, so later samples reveal the controller handoff.
        private static readonly int[] AiFollowUpDelays = { 50, 400, 1500, 4000 };
        private static readonly List<AiFollowUp> aiFollowUps = new List<AiFollowUp>();
        private sealed class AiFollowUp
        {
            internal long Session;
            internal AiUnitRef Unit;
            internal AiMilitaryRole Role;
            internal int Owner, ExpectedRole, Group, StartX, StartY, StartTargetX, StartTargetY, Armed = int.MinValue, Sample;
            internal uint GroupGlobal;
            internal string Detail;
            internal bool EverMoved;
        }
        static partial void DiagAiPlacementReset()
        {
            Interlocked.Exchange(ref aiPlacementDiagnostics, 0);
            lock (aiFollowUps) aiFollowUps.Clear();
        }
        static partial void DiagAiPlacementResult(long session, int owner, AiMilitaryAssignmentResult result)
            => DiagAiState(session, owner, result, "placement");
        static partial void DiagAiPlacementSessionStarted(long session)
        {
            var capability = aiMilitary;
            if (capability == null || engineLock == null) return;
            try
            {
                int tracked = 0;
                lock (engineLock)
                {
                    var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                    for (int i = 0; i < units.Length; i++)
                    {
                        var unit = units[i];
                        if (unit.r_GlobalId == 0 || !capability.TryGetState(new AiUnitRef(i + 1, unit.r_GlobalId), out var state)) continue;
                        tracked++;
                        if (tracked <= 64) DiagAiState(session, state.Group.PlayerId, state, "session-restore-state");
                    }
                }
                Shared.DevDiag.Log(() => "SPECTATOR_AI_RESTORE session=" + session + " tracked=" + tracked + " sampled=" + Math.Min(64, tracked));
            }
            catch (Exception error) { Shared.DevDiag.Log(() => "SPECTATOR_AI_RESTORE_ERROR session=" + session + " error=" + error); }
        }
        private static unsafe void DiagAiState(long session, int owner, AiMilitaryAssignmentResult result, string source)
        {
            if (!IsCurrentSession(session) || Interlocked.Increment(ref aiPlacementDiagnostics) > AiResultLogLimit) return;
            int actualGroup = -1, actualRole = -1, actualOwner = -1, expectedRole = -1;
            uint actualGlobal = 0, actualUnitGlobal = 0;
            bool member = false, identity = false, validGroup = false;
            if (UnitAccess.TryGetById(result.Unit.UnitId, out GameUnit* unit, out _) && unit != null)
            {
                actualUnitGlobal = unit->r_GlobalId; actualOwner = unit->r_ControllableForPlayerId;
                actualRole = unit->r_AITribeRole; actualGroup = unit->r_TribeId;
                expectedRole = DiagnosticNativeRole(result.Assignment.Role);
                if (result.Assignment.Role == AiMilitaryRole.None && (!DiagnosticMilitary(unit->r_UnitChimp) ||
                    actualOwner < 1 || actualOwner > GamePlayerManagerAPI.MAX_PLAYERS ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(actualOwner))) expectedRole = 0;
                identity = unit->r_GlobalId == result.Unit.GlobalId;
                if (identity)
                {
                    if (actualGroup > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(actualGroup, out GameTribe* tribe) && tribe != null)
                    {
                        actualGlobal = tribe->r_GlobalId;
                        validGroup = actualGlobal != 0 && tribe->r_AliveState == AliveState.IsAlive && tribe->r_PlayerIdOwner == actualOwner;
                        int id = result.Unit.UnitId;
                        // Extender GameTribe's native member bitfield starts at +36, not a list of IDs.
                        if (id > 0 && id < 10000) member = ((((ushort*)((byte*)tribe + 0x36))[id >> 4] >> (id & 15)) & 1) != 0;
                    }
                }
            }
            bool additional = aiMilitary?.IsExcludedFromRecruitmentQuota(result.Unit) == true;
            bool snapshot = source == "session-restore-state";
            bool matches = snapshot ? identity && (actualGroup == 0 || validGroup && member) :
                result.Status != AiMilitaryAssignmentStatus.Applied || identity &&
                (owner <= 0 || actualOwner == owner) && actualRole == expectedRole &&
                additional == (result.Assignment.RecruitmentMode == AiRecruitmentMode.Additional) &&
                (result.Assignment.Role == AiMilitaryRole.None && expectedRole == 0 ||
                 actualGroup == result.Group.TribeId && actualGlobal == result.Group.GlobalId && validGroup && member);
            Shared.DevDiag.Log(() => "SPECTATOR_AI_RESULT source=" + source + " session=" + session + " expectedOwner=" + owner + " actualOwner=" + actualOwner +
                " unit=" + result.Unit + " role=" + result.Assignment.Role + " mode=" + result.Assignment.RecruitmentMode +
                " actualUnitGlobal=" + actualUnitGlobal + " status=" + result.Status + " reason=" + result.Reason +
                " expectedRole=" + expectedRole + " actualRole=" + actualRole + " expectedGroup=" + result.Group.TribeId + "/" + result.Group.GlobalId +
                " actualGroup=" + actualGroup + "/" + actualGlobal + " member=" + member + " additional=" + additional +
                " check=" + (snapshot ? "current-snapshot" : "admission-postcondition") + " matches=" + matches + " detail=" + result.Detail);
            if (!snapshot && result.Status == AiMilitaryAssignmentStatus.Applied && identity) TrackAiFollowUp(session, owner, result, expectedRole);
        }

        private static unsafe void TrackAiFollowUp(long session, int owner, AiMilitaryAssignmentResult result, int expectedRole)
        {
            if (!UnitAccess.TryGetById(result.Unit.UnitId, out GameUnit* unit, out _) || unit == null) return;
            var entry = new AiFollowUp
            {
                Session = session, Unit = result.Unit, Role = result.Assignment.Role, Owner = owner, ExpectedRole = expectedRole,
                Group = result.Group.TribeId, GroupGlobal = result.Group.GlobalId, Detail = result.Detail,
                StartX = unit->r_CurrentTilePositionX, StartY = unit->r_CurrentTilePositionY,
                StartTargetX = unit->r_TargetTilePositionX, StartTargetY = unit->r_TargetTilePositionY
            };
            lock (aiFollowUps)
            {
                if (aiFollowUps.Count >= AiFollowUpLimit) return;
                aiFollowUps.Add(entry);
            }
        }

        // Simulation tick (Extender OnTick). Reads only; skips a tick instead of waiting for the monitor.
        private static unsafe void DiagAiFollowUps(int tick)
        {
            if (engineLock == null) return;
            lock (aiFollowUps) if (aiFollowUps.Count == 0) return;
            if (!Monitor.TryEnter(engineLock)) return;
            try
            {
                lock (aiFollowUps)
                {
                    for (int index = aiFollowUps.Count - 1; index >= 0; index--)
                    {
                        var entry = aiFollowUps[index];
                        if (!IsCurrentSession(entry.Session)) { aiFollowUps.RemoveAt(index); continue; }
                        if (entry.Armed == int.MinValue) { entry.Armed = tick; continue; }
                        if (unchecked(tick - entry.Armed) < AiFollowUpDelays[entry.Sample]) continue;
                        LogAiFollowUp(entry, unchecked(tick - entry.Armed));
                        if (++entry.Sample >= AiFollowUpDelays.Length) aiFollowUps.RemoveAt(index);
                    }
                }
            }
            catch (Exception error) { Shared.DevDiag.Log(() => "SPECTATOR_AI_FOLLOWUP_ERROR error=" + error); }
            finally { Monitor.Exit(engineLock); }
        }

        private static unsafe void LogAiFollowUp(AiFollowUp entry, int elapsed)
        {
            bool alive = false, member = false, sameGroup = false;
            int x = -1, y = -1, targetX = -1, targetY = -1, role = -1, group = -1, aiState = -1, distance = -1;
            if (UnitAccess.TryGetById(entry.Unit.UnitId, out GameUnit* unit, out _) && unit != null && unit->r_GlobalId == entry.Unit.GlobalId)
            {
                alive = UnitAccess.IsReallyAlive(unit);
                x = unit->r_CurrentTilePositionX; y = unit->r_CurrentTilePositionY;
                targetX = unit->r_TargetTilePositionX; targetY = unit->r_TargetTilePositionY;
                role = unit->r_AITribeRole; group = unit->r_TribeId; aiState = unit->r_AIState;
                distance = Math.Max(Math.Abs(x - entry.StartX), Math.Abs(y - entry.StartY));
                if (group > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(group, out GameTribe* tribe) && tribe != null)
                {
                    sameGroup = group == entry.Group && tribe->r_GlobalId == entry.GroupGlobal;
                    int id = entry.Unit.UnitId;
                    if (id > 0 && id < 10000) member = ((((ushort*)((byte*)tribe + 0x36))[id >> 4] >> (id & 15)) & 1) != 0;
                }
            }
            bool targetChanged = targetX != entry.StartTargetX || targetY != entry.StartTargetY;
            entry.EverMoved |= distance > 1 || targetChanged;
            bool last = entry.Sample == AiFollowUpDelays.Length - 1;
            bool moved = entry.EverMoved;
            Shared.DevDiag.Log(() => "SPECTATOR_AI_FOLLOWUP unit=" + entry.Unit + " owner=" + entry.Owner + " role=" + entry.Role +
                " ticks=" + elapsed + " alive=" + alive + " expectedRole=" + entry.ExpectedRole + " actualRole=" + role +
                " expectedGroup=" + entry.Group + "/" + entry.GroupGlobal + " actualGroup=" + group + " sameGroup=" + sameGroup + " member=" + member +
                " start=" + entry.StartX + "," + entry.StartY + " now=" + x + "," + y + " distance=" + distance +
                " target=" + entry.StartTargetX + "," + entry.StartTargetY + "->" + targetX + "," + targetY + " aiState=" + aiState +
                " moved=" + moved + (last ? " final=True" : string.Empty) + " handoff=" + (entry.Detail.Length != 0));
        }
        // FBCB9319 diagnostic expectations only; never used by release admission or the menu.
        private static int DiagnosticNativeRole(AiMilitaryRole role)
        {
            switch (role)
            {
                case AiMilitaryRole.None: return ushort.MaxValue;
                case AiMilitaryRole.CastleDefense: return 1;
                case AiMilitaryRole.Harassment: return 2;
                case AiMilitaryRole.DefensePatrol: return 4;
                case AiMilitaryRole.HomeMoat: return 5;
                case AiMilitaryRole.EconomyProtection: return 6;
                case AiMilitaryRole.Intervention: return 7;
                case AiMilitaryRole.SiegeEngineers: return 10;
                case AiMilitaryRole.SiegeMoat: return 11;
                case AiMilitaryRole.SiegeAssassins: return 12;
                case AiMilitaryRole.SiegeDiversion: return 13;
                case AiMilitaryRole.SiegeLadders: return 14;
                case AiMilitaryRole.SiegeTunnelers: return 15;
                case AiMilitaryRole.SiegeStorm: return 16;
                case AiMilitaryRole.SiegeCover: return 17;
                case AiMilitaryRole.SiegeShock: return 18;
                case AiMilitaryRole.SiegeReserve: return 19;
                case AiMilitaryRole.SiegeWall: return 20;
                case AiMilitaryRole.HarassmentEngineers: return 22;
                default: return -1;
            }
        }
        private static bool DiagnosticMilitary(eChimps type) =>
            type >= eChimps.CHIMP_TYPE_ARCHER && type <= eChimps.CHIMP_TYPE_ENGINEER || type == eChimps.CHIMP_TYPE_TUNNELER ||
            type == eChimps.CHIMP_TYPE_MONK || type == eChimps.CHIMP_TYPE_CATAPULT || type == eChimps.CHIMP_TYPE_TREBUCHET ||
            type == eChimps.CHIMP_TYPE_MANGONEL || type == eChimps.CHIMP_TYPE_SIEGE_TOWER || type == eChimps.CHIMP_TYPE_BATTERING_RAM ||
            type == eChimps.CHIMP_TYPE_PORTABLE_SHIELD || type == eChimps.CHIMP_TYPE_BALLISTA ||
            type >= eChimps.CHIMP_TYPE_ARAB_BOW && type <= eChimps.CHIMP_TYPE_ARAB_BALLISTA ||
            type >= eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER && type <= eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER;
    }
}
#endif
