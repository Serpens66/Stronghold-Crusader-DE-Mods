// Process-lifetime native hook which logically skips leading Vanilla instructions.
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal sealed class PermanentInstructionSkipPatch : IDisposable
    {
        internal readonly struct Site
        {
            internal Site(
                ulong address,
                int minimumHookSize,
                int expectedDisplacedByteCount,
                int skippedInstructionCount,
                string label)
            {
                Address = address;
                MinimumHookSize = minimumHookSize;
                ExpectedDisplacedByteCount = expectedDisplacedByteCount;
                SkippedInstructionCount = skippedInstructionCount;
                Label = label ?? throw new ArgumentNullException(nameof(label));
            }

            internal ulong Address { get; }
            internal int MinimumHookSize { get; }
            internal int ExpectedDisplacedByteCount { get; }
            internal int SkippedInstructionCount { get; }
            internal string Label { get; }
        }

        private HookTransaction transaction;
        private readonly List<HookHandle<X64InlineHook>> handles =
            new List<HookHandle<X64InlineHook>>();
        private readonly IntPtr enabledFlag;
        private bool published;
        private bool disposed;

        internal PermanentInstructionSkipPatch(ScanRegion region, params Site[] sites)
        {
            if (sites == null || sites.Length == 0)
                throw new ArgumentException("At least one native skip site is required.", nameof(sites));

            enabledFlag = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(enabledFlag, 0);
            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                ulong flagAddress = unchecked((ulong)enabledFlag.ToInt64());
                foreach (Site site in sites)
                {
                    if (site.Address == 0 || site.MinimumHookSize <= 0 ||
                        site.ExpectedDisplacedByteCount < site.MinimumHookSize ||
                        site.SkippedInstructionCount <= 0)
                    {
                        throw new ArgumentException($"Invalid permanent native skip site '{site.Label}'.");
                    }

                    var handle = new HookHandle<X64InlineHook>();
                    handles.Add(handle);
                    transaction.AddInline(
                        handle,
                        HookTarget.FromAddress(site.Address),
                        (assembler, instructions, returnAddress) =>
                            GenerateConditionalReplay(
                                assembler,
                                instructions,
                                flagAddress,
                                site.SkippedInstructionCount,
                                site.Label),
                        hookSize: site.MinimumHookSize);
                }

                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess)
                    throw new InvalidOperationException($"Permanent native skip hooks were not installed: {result}.");

                for (int index = 0; index < sites.Length; index++)
                {
                    HookHandle<X64InlineHook> handle = handles[index];
                    if (!handle.Success || !handle.IsInstalled ||
                        handle.Hook.DisplacedByteCount != sites[index].ExpectedDisplacedByteCount)
                    {
                        throw new InvalidOperationException(
                            $"Permanent native skip hook '{sites[index].Label}' displaced " +
                            $"{handle.Hook?.DisplacedByteCount ?? -1} bytes; expected " +
                            $"{sites[index].ExpectedDisplacedByteCount}.");
                    }
                }

                published = true;
            }
            catch
            {
                transaction?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
                throw;
            }
        }

        internal bool IsInstalled
        {
            get
            {
                foreach (HookHandle<X64InlineHook> handle in handles)
                {
                    if (!handle.Success || !handle.IsInstalled)
                        return false;
                }
                return true;
            }
        }

        internal void SetEnabled(bool enabled)
        {
            if (disposed)
                return;
            if (!IsInstalled)
                throw new InvalidOperationException("A permanent native skip hook is no longer installed.");

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
            if (!published)
            {
                transaction?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
            }
        }

        private static void GenerateConditionalReplay(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong enabledFlagAddress,
            int skippedInstructionCount,
            string label)
        {
            if (overwrittenInstructions.Length <= skippedInstructionCount)
            {
                throw new InvalidOperationException(
                    $"Permanent native skip hook '{label}' has no fallthrough instructions to replay.");
            }

            Label vanilla = assembler.CreateLabel(label + "Vanilla");
            Label done = assembler.CreateLabel(label + "Done");

            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();
            for (int index = skippedInstructionCount; index < overwrittenInstructions.Length; index++)
                assembler.AddInstruction(overwrittenInstructions[index]);
            assembler.jmp(done);

            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);

            assembler.Label(ref done);
            assembler.nop();
        }
    }
}
