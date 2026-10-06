using System;
using Noesis;
using Shared;
using SHCDESE.NoesisUtil;

namespace SerpsModsHost
{
    public sealed partial class SerpsModsDiagnosticsViewModel
    {
        private bool restartConfirmation;
        private string restartResult = "", restartDetails = "";
        public RelayCommand RequestRestartDiscardCommand { get; private set; }
        public RelayCommand ConfirmRestartDiscardCommand { get; private set; }
        public RelayCommand CancelRestartDiscardCommand { get; private set; }
        public RelayCommand DismissRestartResultCommand { get; private set; }
        public string RestartTitle => SerpLocalization.Get("RestartPreparation.Title");
        public string RestartDiscardText => SerpLocalization.Get("RestartPreparation.Discard");
        public string RestartConfirmText => SerpLocalization.Get("RestartPreparation.Confirm");
        public string RestartHelp => SerpLocalization.Get("RestartPreparation.Help");
        public string RestartDetails => restartDetails;
        public Visibility RestartPreparationVisibility => ModSettingsApplication.HasRestartPreparation ||
            ModSettingsApplication.HasActivationFailures || restartResult.Length != 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RestartDiscardVisibility => ModSettingsApplication.HasRestartPreparation ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RestartConfirmationVisibility => restartConfirmation ? Visibility.Visible : Visibility.Collapsed;

        private void InitializeRestartPreparation()
        {
            RequestRestartDiscardCommand = new RelayCommand(() => { restartConfirmation = true; RefreshRestartPreparation(); });
            CancelRestartDiscardCommand = new RelayCommand(() => { restartConfirmation = false; RefreshRestartPreparation(); });
            DismissRestartResultCommand = new RelayCommand(() => { restartResult = ""; RefreshRestartPreparation(); });
            ConfirmRestartDiscardCommand = new RelayCommand(() =>
            {
                if (!restartConfirmation) return;
                restartConfirmation = false;
                try
                {
                    string[] unresolved = ModSettingsApplication.DiscardPreparationWithReport();
                    restartResult = SerpLocalization.Get(unresolved.Length == 0 ? "RestartPreparation.Discarded" : "RestartPreparation.Partial");
                    if (unresolved.Length != 0) restartResult += "\n" + string.Join(", ", unresolved);
                }
                catch (Exception ex) { restartResult = ex.GetBaseException().Message; }
                RefreshRestartPreparation();
            });
            ModSettingsApplication.PreparationChanged += RefreshRestartPreparation;
            RefreshRestartPreparation();
        }

        private void RefreshRestartPreparation()
        {
            restartDetails = ModSettingsApplication.DescribeRestartPreparation();
            if (restartResult.Length != 0) restartDetails = restartResult + "\n" + restartDetails;
            OnPropertyChanged(nameof(RestartDetails));
            OnPropertyChanged(nameof(RestartPreparationVisibility));
            OnPropertyChanged(nameof(RestartDiscardVisibility));
            OnPropertyChanged(nameof(RestartConfirmationVisibility));
        }
    }
}
