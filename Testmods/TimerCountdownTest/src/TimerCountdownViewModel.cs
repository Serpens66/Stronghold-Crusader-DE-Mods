using System.ComponentModel;

namespace TimerCountdownTest
{
    internal sealed class TimerCountdownViewModel : INotifyPropertyChanged
    {
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ObjectiveRemaining)));
            }
        }

        public string OstRemaining
        {
            get { return ostRemaining; }
            private set
            {
                if (ostRemaining == value) return;
                ostRemaining = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OstRemaining)));
            }
        }

        internal void SetRemaining(string objective, string ost)
        {
            ObjectiveRemaining = objective;
            OstRemaining = ost;
        }
    }
}
