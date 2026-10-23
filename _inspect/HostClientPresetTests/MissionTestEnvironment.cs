using APIShared.GameModes;
// Managed/native input stand-ins. Policy, state machine and adapter are the actual production sources.
internal static class EngineInterface
{
    internal struct LoadMapReturnData
    {
        internal int game_type, skirmishGameType, skirmishTrail, coopTrailID;
    }
}
namespace APIShared
{
    public sealed class NativeCapabilityDiagnostic { }
    internal static class MissionLifecycleService
    {
        internal static APIShared.GameModes.GameModeSnapshot Snapshot;
        internal static bool HasContext => Snapshot.Kind != APIShared.GameModes.GameModeKind.Unknown;
    }
}

namespace SHCDESE.NoesisUtil
{
    // Test-only ICommand boundary, like the other game/Noesis stand-ins above.
    // The real preset methods supply the action and predicate. This suite tests
    // their effects/authority, not the Extender's command implementation, and must
    // run in an isolated consumer build without the Extender source checkout.
    public sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly System.Action execute;
        private readonly System.Func<bool> canExecute;
        public RelayCommand(System.Action execute, System.Func<bool> canExecute = null)
        {
            this.execute = execute ?? throw new System.ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => canExecute == null || canExecute();
        public void Execute(object parameter) => execute();
        public event System.EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
    }
}
