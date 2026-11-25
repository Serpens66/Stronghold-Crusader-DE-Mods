using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrenelatedWallAcceptance
{
    // Editor acceptance without opponents. Walls are drawn through Vanilla's own UI line handler
    // (EngineInterface.PlaceMapperItem with mouse states 1, 2.., 3 like EditorDirector each frame),
    // so the red preview (0x7A3B0 status), release execution, bulldozer removal and owner slots are
    // the player's real paths. No OS input is used.
    internal sealed partial class Fixture
    {
        // UI global 0x60AD494 = TileManager 0x405EDB0 + 0x204E6E4 (invalid int, 1 = red). Hash-bound test read.
        private const long PreviewInvalidRva = 0x60AD494;
        private static readonly string[] EditorMaps = { "4spielerkarte.map", "test_assassinkeep.map" };
        private FieldInfo engineLockField;
        private int ex, ey;
        private string editorMap, vanillaMapPath;
        private readonly Dictionary<int, uint> vanillaSnapshot = new Dictionary<int, uint>();

        private IEnumerable<AutomationStep> EditorSteps()
        {
            yield return new AutomationStep("editor-load", TimeSpan.FromSeconds(180), r => LoadEditor(r, true, null));
            yield return new AutomationStep("editor-prepare", TimeSpan.FromSeconds(60), EditorPrepare, true);
            yield return new AutomationStep("editor-preview", TimeSpan.FromSeconds(180), EditorPreview, true);
            yield return new AutomationStep("editor-owners", TimeSpan.FromSeconds(120), EditorOwners, true);
            yield return new AutomationStep("editor-diagonal", TimeSpan.FromSeconds(240), EditorDiagonal, true);
            yield return new AutomationStep("editor-demolish", TimeSpan.FromSeconds(120), EditorDemolish, true);
            yield return new AutomationStep("editor-damage", TimeSpan.FromSeconds(300), EditorDamage, true);
            yield return new AutomationStep("editor-off-load", TimeSpan.FromSeconds(180), r => LoadEditor(r, false, null));
            yield return new AutomationStep("editor-off-build", TimeSpan.FromSeconds(180), EditorVanillaBuild, true);
            yield return new AutomationStep("editor-on-load", TimeSpan.FromSeconds(180), r => LoadEditor(r, true, vanillaMapPath));
            yield return new AutomationStep("editor-on-check", TimeSpan.FromSeconds(120), EditorVanillaCheck, true);
        }

        // ---------- editor session ----------
        private IEnumerable<AutomationWait> LoadEditor(AutomationRun run, bool enable, string explicitPath, bool heights = true)
        {
            foreach (var w in ToMenu(run)) yield return w;
            SetSwitches(enable, heights);
            string path = explicitPath;
            FileHeader header = null;
            if (path == null)
            {
                string maps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Firefly Studios", "Stronghold Crusader Definitive Edition", "Maps");
                foreach (string name in EditorMaps)
                {
                    string candidate = Path.Combine(maps, name);
                    if (!File.Exists(candidate)) continue;
                    var h = MapFileManager.Instance.GetFileInfoFromFileName(candidate, candidate, 0, true);
                    if (h != null && h.isMapEditable()) { path = candidate; header = h; break; }
                }
                Require(path != null, "no editable test map among " + string.Join(", ", EditorMaps));
            }
            else
            {
                header = MapFileManager.Instance.GetFileInfoFromFileName(path, path, 0, true);
                Require(header != null && header.isMapEditable(), "saved test map is not editable: " + path);
            }
            editorMap = path;
            humanTiles.Clear(); // a new editor session starts with no managed tiles
            long before = run.CurrentMission?.SessionId ?? 0;
            // Public sequence of FRONT_EditorSetup's Load handler.
            GameData.Instance.SetMissionTextFromHeader(header);
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
            Director.instance.SetPausedState(false);
            Require(EditorDirector.instance.loadMapIntoEditor(header.filePath, header.standAlone_filename), "editor load failed: " + path);
            yield return AutomationWait.Until("new editor mission", () => run.CurrentMission != null && run.CurrentMission.SessionId != before);
            Require(run.CurrentMission.Mode.IsMapEditor && !run.CurrentMission.Mode.IsRealMultiplayer, "expected a local editor mission");
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.MEMode != 1) MainViewModel.Instance.MEMode = 1;
            yield return run.WaitForTicks(run.CurrentMission, 20);
            log("EDITOR_MODE meMode=" + MainViewModel.Instance.MEMode + " appMode=" + GameData.Instance.app_mode + "/" + GameData.Instance.app_sub_mode +
                " mapEditorMode=" + MainViewModel.Instance.IsMapEditorMode);
            Drain();
            string parts = "active=" + enable + "; heights=" + (enable && heights);
            Require(crenelSeen.Any(l => l.Contains("CRENEL_SESSION") && l.Contains("editor=True") && l.Contains(parts)),
                "CrenelatedWallTest editor session " + parts + " not reported");
            log("EDITOR_LOADED map=" + path + " modEnabled=" + enable + " session=" + run.CurrentMission.SessionId);
        }

        private IEnumerable<AutomationWait> EditorPrepare(AutomationRun run)
        {
            var m = run.CurrentMission;
            string area = Engine(() =>
            {
                owner = 1;
                Require(CenterArea(12, 20, out ex, out ey), "no free 12x20 area near the map centre");
                return "area=" + ex + "," + ey + " previewAddress=0x" + PreviewAddress().ToInt64().ToString("X");
            });
            log("EDITOR_READY " + area);
            yield return run.WaitForTicks(m, 4);
        }

        // Drag, piece-by-piece, 2x2 and double-row drags: all green and built (no blockade).
        private IEnumerable<AutomationWait> EditorPreview(AutomationRun run)
        {
            var m = run.CurrentMission;
            // The editor's line preview turned red from the seventh tile on, so every drag stays short.
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 0, 2, 3, 2, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 4, 2, 6, 2, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 0, 5, 3, 5, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 4, 5, 6, 5, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 0, 9, 3, 9, null)) yield return w;
            foreach (var w in UiWall(run, m, "drag", 1, eMappers.MAPPER_CRENAL, 1, 1, 5, 1, false)) yield return w;
            for (int i = 1; i <= 5; i++) foreach (var w in UiWall(run, m, "piece", 1, eMappers.MAPPER_CRENAL, i, 4, i, 4, false)) yield return w;
            foreach (var w in UiWall(run, m, "2x2", 1, eMappers.MAPPER_CRENAL, 1, 8, 2, 8, false)) yield return w;
            foreach (var w in UiWall(run, m, "2x2", 1, eMappers.MAPPER_CRENAL, 1, 7, 1, 7, false)) yield return w;
            foreach (var w in UiWall(run, m, "2x2-close", 1, eMappers.MAPPER_CRENAL, 2, 7, 2, 7, false)) yield return w;
            // A whole second row along the dragged row: a double row, built as a checkerboard.
            foreach (var w in UiWall(run, m, "double-row", 1, eMappers.MAPPER_CRENAL, 1, 0, 5, 0, false)) yield return w;
            foreach (var w in EditorSettle(run, m)) yield return w;
            log(Engine(() => CheckHeights("editor-preview")));
        }

        // Editor builds of other slots are managed as well (map designer works with the mod enabled).
        private IEnumerable<AutomationWait> EditorOwners(AutomationRun run)
        {
            var m = run.CurrentMission;
            // Vanilla refuses building next to another owner's walls; slot 2 gets its own distant area.
            int keepX = ex, keepY = ey;
            Engine(() => { Require(CenterArea(8, 4, out ex, out ey, keepX, keepY, 40), "no free area for slot 2"); return 0; });
            log("SLOT2_AREA " + ex + "," + ey);
            foreach (var w in UiWall(run, m, "support", 2, eMappers.MAPPER_WALL, 0, 2, 3, 2, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 2, eMappers.MAPPER_WALL, 4, 2, 6, 2, null)) yield return w;
            for (int i = 1; i <= 5; i++) foreach (var w in UiWall(run, m, "slot2-piece", 2, eMappers.MAPPER_CRENAL, i, 1, i, 1, false)) yield return w;
            foreach (var w in EditorSettle(run, m)) yield return w;
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_BUILD owner=2;")), "slot-2 editor builds were not managed");
            log(Engine(() => CheckHeights("editor-owners")));
            ex = keepX; ey = keepY;
        }

        // The user's case: a screen-horizontal wall is a tile diagonal. A crenel row in front of it is
        // accepted dragged and piece by piece, alternates high/low and its low pieces are enterable.
        private IEnumerable<AutomationWait> EditorDiagonal(AutomationRun run)
        {
            var m = run.CurrentMission;
            int keepX = ex, keepY = ey;
            Engine(() => { Require(CenterArea(10, 16, out ex, out ey, keepX, keepY, 40), "no free area for the diagonal test"); return 0; });
            log("DIAGONAL_AREA " + ex + "," + ey);
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 1, 1, 6, 6, null)) yield return w;
            foreach (var w in UiWall(run, m, "diag-drag", 1, eMappers.MAPPER_CRENAL, 2, 1, 6, 5, false)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 1, 9, 6, 14, null)) yield return w;
            for (int i = 0; i <= 4; i++) foreach (var w in UiWall(run, m, "diag-piece", 1, eMappers.MAPPER_CRENAL, 2 + i, 9 + i, 2 + i, 9 + i, false)) yield return w;
            foreach (var w in EditorSettle(run, m)) yield return w;
            log(Engine(() => CheckHeights("editor-diagonal")));
            // Every low piece of both rows is reachable for an Assassin from the free side.
            int arrivedLow = 0, lowPieces = 0;
            foreach (int row in new[] { 0, 8 })
                for (int i = 0; i <= 4; i++)
                {
                    int x = 2 + i, y = 1 + row + i;
                    bool low = Engine(() => IsCrenel(Tile(x + ex, y + ey)) && (Flags(Tile(x + ex, y + ey)) & (uint)TilePropertyFlag.CrenelatedHigh) == 0);
                    if (!low) continue;
                    lowPieces++;
                    foreach (var w in EditorClimb(run, m, "diagonal-" + row + "-" + i, x + 1, y - 1, x, y)) yield return w;
                    if (climbArrived) arrivedLow++;
                }
            Require(lowPieces >= 4, "diagonal rows have too few low pieces: " + lowPieces);
            Require(arrivedLow == lowPieces, "only " + arrivedLow + "/" + lowPieces + " low diagonal pieces were enterable");
            log("DIAGONAL_PASS lowPieces=" + lowPieces + " enterable=" + arrivedLow);
            ex = keepX; ey = keepY;
        }

        // Bulldozer on the middle of the piece-by-piece row: the survivors become endpoints.
        private IEnumerable<AutomationWait> EditorDemolish(AutomationRun run)
        {
            var m = run.CurrentMission;
            int x = 3 + ex, y = 4 + ey, id = 0;
            Engine(() => { id = Tile(x, y); Require(IsCrenel(id), "demolish target is not a crenel " + Describe(id)); return 0; });
            EditorDirector.instance.SetEditorPlayerID(1);
            MainControls.instance.CurrentAction = 6; // EditorDirector.deleteBuilding (bulldozer tool)
            // The native bulldozer also reads cursor state, so drive the editor's own input path.
            foreach (var w in MouseClick(x, y, 1, 2, 2, 3)) yield return w;
            MainControls.instance.StopAllPlacement();
            bool gone = false;
            for (int i = 0; i < 30 && !gone; i++) { yield return run.WaitForTicks(m, 10); gone = Engine(() => !IsWall(id)); }
            log("DEMOLISH target=" + x + "," + y + " removed=" + gone + " tile=" + Engine(() => Describe(id)));
            Require(gone, "bulldozer did not remove the crenel");
            humanTiles.Remove(id);
            foreach (var w in EditorSettle(run, m)) yield return w;
            log(Engine(() => CheckHeights("editor-demolish")) + " survivors=" + Engine(() => Describe(Tile(x - 1, y)) + " " + Describe(Tile(x + 1, y))));
        }

        // Slot-2 macemen attack a slot-1 managed crenel until it is damaged or destroyed; then rebuild.
        private IEnumerable<AutomationWait> EditorDamage(AutomationRun run)
        {
            var m = run.CurrentMission;
            // Front row (the double row at y=0): the macemen stand directly in front of it.
            int x = 3 + ex, y = 0 + ey, id = 0, tribe = 0;
            var ids = new List<int>();
            Engine(() =>
            {
                id = Tile(x, y); Require(IsCrenel(id), "attack target is not a crenel");
                tribe = checked((int)GameTribeManagerAPI.Instance.Create(2));
                for (int i = 0; i < 3; i++)
                {
                    int tile = Tile(x - 1 + i, y - 1), unitId;
                    Require(GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(tile), "attacker spawn not free " + Describe(tile));
                    long created = GameUnitManagerAPI.Instance.CreateUnitLocal(2, 2, x - 1 + i, y - 1, GameTileManagerAPI.Instance.GetTileHeight(tile), eChimps.CHIMP_TYPE_MACEMAN);
                    Require(created > 0 && created <= int.MaxValue, "maceman spawn failed");
                    unitId = (int)created; ids.Add(unitId);
                    Require(GameTribeManagerAPI.Instance.AssignUnit(tribe, unitId), "maceman tribe assignment failed");
                }
                return 0;
            });
            yield return run.WaitForTicks(m, 10);
            bool ordered = Engine(() => GameTribeManagerAPI.Instance.AttackWall(tribe, id));
            log("DAMAGE_ORDER attackWall=" + ordered + " target=" + Engine(() => Describe(id)));
            Require(ordered, "AttackWall rejected in the editor");
            bool damaged = false, destroyed = false;
            for (int i = 0; i < 150 && !destroyed; i++)
            {
                yield return run.WaitForTicks(m, 20);
                Engine(() => { damaged |= IsWall(id) && Damaged(id); destroyed = !IsWall(id); return 0; });
                if (i % 25 == 24) log("DAMAGE_WAIT i=" + i + " tile=" + Engine(() => Describe(id) + ":damage=" + GameTileManagerAPI.Instance.GetDamageLayer()[id]));
            }
            log("DAMAGE target=" + x + "," + y + " damagedSeen=" + damaged + " destroyed=" + destroyed);
            Require(damaged || destroyed, "the attack did not damage the crenel");
            Engine(() => { foreach (int u in ids) Kill(u); return 0; });
            yield return run.WaitForTicks(m, 20);
            // The attackers may destroy more than the target before they are removed.
            string lost = Engine(() =>
            {
                var gone = humanTiles.Where(t => !IsCrenel(t)).ToList();
                foreach (int t in gone) humanTiles.Remove(t);
                return string.Join(" ", gone.Select(Describe));
            });
            log("DAMAGE_DESTROYED " + (lost.Length == 0 ? "none" : lost));
            foreach (var w in EditorSettle(run, m)) yield return w;
            log(Engine(() => CheckHeights("editor-after-damage")));
            // Rebuild the row over every damaged or destroyed tile through the UI path.
            // Rebuild the destroyed tiles, each next to an undamaged wall or crenel: a damaged
            // neighbour no longer carries a new crenel in Vanilla (red preview).
            var rebuildTiles = Engine(() => lost.Length == 0 ? new List<int>() : Enumerable.Range(1, 5).Select(rx => Tile(rx + ex, y)).Where(t => !IsWall(t)).ToList());
            for (int pass = 0; pass < 8 && rebuildTiles.Count > 0; pass++)
            {
                int next = Engine(() => rebuildTiles.FirstOrDefault(t =>
                {
                    XY(t, out int tx, out int ty);
                    return new[] { (0, -1), (1, 0), (0, 1), (-1, 0) }.Any(d => { int n = Tile(tx + d.Item1, ty + d.Item2); return IsWall(n) && !Damaged(n) && GameTileManagerAPI.Instance.GetDamageLayer()[n] == 0; });
                }));
                Require(next != 0, "no rebuildTiles tile next to an undamaged support: " + Engine(() => string.Join(" ", rebuildTiles.Select(Describe))));
                XY(next, out int rx, out int ry);
                // Killed attackers keep occupying the gap during their death animation, and Vanilla
                // refuses construction next to enemy units; give the bodies time to disappear.
                for (int i = 0; i < 60 && !Engine(() => GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(next)); i++) yield return run.WaitForTicks(m, 10);
                if (pass == 0) yield return run.WaitForTicks(m, 600);
                log("REBUILD_TILE " + Engine(() => Describe(next) + " free=" + GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(next)));
                foreach (var w in UiWall(run, m, "rebuild", 1, eMappers.MAPPER_CRENAL, rx - ex, ry - ey, rx - ex, ry - ey, false)) yield return w;
                rebuildTiles.Remove(next);
            }
            foreach (var w in EditorSettle(run, m)) yield return w;
            log(Engine(() => CheckHeights("editor-rebuild")));
        }

        // Map designer flow: mod disabled, Vanilla walls, save the map; then load it with the mod enabled.
        private IEnumerable<AutomationWait> EditorVanillaBuild(AutomationRun run)
        {
            var m = run.CurrentMission;
            Engine(() => { Require(CenterArea(12, 20, out ex, out ey), "no free area for the designer test"); return 0; });
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 0, 2, 3, 2, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 4, 2, 6, 2, null)) yield return w;
            for (int i = 1; i <= 5; i++) foreach (var w in UiWall(run, m, "vanilla-piece", 1, eMappers.MAPPER_CRENAL, i, 1, i, 1, false, unmanaged: true)) yield return w;
            foreach (var w in EditorSettle(run, m)) yield return w;
            int sticky = Engine(() =>
            {
                vanillaSnapshot.Clear(); int mismatch = 0;
                for (int i = 1; i <= 5; i++)
                {
                    int id = Tile(i + ex, 1 + ey); Require(IsCrenel(id), "vanilla row tile missing");
                    vanillaSnapshot[id] = Flags(id);
                    if (((Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0) != Parity(i + ex, 1 + ey)) mismatch++;
                }
                return mismatch;
            });
            Drain();
            Require(!crenelSeen.Any(l => l.Contains("CRENEL_BUILD")), "the disabled mod still managed a build");
            Require(sticky > 0, "disabled-mod editor row shows no Vanilla sticky high; designer case not proven");
            vanillaMapPath = Path.Combine(Path.GetTempPath(), "SerpCrenel-" + runId + ".map");
            if (File.Exists(vanillaMapPath)) File.Delete(vanillaMapPath);
            yield return AutomationWait.Until("native safe save", () => Director.instance.SafeToSave());
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1); int cx0 = -1, cy0 = -1;
            GameMap.instance.getRotationCentre(ref cx0, ref cy0);
            Require(EngineInterface.SaveSaveGame(vanillaMapPath, screen.x, screen.y, cx0, cy0, false, false, true), "editor map save rejected");
            yield return AutomationWait.Until("map file written", () => File.Exists(vanillaMapPath) && new FileInfo(vanillaMapPath).Length > 0);
            log("DESIGNER_SAVED map=" + vanillaMapPath + " vanillaStickyHigh=" + sticky + "/5");
        }

        private IEnumerable<AutomationWait> EditorVanillaCheck(AutomationRun run)
        {
            var m = run.CurrentMission;
            yield return run.WaitForTicks(m, 40);
            int changed = Engine(() => vanillaSnapshot.Count(p => Flags(p.Key) != p.Value));
            Require(changed == 0, changed + " designer crenels changed after loading with the mod enabled");
            // Extending the designer row adds one managed tile; the designer's tiles stay as they are.
            Drain();
            foreach (var w in UiWall(run, m, "designer-extend", 1, eMappers.MAPPER_CRENAL, 6, 1, 6, 1, false)) yield return w;
            foreach (var w in EditorSettle(run, m)) yield return w;
            int after = Engine(() => vanillaSnapshot.Count(p => Flags(p.Key) != p.Value));
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_BUILD") && l.Contains("managed=1.")), "extension was not managed as a single new tile");
            Require(after == 0, after + " designer crenels changed after extending the row");
            log(Engine(() => CheckHeights("editor-designer-extend")) + " designerTilesUnchanged=" + vanillaSnapshot.Count);
            try { File.Delete(vanillaMapPath); } catch (Exception error) { log("TEMP_MAP_DELETE_FAILED " + error.Message); }
            log("EDITOR_PASS preview, owners, bulldozer, damage, designer map");
        }

        // ---------- UI wall drag ----------
        // expectRed: null for support walls (not checked), false = green and built, true = red and nothing built.
        private IEnumerable<AutomationWait> UiWall(AutomationRun run, MissionContext m, string label, int player, eMappers mapper,
            int x0, int y0, int x1, int y1, bool? expectRed, bool unmanaged = false)
        {
            int fx = x0 + ex, fy = y0 + ey, tx = x1 + ex, ty = y1 + ey;
            Drain();
            int redSeen = 0, greenSeen = 0, startResult = 0;
            List<int> fresh = null;
            Engine(() =>
            {
                EditorDirector.instance.SetEditorPlayerID(player);
                startResult = EngineInterface.StartMapperItem((int)mapper);
                if (startResult >= 0) { MainControls.instance.CurrentAction = 5; if (startResult > 0) MainControls.instance.CurrentSubAction = startResult; }
                var cells = Raster(fx, fy, tx, ty);
                fresh = cells.Where(id => !IsWall(id)).ToList();
                bool inGame = !MainViewModel.Instance.IsMapEditorMode;
                // One uninterrupted gesture under the engine monitor: hover, press, drag, release.
                trace.Clear();
                Gesture((int)mapper, fx, fy, player, inGame, 0);
                Gesture((int)mapper, fx, fy, player, inGame, 1);
                int x = fx, y = fy;
                while (true)
                {
                    Gesture((int)mapper, x, y, player, inGame, 2);
                    if (PreviewInvalid() != 0) redSeen++; else greenSeen++;
                    if (x == tx && y == ty) break;
                    x += Math.Sign(tx - x); y += Math.Sign(ty - y);
                }
                Gesture((int)mapper, tx, ty, player, inGame, 3);
                MainControls.instance.StopAllPlacement();
                return 0;
            });
            yield return run.WaitForTicks(m, 30); // release command executes in the simulation
            log("UI_TRACE " + label + " " + string.Join(" ", trace));
            string result = Engine(() =>
            {
                int built = fresh.Count(id => IsWall(id));
                if (expectRed == null)
                {
                    Require(built == fresh.Count, label + ": support wall incomplete " + built + "/" + fresh.Count + " start=" + startResult +
                        " action=" + MainControls.instance.CurrentAction + " meMode=" + MainViewModel.Instance.MEMode + " appMode=" + GameData.Instance.app_mode + "/" + GameData.Instance.app_sub_mode +
                        " first=" + Describe(fresh.Count > 0 ? fresh[0] : Tile(fx, fy)));
                    return "SUPPORT player=" + player + " line=" + fx + "," + fy + "->" + tx + "," + ty;
                }
                Require(mapper != eMappers.MAPPER_CRENAL || fresh.All(id => !IsWall(id) || IsCrenel(id)), label + ": unexpected wall type");
                if (expectRed == true)
                {
                    Require(redSeen > 0, label + ": preview stayed green (red=" + redSeen + " green=" + greenSeen + ")");
                    Require(built == 0, label + ": red line was still built (" + built + ")");
                }
                else
                {
                    Require(redSeen == 0, label + ": preview was red (red=" + redSeen + " green=" + greenSeen + ")");
                    foreach (int id in fresh)
                    {
                        Require(IsCrenel(id) && Owner(id) == player, label + ": tile " + Describe(id) + " not built for player " + player);
                        if (!unmanaged) humanTiles.Add(id);
                    }
                }
                return "UI_WALL " + label + " start=" + startResult + " player=" + player + " line=" + fx + "," + fy + "->" + tx + "," + ty + " red=" + redSeen + " green=" + greenSeen + " built=" + built;
            });
            log(result);
            yield return run.WaitForTicks(m, 4);
        }

        // The editor has no gameplay pause API; let render maintenance and the mod's redraw checks run.
        private IEnumerable<AutomationWait> EditorSettle(AutomationRun run, MissionContext m) { yield return run.WaitForTicks(m, 80); }

        private readonly List<string> trace = new List<string>();
        // Same call and post-processing as EngineInterface.PlaceMapperItem, keeping the native result.
        private void Gesture(int mapper, int x, int y, int player, bool inGame, int state)
        {
            int result = EngineInterface.DLL_MapAction(mapper, x, y, 0, player, inGame, true, state);
            if (result >= 0) { if (MainViewModel.Instance.MEMode == 0 && Enum.IsDefined(typeof(Enums.eMappers), result)) EditorDirector.instance.mapEditorInteraction((Enums.eMappers)result); }
            else if (result == -50) EditorDirector.instance.CancelPlacement();
            int id = Tile(x, y);
            trace.Add(state + "@" + x + "," + y + "=" + result + "/inv" + PreviewInvalid() + "/f" + Flags(id).ToString("X") + "/h" + GameTileManagerAPI.Instance.GetTileHeight(id) +
                "/b" + GameTileManagerAPI.Instance.GetTileBuildingId(id) + "/u" + (GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(id) ? 1 : 0));
        }

        private IntPtr PreviewAddress()
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                if (string.Equals(module.ModuleName, "CrusaderDE.dll", StringComparison.OrdinalIgnoreCase))
                    return new IntPtr(module.BaseAddress.ToInt64() + PreviewInvalidRva);
            throw new InvalidOperationException("CrusaderDE.dll module not found");
        }
        private IntPtr previewAddress;
        private int PreviewInvalid()
        {
            if (previewAddress == IntPtr.Zero) previewAddress = PreviewAddress();
            return Marshal.ReadInt32(previewAddress);
        }

        // ---------- editor engine access ----------
        private T Engine<T>(Func<T> action)
        {
            if (engineLockField == null)
            {
                engineLockField = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
                Require(engineLockField != null && engineLockField.FieldType == typeof(object), "EngineInterface.threadLock contract changed");
            }
            object monitor = engineLockField.GetValue(null);
            Require(monitor != null, "engine monitor unavailable");
            if (!Monitor.TryEnter(monitor, 2000)) throw new InvalidOperationException("engine monitor busy");
            try { return action(); }
            finally { Monitor.Exit(monitor); }
        }

        private bool CenterArea(int w, int h, out int bx, out int by, int awayX = 0, int awayY = 0, int minDistance = 0)
        {
            var tiles = GameTileManagerAPI.Instance;
            for (int r = 0; r <= 200; r += 3)
                for (int dy = -r; dy <= r; dy += 3)
                    for (int dx = -r; dx <= r; dx += 3)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x0 = 400 + dx, y0 = 400 + dy;
                        if (minDistance > 0 && Math.Max(Math.Abs(x0 - awayX), Math.Abs(y0 - awayY)) < minDistance) continue;
                        if (Free(x0, y0, w, h, 0))
                        { bx = x0; by = y0; return true; }
                    }
            bx = by = 0; return false;
        }
    }
}
