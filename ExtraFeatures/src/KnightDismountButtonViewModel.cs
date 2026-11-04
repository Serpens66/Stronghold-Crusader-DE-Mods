// Feature: Troop-panel mount/dismount button state for the knight transformation.
using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace ExtraFeatures
{
    internal sealed class KnightDismountButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private bool wantsVisibility;
        private bool layoutAvailable = true;
        private Visibility dismountButtonVisibility = Visibility.Hidden;
        private Visibility mountButtonVisibility = Visibility.Hidden;
        private bool mountButtonEnabled;
        private bool dismountButtonEnabled;
        private bool mountDisabledTooltipHitTestVisible;
        private bool dismountDisabledTooltipHitTestVisible;

        public KnightDismountButtonViewModel(
            Action dismount,
            Action mount,
            Action showDismountTooltip,
            Action showMountTooltip,
            Action hideTooltip)
        {
            DismountCommand = new RelayCommand(dismount ?? throw new ArgumentNullException(nameof(dismount)));
            MountCommand = new RelayCommand(mount ?? throw new ArgumentNullException(nameof(mount)));
            DismountMouseEnterCommand = new RelayCommand(showDismountTooltip ?? throw new ArgumentNullException(nameof(showDismountTooltip)));
            MountMouseEnterCommand = new RelayCommand(showMountTooltip ?? throw new ArgumentNullException(nameof(showMountTooltip)));
            MouseLeaveCommand = new RelayCommand(hideTooltip ?? throw new ArgumentNullException(nameof(hideTooltip)));
        }

        public RelayCommand DismountCommand { get; }
        public RelayCommand MountCommand { get; }
        public RelayCommand DismountMouseEnterCommand { get; }
        public RelayCommand MountMouseEnterCommand { get; }
        public RelayCommand MouseLeaveCommand { get; }

        public int TroopActionLayoutVersion => 1;
        public string TroopActionId => "ExtraFeatures_Serp.KnightTransform";
        public int TroopActionPriority => 100;
        public bool WantsVisibility => wantsVisibility;

        public bool LayoutAvailable
        {
            get => layoutAvailable;
            set
            {
                if (layoutAvailable == value)
                    return;
                layoutAvailable = value;
                OnPropertyChanged(nameof(LayoutAvailable));
                OnPropertyChanged(nameof(ButtonVisibility));
            }
        }

        public Visibility ButtonVisibility => wantsVisibility && layoutAvailable
            ? Visibility.Visible
            : Visibility.Hidden;

        public Visibility DismountButtonVisibility
        {
            get => dismountButtonVisibility;
            private set
            {
                if (dismountButtonVisibility == value)
                    return;

                dismountButtonVisibility = value;
                OnPropertyChanged(nameof(DismountButtonVisibility));
            }
        }

        public Visibility MountButtonVisibility
        {
            get => mountButtonVisibility;
            private set
            {
                if (mountButtonVisibility == value)
                    return;

                mountButtonVisibility = value;
                OnPropertyChanged(nameof(MountButtonVisibility));
            }
        }

        public bool MountButtonEnabled
        {
            get => mountButtonEnabled;
            private set
            {
                if (mountButtonEnabled == value)
                    return;

                mountButtonEnabled = value;
                OnPropertyChanged(nameof(MountButtonEnabled));
            }
        }

        public bool DismountButtonEnabled
        {
            get => dismountButtonEnabled;
            private set
            {
                if (dismountButtonEnabled == value)
                    return;

                dismountButtonEnabled = value;
                OnPropertyChanged(nameof(DismountButtonEnabled));
            }
        }

        public bool MountDisabledTooltipHitTestVisible
        {
            get => mountDisabledTooltipHitTestVisible;
            private set
            {
                if (mountDisabledTooltipHitTestVisible == value)
                    return;

                mountDisabledTooltipHitTestVisible = value;
                OnPropertyChanged(nameof(MountDisabledTooltipHitTestVisible));
            }
        }

        public bool DismountDisabledTooltipHitTestVisible
        {
            get => dismountDisabledTooltipHitTestVisible;
            private set
            {
                if (dismountDisabledTooltipHitTestVisible == value)
                    return;

                dismountDisabledTooltipHitTestVisible = value;
                OnPropertyChanged(nameof(DismountDisabledTooltipHitTestVisible));
            }
        }

        public void Hide()
        {
            SetWantsVisibility(false);
            DismountButtonVisibility = Visibility.Hidden;
            MountButtonVisibility = Visibility.Hidden;
            MountButtonEnabled = false;
            DismountButtonEnabled = false;
            MountDisabledTooltipHitTestVisible = false;
            DismountDisabledTooltipHitTestVisible = false;
        }

        public void ShowDismount(bool enabled)
        {
            SetWantsVisibility(true);
            DismountButtonVisibility = Visibility.Visible;
            MountButtonVisibility = Visibility.Hidden;
            MountButtonEnabled = false;
            DismountButtonEnabled = enabled;
            MountDisabledTooltipHitTestVisible = false;
            DismountDisabledTooltipHitTestVisible = !enabled;
        }

        public void ShowMount(bool enabled)
        {
            SetWantsVisibility(true);
            DismountButtonVisibility = Visibility.Hidden;
            MountButtonVisibility = Visibility.Visible;
            MountButtonEnabled = enabled;
            DismountButtonEnabled = false;
            MountDisabledTooltipHitTestVisible = !enabled;
            DismountDisabledTooltipHitTestVisible = false;
        }

        private void SetWantsVisibility(bool value)
        {
            if (wantsVisibility == value)
                return;
            wantsVisibility = value;
            OnPropertyChanged(nameof(WantsVisibility));
            OnPropertyChanged(nameof(ButtonVisibility));
        }
    }
}
