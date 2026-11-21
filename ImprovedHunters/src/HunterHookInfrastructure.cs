using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using APIShared.NativeSites;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;

namespace ImprovedHunters
{
    /// <summary>
    /// Keeps the RedBird ownership and context-capture contract identical for all
    /// independently disposable Improved Hunters hook features, and claims every span in
    /// APIShared's native-site registry before the hook group is written.
    /// </summary>
    internal static class HunterHookInfrastructure
    {
        // RedBird's inline hooks displace at least this many bytes, rounded up to whole instructions.
        private const int MinimumInlineHookBytes = 14;
        private const int DecodeLookahead = 64;

        private static readonly Dictionary<HookTransaction, List<PendingSite>> pendingSites =
            new Dictionary<HookTransaction, List<PendingSite>>();
        private static CrusaderLibraryLoadContext loadContext;
        private static ulong boundImageBase;

        /// <summary>Binds the unmodified load image used to measure and fingerprint hook spans.</summary>
        public static void Bind(CrusaderLibraryLoadContext context, ulong imageBase)
        {
            loadContext = context ?? throw new ArgumentNullException(nameof(context));
            boundImageBase = imageBase;
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
                    OwnsHooks = true,
                    // Commit below lets Shared.NativePatchGuard check the spans for foreign patches, then enable.
                    InstallOnCommit = false
                });

        /// <summary>Records an inline hook of <paramref name="transaction"/> for registration at commit.</summary>
        /// <param name="requestedHookSize">RedBird's minimum hook size; 0 selects the backend default.</param>
        public static void TrackNativeSite(
            HookTransaction transaction,
            ulong address,
            int requestedHookSize,
            HookHandle<X64InlineHook> handle)
        {
            if (!pendingSites.TryGetValue(transaction, out List<PendingSite> sites))
            {
                sites = new List<PendingSite>();
                pendingSites.Add(transaction, sites);
            }

            sites.Add(new PendingSite(address, requestedHookSize, handle));
        }

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
            TrackNativeSite(transaction, address, hookSize, handle);
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

        /// <summary>
        /// Claims all tracked spans as one exclusive group, then commits through Shared.NativePatchGuard
        /// (live spans checked against the load snapshot before Enable). A refusal or a foreign patch
        /// throws before any byte is written, so the caller's feature stays Vanilla. A failed commit releases the claim;
        /// a published group stays registered, also when a later post-commit check disables the feature.
        /// </summary>
        public static CommitResult Commit(HookTransaction transaction, string groupName)
        {
            if (loadContext == null)
                throw new InvalidOperationException("The Crusader load image is not bound for native-site registration.");
            if (!pendingSites.TryGetValue(transaction, out List<PendingSite> sites) || sites.Count == 0)
                throw new InvalidOperationException($"The hook group '{groupName}' has no tracked native sites.");
            pendingSites.Remove(transaction);

            ReadOnlySpan<byte> memory = loadContext.Memory;
            var spans = new NativeSiteSpan[sites.Count];
            var lengths = new int[sites.Count];
            for (int index = 0; index < sites.Count; index++)
            {
                int rva = checked((int)(sites[index].Address - boundImageBase));
                int length = MeasureDisplacedBytes(memory, rva, sites[index].RequestedHookSize);
                lengths[index] = length;
                spans[index] = new NativeSiteSpan(
                    rva,
                    length,
                    NativeSiteMode.Exclusive,
                    ComputeSha256(memory.Slice(rva, length)));
            }

            string name = "hunter-" + groupName;
            NativeSiteRegistrationResult claim = NativeSiteRegistry.Register(ImprovedHuntersPlugin.PluginGuid, name, spans);
            if (!claim.Succeeded)
                throw new InvalidOperationException($"APIShared refused the native sites of '{groupName}': {claim}.");

            CommitResult result;
            try
            {
                result = Shared.NativePatchGuard.Commit(transaction, groupName);
            }
            catch
            {
                NativeSiteRegistry.Unregister(ImprovedHuntersPlugin.PluginGuid, name);
                throw;
            }

            if (!result.IsCompleteSuccess)
            {
                NativeSiteRegistry.Unregister(ImprovedHuntersPlugin.PluginGuid, name);
                return result;
            }

            for (int index = 0; index < sites.Count; index++)
            {
                HookHandle<X64InlineHook> handle = sites[index].Handle;
                if (handle.Success && handle.IsInstalled && handle.Hook.DisplacedByteCount != lengths[index])
                {
                    throw new InvalidOperationException(
                        $"The hook group '{groupName}' displaced {handle.Hook.DisplacedByteCount} bytes at RVA " +
                        $"0x{spans[index].Rva:X}; the registered span has {lengths[index]}.");
                }
            }

            return result;
        }

        /// <summary>Releases tracked sites of a transaction that is disposed before its commit.</summary>
        public static void Forget(HookTransaction transaction)
        {
            if (transaction != null)
                pendingSites.Remove(transaction);
        }

        /// <summary>Whole instructions from the unmodified image until RedBird's minimum hook size is reached.</summary>
        internal static int MeasureDisplacedBytes(ReadOnlySpan<byte> memory, int rva, int requestedHookSize)
        {
            int minimum = Math.Max(requestedHookSize, MinimumInlineHookBytes);
            int available = Math.Min(DecodeLookahead, memory.Length - rva);
            if (rva < 0 || available < minimum)
                throw new InvalidOperationException($"The hook span at RVA 0x{rva:X} lies outside the load image.");

            var decoder = Decoder.Create(64, new ByteArrayCodeReader(memory.Slice(rva, available).ToArray()));
            int length = 0;
            while (length < minimum)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.IsInvalid || instruction.Length == 0)
                    throw new InvalidOperationException($"The hook span at RVA 0x{rva:X} does not decode.");
                length += instruction.Length;
                if (length > available)
                    throw new InvalidOperationException($"The hook span at RVA 0x{rva:X} exceeds the decode window.");
            }

            return length;
        }

        private static string ComputeSha256(ReadOnlySpan<byte> bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", string.Empty);
        }

        private readonly struct PendingSite
        {
            public PendingSite(ulong address, int requestedHookSize, HookHandle<X64InlineHook> handle)
            {
                Address = address;
                RequestedHookSize = requestedHookSize;
                Handle = handle;
            }

            public ulong Address { get; }
            public int RequestedHookSize { get; }
            public HookHandle<X64InlineHook> Handle { get; }
        }
    }
}
