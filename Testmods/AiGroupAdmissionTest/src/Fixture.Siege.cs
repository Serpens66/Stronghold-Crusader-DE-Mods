using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Automation;
using APIShared.Units.AiMilitary;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AiGroupAdmissionTest
{
    // Real AI siege (scenario "ai-siege"): one AI is ordered through Vanilla's ally attack request
    // (HUD_AlliesPanel "Attack", Chore 113, accepted natively by 41500) to besiege a second AI.
    // Its army consists of probes admitted in siege state 0. In every further siege state that
    // occurs, one probe per offered siege role is admitted. Strict checks: admission and its
    // postconditions, and that every admitted probe still alive in its group received an order
    // by the end of the siege cycle (user decision 18.11.2026: at the latest at the next relevant
    // phase). Evidence: siege-state transitions and the tick/state at which each probe moved.
    // Static basis: _inspect/.../knowledge/AI_SIEGE_START.md and AI_MILITARY_ADMISSION.md.
    internal sealed partial class Fixture
    {
        internal const string SiegeScenario = "ai-siege";
        // Variant for siege state 7: the target lord is killed during states 5/6; the lord check
        // in 3C2E0 then turns 4-6 into 7 (wait, no orders) and later 9 (retreat move).
        internal const string SiegeLordScenario = "ai-siege-lord";
        private const int KillAfterState6Ticks = 600;
        private const int KillAfterState5Ticks = 2500;
        internal bool KillTargetLord;
        private int firstState5Tick = -1, firstState6Tick = -1;
        private bool lordKilled;
        private const int SampleTicks = 25;
        private const int ArmyBatch = 4;
        private const int ArmyLimit = 80;
        private const int StartAttemptTicks = 800;
        private const int MaxSiegeTicks = 60000;
        private const int AfterCycleTicks = 1500;
        // Counted by the native troop check 2FD40 (AI_SIEGE_START.md); engineers are excluded.
        private static readonly AiMilitaryRole[] ArmyRoles =
        {
            AiMilitaryRole.SiegeStorm, AiMilitaryRole.SiegeCover, AiMilitaryRole.SiegeReserve,
            AiMilitaryRole.SiegeShock, AiMilitaryRole.SiegeWall, AiMilitaryRole.SiegeDiversion,
        };

        private sealed class SiegeTrack
        {
            internal int SpawnState, SpawnTick, SpawnX, SpawnY, SpawnTargetX, SpawnTargetY;
            internal int OrderedTick = -1, OrderedState = -1;
            internal bool Prompt;
        }

        private readonly Dictionary<Probe, SiegeTrack> siegeTracks = new Dictionary<Probe, SiegeTrack>();
        private readonly HashSet<string> uncheckedRounds = new HashSet<string>();
        private readonly HashSet<int> probedStates = new HashSet<int>();
        private int siegeTarget, simTicks, armyRoleIndex, lastSiegeState = -1;

        internal IEnumerable<AutomationStep> SiegeSteps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), StartSiegeSkirmish);
            yield return new AutomationStep("siege-players", TimeSpan.FromSeconds(120), WaitSiegePlayers, true);
            yield return new AutomationStep("siege-start", TimeSpan.FromSeconds(900), StartSiege, true);
            yield return new AutomationStep("siege-observe", TimeSpan.FromSeconds(1800), ObserveSiege, true);
            yield return new AutomationStep("siege-check", TimeSpan.FromSeconds(30), CheckSiege, true);
            yield return new AutomationStep("verdict", TimeSpan.FromSeconds(10), Verdict);
        }

        // No peace time: it blocks the AI's siege recruitment (AI_SIEGE_START.md).
        private IEnumerable<AutomationWait> StartSiegeSkirmish(AutomationRun run)
        {
            DisableRandomEvents();
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_WOLF, AILords.SK_RAT }, 0)) yield return wait;
            foreach (var wait in EnterMission(run)) yield return wait;
        }

        private IEnumerable<AutomationWait> WaitSiegePlayers(AutomationRun run)
        {
            Require(ApiShared.ForMod(Plugin.Guid).TryGetAiMilitary(out api, out var diagnostic), "AI military unavailable: " + diagnostic.Reason);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until("attacking AI with siege roles and a second live AI", () =>
            {
                try { return run.InSimulation(run.CurrentMission, FindSiegePlayers); }
                catch (InvalidOperationException) { return false; }
            });
            log("siege attacker=" + owner + " target=" + siegeTarget + " " + SiegeContext());
        }

        private unsafe bool FindSiegePlayers()
        {
            var players = GamePlayerManagerAPI.Instance;
            var live = new List<int>();
            for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
                if (players.IsAIPlayer(player) && LordAlive(player)) live.Add(player);
            // The attacker is the AI whose AIC offers the most siege roles (storm/shock depend on it).
            int best = 0, bestRoles = 0;
            foreach (int attacker in live)
            {
                if (!ArmyRoles.Any(role => FirstTypeFor(attacker, role) != null)) continue;
                int roles = SiegeRoles().Count(role => FirstTypeFor(attacker, role) != null);
                log("siege candidate player=" + attacker + " offeredSiegeRoles=" + roles);
                if (roles > bestRoles) { best = attacker; bestRoles = roles; }
            }
            int target = live.FirstOrDefault(other => other != best);
            if (best == 0 || target == 0) return false;
            owner = best; siegeTarget = target;
            return true;
        }

        private static unsafe bool LordAlive(int player)
        {
            var players = GamePlayerManagerAPI.Instance;
            uint global = unchecked((uint)players.GetLordUnitGlobalId(player));
            return global != 0 && UnitAccess.TryGetById(players.GetLordUnitId(player), out GameUnit* lord, out _) &&
                lord->r_GlobalId == global && UnitAccess.IsReallyAlive(lord);
        }

        private eChimps? FirstTypeFor(int player, AiMilitaryRole role)
        {
            foreach (eChimps type in Enum.GetValues(typeof(eChimps)))
            {
                if (type == eChimps.CHIMP_TYPE_LORD) continue;
                IReadOnlyList<AiMilitaryOption> options;
                try { options = api.GetOptions(player, type, false); }
                catch (Exception) { continue; }
                if (options != null && options.Any(option => option.Role == role && option.IsAvailable)) return type;
            }
            return null;
        }

        // Vanilla's own request path; the AI accepts only with enough counted siege troops
        // (41500: AIC siege_troops_before_will_come_to_rescue), so the army grows in batches.
        private IEnumerable<AutomationWait> StartSiege(AutomationRun run)
        {
            int batch = 0, armySize = 0;
            while (true)
            {
                int spawned = run.InSimulation(run.CurrentMission, () => SpawnArmy("army" + batch));
                armySize += spawned; batch++;
                log("siege army batch=" + batch + " spawned=" + spawned + " total=" + armySize);
                foreach (var wait in WaitAdmitted(run, "army" + (batch - 1))) yield return wait;
                yield return run.WaitForTicks(run.CurrentMission, StartAttemptTicks / 2);
                EngineInterface.GameAction(Enums.GameActionCommand.Ally_Orders, 1, owner, siegeTarget);
                for (int waited = 0; waited < StartAttemptTicks; waited += SampleTicks)
                {
                    yield return run.WaitForTicks(run.CurrentMission, SampleTicks);
                    if (run.InSimulation(run.CurrentMission, () => SiegeState()) > 0) break;
                }
                string context = run.InSimulation(run.CurrentMission, SiegeContext);
                log("siege start attempt=" + batch + " " + context);
                if (run.InSimulation(run.CurrentMission, () => SiegeState()) > 0) break;
                Require(armySize < ArmyLimit && spawned > 0, "The AI did not start the requested siege; " + context);
            }
        }

        // Rotates the role per batch so that the counted army spreads over the AIC's group limits.
        private int SpawnArmy(string round)
        {
            int before = probes.Count;
            for (int offset = 0; offset < ArmyRoles.Length; offset++)
            {
                var role = ArmyRoles[(armyRoleIndex + offset) % ArmyRoles.Length];
                eChimps? type = FirstTypeFor(owner, role);
                if (type == null) continue;
                SpawnBatch(round, "army", type.Value, new AiMilitaryAssignment(role, AiRecruitmentMode.Regular), ArmyBatch);
                Track(round, false);
                armyRoleIndex += offset + 1;
                break;
            }
            return probes.Count - before;
        }

        private IEnumerable<AutomationWait> WaitAdmitted(AutomationRun run, string round)
        {
            yield return AutomationWait.Until(round + " admissions completed", () =>
            {
                lock (probes) return probes.Where(probe => probe.Round == round).All(probe => probe.Final != null);
            });
            run.InSimulation(run.CurrentMission, () => { CheckAdmissions(round); return true; });
            uncheckedRounds.Remove(round);
        }

        private IEnumerable<AutomationWait> ObserveSiege(AutomationRun run)
        {
            int cycleEnd = -1;
            bool besieged = false;
            for (int ticks = 0; ticks < MaxSiegeTicks; ticks += SampleTicks)
            {
                yield return run.WaitForTicks(run.CurrentMission, SampleTicks);
                simTicks += SampleTicks;
                int state = run.InSimulation(run.CurrentMission, SampleSiege);
                besieged |= state >= 2;
                if (besieged && state == 0 && cycleEnd < 0) cycleEnd = ticks;
                if (cycleEnd >= 0 && ticks - cycleEnd >= AfterCycleTicks) break;
                foreach (string round in uncheckedRounds.ToList())
                {
                    bool done;
                    lock (probes) done = probes.Where(probe => probe.Round == round).All(probe => probe.Final != null);
                    if (!done) continue;
                    run.InSimulation(run.CurrentMission, () => { CheckAdmissions(round); return true; });
                    uncheckedRounds.Remove(round);
                }
            }
            // Withdraw the persistent request (mode 10, HUD "Cancel") so that no further siege starts.
            EngineInterface.GameAction(Enums.GameActionCommand.Ally_Orders, 10, owner);
            log("siege observed states=" + string.Join(",", probedStates.OrderBy(state => state)) + " cycleEnded=" + (cycleEnd >= 0) +
                " " + run.InSimulation(run.CurrentMission, SiegeContext));
            Require(besieged, "No real siege state (>= 2) was reached");
            Require(cycleEnd >= 0, "The siege cycle did not return to state 0 within " + MaxSiegeTicks + " ticks");
            if (KillTargetLord) Require(lordKilled && probedStates.Contains(7), "Siege state 7 was not reached after the target lord died");
        }

        private unsafe void KillTargetLordWhenDue(int state)
        {
            if (state == 5 && firstState5Tick < 0) firstState5Tick = simTicks;
            if (state == 6 && firstState6Tick < 0) firstState6Tick = simTicks;
            if (!KillTargetLord || lordKilled) return;
            bool due = state == 6 && simTicks - firstState6Tick >= KillAfterState6Ticks ||
                state == 5 && simTicks - firstState5Tick >= KillAfterState5Ticks;
            if (!due) return;
            lordKilled = true;
            int lordId = GamePlayerManagerAPI.Instance.GetLordUnitId(siegeTarget);
            GameUnitManagerAPI.Instance.KillUnit(lordId);
            log("SIEGE-LORD-KILLED target=" + siegeTarget + " lord=" + lordId + " state=" + state + " simTicks=" + simTicks +
                " aliveAfter=" + LordAlive(siegeTarget));
        }

        // Under the engine monitor: siege-state transition, probes for a newly reached state and
        // order detection (moved more than one tile from the spawn tile, or a changed route target).
        private unsafe int SampleSiege()
        {
            int state = SiegeState();
            if (state != lastSiegeState)
            {
                log("SIEGE-STATE " + lastSiegeState + "->" + state + " simTicks=" + simTicks + " tick=" + Tick() + " " + SiegeContext());
                lastSiegeState = state;
            }
            if (state >= 1 && probedStates.Add(state)) SpawnStateProbes(state);
            KillTargetLordWhenDue(state);
            foreach (var pair in siegeTracks)
            {
                var track = pair.Value;
                if (track.OrderedTick >= 0) continue;
                var probe = pair.Key;
                if (!UnitAccess.TryGetById(probe.Unit.UnitId, out GameUnit* unit, out _) || unit->r_GlobalId != probe.Unit.GlobalId ||
                    !UnitAccess.IsReallyAlive(unit)) continue;
                int distance = Math.Max(Math.Abs(unit->r_CurrentTilePositionX - track.SpawnX), Math.Abs(unit->r_CurrentTilePositionY - track.SpawnY));
                if (distance <= 1 && unit->r_TargetTilePositionX == track.SpawnTargetX && unit->r_TargetTilePositionY == track.SpawnTargetY) continue;
                track.OrderedTick = simTicks; track.OrderedState = state;
                if (probe.Batch != "army")
                    log("SIEGE-ORDERED unit=" + probe.Unit + " role=" + probe.Wanted.Role + " spawnState=" + track.SpawnState +
                        " orderedState=" + state + " afterTicks=" + (simTicks - track.SpawnTick) + " prompt=" + track.Prompt);
            }
            return state;
        }

        private void SpawnStateProbes(int state)
        {
            string round = "s" + state;
            foreach (var role in SiegeRoles())
            {
                eChimps? type = FirstTypeFor(owner, role);
                if (type == null) { log("SIEGE-ROLE-UNAVAILABLE state=" + state + " role=" + role + " " + Unavailable(role)); continue; }
                SpawnBatch(round, "state", type.Value, new AiMilitaryAssignment(role, AiRecruitmentMode.Regular), 1);
            }
            Track(round, true);
            log("siege state=" + state + " probes=" + Current(round).Count);
        }

        private static IEnumerable<AiMilitaryRole> SiegeRoles() => Enum.GetValues(typeof(AiMilitaryRole)).Cast<AiMilitaryRole>()
            .Where(role => role >= AiMilitaryRole.SiegeEngineers && role <= AiMilitaryRole.SiegeWall);

        private string Unavailable(AiMilitaryRole role)
        {
            foreach (eChimps type in new[] { eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_ENGINEER, eChimps.CHIMP_TYPE_LADDERMAN, eChimps.CHIMP_TYPE_ARAB_ASSASIN })
            {
                var option = api.GetOptions(owner, type, false)?.FirstOrDefault(candidate => candidate.Role == role);
                if (option != null) return "type=" + type + " reason=" + option.Reason + " detail=" + option.Detail;
            }
            return "not offered";
        }

        private unsafe void Track(string round, bool deferCheck)
        {
            int state = SiegeState();
            foreach (var probe in Current(round))
            {
                if (siegeTracks.ContainsKey(probe)) continue;
                var track = new SiegeTrack { SpawnState = state, SpawnTick = simTicks, Prompt = SiegePrompt(probe.Wanted.Role, state) };
                if (UnitAccess.TryGetById(probe.Unit.UnitId, out GameUnit* unit, out _) && unit->r_GlobalId == probe.Unit.GlobalId)
                {
                    track.SpawnX = unit->r_CurrentTilePositionX; track.SpawnY = unit->r_CurrentTilePositionY;
                    track.SpawnTargetX = unit->r_TargetTilePositionX; track.SpawnTargetY = unit->r_TargetTilePositionY;
                }
                siegeTracks[probe] = track;
            }
            if (deferCheck) uncheckedRounds.Add(round);
        }

        private IEnumerable<AutomationWait> CheckSiege(AutomationRun run)
        {
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => { CheckSiegeProbes(); return true; });
        }

        private unsafe void CheckSiegeProbes()
        {
            foreach (string round in uncheckedRounds) Fail("admissions of " + round + " never completed");
            int ordered = 0, waiting = 0, gone = 0;
            foreach (var pair in siegeTracks.Where(pair => pair.Key.Batch != "army").OrderBy(pair => pair.Value.SpawnTick))
            {
                var probe = pair.Key; var track = pair.Value;
                if (!probe.Checked) continue;
                bool alive = UnitAccess.TryGetById(probe.Unit.UnitId, out GameUnit* unit, out _) && unit->r_GlobalId == probe.Unit.GlobalId &&
                    UnitAccess.IsReallyAlive(unit);
                bool member = alive && IsMember(unit->r_TribeId, probe.Final.Group.GlobalId, probe.Unit.UnitId);
                string line = "unit=" + probe.Unit + " type=" + probe.Type + " role=" + probe.Wanted.Role + " spawnState=" + track.SpawnState +
                    " prompt=" + track.Prompt + " detail=" + (probe.Final.Detail.Length != 0 ? "'" + probe.Final.Detail + "'" : "none") +
                    " orderedState=" + track.OrderedState + " afterTicks=" + (track.OrderedTick < 0 ? -1 : track.OrderedTick - track.SpawnTick) +
                    " alive=" + alive + " member=" + member + " aiState=" + (alive ? unit->r_AIState : -1);
                if (track.OrderedTick >= 0) { ordered++; log("SIEGE-PROBE " + line); }
                else if (!alive || !member) { gone++; log("SIEGE-PROBE-GONE " + line); }
                // Joined without a route (APIShared Detail): like a Vanilla member without a route it
                // may never move; evidence only.
                else if (probe.Final.Detail.Contains("no route")) { gone++; log("SIEGE-PROBE-NO-ROUTE " + line); }
                else { waiting++; Fail("SIEGE-NEVER-ORDERED " + line); }
            }
            log("SIEGE-SUMMARY ordered=" + ordered + " neverOrdered=" + waiting + " goneBeforeOrder=" + gone + " states=" +
                string.Join(",", probedStates.OrderBy(state => state)));
        }

        // Mirror of APIShared AiControllerHandoffPolicy.SiegeControllerReachesIdleMember (evidence only).
        private static bool SiegePrompt(AiMilitaryRole role, int state)
        {
            if (state == 0) return true;
            switch (role)
            {
                case AiMilitaryRole.SiegeStorm: return state >= 3 && state <= 5;
                case AiMilitaryRole.SiegeShock: return state == 4 || state == 5;
                case AiMilitaryRole.SiegeMoat: case AiMilitaryRole.SiegeAssassins: case AiMilitaryRole.SiegeDiversion:
                case AiMilitaryRole.SiegeCover: case AiMilitaryRole.SiegeReserve: case AiMilitaryRole.SiegeWall:
                    return state == 5;
                default: return false;
            }
        }

        // FBCB9319 player-record fields from AI_SIEGE_START.md: +0x30F0 counted siege troops,
        // +0x384C ally order mode, +0x3850 requested target, +0x2BD8 siege target.
        private unsafe string SiegeContext()
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner, out GamePlayerResources* resources) || resources == null)
                return "resources=null";
            int identity = (int)resources->r_AILordMinusOne;
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            string aic = identity > 0 && identity <= aics.Length
                ? "trigger=" + aics.GetValuePointer(identity - 1)->siege_trigger_level + " rescue=" + aics.GetValuePointer(identity - 1)->siege_troops_before_will_come_to_rescue +
                  " onSite%=" + aics.GetValuePointer(identity - 1)->siege_troops_on_site_percent + " soften=" + aics.GetValuePointer(identity - 1)->siege_soften_up_delay
                : "aic=?";
            return "state=" + resources->r_AISiegeState + " counted=" + resources->N00004015 + " engineers=" + resources->r_AICurrentSiegeEngineerCount +
                " allyMode=" + resources->r_AIAllyOrderMode + " requested=" + resources->r_AIRequestedAttackTargetPlayerId +
                " siegeTarget=" + resources->r_SiegeAttackTargetPlayerId + " bonus=" + resources->r_AITargetAttackForceSize +
                " rally=" + resources->r_SiegeRallyTileX + "," + resources->r_SiegeRallyTileY + " targetLord=" + LordAlive(siegeTarget) + " " + aic;
        }
    }
}
