using Noesis;
using System.ComponentModel;

namespace ExtendedData
{
    internal sealed class EditorModSettingsSaveOptionsViewModel : INotifyPropertyChanged
    {
        private bool enabled;
        private bool includeMapModSettings;
        private bool includeTrailModSettings = true;
        private Visibility mapOptionVisibility = Visibility.Collapsed;

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

        private void Changed(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
