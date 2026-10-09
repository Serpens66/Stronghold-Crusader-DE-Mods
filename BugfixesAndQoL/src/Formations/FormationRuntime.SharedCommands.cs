using System;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;

namespace BugfixesAndQoL.UnitCommands
{
    // Keep mod-specific input, planning and packets in the mod. APIShared retains
    // the native interception points and calls this explicit integration boundary.
    internal sealed unsafe partial class FormationRuntime
    {
        bool IFormationCommandHandler.IsDispatching => IsDispatching;
        bool IFormationCommandHandler.TryChooseStandardFormationSlot(IntPtr manager, int spacing, int x, int y) =>
            TryChooseStandardFormationSlot(manager, spacing, x, y);
        bool IFormationCommandHandler.TryChooseAssassinFormationSlot(IntPtr manager, int spacing, int x, int y, out int tileId) =>
            TryChooseAssassinFormationSlot(manager, spacing, x, y, out tileId);
        long IFormationCommandHandler.CommonGroupMoveHook(IntPtr manager, int tribeId, short x, short y,
            short patrol, int newOrder, Func<long> original) =>
            CommonGroupMoveHook(manager, tribeId, x, y, patrol, newOrder, original);
        void IFormationCommandHandler.OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args) =>
            OnTribeIssueOrderMoveHere(args);
        void IFormationCommandHandler.OnUnitMoveHere(UnitMoveHereEventArgs args) => OnUnitMoveHere(args);
        void IFormationCommandHandler.ResetTransientState() => ResetTransientState();
        void IFormationCommandHandler.DisableForProcess(string contract, Exception exception) =>
            DisableForProcess(contract, exception);
    }
}
