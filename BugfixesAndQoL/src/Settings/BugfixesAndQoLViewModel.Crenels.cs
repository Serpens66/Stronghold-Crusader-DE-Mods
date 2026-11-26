using SHCDESE.API.Components.Network;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLViewModel
    {
        private bool enableCrenelCheckerboard = true;
        private bool enableCrenelCheckerboardAI;

        public string EnableCrenelCheckerboardText => SerpLocalization.Get("BugfixesAndQoL.EnableCrenelCheckerboard");
        public string CrenelHumanText => SerpLocalization.Get("BugfixesAndQoL.CrenelHuman");
        public string CrenelAIText => SerpLocalization.Get("BugfixesAndQoL.CrenelAI");
        public string CrenelHumanSearchText => $"{EnableCrenelCheckerboardText} · {CrenelHumanText}";
        public string CrenelAISearchText => $"{EnableCrenelCheckerboardText} · {CrenelAIText}";
        public string EnableCrenelCheckerboardHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableCrenelCheckerboardHelp");
        public string EnableCrenelCheckerboardAIHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableCrenelCheckerboardAIHelp");

        public bool HasCrenelCheckerboardOwners => EnableCrenelCheckerboard || EnableCrenelCheckerboardAI;

        // Keep the original preset key for the Human checkbox.
        [SyncHostOnly]
        public bool EnableCrenelCheckerboard
        {
            get => enableCrenelCheckerboard;
            set => SetCrenelOwnerSetting(ref enableCrenelCheckerboard, value, nameof(EnableCrenelCheckerboard));
        }

        [SyncHostOnly]
        public bool EnableCrenelCheckerboardAI
        {
            get => enableCrenelCheckerboardAI;
            set => SetCrenelOwnerSetting(ref enableCrenelCheckerboardAI, value, nameof(EnableCrenelCheckerboardAI));
        }

        private void SetCrenelOwnerSetting(ref bool field, bool value, string propertyName)
        {
            bool hadOwners = HasCrenelCheckerboardOwners;
            SetSetting(ref field, value, propertyName);
            if (hadOwners != HasCrenelCheckerboardOwners)
                OnPropertyChanged(nameof(HasCrenelCheckerboardOwners));
        }
    }
}
