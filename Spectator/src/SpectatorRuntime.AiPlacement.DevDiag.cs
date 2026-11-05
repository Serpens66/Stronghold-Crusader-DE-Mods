#if SERP_DEVDIAG
using APIShared;
using APIShared.Units.AiMilitary;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static int aiPlacementDiagnostics;
        static partial void DiagAiPlacementReset() => Interlocked.Exchange(ref aiPlacementDiagnostics, 0);
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
            if (!IsCurrentSession(session) || Interlocked.Increment(ref aiPlacementDiagnostics) > 128) return;
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
