// Feature: Multiply global market buy and sell prices.
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private readonly MarketPriceOverrides marketPriceOverrides = new MarketPriceOverrides(
            good => GamePlayerManagerAPI.Instance.GetTradeBasePrice(good),
            (good, value) => GamePlayerManagerAPI.Instance.SetTradeBasePrice(good, value),
            good => GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice(good));
        private readonly System.Collections.Generic.HashSet<string> marketPriceConflicts =
            new System.Collections.Generic.HashSet<string>();

        private void ApplyMarketPriceMultipliers()
        {
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;
            if (APIShared.GameModes.GameModeHelper.IsMapEditor())
                return; // Trading has no editor behavior; do not acquire price cells there.

            for (int i = 0; i < GoodsCount; i++)
            {
                eGoods good = (eGoods)i;
                double buyMultiplier = MarketGoodPriceDefinition.CombineMultipliers(
                    settings.MarketBuyPriceMultiplier,
                    settings.GetMarketGoodPriceMultiplier(good, true));
                double sellMultiplier = MarketGoodPriceDefinition.CombineMultipliers(
                    settings.MarketSellPriceMultiplier,
                    settings.GetMarketGoodPriceMultiplier(good, false));
                ApplyMarketPriceCell(good, true, buyMultiplier);
                ApplyMarketPriceCell(good, false, sellMultiplier);
            }
        }

        private void ApplyMarketPriceCell(eGoods good, bool buying, double multiplier)
        {
            string cell = good + (buying ? "/buy" : "/sell");
            try
            {
                if (marketPriceOverrides.Apply(good, buying, multiplier, out MarketPriceApplication result))
                    return;
                if (marketPriceConflicts.Add(cell))
                    Shared.DebugLogHelper.LogWarning(log,
                        $"MARKET_PRICE_CONFLICT: good={good}, direction={(buying ? "buy" : "sell")}, " +
                        $"baseline={result.Baseline}, current={result.Current}, desired={result.Desired}, " +
                        $"multiplier={multiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}, " +
                        $"previousValueOwned={result.Owned}, mapActive={mapActive}; action=preserve-current; " +
                        "writer=not-observed; independent price cells continue.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures.market-price-cell." + cell,
                    $"ExtraFeatures could not reconcile market price {cell}; independent cells continue: {ex}");
            }
        }

        private void RestoreTradeBasePrices(bool endSession = false)
        {
            marketPriceOverrides.RestoreAll(ex => Shared.DebugLogHelper.LogErrorLimited(log,
                "ExtraFeatures.market-price-restoration",
                $"ExtraFeatures could not restore an owned market price; independent cells continue: {ex}"), endSession);
            if (endSession) marketPriceConflicts.Clear();
        }
    }
}
