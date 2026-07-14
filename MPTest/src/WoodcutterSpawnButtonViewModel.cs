using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace MPTest
{
    internal sealed class WoodcutterSpawnButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility buttonVisibility = Visibility.Hidden;

        public WoodcutterSpawnButtonViewModel(Action spawn)
        {
            SpawnCommand = new RelayCommand(spawn ?? throw new ArgumentNullException(nameof(spawn)));
        }

        public RelayCommand SpawnCommand { get; }

        public Visibility ButtonVisibility
        {
            get => buttonVisibility;
            private set
            {
                if (buttonVisibility == value)
                    return;

                buttonVisibility = value;
                OnPropertyChanged(nameof(ButtonVisibility));
            }
        }

        public void SetVisible(bool visible)
        {
            ButtonVisibility = visible ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
