using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class TimerCountdownFeature
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly TimerCountdownViewModel viewModel = new TimerCountdownViewModel();
        private readonly HashSet<string> loggedFailureKinds = new HashSet<string>(StringComparer.Ordinal);
        private int lastRenderedFrame = -1;
        private volatile bool enabled;
        private int clearPending;

        internal TimerCountdownFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLCountdownObjective", viewModel);
            GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLCountdownBriefing", viewModel);
            GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLCountdownOst", viewModel);
            enabled = settings.EnableClientFeatures && settings.ShowCountdownTimers;
            settings.SettingChanged += OnSettingChanged;
            // Unity's static publisher keeps this callback alive after BepInEx startup cleanup.
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(BugfixesAndQoLViewModel.ShowCountdownTimers) &&
                propertyName != nameof(BugfixesAndQoLViewModel.EnableClientFeatures))
                return;
            enabled = settings.EnableClientFeatures && settings.ShowCountdownTimers;
            if (enabled) return;
            Interlocked.Exchange(ref clearPending, 1);
        }

        private void OnBeforeRender()
        {
            if (!enabled && Volatile.Read(ref clearPending) == 0) return;
            int frame = Time.frameCount;
            if (lastRenderedFrame == frame) return;
            lastRenderedFrame = frame;
            bool clear = Interlocked.Exchange(ref clearPending, 0) != 0;
            if (!enabled)
            {
                if (clear) UpdateViewModel(string.Empty, string.Empty);
                return;
            }
            string objective = string.Empty;
            string ost = string.Empty;
            try
            {
                if (IsGameplayReady())
                {
                    objective = ReadObjectiveRemaining();
                    ost = ReadOstRemaining();
                }
            }
            catch (Exception ex)
            {
                objective = string.Empty;
                ost = string.Empty;
                LogFailureOnce("timer read", ex);
            }
            UpdateViewModel(objective, ost);
        }

        private void UpdateViewModel(string objective, string ost)
        {
            // A failed Noesis notification must not trigger a second notification in this frame.
            if (!viewModel.TrySetRemaining(objective, ost, out Exception failure))
                LogFailureOnce("view-model notification", failure);
        }

        private void LogFailureOnce(string phase, Exception failure)
        {
            string kind = phase + ":" + failure.GetType().FullName;
            if (loggedFailureKinds.Add(kind))
                Shared.DebugLogHelper.LogError(log, "Countdown " + phase + " failed: " + failure);
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
