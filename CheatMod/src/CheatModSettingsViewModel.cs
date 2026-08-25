using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;
using System.Runtime.CompilerServices;

namespace CheatMod
{
    public sealed class CheatModSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private bool enableMod;
        private bool endlessExtremePowers = true;

        public CheatModSettingsViewModel()
        {
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public event Action<string> SettingChanged;

        public RelayCommand ResetToDefaultCommand { get; }

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");

        public string ExtremePowersTitleText => SerpLocalization.Get("CheatMod.ExtremePowersTitle");

        public string EndlessExtremePowersText => SerpLocalization.Get("CheatMod.EndlessExtremePowers");

        public string EndlessExtremePowersHelpText => SerpLocalization.Get("CheatMod.EndlessExtremePowersHelp");

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set => Set(ref enableMod, value);
        }

        [SyncHostOnly]
        public bool EndlessExtremePowers
        {
            get => endlessExtremePowers;
            set => Set(ref endlessExtremePowers, value);
        }

        private void ResetToDefault()
        {
            if (!CanEditHostSettings)
                return;

            EnableMod = false;
            EndlessExtremePowers = true;
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!CanMutateSetting(propertyName) || Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }
    }
}
