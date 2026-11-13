namespace Pathfinding
{
    public sealed partial class PathfindingViewModel
    {
        // Presentation dependencies preserve the saved child values. Host/client permissions
        // still apply through the enclosing settings panel and the preset system.
        public bool CoreOptionsEnabled => EnablePathfindingCore;
        public bool FireOptionsEnabled => CoreOptionsEnabled && AvoidFire;
        public bool PlagueOptionsEnabled => CoreOptionsEnabled && AvoidPlague;
        public bool PlagueRaisedOptionsEnabled => PlagueOptionsEnabled && EscapePlague;
        public bool DetourSettingsEnabled => CoreOptionsEnabled && (AvoidFire || AvoidPlague);

        private void NotifyDependentSettingsChanged(string name)
        {
            if (name == nameof(EnablePathfindingCore)) OnPropertyChanged(nameof(CoreOptionsEnabled));
            if (name == nameof(EnablePathfindingCore) || name == nameof(AvoidFire)) OnPropertyChanged(nameof(FireOptionsEnabled));
            if (name == nameof(EnablePathfindingCore) || name == nameof(AvoidPlague)) OnPropertyChanged(nameof(PlagueOptionsEnabled));
            if (name == nameof(EnablePathfindingCore) || name == nameof(AvoidPlague) || name == nameof(EscapePlague))
                OnPropertyChanged(nameof(PlagueRaisedOptionsEnabled));
            if (name == nameof(EnablePathfindingCore) || name == nameof(AvoidFire) || name == nameof(AvoidPlague))
                OnPropertyChanged(nameof(DetourSettingsEnabled));
        }
    }
}
