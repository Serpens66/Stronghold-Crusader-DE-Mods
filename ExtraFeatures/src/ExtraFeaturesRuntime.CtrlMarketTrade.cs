// Feature: Ctrl-click market trading of exactly one unit.
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;
using System;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private void OnPlayerMarketInteraction(PlayerMarketInteractionEventArgs args)
        {
            string key = BuildResourceEventKey(args.PlayerId, args.Good);
            if (args.ShiftModifier == CtrlMarketTradeHook.SingleTradeMode)
            {
                // Mode 2 belongs to this feature and must never reach unmodified Vanilla as Shift.
                args.SkipOriginalFunction = true;
                if (args.Phase != EventHookPhase.Pre)
                    return;

                if (!args.Selling)
                {
                    marketBuyResourceGuards[key] = new ResourceEventCountGuard
                    {
                        RemainingAmount = 1,
                        ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
                    };
                }

                try
                {
                    ctrlMarketTradeHook?.ExecuteSingleMarketTrade(args);
                }
                finally
                {
                    marketBuyResourceGuards.Remove(key);
                }
                return;
            }

            if (args.Selling)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                PruneExpiredResourceGuards();
                marketBuyResourceGuards[key] = new ResourceEventCountGuard
                {
                    RemainingAmount = GetMarketInteractionAmount(args),
                    ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
                };
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                marketBuyResourceGuards.Remove(key);
            }
        }

        private static int GetMarketInteractionAmount(PlayerMarketInteractionEventArgs args)
        {
            if (args.ShiftModifier == CtrlMarketTradeHook.SingleTradeMode)
                return 1;
            return args.ShiftModifier != 0 ? MarketBuyShiftAmount : MarketBuyAmount;
        }
    }
}
