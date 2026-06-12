using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnitCosts
{
    public sealed class UnitCostsNotificationViewModel : INotifyPropertyChanged
    {
        private string message = string.Empty;
        private bool isVisible;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Message
        {
            get => message;
            private set
            {
                if (message == value)
                    return;

                message = value;
                OnPropertyChanged();
            }
        }

        public bool IsVisible
        {
            get => isVisible;
            private set
            {
                if (isVisible == value)
                    return;

                isVisible = value;
                OnPropertyChanged();
            }
        }

        public void Show(string text)
        {
            Message = text;
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
