using APIShared.ModSettings;
// Feature: Plugin bootstrap for the Bugfixes and QoL mod.
using BepInEx;
using BepInEx.Bootstrap;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    [BepInDependency(ScriptExtenderGuid, "2.14.3")]
    [BepInDependency(ApiSharedGuid, "0.35.0")]
    [BepInDependency("ActiveAIVDetector_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(LegacySomeSettingsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SavegameMissingMapFixTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("AIKeepRangeLimitTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility(PreplacedTestGuid)]
    [BepInIncompatibility("GatehouseLivingCaptureTest_Serp")]
    [BepInIncompatibility("DrawbridgeDestructionFixTest_Serp")]
    [BepInIncompatibility("LordTeleportGuardTest_Serp")]
    [BepInIncompatibility("PitchStairsTest_Serp")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BugfixesAndQoLPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ApiSharedGuid = "APIShared_Serp";
        private const string LegacySomeSettingsGuid = "SomeSettings_Serp";
        private const string PreplacedTestGuid = "PreplacedTest_Serp";

        public const string PluginGuid = "BugfixesAndQoL_Serp";
        public const string PluginName = "Bugfixes and QoL";
        public const string PluginVersion = "1.0.215";

        private static DisplayResolutionPersistenceHook displayResolutionPersistenceHook;
        private static DamagedHealthBarsRuntime damagedHealthBarsRuntime;
        private static StartupUiReadinessGuardHook startupUiReadinessGuardHook;
        private static ResolutionAwareZoomHook resolutionAwareZoomHook;
        private static SteamLobbyInvitePrompt steamLobbyInvitePrompt;
        private static SteamInviteBlacklistStore steamInviteBlacklist;
        private static IDisposable gameplaySessionSubscription;
        private static SkirmishGameOptionsRuntime skirmishGameOptionsRuntime;
        private static SkirmishGameOptionsAccessViewModel skirmishGameOptionsAccess;
        private static VanillaPeaceTimeGameplayPatch vanillaPeaceTimeGameplayPatch;
        private static PeaceTimeCivilianProximityFeature peaceTimeCivilianProximityFeature;
        private static TimerCountdownFeature timerCountdownFeature;
        private static TrailDeletionViewModel trailDeletionViewModel;
        private static NoDogsNativePatch noDogsNativePatch;
        private static bool skirmishGameOptionsCompatible;
        private BugfixesAndQoLRuntime runtime;
        private static StatisticsTeamBadges statisticsTeamBadges;
        private bool marketGoodsVisualRefreshFailureLogged;

        public BugfixesAndQoLViewModel Settings { get; private set; }

        /// <summary>Read-only next-game capability, including installation and classification failures.</summary>
        public static bool TryGetLobbyAIKeepRangeBypass(out bool bypass) =>
            BugfixesAndQoLRuntime.TryGetLobbyAIKeepRangeBypass(out bypass);

        // The try blocks of the start path stay written out. Several catch blocks have side effects
        // or return early, so a shared helper would only save about 33 lines.
        private void Awake()
        {
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            TrailCustomizationLaunchOriginApi.RegisterModeProvider();
            Shared.CrashBreadcrumbDiagnostics.Initialize(Logger, PluginGuid, PluginName, PluginVersion);
            try
            {
                if (startupUiReadinessGuardHook == null)
                    startupUiReadinessGuardHook = new StartupUiReadinessGuardHook(Logger);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/Awake.startup-ui-readiness-guard", $"Bugfixes and QoL startup UI-readiness guard could not be initialized; Vanilla behavior remains active: {ex}");
            }
            bool legacySomeSettingsLoaded = Chainloader.PluginInfos.ContainsKey(LegacySomeSettingsGuid);
            if (legacySomeSettingsLoaded)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/Awake.obsolete-mod-loaded-together", $"The obsolete mod '{LegacySomeSettingsGuid}' is loaded together with {PluginName}. " +
                    "Uninstall it to avoid duplicate or conflicting features.");
            }

            // Pass the startup result into the view model so the warning occupies no UI space otherwise.
            steamInviteBlacklist = new SteamInviteBlacklistStore(
                SteamInviteBlacklistStore.GetDefaultPath(), SteamInviteBlacklistStore.GetLegacyPath(),
                message => Shared.DebugLogHelper.LogWarningLimited(Logger, "invite-blacklist-storage", message));
            Settings = new BugfixesAndQoLViewModel(legacySomeSettingsLoaded, steamInviteBlacklist, Logger);
            if (Chainloader.PluginInfos.TryGetValue("fixes", out PluginInfo fixes) && fixes.Instance != null)
                FixesSuppressMessagesSync.Initialize(Logger, fixes.Instance.GetType().Assembly);
            LobbySaveLoadingFeature.Initialize(Logger, Settings);
            BuildPreview.BuildPreviewRuntime.Configure(Logger, Settings);
            ArabianIdleSoundRuntime.Configure(Logger, Settings);
            skirmishGameOptionsCompatible = !HasLegacyExtraFeaturesPeaceTimeSetting();
            skirmishGameOptionsAccess = new SkirmishGameOptionsAccessViewModel(
                Settings,
                skirmishGameOptionsCompatible,
                Logger);
            if (skirmishGameOptionsCompatible)
            {
                skirmishGameOptionsRuntime = new SkirmishGameOptionsRuntime(Logger, Settings);
            }
            else
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/Awake.skirmish-game-options-disabled", "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_DISABLED_LEGACY_EXTRAFEATURES: " +
                    "the installed ExtraFeatures still owns the synchronized Peace Time setting. " +
                    "Update ExtraFeatures before enabling the integrated Singleplayer Game Options dialog.");
            }
            try
            {
                if (resolutionAwareZoomHook == null)
                    resolutionAwareZoomHook = new ResolutionAwareZoomHook(Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/Awake.resolution-aware-extended-zoom", $"Bugfixes and QoL resolution-aware extended zoom could not be initialized; Vanilla zoom remains active: {ex}");
            }
            try
            {
                // Install before FatControler.Start loads settings.cfg and begins screen monitoring.
                displayResolutionPersistenceHook =
                    new DisplayResolutionPersistenceHook(Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/Awake.display-resolution-persistence-could", $"Bugfixes and QoL display-resolution persistence could not be initialized; Vanilla behavior remains active: {ex}");
            }
            runtime = new BugfixesAndQoLRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void InitializePersistentUiAndMapCallbacks()
        {
            // These publishers outlive the BepInEx component and retain the callbacks for the process lifetime.
            try
            {
                SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel.PropertyChanged +=
                    (_, __) =>
                    {
                        // The game fills MainViewModel.GameSprites after the script extender loads.
                        // A hub change, including opening the settings, is the safe point to retry visuals.
                        try
                        {
                            var hub = SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel;
                            if (Settings.IsCapturingHealthBarHotkey &&
                                (hub.WindowVisibility != Noesis.Visibility.Visible ||
                                 !ReferenceEquals(hub.SelectedTab?.ViewModel, Settings)))
                                Settings.CancelHealthBarCapture();
                            Settings.RefreshStatisticsTeamBadgePreviewVisual();
                            Settings.RefreshMarketGoodsOrderVisuals();
                        }
                        catch (Exception ex)
                        {
                            // A visual retry must never escape through PropertyChanged and abort tab registration.
                            if (!marketGoodsVisualRefreshFailureLogged)
                            {
                                marketGoodsVisualRefreshFailureLogged = true;
                                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializePersistentUiAndMapCallbacks.market-goods-visual-refresh", $"Bugfixes and QoL market-goods visual refresh failed; text controls remain usable: {ex}");
                            }
                        }
                    };
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializePersistentUiAndMapCallbacks.market-goods-visual-callback", $"Bugfixes and QoL market-goods visual callback could not be registered; other callbacks continue: {ex}");
            }

            try
            {
                if (gameplaySessionSubscription == null)
                {
                    gameplaySessionSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                        Logger,
                        _ => steamLobbyInvitePrompt?.TryInitialize());
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializePersistentUiAndMapCallbacks.steam-invite-map-callback", $"Bugfixes and QoL Steam invite map callback could not be registered; other callbacks continue: {ex}");
            }
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter("LibraryLoaded"))
            {
            // Initial native installation window (see BugfixesHookInfrastructure.DetourBackend).
            HookInstallPhase.InitialLibraryLoad = true;
            bool completed;
            try { completed = InitializeLibraryRuntime(context); }
            finally { HookInstallPhase.InitialLibraryLoad = false; }
            // A fail-closed stop inside the runtime initialization leaves the breadcrumb incomplete, as before.
            if (!completed) return;
            diagnostic.Complete();
            }
        }

        // Runs inside the initial native installation window; false = stopped fail-closed.
        private bool InitializeLibraryRuntime(CrusaderLibraryLoadContext context)
        {
            // Register packet types immediately after Script Extender and before settings can vary.
            try
            {
                runtime.InitializeNetwork();
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLAivSyncStatusHost",
                    runtime.MultiplayerAivSyncUi);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.chore-registration-failed-synchronized", $"Bugfixes and QoL Chore registration failed; synchronized game-speed changes remain unavailable: {ex}");
            }

            // Construct the editor rows before subscribing to hub changes, so even an early
            // settings event can only refresh an already complete 20-item collection.
            try
            {
                Settings.InitializeMarketGoodsOrderEditor(Logger);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.market-order-editor-initialization", $"Bugfixes and QoL market-order editor initialization failed: {ex}");
            }

            try
            {
                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Shared.DebugLogHelper.ForExternalCode(Logger),
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/BugfixesAndQoLSettings.xaml",
                    logRoutineActivity: Shared.DebugLogHelper.DiagnosticsEnabled);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.settings-registration-failed-gameplay", $"Bugfixes and QoL settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return false;
            }

            try
            {
                if (trailDeletionViewModel == null)
                    trailDeletionViewModel = new TrailDeletionViewModel(Logger, Settings);
                GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLDeleteTrailHost", trailDeletionViewModel);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.trail-deletion-binding-failed", "Bugfixes and QoL Trail deletion binding failed: " + ex);
            }
            InitializePersistentUiAndMapCallbacks();

            try
            {
                statisticsTeamBadges = new StatisticsTeamBadges(Logger, Settings);
                statisticsTeamBadges.Initialize();
            }
            catch (Exception error) { Shared.DebugLogHelper.LogErrorLimited(Logger, "statistics-badges-init", "Statistics team badges unavailable: " + error); }

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLSkirmishGameOptionsButtonHost",
                    skirmishGameOptionsAccess);
                skirmishGameOptionsRuntime?.Initialize();
            }
            catch (Exception ex)
            {
                skirmishGameOptionsRuntime = null;
                skirmishGameOptionsAccess.SetRuntimeAvailable(false);
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.skirmish-game-options-managed", "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_MANAGED_FAILED: " + ex);
            }

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLQuarryPileRelocationButtonHost",
                    runtime.QuarryPileRelocationButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.quarry-button-binding-failed", $"Bugfixes and QoL quarry button binding failed: {ex}");
            }

            try
            {
                runtime.InitializeSurrenderFeature();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.surrender-ui-network-initialization", $"Bugfixes and QoL surrender UI/network initialization failed; surrender remains inactive: {ex}");
            }

            try
            {
                runtime.InitializeSelectedUnitHealthFeature();
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLSelectedUnitHealthHost",
                    runtime.SelectedUnitHealthUi);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.selected-unit-health-ui", $"Bugfixes and QoL selected-unit health UI initialization failed; the display remains hidden: {ex}");
            }

            try
            {
                if (timerCountdownFeature == null)
                    timerCountdownFeature = new TimerCountdownFeature(Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.countdown-ui-initialization-failed", "Bugfixes and QoL countdown UI initialization failed: " + ex);
            }

            // Keep UI registration independent so one native feature cannot hide the whole mod.
            BuildPreview.BuildPreviewRuntime.Install(context);
            GatehouseTargetMarkerHeightHook.Install(context, Logger, Settings);
            try
            {
                EditorRegions.EditorRegionRefreshRuntime.Install(context, Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, "EDITOR_REGIONS_INSTALL_FAILED: Vanilla behavior retained; " + ex);
            }
            try
            {
                VanillaPerformance.VanillaPerformanceFeature.Install(Logger, Settings, context);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.vanilla-performance-disabled-vanilla", "BUGFIXES_AND_QOL_VANILLA_PERFORMANCE_DISABLED: Vanilla behavior retained; " + ex);
            }
            try
            {
                if (damagedHealthBarsRuntime == null)
                    damagedHealthBarsRuntime = DamagedHealthBarsRuntime.Install(context, Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.damaged-health-bars-disabled", "BUGFIXES_AND_QOL_DAMAGED_HEALTH_BARS_DISABLED: " + ex);
            }

            try
            {
                runtime.InitializeNative(
                    context,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.native-runtime-initialization-failed", $"Bugfixes and QoL native runtime initialization failed; unaffected features may continue: {ex}");
            }

            try
            {
                if (peaceTimeCivilianProximityFeature == null)
                    peaceTimeCivilianProximityFeature = new PeaceTimeCivilianProximityFeature(Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, "PEACE_CIVILIAN_PROXIMITY_UNAVAILABLE: existing building rules remain active; " + ex);
            }

            if (skirmishGameOptionsCompatible)
            {
                bool currentNative = Shared.DebugLogHelper.IsCurrentNativeLibraryVersion();
                try
                {
                    vanillaPeaceTimeGameplayPatch = new VanillaPeaceTimeGameplayPatch(
                        Logger,
                        context.ModuleHandle,
                        context.Region,
                        context.Memory,
                        currentNative, Settings);
                    skirmishGameOptionsRuntime?.SetPeaceTimeNativeAvailable(true);
                }
                catch (Exception ex)
                {
                    skirmishGameOptionsRuntime?.SetPeaceTimeNativeAvailable(false);
                    Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.vanilla-peace-time-patch", "BUGFIXES_AND_QOL_VANILLA_PEACE_TIME_PATCH_FAILED: " + ex);
                }

                if (skirmishGameOptionsRuntime != null)
                {
                    try
                    {
                        noDogsNativePatch = new NoDogsNativePatch(
                            Logger,
                            context.ModuleHandle,
                            context.Region,
                            context.Memory,
                            currentNative, Settings);
                        skirmishGameOptionsRuntime.SetNoDogsNativeAvailable(true);
                    }
                    catch (Exception ex)
                    {
                        skirmishGameOptionsRuntime.SetNoDogsNativeAvailable(false);
                        Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.skirmish-game-options-no", "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_NO_DOGS_PATCH_FAILED: " + ex);
                    }
                }
            }

            try
            {
                object allyGoodsAmountDisplay = runtime.AllyGoodsAmountDisplay;
                if (allyGoodsAmountDisplay != null)
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount5", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount10", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount25", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount100", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount500", allyGoodsAmountDisplay);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.ally-amount-display-binding", $"Bugfixes and QoL ally amount display binding failed: {ex}");
            }

            try
            {
                steamLobbyInvitePrompt = new SteamLobbyInvitePrompt(Logger, Settings, steamInviteBlacklist);
                steamLobbyInvitePrompt.TryInitialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.steam-lobby-invite-prompt", $"Bugfixes and QoL Steam lobby-invite prompt could not be initialized; Vanilla invite handling remains active: {ex}");
            }

            try
            {
                runtime.ApplySettings();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Bugfixes and QoL initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BugfixesAndQoL/BugfixesAndQoLPlugin/InitializeLibraryRuntime.settings-reconciliation-failed-already", $"Bugfixes and QoL settings reconciliation failed; already initialized features remain active: {ex}");
            }
            // Results of a one-time native fallback (unknown game build) are reused on the next start.
            BfqolNativeCache.Save();
            return true;
        }

        private static bool HasLegacyExtraFeaturesPeaceTimeSetting()
        {
            if (!Chainloader.PluginInfos.TryGetValue("ExtraFeatures_Serp", out PluginInfo pluginInfo))
                return false;

            try
            {
                object plugin = pluginInfo.Instance;
                Type settingsType = null;
                if (plugin != null)
                {
                    PropertyInfo settingsProperty = plugin.GetType().GetProperty(
                        "Settings",
                        BindingFlags.Instance | BindingFlags.Public);
                    settingsType = settingsProperty?.GetValue(plugin)?.GetType();
                    if (settingsType == null)
                    {
                        settingsType = plugin.GetType().Assembly.GetType(
                            "ExtraFeatures.ExtraFeaturesViewModel",
                            throwOnError: false);
                    }
                }

                if (settingsType == null)
                    return true;

                PropertyInfo peaceProperty = settingsType.GetProperty(
                    "VanillaPeaceTimeMinutes",
                    BindingFlags.Instance | BindingFlags.Public);
                if (peaceProperty == null)
                    return false;

                foreach (object attribute in peaceProperty.GetCustomAttributes(false))
                {
                    if (string.Equals(
                            attribute.GetType().Name,
                            "SyncHostOnlyAttribute",
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                // Unknown ExtraFeatures metadata must not be allowed to install a second native
                // Peace Time owner. Other BugfixesAndQoL features remain available.
                return true;
            }
        }
    }
}
