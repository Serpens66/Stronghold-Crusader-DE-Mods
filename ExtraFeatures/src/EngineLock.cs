using System;
using System.Reflection;
using System.Threading;

namespace ExtraFeatures
{
    // Vanilla's monitor around EngineInterface.run (DLL_RunTick), GameAction and saving.
    // Main-thread commands that change simulation-owned state take it like Vanilla's HUD
    // buttons. It is private in Assembly-CSharp, so it is resolved by its exact contract
    // (private static object) and callers fail closed when that contract changed.
    internal static class EngineLock
    {
        private static readonly object Missing = new object();
        private static object gate;

        // Throws when the contract changed. Feature initialization resolves this before it
        // publishes anything, so a missing monitor leaves the feature uninitialized.
        internal static object Gate
        {
            get
            {
                if (TryGetGate(out object current))
                    return current;
                throw new MissingFieldException("EngineInterface", "private static object threadLock");
            }
        }

        // For lifecycle paths that also run when initialization already failed closed:
        // without the monitor the feature never acquired native state to release.
        internal static bool TryGetGate(out object current)
        {
            current = Volatile.Read(ref gate);
            if (current == null)
                current = Resolve();
            if (ReferenceEquals(current, Missing))
            {
                current = null;
                return false;
            }
            return true;
        }

        private static object Resolve()
        {
            FieldInfo threadLockField = typeof(EngineInterface).GetField(
                "threadLock",
                BindingFlags.Static | BindingFlags.NonPublic);
            object resolved = threadLockField != null && threadLockField.FieldType == typeof(object)
                ? threadLockField.GetValue(null)
                : null;
            Volatile.Write(ref gate, resolved ?? Missing);
            return resolved ?? Missing;
        }
    }
}
