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

            try
            {
                long key = ResourceEventKey(args.PlayerId, args.Good);
                lock (resourceGuardSync)
                {
                    if (args.Phase == EventHookPhase.Pre)
                    {
                        PruneExpiredResourceGuards();
                        marketBuyResourceGuards[key] = new ResourceEventCountGuard
                        {
                            RemainingAmount = args.ShiftModifier != 0 ? MarketBuyShiftAmount : MarketBuyAmount,
                            CreatedTick = CurrentGameTick()
                        };
                    }
                    else if (args.Phase == EventHookPhase.Post)
                    {
                        marketBuyResourceGuards.Remove(key);
                    }
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/MarketTradeTracking/market-buy-guard",
                    $"Extra Features market-purchase guard failed; the purchase may count as gained goods: {ex}");
            }
        }

        internal void BeginSingleMarketBuyGuard(int playerId, eGoods good)
        {
            long key = ResourceEventKey(playerId, good);
            lock (resourceGuardSync)
            {
                PruneExpiredResourceGuards();
                marketBuyResourceGuards[key] = new ResourceEventCountGuard
                {
                    RemainingAmount = 1,
                    CreatedTick = CurrentGameTick()
                };
            }
        }

        internal void EndSingleMarketBuyGuard(int playerId, eGoods good)
        {
            lock (resourceGuardSync)
                marketBuyResourceGuards.Remove(ResourceEventKey(playerId, good));
        }
    }
}
