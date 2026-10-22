// Feature: Lifecycle orchestration for the Bugfixes and QoL features.
using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.API.LowLevel;
using SHCDESE.API;
using RedBird.Core.Memory;
using System;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly TroopMovementFix3Runtime troopMovementFixRuntime;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly AssassinClimbRuntime assassinClimbRuntime;
        private readonly AssassinClimbCancellationRuntime assassinClimbCancellationRuntime;
        private readonly AssassinPathfindingRuntime assassinPathfindingRuntime;
        private readonly AssassinCombatResumeRuntime assassinCombatResumeRuntime;
        private readonly AssassinCapturedGateProtectionRuntime assassinCapturedGateProtectionRuntime;
        private readonly MultiplayerGameSpeedRuntime multiplayerGameSpeedRuntime;
        private readonly MultiplayerAivSyncRuntime multiplayerAivSyncRuntime;
        private readonly SiegeAmmoRestockFeature siegeAmmoRestockFeature;
        private readonly TroopHudMiddleClickCameraFeature troopHudMiddleClickCameraFeature;
        private readonly LordUnitHudRegistration lordUnitHudRegistration;
        private readonly GatehouseDistanceOriginRegistration gatehouseDistanceOriginRegistration;
        private readonly TunnelPlacementDistanceFeature tunnelPlacementDistanceFeature;
        private readonly TrailCustomizationFeature trailCustomizationFeature;
        private readonly CoopCustomLordSelectionFeature coopCustomLordSelectionFeature;
        private static PlacementCancelMoveSuppressionFeature processPlacementCancelMoveSuppressionFeature;
        private static LobbyYellowContrastFeature processLobbyYellowContrastFeature;
        private static BriefingNoStartingGoldFixRegistration processBriefingNoStartingGoldFixRegistration;
        private ExtendedShiftCommandQueueRuntime extendedShiftCommandQueueRuntime;
        private static ExtendedShiftCommandQueueRuntime processExtendedShiftCommandQueueRuntime;
        private IDisposable playerMarketSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private MinimapInputFeature minimapInputFeature;
        private SkirmishAiSelectionMemoryHook skirmishAiSelectionMemoryHook;
        private CustomLordListEnhancementHook customLordListEnhancementHook;
        private AiCastleSettingsListEnhancementHook aiCastleSettingsListEnhancementHook;
        private MapOriginSortHook mapOriginSortHook;
        private VanillaMapEditorHook vanillaMapEditorHook;
        private AutoTradeSellZeroHook autoTradeSellZeroHook;
        private EnemyProximityBulldozeCursorHook enemyProximityBulldozeCursorHook;
        private MarketKeyMainTradeMenuHook marketKeyMainTradeMenuHook;
        private HdMarketViewHook hdMarketViewHook;
        private CameraMovementModifierHook cameraMovementModifierHook;
        private CustomTrailExtremeGoldFixHook customTrailExtremeGoldFixHook;
        private AbruptHostMigrationFix abruptHostMigrationFix;
        private ResyncHostKickFeature resyncHostKickFeature;
        private SurrenderFeature surrenderFeature;
        private LordUnitControlsFeature lordUnitControlsFeature;
        private LordControlGroupNativePatch lordControlGroupNativePatch;
        private ControlGroupDisbandCleanupRuntime controlGroupDisbandCleanupRuntime;
        private SelectedUnitHealthFeature selectedUnitHealthFeature;
        private AssemblyPointPlacementPatch assemblyPointPlacementPatch;
        private HealerAttackCommandPatch healerAttackCommandPatch;
        private MountedStockpileMovementPatch mountedStockpileMovementPatch;
        private ShiftRepairAllBuildingsHook shiftRepairAllBuildingsHook;
        private AiStoneReserveFix aiStoneReserveFix;
        private AiDefensePatrolFix aiDefensePatrolFix;
        private AiWallTargetingFix aiWallTargetingFix;
        private AivDefenderPositionFix aivDefenderPositionFix;
        private AITowerRuinRepairFix aiTowerRuinRepairFix;
        private BetterAIOverbuildRulesFix betterAIOverbuildRulesFix;
        private PlaguePopularityFix plaguePopularityFix;
        private PlagueTreatmentFadeFix plagueTreatmentFadeFix;
        private PlagueTargetReservationFix plagueTargetReservationFix;
        private PlagueApothecaryStateTransitionFix plagueApothecaryStateTransitionFix;
        private UnitCommandPathRuntime friendlyMoatMovementRuntime;
        private static UnitCommandPathRuntime processUnitCommandPathRuntime;
        private static AllyGoodsAmountModifierHook processAllyGoodsAmountModifierHook;
        private static WorkshopUploadLordSelectionFix processWorkshopUploadLordSelectionFix;
        private static KeepFlagRotationRuntime processKeepFlagRotationRuntime;
        private static CorruptLordDataSpawnRuntime processCorruptLordDataSpawnRuntime;
        private static PrebuiltAiWorkshopBothFixRuntime processPrebuiltAiWorkshopBothFixRuntime;
        private static AIPreplacedBuildingFixRuntime processAIPreplacedBuildingFixRuntime;
        private static AIKeepRangeRuntime processAIKeepRangeRuntime;

        internal static bool TryGetLobbyAIKeepRangeBypass(out bool bypass)
        {
            bypass = processAIKeepRangeRuntime?.LobbyBypass == true;
            return processAIKeepRangeRuntime != null;
        }
        private bool aiKeepRangeInitializationAttempted;
        private static WorkerBreakPauseHook processWorkerBreakPauseHook;
        private static WorkshopIdleDelayHook processWorkshopIdleDelayHook;
        private static DrawbridgeDestructionFix.FixRuntime processDrawbridgeDestructionFixRuntime;
        private static bool drawbridgeDestructionFixInitializationAttempted;
        private static GatehouseLivingCapture.CaptureRuntime processGatehouseLivingCaptureRuntime;
        private static bool gatehouseLivingCaptureInitializationAttempted;
        private static AiRaidRetargetFixRuntime processAiRaidRetargetFixRuntime;
        private static APIShared.Economy.AiMarketSellRegistration processAiSellThresholdPolicy;
        private static bool aiRaidRetargetInitializationAttempted;
        private static bool workshopIdleTickSubscribed, workshopIdleTickLogged;
        private static NativeTannerFade processNativeTannerFade;
        private static WaterboyTargetReservationRuntime processWaterboyTargetReservationRuntime;
        private static NotificationLastFrameFeature processNotificationLastFrameFeature;
        private static NotificationSpeechPlayback processNotificationSpeechPlayback;
        private static bool workerBreakTickSubscribed;
        private static bool workerBreakTickLogged;

        public static bool HasActiveAiEconomyOverlay =>
            processAIPreplacedBuildingFixRuntime?.HasActiveEconomyOverlay == true;

        public static void NotifyAiPathComponentGridRebuilt() =>
            processAIPreplacedBuildingFixRuntime?.OnPathComponentGridRebuilt();

        // Optional, process-lifetime coordination for the separate coarse-grid test mod.
        public static event Action AiEconomyOverlayRestored;

        internal static void NotifyAiEconomyOverlayRestored() =>
            AiEconomyOverlayRestored?.Invoke();

        private CtrlMarketTradeHook ctrlMarketTradeHook;
        private NotificationSkipFeature notificationSkipFeature;
        private IntPtr libraryHandle;
        private int libraryLength;
        private ScanRegion nativeRegion;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool settingsSubscribed;
        private bool enemyProximityFixedLayoutErrorLogged;
        private bool assemblyPointPlacementPatchUnavailable;
        private bool healerAttackCommandPatchUnavailable;
        private bool mountedStockpileMovementPatchUnavailable;
        private bool lordControlGroupNativePatchUnavailable;
        private bool controlGroupDisbandCleanupUnavailable;
        private bool lordMixedDisbandContractValidated;
        private bool aiStoneReserveFixUnavailable;
        private bool aiDefensePatrolFixUnavailable;
        private bool aiWallTargetingFixUnavailable;
        private bool workerBreakPauseHookUnavailable;
        private bool workshopIdleDelayHookUnavailable;
        private bool aivDefenderPositionFixUnavailable;
        private bool aiTowerRuinRepairFixUnavailable;
        private bool betterAIOverbuildRulesFixUnavailable;
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
            assassinClimbRuntime = new AssassinClimbRuntime(log, settings, multiplayerFeatureGate);
            assassinClimbCancellationRuntime = new AssassinClimbCancellationRuntime(log, settings);
            assassinPathfindingRuntime = new AssassinPathfindingRuntime(log, settings, assassinClimbRuntime);
            assassinCombatResumeRuntime = new AssassinCombatResumeRuntime(log, settings);
            assassinCapturedGateProtectionRuntime = new AssassinCapturedGateProtectionRuntime(log, settings);
            multiplayerGameSpeedRuntime = new MultiplayerGameSpeedRuntime(log, settings, multiplayerFeatureGate);
            multiplayerAivSyncRuntime = new MultiplayerAivSyncRuntime(log, settings);
            siegeAmmoRestockFeature = new SiegeAmmoRestockFeature(log, settings, multiplayerFeatureGate);
            troopHudMiddleClickCameraFeature = new TroopHudMiddleClickCameraFeature(log, settings);
            lordUnitHudRegistration = new LordUnitHudRegistration(log, settings, troopHudMiddleClickCameraFeature);
            gatehouseDistanceOriginRegistration = new GatehouseDistanceOriginRegistration(log, settings);
            tunnelPlacementDistanceFeature = new TunnelPlacementDistanceFeature(log, settings);
            trailCustomizationFeature = new TrailCustomizationFeature(log, settings);
            coopCustomLordSelectionFeature = new CoopCustomLordSelectionFeature(log, settings);
            EnsureBriefingNoStartingGoldFixRegistration();
            InitializeMovedFeatures();
            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        public object SurrenderAndStatisticsUi => surrenderFeature?.ButtonViewModel;
        public object SelectedUnitHealthUi => selectedUnitHealthFeature?.ViewModel;
        public object AllyGoodsAmountDisplay => processAllyGoodsAmountModifierHook;
        public object MultiplayerAivSyncUi => multiplayerAivSyncRuntime;
        public object AssassinClimbButton => assassinClimbRuntime.ButtonViewModel;

        private bool MissingMapRecoveryEnabled => settings.EnableClientFeatures && settings.EnableMissingMapSaveRecovery;

        public void InitializeNetwork()
        {
            SavegameMissingMap.SavegameMissingMapFeature.Initialize(log, () => MissingMapRecoveryEnabled);
            TryInitializePersistentFeature(
                "placement-cancel move suppression",
                EnsurePlacementCancelMoveSuppressionFeature);
            TryInitializePersistentFeature(
                "Coop custom-lord selection",
                coopCustomLordSelectionFeature.Initialize);
            TryInitializePersistentFeature(
                "Trail Customize buttons",
                trailCustomizationFeature.Initialize);
            TryInitializePersistentFeature(
                "Shift-repair all buildings",
                EnsureShiftRepairAllBuildingsHook);
            TryInitializePersistentFeature(
                "moved synchronized feature group",
                InitializeMovedFeatureNetwork);
            TryInitializePersistentFeature(
                "Assassin climb-state synchronization",
                assassinClimbRuntime.InitializeNetwork);
            // Map-scoped fix state that the native save omits. Each record fails independently.
            TryInitializePersistentFeature(
                "single-building pause save data",
                InitializeMovedFeatureSaveData);
            TryInitializePersistentFeature(
                "Assassin climb-state save data",
                assassinClimbRuntime.InitializeSaveData);
            TryInitializePersistentFeature(
                "troop movement synchronization save data",
                troopMovementFixRuntime.InitializeSaveData);
            TryInitializePersistentFeature(
                "Assassin climb cancellation event",
                assassinClimbCancellationRuntime.Initialize);
            // APIShared owns the single troop-action setup hook and lays out every mod's host once.
            TryInitializePersistentFeature(
                "troop action HUD registration",
                () =>
                {
                    if (!APIShared.Presentation.TroopActionHud.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "AssassinClimb",
                        assassinClimbRuntime.RefreshButtonVisibility, out string reason))
                        throw new InvalidOperationException(reason);
                });
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
            TryInitializePersistentFeature(
                "tunnel placement-distance validation",
                tunnelPlacementDistanceFeature.Initialize);
            TryInitializePersistentFeature(
                "abrupt host migration",
                EnsureAbruptHostMigrationFix);

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
                    "gameplay-session start subscription",
                    () => mapStartSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                        log,
                        BeginGameplaySession));
            }

            if (mapUnloadSubscription == null)
            {
                TryInitializePersistentFeature(
                    "multiplayer map-unload subscription",
                    () => mapUnloadSubscription = Shared.MissionEvents.Ended
                        .Subscribe(_ => EndGameplaySession()));
            }

        }

        private void EndGameplaySession()
        {
            processNotificationSpeechPlayback?.Invalidate();
            processNotificationLastFrameFeature?.ResetMapState();
            ResetMovedFeatureMapState();
            multiplayerGameSpeedRuntime.ResetMapState();
            abruptHostMigrationFix?.ResetMapState();
            assassinClimbRuntime.EndMap();
            assassinPathfindingRuntime.EndMap();
            assassinCapturedGateProtectionRuntime.EndMap();
            plagueTargetReservationFix?.ResetForMap();
            siegeAmmoRestockFeature.ResetForMap();
            multiplayerFeatureGate.Reset();
        }

        private void BeginGameplaySession(Shared.GameplaySessionStartedContext context)
        {
            abruptHostMigrationFix?.ResetMapState();
            multiplayerFeatureGate.CaptureMapMode(
                context.Notification != null && context.Notification.Context.IsSave && context.Mode.IsRealMultiplayer);
            // Restored customized-save origins are pending until the destination session exists.
            if (!context.IsEditor)
                TrailCustomizationLaunchOriginApi.MarkMapStarted();
            assassinPathfindingRuntime.BeginMap();
            assassinCapturedGateProtectionRuntime.BeginMap();
            assassinClimbRuntime.BeginMap(context);
            TryApplyFeature("single-building pause save restore", () => RestoreMovedFeatureSaveState(context));
            TryApplyFeature("troop movement save restore", () => troopMovementFixRuntime.RestoreSessionState(context));
            multiplayerGameSpeedRuntime.ApplySetting();
            APIShared.Presentation.TroopActionHud.RefreshCurrentPanel();
        }

        public void InitializeSelectedUnitHealthFeature()
        {
            if (selectedUnitHealthFeature != null)
                return;

            selectedUnitHealthFeature = new SelectedUnitHealthFeature(log, settings, () => lordUnitHudRegistration.Capability);
            selectedUnitHealthFeature.RefreshSetting();
            ForeignTroopHudRuntime.Initialize(log, settings, selectedUnitHealthFeature, troopHudMiddleClickCameraFeature);
        }

        public void InitializeSurrenderFeature()
        {
            if (surrenderFeature == null)
            {
                surrenderFeature = new SurrenderFeature(log, settings);
                surrenderFeature.Initialize();
            }

            if (lordUnitControlsFeature == null)
            {
                lordUnitControlsFeature = new LordUnitControlsFeature(
                    log,
                    settings,
                    surrenderFeature,
                    () => lordMixedDisbandContractValidated);
                lordUnitControlsFeature.RefreshSetting();
            }
        }

        public void InitializeNative(CrusaderLibraryLoadContext context, bool isFixedLayoutHashValidated)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            IntPtr newLibraryHandle = context.ModuleHandle;
            ReadOnlySpan<byte> memory = context.Memory;
            if (nativeLibraryAvailable)
                return;

            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            libraryHandle = newLibraryHandle;
            libraryLength = memory.Length;
            nativeRegion = context.Region;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;
            TryInitializePersistentFeature(
                "Keep-flag rotation fix",
                EnsureKeepFlagRotationRuntime);
            TryInitializePersistentFeature(
                "tannery animation fade fix",
                () => EnsureNativeTannerFade(context));
            TryInitializePersistentFeature(
                "corrupt Lord-data spawn fix",
                EnsureCorruptLordDataSpawnRuntime);
            TryInitializePersistentFeature(
                "prebuilt AI workshop Both fix",
                () => EnsurePrebuiltAiWorkshopBothFixRuntime(context, isFixedLayoutHashValidated));
            TryInitializePersistentFeature(
                "nearest-water-carrier targeting QoL",
                () => EnsureWaterboyTargetReservationRuntime(
                    context,
                    isFixedLayoutHashValidated));
            TryInitializePersistentFeature(
                "notification speech ownership",
                () => processNotificationSpeechPlayback = NotificationSpeechPlayback.Get(log));
            TryInitializePersistentFeature(
                "complete notification skip",
                () => notificationSkipFeature = notificationSkipFeature ?? new NotificationSkipFeature(
                    log,
                    settings,
                    nativeRegion,
                    newLibraryHandle));
            TryInitializePersistentFeature(
                "notification last frame",
                () => processNotificationLastFrameFeature =
                    processNotificationLastFrameFeature ?? new NotificationLastFrameFeature(log, settings));
            tunnelPlacementDistanceFeature.SetFixedNativeLayoutValidated(
                isFixedLayoutHashValidated);
            try
            {
                LordControlGroupNativePatch.ValidateMixedDisbandContract(
                    memory,
                    isFixedLayoutHashValidated);
                lordMixedDisbandContractValidated = true;
            }
            catch (Exception ex)
            {
                lordMixedDisbandContractValidated = false;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:367", $"Bugfixes and QoL mixed Lord disband is fail-closed because its native contract could not be validated: {ex}");
            }
            assassinClimbCancellationRuntime.SetFixedUnitLayoutValidated(
                isFixedLayoutHashValidated);
            if (!isFixedLayoutHashValidated &&
                settings.EnableMod &&
                settings.EnableImprovedAssassinPathfinding)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:377", "Bugfixes and QoL Assassin climb cancellation remains inactive because its fixed unit-field layout is not validated for this CrusaderDE.dll.");
            }

            if (isFixedLayoutHashValidated)
            {
                TryInitializePersistentFeature("Assassin captured-gate protection",
                    () => assassinCapturedGateProtectionRuntime.InitializeNative(newLibraryHandle));

                try
                {
                    assassinPathfindingRuntime.InitializeNative(
                        newLibraryHandle,
                        nativeRegion,
                        memory,
                        fixedLayoutHashValidated: true);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:397", $"Bugfixes and QoL weighted Assassin pathfinding could not be initialized and remains inactive: {ex}");
                }

                try
                {
                    // This hook is independent of the weighted builder: with that option off,
                    // Vanilla's own Assassin builder receives the restored post-combat request.
                    assassinCombatResumeRuntime.InitializeNative(
                        newLibraryHandle,
                        nativeRegion,
                        memory,
                        fixedLayoutHashValidated: true);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:414", $"Bugfixes and QoL Assassin combat-resume fix could not be initialized and remains inactive: {ex}");
                }
            }
            else
            {
                if (settings.EnableMod && settings.EnableImprovedAssassinPathfinding)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:423", "Bugfixes and QoL weighted Assassin pathfinding remains inactive because the fixed native layout is not validated for this CrusaderDE.dll; Vanilla pathfinding remains active.");
                }
                if (settings.EnableMod && settings.EnableAssassinCombatResumeFix)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:429", "Bugfixes and QoL Assassin combat-resume fix remains inactive because the fixed native layout is not validated for this CrusaderDE.dll; Vanilla behavior remains active.");
                }
            }

            // Every native feature has its own compatibility surface. A changed signature in
            // one feature must not prevent unrelated fixes from installing.
            try
            {
                troopMovementFixRuntime.InitializeNative(newLibraryHandle, nativeRegion, memory, isFixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:443", $"Bugfixes and QoL feature 'troop movement fix' could not be initialized and remains inactive: {ex}");
            }
            InitializeMovedFeatureNative(
                newLibraryHandle,
                memory,
                isFixedLayoutHashValidated);
            TryInitializeFeature(
                "friendly moat movement",
                () => InitializeFriendlyMoatMovement(context, isFixedLayoutHashValidated));
            TryInitializePersistentFeature("Assassin gatehouse descent", () =>
                GateRouting.AssassinGatehouseDescentFeature.Initialize(context, log, settings, isFixedLayoutHashValidated));
            TryInitializePersistentFeature("Assassin gatehouse climb", ApplyAssassinGatehouseClimbFix);
            TryInitializePersistentFeature("enemy gate routing", () => GateRouting.GateRoutingFeature.Initialize(context,isFixedLayoutHashValidated));
            TryInitializePersistentFeature("drawbridge destruction fix", () =>
            {
                if (drawbridgeDestructionFixInitializationAttempted) return;
                drawbridgeDestructionFixInitializationAttempted = true;
                processDrawbridgeDestructionFixRuntime = new DrawbridgeDestructionFix.FixRuntime(log);
                processDrawbridgeDestructionFixRuntime.Install(context, settings.EnableMod, settings.EnableDrawbridgeDestructionFix);
            });
            TryInitializePersistentFeature("gatehouse living capture", () =>
            {
                if (gatehouseLivingCaptureInitializationAttempted) return;
                gatehouseLivingCaptureInitializationAttempted = true;
                processGatehouseLivingCaptureRuntime = new GatehouseLivingCapture.CaptureRuntime(log);
                processGatehouseLivingCaptureRuntime.Install(context, settings.EnableMod, settings.EnableGatehouseLivingCaptureFix);
            });
            TryInitializePersistentFeature("AI melee raid retarget fix", () =>
            {
                if (aiRaidRetargetInitializationAttempted) return;
                aiRaidRetargetInitializationAttempted = true;
                if (!isFixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:468", "AI_RAID_INACTIVE: unvalidated Native version; Vanilla preserved.");
                    return;
                }
                processAiRaidRetargetFixRuntime = new AiRaidRetargetFixRuntime(log);
                processAiRaidRetargetFixRuntime.Initialize(context, settings.EnableMod && settings.EnableAiRaidRetargetFix);
            });
            TryInitializePersistentFeature("AI sell threshold safety", () =>
            {
                if (processAiSellThresholdPolicy == null && !APIShared.Economy.AiMarketSellPolicies.TryRegister(
                    BugfixesAndQoLPlugin.PluginGuid, "ai-sell-threshold-safety",
                    APIShared.Economy.AiMarketSellSafety.NonNegativeReserve | APIShared.Economy.AiMarketSellSafety.LimitQuantityToStock,
                    out processAiSellThresholdPolicy, out string reason))
                    throw new InvalidOperationException(reason);
                ApplyAiSellThresholdFix();
            });
            TryInitializeFeature(
                "Extended Shift command queue",
                () => InitializeExtendedShiftCommandQueue(context, isFixedLayoutHashValidated));
            TryInitializeFeature("plague popularity fix", EnsurePlaguePopularityFix);
            TryInitializeFeature("plague cloud removal fix", EnsurePlagueTreatmentFadeFix);
            TryInitializeFeature("plague target-reservation fix", EnsurePlagueTargetReservationFix);
            TryInitializeFeature("stuck-apothecary fix", EnsurePlagueApothecaryStateTransitionFix);
            TryInitializeFeature("assembly-point placement fix", ApplyAssemblyPointPlacementPatchSetting);
            TryInitializeFeature("Healer attack-command fix", ApplyHealerAttackCommandPatchSetting);
            TryInitializeFeature("mounted-stockpile movement fix", ApplyMountedStockpileMovementPatchSetting);
            TryInitializeFeature("disbanded-unit control-group cleanup", EnsureControlGroupDisbandCleanup);
            TryInitializeFeature("Lord control groups", ApplyLordControlGroupPatchSetting);
            TryInitializeFeature("AI stone-reserve fix", EnsureAiStoneReserveFix);
            TryInitializePersistentFeature("AI keep build range", () =>
            {
                if (aiKeepRangeInitializationAttempted) return;
                aiKeepRangeInitializationAttempted = true;
                processAIKeepRangeRuntime = new AIKeepRangeRuntime(log, settings);
                processAIKeepRangeRuntime.Initialize(context);
            });
            TryInitializeFeature("AI defense patrol fix", EnsureAiDefensePatrolFix);
            TryInitializeFeature("AI wall-targeting fix", EnsureAiWallTargetingFix);
            TryInitializeFeature("baker/miller breaks", EnsureWorkerBreakPauseHook);
            TryInitializeFeature("workshop idle delay", EnsureWorkshopIdleDelayHook);
            TryInitializeFeature("AIV defender-position fix", EnsureAivDefenderPositionFix);
            TryInitializePersistentFeature("AI preplaced-map-building fix", () =>
            {
                var candidate = new AIPreplacedBuildingFixRuntime(
                    log,
                    () => settings.EnableMod && settings.FixAIPreplacedMapBuildings);
                candidate.TryInstallNativeFixes(context, isFixedLayoutHashValidated);
                candidate.InstallEventHandlers();
                processAIPreplacedBuildingFixRuntime = candidate;
            });
            TryInitializeFeature("AI tower-ruin repair fix", EnsureAiTowerRuinRepairFix);
            TryInitializeFeature("better AI overbuild rules", EnsureBetterAIOverbuildRulesFix);
            TryInitializeFeature("ally goods amount modifiers", InstallAllyGoodsAmountModifierHook);
            TryInitializeFeature("Ctrl single-unit market trade", InstallCtrlMarketTradeHook);
        }

        // APIShared owns permanent hooks and publishes only a logical activation flag.
        // Both initialization and host-setting changes use the existing rooted runtime publishers.
        private void ApplyAssassinGatehouseClimbFix()
        {
            if (!nativeLibraryAvailable) return;
            APIShared.AssassinPathAPI.SetDirectGatehouseClimbing(
                BugfixesAndQoLPlugin.PluginGuid, settings.EnableMod && settings.EnableAssassinGatehouseClimbFix);
            GateRouting.AssassinGatehouseDescentFeature.Refresh();
        }

        // The synchronized host value is applied on every peer, including editor missions.
        // APIShared owns the permanent hooks; disabling only withdraws our own request.
        private void ApplyAiSellThresholdFix() => processAiSellThresholdPolicy?.SetEnabled(
            settings.EnableMod && settings.EnableAiSellThresholdFix);

        public void ApplySettings()
        {
            TryApplyFeature("AI sell threshold safety", ApplyAiSellThresholdFix);
            TryApplyFeature("Assassin gatehouse climb", ApplyAssassinGatehouseClimbFix);
            processGatehouseLivingCaptureRuntime?.SetEnabled(settings.EnableMod, settings.EnableGatehouseLivingCaptureFix);
            processDrawbridgeDestructionFixRuntime?.SetEnabled(settings.EnableMod, settings.EnableDrawbridgeDestructionFix);
            processAIKeepRangeRuntime?.Refresh();
            TryApplyFeature("AI melee raid retarget fix", () => processAiRaidRetargetFixRuntime?.SetEnabled(
                settings.EnableMod && settings.EnableAiRaidRetargetFix));
            processNativeTannerFade?.SetEnabled(
                settings.EnableMod && settings.EnableTanneryAnimationFix);
            TryApplyFeature(
                "nearest-water-carrier targeting QoL",
                () => processWaterboyTargetReservationRuntime?.SetEnabled(
                    settings.EnableMod && settings.EnableNearestWaterboyTargeting));
            TryApplyFeature("Trail Customize buttons", trailCustomizationFeature.RefreshVisibility);
            TryApplyFeature("Coop custom-lord selection", coopCustomLordSelectionFeature.ApplySetting);
            TryApplyFeature("moved feature settings", ApplyMovedFeatureSettings);
            TryInitializeFeature("AI tower-ruin repair fix", EnsureAiTowerRuinRepairFix);
            TryInitializeFeature("better AI overbuild rules", EnsureBetterAIOverbuildRulesFix);
            TryInitializeFeature("AI defense patrol fix", EnsureAiDefensePatrolFix);
            TryInitializeFeature("AI wall-targeting fix", EnsureAiWallTargetingFix);
            TryInitializeFeature("baker/miller breaks", EnsureWorkerBreakPauseHook);
            TryInitializeFeature("workshop idle delay", EnsureWorkshopIdleDelayHook);
            TryInitializeFeature("AIV defender-position fix", EnsureAivDefenderPositionFix);
            TryApplyFeature("ally goods amount modifiers", () => processAllyGoodsAmountModifierHook?.RefreshSetting());
            TryInitializeFeature("surrender", InitializeSurrenderFeature);
            TryApplyFeature("Lord troop HUD", () => lordUnitControlsFeature?.RefreshSetting());
            TryApplyFeature("shared unit HUD", () => lordUnitHudRegistration.RefreshSetting());
            TryInitializeFeature("selected-unit health display", InitializeSelectedUnitHealthFeature);
            TryApplyFeature("selected-unit health display", () => selectedUnitHealthFeature?.RefreshSetting());
            TryApplyFeature("surrender", () => surrenderFeature?.RefreshButtonState());
            TryInitializeFeature("resync host kick", EnsureResyncHostKickFeature);
            TryInitializeFeature("AI castle/settings selection memory", EnsureAiSelectionHook);
            TryInitializeFeature("Workshop upload Lord-selection fix", EnsureWorkshopUploadLordSelectionFix);
            TryApplyFeature("AI castle/settings selection memory", () => skirmishAiSelectionMemoryHook?.ApplySetting());
            TryInitializeFeature("custom-lord list enhancements", EnsureCustomLordListEnhancementHook);
            TryApplyFeature("custom-lord list enhancements", () => customLordListEnhancementHook?.ApplySetting());
            TryInitializePersistentFeature(
                "high-contrast yellow presentation",
                EnsureLobbyYellowContrastFeature);
            TryApplyFeature(
                "high-contrast yellow presentation",
                () => processLobbyYellowContrastFeature?.ApplySetting());
            TryInitializeFeature("AI castle/settings list enhancements", EnsureAiCastleSettingsListEnhancementHook);
            TryApplyFeature("AI castle/settings list enhancements", () => aiCastleSettingsListEnhancementHook?.ApplySetting());
            TryInitializeFeature("map-origin sorting", EnsureMapOriginSortHook);
            TryInitializeFeature("Vanilla maps in the editor", EnsureVanillaMapEditorHook);
            TryApplyFeature("troop movement fix", troopMovementFixRuntime.ApplySetting);
            TryApplyFeature(
                "Extended Shift command queue",
                () => extendedShiftCommandQueueRuntime?.ApplySetting());
            TryApplyFeature("multiplayer game speed", multiplayerGameSpeedRuntime.ApplySetting);
            TryApplyFeature("assembly-point placement fix", ApplyAssemblyPointPlacementPatchSetting);
            TryApplyFeature("Healer attack-command fix", ApplyHealerAttackCommandPatchSetting);
            TryApplyFeature("mounted-stockpile movement fix", ApplyMountedStockpileMovementPatchSetting);
            TryApplyFeature("Lord control groups", ApplyLordControlGroupPatchSetting);
            TryApplyFeature("AI stone-reserve fix", () => aiStoneReserveFix?.ApplySetting());
            TryApplyFeature("AI defense patrol fix", () => aiDefensePatrolFix?.ApplySetting());
            TryApplyFeature("Assassin path reconstruction", assassinPathfindingRuntime.ApplySetting);
            if (settings.EnableMod && settings.EnableImprovedAssassinPathfinding && assassinPathfindingRuntime.IsInstalled)
                TryApplyFeature("Assassin climb button", assassinClimbRuntime.Initialize);
            else
                TryApplyFeature("Assassin climb button cleanup", assassinClimbRuntime.Dispose);

            if (settings.EnableClientFeatures)
                SubscribeHooks();
            else
                UnsubscribeHooks();
        }

        private void InitializeFriendlyMoatMovement(
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            if (!nativeLibraryAvailable || friendlyMoatMovementRuntime != null)
                return;
            if (processUnitCommandPathRuntime == null)
            {
#if SERP_DEVDIAG
                UnitCommandPathAPI.AssassinReconstructionRelaxation = () => AssassinPathfindingRuntime.TemporaryReconstructionRelaxation;
#endif
                processUnitCommandPathRuntime = UnitCommandPathAPI.RegisterCommands(
                    log, settings, context, referenceHashMatches, BugfixesAndQoLPlugin.PluginGuid);
            }
            friendlyMoatMovementRuntime = processUnitCommandPathRuntime;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL shared unit commands initialized: " +
                    $"manualCommands={settings.EnableImprovedManualUnitCommands}, improvedFill=" +
                    $"{settings.EnableMod && settings.EnableImprovedMoatFilling}, " +
                    $"ladderAttackFix={settings.EnableMod && settings.EnableLadderAttackPathfindingFix}.");
        }

        private void InitializeExtendedShiftCommandQueue(
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            if (!nativeLibraryAvailable || extendedShiftCommandQueueRuntime != null)
                return;

            // Root the runtime before installing process-lifetime detours so a partial native
            // transaction can never leave callbacks whose managed owner became collectible.
            if (processExtendedShiftCommandQueueRuntime == null)
            {
                processExtendedShiftCommandQueueRuntime =
                    new ExtendedShiftCommandQueueRuntime(
                        log, settings, friendlyMoatMovementRuntime);
                processExtendedShiftCommandQueueRuntime.Install(context, referenceHashMatches);
            }
            extendedShiftCommandQueueRuntime = processExtendedShiftCommandQueueRuntime;
        }

        private void EnsureShiftRepairAllBuildingsHook()
        {
            if (shiftRepairAllBuildingsHook == null)
                shiftRepairAllBuildingsHook = new ShiftRepairAllBuildingsHook(log, settings);
        }

        private void EnsurePlacementCancelMoveSuppressionFeature()
        {
            if (processPlacementCancelMoveSuppressionFeature != null)
                return;

            var candidate = new PlacementCancelMoveSuppressionFeature(log, settings);
            candidate.Install();
            processPlacementCancelMoveSuppressionFeature = candidate;
        }

        private void EnsureKeepFlagRotationRuntime()
        {
            if (processKeepFlagRotationRuntime != null)
                return;

            var candidate = new KeepFlagRotationRuntime(log, settings);
            candidate.Install();
            processKeepFlagRotationRuntime = candidate;
        }

        private void EnsureNativeTannerFade(CrusaderLibraryLoadContext context)
        {
            if (processNativeTannerFade != null)
                return;

            processNativeTannerFade = new NativeTannerFade(log, context,
                settings.EnableMod && settings.EnableTanneryAnimationFix);
        }

        private void EnsureCorruptLordDataSpawnRuntime()
        {
            if (processCorruptLordDataSpawnRuntime != null)
                return;

            var candidate = new CorruptLordDataSpawnRuntime(log, settings);
            candidate.Install();
            processCorruptLordDataSpawnRuntime = candidate;
        }

        private void EnsurePrebuiltAiWorkshopBothFixRuntime(
            CrusaderLibraryLoadContext context,
            bool isFixedLayoutHashValidated)
        {
            if (processPrebuiltAiWorkshopBothFixRuntime != null)
                return;
            if (!isFixedLayoutHashValidated)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:685", "Prebuilt AI workshop Both fix is inactive because the installed native hash is not audited.");
                return;
            }

            var candidate = new PrebuiltAiWorkshopBothFixRuntime(
                log, settings, context, isFixedLayoutHashValidated);
            candidate.Install();
            processPrebuiltAiWorkshopBothFixRuntime = candidate;
        }

        public void Dispose()
        {
            UnsubscribeHooks();
            DisposeMovedFeatures();
            assassinPathfindingRuntime.Dispose();
            shiftRepairAllBuildingsHook?.Dispose();
            shiftRepairAllBuildingsHook = null;
            skirmishAiSelectionMemoryHook?.Dispose();
            skirmishAiSelectionMemoryHook = null;
            customLordListEnhancementHook?.Dispose();
            customLordListEnhancementHook = null;
            aiCastleSettingsListEnhancementHook?.Dispose();
            aiCastleSettingsListEnhancementHook = null;
            mapOriginSortHook?.Dispose();
            mapOriginSortHook = null;
            vanillaMapEditorHook?.Dispose();
            vanillaMapEditorHook = null;
            resyncHostKickFeature?.Dispose();
            resyncHostKickFeature = null;
            abruptHostMigrationFix?.Dispose();
            abruptHostMigrationFix = null;
            // Lord action hooks are process-lifetime registrations and are not torn down here.
            surrenderFeature?.Dispose();
            surrenderFeature = null;
            // The static HUD publisher owns this view model for the lifetime of the process.
            DisableAssemblyPointPlacementPatch();
            DisableHealerAttackCommandPatch();
            DisableMountedStockpileMovementPatch();
            DisableLordControlGroupNativePatch();
            controlGroupDisbandCleanupRuntime?.Dispose();
            controlGroupDisbandCleanupRuntime = null;
            aiStoneReserveFix?.Dispose();
            aiStoneReserveFix = null;
            aiDefensePatrolFix?.Dispose();
            aiDefensePatrolFix = null;
            aiWallTargetingFix?.Dispose();
            aiWallTargetingFix = null;
            aivDefenderPositionFix?.Dispose();
            aivDefenderPositionFix = null;
            aiTowerRuinRepairFix?.Dispose();
            aiTowerRuinRepairFix = null;
            betterAIOverbuildRulesFix?.Dispose();
            betterAIOverbuildRulesFix = null;
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
            assassinClimbRuntime.Dispose();
            assassinClimbCancellationRuntime.Dispose();
            ctrlMarketTradeHook?.Dispose();
            ctrlMarketTradeHook = null;
            multiplayerGameSpeedRuntime.Dispose();
            multiplayerAivSyncRuntime.Dispose();
            siegeAmmoRestockFeature.Dispose();
            tunnelPlacementDistanceFeature.Dispose();
            trailCustomizationFeature.Dispose();
            playerMarketSubscription?.Dispose();
            playerMarketSubscription = null;
            mapStartSubscription?.Dispose();
            mapStartSubscription = null;
            mapUnloadSubscription?.Dispose();
            mapUnloadSubscription = null;
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
            if (!settings.EnableClientFeatures)
                return;

            ReconcileClientHook(
                "minimap improvements",
                settings.EnableMinimapCursorFollowFix || settings.AllowMinimapWhilePlacingBuilding,
                () => minimapInputFeature != null,
                () => minimapInputFeature = new MinimapInputFeature(log, settings),
                DeactivateMinimapInputFeature);
            ReconcilePermanentClientHook(
                "market autotrade sell threshold",
                settings.EnableAutoTradeSellZeroFix,
                () => autoTradeSellZeroHook != null,
                () => autoTradeSellZeroHook = new AutoTradeSellZeroHook(log, settings));
            ReconcileClientHook(
                "market key main-menu return",
                settings.EnableMarketKeyMainMenuFix,
                () => marketKeyMainTradeMenuHook != null,
                () => marketKeyMainTradeMenuHook = new MarketKeyMainTradeMenuHook(log, settings),
                () => DisposeFeature("market key main-menu return", ref marketKeyMainTradeMenuHook));
            ReconcilePermanentClientHook(
                "HD market view",
                settings.HdMarketView,
                () => hdMarketViewHook != null,
                () => hdMarketViewHook = new HdMarketViewHook(log, settings));
            ReconcilePermanentClientHook(
                "camera movement modifier",
                settings.AllowCameraMovementWithModifiers,
                () => cameraMovementModifierHook != null,
                () => cameraMovementModifierHook = new CameraMovementModifierHook(log, settings));
            ReconcilePermanentClientHook(
                "Custom Trail starting-gold fix",
                settings.EnableCustomTrailExtremeGoldFix,
                () => customTrailExtremeGoldFixHook != null,
                () => customTrailExtremeGoldFixHook = new CustomTrailExtremeGoldFixHook(log, settings));

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
                if (StartupDiagnosticPolicy.ShouldReportFixedLayoutFailure(
                    nativeLibraryAvailable,
                    fixedLayoutHashValidated,
                    settings.EnableEnemyProximityBulldozeCursorFix,
                    enemyProximityFixedLayoutErrorLogged))
                {
                    enemyProximityFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:832", "Bugfixes and QoL enemy-proximity bulldoze cursor remains inactive because its fixed native layout is not validated for this CrusaderDE.dll.");
                }
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL feature hooks reconciled.");
        }

        private void UnsubscribeHooks()
        {
            DeactivateMinimapInputFeature();
            DisposeFeature("market key main-menu return", ref marketKeyMainTradeMenuHook);
        }

        private void DeactivateMinimapInputFeature()
        {
            MinimapInputFeature current = minimapInputFeature;
            minimapInputFeature = null;
            if (current == null)
                return;

            try
            {
                current.Deactivate();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:860", $"Bugfixes and QoL feature 'minimap improvements' deactivation failed; other features continue: {ex}");
            }
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

        private void ReconcilePermanentClientHook(
            string featureName,
            bool enabled,
            Func<bool> isInstalled,
            Action install)
        {
            // MonoMod rewrites executable method entry points. Once a hook has
            // been published, keep it and its trampoline rooted for the process
            // lifetime; each callback performs the corresponding settings gate
            // and follows its original trampoline while logically disabled.
            if (enabled && !isInstalled())
                TryInitializeFeature(featureName, install);
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
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:906", $"Bugfixes and QoL feature '{featureName}' cleanup failed; other features continue: {ex}");
            }
        }

        private void EnsureAiSelectionHook()
        {
            if (skirmishAiSelectionMemoryHook == null)
                skirmishAiSelectionMemoryHook = new SkirmishAiSelectionMemoryHook(log, settings);
        }

        private void EnsureWorkshopUploadLordSelectionFix()
        {
            if (processWorkshopUploadLordSelectionFix != null)
                return;

            var candidate = new WorkshopUploadLordSelectionFix(log, settings);
            processWorkshopUploadLordSelectionFix = candidate;
        }

        private void EnsureCustomLordListEnhancementHook()
        {
            if (customLordListEnhancementHook == null)
            {
                customLordListEnhancementHook = new CustomLordListEnhancementHook(log, settings);
            }
        }

        private void EnsureLobbyYellowContrastFeature()
        {
            if (processLobbyYellowContrastFeature != null)
                return;

            // Hooks and UI resources are process-lifetime state. Publish only the fully
            // initialized candidate and never route it through plugin/component teardown.
            var candidate = new LobbyYellowContrastFeature(log, settings);
            processLobbyYellowContrastFeature = candidate;
        }

        private void EnsureWaterboyTargetReservationRuntime(
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            if (processWaterboyTargetReservationRuntime != null)
            {
                processWaterboyTargetReservationRuntime.SetEnabled(
                    settings.EnableMod && settings.EnableNearestWaterboyTargeting);
                return;
            }

            var candidate = new WaterboyTargetReservationRuntime(
                log,
                settings.EnableMod && settings.EnableNearestWaterboyTargeting,
                context,
                referenceHashMatches);
            processWaterboyTargetReservationRuntime = candidate;
        }

        private void EnsureBriefingNoStartingGoldFixRegistration()
        {
            if (processBriefingNoStartingGoldFixRegistration != null)
                return;

            processBriefingNoStartingGoldFixRegistration =
                new BriefingNoStartingGoldFixRegistration(log, settings);
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

        private void EnsureVanillaMapEditorHook()
        {
            if (vanillaMapEditorHook == null)
                vanillaMapEditorHook = new VanillaMapEditorHook(log, settings);
        }

        private void EnsureResyncHostKickFeature()
        {
            if (resyncHostKickFeature == null)
                resyncHostKickFeature = new ResyncHostKickFeature(log, settings);
        }

        private void EnsureAbruptHostMigrationFix()
        {
            if (abruptHostMigrationFix == null)
                abruptHostMigrationFix = new AbruptHostMigrationFix(
                    log,
                    settings,
                    multiplayerFeatureGate);
        }

        private void InstallAllyGoodsAmountModifierHook()
        {
            if (processAllyGoodsAmountModifierHook != null)
                return;

            try
            {
                var candidate = new AllyGoodsAmountModifierHook(log, settings);
                processAllyGoodsAmountModifierHook = candidate;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1023", $"Bugfixes and QoL ally goods amount modifier hook could not be installed: {ex}");
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
                    nativeRegion,
                    libraryHandle,
                    GetNativeLibraryMemory(),
                    fixedLayoutHashValidated);
                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1045", "Bugfixes and QoL Ctrl single-unit market hooks are running on an unknown CrusaderDE.dll because all required native instruction patterns were validated.");
                }
            }
            catch (Exception ex)
            {
                ctrlMarketTradeHookUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1053", $"Bugfixes and QoL Ctrl single-unit market hooks could not be installed: {ex}");
            }
        }

        private void TryInitializeFeature(string featureName, Action initialize)
        {
            using (Shared.CrashBreadcrumbScope diagnostic =
                (Shared.CrashBreadcrumbDiagnostics.IsEnabled ? Shared.CrashBreadcrumbDiagnostics.Enter("FeatureInitialize", featureName.GetHashCode()) : default(Shared.CrashBreadcrumbScope)))
            {
                try
                {
                    initialize();
                    diagnostic.Complete();
                }
                catch (Exception ex)
                {
                    diagnostic.Complete(-1);
                    Shared.RuntimeHealth.Failure(log, featureName, true, $"Bugfixes and QoL feature '{featureName}' could not be initialized and remains inactive: {ex}");
                }
            }
        }

        private void TryInitializePersistentFeature(string featureName, Action initialize)
        {
            using (Shared.CrashBreadcrumbScope diagnostic =
                (Shared.CrashBreadcrumbDiagnostics.IsEnabled ? Shared.CrashBreadcrumbDiagnostics.Enter("PersistentInitialize", featureName.GetHashCode()) : default(Shared.CrashBreadcrumbScope)))
            {
                try
                {
                    initialize();
                    diagnostic.Complete();
                }
                catch (Exception ex)
                {
                    diagnostic.Complete(-1);
                    Shared.RuntimeHealth.Failure(log, featureName, true, $"Bugfixes and QoL persistent feature '{featureName}' could not be initialized; independent features continue: {ex}");
                }
            }
        }

        private void TryApplyFeature(string featureName, Action apply)
        {
            using (Shared.CrashBreadcrumbScope diagnostic =
                (Shared.CrashBreadcrumbDiagnostics.IsEnabled ? Shared.CrashBreadcrumbDiagnostics.Enter("FeatureApply", featureName.GetHashCode()) : default(Shared.CrashBreadcrumbScope)))
            {
                try
                {
                    apply();
                    diagnostic.Complete();
                }
                catch (Exception ex)
                {
                    diagnostic.Complete(-1);
                    Shared.RuntimeHealth.Failure(log, featureName, true, $"Bugfixes and QoL feature '{featureName}' could not apply its setting; independent features continue: {ex}");
                }
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.RemoveAIKeepRangeLimit))
            {
                processAIKeepRangeRuntime?.Refresh();
                return;
            }
            // The countdown owns its local presentation; unrelated runtime features need no refresh.
            if (propertyName == nameof(BugfixesAndQoLViewModel.ShowCountdownTimers))
                return;
            // Refresh the market hook's validated snapshot only when the order changes.
            if (propertyName == nameof(BugfixesAndQoLViewModel.MarketGoodsOrder))
            {
                hdMarketViewHook?.UpdateOrder(settings.MarketGoodsOrder);
                return;
            }

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

        private void ApplyMountedStockpileMovementPatchSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (settings.EnableMod && settings.EnableMountedStockpileMovementFix)
                InstallMountedStockpileMovementPatch();
            else
                DisableMountedStockpileMovementPatch();
        }

        private void ApplyHealerAttackCommandPatchSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (settings.EnableMod && settings.EnableHealerAttackCommandFix)
                InstallHealerAttackCommandPatch();
            else
                DisableHealerAttackCommandPatch();
        }

        private void ApplyLordControlGroupPatchSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (settings.EnableMod && settings.EnableLordUnitControls)
                InstallLordControlGroupNativePatch();
            else
                DisableLordControlGroupNativePatch();
        }

        private unsafe ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL stays loaded for the process lifetime.
            return nativeRegion == null ? ReadOnlySpan<byte>.Empty : nativeRegion.Span;
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
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                plaguePopularityFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1228", $"Bugfixes and QoL plague popularity fix could not be installed; Vanilla behavior remains active: {ex}");
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
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                aiStoneReserveFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1254", $"Bugfixes and QoL AI stone-reserve fix could not be installed; " +
                    $"only this AI fix remains inactive and Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureAiDefensePatrolFix()
        {
            if (aiDefensePatrolFix != null)
            {
                aiDefensePatrolFix.ApplySetting();
                return;
            }
            if (!nativeLibraryAvailable || aiDefensePatrolFixUnavailable)
                return;

            // Avoid installing a dormant native hook. A later synchronized enable change
            // retries this method while the validated native mapping remains available.
            if (!settings.EnableMod || !settings.EnableAiDefensePatrolFix)
                return;

            try
            {
                aiDefensePatrolFix = new AiDefensePatrolFix(
                    log,
                    settings,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                try
                {
                    aiDefensePatrolFix?.Dispose();
                }
                catch (Exception rollbackEx)
                {
                    ex = new AggregateException(
                        "The AI defense patrol fix failed and cleanup also failed.",
                        ex,
                        rollbackEx);
                }

                aiDefensePatrolFix = null;
                aiDefensePatrolFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1302", $"Bugfixes and QoL AI defense patrol fix could not be installed; " +
                    $"only this AI fix remains inactive and Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureWorkshopIdleDelayHook()
        {
            if (processWorkshopIdleDelayHook == null && nativeLibraryAvailable &&
                !workshopIdleDelayHookUnavailable)
            {
                try
                {
                    processWorkshopIdleDelayHook = new WorkshopIdleDelayHook(
                        log, nativeRegion, GetNativeLibraryMemory(),
                        unchecked((ulong)libraryHandle.ToInt64()), fixedLayoutHashValidated);
                }
                catch (Exception ex)
                {
                    workshopIdleDelayHookUnavailable = true;
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1323", "BUGFIXES_AND_QOL_WORKSHOP_IDLE_INACTIVE: Vanilla remains active: " + ex);
                }
            }
            if (processWorkshopIdleDelayHook != null && !workshopIdleTickSubscribed && !workshopIdleTickLogged)
            {
                GameTimeManagerAPI.Instance.OnTick += OnWorkshopIdleTick;
                workshopIdleTickSubscribed = true;
            }
            processWorkshopIdleDelayHook?.SetEnabled(settings.EnableMod && settings.EnableWorkshopIdleDelayFix);
        }

        private static void OnWorkshopIdleTick(int tick)
        {
            processWorkshopIdleDelayHook?.LogAfterStartup(tick);
            workshopIdleTickLogged = true;
            GameTimeManagerAPI.Instance.OnTick -= OnWorkshopIdleTick;
            workshopIdleTickSubscribed = false;
        }

        private void EnsureWorkerBreakPauseHook()
        {
            if (processWorkerBreakPauseHook == null && nativeLibraryAvailable &&
                !workerBreakPauseHookUnavailable)
            {
                try
                {
                    processWorkerBreakPauseHook = new WorkerBreakPauseHook(
                        log, nativeRegion, GetNativeLibraryMemory(),
                        unchecked((ulong)libraryHandle.ToInt64()), fixedLayoutHashValidated);
                }
                catch (Exception ex)
                {
                    workerBreakPauseHookUnavailable = true;
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1357", "BUGFIXES_AND_QOL_WORKER_BREAK_INACTIVE: Vanilla behavior remains active: " + ex);
                }
            }
            if (processWorkerBreakPauseHook != null && !workerBreakTickSubscribed &&
                !workerBreakTickLogged)
            {
                GameTimeManagerAPI.Instance.OnTick += OnWorkerBreakTick;
                workerBreakTickSubscribed = true;
            }
            processWorkerBreakPauseHook?.SetEnabled(
                settings.EnableMod && settings.EnableBakerMillerBreaks);
        }

        private static void OnWorkerBreakTick(int tick)
        {
            processWorkerBreakPauseHook?.LogAfterStartup(tick);
            workerBreakTickLogged = true;
            GameTimeManagerAPI.Instance.OnTick -= OnWorkerBreakTick;
            workerBreakTickSubscribed = false;
        }

        private void EnsureAiWallTargetingFix()
        {
            if (aiWallTargetingFix != null)
            {
                aiWallTargetingFix.ApplySetting();
                return;
            }
            if (!nativeLibraryAvailable || aiWallTargetingFixUnavailable)
                return;

            try
            {
                // Install once; synchronized setting changes only update the hook's
                // logical decision between Vanilla rejection and the fixed path.
                aiWallTargetingFix = new AiWallTargetingFix(
                    log,
                    settings,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
                aiWallTargetingFix.ApplySetting();
            }
            catch (Exception ex)
            {
                try
                {
                    aiWallTargetingFix?.Dispose();
                }
                catch (Exception rollbackEx)
                {
                    ex = new AggregateException(
                        "The AI wall-targeting fix failed and cleanup also failed.",
                        ex,
                        rollbackEx);
                }

                aiWallTargetingFix = null;
                aiWallTargetingFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1418", $"Bugfixes and QoL AI wall-targeting fix could not be installed; " +
                    $"Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureAivDefenderPositionFix()
        {
            if (aivDefenderPositionFix != null)
            {
                aivDefenderPositionFix.ApplySetting();
                return;
            }
            if (!nativeLibraryAvailable || aivDefenderPositionFixUnavailable)
                return;

            try
            {
                // Install once; synchronized setting changes only update the hook's
                // logical decision between the audited Vanilla and fixed paths.
                aivDefenderPositionFix = new AivDefenderPositionFix(
                    log,
                    settings,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
                aivDefenderPositionFix.ApplySetting();
            }
            catch (Exception ex)
            {
                try
                {
                    aivDefenderPositionFix?.Dispose();
                }
                catch (Exception rollbackEx)
                {
                    ex = new AggregateException(
                        "The AIV defender-position fix failed and cleanup also failed.",
                        ex,
                        rollbackEx);
                }

                aivDefenderPositionFix = null;
                aivDefenderPositionFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1464", $"Bugfixes and QoL AIV defender-position fix could not be installed; " +
                    $"Vanilla behavior remains active: {ex}");
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
            if (!settings.EnableMod || !settings.FixAITowerRepair)
                return;

            try
            {
                aiTowerRuinRepairFix = new AITowerRuinRepairFix(
                    log,
                    settings,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                aiTowerRuinRepairFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1498", $"Bugfixes and QoL AI tower-ruin repair could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureBetterAIOverbuildRulesFix()
        {
            if (betterAIOverbuildRulesFix != null)
            {
                betterAIOverbuildRulesFix.ApplySetting();
                return;
            }
            if (!nativeLibraryAvailable || betterAIOverbuildRulesFixUnavailable)
                return;
            // Keep a fully disabled configuration physically Vanilla. Enabling the synchronized
            // host setting later retries installation with the retained DLL mapping.
            if (!settings.EnableMod || !settings.BetterAIOverbuildRules)
            {
                return;
            }

            try
            {
                betterAIOverbuildRulesFix = new BetterAIOverbuildRulesFix(
                    log,
                    settings,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                betterAIOverbuildRulesFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1533", $"Bugfixes and QoL better AI overbuild rules could not be installed; " +
                    $"Vanilla behavior remains active: {ex}");
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
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                plagueTreatmentFadeFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1558", $"Bugfixes and QoL plague-cloud removal fix could not be installed; " +
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
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                plagueApothecaryStateTransitionFixUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1587", $"Bugfixes and QoL stuck-apothecary fix could not be installed; " +
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
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1606", "Bugfixes and QoL plague target-reservation fix remains inactive because " +
                    "the treatment-completion hook is unavailable; other plague fixes remain independent.");
                return;
            }

            try
            {
                plagueTargetReservationFix = new PlagueTargetReservationFix(
                    log,
                    settings,
                    nativeRegion,
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
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1631", $"Bugfixes and QoL plague target-reservation fix could not be installed; " +
                    $"only this fix remains inactive and Vanilla target selection remains active: {ex}");
            }
        }

        private void InstallAssemblyPointPlacementPatch()
        {
            if (assemblyPointPlacementPatch != null)
            {
                assemblyPointPlacementPatch.SetEnabled(true);
                return;
            }
            if (assemblyPointPlacementPatchUnavailable)
                return;

            try
            {
                assemblyPointPlacementPatch = new AssemblyPointPlacementPatch(
                    log,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                // A changed or already hooked signature cannot become valid later in this process.
                assemblyPointPlacementPatchUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1661", $"Bugfixes and QoL assembly-point placement patch could not be installed: {ex}");
            }
        }

        private void DisableAssemblyPointPlacementPatch()
        {
            assemblyPointPlacementPatch?.SetEnabled(false);
        }

        private void InstallMountedStockpileMovementPatch()
        {
            if (mountedStockpileMovementPatch != null)
            {
                mountedStockpileMovementPatch.SetEnabled(true);
                return;
            }
            if (mountedStockpileMovementPatchUnavailable)
                return;

            try
            {
                mountedStockpileMovementPatch = new MountedStockpileMovementPatch(
                    log,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                mountedStockpileMovementPatchUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1692", $"Bugfixes and QoL mounted-stockpile movement patch could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void InstallHealerAttackCommandPatch()
        {
            if (healerAttackCommandPatch != null)
            {
                healerAttackCommandPatch.SetEnabled(true);
                return;
            }
            if (healerAttackCommandPatchUnavailable)
                return;

            try
            {
                healerAttackCommandPatch = new HealerAttackCommandPatch(
                    log,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                healerAttackCommandPatchUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1720", $"Bugfixes and QoL Healer attack-command patch could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void DisableHealerAttackCommandPatch()
        {
            healerAttackCommandPatch?.SetEnabled(false);
        }

        private void InstallLordControlGroupNativePatch()
        {
            if (lordControlGroupNativePatch != null)
            {
                lordControlGroupNativePatch.SetEnabled(true);
                return;
            }
            if (lordControlGroupNativePatchUnavailable)
                return;

            try
            {
                lordControlGroupNativePatch = new LordControlGroupNativePatch(
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                lordControlGroupNativePatchUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1752", $"Bugfixes and QoL Lord control-group patch could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void EnsureControlGroupDisbandCleanup()
        {
            if (!nativeLibraryAvailable || controlGroupDisbandCleanupRuntime != null ||
                controlGroupDisbandCleanupUnavailable)
            {
                return;
            }

            try
            {
                // Keep the detour installed; the local setting is checked inside the callback.
                controlGroupDisbandCleanupRuntime = new ControlGroupDisbandCleanupRuntime(
                    log,
                    settings,
                    nativeRegion,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);
            }
            catch (Exception ex)
            {
                controlGroupDisbandCleanupUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs:1780", $"Bugfixes and QoL disbanded-unit control-group cleanup could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void DisableLordControlGroupNativePatch()
        {
            bool wasInstalled = lordControlGroupNativePatch != null;
            lordControlGroupNativePatch?.SetEnabled(false);
            if (!wasInstalled || !CrusaderDE.MainViewModel.viewModelLoaded)
                return;
            CrusaderDE.MainViewModel main = CrusaderDE.MainViewModel.Instance;
            if (main?.Show_HUD_ControlGroups == true)
                main.HUDControlGroups?.Update();
        }

        private void DisableMountedStockpileMovementPatch()
        {
            mountedStockpileMovementPatch?.SetEnabled(false);
        }

    }
}
