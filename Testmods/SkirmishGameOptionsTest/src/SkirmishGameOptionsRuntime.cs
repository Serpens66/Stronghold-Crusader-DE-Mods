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
        private bool initialized;

        internal SkirmishGameOptionsRuntime(
            ManualLogSource log,
            ExtraFeaturesViewModel extraFeaturesSettings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.extraFeaturesSettings = extraFeaturesSettings ??
                throw new ArgumentNullException(nameof(extraFeaturesSettings));
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
                !noDogsNativeAvailable)
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

            if (string.Equals(command, "ApplySettings", StringComparison.Ordinal))
            {
                ApplySkirmishOptions(self);
                return;
            }

            if (string.Equals(command, "CancelSettings", StringComparison.Ordinal))
            {
                multiplayerButtonClickedOriginal(self, command);
                RefreshCommittedSkirmishView(self);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "SKIRMISH_GAME_OPTIONS_TEST_CANCEL: working settings discarded.");
                return;
            }

            if (MainViewModel.Instance.Show_MPSettings &&
                SkirmishGameOptionsPolicy.IsWorkingCopyCommand(command))
            {
                RouteCommandToWorkingCopy(self, command);
                return;
            }

            multiplayerButtonClickedOriginal(self, command);
        }

        private void OpenSkirmishOptions(FRONT_Multiplayer self)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(self);
            if (committed == null)
                throw new InvalidOperationException("Skirmish MPsetupData is unavailable.");

            var shadow = new EngineInterface.MultiplayerSetupData();
            shadow.FromString(committed.ToString(), ignoreKeepOrder: true);
            shadow.advanced_options = shadow.advanced_skirmish_options;
            if (extraFeaturesSettings.EnableMod)
                shadow.peacetime = extraFeaturesSettings.VanillaPeaceTimeMinutes;

            AuthoritativeSetupDataField.SetValue(self, shadow);
            try
            {
                multiplayerButtonClickedOriginal(self, "Setup");
            }
            finally
            {
                AuthoritativeSetupDataField.SetValue(self, committed);
            }

            MainViewModel viewModel = MainViewModel.Instance;
            viewModel.Show_MPOnlySettings = true;
            viewModel.Show_MPSettings_MaxPlayers = false;
            viewModel.Show_MPPeacetime = extraFeaturesSettings.EnableMod;
            viewModel.MPSettingHeight = "560";
            if (!noDogsNativeAvailable)
                viewModel.MPSettings_Dogs_Opacity = 0.3f;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"SKIRMISH_GAME_OPTIONS_TEST_OPEN: peace={shadow.peacetime}, advancedSkirmish={shadow.advanced_skirmish_options}, noDogsAvailable={noDogsNativeAvailable}.");
        }

        private void RouteCommandToWorkingCopy(FRONT_Multiplayer self, string command)
        {
            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(self);
            EngineInterface.MultiplayerSetupData working = GetTemporarySetupData(self);
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
            EngineInterface.MultiplayerSetupData working = GetTemporarySetupData(self);
            if (working == null)
            {
                multiplayerButtonClickedOriginal(self, "ApplySettings");
                return;
            }

            working.advanced_skirmish_options =
                SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(working.advanced_options);
            multiplayerButtonClickedOriginal(self, "ApplySettings");

            EngineInterface.MultiplayerSetupData committed = GetAuthoritativeSetupData(self);
            if (committed == null)
                throw new InvalidOperationException("Applied Skirmish MPsetupData is unavailable.");

            committed.advanced_skirmish_options =
                SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(committed.advanced_options);
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
            RefreshCommittedSkirmishView(self);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"SKIRMISH_GAME_OPTIONS_TEST_APPLY: peace={committed.peacetime}, advancedSkirmish={committed.advanced_skirmish_options}, strongWalls={committed.no_knockdown_walls}, noCows={committed.no_cows}, noDogs={committed.no_dogs}, autoTrading={committed.allow_autotrading}.");
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
                EngineInterface.MultiplayerSetupData working = GetTemporarySetupData(front);
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

        private static void RefreshCommittedSkirmishView(FRONT_Multiplayer front)
        {
            SetupSkirmishModeSettingsMethod.Invoke(front, null);
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
