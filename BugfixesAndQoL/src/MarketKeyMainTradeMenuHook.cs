// Feature: Pressing the market key again returns an open tradepost to its main panel.
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal sealed class MarketKeyMainTradeMenuHook : IDisposable
    {
        private const int BuildingAppMode = 16;
        private const int TradepostMainPanel = 25;
        private const int TradepostStructureType = 26;
        private const int TradepostPricesPanel = 53;
        private const int TradepostFoodPanel = 54;
        private const int TradepostResourcesPanel = 55;
        private const int TradepostWeaponsPanel = 56;
        private const int TradepostTradePanel = 57;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IDisposable subscription;

        public MarketKeyMainTradeMenuHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            subscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown);
        }

        public void Dispose()
        {
            subscription.Dispose();
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            try
            {
                if (!settings.EnableClientFeatures || args.Phase != EventHookPhase.Post)
                    return;

                KeyManager keyManager = KeyManager.instance;
                if (keyManager == null || !keyManager.IsActionPressed(Enums.KeyFunctions.Market))
                    return;

                if (GameData.Instance?.lastGameState == null || GameData.Instance.app_mode != BuildingAppMode)
                    return;

                int selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (selectedBuildingId <= 0 ||
                    GameBuildingManagerAPI.Instance.GetType(selectedBuildingId) != eStructs.STRUCT_TRADEPOST)
                {
                    return;
                }

                int subMode = GameData.Instance.app_sub_mode;
                if (subMode == TradepostMainPanel || !IsTradepostSubPanel(subMode))
                    return;

                if (EditorDirector.instance == null || MainViewModel.Instance == null)
                    return;

                EditorDirector.instance.directSetAppSubMode(TradepostMainPanel);
                MainViewModel.Instance.setUpInbuilding(TradepostMainPanel, TradepostStructureType);
                Shared.DebugLogHelper.LogDebug(log, () => $"Bugfixes and QoL reset tradepost menu from market key: selectedBuildingId={selectedBuildingId}, previousSubMode={subMode}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL market key tradepost menu reset failed: {ex}");
            }
        }

        private static bool IsTradepostSubPanel(int subMode)
        {
            return subMode == TradepostPricesPanel ||
                subMode == TradepostFoodPanel ||
                subMode == TradepostResourcesPanel ||
                subMode == TradepostWeaponsPanel ||
                subMode == TradepostTradePanel;
        }
    }
}
