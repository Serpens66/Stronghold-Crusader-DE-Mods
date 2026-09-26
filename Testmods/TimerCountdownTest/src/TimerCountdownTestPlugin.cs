using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace TimerCountdownTest
{
    [BepInDependency("000shcdese", "2.10.1")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class TimerCountdownTestPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "TimerCountdownTest_Serp";
        public const string PluginName = "Timer Countdown Test";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource rootedLog;
        private static TimerCountdownViewModel rootedViewModel;
        private static bool subscribed;
        private static bool afterStartupLogged;
        private static bool callbackErrorLogged;
        // BEGIN TEMP CRASH DIAGNOSTICS: periodic proof that timer reads continue.
        private static int lastDiagnosticTick = -1;
        // END TEMP CRASH DIAGNOSTICS

        private void Awake()
        {
            rootedLog = Logger;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (subscribed) return;
            try
            {
                var viewModel = new TimerCountdownViewModel();
                GameXAMLManagerAPI.Instance.RegisterBinding("TimerCountdownObjective", viewModel);
                GameXAMLManagerAPI.Instance.RegisterBinding("TimerCountdownBriefing", viewModel);
                GameXAMLManagerAPI.Instance.RegisterBinding("TimerCountdownOst", viewModel);
                rootedViewModel = viewModel;
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                subscribed = true;
                rootedLog.LogInfo("Timer countdown bindings and persistent game-tick event registered.");
                // BEGIN TEMP CRASH DIAGNOSTICS
                rootedLog.LogInfo("TIMER_CRASH_DIAGNOSTICS objectiveNotifications=" +
                    TimerCountdownViewModel.NotifyObjectiveRemaining + " ostNotifications=" +
                    TimerCountdownViewModel.NotifyOstRemaining + " xamlPatches=unchanged");
                // END TEMP CRASH DIAGNOSTICS
            }
            catch (Exception ex)
            {
                rootedLog.LogError("Timer countdown initialization failed: " + ex);
            }
        }

        private static void OnGameTick(int tick)
        {
            try
            {
                if (!afterStartupLogged)
                {
                    afterStartupLogged = true;
                    rootedLog.LogInfo("Timer countdown runtime active after startup cleanup; gameTick=" + tick);
                }

                string objective = ReadObjectiveRemaining();
                string ost = ReadOstRemaining();
                rootedViewModel.SetRemaining(objective, ost);
                // BEGIN TEMP CRASH DIAGNOSTICS
                if (lastDiagnosticTick < 0 || tick < lastDiagnosticTick || tick - lastDiagnosticTick >= 200)
                {
                    lastDiagnosticTick = tick;
                    rootedLog.LogInfo("TIMER_CRASH_DIAGNOSTICS tick=" + tick +
                        " objective='" + objective + "' ost='" + ost +
                        "' viewModelUpdated=true");
                }
                // END TEMP CRASH DIAGNOSTICS
            }
            catch (Exception ex)
            {
                if (callbackErrorLogged) return;
                callbackErrorLogged = true;
                rootedLog.LogError("Timer countdown update failed: " + ex);
            }
        }

        private static string ReadObjectiveRemaining()
        {
            if (GameData.scenario == null) return string.Empty;
            int start = 0;
            int now = 0;
            int end = 0;
            if (GameData.scenario.getWinTimer(ref start, ref now, ref end) == null || end <= start)
                return string.Empty;
            return TimerCountdownPolicy.FormatTicks((long)end - now);
        }

        private static string ReadOstRemaining()
        {
            OnScreenText screenText = OnScreenText.Instance;
            if (screenText == null) return string.Empty;
            long? remaining = TimerCountdownPolicy.SelectOstRemaining(
                ReadOst(screenText, Enums.eOnScreenText.OST_TIMETODEFEAT),
                ReadOst(screenText, Enums.eOnScreenText.OST_WIN_TIMER),
                ReadOst(screenText, Enums.eOnScreenText.OST_PEACETIMER));
            return remaining.HasValue ? TimerCountdownPolicy.FormatTicks(remaining.Value) : string.Empty;
        }

        private static long? ReadOst(OnScreenText screenText, Enums.eOnScreenText id)
        {
            bool turnedOff = false;
            bool changed = false;
            OnScreenText.OST entry = screenText.getOST(id, ref turnedOff, ref changed, false);
            return entry != null ? (long?)entry.curValue : null;
        }
    }
}
