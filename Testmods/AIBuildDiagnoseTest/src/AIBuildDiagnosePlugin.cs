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
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.181")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AIBuildDiagnosePlugin : BaseUnityPlugin
    {
        public const string Guid = "AIBuildDiagnoseTest_Serp";
        public const string Name = "AI Build Diagnose Test";
        public const string Version = "0.1.1";
        private static ManualLogSource log;
        private static AIBuildDiagnoseRuntime runtime;
        private static IDisposable sessionSubscription;
        private static bool registered;
        private static bool placementProbeEnabled;
        private static bool nearbyWoodTestEnabled;
        private static bool farmContractProbeEnabled;
        private static bool canariFarmSwapEnabled;
        private static FixesFarmFilterOverride fixesFarmFilterOverride;

        private void Awake()
        {
            log = Logger;
            placementProbeEnabled = Config.Bind("PlacementProbe", "Enabled", false,
                "Explicitly run the one-time placement probe on test_canari_nowoodcutters_probe.sav. " +
                "Keep false for wall-isolation runs.").Value;
            nearbyWoodTestEnabled = Config.Bind("NearbyWoodTest", "Enabled", false,
                "Temporarily exclude zero-component or parcel-blocked wood build candidates " +
                "for up to twelve calls only on the named save copy, after matching read-only calibration.").Value;
            farmContractProbeEnabled = Config.Bind("FarmContractProbe", "Enabled", false,
                "On rat_farm_site_contract_probe.sav only, place four farms and run controlled " +
                "wood-site probes. This changes the running copy; never save it.").Value;
            canariFarmSwapEnabled = Config.Bind("CanariFarmSwap", "Enabled", false,
                "Only on the three byte-verified test_canari_farm_swap_*.sav copies, " +
                "replace the orchard through Vanilla deletion and placement. Never save the result.").Value;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version +
                $" loaded; placementProbeEnabled={placementProbeEnabled}, nearbyWoodTestEnabled={nearbyWoodTestEnabled}, " +
                $"farmContractProbeEnabled={farmContractProbeEnabled}, " +
                $"canariFarmSwapEnabled={canariFarmSwapEnabled}; " +
                "probe limited to test_canari_nowoodcutters_probe.sav.");
            if (registered) return;
            try { fixesFarmFilterOverride = FixesFarmFilterOverride.Begin(log); }
            catch (Exception ex)
            {
                fixesFarmFilterOverride = null;
                Shared.DebugLogHelper.LogError(log,
                    "AI_BUILD_FIXES_FILTER: startup check failed; active probes disabled: " + ex);
            }
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            bool vanillaComparisonReady;
            try { vanillaComparisonReady = fixesFarmFilterOverride != null &&
                fixesFarmFilterOverride.Complete(context.ModuleHandle); }
            catch (Exception ex)
            {
                vanillaComparisonReady = false;
                Shared.DebugLogHelper.LogError(log,
                    "AI_BUILD_FIXES_FILTER: compatibility check failed: " + ex);
            }
            if (runtime != null) return;
            try
            {
                var candidate = new AIBuildDiagnoseRuntime(log,
                    Chainloader.PluginInfos.ContainsKey("fixes"),
                    vanillaComparisonReady && placementProbeEnabled,
                    vanillaComparisonReady && nearbyWoodTestEnabled,
                    vanillaComparisonReady && farmContractProbeEnabled,
                    vanillaComparisonReady && canariFarmSwapEnabled,
                    unchecked((ulong)context.ModuleHandle.ToInt64()));
                // Publisher subscriptions and static fields survive SHCDE startup cleanup.
                IDisposable candidateSession = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                string nativeHash;
                try { nativeHash = NativeDiagnosticContracts.ComputeInstalledHash(); }
                catch (Exception ex)
                {
                    nativeHash = string.Empty;
                    Shared.DebugLogHelper.LogError(log, "Could not hash the diagnostic native module: " + ex);
                }
                AiBuildDiagnostic.Initialize(context.ModuleHandle.ToInt64(), nativeHash, context.Region, log);
                if (!AiBuildDiagnostic.TryRegister(Guid, candidate.OnNativeRecord, out string error))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_NATIVE_OBSERVATION_INCOMPLETE: " + error);
                if (!AiBuildDiagnostic.TryRegisterWoodBuildGate(Guid,
                    candidate.ShouldDeferCanariWoodBuild, out string gateError))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_CANARI_WOOD_GATE_UNAVAILABLE: " + gateError);
                if (!AiBuildDiagnostic.TryRegisterNearbyWoodOverlay(Guid,
                    candidate.BeginNearbyWoodOverlay, out string overlayError))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_NEARBY_TEST_UNAVAILABLE: " + overlayError);
                if (AiBuildDiagnostic.HasObserver &&
                    !BugfixesAndQoL.Diagnostics.AiBuildObservation.TryRegister(
                        AiBuildObservationSink.Instance, log, out string sinkError))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_OBSERVATION_BRIDGE_UNAVAILABLE: " + sinkError);
                candidate.TryInstallGeneralSiteSearchHooks(context);
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
