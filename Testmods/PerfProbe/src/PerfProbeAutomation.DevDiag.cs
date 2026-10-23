using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using UnityEngine;

namespace PerfProbe
{
    // Reproducible A/B measurement run (see AutomatisierungTest.md), moved from the retired
    // PerfFixTest test mod (last state: commit a7978899f):
    // main menu -> load save -> warm-up ticks -> zoom out -> settle -> native pause (hold)
    // -> unpause (run) -> PASS/FAIL -> optional quit. Every phase boundary is logged with an
    // "AUTOMATION" marker so PerfProbe windows can be cut by phase.
    //
    // Deliberate exception (user decision 2026-10-20): no APIShared mission lifecycle, so a
    // baseline installation stays Extender + PerfProbe (AutomationOnly) only. Readiness is derived
    // from Vanilla state plus observed simulation ticks; this detector exists only in this test mod.
    //
    // Driven by a static Application.onBeforeRender callback (runs while paused; survives the
    // SHCDE startup cleanup), limited to once per frame. Simulation ticks arrive on the sim
    // thread through GameTimeManagerAPI.OnTick (rooted by the extender) and only increment counters.
    internal static class PerfProbeAutomation
    {
        private enum Phase { Disabled, WaitMenu, WaitMission, ZoomOut, Settle, PauseHold, RunHold, Done, Failed }

        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static ManualLogSource log;
        private static Phase phase = Phase.Disabled;
        private static string savePath, runLabel, screenshotDir;
        private static int warmupTicks, settleSeconds, pauseSeconds, runSeconds, timeoutSeconds, maxZoomSteps, gameSpeed;
        private static bool quitWhenDone, extenderReady, tickSubscribed, missionReadyLogged, speedApplied;
        private static int lastFrame = -1, zoomSteps, unchangedSteps, gcAtRunStart;
        private static long ticks, ticksAtPhase, startStamp, phaseStamp, loadStamp, heapAtRunStart, lastFrameStamp;
        private static float zoomBeforeStep = -1f, targetZoom;
        private static bool stepPending, resumeConfirmed, screenshotTaken;
        private static readonly FrameStats frames = new FrameStats();

        // Baseline runs: skip every other PerfProbe hook and sampler, so the installation measures
        // only the automation (equivalent to the former PerfFixTest with all fixes off).
        internal static bool AutomationOnly { get; private set; }

        internal static void Bind(ConfigFile config, ManualLogSource logger)
        {
            log = logger;
            bool enabled = config.Bind("Automation", "Enabled", false,
                "Run the automated A/B measurement once per game start (load save, zoom out, pause, run, quit).").Value;
            AutomationOnly = config.Bind("Automation", "AutomationOnly", false,
                "Install no PerfProbe timing patches, samplers or extras (baseline runs: only the automation is active). Restart required.").Value;
            savePath = config.Bind("Automation", "SavePath", "test_dustyFPS.sav",
                "Single-player save: absolute path or relative to the game's Saves directory.").Value;
            runLabel = config.Bind("Automation", "RunLabel", "",
                "Free label written to every AUTOMATION log line (e.g. A or B).").Value;
            warmupTicks = config.Bind("Automation", "WarmupTicks", 80, "Simulation ticks to observe after loading before zooming.").Value;
            settleSeconds = config.Bind("Automation", "SettleSeconds", 3, "Seconds to wait after zooming out before pausing.").Value;
            pauseSeconds = config.Bind("Automation", "PauseSeconds", 25, "Seconds to hold the native pause (identical scene).").Value;
            runSeconds = config.Bind("Automation", "RunSeconds", 25, "Seconds to run unpaused at the measured zoom.").Value;
            timeoutSeconds = config.Bind("Automation", "TimeoutSeconds", 300, "Overall timeout from game start; FAIL on expiry.").Value;
            maxZoomSteps = config.Bind("Automation", "MaxZoomSteps", 20, "Upper bound of zoom-out steps.").Value;
            gameSpeed = config.Bind("Automation", "GameSpeed", 0, "Game speed set after loading (0 = keep the save's speed).").Value;
            targetZoom = config.Bind("Automation", "TargetZoom", 0f, "Stop zooming out at this zoom (0 = until the zoom-out limit).").Value;
            quitWhenDone = config.Bind("Automation", "QuitWhenDone", true, "Quit the game after PASS or FAIL.").Value;
            screenshotDir = config.Bind("Automation", "ScreenshotDir", "BepInEx/PerfProbe_shots",
                "Folder (relative to the game root or absolute) for the paused-scene screenshot used for pixel comparison; empty disables it.").Value;
            if (!enabled) return;
            phase = Phase.WaitMenu;
            startStamp = phaseStamp = Stopwatch.GetTimestamp();
            Application.onBeforeRender += OnBeforeRender;
            Log("armed save=" + savePath + " warmupTicks=" + warmupTicks + " settle=" + settleSeconds + "s pause=" + pauseSeconds +
                "s run=" + runSeconds + "s timeout=" + timeoutSeconds + "s quit=" + quitWhenDone + " automationOnly=" + AutomationOnly);
        }

        // Extender APIs (GameTimeManagerAPI, GamePlayerManagerAPI) only after the native library is ready.
        internal static void OnExtenderReady() => extenderReady = true;

        // Sim-thread tick gaps while running unpaused (> 100 ms = simulation stall, even if frames stay smooth).
        private static volatile bool recordTickGaps;
        private static long lastTickStamp, tickGapMaxTicks, tickGapsOver100, tickGapsOver50;

        private static void OnTick(int tick)
        {
            Interlocked.Increment(ref ticks);
            long now = Stopwatch.GetTimestamp();
            long previous = Interlocked.Exchange(ref lastTickStamp, now);
            if (!recordTickGaps || previous == 0) return;
            long gap = now - previous;
            if (gap > Interlocked.Read(ref tickGapMaxTicks)) Interlocked.Exchange(ref tickGapMaxTicks, gap);
            if (gap > Stopwatch.Frequency / 10) Interlocked.Increment(ref tickGapsOver100);
            if (gap > Stopwatch.Frequency / 20) Interlocked.Increment(ref tickGapsOver50);
        }

        private static void OnBeforeRender()
        {
            if (phase == Phase.Disabled) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (phase == Phase.Done || phase == Phase.Failed) { StepQuit(); return; }
            long frameNow = Stopwatch.GetTimestamp();
            // Frame interval of the frame that just ended; the first 2 s of a hold phase are skipped (transition).
            if (lastFrameStamp != 0 && (phase == Phase.PauseHold || phase == Phase.RunHold) && Seconds(phaseStamp) >= 2)
                frames.Add((frameNow - lastFrameStamp) * 1000.0 / Stopwatch.Frequency);
            lastFrameStamp = frameNow;
            try
            {
                if (Seconds(startStamp) > timeoutSeconds) { Fail("timeout in phase " + phase); return; }
                switch (phase)
                {
                    case Phase.WaitMenu: StepWaitMenu(); break;
                    case Phase.WaitMission: StepWaitMission(); break;
                    case Phase.ZoomOut: StepZoomOut(); break;
                    case Phase.Settle: if (Seconds(phaseStamp) >= settleSeconds) BeginPause(); break;
                    case Phase.PauseHold: StepPauseHold(); break;
                    case Phase.RunHold: StepRunHold(); break;
                }
            }
            catch (Exception exception) { Fail("exception in phase " + phase + ": " + exception); }
        }

        private static void StepWaitMenu()
        {
            if (!extenderReady || !MainViewModel.viewModelLoaded || FatControler.currentScene != Enums.SceneIDS.FrontEnd) return;
            MainViewModel view = MainViewModel.Instance;
            if (!view.Show_Frontend_MainMenu || view.Show_IntroSequence || view.HUDmain == null || view.HUDIngameMenu == null ||
                Director.instance == null || EditorDirector.instance == null) return;
            string resolved = Path.IsPathRooted(savePath) ? savePath : Path.Combine(ConfigSettings.GetSavesPath(), savePath);
            if (!File.Exists(resolved)) { Fail("save not found: " + resolved); return; }
            // Folder type 0 is Vanilla's user-file catalog selector, not a game/player ID.
            var header = MapFileManager.Instance.GetFileInfoFromFileName(resolved, resolved, 0, true);
            if (header == null) { Fail("Vanilla rejected the savegame header: " + resolved); return; }
            if (!tickSubscribed) { GameTimeManagerAPI.Instance.OnTick += OnTick; tickSubscribed = true; }
            Log("startup menuReadySinceProcessStartS=" + ProcessUptime() + " sincePluginBindS=" + Seconds(startStamp).ToString("0.00", C));
            loadStamp = Stopwatch.GetTimestamp();
            Enter(Phase.WaitMission, "loading " + resolved);
            // Same preparation as FrontendMenus' single-player load confirmation (as in AutoLoadSave).
            // Loading without the dialog keeps the current mod settings (APIShared "use current").
            Platform_Multiplayer.Instance.gameMembers = null;
            view.InitNewScene(Enums.SceneIDS.MainGame);
            Director.instance.SetPausedState(false);
            EditorDirector.instance.stopGameSim();
            EditorDirector.instance.loadSaveGame(resolved, header.standAlone_filename, header);
            view.InitObjectiveGoodsPanelDelayed();
        }

        private static void StepWaitMission()
        {
            Director director = Director.instance;
            if (director == null || !director.SimRunning || GameData.Instance.lastGameState == null || !MainViewModel.viewModelLoaded) return;
            MainViewModel view = MainViewModel.Instance;
            if (view.HUDmain == null || view.Show_HUD_Briefing || PerfectPixelWithZoom.instance == null || CameraControls2D.instance == null) return;
            if (director.MultiplayerGame) { Fail("multiplayer session; automation is single-player only"); return; }
            if (!missionReadyLogged)
            {
                missionReadyLogged = true;
                Log("startup missionReadyAfterLoadS=" + Seconds(loadStamp).ToString("0.00", C) + " sinceProcessStartS=" + ProcessUptime());
            }
            if (gameSpeed > 0 && !speedApplied)
            {
                // Same call as Vanilla's speed slider path; more simulated game time per measured second.
                speedApplied = true;
                director.SetEngineFrameRate(gameSpeed);
                Log("game speed requested=" + gameSpeed + " engineFrameTime=" + director.EngineFrameTime.ToString("0.#####", C) +
                    " observed=" + (director.EngineFrameTime > 0 ? (1.0 / director.EngineFrameTime).ToString("0", C) : "n/a"));
            }
            if (Interlocked.Read(ref ticks) - ticksAtPhase < warmupTicks) return;
            Enter(Phase.ZoomOut, "mission ready; starting zoom-out from zoom=" + Zoom());
        }

        private static void StepZoomOut()
        {
            PerfectPixelWithZoom zoom = PerfectPixelWithZoom.instance;
            if (zoom == null) { Fail("camera zoom component vanished"); return; }
            if (zoom.midZoom) return;
            if (stepPending)
            {
                stepPending = false;
                float now = zoom.getZoom();
                unchangedSteps = Math.Abs(now - zoomBeforeStep) < 0.0001f ? unchangedSteps + 1 : 0;
                // Two completed steps without change: the zoom-out limit is reached.
                if (unchangedSteps >= 2) { Enter(Phase.Settle, "zoom-out limit reached zoom=" + now.ToString("0.###", C) + " steps=" + zoomSteps); return; }
            }
            // Fixed comparison zoom: mods with an extended zoom range must not change the measured scene.
            if (targetZoom > 0 && zoom.getZoom() <= targetZoom + 0.0001f) { Enter(Phase.Settle, "target zoom reached zoom=" + Zoom() + " steps=" + zoomSteps); return; }
            if (zoomSteps >= maxZoomSteps) { Enter(Phase.Settle, "zoom step bound reached zoom=" + Zoom() + " steps=" + zoomSteps); return; }
            zoomBeforeStep = zoom.getZoom();
            float step = ConfigSettings.Settings_ExtraZoom ? 0.5f : 1f;
            zoom.adjustZoom(-step, loop: false);
            zoomSteps++;
            stepPending = true;
        }

        private static void BeginPause()
        {
            if (!TrySetPause(true)) { Fail("native pause could not be set"); return; }
            Enter(Phase.PauseHold, "paused zoom=" + Zoom());
        }

        private static void StepPauseHold()
        {
            if (!GamePlayerManagerAPI.Instance.IsLocalPaused()) { Fail("pause was lifted externally"); return; }
            if (!screenshotTaken && Seconds(phaseStamp) >= pauseSeconds / 2.0) TakeScreenshot();
            if (Seconds(phaseStamp) < pauseSeconds) return;
            Log("framestats phase=pause " + frames.Summary());
            frames.Clear();
            Log("phase=pause-end held=" + Seconds(phaseStamp).ToString("0.0", C) + "s zoom=" + Zoom());
            if (!TrySetPause(false)) { Fail("native pause could not be lifted"); return; }
            resumeConfirmed = false;
            Interlocked.Exchange(ref lastTickStamp, 0);
            Interlocked.Exchange(ref tickGapMaxTicks, 0);
            Interlocked.Exchange(ref tickGapsOver100, 0);
            Interlocked.Exchange(ref tickGapsOver50, 0);
            recordTickGaps = true;
            gcAtRunStart = GC.CollectionCount(0);
            heapAtRunStart = GC.GetTotalMemory(false);
            Log("nativecounters phase=run-start " + NativePathCounters());
            Enter(Phase.RunHold, "unpaused zoom=" + Zoom());
        }

        private static void StepRunHold()
        {
            if (!resumeConfirmed && Interlocked.Read(ref ticks) - ticksAtPhase >= 5)
            {
                resumeConfirmed = true;
                Log("phase=run-resumed ticks=" + (Interlocked.Read(ref ticks) - ticksAtPhase) + " after " + Seconds(phaseStamp).ToString("0.0", C) + "s");
            }
            if (!resumeConfirmed && Seconds(phaseStamp) > 5) { Fail("no simulation ticks after unpause"); return; }
            if (Seconds(phaseStamp) < runSeconds) return;
            recordTickGaps = false;
            double ran = Seconds(phaseStamp);
            long ranTicks = Interlocked.Read(ref ticks) - ticksAtPhase;
            Log("framestats phase=run " + frames.Summary());
            Log(string.Format(C, "tickstats phase=run ticks={0} ticksPerS={1:0.0} maxGapMs={2:0.0} gapsOver50={3} gapsOver100={4}",
                ranTicks, ranTicks / ran, Interlocked.Read(ref tickGapMaxTicks) * 1000.0 / Stopwatch.Frequency,
                Interlocked.Read(ref tickGapsOver50), Interlocked.Read(ref tickGapsOver100)));
            Log("nativecounters phase=run-end " + NativePathCounters());
            // Mono's Boehm collector stops every thread; collections during the run also pause the simulation.
            Log(string.Format(C, "gcstats phase=run collections={0} perS={1:0.00} heapStartMB={2:0.0} heapEndMB={3:0.0}",
                GC.CollectionCount(0) - gcAtRunStart, (GC.CollectionCount(0) - gcAtRunStart) / ran,
                heapAtRunStart / 1048576.0, GC.GetTotalMemory(false) / 1048576.0));
            frames.Clear();
            Log("phase=run-end ran=" + ran.ToString("0.0", C) + "s ticks=" + ranTicks + " zoom=" + Zoom());
            Finish(Phase.Done, "PASS total=" + Seconds(startStamp).ToString("0.0", C) + "s");
        }

        // Native path manager (FBCB9319 base+0x60AD660): +0x78 counts failed route reconstructions
        // (0xE1640, each followed by a full-map rebuild 0xDAA50), +0x74 executed PCL rebuilds (0xE49D0).
        // Read-only, so the same counters can be compared between baseline and mod runs.
        private static string NativePathCounters()
        {
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            if (module == IntPtr.Zero) return "unavailable";
            // Fixed layout of the audited DLL: refuse a smaller image instead of reading outside it.
            int peOffset = System.Runtime.InteropServices.Marshal.ReadInt32(module, 0x3C);
            uint imageSize = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(module, peOffset + 0x18 + 0x38);
            if (imageSize < 0x60AD660 + 0x80) return "unavailable(image)";
            IntPtr manager = new IntPtr(module.ToInt64() + 0x60AD660);
            return "reconstructFailures=" + System.Runtime.InteropServices.Marshal.ReadInt32(manager, 0x78).ToString(C) +
                " pclRebuilds=" + System.Runtime.InteropServices.Marshal.ReadInt32(manager, 0x74).ToString(C);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        // Paused, identical scene: the PNG of run A and B must match pixel for pixel unless a fix changes the image.
        private static void TakeScreenshot()
        {
            screenshotTaken = true;
            if (string.IsNullOrEmpty(screenshotDir)) return;
            try
            {
                string dir = Path.IsPathRooted(screenshotDir) ? screenshotDir : Path.Combine(BepInEx.Paths.GameRootPath, screenshotDir);
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, (runLabel.Length == 0 ? "run" : runLabel) + "_pause.png");
                ScreenCapture.CaptureScreenshot(file);
                Log("screenshot requested " + file + " textureMemoryMB=" + (Texture.currentTextureMemory / 1048576.0).ToString("0", C));
            }
            catch (Exception exception) { Log("screenshot failed: " + exception.Message); }
        }

        // Single-player native pause via the public wrapper; the result is verified, not assumed
        // (GameAction returns 0 for this command even on success; other mods may veto).
        private static bool TrySetPause(bool paused)
        {
            Director director = Director.instance;
            if (director == null || director.MultiplayerGame || director.Paused) return false;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            if (players.IsLocalPaused() != paused)
            {
                int state = paused ? 1 : 0;
                EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, state, state);
            }
            return players.IsLocalPaused() == paused;
        }

        private static void Enter(Phase next, string detail)
        {
            phase = next;
            phaseStamp = Stopwatch.GetTimestamp();
            ticksAtPhase = Interlocked.Read(ref ticks);
            Log("phase=" + next + " " + detail);
        }

        private static void Fail(string reason) => Finish(Phase.Failed, "FAIL " + reason);

        private static void Finish(Phase final, string result)
        {
            phase = final;
            Log("result " + result);
            if (!quitWhenDone) return;
            quitStamp = Stopwatch.GetTimestamp();
            Log("quitting the game in " + QuitDelaySeconds + "s");
        }

        // BepInEx 5.4.23 has no disk-log flush setting and flushes LogOutput.log on a timer; a direct
        // quit lost the last seconds. Flush the disk listener explicitly, then quit after a short delay.
        private const int QuitDelaySeconds = 2;
        private static long quitStamp;

        private static void StepQuit()
        {
            if (quitStamp == 0 || Seconds(quitStamp) < QuitDelaySeconds) return;
            quitStamp = 0;
            Log("quit now");
            try
            {
                foreach (ILogListener listener in BepInEx.Logging.Logger.Listeners)
                    if (listener is DiskLogListener disk) disk.LogWriter?.Flush();
            }
            catch (Exception exception) { log?.LogError(Stamp() + "AUTOMATION disk log flush failed: " + exception.Message); }
            Application.Quit();
        }

        private static string ProcessUptime()
        {
            try { return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds.ToString("0.00", C); }
            catch (Exception exception) { return "n/a(" + exception.GetType().Name + ")"; }
        }

        private static string Zoom() => PerfectPixelWithZoom.instance != null ? PerfectPixelWithZoom.instance.getZoom().ToString("0.###", C) : "n/a";

        private static double Seconds(long since) => (Stopwatch.GetTimestamp() - since) / (double)Stopwatch.Frequency;

        private static string Stamp() => "[" + DateTime.Now.ToString("HH:mm:ss.fff", C) + "] ";

        // Automation markers are plain messages (not throttled DevDiag output); ms timestamps.
        private static void Log(string message) =>
            log?.LogMessage(Stamp() + "AUTOMATION run=" + (runLabel.Length == 0 ? "-" : runLabel) + " " + message);

        // Frame-interval statistics of one hold phase, measured by the automation itself so a
        // comparison does not depend on the samplers (and works with AutomationOnly). Lag counters
        // use the user's freeze definition (> 100 ms) plus a 50 ms stutter bucket.
        private sealed class FrameStats
        {
            private readonly List<double> intervals = new List<double>(8192);

            internal void Add(double milliseconds) => intervals.Add(milliseconds);

            internal void Clear() => intervals.Clear();

            internal string Summary()
            {
                if (intervals.Count == 0) return "frames=0";
                double[] sorted = intervals.OrderBy(v => v).ToArray();
                double total = sorted.Sum();
                int over50 = sorted.Count(v => v > 50), over100 = sorted.Count(v => v > 100);
                double lagMs = sorted.Where(v => v > 100).Sum();
                string top = string.Join(",", sorted.Reverse().Take(5).Select(v => v.ToString("0", C)));
                return string.Format(C, "frames={0} fps={1:0.0} meanMs={2:0.000} p50Ms={3:0.000} p95Ms={4:0.000} p99Ms={5:0.000} maxMs={6:0.0} over50={7} over100={8} lagSumMs={9:0} top5Ms={10}",
                    sorted.Length, sorted.Length * 1000.0 / total, total / sorted.Length, Percentile(sorted, 0.50),
                    Percentile(sorted, 0.95), Percentile(sorted, 0.99), sorted[sorted.Length - 1], over50, over100, lagMs, top);
            }

            private static double Percentile(double[] sorted, double fraction) =>
                sorted[Math.Min(sorted.Length - 1, (int)Math.Floor(fraction * (sorted.Length - 1)))];
        }
    }
}
