using System;
using System.ComponentModel;
using System.Linq;

namespace ExtendedData
{
    // A second presentation of the existing setting; never shares Noesis item containers.
    internal sealed partial class CoopTrailMenuViewModel : INotifyPropertyChanged
    {
        private readonly ExtendedDataSettingsViewModel settings;
        private string[] options = Array.Empty<string>();
        private bool runtimeEnabled = true;
        private bool isDropDownOpen;
        private bool dismissingClick;
        private bool refreshingOptions;

        internal CoopTrailMenuViewModel(ExtendedDataSettingsViewModel settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            RefreshOptions();
            settings.PropertyChanged += OnSettingsChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string[] Options => options;
        public string HelpText => settings.CoopPackageText + ": " + settings.CoopPackageHelpText;
        public bool CanEdit => runtimeEnabled && settings.CanEditCoopPackage;
        public bool IsInputBlocked => isDropDownOpen || dismissingClick;
        internal bool IsDismissingClick => dismissingClick;

        public int SelectedIndex
        {
            get => Array.IndexOf(settings.CoopPackageOptions, settings.SelectedCoopPackage);
            set
            {
                if (refreshingOptions) return;
                if (CanEdit && value >= 0 && value < settings.CoopPackageOptions.Length)
                    settings.SelectedCoopPackage = settings.CoopPackageOptions[value];
                // Reassert the authoritative value if the setting rejected the edit.
                Changed(nameof(SelectedIndex));
            }
        }

        public bool IsDropDownOpen
        {
            get => isDropDownOpen;
            set
            {
                value = value && CanEdit;
                if (isDropDownOpen == value) return;
                isDropDownOpen = value;
                Changed(nameof(IsDropDownOpen));
                Changed(nameof(IsInputBlocked));
            }
        }

        internal void BeginDismissClick()
        {
            // Keep the shield alive after closing the popup, until this click's release.
            dismissingClick = true;
            IsDropDownOpen = false;
            Changed(nameof(IsInputBlocked));
        }

        internal void EndDismissClick()
        {
            dismissingClick = false;
            Changed(nameof(IsInputBlocked));
        }

        internal void ResetView()
        {
            IsDropDownOpen = false;
            EndDismissClick();
        }

        internal void Disable()
        {
            runtimeEnabled = false;
            ResetView();
            Changed(nameof(CanEdit));
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs args)
        {
            string name = args.PropertyName;
            if (string.IsNullOrEmpty(name) || name == nameof(settings.CoopPackageOptions))
                RefreshOptions();
            if (string.IsNullOrEmpty(name) || name == nameof(settings.CoopPackageOptions) ||
                name == nameof(settings.SelectedCoopPackage) || name == nameof(settings.ActiveCoopPackageId))
                Changed(nameof(SelectedIndex));
            if (string.IsNullOrEmpty(name) || name == nameof(settings.CanEditCoopPackage) ||
                name == nameof(settings.CanEditHostSettings))
            {
                if (!CanEdit) IsDropDownOpen = false;
                Changed(nameof(CanEdit));
            }
            if (string.IsNullOrEmpty(name) || name == nameof(settings.CoopPackageText) ||
                name == nameof(settings.CoopPackageHelpText))
                Changed(nameof(HelpText));
        }

        private void RefreshOptions()
        {
            string[] refreshed = settings.CoopPackageOptions
                .Select(item => item.Content?.ToString() ?? string.Empty).ToArray();
            if (options.SequenceEqual(refreshed, StringComparer.Ordinal)) return;
            options = refreshed;
            // Replacing ItemsSource can synchronously reset Noesis' selection. That is
            // presentation maintenance, not a user request to select Vanilla.
            refreshingOptions = true;
            try { Changed(nameof(Options)); }
            finally { refreshingOptions = false; }
        }

        private void Changed(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
