using CrusaderDE;
using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace SomeSettings
{
    internal sealed class AiAivSelectionListViewModel : LobbyModSettingsBaseViewModel
    {
        public ObservableCollection<AiAivSelectionRowViewModel> Entries { get; } =
            new ObservableCollection<AiAivSelectionRowViewModel>();

        public event Action<CustomisationFileManager.CustomAIV> RemoveRequested;

        private string countText = $"0 / {SkirmishAiSelectionMemoryHook.MaxCustomAivsPerLord}";

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
            int entryIndex = 0;
            bool rebuildingTail = false;
            if (info?.aivs != null)
            {
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    if (aiv == null)
                        continue;

                    if (!rebuildingTail &&
                        entryIndex < Entries.Count &&
                        Entries[entryIndex].Matches(aiv))
                    {
                        Entries[entryIndex].SetRemovalAllowed(allowRemoval);
                        entryIndex++;
                        continue;
                    }

                    if (!rebuildingTail && entryIndex < Entries.Count)
                    {
                        while (Entries.Count > entryIndex)
                            Entries.RemoveAt(Entries.Count - 1);
                        rebuildingTail = true;
                    }

                    Entries.Add(CreateRow(aiv, allowRemoval));
                    entryIndex++;
                }
            }

            while (Entries.Count > entryIndex)
                Entries.RemoveAt(Entries.Count - 1);

            CountText = $"{entryIndex} / {SkirmishAiSelectionMemoryHook.MaxCustomAivsPerLord}";
        }

        private AiAivSelectionRowViewModel CreateRow(
            CustomisationFileManager.CustomAIV aiv,
            bool allowRemoval)
        {
            ImageSource icon = null;
            if (MainViewModel.Instance != null)
            {
                icon = aiv.builtIn
                    ? MainViewModel.Instance.GameSprites[88]
                    : aiv.workshop
                        ? MainViewModel.Instance.GameSprites[89]
                        : MainViewModel.Instance.GameSprites[90];
            }

            return new AiAivSelectionRowViewModel(
                aiv,
                icon,
                allowRemoval,
                () => RemoveRequested?.Invoke(aiv));
        }
    }

    internal sealed class AiAivSelectionRowViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility removeVisibility;

        public AiAivSelectionRowViewModel(
            CustomisationFileManager.CustomAIV aiv,
            ImageSource icon,
            bool allowRemoval,
            Action remove)
        {
            Aiv = aiv ?? throw new ArgumentNullException(nameof(aiv));
            Icon = icon;
            removeVisibility = ToVisibility(allowRemoval);
            RemoveCommand = new RelayCommand(remove ?? throw new ArgumentNullException(nameof(remove)));
        }

        public CustomisationFileManager.CustomAIV Aiv { get; }
        public string Name => Aiv.AIVName ?? string.Empty;
        public string GlobalTextFlowAL2R =>
            MainViewModel.Instance?.GlobalTextFlowAL2R ?? "LeftToRight";
        public ImageSource Icon { get; }
        public Visibility RemoveVisibility
        {
            get => removeVisibility;
            private set
            {
                if (removeVisibility == value)
                    return;

                removeVisibility = value;
                OnPropertyChanged(nameof(RemoveVisibility));
            }
        }

        public RelayCommand RemoveCommand { get; }

        public bool Matches(CustomisationFileManager.CustomAIV aiv)
        {
            return aiv != null &&
                   (ReferenceEquals(Aiv, aiv) || Aiv.checksum == aiv.checksum);
        }

        public void SetRemovalAllowed(bool allowRemoval)
        {
            RemoveVisibility = ToVisibility(allowRemoval);
        }

        private static Visibility ToVisibility(bool visible)
        {
            return visible ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
