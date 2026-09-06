using Noesis;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace MoveMoatTest
{
    internal enum RouteCalculationMode
    {
        Exact = 0,
        RequiredOnly = 1
    }

    public sealed class MoveMoatSettings : Shared.PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private int routeMode = (int)RouteCalculationMode.RequiredOnly;
        [SyncHostOnly]
        public bool EnableMod { get => enableMod; set => SetSetting(ref enableMod, value); }
        [SyncHostOnly]
        public int RouteMode { get => routeMode; set => SetSetting(
            ref routeMode,
            value == (int)RouteCalculationMode.RequiredOnly
                ? (int)RouteCalculationMode.RequiredOnly
                : (int)RouteCalculationMode.Exact); }
        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);
        public string RouteModeText => SerpLocalization.Get("MoveMoat.RouteMode");
        public string RouteModeHelp => SerpLocalization.Get("MoveMoat.RouteModeHelp");
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public ComboBoxItem[] RouteModeOptions { get; } = {
            new ComboBoxItem { Content = SerpLocalization.Get("MoveMoat.Individual") },
            new ComboBoxItem { Content = SerpLocalization.Get("MoveMoat.RequiredOnly") }
        };
        public RelayCommand ResetToDefaultCommand { get; }
        public MoveMoatSettings() { ResetToDefaultCommand = new RelayCommand(ResetToDefault); }
        private void SetSetting<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            // The shared notification layer merges presets after Extender persistence.
            if (!CanMutateSetting(name) || EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            OnPropertyChanged(name);
        }
        private void ResetToDefault()
        {
            if (!CanEditHostSettings || !CanResetSettings) return;
            EnableMod = true;
            RouteMode = (int)RouteCalculationMode.RequiredOnly;
        }
    }
}
