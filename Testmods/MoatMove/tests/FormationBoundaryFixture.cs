using System;

namespace MoatMove
{
    // Optional formation-provider boundary for the extracted traversal runtime.
    // Productive formation behavior is exercised separately by Formations.Tests.
    internal sealed class FormationRuntime
    {
        internal Func<IntPtr, int, int, int, bool> Standard;
        internal Func<IntPtr, int, int, int, int?> Assassin;
        internal bool Disabled;
        internal long CommonGroupMoveHook(IntPtr manager, int tribeId, short x, short y, short patrol, int newOrder, Func<long> original) => original();
        internal bool TryChooseStandardFormationSlot(IntPtr manager, int spacing, int x, int y) =>
            !Disabled && Standard != null && Standard(manager, spacing, x, y);
        internal bool TryChooseAssassinFormationSlot(IntPtr manager, int spacing, int x, int y, out int tileId)
        {
            int? result = Disabled ? null : Assassin?.Invoke(manager, spacing, x, y);
            tileId = result ?? 0;
            return result.HasValue;
        }
        internal void DisableForProcess(string stage, Exception error) => Disabled = true;
    }
}
