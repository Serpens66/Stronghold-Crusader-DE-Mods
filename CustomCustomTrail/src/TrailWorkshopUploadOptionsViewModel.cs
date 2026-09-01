using Noesis;
using System.ComponentModel;

namespace CustomCustomTrail
{
    internal sealed class TrailWorkshopUploadOptionsViewModel : INotifyPropertyChanged
    {
        private bool includeModSettings = true;
        private Visibility optionVisibility = Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IncludeModSettings
        {
            get => includeModSettings;
            set
            {
                if (includeModSettings == value)
                    return;
                includeModSettings = value;
                Changed(nameof(IncludeModSettings));
            }
        }

        public Visibility OptionVisibility
        {
            get => optionVisibility;
            private set
            {
                if (optionVisibility == value)
                    return;
                optionVisibility = value;
                Changed(nameof(OptionVisibility));
            }
        }

        public string LabelText => SerpLocalization.Get("WorkshopUpload.IncludeModSettings");
        public string HelpText => SerpLocalization.Get("WorkshopUpload.IncludeModSettingsHelp");

        internal void Open()
        {
            includeModSettings = true;
            OptionVisibility = Visibility.Visible;
            Changed(nameof(IncludeModSettings));
        }

        internal void Close() => OptionVisibility = Visibility.Collapsed;

        private void Changed(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
