using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using System.Text.RegularExpressions;
using APIShared.Automation;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrenelatedWallAcceptance
{
    // Height adjustment alone (blockade off): any build order alternates, forbidden shapes are
    // built, and crenels of the automatic AI (native 0x6CB20, SE OnAIBuildWall) are adopted.
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> HeightsSteps()
        {
            yield return new AutomationStep("heights-editor-load", TimeSpan.FromSeconds(180), r => LoadEditor(r, true, null, heights: true, block: false));
            yield return new AutomationStep("editor-prepare", TimeSpan.FromSeconds(60), EditorPrepare, true);
            yield return new AutomationStep("heights-any-order", TimeSpan.FromSeconds(420), HeightsAnyOrder, true);
            yield return new AutomationStep("heights-diagonal", TimeSpan.FromSeconds(240), EditorDiagonal, true);
            foreach (var step in AiSteps()) yield return step;
        }

        internal IEnumerable<AutomationStep> AiSteps()
        {
            yield return new AutomationStep("prebuilt-start", TimeSpan.FromSeconds(240), r => AiStart(r, 1));
            yield return new AutomationStep("prepare", TimeSpan.FromSeconds(90), Prepare, true);
            yield return new AutomationStep("prebuilt-check", TimeSpan.FromSeconds(180), PrebuiltCheck, true);
            yield return new AutomationStep("heights-ai-start", TimeSpan.FromSeconds(240), r => AiStart(r, 0));
            yield return new AutomationStep("prepare-manual", TimeSpan.FromSeconds(90), Prepare, true);
            yield return new AutomationStep("heights-ai-build", TimeSpan.FromSeconds(900), AiBuild, true);
        }

        // Raw 0x6CB20 calls per mapper, independent of the mod (diagnoses whether the AI builds at all).
        private readonly Dictionary<eMappers, int> aiCalls = new Dictionary<eMappers, int>();
        private IDisposable aiCallSubscription;

        // Lobby option "pre-built castles" (MPsetupData.advopt_pre_build) for the next start only:
        // 1 = the AI castle is placed at mission start, 0 = the AI builds it during the match.
        private static Harmony lobbyHarmony;
        private static int forcedPreBuild = -1, forcedPeace = -1;
        // Peace time (minutes): Vanilla hides the slider in AI Skirmish (Show_MPPeacetime false) and
        // ApplySettings then writes 0, so the test sets MPsetupData.peacetime directly. Prepare checks
        // the native peace flag afterwards, so an ignored value fails the run instead of passing silently.
        private static void BeforeSkirmishStart(FRONT_Multiplayer __instance)
        {
            if (forcedPreBuild < 0 && forcedPeace < 0) return;
            var field = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException("MPsetupData");
            var data = (EngineInterface.MultiplayerSetupData)field.GetValue(__instance);
            if (forcedPreBuild >= 0) { Plugin.Stamp("LOBBY pre_build=" + data.advopt_pre_build + " -> " + forcedPreBuild); data.advopt_pre_build = forcedPreBuild; }
            if (forcedPeace >= 0) { Plugin.Stamp("LOBBY peacetime=" + data.peacetime + " -> " + forcedPeace); data.peacetime = forcedPeace; }
            forcedPreBuild = forcedPeace = -1;
        }
        // Fixed AI castle and start positions (test world, not chance). Built-in lords get a random
        // default AIV and rotation: StartSkirmishGame writes preferredAIVs = -1 - rotation, and native
        // 0x94350 sends negative selectors to the random chooser 0x54F60. A selector >= 0 is read as
        // rotation*100 + candidate: candidate goes to the fixed fit 0x54DE0, rotation (v/100)*2-2
        // (100 = 0 degrees). The keep order maps keep locations to player indices. Both are set
        // in the managed setup data right before EngineInterface.setMultiplayerStartingData.
        private const int FixedAiv = 100;
        private static bool forceWorld;
        private static void BeforeStartingData(EngineInterface.MultiplayerSetupData setupData)
        {
            if (!forceWorld || setupData == null) return;
            forceWorld = false;
            string beforeAiv = string.Join(",", setupData.preferredAIVs), beforeKeeps = string.Join(",", setupData.start_keep_location_order);
            for (int i = 1; i < setupData.preferredAIVs.Length; i++) setupData.preferredAIVs[i] = FixedAiv;
            int[] order = setupData.start_keep_location_order;
            var occupants = order.Where(v => v >= 0).OrderBy(v => v).ToList();
            int free = order.Any(v => v < 0) ? order.First(v => v < 0) : -10;
            int[,] keeps = GameData.Instance.Keep_Locations;
            for (int k = 0; k < order.Length; k++) order[k] = free;
            for (int k = 0, next = 0; k < order.Length && next < occupants.Count; k++)
                if (keeps[k, 0] >= 0) order[k] = occupants[next++];
            Require(occupants.All(o => order.Contains(o)), "not enough keep locations for the fixed order");
            Plugin.Stamp("LOBBY fixed world preferredAIVs " + beforeAiv + " -> " + string.Join(",", setupData.preferredAIVs) +
                "; keepOrder " + beforeKeeps + " -> " + string.Join(",", order));
        }
        // Arms the one-shot lobby overrides for the next StartSkirmishGame.
        private static void ArmLobby(int preBuild, int peace, bool fixedWorld = false)
        {
            if (lobbyHarmony == null)
            {
                lobbyHarmony = new Harmony(Plugin.Guid + ".lobby");
                lobbyHarmony.Patch(AccessTools.Method(typeof(FRONT_Multiplayer), "StartSkirmishGame"),
                    prefix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(BeforeSkirmishStart), BindingFlags.Static | BindingFlags.NonPublic)));
                lobbyHarmony.Patch(AccessTools.Method(typeof(EngineInterface), nameof(EngineInterface.setMultiplayerStartingData)),
                    prefix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(BeforeStartingData), BindingFlags.Static | BindingFlags.NonPublic)));
            }
            forcedPreBuild = preBuild; forcedPeace = peace; expectPeace = peace > 0; forceWorld = fixedWorld;
        }

        // Single pieces in a shuffled order (gaps closed later), a 2x2 block that the blockade
        // would reject, an L and a row dragged in reverse halves and closed piece by piece.
        private IEnumerable<AutomationWait> HeightsAnyOrder(AutomationRun run)
        {
            var m = run.CurrentMission;
            int keepX = ex, keepY = ey;
            Engine(() => { Require(CenterArea(12, 10, out ex, out ey, keepX, keepY, 40), "no free area for the build-order test"); return 0; });
            log("ORDER_AREA " + ex + "," + ey);
            foreach (int y in new[] { 2, 5, 8 })
            {
                foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 0, y, 5, y, null)) yield return w;
                foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 6, y, 10, y, null)) yield return w;
            }
            var pieces = new List<(int, int)>();
            for (int x = 0; x <= 9; x++) pieces.Add((x, 1));                     // straight row
            pieces.AddRange(new[] { (0, 3), (1, 3), (0, 4), (1, 4) });            // 2x2 block
            for (int x = 4; x <= 8; x++) pieces.Add((x, 4)); pieces.Add((8, 3)); // L
            var random = new Random(20261124);
            foreach (var p in pieces.OrderBy(_ => random.Next()).ToList())
                foreach (var w in UiWall(run, m, "order-piece", 1, eMappers.MAPPER_CRENAL, p.Item1, p.Item2, p.Item1, p.Item2, false)) yield return w;
            foreach (var w in UiWall(run, m, "reverse-drag", 1, eMappers.MAPPER_CRENAL, 9, 7, 6, 7, false)) yield return w;
            foreach (var w in UiWall(run, m, "reverse-drag", 1, eMappers.MAPPER_CRENAL, 0, 7, 3, 7, false)) yield return w;
            foreach (int x in new[] { 5, 4 }) foreach (var w in UiWall(run, m, "close-gap", 1, eMappers.MAPPER_CRENAL, x, 7, x, 7, false)) yield return w;
            foreach (var w in EditorSettle(run, m)) yield return w;
            Engine(() =>
            {
                Require(new[] { (0, 3), (1, 3), (0, 4), (1, 4) }.All(c => IsCrenel(Tile(c.Item1 + ex, c.Item2 + ey))), "2x2 block not built with the blockade off");
                return 0;
            });
            Drain();
            Require(!crenelSeen.Any(l => l.Contains("CRENEL_REJECT")), "a build was rejected although the blockade is off");
            log(Engine(() => CheckHeights("heights-any-order")));
            ex = keepX; ey = keepY;
        }

        private IEnumerable<AutomationWait> AiStart(AutomationRun run, int preBuild)
        {
            foreach (var w in ToMenu(run)) yield return w;
            SetSwitches(true, true, false);
            humanTiles.Clear();
            aiCallSubscription?.Dispose();
            aiCallSubscription = AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(e =>
            {
                if (e.Phase != EventHookPhase.Post) return;
                lock (aiCalls) aiCalls[e.Mappers] = (aiCalls.TryGetValue(e.Mappers, out int n) ? n : 0) + 1;
            });
            // Peace time only for the long manual build: without it the AI killed the human lord
            // (run 682010f4). With pre-built castles and peace time, all AI walls were gone 20 ticks
            // after the start (run 2fc44ab9, Vanilla cause not investigated); that check is short.
            int peace = preBuild == 0 ? PeaceMinutes : 0;
            ArmLobby(preBuild, peace, fixedWorld: true);
            log("AI run map=4spielerkarte.map opponent=SK_WOLF; heights only; preBuild=" + preBuild + "; peace=" + peace + "min");
            foreach (var wait in run.StartSkirmish("4spielerkarte.map", new[] { AILords.SK_WOLF })) yield return wait;
        }

        // Runs the simulation until the AI has placed crenels, then checks every adopted AI tile.
        private IEnumerable<AutomationWait> AiBuild(AutomationRun run)
        {
            var m = run.CurrentMission;
            var positions = new Regex(@"CRENEL_AI_BUILD owner=(\d+); position=(\d+),(\d+);");
            var adopted = new HashSet<int>();
            // Prepare zeroes wall costs for the human tests; the AI builds with normal Vanilla costs.
            run.InSimulation(m, () => { GameBuildingManagerAPI.Instance.SetLowWallCostMultiplier(1f); GameBuildingManagerAPI.Instance.SetHighWallCostMultiplier(1f); return 0; });
            Require(forcedPreBuild < 0, "the lobby start hook did not run");
            foreach (var w in run.SetPause(m, false)) yield return w;
            for (int waited = 0; waited < 150000 && adopted.Count < 16; waited += 500)
            {
                bool stocked = run.InSimulation(m, () => GamePlayerManagerAPI.Instance.TryAddGood(ai, eGoods.STORED_STONE_BLOCKS, 50));
                yield return run.WaitForTicks(m, 500);
                Drain();
                foreach (string line in crenelSeen)
                {
                    Match match = positions.Match(line);
                    if (match.Success) adopted.Add(Tile(int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)));
                }
                if (waited % 10000 == 0)
                {
                    string calls; lock (aiCalls) calls = string.Join(",", aiCalls.Select(p => p.Key + ":" + p.Value));
                    string walls = run.InSimulation(m, () => AiWalls());
                    log("AI_PROGRESS ticks=" + waited + " adopted=" + adopted.Count + " stocked=" + stocked +
                        " aiStone=" + GamePlayerManagerAPI.Instance.GetGoodAmount(ai, eGoods.STORED_STONE_BLOCKS) + " calls=" + calls + " " + walls);
                }
            }
            // Vanilla's renderer raises special pieces (endpoints) later; check after that maintenance.
            yield return run.WaitForTicks(m, 400);
            foreach (var w in run.SetPause(m, true)) yield return w;
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_AI_HOOK_CONFIRMED")), "no AI crenel construction was adopted");
            Require(!crenelSeen.Any(l => l.Contains("CRENEL_CALLBACK_ERROR")), "runtime error during AI construction");
            string result = run.InSimulation(m, () =>
            {
                humanTiles.Clear();
                foreach (int id in adopted) if (IsCrenel(id) && Owner(id) == ai) humanTiles.Add(id);
                int low = humanTiles.Count(id => (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) == 0);
                return CheckHeights("heights-ai") + " adopted=" + adopted.Count + " aiLow=" + low;
            });
            log(result);
        }

        // The pre-built castle exists before the mission is ready; the mod adopts it at Started.
        private IEnumerable<AutomationWait> PrebuiltCheck(AutomationRun run)
        {
            var m = run.CurrentMission;
            Require(forcedPreBuild < 0 && !forceWorld, "the lobby start hooks did not run");
            log("PREBUILT_WALLS " + run.InSimulation(m, () => AiWalls()));
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 400); // Vanilla render maintenance (sticky highs)
            foreach (var w in run.SetPause(m, true)) yield return w;
            Drain();
            string line = crenelSeen.LastOrDefault(l => l.Contains("CRENEL_AI_PREBUILT"));
            Require(line != null, "no CRENEL_AI_PREBUILT decision logged");
            Require(!crenelSeen.Any(l => l.Contains("CRENEL_CALLBACK_ERROR")), "runtime error while adopting the pre-built castle");
            string result = run.InSimulation(m, () =>
            {
                humanTiles.Clear();
                var tiles = GameTileManagerAPI.Instance; int size = tiles.GetCurrentMapSize();
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                    int id = Tile(x, y);
                    if (IsCrenel(id) && Owner(id) == ai) humanTiles.Add(id);
                }
                Require(humanTiles.Count > 0, "the pre-built AI castle has no crenels");
                return CheckHeights("prebuilt") + " aiCrenels=" + humanTiles.Count;
            });
            log(result + " | " + line.Substring(line.IndexOf("CRENEL_AI_PREBUILT", StringComparison.Ordinal)));
            humanTiles.Clear();
        }

        // AI-owned wall and crenel tiles on the whole map.
        private string AiWalls()
        {
            var tiles = GameTileManagerAPI.Instance; int size = tiles.GetCurrentMapSize();
            var walls = new int[9]; var crenels = new int[9];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                int id = tiles.GetTileId(x, y);
                if (!IsWall(id)) continue;
                int o = (tiles.GetWallOwnerLayer()[id] & 7) + 1;
                walls[o]++; if (IsCrenel(id)) crenels[o]++;
            }
            return "size=" + size + " ai=" + ai + " wallsByOwner=" + string.Join("/", walls.Skip(1)) + " crenelsByOwner=" + string.Join("/", crenels.Skip(1));
        }

        private IEnumerable<AutomationWait> ToMenu(AutomationRun run)
        {
            if (run.CurrentMission != null)
            {
                Drain(); crenelSeen.Clear();
                MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            }
            yield return AutomationWait.Until("ready main menu", () => run.CurrentMission == null && MainViewModel.viewModelLoaded &&
                FatControler.currentScene == Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu &&
                !MainViewModel.Instance.Show_IntroSequence && MainViewModel.Instance.HUDmain != null && EditorDirector.instance != null && Director.instance != null);
        }

        // Host settings, changed only in the menu; the mod freezes them per session.
        private void SetSwitches(bool enable, bool heights, bool block)
        {
            object settings = WallSettings();
            foreach (var entry in new[] { ("EnableMod", enable), ("AdjustHeights", heights), ("BlockPlacements", block) })
            {
                var property = settings.GetType().GetProperty(entry.Item1) ?? throw new MissingMemberException(entry.Item1);
                property.SetValue(settings, entry.Item2);
                Require((bool)property.GetValue(settings) == entry.Item2, entry.Item1 + " could not be set to " + entry.Item2);
            }
            log("SETTINGS enable=" + enable + " heights=" + heights + " block=" + block);
        }
    }
}
