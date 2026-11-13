using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.SiegeCrew
{
    internal static class AiCrewReadinessPatch
    {
        internal const int UnitManagerRva = 0x67E8400;
        internal const int TribeManagerRva = 0x7CC6720;
        internal const int UnitStride = 0x490;
        internal const int TribeStride = 0x688;
        internal const int MaximumUnits = 10000;
        internal const int MaximumTribes = 4500;
        internal const int RetryRva = 0x2F059;
        internal const int IndexInRangeRva = 0x2EEE5;
        // Native unit role, distinct from AITribeStorageRole16.HarassmentSiegeTentEngineers (165).
        internal const int HarassmentEngineerRole = 22;

        internal static readonly NativeContract.Site Site = new NativeContract.Site(
            "AI harassment crew readiness", 0x2EECC,
            "428B8C2BE4E6790333FF83F9087C0A", "8BCF", r13);

        internal static void Emit(Assembler a, ulong activation, ulong image)
        {
            Label vanilla = a.CreateLabel();
            Label failed = a.CreateLabel();
            Label accepted = a.CreateLabel();
            Label word = a.CreateLabel();
            Label member = a.CreateLabel();
            Label nextWord = a.CreateLabel();
            a.pushfq();
            a.push(rax);
            a.mov(rax, activation);
            a.cmp(__dword_ptr[rax], 0);
            a.je(vanilla);
            a.push(rcx);
            a.push(rdx);
            a.push(r8);
            a.push(r9);
            a.push(r10);
            a.push(r11);
            a.push(r12);
            a.push(r15);

            // The original valid-generation branch has R14 = RSI * TribeStride.
            a.test(esi, esi);
            a.jle(failed);
            a.cmp(esi, MaximumTribes);
            a.jge(failed);
            a.mov(r10, image + TribeManagerRva);
            a.add(r10, r14);
            a.cmp(__word_ptr[r10 + 0x40], (int)AliveState.IsAlive);
            a.jne(failed);
            a.cmp(__word_ptr[r10 + 0x2C], bp);
            a.jne(failed);
            a.mov(eax, __dword_ptr[r10 + 0x34]);
            a.test(eax, eax);
            a.je(failed);
            a.cmp(eax, __dword_ptr[r13 + rbx + 0x379FB5C]);
            a.jne(failed);
            a.movsx(r8d, __word_ptr[r10 + 0x5C]);
            a.cmp(r8d, 2);
            a.jl(failed);
            a.cmp(r8d, MaximumUnits);
            a.jge(failed);
            a.mov(r11, image + UnitManagerRva);
            a.mov(r15d, __dword_ptr[r11]);
            a.cmp(r15d, 2);
            a.jl(failed);
            a.cmp(r15d, MaximumUnits);
            a.jg(failed);
            a.xor(edx, edx);
            a.xor(r9d, r9d);

            // Read the native membership bitmap, including its tail, so stale bits
            // and an inconsistent member count cannot authorize deployment.
            a.Label(ref word);
            a.movzx(eax, __word_ptr[r10 + rdx * 2 + 0x60]);
            a.test(eax, eax);
            a.je(nextWord);
            a.Label(ref member);
            a.bsf(ecx, eax);
            a.btr(eax, ecx);
            a.mov(r12d, edx);
            a.shl(r12d, 4);
            a.add(r12d, ecx);
            a.test(r12d, r12d);
            a.jle(failed);
            a.cmp(r12d, r15d);
            a.jge(failed);
            a.imul(r12, r12, UnitStride);
            a.add(r12, r11);
            a.cmp(__word_ptr[r12 + 0x6E4], (int)AliveState.IsAlive);
            a.jne(failed);
            a.cmp(__word_ptr[r12 + 0x6E6], (int)eChimps.CHIMP_TYPE_ENGINEER);
            a.jne(failed);
            a.cmp(__word_ptr[r12 + 0x6EE], bp);
            a.jne(failed);
            a.cmp(__dword_ptr[r12 + 0x6F0], 0);
            a.je(failed);
            a.cmp(__word_ptr[r12 + 0x8F8], 0);
            a.jne(failed);
            a.cmp(__word_ptr[r12 + 0x930], si);
            a.jne(failed);
            a.mov(ecx, __dword_ptr[r10 + 0x34]);
            a.cmp(__dword_ptr[r12 + 0x93C], ecx);
            a.jne(failed);
            a.cmp(__word_ptr[r12 + 0xA82], HarassmentEngineerRole);
            a.jne(failed);
            a.cmp(__word_ptr[r12 + 0x9CA], 0);
            a.jne(failed);
            // Idle states 0..2 are safe. Moving/bound/building/exit transitions
            // remain under Vanilla until that member is ready again.
            a.cmp(__word_ptr[r12 + 0x918], 2);
            a.ja(failed);
            a.inc(r9d);
            a.cmp(r9d, r8d);
            a.jg(failed);
            a.test(eax, eax);
            a.jne(member);
            a.Label(ref nextWord);
            a.inc(edx);
            a.cmp(edx, MaximumUnits / 16);
            a.jl(word);
            a.cmp(r9d, r8d);
            a.je(accepted);

            a.Label(ref failed);
            Restore(a);
            a.pop(rax);
            a.popfq();
            a.AddUnrestrictedJmp(image + RetryRva);
            a.Label(ref accepted);
            Restore(a);
            a.Label(ref vanilla);
            a.pop(rax);
            a.popfq();
            // Replay all displaced instructions. JL leaves the owned span and
            // must target the original in-range block, including while dormant.
            a.mov(ecx, __dword_ptr[r13 + rbx + 0x379E6E4]);
            a.xor(edi, edi);
            a.cmp(ecx, 8);
            a.jl(image + IndexInRangeRva);
        }

        private static void Restore(Assembler a)
        {
            a.pop(r15);
            a.pop(r12);
            a.pop(r11);
            a.pop(r10);
            a.pop(r9);
            a.pop(r8);
            a.pop(rdx);
            a.pop(rcx);
        }
    }
}
