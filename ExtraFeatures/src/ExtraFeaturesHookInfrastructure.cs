using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using System.Threading;

namespace ExtraFeatures
{
    internal static class ExtraFeaturesHookInfrastructure
    {
        // Managed ID of the thread that opened the initial installation window (0 = closed).
        private static int initialLibraryLoadThread;

        // True only on the thread running ExtraFeaturesPlugin.OnCrusaderLibraryLoaded: the native
        // library has just loaded and no simulation, AI or pathing code runs yet. Detours created
        // then skip RedBird's per-patch suspension of all process threads (~55 ms each), like the
        // Script Extender and BugfixesAndQoL (HookInstallPhase) in the same phase. Inline and
        // context hooks never suspend. Later installations keep the suspending backend.
        internal static bool InitialLibraryLoad
        {
            get => Volatile.Read(ref initialLibraryLoadThread) == Thread.CurrentThread.ManagedThreadId;
            set => Volatile.Write(ref initialLibraryLoadThread, value ? Thread.CurrentThread.ManagedThreadId : 0);
        }

        // Same NativeX64 options without thread suspension inside the window; otherwise the given
        // backend (null = the transaction default). Other backends are never changed.
        internal static IHookBackend DetourBackend(IHookBackend backend = null)
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

        internal static HookTransaction CreateOwnedTransaction(ScanRegion region) =>
            new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    Backend = DetourBackend(),
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = true
                });

        internal static void AddContextHook(
            HookTransaction transaction,
            HookHandle<X64InlineHook> handle,
            ulong address,
            ContextHookDelegate callback,
            X64SmartCPUContextRegs registers,
            int hookSize = 0,
            CallbackErrorMode errorMode = CallbackErrorMode.LogAndContinue,
            OverwrittenInstructionPlacement placement = OverwrittenInstructionPlacement.AfterCallback)
        {
            transaction.AddContextHook(
                handle,
                HookTarget.FromAddress(address),
                callback,
                new ContextHookOptions
                {
                    Registers = registers,
                    HookSize = hookSize,
                    ErrorMode = errorMode,
                    Placement = placement
                });
        }
    }
}
