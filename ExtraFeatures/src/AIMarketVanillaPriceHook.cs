// Feature: Consumer-specific AI policy on APIShared's permanent native market query events.
using APIShared.Economy;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;

namespace ExtraFeatures
{
    internal sealed class AIMarketVanillaPriceHook : IDisposable
    {
        private volatile bool useVanillaAIPricesForSession;
        private volatile bool disposed;
        public AIMarketVanillaPriceHook(ManualLogSource log)
        {
            if (!MarketPriceEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid, "ai-default-market-prices", OnPrice, null, out string reason))
                throw new InvalidOperationException(reason);
            log?.LogInfo("Extra Features AI market policy registered with APIShared; native helpers have one process owner.");
        }
        public void Dispose() { disposed = true; }
        internal void SetSessionOverride(bool enabled) { useVanillaAIPricesForSession = enabled; }
        private void OnPrice(MarketPricePreEventArgs args)
        {
            if (disposed || args.SkipOriginalFunction || !useVanillaAIPricesForSession || !args.HasNativeManager ||
                args.PlayerId < 1 || args.PlayerId > 8 || args.Good < 0 || args.Good >= (int)eGoods.Count ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId)) return;
            PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)args.Good);
            args.ReplacementTotal = MarketPriceEvents.CalculateTradeTotal(
                args.Direction == MarketPriceDirection.Buy ? price.BuyPrice : price.SellPrice, args.Amount);
            args.SkipOriginalFunction = true;
        }
    }
}