// Feature: Session-local selection of ExtraFeatures' AI price factors; no total veto.
using APIShared.Economy;
using BepInEx.Logging;
using System;

namespace ExtraFeatures
{
    internal sealed class AIMarketVanillaPriceHook : IDisposable
    {
        private volatile bool useVanillaAIPricesForSession;
        internal bool UseVanillaAIPricesForSession => useVanillaAIPricesForSession;
        public AIMarketVanillaPriceHook(ManualLogSource log)
        {
            Shared.DebugLogHelper.LogInfo(log,
                "Extra Features AI market selection uses shared base-price contributions; foreign fixed and final factors remain effective.");
        }
        public void Dispose() => SetSessionOverride(false);
        internal void SetSessionOverride(bool enabled)
        {
            if (useVanillaAIPricesForSession == enabled) return;
            useVanillaAIPricesForSession = enabled;
            MarketPriceModifiers.Refresh();
        }
    }
}
