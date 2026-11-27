using SHCDESE.API.Components.Network;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLViewModel
    {
        private bool enableTrappedUnitRescue = true;
        public string EnableTrappedUnitRescueText => SerpLocalization.Get("BugfixesAndQoL.EnableTrappedUnitRescue");
        public string EnableTrappedUnitRescueHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableTrappedUnitRescueHelp");

        [SyncHostOnly]
        public bool EnableTrappedUnitRescue
        {
            get => enableTrappedUnitRescue;
            set => SetSetting(ref enableTrappedUnitRescue, value, nameof(EnableTrappedUnitRescue));
        }
    }
}