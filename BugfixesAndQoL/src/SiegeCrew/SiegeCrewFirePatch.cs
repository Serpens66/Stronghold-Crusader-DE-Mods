using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static BugfixesAndQoL.SiegeCrew.NativeContract;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.SiegeCrew
{
    internal static class SiegeCrewFirePatch
    {
        internal const uint UnitManagerRva = 0x67E8400;
        internal static readonly Site FireSite =
            new Site("fatal unit fire damage", 0x1925DB, "B8040000006689BB24090000668983E4060000", "418D41F7", rbx);

        // These offsets are relative to manager + one-based unit ID * UnitStride.
        private const int UnitStride = 0x490;
        private const int UnitCapacity = 10000;
        private const int Life = 0x6E4;
        private const int Type = 0x6E6;
        private const int Owner = 0x6EE;
        private const int GlobalId = 0x6F0;
        private const int DeathMarker = 0x8F8;
        private const int State = 0x918;
        private const int CrewIds = 0x96C;
        private const int CrewGlobalIds = 0x974;
        private const int CrewCount = 0xA0C;
        private const int Dismissal = 0xA48;

        private const int Bound = 5;
        private const int TransitioningToBound = (Bound << 16) | 0x6D;

        internal static void Emit(Assembler assembler, Site site, ulong activation, ulong manager, ReadOnlySpan<Instruction> original)
        {
            Label restoreGate = assembler.CreateLabel();
            Label done = assembler.CreateLabel();
            Label oneSeat = assembler.CreateLabel();
            Label twoSeats = assembler.CreateLabel();
            Label threeSeats = assembler.CreateLabel();
            Label fourSeats = assembler.CreateLabel();
            Label checkCrew = assembler.CreateLabel();
            Label loop = assembler.CreateLabel();
            Label next = assembler.CreateLabel();
            Label remove = assembler.CreateLabel();

            AssemblerRegister64 device = site.Manager;
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, activation);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(restoreGate);

            assembler.push(rcx);
            assembler.push(rdx);
            assembler.push(r8);
            assembler.push(r10);
            assembler.push(r11);
            assembler.cmp(__word_ptr[device + Dismissal], 0);
            assembler.jne(done);

            // R9D is the zero-extended device type. EAX carries the audited seat limit.
            CompareType(assembler, eChimps.CHIMP_TYPE_PORTABLE_SHIELD, oneSeat);
            CompareType(assembler, eChimps.CHIMP_TYPE_CATAPULT, twoSeats);
            CompareType(assembler, eChimps.CHIMP_TYPE_MANGONEL, twoSeats);
            CompareType(assembler, eChimps.CHIMP_TYPE_BALLISTA, twoSeats);
            CompareType(assembler, eChimps.CHIMP_TYPE_ARAB_BALLISTA, twoSeats);
            CompareType(assembler, eChimps.CHIMP_TYPE_TREBUCHET, threeSeats);
            CompareType(assembler, eChimps.CHIMP_TYPE_SIEGE_TOWER, fourSeats);
            CompareType(assembler, eChimps.CHIMP_TYPE_BATTERING_RAM, fourSeats);
            assembler.jmp(done);
            assembler.Label(ref oneSeat);
            assembler.mov(eax, 1);
            assembler.jmp(checkCrew);
            assembler.Label(ref twoSeats);
            assembler.mov(eax, 2);
            assembler.jmp(checkCrew);
            assembler.Label(ref threeSeats);
            assembler.mov(eax, 3);
            assembler.jmp(checkCrew);
            assembler.Label(ref fourSeats);
            assembler.mov(eax, 4);

            assembler.Label(ref checkCrew);
            assembler.movzx(ecx, __word_ptr[device + CrewCount]);
            assembler.test(ecx, ecx);
            assembler.je(done);
            assembler.cmp(ecx, eax);
            assembler.ja(done);
            assembler.mov(r10, manager);
            assembler.xor(r8d, r8d);

            assembler.Label(ref loop);
            assembler.movzx(eax, __word_ptr[device + r8 * 2 + CrewIds]);
            assembler.test(eax, eax);
            assembler.je(next);
            assembler.cmp(eax, UnitCapacity);
            assembler.jae(next);
            assembler.cmp(eax, __dword_ptr[r10]);
            assembler.jae(next);
            assembler.mov(edx, __dword_ptr[device + r8 * 4 + CrewGlobalIds]);
            assembler.test(edx, edx);
            assembler.je(next);

            assembler.imul(r11, rax, UnitStride);
            assembler.add(r11, r10);
            assembler.cmp(__dword_ptr[r11 + GlobalId], edx);
            assembler.jne(next);
            assembler.cmp(__word_ptr[r11 + Type], (int)eChimps.CHIMP_TYPE_ENGINEER);
            assembler.jne(next);
            assembler.cmp(__word_ptr[r11 + Life], (int)AliveState.IsAlive);
            assembler.jne(next);
            assembler.cmp(__word_ptr[r11 + DeathMarker], 0);
            assembler.jne(next);
            assembler.movzx(edx, __word_ptr[device + Owner]);
            assembler.cmp(__word_ptr[r11 + Owner], dx);
            assembler.jne(next);
            assembler.cmp(__word_ptr[r11 + State], Bound);
            assembler.je(remove);
            assembler.cmp(__dword_ptr[r11 + State], TransitioningToBound);
            assembler.jne(next);

            assembler.Label(ref remove);
            assembler.mov(__word_ptr[r11 + Life], (int)AliveState.MarkedForDeletion);
            assembler.Label(ref next);
            assembler.inc(r8d);
            assembler.cmp(r8d, ecx);
            assembler.jb(loop);

            assembler.Label(ref done);
            assembler.pop(r11);
            assembler.pop(r10);
            assembler.pop(r8);
            assembler.pop(rdx);
            assembler.pop(rcx);
            assembler.Label(ref restoreGate);
            assembler.pop(rax);
            assembler.popfq();
            assembler.AddInstructions(original);
        }

        private static void CompareType(Assembler assembler, eChimps type, Label match)
        {
            assembler.cmp(r9d, (int)type);
            assembler.je(match);
        }
    }
}
