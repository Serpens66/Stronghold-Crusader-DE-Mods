using System;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
namespace EnemyGatePathfindingTest
{
    internal static class AssassinGateDescentAdapter
    {
        internal static void Emit(Assembler a, ReadOnlySpan<Instruction> original, ulong callback,
            ulong enabled, ulong continuation, ulong before, ulong after, ulong diagnostics, ulong module)
        {
            Label reject = a.CreateLabel();
            // Separate saved flags and R11. Callback returns a Boolean in its saved R11;
            // TEST runs after every managed call/stack cleanup. Both branches restore the originals.
            a.pushfq(); a.push(r11); a.mov(r11d, 0);
            NativeCallbackAdapterEmitter.Emit(a, ReadOnlySpan<Instruction>.Empty, callback, enabled);
            a.test(r11d, r11d); a.pop(r11); a.je(reject);
            a.popfq(); a.jmp(continuation);
            a.Label(ref reject); a.popfq();
            NativeCallbackAdapterEmitter.EmitExecution(a, original, before, after, diagnostics, module, false);
        }
    }
}
