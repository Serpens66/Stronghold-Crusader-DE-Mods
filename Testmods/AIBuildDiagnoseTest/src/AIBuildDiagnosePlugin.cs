using APIShared;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
namespace AIBuildDiagnoseTest
{
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.9.2")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.192")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SpectatorEditorBuildTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AIBuildDiagnosePlugin : BaseUnityPlugin
    {
        public const string Guid = "AIBuildDiagnoseTest_Serp";
        public const string Name = "AI Build Diagnose Test";
        public const string Version = "0.2.1";
        private static ManualLogSource log;
        private static AIBuildDiagnoseRuntime runtime;
        private static Action<AiBuildDiagnosticRecord> generalCapture;
        private static IDisposable sessionSubscription;
        private static bool registered;
        private void Awake()
        {
            log = Logger;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; general observation only; editor/replay excluded.");
            if (registered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }
        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                runtime = new AIBuildDiagnoseRuntime(log, Chainloader.PluginInfos.ContainsKey("fixes"),
                    unchecked((ulong)context.ModuleHandle.ToInt64()));
                generalCapture = runtime.OnNativeRecord;
                sessionSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(log, runtime.OnSessionStarted, runtime.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                string hash;
                try { hash = NativeDiagnosticContracts.ComputeInstalledHash(); }
                catch (Exception ex) { hash = string.Empty; Shared.DebugLogHelper.LogError(log, "Could not hash diagnostic native module: " + ex); }
                AiBuildDiagnostic.Initialize(context.ModuleHandle.ToInt64(), hash, context.Region, log);
                if (!AiBuildDiagnostic.TryRegister(Guid, DeliverRecord, out string error))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_NATIVE_OBSERVATION_INCOMPLETE: " + error);
                if (AiBuildDiagnostic.HasObserver && !BugfixesAndQoL.Diagnostics.AiBuildObservation.TryRegister(
                    AiBuildObservationSink.Instance, log, out string sinkError))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_OBSERVATION_BRIDGE_UNAVAILABLE: " + sinkError);
                runtime.TryInstallGeneralSiteSearchHooks(context);
                runtime.InitializeEconomy(context, hash);
                UnityEngine.Application.onBeforeRender += DrainEconomy;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                Shared.DebugLogHelper.LogInfo(log, "AI_BUILD_DIAGNOSTIC_READY: publisher=GameTimeManagerAPI.OnTick; " +
                    "observer=" + AiBuildDiagnostic.HasObserver + ", schedulerHook=" + AiBuildDiagnostic.SchedulerReady +
                    ", routeHook=" + AiBuildDiagnostic.RouteReady + "; observationDoesNotGuaranteeCompleteCauseDetection=true.");
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI build diagnostic initialization failed: " + ex); }
        }
        private static void DeliverRecord(AiBuildDiagnosticRecord record)
        {
            AiBuildExperimentBridge.Dispatch(record, generalCapture, log);
        }
        private static void OnTick(int tick) => runtime?.OnTick(tick);
        private static void DrainEconomy() => runtime?.DrainEconomy();
        private static void OnProcessExit(object sender, EventArgs args) => runtime?.DrainEconomyOnExit();
    }
}
