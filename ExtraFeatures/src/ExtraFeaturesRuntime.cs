// Feature: Lifecycle orchestration and shared event guards for Extra Features.
using APIShared;
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
    // Process-lifetime: rooted by CrusaderLibrary.LibraryLoaded and its subscriptions. Native hooks
    // stay installed until process exit and are only switched logically.
    public sealed partial class ExtraFeaturesRuntime
    {
        private static readonly int GoodsCount = (int)eGoods.Count;
        // Guard windows are measured in simulation ticks, not wall-clock time, so every
        // multiplayer peer expires them at the same tick. Vanilla simulates 40 ticks per
        // game second at normal speed; 80 ticks preserve the previous two-second window.
        private const int ResourceGuardLifetimeTicks = 80;
        private const int StockpileRefundLifetimeTicks = 80;
        private const int MarketBuyAmount = 5;
        private const int MarketBuyShiftAmount = 25;
        private const float VanillaRefundMultiplier = 0.5f;
        // Must match the upper bound of ExtraFeaturesViewModel.CampfirePeasantsLimit.
        internal const int CampfirePeasantsMaximum = 200;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private static KeepBuildRangeRuntime processKeepBuildRangeRuntime;

        internal static bool TryGetLobbyKeepRangeOverride(int currentOverride, out int range)
        {
            range = currentOverride;
            if (processKeepBuildRangeRuntime == null) return false;
            range = processKeepBuildRangeRuntime.PreviewLobby(currentOverride);
            return true;
        }
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        // Process-lifetime mission subscriptions; intentionally never disposed.
        private readonly List<IDisposable> lifecycleSubscriptions = new List<IDisposable>();
        private bool missionLifecycleSubscribed;
        // Keys pack (player, good) without allocation; see ResourceEventKey.
        // Guards the resource-event state below and pendingStockpileRefund: the simulation's
        // building and market hooks use it while settings and lifecycle callbacks clear it.
        // Re-entrant on purpose: adding goods raises OnGoodsyardAddGood on the same thread.
        private readonly object resourceGuardSync = new object();
        private readonly HashSet<long> resourceAddReentryGuards = new HashSet<long>();
        private readonly Dictionary<long, ResourceEventCountGuard> marketBuyResourceGuards = new Dictionary<long, ResourceEventCountGuard>();
        private readonly Dictionary<long, ResourceEventCountGuard> refundResourceGuards = new Dictionary<long, ResourceEventCountGuard>();
        private readonly List<long> activeRefundGuardKeys = new List<long>();
        private readonly List<long> expiredResourceGuardKeys = new List<long>();
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly UnitLimitBridge unitLimitBridge;
        private readonly KnightDismountRuntime knightDismountRuntime;
        private readonly ChurchPriestCountRuntime churchPriestCountRuntime;
        private readonly GatehouseAutomationRuntime gatehouseAutomationRuntime;
        private readonly AIDefenseRepairRuntime aiDefenseRepairRuntime;
        private readonly HealthModifierRuntime healthModifierRuntime;
        private readonly MarketTradeGuardBridge marketTradeGuardBridge;
        private readonly ElevatedMoatRuntime elevatedMoatRuntime;
        private readonly NoKillRewardHook noKillRewardHook;
        private HealerTargetsRuntime healerTargetsRuntime;
        private static BuildingRepairHudRuntime buildingRepairHudRuntime;
        private bool buildingRepairReadinessRegistered;

        private PendingStockpileRefund pendingStockpileRefund;
        private AIMarketVanillaPriceHook aiMarketVanillaPriceHook;
        private MonkAlwaysRunPatch monkAlwaysRunPatch;
        private FearFactorNeutralizationRuntime fearFactorRuntime;
        private PopularityRuntime popularityRuntime;
        private PeriodicGoldRuntime periodicGoldRuntime;
        private WorkerProductionBonusRuntime workerProductionBonusRuntime;
        private PlagueDurationPatch plagueDurationPatch;
        private PlagueApothecarySearchRangePatch plagueApothecarySearchRangePatch;
        private IntPtr libraryHandle;
        private ScanRegion nativeRegion;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool monkAlwaysRunPatchUnavailable;
        private bool hooksSubscribed;
        // Set by every session start, enabled or not; cleared with the map session.
        private readonly Dictionary<string, Action> gatedSettingHandlers;
        private volatile bool sessionStarted;
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
            unitLimitBridge = new UnitLimitBridge(log);
            knightDismountRuntime = new KnightDismountRuntime(log, settings, multiplayerFeatureGate, unitLimitBridge);
            unitLimitBridge.RegisterStateChanged(knightDismountRuntime.OnUnitLimitStateChanged);
            churchPriestCountRuntime = new ChurchPriestCountRuntime(log, settings);
            gatehouseAutomationRuntime = new GatehouseAutomationRuntime(log, settings, multiplayerFeatureGate);
            aiDefenseRepairRuntime = new AIDefenseRepairRuntime(log, settings);
            healthModifierRuntime = new HealthModifierRuntime(log, settings);
            marketTradeGuardBridge = new MarketTradeGuardBridge(log, this);
            elevatedMoatRuntime = new ElevatedMoatRuntime(log);
            noKillRewardHook = new NoKillRewardHook(log);
            gatedSettingHandlers = CreateGatedSettingHandlers();
            settings.SettingChanged += OnSettingChanged;
        }

        public object GatehouseAutomationButton => gatehouseAutomationRuntime.ButtonViewModel;
        public void InitializeNetwork()
        {
            TryRunFeature("keep build range lifecycle", () =>
            {
                if (processKeepBuildRangeRuntime == null)
                    processKeepBuildRangeRuntime = new KeepBuildRangeRuntime(log, settings);
                processKeepBuildRangeRuntime.Initialize();
            });
            // APIShared owns the button, overflow placement and the single troop-action setup hook.
            TryRunFeature("troop action HUD registration", () =>
            {
                knightDismountRuntime.RegisterActionButton();
                if (!APIShared.Presentation.TroopActionHud.TryRegister(ExtraFeaturesPlugin.PluginGuid, "KnightDismount",
                    knightDismountRuntime.RefreshButtonVisibility, out string reason))
                    throw new InvalidOperationException(reason);
            });
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
            TryRunFeature("health lifecycle", healthModifierRuntime.Initialize);
            TryRunFeature("building repair HUD lifecycle", InitializeBuildingRepairHud);
            InitializeMissionLifecycle();
            TryRunFeature("worker production bonus lifecycle", () =>
            {
                if (workerProductionBonusRuntime != null) return;
                var candidate = new WorkerProductionBonusRuntime(log, settings);
                candidate.Register();
                workerProductionBonusRuntime = candidate;
            });
            TryRunFeature("periodic gold lifecycle", () =>
            {
                if (periodicGoldRuntime != null) return;
                var candidate = new PeriodicGoldRuntime(log, settings, new PeriodicGoldGameWorld());
                candidate.Register();
                periodicGoldRuntime = candidate;
            });
            TryRunFeature("popularity lifecycle", () =>
            {
                if (popularityRuntime != null) return;
                var candidate = new PopularityRuntime(log, settings);
                candidate.Register();
                popularityRuntime = candidate;
            });
            // LibraryLoaded runs after every plugin's Awake, so Bugfixes and QoL is now present.
            TryRunFeature("market-purchase guard integration", marketTradeGuardBridge.TryConnect);
        }

        private void InitializeMissionLifecycle()
        {
            if (missionLifecycleSubscribed)
                return;
            missionLifecycleSubscribed = true;

            // Registered once for the process and never part of the toggled subscriptions:
            // the activation gate closes before MissionEvents delivers End to observers, so a
            // toggled Ended subscription would already be gone when the mission ends.
            try
            {
                lifecycleSubscriptions.Add(
                    Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted));
                lifecycleSubscriptions.Add(Shared.MissionEvents.Ended.Subscribe(OnUnloadMap));
                lifecycleSubscriptions.Add(Shared.MissionEvents.Initialization
                    .Where(e => e.Phase == APIShared.MissionInitializationPhase.NativeLoaded)
                    .Subscribe(_ => ClearResourceEventGuards()));
            }
            catch (Exception ex)
            {
                LogFeatureFailure("mission lifecycle subscriptions", ex);
            }
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
            nativeRegion = context.Region;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;
            elevatedMoatRuntime.InitializeNative(context, fixedLayoutHashValidated);
            TryRunFeature("No Kill Reward native hooks", () => noKillRewardHook.Install(context));
            TryRunFeature("Bedouin healer targets", () =>
                healerTargetsRuntime = HealerTargetsRuntime.Install(context, log));

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
            TryRunFeature("fear-factor neutralization", () =>
            {
                fearFactorRuntime = new FearFactorNeutralizationRuntime(log, context);
                fearFactorRuntime.Apply(fixedLayoutHashValidated);
                ApplyFearFactorSetting();
            });
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
            ReconcileKnightAndElevatedMoat();
        }

        public void ApplySettings()
        {
            popularityRuntime?.RefreshActivation();
            ApplyPlagueHeightSettings();
            processKeepBuildRangeRuntime?.Refresh();
            TryRunFeature("building repair HUD configuration", ReconcileBuildingRepairHud);
            ApplyNoKillRewardSetting();
            ApplyHealerTargetsSetting();
            ReconcileElevatedMoatRuntime();
            TryRunFeature("fear-factor configuration", ApplyFearFactorSetting);
            TryRunFeature("AI defense repair configuration", ReconcileAIDefenseRepairRuntime);
            ApplyPlagueApothecarySearchRangeSetting();
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
            TryRunFeature("health", ReconcileHealthModifierRuntime);
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
                    log);
                CaptureAIMarketSessionSettings();
            }
            catch (Exception ex)
            {
                aiMarketVanillaPriceHook?.Dispose();
                aiMarketVanillaPriceHook = null;
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/InstallAIMarketVanillaPriceHook", "Extra Features AI Vanilla market-price override is disabled for this process; " +
                    $"the AI uses the active global prices and all other Extra Features remain active: {ex}");
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
            TryRunFeature("health tick", ReconcileHealthModifierRuntime);
            hooksSubscribed = true;
            ReconcileKnightAndElevatedMoat();
            Shared.DebugLogHelper.LogDebug(log, "Extra Features feature hooks reconciled.");
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
                TryRunFeature("event subscription cleanup", subscription.Dispose);
            subscriptions.Clear();
            TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Deactivate);
            TryRunFeature("health cleanup", healthModifierRuntime.ResetMapState);
            ClearResourceEventGuards();
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
            Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/LogFeatureFailure", $"Extra Features feature '{featureName}' failed and remains inactive: {ex}");
        }

        private void ReconcileKnightAndElevatedMoat()
        {
            ReconcileElevatedMoatRuntime();

            if (!nativeLibraryAvailable || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Deactivate);
                return;
            }

            // Knight mount/dismount uses the public bidirectional link API plus a managed,
            // Vanilla-equivalent TotalHorses transition; neither requires a hash-bound delegate.
            if (settings.EnableKnightDismount)
                TryRunFeature("knight mount/dismount", knightDismountRuntime.Initialize);
            else
                TryRunFeature("knight mount/dismount cleanup", knightDismountRuntime.Deactivate);

        }

        private void ReconcileElevatedMoatRuntime()
        {
            TryRunFeature("elevated moat", () => elevatedMoatRuntime.Reconcile(
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.AllowElevatedMoatAI,
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.AllowElevatedMoatHuman));
        }

        private void ApplyNoKillRewardSetting()
        {
            bool enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
            noKillRewardHook.Configure(enabled
                ? new KillRewardConfiguration(settings.KillRewardGoldHuman, settings.KillRewardGoodsHuman, settings.KillRewardGoldMaximumHuman,
                    settings.KillRewardGoldAI, settings.KillRewardGoodsAI, settings.KillRewardGoldMaximumAI)
                : KillRewardConfiguration.Vanilla);
        }

        private void ApplyHealerTargetsSetting()
        {
            bool enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
            healerTargetsRuntime?.SetEnabled(
                enabled && settings.HealCivilianTargets,
                enabled && settings.HealSiegeTargets);
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

        private void InitializeBuildingRepairHud()
        {
            if (buildingRepairReadinessRegistered) return;
            buildingRepairReadinessRegistered = true;
            ApiShared.WhenReady(OnBuildingRepairApiReady);
        }

        private void OnBuildingRepairApiReady(IApiShared api)
        {
            if (buildingRepairHudRuntime != null)
            {
                ReconcileBuildingRepairHud();
                return;
            }

            IBuildingRepairCapability candidate = null;
            try
            {
                if (!api.TryGetBuildingRepair(ExtraFeaturesPlugin.PluginGuid, out candidate,
                    out NativeCapabilityDiagnostic diagnostic))
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/OnBuildingRepairApiReady.unavailable", "Building repair HUD is unavailable: " + diagnostic?.Reason);
                    return;
                }

                candidate.SetActive(false);
                BuildingRepairHudRuntime installed = BuildingRepairHudRuntime.Install(log, candidate);
                buildingRepairHudRuntime = installed;
                ReconcileBuildingRepairHud();
            }
            catch (Exception ex)
            {
                candidate?.SetActive(false);
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/OnBuildingRepairApiReady.installation-failed", "Building repair HUD installation failed: " + ex);
            }
        }

        private void ReconcileBuildingRepairHud()
        {
            bool enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                settings.EnableBuildingRepair;
            buildingRepairHudRuntime?.SetActive(enabled);
        }

        private void OnSettingChanged(string propertyName)
        {
            // The independent gold runtime captures these at completed mission start.
            // Editing an amount must not reapply unrelated native feature settings.
            if (propertyName == nameof(ExtraFeaturesViewModel.FixedProductionBonusPercent) ||
                propertyName == nameof(ExtraFeaturesViewModel.PeriodicGoldHuman) ||
                propertyName == nameof(ExtraFeaturesViewModel.PeriodicGoldAI)) return;
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod)) popularityRuntime?.RefreshActivation();
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod) || propertyName.StartsWith("PlagueDamage", StringComparison.Ordinal))
                ApplyPlagueHeightSettings();
            if (propertyName == nameof(ExtraFeaturesViewModel.KeepBuildRange))
            {
                processKeepBuildRangeRuntime?.Refresh();
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod))
                processKeepBuildRangeRuntime?.Refresh();
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod) ||
                propertyName == nameof(ExtraFeaturesViewModel.EnableFearFactorNeutralization))
                TryRunFeature("fear-factor configuration", ApplyFearFactorSetting);
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod))
            {
                TryRunFeature("building repair HUD configuration", ReconcileBuildingRepairHud);
                ApplyNoKillRewardSetting();
                ApplyHealerTargetsSetting();
                if (Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                {
                    SubscribeHooks();
                    ApplySettings();
                }
                else
                {
                    // Restores Vanilla's distance 30, as ApplySettings does when the mod is disabled.
                    ApplyPlagueApothecarySearchRangeSetting();
                    TryRunFeature("enemy proximity restoration", ReconcileAIDefenseRepairRuntime);
                    TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
                    TryRunFeature("Vanilla value restoration", RestoreDefaultSettings);
                    TryRunFeature("Monks Always Run", ApplyMonkAlwaysRunSetting);
                    TryRunFeature("optional local hooks", UnsubscribeHooks);
                }
                ReconcileElevatedMoatRuntime();
                return;
            }

            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;

            if (propertyName.StartsWith("KillReward", StringComparison.Ordinal))
            {
                ApplyNoKillRewardSetting();
                return;
            }
            if (gatedSettingHandlers.TryGetValue(propertyName, out Action handler))
            {
                handler();
                return;
            }

            // Unlisted settings (refunds, goods, market, knight costs, AI rebuild delay, enemy
            // proximity) need the full pass: it also installs lazily required hooks.
            ApplySettings();
        }

        // Settings with a dedicated reconciliation while the mod is enabled; each replaces the full pass.
        private Dictionary<string, Action> CreateGatedSettingHandlers()
        {
            Action noKillReward = ApplyNoKillRewardSetting;
            Action healerTargets = ApplyHealerTargetsSetting;
            Action elevatedMoat = ReconcileElevatedMoatRuntime;
            Action gatehouse = () => TryRunFeature("gatehouse automation", gatehouseAutomationRuntime.ApplySettings);
            Action health = () => TryRunFeature("health", ReconcileHealthModifierRuntime);
            return new Dictionary<string, Action>(StringComparer.Ordinal)
            {
                [nameof(ExtraFeaturesViewModel.EnableBuildingRepair)] =
                    () => TryRunFeature("building repair HUD configuration", ReconcileBuildingRepairHud),
                [nameof(ExtraFeaturesViewModel.NoKillRewardHuman)] = noKillReward,
                [nameof(ExtraFeaturesViewModel.NoKillRewardAI)] = noKillReward,
                [nameof(ExtraFeaturesViewModel.HealCivilianTargets)] = healerTargets,
                [nameof(ExtraFeaturesViewModel.HealSiegeTargets)] = healerTargets,
                [nameof(ExtraFeaturesViewModel.EnableKnightDismount)] = () =>
                {
                    ReconcileKnightAndElevatedMoat();
                    TryRunFeature("knight mount/dismount visibility", knightDismountRuntime.RefreshButtonVisibility);
                },
                [nameof(ExtraFeaturesViewModel.AllowElevatedMoatAI)] = elevatedMoat,
                [nameof(ExtraFeaturesViewModel.AllowElevatedMoatHuman)] = elevatedMoat,
                [nameof(ExtraFeaturesViewModel.HumanGateReopenDelaySeconds)] = gatehouse,
                [nameof(ExtraFeaturesViewModel.AIGateReopenDelaySeconds)] = gatehouse,
                [nameof(ExtraFeaturesViewModel.HumanGateClosingDistanceTiles)] = gatehouse,
                [nameof(ExtraFeaturesViewModel.AIGateClosingDistanceTiles)] = gatehouse,
                [nameof(ExtraFeaturesViewModel.EnableExtraChurchPriests)] =
                    () => TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting),
                [nameof(ExtraFeaturesViewModel.CampfirePeasantsLimit)] =
                    () => TryRunFeature("campfire peasants", ApplyCampfirePeasantsLimit),
                [nameof(ExtraFeaturesViewModel.EnableMonksAlwaysRun)] = ApplyMonkAlwaysRunSetting,
                [nameof(ExtraFeaturesViewModel.PlagueDurationMultiplier)] = ApplyPlagueDurationSetting,
                [nameof(ExtraFeaturesViewModel.ApothecaryPlagueSearchDistance)] = () =>
                {
                    ApplyPlagueApothecarySearchRangeSetting();
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/CreateGatedSettingHandlers", $"Extra Features apothecary plague-search distance changed to " +
                            $"{settings.ApothecaryPlagueSearchDistance}.");
                },
                [nameof(ExtraFeaturesViewModel.HumanLordHealthPercent)] = health,
                [nameof(ExtraFeaturesViewModel.AILordHealthPercent)] = health,
                [nameof(ExtraFeaturesViewModel.HumanSoldierHealthPercent)] = health,
                [nameof(ExtraFeaturesViewModel.AISoldierHealthPercent)] = health,
                [nameof(ExtraFeaturesViewModel.HumanCivilianHealthPercent)] = health,
                [nameof(ExtraFeaturesViewModel.AICivilianHealthPercent)] = health,
                [nameof(ExtraFeaturesViewModel.HumanBuildingHealthPercent)] = health,
                [nameof(ExtraFeaturesViewModel.AIBuildingHealthPercent)] = health
            };
        }

        private void OnModeStateChanged(bool allowed)
        {
            if (!allowed)
                ResetMapSession();

            ApplySettings();
        }

        // Shared by gate closure and the permanent mission-end subscription; idempotent.
        private void ResetMapSession()
        {
            settings.SetKillRewardMissionLocked(false);
            mapActive = false;
            sessionStarted = false;
            aiMarketVanillaPriceHook?.SetSessionOverride(false);
            healthModifierRuntime.ResetMapState();
            ClearResourceEventGuards();
            multiplayerFeatureGate.Reset();
            gatehouseAutomationRuntime.EndMap();
        }

        private void ApplyMapLoadedSettings(bool editor = false)
        {
            mapActive = true;
            TryRunFeature("gatehouse map initialization", () =>
            {
                if (editor) gatehouseAutomationRuntime.BeginEditorMap();
                else gatehouseAutomationRuntime.BeginMap();
            });
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
            }
            catch (Exception ex)
            {
                try { monkAlwaysRunPatch?.Deactivate(); } catch { }
                monkAlwaysRunPatch = null;
                monkAlwaysRunPatchUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/InitializeMonkAlwaysRunPatch", "Extra Features Monks Always Run is disabled for this process; " +
                    $"all other features remain available: {ex}");
            }
        }

        private void ApplyFearFactorSetting()
        {
            fearFactorRuntime?.SetEnabled(
                FearFactorNeutralizationPolicy.IsEnabled(settings.EnableMod,
                    settings.EnableFearFactorNeutralization, Shared.GameplayModActivationGate.IsAllowed));
        }

        private void ApplyMonkAlwaysRunSetting()
        {
            monkAlwaysRunPatch?.SetEnabled(
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.EnableMonksAlwaysRun);
        }

        private void OnSessionStarted(Shared.GameplaySessionStartedContext context)
        {
            sessionStarted = true;
            settings.SetKillRewardMissionLocked(true);
            // The subscription is process-permanent; only enabled sessions are initialized,
            // exactly as when it was part of the toggled feature subscriptions.
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;

            TryRunFeature("fear-factor army report", () => fearFactorRuntime?.TryAttachArmyReportViewModel());
            multiplayerFeatureGate.CaptureMapMode();

            ApplyMapLoadedSettings(context.IsEditor);

            CaptureAIMarketSessionSettings();
            TryRunFeature("health map initialization", () =>
            {
                ReconcileHealthModifierRuntime();
                if (Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                    healthModifierRuntime.BeginMap(context.SessionId, context.IsLoadedSave);
            });

            // ApplyMapLoadedSettings already started the gatehouse map state for every start kind.
            TryRunFeature("knight mount/dismount visibility", knightDismountRuntime.RefreshButtonVisibility);
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
                    Shared.DebugLogHelper.LogWarningLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/InitializePlagueDurationPatch", "Extra Features plague duration is running on an unknown CrusaderDE.dll because its native instruction signature and Vanilla lifetime were validated.");
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
                plagueApothecarySearchRangePatch = PlagueApothecarySearchRangePatch.Install(
                    log,
                    nativeLibraryHandle,
                    region,
                    memory,
                    fixedLayoutHashValidated);
                ApplyPlagueApothecarySearchRangeSetting();
            }
            catch (Exception ex)
            {
                plagueApothecarySearchRangePatchUnavailable = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/InitializePlagueApothecarySearchRangePatch", $"Extra Features apothecary plague-search range is disabled for this process; " +
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

        private void ApplyPlagueApothecarySearchRangeSetting()
        {
            if (plagueApothecarySearchRangePatch == null ||
                plagueApothecarySearchRangePatchUnavailable)
                return;

            try
            {
                plagueApothecarySearchRangePatch.SetEffectiveMaximum(
                    Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod),
                    settings.ApothecaryPlagueSearchDistance);
            }
            catch (Exception ex)
            {
                plagueApothecarySearchRangePatchUnavailable = true;
                try
                {
                    plagueApothecarySearchRangePatch.SetEffectiveMaximum(
                        false,
                        PlagueApothecarySearchRangePatch.VanillaMaximumDistance);
                }
                catch (Exception fallbackError) { Shared.RuntimeHealth.Report("apothecary-range-fallback", true,
                    "Restoring Vanilla apothecary range failed: " + fallbackError + "; the fallback result is unverified."); }
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/ApplyPlagueApothecarySearchRangeSetting", $"Extra Features apothecary plague-search range is disabled for this process; " +
                    $"Vanilla distance 30 and all other features remain active: {ex}");
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
                    plagueDurationPatch.Deactivate();
                }
                catch (Exception ex)
                {
                    restoreFailure = ex;
                }
            }

            // Keep the reference: a published comparison hook must stay rooted. Every caller
            // checks plagueDurationPatchUnavailable first, so the instance stays logically off.
            string restoreDetails = restoreFailure == null
                ? string.Empty
                : $" Vanilla restoration also failed: {restoreFailure}";
            Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/DisablePlagueDurationPatch", $"Extra Features plague duration multiplier is disabled for this process; all other features remain available: {failure}.{restoreDetails}");
        }

        private void RestoreDefaultSettings()
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            TryRunFeature("wood refund restoration", () => buildingApi.WoodRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("stone refund restoration", () => buildingApi.StoneRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("iron refund restoration", () => buildingApi.IronRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("pitch refund restoration", () => buildingApi.PitchRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("gold refund restoration", () => buildingApi.GoldRefundMultiplier.SetValue(VanillaRefundMultiplier));
            TryRunFeature("market price restoration", () => RestoreTradeBasePrices());
            TryRunFeature("church priest restoration", churchPriestCountRuntime.ApplySetting);
            TryRunFeature("campfire peasants restoration", RestoreCampfirePeasantsCap);
            TryRunFeature("plague duration restoration", ApplyPlagueDurationSetting);
        }

        private void ReconcileHealthModifierRuntime()
        {
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                healthModifierRuntime.ResetMapState();
                return;
            }

            // Keep subscriptions rooted; only a completed session start captures settings.
            // The shared service captures bases even when every contribution is 100%.
            healthModifierRuntime.Initialize();
        }

        private void CaptureAIMarketSessionSettings()
        {
            aiMarketVanillaPriceHook?.SetSessionOverride(mapActive &&
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                !settings.MarketPricesAlsoForAI);
        }

        private ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL stays loaded for the process lifetime.
            return nativeRegion == null ? ReadOnlySpan<byte>.Empty : nativeRegion.Span;
        }

        private void OnUnloadMap(APIShared.MissionLifecycleNotification args)
        {
            TryRunFeature("market price session cleanup", () => RestoreTradeBasePrices(endSession: true));
            TryRunFeature("campfire peasants restoration", RestoreCampfirePeasantsCap);
            TryRunFeature("map-end cleanup", ResetMapSession);
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
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/ApplyCampfirePeasantsLimit.campfire-peasants-setting-deferred", "Extra Features campfire peasants setting deferred until a map is active.");
                return;
            }

            if (GameGlobalsManager.Instance.CampPeasantsCap == null)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/ApplyCampfirePeasantsLimit.campfire-peasants-setting-skipped", "Extra Features campfire peasants setting skipped: CampPeasantsCap global was not found.");
                return;
            }

            if (!originalCampPeasantsCap.HasValue)
                originalCampPeasantsCap = GameGlobalsManager.Instance.CampPeasantsCap.GetValue();

            ushort value = (ushort)Math.Min(CampfirePeasantsMaximum, Math.Max(0, settings.CampfirePeasantsLimit));
            GameGlobalsManager.Instance.CampPeasantsCap.SetValue(value);
            Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/ApplyCampfirePeasantsLimit.applied-campfire-peasants-limit", "Extra Features applied campfire peasants limit:", value);
        }

        // sessionStarted also covers a session that started while the mod was disabled and is
        // enabled later. It replaces a map-name probe that could latch a previous map's name
        // during the next map's initialization.
        private bool IsMapActive() => mapActive || sessionStarted;

        private void RestoreCampfirePeasantsCap()
        {
            if (!originalCampPeasantsCap.HasValue || GameGlobalsManager.Instance.CampPeasantsCap == null)
                return;

            GameGlobalsManager.Instance.CampPeasantsCap.SetValue(originalCampPeasantsCap.Value);
            Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/ExtraFeaturesRuntime/RestoreCampfirePeasantsCap", "Extra Features restored campfire peasants limit:", originalCampPeasantsCap.Value);
            originalCampPeasantsCap = null;
        }

        // Same identity as the former "player:good" text key, without per-event allocation.
        private static long ResourceEventKey(int playerId, eGoods good) =>
            ((long)playerId << 32) | (uint)(int)good;

        private void ClearResourceEventGuards()
        {
            lock (resourceGuardSync)
            {
                activeRefundGuardKeys.Clear();
                resourceAddReentryGuards.Clear();
                marketBuyResourceGuards.Clear();
                refundResourceGuards.Clear();
                pendingStockpileRefund = null;
            }
        }

        private sealed class PendingStockpileRefund
        {
            public int PlayerId;
            public int Owner;
            public int? CreatedTick;
            public int PartsRemaining;
            public HashSet<int> ProcessedBuildingIds = new HashSet<int>();
        }

        private sealed class ResourceEventCountGuard
        {
            public int RemainingAmount { get; set; }
            public int? CreatedTick { get; set; }
        }

        private static int? CurrentGameTick() =>
            Shared.MissionEvents.TryGetCurrentMapTick(out int tick) ? tick : (int?)null;

        // Unanchored guards belong to a synchronous initialization operation. Post/End clears
        // them; unavailable time must never expire half of an active transaction.
        private static bool IsTickWindowExpired(int? createdTick, int? nowTick, int lifetimeTicks)
        {
            if (!createdTick.HasValue || !nowTick.HasValue) return false;
            return !APIShared.Simulation.SimulationClock.TryGetElapsedTicks(createdTick.Value, nowTick.Value, out int elapsed) ||
                elapsed >= lifetimeTicks;
        }
    }
}
