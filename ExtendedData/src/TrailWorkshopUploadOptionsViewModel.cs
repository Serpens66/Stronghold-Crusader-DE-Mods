using Noesis;
using System.ComponentModel;

namespace ExtendedData
{
    internal enum WorkshopUploadOptionKind
    {
        Trail,
        CustomLord
    }

    internal sealed class TrailWorkshopUploadOptionsViewModel : INotifyPropertyChanged
    {
        private bool includeExtendedData = true;
        private Visibility optionVisibility = Visibility.Collapsed;
        private WorkshopUploadOptionKind optionKind;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IncludeExtendedData
        {
            get => includeExtendedData;
            set
            {
                if (includeExtendedData == value)
                    return;
                includeExtendedData = value;
                Changed(nameof(IncludeExtendedData));
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

        public string LabelText => SerpLocalization.Get(
            optionKind == WorkshopUploadOptionKind.CustomLord
                ? "WorkshopUpload.IncludeAdditionalFiles"
                : "WorkshopUpload.IncludeModSettings");
        public string HelpText => SerpLocalization.Get(
            optionKind == WorkshopUploadOptionKind.CustomLord
                ? "WorkshopUpload.IncludeAdditionalFilesHelp"
                : "WorkshopUpload.IncludeModSettingsHelp");

        internal void Open(WorkshopUploadOptionKind kind)
        {
            optionKind = kind;
            includeExtendedData = true;
            OptionVisibility = Visibility.Visible;
            Changed(nameof(IncludeExtendedData));
            Changed(nameof(LabelText));
            Changed(nameof(HelpText));
        }

        internal void Close() => OptionVisibility = Visibility.Collapsed;

        private void Changed(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
