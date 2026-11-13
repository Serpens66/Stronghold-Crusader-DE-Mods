// Feature: Register ExtraFeatures factors with APIShared's one market-price owner.
using APIShared.Economy;
using APIShared.Modifiers;
using SHCDESE.Interop;
using System;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private MarketPriceModifierHandle marketPriceModifier;
        private sealed class MarketFactors
        {
            internal readonly decimal[] Buy, Sell;
            internal MarketFactors(decimal[] buy, decimal[] sell) { Buy = buy; Sell = sell; }
        }
        private MarketFactors marketFactors;

        private void ApplyMarketPriceMultipliers()
        {
            bool enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                !APIShared.GameModes.GameModeHelper.IsMapEditor();
            if (marketPriceModifier == null)
            {
                if (!enabled) return;
                if (!MarketPriceModifiers.TryRegister(ExtraFeaturesPlugin.PluginGuid, "market-price-factors",
                    GetMarketPriceContribution, out marketPriceModifier, out string reason))
                    throw new InvalidOperationException("Shared market-price modifiers unavailable: " + reason);
            }
            var buy = new decimal[GoodsCount];
            var sell = new decimal[GoodsCount];
            for (int i = 0; i < GoodsCount; i++)
            {
                buy[i] = (decimal)MarketGoodPriceDefinition.CombineMultipliers(settings.MarketBuyPriceMultiplier,
                    settings.GetMarketGoodPriceMultiplier((eGoods)i, true));
                sell[i] = (decimal)MarketGoodPriceDefinition.CombineMultipliers(settings.MarketSellPriceMultiplier,
                    settings.GetMarketGoodPriceMultiplier((eGoods)i, false));
            }
            // Publish complete immutable arrays; queries never read a partly updated settings table.
            System.Threading.Volatile.Write(ref marketFactors, new MarketFactors(buy, sell));
            marketPriceModifier.SetEnabled(enabled);
            MarketPriceModifiers.Refresh();
        }

        private ValueModifierContribution GetMarketPriceContribution(MarketPriceModifierContext context)
        {
            if (context.IsAIPlayer && aiMarketVanillaPriceHook != null && aiMarketVanillaPriceHook.UseVanillaAIPricesForSession)
                return ValueModifierContribution.None;
            MarketFactors factors = System.Threading.Volatile.Read(ref marketFactors);
            decimal[] values = factors == null ? null : context.Direction == MarketPriceDirection.Buy ? factors.Buy : factors.Sell;
            int good = (int)context.Good;
            if (values == null || good < 0 || good >= values.Length || values[good] == 1m)
                return ValueModifierContribution.None;
            return ValueModifierContribution.Factor(values[good], ValueModifierStage.BeforeFixedValue);
        }

        private void RestoreTradeBasePrices(bool endSession = false) => marketPriceModifier?.SetEnabled(false);
    }
}
