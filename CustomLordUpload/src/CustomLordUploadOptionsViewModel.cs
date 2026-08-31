using Noesis;
using System.ComponentModel;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadOptionsViewModel : INotifyPropertyChanged
    {
        private bool includeAdditionalFiles = true;
        private Visibility optionVisibility = Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IncludeAdditionalFiles
        {
            get => includeAdditionalFiles;
            set
            {
                if (includeAdditionalFiles == value)
                    return;
                includeAdditionalFiles = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IncludeAdditionalFiles)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OptionVisibility)));
            }
        }

        public string LabelText => SerpLocalization.Get("WorkshopUpload.IncludeAdditionalFiles");
        public string HelpText => SerpLocalization.Get("WorkshopUpload.IncludeAdditionalFilesHelp");

        internal void Open()
        {
            IncludeAdditionalFiles = true;
            OptionVisibility = Visibility.Visible;
        }

        internal void Close() => OptionVisibility = Visibility.Collapsed;
    }
}
