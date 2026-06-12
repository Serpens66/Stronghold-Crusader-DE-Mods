using Noesis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuildingCosts
{
    public sealed class BuildingCostTooltipViewModel : INotifyPropertyChanged
    {
        private bool hasAdditionalCosts;
        private bool showDetailed;
        private bool showCompact;
        private string rollOverText = "";

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<BuildingCostTooltipEntry> AdditionalCosts { get; } = new ObservableCollection<BuildingCostTooltipEntry>();

        public string RollOverText
        {
            get => rollOverText;
            private set
            {
                if (rollOverText == value)
                    return;

                rollOverText = value;
                OnPropertyChanged(nameof(RollOverText));
            }
        }

        public bool HasAdditionalCosts
        {
            get => hasAdditionalCosts;
            private set
            {
                if (hasAdditionalCosts == value)
                    return;

                hasAdditionalCosts = value;
                OnPropertyChanged(nameof(HasAdditionalCosts));
                OnPropertyChanged(nameof(DetailedVisibility));
                OnPropertyChanged(nameof(CompactVisibility));
                OnPropertyChanged(nameof(AdditionalCostsVisibility));
            }
        }

        public Visibility DetailedVisibility => HasAdditionalCosts && showDetailed ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CompactVisibility => HasAdditionalCosts && showCompact ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AdditionalCostsVisibility => HasAdditionalCosts ? Visibility.Visible : Visibility.Collapsed;

        public void SetTooltip(string rollOverText, IEnumerable<BuildingCostTooltipEntry> costs)
        {
            RollOverText = rollOverText ?? "";
            AdditionalCosts.Clear();
            foreach (BuildingCostTooltipEntry cost in costs)
                AdditionalCosts.Add(cost);

            HasAdditionalCosts = AdditionalCosts.Count > 0;
            OnPropertyChanged(nameof(AdditionalCosts));
            OnPropertyChanged(nameof(AdditionalCostsVisibility));
        }

        public void Clear()
        {
            SetTooltip("", Array.Empty<BuildingCostTooltipEntry>());
        }

        public void SetRollOverTextOnly(string rollOverText)
        {
            SetTooltip(rollOverText, Array.Empty<BuildingCostTooltipEntry>());
        }

        public void SetPlacement(bool detailed, bool compact)
        {
            if (showDetailed == detailed && showCompact == compact)
                return;

            showDetailed = detailed;
            showCompact = compact;
            OnPropertyChanged(nameof(DetailedVisibility));
            OnPropertyChanged(nameof(CompactVisibility));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
