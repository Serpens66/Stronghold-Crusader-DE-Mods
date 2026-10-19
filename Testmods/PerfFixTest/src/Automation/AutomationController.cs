using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using BepInEx.Configuration;
using CrusaderDE;
using SHCDESE.API;
using UnityEngine;

namespace PerfFixTest.Automation
{
    // Reproducible A/B measurement run (see AutomatisierungTest.md):
    // main menu -> load save -> warm-up ticks -> zoom fully out -> settle -> native pause (hold)
    // -> unpause (run) -> PASS/FAIL -> optional quit. Every phase boundary is logged with an
    // "AUTOMATION" marker so PerfProbe windows can be cut by phase.
    //
    // Deliberate exception (user decision 2026-10-20): no APIShared mission lifecycle, so the
    // measured installation stays Extender + PerfProbe + PerfFixTest only. Readiness is derived
    // from Vanilla state plus observed simulation ticks; this detector exists only in this test mod.
    //
    // Driven by a static Application.onBeforeRender callback (runs while paused; survives the
    // SHCDE startup cleanup), limited to once per frame. Simulation ticks arrive on the sim
    // thread and only increment a counter.
    internal static class AutomationController
    {
        private enum Phase { Disabled, WaitMenu, WaitMission, ZoomOut, Settle, PauseHold, RunHold, Done, Failed }

        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static Phase phase = Phase.Disabled;
        private static string savePath, runLabel;
        private static int warmupTicks, settleSeconds, pauseSeconds, runSeconds, timeoutSeconds, maxZoomSteps;
        private static bool quitWhenDone, extenderReady, tickSubscribed;
        private static int lastFrame = -1, zoomSteps, unchangedSteps;
        private static long ticks, ticksAtPhase;
        private static long startStamp, phaseStamp;
        private static float zoomBeforeStep = -1f;
        private static bool stepPending, resumeConfirmed;

        internal static void Bind(ConfigFile config)
        {
            bool enabled = config.Bind("Automation", "Enabled", false,
                "Run the automated A/B measurement once per game start (load save, zoom out, pause, run, quit).").Value;
            savePath = config.Bind("Automation", "SavePath", "test_dustyFPS.sav",
                "Single-player save: absolute path or relative to the game's Saves directory.").Value;
            runLabel = config.Bind("Automation", "RunLabel", "",
                "Free label written to every AUTOMATION log line (e.g. A or B).").Value;
            warmupTicks = config.Bind("Automation", "WarmupTicks", 80, "Simulation ticks to observe after loading before zooming.").Value;
            settleSeconds = config.Bind("Automation", "SettleSeconds", 3, "Seconds to wait after zooming out before pausing.").Value;
            pauseSeconds = config.Bind("Automation", "PauseSeconds", 25, "Seconds to hold the native pause (identical scene).").Value;
            runSeconds = config.Bind("Automation", "RunSeconds", 25, "Seconds to run unpaused at full zoom-out.").Value;
            timeoutSeconds = config.Bind("Automation", "TimeoutSeconds", 300, "Overall timeout from game start; FAIL on expiry.").Value;
            maxZoomSteps = config.Bind("Automation", "MaxZoomSteps", 20, "Upper bound of zoom-out steps.").Value;
            quitWhenDone = config.Bind("Automation", "QuitWhenDone", true, "Quit the game after PASS or FAIL.").Value;
            if (!enabled) return;
            phase = Phase.WaitMenu;
            startStamp = phaseStamp = Stopwatch.GetTimestamp();
            Application.onBeforeRender += OnBeforeRender;
            Log("armed save=" + savePath + " warmupTicks=" + warmupTicks + " settle=" + settleSeconds + "s pause=" + pauseSeconds +
                "s run=" + runSeconds + "s timeout=" + timeoutSeconds + "s quit=" + quitWhenDone);
        }

        // Extender APIs (GameTimeManagerAPI, GamePlayerManagerAPI) only after the native library is ready.
        internal static void OnExtenderReady() => extenderReady = true;

        private static void OnTick(int tick) => Interlocked.Increment(ref ticks);

        private static void OnBeforeRender()
        {
            if (phase == Phase.Disabled) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (phase == Phase.Done || phase == Phase.Failed) { StepQuit(); return; }
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
            Enter(Phase.WaitMission, "loading " + resolved);
            // Same preparation as FrontendMenus' single-player load confirmation (as in AutoLoadSave).
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
            if (Seconds(phaseStamp) < pauseSeconds) return;
            Log("phase=pause-end held=" + Seconds(phaseStamp).ToString("0.0", C) + "s zoom=" + Zoom());
            if (!TrySetPause(false)) { Fail("native pause could not be lifted"); return; }
            resumeConfirmed = false;
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
            Log("phase=run-end ran=" + Seconds(phaseStamp).ToString("0.0", C) + "s ticks=" + (Interlocked.Read(ref ticks) - ticksAtPhase) + " zoom=" + Zoom());
            Finish(Phase.Done, "PASS total=" + Seconds(startStamp).ToString("0.0", C) + "s");
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
            FlushDiskLog();
            Application.Quit();
        }

        private static void FlushDiskLog()
        {
            try
            {
                foreach (BepInEx.Logging.ILogListener listener in BepInEx.Logging.Logger.Listeners)
                    if (listener is BepInEx.Logging.DiskLogListener disk) disk.LogWriter?.Flush();
            }
            catch (Exception exception) { PerfFixTestPlugin.Fail("disk log flush failed: " + exception.Message); }
        }

        private static string Zoom() => PerfectPixelWithZoom.instance != null ? PerfectPixelWithZoom.instance.getZoom().ToString("0.###", C) : "n/a";

        private static double Seconds(long since) => (Stopwatch.GetTimestamp() - since) / (double)Stopwatch.Frequency;

        private static void Log(string message) => PerfFixTestPlugin.Note("AUTOMATION run=" + (runLabel.Length == 0 ? "-" : runLabel) +
            " features(" + PerfFixTestPlugin.FeatureSummary + ") " + message);
    }
}
