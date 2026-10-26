using System;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Logging;
using CrusaderDE;
using UnityEngine;

namespace BugfixesAndQoL.FocusGuardTests
{
    internal static class ProductionHookTests
    {
        private static readonly DisplaySettingsSnapshot Target = new DisplaySettingsSnapshot(2459, 1046, 3440, 1440, 240, 1);
        private static int passed, failed;

        internal static void Run()
        {
            passed = failed = 0;
            Case("off-list Borderless focus recovery", OffListFocusRecovery);
            Case("off-list automatic saves retain all six loaded values", OffListAutomaticSave);
            Case("listed, missing, empty and null mode lists are equivalent", ModeListsAreEquivalent);
            Case("two distinct matching frames complete recovery", MatchingFramesCompleteRecovery);
            Case("timeout removes observation but retains save protection", TimeoutRetainsProtection);
            Case("focus loss removes observation and retains the original target", FocusLossRetainsTarget);
            Case("manual Apply remains authoritative during recovery", ManualApplyIsAuthoritative);
            Case("disabled features and non-Borderless settings remain inactive", InactiveSettingsAreUntouched);
            Case("disabling during recovery removes observation", DisableDuringRecovery);
            Case("already matching display needs no recovery request", AlreadyMatchingDisplay);
            Case("settings reload supersedes the previous protected target", ReloadReplacesTarget);
            Console.WriteLine($"Production display-hook tests: {passed} passed, {failed} failed.");
            if (failed != 0) throw new InvalidOperationException("Production display-hook regressions failed.");
        }

        private static void Case(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS: " + name); }
            catch (Exception exception) { failed++; Console.Error.WriteLine("FAIL: " + name + ": " + exception); }
            finally { Application.ResetProcess(); MonoMod.RuntimeDetour.Hook.ResetProcess(); }
        }

        private static void OffListFocusRecovery()
        {
            using (var fixture = new Fixture())
            {
                Application.Focus(false);
                Application.Focus(true);
                AssertRequest(Target);
                Check(Application.RenderObservers == 1, "recovery observation was not installed exactly once");
            }
        }

        private static void OffListAutomaticSave()
        {
            foreach (bool focused in new[] { false, true })
            using (var fixture = new Fixture())
            {
                if (!focused) Application.Focus(false);
                SetSettings(new DisplaySettingsSnapshot(1280, 768, 1920, 1080, 60, 1));
                ConfigSettings.SaveSettings(onlyWhenAlreadyExists: true);
                Equal(ConfigSettings.Saved, Target);
                Check(ConfigSettings.SaveCalls == 1 && ConfigSettings.SavedOnlyWhenAlreadyExists,
                    "Vanilla persistence was skipped, repeated or given a different flag");
                Check(Screen.Requests.Count == (focused ? 1 : 0), "recovery ran while unfocused or failed at startup");
                if (focused) AssertRequest(Target);
                else { Application.Focus(true); AssertRequest(Target); }
            }
        }

        private static void ModeListsAreEquivalent()
        {
            foreach (Resolution[] modes in new[] {
                new[] { new Resolution { width = 3440, height = 1440, refreshRate = 60 } },
                new[] { new Resolution { width = 1920, height = 1080, refreshRate = 240 } },
                Array.Empty<Resolution>(), null })
            using (var fixture = new Fixture())
            {
                Screen.resolutions = modes;
                Application.Focus(false);
                Application.Focus(true);
                AssertRequest(Target);
            }
        }

        private static void MatchingFramesCompleteRecovery()
        {
            using (var fixture = new Fixture())
            {
                Application.Focus(false); Application.Focus(true);
                MatchDisplay(Target);
                Application.Render();
                Application.Render(nextFrame: false);
                Check(Application.RenderObservers == 1, "one frame counted twice");
                Screen.width = 1920;
                Application.Render();
                MatchDisplay(Target);
                Application.Render();
                Check(Application.RenderObservers == 1, "mismatch did not reset consecutive matches");
                Application.Render();
                Check(Application.RenderObservers == 0 && Screen.Requests.Count == 1, "success left frame work or retried");
                SetSettings(new DisplaySettingsSnapshot(1280, 768, 1920, 1080, 60, 1));
                ConfigSettings.SaveSettings();
                Check(ConfigSettings.Saved.FullscreenWidth == 1920, "completed recovery retained stale save protection");
            }
        }

        private static void TimeoutRetainsProtection()
        {
            using (var fixture = new Fixture())
            {
                Application.Focus(false); Application.Focus(true);
                // Inject elapsed time into the production observer; no real five-second sleep.
                typeof(DisplayResolutionPersistenceHook).GetField("recoveryStartedTimestamp",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fixture.Hook,
                        Stopwatch.GetTimestamp() - 6 * Stopwatch.Frequency);
                Application.Render(); Application.Render();
                Check(Application.RenderObservers == 0 && Screen.Requests.Count == 1, "timeout left polling or retried");
                Application.Focus(false);
                SetSettings(new DisplaySettingsSnapshot(1280, 768, 1920, 1080, 60, 1));
                ConfigSettings.SaveSettings();
                Equal(ConfigSettings.Saved, Target);
                Application.Focus(true);
                Check(Screen.Requests.Count == 2, "next focus gain could not recover after timeout");
            }
        }

        private static void FocusLossRetainsTarget()
        {
            using (var fixture = new Fixture())
            {
                Application.Focus(false); Application.Focus(true);
                SetSettings(new DisplaySettingsSnapshot(1280, 768, 1920, 1080, 60, 1));
                Application.Focus(false);
                Check(Application.RenderObservers == 0, "focus loss left observation active");
                ConfigSettings.SaveSettings();
                Equal(ConfigSettings.Saved, Target);
                Application.Focus(true);
                Check(Screen.Requests.Count == 2, "new focus event failed to start recovery");
                Check(Screen.Requests[1].Width == Target.FullscreenWidth, "focus loss replaced the original target");
            }
        }

        private static void ManualApplyIsAuthoritative()
        {
            using (var fixture = new Fixture())
            {
                Application.Focus(false); Application.Focus(true);
                var manual = new DisplaySettingsSnapshot(1280, 768, 1920, 1080, 60, 0);
                var options = new HUD_Options { OriginalAction = parameter => { SetSettings(manual); ConfigSettings.SaveSettings(); } };
                options.ButtonClicked(-2);
                Equal(ConfigSettings.Saved, manual);
                Check(options.OriginalCalls == 1 && ConfigSettings.SaveCalls == 1 && Application.RenderObservers == 0,
                    "manual Apply skipped Vanilla or retained recovery observation");
                Application.Focus(false); Application.Focus(true);
                Check(Screen.Requests.Count == 1, "manual Exclusive selection was changed back to Borderless");
            }
        }

        private static void InactiveSettingsAreUntouched()
        {
            for (int variant = 0; variant < 4; variant++)
            using (var fixture = new Fixture(load: false))
            {
                if (variant == 0) fixture.Settings.PreserveDisplayResolution = false;
                if (variant == 1) fixture.Settings.EnableClientFeatures = false;
                if (variant >= 2) ConfigSettings.Settings_LastFullscreenType = variant == 2 ? 0 : -1;
                ConfigSettings.LoadSettings();
                Application.Focus(false); Application.Focus(true);
                SetSettings(new DisplaySettingsSnapshot(1280, 768, 1920, 1080, 60, 0));
                ConfigSettings.SaveSettings();
                Check(Screen.Requests.Count == 0 && Application.RenderObservers == 0 && ConfigSettings.Saved.FullscreenWidth == 1920,
                    "inactive display protection changed Vanilla behavior");
            }
        }

        private static void DisableDuringRecovery()
        {
            using (var fixture = new Fixture())
            {
                Application.Focus(false); Application.Focus(true);
                fixture.Settings.PreserveDisplayResolution = false;
                fixture.Settings.Notify(nameof(BugfixesAndQoLViewModel.PreserveDisplayResolution));
                Check(Application.RenderObservers == 0, "disable left observation active");
                SetSettings(new DisplaySettingsSnapshot(1280, 768, 1920, 1080, 60, 1));
                ConfigSettings.SaveSettings();
                Check(ConfigSettings.Saved.FullscreenWidth == 1920, "disable retained protection");
            }
        }

        private static void AlreadyMatchingDisplay()
        {
            using (var fixture = new Fixture())
            {
                MatchDisplay(Target);
                Application.Focus(false); Application.Focus(true);
                Check(Screen.Requests.Count == 0 && Application.RenderObservers == 0, "matching target requested recovery");
            }
        }

        private static void ReloadReplacesTarget()
        {
            using (var fixture = new Fixture())
            {
                var replacement = new DisplaySettingsSnapshot(1280, 768, 2560, 1440, 144, 1);
                SetSettings(replacement);
                ConfigSettings.LoadSettings();
                Application.Focus(false); Application.Focus(true);
                AssertRequest(replacement);
                Check(ConfigSettings.LoadCalls == 2, "Vanilla load was skipped or repeated");
            }
        }

        private static void AssertRequest(DisplaySettingsSnapshot expected)
        {
            Check(Screen.Requests.Count == 1, "expected exactly one resolution request");
            Screen.ResolutionRequest request = Screen.Requests[0];
            Check(request.Width == expected.FullscreenWidth && request.Height == expected.FullscreenHeight &&
                request.Refresh == expected.FullscreenRefresh && request.Mode == FullScreenMode.FullScreenWindow,
                "resolution request did not retain target dimensions, mode and refresh");
        }

        private static void MatchDisplay(DisplaySettingsSnapshot target)
        {
            Screen.width = target.FullscreenWidth; Screen.height = target.FullscreenHeight;
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        private static void SetSettings(DisplaySettingsSnapshot value)
        {
            ConfigSettings.Settings_LastWindowWidth = value.WindowWidth; ConfigSettings.Settings_LastWindowHeight = value.WindowHeight;
            ConfigSettings.Settings_LastFullscreenWidth = value.FullscreenWidth; ConfigSettings.Settings_LastFullscreenHeight = value.FullscreenHeight;
            ConfigSettings.Settings_LastFullscreenRefresh = value.FullscreenRefresh; ConfigSettings.Settings_LastFullscreenType = value.FullscreenType;
        }

        private static void Equal(DisplaySettingsSnapshot actual, DisplaySettingsSnapshot expected) => Check(
            actual.WindowWidth == expected.WindowWidth && actual.WindowHeight == expected.WindowHeight &&
            actual.FullscreenWidth == expected.FullscreenWidth && actual.FullscreenHeight == expected.FullscreenHeight &&
            actual.FullscreenRefresh == expected.FullscreenRefresh && actual.FullscreenType == expected.FullscreenType,
            "persisted display snapshot differed: " + actual);
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private sealed class Fixture : IDisposable
        {
            internal readonly ManualLogSource Log = new ManualLogSource();
            internal readonly BugfixesAndQoLViewModel Settings = new BugfixesAndQoLViewModel();
            internal readonly DisplayResolutionPersistenceHook Hook;
            internal Fixture(bool load = true)
            {
                Application.ResetProcess(); MonoMod.RuntimeDetour.Hook.ResetProcess();
                Application.isFocused = true; Time.frameCount = 0; Screen.Requests.Clear();
                Screen.width = 1920; Screen.height = 1080; Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.currentResolution = new Resolution { width = 1920, height = 1080, refreshRate = 60 };
                Screen.resolutions = new[] { Screen.currentResolution };
                ConfigSettings.LoadCalls = ConfigSettings.SaveCalls = 0;
                SetSettings(Target);
                Hook = new DisplayResolutionPersistenceHook(Log, Settings);
                if (load) ConfigSettings.LoadSettings();
            }
            public void Dispose()
            {
                // Simulated process exit, never an instruction to dispose published runtime hooks.
                Application.ResetProcess(); MonoMod.RuntimeDetour.Hook.ResetProcess();
                Check(Log.Errors.Count == 0, "production hook reported an exception: " + string.Join("; ", Log.Errors));
            }
        }
    }
}
