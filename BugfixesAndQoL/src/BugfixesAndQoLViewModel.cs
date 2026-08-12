// Feature: Lobby settings model for the Bugfixes and QoL features.
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BepInEx.Logging;
using Noesis;

namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        public event Action<string> SettingChanged;

        private bool enableMod = true;
        private bool rememberAiAivSettings = true;
        private bool enableTroopMovementFix = true;
        private bool enablePlaguePopularityFix = true;
        private bool enablePlagueCloudRemovalFix = true;
        private bool enableStuckApothecaryFix = true;
        private bool enablePlagueTargetReservationFix = true;
        private bool enableAssemblyPointPlacementFix = true;
        private readonly LocalPerPlayerSetting<bool> enableClientFeatures = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMinimapCursorFollowFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMarketKeyMainMenuFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableAutoTradeSellZeroFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableEnemyProximityBulldozeCursorFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> allowMinimapWhilePlacingBuilding = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> allowCameraMovementWithModifiers = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> hdMarketView = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<int[]> marketGoodsOrder =
            new LocalPerPlayerSetting<int[]>(
                MarketGoodsOrderDefinition.CreateHdOrder(),
                MarketGoodsOrderDefinition.CloneOrDefault);
        private readonly Dictionary<int, string> marketGoodNames = new Dictionary<int, string>();
        private readonly Dictionary<int, ImageSource> marketGoodIcons = new Dictionary<int, ImageSource>();
        private ManualLogSource marketGoodsLog;

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public BugfixesAndQoLViewModel(bool legacySomeSettingsLoaded)
        {
            LegacyModWarningVisibility = legacySomeSettingsLoaded ? Visibility.Visible : Visibility.Collapsed;
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
            RestoreHdMarketOrderCommand = new RelayCommand(RestoreHdMarketOrder);
        }

        public RelayCommand ResetToDefaultCommand { get; }
        public RelayCommand RestoreHdMarketOrderCommand { get; }
        public ObservableCollection<MarketGoodOrderItemViewModel> MarketGoodsOrderItems { get; } =
            new ObservableCollection<MarketGoodOrderItemViewModel>();
        public Visibility LegacyModWarningVisibility { get; }
        public string LegacyModWarningText => SerpLocalization.Get(SerpLocalization.LegacySomeSettingsWarning);
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string EnableClientFeaturesText => SerpLocalization.Get("BugfixesAndQoL.EnableClientFeatures");
        public string EnableClientFeaturesHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableClientFeaturesHelp");
        public string EnableHostFeaturesText => SerpLocalization.Get("BugfixesAndQoL.EnableHostFeatures");
        public string EnableHostFeaturesHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableHostFeaturesHelp");
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string ClientInterfaceTitleText => SerpLocalization.Get("BugfixesAndQoL.ClientInterfaceTitle");
        public string AiAivTitleText => SerpLocalization.Get("BugfixesAndQoL.AiAivTitle");
        public string TroopMovementTitleText => SerpLocalization.Get("BugfixesAndQoL.TroopMovementTitle");
        public string PlagueTitleText => SerpLocalization.Get("BugfixesAndQoL.PlagueTitle");
        public string GameplayTitleText => SerpLocalization.Get("BugfixesAndQoL.GameplayTitle");
        public string EnableMinimapCursorFollowFixText => SerpLocalization.Get("BugfixesAndQoL.EnableMinimapCursorFollowFix");
        public string EnableMinimapCursorFollowFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableMinimapCursorFollowFixHelp");
        public string EnableMarketKeyMainMenuFixText => SerpLocalization.Get("BugfixesAndQoL.EnableMarketKeyMainMenuFix");
        public string EnableMarketKeyMainMenuFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableMarketKeyMainMenuFixHelp");
        public string EnableAutoTradeSellZeroFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAutoTradeSellZeroFix");
        public string EnableAutoTradeSellZeroFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAutoTradeSellZeroFixHelp");
        public string EnableEnemyProximityBulldozeCursorFixText => SerpLocalization.Get("BugfixesAndQoL.EnableEnemyProximityBulldozeCursorFix");
        public string EnableEnemyProximityBulldozeCursorFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableEnemyProximityBulldozeCursorFixHelp");
        public string EnableAssemblyPointPlacementFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAssemblyPointPlacementFix");
        public string EnableAssemblyPointPlacementFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAssemblyPointPlacementFixHelp");
        public string AllowMinimapWhilePlacingBuildingText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuilding);
        public string AllowMinimapWhilePlacingBuildingHelpText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuildingHelp);
        public string RememberAiAivSettingsText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettings);
        public string RememberAiAivSettingsHelpText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettingsHelp);
        public string EnableTroopMovementFixText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFix);
        public string EnableTroopMovementFixHelpText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFixHelp);
        public string EnablePlaguePopularityFixText => SerpLocalization.Get(SerpLocalization.EnablePlaguePopularityFix);
        public string EnablePlaguePopularityFixHelpText => SerpLocalization.Get(SerpLocalization.EnablePlaguePopularityFixHelp);
        public string EnablePlagueCloudRemovalFixText => SerpLocalization.Get(SerpLocalization.EnablePlagueCloudRemovalFix);
        public string EnablePlagueCloudRemovalFixHelpText => SerpLocalization.Get(SerpLocalization.EnablePlagueCloudRemovalFixHelp);
        public string EnableStuckApothecaryFixText => SerpLocalization.Get(SerpLocalization.EnableStuckApothecaryFix);
        public string EnableStuckApothecaryFixHelpText => SerpLocalization.Get(SerpLocalization.EnableStuckApothecaryFixHelp);
        public string EnablePlagueTargetReservationFixText => SerpLocalization.Get(SerpLocalization.EnablePlagueTargetReservationFix);
        public string EnablePlagueTargetReservationFixHelpText => SerpLocalization.Get(SerpLocalization.EnablePlagueTargetReservationFixHelp);
        public string AllowCameraMovementWithModifiersText => SerpLocalization.Get(SerpLocalization.AllowCameraMovementWithModifiers);
        public string AllowCameraMovementWithModifiersHelpText => SerpLocalization.Get(SerpLocalization.AllowCameraMovementWithModifiersHelp);
        public string HdMarketViewText => SerpLocalization.Get(SerpLocalization.HdMarketView);
        public string HdMarketViewHelpText => SerpLocalization.Get(SerpLocalization.HdMarketViewHelp);
        public string MarketGoodsOrderTitleText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderTitle);
        public string MarketGoodsOrderHelpText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderHelp);
        public string RestoreHdMarketOrderText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderRestoreHd);
        public string RestoreHdMarketOrderHelpText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderRestoreHdHelp);

        public bool[] EnableMinimapCursorFollowFixData => enableMinimapCursorFollowFix.Data;
        public bool[] EnableMarketKeyMainMenuFixData => enableMarketKeyMainMenuFix.Data;
        public bool[] EnableAutoTradeSellZeroFixData => enableAutoTradeSellZeroFix.Data;
        public bool[] EnableEnemyProximityBulldozeCursorFixData => enableEnemyProximityBulldozeCursorFix.Data;
        public bool[] EnableClientFeaturesData => enableClientFeatures.Data;
        public bool[] AllowMinimapWhilePlacingBuildingData => allowMinimapWhilePlacingBuilding.Data;
        public bool[] AllowCameraMovementWithModifiersData => allowCameraMovementWithModifiers.Data;
        public bool[] HdMarketViewData => hdMarketView.Data;
        public int[][] MarketGoodsOrderData => marketGoodsOrder.Data;

        [SyncPerPlayer]
        public bool EnableMinimapCursorFollowFix
        {
            get => enableMinimapCursorFollowFix.Value;
            set => SetPlayerSetting(enableMinimapCursorFollowFix, value, nameof(EnableMinimapCursorFollowFix));
        }

        [SyncPerPlayer]
        public bool EnableMarketKeyMainMenuFix
        {
            get => enableMarketKeyMainMenuFix.Value;
            set => SetPlayerSetting(enableMarketKeyMainMenuFix, value, nameof(EnableMarketKeyMainMenuFix));
        }

        [SyncPerPlayer]
        public bool EnableAutoTradeSellZeroFix
        {
            get => enableAutoTradeSellZeroFix.Value;
            set => SetPlayerSetting(enableAutoTradeSellZeroFix, value, nameof(EnableAutoTradeSellZeroFix));
        }

        [SyncPerPlayer]
        public bool EnableEnemyProximityBulldozeCursorFix
        {
            get => enableEnemyProximityBulldozeCursorFix.Value;
            set => SetPlayerSetting(enableEnemyProximityBulldozeCursorFix, value, nameof(EnableEnemyProximityBulldozeCursorFix));
        }

        [SyncPerPlayer]
        public bool EnableClientFeatures
        {
            get => enableClientFeatures.Value;
            set => SetPlayerSetting(enableClientFeatures, value, nameof(EnableClientFeatures));
        }

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set => SetSetting(ref enableMod, value, nameof(EnableMod));
        }

        [SyncPerPlayer]
        public bool AllowMinimapWhilePlacingBuilding
        {
            get => allowMinimapWhilePlacingBuilding.Value;
            set => SetPlayerSetting(allowMinimapWhilePlacingBuilding, value, nameof(AllowMinimapWhilePlacingBuilding));
        }

        [SyncPerPlayer]
        public bool AllowCameraMovementWithModifiers
        {
            get => allowCameraMovementWithModifiers.Value;
            set => SetPlayerSetting(allowCameraMovementWithModifiers, value, nameof(AllowCameraMovementWithModifiers));
        }

        [SyncPerPlayer]
        public bool HdMarketView
        {
            get => hdMarketView.Value;
            set => SetPlayerSetting(hdMarketView, value, nameof(HdMarketView));
        }

        [SyncPerPlayer]
        public int[] MarketGoodsOrder
        {
            get => MarketGoodsOrderDefinition.CloneOrDefault(marketGoodsOrder.Value);
            set
            {
                bool valid = MarketGoodsOrderDefinition.IsValid(value);
                int[] normalized = MarketGoodsOrderDefinition.CloneOrDefault(value);
                if (!CanMutateSetting(nameof(MarketGoodsOrder)))
                    return;

                if (!valid && value != null)
                {
                    Shared.DebugLogHelper.LogWarning(
                        marketGoodsLog,
                        "Bugfixes and QoL rejected an invalid market-goods order and restored the HD order.");
                }

                if (MarketGoodsOrderDefinition.AreEqual(marketGoodsOrder.Value, normalized))
                {
                    RefreshMarketGoodsOrderItems();
                    return;
                }

                marketGoodsOrder.SetValue(normalized);
                RefreshMarketGoodsOrderItems();
                SettingChanged?.Invoke(nameof(MarketGoodsOrder));
                OnPropertyChanged(nameof(MarketGoodsOrder));
            }
        }

        [SyncHostOnly]
        public bool RememberAiAivSettings
        {
            get => rememberAiAivSettings;
            set => SetSetting(ref rememberAiAivSettings, value, nameof(RememberAiAivSettings));
        }

        [SyncHostOnly]
        public bool EnableTroopMovementFix
        {
            get => enableTroopMovementFix;
            set => SetSetting(ref enableTroopMovementFix, value, nameof(EnableTroopMovementFix));
        }

        [SyncHostOnly]
        public bool EnablePlaguePopularityFix
        {
            get => enablePlaguePopularityFix;
            set => SetSetting(ref enablePlaguePopularityFix, value, nameof(EnablePlaguePopularityFix));
        }

        [SyncHostOnly]
        public bool EnablePlagueCloudRemovalFix
        {
            get => enablePlagueCloudRemovalFix;
            set => SetSetting(ref enablePlagueCloudRemovalFix, value, nameof(EnablePlagueCloudRemovalFix));
        }

        [SyncHostOnly]
        public bool EnableStuckApothecaryFix
        {
            get => enableStuckApothecaryFix;
            set => SetSetting(ref enableStuckApothecaryFix, value, nameof(EnableStuckApothecaryFix));
        }

        [SyncHostOnly]
        public bool EnablePlagueTargetReservationFix
        {
            get => enablePlagueTargetReservationFix;
            set => SetSetting(ref enablePlagueTargetReservationFix, value, nameof(EnablePlagueTargetReservationFix));
        }

        [SyncHostOnly]
        public bool EnableAssemblyPointPlacementFix
        {
            get => enableAssemblyPointPlacementFix;
            set => SetSetting(ref enableAssemblyPointPlacementFix, value, nameof(EnableAssemblyPointPlacementFix));
        }

        private void ResetToDefault()
        {
            if (CanEditHostSettings)
            {
                EnableMod = true;
                RememberAiAivSettings = true;
                EnableTroopMovementFix = true;
                EnablePlaguePopularityFix = true;
                EnablePlagueCloudRemovalFix = true;
                EnableStuckApothecaryFix = true;
                EnablePlagueTargetReservationFix = true;
                EnableAssemblyPointPlacementFix = true;
            }

            // Every participant resets only their own per-player preferences.
            EnableClientFeatures = true;
            AllowMinimapWhilePlacingBuilding = true;
            AllowCameraMovementWithModifiers = true;
            HdMarketView = true;
            MarketGoodsOrder = MarketGoodsOrderDefinition.CreateHdOrder();
            EnableMinimapCursorFollowFix = true;
            EnableMarketKeyMainMenuFix = true;
            EnableAutoTradeSellZeroFix = true;
            EnableEnemyProximityBulldozeCursorFix = true;
        }

        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        internal bool TrySetLocalPlayerId(int playerId)
        {
            if (!enableClientFeatures.TrySetLocalPlayerId(playerId))
                return false;

            enableMinimapCursorFollowFix.TrySetLocalPlayerId(playerId);
            enableMarketKeyMainMenuFix.TrySetLocalPlayerId(playerId);
            enableAutoTradeSellZeroFix.TrySetLocalPlayerId(playerId);
            enableEnemyProximityBulldozeCursorFix.TrySetLocalPlayerId(playerId);
            allowMinimapWhilePlacingBuilding.TrySetLocalPlayerId(playerId);
            allowCameraMovementWithModifiers.TrySetLocalPlayerId(playerId);
            hdMarketView.TrySetLocalPlayerId(playerId);
            marketGoodsOrder.TrySetLocalPlayerId(playerId);
            return true;
        }

        internal void InitializeMarketGoodsOrderEditor(ManualLogSource log)
        {
            marketGoodsLog = log;
            marketGoodNames.Clear();
            marketGoodIcons.Clear();

            bool missingSprite = false;
            foreach (int good in MarketGoodsOrderDefinition.CreateHdOrder())
            {
                marketGoodNames[good] = ResolveMarketGoodName(good);
                ImageSource icon = ResolveMarketGoodIcon(good);
                marketGoodIcons[good] = icon;
                missingSprite |= icon == null;
            }

            if (missingSprite)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Bugfixes and QoL could not resolve every market-goods icon; text tooltips remain available.");
            }

            while (MarketGoodsOrderItems.Count < MarketGoodsOrderDefinition.Count)
                MarketGoodsOrderItems.Add(new MarketGoodOrderItemViewModel(MoveMarketGood));

            RefreshMarketGoodsOrderItems();
        }

        private void MoveMarketGood(int good, int direction)
        {
            if (!CanEditClientSettings || !CanMutateSetting(nameof(MarketGoodsOrder)))
                return;

            MarketGoodsOrder = MarketGoodsOrderDefinition.SwapGoodWithNeighbor(
                marketGoodsOrder.Value,
                good,
                direction);
        }

        private void RestoreHdMarketOrder()
        {
            if (!CanEditClientSettings || !CanMutateSetting(nameof(MarketGoodsOrder)))
                return;

            MarketGoodsOrder = MarketGoodsOrderDefinition.CreateHdOrder();
        }

        private void RefreshMarketGoodsOrderItems()
        {
            if (MarketGoodsOrderItems.Count != MarketGoodsOrderDefinition.Count)
                return;

            int[] order = MarketGoodsOrderDefinition.CloneOrDefault(marketGoodsOrder.Value);
            for (int index = 0; index < order.Length; index++)
            {
                int good = order[index];
                marketGoodNames.TryGetValue(good, out string name);
                marketGoodIcons.TryGetValue(good, out ImageSource icon);
                MarketGoodsOrderItems[index].Update(
                    index,
                    good,
                    string.IsNullOrWhiteSpace(name) ? ((Enums.Goods)good).ToString() : name,
                    icon);
            }
        }

        private static string ResolveMarketGoodName(int good)
        {
            try
            {
                string name = Translate.Instance?.lookUpText(Enums.eTextSections.TEXT_GOODS, good);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
            catch
            {
            }

            return ((Enums.Goods)good).ToString();
        }

        private static ImageSource ResolveMarketGoodIcon(int good)
        {
            try
            {
                MainViewModel viewModel = MainViewModel.Instance;
                if (viewModel == null || viewModel.GameSprites == null)
                    return null;

                int spriteId = (int)viewModel.goodsSpriteEnumFromGoodsEnum((Enums.Goods)good);
                return spriteId >= 0 && spriteId < viewModel.GameSprites.Count
                    ? viewModel.GameSprites[spriteId]
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private void SetPlayerSetting(LocalPerPlayerSetting<bool> setting, bool value, string propertyName)
        {
            if (!setting.SetValue(value))
                return;

            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }
    }
}
