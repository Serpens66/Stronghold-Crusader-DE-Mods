using Noesis;
using System.ComponentModel;

namespace CustomCustomTrail
{
    internal sealed class TrailWorkshopUploadOptionsViewModel : INotifyPropertyChanged
    {
        private bool includeAdditionalFiles = true;
        private bool canChangeOption = true;
        private bool coopPackage;
        private Visibility optionVisibility = Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IncludeAdditionalFiles
        {
            get => includeAdditionalFiles;
            set
            {
                if (includeAdditionalFiles == value || !canChangeOption)
                    return;
                includeAdditionalFiles = value;
                Changed(nameof(IncludeAdditionalFiles));
            }
        }

        public bool CanChangeOption
        {
            get => canChangeOption;
            private set
            {
                if (canChangeOption == value)
                    return;
                canChangeOption = value;
                Changed(nameof(CanChangeOption));
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

        public string LabelText => SerpLocalization.Get("WorkshopUpload.IncludeAdditionalFiles");
        public string HelpText => SerpLocalization.Get(
            coopPackage
                ? "WorkshopUpload.CoopAdditionalFilesRequiredHelp"
                : "WorkshopUpload.IncludeAdditionalFilesHelp");

        internal void Open(bool isCoopPackage)
        {
            coopPackage = isCoopPackage;
            includeAdditionalFiles = true;
            CanChangeOption = !isCoopPackage;
            OptionVisibility = Visibility.Visible;
            Changed(nameof(IncludeAdditionalFiles));
            Changed(nameof(HelpText));
        }

        internal void Close() => OptionVisibility = Visibility.Collapsed;

        private void Changed(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
