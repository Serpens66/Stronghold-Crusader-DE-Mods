using Noesis;
using APIShared;
using System.ComponentModel;

namespace ExtendedData
{
    internal sealed class EditorModSettingsSaveOptionsViewModel : INotifyPropertyChanged
    {
        private bool enabled;
        private bool includeMapModSettings;
        private bool includeTrailModSettings = true;
        private Visibility mapOptionVisibility = Visibility.Collapsed;
        private Visibility loadOptionVisibility = Visibility.Collapsed;
        private bool useCurrentSavegameSettings;
        private bool canUseCurrentSavegameSettings;
        private string selectedSavegamePath;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IncludeMapModSettings
        {
            get => includeMapModSettings;
            set
            {
                if (includeMapModSettings == value)
                    return;
                includeMapModSettings = value;
                Changed(nameof(IncludeMapModSettings));
            }
        }

        public bool IncludeTrailModSettings
        {
            get => includeTrailModSettings;
            set
            {
                if (includeTrailModSettings == value)
                    return;
                includeTrailModSettings = value;
                Changed(nameof(IncludeTrailModSettings));
            }
        }

        public Visibility MapOptionVisibility => enabled ? mapOptionVisibility : Visibility.Collapsed;
        public Visibility TrailOptionVisibility => enabled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LoadOptionVisibility => loadOptionVisibility;
        public bool CanUseCurrentSavegameSettings => canUseCurrentSavegameSettings;
        public bool UseCurrentSavegameSettings
        {
            get => useCurrentSavegameSettings;
            set
            {
                bool next = canUseCurrentSavegameSettings && value;
                if (useCurrentSavegameSettings == next) return;
                useCurrentSavegameSettings = next;
                Changed(nameof(UseCurrentSavegameSettings));
            }
        }
        public string UseCurrentSavegameSettingsText => SerpLocalization.Get("Savegame.UseCurrentModSettings");
        public string UseCurrentSavegameSettingsHelp => SerpLocalization.Get("Savegame.UseCurrentModSettingsHelp");
        public string IncludeText => SerpLocalization.Get("EditorSave.IncludeModSettings");
        public string MapHelpText => SerpLocalization.Get("EditorSave.IncludeMapModSettingsHelp");
        public string TrailHelpText => SerpLocalization.Get("EditorSave.IncludeTrailModSettingsHelp");

        internal void SetEnabled(bool value)
        {
            if (enabled == value)
                return;
            enabled = value;
            Changed(nameof(MapOptionVisibility));
            Changed(nameof(TrailOptionVisibility));
        }

        internal void OpenMap(bool hasExistingEntry)
        {
            IncludeMapModSettings = hasExistingEntry;
            mapOptionVisibility = Visibility.Visible;
            Changed(nameof(MapOptionVisibility));
        }

        internal void CloseMap()
        {
            if (mapOptionVisibility == Visibility.Collapsed)
                return;
            mapOptionVisibility = Visibility.Collapsed;
            Changed(nameof(MapOptionVisibility));
        }

        internal void OpenLoad()
        {
            selectedSavegamePath = null;
            UseCurrentSavegameSettings = false;
            canUseCurrentSavegameSettings = false;
            loadOptionVisibility = Visibility.Visible;
            Changed(nameof(CanUseCurrentSavegameSettings));
            Changed(nameof(LoadOptionVisibility));
        }

        internal void SelectSavegame(string path)
        {
            if (!string.Equals(selectedSavegamePath, path, System.StringComparison.OrdinalIgnoreCase))
            {
                selectedSavegamePath = path;
                UseCurrentSavegameSettings = false;
            }
            bool allowed = SavegameModSettings.CanUseCurrentSettings(path);
            if (canUseCurrentSavegameSettings == allowed) return;
            canUseCurrentSavegameSettings = allowed;
            if (!allowed) UseCurrentSavegameSettings = false;
            Changed(nameof(CanUseCurrentSavegameSettings));
        }

        internal void CloseLoad()
        {
            selectedSavegamePath = null;
            loadOptionVisibility = Visibility.Collapsed;
            canUseCurrentSavegameSettings = false;
            UseCurrentSavegameSettings = false;
            Changed(nameof(CanUseCurrentSavegameSettings));
            Changed(nameof(LoadOptionVisibility));
        }

        private void Changed(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
