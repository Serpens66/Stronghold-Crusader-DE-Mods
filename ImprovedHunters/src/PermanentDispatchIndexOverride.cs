using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace ImprovedHunters
{
    internal sealed class PermanentDispatchIndexOverride : IDisposable
    {
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> hook = new HookHandle<X64InlineHook>();
        private readonly IntPtr enabledFlag;
        private bool published;
        private bool disposed;

        internal PermanentDispatchIndexOverride(
            ScanRegion region,
            ulong address,
            int expectedDisplacedBytes,
            int sourceType,
            byte replacementIndex,
            bool sourceTypeInR9)
        {
            enabledFlag = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(enabledFlag, 0);
            try
            {
                ulong flagAddress = unchecked((ulong)enabledFlag.ToInt64());
                transaction = HunterHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddInline(
                    hook,
                    HookTarget.FromAddress(address),
                    (assembler, instructions, returnAddress) => Generate(
                        assembler, instructions, flagAddress, sourceType,
                        replacementIndex, sourceTypeInR9),
                    hookSize: expectedDisplacedBytes);
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !hook.Success || !hook.IsInstalled ||
                    hook.Hook.DisplacedByteCount != expectedDisplacedBytes)
                {
                    throw new InvalidOperationException(
                        $"Permanent dispatch override displaced {hook.Hook?.DisplacedByteCount ?? -1} bytes; " +
                        $"expected {expectedDisplacedBytes}: {result}.");
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

        internal bool IsInstalled => hook.Success && hook.IsInstalled;

        internal void SetEnabled(bool enabled)
        {
            if (disposed) return;
            if (!IsInstalled) throw new InvalidOperationException("The permanent dispatch override is no longer installed.");
            Thread.MemoryBarrier();
            Marshal.WriteInt32(enabledFlag, enabled ? 1 : 0);
            Thread.MemoryBarrier();
        }

        public void Dispose()
        {
            if (disposed) return;
            SetEnabled(false);
            disposed = true;
            if (!published)
            {
                transaction?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
            }
        }

        private static void Generate(
            Assembler assembler,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            int sourceType,
            byte replacementIndex,
            bool sourceTypeInR9)
        {
            if (instructions.Length < 2)
                throw new InvalidOperationException("Unexpected dispatch-reader hook boundary.");
            Label vanilla = assembler.CreateLabel("dispatchOverrideVanilla");
            Label unchanged = assembler.CreateLabel("dispatchOverrideUnchanged");
            Label done = assembler.CreateLabel("dispatchOverrideDone");
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, flagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();
            assembler.AddInstruction(instructions[0]);
            if (sourceTypeInR9) assembler.cmp(r9d, sourceType);
            else assembler.cmp(r11d, sourceType);
            assembler.jne(unchanged);
            assembler.mov(eax, replacementIndex);
            assembler.Label(ref unchanged);
            for (int index = 1; index < instructions.Length; index++)
                assembler.AddInstruction(instructions[index]);
            assembler.jmp(done);
            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in instructions) assembler.AddInstruction(instruction);
            assembler.Label(ref done);
            assembler.nop();
        }
    }
}
