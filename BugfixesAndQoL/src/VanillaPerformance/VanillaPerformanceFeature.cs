using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    // Local client optimizations of Vanilla presentation paths (VanillaOptimization.md V1, V5, V7, V13).
    // None of them changes simulation state, so they need no host or multiplayer agreement.
    // Installed once from LibraryLoaded; the Harmony patches and the V13 detour stay published for the
    // process lifetime (rooted by static fields) and the settings only switch the logical activation.
    internal static class VanillaPerformanceFeature
    {
        private static BugfixesAndQoLViewModel settings;
        private static int installed;

        internal static void Install(ManualLogSource log, BugfixesAndQoLViewModel viewModel, CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref installed, 1) != 0) return;
            settings = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            RadarRebindOptimization.Install(log);
            EnginePresentationBridge.Install(log);
            MeritDataLockOptimization.Install(log);
            MinimapRefreshThrottle.Install(log, context);
            settings.SettingChanged += OnSettingChanged;
            RefreshEnabled();
            // V7 reads the setting once, when Vanilla has finished loading its sprites (main menu);
            // later changes apply after a restart.
            SpriteSheetMergeOptimization.Arm(log,
                () => settings.EnableClientFeatures ? settings.SpriteSheetMergeMode : SpriteSheetMergeMode.Off);
        }

        private static void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableMod) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableVanillaPerformanceOptimizations) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableMinimapRefreshThrottle))
                RefreshEnabled();
        }

        private static void RefreshEnabled()
        {
            bool enabled = settings.VanillaPerformanceOptimizationsActive;
            RadarRebindOptimization.SetEnabled(enabled);
            EnginePresentationBridge.SetEnabled(enabled);
            MeritDataLockOptimization.SetEnabled(enabled);
            // Lossy (minimap up to 100 ms late), therefore a separate setting. Host-controlled so every
            // peer of a lockstep match draws the same way (the slowest peer sets the speed).
            MinimapRefreshThrottle.SetEnabled(settings.EnableMod && settings.EnableMinimapRefreshThrottle);
        }
    }
}
