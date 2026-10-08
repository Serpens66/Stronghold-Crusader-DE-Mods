using APIShared.GameModes;
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
        private delegate void ImageSetterDelegate(MainViewModel self, ImageSource image);
        private delegate void SpriteWidthDelegate(MainViewModel self, int sprite, int scale);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Hook cycleTradeGoodsHook;
        private Hook noesisGuiUpdateHook;
        private Hook previousImageHook;
        private Hook nextImageHook;
        private Hook previousWidthHook;
        private Hook nextWidthHook;
        private CycleTradeGoodsDelegate cycleTradeGoodsTrampoline;
        private NoesisGuiUpdateDelegate noesisGuiUpdateTrampoline;
        private ImageSetterDelegate previousImageTrampoline;
        private ImageSetterDelegate nextImageTrampoline;
        private SpriteWidthDelegate previousWidthTrampoline;
        private SpriteWidthDelegate nextWidthTrampoline;
        private int[] orderSnapshot;
        private MainViewModel suppressedViewModel;
        private int suppressedWriteCount;
        private bool suppressionObserved;
        private bool missingMarketSpriteLogged;
        private bool disposed;
        private bool published;

        public HdMarketViewHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            UpdateOrder(settings.MarketGoodsOrder);

            try
            {
                previousImageHook = new Hook(
                    FindSetter(nameof(MainViewModel.TradePrevGoodsImage)),
                    (ImageSetterDelegate)PreviousImageHook);
                previousImageTrampoline = previousImageHook.GenerateTrampoline<ImageSetterDelegate>();
                nextImageHook = new Hook(
                    FindSetter(nameof(MainViewModel.TradeNextGoodsImage)),
                    (ImageSetterDelegate)NextImageHook);
                nextImageTrampoline = nextImageHook.GenerateTrampoline<ImageSetterDelegate>();
                previousWidthHook = new Hook(
                    FindMethod(typeof(MainViewModel), nameof(MainViewModel.SetSpriteWidth3), typeof(int), typeof(int)),
                    (SpriteWidthDelegate)PreviousWidthHook);
                previousWidthTrampoline = previousWidthHook.GenerateTrampoline<SpriteWidthDelegate>();
                nextWidthHook = new Hook(
                    FindMethod(typeof(MainViewModel), nameof(MainViewModel.SetSpriteWidth4), typeof(int), typeof(int)),
                    (SpriteWidthDelegate)NextWidthHook);
                nextWidthTrampoline = nextWidthHook.GenerateTrampoline<SpriteWidthDelegate>();

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

            published = true;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL HD market view hooks installed.");
        }

        internal void UpdateOrder(int[] order)
        {
            // The setting may change while a map is open; publish one validated immutable snapshot.
            orderSnapshot = MarketGoodsOrderDefinition.CloneOrDefault(order);
        }

        public void Dispose()
        {
            if (disposed || published)
                return;

            disposed = true;
            noesisGuiUpdateHook?.Undo();
            noesisGuiUpdateHook?.Dispose();
            noesisGuiUpdateHook = null;
            cycleTradeGoodsHook?.Undo();
            cycleTradeGoodsHook?.Dispose();
            cycleTradeGoodsHook = null;
            nextWidthHook?.Undo();
            nextWidthHook?.Dispose();
            nextWidthHook = null;
            previousWidthHook?.Undo();
            previousWidthHook?.Dispose();
            previousWidthHook = null;
            nextImageHook?.Undo();
            nextImageHook?.Dispose();
            nextImageHook = null;
            previousImageHook?.Undo();
            previousImageHook?.Dispose();
            previousImageHook = null;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL HD market view hooks disposed.");
        }

        private static MethodInfo FindSetter(string propertyName)
        {
            PropertyInfo property = typeof(MainViewModel).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod();
            if (property?.PropertyType != typeof(ImageSource) || setter == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, "set_" + propertyName);
            return setter;
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
            MainViewModel viewModel = null;
            int previousSpriteId = -1;
            int nextSpriteId = -1;
            ImageSource previousIcon = null;
            ImageSource nextIcon = null;
            bool ready = false;
            try
            {
                if (settings.EnableClientFeatures && settings.HdMarketView &&
                    GameData.Instance?.lastGameState != null &&
                    GameData.Instance.lastGameState.app_sub_mode == TradepostTradePanel &&
                    IsSelectedTradepostControlled())
                {
                    viewModel = MainViewModel.Instance;
                    int currentGood = GameData.Instance.lastGameState.trading_current_goods;
                    if (viewModel != null &&
                        TryGetTradeableNeighbor(currentGood, -1, out int previousGood) &&
                        TryGetTradeableNeighbor(currentGood, 1, out int nextGood))
                    {
                        ready = TryResolveNeighborIcon(viewModel, previousGood, out previousSpriteId, out previousIcon) &&
                            TryResolveNeighborIcon(viewModel, nextGood, out nextSpriteId, out nextIcon);
                        if (!ready && !missingMarketSpriteLogged)
                        {
                            missingMarketSpriteLogged = true;
                            Shared.DebugLogHelper.LogWarning(log,
                                "Bugfixes and QoL kept Vanilla market-neighbor icons because a configured goods sprite was unavailable.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL HD market icon preparation failed: {ex}");
            }

            if (!ready)
            {
                noesisGuiUpdateTrampoline(self);
                return;
            }

            // Vanilla writes its DE neighbors on every GUI check. Keep its other work,
            // but prevent these four writes from invalidating the HD bindings each time.
            MainViewModel previousSuppressedViewModel = suppressedViewModel;
            suppressedWriteCount = 0;
            suppressedViewModel = viewModel;
            try
            {
                noesisGuiUpdateTrampoline(self);
            }
            finally
            {
                suppressedViewModel = previousSuppressedViewModel;
            }

            if (!suppressionObserved)
            {
                suppressionObserved = true;
                if (suppressedWriteCount == 4)
                    Shared.DebugLogHelper.LogDebug(log,
                        "Bugfixes and QoL HD market UI writes suppressed after startup cleanup.");
                else
                    Shared.DebugLogHelper.LogWarning(log,
                        $"Bugfixes and QoL HD market UI suppression intercepted {suppressedWriteCount}/4 Vanilla writes; check managed hook compatibility.");
            }

            try
            {
                SetNeighborIcon(viewModel, previousSpriteId, previousIcon, isPrevious: true);
                SetNeighborIcon(viewModel, nextSpriteId, nextIcon, isPrevious: false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL HD market icon update failed: {ex}");
            }
        }

        private void PreviousImageHook(MainViewModel self, ImageSource image)
        {
            if (ReferenceEquals(self, suppressedViewModel))
                suppressedWriteCount++;
            else
                previousImageTrampoline(self, image);
        }

        private void NextImageHook(MainViewModel self, ImageSource image)
        {
            if (ReferenceEquals(self, suppressedViewModel))
                suppressedWriteCount++;
            else
                nextImageTrampoline(self, image);
        }

        private void PreviousWidthHook(MainViewModel self, int sprite, int scale)
        {
            if (ReferenceEquals(self, suppressedViewModel))
                suppressedWriteCount++;
            else
                previousWidthTrampoline(self, sprite, scale);
        }

        private void NextWidthHook(MainViewModel self, int sprite, int scale)
        {
            if (ReferenceEquals(self, suppressedViewModel))
                suppressedWriteCount++;
            else
                nextWidthTrampoline(self, sprite, scale);
        }

        private bool TryGetTradeableNeighbor(int currentGood, int direction, out int neighborGood)
        {
            return MarketGoodsOrderDefinition.TryGetTradeableNeighborValidated(
                orderSnapshot,
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
            if (spriteId < 0 || spriteId >= viewModel.GameSprites.Count ||
                viewModel.GameSpriteDims == null ||
                spriteId >= viewModel.GameSpriteDims.GetLength(0) ||
                viewModel.GameSpriteDims.GetLength(1) < 2)
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
                if (!ReferenceEquals(viewModel.TradePrevGoodsImage, icon))
                    viewModel.TradePrevGoodsImage = icon;
                if (viewModel.SpriteWidth3 != viewModel.GameSpriteDims[spriteId, 0] * 50 / 100 ||
                    viewModel.SpriteHeight3 != viewModel.GameSpriteDims[spriteId, 1] * 50 / 100)
                    viewModel.SetSpriteWidth3(spriteId, 50);
            }
            else
            {
                if (!ReferenceEquals(viewModel.TradeNextGoodsImage, icon))
                    viewModel.TradeNextGoodsImage = icon;
                if (viewModel.SpriteWidth4 != viewModel.GameSpriteDims[spriteId, 0] * 50 / 100 ||
                    viewModel.SpriteHeight4 != viewModel.GameSpriteDims[spriteId, 1] * 50 / 100)
                    viewModel.SetSpriteWidth4(spriteId, 50);
            }
        }

        private static bool IsMapEditor() => APIShared.GameModes.GameModeHelper.IsMapEditor();

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
