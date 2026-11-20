using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class KeepFlagRotationNative
    {
        internal const int SpanLength = 15;
        internal static readonly byte[][] OriginalBytes =
        {
            new byte[] { 0x8B, 0x05, 0x8C, 0x78, 0x01, 0x06, 0x45, 0x03, 0xC4, 0x41, 0x8B, 0xCD, 0x83, 0xF8, 0x02 },
            new byte[] { 0x8B, 0x05, 0x18, 0x75, 0x01, 0x06, 0x45, 0x33, 0xE4, 0x41, 0xB9, 0x07, 0x00, 0x00, 0x00 }
        };

        internal static void Emit(Assembler asm, ReadOnlySpan<Instruction> original, ulong state, ulong mark)
        {
            var vanilla = asm.CreateLabel();
            var rest = asm.CreateLabel();
            asm.pushfq();
            // Both original spans overwrite EAX. RAX can address the gate without a spill.
            asm.mov(rax, state);
            asm.cmp(__dword_ptr[rax], 0);
            asm.je(vanilla);

            // The next call in both audited branches is the main Flag3 allocator. Its pre-event
            // consumes this thread-local provenance before the engine links the record to a tile.
            asm.X64FastcallSafe(mark, 1, a => a.mov(ecx, esi), preserveRAX: false, preserveXMM: true);
            asm.mov(eax, 0);
            asm.jmp(rest);

            asm.Label(ref vanilla);
            asm.mov(rax, original[0].IPRelativeMemoryAddress);
            asm.mov(eax, __dword_ptr[rax]);
            asm.Label(ref rest);
            asm.popfq();
            asm.AddInstructions(original.Slice(1));
        }
    }
}
