// Feature: Pure routing and arithmetic shared by the AI Vanilla market-price hooks and tests.
namespace ExtraFeatures
{
    internal static class AIMarketVanillaPricePolicy
    {
        internal const string BuyPriceFunctionPattern =
            "49 63 C0 8B 8C C1 B8 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3";
        internal const string SellPriceFunctionPattern =
            "49 63 C0 8B 8C C1 BC 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3";
        internal const int BuyPriceFunctionRva = 0xCEB10;
        internal const int SellPriceFunctionRva = 0xCEB90;

        internal static bool ShouldUseVanillaPrice(
            bool modEnabled,
            bool marketPricesAlsoForAI,
            bool validPlayer,
            bool validGood,
            bool isAIPlayer)
        {
            return modEnabled &&
                !marketPricesAlsoForAI &&
                validPlayer &&
                validGood &&
                isAIPlayer;
        }

        internal static int CalculateTradeTotal(int basePrice, int amount)
        {
            // Vanilla performs signed division before the signed multiply.
            return unchecked((basePrice / 5) * amount);
        }
    }
}
