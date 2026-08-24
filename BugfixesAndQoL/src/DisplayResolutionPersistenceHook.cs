// Feature: Protect loaded borderless display settings during startup and focus changes without permanent frame polling.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class DisplayResolutionPersistenceHook : IDisposable
    {
        private const long RecoveryTimeoutMilliseconds = 5000;

        private delegate void LoadSettingsDelegate();
        private delegate void SaveSettingsDelegate(bool onlyWhenAlreadyExists);
        private delegate void OptionsButtonDelegate(HUD_Options self, int parameter);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly DisplayResolutionFocusState state = new DisplayResolutionFocusState();
        private Hook loadSettingsHook;
        private Hook saveSettingsHook;
        private Hook optionsButtonHook;
        private LoadSettingsDelegate loadSettingsOriginal;
        private SaveSettingsDelegate saveSettingsOriginal;
        private OptionsButtonDelegate optionsButtonOriginal;
        private long recoveryStartedTimestamp;
        private int lastRecoveryFrame = -1;
        private int manualApplyDepth;
        private bool settingsLoaded;
        private bool focusRecoveryPending;
        private bool interceptedSaveLogged;
        private bool failureLogged;
        private bool disposed;

        public DisplayResolutionPersistenceHook(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            try
            {
                loadSettingsHook = new Hook(
                    FindMethod(typeof(ConfigSettings), nameof(ConfigSettings.LoadSettings), true, Type.EmptyTypes),
                    (LoadSettingsDelegate)LoadSettingsHook);
                loadSettingsOriginal = loadSettingsHook.GenerateTrampoline<LoadSettingsDelegate>();
                saveSettingsHook = new Hook(
                    FindMethod(typeof(ConfigSettings), nameof(ConfigSettings.SaveSettings), true, new[] { typeof(bool) }),
                    (SaveSettingsDelegate)SaveSettingsHook);
                saveSettingsOriginal = saveSettingsHook.GenerateTrampoline<SaveSettingsDelegate>();
                optionsButtonHook = new Hook(
                    FindMethod(typeof(HUD_Options), nameof(HUD_Options.ButtonClicked), false, new[] { typeof(int) }),
                    (OptionsButtonDelegate)OptionsButtonHook);
                optionsButtonOriginal = optionsButtonHook.GenerateTrampoline<OptionsButtonDelegate>();
                settings.SettingChanged += OnSettingChanged;
                Application.focusChanged += OnFocusChanged;
            }
            catch
            {
                Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL event-driven startup and focus display-resolution guard installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            StopRecoveryObservation();
            settings.SettingChanged -= OnSettingChanged;
            Application.focusChanged -= OnFocusChanged;
            UndoAndDispose(ref optionsButtonHook);
            UndoAndDispose(ref saveSettingsHook);
            UndoAndDispose(ref loadSettingsHook);
            state.Cancel();
            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL display-resolution guard disposed.");
        }

        private void LoadSettingsHook()
        {
            // Vanilla owns loading and must run exactly once.
            loadSettingsOriginal();

            try
            {
                // A deliberate reload supersedes any snapshot from an earlier load in the same process.
                CancelProtection("settings reloaded");
                settingsLoaded = true;
                if (!Application.isFocused)
                {
                    focusRecoveryPending = true;
                    state.OnFocusLost();
                }

                // The loaded values are authoritative before Unity finalizes its borderless window.
                TryArmProtection(
                    Application.isFocused
                        ? "settings loaded for startup protection"
                        : "settings loaded while application was unfocused");
            }
            catch (Exception ex)
            {
                LogFailureOnce("after settings load", ex);
            }
        }

        private void SaveSettingsHook(bool onlyWhenAlreadyExists)
        {
            try
            {
                DisplaySettingsSnapshot current = CaptureSettings();
                if (state.TryProtectSave(
                        current,
                        IsEnabled,
                        manualApplyDepth > 0,
                        out DisplaySettingsSnapshot protectedSettings))
                {
                    bool changed = !SettingsEqual(current, protectedSettings);
                    if (changed && !IsSupportedFullscreenResolution(
                            protectedSettings.FullscreenWidth,
                            protectedSettings.FullscreenHeight))
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            "Bugfixes and QoL left an automatic display change untouched because this PC no longer " +
                            $"reports the protected target: observed={current}, protected={protectedSettings}.");
                        CancelProtection("protected target resolution unsupported before save");
                    }
                    else
                    {
                        RestoreSettings(protectedSettings);
                        if (changed)
                        {
                            if (!interceptedSaveLogged)
                            {
                                interceptedSaveLogged = true;
                                Shared.DebugLogHelper.LogWarning(
                                    log,
                                    "Bugfixes and QoL prevented an automatic borderless resolution change " +
                                    $"from reaching settings.cfg: observed={current}, protected={protectedSettings}, " +
                                    $"focused={Application.isFocused}.");
                            }

                            // Focused startup changes have no later focus event, so this Save is the recovery trigger.
                            if (Application.isFocused)
                                RecoverProtectedTarget("automatic change detected before settings save");
                        }
                    }
                }
                else if (!IsEnabled && state.IsArmed)
                {
                    CancelProtection("feature disabled");
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce("before settings save", ex);
            }

            // Diagnostics and protection must never suppress Vanilla persistence.
            saveSettingsOriginal(onlyWhenAlreadyExists);
        }

        private void OptionsButtonHook(HUD_Options self, int parameter)
        {
            if (parameter != -2)
            {
                optionsButtonOriginal(self, parameter);
                return;
            }

            manualApplyDepth++;
            try
            {
                if (state.IsArmed)
                {
                    CancelProtection("manual display Apply selected");
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Bugfixes and QoL accepted the manually applied display settings as the new target.");
                }

                // The user's Apply action remains entirely authoritative.
                optionsButtonOriginal(self, parameter);
            }
            finally
            {
                manualApplyDepth--;
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(BugfixesAndQoLViewModel.PreserveDisplayResolution) &&
                propertyName != nameof(BugfixesAndQoLViewModel.EnableClientFeatures))
                return;

            try
            {
                if (!IsEnabled)
                {
                    CancelProtection("feature disabled");
                    return;
                }

                if (settingsLoaded)
                    TryArmProtection("feature enabled after settings load");
            }
            catch (Exception ex)
            {
                LogFailureOnce("while applying the feature setting", ex);
                CancelProtection("feature setting handling failed");
            }
        }

        private void OnFocusChanged(bool focused)
        {
            try
            {
                if (!focused)
                {
                    StopRecoveryObservation();
                    focusRecoveryPending = true;
                    state.OnFocusLost();
                    TryArmProtection("application lost focus");
                    return;
                }

                // Unity can emit an initial focused=true notification without a preceding loss.
                if (!focusRecoveryPending)
                    return;

                focusRecoveryPending = false;
                RecoverAfterFocusGain();
            }
            catch (Exception ex)
            {
                LogFailureOnce($"on focus changed to {focused}", ex);
                CancelProtection("focus handling failed");
            }
        }

        private void TryArmProtection(string reason)
        {
            if (!settingsLoaded || !IsEnabled)
                return;

            DisplaySettingsSnapshot snapshot = CaptureSettings();
            if (!state.TryArm(snapshot, enabled: true))
                return;

            interceptedSaveLogged = false;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bugfixes and QoL armed borderless resolution protection ({reason}): target={snapshot}, actual={DescribeActual()}.");
        }

        private void RecoverAfterFocusGain()
        {
            RecoverProtectedTarget("focus regained");
        }

        private void RecoverProtectedTarget(string reason)
        {
            if (!state.IsArmed)
                return;

            if (!IsEnabled)
            {
                CancelProtection("feature disabled before focus recovery");
                return;
            }

            DisplaySettingsSnapshot target = state.Snapshot;
            RestoreSettings(target);
            bool matches = ResolutionMatches(target);
            DisplayRecoveryAction action = state.OnFocusGained(enabled: true, matches);
            if (action == DisplayRecoveryAction.Completed)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL completed borderless resolution protection without a correction " +
                    $"({reason}): actual={DescribeActual()}.");
                return;
            }

            if (action != DisplayRecoveryAction.ApplyTarget)
                return;

            if (!IsSupportedFullscreenResolution(target.FullscreenWidth, target.FullscreenHeight))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL cannot restore the protected borderless target because this PC does not report it: target={target}.");
                CancelProtection("target resolution unsupported");
                return;
            }

            Screen.SetResolution(
                target.FullscreenWidth,
                target.FullscreenHeight,
                FullScreenMode.FullScreenWindow,
                target.FullscreenRefresh > 0 ? target.FullscreenRefresh : 0);
            recoveryStartedTimestamp = Stopwatch.GetTimestamp();
            lastRecoveryFrame = -1;
            Application.onBeforeRender -= ObserveRecoveryBeforeRender;
            Application.onBeforeRender += ObserveRecoveryBeforeRender;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Bugfixes and QoL requested borderless resolution recovery ({reason}): " +
                $"target={target}, observedBeforeRequest={DescribeActual()}.");
        }

        private void ObserveRecoveryBeforeRender()
        {
            if (lastRecoveryFrame == Time.frameCount)
                return;

            lastRecoveryFrame = Time.frameCount;
            try
            {
                long elapsed = ElapsedMilliseconds(
                    recoveryStartedTimestamp,
                    Stopwatch.GetTimestamp());
                DisplayRecoveryAction action = state.ObserveRecovery(
                    IsEnabled,
                    Application.isFocused,
                    state.IsArmed && ResolutionMatches(state.Snapshot),
                    elapsed >= RecoveryTimeoutMilliseconds);

                if (!state.IsRecoveryActive)
                    StopRecoveryObservation();

                if (action == DisplayRecoveryAction.Completed)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Bugfixes and QoL confirmed borderless resolution recovery after two rendered frames: actual={DescribeActual()}.");
                }
                else if (action == DisplayRecoveryAction.TimedOut)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL stopped temporary resolution verification after {RecoveryTimeoutMilliseconds} ms; " +
                        $"save protection remains armed for the next focus gain. actual={DescribeActual()}.");
                }
            }
            catch (Exception ex)
            {
                StopRecoveryObservation();
                LogFailureOnce("during temporary recovery verification", ex);
            }
        }

        private void CancelProtection(string reason)
        {
            bool wasActive = state.IsArmed || state.IsRecoveryActive;
            StopRecoveryObservation();
            focusRecoveryPending = false;
            state.Cancel();
            if (wasActive)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL ended borderless resolution protection ({reason}).");
            }
        }

        private void StopRecoveryObservation()
        {
            Application.onBeforeRender -= ObserveRecoveryBeforeRender;
            recoveryStartedTimestamp = 0;
            lastRecoveryFrame = -1;
        }

        private static DisplaySettingsSnapshot CaptureSettings()
        {
            return new DisplaySettingsSnapshot(
                ConfigSettings.Settings_LastWindowWidth,
                ConfigSettings.Settings_LastWindowHeight,
                ConfigSettings.Settings_LastFullscreenWidth,
                ConfigSettings.Settings_LastFullscreenHeight,
                ConfigSettings.Settings_LastFullscreenRefresh,
                ConfigSettings.Settings_LastFullscreenType);
        }

        private static void RestoreSettings(DisplaySettingsSnapshot snapshot)
        {
            ConfigSettings.Settings_LastWindowWidth = snapshot.WindowWidth;
            ConfigSettings.Settings_LastWindowHeight = snapshot.WindowHeight;
            ConfigSettings.Settings_LastFullscreenWidth = snapshot.FullscreenWidth;
            ConfigSettings.Settings_LastFullscreenHeight = snapshot.FullscreenHeight;
            ConfigSettings.Settings_LastFullscreenRefresh = snapshot.FullscreenRefresh;
            ConfigSettings.Settings_LastFullscreenType = snapshot.FullscreenType;
        }

        private static bool SettingsEqual(
            DisplaySettingsSnapshot left,
            DisplaySettingsSnapshot right)
        {
            return
                left.WindowWidth == right.WindowWidth &&
                left.WindowHeight == right.WindowHeight &&
                left.FullscreenWidth == right.FullscreenWidth &&
                left.FullscreenHeight == right.FullscreenHeight &&
                left.FullscreenRefresh == right.FullscreenRefresh &&
                left.FullscreenType == right.FullscreenType;
        }

        private static bool ResolutionMatches(DisplaySettingsSnapshot target)
        {
            return
                Screen.fullScreenMode == FullScreenMode.FullScreenWindow &&
                Screen.width == target.FullscreenWidth &&
                Screen.height == target.FullscreenHeight;
        }

        private static bool IsSupportedFullscreenResolution(int width, int height)
        {
            Resolution[] resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
                return true;

            foreach (Resolution resolution in resolutions)
            {
                if (resolution.width == width && resolution.height == height)
                    return true;
            }

            return false;
        }

        private bool IsEnabled =>
            settings.EnableClientFeatures && settings.PreserveDisplayResolution;

        private static long ElapsedMilliseconds(long start, long end)
        {
            return (end - start) * 1000L / Stopwatch.Frequency;
        }

        private static string DescribeActual()
        {
            return
                $"{Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRate}Hz/" +
                Screen.fullScreenMode;
        }

        private void LogFailureOnce(string phase, Exception ex)
        {
            if (failureLogged)
                return;

            failureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL display-resolution protection failed {phase}; Vanilla remains active: {ex}");
        }

        private static MethodInfo FindMethod(
            Type type,
            string name,
            bool isStatic,
            Type[] parameterTypes)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            MethodInfo method = type.GetMethod(name, flags, null, parameterTypes, null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static void UndoAndDispose(ref Hook hook)
        {
            hook?.Undo();
            hook?.Dispose();
            hook = null;
        }
    }
}
