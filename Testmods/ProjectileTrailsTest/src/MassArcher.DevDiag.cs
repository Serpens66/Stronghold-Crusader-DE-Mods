#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Unity.Profiling;
using UnityEngine;

namespace ProjectileTrailsTest
{
    public sealed partial class Plugin
    {
        private const int MassUnitLimit = 2999;
        // Vanilla reserves low projectile slots and shares the remaining pool with effects.
        private const int MassArrowsPerVolley = 2900;

        private sealed class MassFixture
        {
            internal readonly List<MassShot> Archers = new List<MassShot>(MassUnitLimit);
            internal int Owner, InitialUnits;
        }

        private struct MassShot
        {
            internal int Unit, X, Y, TargetX, Height;
        }

        private sealed class MassObservation
        {
            private readonly uint[] serials = new uint[WideProjectileLimit];
            private readonly bool[] fixtureArchers;
            private readonly bool[] hasFired;
            internal int Shooters, Launches, PeakArrows, PeakVanilla, ActiveArrows, ActiveVanilla;

            internal MassObservation(List<MassShot> units)
            {
                int length = 1;
                foreach (var shot in units) length = Math.Max(length, shot.Unit + 1);
                fixtureArchers = new bool[length];
                hasFired = new bool[length];
                foreach (var shot in units) fixtureArchers[shot.Unit] = true;
            }

            internal void OnTick(int _)
            {
                int arrows = 0, echoes = 0;
                var slots = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
                for (int i = 1; i < slots.Length; i++)
                {
                    ref readonly var shot = ref slots[i];
                    if (shot.r_AliveState == AliveState.None) continue;
                    if (shot.r_ProjectileType == ProjectileType.AfterImage) echoes++;
                    if (shot.r_ProjectileType != ProjectileType.ArcherArrow) continue;
                    int unit = shot.r_SourceUnitId;
                    if (unit >= fixtureArchers.Length || !fixtureArchers[unit]) continue;
                    arrows++;
                    if (serials[i] == shot.r_GlobalId) continue;
                    serials[i] = shot.r_GlobalId;
                    Launches++;
                    if (hasFired[unit]) continue;
                    hasFired[unit] = true;
                    Shooters++;
                }
                ActiveArrows = arrows;
                ActiveVanilla = echoes;
                PeakArrows = Math.Max(PeakArrows, arrows);
                PeakVanilla = Math.Max(PeakVanilla, echoes);
            }

            internal int RetireRemainingFixtureArrows()
            {
                int marked = 0;
                var projectiles = GameProjectileManagerAPI.Instance;
                var slots = projectiles.GetProjectilesAsSpan();
                for (int i = 1; i < slots.Length; i++)
                {
                    ref readonly var shot = ref slots[i];
                    int unit = shot.r_SourceUnitId;
                    if (shot.r_AliveState == AliveState.None || shot.r_ProjectileType != ProjectileType.ArcherArrow ||
                        unit >= fixtureArchers.Length || !fixtureArchers[unit] || serials[i] != shot.r_GlobalId) continue;
                    if (projectiles.DeleteProjectileSafe(i)) marked++;
                }
                return marked;
            }
        }

        private static int OccupiedUnitSlots()
        {
            int count = 0;
            foreach (ref readonly var unit in GameUnitManagerAPI.Instance.GetUnitsAsSpan())
                if (unit.r_AliveState != AliveState.None) count++;
            return count;
        }

        private static MassFixture PrepareMassBattle(Vector2Int center, int archerCount)
        {
            var players = GamePlayerManagerAPI.Instance;
            var units = GameUnitManagerAPI.Instance;
            var tiles = GameTileManagerAPI.Instance;
            var result = new MassFixture
            {
                Owner = players.GetLocalPlayerId(),
                InitialUnits = OccupiedUnitSlots()
            };
            int enemy = result.Owner == 1 ? 2 : 1; // The fixture starts exactly one opponent.
            Require(players.GetPlayerKeepId(result.Owner) > 0 && players.GetPlayerKeepId(enemy) > 0,
                "The paired Skirmish keeps are missing.");
            players.SetPlayerPopularity(result.Owner, 0);
            players.SetPlayerPopularity(enemy, 0);

            int quota = archerCount > 0 ? archerCount : MassUnitLimit - result.InitialUnits;
            // Fill square rings around the camera centre to keep the formation in view.
            for (int ring = 1; ring <= 50 && result.Archers.Count < quota; ring++)
            {
                for (int y = -ring; y <= ring && result.Archers.Count < quota; y++)
                {
                    for (int x = -ring; x <= ring && result.Archers.Count < quota; x++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring) continue;
                        int px = center.x + x, py = center.y + y;
                        int targetX = px + (x >= 0 ? -22 : 22);
                        if (!tiles.IsTileInsideMapBounds(px, py) ||
                            !tiles.IsTileInsideMapBounds(targetX, py)) continue;
                        int tile = tiles.GetTileId(px, py);
                        if (!tiles.IsTileWalkableAndUnoccupied(tile)) continue;
                        int ground = tiles.GetTileHeight(tile);
                        int unit = checked((int)units.CreateUnitLocal(result.Owner, result.Owner, px, py, ground, eChimps.CHIMP_TYPE_ARCHER));
                        Require(unit > 0, "The native unit allocator refused an archer.");
                        result.Archers.Add(new MassShot
                        {
                            Unit = unit, X = FineTileCentre(px), Y = FineTileCentre(py),
                            TargetX = FineTileCentre(targetX),
                            Height = Math.Max(ground, tiles.GetTileHeight(tiles.GetTileId(targetX, py))) + 64
                        });
                    }
                }
            }
            Require(result.Archers.Count == quota && OccupiedUnitSlots() == result.InitialUnits + quota,
                "The field must match the requested formation.");
            return result;
        }

        private static int LaunchMass(MassFixture fixture, bool finalGroup, int arrowsPerVolley)
        {
            int admitted = 0;
            var projectiles = GameProjectileManagerAPI.Instance;
            int first = finalGroup ? fixture.Archers.Count - arrowsPerVolley : 0;
            for (int i = first; i < first + arrowsPerVolley; i++)
            {
                var shot = fixture.Archers[i];
                if (projectiles.CreateProjectile(shot.Unit, fixture.Owner, shot.X, shot.Y, shot.Height,
                        shot.TargetX, shot.Y, shot.Height, ProjectileType.ArcherArrow, 0) > 0) admitted++;
            }
            return admitted;
        }

        private static string MassMean(double sum, int count) => count == 0 ? "unavailable" :
            (sum / count).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static IEnumerable<AutomationWait> MeasureMass(AutomationRun run, System.Guid runId, string variant)
        {
            var mission = run.CurrentMission;
            var center = new Vector2Int(400, 396);
            foreach (var wait in PositionCamera(run, center, Enums.Dircs.North)) yield return wait;
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            bool large = variant == "gpu" || variant == "gpu-off";
            bool enabled = variant == "mod" || variant == "gpu" || variant == "match";
            const int largeVolley = 5000;
            int arrowsPerVolley = large ? largeVolley : MassArrowsPerVolley;
            var fixture = run.InSimulation(mission, () => PrepareMassBattle(center, large ? largeVolley : 0));
            int initialUnits = fixture.InitialUnits + fixture.Archers.Count;
            var observation = new MassObservation(fixture.Archers);
            var clock = GameTimeManagerAPI.Instance;
            Require(run.InSimulation(mission, () => native.Tick == unchecked((uint)clock.GetElapsedMapTicks())),
                "The observer and Extender disagree on the native map clock.");
            WriteInfo($"MASS_SETUP variant={variant} units={initialUnits} archers={fixture.Archers.Count} initial={fixture.InitialUnits} camera={center}");
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 12);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            var samples = new double[4096];
            int frames = 0, drawSamples = 0, maximumMod = 0, admitted = 0, mainSamples = 0, gpuSamples = 0;
            long mainNanoseconds = 0;
            double gpuMilliseconds = 0;
            var frameTimings = new FrameTiming[1];
            ulong lastGpuFrame = 0;
            long draws = 0;
            var camera = CameraControls2D.instance;
            float cameraX = camera.getCameraXPos(), cameraY = camera.getCameraYPos();
            float zoom = PerfectPixelWithZoom.instance.getZoom();
            float drift = 0;
            clock.OnTick += observation.OnTick;
            try
            {
                using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
                using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread"))
                {
                    for (int volley = 0; volley < 2; volley++)
                    {
                        int accepted = run.InSimulation(mission, () => LaunchMass(fixture, (volley & 1) != 0, arrowsPerVolley));
                        Require(accepted == arrowsPerVolley, $"Only {accepted}/{arrowsPerVolley} projectiles fit in volley {volley + 1}.");
                        admitted += accepted;
                        foreach (var wait in run.SetPause(mission, false)) yield return wait;
                        if (volley == 0)
                        {
                            yield return run.WaitForTicks(mission, 12);
                            foreach (var wait in run.SetPause(mission, true)) yield return wait;
                            foreach (var wait in Capture(runId, "test-green-mass-" + variant)) yield return wait;
                            foreach (var wait in run.SetPause(mission, false)) yield return wait;
                        }
                        uint start = native.Tick;
                        long lastFrame = Stopwatch.GetTimestamp();
                        while (unchecked(native.Tick - start) < 45 && frames < samples.Length)
                        {
                            yield return AutomationWait.NextFrame;
                            long now = Stopwatch.GetTimestamp();
                            samples[frames++] = (now - lastFrame) * 1000d / Stopwatch.Frequency;
                            lastFrame = now;
                            maximumMod = Math.Max(maximumMod, LastSegmentCount);
                            drift = Math.Max(drift, Math.Max(Math.Abs(camera.getCameraXPos() - cameraX), Math.Abs(camera.getCameraYPos() - cameraY)));
                            if (recorder.Valid && recorder.Count > 0) { draws += recorder.LastValue; drawSamples++; }
                            if (main.Valid && main.Count > 0) { mainNanoseconds += main.LastValue; mainSamples++; }
                            FrameTimingManager.CaptureFrameTimings();
                            if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0 &&
                                frameTimings[0].cpuTimePresentCalled != lastGpuFrame && frameTimings[0].gpuFrameTime > 0)
                            {
                                lastGpuFrame = frameTimings[0].cpuTimePresentCalled;
                                gpuMilliseconds += frameTimings[0].gpuFrameTime;
                                gpuSamples++;
                            }
                        }
                        if (volley == 1)
                        {
                            foreach (var wait in run.SetPause(mission, true)) yield return wait;
                            break;
                        }
                        yield return run.WaitForTicks(mission, 45);
                        foreach (var wait in run.SetPause(mission, true)) yield return wait;
                        int retired = run.InSimulation(mission, observation.RetireRemainingFixtureArrows);
                        WriteInfo($"MASS_WAVE variant={variant} wave={volley + 1} tick={native.Tick} observed={observation.Shooters} liveArrows={observation.ActiveArrows} liveVanilla={observation.ActiveVanilla} retired={retired}");
                        foreach (var wait in run.SetPause(mission, false)) yield return wait;
                        yield return run.WaitForTicks(mission, 30);
                        foreach (var wait in run.SetPause(mission, true)) yield return wait;
                    }
                }
                int finalUnits = run.InSimulation(mission, OccupiedUnitSlots);
                Array.Sort(samples, 0, frames);
                Require(frames >= 30 && observation.Shooters == fixture.Archers.Count,
                    $"Only {observation.Shooters}/{fixture.Archers.Count} distinct sources were observed.");
                WriteInfo(FormattableString.Invariant(
                    $"MASS_RESULT variant={variant} unitsStart={initialUnits} unitsFinal={finalUnits} archers={fixture.Archers.Count} shooters={observation.Shooters} admitted={admitted} launches={observation.Launches} peakArrows={observation.PeakArrows} peakVanilla={observation.PeakVanilla} peakMod={maximumMod} frames={frames} p50={Percentile(samples, frames, 0.50):0.###} p95={Percentile(samples, frames, 0.95):0.###} p99={Percentile(samples, frames, 0.99):0.###} draws={Mean(draws, drawSamples)} mainMs={MassMean(mainNanoseconds / 1000000d, mainSamples)} gpuMs={MassMean(gpuMilliseconds, gpuSamples)} zoom={zoom:0.###} cam={cameraX:0.###},{cameraY:0.###} drift={drift:0.###} resolution={Screen.width}x{Screen.height}"));
                Require(enabled == (maximumMod > 0) &&
                    (variant == "vanilla") == (observation.PeakVanilla > 0),
                    "The requested projectile drawing mode was not observed.");
            }
            finally
            {
                clock.OnTick -= observation.OnTick;
                meshes.MatchLegacyBudget = false;
            }
        }
    }
}
#endif
