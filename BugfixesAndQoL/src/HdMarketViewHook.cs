// Feature: Restore Stronghold Crusader HD's product cycle in the detailed market view.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class HdMarketViewHook : IDisposable
    {
        private const int TradepostTradePanel = 57;

        private delegate void CycleTradeGoodsDelegate(MainViewModel self, object parameter);
        private delegate void NoesisGuiUpdateDelegate(FatControler self);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Hook cycleTradeGoodsHook;
        private Hook noesisGuiUpdateHook;
        private CycleTradeGoodsDelegate cycleTradeGoodsTrampoline;
        private NoesisGuiUpdateDelegate noesisGuiUpdateTrampoline;
        private bool missingMarketSpriteLogged;
        private bool disposed;

        public HdMarketViewHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            try
            {
                cycleTradeGoodsHook = new Hook(
                    FindMethod(typeof(MainViewModel), "ButtonCycleTradeGoodsType", typeof(object)),
                    (CycleTradeGoodsDelegate)CycleTradeGoodsHook);
                cycleTradeGoodsTrampoline = cycleTradeGoodsHook.GenerateTrampoline<CycleTradeGoodsDelegate>();

                noesisGuiUpdateHook = new Hook(
                    FindMethod(typeof(FatControler), nameof(FatControler.NoesisGUIUpdateChecksInGame)),
                    (NoesisGuiUpdateDelegate)NoesisGuiUpdateHook);
                noesisGuiUpdateTrampoline = noesisGuiUpdateHook.GenerateTrampoline<NoesisGuiUpdateDelegate>();
            }
            catch
            {
                Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL HD market view hooks installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            noesisGuiUpdateHook?.Undo();
            noesisGuiUpdateHook?.Dispose();
            noesisGuiUpdateHook = null;
            cycleTradeGoodsHook?.Undo();
            cycleTradeGoodsHook?.Dispose();
            cycleTradeGoodsHook = null;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL HD market view hooks disposed.");
        }

        private static MethodInfo FindMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);

            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, methodName);

            return method;
        }

        private void CycleTradeGoodsHook(MainViewModel self, object parameter)
        {
            if (!settings.EnableClientFeatures || !settings.HdMarketView || !IsSelectedTradepostControlled())
            {
                cycleTradeGoodsTrampoline(self, parameter);
                return;
            }

            try
            {
                int direction = Convert.ToInt32(parameter as string) == 0 ? -1 : 1;
                int currentGood = GameData.Instance.lastGameState.trading_current_goods;
                if (!TryGetTradeableNeighbor(currentGood, direction, out int targetGood))
                {
                    cycleTradeGoodsTrampoline(self, parameter);
                    return;
                }

                EngineInterface.GameAction(
                    Enums.GameActionCommand.SetCurrentTradedGood,
                    GameData.Instance.lastGameState.in_structure,
                    targetGood);

                if ((int)((UIElement)self.HUDBuildingPanel.RefTradePost_Trade_Auto).Visibility == 2)
                {
                    EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_Apply, 0, 0);
                    self.HUDBuildingPanel.initAutoTrade(targetGood);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL HD market navigation failed: {ex}");
                cycleTradeGoodsTrampoline(self, parameter);
            }
        }

        private void NoesisGuiUpdateHook(FatControler self)
        {
            noesisGuiUpdateTrampoline(self);

            if (!settings.EnableClientFeatures || !settings.HdMarketView || !IsSelectedTradepostControlled())
                return;

            try
            {
                if (GameData.Instance?.lastGameState == null ||
                    GameData.Instance.lastGameState.app_sub_mode != TradepostTradePanel)
                {
                    return;
                }

                MainViewModel viewModel = MainViewModel.Instance;
                int currentGood = GameData.Instance.lastGameState.trading_current_goods;
                if (viewModel == null ||
                    !TryGetTradeableNeighbor(currentGood, -1, out int previousGood) ||
                    !TryGetTradeableNeighbor(currentGood, 1, out int nextGood))
                {
                    return;
                }

                if (!TryResolveNeighborIcon(viewModel, previousGood, out int previousSpriteId, out ImageSource previousIcon) ||
                    !TryResolveNeighborIcon(viewModel, nextGood, out int nextSpriteId, out ImageSource nextIcon))
                {
                    if (!missingMarketSpriteLogged)
                    {
                        missingMarketSpriteLogged = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            "Bugfixes and QoL kept Vanilla market-neighbor icons because a configured goods sprite was unavailable.");
                    }

                    return;
                }

                // Vanilla draws DE's neighbors, so replace both icons only after both resources are valid.
                SetNeighborIcon(viewModel, previousSpriteId, previousIcon, isPrevious: true);
                SetNeighborIcon(viewModel, nextSpriteId, nextIcon, isPrevious: false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL HD market icon update failed: {ex}");
            }
        }

        private bool TryGetTradeableNeighbor(int currentGood, int direction, out int neighborGood)
        {
            return MarketGoodsOrderDefinition.TryGetTradeableNeighbor(
                settings.MarketGoodsOrder,
                currentGood,
                direction,
                IsTradeable,
                out neighborGood);
        }

        private static bool IsTradeable(int good)
        {
            short[] tradeBuyAmounts = GameData.Instance?.lastGameState?.trade_buy_amounts;
            return tradeBuyAmounts != null &&
                good >= 0 &&
                good < tradeBuyAmounts.Length &&
                tradeBuyAmounts[good] >= 0;
        }

        private static bool TryResolveNeighborIcon(
            MainViewModel viewModel,
            int good,
            out int spriteId,
            out ImageSource icon)
        {
            spriteId = -1;
            icon = null;
            if (viewModel?.GameSprites == null)
                return false;

            spriteId = (int)viewModel.goodsSpriteEnumFromGoodsEnum((Enums.Goods)good);
            if (spriteId < 0 || spriteId >= viewModel.GameSprites.Count)
                return false;

            icon = viewModel.GameSprites[spriteId];
            return (BaseComponent)(object)icon != (BaseComponent)null;
        }

        private static void SetNeighborIcon(
            MainViewModel viewModel,
            int spriteId,
            ImageSource icon,
            bool isPrevious)
        {
            if (isPrevious)
            {
                viewModel.TradePrevGoodsImage = icon;
                viewModel.SetSpriteWidth3(spriteId, 50);
            }
            else
            {
                viewModel.TradeNextGoodsImage = icon;
                viewModel.SetSpriteWidth4(spriteId, 50);
            }
        }

        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();

        private static unsafe bool IsSelectedTradepostControlled()
        {
            if (!IsMapEditor())
                return true;

            int activePlayerId = EditorDirector.instance?.ActivePlayerID ?? -1;
            int buildingId = SHCDESE.API.GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
            return activePlayerId > 0 && buildingId > 0 &&
                SHCDESE.API.GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out SHCDESE.Interop.GameBuilding* building) &&
                building != null &&
                building->r_AliveState == SHCDESE.Interop.Enums.AliveState.IsAlive &&
                building->r_BuildingType == SHCDESE.Interop.eStructs.STRUCT_TRADEPOST &&
                building->r_PlayerIdOwner == activePlayerId;
        }
    }
}
