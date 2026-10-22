using BepInEx.Logging;
using System;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    // Local client optimizations of Vanilla presentation paths (VanillaOptimization.md V1, V5, V7).
    // None of them changes simulation state, so they need no host or multiplayer agreement.
    // Installed once from LibraryLoaded; the Harmony patches stay published for the process lifetime
    // (rooted by the static Harmony instances) and the settings only switch the logical activation.
    internal static class VanillaPerformanceFeature
    {
        private static BugfixesAndQoLViewModel settings;
        private static int installed;

        internal static void Install(ManualLogSource log, BugfixesAndQoLViewModel viewModel)
        {
            if (Interlocked.Exchange(ref installed, 1) != 0) return;
            settings = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            RadarRebindOptimization.Install(log);
            EnginePresentationBridge.Install(log);
            MeritDataLockOptimization.Install(log);
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
                propertyName == nameof(BugfixesAndQoLViewModel.EnableVanillaPerformanceOptimizations))
                RefreshEnabled();
        }

        private static void RefreshEnabled()
        {
            bool enabled = settings.EnableClientFeatures && settings.EnableVanillaPerformanceOptimizations;
            RadarRebindOptimization.SetEnabled(enabled);
            EnginePresentationBridge.SetEnabled(enabled);
            MeritDataLockOptimization.SetEnabled(enabled);
        }
    }
}
