using System;

namespace BugfixesAndQoL.UnitCommands
{
    // A value scope: idle runs allocate nothing, and nested/concurrent callers cannot
    // overwrite another thread's native-frame proof.
    internal static class FormationNativeRunContext
    {
        internal struct State
        {
            internal object Owner;
            internal long Generation;
            internal bool RenderEntered;
        }

        [ThreadStatic] private static State current;
        internal static State Enter(object owner, long generation)
        {
            State previous = current;
            current = new State { Owner = owner, Generation = generation };
            return previous;
        }
        internal static void Restore(State previous) => current = previous;
        internal static bool IsActive(object owner) => ReferenceEquals(current.Owner, owner);
        internal static long Generation(object owner) => IsActive(owner) ? current.Generation : 0;
        internal static void MarkRender(object owner) { if (IsActive(owner)) current.RenderEntered = true; }
        internal static bool HasRender(object owner) => IsActive(owner) && current.RenderEntered;
    }
}
