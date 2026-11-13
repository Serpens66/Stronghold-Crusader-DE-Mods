using System;
using System.Linq;
using System.Runtime.InteropServices;
using BugfixesAndQoL.SiegeCrew;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

internal static partial class Program
{
    private static void VerifyAutoBinding(byte[] file, IntPtr activation)
    {
        IntPtr image = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x210000, 0x3000, 0x40);
        IntPtr units = Marshal.AllocHGlobal(0x2000), output = Marshal.AllocHGlobal(160);
        try
        {
            Check(image != IntPtr.Zero, "auto crew executable fixture");
            for (int index = 0; index < AutoCrewBindingPatch.Sites.Length; index++)
            {
                var site = AutoCrewBindingPatch.Sites[index];
                int offset = CodeOffset(file, site.Rva, site.Original.Length + site.Continuation.Length);
                byte[] bytes = file.AsSpan(offset, site.Original.Length).ToArray();
                site.Verify(bytes, file.AsSpan(offset + bytes.Length, site.Continuation.Length), bytes);
                Reject(() => site.Verify(bytes, site.Continuation, new byte[bytes.Length]), "auto occupied site");
                Reject(() => site.Verify(bytes, new byte[site.Continuation.Length], bytes), "auto changed continuation");
                var d = Decoder.Create(64, new ByteArrayCodeReader(bytes), unchecked((ulong)image.ToInt64()) + (uint)site.Rva);
                Instruction[] original = { d.Decode(), d.Decode() };
                Check(original[0].Mnemonic == Mnemonic.Cmp && original[1].Mnemonic == Mnemonic.Jne &&
                    original.Sum(i => i.Length) == site.Original.Length &&
                    original[1].NearBranchTarget == unchecked((ulong)image.ToInt64()) + (uint)AutoCrewBindingPatch.RejectRvas[index], "auto original CMP/JNE contract");
                foreach (bool master in new[] { false, true })
                foreach (bool feature in new[] { false, true })
                foreach (int state in new[] { 0, 1, 2, 5, 7, 8, 101, 105, 0x0005006D, 0x0007006D, 0x0000006D })
                foreach (int recruiting in new[] { 0, 1 })
                {
                    bool blocked = master && feature && (recruiting != 0 || state == 5 || state == 7 || state == 0x5006D || state == 0x7006D);
                    NativeContract.SetActivation(activation, master, feature);
                    ExecuteAuto(site, index, original, activation, image, units, output, state, recruiting, true, blocked ? 1 : 0);
                }
                // Original type/command rejection remains effective, enabled or dormant.
                foreach (bool enabled in new[] { false, true })
                {
                    NativeContract.SetActivation(activation, true, enabled);
                    ExecuteAuto(site, index, original, activation, image, units, output, 0, 0, false, 1);
                }
            }
        }
        finally { Marshal.FreeHGlobal(units); Marshal.FreeHGlobal(output); if (image != IntPtr.Zero) VirtualFree(image, UIntPtr.Zero, 0x8000); }
    }

    private static void ExecuteAuto(NativeContract.Site site, int index, Instruction[] original, IntPtr activation,
        IntPtr image, IntPtr units, IntPtr output, int state, int recruiting, bool matchesOriginal, int expected)
    {
        Marshal.Copy(new byte[0x2000], 0, units, 0x2000);
        IntPtr unit = IntPtr.Add(units, 0x490);
        Marshal.WriteInt16(unit, 0x6E6, (short)(matchesOriginal ? eChimps.CHIMP_TYPE_ENGINEER : eChimps.CHIMP_TYPE_PEASANT));
        Marshal.WriteInt16(unit, 0x9F4, (short)(matchesOriginal ? TribeAICommand.ManSiegeEquipment : TribeAICommand.Unknown0));
        Marshal.WriteInt32(unit, 0x918, state); Marshal.WriteInt16(unit, 0x9CA, (short)recruiting);
        byte[] before = ReadNative(units, 0, 0x2000);
        ulong module = unchecked((ulong)image.ToInt64());
        const int entry = 0x200000, finish = 0x201000;
        foreach (var point in new[] { (site.End, 0), (AutoCrewBindingPatch.RejectRvas[index], 1) })
        {
            var marker = new Assembler(64); marker.mov(__dword_ptr[rsp + 8], point.Item2); marker.jmp(module + finish);
            WriteNativeCode(image, point.Item1, marker);
        }
        var a = new Assembler(64);
        foreach (var r in new[] { rbx, rbp, rsi, rdi, r12, r13, r14, r15 }) a.push(r);
        a.sub(rsp, 16); a.mov(__qword_ptr[rsp], rdx);
        a.mov(r8, unchecked((ulong)units.ToInt64())); a.mov(r9d, 0x490);
        a.mov(r11, unchecked((ulong)unit.ToInt64()) + (uint)(index == 3 ? 0x6E6 : 0x8F8));
        a.mov(rax, RaxSentinel); a.mov(rcx, 0x1234); a.mov(rdx, 0x2345); a.mov(r10, 0x5678);
        a.mov(ebx, 0x89AB); a.mov(ebp, 0x9ABC); a.mov(esi, 0xABCD); a.mov(edi, 123);
        a.mov(r12, 0xBCDE); a.mov(r13, 0xCDEF); a.mov(r14, 0xDEF0); a.mov(r15, 0xEF01);
        a.cmp(edi, 124);
        AutoCrewBindingPatch.Emit(a, site, unchecked((ulong)activation.ToInt64()), module, original);
        a.jmp(module + (uint)site.End); WriteNativeCode(image, entry, a);
        var done = new Assembler(64); done.pushfq();
        foreach (var r in new[] { rax, rcx, rdx, r8, r9, r10, r11, r12, r13, r14, r15, rbx, rbp, rsi, rdi }) done.push(r);
        done.mov(rdi, __qword_ptr[rsp + 128]);
        for (int n = 0; n < 16; n++) { done.mov(rax, __qword_ptr[rsp + n * 8]); done.mov(__qword_ptr[rdi + n * 8], rax); }
        done.mov(eax, __dword_ptr[rsp + 136]); done.add(rsp, 144);
        foreach (var r in new[] { r15, r14, r13, r12, rdi, rsi, rbp, rbx }) done.pop(r);
        done.ret(); WriteNativeCode(image, finish, done);
        Check(FlushInstructionCache(GetCurrentProcess(), image, (UIntPtr)0x210000), "auto fixture flush");
        int actual = Marshal.GetDelegateForFunctionPointer<RunReadiness>(IntPtr.Add(image, entry))(units, output);
        Check(actual == expected, "auto decision " + site.Name + " state=" + state + " recruit=" + recruiting);
        long[] registers = { 123, 0xABCD, 0x9ABC, 0x89AB, 0xEF01, 0xDEF0, 0xCDEF, 0xBCDE,
            unit.ToInt64() + (index == 3 ? 0x6E6 : 0x8F8), 0x5678, 0x490, units.ToInt64(), 0x2345, 0x1234, unchecked((long)RaxSentinel) };
        for (int n = 0; n < registers.Length; n++) Check(Marshal.ReadInt64(output, n * 8) == registers[n], "auto preserved register " + n);
        ulong flags = unchecked((ulong)Marshal.ReadInt64(output, 120)) & 0x8D5;
        Check(flags == (matchesOriginal ? 0x44UL : index == 3 ? 0x85UL : 0x91UL), "auto flags");
        Check(before.SequenceEqual(ReadNative(units, 0, 0x2000)), "auto candidate records unchanged");
    }
}
