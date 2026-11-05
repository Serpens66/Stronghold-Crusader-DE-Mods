using Noesis;
using System;
using System.ComponentModel;

namespace BugfixesAndQoL
{
    internal sealed class TimerCountdownViewModel : INotifyPropertyChanged
    {
        private string objectiveRemaining = string.Empty;
        private string ostRemaining = string.Empty;
        private int pendingNotifications;
        private const int ObjectiveText = 1, ObjectiveShown = 2, OstText = 4, OstShown = 8;

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
                pendingNotifications |= ObjectiveText;
                if (visibilityChanged) pendingNotifications |= ObjectiveShown;
            }
            NotifyPending(ObjectiveText, nameof(ObjectiveRemaining));
            NotifyPending(ObjectiveShown, nameof(ObjectiveVisibility));
            if (ostRemaining != ost)
            {
                bool visibilityChanged = (ostRemaining.Length == 0) != (ost.Length == 0);
                ostRemaining = ost;
                pendingNotifications |= OstText;
                if (visibilityChanged) pendingNotifications |= OstShown;
            }
            NotifyPending(OstText, nameof(OstRemaining));
            NotifyPending(OstShown, nameof(OstVisibility));
        }

        private void NotifyPending(int flag, string property)
        {
            if ((pendingNotifications & flag) == 0) return;
            // Clear before notifying so reentrant changes survive. A failed notification is
            // retained for the next scheduled attempt even though its backing value changed.
            pendingNotifications &= ~flag;
            try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property)); }
            catch { pendingNotifications |= flag; throw; }
        }
    }
}
