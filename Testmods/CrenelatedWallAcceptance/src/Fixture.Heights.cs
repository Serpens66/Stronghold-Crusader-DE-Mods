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
    // Height adjustment: any build order alternates, double rows and blocks are
    // built, and crenels of the automatic AI (native 0x6CB20, SE OnAIBuildWall) are adopted.
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> HeightsSteps()
        {
            yield return new AutomationStep("heights-editor-load", TimeSpan.FromSeconds(180), r => LoadEditor(r, true, null, heights: true));
            yield return new AutomationStep("editor-prepare", TimeSpan.FromSeconds(60), EditorPrepare, true);
            yield return new AutomationStep("heights-any-order", TimeSpan.FromSeconds(420), HeightsAnyOrder, true);
            yield return new AutomationStep("heights-diagonal", TimeSpan.FromSeconds(240), EditorDiagonal, true);
            yield return new AutomationStep("heights-frame", TimeSpan.FromSeconds(420), HeightsFrame, true);
            yield return new AutomationStep("heights-local-redraw", TimeSpan.FromSeconds(180), HeightsLocalRedraw, true);
        }

        // Camera rotation and the flatten toggle re-run Vanilla's tile graphics builder (65830) over
        // the whole map on this peer only, and its special branch ORs the logic high bit (65EF0). No
        // managed crenel may change; in a /devdiag build no redraw line may report height writes.
        // Four right rotations and two toggles restore the original view.
        private IEnumerable<AutomationWait> HeightsLocalRedraw(AutomationRun run)
        {
            var m = run.CurrentMission;
            foreach (var w in EditorSettle(run, m)) yield return w;
            Drain(); crenelSeen.Clear();
            Dictionary<int, uint> before = Engine(() => humanTiles.ToDictionary(id => id, id => Flags(id)));
            int lowBefore = before.Count(p => (p.Value & (uint)TilePropertyFlag.CrenelatedHigh) == 0);
            Require(lowBefore > 0, "no low managed crenel before the local redraw");
            for (int i = 0; i < 4; i++) { GameMap.instance.RotateMapRight(); yield return run.WaitForTicks(m, 4); }
            for (int i = 0; i < 2; i++) { EngineInterface.toggleFlattenedLandscapeMode(); yield return run.WaitForTicks(m, 4); }
            foreach (var w in EditorSettle(run, m)) yield return w;
            Drain();
            List<string> changed = Engine(() => before.Where(p => Flags(p.Key) != p.Value).Select(p => Describe(p.Key) + " was=" + p.Value.ToString("X")).ToList());
            Require(changed.Count == 0, "local redraw changed managed crenels: " + string.Join(" | ", changed));
            List<string> writes = crenelSeen.Where(l => l.Contains("CRENEL_MP_REDRAW") && !l.Contains("heightWrites=0;")).ToList();
            Require(writes.Count == 0, "redraw normalization wrote heights: " + string.Join(" | ", writes));
            log(Engine(() => CheckHeights("heights-local-redraw")) + " lowBefore=" + lowBefore + " rotations=4 flattenToggles=2 redrawLines=" +
                crenelSeen.Count(l => l.Contains("CRENEL_MP_REDRAW")));
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
        // Every AI wall placement of the current start: position, mapper and the logic flags right after it.
        private readonly List<(int x, int y, eMappers mapper, uint flags)> aiPlaced = new List<(int, int, eMappers, uint)>();

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
            // The lobby lists only the human; AI keeps would be chosen natively. The test opponents
            // are the next player indices (one AI lord), assigned explicitly like Vanilla's trail
            // missions do (FRONT_Multiplayer: order[location - 1] = player index).
            var occupants = order.Where(v => v >= 0).Concat(Enumerable.Range(1, 1)).Distinct().OrderBy(v => v).ToList();
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

        // Two rectangular frames (user screenshots 2026-11-25): long sides joined by three-piece end
        // rows, at both map parities and with an odd (5) and an even (6) run between the corners,
        // built piece by piece in a shuffled order. Every piece, corners included, follows the map
        // checkerboard (CheckHeights); the rows are logged for the record.
        private IEnumerable<AutomationWait> HeightsFrame(AutomationRun run)
        {
            var m = run.CurrentMission;
            int keepX = ex, keepY = ey;
            Engine(() => { Require(CenterArea(19, 6, out ex, out ey, keepX, keepY, 40), "no free area for the frame test"); return 0; });
            log("FRAME_AREA " + ex + "," + ey);
            foreach (int y in new[] { 0, 4 })
                for (int x = 0; x <= 18; x += 6)
                    foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, x, y, Math.Min(x + 5, 18), y, null)) yield return w;
            // Plain walls beside the end-row middles, so every crenel has support in any build order.
            foreach (int x in new[] { 0, 8, 9, 18 })
                foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, x, 2, x, 2, null)) yield return w;
            var frames = new[] { (1, 7), (10, 8) }; // (left x, width): end rows at both parities
            var pieces = new List<(int, int)>();
            foreach (var (x0, width) in frames)
            {
                for (int x = x0; x < x0 + width; x++) { pieces.Add((x, 1)); pieces.Add((x, 3)); }
                pieces.Add((x0, 2)); pieces.Add((x0 + width - 1, 2));
            }
            var random = new Random(20261125);
            foreach (var p in pieces.OrderBy(_ => random.Next()).ToList())
                foreach (var w in UiWall(run, m, "frame-piece", 1, eMappers.MAPPER_CRENAL, p.Item1, p.Item2, p.Item1, p.Item2, false)) yield return w;
            foreach (var w in EditorSettle(run, m)) yield return w;
            string result = Engine(() =>
            {
                var cells = new List<string>();
                foreach (var (x0, width) in frames)
                {
                    int x1 = x0 + width - 1;
                    for (int y = 1; y <= 3; y++)
                    {
                        var row = new System.Text.StringBuilder();
                        for (int x = x0; x <= x1; x++) row.Append(!IsCrenel(Tile(x + ex, y + ey)) ? '.' : IsHigh(x + ex, y + ey) ? 'H' : 'L');
                        cells.Add(row.ToString());
                    }
                }
                return CheckHeights("heights-frame") + " rows=" + string.Join(",", cells);
            });
            log(result);
            ex = keepX; ey = keepY;
        }
        private static bool IsHigh(int x, int y) { int id = Tile(x, y); return IsCrenel(id) && (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0; }

        // Single pieces in a shuffled order (gaps closed later), a 2x2 block (a former blockade
        // case), an L and a row dragged in reverse halves and closed piece by piece.
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
                Require(new[] { (0, 3), (1, 3), (0, 4), (1, 4) }.All(c => IsCrenel(Tile(c.Item1 + ex, c.Item2 + ey))), "2x2 block not built");
                return 0;
            });
            Drain();
            log(Engine(() => CheckHeights("heights-any-order")));
            ex = keepX; ey = keepY;
        }

        private IEnumerable<AutomationWait> AiStart(AutomationRun run, int preBuild)
        {
            foreach (var w in ToMenu(run)) yield return w;
            SetSwitches(true, true);
            humanTiles.Clear();
            aiCallSubscription?.Dispose();
            lock (aiCalls) aiPlaced.Clear();
            aiCallSubscription = AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(e =>
            {
                if (e.Phase != EventHookPhase.Post) return;
                lock (aiCalls)
                {
                    aiCalls[e.Mappers] = (aiCalls.TryGetValue(e.Mappers, out int n) ? n : 0) + 1;
                    if (aiPlaced.Count < 4000) aiPlaced.Add((e.TileX, e.TileY, e.Mappers, Flags(GameTileManagerAPI.Instance.GetTileId(e.TileX, e.TileY))));
                }
            });
            // Peace time: without it the AI killed the human lord during the long build (run 682010f4).
            // (Walls that seemed to vanish with pre-built castles were a counting error of AiWalls.)
            int peace = PeaceMinutes;
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
            log(run.InSimulation(m, () => PlacedState("prepare")));
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 400); // Vanilla render maintenance (sticky highs)
            foreach (var w in run.SetPause(m, true)) yield return w;
            Drain();
            string line = crenelSeen.LastOrDefault(l => l.Contains("CRENEL_AI_PREBUILT"));
            Require(line != null, "no CRENEL_AI_PREBUILT decision logged");
            Require(!crenelSeen.Any(l => l.Contains("CRENEL_CALLBACK_ERROR")), "runtime error while adopting the pre-built castle");
            log(run.InSimulation(m, () => PlacedState("after400")));
            string result = run.InSimulation(m, () =>
            {
                humanTiles.Clear();
                var tiles = GameTileManagerAPI.Instance; int size = tiles.GetCurrentMapSize();
                // Tile coordinates span the full native axes (a 400 map reaches x/y beyond 400), not 0..size.
                for (int y = 0; y < GameTileManagerAPI.MAX_HEIGHT; y++) for (int x = 0; x < GameTileManagerAPI.MAX_WIDTH; x++)
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

        // What stands now at every position the AI placed a wall piece during this start: counts of
        // the current logic flags, structure and owner, plus samples with the flags right after placement.
        private string PlacedState(string stage)
        {
            List<(int x, int y, eMappers mapper, uint flags)> placed;
            lock (aiCalls) placed = aiPlaced.ToList();
            var tiles = GameTileManagerAPI.Instance;
            var states = new Dictionary<string, int>();
            var samples = new List<string>();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in placed)
            {
                minX = Math.Min(minX, p.x); minY = Math.Min(minY, p.y); maxX = Math.Max(maxX, p.x); maxY = Math.Max(maxY, p.y);
                int id = Tile(p.x, p.y);
                uint now = Flags(id);
                int structure = tiles.GetStructureLayer()[id];
                string key = "wall=" + ((now & (uint)TilePropertyFlag.IsWall) != 0 ? 1 : 0) + "/crenel=" + ((now & (uint)TilePropertyFlag.CrenelatedLow) != 0 ? 1 : 0) +
                    "/structure=" + (structure != 0 ? 1 : 0) + "/owner=" + Owner(id);
                states[key] = (states.TryGetValue(key, out int c) ? c : 0) + 1;
                if (samples.Count < 12) samples.Add(p.x + "," + p.y + ":" + p.mapper + ":after=" + p.flags.ToString("X") + ":now=" + now.ToString("X") +
                    ":struct=" + structure + ":h=" + tiles.GetTileHeight(id) + ":unit=" + tiles.GetChimpLayer()[id]);
            }
            return "PLACED_STATE " + stage + " ai=" + ai + " calls=" + placed.Count + " area=" + minX + "," + minY + "-" + maxX + "," + maxY +
                " keep=" + GamePlayerManagerAPI.Instance.GetPlayerKeepId(ai) + " states=" + string.Join(" ", states.OrderByDescending(k => k.Value).Select(k => k.Key + ":" + k.Value)) +
                " samples=" + string.Join(" ", samples);
        }

        // AI-owned wall and crenel tiles on the whole map.
        private string AiWalls()
        {
            var tiles = GameTileManagerAPI.Instance; int size = tiles.GetCurrentMapSize();
            var walls = new int[9]; var crenels = new int[9];
            // Tile coordinates span the full native axes (a 400 map reaches x/y beyond 400), not 0..size.
            for (int y = 0; y < GameTileManagerAPI.MAX_HEIGHT; y++) for (int x = 0; x < GameTileManagerAPI.MAX_WIDTH; x++)
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
        // Options "align existing crenels" (editor load, savegame load, map start); frozen per session.
        private void SetExisting(bool editor, bool saveLoad, bool mapStart)
        {
            object settings = WallSettings();
            foreach (var entry in new[] { ("AlignExistingCrenelsInEditor", editor), ("AlignExistingCrenelsOnSaveLoad", saveLoad), ("AlignExistingCrenelsOnMapStart", mapStart) })
            {
                var property = settings.GetType().GetProperty(entry.Item1) ?? throw new MissingMemberException(entry.Item1);
                property.SetValue(settings, entry.Item2);
                Require((bool)property.GetValue(settings) == entry.Item2, entry.Item1 + " could not be set to " + entry.Item2);
            }
            log("SETTINGS existing editor=" + editor + " save=" + saveLoad + " map=" + mapStart);
        }
        private void SetSwitches(bool enable, bool heights)
        {
            object settings = WallSettings();
            foreach (var entry in new[] { ("EnableCrenelCheckerboard", enable && heights) })
            {
                var property = settings.GetType().GetProperty(entry.Item1) ?? throw new MissingMemberException(entry.Item1);
                property.SetValue(settings, entry.Item2);
                Require((bool)property.GetValue(settings) == entry.Item2, entry.Item1 + " could not be set to " + entry.Item2);
            }
            log("SETTINGS enable=" + enable + " heights=" + heights);
            // Test world: the "align existing crenels" options are personal settings the user may have
            // left on; every session start defines them (off unless a step sets them afterwards).
            SetExisting(false, false, false);
        }
    }
}
