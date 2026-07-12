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
            List<UnitRecruitmentCostEntry> updatedCosts = new List<UnitRecruitmentCostEntry>(costs ?? Array.Empty<UnitRecruitmentCostEntry>());
            if (CostsMatch(updatedCosts))
                return;

            Costs.Clear();
            foreach (UnitRecruitmentCostEntry cost in updatedCosts)
                Costs.Add(cost);

            OnPropertyChanged(nameof(Costs));
            HasCosts = Costs.Count > 0;
            OnPropertyChanged(nameof(CostsVisibility));
        }

        private bool CostsMatch(IReadOnlyList<UnitRecruitmentCostEntry> updatedCosts)
        {
            if (Costs.Count != updatedCosts.Count)
                return false;

            for (int i = 0; i < Costs.Count; i++)
            {
                UnitRecruitmentCostEntry current = Costs[i];
                UnitRecruitmentCostEntry updated = updatedCosts[i];
                if (current.Amount != updated.Amount ||
                    current.AmountAvailable != updated.AmountAvailable ||
                    !ReferenceEquals(current.Image, updated.Image))
                {
                    return false;
                }
            }

            return true;
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
