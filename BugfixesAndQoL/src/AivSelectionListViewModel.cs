using CrusaderDE;
using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace BugfixesAndQoL
{
    internal sealed class AivSelectionListViewModel : LobbyModSettingsBaseViewModel
    {
        private string countText = "0 / 50";

        public AivSelectionListViewModel()
        {
            AivCandidateStatusApi.StatusChanged += OnStatusChanged;
        }

        public ObservableCollection<AivSelectionRowViewModel> Entries { get; } =
            new ObservableCollection<AivSelectionRowViewModel>();

        public event Action<CustomisationFileManager.CustomAIV> RemoveRequested;

        public FRONT_Multiplayer.MPAIVInfo ActiveInfo { get; private set; }

        public string CountText
        {
            get => countText;
            private set
            {
                if (string.Equals(countText, value, StringComparison.Ordinal))
                    return;
                countText = value;
                OnPropertyChanged(nameof(CountText));
            }
        }

        public void Refresh(FRONT_Multiplayer.MPAIVInfo info, bool allowRemoval)
        {
            ActiveInfo = info;
            Entries.Clear();
            if (info?.aivs != null)
            {
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    if (aiv == null)
                        continue;
                    AivCandidateStatusApi.TryGetStatus(info, aiv.checksum, out AivCandidateStatusInfo status);
                    Entries.Add(new AivSelectionRowViewModel(
                        aiv,
                        GetIcon(aiv),
                        allowRemoval,
                        status,
                        () => RemoveRequested?.Invoke(aiv)));
                }
            }
            CountText = $"{Entries.Count} / {AivAicPresetStore.MaximumAivEntries}";
        }

        private void OnStatusChanged(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (ReferenceEquals(info, ActiveInfo))
                Refresh(ActiveInfo, true);
        }

        private static ImageSource GetIcon(CustomisationFileManager.CustomAIV aiv)
        {
            if (!MainViewModel.viewModelLoaded || MainViewModel.Instance?.GameSprites == null)
                return null;
            return aiv.builtIn
                ? MainViewModel.Instance.GameSprites[88]
                : aiv.workshop
                    ? MainViewModel.Instance.GameSprites[89]
                    : MainViewModel.Instance.GameSprites[90];
        }
    }

    internal sealed class AivSelectionRowViewModel : LobbyModSettingsBaseViewModel
    {
        public AivSelectionRowViewModel(
            CustomisationFileManager.CustomAIV aiv,
            ImageSource icon,
            bool allowRemoval,
            AivCandidateStatusInfo status,
            Action remove)
        {
            Aiv = aiv ?? throw new ArgumentNullException(nameof(aiv));
            Icon = icon;
            RemoveVisibility = allowRemoval ? Visibility.Visible : Visibility.Collapsed;
            RemoveCommand = new RelayCommand(remove ?? throw new ArgumentNullException(nameof(remove)));
            StatusToolTip = status?.ToolTip ?? string.Empty;
            AivCandidateStatus? value = status?.Status;
            PendingVisibility = ToVisibility(value == AivCandidateStatus.Pending);
            CompleteVisibility = ToVisibility(value == AivCandidateStatus.Complete);
            PartialVisibility = ToVisibility(value == AivCandidateStatus.Partial);
            ImpossibleVisibility = ToVisibility(value == AivCandidateStatus.Impossible);
            NotEvaluableVisibility = ToVisibility(value == AivCandidateStatus.NotEvaluable);
        }

        public CustomisationFileManager.CustomAIV Aiv { get; }
        public string Name => Aiv.AIVName ?? string.Empty;
        public string GlobalTextFlowAL2R => MainViewModel.viewModelLoaded
            ? MainViewModel.Instance?.GlobalTextFlowAL2R ?? "LeftToRight"
            : "LeftToRight";
        public ImageSource Icon { get; }
        public RelayCommand RemoveCommand { get; }
        public Visibility RemoveVisibility { get; }
        public Visibility PendingVisibility { get; }
        public Visibility CompleteVisibility { get; }
        public Visibility PartialVisibility { get; }
        public Visibility ImpossibleVisibility { get; }
        public Visibility NotEvaluableVisibility { get; }
        public string StatusToolTip { get; }
        public string RemoveHelpText => SerpLocalization.Get("BugfixesAndQoL.AivRemoveHelp");

        private static Visibility ToVisibility(bool value) =>
            value ? Visibility.Visible : Visibility.Collapsed;
    }
}
