using System;
using SHCDESE.API;
using SHCDESE.Interop;
namespace ExtraFeatures {
public class MarketHarness { private volatile bool useVanillaAIPricesForSession;
internal void SetSessionOverride(bool enabled)
        {
            useVanillaAIPricesForSession = enabled;
        }
public bool ShouldUseVanillaPrice(IntPtr playerManager, int playerId, int good)
        {
            if (!useVanillaAIPricesForSession || playerManager == IntPtr.Zero ||
                playerId < 1 || playerId > 8 || good < 0 || good >= (int)eGoods.Count)
                return false;

            // Settings are fixed for the session; player classification is live game state.
            return GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
        }
}
public class SessionHarness { public MarketHarness aiMarketVanillaPriceHook=new MarketHarness(); public bool mapActive; public ExtraFeaturesViewModel settings=new ExtraFeaturesViewModel();
public void CaptureAIMarketSessionSettings()
        {
            aiMarketVanillaPriceHook?.SetSessionOverride(mapActive &&
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                !settings.MarketPricesAlsoForAI);
        }
}

}
