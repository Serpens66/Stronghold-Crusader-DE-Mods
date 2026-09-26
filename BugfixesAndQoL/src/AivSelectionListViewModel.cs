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
        private bool activeAllowRemoval;

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
            activeAllowRemoval = allowRemoval;
            int index = 0;
            bool rebuildingTail = false;
            if (info?.aivs != null)
            {
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    if (aiv == null)
                        continue;
                    AivCandidateStatusApi.TryGetStatus(info, aiv.checksum, out AivCandidateStatusInfo status);
                    if (!rebuildingTail && index < Entries.Count &&
                        ReferenceEquals(Entries[index].Aiv, aiv))
                    {
                        Entries[index].Update(allowRemoval, status);
                        index++;
                        continue;
                    }
                    if (!rebuildingTail && index < Entries.Count)
                    {
                        while (Entries.Count > index)
                            Entries.RemoveAt(Entries.Count - 1);
                        rebuildingTail = true;
                    }
                    Entries.Add(new AivSelectionRowViewModel(
                        aiv,
                        GetIcon(aiv),
                        allowRemoval,
                        status,
                        () => RemoveRequested?.Invoke(aiv)));
                    index++;
                }
            }
            while (Entries.Count > index)
                Entries.RemoveAt(Entries.Count - 1);
            CountText = $"{Entries.Count} / {AivAicPresetStore.MaximumAivEntries}";
        }

        private void OnStatusChanged(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (ReferenceEquals(info, ActiveInfo))
            {
                foreach (AivSelectionRowViewModel entry in Entries)
                {
                    AivCandidateStatusApi.TryGetStatus(info, entry.Aiv.checksum, out AivCandidateStatusInfo status);
                    entry.Update(activeAllowRemoval, status);
                }
            }
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
            RemoveCommand = new RelayCommand(remove ?? throw new ArgumentNullException(nameof(remove)));
            Update(allowRemoval, status);
        }

        public CustomisationFileManager.CustomAIV Aiv { get; }
        public string Name => Aiv.AIVName ?? string.Empty;
        public string GlobalTextFlowAL2R => MainViewModel.viewModelLoaded
            ? MainViewModel.Instance?.GlobalTextFlowAL2R ?? "LeftToRight"
            : "LeftToRight";
        public ImageSource Icon { get; }
        public RelayCommand RemoveCommand { get; }
        public Visibility RemoveVisibility { get; private set; }
        public string PercentageText { get; private set; } = "-%";
        public string StatusToolTip { get; private set; }
        public string RemoveHelpText => SerpLocalization.Get("BugfixesAndQoL.AivRemoveHelp");

        public void Update(bool allowRemoval, AivCandidateStatusInfo status)
        {
            Set(nameof(RemoveVisibility), ToVisibility(allowRemoval), RemoveVisibility,
                value => RemoveVisibility = value);
            string percentage = status?.PercentageText ?? "-%";
            if (!string.Equals(PercentageText, percentage, StringComparison.Ordinal))
            {
                PercentageText = percentage;
                OnPropertyChanged(nameof(PercentageText));
            }
            string tip = status?.ToolTip ?? string.Empty;
            if (!string.Equals(StatusToolTip, tip, StringComparison.Ordinal))
            {
                StatusToolTip = tip;
                OnPropertyChanged(nameof(StatusToolTip));
            }
        }

        private void Set(string property, Visibility value, Visibility previous, Action<Visibility> assign)
        {
            if (value == previous)
                return;
            assign(value);
            OnPropertyChanged(property);
        }

        private static Visibility ToVisibility(bool value) =>
            value ? Visibility.Visible : Visibility.Collapsed;
    }
}
