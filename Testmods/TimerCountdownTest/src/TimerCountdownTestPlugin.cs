using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using UnityEngine;

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
        private static bool callbackErrorLogged;
        private static int lastRenderedFrame = -1;
        // BEGIN TEMP CRASH DIAGNOSTICS
        private static bool afterStartupLogged;
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
                Application.onBeforeRender += OnBeforeRender;
                subscribed = true;
                rootedLog.LogInfo("Timer countdown bindings and persistent Unity render event registered.");
                // BEGIN TEMP CRASH DIAGNOSTICS
                rootedLog.LogInfo("TIMER_CRASH_DIAGNOSTICS objectiveNotifications=" +
                    TimerCountdownViewModel.NotifyObjectiveRemaining + " ostNotifications=" +
                    TimerCountdownViewModel.NotifyOstRemaining + " xamlPatches=unchanged updatePath=UnityMainThread");
                // END TEMP CRASH DIAGNOSTICS
            }
            catch (Exception ex)
            {
                rootedLog.LogError("Timer countdown initialization failed: " + ex);
            }
        }

        private static void OnBeforeRender()
        {
            int frame = Time.frameCount;
            if (lastRenderedFrame == frame) return;
            lastRenderedFrame = frame;
            try
            {
                if (!IsGameplayReady())
                {
                    rootedViewModel.SetRemaining(string.Empty, string.Empty);
                    return;
                }

                string objective = ReadObjectiveRemaining();
                string ost = TimerCountdownViewModel.NotifyOstRemaining ? ReadOstRemaining() : string.Empty;
                rootedViewModel.SetRemaining(objective, ost);
                // BEGIN TEMP CRASH DIAGNOSTICS
                if (!afterStartupLogged && MainViewModel.viewModelLoaded &&
                    MainViewModel.Instance?.HUDmain != null)
                {
                    afterStartupLogged = true;
                    rootedLog.LogInfo("TIMER_CRASH_DIAGNOSTICS persistent Unity main-thread render callback active after HUD startup; frame=" + frame);
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

        private static bool IsGameplayReady()
        {
            return MainViewModel.viewModelLoaded && MainViewModel.Instance?.Show_InGame == true &&
                Director.instance?.SimRunning == true;
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
