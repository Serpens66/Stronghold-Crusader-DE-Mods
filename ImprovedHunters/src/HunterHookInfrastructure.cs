using System.Collections.Generic;
using APIShared.NativeSites;
using BepInEx.Logging;
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
        private static readonly List<(ulong Address, HookHandle<X64InlineHook> Handle)> nativeSites =
            new List<(ulong, HookHandle<X64InlineHook>)>();

        /// <summary>Remembers a hook so its span can be announced in APIShared's native-site registry.</summary>
        public static void TrackNativeSite(ulong address, HookHandle<X64InlineHook> handle) =>
            nativeSites.Add((address, handle));

        /// <summary>
        /// Announces all installed hooks to other mods. The hooks are already live and stay installed
        /// (they are process-lived); a conflict is reported, not resolved.
        /// </summary>
        public static void PublishNativeSites(ulong imageBase, ManualLogSource log)
        {
            foreach ((ulong address, HookHandle<X64InlineHook> handle) in nativeSites)
            {
                if (!handle.Success || !handle.IsInstalled)
                    continue;

                int rva = checked((int)(address - imageBase));
                NativeSiteRegistrationResult result = NativeSiteRegistry.Register(
                    ImprovedHuntersPlugin.PluginGuid,
                    $"hunter-hook-0x{rva:X}",
                    rva,
                    handle.Hook.DisplacedByteCount,
                    NativeSiteMode.Exclusive);
                if (result.Status != NativeSiteRegistrationStatus.Accepted &&
                    result.Status != NativeSiteRegistrationStatus.AlreadyRegistered)
                {
                    Shared.DebugLogHelper.LogWarning(log, $"Improved Hunters native site 0x{rva:X} is not exclusive: {result.Status}.");
                }
            }

            nativeSites.Clear();
        }

        public static HookTransaction CreateOwnedTransaction(ScanRegion region) =>
            new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    // Committed hooks are process-lifetime objects. Dispose paths only
                    // deactivate logical feature state; this ownership is never released.
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
            TrackNativeSite(address, handle);
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
