using BepInEx.Bootstrap;
using SHCDESE.API.LowLevel;
using System;

namespace AIMarketOverbuyDiagnostic
{
    public sealed partial class AIMarketOverbuyDiagnosticPlugin
    {
        private static AIMarketOverbuyDiagnosticRuntime runtime;
        private static IDisposable sessionSubscription;

        static partial void PrepareDiagnostics()
        {
            Shared.DevDiag.Attach(log);
            MarketBacklogAic.Reporter = line => Shared.DebugLogHelper.LogInfo(log, line);
            try { MarketAutomation.PrepareTestStartup(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_MARKET_DIAGNOSTIC_PREPARE_FAILED: " + ex); }
        }

        static partial void InitializeDiagnostics(CrusaderLibraryLoadContext context)
        {
            IDisposable candidateSubscription = null;
            try
            {
                var candidate = new AIMarketOverbuyDiagnosticRuntime(log, Chainloader.PluginInfos.ContainsKey("fixes"));
                candidate.InstallTrace(context);
                candidateSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                if (!APIShared.Simulation.SimulationTickEvents.TryRegister(Guid, "market-overview", OnTick, out string reason))
                    throw new InvalidOperationException(reason);
                sessionSubscription = candidateSubscription;
                runtime = candidate;
                candidate.StartWriter();
                Shared.DebugLogHelper.LogInfo(log, "AI_MARKET_DIAGNOSTIC_READY: optional overview/trace writer enabled.");
            }
            catch (Exception ex)
            {
                if (runtime == null) candidateSubscription?.Dispose();
                Shared.DebugLogHelper.LogError(log, "AI_MARKET_DIAGNOSTIC_INIT_FAILED: " + ex);
                return;
            }
            try { MarketAutomation.TryStart(log); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_MARKET_AUTOMATION_INIT_FAILED: " + ex); }
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
        internal static long StopForAutomation()
        {
            runtime?.OnSessionEnded();
            MarketPendingDemandFix.StopDiagnosticCapture();
            return System.Threading.Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.WriterPasses);
        }
        internal static void CheckTraceHealthForAutomation(bool requireCalls) => runtime?.CheckTraceHealthForAutomation(requireCalls);
        internal static bool TraceOutputDrainedForAutomation => runtime != null && runtime.TraceOutputDrainedForAutomation;
    }
}
