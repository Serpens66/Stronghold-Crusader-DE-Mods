namespace BugfixesAndQoL
{
    internal static class AllyGoodsTransferPolicy
    {
        internal const int MinimumGoodsId = 1;
        internal const int MaximumGoodsId = 24;

        internal static bool ShouldForceConfirmVisible(
            bool clientFeaturesEnabled,
            bool amountModifiersEnabled,
            bool sendGoodsViewVisible,
            int selectedGoods,
            int selectedGoodsAmount) =>
            clientFeaturesEnabled &&
            amountModifiersEnabled &&
            sendGoodsViewVisible &&
            selectedGoods >= MinimumGoodsId &&
            selectedGoods <= MaximumGoodsId &&
            selectedGoodsAmount > 0;

        internal static bool IsSendRejected(int gameActionResult) => gameActionResult > 0;

        internal static bool IsSendSuccessful(int gameActionResult) => gameActionResult == 0;
    }
}
