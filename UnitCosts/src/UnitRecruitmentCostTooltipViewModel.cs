using Noesis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace UnitCosts
{
    public sealed class UnitRecruitmentCostTooltipViewModel : INotifyPropertyChanged
    {
        private bool hasCosts;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<UnitRecruitmentCostEntry> Costs { get; } = new ObservableCollection<UnitRecruitmentCostEntry>();

        public bool HasCosts
        {
            get => hasCosts;
            private set
            {
                if (hasCosts == value)
                    return;

                hasCosts = value;
                OnPropertyChanged(nameof(HasCosts));
                OnPropertyChanged(nameof(CostsVisibility));
            }
        }

        public Visibility CostsVisibility => HasCosts ? Visibility.Visible : Visibility.Collapsed;

        public void SetCosts(IEnumerable<UnitRecruitmentCostEntry> costs)
        {
            Costs.Clear();
            foreach (UnitRecruitmentCostEntry cost in costs)
                Costs.Add(cost);

            OnPropertyChanged(nameof(Costs));
            HasCosts = Costs.Count > 0;
            OnPropertyChanged(nameof(CostsVisibility));
        }

        public void Clear()
        {
            SetCosts(Array.Empty<UnitRecruitmentCostEntry>());
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
