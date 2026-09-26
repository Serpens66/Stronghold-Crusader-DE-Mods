using System.ComponentModel;

namespace TimerCountdownTest
{
    internal sealed class TimerCountdownViewModel : INotifyPropertyChanged
    {
        // BEGIN TEMP CRASH DIAGNOSTICS: change these independently in later isolation runs.
        internal static readonly bool NotifyObjectiveRemaining = true;
        internal static readonly bool NotifyOstRemaining = true;
        // END TEMP CRASH DIAGNOSTICS

        private string objectiveRemaining = string.Empty;
        private string ostRemaining = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ObjectiveRemaining
        {
            get { return objectiveRemaining; }
            private set
            {
                if (objectiveRemaining == value) return;
                objectiveRemaining = value;
                // BEGIN TEMP CRASH DIAGNOSTICS
                if (NotifyObjectiveRemaining)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ObjectiveRemaining)));
                // END TEMP CRASH DIAGNOSTICS
            }
        }

        public string OstRemaining
        {
            get { return ostRemaining; }
            private set
            {
                if (ostRemaining == value) return;
                ostRemaining = value;
                // BEGIN TEMP CRASH DIAGNOSTICS
                if (NotifyOstRemaining)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OstRemaining)));
                // END TEMP CRASH DIAGNOSTICS
            }
        }

        internal void SetRemaining(string objective, string ost)
        {
            ObjectiveRemaining = objective;
            OstRemaining = ost;
        }
    }
}
