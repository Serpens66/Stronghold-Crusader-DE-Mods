using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Units.AiMilitary;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Path = System.IO.Path;

namespace CrenelSiegeAcceptance
{
    [BepInPlugin(Guid, "Crenel Siege Acceptance", "0.1.0")]
    [BepInDependency("APIShared_Serp", "0.73.2")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.246")]
    [BepInDependency("Spectator_Serp", "0.2.9")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "CrenelSiegeAcceptance_Serp";
        private const string Scenario = "crenel-siege";
        private static ManualLogSource log;

        private void Awake()
        {
            log = Logger;
            string file = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(file)) return;
            string[] lines = File.ReadAllLines(file);
            if (lines.Length < 2 || (lines[1] != Scenario && lines[1] != "crenel-damage") ||
                !System.Guid.TryParseExact(lines[0], "N", out var id))
                return;
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => api.Ui.TryPost(() =>
            {
                var test = new Fixture();
                File.Delete(file);
                api.Automation.Start(id, test.Steps(includeSiege: lines[1] == Scenario), log, _result => test.Dispose(), quitWhenDone: true);
            }));
        }

        internal static void Report(string value) =>
            log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] CRENEL_SIEGE {value}");
    }

    internal sealed class Fixture : IDisposable
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Map = "4spielerkarte.map";
        private const int SnakeLobbyId = (int)AILords.SK_SNAKE - 1;
        private static bool armLobby;
        private static int snakeSlot, passiveSlot;
        private readonly Harmony harmony = new Harmony(Plugin.Guid);
        private readonly ConcurrentQueue<AiMilitaryAssignmentResult> admissions = new ConcurrentQueue<AiMilitaryAssignmentResult>();
        private readonly List<AiUnitRef> assault = new List<AiUnitRef>();
        private readonly List<(int X, int Y)> crenels = new List<(int, int)>();
        private readonly Dictionary<int, (byte Damage, byte Height)> hitsBefore = new Dictionary<int, (byte, byte)>();
        private readonly HashSet<int> targets = new HashSet<int>();
        private readonly Dictionary<int, bool> baselineHeights = new Dictionary<int, bool>();
        private IDisposable damage;
        private int defender, attacker, spawnX, spawnY, hits, declared, directTile;
        private volatile int damagingHits, directDamage;
        private bool lobbyPatched;
        private bool aligned;
        private GameTileManagerAPI Tiles => GameTileManagerAPI.Instance;

        internal IEnumerable<AutomationStep> Steps(bool includeSiege)
        {
            foreach (bool compare in includeSiege ? new[] { false, true } : Array.Empty<bool>())
            {
                string variant = compare ? "align" : "default";
                yield return new AutomationStep(variant + "-setup", TimeSpan.FromSeconds(90), r => Start(r, compare));
                yield return new AutomationStep(variant + "-walls-and-paths", TimeSpan.FromSeconds(90), Prepare, true);
                yield return new AutomationStep(variant + "-ai-attack", TimeSpan.FromSeconds(155), Attack, true);
                yield return new AutomationStep(variant + "-leave", TimeSpan.FromSeconds(30), Leave);
            }
            foreach (bool compare in new[] { false, true })
            {
                string variant = compare ? "editor-align" : "editor-default";
                yield return new AutomationStep(variant + "-setup", TimeSpan.FromSeconds(60), r => StartEditor(r, compare));
                yield return new AutomationStep(variant + "-direct-damage", TimeSpan.FromSeconds(50), DirectDamage, true);
                yield return new AutomationStep(variant + "-leave", TimeSpan.FromSeconds(30), Leave);
            }
        }

        private static bool MenuReady() => MainViewModel.viewModelLoaded && FatControler.currentScene == Enums.SceneIDS.FrontEnd &&
            MainViewModel.Instance.Show_Frontend_MainMenu && !MainViewModel.Instance.Show_IntroSequence &&
            Director.instance != null && EditorDirector.instance != null;

        private IEnumerable<AutomationWait> Start(AutomationRun run, bool compare)
        {
            yield return AutomationWait.Until("main menu", MenuReady);
            if (!lobbyPatched)
            {
                harmony.Patch(AccessTools.Method(typeof(FRONT_Multiplayer), "StartSkirmishGame"),
                    prefix: new HarmonyMethod(typeof(Fixture), nameof(BeforeStart)));
                harmony.Patch(AccessTools.Method(typeof(EngineInterface), nameof(EngineInterface.setMultiplayerStartingData)),
                    prefix: new HarmonyMethod(typeof(Fixture), nameof(FixedStart)));
                lobbyPatched = true;
            }
            aligned = compare;
            object bfq = ModSettings("BugfixesAndQoL_Serp");
            object spectator = ModSettings("Spectator_Serp");
            bfq.GetType().GetMethod("System_LoadModDefaults").Invoke(bfq, null);
            spectator.GetType().GetMethod("System_LoadModDefaults").Invoke(spectator, null);
            Set(bfq, "EnableMod", true);
            Set(bfq, "EnableCrenelCheckerboard", true);
            Set(bfq, "EnableCrenelCheckerboardAI", compare);
            foreach (string option in new[] {"AlignExistingCrenelsInEditor", "AlignExistingCrenelsOnSaveLoad", "AlignExistingCrenelsOnMapStart"})
                Set(bfq, option, false);
            Set(spectator, "EnableTestTools", true);
            Plugin.Report($"SETTINGS human=true ai={compare} alignExisting=false");
            MainViewModel.Instance.ButtonMainMenuFunction("Custom");
            var view = MainViewModel.Instance;
            yield return AutomationWait.Until("skirmish map picker", () =>
                view.FRONTMultiplayer?.currentLobby != null &&
                view.FRONTMultiplayer.FindName("MapList") is ListView list && list.ItemsSource != null);
            var lobby = view.FRONTMultiplayer;
            var maps = (ListView)lobby.FindName("MapList");
            var selected = ((IEnumerable)maps.ItemsSource).OfType<FileRow>()
                .Single(r => string.Equals(Path.GetFileName(r.fileHeader?.filePath), Map, StringComparison.OrdinalIgnoreCase));
            maps.SelectedItem = selected;
            lobby.ButtonClicked("EnableSpectatorMode");
            Require(FRONT_Multiplayer.spectatorMode, "lobby did not switch to Spectator");
            lobby.SkirmishAIAddClick(SnakeLobbyId.ToString());
            yield return AutomationWait.Until("Snake added", () => lobby.currentLobby.members.Any(
                m => m.SkirmishMember && !m.SkirmishHumanMember && m.GetLordType() == SnakeLobbyId));
            lobby.SkirmishAIAddClick("98");
            var picker = lobby.FindName("CustomLordList") as ListView;
            Require(picker?.ItemsSource != null, "custom lord picker unavailable");
            picker.SelectedItem = ((IEnumerable)picker.ItemsSource).OfType<FileRow>().Single(r => r.lord?.lordName == "DoNothing" && !r.lord.workshop);
            lobby.ButtonClicked("AddCustomLord");
            yield return AutomationWait.Until("DoNothing added", () => lobby.currentLobby.members.Any(m => m.customLordName == "DoNothing"));
            armLobby = true;
            lobby.ButtonClicked("Play");
            yield return AutomationWait.Until("fresh mission", () => run.CurrentMission != null);
        }

        private static void BeforeStart(FRONT_Multiplayer __instance)
        {
            if (!armLobby) return;
            var lobby = __instance.currentLobby;
            var snake = lobby.members.Single(member => member.SkirmishMember && !member.SkirmishHumanMember &&
                member.GetLordType() == SnakeLobbyId);
            var passive = lobby.members.Single(member => member.customLordName == "DoNothing");
            snakeSlot = lobby.getThisPlayerFromSteamID(snake.id.m_SteamID);
            passiveSlot = lobby.getThisPlayerFromSteamID(passive.id.m_SteamID);
            Require(snakeSlot > 0 && passiveSlot > 0 && snakeSlot != passiveSlot, "could not resolve Spectator lobby slots");
            Plugin.Report($"ROSTER Snake={snakeSlot};DoNothing={passiveSlot}");
            var data = (EngineInterface.MultiplayerSetupData)typeof(FRONT_Multiplayer)
                .GetField("MPsetupData", Instance).GetValue(__instance);
            data.peacetime = 0;
            data.advopt_pre_build = 1;
        }

        private static void FixedStart(EngineInterface.MultiplayerSetupData setupData)
        {
            if (!armLobby) return;
            armLobby = false;
            int[] order = setupData.start_keep_location_order;
            int empty = order.First(value => value < 0);
            var players = order.Where(value => value >= 0).Distinct().OrderBy(value => value).ToArray();
            if (players.Length < 2) players = new[] { 0, 1 };
            for (int i = 0; i < order.Length; i++) order[i] = empty;
            int[,] keeps = GameData.Instance.Keep_Locations;
            var sites = Enumerable.Range(0, order.Length).Where(i => keeps[i, 0] >= 0).ToArray();
            Require(players.Length == 2 && sites.Length >= 2, "expected exactly two test slots and two keep sites");
            order[sites[0]] = players[0];
            order[sites[1]] = players[1];
            for (int i = 0; i < setupData.preferredAIVs.Length; i++) setupData.preferredAIVs[i] = 100;
            Plugin.Report($"LOBBY_KEEP order={string.Join(",", order)} AIV=100 (candidate zero, fixed rotation)");
        }

        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("spectator simulation", () => GameData.Instance.lastGameState?.spectatorMode != 0 &&
                !MainViewModel.Instance.Show_HUD_Briefing && EditorDirector.instance.ActivePlayerID <= 0);
            yield return run.WaitForTicks(mission, 140);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            run.InSimulation(mission, BuildFixture);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 35);
            run.InSimulation(mission, CheckTopology);
        }

        private int BuildFixture()
        {
            var players = GamePlayerManagerAPI.Instance;
            var live = Enumerable.Range(1, 8).Where(i => players.IsAIPlayer(i) && players.GetPlayerKeepId(i) > 0).ToArray();
            Require(live.Length == 2, "expected two live AI keeps, saw " + string.Join(",", live));
            attacker = snakeSlot;
            defender = passiveSlot;
            Require(live.Contains(attacker) && live.Contains(defender), "native AI keeps do not match the lobby slots");
            Require(attacker != defender && players.GetPlayerTeam(attacker) != players.GetPlayerTeam(defender),
                "Snake and DoNothing must be enemies");
            var attackKeep = players.GetPlayerKeepPosition(attacker);
            var defendKeep = players.GetPlayerKeepPosition(defender);
            string current = $"{attacker}:{attackKeep.X},{attackKeep.Y};{defender}:{defendKeep.X},{defendKeep.Y}";
            Plugin.Report("KEEPS " + current);
            Require(current == "1:283,403;2:403,282", "fixed keep positions or player IDs changed");
            crenels.Clear();
            hits = damagingHits = 0;
            directTile = directDamage = 0;
            hitsBefore.Clear();
            targets.Clear();
            assault.Clear();
            for (int i = 0; i < 15; i++) players.TryAddGood(defender, eGoods.STORED_STONE_BLOCKS, 25);
            var paths = GamePathingManagerAPI.Instance;
            var enclosure = (Left: 394, Right: 417, Back: 269, Front: 291);
            int wallY = enclosure.Front + 1;
            spawnX = 403;
            spawnY = enclosure.Front + 3;
            var buildings = GameBuildingManagerAPI.Instance;
            var boundary = Boundary(enclosure);
            foreach (var line in boundary)
                foreach (var point in Axis(line.X0, line.Y0, line.X1, line.Y1))
                    Require(Free(point.X, point.Y), $"enclosure is blocked at {point.X},{point.Y}");
            foreach (var line in boundary)
                buildings.CreateWall(defender, line.X0, line.Y0, line.X1, line.Y1, eMappers.MAPPER_WALL);
            foreach (var line in boundary)
                foreach (var point in Axis(line.X0, line.Y0, line.X1, line.Y1))
                {
                    int tile = Tiles.GetTileId(point.X, point.Y);
                    Require(Is(tile, TilePropertyFlag.IsWall) && Owner(tile) == defender,
                        $"enclosure has a gap at {point.X},{point.Y}");
                }
            for (int x = 399; x <= 408; x++)
            {
                Require(Free(x, wallY), $"crenel row is blocked at {x},{wallY}");
                buildings.CreateWall(defender, x, wallY, x, wallY, eMappers.MAPPER_CRENAL);
                int tile = Tiles.GetTileId(x, wallY);
                Require(Is(tile, TilePropertyFlag.CrenelatedLow) && Owner(tile) == defender,
                    $"crenel not placed at {x},{wallY}; flags={Tiles.GetTilePropertyFlag(tile)} owner={Owner(tile)}");
                crenels.Add((x, wallY));
            }
            Plugin.Report($"FIXTURE wall=399,{wallY}..408,{wallY} spawn={spawnX},{spawnY} ring={enclosure} " +
                $"approachComponent={paths.GetPathComponentGrid()[Tiles.GetTileId(spawnX, spawnY)]}");
            return 0;
        }

        private static (int X0, int Y0, int X1, int Y1)[] Boundary((int Left, int Right, int Back, int Front) area) =>
            new[] { (area.Left, area.Front, area.Right, area.Front), (area.Left, area.Back, area.Right, area.Back),
                (area.Left, area.Back, area.Left, area.Front), (area.Right, area.Back, area.Right, area.Front) };

        private static IEnumerable<(int X, int Y)> Axis(int x0, int y0, int x1, int y1)
        {
            yield return (x0, y0);
            while (x0 != x1 || y0 != y1)
            {
                x0 += Math.Sign(x1 - x0);
                y0 += Math.Sign(y1 - y0);
                yield return (x0, y0);
            }
        }

        private bool Free(int x, int y)
        {
            if (!Tiles.IsTileInsideMapBounds(x, y)) return false;
            int tile = Tiles.GetTileId(x, y);
            return Tiles.GetStructureLayer()[tile] == 0 && !Is(tile, TilePropertyFlag.IsWall) &&
                Tiles.GetHeightLayer()[tile] == Tiles.GetDefaultHeightLayer()[tile] && Tiles.GetChimpLayer()[tile] == 0 &&
                GamePathingManagerAPI.Instance.GetPathComponentGrid()[tile] != 0;
        }

        private int CheckTopology()
        {
            var paths = GamePathingManagerAPI.Instance;
            int low = 0, changed = 0;
            foreach (var cell in crenels)
            {
                int tile = Tiles.GetTileId(cell.X, cell.Y);
                bool high = Is(tile, TilePropertyFlag.CrenelatedHigh);
                byte edges = paths.GetPathEdgeMaskGrid()[tile];
                if (!high) low++;
                if (aligned)
                {
                    Require(high == (((cell.X ^ cell.Y) & 1) != 0) && (edges == 0) == high,
                        $"wrong height or path edges at {cell.X},{cell.Y}: high={high} edges={edges:X2}");
                    if (baselineHeights[tile] != high) changed++;
                }
                else baselineHeights.Add(tile, high);
                Plugin.Report($"PATH ai={aligned} tile={tile} xy={cell.X},{cell.Y} high={high} edges={edges:X2}");
            }
            Require(!aligned || low > 0 && low < crenels.Count, "need low and high crenels");
            Require(!aligned || changed > 0, "fixture did not produce a changed crenel");
            return 0;
        }

        private IEnumerable<AutomationWait> Attack(AutomationRun run)
        {
            var mission = run.CurrentMission;
            Require(ApiShared.ForMod(Plugin.Guid).TryGetAiMilitary(out var military, out var diagnostic),
                "AI assignment unavailable: " + diagnostic?.Reason);
            damage = BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnDamage);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            run.InSimulation(mission, () => Spawn(military));
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return AutomationWait.Until("native military admission", () => admissions.Count == declared);
            while (admissions.TryDequeue(out var result))
            {
                Require(result.Status == AiMilitaryAssignmentStatus.Applied, "native admission failed: " + result.Reason);
                assault.Add(result.Unit);
                Plugin.Report($"ADMISSION status={result.Status} role={result.Assignment.Role} group={result.Group.TribeId} unit={result.Unit}");
            }
            run.InSimulation(mission, ForceAttack);
            for (int i = 0; i < 20 && damagingHits == 0; i++)
            {
                yield return run.WaitForTicks(mission, 400);
                Plugin.Report(run.InSimulation(mission, AssaultSnapshot));
            }
            Plugin.Report($"RESULT ai={aligned} wallHits={hits} damagingHits={damagingHits}");
            Require(damagingHits != 0, "strategic attack failed to damage a wall in the observation window");
            damage.Dispose();
            damage = null;
        }

        private unsafe string AssaultSnapshot()
        {
            var players = GamePlayerManagerAPI.Instance;
            Require(players.TryGetPlayerResourcesById(attacker, out var player), "siege player unavailable");
            var positions = new List<string>();
            foreach (var unit in assault)
                if (UnitAccess.TryGetById(unit.UnitId, out var current, out _) && current->r_GlobalId == unit.GlobalId)
                    positions.Add($"{unit.UnitId}@{current->r_CurrentTilePositionX},{current->r_CurrentTilePositionY}" +
                        $"/state{current->r_AIState}/target{current->r_AI_ContextTargetBuildingTileId}");
            return $"ATTACK_WINDOW ai={aligned} siege={player->r_AISiegeState} wallUnits={player->r_AICurrentSiegeWallUnitCount} " +
                $"forceSize={player->r_AITargetAttackForceSize} attempts={player->r_AISiegeAttempts} " +
                $"hits={hits} damaging={damagingHits} units={string.Join(";", positions)}";
        }

        private IEnumerable<AutomationWait> DirectDamage(AutomationRun run)
        {
            var mission = run.CurrentMission;
            int tribe = EditorSimulation(() =>
            {
                var target = crenels[2];
                directTile = Tiles.GetTileId(target.X, target.Y);
                Require(Is(directTile, TilePropertyFlag.CrenelatedLow), "direct control target is not a crenel");
                int advance = Math.Sign(spawnY - target.Y);
                var tribes = GameTribeManagerAPI.Instance;
                int id = checked((int)tribes.Create(attacker));
                Require(id > 0, "could not create direct-command control tribe");
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = target.X + dx, y = target.Y + advance;
                    Require(Free(x, y), $"direct control spawn is blocked at {x},{y}");
                    int tile = Tiles.GetTileId(x, y);
                    int unit = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(attacker, attacker, x, y,
                        Tiles.GetHeightLayer()[tile], eChimps.CHIMP_TYPE_MACEMAN));
                    Require(unit > 0 && tribes.AssignUnit(id, unit), "could not put the maceman in the control tribe");
                }
                Plugin.Report($"DIRECT_TARGET ai={aligned} tile={directTile} xy={target.X},{target.Y}" +
                    $" high={Is(directTile, TilePropertyFlag.CrenelatedHigh)} damage={Tiles.GetDamageLayer()[directTile]}");
                return id;
            });
            yield return run.WaitForTicks(mission, 12);
            Require(EditorSimulation(() => GameTribeManagerAPI.Instance.AttackWall(tribe, directTile)),
                "native direct wall attack rejected");
            for (int i = 0; i < 9 && directDamage == 0; i++)
                yield return run.WaitForTicks(mission, 60);
            Require(directDamage != 0, "direct attack did not damage the selected crenel");
            Plugin.Report($"DIRECT_DAMAGE ai={aligned} tile={directTile} damagingHits={directDamage}");
            damage.Dispose();
            damage = null;
        }

        private IEnumerable<AutomationWait> StartEditor(AutomationRun run, bool compare)
        {
            yield return AutomationWait.Until("main menu", MenuReady);
            aligned = compare;
            object bfq = ModSettings("BugfixesAndQoL_Serp");
            bfq.GetType().GetMethod("System_LoadModDefaults").Invoke(bfq, null);
            Set(bfq, "EnableMod", true);
            Set(bfq, "EnableCrenelCheckerboardAI", compare);
            Set(bfq, "AlignExistingCrenelsInEditor", false);
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Firefly Studios", "Stronghold Crusader Definitive Edition", "Maps", Map);
            var header = MapFileManager.Instance.GetFileInfoFromFileName(path, path, 0, true);
            Require(header != null && header.isMapEditable(), "fixed editor map unavailable");
            GameData.Instance.SetMissionTextFromHeader(header);
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
            Director.instance.SetPausedState(false);
            Require(EditorDirector.instance.loadMapIntoEditor(header.filePath, header.standAlone_filename), "editor load failed");
            yield return AutomationWait.Until("editor session", () => run.CurrentMission?.Mode.IsMapEditor == true);
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            MainViewModel.Instance.MEMode = 1;
            yield return run.WaitForTicks(mission, 20);
            foreach (var wait in BuildEditorControl(run, mission)) yield return wait;
            yield return run.WaitForTicks(mission, 80);
            damage = BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnDamage);
        }

        private static T EditorSimulation<T>(Func<T> action)
        {
            var monitor = AccessTools.Field(typeof(EngineInterface), "threadLock")?.GetValue(null);
            Require(monitor != null, "editor engine monitor unavailable");
            lock (monitor) return action();
        }

        private IEnumerable<AutomationWait> BuildEditorControl(AutomationRun run, MissionContext mission)
        {
            attacker = 2;
            defender = 1;
            targets.Clear();
            hitsBefore.Clear();
            hits = damagingHits = directTile = directDamage = 0;
            crenels.Clear();
            const int supportY = 423;
            const int crenelY = supportY - 1;
            Require(EditorSimulation(() => Enumerable.Range(394, 7).All(x => Enumerable.Range(421, 3).All(y => Free(x, y)))),
                "fixed editor control area is occupied");
            foreach (var (left, right) in new[] { (394, 397), (398, 400) })
            {
                EditorLine(eMappers.MAPPER_WALL, left, right, supportY);
                yield return run.WaitForTicks(mission, 30);
                Require(EditorSimulation(() => Enumerable.Range(left, right - left + 1)
                    .All(x => Is(Tiles.GetTileId(x, supportY), TilePropertyFlag.IsWall))),
                    $"editor support wall incomplete at x={left}..{right}, y={supportY}");
            }
            EditorLine(eMappers.MAPPER_CRENAL, 395, 399, crenelY);
            yield return run.WaitForTicks(mission, 30);
            for (int x = 395; x <= 399; x++)
            {
                Require(EditorSimulation(() =>
                {
                    int tile = Tiles.GetTileId(x, crenelY);
                    return Is(tile, TilePropertyFlag.CrenelatedLow) && Owner(tile) == defender;
                }), $"editor crenel placement failed at {x},{crenelY}");
                crenels.Add((x, crenelY));
            }
            spawnY = 421;
        }

        private void EditorLine(eMappers mapper, int left, int right, int y)
        {
            EditorSimulation(() =>
            {
                EditorDirector.instance.SetEditorPlayerID(defender);
                int command = (int)mapper;
                int start = EngineInterface.StartMapperItem(command);
                Require(start >= 0, "editor could not select wall mapper");
                MainControls.instance.CurrentAction = 5;
                if (start > 0) MainControls.instance.CurrentSubAction = start;
                bool inGame = !MainViewModel.Instance.IsMapEditorMode;
                EngineInterface.DLL_MapAction(command, left, y, 0, defender, inGame, true, 0);
                EngineInterface.DLL_MapAction(command, left, y, 0, defender, inGame, true, 1);
                for (int x = left; x <= right; x++)
                    EngineInterface.DLL_MapAction(command, x, y, 0, defender, inGame, true, 2);
                int release = EngineInterface.DLL_MapAction(command, right, y, 0, defender, inGame, true, 3);
                MainControls.instance.StopAllPlacement();
                Plugin.Report($"EDITOR_BUILD mapper={mapper} line={left}..{right},{y} start={start} release={release} inGame={inGame}");
                return 0;
            });
        }

        private int Spawn(IAiMilitaryCapability military)
        {
            declared = 0;
            int outside = Math.Sign(spawnY - crenels[0].Y);
            SpawnFormation(military, AiMilitaryRole.SiegeWall, 30, spawnY, outside);
            return 0;
        }

        private void SpawnFormation(IAiMilitaryCapability military, AiMilitaryRole role, int count, int y0, int outside)
        {
            var allowed = new[] { eChimps.CHIMP_TYPE_SPEARMAN, eChimps.CHIMP_TYPE_MACEMAN,
                eChimps.CHIMP_TYPE_PIKEMAN, eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_ARCHER }
                .Where(type => military.GetOptions(attacker, type, false).Any(o => o.Role == role && o.IsAvailable))
                .ToArray();
            Require(allowed.Length != 0, "No compatible Snake unit type for " + role);
            var type = allowed[0];
            var assignment = new AiMilitaryAssignment(role, AiRecruitmentMode.Regular);
            for (int i = 0; i < count; i++)
            {
                int x = spawnX - 3 + i % 6, y = y0 + i / 6 * outside;
                Require(Free(x, y), $"spawn tile occupied at {x},{y}");
                Require(military.TryBeginSpawnScope(attacker, type, assignment, false, false, admissions.Enqueue,
                    out var scope, out string reason), "Spectator placement scope failed: " + reason);
                using (scope)
                {
                    GameUnitManagerAPI.Instance.CreateUnitLocal(attacker, attacker, x, y,
                        Tiles.GetHeightLayer()[Tiles.GetTileId(x, y)], type);
                    var created = scope.Complete();
                    Require(created.Count == 1, "expected one created and captured soldier");
                    declared += created.Count;
                }
            }
            Plugin.Report($"SPAWN owner={attacker} type={type} role={role} count={count}");
        }

        private int ForceAttack()
        {
            Type runtime = Type.GetType("Spectator.SpectatorRuntime, Spectator", true);
            var selected = runtime.GetField("humanSelectedOwner", Static);
            Require(selected != null, "Spectator actor field unavailable");
            selected.SetValue(null, attacker);
            var queue = runtime.GetMethod("QueueOrder", Static);
            Require(queue != null, "Spectator order tool unavailable");
            var kind = Enum.Parse(queue.GetParameters()[0].ParameterType, "Attack");
            queue.Invoke(null, new[] { kind, (object)defender, true });
            Plugin.Report($"ATTACK_REQUEST actor={attacker} target={defender}");
            return 0;
        }

        private void OnDamage(BuildingTileTakeDamageEventArgs e)
        {
            if (e.PlayerIdSource != attacker || !Tiles.IsValidTileId(e.TileId)) return;
            if (e.Phase == EventHookPhase.Pre)
            {
                if (Is(e.TileId, TilePropertyFlag.IsWall) && Owner(e.TileId) == defender)
                    hitsBefore[e.TileId] = (Tiles.GetDamageLayer()[e.TileId], Tiles.GetHeightLayer()[e.TileId]);
                return;
            }
            if (e.Phase != EventHookPhase.Post || !hitsBefore.TryGetValue(e.TileId, out var before)) return;
            hitsBefore.Remove(e.TileId);
            var after = (Damage: Tiles.GetDamageLayer()[e.TileId], Height: Tiles.GetHeightLayer()[e.TileId]);
            hits++;
            if (before != after)
            {
                damagingHits++;
                if (e.TileId == directTile) directDamage++;
            }
            if (targets.Add(e.TileId) || hits <= 18)
                Plugin.Report($"HIT ai={aligned} tile={e.TileId} xy={e.TileX},{e.TileY} damageArg={e.Damage} phaseArg={e.Unknown1} " +
                    $"damage={before.Damage}->{after.Damage} height={before.Height}->{after.Height}");
        }

        private static IEnumerable<AutomationWait> Leave(AutomationRun run)
        {
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            yield return AutomationWait.Until("mission ended", () => run.CurrentMission == null && MenuReady());
        }

        private bool Is(int tile, TilePropertyFlag flag) => (Tiles.GetTilePropertyFlag(tile) & flag) != 0;
        private int Owner(int tile) => (Tiles.GetWallOwnerLayer()[tile] & 7) + 1;
        private static object ModSettings(string guid)
        {
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos[guid].Instance;
            return plugin.GetType().GetProperty("Settings", Instance)?.GetValue(plugin) ??
                throw new InvalidOperationException("Settings unavailable for " + guid);
        }
        private static void Set(object vm, string name, bool value)
        {
            var property = vm.GetType().GetProperty(name);
            Require(property != null, "missing setting " + name);
            property.SetValue(vm, value);
            Require((bool)property.GetValue(vm) == value, "setting did not apply: " + name);
        }
        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
        }
        public void Dispose()
        {
            damage?.Dispose();
            // Hooks belong to this short-lived test process, gated by armLobby after the final session.
            armLobby = false;
        }
    }
}
