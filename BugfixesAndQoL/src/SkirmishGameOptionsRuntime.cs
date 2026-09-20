using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class SkirmishGameOptionsRuntime
    {
        private delegate void MultiplayerButtonClickedDelegate(FRONT_Multiplayer self, string command);
        private delegate void MainViewModelObjectCommandDelegate(MainViewModel self, object parameter);
        private delegate void SetUpInbuildingDelegate(MainViewModel self, int overridePanel, int overrideType);
        private delegate void ShowAmmoOrdersDelegate(HUD_Troops self);

        private static readonly FieldInfo AuthoritativeSetupDataField = RequireField(
            typeof(FRONT_Multiplayer), "MPsetupData");
        private static readonly FieldInfo TemporarySetupDataField = RequireField(
            typeof(FRONT_Multiplayer), "MPTEMPsetupData");
        private static readonly MethodInfo UpdateHostInfoMethod = RequireMethod(
            typeof(FRONT_Multiplayer), "UpdateHostInfo", typeof(bool));
        private static readonly MethodInfo SetupSkirmishModeSettingsMethod = RequireMethod(
            typeof(FRONT_Multiplayer), "SetupSkirmishModeSettings");

        private readonly ManualLogSource log;
        private readonly WorkingCopyTransaction<EngineInterface.MultiplayerSetupData> transaction;

        // The plugin roots this runtime for the complete process lifetime. Dispose is used only
        // to roll back hook candidates that were not published after failed initialization.
        private Hook multiplayerButtonHook;
        private Hook launchCowHook;
        private Hook buySellHook;
        private Hook setUpInbuildingHook;
        private Hook showAmmoOrdersHook;
        private MultiplayerButtonClickedDelegate multiplayerButtonOriginal;
        private MainViewModelObjectCommandDelegate launchCowOriginal;
        private MainViewModelObjectCommandDelegate buySellOriginal;
        private SetUpInbuildingDelegate setUpInbuildingOriginal;
        private ShowAmmoOrdersDelegate showAmmoOrdersOriginal;
        private bool noDogsNativeAvailable;
        private bool peaceTimeNativeAvailable;
        private bool noDogsUnavailableLogged;
        private bool outpostsUnavailableLogged;
        private bool initialized;

        internal SkirmishGameOptionsRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            transaction =
                new WorkingCopyTransaction<EngineInterface.MultiplayerSetupData>(CloneSetupData);
        }

        internal void Initialize()
        {
            if (initialized)
                return;

            Hook pendingMultiplayer = null;
            Hook pendingCow = null;
            Hook pendingTrade = null;
            Hook pendingBuilding = null;
            Hook pendingAmmo = null;
            try
            {
                pendingMultiplayer = new Hook(
                    RequireMethod(typeof(FRONT_Multiplayer), "ButtonClicked", typeof(string)),
                    (MultiplayerButtonClickedDelegate)MultiplayerButtonClickedHook);
                MultiplayerButtonClickedDelegate multiplayerOriginal =
                    pendingMultiplayer.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

                pendingCow = new Hook(
                    RequireMethod(typeof(MainViewModel), "ButtonUnitLaunchCow", typeof(object)),
                    (MainViewModelObjectCommandDelegate)ButtonUnitLaunchCowHook);
                MainViewModelObjectCommandDelegate cowOriginal =
                    pendingCow.GenerateTrampoline<MainViewModelObjectCommandDelegate>();

                pendingTrade = new Hook(
                    RequireMethod(typeof(MainViewModel), "ButtonBuySell", typeof(object)),
                    (MainViewModelObjectCommandDelegate)ButtonBuySellHook);
                MainViewModelObjectCommandDelegate tradeOriginal =
                    pendingTrade.GenerateTrampoline<MainViewModelObjectCommandDelegate>();

                pendingBuilding = new Hook(
                    RequireMethod(typeof(MainViewModel), "setUpInbuilding", typeof(int), typeof(int)),
                    (SetUpInbuildingDelegate)SetUpInbuildingHook);
                SetUpInbuildingDelegate buildingOriginal =
                    pendingBuilding.GenerateTrampoline<SetUpInbuildingDelegate>();

                pendingAmmo = new Hook(
                    RequireMethod(typeof(HUD_Troops), "ShowAmmoOrders"),
                    (ShowAmmoOrdersDelegate)ShowAmmoOrdersHook);
                ShowAmmoOrdersDelegate ammoOriginal =
                    pendingAmmo.GenerateTrampoline<ShowAmmoOrdersDelegate>();

                multiplayerButtonHook = pendingMultiplayer;
                multiplayerButtonOriginal = multiplayerOriginal;
                launchCowHook = pendingCow;
                launchCowOriginal = cowOriginal;
                buySellHook = pendingTrade;
                buySellOriginal = tradeOriginal;
                setUpInbuildingHook = pendingBuilding;
                setUpInbuildingOriginal = buildingOriginal;
                showAmmoOrdersHook = pendingAmmo;
                showAmmoOrdersOriginal = ammoOriginal;
                initialized = true;
            }
            catch
            {
                pendingAmmo?.Dispose();
                pendingBuilding?.Dispose();
                pendingTrade?.Dispose();
                pendingCow?.Dispose();
                pendingMultiplayer?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_MANAGED_READY: lobby, cow and auto-trading hooks installed.");
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
                "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_NO_DOGS_DISABLED: native validation or patch installation failed; all other options remain available.");
        }

        internal void SetPeaceTimeNativeAvailable(bool available)
        {
            peaceTimeNativeAvailable = available;
            if (!available)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_PEACE_TIME_DISABLED: native validation or patch installation failed; all other options remain available.");
            }
        }

        private void MultiplayerButtonClickedHook(FRONT_Multiplayer self, string command)
        {
            if (self == null || !FRONT_Multiplayer.skirmishGame)
            {
                multiplayerButtonOriginal(self, command);
                return;
            }

            if (string.Equals(command, "Setup", StringComparison.Ordinal))
            {
                Open(self);
                return;
            }

            if (string.Equals(command, "Settings_Dogs", StringComparison.Ordinal) &&
                !SkirmishGameOptionsPolicy.ShouldAllowNoDogsToggle(noDogsNativeAvailable))
            {
                MainViewModel.Instance.MPSettings_Dogs_Opacity = 0.3f;
                if (!noDogsUnavailableLogged)
                {
                    noDogsUnavailableLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_NO_DOGS_CLICK_REJECTED: the native mode-99 gate is unavailable.");
                }
                return;
            }

            if (string.Equals(command, "Settings_AllowOutposts", StringComparison.Ordinal) &&
                !SkirmishGameOptionsPolicy.ShouldAllowOutpostToggle(
                    true,
                    MainViewModel.Instance.Show_SkirmishAllowOutposts))
            {
                MainViewModel.Instance.MPSettings_AllowOutposts_Opacity = 0.3f;
                if (!outpostsUnavailableLogged)
                {
                    outpostsUnavailableLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_OUTPOSTS_CLICK_REJECTED: the selected map does not support outposts.");
                }
                return;
            }

            if (string.Equals(command, "ApplySettings", StringComparison.Ordinal))
            {
                Apply(self);
                return;
            }

            if (string.Equals(command, "CancelSettings", StringComparison.Ordinal))
            {
                try
                {
                    multiplayerButtonOriginal(self, command);
                }
                finally
                {
                    transaction.Cancel();
                }
                RefreshAllSkirmishViews(self);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_CANCEL: working settings discarded.");
                return;
            }

            if (string.Equals(command, "CloseSkirmishAdvanced", StringComparison.Ordinal))
            {
                multiplayerButtonOriginal(self, command);
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
                RouteToWorkingCopy(self, command);
                if (SkirmishGameOptionsPolicy.IsPresetLoadCommand(command))
                    NormalizeLoadedPreset(self, command);
                return;
            }

            multiplayerButtonOriginal(self, command);
        }

        private void Open(FRONT_Multiplayer self)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritative(self);
            if (committed == null)
                throw new InvalidOperationException("Skirmish MPsetupData is unavailable.");

            EngineInterface.MultiplayerSetupData shadow = transaction.Begin(committed);
            shadow.advanced_options = shadow.advanced_skirmish_options;
            AuthoritativeSetupDataField.SetValue(self, shadow);
            try
            {
                multiplayerButtonOriginal(self, "Setup");
            }
            catch
            {
                transaction.Cancel();
                throw;
            }
            finally
            {
                AuthoritativeSetupDataField.SetValue(self, committed);
            }

            EngineInterface.MultiplayerSetupData working = GetTemporary(self);
            if (working == null)
            {
                transaction.Cancel();
                throw new InvalidOperationException("Skirmish MPTEMPsetupData is unavailable.");
            }
            transaction.Attach(working);

            MainViewModel viewModel = MainViewModel.Instance;
            viewModel.Show_MPOnlySettings = true;
            viewModel.Show_MPSettings_MaxPlayers = false;
            viewModel.Show_MPPeacetime = peaceTimeNativeAvailable;
            ApplyDialogConstraints(working);
            outpostsUnavailableLogged = false;

            Shared.DebugLogHelper.LogDebug(
                log,
                $"BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_OPEN: peace={working.peacetime}, advancedSkirmish={working.advanced_skirmish_options}, noDogsAvailable={noDogsNativeAvailable}.");
        }

        private void RouteToWorkingCopy(FRONT_Multiplayer self, string command)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritative(self);
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (committed == null || working == null)
            {
                multiplayerButtonOriginal(self, command);
                return;
            }

            AuthoritativeSetupDataField.SetValue(self, working);
            try
            {
                multiplayerButtonOriginal(self, command);
            }
            finally
            {
                AuthoritativeSetupDataField.SetValue(self, committed);
            }
        }

        private void Apply(FRONT_Multiplayer self)
        {
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (working == null)
            {
                multiplayerButtonOriginal(self, "ApplySettings");
                return;
            }

            EngineInterface.MultiplayerSetupData committedBeforeApply = GetAuthoritative(self);
            if (!peaceTimeNativeAvailable && committedBeforeApply != null)
                working.peacetime = committedBeforeApply.peacetime;

            working.allow_outposts = SkirmishGameOptionsPolicy.NormalizeOutpostsForApply(
                true,
                MainViewModel.Instance.Show_SkirmishAllowOutposts,
                working.allow_outposts);
            int advancedFlag = SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(
                working.advanced_options != 0,
                CaptureAdvancedState(working));
            working.advanced_skirmish_options = advancedFlag;

            try
            {
                multiplayerButtonOriginal(self, "ApplySettings");
            }
            catch
            {
                transaction.Cancel();
                throw;
            }

            EngineInterface.MultiplayerSetupData committed = GetAuthoritative(self);
            if (committed == null)
                throw new InvalidOperationException("Applied Skirmish MPsetupData is unavailable.");

            EngineInterface.MultiplayerSetupData previous = CloneSetupData(working);
            CanonicalizeSharedAdvancedState(previous, true);
            FRONT_Multiplayer.MPLastSetupData = previous;

            transaction.ApplyTo(committed, CopySetupData);
            committed.advanced_skirmish_options = advancedFlag;
            committed.advanced_options = 0;

            UpdateHostInfoMethod.Invoke(self, new object[] { false });
            RefreshAllSkirmishViews(self);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_APPLY: peace={committed.peacetime}, advancedSkirmish={committed.advanced_skirmish_options}, strongWalls={committed.no_knockdown_walls}, noCows={committed.no_cows}, noDogs={committed.no_dogs}, autoTrading={committed.allow_autotrading}.");
        }

        private void NormalizeLoadedPreset(FRONT_Multiplayer self, string command)
        {
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (working == null)
                return;
            CanonicalizeSharedAdvancedState(working, true);
            ApplyDialogConstraints(working);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_PRESET_LOADED: command={command}, peace={working.peacetime}, advanced={working.advanced_options}.");
        }

        private void SaveSharedPreset(FRONT_Multiplayer self, string command)
        {
            EngineInterface.MultiplayerSetupData working = GetActiveWorkingCopy(self);
            if (working == null)
            {
                multiplayerButtonOriginal(self, command);
                return;
            }

            CanonicalizeSharedAdvancedState(working, false);
            multiplayerButtonOriginal(self, command);
            UpdatePresetButtonAvailability();
            Shared.DebugLogHelper.LogDebug(
                log,
                $"BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_PRESET_SAVED: command={command}, sharedWithMultiplayer=true.");
        }

        private void ApplyDialogConstraints(EngineInterface.MultiplayerSetupData working)
        {
            MainViewModel viewModel = MainViewModel.Instance;
            viewModel.MPSettingHeight = "640";
            viewModel.MPSettings_ExTroops_Opacity = 1f;
            viewModel.MPSettings_AllowOutposts_Opacity =
                viewModel.Show_SkirmishAllowOutposts ? 1f : 0.3f;
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

        private void ButtonUnitLaunchCowHook(MainViewModel self, object parameter)
        {
            if (ShouldBlockCows())
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_COW_ACTION_BLOCKED: No Cows is active.");
                return;
            }
            launchCowOriginal(self, parameter);
        }

        private void ShowAmmoOrdersHook(HUD_Troops self)
        {
            showAmmoOrdersOriginal(self);
            if (!ShouldBlockCows() || self == null)
                return;

            Grid fireCow = self.FindName("UnitFireCow") as Grid;
            if (fireCow != null)
                fireCow.Visibility = Visibility.Hidden;
        }

        private void ButtonBuySellHook(MainViewModel self, object parameter)
        {
            if (ShouldBlockAutoTrading() &&
                int.TryParse(Convert.ToString(parameter), out int command) &&
                (command == 100 || command == 101))
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_AUTOTRADE_ACTION_BLOCKED: command={command}.");
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

        private static bool ShouldBlockCows() =>
            SkirmishGameOptionsPolicy.ShouldBlockCow(
                Director.instance != null && Director.instance.SkirmishModeGame,
                GameData.Instance?.lastGameState?.MP_No_Cows == true);

        private static bool ShouldBlockAutoTrading() =>
            SkirmishGameOptionsPolicy.ShouldBlockAutoTrading(
                Director.instance != null && Director.instance.SkirmishModeGame,
                GameData.Instance?.lastGameState?.MP_AllowAutoTrading == true);

        private static EngineInterface.MultiplayerSetupData GetAuthoritative(
            FRONT_Multiplayer front) =>
            AuthoritativeSetupDataField.GetValue(front) as EngineInterface.MultiplayerSetupData;

        private static EngineInterface.MultiplayerSetupData GetTemporary(
            FRONT_Multiplayer front) =>
            TemporarySetupDataField.GetValue(front) as EngineInterface.MultiplayerSetupData;

        private EngineInterface.MultiplayerSetupData GetActiveWorkingCopy(FRONT_Multiplayer front)
        {
            EngineInterface.MultiplayerSetupData working = transaction.Working;
            if (working != null)
                return working;
            working = GetTemporary(front);
            if (working != null)
                transaction.Attach(working);
            return working;
        }

        private static void RefreshAllSkirmishViews(FRONT_Multiplayer front)
        {
            SetupSkirmishModeSettingsMethod.Invoke(front, null);
            EngineInterface.MultiplayerSetupData committed = GetAuthoritative(front);
            MainViewModel viewModel = MainViewModel.Instance;
            if (committed == null || viewModel == null)
                return;

            bool advancedEnabled = committed.advanced_skirmish_options != 0;
            viewModel.Show_SkirmishAdvancedEnabled =
                SkirmishGameOptionsPolicy.ShouldShowAdvancedIndicator(
                    committed.advanced_skirmish_options,
                    CaptureAdvancedState(committed));
            viewModel.MPSettings_AdvSkirmish_Opacity = advancedEnabled ? 1f : 0.5f;
            CopyAvailability(committed.MP_BuildingsAvailable, viewModel.MPSetupBuildingsBool);
            CopyAvailability(committed.MP_GoodsAvailable, viewModel.TradingGoodsBool);
            CopyAvailability(committed.MP_TroopsAvailable, viewModel.MPSetupTroopsBool);

            if (viewModel.Show_MP_SkirmishAdvanced &&
                front.RefEnableAdvancedSkirmishCheck != null &&
                front.RefEnableAdvancedSkirmishCheck.IsChecked != advancedEnabled)
            {
                front.RefEnableAdvancedSkirmishCheck.IsChecked = advancedEnabled;
            }
        }

        private static void NormalizeCommittedAdvancedState(FRONT_Multiplayer front)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritative(front);
            if (committed == null)
                return;
            committed.advanced_skirmish_options =
                SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(
                    committed.advanced_skirmish_options != 0,
                    CaptureAdvancedState(committed));
            committed.advanced_options = 0;
        }

        private static void CopyAvailability(
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
            EngineInterface.MultiplayerSetupData target) =>
            target.FromString(source.ToString(), ignoreKeepOrder: true);

        private static FieldInfo RequireField(Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
            throw new MissingFieldException(type.FullName, name);

        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                parameters,
                null) ?? throw new MissingMethodException(type.FullName, name);
    }
}
