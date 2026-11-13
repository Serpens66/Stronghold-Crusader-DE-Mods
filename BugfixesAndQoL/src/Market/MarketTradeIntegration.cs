// Optional integration used by Extra Features to exclude market purchases from goods multipliers.
using System;

namespace BugfixesAndQoL
{
    public static class MarketTradeIntegration
    {
        private static Action<int, int> beginSingleBuy;
        private static Action<int, int> endSingleBuy;

        public static bool HasSingleBuyGuards => beginSingleBuy != null && endSingleBuy != null;

        public static void RegisterSingleBuyGuards(
            Action<int, int> begin,
            Action<int, int> end)
        {
            beginSingleBuy = begin ?? throw new ArgumentNullException(nameof(begin));
            endSingleBuy = end ?? throw new ArgumentNullException(nameof(end));
        }

        public static void UnregisterSingleBuyGuards(Action<int, int> begin)
        {
            // Only the current owner may remove the process-wide callbacks.
            if (beginSingleBuy != begin)
                return;

            beginSingleBuy = null;
            endSingleBuy = null;
        }

        internal static void BeginSingleBuy(int playerId, int good) =>
            beginSingleBuy?.Invoke(playerId, good);

        internal static void EndSingleBuy(int playerId, int good) =>
            endSingleBuy?.Invoke(playerId, good);
    }
}
