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

        // Verified against Stronghold Crusader HD's DAT_MarketResourceCycleArray.
        private static readonly int[] HdGoodsCycle =
        {
            12, 11, 13, 10, 9, 16, 2, 4, 6, 8,
            19, 17, 21, 18, 20, 22, 23, 24, 14, 3
        };

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Hook cycleTradeGoodsHook;
        private Hook noesisGuiUpdateHook;
        private CycleTradeGoodsDelegate cycleTradeGoodsTrampoline;
        private NoesisGuiUpdateDelegate noesisGuiUpdateTrampoline;
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
            if (!settings.EnableMod || !settings.HdMarketView)
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

            if (!settings.EnableMod || !settings.HdMarketView)
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

                // Vanilla draws DE's neighbors, so replace both icons after its UI update.
                SetNeighborIcon(viewModel, previousGood, isPrevious: true);
                SetNeighborIcon(viewModel, nextGood, isPrevious: false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL HD market icon update failed: {ex}");
            }
        }

        private static bool TryGetTradeableNeighbor(int currentGood, int direction, out int neighborGood)
        {
            neighborGood = currentGood;
            int currentIndex = Array.IndexOf(HdGoodsCycle, currentGood);
            if (currentIndex < 0 || direction == 0)
                return false;

            for (int offset = 1; offset <= HdGoodsCycle.Length; offset++)
            {
                int index = (currentIndex + direction * offset) % HdGoodsCycle.Length;
                if (index < 0)
                    index += HdGoodsCycle.Length;

                int candidate = HdGoodsCycle[index];
                if (IsTradeable(candidate))
                {
                    neighborGood = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool IsTradeable(int good)
        {
            short[] tradeBuyAmounts = GameData.Instance?.lastGameState?.trade_buy_amounts;
            return tradeBuyAmounts != null &&
                good >= 0 &&
                good < tradeBuyAmounts.Length &&
                tradeBuyAmounts[good] >= 0;
        }

        private static void SetNeighborIcon(MainViewModel viewModel, int good, bool isPrevious)
        {
            Enums.eUISprites sprite = viewModel.goodsSpriteEnumFromGoodsEnum((Enums.Goods)good);
            int spriteId = (int)sprite;

            if (isPrevious)
            {
                viewModel.TradePrevGoodsImage = viewModel.GameSprites[spriteId];
                viewModel.SetSpriteWidth3(spriteId, 50);
            }
            else
            {
                viewModel.TradeNextGoodsImage = viewModel.GameSprites[spriteId];
                viewModel.SetSpriteWidth4(spriteId, 50);
            }
        }
    }
}
