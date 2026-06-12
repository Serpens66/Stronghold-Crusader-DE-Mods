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

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<BuildingCostTooltipEntry> Costs { get; } = new ObservableCollection<BuildingCostTooltipEntry>();

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

        public void SetCosts(IEnumerable<BuildingCostTooltipEntry> costs)
        {
            Costs.Clear();
            foreach (BuildingCostTooltipEntry cost in costs)
                Costs.Add(cost);

            HasCosts = Costs.Count > 0;
        }

        public void Clear()
        {
            SetCosts(Array.Empty<BuildingCostTooltipEntry>());
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
