using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System.Threading;

namespace BugfixesAndQoL
{
    // Detour backend selection by installation phase (startup cost, BFQoLPerformace.md).
    // InitialLibraryLoad is true only while BugfixesAndQoLPlugin.OnCrusaderLibraryLoaded runs on the
    // Unity main thread: the native library has just loaded and no simulation, AI or pathing code
    // has run yet. In that window the same NativeX64 options are used without suspending all process
    // threads around every patch (RedBird ThreadPatchScope: no-GC region, system thread snapshot,
    // suspend/resume; measured ~60 ms per detour). The Script Extender installs its own detours the
    // same way at this point (DetourManager: SuspendThreadsDuringPatch = false), and BugfixesAndQoL
    // context hooks already write their patches directly. Detours installed later (e.g. a setting
    // change in a running game) keep the given backend including thread suspension; backends that
    // are not NativeX64 are never changed.
    internal static class HookInstallPhase
    {
        // Managed ID of the thread that opened the window (0 = closed). Another thread that installs
        // a hook while the window is open keeps thread suspension.
        private static int windowThread;

        internal static bool InitialLibraryLoad
        {
            get => Volatile.Read(ref windowThread) == Thread.CurrentThread.ManagedThreadId;
            set => Volatile.Write(ref windowThread, value ? Thread.CurrentThread.ManagedThreadId : 0);
        }

        internal static IHookBackend Backend(IHookBackend backend = null)
        {
            if (!InitialLibraryLoad)
                return backend;
            NativeDetourOptions options = backend == null
                ? NativeDetourBackend.Instance.Options
                : (backend as NativeDetourBackend)?.Options;
            if (options == null || !options.SuspendThreadsDuringPatch)
                return backend;
            return new NativeDetourBackend(options with { SuspendThreadsDuringPatch = false });
        }
    }
}
