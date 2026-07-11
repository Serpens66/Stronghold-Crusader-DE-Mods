using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;
using System.Threading;

namespace AIEconomyProtection
{
    public sealed class AIEconomyProtectionSettings : LobbyModSettingsBaseViewModel
    {
        private int preventAIPause = 1;
        private int preventEmergencyDemolition = 1;
        private int preventHovelDeletion = 1;

        public string PreventAIPauseText => "Prevent AI building pauses";
        public string PreventAIPauseHelpText => "Prevents AI-controlled players from putting their own production buildings to sleep.";
        public string PreventEmergencyDemolitionText => "Prevent AI panic demolition";
        public string PreventEmergencyDemolitionHelpText => "Skips the AI emergency resource-recovery demolition block, which can otherwise remove useful buildings under pressure.";
        public string PreventHovelDeletionText => "Prevent AI hovel deletion";
        public string PreventHovelDeletionHelpText => "Blocks direct deletes of living AI-owned hovels while still allowing normal destruction by damage.";

        internal bool IsPauseProtectionEnabled => Volatile.Read(ref preventAIPause) != 0;
        internal bool IsEmergencyDemolitionProtectionEnabled => Volatile.Read(ref preventEmergencyDemolition) != 0;
        internal bool IsHovelDeletionProtectionEnabled => Volatile.Read(ref preventHovelDeletion) != 0;

        [SyncHostOnly]
        public bool PreventAIPause
        {
            get => IsPauseProtectionEnabled;
            set => SetFlag(ref preventAIPause, value, nameof(PreventAIPause));
        }

        [SyncHostOnly]
        public bool PreventEmergencyDemolition
        {
            get => IsEmergencyDemolitionProtectionEnabled;
            set => SetFlag(ref preventEmergencyDemolition, value, nameof(PreventEmergencyDemolition));
        }

        [SyncHostOnly]
        public bool PreventHovelDeletion
        {
            get => IsHovelDeletionProtectionEnabled;
            set => SetFlag(ref preventHovelDeletion, value, nameof(PreventHovelDeletion));
        }

        private void SetFlag(ref int storage, bool value, string propertyName)
        {
            int newValue = value ? 1 : 0;
            if (Volatile.Read(ref storage) == newValue)
                return;

            Volatile.Write(ref storage, newValue);
            OnPropertyChanged(propertyName);
        }
    }
}
