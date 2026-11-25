using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using APIShared;
using APIShared.Automation;
using APIShared.GameModes;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using ListView = Noesis.ListView;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PrebuiltAiStorageTest
{
    [BepInPlugin(Guid, "Prebuilt AI Storage Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.73.2")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.243")]
    [BepInDependency("ExtendedData_Serp")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PrebuiltAiStorageTest_Serp";

        private void Awake()
        {
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length != 2 ||
                (lines[1] != "close-snake2" && lines[1] != "skirmish-snake2" &&
                 lines[1] != "skirmish-snake2-speed200" && lines[1] != "snake2-aiv-toggle") ||
                !System.Guid.TryParseExact(lines[0], "N", out var id)) return;
            var api = ApiShared.ForMod(Guid);
            var log = Logger;
            api.WhenReady(_ => api.Ui.TryPost(() =>
            {
                var fixture = new Fixture(log, lines[1] != "close-snake2", lines[1] == "skirmish-snake2-speed200");
                File.Delete(request);
                Func<AutomationRun, IEnumerable<AutomationWait>> scenario =
                    lines[1] == "snake2-aiv-toggle" ? fixture.RunAivToggle : fixture.Run;
                api.Automation.Start(id, new[] { new AutomationStep("exact-close-encounters", TimeSpan.FromSeconds(180),
                    scenario) }, log, quitWhenDone: true);
            }));
        }
    }

    // The automation runner owns the fixture for the test lifetime. The R3
    // subscriptions are scoped to the mission launched by this scenario.
    internal sealed class Fixture
    {
        private const string PackageId = "2d5aa95f-a56c-42be-8f3c-867d47a76811";
        private const string PackageFingerprint = "771ea4e2c74a92478b518968693b1b6791df87792615fd47a90c1ab1b49ec65a";
        private const string MapHash = "FBA595F04448E7FB3371C6A3637A0BADE8CB41B051A5F92E873B64852A2A3FD8";
        private const string AivHash = "8B7AFD53BBDDD634B0844F8443600E0AF43CB8A816C438C64AEA04B7B2CEF112";
        private const int Owner = 3, Candidate = 1, Selector = 101, AivLength = 887;
        private const int TargetX = 410, TargetY = 528;
        private const int FirstYardX = 405, FirstYardY = 533;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly ManualLogSource log;
        private readonly bool skirmish;
        private readonly bool speedObservation;
        private const int RequestedSpeed = 200;
        private const int ObservationSeconds = 30;
        private volatile bool speedConfirmed;
        private long observationStart;
        private int observationFirstTick;
        private int observedSpawnMask;
        private static bool pinSkirmish;
        private int initializedRejects;
        private volatile bool launched;
        private volatile bool observedLate;
        private bool controlBuilt;
        private int? firstTick;
        private string captureFailure;

        internal Fixture(ManualLogSource log, bool skirmish, bool speedObservation)
        { this.log = log; this.skirmish = skirmish; this.speedObservation = speedObservation; }

        internal IEnumerable<AutomationWait> Run(AutomationRun run)
        {
            if (!skirmish)
            {
                foreach (var wait in RunCoop(run)) yield return wait;
                yield break;
            }

            var bugfixes = Chainloader.PluginInfos["BugfixesAndQoL_Serp"].Instance;
            object settings = Member(bugfixes, "Settings");
            Set(settings, "EnableLocalAutosave", false);
            Set(settings, "EnablePrebuiltAiStorageFix", true);
            DisableSnake2FixIfAvailable(settings);
            Require(SimulationTickEvents.TryRegister(Plugin.Guid, "storage-state", ObserveTick,
                out string tickError, order: 200), "Tick observer unavailable: " + tickError);
            InstallSkirmishPin();
            using (BuildingR3EventHooks.OnIsBuildingAdjacentToOther.Observable.Subscribe(ObserveAdjacency))
            using (speedObservation ? BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(ObserveTargetSpawn) : null)
            {
                launched = true;
                pinSkirmish = true;
                try
                {
                    foreach (var wait in StartSkirmish(run)) yield return wait;
                }
                finally { pinSkirmish = false; }
                Require(Checksum(File.ReadAllBytes(run.CurrentMission.FilePath)) == MapHash, "Unexpected map bytes.");
                if (speedObservation)
                {
                    yield return AutomationWait.Until("ordinary Skirmish ready for speed 200", () =>
                    {
                        if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResume(null);
                        return Director.instance?.SimRunning == true && !MainViewModel.Instance.Show_HUD_Briefing;
                    });
                    Director.instance.SetEngineFrameRate(RequestedSpeed);
                    Require(IsRequestedSpeed(), "Engine did not accept game speed 200.");
                    speedConfirmed = true;
                }
                yield return AutomationWait.Until("exact Skirmish observations", () => {
                    if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResume(null);
                    return observedLate;
                });
                launched = false;
                Require(captureFailure == null, captureFailure);
                Require(initializedRejects > 0, "Expected the stockpile rejection in the verified start state.");
                if (!speedObservation)
                {
                    Require(controlBuilt, "Expected successful armory control.");
                    Log("PASS exact Skirmish Snake2 rejects stockpile and admits later armory with normal rules");
                }
            }
        }

        internal IEnumerable<AutomationWait> RunAivToggle(AutomationRun run)
        {
            var bugfixes = Chainloader.PluginInfos["BugfixesAndQoL_Serp"].Instance;
            object settings = Member(bugfixes, "Settings");
            yield return AutomationWait.Until("ready frontend before changing settings", FrontendReady);
            Set(settings, "EnableMod", true);
            Set(settings, "EnableLocalAutosave", false);
            Set(settings, "EnablePrebuiltAiStorageFix", true);
            Require(GameAssetManagerAPI.Instance.GetModifiedFileTextContent("AIV/SK_SNAKE_4.aivjson", out _),
                "Fixes Snake5 override was not indexed.");
            var vanilla = (short[][])typeof(AIVLoader).GetField("snake", Static).GetValue(null);
            short[] fixedSnake5 = AIVLoader.getAIVData((int)AILords.SK_SNAKE - 1, 4);
            Require(fixedSnake5 != null && !fixedSnake5.SequenceEqual(vanilla[4]),
                "The loader did not use Fixes' Snake5 AIV.");
            string snake5 = Checksum(ToBytes(fixedSnake5));
            InstallSkirmishPin();
            foreach (bool enabled in new[] { true, false, true })
            {
                Set(settings, "EnableSnake2StockpileAivFix", enabled);
                Require(Equals(Member(settings, "EnableSnake2StockpileAivFix"), enabled),
                    "The singleplayer host setting did not accept the change.");
                CheckCastlePlannerCorrection(vanilla[1], enabled);
                pinSkirmish = true;
                try
                {
                    foreach (var wait in StartSkirmish(run)) yield return wait;
                }
                finally { pinSkirmish = false; }
                Require(Checksum(File.ReadAllBytes(run.CurrentMission.FilePath)) == MapHash, "Unexpected map bytes.");
                CheckToggleImport(vanilla[1], enabled);
                int targetY = enabled ? TargetY + 1 : TargetY;
                yield return AutomationWait.Until("stockpile startup settled", () =>
                {
                    if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResume(null);
                    return GameTimeManagerAPI.Instance.GetElapsedMapTicks() >= 24;
                });
                int targetParts = Buildings(eStructs.STRUCT_GOODS_YARD).Count(b => b.r_AliveState == AliveState.IsAlive &&
                    (b.r_TilePositionXBegin == TargetX || b.r_TilePositionXBegin == TargetX + 3) &&
                    (b.r_TilePositionYBegin == targetY || b.r_TilePositionYBegin == targetY + 3));
                Require(targetParts == (enabled ? 4 : 0), $"Unexpected stockpile result: enabled={enabled}, alive={targetParts}.");
                Require(Checksum(ToBytes(AIVLoader.getAIVData((int)AILords.SK_SNAKE - 1, 4))) == snake5,
                    "Fixes' Snake5 changed after switching the Snake2 fix.");
                Log($"AIV_TOGGLE enabled={enabled} target={TargetX}/{targetY} alive={targetParts}/4 fixesSnake5={snake5}");
                MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
                yield return AutomationWait.Until("back between matches", () => run.CurrentMission == null && FrontendReady());
            }
        }

        private static bool FrontendReady() => MainViewModel.viewModelLoaded &&
            MainViewModel.Instance.Show_Frontend_MainMenu && !MainViewModel.Instance.Show_IntroSequence;

        private static void InstallSkirmishPin()
        {
            var original = typeof(EngineInterface).GetMethod("setMultiplayerStartingData", Static, null,
                new[] { typeof(EngineInterface.MultiplayerSetupData) }, null);
            Require(original != null && original.IsPublic, "Public Skirmish setup entry not available.");
            new Harmony(Plugin.Guid).Patch(original,
                prefix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(PinSetup), Static)));
        }

        private static unsafe void CheckToggleImport(short[] original, bool enabled)
        {
            var aiv = GameAIVManagerAPI.Instance;
            Require(aiv.TryGetVillageSlotByPlayerId(Owner, out int slot) &&
                aiv.TryGetVillageBySlot(slot, out AivVillageState* village) &&
                GamePlayerManagerAPI.Instance.GetAILord(Owner) == Enums.AILords.SK_SNAKE &&
                village->SelectedVariantIndex == Candidate && (int)village->Rotation == 0,
                "The exact Snake2 variant or rotation was not selected.");
            Require(aiv.TryGetImportedAIVVariant(Owner - 1, Candidate, out short* raw), "Snake2 import missing.");
            var imported = new short[AivLength];
            Marshal.Copy((IntPtr)raw, imported, 0, imported.Length);
            Require(Checksum(ToBytes(original)) == AivHash && imported.Select((word, index) => word == original[index])
                .Count(equal => !equal) == (enabled ? 1 : 0) && imported[8] == (enabled ? 5855 : 5955),
                "Wrong Snake2 import or mutation of the original plan.");
            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(Owner);
            Require(BuildingAccess.TryGetById(keepId, out var keep, out _) &&
                keep->r_TilePositionXBegin == 398 && keep->r_TilePositionYBegin == 531,
                "Player 3 Keep is not at (398,531).");
            Require(aiv.TryGetBuildStep(slot, 2, out AivBuildStep* step) && step->BuildingType == eMappers.MAPPER_STORES,
                "Snake2 stockpile frame missing.");
            var tile = GameTileManagerAPI.Instance.GetTileVectorFromId(step->MapTileIdOrBufferIndex);
            Require(tile.X == TargetX && tile.Y == (enabled ? TargetY + 1 : TargetY), "Unexpected stockpile frame tile.");
        }

        private static byte[] ToBytes(short[] words)
        {
            var bytes = new byte[words.Length * sizeof(short)];
            Buffer.BlockCopy(words, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static void DisableSnake2FixIfAvailable(object settings) =>
            settings.GetType().GetProperty("EnableSnake2StockpileAivFix", Instance)?.SetValue(settings, false);

        private void CheckCastlePlannerCorrection(short[] original, bool enabled)
        {
            Require(Chainloader.PluginInfos.TryGetValue("CastlePlanner_Serp", out var planner),
                "CastlePlanner is missing from the AIV toggle test layout.");
            var assembly = planner.Instance.GetType().Assembly;
            var bridge = assembly.GetType("CastlePlanner.BuiltInAivCorrectionBridge", true);
            string text = (string)bridge.GetMethod("GetText", Static).Invoke(null, new object[] { "SK_SNAKE", 1 });
            string originalPath = Path.Combine(Path.GetDirectoryName(planner.Location), "VanillaAIV", "snake2.aivjson");
            Require(File.Exists(originalPath), "CastlePlanner's bundled Snake2 is unavailable.");
            string resolvedPath = (string)bridge.GetMethod("ResolveFile", Static).Invoke(null,
                new object[] { "AIV/SK_SNAKE_1.aivjson", originalPath });
            Require((text != null) == enabled && (resolvedPath != originalPath) == enabled,
                "CastlePlanner did not track the current Snake2 setting.");
            if (!enabled) return;
            Require(File.ReadAllText(resolvedPath) == text, "CastlePlanner import and preview disagree.");
            object document = assembly.GetType("CastlePlanner.AivJsonReader", true).GetMethod("Parse", Static)
                .Invoke(null, new object[] { text });
            short[] raw = (short[])assembly.GetType("CastlePlanner.AivRawDataEncoder", true).GetMethod("Encode", Static)
                .Invoke(null, new[] { document });
            Require(raw.Length == original.Length && raw[8] == 5855 &&
                raw.Where((word, index) => word != original[index]).Count() == 1,
                "CastlePlanner did not preserve all other Snake2 AIV data.");
            Log("CASTLEPLANNER_AIV matches corrected Snake2 preview and import");
        }

        private static IEnumerable<AutomationWait> StartSkirmish(AutomationRun run)
        {
            yield return AutomationWait.Until("ready frontend", () => MainViewModel.viewModelLoaded &&
                MainViewModel.Instance.Show_Frontend_MainMenu && !MainViewModel.Instance.Show_IntroSequence);
            var view = MainViewModel.Instance;
            view.ButtonMainMenuFunction("Custom");
            yield return AutomationWait.Until("local Skirmish map list", () =>
                FRONT_Multiplayer.skirmishGame && !FRONT_Multiplayer.coopGame &&
                view.FRONTMultiplayer?.FindName("MapList") is ListView list && list.ItemsSource != null);
            var lobby = view.FRONTMultiplayer;
            var maps = (ListView)lobby.FindName("MapList");
            var map = ((IEnumerable)maps.ItemsSource).OfType<FileRow>().Single(r =>
                string.Equals(Path.GetFileName(r.fileHeader?.filePath), "Close Encounters.map", StringComparison.OrdinalIgnoreCase));
            Require(Checksum(File.ReadAllBytes(map.fileHeader.filePath)) == MapHash, "Unexpected selected map bytes.");
            maps.SelectedItem = map;
            yield return AutomationWait.NextFrame;
            foreach (var lord in new[] { AILords.SK_RAT, AILords.SK_SNAKE, AILords.SK_SULTAN, AILords.SK_EMIR })
            {
                int count = lobby.currentLobby.members.Count;
                lobby.SkirmishAIAddClick(((int)lord - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                yield return AutomationWait.Until("added " + lord, () => lobby.currentLobby.members.Count == count + 1);
            }
            lobby.ButtonClicked("Play");
            yield return AutomationWait.Until("started local Skirmish", () => run.CurrentMission != null &&
                run.CurrentMission.Mode.Kind != GameModeKind.CoopTrail && !run.CurrentMission.Mode.IsRealMultiplayer);
        }

        // The vanilla Skirmish launch rewrites the lobby AIV selectors before serialization.
        private static void PinSetup(EngineInterface.MultiplayerSetupData __0)
        {
            if (!pinSkirmish) return;
            for (int slot = 0; slot < __0.start_keep_location_order.Length; slot++)
                __0.start_keep_location_order[slot] = -10;
            int[] positions = { 0, 2, 3, 5, 1 };
            for (int player = 0; player < positions.Length; player++)
            {
                __0.start_keep_location_order[positions[player]] = player;
                if (player > 0) __0.preferredAIVs[player] = player == Owner - 1 ? Selector : 100;
            }
            __0.advopt_pre_build = 1;
            __0.autosave = 0;
        }

        private IEnumerable<AutomationWait> RunCoop(AutomationRun run)
        {
            yield return AutomationWait.Until("ready frontend", () => MainViewModel.viewModelLoaded &&
                MainViewModel.Instance.Show_Frontend_MainMenu && !MainViewModel.Instance.Show_IntroSequence &&
                MainViewModel.Instance.FrontEndMenu != null);
            var view = MainViewModel.Instance;
            var bugfixes = Chainloader.PluginInfos["BugfixesAndQoL_Serp"].Instance;
            object bugfixSettings = Member(bugfixes, "Settings");
            Set(bugfixSettings, "EnableLocalAutosave", false);
            DisableSnake2FixIfAvailable(bugfixSettings);
            view.FrontEndMenu.ButtonClicked("Coops");
            view.FrontEndMenu.ButtonClicked("Coop");
            yield return AutomationWait.Until("first Coop Trail lobby", () =>
                FRONT_Multiplayer.coopGame && view.FRONTMultiplayer?.currentLobby?.isHost == true && view.Show_CoopTrail1);
            var lobby = view.FRONTMultiplayer;
            lobby.ButtonClicked("CoopSinglePlayer");
            lobby.SkirmishAIAddClick("1");
            yield return AutomationWait.Until("Coop AI partner", () => lobby.singlePlayerCoop);

            var extended = Chainloader.PluginInfos["ExtendedData_Serp"].Instance;
            object runtime = extended.GetType().GetField("runtime", Static).GetValue(null);
            object settings = Member(runtime, "settings");
            Set(settings, "ActiveCoopPackageId", PackageId);
            yield return AutomationWait.Until("exact Coop package selected", () =>
                string.Equals((string)Member(settings, "ActiveCoopPackageFingerprint"), PackageFingerprint, StringComparison.OrdinalIgnoreCase) &&
                ((string)Member(settings, "CoopPackageStatus"))?.StartsWith("OK", StringComparison.Ordinal) == true);
            lobby.CoopMissionChanged(0, 1);
            var customize = bugfixes.GetType().Assembly.GetType("BugfixesAndQoL.TrailCustomizationProviderHostApi", true)
                .GetMethod("TryCustomizeCoopTrail", Static);
            object[] args = { false };
            Require((bool)customize.Invoke(null, args) && (bool)args[0], "Customize did not open.");
            yield return AutomationWait.Until("Customize setup", () => view.Show_MultiplayerSetup &&
                FRONT_Multiplayer.skirmishGame && FRONT_Multiplayer.coopGame);
            var setup = (EngineInterface.MultiplayerSetupData)Member(lobby, "MPsetupData");
            setup.preferredAIVs[1] = 100;
            setup.preferredAIVs[2] = Selector;
            setup.preferredAIVs[3] = 100;
            setup.preferredAIVs[4] = 100;
            setup.autosave = 0;
            Require((bool)Member(bugfixSettings, "EnablePrebuiltAiStorageFix"), "Storage fix must be active.");
            long previous = run.CurrentMission?.SessionId ?? 0;
            Require(SimulationTickEvents.TryRegister(Plugin.Guid, "storage-state", ObserveTick,
                out string tickError, order: 200), "Tick observer unavailable: " + tickError);

            using (BuildingR3EventHooks.OnIsBuildingAdjacentToOther.Observable.Subscribe(ObserveAdjacency))
            {
                launched = true;
                lobby.ButtonClicked("Play");
                yield return AutomationWait.Until("exact customized local Coop mission", () =>
                    run.CurrentMission is MissionContext context && context.SessionId != previous &&
                    context.Mode.Kind == GameModeKind.CoopTrail && context.Mode.IsCustomized && !context.Mode.IsRealMultiplayer);
                var mission = run.CurrentMission;
                Require(Checksum(File.ReadAllBytes(mission.FilePath)) == MapHash, "Unexpected map bytes.");
                if (view.Show_HUD_Briefing) view.ButtonBriefingResume(null);
                yield return AutomationWait.Until("storage snapshots through 450 simulation ticks", () => {
                    if (view.Show_HUD_Briefing) view.ButtonBriefingResume(null);
                    return observedLate;
                });
                launched = false;
                Require(captureFailure == null, captureFailure);
                Require(initializedRejects > 0, "No exact-target rejection with a fully initialized starter yard was observed.");
                Log("PASS exact Snake2 map, variant, origin and initialized-neighbor rejection");
            }
        }

        private void ObserveTick(int tick)
        {
            if (!launched || observedLate) return;
            try
            {
                if (!firstTick.HasValue)
                {
                    firstTick = tick;
                    Log(Snapshot("early"));
                }
                if (skirmish)
                {
                    if (speedObservation)
                    {
                        if (!ObserveTimedWindow(tick)) return;
                    }
                    else
                    {
                        if (tick < firstTick.Value + 16 || initializedRejects == 0) return;
                        CheckArmoryControl();
                    }
                }
                else if (tick < firstTick.Value + 450) return;
                Log(Snapshot("late"));
            }
            catch (Exception ex) { captureFailure = captureFailure ?? ex.ToString(); }
            observedLate = true;
        }

        private bool ObserveTimedWindow(int tick)
        {
            if (!speedConfirmed) return false;
            Require(IsRequestedSpeed(), "Engine speed changed during observation.");
            long now = Stopwatch.GetTimestamp();
            if (observationStart == 0)
            {
                observationStart = now;
                observationFirstTick = tick;
                Log($"speed-window start tick={tick} requested={RequestedSpeed} engineFrameTime={Director.instance.EngineFrameTime:R}");
            }
            double elapsed = (double)(now - observationStart) / Stopwatch.Frequency;
            if (elapsed < ObservationSeconds) return false;
            var yards = Buildings(eStructs.STRUCT_GOODS_YARD);
            int present = yards.Count(b => AtTargetCorner(b.r_TilePositionXBegin, b.r_TilePositionYBegin));
            Log($"speed-window result elapsedSeconds={elapsed:0.000} speed={RequestedSpeed} " +
                $"firstTick={observationFirstTick} lastTick={tick} targetRecords={present}/4 targetSpawnMask=0x{observedSpawnMask:X}");
            return true;
        }

        private void ObserveTargetSpawn(BuildingSpawnEventArgs args)
        {
            if (!launched || args.Phase != EventHookPhase.Post || args.PlayerId != Owner ||
                args.Building != eStructs.STRUCT_GOODS_YARD || args.ReturnValue <= 0 ||
                !AtTargetCorner(args.TileX, args.TileY)) return;
            int corner = (args.TileX == TargetX ? 0 : 1) + (args.TileY == TargetY ? 0 : 2);
            observedSpawnMask |= 1 << corner;
            Log($"target-spawn tick={GameTimeManagerAPI.Instance.GetElapsedMapTicks()} tile={args.TileX}/{args.TileY} id={args.ReturnValue}");
        }

        private static bool AtTargetCorner(int x, int y) =>
            (x == TargetX || x == TargetX + 3) && (y == TargetY || y == TargetY + 3);

        private static bool IsRequestedSpeed() =>
            Math.Abs(Director.instance.EngineFrameTime - 1.0 / RequestedSpeed) < 0.000001;

        private void CheckArmoryControl()
        {
            Log(Snapshot("before-armory-control"));
            var existing = Buildings(eStructs.STRUCT_ARMOURY);
            Require(existing.Any(b => b.r_TilePositionXBegin == 395 && b.r_TilePositionYBegin == 522 &&
                b.r_AliveState == AliveState.IsAlive), "Initialized armory seed is missing.");
            Require(!existing.Any(b => b.r_TilePositionXBegin == 399 && b.r_TilePositionYBegin == 521),
                "Later armory has already been built.");
            GameBuildingManagerAPI.Instance.CreatePrefab(Owner, 399, 521, eMappers.MAPPER_ARMOURY,
                BuildingScales.GetScale(eMappers.MAPPER_ARMOURY), 15, true, false);
            controlBuilt = Buildings(eStructs.STRUCT_ARMOURY)
                .Any(b => b.r_TilePositionXBegin == 399 && b.r_TilePositionYBegin == 521);
            Log("armory-control tile=399/521 free=true bypassPlacementRules=false spawned=" + controlBuilt);
        }

        private unsafe void ObserveAdjacency(IsGameBuildingAdjacentToOtherEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || args.PlayerId != Owner ||
                args.TileX != TargetX || args.TileY != TargetY || args.Building != eStructs.STRUCT_GOODS_YARD ||
                args.PotentialRange != 5 || initializedRejects != 0 || captureFailure != null) return;
            try
            {
                var records = Buildings(eStructs.STRUCT_GOODS_YARD);
                int alive = records.Count(b => b.r_AliveState == AliveState.IsAlive &&
                    (b.r_TilePositionXBegin == FirstYardX || b.r_TilePositionXBegin == FirstYardX + 3) &&
                    (b.r_TilePositionYBegin == FirstYardY || b.r_TilePositionYBegin == FirstYardY + 3));
                if (alive != 4) return;
                string snapshot = Snapshot("adjacency");
                Require(args.ReturnValue == 0, "Native stockpile neighbor search accepted the diagonal.");
                initializedRejects++;
                Log(snapshot + " nativeNeighbor=" + args.ReturnValue);
            }
            catch (Exception ex) { captureFailure = ex.ToString(); }
        }

        private unsafe string Snapshot(string phase)
        {
            var ai = GameAIVManagerAPI.Instance;
            Require(ai.TryGetVillageSlotByPlayerId(Owner, out int slot), "Player 3 AIV slot missing.");
            Require(ai.TryGetVillageBySlot(slot, out AivVillageState* village), "Player 3 AIV missing.");
            Require(GamePlayerManagerAPI.Instance.GetAILord(Owner) == Enums.AILords.SK_SNAKE &&
                village->SelectedVariantIndex == Candidate &&
                (int)village->Rotation == 0, $"Unexpected AIV {village->AILord}/{village->SelectedVariantIndex}/{village->Rotation}.");
            Require(ai.TryGetImportedAIVVariant(Owner - 1, Candidate, out short* raw), "Snake2 bank entry missing.");
            var bytes = new byte[AivLength * sizeof(short)];
            Marshal.Copy((IntPtr)raw, bytes, 0, bytes.Length);
            Require(Checksum(bytes) == AivHash, "Imported AIV differs from Snake2 source.");
            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(Owner);
            Require(BuildingAccess.TryGetById(keepId, out var keep, out _) &&
                keep->r_TilePositionXBegin == 398 && keep->r_TilePositionYBegin == 531,
                "Player 3 Keep is not at (398,531).");
            var storage = new List<string>();
            for (int index = 0; index <= village->MaximumBuildStep; index++)
            {
                if (!ai.TryGetBuildStep(slot, index, out AivBuildStep* step) ||
                    step->State == AivBuildStepState.Inactive ||
                    (step->BuildingType != eMappers.MAPPER_STORES && step->BuildingType != eMappers.MAPPER_GRANARY &&
                     step->BuildingType != eMappers.MAPPER_ARMOURY)) continue;
                var tile = GameTileManagerAPI.Instance.GetTileVectorFromId(step->MapTileIdOrBufferIndex);
                if (index == 2)
                    Require(step->BuildingType == eMappers.MAPPER_STORES && tile.X == TargetX && tile.Y == TargetY,
                        "AIV frame 2 does not match the failed target.");
                var ids = Buildings(step->BuildingType == eMappers.MAPPER_STORES ? eStructs.STRUCT_GOODS_YARD :
                    step->BuildingType == eMappers.MAPPER_GRANARY ? eStructs.STRUCT_GRANARY : eStructs.STRUCT_ARMOURY);
                int present = ids.Count(b => b.r_TilePositionXBegin == tile.X && b.r_TilePositionYBegin == tile.Y);
                storage.Add($"{index}:{step->BuildingType}@{tile.X}/{tile.Y}:{step->State}:present={present}");
            }
            Require(storage.Any(s => s.StartsWith("2:MAPPER_STORES@410/528:", StringComparison.Ordinal)), "AIV target frame was absent.");
            string yards = string.Join(" ", Buildings(eStructs.STRUCT_GOODS_YARD)
                .Select(b => $"{b.r_TilePositionXBegin}/{b.r_TilePositionYBegin}:{b.r_AliveState}"));
            return $"{phase} nativeLord={village->AILord} variant=Snake2 rawSha={AivHash} rotation=0 keep=398/531 " +
                $"tick={GameTimeManagerAPI.Instance.GetElapsedMapTicks()} yards=[{yards}] frames=[{string.Join(" ", storage)}]";
        }

        private static List<GameBuilding> Buildings(eStructs type)
        {
            var result = new List<GameBuilding>();
            foreach (ref GameBuilding building in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())
                if (building.r_PlayerIdOwner == Owner && building.r_BuildingType == type &&
                    (building.r_AliveState == AliveState.IsAlive || building.r_AliveState == AliveState.NeedsInit))
                    result.Add(building);
            return result;
        }

        private static object Member(object owner, string name) => owner.GetType().GetProperty(name, Instance)?.GetValue(owner) ??
            owner.GetType().GetField(name, Instance)?.GetValue(owner);
        private static void Set(object owner, string name, object value) =>
            owner.GetType().GetProperty(name, Instance).SetValue(owner, value, null);
        private static string Checksum(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private void Log(string line) => log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] STORAGE_ACCEPTANCE {line}");
    }
}
