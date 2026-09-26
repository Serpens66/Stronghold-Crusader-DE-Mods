using Noesis;
using System;
using System.ComponentModel;

namespace BugfixesAndQoL
{
    internal sealed class TimerCountdownViewModel : INotifyPropertyChanged
    {
        private string objectiveRemaining = string.Empty;
        private string ostRemaining = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ObjectiveRemaining => objectiveRemaining;
        public string OstRemaining => ostRemaining;
        public Visibility ObjectiveVisibility => objectiveRemaining.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility OstVisibility => ostRemaining.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        internal bool TrySetRemaining(string objective, string ost, out Exception failure)
        {
            try
            {
                SetRemaining(objective, ost);
                failure = null;
                return true;
            }
            catch (Exception ex)
            {
                failure = ex;
                return false;
            }
        }

        internal void SetRemaining(string objective, string ost)
        {
            if (objectiveRemaining != objective)
            {
                bool visibilityChanged = (objectiveRemaining.Length == 0) != (objective.Length == 0);
                objectiveRemaining = objective;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ObjectiveRemaining)));
                if (visibilityChanged)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ObjectiveVisibility)));
            }
            if (ostRemaining != ost)
            {
                bool visibilityChanged = (ostRemaining.Length == 0) != (ost.Length == 0);
                ostRemaining = ost;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OstRemaining)));
                if (visibilityChanged)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OstVisibility)));
            }
        }
    }
}
