using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIVPlacement.Core;
using CrusaderDE;
using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;

namespace CastlePlanner.AIVPlacement
{
    internal sealed class AivCandidateVisualState
    {
        public static readonly AivCandidateVisualState Pending = new AivCandidateVisualState(null, string.Empty);

        public AivCandidateVisualState(AivPlacementStatus? status, string toolTip)
        {
            Status = status;
            ToolTip = toolTip ?? string.Empty;
        }

        public AivPlacementStatus? Status { get; }
        public string ToolTip { get; }
    }

    internal sealed class AivSelectionListViewModel : LobbyModSettingsBaseViewModel
    {
        public ObservableCollection<AivSelectionRowViewModel> Entries { get; } =
            new ObservableCollection<AivSelectionRowViewModel>();

        public event Action<CustomisationFileManager.CustomAIV> RemoveRequested;

        private string countText = "0 / 8";

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

        public void Refresh(
            FRONT_Multiplayer.MPAIVInfo info,
            bool allowRemoval,
            IReadOnlyDictionary<int, AivCandidateVisualState> states,
            int maximumEntries)
        {
            int entryIndex = 0;
            bool rebuildingTail = false;
            if (info?.aivs != null)
            {
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    if (aiv == null)
                        continue;

                    AivCandidateVisualState state = states != null && states.TryGetValue(entryIndex, out AivCandidateVisualState found)
                        ? found
                        : AivCandidateVisualState.Pending;
                    if (!rebuildingTail && entryIndex < Entries.Count && Entries[entryIndex].Matches(aiv))
                    {
                        Entries[entryIndex].Update(entryIndex, allowRemoval, state);
                        entryIndex++;
                        continue;
                    }

                    if (!rebuildingTail && entryIndex < Entries.Count)
                    {
                        while (Entries.Count > entryIndex)
                            Entries.RemoveAt(Entries.Count - 1);
                        rebuildingTail = true;
                    }

                    Entries.Add(CreateRow(aiv, entryIndex, allowRemoval, state));
                    entryIndex++;
                }
            }

            while (Entries.Count > entryIndex)
                Entries.RemoveAt(Entries.Count - 1);
            CountText = $"{entryIndex} / {maximumEntries}";
        }

        private AivSelectionRowViewModel CreateRow(
            CustomisationFileManager.CustomAIV aiv,
            int candidateId,
            bool allowRemoval,
            AivCandidateVisualState state)
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

            return new AivSelectionRowViewModel(
                aiv,
                candidateId,
                icon,
                allowRemoval,
                state,
                () => RemoveRequested?.Invoke(aiv));
        }
    }

    internal sealed class AivSelectionRowViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility removeVisibility;
        private Visibility completeVisibility;
        private Visibility partialVisibility;
        private Visibility impossibleVisibility;
        private Visibility notEvaluableVisibility;
        private string statusToolTip = string.Empty;

        public AivSelectionRowViewModel(
            CustomisationFileManager.CustomAIV aiv,
            int candidateId,
            ImageSource icon,
            bool allowRemoval,
            AivCandidateVisualState state,
            Action remove)
        {
            Aiv = aiv ?? throw new ArgumentNullException(nameof(aiv));
            CandidateId = candidateId;
            Icon = icon;
            RemoveCommand = new RelayCommand(remove ?? throw new ArgumentNullException(nameof(remove)));
            Update(candidateId, allowRemoval, state);
        }

        public CustomisationFileManager.CustomAIV Aiv { get; }
        public int CandidateId { get; private set; }
        public string Name => Aiv.AIVName ?? string.Empty;
        public string GlobalTextFlowAL2R => MainViewModel.Instance?.GlobalTextFlowAL2R ?? "LeftToRight";
        public ImageSource Icon { get; }
        public RelayCommand RemoveCommand { get; }
        public Visibility RemoveVisibility => removeVisibility;
        public Visibility CompleteVisibility => completeVisibility;
        public Visibility PartialVisibility => partialVisibility;
        public Visibility ImpossibleVisibility => impossibleVisibility;
        public Visibility NotEvaluableVisibility => notEvaluableVisibility;
        public string StatusToolTip => statusToolTip;
        public string RemoveHelpText => SerpLocalization.Get("CastlePlanner.AivRemoveHelp");

        public bool Matches(CustomisationFileManager.CustomAIV aiv) =>
            aiv != null && (ReferenceEquals(Aiv, aiv) || Aiv.checksum == aiv.checksum);

        public void Update(int candidateId, bool allowRemoval, AivCandidateVisualState state)
        {
            CandidateId = candidateId;
            Set(ref removeVisibility, ToVisibility(allowRemoval), nameof(RemoveVisibility));
            AivPlacementStatus? status = state?.Status;
            Set(ref completeVisibility, ToVisibility(status == AivPlacementStatus.Complete), nameof(CompleteVisibility));
            Set(ref partialVisibility, ToVisibility(status == AivPlacementStatus.Partial), nameof(PartialVisibility));
            Set(ref impossibleVisibility, ToVisibility(status == AivPlacementStatus.Impossible), nameof(ImpossibleVisibility));
            Set(ref notEvaluableVisibility, ToVisibility(status == AivPlacementStatus.NotEvaluable), nameof(NotEvaluableVisibility));

            string toolTip = state?.ToolTip ?? string.Empty;
            if (!string.Equals(statusToolTip, toolTip, StringComparison.Ordinal))
            {
                statusToolTip = toolTip;
                OnPropertyChanged(nameof(StatusToolTip));
            }
        }

        private void Set(ref Visibility field, Visibility value, string propertyName)
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged(propertyName);
        }

        private static Visibility ToVisibility(bool visible) =>
            visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
