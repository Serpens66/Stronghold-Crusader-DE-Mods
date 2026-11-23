#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using APIShared;
using APIShared.Automation;
using BepInEx;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Unity.Profiling;
using UnityEngine;

namespace ProjectileTrailsTest
{
    public sealed partial class Plugin
    {
        private const string ExtremeSave = "test_stresstest5.sav";
        private const string StandardSave = "test_gates_vanilla.sav";
        private const string ArcherSave = "test_dustynoattack2.sav";
        private const int MeasuredSpeed = 40;
        private const int VolleySize = 320;
        private const int FineCoordinatesPerTile = 8;
        private static CameraControls2D fixtureCamera;
        private static bool previousCameraMovement;

        static partial void StartAutomation(ModApiClient api)
        {
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2) return;
            string scenario = lines[1];
            bool replace = scenario == "trails-green-standard-replaced";
            bool green = scenario == "trails-green-standard" || scenario == "trails-green-extreme" || replace;
            bool gpuCapacity = scenario == "trails-gpu-capacity";
            bool mass = scenario == "trails-mass-match" || scenario == "trails-mass-gpu" || scenario == "trails-mass-gpu-off" || scenario == "trails-mass-vanilla" || scenario == "trails-mass-extreme" || scenario == "trails-mass-mod";
            bool smoke = scenario == "trails-smoke";
            bool standard = scenario == "trails-standard";
            bool archers = scenario == "trails-archers";
            bool stressOn = scenario == "trails-stress-on";
            if (!gpuCapacity && !mass && !green && !smoke && !standard && !archers && !stressOn && scenario != "trails-stress-off") return;
            if (!System.Guid.TryParseExact(lines[0], "N", out System.Guid runId) || runId == System.Guid.Empty)
                throw new InvalidDataException("Invalid projectile-trails test run identity.");

            bool enabled = TrailRenderingEnabled;
            // The runner backs up the preset store. Exercise the real setting instead of overriding admission.
            settings.ReplaceStandardAfterimages = replace;
            if (gpuCapacity)
            {
                TrailRenderingEnabled = true;
                File.Delete(request);
                api.Automation.Start(runId, new[]
                {
                    new AutomationStep("start-capacity-map", TimeSpan.FromSeconds(110), run => StartGreen(run, true)),
                    new AutomationStep("prepare", TimeSpan.FromSeconds(35), run => Prepare(run, true), missionBound: true),
                    new AutomationStep("gpu-capacity", TimeSpan.FromSeconds(45), run => CheckGpuCapacity(run, runId), missionBound: true)
                }, log, completed: _ =>
                {
                    if (fixtureCamera != null) fixtureCamera.AllowMove = previousCameraMovement;
                    TrailRenderingEnabled = enabled;
                }, quitWhenDone: true);
                return;
            }
            if (mass)
            {
                string variant = scenario.Substring("trails-mass-".Length);
                bool extreme = variant != "vanilla";
                TrailRenderingEnabled = variant == "mod" || variant == "gpu" || variant == "match";
                meshes.MatchLegacyBudget = variant == "match";
                File.Delete(request);
                api.Automation.Start(runId, new[]
                {
                    new AutomationStep("start-test-green", TimeSpan.FromSeconds(110), run => StartGreen(run, extreme)),
                    new AutomationStep("prepare", TimeSpan.FromSeconds(35), run => Prepare(run, extreme), missionBound: true),
                    new AutomationStep("mass-archers", TimeSpan.FromSeconds(95), run => MeasureMass(run, runId, variant), missionBound: true)
                }, log, completed: _ =>
                {
                    if (fixtureCamera != null) fixtureCamera.AllowMove = previousCameraMovement;
                    TrailRenderingEnabled = enabled;
                }, quitWhenDone: true);
                return;
            }
            if (green)
            {
                bool extreme = scenario == "trails-green-extreme";
                TrailRenderingEnabled = true;
                settings.AfterimageCount = TrailBuffer.Lifetime;
                settings.MaximumVisible = TrailBuffer.MaximumVisibleCopies;
                File.Delete(request);
                api.Automation.Start(runId, new[]
                {
                    new AutomationStep("start-test-green", TimeSpan.FromSeconds(110), run => StartGreen(run, extreme)),
                    new AutomationStep("prepare", TimeSpan.FromSeconds(35), run => Prepare(run, extreme), missionBound: true),
                    new AutomationStep("green-flight", TimeSpan.FromSeconds(55), run => GreenFlight(run, runId, extreme, replace), missionBound: true)
                }, log, completed: _ =>
                {
                    if (fixtureCamera != null) fixtureCamera.AllowMove = previousCameraMovement;
                    TrailRenderingEnabled = enabled;
                }, quitWhenDone: true);
                return;
            }
            traceVisibility = smoke;
            TrailRenderingEnabled = scenario != "trails-stress-off";
            File.Delete(request);
            var steps = new List<AutomationStep>
            {
                new AutomationStep("load-fixture", TimeSpan.FromSeconds(110),
                    run => run.LoadSave(standard ? StandardSave : archers ? ArcherSave : ExtremeSave)),
                new AutomationStep("prepare", TimeSpan.FromSeconds(40),
                    run => Prepare(run, !standard), missionBound: true),
                new AutomationStep(scenario, TimeSpan.FromSeconds(90),
                    run => archers ? CaptureArchers(run, runId) :
                        smoke || standard ? CaptureViews(run, runId, standard) : Measure(run, stressOn),
                    missionBound: true)
            };
            if (smoke)
            {
                steps.Add(new AutomationStep("switch-from-extreme-save", TimeSpan.FromSeconds(130), SwitchSave));
                steps.Add(new AutomationStep("verify-standard-after-switch", TimeSpan.FromSeconds(45),
                    run => CaptureViews(run, runId, standard: true), missionBound: true));
            }
            api.Automation.Start(runId, steps, log, completed: _ =>
            {
                if (fixtureCamera != null) fixtureCamera.AllowMove = previousCameraMovement;
                TrailRenderingEnabled = enabled;
            }, quitWhenDone: true);
        }

        private static IEnumerable<AutomationWait> Prepare(AutomationRun run, bool extreme)
        {
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            Require(SessionExtreme == extreme, "The save's Extreme Troops state differs from the fixture.");
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
                    Require(LastSegmentCount == 0, "The prototype rendered a trail in the Standard Troops fixture.");
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

        private static IEnumerable<AutomationWait> CaptureArchers(AutomationRun run, System.Guid runId)
        {
            var mission = run.CurrentMission;
            // Open firing position already used by the paused comparison in this save.
            var centre = new Vector2Int(424, 424);
            foreach (var wait in PositionCamera(run, centre, Enums.Dircs.North)) yield return wait;
            foreach (var wait in run.ZoomStep(mission, true, _ => { })) yield return wait;
            GamePlayerManagerAPI.Instance.SetScreenCenterToTilePosition(new System.Numerics.Vector2(centre.x, centre.y));
            yield return run.WaitForTicks(mission, 4);

            var shooters = new HashSet<int>();
            int tribe = run.InSimulation(mission, () => SpawnShooters(centre, shooters));
            yield return run.WaitForTicks(mission, 8);
            Require(run.InSimulation(mission, () => GameTribeManagerAPI.Instance.AttackTile(tribe, centre.x + 8, centre.y + 1)),
                "The ranged group declined the ordinary Attack Here command.");
            WriteInfo($"Spawned {shooters.Count} ranged troops near tile {centre.x},{centre.y} and issued Attack Here");
            int fromShooters = 0;
            for (var wait = Stopwatch.StartNew(); wait.Elapsed.TotalSeconds < 10 && fromShooters < 8;)
            {
                yield return AutomationWait.NextFrame;
                if (LastSegmentCount > 0)
                    fromShooters = run.InSimulation(mission, () => CountShooterTrails(shooters));
            }
            if (fromShooters < 8)
            {
                WriteInfo($"Spawned ranged group supplied {fromShooters} observed arrows. Using a controlled volley for the paired GPU capture.");
                TrailRenderingEnabled = false;
                TrailRenderingEnabled = true;
                var volley = run.InSimulation(mission, () => LaunchVolley(centre, 96, lowTrajectory: false));
                Require(volley.arrows > 0 && volley.bolts > 0, "The controlled GPU volley was not admitted.");
                yield return AutomationWait.Until("controlled volley rendered", () => LastSegmentCount >= 20);
                yield return run.WaitForTicks(mission, 4);
            }
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            Require(LastSegmentCount > 0, "No trail remained at the paused capture.");
            var focus = FindVisibleEchoForCapture();
            if (focus.HasValue)
            {
                for (int step = 0; step < 3 && Camera.main.orthographicSize > 9f; step++)
                    foreach (var wait in run.ZoomStep(mission, true, _ => { })) yield return wait;
                CameraControls2D.instance.setCameraPos(focus.Value.x, focus.Value.y);
                yield return AutomationWait.NextFrame;
                yield return AutomationWait.NextFrame;
            }
            WriteInfo($"Spawned-troop capture: observed={fromShooters} rendered={LastSegmentCount}");
            foreach (var wait in Capture(runId, "spawned-archers-on")) yield return wait;
            TrailRenderingEnabled = false;
            yield return AutomationWait.NextFrame;
            foreach (var wait in Capture(runId, "spawned-archers-off")) yield return wait;
        }

        private static Vector2? FindVisibleEchoForCapture()
        {
            var current = Camera.main;
            Vector2? focal = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < LastSegmentCount; i++)
            {
                var point = meshes.GetAccepted(i).Position;
                var projected = current.WorldToViewportPoint(new Vector3(point.X, point.Y, point.Z));
                if (projected.z <= 0 || projected.x < 0.2f || projected.x > 0.8f ||
                    projected.y < 0.2f || projected.y > 0.8f) continue;
                float distance = (projected.x - 0.5f) * (projected.x - 0.5f) + (projected.y - 0.5f) * (projected.y - 0.5f);
                if (distance >= best) continue;
                best = distance;
                focal = new Vector2(point.X, point.Y);
            }
            return focal;
        }

        private static int SpawnShooters(Vector2Int centre, HashSet<int> shooters)
        {
            var tiles = GameTileManagerAPI.Instance;
            var tribes = GameTribeManagerAPI.Instance;
            int owner = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            Require(owner >= 1 && owner <= GamePlayerManagerAPI.MAX_PLAYERS, "No local owner for the ranged group.");
            int tribe = checked((int)tribes.Create(owner, false));
            Require(tribe > 0 && tiles.IsTileInsideMapBounds(centre.x + 8, centre.y + 1), "Invalid ranged group or target.");
            for (int dx = -5; dx >= -12 && shooters.Count < 32; dx--)
            {
                for (int dy = -4; dy <= 4 && shooters.Count < 32; dy++)
                {
                    int x = centre.x + dx;
                    int y = centre.y + dy;
                    if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                    int tile = tiles.GetTileId(x, y);
                    if (!tiles.IsTileWalkableAndUnoccupied(tile)) continue;
                    var type = shooters.Count % 4 == 3 ? eChimps.CHIMP_TYPE_XBOWMAN : eChimps.CHIMP_TYPE_ARCHER;
                    int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, tiles.GetTileHeight(tile), type));
                    if (id > 0 && tribes.AssignUnit(tribe, id)) shooters.Add(id);
                }
            }
            Require(shooters.Count >= 16, "Too few ranged troops fit into the fixture.");
            return tribe;
        }

        private static int CountShooterTrails(HashSet<int> shooters)
        {
            int count = 0;
            var projectiles = GameProjectileManagerAPI.Instance;
            for (int i = 0; i < LastSegmentCount; i++)
                if (shooters.Contains(projectiles.GetSourceUnit(meshes.GetAccepted(i).SourceSlot))) count++;
            return count;
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

        private static IEnumerable<AutomationWait> Measure(AutomationRun run, bool enabled)
        {
            var mission = run.CurrentMission;
            // The audited stress fixture is an 800-tile map. The paired runs share a fixed camera and scripted shot pattern.
            var centre = new Vector2Int(400, 400);
            foreach (var wait in PositionCamera(run, centre, Enums.Dircs.North)) yield return wait;
            float zoom = PerfectPixelWithZoom.instance.getZoom();
            var frameMs = new double[8192];
            int sampleCount = 0, activeFrames = 0, peakSegments = 0, arrows = 0, bolts = 0, drawSamples = 0, gcSamples = 0;
            long draws = 0, bytes = 0;
            var camera = CameraControls2D.instance;
            float cameraX = camera.getCameraXPos(), cameraY = camera.getCameraYPos();
            float cameraDrift = 0;
            using (var drawRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
            using (var gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                foreach (var wait in run.SetPause(mission, true)) yield return wait;
                run.InSimulation(mission, () => LaunchVolley(centre, VolleySize, lowTrajectory: false));
                foreach (var wait in run.SetPause(mission, false)) yield return wait;
                yield return AutomationWait.Delay(TimeSpan.FromSeconds(2));
                for (int volley = 0; volley < 5; volley++)
                {
                    foreach (var wait in run.SetPause(mission, true)) yield return wait;
                    var launched = run.InSimulation(mission, () => LaunchVolley(centre, VolleySize, lowTrajectory: false));
                    foreach (var wait in run.SetPause(mission, false)) yield return wait;
                    arrows += launched.arrows;
                    bolts += launched.bolts;
                    // The injection frame includes fixture allocations and is excluded from both variants.
                    yield return AutomationWait.NextFrame;
                    yield return AutomationWait.NextFrame;
                    long previous = Stopwatch.GetTimestamp();
                    long end = previous + Stopwatch.Frequency;
                    while (previous < end && sampleCount < frameMs.Length)
                    {
                        yield return AutomationWait.NextFrame;
                        long now = Stopwatch.GetTimestamp();
                        frameMs[sampleCount++] = (now - previous) * 1000d / Stopwatch.Frequency;
                        previous = now;
                        int segments = LastSegmentCount;
                        if (segments > 0) activeFrames++;
                        peakSegments = Math.Max(peakSegments, segments);
                        cameraDrift = Math.Max(cameraDrift, Math.Max(Math.Abs(camera.getCameraXPos() - cameraX), Math.Abs(camera.getCameraYPos() - cameraY)));
                        if (drawRecorder.Valid && drawRecorder.Count > 0) { draws += drawRecorder.LastValue; drawSamples++; }
                        if (gcRecorder.Valid && gcRecorder.Count > 0) { bytes += gcRecorder.LastValue; gcSamples++; }
                    }
                }
            }
            Require(arrows > 0 && bolts > 0 && sampleCount >= 30, "Too few launched shots or rendered frames for comparison.");
            Require(enabled ? activeFrames > 0 : peakSegments == 0, "The measured run did not honor the rendering toggle.");
            Require(Math.Abs(PerfectPixelWithZoom.instance.getZoom() - zoom) < 0.001f, "Zoom changed during measurement.");
            Array.Sort(frameMs, 0, sampleCount);
            WriteInfo(FormattableString.Invariant($"TRAIL_PERF enabled={enabled} arrows={arrows} bolts={bolts} frames={sampleCount} activeFrames={activeFrames} peakSegments={peakSegments} speed={(1 / Director.instance.EngineFrameTime):0.##} zoom={zoom:0.###} camera={cameraX:0.###},{cameraY:0.###} cameraDrift={cameraDrift:0.###} resolution={Screen.width}x{Screen.height} p50ms={Percentile(frameMs, sampleCount, 0.5):0.###} p95ms={Percentile(frameMs, sampleCount, 0.95):0.###} p99ms={Percentile(frameMs, sampleCount, 0.99):0.###} drawCallsMean={Mean(draws, drawSamples)} drawSamples={drawSamples} gcBytesPerFrame={Mean(bytes, gcSamples)} gcSamples={gcSamples}"));
        }

        private static double Percentile(double[] ordered, int count, double fraction) =>
            ordered[(int)Math.Ceiling(count * fraction) - 1];

        private static string Mean(long sum, int count) => count == 0 ? "unavailable" :
            ((double)sum / count).ToString("0.###", CultureInfo.InvariantCulture);

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
#endif
