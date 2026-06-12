using Noesis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuildingCosts
{
    public sealed class BuildingCostTooltipViewModel : INotifyPropertyChanged
    {
        private bool hasCosts;
        private bool showDetailed;
        private bool showCompact;
        private string rollOverText = "";

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<BuildingCostTooltipEntry> Costs { get; } = new ObservableCollection<BuildingCostTooltipEntry>();

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

        public bool HasCosts
        {
            get => hasCosts;
            private set
            {
                if (hasCosts == value)
                    return;

                hasCosts = value;
                OnPropertyChanged(nameof(HasCosts));
                OnPropertyChanged(nameof(DetailedVisibility));
                OnPropertyChanged(nameof(CompactVisibility));
            }
        }

        public Visibility DetailedVisibility => HasCosts && showDetailed ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CompactVisibility => HasCosts && showCompact ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CostsVisibility => HasCosts ? Visibility.Visible : Visibility.Collapsed;

        public void SetTooltip(string rollOverText, IEnumerable<BuildingCostTooltipEntry> costs)
        {
            RollOverText = rollOverText ?? "";
            Costs.Clear();
            foreach (BuildingCostTooltipEntry cost in costs)
                Costs.Add(cost);

            HasCosts = Costs.Count > 0;
            OnPropertyChanged(nameof(CostsVisibility));
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
