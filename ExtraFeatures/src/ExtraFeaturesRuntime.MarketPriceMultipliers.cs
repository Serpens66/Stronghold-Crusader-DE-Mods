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
                playerApi.SetTradeBasePrice(
                    good,
                    new PackedGoodPrice(
                        MultiplyPrice(vanillaPrice.BuyPrice, settings.MarketBuyPriceMultiplier),
                        MultiplyPrice(vanillaPrice.SellPrice, settings.MarketSellPriceMultiplier)));
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
