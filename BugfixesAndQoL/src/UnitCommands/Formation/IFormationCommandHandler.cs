using System;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;

namespace BugfixesAndQoL.UnitCommands
{
    // The permanent shared hook owner calls the mod-owned formation feature.
    // This is an internal friend integration, not a public formation API. Calls are
    // synchronous on the original publisher thread and retain existing Pre/Post order.
    // Returning false from a selector leaves its original native path to the owner;
    // the owner restores selector output and reports callback faults before fallback.
    // The handler is retained for the process lifetime; reset only clears logical state.
    internal interface IFormationCommandHandler
    {
        bool IsDispatching { get; }
        bool TryChooseStandardFormationSlot(IntPtr manager, int spacing, int x, int y);
        bool TryChooseAssassinFormationSlot(IntPtr manager, int spacing, int x, int y, out int tileId);
        long CommonGroupMoveHook(IntPtr manager, int tribeId, short x, short y,
            short patrol, int newOrder, Func<long> original);
        void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args);
        void OnUnitMoveHere(UnitMoveHereEventArgs args);
        void ResetTransientState();
        void DisableForProcess(string contract, Exception exception);
    }
}
