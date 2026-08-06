using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using Zhuqiaomon.Memory.Managed;

namespace SomeSettings
{
    // Storage refunds need both OnBuildingRefund and OnBuildingBulldoze:
    // OnBuildingRefund fires once for a stockpile refund, even though the game removes all four stockpile parts at once.
    // OnBuildingBulldoze fires for each of those four parts, but it also fires for buildings destroyed by enemies.
    public sealed class SomeSettingsRuntime : IDisposable
    {
        private static readonly int GoodsCount = (int)eGoods.Count;

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly HashSet<string> resourceAddReentryGuards = new HashSet<string>();
        private readonly Dictionary<string, ResourceEventCountGuard> marketBuyResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private readonly Dictionary<string, ResourceEventCountGuard> refundResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private PendingStockpileRefund pendingStockpileRefund;
        private MinimapPlacementClickHook minimapPlacementClickHook;
        private CoopTrailCustomizeHook coopTrailCustomizeHook;
        private SkirmishAiSelectionMemoryHook skirmishAiSelectionMemoryHook;
        private AutoTradeSellZeroHook autoTradeSellZeroHook;
        private CtrlMarketTradeHook ctrlMarketTradeHook;
        private EnemyProximityBulldozeCursorHook enemyProximityBulldozeCursorHook;
        private SingleBuildingPauseHook singleBuildingPauseHook;
        private readonly KnightDismountRuntime knightDismountRuntime;
        private readonly QuarryPileRelocationRuntime quarryPileRelocationRuntime;
        private readonly TroopMovementFix3Runtime troopMovementFixRuntime;
        private readonly ChurchPriestCountRuntime churchPriestCountRuntime;
        private AssemblyPointPlacementPatch assemblyPointPlacementPatch;
        private AIEconomyProtectionHook aiEconomyProtectionHook;
        private IntPtr libraryHandle;
        private int libraryLength;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool knightFixedLayoutErrorLogged;
        private bool quarryFixedLayoutErrorLogged;
        private bool enemyProximityFixedLayoutErrorLogged;

        private bool hooksSubscribed;
        private bool settingsSubscribed;
        private const int BuildingAppMode = 16;
        private const int TradepostMainPanel = 25;
        private const int TradepostStructureType = 26;
        private const int TradepostPricesPanel = 53;
        private const int TradepostFoodPanel = 54;
        private const int TradepostResourcesPanel = 55;
        private const int TradepostWeaponsPanel = 56;
        private const int TradepostTradePanel = 57;
        private const int MarketBuyAmount = 5;
        private const int MarketBuyShiftAmount = 25;
        private const float VanillaRefundMultiplier = 0.5f;
        private static readonly TimeSpan MarketBuyGuardLifetime = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RefundGuardLifetime = TimeSpan.FromSeconds(2);

        public SomeSettingsRuntime(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            knightDismountRuntime = new KnightDismountRuntime(log, settings);
            quarryPileRelocationRuntime = new QuarryPileRelocationRuntime(log, settings);
            troopMovementFixRuntime =
                new TroopMovementFix3Runtime(log, settings);
            churchPriestCountRuntime = new ChurchPriestCountRuntime(log, settings);
            SubscribeSettingsChanges();
        }

        public object KnightDismountButton => knightDismountRuntime.ButtonViewModel;
        public object QuarryPileRelocationButton => quarryPileRelocationRuntime.ButtonViewModel;

        public void SetFixedLayoutHashValidated(bool isValidated)
        {
            fixedLayoutHashValidated = isValidated;
        }

        public void InstallKnightMountNativeFunctions(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (!fixedLayoutHashValidated)
                return;

            knightDismountRuntime.InstallNativeFunctions(libraryHandle, memory);
        }

        public void InstallQuarryPileNativeFunctions(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (!fixedLayoutHashValidated)
                return;

            quarryPileRelocationRuntime.InstallNativeFunctions(libraryHandle, memory);
        }

        public void InstallTroopMovementFixNativeFunctions(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory)
        {
            try
            {
                troopMovementFixRuntime.InitializeNative(
                    libraryHandle,
                    memory,
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings Troop Movement Fix 3 could not be initialized: {ex}");
            }
        }

        public void InstallChurchPriestCountNativeData(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                churchPriestCountRuntime.InitializeNative(libraryHandle, memory);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings church priest counts could not be initialized: {ex}");
            }
        }

        public void InitializeAssemblyPointPlacementPatch(IntPtr newLibraryHandle, ReadOnlySpan<byte> memory)
        {
            if (nativeLibraryAvailable)
                return;

            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            libraryHandle = newLibraryHandle;
            libraryLength = memory.Length;
            nativeLibraryAvailable = true;
            // Lobby-setting restoration can subscribe hooks before the native bootstrap finishes.
            if (settings.EnableMod && hooksSubscribed)
                InstallCtrlMarketTradeHook();
            ApplyAssemblyPointPlacementPatchSetting();
        }

        public void SubscribeHooks()
        {
            if (!settings.EnableMod)
                return;

            if (hooksSubscribed)
                return;

            try
            {
                subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(OnBuildingBulldoze));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(OnBuildingRefund));
                subscriptions.Add(BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(OnGoodsyardAddGood));
                subscriptions.Add(PlayerR3EventHooks.OnPlayerMarketInteraction.Observable.Subscribe(OnPlayerMarketInteraction));
                subscriptions.Add(InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(churchPriestCountRuntime.ApplySpawnedBuilding));
                subscriptions.Add(MapLoaderR3EventHooks.OnLoadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ApplyMapLoadedSettings()));
                subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ApplyMapLoadedSettings()));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnUnloadMap));
                minimapPlacementClickHook = new MinimapPlacementClickHook(log, settings);
                coopTrailCustomizeHook = new CoopTrailCustomizeHook(log);
                InstallAutoTradeSellZeroHook();
                InstallCtrlMarketTradeHook();
                InstallSingleBuildingPauseHook();
                hooksSubscribed = true;
                ReconcileFixedLayoutFeatures();
                Shared.DebugLogHelper.LogDebug(log, "SomeSettings hooks subscribed.");
            }
            catch
            {
                UnsubscribeHooks();
                throw;
            }
        }

        public void ApplySettings()
        {
            EnsureAiSelectionHook();

            if (!settings.EnableMod)
                return;

            SubscribeHooks();
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;

            ApplyRefundPercent(buildingApi.WoodRefundMultiplier, settings.WoodRefundPercent, "wood");
            ApplyRefundPercent(buildingApi.StoneRefundMultiplier, settings.StoneRefundPercent, "stone");
            ApplyRefundPercent(buildingApi.IronRefundMultiplier, settings.IronRefundPercent, "iron");
            ApplyRefundPercent(buildingApi.PitchRefundMultiplier, settings.PitchRefundPercent, "pitch");
            ApplyRefundPercent(buildingApi.GoldRefundMultiplier, settings.GoldRefundPercent, "gold");
            ApplyMarketPriceMultipliers();
            churchPriestCountRuntime.ApplySetting();
        }

        public void InstallAIEconomyProtectionHook(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (aiEconomyProtectionHook != null)
                return;

            try
            {
                aiEconomyProtectionHook = new AIEconomyProtectionHook(log, settings, libraryHandle, memory);
                singleBuildingPauseHook?.SetSleepStateSynchronizer(aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings AI economy protection hook could not be installed: {ex}");
            }
        }

        public void Dispose()
        {
            UnsubscribeHooks();
            skirmishAiSelectionMemoryHook?.Dispose();
            skirmishAiSelectionMemoryHook = null;
            aiEconomyProtectionHook?.Dispose();
            aiEconomyProtectionHook = null;
            DisableAssemblyPointPlacementPatch();
            nativeLibraryAvailable = false;
            libraryHandle = IntPtr.Zero;
            libraryLength = 0;
            troopMovementFixRuntime.Dispose();
            if (settingsSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsSubscribed = false;
            }
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            minimapPlacementClickHook?.Dispose();
            minimapPlacementClickHook = null;
            coopTrailCustomizeHook?.Dispose();
            coopTrailCustomizeHook = null;
            knightDismountRuntime.Dispose();
            quarryPileRelocationRuntime.Dispose();
            autoTradeSellZeroHook?.Dispose();
            autoTradeSellZeroHook = null;
            ctrlMarketTradeHook?.Dispose();
            ctrlMarketTradeHook = null;
            enemyProximityBulldozeCursorHook?.Dispose();
            enemyProximityBulldozeCursorHook = null;
            singleBuildingPauseHook?.Dispose();
            singleBuildingPauseHook = null;
            ClearResourceEventGuards();
            pendingStockpileRefund = null;
            hooksSubscribed = false;
        }

        private void EnsureAiSelectionHook()
        {
            if (skirmishAiSelectionMemoryHook != null)
                return;

            skirmishAiSelectionMemoryHook =
                new SkirmishAiSelectionMemoryHook(log, settings);
        }

        private void InstallAutoTradeSellZeroHook()
        {
            try
            {
                autoTradeSellZeroHook = new AutoTradeSellZeroHook(log);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings auto-trade sell zero hook could not be installed: {ex}");
            }
        }

        private void InstallCtrlMarketTradeHook()
        {
            if (ctrlMarketTradeHook != null)
                return;

            if (!nativeLibraryAvailable)
            {
                return;
            }

            try
            {
                ctrlMarketTradeHook = new CtrlMarketTradeHook(
                    log,
                    settings,
                    libraryHandle,
                    GetNativeLibraryMemory());

                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "SomeSettings Ctrl single-unit market hooks are running on an unknown CrusaderDE.dll because all required native instruction patterns were validated.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings Ctrl single-unit market hooks could not be installed and only this feature was disabled: {ex}");
            }
        }

        private void InstallEnemyProximityBulldozeCursorHook()
        {
            if (enemyProximityBulldozeCursorHook != null)
                return;

            try
            {
                enemyProximityBulldozeCursorHook = new EnemyProximityBulldozeCursorHook(log, settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings enemy-proximity bulldoze cursor hook could not be installed: {ex}");
            }
        }

        private void ReconcileFixedLayoutFeatures()
        {
            // Registration can raise setting changes before the native bootstrap is complete.
            if (!nativeLibraryAvailable || !settings.EnableMod)
                return;

            if (!fixedLayoutHashValidated)
            {
                if (settings.EnableKnightDismount && !knightFixedLayoutErrorLogged)
                {
                    knightFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "SomeSettings knight mount/dismount remains inactive because its fixed native unit and stable field layout is not validated for this CrusaderDE.dll.");
                }

                if (settings.EnableQuarryPileRelocation && !quarryFixedLayoutErrorLogged)
                {
                    quarryFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "SomeSettings quarry-pile relocation remains inactive because its fixed native building-manager field layout is not validated for this CrusaderDE.dll.");
                }

                if (!enemyProximityFixedLayoutErrorLogged)
                {
                    enemyProximityFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "SomeSettings enemy-proximity bulldoze cursor hook remains inactive because its fixed ChoreManager field offset is not validated for this CrusaderDE.dll.");
                }

                return;
            }

            knightDismountRuntime.Initialize();
            quarryPileRelocationRuntime.Initialize();
            InstallEnemyProximityBulldozeCursorHook();
        }

        private void InstallSingleBuildingPauseHook()
        {
            try
            {
                singleBuildingPauseHook = new SingleBuildingPauseHook(log, settings);
                if (aiEconomyProtectionHook != null)
                    singleBuildingPauseHook.SetSleepStateSynchronizer(aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings single-building pause hook could not be installed: {ex}");
            }
        }

        private void ApplyAssemblyPointPlacementPatchSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (settings.EnableMod)
            {
                InstallAssemblyPointPlacementPatch();
                return;
            }

            DisableAssemblyPointPlacementPatch();
        }

        private unsafe ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL remains loaded for the process lifetime.
            return new ReadOnlySpan<byte>(
                libraryHandle.ToPointer(),
                libraryLength);
        }

        private void InstallAssemblyPointPlacementPatch()
        {
            if (assemblyPointPlacementPatch != null)
                return;

            try
            {
                assemblyPointPlacementPatch = new AssemblyPointPlacementPatch(
                    log,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings assembly-point placement patch could not be installed: {ex}");
            }
        }

        private void DisableAssemblyPointPlacementPatch()
        {
            if (assemblyPointPlacementPatch == null)
                return;

            assemblyPointPlacementPatch?.Dispose();
            assemblyPointPlacementPatch = null;
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(SomeSettingsViewModel.EnableMod))
            {
                // Native movement patches must follow the global switch too.
                troopMovementFixRuntime.ApplySetting();
                ApplyAssemblyPointPlacementPatchSetting();

                if (settings.EnableMod)
                {
                    SubscribeHooks();
                    ReconcileFixedLayoutFeatures();
                    ApplySettings();
                }
                else
                {
                    RestoreDefaultSettings();
                    UnsubscribeHooks();
                }

                return;
            }

            if (!settings.EnableMod)
                return;

            if (propertyName == nameof(SomeSettingsViewModel.KeepStorageContent))
            {
                Shared.DebugLogHelper.LogDebug(log, () => $"SomeSettings changed: KeepStorageContent={settings.KeepStorageContent}.");
                return;
            }

            if (propertyName == nameof(SomeSettingsViewModel.EnableKnightDismount))
            {
                ReconcileFixedLayoutFeatures();
                knightDismountRuntime?.RefreshButtonVisibility();
                return;
            }

            if (propertyName == nameof(SomeSettingsViewModel.EnableQuarryPileRelocation))
            {
                ReconcileFixedLayoutFeatures();
                quarryPileRelocationRuntime?.ApplySetting();
                return;
            }

            if (propertyName == nameof(SomeSettingsViewModel.EnableExtraChurchPriests))
            {
                churchPriestCountRuntime.ApplySetting();
                return;
            }

            if (propertyName == nameof(SomeSettingsViewModel.EnableTroopMovementFix) ||
                propertyName == nameof(SomeSettingsViewModel.EnableFastRecruitRallyMovement))
            {
                troopMovementFixRuntime.ApplySetting();
                return;
            }

            ApplySettings();
        }

        private static void ApplyRefundPercent(ManagedValue<float> refundMultiplier, int percent, string label)
        {
            if (percent < 0)
            {
                refundMultiplier.SetValue(VanillaRefundMultiplier);
                return;
            }

            refundMultiplier.SetValue(percent / 100f);
        }

        private void RestoreDefaultSettings()
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            buildingApi.WoodRefundMultiplier.SetValue(0.5f);
            buildingApi.StoneRefundMultiplier.SetValue(0.5f);
            buildingApi.IronRefundMultiplier.SetValue(0.5f);
            buildingApi.PitchRefundMultiplier.SetValue(0.5f);
            buildingApi.GoldRefundMultiplier.SetValue(0.5f);
            RestoreTradeBasePrices();
            churchPriestCountRuntime.ApplySetting();
        }

        private void ApplyMapLoadedSettings()
        {
            ApplyMarketPriceMultipliers();
            churchPriestCountRuntime.ApplySetting();
        }

        private void ApplyMarketPriceMultipliers()
        {
            if (!settings.EnableMod)
                return;

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            for (int i = 0; i < GoodsCount; i++)
            {
                eGoods good = (eGoods)i;
                PackedGoodPrice vanillaPrice = playerApi.GetDefaultTradeBasePrice(good);
                PackedGoodPrice multipliedPrice = new PackedGoodPrice(
                    MultiplyPrice(vanillaPrice.BuyPrice, settings.MarketBuyPriceMultiplier),
                    MultiplyPrice(vanillaPrice.SellPrice, settings.MarketSellPriceMultiplier));

                playerApi.SetTradeBasePrice(good, multipliedPrice);
            }
        }

        private void RestoreTradeBasePrices()
        {
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            for (int i = 0; i < GoodsCount; i++)
            {
                eGoods good = (eGoods)i;
                playerApi.SetTradeBasePrice(good, playerApi.GetDefaultTradeBasePrice(good));
            }
        }

        private static int MultiplyPrice(int price, double multiplier)
        {
            if (price == 0 || Math.Abs(multiplier - 1.0) < 0.0001)
                return price;

            return (int)Math.Round(price * multiplier, MidpointRounding.AwayFromZero);
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            try
            {
                if (!settings.EnableMod || args.Phase != EventHookPhase.Post)
                    return;

                KeyManager keyManager = KeyManager.instance;
                if (keyManager == null || !keyManager.IsActionPressed(Enums.KeyFunctions.Market))
                    return;

                if (GameData.Instance == null || GameData.Instance.lastGameState == null)
                    return;

                if (GameData.Instance.app_mode != BuildingAppMode)
                    return;

                int selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (selectedBuildingId <= 0)
                    return;

                if (GameBuildingManagerAPI.Instance.GetType(selectedBuildingId) != eStructs.STRUCT_TRADEPOST)
                    return;

                int subMode = GameData.Instance.app_sub_mode;
                if (subMode == TradepostMainPanel || !IsTradepostSubPanel(subMode))
                    return;

                if (EditorDirector.instance == null || MainViewModel.Instance == null)
                    return;

                EditorDirector.instance.directSetAppSubMode(TradepostMainPanel);
                MainViewModel.Instance.setUpInbuilding(TradepostMainPanel, TradepostStructureType);
                Shared.DebugLogHelper.LogDebug(log, () => $"SomeSettings reset tradepost menu from market key: selectedBuildingId={selectedBuildingId}, previousSubMode={subMode}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings market key tradepost menu reset failed: {ex}");
            }
        }

        private static bool IsTradepostSubPanel(int subMode)
        {
            return subMode == TradepostPricesPanel
                || subMode == TradepostFoodPanel
                || subMode == TradepostResourcesPanel
                || subMode == TradepostWeaponsPanel
                || subMode == TradepostTradePanel;
        }

        private unsafe void OnBuildingBulldoze(BuildingBulldozeEventArgs args)
        {
            try
            {
                if (args.Phase != EventHookPhase.Pre)
                    return;

                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building))
                {
                    Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze: phase={args.Phase}, buildingId={args.BuildingId}, ignored=building-not-found.");
                    return;
                }

                eStructs structure = building->r_BuildingType;
                int owner = building->r_PlayerIdOwner;
                uint globalId = building->r_GlobalId;
                ushort tileX = building->r_TilePositionXBegin;
                ushort tileY = building->r_TilePositionYBegin;

                Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze: phase={args.Phase}, buildingId={args.BuildingId}, owner={owner}, type={structure}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");

                if (structure != eStructs.STRUCT_GOODS_YARD)
                {
                    Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze ignored non-stockpile buildingId={args.BuildingId}, type={structure}.");
                    return;
                }

                PendingStockpileRefund pending = pendingStockpileRefund;
                if (pending == null)
                {
                    Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze stockpile ignored: no pending stockpile refund, buildingId={args.BuildingId}, owner={owner}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");
                    return;
                }

                if (pending.CreatedAt < DateTime.UtcNow.AddSeconds(-2))
                {
                    Shared.DebugLogHelper.LogWarning(log, $"Pending stockpile refund expired: refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, partsRemaining={pending.PartsRemaining}.");
                    pendingStockpileRefund = null;
                    return;
                }

                if (owner != pending.Owner)
                {
                    Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze stockpile ignored: owner mismatch, buildingId={args.BuildingId}, owner={owner}, pendingOwner={pending.Owner}, refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");
                    return;
                }

                if (pending.ProcessedBuildingIds.Contains(args.BuildingId))
                {
                    Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze stockpile ignored: duplicate processed buildingId={args.BuildingId}, refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, processedBuildingIds={BuildProcessedBuildingIdSummary(pending.ProcessedBuildingIds)}.");
                    return;
                }

                int[] goods = CopyLocalGoods(building);
                RestoreGoods(pending.PlayerId, goods);
                int total = GetGoodsTotal(goods);
                string goodsSummary = BuildGoodsSummary(goods);
                pending.ProcessedBuildingIds.Add(args.BuildingId);
                pending.PartsRemaining--;

                Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze restored pending stockpile part: buildingId={args.BuildingId}, refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, globalId={globalId}, tileX={tileX}, tileY={tileY}, total={total}, goods={goodsSummary}, partsRemaining={pending.PartsRemaining}.");

                if (pending.PartsRemaining <= 0)
                {
                    Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingBulldoze pending stockpile refund completed: refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, processedBuildingIds={BuildProcessedBuildingIdSummary(pending.ProcessedBuildingIds)}.");
                    pendingStockpileRefund = null;
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings bulldoze pending stockpile refund hook failed: {ex}");
            }
        }

        private unsafe void OnBuildingRefund(BuildingRefundEventArgs args)
        {
            try
            {
                Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingRefund: phase={args.Phase}, playerId={args.PlayerId}, buildingId={args.BuildingId}, percentage={args.Percentage}, skipOriginal={args.SkipOriginalFunction}.");

                NormalizeRefundPercentage(args);
                AddResourceRefundGuards(args);

                if (args.Phase != EventHookPhase.Pre || !settings.KeepStorageContent)
                    return;

                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building))
                    return;

                eStructs structure = building->r_BuildingType;
                int owner = building->r_PlayerIdOwner;
                uint globalId = building->r_GlobalId;
                ushort tileX = building->r_TilePositionXBegin;
                ushort tileY = building->r_TilePositionYBegin;

                Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingRefund resolved building: buildingId={args.BuildingId}, owner={owner}, type={structure}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");

                if (structure == eStructs.STRUCT_GOODS_YARD)
                {
                    pendingStockpileRefund = new PendingStockpileRefund
                    {
                        PlayerId = args.PlayerId,
                        Owner = owner,
                        RefundBuildingId = args.BuildingId,
                        CreatedAt = DateTime.UtcNow,
                        PartsRemaining = 4
                    };

                    Shared.DebugLogHelper.LogDebug(log, () => $"OnBuildingRefund pending stockpile refund created: refundBuildingId={args.BuildingId}, playerId={args.PlayerId}, owner={owner}, globalId={globalId}, tileX={tileX}, tileY={tileY}, partsRemaining=4.");
                    return;
                }

                int[] goods = CopyLocalGoods(building);
                RestoreGoods(args.PlayerId, goods);
                int total = GetGoodsTotal(goods);
                string goodsSummary = BuildGoodsSummary(goods);

                Shared.DebugLogHelper.LogDebug(log, () => $"Kept storage content for refunded {structure} buildingId={args.BuildingId}, playerId={args.PlayerId}, percentage={args.Percentage}, total={total}, goods={goodsSummary}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings refund storage hook failed: {ex}");
            }
        }

        private void NormalizeRefundPercentage(BuildingRefundEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || !HasCustomRefundPercent())
                return;

            // The script extender calculates:
            // cost * (args.Percentage / 100f) * ResourceRefundMultiplier.
            // SomeSettings exposes final refund percentages, so custom values
            // use the per-resource multiplier directly and keep the event
            // percentage at 100. Vanilla/unchanged resources remain 50% through
            // their default 0.5 multiplier.
            args.Percentage = 100;
        }

        private bool HasCustomRefundPercent()
        {
            return settings.WoodRefundPercent >= 0 ||
                settings.StoneRefundPercent >= 0 ||
                settings.IronRefundPercent >= 0 ||
                settings.PitchRefundPercent >= 0 ||
                settings.GoldRefundPercent >= 0;
        }

        private void OnGoodsyardAddGood(AddGoodToGoodsyardEventArgs args)
        {
            int playerId = GameBuildingManagerAPI.Instance.GetOwner(args.BuildingId);
            string key = BuildResourceEventKey(playerId, args.Good);
            bool reentryGuardActive = resourceAddReentryGuards.Contains(key);

            LogDebugForResourceEventPlayer(
                playerId,
                "OnGoodsyardAddGood:",
                "phase", args.Phase,
                "player", playerId,
                "good", args.Good,
                "addAmount", args.AddAmount,
                "add", args.Add,
                "buildingId", args.BuildingId,
                "buildingGlobalId", args.BuildingGlobalId,
                "capacity", args.Capacity,
                "reentryGuard", reentryGuardActive);

            if (args.Phase != EventHookPhase.Post)
                return;

            if (!args.Add)
                return;

            if (args.AddAmount <= 0)
                return;

            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                return;

            PruneExpiredResourceGuards();

            bool marketGuardActive = marketBuyResourceGuards.TryGetValue(key, out ResourceEventCountGuard marketGuard);
            bool refundGuardActive = refundResourceGuards.TryGetValue(key, out ResourceEventCountGuard refundGuard);

            if (reentryGuardActive)
            {
                LogDebugForResourceEventPlayer(playerId, "OnGoodsyardAddGood ignored own TryAddGood event:", "player", playerId, "good", args.Good, "addAmount", args.AddAmount);
                return;
            }

            if (marketGuardActive)
            {
                marketGuard.RemainingAmount -= args.AddAmount;
                if (marketGuard.RemainingAmount <= 0)
                    marketBuyResourceGuards.Remove(key);

                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood ignored market buy resource event:",
                    "player", playerId,
                    "good", args.Good,
                    "addAmount", args.AddAmount,
                    "remainingMarketGuardAmount", marketGuard.RemainingAmount);
                return;
            }

            if (refundGuardActive)
            {
                refundGuard.RemainingAmount -= args.AddAmount;
                if (refundGuard.RemainingAmount <= 0)
                    refundResourceGuards.Remove(key);

                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood ignored building refund resource event:",
                    "player", playerId,
                    "good", args.Good,
                    "addAmount", args.AddAmount,
                    "remainingRefundGuardAmount", refundGuard.RemainingAmount);
                return;
            }

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            double multiplyGoods = isAI ? settings.MultiplyGoodsGainAI : settings.MultiplyGoodsGainHuman;
            double multiplyMoney = isAI ? settings.MultiplyGoodsGainInMoneyAI : settings.MultiplyGoodsGainInMoneyHuman;
            LogDebugForResourceEventPlayer(
                playerId,
                "OnGoodsyardAddGood processing:",
                "player", playerId,
                "good", args.Good,
                "addAmount", args.AddAmount,
                "marketBuyGuard", marketGuardActive,
                "refundGuard", refundGuardActive,
                "isAI", isAI,
                "multiplyGoods", multiplyGoods,
                "multiplyMoney", multiplyMoney);

            if (multiplyGoods > 1)
            {
                int bonusAmount = (int)Math.Round(args.AddAmount * (multiplyGoods - 1), MidpointRounding.AwayFromZero);
                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood TryAddGood bonus:",
                    "player", playerId,
                    "good", args.Good,
                    "sourceAmount", args.AddAmount,
                    "bonusAmount", bonusAmount,
                    "multiplyGoods", multiplyGoods);
                resourceAddReentryGuards.Add(key);
                try
                {
                    GamePlayerManagerAPI.Instance.TryAddGood(playerId, args.Good, bonusAmount);
                }
                finally
                {
                    resourceAddReentryGuards.Remove(key);
                }
            }

            if (multiplyMoney > 0)
            {
                PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetTradeBasePrice(args.Good);
                double sellPricePerItem = price.SellPrice / 5.0;
                int money = (int)Math.Round(args.AddAmount * sellPricePerItem * multiplyMoney, MidpointRounding.AwayFromZero);
                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood money bonus:",
                    "player", playerId,
                    "good", args.Good,
                    "amount", args.AddAmount,
                    "sellPricePerItem", sellPricePerItem,
                    "money", money,
                    "multiplyMoney", multiplyMoney);
                if (money != 0)
                    GamePlayerManagerAPI.Instance.AddPlayerGold(playerId, money);
            }
        }

        private void OnPlayerMarketInteraction(PlayerMarketInteractionEventArgs args)
        {
            string key = BuildResourceEventKey(args.PlayerId, args.Good);
            LogDebugForResourceEventPlayer(
                args.PlayerId,
                "OnPlayerMarketInteraction:",
                "phase", args.Phase,
                "player", args.PlayerId,
                "selling", args.Selling,
                "good", args.Good,
                "shiftModifier", args.ShiftModifier,
                "skipOriginal", args.SkipOriginalFunction,
                "key", key);

            if (args.ShiftModifier == CtrlMarketTradeHook.SingleTradeMode)
            {
                // Mode 2 belongs to SomeSettings. Never let unmodified Vanilla interpret it as Shift.
                args.SkipOriginalFunction = true;
                if (args.Phase != EventHookPhase.Pre)
                    return;

                if (args.Selling)
                {
                    ctrlMarketTradeHook?.ExecuteSingleMarketTrade(args);
                    return;
                }

                PruneExpiredMarketBuyResourceGuards();
                marketBuyResourceGuards[key] = new ResourceEventCountGuard
                {
                    RemainingAmount = 1,
                    ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
                };
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
                PruneExpiredMarketBuyResourceGuards();
                int expectedAmount = GetMarketInteractionAmount(args);
                marketBuyResourceGuards[key] = new ResourceEventCountGuard
                {
                    RemainingAmount = expectedAmount,
                    ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
                };
                LogDebugForResourceEventPlayer(
                    args.PlayerId,
                    "OnPlayerMarketInteraction market buy guard added:",
                    "player", args.PlayerId,
                    "good", args.Good,
                    "key", key,
                    "expectedAmount", expectedAmount);
                return;
            }

            if (args.Phase == EventHookPhase.Post)
            {
                marketBuyResourceGuards.Remove(key);
                LogDebugForResourceEventPlayer(args.PlayerId, "OnPlayerMarketInteraction market buy guard removed on post:", "player", args.PlayerId, "good", args.Good, "key", key);
            }
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            ClearResourceEventGuards();
            singleBuildingPauseHook?.ClearOverrides("map unload");
        }

        private unsafe static int[] CopyLocalGoods(GameBuilding* building)
        {
            int[] goods = new int[GoodsCount];
            int* localStorage = (int*)&building->r_NullAmount;
            for (int i = 0; i < GoodsCount; i++)
                goods[i] = localStorage[i];

            return goods;
        }

        private static void RestoreGoods(int playerId, int[] goods)
        {
            for (int i = 0; i < GoodsCount; i++)
            {
                int amount = goods[i];
                if (amount <= 0)
                    continue;

                GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, (eGoods)i, amount);
            }
        }

        private static int GetGoodsTotal(int[] goods)
        {
            int total = 0;
            for (int i = 0; i < goods.Length; i++)
            {
                if (goods[i] > 0)
                    total += goods[i];
            }

            return total;
        }

        private static string BuildGoodsSummary(int[] goods)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < goods.Length; i++)
            {
                int amount = goods[i];
                if (amount <= 0)
                    continue;

                parts.Add($"{(eGoods)i}={amount}");
            }

            if (parts.Count == 0)
                return "none";

            return string.Join(", ", parts);
        }

        private static string BuildProcessedBuildingIdSummary(HashSet<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return "none";

            List<int> sorted = new List<int>(ids);
            sorted.Sort();
            return string.Join(", ", sorted);
        }

        private void AddResourceRefundGuards(BuildingRefundEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre)
                return;

            if (args.PlayerId <= 0)
                return;

            PruneExpiredResourceGuards();
            AddBuildingRefundGuards(args);
            LogDebugForResourceEventPlayer(
                args.PlayerId,
                "OnBuildingRefund resource guard added:",
                "phase", args.Phase,
                "player", args.PlayerId,
                "buildingId", args.BuildingId,
                "percentage", args.Percentage,
                "skipOriginal", args.SkipOriginalFunction);
        }

        private static string BuildResourceEventKey(int playerId, eGoods good)
        {
            return playerId + ":" + (int)good;
        }

        private void ClearResourceEventGuards()
        {
            resourceAddReentryGuards.Clear();
            marketBuyResourceGuards.Clear();
            refundResourceGuards.Clear();
        }

        private void PruneExpiredResourceGuards()
        {
            PruneExpiredMarketBuyResourceGuards();
            PruneExpiredRefundResourceGuards();
        }

        private void PruneExpiredMarketBuyResourceGuards()
        {
            if (marketBuyResourceGuards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, ResourceEventCountGuard> entry in marketBuyResourceGuards)
            {
                if (entry.Value.ExpiresAt > now)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<string>();

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                marketBuyResourceGuards.Remove(expiredKeys[i]);
        }

        private void PruneExpiredRefundResourceGuards()
        {
            PruneExpiredCountGuardKeys(refundResourceGuards);
        }

        private static void PruneExpiredCountGuardKeys(Dictionary<string, ResourceEventCountGuard> guards)
        {
            if (guards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, ResourceEventCountGuard> entry in guards)
            {
                if (entry.Value.ExpiresAt > now)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<string>();

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                guards.Remove(expiredKeys[i]);
        }

        private static int GetMarketInteractionAmount(PlayerMarketInteractionEventArgs args)
        {
            if (args.ShiftModifier == CtrlMarketTradeHook.SingleTradeMode)
                return 1;

            return args.ShiftModifier != 0 ? MarketBuyShiftAmount : MarketBuyAmount;
        }

        private void AddBuildingRefundGuards(BuildingRefundEventArgs args)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(args.PlayerId))
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            eStructs buildingType = buildingApi.GetType(args.BuildingId);
            DateTime expiresAt = DateTime.UtcNow + RefundGuardLifetime;

            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_WOOD_PLANKS, GetRefundAmount(buildingApi.GetWoodCost(buildingType), buildingApi.WoodRefundMultiplier, args.Percentage), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_STONE_BLOCKS, GetRefundAmount(buildingApi.GetStoneCost(buildingType), buildingApi.StoneRefundMultiplier, args.Percentage), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_IRON_INGOTS, GetRefundAmount(buildingApi.GetIronIngotCost(buildingType), buildingApi.IronRefundMultiplier, args.Percentage), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_PITCH_RAW, GetRefundAmount(buildingApi.GetRawPitchCost(buildingType), buildingApi.PitchRefundMultiplier, args.Percentage), expiresAt);
        }

        private void AddBuildingRefundGuard(int playerId, eGoods good, int amount, DateTime expiresAt)
        {
            if (amount <= 0)
                return;

            string key = BuildResourceEventKey(playerId, good);
            refundResourceGuards[key] = new ResourceEventCountGuard
            {
                RemainingAmount = amount,
                ExpiresAt = expiresAt
            };

            LogDebugForResourceEventPlayer(
                playerId,
                "OnBuildingRefund resource good guard added:",
                "player", playerId,
                "good", good,
                "expectedAmount", amount,
                "key", key);
        }

        private static int GetRefundAmount(int cost, float refundMultiplier, int percentage)
        {
            if (cost <= 0 || refundMultiplier <= 0 || percentage <= 0)
                return 0;

            return (int)(cost * refundMultiplier * (percentage / 100f));
        }

        private void LogDebugForResourceEventPlayer(int playerId, params object[] parts)
        {
            if (ShouldLogResourceEventPlayer(playerId))
                Shared.DebugLogHelper.LogDebug(log, parts);
        }

        private static bool ShouldLogResourceEventPlayer(int playerId)
        {
            try
            {
                return GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            }
            catch
            {
                return false;
            }
        }

        private sealed class PendingStockpileRefund
        {
            public int PlayerId;
            public int Owner;
            public int RefundBuildingId;
            public DateTime CreatedAt;
            public int PartsRemaining;
            public HashSet<int> ProcessedBuildingIds = new HashSet<int>();
        }

        private sealed class ResourceEventCountGuard
        {
            public int RemainingAmount { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}
