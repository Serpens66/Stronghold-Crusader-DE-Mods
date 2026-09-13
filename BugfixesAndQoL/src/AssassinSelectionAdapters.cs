using System;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate long SelectionResultDelegate(IntPtr selectionState, long originalResult);

        private SelectionResultDelegate selectionResultCallback;
        private readonly HookHandle<X64InlineHook>[] selectionCallHooks =
        {
            new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>(),
            new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>(),
            new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>()
        };

        private static readonly int[] SelectionCallRvas =
        {
            0x8D724, 0x8E2B8, 0x8E550, 0x8F325, 0xB7161, 0xB7321
        };

        private static readonly string[] SelectionCallBytes =
        {
            "E84791100085C07423468B84262C070000",
            "E8B3851000488D153C1DF7FF85C0",
            "E81B83100085C07423458B842C2C070000",
            "E846751000498BFE85C074348B15F12A9803",
            "E80AF70D004533F6B90100000085C0",
            "E84AF50D0085C0757E488BF333DB"
        };

        private void AddSelectionCallAdapters(
            HookTransaction transaction, ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            selectionResultCallback = ObserveCursorTilePairFallbackSelection;
            ulong callback = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                selectionResultCallback).ToInt64());

            for (int i = 0; i < SelectionCallRvas.Length; i++)
            {
                int rva = SelectionCallRvas[i];
                string hex = SelectionCallBytes[i];
                var bytes = new byte[hex.Length / 2];
                for (int j = 0; j < bytes.Length; j++)
                    bytes[j] = Convert.ToByte(hex.Substring(j * 2, 2), 16);

                ValidateExactBytes(
                    memory, rva, bytes, "SE 2.6 selection call and complete displaced span");
                int expectedLength = bytes.Length;
                transaction.AddInline(
                    selectionCallHooks[i],
                    HookTarget.FromAddress(libraryBase + (uint)rva),
                    (asm, original, returnAddress) =>
                    {
                        if (returnAddress != libraryBase + (uint)rva + (uint)expectedLength ||
                            original.Length == 0 || original[0].Code != Code.Call_rel32_64 ||
                            original[0].NearBranch64 != libraryBase + 0x196870)
                        {
                            throw new InvalidOperationException(
                                "SE selection call relocation contract changed.");
                        }

                        EmitSelectionCallAdapter(asm, original, callback);
                    },
                    hookSize: 14);
            }
        }

        private void ValidateSelectionCallAdapters(ulong libraryBase)
        {
            for (int i = 0; i < selectionCallHooks.Length; i++)
            {
                HookHandle<X64InlineHook> handle = selectionCallHooks[i];
                if (!handle.Success || !handle.IsInstalled || handle.Failure != null ||
                    handle.ResolvedAddress != libraryBase + (uint)SelectionCallRvas[i] ||
                    handle.Require().DisplacedByteCount != SelectionCallBytes[i].Length / 2)
                {
                    throw new InvalidOperationException(
                        "SE selection adapter installation mismatch.");
                }
            }
        }

        private static void EmitSelectionCallAdapter(
            Assembler asm, ReadOnlySpan<Instruction> original, ulong callback)
        {
            // Each site is immediately before a Win64 call: RSP is aligned and RCX
            // is the selection manager. No stack arguments or input flags exist.
            // Keep private storage outside both callees' 32-byte shadow space.
            asm.sub(rsp, 0xE0);
            asm.mov(__qword_ptr[rsp + 0xD0], rcx);
            asm.AddInstruction(original[0]);
            asm.mov(__qword_ptr[rsp + 0x20], rax);
            asm.mov(__qword_ptr[rsp + 0x28], rcx);
            asm.mov(__qword_ptr[rsp + 0x30], rdx);
            asm.mov(__qword_ptr[rsp + 0x38], r8);
            asm.mov(__qword_ptr[rsp + 0x40], r9);
            asm.mov(__qword_ptr[rsp + 0x48], r10);
            asm.mov(__qword_ptr[rsp + 0x50], r11);
            asm.movdqu(__xmmword_ptr[rsp + 0x60], xmm0);
            asm.movdqu(__xmmword_ptr[rsp + 0x70], xmm1);
            asm.movdqu(__xmmword_ptr[rsp + 0x80], xmm2);
            asm.movdqu(__xmmword_ptr[rsp + 0x90], xmm3);
            asm.movdqu(__xmmword_ptr[rsp + 0xA0], xmm4);
            asm.movdqu(__xmmword_ptr[rsp + 0xB0], xmm5);
            asm.pushfq();
            asm.pop(rax);
            asm.mov(__qword_ptr[rsp + 0xC0], rax);
            asm.mov(rcx, __qword_ptr[rsp + 0xD0]);
            asm.mov(rdx, __qword_ptr[rsp + 0x20]);
            asm.mov(rax, callback);
            asm.call(rax);
            asm.mov(__qword_ptr[rsp + 0x20], rax);
            asm.movdqu(xmm0, __xmmword_ptr[rsp + 0x60]);
            asm.movdqu(xmm1, __xmmword_ptr[rsp + 0x70]);
            asm.movdqu(xmm2, __xmmword_ptr[rsp + 0x80]);
            asm.movdqu(xmm3, __xmmword_ptr[rsp + 0x90]);
            asm.movdqu(xmm4, __xmmword_ptr[rsp + 0xA0]);
            asm.movdqu(xmm5, __xmmword_ptr[rsp + 0xB0]);
            asm.push(__qword_ptr[rsp + 0xC0]);
            asm.popfq();
            asm.mov(rcx, __qword_ptr[rsp + 0x28]);
            asm.mov(rdx, __qword_ptr[rsp + 0x30]);
            asm.mov(r8, __qword_ptr[rsp + 0x38]);
            asm.mov(r9, __qword_ptr[rsp + 0x40]);
            asm.mov(r10, __qword_ptr[rsp + 0x48]);
            asm.mov(r11, __qword_ptr[rsp + 0x50]);
            asm.mov(rax, __qword_ptr[rsp + 0x20]);
            asm.lea(rsp, __qword_ptr[rsp + 0xE0]);

            // Relocate all following instructions, including RIP loads and branches.
            // A taken original branch therefore observes the final selection result.
            for (int i = 1; i < original.Length; i++)
                asm.AddInstruction(original[i]);
        }
    }
}
