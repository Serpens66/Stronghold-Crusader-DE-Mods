using System;
using Shared;

namespace BugfixesAndQoL
{
    // One native resolution cache for every BugfixesAndQoL startup feature. With the audited reference
    // build it only confirms reference values; otherwise it reuses fallback results across starts.
    // LibraryLoaded saves it once at the end of the synchronous startup run.
    internal static class BfqolNativeCache
    {
        private static NativeResolutionCache current;

        internal static NativeResolutionCache Current
        {
            get
            {
                if (current != null) return current;
                string hash = DebugLogHelper.InstalledNativeSha256();
                bool reference = string.Equals(hash, DebugLogHelper.CurrentNativeSha256, StringComparison.OrdinalIgnoreCase);
                return current = NativeResolutionCache.Open(BugfixesAndQoLPlugin.PluginGuid,
                    BugfixesAndQoLPlugin.PluginVersion, hash, reference);
            }
        }

        internal static void Save() => current?.Save();
    }
}
