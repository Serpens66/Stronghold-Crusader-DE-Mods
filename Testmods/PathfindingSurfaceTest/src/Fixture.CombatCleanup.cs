using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingSurfaceTest
{
    internal sealed partial class Fixture
    {
        private volatile bool combatCleanupEnabled;
        private int combatCleanupLastTick, combatCleanupTotal;
        private readonly List<CombatDeleteReceipt> initialCombatDeletes = new List<CombatDeleteReceipt>();
        private readonly Dictionary<eChimps, int> combatCleanupTypes = new Dictionary<eChimps, int>();
        private sealed class CombatDeleteReceipt
        {
            internal int Id, Owner;
            internal uint Global;
            internal eChimps Type;
        }

        // Exact semantic union from the frozen SDK AiMilitaryCatalog.IsMilitary:
        // IsCombat + its crewed machines + engineering support + fixed machines.
        // Never classify a Lord as disposable, even if a future enum range moves.
        private static string DisposableMilitaryCategory(eChimps type)
        {
            if (type == eChimps.CHIMP_TYPE_LORD) return null;
            if ((type >= eChimps.CHIMP_TYPE_ARCHER && type <= eChimps.CHIMP_TYPE_KNIGHT) ||
                type == eChimps.CHIMP_TYPE_MONK ||
                (type >= eChimps.CHIMP_TYPE_ARAB_BOW && type <= eChimps.CHIMP_TYPE_ARAB_GRENADIER) ||
                (type >= eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER && type <= eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER)) return "troops";
            switch (type)
            {
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD: return "mobile-machines";
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_BALLISTA: return "fixed-machines";
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_LADDERMAN:
                case eChimps.CHIMP_TYPE_TUNNELER: return "support";
                default: return null;
            }
        }

        private IEnumerable<AutomationWait> PrepareCombatCleanup(AutomationRun run)
        {
            Execute(run, () => {
                Require(scenario == "natural-route-corridor" && !run.CurrentMission.Mode.IsRealMultiplayer,
                    "Disposable combat cleanup requires this explicit offline save fixture");
                Require(SimulationTickEvents.TryGetCurrentTick(out combatCleanupLastTick), "Combat cleanup native tick unavailable");
                combatCleanupEnabled = true;
                initialCombatDeletes.Clear();
                SweepDisposableMilitary(true);
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            bool settled = false;
            for (int i = 0; i < 64 && !settled; i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 16);
                settled = run.InSimulation(run.CurrentMission, () => { Require(error == null, error); return InitialCombatCleanupSettled(); });
            }
            Require(settled, "Native cleanup did not retire the exact initially marked military identities");
            // Initial units have retired; let their existing projectiles expire too.
            yield return run.WaitForTicks(run.CurrentMission, 256);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                Require(error == null, error);
                Require(InitialCombatCleanupSettled(), "An initially deleted military identity became active again");
                log("combat-cleanup-ready initialRetired=" + initialCombatDeletes.Count + " totalMarked=" + combatCleanupTotal +
                    " projectileSettlingTicks=256 periodicTicks=64 buildingsDeleted=0 lordsDeleted=0 civiliansDeleted=0");
                initialCombatDeletes.Clear();
            });
        }

        private bool IsExactFixtureIdentity(int id, uint global)
        {
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Id == id && actors[i].Global == global) return true;
            return false;
        }

        private unsafe void SweepDisposableMilitary(bool initial)
        {
            int marked = 0, lords = 0, civilians = 0, fixtures = 0, stale = 0;
            int troops = 0, mobile = 0, fixedMachines = 0, support = 0;
            combatCleanupTypes.Clear();
            foreach (int id in GameUnitManagerAPI.Instance.GetAllAliveUnits())
            {
                if (!UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit->r_GlobalId == 0) { stale++; continue; }
                eChimps type = unit->r_UnitChimp;
                if (type == eChimps.CHIMP_TYPE_LORD) { lords++; continue; }
                uint global = unit->r_GlobalId; int player = unit->r_ControllableForPlayerId;
                if (IsExactFixtureIdentity(id, global)) { fixtures++; continue; }
                string category = DisposableMilitaryCategory(type);
                if (category == null) { civilians++; continue; }
                // Revalidate the complete receipt immediately before the public
                // setter. Native record lifetime cannot change under this monitor.
                if (!UnitAccess.TryGetById(id, out GameUnit* current, out _) || current->r_GlobalId != global ||
                    current->r_UnitChimp != type || current->r_ControllableForPlayerId != player ||
                    current->r_UnitChimp == eChimps.CHIMP_TYPE_LORD || IsExactFixtureIdentity(id, global)) { stale++; continue; }
                bool accepted = APIShared.UnitAccess.TryGetById(id, out _, out _) ? GameUnitManagerAPI.Instance.DeleteUnitSafe(id) : false;
                Require(accepted && current->r_GlobalId == global && current->r_AliveState == AliveState.MarkedForDeletion,
                    "Public military deletion was not confirmed: id=" + id + " global=" + global + " type=" + type);
                if (initial) initialCombatDeletes.Add(new CombatDeleteReceipt { Id = id, Global = global, Type = type, Owner = player });
                marked++; combatCleanupTotal++;
                combatCleanupTypes[type] = combatCleanupTypes.TryGetValue(type, out int count) ? count + 1 : 1;
                if (category == "troops") troops++;
                else if (category == "mobile-machines") mobile++;
                else if (category == "fixed-machines") fixedMachines++;
                else support++;
            }
            if (initial || marked != 0)
                log("combat-cleanup initial=" + initial + " marked=" + marked + " cumulative=" + combatCleanupTotal +
                    " troops=" + troops + " mobileMachines=" + mobile + " fixedMachines=" + fixedMachines + " support=" + support +
                    " keptLords=" + lords + " keptCiviliansAndNeutral=" + civilians + " keptExactFixture=" + fixtures + " staleSkipped=" + stale +
                    " byType=" + string.Join(",", combatCleanupTypes.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value)));
        }

        private unsafe bool InitialCombatCleanupSettled()
        {
            foreach (CombatDeleteReceipt receipt in initialCombatDeletes)
                if (UnitAccess.TryGetById(receipt.Id, out GameUnit* unit, out _) && unit->r_GlobalId == receipt.Global && unit->r_AliveState != AliveState.None)
                    return false;
            return true;
        }
    }
}
