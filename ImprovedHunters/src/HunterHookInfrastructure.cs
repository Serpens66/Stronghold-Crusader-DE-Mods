using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;

namespace ImprovedHunters
{
    /// <summary>
    /// Keeps the RedBird ownership and context-capture contract identical for all
    /// independently disposable Improved Hunters hook features.
    /// </summary>
    internal static class HunterHookInfrastructure
    {
        public static HookTransaction CreateOwnedTransaction(ScanRegion region) =>
            new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    // These feature objects are disposed only on a real toggle/final teardown.
                    OwnsHooks = true
                });

        public static void AddContextHook(
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
