using System;
using System.Collections.Generic;
using System.IO;
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
    // Spawns every unit type into every role the capability offers for one AI, first in a fresh
    // world and again after a save/load (existing groups whose order may be unknown). Strict
    // checks: offered-but-rejected admissions and violated admission postconditions. Evidence
    // only: what the AI does with the recruits during the following simulation ticks.
    internal sealed partial class Fixture
    {
        internal const string Scenario = "ai-groups";
        // Simulation ticks after admission at which the controller handoff is observed.
        private static readonly int[] ObserveTicks = { 600, 3000 };
        private const int MultiPlacementCount = 4;

        private readonly string map;
        private readonly int speed;
        private readonly Guid runId;
        private readonly Action<string> log;
        private readonly List<Probe> probes = new List<Probe>();
        private readonly List<string> failures = new List<string>();
        private IAiMilitaryCapability api;
        private int owner, lordX, lordY, lordHeight;

        internal Fixture(string map, int speed, Guid runId, Action<string> log)
        { this.map = map; this.speed = speed; this.runId = runId; this.log = log; }

        internal static bool Owns(string value) => value == Scenario || value == SiegeScenario || value == SiegeLordScenario;

        private sealed class Probe
        {
            internal string Round, Batch;
            internal eChimps Type;
            internal AiMilitaryAssignment Wanted;
            internal AiUnitRef Unit;
            internal AiMilitaryAssignmentResult Initial, Final;
            internal int Callbacks, StartX, StartY, StartTargetX, StartTargetY, SpawnX, SpawnY, SpawnHeight;
            internal bool Checked, EverMoved;
        }

        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("ai-owner", TimeSpan.FromSeconds(120), WaitOwner, true);
            foreach (var step in Round("fresh", true)) yield return step;
            yield return new AutomationStep("save-reload", TimeSpan.FromSeconds(300), SaveReload);
            foreach (var step in Round("loaded", false)) yield return step;
            yield return new AutomationStep("verdict", TimeSpan.FromSeconds(10), Verdict);
        }

        private IEnumerable<AutomationStep> Round(string round, bool everyType)
        {
            yield return new AutomationStep(round + "-spawn", TimeSpan.FromSeconds(60), run => Spawn(run, round, everyType), true);
            yield return new AutomationStep(round + "-admit", TimeSpan.FromSeconds(180), run => Admit(run, round), true);
            for (int index = 0; index < ObserveTicks.Length; index++)
            {
                int ticks = ObserveTicks[index] - (index == 0 ? 0 : ObserveTicks[index - 1]);
                int total = ObserveTicks[index];
                bool last = index == ObserveTicks.Length - 1;
                yield return new AutomationStep(round + "-observe-" + total, TimeSpan.FromSeconds(240),
                    run => Observe(run, round, ticks, total, last), true);
            }
        }

        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            DisableRandomEvents();
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT }, 60)) yield return wait;
            foreach (var wait in EnterMission(run)) yield return wait;
        }

        private IEnumerable<AutomationWait> EnterMission(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            Director.instance.SetEngineFrameRate(speed);
            log("speed requested=" + speed);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }

        // A quiet world: random events (wolves, fires, plague) would kill probes nondeterministically.
        private void DisableRandomEvents()
        {
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("RandomEvents_Serp", out var info) || info?.Instance == null)
            { log("RandomEvents not loaded"); return; }
            object settings = info.Instance.GetType().GetProperty("Settings")?.GetValue(info.Instance);
            var enable = settings?.GetType().GetProperty("EnableMod");
            Require(enable != null, "RandomEvents settings shape differs; cannot keep the test world quiet");
            enable.SetValue(settings, false);
            log("RandomEvents EnableMod=false for this run (runner restores settings)");
        }

        private IEnumerable<AutomationWait> WaitOwner(AutomationRun run)
        {
            Require(ApiShared.ForMod(Plugin.Guid).TryGetAiMilitary(out api, out var diagnostic), "AI military unavailable: " + diagnostic.Reason);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            // AIC/AIV readiness grows with the first AI passes; wait until castle defense is offered.
            yield return AutomationWait.Until("AI owner with castle defense offered", () =>
            {
                try { return run.InSimulation(run.CurrentMission, FindOwner); }
                catch (InvalidOperationException) { return false; }
            });
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            log("owner=" + owner + " lord=" + lordX + "," + lordY);
        }

        private unsafe bool FindOwner()
        {
            var players = GamePlayerManagerAPI.Instance;
            for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
            {
                if (!players.IsAIPlayer(player)) continue;
                int id = players.GetLordUnitId(player);
                uint global = unchecked((uint)players.GetLordUnitGlobalId(player));
                if (global == 0 || !UnitAccess.TryGetById(id, out GameUnit* lord, out _) || lord->r_GlobalId != global ||
                    !UnitAccess.IsReallyAlive(lord)) continue;
                if (!api.GetOptions(player, eChimps.CHIMP_TYPE_ARCHER, false).Any(option =>
                    option.Role == AiMilitaryRole.CastleDefense && option.IsAvailable)) continue;
                owner = player; lordX = lord->r_CurrentTilePositionX; lordY = lord->r_CurrentTilePositionY; lordHeight = lord->r_HeightElevation;
                return true;
            }
            return false;
        }

        private IEnumerable<AutomationWait> Spawn(AutomationRun run, string round, bool everyType)
        {
            int created = run.InSimulation(run.CurrentMission, () => SpawnRound(round, everyType));
            log(round + " spawned probes=" + created);
            yield break;
        }

        private int SpawnRound(string round, bool everyType)
        {
            Require(GamePlayerManagerAPI.Instance.IsAIPlayer(owner), "AI owner changed");
            int before = probes.Count;
            var additionalDone = new HashSet<AiMilitaryRole>();
            var roleDone = new HashSet<AiMilitaryRole>();
            foreach (eChimps type in Enum.GetValues(typeof(eChimps)))
            {
                if (type == eChimps.CHIMP_TYPE_LORD) continue;
                IReadOnlyList<AiMilitaryOption> options;
                try { options = api.GetOptions(owner, type, false); }
                catch (Exception error) { log(round + " options type=" + type + " error=" + error.GetType().Name); continue; }
                // Only military types: None alone is offered for civilians and keeps ordinary behavior.
                if (options == null || !options.Any(option => option.IsAvailable && option.Role != AiMilitaryRole.None)) continue;
                foreach (var option in options)
                {
                    if (!option.IsAvailable)
                    {
                        log(round + " offer type=" + type + " role=" + option.Role + " available=False reason=" + option.Reason + " detail=" + option.Detail);
                        continue;
                    }
                    // The loaded round covers every role once (with the first offered type) to keep it short.
                    if (!everyType && !roleDone.Add(option.Role)) continue;
                    SpawnBatch(round, "single", type, new AiMilitaryAssignment(option.Role, AiRecruitmentMode.Regular), 1);
                    if (additionalDone.Add(option.Role))
                        SpawnBatch(round, "additional", type, new AiMilitaryAssignment(option.Role, AiRecruitmentMode.Additional), 1);
                }
            }
            // Shift placement: several units in one capture scope, distributed by the group resolver.
            foreach (var role in new[] { AiMilitaryRole.CastleDefense, AiMilitaryRole.DefensePatrol, AiMilitaryRole.EconomyProtection })
                if (api.GetOptions(owner, eChimps.CHIMP_TYPE_ARCHER, false).Any(option => option.Role == role && option.IsAvailable))
                    SpawnBatch(round, "multi", eChimps.CHIMP_TYPE_ARCHER, new AiMilitaryAssignment(role, AiRecruitmentMode.Regular), MultiPlacementCount);
            return probes.Count - before;
        }

        private unsafe void SpawnBatch(string round, string batch, eChimps type, AiMilitaryAssignment wanted, int count)
        {
            var batchProbes = new List<Probe>();
            Require(api.TryBeginSpawnScope(owner, type, wanted, false, false, result => OnCompleted(batchProbes, result),
                out IAiMilitarySpawnScope scope, out string reason), "scope " + type + "/" + wanted.Role + ": " + reason);
            try
            {
                for (int index = 0; index < count; index++)
                {
                    if (!TrySpawnTile(out int x, out int y, out int height))
                    { Fail(round + " no walkable spawn tile type=" + type + " role=" + wanted.Role); continue; }
                    int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, height, type));
                    GameUnit* unit = null;
                    if (id <= 0 || !UnitAccess.TryGetById(id, out unit, out _) || unit->r_GlobalId == 0)
                    { Fail(round + " create failed type=" + type + " role=" + wanted.Role); continue; }
                    var probe = new Probe { Round = round, Batch = batch, Type = type, Wanted = wanted, Unit = new AiUnitRef(id, unit->r_GlobalId),
                        SpawnX = x, SpawnY = y, SpawnHeight = height };
                    lock (probes) { batchProbes.Add(probe); probes.Add(probe); }
                }
                var initial = scope.Complete();
                foreach (var result in initial)
                {
                    var probe = batchProbes.FirstOrDefault(candidate => candidate.Unit.Equals(result.Unit));
                    if (probe == null) { Fail(round + " initial result for uncaptured unit " + result.Unit); continue; }
                    probe.Initial = result;
                    if (result.Status == AiMilitaryAssignmentStatus.Rejected && probe.Final == null) probe.Final = result;
                }
                foreach (var probe in batchProbes)
                    if (probe.Initial == null) Fail(round + " no initial result unit=" + probe.Unit + " type=" + type + " role=" + wanted.Role);
            }
            finally { scope.Dispose(); }
        }

        // The lord can stand on a keep tile from which no route exists (run 17.11.2026 23:03: every
        // recruit stuck, routes raw=0). Spawn on the nearest walkable free tile of a grid around the
        // keep door instead, like PerfProbe's spawn action.
        private int spawnIndex;
        private bool TrySpawnTile(out int x, out int y, out int height)
        {
            var tiles = GameTileManagerAPI.Instance;
            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(owner);
            int index = spawnIndex++;
            var position = tiles.GetNearestUnoccupiedTile(door.X + index % 15 - 7, door.Y + index / 15 % 15 - 7, 12);
            int tile = tiles.GetTileId(position.X, position.Y);
            x = position.X; y = position.Y; height = 0;
            if (!tiles.IsValidTileId(tile) || !tiles.IsTileWalkableAndUnoccupied(tile)) return false;
            height = tiles.GetTileHeight(tile);
            return true;
        }

        // Called synchronously under the engine monitor at the completed engine-run safe point.
        private void OnCompleted(List<Probe> batch, AiMilitaryAssignmentResult result)
        {
            lock (probes)
            {
                var probe = batch.FirstOrDefault(candidate => candidate.Unit.Equals(result.Unit));
                if (probe == null) { failures.Add("callback for unknown unit " + result.Unit); return; }
                probe.Callbacks++; probe.Final = result;
                if (probe.Callbacks != 1) failures.Add("callback repeated " + result.Unit);
            }
        }

        private IEnumerable<AutomationWait> Admit(AutomationRun run, string round)
        {
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until(round + " admissions completed", () =>
            {
                lock (probes) return probes.Where(probe => probe.Round == round).All(probe => probe.Final != null);
            });
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => { CheckAdmissions(round); return true; });
        }

        private unsafe void CheckAdmissions(string round)
        {
            int applied = 0, rejected = 0, handoff = 0;
            foreach (var probe in Current(round))
            {
                var result = probe.Final;
                string head = round + " " + probe.Batch + " unit=" + probe.Unit + " type=" + probe.Type + " role=" + probe.Wanted.Role +
                    " mode=" + probe.Wanted.RecruitmentMode;
                if (result.Status != AiMilitaryAssignmentStatus.Applied)
                {
                    rejected++;
                    // Only roles whose controller is not proven to command joined members may stay
                    // explicit after a load (APIShared AiControllerHandoffPolicy, audit 18.11.2026).
                    bool documented = round == "loaded" && result.Reason == AiMilitaryReason.CurrentOrderUnknown &&
                        !HandoffRole(probe.Wanted.Role, SiegeState());
                    string line = head + " status=" + result.Status + " reason=" + result.Reason + " detail=" + result.Detail;
                    if (documented) log("EXPECTED-REJECT " + line);
                    else Fail("REJECTED " + line);
                    log("SPAWN-CONTEXT " + head + " " + SpawnContext(probe));
                    continue;
                }
                applied++;
                if (result.Detail.Length != 0) handoff++;
                if (result.Detail.Contains("no route")) log("NO-ROUTE-JOIN " + head + " " + SpawnContext(probe) + " detail=" + result.Detail);
                bool ok = UnitAccess.TryGetById(probe.Unit.UnitId, out GameUnit* unit, out _) && unit->r_GlobalId == probe.Unit.GlobalId &&
                    UnitAccess.IsReallyAlive(unit) && unit->r_ControllableForPlayerId == owner;
                int role = ok ? unit->r_AITribeRole : -1, group = ok ? unit->r_TribeId : -1;
                bool member = ok && IsMember(group, result.Group.GlobalId, probe.Unit.UnitId);
                bool additional = api.IsExcludedFromRecruitmentQuota(probe.Unit);
                bool roleOk = role == NativeRole(probe.Wanted.Role);
                bool groupOk = group == result.Group.TribeId && member;
                bool markerOk = additional == (probe.Wanted.RecruitmentMode == AiRecruitmentMode.Additional);
                string evidence = head + " group=" + result.Group.TribeId + "/" + result.Group.GlobalId + " actualRole=" + role +
                    " actualGroup=" + group + " member=" + member + " additional=" + additional + " handoff=" + (result.Detail.Length != 0);
                if (ok && roleOk && groupOk && markerOk)
                {
                    probe.Checked = true;
                    probe.StartX = unit->r_CurrentTilePositionX; probe.StartY = unit->r_CurrentTilePositionY;
                    probe.StartTargetX = unit->r_TargetTilePositionX; probe.StartTargetY = unit->r_TargetTilePositionY;
                    log("APPLIED " + evidence);
                }
                else Fail("POSTCONDITION " + evidence + " alive=" + ok);
            }
            log(round + " admissions applied=" + applied + " rejected=" + rejected + " handoff=" + handoff);
        }

        // Evidence for a rejected or routeless admission: the spawn tile (the unit itself may already
        // be removed), its path component and height, and whether the keep door is reachable from it.
        private string SpawnContext(Probe probe)
        {
            var tiles = GameTileManagerAPI.Instance;
            Span<ushort> layer = tiles.GetPathConnectionLayer();
            int tile = tiles.GetTileId(probe.SpawnX, probe.SpawnY);
            int from = tile > 0 && tile < layer.Length ? (short)layer[tile] : 0;
            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(owner);
            int doorTile = tiles.GetTileId(door.X, door.Y);
            int to = doorTile > 0 && doorTile < layer.Length ? (short)layer[doorTile] : 0;
            bool reachable = from > 0 && to > 0 && (from == to || GamePathingManagerAPI.Instance.FindNextComponentTowardDestination(owner, from, to,
                PathConnectionQueryMode.ExcludeLadderClimb) != 0);
            return "spawn=" + probe.SpawnX + "," + probe.SpawnY + " spawnHeight=" + probe.SpawnHeight + " component=" + from +
                " keepDoor=" + door.X + "," + door.Y + " doorComponent=" + to + " doorReachable=" + reachable;
        }

        private IEnumerable<AutomationWait> Observe(AutomationRun run, string round, int ticks, int total, bool last)
        {
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, ticks);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => { ObserveRound(round, total, last); return true; });
        }

        // Evidence of the AI's own handling after admission. Not a pass criterion: the AI may move,
        // regroup or lose units legitimately; patrol and strength thresholds can also delay orders.
        private unsafe void ObserveRound(string round, int total, bool last)
        {
            var summary = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
            foreach (var probe in Current(round).Where(probe => probe.Checked))
            {
                bool alive = UnitAccess.TryGetById(probe.Unit.UnitId, out GameUnit* unit, out _) && unit->r_GlobalId == probe.Unit.GlobalId &&
                    UnitAccess.IsReallyAlive(unit);
                int x = alive ? unit->r_CurrentTilePositionX : -1, y = alive ? unit->r_CurrentTilePositionY : -1;
                int tx = alive ? unit->r_TargetTilePositionX : -1, ty = alive ? unit->r_TargetTilePositionY : -1;
                int role = alive ? unit->r_AITribeRole : -1, group = alive ? unit->r_TribeId : -1;
                bool member = alive && group == probe.Final.Group.TribeId && IsMember(group, probe.Final.Group.GlobalId, probe.Unit.UnitId);
                bool roleKept = role == NativeRole(probe.Wanted.Role);
                int distance = alive ? Math.Max(Math.Abs(x - probe.StartX), Math.Abs(y - probe.StartY)) : -1;
                probe.EverMoved |= distance > 1 || alive && (tx != probe.StartTargetX || ty != probe.StartTargetY);
                if (!summary.TryGetValue(probe.Wanted.Role.ToString(), out int[] counts)) summary[probe.Wanted.Role.ToString()] = counts = new int[5];
                counts[0]++; if (alive) counts[1]++; if (member) counts[2]++; if (roleKept) counts[3]++; if (probe.EverMoved) counts[4]++;
                if (last || !alive || !member)
                    log("OBSERVE " + round + " ticks=" + total + " unit=" + probe.Unit + " type=" + probe.Type + " role=" + probe.Wanted.Role +
                        " batch=" + probe.Batch + " alive=" + alive + " member=" + member + " roleKept=" + roleKept + " actualRole=" + role +
                        " start=" + probe.StartX + "," + probe.StartY + " now=" + x + "," + y + " distance=" + distance +
                        " target=" + probe.StartTargetX + "," + probe.StartTargetY + "->" + tx + "," + ty + " moved=" + probe.EverMoved +
                        " aiState=" + (alive ? unit->r_AIState : -1) + " handoff=" + (probe.Final.Detail.Length != 0));
            }
            foreach (var row in summary)
                log("OBSERVE-SUMMARY " + round + " ticks=" + total + " role=" + row.Key + " probes=" + row.Value[0] + " alive=" + row.Value[1] +
                    " sameGroup=" + row.Value[2] + " roleKept=" + row.Value[3] + " moved=" + row.Value[4]);
        }

        private static string Tick() => APIShared.Simulation.SimulationTickEvents.TryGetCurrentTick(out int tick) ? tick.ToString() : "?";

        private IEnumerable<AutomationWait> Verdict(AutomationRun run)
        {
            lock (probes)
            {
                log("VERDICT probes=" + probes.Count + " failures=" + failures.Count);
                foreach (string failure in failures) log("FAILURE " + failure);
                Require(failures.Count == 0, failures.Count + " admission failures; see FAILURE lines");
            }
            yield break;
        }

        private List<Probe> Current(string round) { lock (probes) return probes.Where(probe => probe.Round == round).ToList(); }

        private void Fail(string text) { lock (probes) failures.Add(text); log("FAIL " + text); }

        // Expected handoff roles: defensive roles and harassment always, siege roles while the owner's
        // AI siege state is 0 (3C2E0 then commands every siege group as a whole on each pass).
        private static bool HandoffRole(AiMilitaryRole role, int siegeState)
        {
            switch (role)
            {
                case AiMilitaryRole.CastleDefense: case AiMilitaryRole.DefensePatrol: case AiMilitaryRole.HomeMoat:
                case AiMilitaryRole.EconomyProtection: case AiMilitaryRole.Intervention: case AiMilitaryRole.Harassment:
                    return true;
                case AiMilitaryRole.HarassmentEngineers: case AiMilitaryRole.None:
                    return false;
                // Mirrors APIShared AiControllerHandoffPolicy: siege roles join in every known siege
                // state (0-9), waiting for the next phase where the controller does not reach them sooner.
                default:
                    return siegeState >= 0 && siegeState <= 9;
            }
        }

        private unsafe int SiegeState() =>
            GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner, out GamePlayerResources* resources) && resources != null
                ? (int)resources->r_AISiegeState : -1;

        // FBCB9319 native GameTribe member bitset ABI (+0x36); same contract as UiAcceptance/Spectator.
        private static unsafe bool IsMember(int group, uint groupGlobal, int unitId)
        {
            if (group <= 0 || unitId <= 0 || unitId >= 10000 ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(group, out GameTribe* tribe) || tribe == null ||
                tribe->r_GlobalId != groupGlobal || tribe->r_AliveState != AliveState.IsAlive) return false;
            return ((((ushort*)((byte*)tribe + 0x36))[unitId >> 4] >> (unitId & 15)) & 1) != 0;
        }

        // FBCB9319 native unit-role ABI, identical to Spectator's diagnostic expectations.
        private static int NativeRole(AiMilitaryRole role)
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

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
