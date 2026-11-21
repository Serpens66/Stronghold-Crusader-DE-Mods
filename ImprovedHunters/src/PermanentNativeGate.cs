using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ImprovedHunters
{
    internal delegate void NativeStubBody(Assembler assembler, ReadOnlySpan<Instruction> instructions);

    /// <summary>
    /// One atomic set of process-lifetime inline hooks sharing a logical enable flag.
    /// Once committed the code is never repatched; only the flag changes.
    /// </summary>
    internal sealed class PermanentNativeGate : IDisposable
    {
        private readonly ulong imageBase;
        private readonly string groupName;
        private readonly IntPtr enabledFlag;
        private readonly List<(HookHandle<X64InlineHook> Handle, int Rva, int Displaced)> sites =
            new List<(HookHandle<X64InlineHook>, int, int)>();
        private HookTransaction transaction;
        private bool published;
        private bool disposed;

        /// <param name="groupName">Owner-local name of the span group in APIShared's native-site registry.</param>
        internal PermanentNativeGate(ScanRegion region, ulong imageBase, string groupName)
        {
            this.imageBase = imageBase;
            this.groupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
            enabledFlag = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(enabledFlag, 0);
            transaction = HunterHookInfrastructure.CreateOwnedTransaction(region);
        }

        internal ulong FlagAddress => unchecked((ulong)enabledFlag.ToInt64());

        internal bool IsInstalled => published && !disposed;

        /// <param name="memory">The unmodified load image; signatures are validated against it.</param>
        internal unsafe void AddSite(ReadOnlySpan<byte> memory, int rva, int expectedDisplaced, NativeStubBody body)
        {
            // A live difference means a foreign hook overlaps this span.
            byte* live = (byte*)unchecked((long)(imageBase + (ulong)rva));
            for (int index = 0; index < expectedDisplaced; index++)
            {
                if (live[index] != memory[rva + index])
                    throw new InvalidOperationException($"A foreign hook overlaps the native span at RVA 0x{rva:X}.");
            }

            var handle = new HookHandle<X64InlineHook>();
            transaction.AddInline(
                handle,
                HookTarget.FromAddress(imageBase + (ulong)rva),
                (assembler, instructions, returnAddress) => body(assembler, instructions),
                hookSize: 14);
            sites.Add((handle, rva, expectedDisplaced));
            HunterHookInfrastructure.TrackNativeSite(transaction, imageBase + (ulong)rva, 14, handle);
        }

        // Commits through HunterHookInfrastructure (native-site claim and Shared.NativePatchGuard).
        // Not named Commit: the permanent-hook preflight reads any parameterless Commit call as an
        // unguarded HookTransaction commit.
        internal void Publish()
        {
            CommitResult commitResult = HunterHookInfrastructure.Commit(transaction, groupName);
            if (!commitResult.IsCompleteSuccess)
                throw new InvalidOperationException($"Native gate transaction failed: {commitResult}.");
            foreach ((HookHandle<X64InlineHook> handle, int rva, int displaced) in sites)
            {
                if (!handle.Success || !handle.IsInstalled || handle.Hook.DisplacedByteCount != displaced)
                {
                    throw new InvalidOperationException(
                        $"Native gate at RVA 0x{rva:X} displaced {handle.Hook?.DisplacedByteCount ?? -1} bytes; expected {displaced}.");
                }
            }

            published = true;
        }

        internal void SetEnabled(bool enabled)
        {
            if (!IsInstalled)
                return;

            Thread.MemoryBarrier();
            Marshal.WriteInt32(enabledFlag, enabled ? 1 : 0);
            Thread.MemoryBarrier();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            SetEnabled(false);
            disposed = true;
            // Published hooks and their flag stay valid until process exit.
            if (!published)
            {
                HunterHookInfrastructure.Forget(transaction);
                transaction?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
            }
        }
    }
}
