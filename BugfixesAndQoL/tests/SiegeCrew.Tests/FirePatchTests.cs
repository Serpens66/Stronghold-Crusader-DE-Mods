using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BugfixesAndQoL.SiegeCrew;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;
using static BugfixesAndQoL.SiegeCrew.NativeContract;

internal static partial class Program
{
    private const int FireFixtureSize = 0x4000;
    private const int LifeOffset = 0x6E4;
    private const int TypeOffset = 0x6E6;
    private const int OwnerOffset = 0x6EE;
    private const int GlobalOffset = 0x6F0;
    private const int DeathOffset = 0x8F8;
    private const int PendingStateOffset = 0x924;
    private const int CrewIdsOffset = 0x96C;
    private const int CrewGlobalsOffset = 0x974;
    private const int CrewCountOffset = 0xA0C;
    private const int DismissalOffset = 0xA48;
    private const int DeviceId = 1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong RunFireStub(IntPtr device, int type, IntPtr output);

    private static void VerifyFirePatch(byte[] image, IntPtr activation)
    {
        var site = SiegeCrewFirePatch.FireSite;
        int offset = CodeOffset(image, site.Rva, site.Original.Length + site.Continuation.Length);
        byte[] original = image.AsSpan(offset, site.Original.Length).ToArray();
        byte[] next = image.AsSpan(offset + original.Length, site.Continuation.Length).ToArray();
        site.Verify(original, next, original);
        byte[] occupied = (byte[])original.Clone();
        occupied[0] ^= 1;
        Reject(() => site.Verify(original, next, occupied), "fire site rejects live overlap");
        Reject(() => site.Verify(original, new byte[next.Length], original), "fire site rejects a mismatched continuation");

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(original), 0x180000000UL + (uint)site.Rva);
        Instruction[] moves = { decoder.Decode(), decoder.Decode(), decoder.Decode() };
        Check(moves.All(move => move.Mnemonic == Mnemonic.Mov) && moves.Sum(move => move.Length) == 19 &&
              moves[1].MemoryBase == Register.RBX && moves[1].MemoryDisplacement64 == PendingStateOffset &&
              moves[2].MemoryBase == Register.RBX && moves[2].MemoryDisplacement64 == LifeOffset,
              "fire span contains three complete MOVs and targets the device record");

        var devices = new[]
        {
            (eChimps.CHIMP_TYPE_CATAPULT, 2), (eChimps.CHIMP_TYPE_TREBUCHET, 3),
            (eChimps.CHIMP_TYPE_MANGONEL, 2), (eChimps.CHIMP_TYPE_SIEGE_TOWER, 4),
            (eChimps.CHIMP_TYPE_BATTERING_RAM, 4), (eChimps.CHIMP_TYPE_PORTABLE_SHIELD, 1),
            (eChimps.CHIMP_TYPE_BALLISTA, 2), (eChimps.CHIMP_TYPE_ARAB_BALLISTA, 2)
        };

        SetActivation(activation, true, true);
        foreach (var (type, seats) in devices)
        {
            ExecuteFire(type, seats, activation, moves, null, Enumerable.Range(2, seats).ToArray());
            ExecuteFire(type, 0, activation, moves, null);
            ExecuteFire(type, seats + 1, activation, moves, null);
        }

        eChimps ram = eChimps.CHIMP_TYPE_BATTERING_RAM;
        ExecuteFire(ram, 4, activation, moves, data =>
        {
            Write32(data, 2, StateOffset, 0x0005006D);
            Write32(data, 3, StateOffset, 0x0000006D);
            Write32(data, 4, StateOffset, 0x0007006D);
            Write32(data, 5, StateOffset, 0);
        }, 2);
        ExecuteFire(ram, 4, activation, moves, data =>
        {
            Write16(data, DeviceId, CrewIdsOffset, 0);
            Write16(data, DeviceId, CrewIdsOffset + 2, 10000);
            Write16(data, DeviceId, CrewIdsOffset + 4, 6);
        }, 5);
        ExecuteFire(ram, 4, activation, moves, data =>
        {
            Write32(data, 2, GlobalOffset, 42);
            Write32(data, DeviceId, CrewGlobalsOffset + 4, 0);
            Write16(data, 4, OwnerOffset, 2);
        }, 5);
        ExecuteFire(ram, 4, activation, moves, data =>
        {
            Write16(data, 2, TypeOffset, (int)eChimps.CHIMP_TYPE_PEASANT);
            Write16(data, 3, LifeOffset, (int)AliveState.MarkedForDeletion);
            Write16(data, 4, DeathOffset, 1);
        }, 5);
        ExecuteFire(ram, 4, activation, moves, data => Write16(data, DeviceId, DismissalOffset, 1));
        ExecuteFire(eChimps.CHIMP_SIEGE_TENT, 4, activation, moves, null);
        ExecuteFire(eChimps.CHIMP_TYPE_PEASANT, 4, activation, moves, null);
        SetActivation(activation, true, false);
        ExecuteFire(ram, 4, activation, moves, null);
    }

    private static void ExecuteFire(eChimps type, int crewCount, IntPtr activation, Instruction[] original,
        Action<byte[]> alter, params int[] casualties)
    {
        IntPtr manager = Marshal.AllocHGlobal(FireFixtureSize);
        IntPtr output = Marshal.AllocHGlobal(10 * sizeof(long));
        IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
        try
        {
            Check(code != IntPtr.Zero, "fire fixture allocation");
            byte[] expected = new byte[FireFixtureSize];
            Write32(expected, 0, 0, 6); // Slots 1..5 are in range.
            Write16(expected, DeviceId, LifeOffset, (int)AliveState.IsAlive);
            Write16(expected, DeviceId, TypeOffset, (int)type);
            Write16(expected, DeviceId, OwnerOffset, 1);
            Write16(expected, DeviceId, PendingStateOffset, 0x55AA);
            Write16(expected, DeviceId, CrewCountOffset, crewCount);
            for (int seat = 0; seat < 4; seat++)
            {
                int id = seat + 2;
                Write16(expected, DeviceId, CrewIdsOffset + seat * 2, id);
                Write32(expected, DeviceId, CrewGlobalsOffset + seat * 4, 900 + id);
                Write16(expected, id, LifeOffset, (int)AliveState.IsAlive);
                Write16(expected, id, TypeOffset, (int)eChimps.CHIMP_TYPE_ENGINEER);
                Write16(expected, id, OwnerOffset, 1);
                Write32(expected, id, GlobalOffset, 900 + id);
                Write32(expected, id, StateOffset, 0x00070005);
            }
            alter?.Invoke(expected);
            Marshal.Copy(expected, 0, manager, expected.Length);

            var assembler = new Assembler(64);
            assembler.push(rbx);
            assembler.push(rdi);
            assembler.push(r12);
            assembler.push(r13);
            assembler.mov(rbx, rcx);
            assembler.mov(r9d, edx);
            assembler.mov(r12, r8);
            assembler.mov(r13, rsp);
            assembler.xor(edi, edi); // Native pending-state source.
            assembler.mov(rcx, 0x1234UL);
            assembler.mov(rdx, 0x5678UL);
            assembler.mov(r10, 0x9ABCUL);
            assembler.mov(r11, 0xDEF0UL);
            assembler.cmp(ecx, 0x1235); // CF and SF set, ZF clear.
            assembler.pushfq();
            assembler.pop(rax);
            assembler.mov(__qword_ptr[r12], rax);

            SiegeCrewFirePatch.Emit(assembler, SiegeCrewFirePatch.FireSite,
                unchecked((ulong)activation.ToInt64()), unchecked((ulong)manager.ToInt64()), original);

            assembler.mov(__qword_ptr[r12 + 8], rax);
            assembler.mov(__qword_ptr[r12 + 16], rcx);
            assembler.mov(__qword_ptr[r12 + 24], rdx);
            assembler.mov(__qword_ptr[r12 + 32], r8);
            assembler.mov(__qword_ptr[r12 + 40], r9);
            assembler.mov(__qword_ptr[r12 + 48], r10);
            assembler.mov(__qword_ptr[r12 + 56], r11);
            assembler.mov(__qword_ptr[r12 + 64], rsp);
            assembler.mov(__qword_ptr[r12 + 72], r13);
            assembler.pushfq();
            assembler.pop(rax);
            assembler.pop(r13);
            assembler.pop(r12);
            assembler.pop(rdi);
            assembler.pop(rbx);
            assembler.ret();

            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), unchecked((ulong)code.ToInt64()));
                byte[] payload = stream.ToArray();
                Marshal.Copy(payload, 0, code, payload.Length);
                Check(FlushInstructionCache(GetCurrentProcess(), code, (UIntPtr)payload.Length), "fire fixture instruction cache");
            }

            ulong flags = Marshal.GetDelegateForFunctionPointer<RunFireStub>(code)(IntPtr.Add(manager, UnitStride), (int)type, output);
            Check(unchecked((ulong)Marshal.ReadInt64(output)) == flags, type + " fire flags preserved");
            Check(Marshal.ReadInt64(output, 8) == 4 && Marshal.ReadInt64(output, 16) == 0x1234 &&
                Marshal.ReadInt64(output, 24) == 0x5678 && Marshal.ReadInt64(output, 32) == output.ToInt64() &&
                Marshal.ReadInt64(output, 40) == (int)type && Marshal.ReadInt64(output, 48) == 0x9ABC &&
                Marshal.ReadInt64(output, 56) == 0xDEF0, type + " fire continuation registers");
            Check(Marshal.ReadInt64(output, 64) == Marshal.ReadInt64(output, 72), type + " fire stack restored");
            Write16(expected, DeviceId, PendingStateOffset, 0);
            Write16(expected, DeviceId, LifeOffset, 4);
            foreach (int id in casualties)
                Write16(expected, id, LifeOffset, (int)AliveState.MarkedForDeletion);
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(manager, actual, 0, actual.Length);
            Check(expected.SequenceEqual(actual), type + " fire writes only to the device and confirmed crew");
        }
        finally
        {
            if (code != IntPtr.Zero)
                VirtualFree(code, UIntPtr.Zero, 0x8000);
            Marshal.FreeHGlobal(output);
            Marshal.FreeHGlobal(manager);
        }
    }

    private static void Write16(byte[] buffer, int id, int offset, int value) =>
        Array.Copy(BitConverter.GetBytes((ushort)value), 0, buffer, id * UnitStride + offset, 2);

    private static void Write32(byte[] buffer, int id, int offset, int value) =>
        Array.Copy(BitConverter.GetBytes(value), 0, buffer, id * UnitStride + offset, 4);
}
