// Feature: Ctrl-click market trading of exactly one unit.
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;
using System;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime
    {
        private void OnPlayerMarketInteraction(PlayerMarketInteractionEventArgs args)
        {
            if (args.ShiftModifier != CtrlMarketTradeHook.SingleTradeMode)
                return;

            // Mode 2 belongs to this feature and must never reach unmodified Vanilla as Shift.
            args.SkipOriginalFunction = true;
            if (args.Phase != EventHookPhase.Pre || ctrlMarketTradeHook == null)
                return;

            bool guardStarted = false;
            if (!args.Selling)
            {
                try
                {
                    MarketTradeIntegration.BeginSingleBuy(args.PlayerId, (int)args.Good);
                    guardStarted = true;
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL could not start the optional Extra Features market-purchase guard; the single-unit trade continues: {ex}");
                }
            }

            try
            {
                ctrlMarketTradeHook.ExecuteSingleMarketTrade(args);
            }
            finally
            {
                if (guardStarted)
                {
                    try
                    {
                        MarketTradeIntegration.EndSingleBuy(args.PlayerId, (int)args.Good);
                    }
                    catch (Exception ex)
                    {
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Bugfixes and QoL could not finish the optional Extra Features market-purchase guard: {ex}");
                    }
                }
            }
        }
    }
}
