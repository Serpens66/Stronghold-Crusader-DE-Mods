using Noesis;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BuildingLimit
{
    public sealed class BuildingLimitTooltipViewModel : INotifyPropertyChanged
    {
        private string limitText = string.Empty;
        private bool isVisible;

        public event PropertyChangedEventHandler PropertyChanged;

        public string LimitText
        {
            get => limitText;
            private set
            {
                if (limitText == value)
                    return;

                limitText = value;
                OnPropertyChanged();
            }
        }

        public Visibility LimitVisibility => isVisible ? Visibility.Visible : Visibility.Collapsed;

        public void Show(int count, int limit)
        {
            LimitText = "Limit: " + count + "/" + limit;
            SetVisible(true);
        }

        public void Clear()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (isVisible == visible)
                return;

            isVisible = visible;
            OnPropertyChanged(nameof(LimitVisibility));
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

