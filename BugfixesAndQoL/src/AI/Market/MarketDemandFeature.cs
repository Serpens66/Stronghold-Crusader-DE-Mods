using System;
using System.Threading;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.AI.Market
{
    // The existing persistent bootstrap owns initialization and settings delivery.
    // Hooks and public registrations remain rooted; settings change only data gates.
    internal static class MarketDemandFeature
    {
        private static int initialized;

        internal static void Initialize(CrusaderLibraryLoadContext context, ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;
            ApplySettings(settings);
            MarketPendingDemandFix.Install(context, log);
            // Register the optional capability even if local native installation
            // failed, so peers can agree to leave this feature inactive together.
            MarketPendingDemandFix.InitializeConsumers(log);
        }

        internal static void ApplySettings(BugfixesAndQoLViewModel settings) =>
            MarketPendingDemandFix.SetRequested(settings != null && settings.EnableMod && settings.EnableAiMarketOverbuyFix);
    }
}
