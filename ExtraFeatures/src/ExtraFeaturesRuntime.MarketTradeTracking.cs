// Feature: Exclude ordinary market purchases from Extra Features goods-gain multipliers.
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using System;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private const int SingleMarketTradeMode = 2;

        private void OnPlayerMarketInteraction(PlayerMarketInteractionEventArgs args)
        {
            // Bugfixes and QoL owns mode 2 and wraps it through MarketTradeGuardBridge.
            if (args.ShiftModifier == SingleMarketTradeMode || args.Selling)
                return;

            string key = BuildResourceEventKey(args.PlayerId, args.Good);
            if (args.Phase == EventHookPhase.Pre)
            {
                PruneExpiredResourceGuards();
                marketBuyResourceGuards[key] = new ResourceEventCountGuard
                {
                    RemainingAmount = args.ShiftModifier != 0 ? MarketBuyShiftAmount : MarketBuyAmount,
                    ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
                };
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                marketBuyResourceGuards.Remove(key);
            }
        }

        internal void BeginSingleMarketBuyGuard(int playerId, eGoods good)
        {
            string key = BuildResourceEventKey(playerId, good);
            PruneExpiredResourceGuards();
            marketBuyResourceGuards[key] = new ResourceEventCountGuard
            {
                RemainingAmount = 1,
                ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
            };
        }

        internal void EndSingleMarketBuyGuard(int playerId, eGoods good) =>
            marketBuyResourceGuards.Remove(BuildResourceEventKey(playerId, good));
    }
}
