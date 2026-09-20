using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.NoesisUtil;
using System;
using System.ComponentModel;

namespace BugfixesAndQoL
{
    internal sealed class SkirmishGameOptionsAccessViewModel :
        INotifyPropertyChanged,
        INoesisElementBindingAware
    {
        private const string SettingsButtonName =
            "BugfixesAndQoLSkirmishGameOptionsButton";

        private readonly BugfixesAndQoLViewModel settings;
        private readonly ManualLogSource log;
        private bool runtimeAvailable;

        internal SkirmishGameOptionsAccessViewModel(
            BugfixesAndQoLViewModel settings,
            bool compatible,
            ManualLogSource log)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            runtimeAvailable = compatible;
            settings.SettingChanged += OnSettingChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Visibility ButtonVisibility =>
            runtimeAvailable && settings.EnableMod && settings.ShowMpAdvancedSettingsInSingleplayer
                ? Visibility.Visible
                : Visibility.Collapsed;

        internal void SetRuntimeAvailable(bool available)
        {
            if (runtimeAvailable == available)
                return;
            runtimeAvailable = available;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(ButtonVisibility)));
        }

        void INoesisElementBindingAware.OnNoesisElementBound(FrameworkElement element)
        {
            Button button = element?.FindName(SettingsButtonName) as Button;
            MainViewModel vanillaViewModel = MainViewModel.Instance;
            if (button == null || vanillaViewModel == null ||
                vanillaViewModel.MultiplayerMenuCommand == null)
            {
                SetRuntimeAvailable(false);
                Shared.DebugLogHelper.LogError(
                    log,
                    "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_BUTTON_BIND_FAILED: " +
                    $"hostAvailable={element != null}, buttonAvailable={button != null}, " +
                    $"mainViewModelAvailable={vanillaViewModel != null}, " +
                    $"commandAvailable={vanillaViewModel?.MultiplayerMenuCommand != null}, " +
                    $"textAvailable={!string.IsNullOrEmpty(vanillaViewModel?.MP_Settings_Button)}.");
                return;
            }

            button.DataContext = vanillaViewModel;
            Shared.DebugLogHelper.LogDebug(
                log,
                "BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_BUTTON_BOUND: " +
                $"textReady={!string.IsNullOrEmpty(vanillaViewModel.MP_Settings_Button)}, " +
                "commandAvailable=true, " +
                "dataContext=MainViewModel.Instance.");
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(BugfixesAndQoLViewModel.EnableMod) &&
                propertyName != nameof(BugfixesAndQoLViewModel.ShowMpAdvancedSettingsInSingleplayer))
            {
                return;
            }

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(ButtonVisibility)));
        }
    }
}
