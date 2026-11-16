using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    // The gate belongs to the demolition callee, never to the caller's emergency state machine.
    internal sealed class AIEmergencyDemolitionDispatch
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DemolitionDelegate(IntPtr manager, int playerId, int category, byte specialFlag);

        private readonly Func<bool> shouldSuppress;
        private readonly DemolitionDelegate original;
        private readonly Action<Exception> onError;

        internal AIEmergencyDemolitionDispatch(Func<bool> shouldSuppress, DemolitionDelegate original, Action<Exception> onError)
        {
            this.shouldSuppress = shouldSuppress ?? throw new ArgumentNullException(nameof(shouldSuppress));
            this.original = original ?? throw new ArgumentNullException(nameof(original));
            this.onError = onError;
        }

        internal void Invoke(IntPtr manager, int playerId, int category, byte specialFlag)
        {
            bool suppress = false;
            try { suppress = shouldSuppress(); }
            catch (Exception error)
            {
                try { onError?.Invoke(error); } catch { }
            }
            if (!suppress) original(manager, playerId, category, specialFlag);
        }
    }
}
