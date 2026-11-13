// Feature: One stateful troop action for mounting swordsmen or dismounting knights.
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace ExtraFeatures
{
    internal sealed class KnightDismountButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private readonly Action dismount, mount;
        private bool wantsVisibility, mountSelected, enabled;

        public KnightDismountButtonViewModel(Action dismount, Action mount)
        {
            this.dismount = dismount ?? throw new ArgumentNullException(nameof(dismount));
            this.mount = mount ?? throw new ArgumentNullException(nameof(mount));
            TransformCommand = new RelayCommand(Execute);
        }

        public RelayCommand TransformCommand { get; }
        public bool WantsVisibility => wantsVisibility;
        public bool MountSelected => mountSelected;
        public bool Enabled => enabled;

        private void Execute()
        {
            if (!wantsVisibility || !enabled) return;
            if (mountSelected) mount(); else dismount();
        }

        public void Hide() => SetState(false, mountSelected, false);
        public void ShowDismount(bool canExecute) => SetState(true, false, canExecute);
        public void ShowMount(bool canExecute) => SetState(true, true, canExecute);

        private void SetState(bool visible, bool mounting, bool canExecute)
        {
            if (wantsVisibility == visible && mountSelected == mounting && enabled == canExecute) return;
            wantsVisibility = visible;
            mountSelected = mounting;
            enabled = canExecute;
            // Publish one complete state; a subscriber never sees a mount icon with the old command.
            OnPropertyChanged(string.Empty);
        }
    }
}
