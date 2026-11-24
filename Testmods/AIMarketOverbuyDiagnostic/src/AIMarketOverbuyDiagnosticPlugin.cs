using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace AIMarketOverbuyDiagnostic
{
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.62.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed partial class AIMarketOverbuyDiagnosticPlugin : BaseUnityPlugin
    {
        public const string Guid = "AIMarketOverbuyDiagnostic_Serp";
        public const string Name = "AI Market Overbuy Diagnostic";
        public const string Version = "0.1.0";
        private static ManualLogSource log;
        private static bool libraryRegistered, initialized;

        private void Awake()
        {
            log = Logger;
            PrepareDiagnostics();
            Shared.DebugLogHelper.LogInfo(log, $"{Name} {Version} loaded; waiting for native library.");
            if (libraryRegistered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            libraryRegistered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (initialized) return;
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true)) return;
            initialized = true;
            try
            {
                MarketPendingDemandFix.Install(context, log);
                if (!MarketPendingDemandFix.Ready) throw new InvalidOperationException("Native demand hooks unavailable.");
                MarketPendingDemandFix.InitializeConsumers(log);
                Shared.DebugLogHelper.LogInfo(log, "AI_MARKET_FIX_READY: source-aware pending correction; publisher=APIShared.AiMarketEvents; editor=inactive.");
            }
            catch (Exception ex)
            {
                MarketPendingDemandFix.FailInitialization(ex);
                Shared.DebugLogHelper.LogError(log, "AI_MARKET_FIX_INIT_FAILED: " + ex);
                return;
            }
            InitializeDiagnostics(context);
        }

        static partial void PrepareDiagnostics();
        static partial void InitializeDiagnostics(CrusaderLibraryLoadContext context);
    }
}
