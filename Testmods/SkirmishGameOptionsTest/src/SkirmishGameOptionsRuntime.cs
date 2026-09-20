using BepInEx.Logging;
using CrusaderDE;
using ExtraFeatures;
using MonoMod.RuntimeDetour;
using Noesis;
using System;
using System.Reflection;

namespace SkirmishGameOptionsTest
{
    internal sealed class SkirmishGameOptionsRuntime
    {
        private delegate void MultiplayerButtonClickedDelegate(FRONT_Multiplayer self, string command);
        private delegate void MainViewModelObjectCommandDelegate(MainViewModel self, object parameter);
        private delegate void SetUpInbuildingDelegate(MainViewModel self, int overridePanel, int overrideType);
        private delegate void ShowAmmoOrdersDelegate(HUD_Troops self);

        private static readonly FieldInfo AuthoritativeSetupDataField = RequireField(
            typeof(FRONT_Multiplayer),
            "MPsetupData");
        private static readonly FieldInfo TemporarySetupDataField = RequireField(
            typeof(FRONT_Multiplayer),
            "MPTEMPsetupData");
        private static readonly MethodInfo UpdateHostInfoMethod = RequireMethod(
            typeof(FRONT_Multiplayer),
            "UpdateHostInfo",
            typeof(bool));
        private static readonly MethodInfo SetupSkirmishModeSettingsMethod = RequireMethod(
            typeof(FRONT_Multiplayer),
            "SetupSkirmishModeSettings");

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel extraFeaturesSettings;
        private readonly WorkingCopyTransaction<EngineInterface.MultiplayerSetupData>
            setupTransaction;

        // The plugin owns this runtime statically. These hooks intentionally remain installed
        // until process exit and are never tied to a Unity component teardown path.
        private Hook multiplayerButtonClickedHook;
        private Hook launchCowHook;
        private Hook buySellHook;
        private Hook setUpInbuildingHook;
        private Hook showAmmoOrdersHook;
        private MultiplayerButtonClickedDelegate multiplayerButtonClickedOriginal;
        private MainViewModelObjectCommandDelegate launchCowOriginal;
        private MainViewModelObjectCommandDelegate buySellOriginal;
        private SetUpInbuildingDelegate setUpInbuildingOriginal;
        private ShowAmmoOrdersDelegate showAmmoOrdersOriginal;
        private bool synchronizingPeaceTime;
        private bool noDogsNativeAvailable;
        private bool noDogsUnavailableLogged;
        private bool outpostsUnavailableLogged;
        private bool initialized;

        internal SkirmishGameOptionsRuntime(
            ManualLogSource log,
            ExtraFeaturesViewModel extraFeaturesSettings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.extraFeaturesSettings = extraFeaturesSettings ??
                throw new ArgumentNullException(nameof(extraFeaturesSettings));
            setupTransaction =
                new WorkingCopyTransaction<EngineInterface.MultiplayerSetupData>(CloneSetupData);
        }

        internal void Initialize()
        {
            if (initialized)
                return;

            Hook pendingMultiplayer = null;
            Hook pendingLaunchCow = null;
            Hook pendingBuySell = null;
            Hook pendingSetUpInbuilding = null;
            Hook pendingShowAmmoOrders = null;
            try
            {
                pendingMultiplayer = new Hook(
                    RequireMethod(typeof(FRONT_Multiplayer), "ButtonClicked", typeof(string)),
                    (MultiplayerButtonClickedDelegate)MultiplayerButtonClickedHook);
                MultiplayerButtonClickedDelegate multiplayerOriginal =
                    pendingMultiplayer.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

                pendingLaunchCow = new Hook(
                    RequireMethod(typeof(MainViewModel), "ButtonUnitLaunchCow", typeof(object)),
                    (MainViewModelObjectCommandDelegate)ButtonUnitLaunchCowHook);
                MainViewModelObjectCommandDelegate cowOriginal =
                    pendingLaunchCow.GenerateTrampoline<MainViewModelObjectCommandDelegate>();

                pendingBuySell = new Hook(
                    RequireMethod(typeof(MainViewModel), "ButtonBuySell", typeof(object)),
                    (MainViewModelObjectCommandDelegate)ButtonBuySellHook);
                MainViewModelObjectCommandDelegate buySellOriginalCandidate =
                    pendingBuySell.GenerateTrampoline<MainViewModelObjectCommandDelegate>();

                pendingSetUpInbuilding = new Hook(
                    RequireMethod(typeof(MainViewModel), "setUpInbuilding", typeof(int), typeof(int)),
                    (SetUpInbuildingDelegate)SetUpInbuildingHook);
                SetUpInbuildingDelegate setupOriginal =
                    pendingSetUpInbuilding.GenerateTrampoline<SetUpInbuildingDelegate>();

                pendingShowAmmoOrders = new Hook(
                    RequireMethod(typeof(HUD_Troops), "ShowAmmoOrders"),
                    (ShowAmmoOrdersDelegate)ShowAmmoOrdersHook);
                ShowAmmoOrdersDelegate ammoOriginal =
                    pendingShowAmmoOrders.GenerateTrampoline<ShowAmmoOrdersDelegate>();

                multiplayerButtonClickedHook = pendingMultiplayer;
                multiplayerButtonClickedOriginal = multiplayerOriginal;
                launchCowHook = pendingLaunchCow;
                launchCowOriginal = cowOriginal;
                buySellHook = pendingBuySell;
                buySellOriginal = buySellOriginalCandidate;
                setUpInbuildingHook = pendingSetUpInbuilding;
                setUpInbuildingOriginal = setupOriginal;
                showAmmoOrdersHook = pendingShowAmmoOrders;
                showAmmoOrdersOriginal = ammoOriginal;
                extraFeaturesSettings.SettingChanged += OnExtraFeaturesSettingChanged;
                initialized = true;
            }
            catch
            {
                pendingShowAmmoOrders?.Dispose();
                pendingSetUpInbuilding?.Dispose();
                pendingBuySell?.Dispose();
                pendingLaunchCow?.Dispose();
                pendingMultiplayer?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "SKIRMISH_GAME_OPTIONS_TEST_MANAGED_READY: lobby, cow and auto-trading hooks installed.");
        }

        internal void SetNoDogsNativeAvailable(bool available)
        {
            noDogsNativeAvailable = available;
            if (available)
            {
                noDogsUnavailableLogged = false;
                return;
            }

            Shared.DebugLogHelper.LogWarning(
                log,
                "SKIRMISH_GAME_OPTIONS_TEST_NO_DOGS_DISABLED: native validation or patch installation failed; all other options remain available.");
        }

        private void MultiplayerButtonClickedHook(FRONT_Multiplayer self, string command)
        {
            if (self == null || !FRONT_Multiplayer.skirmishGame)
            {
                multiplayerButtonClickedOriginal(self, command);
                return;
            }

            if (string.Equals(command, "Setup", StringComparison.Ordinal))
            {
                OpenSkirmishOptions(self);
                return;
            }

            if (string.Equals(command, "Settings_Dogs", StringComparison.Ordinal) &&
                !SkirmishGameOptionsPolicy.ShouldAllowNoDogsToggle(noDogsNativeAvailable))
            {
                MainViewModel.Instance.MPSettings_Dogs_Opacity = 0.3f;
                if (!noDogsUnavailableLogged)
                {
                    noDogsUnavailableLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "SKIRMISH_GAME_OPTIONS_TEST_NO_DOGS_CLICK_REJECTED: the native mode-99 gate is unavailable.");
                }
                return;
            }

            if (string.Equals(command, "Settings_AllowOutposts", StringComparison.Ordinal) &&
                !SkirmishGameOptionsPolicy.ShouldAllowOutpostToggle(
                    localSkirmish: true,
                    MainViewModel.Instance.Show_SkirmishAllowOutposts))
            {
                MainViewModel.Instance.MPSettings_AllowOutposts_Opacity = 0.3f;
                if (!outpostsUnavailableLogged)
                {
                    outpostsUnavailableLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "SKIRMISH_GAME_OPTIONS_TEST_OUTPOSTS_CLICK_REJECTED: the selected map does not support outposts.");
                }
                return;
            }

            if (string.Equals(command, "ApplySettings", StringComparison.Ordinal))
            {
                ApplySkirmishOptions(self);
                return;
            }

            if (string.Equals(command, "CancelSettings", StringComparison.Ordinal))
            {
                try
                {
                    multiplayerButtonClickedOriginal(self, command);
                }
                finally
                {
                    setupTransaction.Cancel();
                }
                RefreshAllSkirmishViews(self);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "SKIRMISH_GAME_OPTIONS_TEST_CANCEL: working settings discarded.");
                return;
            }

            if (string.Equals(command, "CloseSkirmishAdvanced", StringComparison.Ordinal))
            {
                multiplayerButtonClickedOriginal(self, command);
                NormalizeCommittedAdvancedState(self);
                UpdateHostInfoMethod.Invoke(self, new object[] { false });
                RefreshAllSkirmishViews(self);
                return;
            }

            if (MainViewModel.Instance.Show_MPSettings &&
                SkirmishGameOptionsPolicy.IsPresetSaveCommand(command))
            {
                SaveSharedPreset(self, command);
                return;
            }

            if (MainViewModel.Instance.Show_MPSettings &&
                SkirmishGameOptionsPolicy.IsWorkingCopyCommand(command))
            {
                RouteCommandToWorkingCopy(self, command);
                if (SkirmishGameOptionsPolicy.IsPresetLoadCommand(command))
                    NormalizeLoadedSharedPreset(self, command);
                return;
            }

            multiplayerButtonClickedOriginal(self, command);
        }

        private void OpenSkirmishOptions(FRONT_Multiplayer self)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(self);
            if (committed == null)
                throw new InvalidOperationException("Skirmish MPsetupData is unavailable.");

            EngineInterface.MultiplayerSetupData shadow = setupTransaction.Begin(committed);
            shadow.advanced_options = shadow.advanced_skirmish_options;
            if (extraFeaturesSettings.EnableMod)
                shadow.peacetime = extraFeaturesSettings.VanillaPeaceTimeMinutes;

            AuthoritativeSetupDataField.SetValue(self, shadow);
            try
            {
                multiplayerButtonClickedOriginal(self, "Setup");
            }
            catch
            {
                setupTransaction.Cancel();
                throw;
            }
            finally
            {
                AuthoritativeSetupDataField.SetValue(self, committed);
            }

            EngineInterface.MultiplayerSetupData working = GetTemporarySetupData(self);
            if (working == null)
            {
                setupTransaction.Cancel();
                throw new InvalidOperationException("Skirmish MPTEMPsetupData is unavailable.");
            }
            setupTransaction.Attach(working);

            MainViewModel viewModel = MainViewModel.Instance;
            viewModel.Show_MPOnlySettings = true;
            viewModel.Show_MPSettings_MaxPlayers = false;
            viewModel.Show_MPPeacetime = extraFeaturesSettings.EnableMod;
            viewModel.MPSettingHeight = "640";
            viewModel.MPSettings_ExTroops_Opacity =
                SkirmishGameOptionsPolicy.GetExtremeTroopsOpacity(localSkirmish: true);
            bool mapAllowsOutposts = viewModel.Show_SkirmishAllowOutposts;
            viewModel.MPSettings_AllowOutposts_Opacity =
                SkirmishGameOptionsPolicy.GetOutpostOpacity(mapAllowsOutposts);
            outpostsUnavailableLogged = false;
            if (!noDogsNativeAvailable)
                viewModel.MPSettings_Dogs_Opacity = 0.3f;
            UpdatePresetButtonAvailability();

            Shared.DebugLogHelper.LogInfo(
                log,
                $"SKIRMISH_GAME_OPTIONS_TEST_OPEN: peace={shadow.peacetime}, advancedSkirmish={shadow.advanced_skirmish_options}, noDogsAvailable={noDogsNativeAvailable}.");
        }

        private void RouteCommandToWorkingCopy(FRONT_Multiplayer self, string command)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(self);
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (committed == null || working == null)
            {
                multiplayerButtonClickedOriginal(self, command);
                return;
            }

            AuthoritativeSetupDataField.SetValue(self, working);
            try
            {
                multiplayerButtonClickedOriginal(self, command);
            }
            finally
            {
                AuthoritativeSetupDataField.SetValue(self, committed);
            }
        }

        private void ApplySkirmishOptions(FRONT_Multiplayer self)
        {
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (working == null)
            {
                multiplayerButtonClickedOriginal(self, "ApplySettings");
                return;
            }

            working.allow_outposts = SkirmishGameOptionsPolicy.NormalizeOutpostsForApply(
                localSkirmish: true,
                MainViewModel.Instance.Show_SkirmishAllowOutposts,
                working.allow_outposts);

            bool advancedRequested = working.advanced_options != 0;
            int advancedSkirmishFlag = SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(
                advancedRequested,
                CaptureAdvancedState(working));
            working.advanced_skirmish_options = advancedSkirmishFlag;
            try
            {
                multiplayerButtonClickedOriginal(self, "ApplySettings");
            }
            catch
            {
                setupTransaction.Cancel();
                throw;
            }

            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(self);
            if (committed == null)
                throw new InvalidOperationException("Applied Skirmish MPsetupData is unavailable.");

            EngineInterface.MultiplayerSetupData previous = CloneSetupData(working);
            CanonicalizeSharedAdvancedState(previous, acceptEitherModeFlag: true);
            FRONT_Multiplayer.MPLastSetupData = previous;

            setupTransaction.ApplyTo(committed, CopySetupData);
            committed.advanced_skirmish_options = advancedSkirmishFlag;
            committed.advanced_options = 0;

            if (extraFeaturesSettings.EnableMod)
            {
                synchronizingPeaceTime = true;
                try
                {
                    extraFeaturesSettings.VanillaPeaceTimeMinutes = committed.peacetime;
                }
                finally
                {
                    synchronizingPeaceTime = false;
                }
            }

            UpdateHostInfoMethod.Invoke(self, new object[] { false });
            RefreshAllSkirmishViews(self);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"SKIRMISH_GAME_OPTIONS_TEST_APPLY: peace={committed.peacetime}, advancedSkirmish={committed.advanced_skirmish_options}, strongWalls={committed.no_knockdown_walls}, noCows={committed.no_cows}, noDogs={committed.no_dogs}, autoTrading={committed.allow_autotrading}.");
        }

        private void NormalizeLoadedSharedPreset(FRONT_Multiplayer self, string command)
        {
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (working == null)
                return;

            CanonicalizeSharedAdvancedState(working, acceptEitherModeFlag: true);
            ApplySkirmishDialogConstraints(working);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"SKIRMISH_GAME_OPTIONS_TEST_PRESET_LOADED: command={command}, peace={working.peacetime}, advanced={working.advanced_options}.");
        }

        private void SaveSharedPreset(FRONT_Multiplayer self, string command)
        {
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (working == null)
            {
                multiplayerButtonClickedOriginal(self, command);
                return;
            }

            CanonicalizeSharedAdvancedState(working, acceptEitherModeFlag: false);
            multiplayerButtonClickedOriginal(self, command);
            UpdatePresetButtonAvailability();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"SKIRMISH_GAME_OPTIONS_TEST_PRESET_SAVED: command={command}, sharedWithMultiplayer=true.");
        }

        private void ApplySkirmishDialogConstraints(
            EngineInterface.MultiplayerSetupData working)
        {
            MainViewModel viewModel = MainViewModel.Instance;
            viewModel.MPSettingHeight = "640";
            viewModel.MPSettings_ExTroops_Opacity =
                SkirmishGameOptionsPolicy.GetExtremeTroopsOpacity(localSkirmish: true);
            viewModel.MPSettings_AllowOutposts_Opacity =
                SkirmishGameOptionsPolicy.GetOutpostOpacity(
                    viewModel.Show_SkirmishAllowOutposts);
            viewModel.MPSettings_Dogs_Opacity = noDogsNativeAvailable ? 1f : 0.3f;
            viewModel.Show_MPSettings_AdvancedOptions = working.advanced_options != 0;
            viewModel.MPSettings_AdvancedButtonText = Translate.Instance.lookUpText(
                Enums.eTextSections.TEXT_SKIRMISH_MISC,
                working.advanced_options != 0 ? 20 : 19);
            UpdatePresetButtonAvailability();
        }

        private static void CanonicalizeSharedAdvancedState(
            EngineInterface.MultiplayerSetupData setup,
            bool acceptEitherModeFlag)
        {
            int flag = acceptEitherModeFlag
                ? SkirmishGameOptionsPolicy.ToSharedAdvancedFlag(
                    setup.advanced_options,
                    setup.advanced_skirmish_options,
                    CaptureAdvancedState(setup))
                : SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(
                    setup.advanced_options != 0,
                    CaptureAdvancedState(setup));
            setup.advanced_options = flag;
            setup.advanced_skirmish_options = flag;
        }

        private static void UpdatePresetButtonAvailability()
        {
            FRONT_Multiplayer_Setup setup = FRONT_Multiplayer_Setup.Instance;
            if (setup == null)
                return;

            setup.RefMP_UsePrevious.IsEnabled = FRONT_Multiplayer.MPLastSetupData != null;
            setup.RefMP_UsePresets1.IsEnabled =
                !string.IsNullOrEmpty(ConfigSettings.Settings_MPPresets1);
            setup.RefMP_UsePresets2.IsEnabled =
                !string.IsNullOrEmpty(ConfigSettings.Settings_MPPresets2);
        }

        private void OnExtraFeaturesSettingChanged(string propertyName)
        {
            if (synchronizingPeaceTime ||
                (propertyName != nameof(ExtraFeaturesViewModel.VanillaPeaceTimeMinutes) &&
                 propertyName != nameof(ExtraFeaturesViewModel.EnableMod)))
            {
                return;
            }

            try
            {
                FRONT_Multiplayer front = MainViewModel.Instance?.FRONTMultiplayer;
                if (front == null || !FRONT_Multiplayer.skirmishGame)
                    return;

                MainViewModel.Instance.Show_MPPeacetime = extraFeaturesSettings.EnableMod;
                if (!extraFeaturesSettings.EnableMod)
                    return;

                int minutes = extraFeaturesSettings.VanillaPeaceTimeMinutes;
                EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(front);
                EngineInterface.MultiplayerSetupData working = setupTransaction.Working;
                if (committed != null)
                    committed.peacetime = minutes;
                if (working != null)
                    working.peacetime = minutes;

                if (MainViewModel.Instance.Show_MPSettings &&
                    FRONT_Multiplayer_Setup.Instance?.RefMP_Settings_Peacetime_Slider != null)
                {
                    FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider.Value = minutes;
                    UpdatePeaceTimeText(minutes);
                }

                if (committed != null)
                    UpdateHostInfoMethod.Invoke(front, new object[] { false });

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"SKIRMISH_GAME_OPTIONS_TEST_PEACE_SYNC_FROM_EXTRAFEATURES: minutes={minutes}.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "SKIRMISH_GAME_OPTIONS_TEST_PEACE_SYNC_FAILED: " + exception);
            }
        }

        private void ButtonUnitLaunchCowHook(MainViewModel self, object parameter)
        {
            if (ShouldBlockCows())
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "SKIRMISH_GAME_OPTIONS_TEST_COW_ACTION_BLOCKED: No Cows is active.");
                return;
            }

            launchCowOriginal(self, parameter);
        }

        private void ShowAmmoOrdersHook(HUD_Troops self)
        {
            showAmmoOrdersOriginal(self);
            if (ShouldBlockCows() && self?.RefUnitFireCow != null)
                self.RefUnitFireCow.Visibility = Visibility.Hidden;
        }

        private void ButtonBuySellHook(MainViewModel self, object parameter)
        {
            if (ShouldBlockAutoTrading() &&
                int.TryParse(Convert.ToString(parameter), out int command) &&
                (command == 100 || command == 101))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"SKIRMISH_GAME_OPTIONS_TEST_AUTOTRADE_ACTION_BLOCKED: command={command}.");
                return;
            }

            buySellOriginal(self, parameter);
        }

        private void SetUpInbuildingHook(MainViewModel self, int overridePanel, int overrideType)
        {
            setUpInbuildingOriginal(self, overridePanel, overrideType);
            if (!ShouldBlockAutoTrading() || self?.HUDBuildingPanel == null)
                return;

            self.HUDBuildingPanel.RefTrade_GoTo_Auto.Visibility = Visibility.Hidden;
            self.HUDBuildingPanel.RefTradePost_Trade_Auto.Visibility = Visibility.Hidden;
            self.HUDBuildingPanel.RefTradePost_Trade_Normal.Visibility = Visibility.Visible;
        }

        private static bool ShouldBlockCows()
        {
            bool localSkirmish = Director.instance != null && Director.instance.SkirmishModeGame;
            bool noCows = GameData.Instance?.lastGameState?.MP_No_Cows == true;
            return SkirmishGameOptionsPolicy.ShouldBlockCow(localSkirmish, noCows);
        }

        private static bool ShouldBlockAutoTrading()
        {
            bool localSkirmish = Director.instance != null && Director.instance.SkirmishModeGame;
            bool allowAutoTrading = GameData.Instance?.lastGameState?.MP_AllowAutoTrading == true;
            return SkirmishGameOptionsPolicy.ShouldBlockAutoTrading(
                localSkirmish,
                allowAutoTrading);
        }

        private static EngineInterface.MultiplayerSetupData GetAuthoritativeSetupData(
            FRONT_Multiplayer front) =>
            AuthoritativeSetupDataField.GetValue(front) as EngineInterface.MultiplayerSetupData;

        private static EngineInterface.MultiplayerSetupData GetTemporarySetupData(
            FRONT_Multiplayer front) =>
            TemporarySetupDataField.GetValue(front) as EngineInterface.MultiplayerSetupData;

        private EngineInterface.MultiplayerSetupData GetActiveWorkingCopy(
            FRONT_Multiplayer front)
        {
            EngineInterface.MultiplayerSetupData working = setupTransaction.Working;
            if (working != null)
                return working;

            working = GetTemporarySetupData(front);
            if (working != null)
                setupTransaction.Attach(working);
            return working;
        }

        private static void RefreshAllSkirmishViews(FRONT_Multiplayer front)
        {
            SetupSkirmishModeSettingsMethod.Invoke(front, null);

            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(front);
            MainViewModel viewModel = MainViewModel.Instance;
            if (committed == null || viewModel == null)
                return;

            bool advancedEnabled = committed.advanced_skirmish_options != 0;
            bool effectiveAdvanced =
                SkirmishGameOptionsPolicy.ShouldShowAdvancedIndicator(
                    committed.advanced_skirmish_options,
                    CaptureAdvancedState(committed));
            viewModel.Show_SkirmishAdvancedEnabled = effectiveAdvanced;
            viewModel.MPSettings_AdvSkirmish_Opacity = advancedEnabled ? 1f : 0.5f;
            CopyAvailabilityToViewModel(
                committed.MP_BuildingsAvailable,
                viewModel.MPSetupBuildingsBool);
            CopyAvailabilityToViewModel(
                committed.MP_GoodsAvailable,
                viewModel.TradingGoodsBool);
            CopyAvailabilityToViewModel(
                committed.MP_TroopsAvailable,
                viewModel.MPSetupTroopsBool);

            if (viewModel.Show_MP_SkirmishAdvanced &&
                front.RefEnableAdvancedSkirmishCheck != null &&
                front.RefEnableAdvancedSkirmishCheck.IsChecked != advancedEnabled)
            {
                front.RefEnableAdvancedSkirmishCheck.IsChecked = advancedEnabled;
            }
        }

        private static void NormalizeCommittedAdvancedState(FRONT_Multiplayer front)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(front);
            if (committed == null)
                return;

            committed.advanced_skirmish_options =
                SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(
                    committed.advanced_skirmish_options != 0,
                    CaptureAdvancedState(committed));
            committed.advanced_options = 0;
        }

        private static void CopyAvailabilityToViewModel(
            int[] source,
            System.Collections.ObjectModel.ObservableCollection<bool> target)
        {
            if (source == null || target == null)
                return;

            int count = Math.Min(source.Length, target.Count);
            for (int index = 0; index < count; index++)
                target[index] = source[index] != 0;
        }

        private static SkirmishGameOptionsPolicy.AdvancedState CaptureAdvancedState(
            EngineInterface.MultiplayerSetupData setup) =>
            new SkirmishGameOptionsPolicy.AdvancedState
            {
                Buildings = setup.MP_BuildingsAvailable,
                Goods = setup.MP_GoodsAvailable,
                Troops = setup.MP_TroopsAvailable,
                PreBuild = setup.advopt_pre_build,
                ImprovedArabSwordsmen = setup.advopt_improved_arabswordsmen,
                ImprovedLaddermen = setup.advopt_improved_laddermen,
                ImprovedSpearmen = setup.advopt_improved_spearmen,
                RebalancedHorseArchers = setup.advopt_rebalanced_horsearchers,
                ImprovedFletchers = setup.advopt_improved_fletchers,
                UncappedPeasants = setup.advopt_uncapped_peasants,
                FasterPeasants = setup.advopt_faster_peasants,
                EnemyHitPoints = setup.advopt_enemy_hps,
                ImprovedSieging = setup.global_improved_sieging,
                ImprovedSieging2 = setup.global_improved_sieging2,
                Healers = setup.advopt_healers,
                Eunuchs = setup.advopt_eunuchs,
                NoGold = setup.advopt_nogold
            };

        private static EngineInterface.MultiplayerSetupData CloneSetupData(
            EngineInterface.MultiplayerSetupData source)
        {
            var clone = new EngineInterface.MultiplayerSetupData();
            clone.FromString(source.ToString());
            return clone;
        }

        private static void CopySetupData(
            EngineInterface.MultiplayerSetupData source,
            EngineInterface.MultiplayerSetupData target)
        {
            target.FromString(source.ToString(), ignoreKeepOrder: true);
        }

        private static void UpdatePeaceTimeText(int minutes)
        {
            if (Translate.Instance == null || MainViewModel.Instance == null)
                return;

            string suffix = Translate.Instance.lookUpText(
                Enums.eTextSections.TEXT_NEW_TEXT2,
                168);
            MainViewModel.Instance.MP_Settings_Peacetime =
                FatControler.ukrainian ? minutes + " " + suffix : minutes + suffix;
        }

        private static FieldInfo RequireField(Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
            throw new MissingFieldException(type.FullName, name);

        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                parameters,
                null) ??
            throw new MissingMethodException(type.FullName, name);
    }
}
