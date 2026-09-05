// Feature: Lifecycle orchestration and shared event guards for Extra Features.
using BepInEx.Logging;
using RedBird.Core.Memory;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime : IDisposable
    {
        private static readonly int GoodsCount = (int)eGoods.Count;
        private static readonly TimeSpan MarketBuyGuardLifetime = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RefundGuardLifetime = TimeSpan.FromSeconds(2);
        private const int MarketBuyAmount = 5;
        private const int MarketBuyShiftAmount = 25;
        private const float VanillaRefundMultiplier = 0.5f;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly HashSet<string> resourceAddReentryGuards = new HashSet<string>();
        private readonly Dictionary<string, ResourceEventCountGuard> marketBuyResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private readonly Dictionary<string, ResourceEventCountGuard> refundResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly Shared.TroopActionHudCoordinator troopActionHudCoordinator;
        private readonly KnightDismountRuntime knightDismountRuntime;
        private readonly ChurchPriestCountRuntime churchPriestCountRuntime;
        private readonly GatehouseAutomationRuntime gatehouseAutomationRuntime;
        private readonly AIDefenseRepairRuntime aiDefenseRepairRuntime;
        private readonly LordHealthRuntime lordHealthRuntime;
        private readonly MarketTradeGuardBridge marketTradeGuardBridge;

        private PendingStockpileRefund pendingStockpileRefund;
        private AIMarketVanillaPriceHook aiMarketVanillaPriceHook;
        private MonkAlwaysRunPatch monkAlwaysRunPatch;
        private PlagueDurationPatch plagueDurationPatch;
        private PlagueApothecarySearchRangePatch plagueApothecarySearchRangePatch;
        private IntPtr libraryHandle;
        private int libraryLength;
        private ScanRegion nativeRegion;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool monkAlwaysRunPatchUnavailable;
        private bool hooksSubscribed;
        private bool settingsSubscribed;
        private bool plagueDurationPatchUnavailable;
        private bool plagueApothecarySearchRangePatchUnavailable;
        private bool mapActive;
        private ushort? originalCampPeasantsCap;

        public ExtraFeaturesRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Shared.GameplayModActivationGate.Initialize(log, ExtraFeaturesPlugin.PluginGuid, ExtraFeaturesPlugin.PluginName, () => settings.EnableMod);
            Shared.GameplayModActivationGate.StateChanged += OnModeStateChanged;
            multiplayerFeatureGate = new MultiplayerFeatureGate(log);
            troopActionHudCoordinator = new Shared.TroopActionHudCoordinator(log);
            knightDismountRuntime = new KnightDismountRuntime(log, settings, multiplayerFeatureGate);
            troopActionHudCoordinator.Register(knightDismountRuntime.RefreshButtonVisibility);
            churchPriestCountRuntime = new ChurchPriestCountRuntime(log, settings);
            gatehouseAutomationRuntime = new GatehouseAutomationRuntime(log, settings, multiplayerFeatureGate);
            aiDefenseRepairRuntime = new AIDefenseRepairRuntime(log, settings);
            lordHealthRuntime = new LordHealthRuntime(log, settings);
            marketTradeGuardBridge = new MarketTradeGuardBridge(log, this);
            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        public object KnightDismountButton => knightDismountRuntime.ButtonViewModel;
        public object GatehouseAutomationButton => gatehouseAutomationRuntime.ButtonViewModel;
        public void InitializeNetwork()
        {
            TryRunFeature("troop action HUD coordinator", troopActionHudCoordinator.Initialize);
            try
            {
                // Registration order is a shared protocol boundary. Keep the group fail-closed
                // and identical on every peer instead of skipping individual packet IDs.
                knightDismountRuntime.InitializeNetwork();
                gatehouseAutomationRuntime.InitializeNetwork();
            }
            catch (Exception ex)
            {
                LogFeatureFailure("synchronized action packet group", ex);
            }

            // Local lifecycle setup does not require a functioning custom packet group.
            TryRunFeature("gatehouse automation lifecycle", gatehouseAutomationRuntime.Initialize);
            TryRunFeature("AI defense repair lifecycle", aiDefenseRepairRuntime.Initialize);
        }

        public void InitializeNative(CrusaderLibraryLoadContext context, bool isFixedLayoutHashValidated)
        {
            if (nativeLibraryAvailable)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            IntPtr newLibraryHandle = context.ModuleHandle;
            ReadOnlySpan<byte> memory = context.Memory;
            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0 || context.Region == null)
                throw new ArgumentException("The Crusader library is unavailable.");

            libraryHandle = newLibraryHandle;
            libraryLength = memory.Length;
            nativeRegion = context.Region;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;

            try
            {
                churchPriestCountRuntime.InitializeNative(newLibraryHandle, memory, fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                LogFeatureFailure("church priest counts", ex);
            }

            InitializePlagueDurationPatch(newLibraryHandle, nativeRegion, memory);
            InitializePlagueApothecarySearchRangePatch(newLibraryHandle, nativeRegion, memory);
            InitializeMonkAlwaysRunPatch(newLibraryHandle, nativeRegion, memory);
            try
            {
                gatehouseAutomationRuntime.InitializeNative(newLibraryHandle, memory, fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                LogFeatureFailure("gatehouse automation native timing", ex);
            }

            try
            {
                aiDefenseRepairRuntime.InitializeNative(newLibraryHandle, nativeRegion, memory, fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                LogFeatureFailure("AI defense rebuild native hook", ex);
            }

            // Settings may be restored before LibraryLoaded. Retry activation now that the native
            // library is available instead of waiting for a later setting change.
            ReconcileFixedLayoutFeatures();
        }

        public void ApplySettings()
        {
            TryRunFeature("AI defense repair configuration", ReconcileAIDefenseRepairRuntime);
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
                TryRunFeature("Vanilla value restoration", RestoreDefaultSettings);
                TryRunFeature("Monks Always Run", ApplyMonkAlwaysRunSetting);
                TryRunFeature("optional local hooks", UnsubscribeHooks);
                return;
            }

            TryRunFeature("shared event hooks", SubscribeHooks);
            TryRunFeature("bulldoze refunds", ApplyRefundSettings);
            TryRunFeature("market price multipliers", ApplyMarketPriceMultipliers);
            TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting);
            TryRunFeature("campfire peasants", ApplyCampfirePeasantsLimit);
            TryRunFeature("Lord health", ReconcileLordHealthRuntime);
            ApplyPlagueDurationSetting();
            ApplyMonkAlwaysRunSetting();
            TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
        }

        public void InstallAIMarketVanillaPriceHook()
        {
            if (aiMarketVanillaPriceHook != null)
                return;

            try
            {
                aiMarketVanillaPriceHook = new AIMarketVanillaPriceHook(
                    log, settings, libraryHandle, nativeRegion, GetNativeLibraryMemory(), fixedLayoutHashValidated);
                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Extra Features AI Vanilla market prices are running on an unknown " +
                        "CrusaderDE.dll because both native helper signatures and hook spans were validated.");
                }
            }
            catch (Exception ex)
            {
                aiMarketVanillaPriceHook?.Dispose();
                aiMarketVanillaPriceHook = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Extra Features AI Vanilla market-price override is disabled for this process; " +
                    $"the AI uses the active global prices and all other Extra Features remain active: {ex}");
            }
        }

        public void Dispose()
        {
            Shared.GameplayModActivationGate.StateChanged -= OnModeStateChanged;
            RestoreCampfirePeasantsCap();
            UnsubscribeHooks();
            aiMarketVanillaPriceHook?.Dispose();
            aiMarketVanillaPriceHook = null;
            monkAlwaysRunPatch?.Dispose();
            monkAlwaysRunPatch = null;
            plagueDurationPatch?.Dispose();
            plagueDurationPatch = null;
            plagueApothecarySearchRangePatch?.Dispose();
            plagueApothecarySearchRangePatch = null;
            troopActionHudCoordinator.Dispose();
            knightDismountRuntime.Dispose();
            gatehouseAutomationRuntime.Dispose();
            aiDefenseRepairRuntime.Dispose();
            marketTradeGuardBridge.Dispose();
            lordHealthRuntime.Dispose();
            nativeLibraryAvailable = false;
            libraryHandle = IntPtr.Zero;
            libraryLength = 0;
            nativeRegion = null;

            if (settingsSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsSubscribed = false;
            }
        }

        private void SubscribeHooks()
        {
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) || hooksSubscribed)
                return;

            TrySubscribeFeature("bulldoze tracking", () =>
                BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(OnBuildingBulldoze));
            TrySubscribeFeature("refund tracking", () =>
                BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(OnBuildingRefund));
            TrySubscribeFeature("goods gain tracking", () =>
                BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(OnGoodsyardAddGood));
            TrySubscribeFeature("market trade tracking", () =>
                PlayerR3EventHooks.OnPlayerMarketInteraction.Observable.Subscribe(OnPlayerMarketInteraction));
            TrySubscribeFeature("church priest spawn handling", () =>
                BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(churchPriestCountRuntime.ApplySpawnedBuilding));
            TrySubscribeFeature("map-load settings", () =>
                MapLoaderR3EventHooks.OnLoadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ApplyMapLoadedSettings()));
            TrySubscribeFeature("save-load settings", () =>
                MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ApplyMapLoadedSettings()));
            TrySubscribeFeature("map-start multiplayer feature gate", () =>
                MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnStartMap));
            TrySubscribeFeature("map-unload cleanup", () =>
                MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnUnloadMap));
            TryRunFeature("Lord health tick", ReconcileLordHealthRuntime);
            hooksSubscribed = true;
            ReconcileFixedLayoutFeatures();
            Shared.DebugLogHelper.LogDebug(log, "Extra Features feature hooks reconciled.");
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
                TryRunFeature("event subscription cleanup", subscription.Dispose);
            subscriptions.Clear();
            TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Dispose);
            TryRunFeature("Lord health cleanup", lordHealthRuntime.Dispose);
            ClearResourceEventGuards();
            pendingStockpileRefund = null;
            hooksSubscribed = false;
        }

        private void TryRunFeature(string featureName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogFeatureFailure(featureName, ex);
            }
        }

        private void TrySubscribeFeature(string featureName, Func<IDisposable> subscribe)
        {
            try
            {
                IDisposable subscription = subscribe();
                if (subscription != null)
                    subscriptions.Add(subscription);
            }
            catch (Exception ex)
            {
                LogFeatureFailure(featureName, ex);
            }
        }

        private void LogFeatureFailure(string featureName, Exception ex)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"Extra Features feature '{featureName}' failed and remains inactive: {ex}");
        }

        private void ReconcileFixedLayoutFeatures()
        {
            if (!nativeLibraryAvailable || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Dispose);
                return;
            }

            // Knight mount/dismount uses the public bidirectional link API plus a managed,
            // Vanilla-equivalent TotalHorses transition; neither requires a hash-bound delegate.
            if (settings.EnableKnightDismount)
                TryRunFeature("knight mount/dismount", knightDismountRuntime.Initialize);
            else
                TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Dispose);

        }

        private void ReconcileAIDefenseRepairRuntime()
        {
            aiDefenseRepairRuntime.ReconcileConfiguration();
            if (nativeLibraryAvailable)
            {
                aiDefenseRepairRuntime.InitializeNative(
                    libraryHandle,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    fixedLayoutHashValidated);
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod))
            {
                if (Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                {
                    SubscribeHooks();
                    ApplySettings();
                }
                else
                {
                    TryRunFeature("enemy proximity restoration", ReconcileAIDefenseRepairRuntime);
                    TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
                    TryRunFeature("Vanilla value restoration", RestoreDefaultSettings);
                    TryRunFeature("Monks Always Run", ApplyMonkAlwaysRunSetting);
                    TryRunFeature("optional local hooks", UnsubscribeHooks);
                }
                return;
            }

            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;

            if (propertyName == nameof(ExtraFeaturesViewModel.EnableKnightDismount))
            {
                ReconcileFixedLayoutFeatures();
                TryRunFeature("knight mount/dismount visibility", knightDismountRuntime.RefreshButtonVisibility);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.HumanGateReopenDelaySeconds) ||
                propertyName == nameof(ExtraFeaturesViewModel.AIGateReopenDelaySeconds) ||
                propertyName == nameof(ExtraFeaturesViewModel.HumanGateClosingDistanceTiles) ||
                propertyName == nameof(ExtraFeaturesViewModel.AIGateClosingDistanceTiles))
            {
                TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableExtraChurchPriests))
            {
                TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.CampfirePeasantsLimit))
            {
                TryRunFeature("campfire peasants", ApplyCampfirePeasantsLimit);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMonksAlwaysRun))
            {
                ApplyMonkAlwaysRunSetting();
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.PlagueDurationMultiplier))
            {
                ApplyPlagueDurationSetting();
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.ApothecaryPlagueSearchDistance))
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features apothecary plague-search distance changed to " +
                    $"{settings.ApothecaryPlagueSearchDistance}.");
                return;
            }

            if (propertyName == nameof(ExtraFeaturesViewModel.HumanLordHealthPercent) ||
                propertyName == nameof(ExtraFeaturesViewModel.AILordHealthPercent))
            {
                TryRunFeature("Lord health", ReconcileLordHealthRuntime);
                return;
            }

            ApplySettings();
        }

        private void OnModeStateChanged(bool allowed)
        {
            if (!allowed)
            {
                mapActive = false;
                ClearResourceEventGuards();
                pendingStockpileRefund = null;
                multiplayerFeatureGate.Reset();
                gatehouseAutomationRuntime.EndMap();
            }

            ApplySettings();
        }

        private void ApplyMapLoadedSettings()
        {
            mapActive = true;
            TryRunFeature("gatehouse map initialization", gatehouseAutomationRuntime.BeginMap);
            TryRunFeature("market price multipliers", ApplyMarketPriceMultipliers);
            TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting);
            TryRunFeature("campfire peasants", ApplyCampfirePeasantsLimit);
            ApplyPlagueDurationSetting();
            ApplyMonkAlwaysRunSetting();
            TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
        }

        private void InitializeMonkAlwaysRunPatch(
            IntPtr nativeLibraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory)
        {
            if (monkAlwaysRunPatch != null || monkAlwaysRunPatchUnavailable)
                return;

            try
            {
                monkAlwaysRunPatch = new MonkAlwaysRunPatch(
                    log,
                    nativeLibraryHandle,
                    region,
                    memory,
                    fixedLayoutHashValidated);
                ApplyMonkAlwaysRunSetting();
                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Extra Features Monks Always Run is operating on an unknown " +
                        "CrusaderDE.dll after its signature, hook span, branch targets, and movement semantics were validated.");
                }
            }
            catch (Exception ex)
            {
                try { monkAlwaysRunPatch?.Dispose(); } catch { }
                monkAlwaysRunPatch = null;
                monkAlwaysRunPatchUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Extra Features Monks Always Run is disabled for this process; " +
                    $"all other features remain available: {ex}");
            }
        }

        private void ApplyMonkAlwaysRunSetting()
        {
            monkAlwaysRunPatch?.SetEnabled(
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.EnableMonksAlwaysRun);
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            multiplayerFeatureGate.CaptureMapMode(args.bMultiplayerSave != 0);

            TryRunFeature("Lord health map initialization", ReconcileLordHealthRuntime);

            TryRunFeature("knight mount/dismount visibility", knightDismountRuntime.RefreshButtonVisibility);
            TryRunFeature("gatehouse map initialization", gatehouseAutomationRuntime.BeginMap);
        }

        private void InitializePlagueDurationPatch(IntPtr nativeLibraryHandle, ScanRegion region, ReadOnlySpan<byte> memory)
        {
            if (plagueDurationPatch != null || plagueDurationPatchUnavailable)
                return;

            try
            {
                plagueDurationPatch = new PlagueDurationPatch(
                    log, nativeLibraryHandle, region, memory, fixedLayoutHashValidated);
                ApplyPlagueDurationSetting();
                if (plagueDurationPatchUnavailable)
                    return;

                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Extra Features plague duration is running on an unknown CrusaderDE.dll because its native instruction signature and Vanilla lifetime were validated.");
                }
            }
            catch (Exception ex)
            {
                DisablePlagueDurationPatch(ex);
            }
        }

        private void InitializePlagueApothecarySearchRangePatch(
            IntPtr nativeLibraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory)
        {
            if (plagueApothecarySearchRangePatch != null || plagueApothecarySearchRangePatchUnavailable)
                return;

            try
            {
                plagueApothecarySearchRangePatch = new PlagueApothecarySearchRangePatch(
                    log,
                    settings,
                    nativeLibraryHandle,
                    region,
                    memory,
                    fixedLayoutHashValidated);
                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Extra Features apothecary plague-search range is running on an unknown " +
                        "CrusaderDE.dll because its native instruction signature was validated.");
                }
            }
            catch (Exception ex)
            {
                plagueApothecarySearchRangePatchUnavailable = true;
                plagueApothecarySearchRangePatch?.Dispose();
                plagueApothecarySearchRangePatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features apothecary plague-search range is disabled for this process; " +
                    $"Vanilla distance 30 and all other features remain active: {ex}");
            }
        }

        private void ApplyPlagueDurationSetting()
        {
            if (plagueDurationPatch == null || plagueDurationPatchUnavailable)
                return;

            try
            {
                plagueDurationPatch.Apply(
                    settings.PlagueDurationMultiplier,
                    Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod));
            }
            catch (Exception ex)
            {
                DisablePlagueDurationPatch(ex);
            }
        }

        public bool TryRegisterVanillaFlagDisease(int projectileId) =>
            !plagueDurationPatchUnavailable &&
            plagueDurationPatch != null &&
            plagueDurationPatch.TryRegisterVanillaFlagDisease(projectileId);

        private void DisablePlagueDurationPatch(Exception failure)
        {
            if (plagueDurationPatchUnavailable)
                return;

            plagueDurationPatchUnavailable = true;
            Exception restoreFailure = null;
            if (plagueDurationPatch != null)
            {
                try
                {
                    plagueDurationPatch.Dispose();
                }
                catch (Exception ex)
                {
                    restoreFailure = ex;
                }
            }

            plagueDurationPatch = null;
            string restoreDetails = restoreFailure == null
                ? string.Empty
                : $" Vanilla restoration also failed: {restoreFailure}";
            Shared.DebugLogHelper.LogError(
                log,
                $"Extra Features plague duration multiplier is disabled for this process; all other features remain available: {failure}.{restoreDetails}");
        }

        private void RestoreDefaultSettings()
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            TryRunFeature("wood refund restoration", () => buildingApi.WoodRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("stone refund restoration", () => buildingApi.StoneRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("iron refund restoration", () => buildingApi.IronRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("pitch refund restoration", () => buildingApi.PitchRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("gold refund restoration", () => buildingApi.GoldRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("market price restoration", RestoreTradeBasePrices);
            TryRunFeature("church priest restoration", churchPriestCountRuntime.ApplySetting);
            TryRunFeature("campfire peasants restoration", RestoreCampfirePeasantsCap);
            TryRunFeature("plague duration restoration", ApplyPlagueDurationSetting);
        }

        private void ReconcileLordHealthRuntime()
        {
            bool enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                (settings.HumanLordHealthPercent != LordHealthMultiplierPolicy.DefaultPercent ||
                 settings.AILordHealthPercent != LordHealthMultiplierPolicy.DefaultPercent);
            if (!enabled)
            {
                lordHealthRuntime.Dispose();
                return;
            }

            lordHealthRuntime.Initialize();
            if (mapActive)
                lordHealthRuntime.BeginMap();
        }

        private unsafe ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL stays loaded for the process lifetime.
            return nativeRegion == null ? ReadOnlySpan<byte>.Empty : nativeRegion.Span;
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            RestoreCampfirePeasantsCap();
            lordHealthRuntime.ResetMapState();
            mapActive = false;
            ClearResourceEventGuards();
            multiplayerFeatureGate.Reset();
            gatehouseAutomationRuntime.EndMap();
        }

        private void ApplyCampfirePeasantsLimit()
        {
            if (settings.CampfirePeasantsLimit < 0)
            {
                RestoreCampfirePeasantsCap();
                return;
            }

            if (!IsMapActive())
            {
                Shared.DebugLogHelper.LogDebug(log, "Extra Features campfire peasants setting deferred until a map is active.");
                return;
            }

            if (GameGlobalsManager.Instance.CampPeasantsCap == null)
            {
                Shared.DebugLogHelper.LogWarning(log, "Extra Features campfire peasants setting skipped: CampPeasantsCap global was not found.");
                return;
            }

            if (!originalCampPeasantsCap.HasValue)
                originalCampPeasantsCap = GameGlobalsManager.Instance.CampPeasantsCap.GetValue();

            ushort value = (ushort)Math.Min(500, Math.Max(0, settings.CampfirePeasantsLimit));
            GameGlobalsManager.Instance.CampPeasantsCap.SetValue(value);
            Shared.DebugLogHelper.LogDebug(log, "Extra Features applied campfire peasants limit:", value);
        }

        private bool IsMapActive()
        {
            if (mapActive)
                return true;

            try
            {
                if (!string.IsNullOrEmpty(GamePlayerManagerAPI.Instance.GetCurrentMapName()))
                {
                    mapActive = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "Extra Features could not determine active map state:", ex.Message);
            }

            return false;
        }

        private void RestoreCampfirePeasantsCap()
        {
            if (!originalCampPeasantsCap.HasValue || GameGlobalsManager.Instance.CampPeasantsCap == null)
                return;

            GameGlobalsManager.Instance.CampPeasantsCap.SetValue(originalCampPeasantsCap.Value);
            Shared.DebugLogHelper.LogDebug(log, "Extra Features restored campfire peasants limit:", originalCampPeasantsCap.Value);
            originalCampPeasantsCap = null;
        }

        private static string BuildResourceEventKey(int playerId, eGoods good) => playerId + ":" + (int)good;

        private void ClearResourceEventGuards()
        {
            resourceAddReentryGuards.Clear();
            marketBuyResourceGuards.Clear();
            refundResourceGuards.Clear();
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
