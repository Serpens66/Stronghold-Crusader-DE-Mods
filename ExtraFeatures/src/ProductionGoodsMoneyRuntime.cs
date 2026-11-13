using APIShared.Economy;
using SHCDESE.Interop;
using System;
#if !API_SHARED_PRESET_TESTS
using APIShared.GameModes;
using APIShared.Lobby;
using SHCDESE.API;
#endif

namespace ExtraFeatures
{
    internal interface IProductionGoodsMoneyWorld
    {
        bool IsValidPlayer(int playerId);
        bool IsAIPlayer(int playerId);
        int SellPrice(eGoods good);
        void AddGold(int playerId, int amount);
    }

    internal sealed class ProductionGoodsMoneyWorld : IProductionGoodsMoneyWorld
    {
#if API_SHARED_PRESET_TESTS
        public bool IsValidPlayer(int playerId) => false;
        public bool IsAIPlayer(int playerId) => false;
        public int SellPrice(eGoods good) => 0;
        public void AddGold(int playerId, int amount) { }
#else
        public bool IsValidPlayer(int playerId) => GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId);
        public bool IsAIPlayer(int playerId) => GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
        public int SellPrice(eGoods good) => GamePlayerManagerAPI.Instance.GetTradeBasePrice(good).SellPrice;
        public void AddGold(int playerId, int amount) => GamePlayerManagerAPI.Instance.AddPlayerGold(playerId, amount);
#endif
    }

    internal sealed partial class WorkerProductionBonusRuntime
    {
        private readonly IProductionGoodsMoneyWorld moneyWorld;
        private readonly object moneySync = new object();
        private ProductionGoodsReceivedRegistration moneyRegistration;
        private bool moneyStarted, moneyAllowed, moneyAdmittedEnabled;
        private long moneySession;
        private double moneyHuman, moneyAI;

        private bool HasMoneySetting => settings.EnableMod &&
            (settings.MultiplyGoodsGainInMoneyHuman > 0 || settings.MultiplyGoodsGainInMoneyAI > 0);

        private void RegisterMoney()
        {
            if (!ProductionGoodsReceived.TryRegister(ExtraFeaturesPlugin.PluginGuid, "production-money",
                ReceiveProductionGoods, out moneyRegistration, out string diagnostic,
                onMissionAdmitted: MoneyAdmitted, onMissionEnded: MoneyEnded))
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures.ProductionMoney.Register", diagnostic);
            RefreshMoneyActivation(false);
#if !API_SHARED_PRESET_TESTS
            if (!LobbyLaunchGuards.TryRegister(ExtraFeaturesPlugin.PluginGuid, "production-money", context => {
                string issue = MoneyLaunchIssue();
                if (issue.Length != 0 && !context.RequiresPeerAgreement)
                    showLocalDialog(SerpLocalization.Get("ExtraFeatures.ProductionBonus.DetailsTitle"), issue);
                return issue;
            }, out diagnostic))
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures.ProductionMoney.LaunchGuard", diagnostic);
            if (!LobbyLaunchStatus.TryRegisterProvider(ExtraFeaturesPlugin.PluginGuid, "production-money", _ => {
                string issue = MoneyLaunchIssue();
                return issue.Length == 0 ? Array.Empty<LobbyLaunchStatusItem>() : new[] {
                    new LobbyLaunchStatusItem(issue, LobbyLaunchStatusKind.Blocking, ProductionGoodsReceived.Diagnostic,
                        LobbyLaunchStatusScope.Lobby) };
            }, out diagnostic))
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures.ProductionMoney.LaunchStatus", diagnostic);
#endif
        }

        // A production receipt is the authority for source and actual accepted quantity.
        // No storage re-add, market exclusion or inference from the current worker is needed.
        private void ReceiveProductionGoods(ProductionGoodsReceipt receipt)
        {
            lock (moneySync)
            {
                if (!moneyStarted || !moneyAllowed || !moneyAdmittedEnabled || !settings.EnableMod || !ProductionGoodsReceived.IsAvailable ||
                    receipt.Amount <= 0 || !moneyWorld.IsValidPlayer(receipt.PlayerId)) return;
                double factor = moneyWorld.IsAIPlayer(receipt.PlayerId) ? moneyAI : moneyHuman;
                if (factor <= 0) return;
                try
                {
                    // Preserve the former per-receipt rounding and five-item base-price unit.
                    int amount = (int)Math.Round(receipt.Amount * (moneyWorld.SellPrice(receipt.Good) / 5.0) * factor,
                        MidpointRounding.AwayFromZero);
                    if (amount != 0) moneyWorld.AddGold(receipt.PlayerId, amount);
                }
                catch (Exception error)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures.ProductionMoney.Receipt",
                        "Production money reward failed: " + error);
                }
            }
        }

        // APIShared calls this under the engine gate before releasing the admitted native
        // world. Managed Started is later and cannot determine the first rewarded delivery.
        private void MoneyAdmitted(long sessionId, APIShared.GameModes.GameModeSnapshot mode)
        {
            lock (moneySync)
            {
                if (moneyStarted && moneySession == sessionId) return;
                moneySession = sessionId;
                moneyHuman = settings.MultiplyGoodsGainInMoneyHuman;
                moneyAI = settings.MultiplyGoodsGainInMoneyAI;
                moneyAdmittedEnabled = settings.EnableMod;
                moneyAllowed = APIShared.GameModes.GameplayModModePolicy.IsAllowed(
                    Shared.SerpsModProfiles.GetProfile(ExtraFeaturesPlugin.PluginGuid, ExtraFeaturesPlugin.PluginName), mode, out _);
                moneyStarted = true;
                RefreshMoneyActivation(false);
            }
        }

        private void MoneyEnded(long sessionId)
        {
            lock (moneySync)
            {
                if (moneySession != sessionId) return;
                moneyStarted = moneyAllowed = moneyAdmittedEnabled = false;
                moneySession = 0;
                moneyHuman = moneyAI = 0;
                moneyRegistration?.SetEnabled(false);
            }
        }

        private void RefreshMoneyActivation(bool ignored)
        {
            moneyRegistration?.SetRequiredForMission(HasMoneySetting,
                Shared.SerpsModProfiles.GetProfile(ExtraFeaturesPlugin.PluginGuid, ExtraFeaturesPlugin.PluginName));
            lock (moneySync)
                moneyRegistration?.SetEnabled(moneyStarted && moneyAllowed && moneyAdmittedEnabled && settings.EnableMod &&
                    ProductionGoodsReceived.IsAvailable && (moneyHuman > 0 || moneyAI > 0));
#if !API_SHARED_PRESET_TESTS
            LobbyLaunchStatus.RequestRefresh();
#endif
        }

        internal string MoneyAvailabilityIssue() => HasMoneySetting &&
            (moneyRegistration == null || !ProductionGoodsReceived.IsAvailable)
            ? SerpLocalization.Get("ExtraFeatures.ProductionGoods.Unavailable") : string.Empty;

#if !API_SHARED_PRESET_TESTS
        private string MoneyLaunchIssue()
        {
            string issue = MoneyAvailabilityIssue();
            if (issue.Length == 0) return issue;
            var mode = GameModeHelper.Capture();
            // Unknown lobby context cannot prove that this gameplay feature is excluded.
            if (mode.Kind != GameModeKind.Unknown && !GameplayModModePolicy.IsAllowed(
                Shared.SerpsModProfiles.GetProfile(ExtraFeaturesPlugin.PluginGuid, ExtraFeaturesPlugin.PluginName), mode, out _))
                return string.Empty;
            return issue;
        }
#endif
    }
}
