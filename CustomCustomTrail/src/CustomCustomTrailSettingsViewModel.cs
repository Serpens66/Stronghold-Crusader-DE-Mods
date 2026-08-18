using SHCDESE.API.Components.ModManager;
using SHCDESE.ViewModels;
using System;

namespace CustomCustomTrail
{
    public sealed class CustomCustomTrailSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        private bool enableMod = true;

        public event Action<bool> EnableModChanged;

        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);

        public string EnableModHelpText => SerpLocalization.Get(SerpLocalization.EnableModHelp);

        public string PracticalEffectsText => SerpLocalization.Get("CustomCustomTrail.PracticalEffects");

        // Disabling the coordinator is a machine-local safety choice and must not be
        // overridden by a lobby host or stored inside a gameplay preset.
        [PersistLocal]
        public bool EnableMod
        {
            get => enableMod;
            set
            {
                if (enableMod == value)
                    return;

                enableMod = value;
                OnPropertyChanged(nameof(EnableMod));
                EnableModChanged?.Invoke(value);
            }
        }
    }
}
