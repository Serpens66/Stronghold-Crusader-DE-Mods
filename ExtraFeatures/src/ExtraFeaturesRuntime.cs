// Feature: Lifecycle orchestration and shared event guards for Extra Features.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
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
        private readonly QuarryPileRelocationRuntime quarryPileRelocationRuntime;
        private readonly ChurchPriestCountRuntime churchPriestCountRuntime;
        private readonly GatehouseAutomationRuntime gatehouseAutomationRuntime;
        private readonly AIDefenseRepairRuntime aiDefenseRepairRuntime;
        private readonly LordHealthRuntime lordHealthRuntime;
        private readonly MarketTradeGuardBridge marketTradeGuardBridge;

        private PendingStockpileRefund pendingStockpileRefund;
        private SingleBuildingPauseHook singleBuildingPauseHook;
        private AIEconomyProtectionHook aiEconomyProtectionHook;
        private AIMarketVanillaPriceHook aiMarketVanillaPriceHook;
        private FastRecruitMovementBridge fastRecruitMovementBridge;
        private MonkAlwaysRunPatch monkAlwaysRunPatch;
        private PlagueDurationPatch plagueDurationPatch;
        private PlagueApothecarySearchRangePatch plagueApothecarySearchRangePatch;
        private IntPtr libraryHandle;
        private int libraryLength;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool quarryFixedLayoutErrorLogged;
        private bool fastRecruitInitializationAttempted;
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
            multiplayerFeatureGate = new MultiplayerFeatureGate(log);
            troopActionHudCoordinator = new Shared.TroopActionHudCoordinator(log);
            knightDismountRuntime = new KnightDismountRuntime(log, settings, multiplayerFeatureGate);
            troopActionHudCoordinator.Register(knightDismountRuntime.RefreshButtonVisibility);
            quarryPileRelocationRuntime = new QuarryPileRelocationRuntime(log, settings, multiplayerFeatureGate);
            churchPriestCountRuntime = new ChurchPriestCountRuntime(log, settings);
            gatehouseAutomationRuntime = new GatehouseAutomationRuntime(log, settings, multiplayerFeatureGate);
            aiDefenseRepairRuntime = new AIDefenseRepairRuntime(log, settings);
            lordHealthRuntime = new LordHealthRuntime(log, settings);
            marketTradeGuardBridge = new MarketTradeGuardBridge(log, this);
            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        public object KnightDismountButton => knightDismountRuntime.ButtonViewModel;
        public object QuarryPileRelocationButton => quarryPileRelocationRuntime.ButtonViewModel;
        public object GatehouseAutomationButton => gatehouseAutomationRuntime.ButtonViewModel;
        public void InitializeNetwork()
        {
            InstallSingleBuildingPauseHook();
            TryRunFeature("troop action HUD coordinator", troopActionHudCoordinator.Initialize);
            try
            {
                // Registration order is a shared protocol boundary. Keep the group fail-closed
                // and identical on every peer instead of skipping individual packet IDs.
                knightDismountRuntime.InitializeNetwork();
                quarryPileRelocationRuntime.InitializeNetwork();
                gatehouseAutomationRuntime.InitializeNetwork();
                singleBuildingPauseHook.InitializeNetwork();
            }
            catch (Exception ex)
            {
                LogFeatureFailure("synchronized action packet group", ex);
            }

            // Local lifecycle setup does not require a functioning custom packet group.
            TryRunFeature("gatehouse automation lifecycle", gatehouseAutomationRuntime.Initialize);
            TryRunFeature("AI defense repair lifecycle", aiDefenseRepairRuntime.Initialize);
        }

        public void InitializeNative(IntPtr newLibraryHandle, ReadOnlySpan<byte> memory, bool isFixedLayoutHashValidated)
        {
            if (nativeLibraryAvailable)
                return;
            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            libraryHandle = newLibraryHandle;
            libraryLength = memory.Length;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;

            if (fixedLayoutHashValidated)
            {
                try
                {
                    quarryPileRelocationRuntime.InstallNativeFunctions(newLibraryHandle, memory, referenceHashMatches: true);
                }
                catch (Exception ex)
                {
                    LogFeatureFailure("quarry-pile relocation native functions", ex);
                }

            }

            try
            {
                churchPriestCountRuntime.InitializeNative(newLibraryHandle, memory, fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                LogFeatureFailure("church priest counts", ex);
            }

            InitializePlagueDurationPatch(newLibraryHandle, memory);
            InitializePlagueApothecarySearchRangePatch(newLibraryHandle, memory);
            InitializeMonkAlwaysRunPatch(newLibraryHandle, memory);
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
                aiDefenseRepairRuntime.InitializeNative(newLibraryHandle, memory, fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                LogFeatureFailure("AI defense rebuild native hook", ex);
            }

            TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);

            // Settings may be restored before LibraryLoaded. Retry activation now that the native
            // library is available instead of waiting for a later setting change.
            ReconcileFixedLayoutFeatures();
        }

        public void ApplySettings()
        {
            TryRunFeature("AI defense repair configuration", ReconcileAIDefenseRepairRuntime);
            if (!settings.EnableMod)
            {
                TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
                TryRunFeature("Vanilla value restoration", RestoreDefaultSettings);
                TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);
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
            TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);
            ApplyMonkAlwaysRunSetting();
            TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
        }

        public void InstallAIEconomyProtectionHook(IntPtr nativeLibraryHandle, ReadOnlySpan<byte> memory)
        {
            if (aiEconomyProtectionHook != null)
                return;

            try
            {
                aiEconomyProtectionHook = new AIEconomyProtectionHook(
                    log, settings, nativeLibraryHandle, memory, fixedLayoutHashValidated);
                singleBuildingPauseHook?.SetSleepStateSynchronizer(aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features AI economy protection hook could not be installed: {ex}");
            }
        }

        public void InstallAIMarketVanillaPriceHook(IntPtr nativeLibraryHandle, ReadOnlySpan<byte> memory)
        {
            if (aiMarketVanillaPriceHook != null)
                return;

            try
            {
                aiMarketVanillaPriceHook = new AIMarketVanillaPriceHook(
                    log, settings, nativeLibraryHandle, memory, fixedLayoutHashValidated);
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
            RestoreCampfirePeasantsCap();
            UnsubscribeHooks();
            aiEconomyProtectionHook?.Dispose();
            aiEconomyProtectionHook = null;
            aiMarketVanillaPriceHook?.Dispose();
            aiMarketVanillaPriceHook = null;
            fastRecruitMovementBridge?.Dispose();
            fastRecruitMovementBridge = null;
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

            if (settingsSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsSubscribed = false;
            }
        }

        private void SubscribeHooks()
        {
            if (!settings.EnableMod || hooksSubscribed)
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
            InstallSingleBuildingPauseHook();
            if (settings.EnableSingleBuildingPause && singleBuildingPauseHook != null)
                TryRunFeature("single-building pause local hooks", singleBuildingPauseHook.InstallLocalHooks);
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
            TryRunFeature("quarry-pile relocation cleanup", quarryPileRelocationRuntime.Dispose);
            // The packet receiver remains alive for registration-order stability, while the
            // reversible local detours are removed when the feature or mod is disabled.
            TryRunFeature("single-building pause cleanup", () =>
            {
                singleBuildingPauseHook?.ClearOverrides("mod disabled");
                singleBuildingPauseHook?.UninstallLocalHooks();
            });
            TryRunFeature("Lord health cleanup", lordHealthRuntime.Dispose);
            ClearResourceEventGuards();
            pendingStockpileRefund = null;
            hooksSubscribed = false;
        }

        private void InstallSingleBuildingPauseHook()
        {
            if (singleBuildingPauseHook != null)
                return;

            try
            {
                singleBuildingPauseHook = new SingleBuildingPauseHook(log, settings, multiplayerFeatureGate);
                if (aiEconomyProtectionHook != null)
                    singleBuildingPauseHook.SetSleepStateSynchronizer(aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features single-building pause hook could not be installed: {ex}");
            }
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
            if (!nativeLibraryAvailable || !settings.EnableMod)
            {
                TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Dispose);
                TryRunFeature("quarry-pile relocation cleanup", quarryPileRelocationRuntime.Dispose);
                return;
            }

            // Knight mount/dismount uses the public bidirectional link API plus a managed,
            // Vanilla-equivalent TotalHorses transition; neither requires a hash-bound delegate.
            if (settings.EnableKnightDismount)
                TryRunFeature("knight mount/dismount", knightDismountRuntime.Initialize);
            else
                TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Dispose);

            if (!fixedLayoutHashValidated)
            {
                if ((settings.EnableQuarryPileRelocation || settings.EnableAIQuarryPileTowardsKeep) &&
                    !quarryFixedLayoutErrorLogged)
                {
                    quarryFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(log, "Extra Features quarry-pile relocation remains inactive because its fixed native layout is not validated for this CrusaderDE.dll.");
                }
                return;
            }

            if (settings.EnableQuarryPileRelocation || settings.EnableAIQuarryPileTowardsKeep)
                TryRunFeature("quarry-pile relocation", quarryPileRelocationRuntime.Initialize);
            else
                TryRunFeature("quarry-pile relocation cleanup", quarryPileRelocationRuntime.Dispose);
        }

        private void ReconcileAIDefenseRepairRuntime()
        {
            aiDefenseRepairRuntime.ReconcileConfiguration();
            if (nativeLibraryAvailable)
            {
                aiDefenseRepairRuntime.InitializeNative(
                    libraryHandle,
                    GetNativeLibraryMemory(),
                    fixedLayoutHashValidated);
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod))
            {
                if (settings.EnableMod)
                {
                    SubscribeHooks();
                    ApplySettings();
                }
                else
                {
                    TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
                    TryRunFeature("Vanilla value restoration", RestoreDefaultSettings);
                    TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);
                    TryRunFeature("Monks Always Run", ApplyMonkAlwaysRunSetting);
                    TryRunFeature("optional local hooks", UnsubscribeHooks);
                }
                return;
            }

            if (!settings.EnableMod)
                return;

            if (propertyName == nameof(ExtraFeaturesViewModel.EnableKnightDismount))
            {
                ReconcileFixedLayoutFeatures();
                TryRunFeature("knight mount/dismount visibility", knightDismountRuntime.RefreshButtonVisibility);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableQuarryPileRelocation) ||
                propertyName == nameof(ExtraFeaturesViewModel.EnableAIQuarryPileTowardsKeep))
            {
                ReconcileFixedLayoutFeatures();
                TryRunFeature("quarry-pile relocation", quarryPileRelocationRuntime.ApplySetting);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableSingleBuildingPause))
            {
                if (settings.EnableSingleBuildingPause && singleBuildingPauseHook != null)
                    TryRunFeature("single-building pause local hooks", singleBuildingPauseHook.InstallLocalHooks);
                else
                {
                    TryRunFeature("single-building pause cleanup", () =>
                    {
                        singleBuildingPauseHook?.ClearOverrides("setting disabled");
                        singleBuildingPauseHook?.UninstallLocalHooks();
                    });
                }
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.HumanGateReopenDelaySeconds) ||
                propertyName == nameof(ExtraFeaturesViewModel.AIGateReopenDelaySeconds) ||
                propertyName == nameof(ExtraFeaturesViewModel.HumanGateClosingDistanceTiles) ||
                propertyName == nameof(ExtraFeaturesViewModel.AIGateClosingDistanceTiles) ||
                propertyName == nameof(ExtraFeaturesViewModel.RequireReachableEnemyForAutomaticGateClosing))
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
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableFastRecruitRallyMovement))
            {
                TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);
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

        private void ApplyMapLoadedSettings()
        {
            mapActive = true;
            TryRunFeature("market price multipliers", ApplyMarketPriceMultipliers);
            TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting);
            TryRunFeature("campfire peasants", ApplyCampfirePeasantsLimit);
            ApplyPlagueDurationSetting();
            ApplyMonkAlwaysRunSetting();
            TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
        }

        private void InitializeMonkAlwaysRunPatch(
            IntPtr nativeLibraryHandle,
            ReadOnlySpan<byte> memory)
        {
            if (monkAlwaysRunPatch != null || monkAlwaysRunPatchUnavailable)
                return;

            try
            {
                monkAlwaysRunPatch = new MonkAlwaysRunPatch(
                    log,
                    nativeLibraryHandle,
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
                settings.EnableMod && settings.EnableMonksAlwaysRun);
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            multiplayerFeatureGate.CaptureMapMode(args.bMultiplayerSave != 0);

            TryRunFeature("Lord health map initialization", ReconcileLordHealthRuntime);

            TryRunFeature("knight mount/dismount visibility", knightDismountRuntime.RefreshButtonVisibility);
            TryRunFeature("quarry-pile relocation visibility", quarryPileRelocationRuntime.RefreshButtonVisibility);
            TryRunFeature("gatehouse map initialization", gatehouseAutomationRuntime.BeginMap);
        }

        private void InitializePlagueDurationPatch(IntPtr nativeLibraryHandle, ReadOnlySpan<byte> memory)
        {
            if (plagueDurationPatch != null || plagueDurationPatchUnavailable)
                return;

            try
            {
                plagueDurationPatch = new PlagueDurationPatch(
                    log, nativeLibraryHandle, memory, fixedLayoutHashValidated);
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
                    settings.EnableMod);
            }
            catch (Exception ex)
            {
                DisablePlagueDurationPatch(ex);
            }
        }

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
            bool enabled = settings.EnableMod &&
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

        private void ApplyFastRecruitRallyMovementSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (!settings.EnableMod || !settings.EnableFastRecruitRallyMovement)
            {
                fastRecruitMovementBridge?.Dispose();
                fastRecruitMovementBridge = null;
                fastRecruitInitializationAttempted = false;
                return;
            }

            if (fastRecruitMovementBridge != null || fastRecruitInitializationAttempted)
                return;

            fastRecruitInitializationAttempted = true;
            FastRecruitMovementBridge bridge = new FastRecruitMovementBridge(log);
            if (bridge.IsActive)
                fastRecruitMovementBridge = bridge;
            else
                bridge.Dispose();
        }

        private unsafe ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL stays loaded for the process lifetime.
            return new ReadOnlySpan<byte>(libraryHandle.ToPointer(), libraryLength);
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            RestoreCampfirePeasantsCap();
            lordHealthRuntime.ResetMapState();
            mapActive = false;
            ClearResourceEventGuards();
            singleBuildingPauseHook?.ClearOverrides("map unload");
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
