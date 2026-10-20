using System;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace PerfFixTest
{
    // Test build of the client-side performance fixes V1, V5 and V9 (see VanillaOptimization.md).
    // Each feature is read once at startup; patches are published for the process lifetime and
    // a failed validation only disables that feature (fail-closed, Vanilla/Extender unchanged).
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class PerfFixTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "PerfFixTest_Serp", Name = "Perf Fix Test", Version = "0.1.0";
        internal static ManualLogSource Log;
        internal static readonly Harmony Harmony = new Harmony("PerfFixTest_Serp");
        private static bool registered, installed, radarEnabled, meritEnabled, interpolateEnabled, sheetMergeEnabled;

        private void Awake()
        {
            if (registered) return;
            registered = true;
            Log = Logger;
            radarEnabled = Bind(Config, "RadarRebindSkip", "V1: skip re-binding the unchanged minimap texture to Noesis on every tick.");
            meritEnabled = Bind(Config, "MeritDataLockCache", "V5: return the last merit data instead of blocking while a simulation tick holds the engine lock.");
            interpolateEnabled = Bind(Config, "InterpolateEventReuse", "V9: reuse one Script Extender interpolate event object instead of allocating one per unit and frame.");
            sheetMergeEnabled = Bind(Config, "SheetMerge", "V7 (prototype): merge the TeamColour sprite sheets onto shared 16384 pages after sprite loading so units of different sheets batch (about +1.5 GB VRAM).");
            Automation.AutomationController.Bind(Config);
            // The automation's in-run V7 comparison switches the merge itself.
            if (Automation.AutomationController.ControlsSheetMerge) sheetMergeEnabled = false;
            FeatureSummary = "V1=" + (radarEnabled ? 1 : 0) + " V5=" + (meritEnabled ? 1 : 0) + " V9=" + (interpolateEnabled ? 1 : 0) + " V7=" + (sheetMergeEnabled ? 1 : 0);
            // Rooted by the extender's library publisher, not by this component (startup cleanup).
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            Note(Name + " " + Version + " loaded; radarRebindSkip=" + radarEnabled + " meritDataLockCache=" + meritEnabled +
                " interpolateEventReuse=" + interpolateEnabled + " sheetMerge=" + sheetMergeEnabled + " (restart required after config changes).");
        }

        private static bool Bind(ConfigFile config, string key, string description) =>
            config.Bind("Features", key, true, description + " Restart required.").Value;

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (installed) return;
            installed = true;
            if (radarEnabled) RadarRebindFix.Install();
            if (meritEnabled) MeritDataLockFix.Install();
            if (interpolateEnabled) InterpolateEventFix.Install();
            if (sheetMergeEnabled) SheetMerge.ArmAfterSpriteLoad(Note);
            Automation.AutomationController.OnExtenderReady();
        }

        internal static string FeatureSummary = "";

        // Test-mod markers stay visible without the workspace diagnostics switch; ms timestamps.
        internal static void Note(string message) =>
            Log?.LogMessage("[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + message);

        internal static void Fail(string message) =>
            Log?.LogError("[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + message);
    }
}
