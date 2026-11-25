using SHCDESE.API.Components.Network;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLViewModel
    {
        private readonly LocalPerPlayerSetting<bool> enableAutoSend = new LocalPerPlayerSetting<bool>(true);
        private bool autoSendMissionLocked;

        public bool[] EnableAutoSendData => enableAutoSend.Data;
        public bool CanConfigureAutoSend => CanEditClientSettings && !autoSendMissionLocked;

        // Called by AutoSend presentation on the validated Unity thread, never by simulation.
        internal void SetAutoSendMissionLocked(bool locked)
        {
            if (autoSendMissionLocked == locked) return;
            autoSendMissionLocked = locked;
            OnPropertyChanged(nameof(CanConfigureAutoSend));
        }

        [SyncPerPlayer]
        public bool EnableAutoSend
        {
            get => enableAutoSend.Value;
            set
            {
                if (autoSendMissionLocked && !IsApplyingMissionSettingsSnapshot) return;
                if (!CanMutateSetting(nameof(EnableAutoSend)) || !enableAutoSend.SetValue(value))
                    return;
                SettingChanged?.Invoke(nameof(EnableAutoSend));
                OnPropertyChanged(nameof(EnableAutoSend));
            }
        }
    }
}
