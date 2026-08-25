// Feature: Lifecycle orchestration for the Bugfixes and QoL features.
using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using System;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly TroopMovementFix3Runtime troopMovementFixRuntime;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly Shared.TroopActionHudCoordinator troopActionHudCoordinator;
        private readonly AssassinClimbRuntime assassinClimbRuntime;
        private readonly AssassinClimbCancellationRuntime assassinClimbCancellationRuntime;
        private readonly AssassinPathfindingRuntime assassinPathfindingRuntime;
        private readonly MultiplayerGameSpeedRuntime multiplayerGameSpeedRuntime;
        private readonly MultiplayerAivSyncRuntime multiplayerAivSyncRuntime;
        private readonly SiegeAmmoRestockFeature siegeAmmoRestockFeature;
        private IDisposable playerMarketSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private MinimapPlacementClickHook minimapPlacementClickHook;
        private SkirmishAiSelectionMemoryHook skirmishAiSelectionMemoryHook;
        private CustomLordListEnhancementHook customLordListEnhancementHook;
        private AiCastleSettingsListEnhancementHook aiCastleSettingsListEnhancementHook;
        private MapOriginSortHook mapOriginSortHook;
        private AutoTradeSellZeroHook autoTradeSellZeroHook;
        private EnemyProximityBulldozeCursorHook enemyProximityBulldozeCursorHook;
        private MarketKeyMainTradeMenuHook marketKeyMainTradeMenuHook;
        private HdMarketViewHook hdMarketViewHook;
        private CameraMovementModifierHook cameraMovementModifierHook;
        private CustomTrailExtremeGoldFixHook customTrailExtremeGoldFixHook;
        private ResyncHostKickFeature resyncHostKickFeature;
        private SurrenderFeature surrenderFeature;
        private LordUnitControlsFeature lordUnitControlsFeature;
        private SelectedUnitHealthFeature selectedUnitHealthFeature;
        private AssemblyPointPlacementPatch assemblyPointPlacementPatch;
        private AiRecruitmentHorseDemandFix aiRecruitmentHorseDemandFix;
        private AiStoneReserveFix aiStoneReserveFix;
        private AITowerRuinRepairFix aiTowerRuinRepairFix;
        private PlaguePopularityFix plaguePopularityFix;
        private PlagueTreatmentFadeFix plagueTreatmentFadeFix;
        private PlagueTargetReservationFix plagueTargetReservationFix;
        private PlagueApothecaryStateTransitionFix plagueApothecaryStateTransitionFix;
        private AllyGoodsAmountModifierHook allyGoodsAmountModifierHook;
        private CtrlMarketTradeHook ctrlMarketTradeHook;
        private IntPtr libraryHandle;
        private int libraryLength;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool settingsSubscribed;
        private bool enemyProximityFixedLayoutErrorLogged;
        private bool assemblyPointPlacementPatchUnavailable;
        private bool aiRecruitmentHorseDemandFixUnavailable;
        private bool aiStoneReserveFixUnavailable;
        private bool aiTowerRuinRepairFixUnavailable;
        private bool plaguePopularityFixUnavailable;
        private bool plagueTreatmentFadeFixUnavailable;
        private bool plagueTargetReservationFixUnavailable;
        private bool plagueApothecaryStateTransitionFixUnavailable;
        private bool ctrlMarketTradeHookUnavailable;

        public BugfixesAndQoLRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            troopMovementFixRuntime = new TroopMovementFix3Runtime(log, settings);
            multiplayerFeatureGate = new MultiplayerFeatureGate(log);
            troopActionHudCoordinator = new Shared.TroopActionHudCoordinator(log);
            assassinClimbRuntime = new AssassinClimbRuntime(log, settings, multiplayerFeatureGate);
            assassinClimbCancellationRuntime = new AssassinClimbCancellationRuntime(log, settings);
            assassinPathfindingRuntime = new AssassinPathfindingRuntime(log, settings, assassinClimbRuntime);
            troopActionHudCoordinator.Register(assassinClimbRuntime.RefreshButtonVisibility);
            multiplayerGameSpeedRuntime = new MultiplayerGameSpeedRuntime(log, settings, multiplayerFeatureGate);
            multiplayerAivSyncRuntime = new MultiplayerAivSyncRuntime(log, settings);
            siegeAmmoRestockFeature = new SiegeAmmoRestockFeature(log, settings, multiplayerFeatureGate);
            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        public object SurrenderAndStatisticsUi => surrenderFeature?.ButtonViewModel;
        public object SelectedUnitHealthUi => selectedUnitHealthFeature?.ViewModel;
        public object AllyGoodsAmountDisplay => allyGoodsAmountModifierHook;
        public object MultiplayerAivSyncUi => multiplayerAivSyncRuntime;
        public object AssassinClimbButton => assassinClimbRuntime.ButtonViewModel;

        public void InitializeNetwork()
        {
            TryInitializePersistentFeature(
                "Assassin climb-state synchronization",
                assassinClimbRuntime.InitializeNetwork);
            TryInitializePersistentFeature(
                "troop action HUD coordinator",
                troopActionHudCoordinator.Initialize);
            // These registrations serve independent features. Keep each failure isolated so a
            // managed speed-UI hook can never disable Ctrl trading or map-state maintenance.
            TryInitializePersistentFeature(
                "multiplayer AIV synchronization",
                multiplayerAivSyncRuntime.Initialize);
            TryInitializePersistentFeature(
                "multiplayer game-speed packet",
                multiplayerGameSpeedRuntime.InitializeNetwork);
            TryInitializePersistentFeature(
                "multiplayer game-speed managed hooks",
                multiplayerGameSpeedRuntime.InstallHooks);
            TryInitializePersistentFeature(
                "fair siege-ammunition restock",
                siegeAmmoRestockFeature.Initialize);

            if (playerMarketSubscription == null)
            {
                TryInitializePersistentFeature(
                    "Ctrl market interaction subscription",
                    () => playerMarketSubscription =
                        PlayerR3EventHooks.OnPlayerMarketInteraction.Observable.Subscribe(OnPlayerMarketInteraction));
            }

            if (mapStartSubscription == null)
            {
                TryInitializePersistentFeature(
                    "multiplayer map-start subscription",
                    () => mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                        .Where(args => args.Phase == EventHookPhase.Post)
                        .Subscribe(args =>
                        {
                            multiplayerFeatureGate.CaptureMapMode(args.bMultiplayerSave != 0);
                            assassinPathfindingRuntime.BeginMap();
                            assassinClimbRuntime.BeginMap();
                            multiplayerGameSpeedRuntime.ApplySetting();
                        }));
            }

            if (mapUnloadSubscription == null)
            {
                TryInitializePersistentFeature(
                    "multiplayer map-unload subscription",
                    () => mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                        .Where(args => args.Phase == EventHookPhase.Post)
                        .Subscribe(_ =>
                        {
                            multiplayerGameSpeedRuntime.ResetMapState();
                            assassinClimbRuntime.EndMap();
                            assassinPathfindingRuntime.EndMap();
                            multiplayerFeatureGate.Reset();
                        }));
            }
        }

        public void InitializeSelectedUnitHealthFeature()
        {
            if (selectedUnitHealthFeature != null)
                return;

            selectedUnitHealthFeature = new SelectedUnitHealthFeature(
                log,
                settings,
                () => lordUnitControlsFeature?.IsLordModeActive == true,
                () => lordUnitControlsFeature?.ActiveLordPlayerId ?? -1);
            selectedUnitHealthFeature.RefreshSetting();
        }

        public void InitializeSurrenderFeature()
        {
            if (surrenderFeature == null)
            {
                surrenderFeature = new SurrenderFeature(log, settings);
                surrenderFeature.Initialize();
            }

            if (lordUnitControlsFeature == null)
                lordUnitControlsFeature = new LordUnitControlsFeature(log, settings, surrenderFeature);
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
            try
            {
                assassinClimbCancellationRuntime.InitializeNative(
                    newLibraryHandle,
                    memory,
                    isFixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL Assassin climb cancellation could not be initialized and remains inactive: {ex}");
            }

            if (isFixedLayoutHashValidated)
            {
                try
                {
                    assassinPathfindingRuntime.InitializeNative(
                        newLibraryHandle,
                        memory,
                        fixedLayoutHashValidated: true);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL weighted Assassin pathfinding could not be initialized and remains inactive: {ex}");
                }
            }
            else if (settings.EnableMod && settings.EnableImprovedAssassinPathfinding)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Bugfixes and QoL weighted Assassin pathfinding remains inactive because the fixed native layout is not validated for this CrusaderDE.dll; Vanilla pathfinding remains active.");
            }

            // Every native feature has its own compatibility surface. A changed signature in
            // one feature must not prevent unrelated fixes from installing.
            try
            {
                troopMovementFixRuntime.InitializeNative(newLibraryHandle, memory, isFixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL feature 'troop movement fix' could not be initialized and remains inactive: {ex}");
            }
            TryInitializeFeature("plague popularity fix", EnsurePlaguePopularityFix);
            TryInitializeFeature("plague cloud removal fix", EnsurePlagueTreatmentFadeFix);
            TryInitializeFeature("plague target-reservation fix", EnsurePlagueTargetReservationFix);
            TryInitializeFeature("stuck-apothecary fix", EnsurePlagueApothecaryStateTransitionFix);
            TryInitializeFeature("assembly-point placement fix", ApplyAssemblyPointPlacementPatchSetting);
            TryInitializeFeature("AI recruitment horse-demand fix", EnsureAiRecruitmentHorseDemandFix);
            TryInitializeFeature("AI stone-reserve fix", EnsureAiStoneReserveFix);
            TryInitializeFeature("AI tower-ruin repair fix", EnsureAiTowerRuinRepairFix);
            TryInitializeFeature("ally goods amount modifiers", InstallAllyGoodsAmountModifierHook);
            TryInitializeFeature("Ctrl single-unit market trade", InstallCtrlMarketTradeHook);
        }

        public void ApplySettings()
        {
            TryInitializeFeature("AI tower-ruin repair fix", EnsureAiTowerRuinRepairFix);
            TryInitializeFeature("surrender", InitializeSurrenderFeature);
            TryInitializeFeature("selected-unit health display", InitializeSelectedUnitHealthFeature);
            TryApplyFeature("selected-unit health display", () => selectedUnitHealthFeature?.RefreshSetting());
            TryApplyFeature("surrender", () => surrenderFeature?.RefreshButtonState());
            TryInitializeFeature("resync host kick", EnsureResyncHostKickFeature);
            TryInitializeFeature("AI castle/settings selection memory", EnsureAiSelectionHook);
            TryApplyFeature("AI castle/settings selection memory", () => skirmishAiSelectionMemoryHook?.ApplySetting());
            TryInitializeFeature("custom-lord list enhancements", EnsureCustomLordListEnhancementHook);
            TryApplyFeature("custom-lord list enhancements", () => customLordListEnhancementHook?.ApplySetting());
            TryInitializeFeature("AI castle/settings list enhancements", EnsureAiCastleSettingsListEnhancementHook);
            TryApplyFeature("AI castle/settings list enhancements", () => aiCastleSettingsListEnhancementHook?.ApplySetting());
            TryInitializeFeature("map-origin sorting", EnsureMapOriginSortHook);
            TryApplyFeature("troop movement fix", troopMovementFixRuntime.ApplySetting);
            TryApplyFeature("multiplayer game speed", multiplayerGameSpeedRuntime.ApplySetting);
            TryApplyFeature("assembly-point placement fix", ApplyAssemblyPointPlacementPatchSetting);
            TryApplyFeature("AI stone-reserve fix", () => aiStoneReserveFix?.ApplySetting());
            if (settings.EnableMod && settings.EnableImprovedAssassinPathfinding && assassinPathfindingRuntime.IsInstalled)
                TryApplyFeature("Assassin climb button", assassinClimbRuntime.Initialize);
            else
                TryApplyFeature("Assassin climb button cleanup", assassinClimbRuntime.Dispose);

            if (settings.EnableClientFeatures)
                SubscribeHooks();
            else
                UnsubscribeHooks();
        }

        public void Dispose()
        {
            UnsubscribeHooks();
            skirmishAiSelectionMemoryHook?.Dispose();
            skirmishAiSelectionMemoryHook = null;
            customLordListEnhancementHook?.Dispose();
            customLordListEnhancementHook = null;
            aiCastleSettingsListEnhancementHook?.Dispose();
            aiCastleSettingsListEnhancementHook = null;
            mapOriginSortHook?.Dispose();
            mapOriginSortHook = null;
            resyncHostKickFeature?.Dispose();
            resyncHostKickFeature = null;
            lordUnitControlsFeature?.Dispose();
            lordUnitControlsFeature = null;
            surrenderFeature?.Dispose();
            surrenderFeature = null;
            selectedUnitHealthFeature?.Dispose();
            selectedUnitHealthFeature = null;
            DisableAssemblyPointPlacementPatch();
            aiRecruitmentHorseDemandFix?.Dispose();
            aiRecruitmentHorseDemandFix = null;
            aiStoneReserveFix?.Dispose();
            aiStoneReserveFix = null;
            aiTowerRuinRepairFix?.Dispose();
            aiTowerRuinRepairFix = null;
            plaguePopularityFix?.Dispose();
            plaguePopularityFix = null;
            plagueTreatmentFadeFix?.SetTreatmentCompletedObserver(null);
            plagueTargetReservationFix?.Dispose();
            plagueTargetReservationFix = null;
            plagueTreatmentFadeFix?.Dispose();
            plagueTreatmentFadeFix = null;
            plagueApothecaryStateTransitionFix?.Dispose();
            plagueApothecaryStateTransitionFix = null;
            troopMovementFixRuntime.Dispose();
            troopActionHudCoordinator.Dispose();
            assassinClimbRuntime.Dispose();
            assassinClimbCancellationRuntime.Dispose();
            ctrlMarketTradeHook?.Dispose();
            ctrlMarketTradeHook = null;
            allyGoodsAmountModifierHook?.Dispose();
            allyGoodsAmountModifierHook = null;
            multiplayerGameSpeedRuntime.Dispose();
            multiplayerAivSyncRuntime.Dispose();
            siegeAmmoRestockFeature.Dispose();
            playerMarketSubscription?.Dispose();
            playerMarketSubscription = null;
            mapStartSubscription?.Dispose();
            mapStartSubscription = null;
            mapUnloadSubscription?.Dispose();
            mapUnloadSubscription = null;
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
            if (!settings.EnableClientFeatures)
                return;

            ReconcileClientHook(
                "minimap improvements",
                settings.EnableMinimapCursorFollowFix || settings.AllowMinimapWhilePlacingBuilding,
                () => minimapPlacementClickHook != null,
                () => minimapPlacementClickHook = new MinimapPlacementClickHook(log, settings),
                () => DisposeFeature("minimap improvements", ref minimapPlacementClickHook));
            ReconcileClientHook(
                "market autotrade sell threshold",
                settings.EnableAutoTradeSellZeroFix,
                () => autoTradeSellZeroHook != null,
                () => autoTradeSellZeroHook = new AutoTradeSellZeroHook(log, settings),
                () => DisposeFeature("market autotrade sell threshold", ref autoTradeSellZeroHook));
            ReconcileClientHook(
                "market key main-menu return",
                settings.EnableMarketKeyMainMenuFix,
                () => marketKeyMainTradeMenuHook != null,
                () => marketKeyMainTradeMenuHook = new MarketKeyMainTradeMenuHook(log, settings),
                () => DisposeFeature("market key main-menu return", ref marketKeyMainTradeMenuHook));
            ReconcileClientHook(
                "HD market view",
                settings.HdMarketView,
                () => hdMarketViewHook != null,
                () => hdMarketViewHook = new HdMarketViewHook(log, settings),
                () => DisposeFeature("HD market view", ref hdMarketViewHook));
            ReconcileClientHook(
                "camera movement modifier",
                settings.AllowCameraMovementWithModifiers,
                () => cameraMovementModifierHook != null,
                () => cameraMovementModifierHook = new CameraMovementModifierHook(log, settings),
                () => DisposeFeature("camera movement modifier", ref cameraMovementModifierHook));
            ReconcileClientHook(
                "Custom Trail starting-gold fix",
                settings.EnableCustomTrailExtremeGoldFix,
                () => customTrailExtremeGoldFixHook != null,
                () => customTrailExtremeGoldFixHook = new CustomTrailExtremeGoldFixHook(log, settings),
                () => DisposeFeature("Custom Trail starting-gold fix", ref customTrailExtremeGoldFixHook));

            if (nativeLibraryAvailable && fixedLayoutHashValidated && settings.EnableEnemyProximityBulldozeCursorFix)
            {
                if (enemyProximityBulldozeCursorHook == null)
                {
                    TryInitializeFeature("enemy-proximity bulldoze cursor", () =>
                        enemyProximityBulldozeCursorHook = new EnemyProximityBulldozeCursorHook(log, settings));
                }
            }
            else
            {
                DisposeFeature("enemy-proximity bulldoze cursor", ref enemyProximityBulldozeCursorHook);
                if (StartupDiagnosticPolicy.ShouldReportFixedLayoutFailure(
                    nativeLibraryAvailable,
                    fixedLayoutHashValidated,
                    settings.EnableEnemyProximityBulldozeCursorFix,
                    enemyProximityFixedLayoutErrorLogged))
                {
                    enemyProximityFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Bugfixes and QoL enemy-proximity bulldoze cursor remains inactive because its fixed native layout is not validated for this CrusaderDE.dll.");
                }
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL feature hooks reconciled.");
        }

        private void UnsubscribeHooks()
        {
            DisposeFeature("minimap improvements", ref minimapPlacementClickHook);
            DisposeFeature("market autotrade sell threshold", ref autoTradeSellZeroHook);
            DisposeFeature("enemy-proximity bulldoze cursor", ref enemyProximityBulldozeCursorHook);
            DisposeFeature("market key main-menu return", ref marketKeyMainTradeMenuHook);
            DisposeFeature("HD market view", ref hdMarketViewHook);
            DisposeFeature("camera movement modifier", ref cameraMovementModifierHook);
            DisposeFeature("Custom Trail starting-gold fix", ref customTrailExtremeGoldFixHook);
        }

        private void ReconcileClientHook(
            string featureName,
            bool enabled,
            Func<bool> isInstalled,
            Action install,
            Action uninstall)
        {
            if (enabled && !isInstalled())
                TryInitializeFeature(featureName, install);
            else if (!enabled)
                uninstall();
        }

        private void DisposeFeature<T>(string featureName, ref T feature) where T : class, IDisposable
        {
            T current = feature;
            feature = null;
            if (current == null)
                return;

            try
            {
                current.Dispose();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL feature '{featureName}' cleanup failed; other features continue: {ex}");
            }
        }

        private void EnsureAiSelectionHook()
        {
            if (skirmishAiSelectionMemoryHook == null)
                skirmishAiSelectionMemoryHook = new SkirmishAiSelectionMemoryHook(log, settings);
        }

        private void EnsureCustomLordListEnhancementHook()
        {
            if (customLordListEnhancementHook == null)
                customLordListEnhancementHook = new CustomLordListEnhancementHook(log, settings);
        }

        private void EnsureAiCastleSettingsListEnhancementHook()
        {
            if (aiCastleSettingsListEnhancementHook == null)
                aiCastleSettingsListEnhancementHook =
                    new AiCastleSettingsListEnhancementHook(
                        log,
                        settings,
                        info => skirmishAiSelectionMemoryHook?.RecordSelection(info));
        }

        private void EnsureMapOriginSortHook()
        {
            if (mapOriginSortHook == null)
                mapOriginSortHook = new MapOriginSortHook(log, settings);
        }

        private void EnsureResyncHostKickFeature()
        {
            if (resyncHostKickFeature == null)
                resyncHostKickFeature = new ResyncHostKickFeature(log, settings);
        }

        private void InstallAllyGoodsAmountModifierHook()
        {
            if (allyGoodsAmountModifierHook != null)
                return;

            try
            {
                allyGoodsAmountModifierHook = new AllyGoodsAmountModifierHook(log, settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL ally goods amount modifier hook could not be installed: {ex}");
            }
        }

        private void InstallCtrlMarketTradeHook()
        {
            if (ctrlMarketTradeHook != null || ctrlMarketTradeHookUnavailable || !nativeLibraryAvailable)
                return;

            try
            {
                ctrlMarketTradeHook = new CtrlMarketTradeHook(
                    log,
                    settings,
                    libraryHandle,
                    GetNativeLibraryMemory(),
                    fixedLayoutHashValidated);
                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Bugfixes and QoL Ctrl single-unit market hooks are running on an unknown CrusaderDE.dll because all required native instruction patterns were validated.");
                }
            }
            catch (Exception ex)
            {
                ctrlMarketTradeHookUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL Ctrl single-unit market hooks could not be installed: {ex}");
            }
        }

        private void TryInitializeFeature(string featureName, Action initialize)
        {
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL feature '{featureName}' could not be initialized and remains inactive: {ex}");
            }
        }

        private void TryInitializePersistentFeature(string featureName, Action initialize)
        {
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL persistent feature '{featureName}' could not be initialized; independent features continue: {ex}");
            }
        }

        private void TryApplyFeature(string featureName, Action apply)
        {
            try
            {
                apply();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL feature '{featureName}' could not apply its setting; independent features continue: {ex}");
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            // The installed market hook reads this local order directly; no hooks need reconciliation.
            if (propertyName == nameof(BugfixesAndQoLViewModel.MarketGoodsOrder))
                return;

            TryApplyFeature("plague target-reservation fix", () => plagueTargetReservationFix?.ApplySetting());
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableTroopMovementFix))
            {
                TryApplyFeature("troop movement fix", troopMovementFixRuntime.ApplySetting);
                return;
            }

            ApplySettings();
        }

        private void ApplyAssemblyPointPlacementPatchSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (settings.EnableMod && settings.EnableAssemblyPointPlacementFix)
                InstallAssemblyPointPlacementPatch();
            else
                DisableAssemblyPointPlacementPatch();
        }

        private unsafe ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL stays loaded for the process lifetime.
            return new ReadOnlySpan<byte>(libraryHandle.ToPointer(), libraryLength);
        }

        private void EnsurePlaguePopularityFix()
        {
            if (!nativeLibraryAvailable || plaguePopularityFix != null || plaguePopularityFixUnavailable)
                return;

            try
            {
                // This hook remains installed while disabled so an in-progress herd can
                // still be identified if the host enables the setting later.
                plaguePopularityFix = new PlaguePopularityFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                plaguePopularityFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL plague popularity fix could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureAiRecruitmentHorseDemandFix()
        {
            if (!nativeLibraryAvailable ||
                aiRecruitmentHorseDemandFix != null ||
                aiRecruitmentHorseDemandFixUnavailable)
            {
                return;
            }

            try
            {
                // Keep the hook installed: synchronized setting changes only select whether
                // its callback corrects the stale Vanilla output before recruitment.
                aiRecruitmentHorseDemandFix = new AiRecruitmentHorseDemandFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                aiRecruitmentHorseDemandFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI recruitment horse-demand fix could not be installed; " +
                    $"only this AI fix remains inactive and Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureAiStoneReserveFix()
        {
            if (!nativeLibraryAvailable || aiStoneReserveFix != null || aiStoneReserveFixUnavailable)
                return;

            try
            {
                // The hook object remains available for synchronized setting changes, but
                // ApplySetting physically restores Vanilla bytes while either switch is off.
                aiStoneReserveFix = new AiStoneReserveFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                aiStoneReserveFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI stone-reserve fix could not be installed; " +
                    $"only this AI fix remains inactive and Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureAiTowerRuinRepairFix()
        {
            if (aiTowerRuinRepairFix != null)
            {
                aiTowerRuinRepairFix.ApplySetting();
                return;
            }
            if (!nativeLibraryAvailable || aiTowerRuinRepairFixUnavailable)
                return;
            // Avoid installing the native classifiers in Vanilla mode. If the setting is enabled
            // later, ApplySettings retries this method with the retained DLL mapping.
            if (!settings.EnableMod || !settings.EnableAiFixes || !settings.FixAITowerRepair)
                return;

            try
            {
                aiTowerRuinRepairFix = new AITowerRuinRepairFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                aiTowerRuinRepairFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI tower-ruin repair could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsurePlagueTreatmentFadeFix()
        {
            if (!nativeLibraryAvailable || plagueTreatmentFadeFix != null || plagueTreatmentFadeFixUnavailable)
                return;

            try
            {
                plagueTreatmentFadeFix = new PlagueTreatmentFadeFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                plagueTreatmentFadeFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL plague-cloud removal fix could not be installed; " +
                    $"only this fix remains inactive and Vanilla treatment remains active: {ex}");
            }
        }

        private void EnsurePlagueApothecaryStateTransitionFix()
        {
            if (!nativeLibraryAvailable ||
                plagueApothecaryStateTransitionFix != null ||
                plagueApothecaryStateTransitionFixUnavailable)
            {
                return;
            }

            try
            {
                plagueApothecaryStateTransitionFix = new PlagueApothecaryStateTransitionFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                plagueApothecaryStateTransitionFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL stuck-apothecary fix could not be installed; " +
                    $"only this fix remains inactive and Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsurePlagueTargetReservationFix()
        {
            if (!nativeLibraryAvailable ||
                plagueTargetReservationFix != null ||
                plagueTargetReservationFixUnavailable)
            {
                return;
            }

            if (plagueTreatmentFadeFix == null)
            {
                plagueTargetReservationFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Bugfixes and QoL plague target-reservation fix remains inactive because " +
                    "the treatment-completion hook is unavailable; other plague fixes remain independent.");
                return;
            }

            try
            {
                plagueTargetReservationFix = new PlagueTargetReservationFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
                plagueTreatmentFadeFix.SetTreatmentCompletedObserver(
                    plagueTargetReservationFix.OnTreatmentCompleted);
                plagueTargetReservationFix.ApplySetting();
            }
            catch (Exception ex)
            {
                plagueTargetReservationFix?.Dispose();
                plagueTargetReservationFix = null;
                plagueTargetReservationFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL plague target-reservation fix could not be installed; " +
                    $"only this fix remains inactive and Vanilla target selection remains active: {ex}");
            }
        }

        private void InstallAssemblyPointPlacementPatch()
        {
            if (assemblyPointPlacementPatch != null || assemblyPointPlacementPatchUnavailable)
                return;

            try
            {
                assemblyPointPlacementPatch = new AssemblyPointPlacementPatch(
                    log,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                // A changed or already hooked signature cannot become valid later in this process.
                assemblyPointPlacementPatchUnavailable = true;
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL assembly-point placement patch could not be installed: {ex}");
            }
        }

        private void DisableAssemblyPointPlacementPatch()
        {
            assemblyPointPlacementPatch?.Dispose();
            assemblyPointPlacementPatch = null;
        }
    }
}
