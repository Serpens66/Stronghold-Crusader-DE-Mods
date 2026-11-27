using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.ModSettings;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UnitRelocationAcceptance
{
    [BepInPlugin(Guid, "Unit Relocation Acceptance", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.79.0")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.250")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "UnitRelocationAcceptance_Serp";
        private void Awake() => Fixture.Initialize(Logger);
    }

    // The static fixture and APIShared's automation/tick registrations survive
    // startup cleanup. No polling component, teardown or private production entry.
    internal static partial class Fixture
    {
        private const string MainGuid = "BugfixesAndQoL_Serp";
        private static ManualLogSource log;
        private static ModApiClient api;
        private static string runId, save;
        private static Action pending;
        private static Exception error;
        private static int initialized, completed, policyCalls, checks;
        private static UnitRelocationOrigin lastOrigin;
        private static UnitRelocationPolicyRegistration veto;
        private static int owner, soldier, lord;
        private static long oldSession, oldGeneration;
        private static UnitRelocationRequest savedRequest;
        private static Actor savedActor;
        private static bool expectLordPolicy;
        private static int targetX, targetY, alternateX, alternateY;
        private struct Actor
        {
            internal int Id, Owner, Tribe, X, Y, Tile;
            internal uint Global, TribeGlobal;
            internal uint Health;
            internal eChimps Type;
        }

        internal static void Initialize(ManualLogSource logger)
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;
            log = logger;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Plugin.Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !OwnScenario(lines[1])) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var id) || id == System.Guid.Empty)
            { Info("invalid owned request; left unconsumed"); return; }
            runId = id.ToString("N");
            api = ApiShared.ForMod(Plugin.Guid);
            api.WhenReady(_ =>
            {
                try
                {
                    ValidateReadLayouts();
                    Require(UnitRelocationService.EnsureAvailable(Plugin.Guid, out string reason), reason);
                    Require(UnitRelocationService.TryRegisterPolicy(Plugin.Guid, "soldier-veto", eChimps.CHIMP_TYPE_SPEARMAN,
                        context => { policyCalls++; lastOrigin = context.Origin; return true; }, out veto, out reason), reason);
                    Require(SimulationTickEvents.TryRegister(Plugin.Guid, "commands", Tick, out reason, 2000), reason);
                    Require(api.Ui.TryPost(() =>
                    {
                        File.Delete(request);
                        fixtureUnityThread = Thread.CurrentThread.ManagedThreadId;
                        api.Automation.Start(id, ScenarioSteps(lines[1]), log, result =>
                        {
                            veto.SetEnabled(false); waitBridgeOutcome = false; Interlocked.Exchange(ref pending, null);
                            Info("FINISHED checks=" + checks + " status=" + result.Status + " detail=" + result.Detail);
                        }, quitWhenDone: true);
                    }), "automation Unity dispatch unavailable");
                }
                catch (Exception ex) { Info("INITIALIZATION_FAILED " + ex); }
            });
        }

        private static void ValidateReadLayouts()
        {
            const string hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
            string path = Path.Combine(Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var input = File.OpenRead(path)) using (var sha = SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "") == hash, "unsupported native read layout");
            Require(Marshal.SizeOf(typeof(GameUnit)) == GameUnitManager.UnitRecordByteSize && GameUnitManager.UnitRecordByteSize == 0x490,
                "unit read stride changed");
            Require(Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_InterestingTodo4MaybeRandomPathing)).ToInt32() == 0x29E,
                "occupancy-link read offset changed");
            Require(Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_CurrentPositionTileId)).ToInt32() == 0xD0,
                "packedCurrent read offset changed");
            Require(Marshal.SizeOf(typeof(GamePlayerResources)) == 0x583C &&
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_AIVVillageSlotIndex)).ToInt32() == 0x22FC,
                "AIV owner resource view changed");
            Require(Marshal.SizeOf(typeof(AivBuildStep)) == 0x0C && Marshal.SizeOf(typeof(AivVillageState)) == 0x6D98 &&
                Marshal.OffsetOf(typeof(AivBuildStep), nameof(AivBuildStep.BuildingType)).ToInt32() == 2 &&
                Marshal.OffsetOf(typeof(AivBuildStep), nameof(AivBuildStep.MapTileIdOrBufferIndex)).ToInt32() == 8 &&
                Marshal.OffsetOf(typeof(AivVillageState), nameof(AivVillageState.BuildStepsBuffer)).ToInt32() == 0x34 &&
                Marshal.OffsetOf(typeof(AivVillageState), nameof(AivVillageState.OrderedMapTileCount)).ToInt32() == 0x6D94,
                "controlled AIV frame/header layout changed");
        }

        private static IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("fresh-on", TimeSpan.FromSeconds(180), run => Start(run, true));
            yield return new AutomationStep("on-relocation", TimeSpan.FromSeconds(60), run => Exercise(run, true), true);
            yield return new AutomationStep("automatic-aiv-rescue", TimeSpan.FromSeconds(120), AutomaticBridge, true);
            yield return new AutomationStep("fresh-off", TimeSpan.FromSeconds(180), run => Start(run, false));
            yield return new AutomationStep("off-relocation", TimeSpan.FromSeconds(60), run => Exercise(run, false), true);
            yield return new AutomationStep("save", TimeSpan.FromSeconds(45), Save, true);
            yield return new AutomationStep("reload", TimeSpan.FromSeconds(120), Reload);
            yield return new AutomationStep("loaded-relocation", TimeSpan.FromSeconds(45), Loaded, true);
        }

        private static IEnumerable<AutomationWait> Start(AutomationRun run, bool enabled)
        {
            if (run.CurrentMission != null) MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            yield return AutomationWait.Until("front end before settings", () => run.CurrentMission == null &&
                MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_Frontend_MainMenu);
            Require(ModSettingsApplication.Endpoints.TryGetValue(MainGuid, out var model), "BQoL settings endpoint absent");
            model.System_LoadModDefaults();
            WriteSetting(model, "EnableMod", true);
            WriteSetting(model, "EnableLordReturnTeleportFix", enabled);
            WriteSetting(model, "EnableTrappedUnitRescue", true);
            expectLordPolicy = enabled;
            Info("FRONTEND_SETTINGS lord=" + enabled + " master=true rescue=true");
            foreach (var wait in run.StartSkirmish("test AI.map", new[] { AILords.SK_RAT }, null, PrepareLobby)) yield return wait;
        }

        private static void PrepareLobby()
        {
            var view = MainViewModel.Instance.FRONTMultiplayer;
            var room = view?.currentLobby;
            Require(room != null && room.isHost && FRONT_Multiplayer.skirmishGame && !FRONT_Multiplayer.coopGame, "local lobby missing");
            var members = room.members.Where(m => !m.dummyToBeKicked).ToArray();
            Require(members.Length == 2 && members.Count(m => m.SkirmishHumanMember) == 1, "fixture roster is not one human/one Rat");
            var human = members.Single(m => m.SkirmishHumanMember);
            var opponent = members.Single(m => !m.SkirmishHumanMember);
            Require(opponent.GetLordType() == (int)AILords.SK_RAT - 1 && string.IsNullOrEmpty(opponent.customLordName), "wrong opponent");
            var setupField = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(setupField != null && setupField.FieldType == typeof(EngineInterface.MultiplayerSetupData), "lobby setup contract changed");
            var setup = (EngineInterface.MultiplayerSetupData)setupField.GetValue(view);
            var locations = GameData.Instance.Keep_Locations;
            var places = Enumerable.Range(0, Math.Min(setup.start_keep_location_order.Length, locations.GetLength(0)))
                .Where(i => locations[i, 0] >= 0 && locations[i, 1] >= 0).Take(2).ToArray();
            Require(places.Length == 2, "fixture needs two Keep positions");
            owner = room.getThisPlayerFromSteamID(human.GetSteamID());
            int ai = room.getThisPlayerFromSteamID(opponent.GetSteamID());
            for (int i = 0; i < setup.start_keep_location_order.Length; i++) setup.start_keep_location_order[i] = -10;
            setup.start_keep_location_order[places[0]] = owner - 1;
            setup.start_keep_location_order[places[1]] = ai - 1;
            setup.peacetime = 60; setup.advopt_pre_build = 0;
            room.setTeam(human, 1); room.setTeam(opponent, 2);
            int ratType = (int)AILords.SK_RAT - 1;
            short[] data = AIVLoader.getAIVData(ratType, 0, false, false);
            Require(data != null && data.Length > 0 && view.AIVs != null && view.AIVs.Length >= ai, "Rat Default1 AIV unavailable");
            var original = CustomisationFileManager.Instance.getLordAIVList(ratType)?.Where(a => a != null && a.builtIn && a.lordType == ratType &&
                a.data != null && a.data.SequenceEqual(data)).OrderBy(a => a.AIVName, StringComparer.Ordinal).FirstOrDefault();
            Require(original != null, "Rat Default1 catalogue metadata missing");
            var selection = view.AIVs[ai - 1]; selection.Init(ratType, string.Empty);
            selection.builtIn = false; selection.rotation = 0;
            selection.aivs.Add(new CustomisationFileManager.CustomAIV { lordType = original.lordType, AIVName = original.AIVName,
                builtIn = original.builtIn, checksum = original.checksum, path = original.path, data = (short[])data.Clone() });
            Require(selection.builtInLord && selection.lordConfig == null && selection.aivs.Count == 1, "Rat AIC/AIV selection did not settle");
            var bytes = new byte[data.Length * sizeof(short)]; Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            using (var sha = SHA256.Create()) Info("LOBBY_AI AIC=vanilla-Rat AIV=Default1 rotation=auto SHA256=" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""));
            typeof(FRONT_Multiplayer).GetMethod("UpdateHostInfo", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, new object[] { false });
            Require(setup.peacetime == 60 && setup.advopt_pre_build == 0, "fixture lobby settings changed");
            foreach (int place in places) Info("LOBBY_KEEP place=" + place + " at=" + locations[place, 0] + "," + locations[place, 1] +
                " player=" + (setup.start_keep_location_order[place] + 1));
        }

        private static IEnumerable<AutomationWait> Ready(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            Director.instance.SetEngineFrameRate(40);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 20);
            run.InSimulation(run.CurrentMission, () => { LogLordInventory(); return true; });
            // Vanilla creates missing map Lords later than the first20 startup ticks.
            // Wait for the actual native lifetime; never manufacture a Lord/home identity.
            yield return AutomationWait.Until("native human Lord initialized", () => run.InSimulation(run.CurrentMission, HumanLordReady));
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Require(UnitRelocationService.IsAvailable, "shared relocation unavailable in started world");
            Require(ModSettingsApplication.Endpoints.TryGetValue(MainGuid, out var model) &&
                (bool)model.GetType().GetProperty("EnableLordReturnTeleportFix").GetValue(model, null) == expectLordPolicy,
                "mission preset changed Lord policy setting");
        }

        private static IEnumerable<AutomationWait> Exercise(AutomationRun run, bool enabled)
        {
            foreach (var wait in Ready(run)) yield return wait;
            run.InSimulation(run.CurrentMission, () => { SetupActors(); return true; });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 4);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () =>
            {
                Actor before = Read(soldier);
                var request = Request(before, targetX, targetY);
                Require(!UnitRelocationService.TryRelocate(Plugin.Guid, request, out var result, out _) && !result.NativeInvoked,
                    "an engine monitor without tick admission unexpectedly allowed relocation");
                RequireSame(before, Read(soldier), true, "unsafe-context refusal"); Case("unsafe-context", result.Status.ToString());
                return true;
            });
            foreach (var wait in Stage(run, () => RunCommands(enabled))) yield return wait;
        }

        private static unsafe bool HumanLordReady()
        {
            int local = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (local != owner || GamePlayerManagerAPI.Instance.IsAIPlayer(local)) return false;
            for (int id = GameUnitManager.FirstLiveUnitId; id <= GameUnitManager.LastLiveUnitId; id++)
                if (UnitAccess.TryGetById(id, out GameUnit* u, out _) && UnitAccess.IsReallyAlive(u) &&
                    u->r_UnitChimp == eChimps.CHIMP_TYPE_LORD && u->r_ControllableForPlayerId == owner) return true;
            return false;
        }

        private static unsafe void LogLordInventory()
        {
            Info("WORLD_LOCAL pinned=" + owner + " actual=" + GamePlayerManagerAPI.Instance.GetLocalPlayerId());
            for (int p = 1; p <= 8; p++)
                if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(p, out GamePlayerResources* r))
                    Info("WORLD_PLAYER id=" + p + " keep=" + r->r_KeepId + " lord=" + r->r_LordUnitId + ":" + r->r_LordUnitGlobalId);
            for (int id = GameUnitManager.FirstLiveUnitId; id <= GameUnitManager.LastLiveUnitId; id++)
                if (UnitAccess.TryGetById(id, out GameUnit* u, out _) && u->r_UnitChimp == eChimps.CHIMP_TYPE_LORD)
                    Info("WORLD_LORD id=" + id + " gid=" + u->r_GlobalId + " owner=" + u->r_ControllableForPlayerId + " alive=" + UnitAccess.IsReallyAlive(u));
        }

        private static unsafe void SetupActors()
        {
            Require(owner == GamePlayerManagerAPI.Instance.GetLocalPlayerId(), "pinned human/local identity mismatch"); lord = 0;
            for (int id = GameUnitManager.FirstLiveUnitId; id <= GameUnitManager.LastLiveUnitId; id++)
                if (UnitAccess.TryGetById(id, out GameUnit* u, out _) && UnitAccess.IsReallyAlive(u) &&
                    u->r_UnitChimp == eChimps.CHIMP_TYPE_LORD && u->r_ControllableForPlayerId == owner) { lord = id; break; }
            Require(lord != 0 && !GamePlayerManagerAPI.Instance.IsAIPlayer(owner), "human Lord absent");
            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(owner);
            var spots = new List<Tuple<int, int>>();
            for (int y = door.Y - 40; y <= door.Y + 40 && spots.Count < 5; y += 3)
                for (int x = door.X - 40; x <= door.X + 40 && spots.Count < 5; x += 3)
                    if (Ground(x, y)) spots.Add(Tuple.Create(x, y));
            Require(spots.Count == 5, "five verified ordinary empty sites unavailable");
            targetX = spots[1].Item1; targetY = spots[1].Item2; alternateX = spots[2].Item1; alternateY = spots[2].Item2;
            var tiles = GameTileManagerAPI.Instance;
            soldier = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, spots[0].Item1, spots[0].Item2,
                tiles.GetTileHeight(tiles.GetTileId(spots[0].Item1, spots[0].Item2)), eChimps.CHIMP_TYPE_SPEARMAN));
            Require(soldier >= GameUnitManager.FirstLiveUnitId && soldier <= GameUnitManager.LastLiveUnitId, "soldier spawn refused");
            Info("ACTORS owner=" + owner + " soldier=" + soldier + " lord=" + lord + " target=" + targetX + "," + targetY +
                " alternate=" + alternateX + "," + alternateY);
        }

        private static unsafe void RunCommands(bool enabled)
        {
            Require(WorldMutationEvents.TryGetCurrentWorld(out oldSession, out oldGeneration), "world identity missing");
            Actor before = Read(soldier);
            int oldTile = before.Tile;
            VerifyRelocation(before, targetX, targetY, "ordinary-soldier");
            Require(Occurrences(oldTile, soldier) == 0, "old source retained moved soldier");
            Actor moved = Read(soldier);
            var stale = new UnitRelocationRequest(oldSession, oldGeneration, moved.Id, unchecked(moved.Global + 1), moved.Type, moved.Owner, alternateX, alternateY);
            Require(!UnitRelocationService.TryRelocate(Plugin.Guid, stale, out var result, out _) && !result.NativeInvoked,
                "stale lifetime unexpectedly invoked Original");
            RequireSame(moved, Read(soldier), true, "stale lifetime"); Case("stale-lifetime", result.Status.ToString());
            stale = new UnitRelocationRequest(oldSession, oldGeneration + 1, moved.Id, moved.Global, moved.Type, moved.Owner, alternateX, alternateY);
            Require(!UnitRelocationService.TryRelocate(Plugin.Guid, stale, out result, out _) && !result.NativeInvoked,
                "stale world unexpectedly invoked Original");
            RequireSame(moved, Read(soldier), true, "stale world"); Case("stale-world", result.Status.ToString());
            int calls = policyCalls;
            veto.SetEnabled(true);
            try
            {
                Require(!UnitRelocationService.TryRelocate(Plugin.Guid, Request(moved, alternateX, alternateY), out result, out _) &&
                    result.Status == UnitRelocationStatus.Vetoed && !result.NativeInvoked && !result.NativeReturned,
                    "exact-type policy failed to veto");
                Require(policyCalls == calls + 1 && lastOrigin == UnitRelocationOrigin.ExplicitApiRequest, "policy origin/count mismatch");
                RequireSame(moved, Read(soldier), true, "policy veto"); Case("typed-policy-veto", result.Status.ToString());
                Actor lordBefore = Read(lord);
                VerifyRelocation(lordBefore, alternateX, alternateY, "Lord-away");
                Require(policyCalls == calls + 1, "Spearman-only policy saw a Lord");
            }
            finally { veto.SetEnabled(false); }
            Actor away = Read(lord);
            Require(UnitAccess.TryGetById(lord, out GameUnit* l, out _), "Lord disappeared");
            int hx = l->r_LinkedProductionBuildingTileEndX, hy = l->r_LinkedProductionBuildingTileEndY;
            bool success = UnitRelocationService.TryRelocate(Plugin.Guid, Request(away, hx, hy), out result, out string reason);
            if (enabled)
            {
                Require(!success && result.Status == UnitRelocationStatus.Vetoed && !result.NativeInvoked, "BQoL Lord policy failed: " + reason);
                RequireSame(away, Read(lord), true, "Lord home veto"); Case("Lord-home-ON", result.Status.ToString());
            }
            else
            {
                Require(success && result.Status == UnitRelocationStatus.VerifiedRelocated && result.NativeInvoked && result.NativeReturned,
                    "Lord home OFF failed: " + reason);
                Actor after = Read(lord); RequireSame(away, after, false, "Lord home OFF");
                Require(after.X == hx && after.Y == hy && Occurrences(after.Tile, lord) == 1, "Lord home OFF poststate");
                Case("Lord-home-OFF", result.Status.ToString());
            }
            savedActor = Read(soldier);
            savedRequest = Request(savedActor, targetX, targetY);
            Info("COMMANDS_COMPLETED lordPolicy=" + enabled + " nativeTick=" + (SimulationTickEvents.TryGetCurrentTick(out int tick) ? tick : -1));
        }

        private static IEnumerable<AutomationWait> Save(AutomationRun run)
        {
            save = Path.Combine(ConfigSettings.GetSavesPath(), "SerpAcceptance-" + runId + ".sav");
            Require(!File.Exists(save), "run-owned save exists");
            yield return AutomationWait.Until("native safe save", () => Director.instance.SafeToSave());
            Director.instance.FinishedSaving();
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1); int x = -1, y = -1;
            GameMap.instance.getRotationCentre(ref x, ref y);
            Require(EngineInterface.SaveSaveGame(save, screen.x, screen.y, x, y, false, false, false), "save rejected");
            Require(File.Exists(save) && new FileInfo(save).Length > 0, "save missing");
            Info("SAVE_WRITTEN path=" + save + " world=" + oldSession + "/" + oldGeneration);
        }

        private static IEnumerable<AutomationWait> Reload(AutomationRun run)
        {
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            yield return AutomationWait.Until("retired mission before reload", () => run.CurrentMission == null &&
                MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_Frontend_MainMenu);
            SavegameModSettings.PrepareLoad(save, true);
            foreach (var wait in run.LoadSave(save)) yield return wait;
        }

        private static IEnumerable<AutomationWait> Loaded(AutomationRun run)
        {
            foreach (var wait in Ready(run)) yield return wait;
            foreach (var wait in Stage(run, () =>
            {
                Require(WorldMutationEvents.TryGetCurrentWorld(out long session, out long generation) &&
                    (session != oldSession || generation != oldGeneration), "load retained old world identity");
                Actor before = Read(savedRequest.UnitId);
                RequireSame(savedActor, before, false, "saved original lifetime/group/health");
                Require(!UnitRelocationService.TryRelocate(Plugin.Guid, savedRequest, out var result, out _) && !result.NativeInvoked,
                    "retired-world request invoked Original");
                RequireSame(before, Read(before.Id), true, "retired-world refusal"); Case("loaded-old-world", result.Status.ToString());
                VerifyRelocation(before, alternateX, alternateY, "loaded-current-world");
            })) yield return wait;
        }

        private static IEnumerable<AutomationWait> Stage(AutomationRun run, Action action)
        {
            error = null; Volatile.Write(ref completed, 0);
            Require(Interlocked.CompareExchange(ref pending, action, null) == null, "another staged command pending");
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until("staged native tick command", () =>
            { if (error != null) throw error; return Volatile.Read(ref completed) != 0; });
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }

        private static void Tick(int tick)
        {
            Action action = Interlocked.Exchange(ref pending, null);
            try
            {
                ObserveBridgeOutcome();
                if (action != null) action();
            }
            catch (Exception ex) { error = ex; }
            finally { if (action != null) Volatile.Write(ref completed, 1); }
        }

        private static void VerifyRelocation(Actor before, int x, int y, string name)
        {
            Require(UnitRelocationService.TryRelocate(Plugin.Guid, Request(before, x, y), out var result, out string reason) &&
                result.Status == UnitRelocationStatus.VerifiedRelocated && result.NativeInvoked && result.NativeReturned,
                name + " failed: " + result.Status + ": " + reason);
            Actor after = Read(before.Id); RequireSame(before, after, false, name);
            Require(after.X == x && after.Y == y && Occurrences(after.Tile, before.Id) == 1, name + " pose/occupancy failed");
            Case(name, result.Status.ToString());
        }

        private static UnitRelocationRequest Request(Actor actor, int x, int y)
        {
            Require(WorldMutationEvents.TryGetCurrentWorld(out long session, out long generation), "world unavailable");
            return new UnitRelocationRequest(session, generation, actor.Id, actor.Global, actor.Type, actor.Owner, x, y);
        }
        private static unsafe Actor Read(int id)
        {
            Require(UnitAccess.TryGetById(id, out GameUnit* u, out _) && UnitAccess.IsReallyAlive(u), "live original missing: " + id);
            uint tribeGlobal = 0;
            if (u->r_TribeId != 0)
            {
                Require(GameTribeManagerAPI.Instance.TryGetTribeById(u->r_TribeId, out GameTribe* tribe) && tribe != null,
                    "original tribe unavailable");
                tribeGlobal = tribe->r_GlobalId;
            }
            return new Actor { Id = id, Global = u->r_GlobalId, TribeGlobal = tribeGlobal, Type = u->r_UnitChimp, Owner = u->r_ControllableForPlayerId,
                Tribe = u->r_TribeId, Health = u->r_CurrentHealth, X = u->r_CurrentTilePositionX, Y = u->r_CurrentTilePositionY,
                Tile = checked((int)u->r_CurrentPositionTileId) };
        }
        private static void RequireSame(Actor before, Actor after, bool pose, string name)
        {
            Require(before.Id == after.Id && before.Global == after.Global && before.Type == after.Type && before.Owner == after.Owner &&
                before.Health == after.Health && before.Tribe == after.Tribe && before.TribeGlobal == after.TribeGlobal && (!pose || before.X == after.X && before.Y == after.Y && before.Tile == after.Tile),
                name + " changed original identity/health/tribe" + (pose ? "/pose" : ""));
        }
        private static bool Ground(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            if (x < 0 || y < 0 || !tiles.IsTileInsideMapBounds(x, y)) return false;
            int tile = tiles.GetTileId(x, y);
            // Audited ordinary-ground exclusion mask, same CURRENT hash as the rescue native audit.
            return ((uint)tiles.GetTilePropertyFlag(tile) & 0x5A7015B1u) == 0 && tiles.GetTileBuildingId(tile) == 0 && tiles.GetTileUnitId(tile) == 0;
        }
        private static unsafe int Occurrences(int tile, int id)
        {
            int found = 0, cursor = GameTileManagerAPI.Instance.GetTileUnitId(tile);
            var seen = new HashSet<int>();
            while (cursor != 0)
            {
                Require(cursor >= GameUnitManager.FirstLiveUnitId && cursor <= GameUnitManager.LastLiveUnitId && seen.Add(cursor), "invalid occupancy chain");
                Require(UnitAccess.TryGetById(cursor, out GameUnit* u, out _) && u != null,
                    "occupancy member unavailable");
                if (cursor == id) found++;
                cursor = u->r_InterestingTodo4MaybeRandomPathing; // native8FA / public29E: next occupancy link.
            }
            return found;
        }
        private static void WriteSetting(object model, string name, bool value)
        {
            var property = model.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Require(property != null && property.PropertyType == typeof(bool) && property.GetSetMethod() != null, "setting missing: " + name);
            property.SetValue(model, value, null);
            Require((bool)property.GetValue(model, null) == value, "setting refused: " + name);
        }
        private static void Case(string name, string result) { checks++; Info("CASE_PASS name=" + name + " outcome=" + result); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Info(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] RELOCATION_ACCEPTANCE " + text);
    }
}
