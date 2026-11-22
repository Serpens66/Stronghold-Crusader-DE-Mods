using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace AIMarketOverbuyDiagnostic
{
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.56.1")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AIMarketOverbuyDiagnosticPlugin : BaseUnityPlugin
    {
        public const string Guid = "AIMarketOverbuyDiagnostic_Serp";
        public const string Name = "AI Market Overbuy Diagnostic";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static AIMarketOverbuyDiagnosticRuntime runtime;
        private static IDisposable sessionSubscription;
        private static bool libraryRegistered;

        private void Awake()
        {
            log = Logger;
            Shared.DevDiag.Attach(log);
#if SERP_DEVDIAG
            MarketAutomation.PrepareTestStartup();
#endif
            Shared.DebugLogHelper.LogInfo(log, $"{Name} {Version} loaded; waiting for native library.");
            if (libraryRegistered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            libraryRegistered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true))
                return;

            IDisposable candidateSubscription = null;
            bool candidateTickRegistered = false;
            try
            {
                var candidate = new AIMarketOverbuyDiagnosticRuntime(log,
                    Chainloader.PluginInfos.ContainsKey("fixes"));
#if SERP_DEVDIAG
                Shared.NativePatchGuard.Bind(context, log);
                candidate.InstallTrace(context);
#endif
                candidateSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                candidateTickRegistered = true;

                // The extender's publisher and these static fields outlive startup cleanup.
                Shared.DebugLogHelper.LogInfo(log,
                    "AI_MARKET_DIAGNOSTIC_READY: readOnly=true, publisher=GameTimeManagerAPI.OnTick.");
                candidate.StartWriter();
                sessionSubscription = candidateSubscription;
                runtime = candidate;
            }
            catch (Exception ex)
            {
                if (candidateTickRegistered)
                    GameTimeManagerAPI.Instance.OnTick -= OnTick;
                candidateSubscription?.Dispose(); // Only an unpublished initialization candidate.
                Shared.DebugLogHelper.LogError(log, "AI market diagnostic initialization failed: " + ex);
                return;
            }
#if SERP_DEVDIAG
            // Automation is an independent consumer of the committed runtime. Its
            // failures must never undo published event registrations or native hooks.
            try { MarketAutomation.TryStart(log); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_MARKET_AUTOMATION_INIT_FAILED: " + ex); }
#endif
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
#if SERP_DEVDIAG
        internal static long StopForAutomation()
        {
            runtime?.OnSessionEnded();
            return System.Threading.Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.WriterPasses);
        }
        internal static void CheckTraceHealthForAutomation(bool requireCalls) => runtime?.CheckTraceHealthForAutomation(requireCalls);
        internal static bool TraceOutputDrainedForAutomation => runtime != null && runtime.TraceOutputDrainedForAutomation;
#endif
    }
}
