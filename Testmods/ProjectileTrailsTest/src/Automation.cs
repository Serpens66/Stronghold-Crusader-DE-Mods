using System;
using System.Collections.Generic;
using System.IO;
using APIShared;
using APIShared.Automation;
using BepInEx;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace ProjectileTrailsTest
{
    public sealed partial class Plugin
    {
        private const string ExtremeSave = "test_stresstest5.sav";
        private const string StandardSave = "test_gates_vanilla.sav";
        private const int MeasuredSpeed = 40;
        private const int FineCoordinatesPerTile = 8;
        private static CameraControls2D fixtureCamera;
        private static bool previousCameraMovement;

        private static void StartAutomation(ModApiClient api)
        {
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2) return;

            var steps = new List<AutomationStep>();
            switch (lines[1])
            {
                case "trails-green-standard":
                case "trails-green-standard-replaced":
                case "trails-green-standard-hidden":
                case "trails-green-standard-unavailable":
                case "trails-green-standard-restart":
                case "trails-green-fire":
                case "trails-green-extreme":
                    bool extreme = lines[1] == "trails-green-extreme";
                    bool replace = lines[1] != "trails-green-standard" && !extreme;
                    bool hidden = lines[1] == "trails-green-standard-hidden";
                    bool unavailable = lines[1] == "trails-green-standard-unavailable";
                    bool fire = lines[1] == "trails-green-fire";
                    steps.Add(new AutomationStep("start-test-green", TimeSpan.FromSeconds(110), run =>
                    {
                        Configure(replace, enabled: true, hidden ? 0 : FullLength, FullMaximum);
                        return StartGreen(run, extreme);
                    }));
                    steps.Add(new AutomationStep("prepare", TimeSpan.FromSeconds(35), run => Prepare(run, extreme, unavailable), missionBound: true));
                    steps.Add(new AutomationStep("green-flight", TimeSpan.FromSeconds(55),
                        run => GreenFlight(run, ReadRunId(lines[0]), extreme, replace, hidden, fire, unavailable), missionBound: true));
                    if (lines[1] == "trails-green-standard-restart")
                    {
                        steps.Add(new AutomationStep("restart-standard-skirmish", TimeSpan.FromSeconds(80), RestartStandard));
                        steps.Add(new AutomationStep("green-flight-after-restart", TimeSpan.FromSeconds(55),
                            run => GreenFlight(run, ReadRunId(lines[0]), extreme: false, replace: true, hidden: false,
                                fire: false, unavailable: false, restarted: true), missionBound: true));
                    }
                    break;
                case "trails-mass-gpu":
                case "trails-mass-default":
                    bool full = lines[1] == "trails-mass-gpu";
                    steps.Add(new AutomationStep("start-test-green", TimeSpan.FromSeconds(110), run =>
                    {
                        Configure(replace: false, enabled: true, full ? FullLength : DefaultLength, full ? FullMaximum : DefaultMaximum);
                        return StartGreen(run, extreme: true);
                    }));
                    steps.Add(new AutomationStep("prepare", TimeSpan.FromSeconds(35), run => Prepare(run, extreme: true), missionBound: true));
                    steps.Add(new AutomationStep("mass-archers", TimeSpan.FromSeconds(110),
                        run => MeasureMass(run, ReadRunId(lines[0]), full), missionBound: true));
                    break;
                case "trails-smoke":
                    steps.Add(new AutomationStep("load-extreme", TimeSpan.FromSeconds(110), run =>
                    {
                        Configure(replace: false, enabled: true, DefaultLength, DefaultMaximum);
                        return run.LoadSave(ExtremeSave);
                    }));
                    steps.Add(new AutomationStep("prepare", TimeSpan.FromSeconds(40), run => Prepare(run, extreme: true), missionBound: true));
                    steps.Add(new AutomationStep("extreme-rotations", TimeSpan.FromSeconds(80),
                        run => CaptureViews(run, ReadRunId(lines[0]), standard: false), missionBound: true));
                    steps.Add(new AutomationStep("switch-from-extreme-save", TimeSpan.FromSeconds(130), SwitchSave));
                    steps.Add(new AutomationStep("verify-standard-after-switch", TimeSpan.FromSeconds(45),
                        run => CaptureViews(run, ReadRunId(lines[0]), standard: true), missionBound: true));
                    break;
                default:
                    return;
            }

            System.Guid runId = ReadRunId(lines[0]);
            File.Delete(request);
            try { BindMainMod(); }
            catch (Exception error)
            {
                steps.Clear();
                steps.Add(new AutomationStep("bind-bugfixes-and-qol", TimeSpan.FromSeconds(10), _ =>
                    throw new InvalidOperationException("Could not bind the production projectile trail runtime.", error)));
            }
            api.Automation.Start(runId, steps, log, completed: _ =>
            {
                if (fixtureCamera != null) fixtureCamera.AllowMove = previousCameraMovement;
            }, quitWhenDone: true);
        }

        private static System.Guid ReadRunId(string value)
        {
            if (!System.Guid.TryParseExact(value, "N", out System.Guid runId) || runId == System.Guid.Empty)
                throw new InvalidDataException("Invalid projectile-trails test run identity.");
            return runId;
        }

        private static IEnumerable<AutomationWait> Prepare(AutomationRun run, bool extreme, bool unavailable = false)
        {
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            if (!unavailable) Require(SessionExtreme == extreme, "The save's Extreme Troops state differs from the fixture.");
            Director.instance.SetEngineFrameRate(MeasuredSpeed);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 8);
        }

        private static IEnumerable<AutomationWait> CaptureViews(AutomationRun run, System.Guid runId, bool standard)
        {
            var mission = run.CurrentMission;
            int x = -1, y = -1;
            GameMap.instance.getRotationCentre(ref x, ref y);
            var savedCentre = new Vector2Int(x, y);
            var centre = run.InSimulation(mission, () => FindGate(savedCentre));
            var views = standard ? new[] { GameMap.instance.CurrentRotation() } :
                new[] { Enums.Dircs.North, Enums.Dircs.East, Enums.Dircs.South, Enums.Dircs.West };
            foreach (var view in views)
            {
                foreach (var wait in PositionCamera(run, centre, view)) yield return wait;
                var launched = run.InSimulation(mission, () => LaunchVolley(centre, 96, lowTrajectory: true));
                WriteInfo($"Launched fixture view={view} centre={centre.x},{centre.y} arrows={launched.arrows} bolts={launched.bolts}");
                Require(launched.arrows > 0 && launched.bolts > 0, "The visual test needs admitted arrows and bolts.");
                if (standard)
                {
                    yield return run.WaitForTicks(mission, 15);
                    Require(LastSegmentCount == 0, "The production renderer drew a trail in the Standard Troops fixture.");
                }
                else
                {
                    yield return AutomationWait.Until("projectile trails visible", () => LastSegmentCount > 0);
                }
                WriteInfo($"Visual fixture view={view} centre={centre.x},{centre.y} arrows={launched.arrows} bolts={launched.bolts} segments={LastSegmentCount}");
                foreach (var wait in Capture(runId, (standard ? "standard-" : "extreme-") + view)) yield return wait;
            }

            if (!standard)
            {
                run.InSimulation(mission, () => LaunchVolley(centre, 96, lowTrajectory: true));
                yield return AutomationWait.Until("active trails before native pause", () => LastSegmentCount > 0);
            }
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            int pausedSegments = LastSegmentCount;
            Require(standard ? pausedSegments == 0 : pausedSegments > 0, "The paused snapshot is missing its expected trail state.");
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            Require(LastSegmentCount == pausedSegments, "Trail topology changed during a stationary native pause.");
            foreach (var wait in Capture(runId, standard ? "standard-paused" : "extreme-paused")) yield return wait;
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 8);
        }

        private static IEnumerable<AutomationWait> SwitchSave(AutomationRun run)
        {
            var oldMission = run.CurrentMission;
            Require(oldMission != null && SessionExtreme, "The Extreme fixture is no longer current before the save switch.");
            var view = MainViewModel.Instance;
            // The CastlePlanner save-roundtrip fixture takes the same public menu path.
            // Clearing this session's restart shortcut returns to the main menu instead of its retained lobby.
            if (view.HUDIngameMenu.restartSkirmishMapInfo != null) view.HUDIngameMenu.restartSkirmishMapInfo = null;
            view.HUDmain.InGameOptions(null, null);
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonIngameMenuCommand.Execute("6");
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonConfirmationCommand.Execute("1");
            yield return AutomationWait.Until("retired Extreme mission and meshes", () =>
                FatControler.currentScene == Enums.SceneIDS.FrontEnd && run.CurrentMission == null &&
                !SessionExtreme && LastSegmentCount == 0);
            for (int attempt = 0; attempt < 10 && !ReadyMainMenu(); attempt++)
            {
                view = MainViewModel.Instance;
                if (view.Show_HUD_Confirmation || view.Show_HUD_ConfirmationMP || view.Show_HUD_ConfirmationSM ||
                    view.Show_HUD_ConfirmationSands)
                    view.ButtonConfirmationCommand.Execute("1");
                else if (view.FRONTMultiplayer != null && view.FRONTMultiplayer.panelActive && view.Show_MPGameCreation)
                    view.FRONTMultiplayer.ButtonClicked("Back");
                else
                    view.FrontEndMenu.ButtonClicked("BackMain");
                yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            }
            foreach (var wait in run.LoadSave(StandardSave)) yield return wait;
            Require(run.CurrentMission.SessionId != oldMission.SessionId, "The Standard fixture did not start a distinct mission.");
            foreach (var wait in Prepare(run, extreme: false)) yield return wait;
            Require(LastSegmentCount == 0, "The Standard fixture inherited mesh state from Extreme Troops.");
        }

        private static IEnumerable<AutomationWait> RestartStandard(AutomationRun run)
        {
            var previous = run.CurrentMission;
            var view = MainViewModel.Instance;
            Require(previous != null && view.HUDIngameMenu.restartSkirmishMapInfo != null && LastSegmentCount > 0,
                "The Standard fixture has no restartable skirmish with visible trails.");
            view.HUDmain.InGameOptions(null, null);
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonIngameMenuCommand.Execute("5");
            yield return AutomationWait.Until("restart confirmation open", () => view.Show_HUD_Confirmation);
            view.ButtonConfirmationCommand.Execute("1");
            yield return AutomationWait.Until("new skirmish mission", () =>
                run.CurrentMission != null && run.CurrentMission.SessionId != previous.SessionId);
            var current = run.CurrentMission;
            Require(current.IsRestart && current.RestartedFromSessionId == previous.SessionId,
                "The new skirmish was not linked to the previous mission's restart.");
            foreach (var wait in Prepare(run, extreme: false)) yield return wait;
            Require(ReplaceStandard && LastSegmentCount == 0, "Restart did not reset the renderer while retaining the host setting.");
            WriteInfo($"TRAIL_RESTART cleared=True replacement=True session={current.SessionId} from={previous.SessionId}");
        }

        private static bool ReadyMainMenu() => MainViewModel.viewModelLoaded &&
            FatControler.currentScene == Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu &&
            !MainViewModel.Instance.Show_IntroSequence;

        private static IEnumerable<AutomationWait> PositionCamera(AutomationRun run, Vector2Int centre, Enums.Dircs rotation)
        {
            var map = GameMap.instance;
            var controls = CameraControls2D.instance;
            if (fixtureCamera != controls)
            {
                if (fixtureCamera != null) fixtureCamera.AllowMove = previousCameraMovement;
                fixtureCamera = controls;
                previousCameraMovement = controls.AllowMove;
            }
            // The hidden window can report a pointer at the edge, causing ordinary edge scrolling.
            controls.AllowMove = false;
            map.setMapRotation(rotation, centre.x, centre.y);
            yield return AutomationWait.Until("camera rotation applied", () =>
                ReferenceEquals(map, GameMap.instance) && map.CurrentRotation() == rotation && !map.pendingRotationtriggered);
            yield return AutomationWait.Until("zoom interpolation finished", () => !PerfectPixelWithZoom.instance.midZoom);
            for (int step = 0; step < 8 && Camera.main.orthographicSize < 24f; step++)
            {
                float before = PerfectPixelWithZoom.instance.getZoom();
                foreach (var wait in run.ZoomStep(run.CurrentMission, false, _ => { })) yield return wait;
                if (Math.Abs(PerfectPixelWithZoom.instance.getZoom() - before) < 0.0001f) break;
            }
            GamePlayerManagerAPI.Instance.SetScreenCenterToTilePosition(new System.Numerics.Vector2(centre.x, centre.y));
            yield return run.WaitForTicks(run.CurrentMission, 4);
        }

        private static IEnumerable<AutomationWait> Capture(System.Guid runId, string label)
        {
            string output = Environment.GetEnvironmentVariable("SERP_TRAIL_SHOTS");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Path.GetTempPath(), "SerpProjectileTrails");
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, $"{runId:N}-{label}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return AutomationWait.Until("GPU screenshot written", () => File.Exists(path) && new FileInfo(path).Length > 0);
            WriteInfo("Screenshot " + path);
        }

        private static Vector2Int FindGate(Vector2Int savedCentre)
        {
            var tiles = GameTileManagerAPI.Instance;
            var candidate = savedCentre;
            int distance = int.MaxValue;
            foreach (ref readonly GameBuilding building in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())
            {
                if (building.r_AliveState != AliveState.IsAlive || !IsStoneGate(building.r_BuildingType)) continue;
                int x = building.r_TilePositionXBegin, y = building.r_TilePositionYBegin;
                int nextDistance = Math.Abs(x - savedCentre.x) + Math.Abs(y - savedCentre.y);
                if (nextDistance >= distance || !tiles.IsTileInsideMapBounds(x - 18, y - 18) ||
                    !tiles.IsTileInsideMapBounds(x + 18, y + 18)) continue;
                candidate = new Vector2Int(x, y);
                distance = nextDistance;
            }
            Require(tiles.IsTileInsideMapBounds(candidate.x, candidate.y), "No valid centre for the visual fixture.");
            return candidate;
        }

        private static (int arrows, int bolts) LaunchVolley(Vector2Int centre, int count, bool lowTrajectory)
        {
            var tiles = GameTileManagerAPI.Instance;
            var projectiles = GameProjectileManagerAPI.Instance;
            int sourcePlayer = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            Require(sourcePlayer >= 1 && sourcePlayer <= GamePlayerManagerAPI.MAX_PLAYERS, "Invalid local player on test fixture.");
            int arrows = 0, bolts = 0;
            for (int shot = 0; shot < count; shot++)
            {
                int lane = (shot / 4) % 17 - 8;
                int x = centre.x + ((shot & 1) == 0 ? -16 : lane);
                int y = centre.y + ((shot & 1) == 0 ? lane : -16);
                int targetX = centre.x + ((shot & 1) == 0 ? 16 : lane);
                int targetY = centre.y + ((shot & 1) == 0 ? lane : 16);
                if (!tiles.IsTileInsideMapBounds(x, y) || !tiles.IsTileInsideMapBounds(targetX, targetY)) continue;
                int z = Math.Max(tiles.GetTileHeight(tiles.GetTileId(x, y)), tiles.GetTileHeight(tiles.GetTileId(targetX, targetY))) +
                    (lowTrajectory ? 24 + shot % 7 * 12 : 96 + shot % 7 * 12);
                ProjectileType type = (shot & 2) == 0 ? ProjectileType.ArcherArrow : ProjectileType.CrossbowBolt;
                if (projectiles.CreateProjectile(0, sourcePlayer, FineTileCentre(x), FineTileCentre(y), z,
                        FineTileCentre(targetX), FineTileCentre(targetY), z, type, 0) <= 0) continue;
                if (type == ProjectileType.ArcherArrow) arrows++;
                else bolts++;
            }
            return (arrows, bolts);
        }

        private static int FineTileCentre(int tile) => tile * FineCoordinatesPerTile + FineCoordinatesPerTile / 2;

        private static bool IsStoneGate(eStructs type) => type == eStructs.STRUCT_GATE_STONE1A ||
            type == eStructs.STRUCT_GATE_STONE1B || type == eStructs.STRUCT_GATE_STONE2A ||
            type == eStructs.STRUCT_GATE_STONE2B;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
