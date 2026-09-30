using APIShared;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace AIBuildDiagnoseTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.6")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AIBuildDiagnosePlugin : BaseUnityPlugin
    {
        public const string Guid = "AIBuildDiagnoseTest_Serp";
        public const string Name = "AI Build Diagnose Test";
        public const string Version = "0.1.0";
        private static ManualLogSource log;
        private static AIBuildDiagnoseRuntime runtime;
        private static IDisposable sessionSubscription;
        private static bool registered;
        private static bool placementProbeEnabled;
        private static bool nearbyWoodTestEnabled;

        private void Awake()
        {
            log = Logger;
            placementProbeEnabled = Config.Bind("PlacementProbe", "Enabled", false,
                "Explicitly run the one-time placement probe on test_canari_nowoodcutters_probe.sav. " +
                "Keep false for wall-isolation runs.").Value;
            nearbyWoodTestEnabled = Config.Bind("NearbyWoodTest", "Enabled", false,
                "Temporarily exclude zero-component or parcel-blocked wood build candidates " +
                "for up to twelve calls only on the named save copy, after matching read-only calibration.").Value;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version +
                $" loaded; placementProbeEnabled={placementProbeEnabled}, nearbyWoodTestEnabled={nearbyWoodTestEnabled}; " +
                "probe limited to test_canari_nowoodcutters_probe.sav.");
            if (registered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                var candidate = new AIBuildDiagnoseRuntime(log,
                    Chainloader.PluginInfos.ContainsKey("fixes"), placementProbeEnabled,
                    nearbyWoodTestEnabled, unchecked((ulong)context.ModuleHandle.ToInt64()));
                // Publisher subscriptions and static fields survive SHCDE startup cleanup.
                IDisposable candidateSession = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                if (!AiBuildDiagnostic.TryRegister(Guid, candidate.OnNativeRecord, out string error))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_NATIVE_OBSERVATION_INCOMPLETE: " + error);
                if (!AiBuildDiagnostic.TryRegisterNearbyWoodOverlay(Guid,
                    candidate.BeginNearbyWoodOverlay, out string overlayError))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_NEARBY_TEST_UNAVAILABLE: " + overlayError);
                sessionSubscription = candidateSession;
                runtime = candidate;
                Shared.DebugLogHelper.LogInfo(log,
                    "AI_BUILD_DIAGNOSTIC_READY: publisher=GameTimeManagerAPI.OnTick; " +
                    "observer=" + AiBuildDiagnostic.HasObserver +
                    ", schedulerHook=" + AiBuildDiagnostic.SchedulerReady +
                    ", routeHook=" + AiBuildDiagnostic.RouteReady + ".");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI build diagnostic initialization failed: " + ex);
            }
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
    }
}
