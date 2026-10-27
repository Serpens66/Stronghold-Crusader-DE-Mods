using System;
using Microsoft.Extensions.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal static class BugfixesHookInfrastructure
    {
        internal static HookTransaction CreateOwnedTransaction(ScanRegion region, bool installOnCommit = true) =>
            new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    // Ownership is retained for process lifetime after publication. Disposal is
                    // reserved for rolling back an unpublished initialization candidate.
                    OwnsHooks = true,
                    InstallOnCommit = installOnCommit,
                    Backend = HookInstallPhase.Backend()
                });

        // See DemandGatedContextHook: hot-loop context hook behind a native demand byte.
        internal static X64InlineHook CreateDemandGatedContextHook(
            ulong address,
            int minimumHookSize,
            ContextHookDelegate callback,
            ContextHookOptions options,
            IntPtr demand,
            string name) =>
            DemandGatedContextHook.Create(address, minimumHookSize, callback, options, demand, name,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory.CreateLogger(name));

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
