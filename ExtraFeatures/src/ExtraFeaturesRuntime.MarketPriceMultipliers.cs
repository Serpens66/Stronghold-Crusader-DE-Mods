// Feature: Multiply global market buy and sell prices.
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private void ApplyMarketPriceMultipliers()
        {
            if (!settings.EnableMod)
                return;

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            for (int i = 0; i < GoodsCount; i++)
            {
                eGoods good = (eGoods)i;
                PackedGoodPrice vanillaPrice = playerApi.GetDefaultTradeBasePrice(good);
                double buyMultiplier = MarketGoodPriceDefinition.CombineMultipliers(
                    settings.MarketBuyPriceMultiplier,
                    settings.GetMarketGoodPriceMultiplier(good, true));
                double sellMultiplier = MarketGoodPriceDefinition.CombineMultipliers(
                    settings.MarketSellPriceMultiplier,
                    settings.GetMarketGoodPriceMultiplier(good, false));
                playerApi.SetTradeBasePrice(
                    good,
                    new PackedGoodPrice(
                        MultiplyPrice(vanillaPrice.BuyPrice, buyMultiplier),
                        MultiplyPrice(vanillaPrice.SellPrice, sellMultiplier)));
            }
        }

        private void RestoreTradeBasePrices()
        {
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            for (int i = 0; i < GoodsCount; i++)
            {
                eGoods good = (eGoods)i;
                playerApi.SetTradeBasePrice(good, playerApi.GetDefaultTradeBasePrice(good));
            }
        }

        private static int MultiplyPrice(int price, double multiplier)
        {
            if (price == 0 || Math.Abs(multiplier - 1.0) < 0.0001)
                return price;
            return (int)Math.Round(price * multiplier, MidpointRounding.AwayFromZero);
        }
    }
}
