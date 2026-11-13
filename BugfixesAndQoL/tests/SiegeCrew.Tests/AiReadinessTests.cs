using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BugfixesAndQoL.SiegeCrew;
using Iced.Intel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;
using static BugfixesAndQoL.SiegeCrew.AiCrewReadinessPatch;

internal static partial class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int RunReadiness(IntPtr image, IntPtr output);

    private static void VerifyAiReadiness(byte[] file, IntPtr activation)
    {
        var site = AiCrewReadinessPatch.Site;
        int offset = CodeOffset(file, site.Rva, site.Original.Length + site.Continuation.Length);
        var pristine = file.AsSpan(offset, site.Original.Length).ToArray();
        site.Verify(pristine, file.AsSpan(offset + pristine.Length, site.Continuation.Length), pristine);
        Reject(() => site.Verify(pristine, new byte[2], pristine), "AI continuation mismatch");
        Reject(() => site.Verify(pristine, site.Continuation, new byte[pristine.Length]), "AI occupied live site");
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(pristine), 0x180000000UL + (uint)site.Rva);
        var block = Enumerable.Range(0, 4).Select(_ => decoder.Decode()).ToArray();
        Check(block.Sum(instruction => instruction.Length) == 15 && block[3].Mnemonic == Mnemonic.Jl &&
            block[3].NearBranchTarget == 0x180000000UL + IndexInRangeRva, "AI complete original branch contract");
        int functionOffset = CodeOffset(file, 0x2ECD0, 0x3AE);
        var functionDecoder = Decoder.Create(64, new ByteArrayCodeReader(file.AsSpan(functionOffset, 0x3AE).ToArray()), 0x2ECD0);
        while (functionDecoder.IP < 0x2F07E)
        {
            Instruction instruction = functionDecoder.Decode();
            Check(instruction.Code != Code.INVALID, "AI full function decode");
            if (instruction.FlowControl == FlowControl.ConditionalBranch || instruction.FlowControl == FlowControl.UnconditionalBranch ||
                instruction.FlowControl == FlowControl.Call)
                Check(instruction.NearBranchTarget <= (uint)site.Rva || instruction.NearBranchTarget >= (uint)site.End,
                    "AI no direct interior entry");
        }

        IntPtr module = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x7CD0000, 0x3000, 0x40);
        IntPtr output = Marshal.AllocHGlobal(160);
        try
        {
            Check(module != IntPtr.Zero, "AI sparse native layout allocation");
            foreach (int index in new[] { 0, 8 })
            {
                foreach (bool global in new[] { false, true })
                foreach (bool feature in new[] { false, true })
                {
                    NativeContract.SetActivation(activation, global, feature);
                    ExecuteReadiness(module, output, activation, index, null, 0);
                    ExecuteReadiness(module, output, activation, index,
                        data => Put16(data, UnitManagerRva + 2 * UnitStride + 0x9CA, 1), global && feature ? 1 : 0);
                }
            }
            NativeContract.SetActivation(activation, true, true);
            int second = UnitManagerRva + 2 * UnitStride;
            var invalid = new (string Name, Action<IntPtr> Alter)[]
            {
                ("pending peasant", data => Put16(data, second + 0x6E6, (int)eChimps.CHIMP_TYPE_PEASANT)),
                ("initializing life", data => Put16(data, second + 0x6E4, (int)AliveState.NeedsInit)),
                ("dead life", data => Put16(data, second + 0x6E4, (int)AliveState.MarkedForDeletion)),
                ("death marker", data => Put16(data, second + 0x8F8, 1)),
                ("wrong owner", data => Put16(data, second + 0x6EE, 3)),
                ("missing global", data => Marshal.WriteInt32(data, second + 0x6F0, 0)),
                ("changed group", data => Put16(data, second + 0x930, 6)),
                ("reused group slot", data => Marshal.WriteInt32(data, second + 0x93C, 701)),
                ("wrong role", data => Put16(data, second + 0xA82, 10)),
                ("moving", data => Put16(data, second + StateOffset, 0x65)),
                ("already building", data => Put16(data, second + StateOffset, 8)),
                ("already bound", data => Put16(data, second + StateOffset, 5)),
                ("exit transition", data => Marshal.WriteInt32(data, second + StateOffset, 0x0007006D)),
                ("missing bit", data => Put16(data, AiGroup + 0x60, 2)),
                ("invalid zero ID", data => Put16(data, AiGroup + 0x60, 7)),
                ("slot beyond next ID", data => Put16(data, AiGroup + 0x60, 14)),
                ("stale bitmap tail", data => Put16(data, AiGroup + 0x60 + 1248, 0x8000)),
                ("wrong group owner", data => Marshal.WriteInt32(data, AiGroup + 0x2C, 3)),
                ("wrong group generation", data => Marshal.WriteInt32(data, AiGroup + 0x34, 701)),
                ("dead group", data => Put16(data, AiGroup + 0x40, 3)),
                ("empty group", data => Put16(data, AiGroup + 0x5C, 0)),
                ("inconsistent count", data => Put16(data, AiGroup + 0x5C, 3)),
                ("unit array overflow", data => Marshal.WriteInt32(data, UnitManagerRva, MaximumUnits + 1)),
            };
            foreach (var test in invalid)
                ExecuteReadiness(module, output, activation, 0, test.Alter, 1);
            foreach (int state in new[] { 0, 1, 2 })
                ExecuteReadiness(module, output, activation, 0, data => Put16(data, second + StateOffset, state), 0);
            ExecuteReadiness(module, output, activation, 0,
                data => Put16(data, AiGroup + 0x2E, 0xABCD), 0); // Owner is a word, not the adjacent field.
        }
        finally
        {
            Marshal.FreeHGlobal(output);
            if (module != IntPtr.Zero) VirtualFree(module, UIntPtr.Zero, 0x8000);
        }
    }

    private const int AiGroup = TribeManagerRva + 5 * TribeStride;
    private static void Put16(IntPtr image, int offset, int value) => Marshal.WriteInt16(image, offset, unchecked((short)value));

    private static void ExecuteReadiness(IntPtr module, IntPtr output, IntPtr activation, int machineIndex,
        Action<IntPtr> alter, int expected)
    {
        var zero = new byte[TribeStride];
        Marshal.Copy(zero, 0, IntPtr.Add(module, AiGroup), zero.Length);
        Marshal.WriteInt32(module, AiGroup + 0x2C, 2);
        Marshal.WriteInt32(module, AiGroup + 0x34, 700);
        Put16(module, AiGroup + 0x40, (int)AliveState.IsAlive);
        Put16(module, AiGroup + 0x5C, 2);
        Put16(module, AiGroup + 0x60, 6);
        Marshal.WriteInt32(module, 0x379FB5C + 2 * 0x583C, 700);
        Marshal.WriteInt32(module, 0x379E6E4 + 2 * 0x583C, machineIndex);
        Marshal.WriteInt32(module, UnitManagerRva, 3);
        for (int id = 1; id <= 2; id++)
        {
            int unit = UnitManagerRva + id * UnitStride;
            Marshal.Copy(new byte[UnitStride], 0, IntPtr.Add(module, unit + 0x65C), UnitStride);
            Put16(module, unit + 0x6E4, (int)AliveState.IsAlive);
            Put16(module, unit + 0x6E6, (int)eChimps.CHIMP_TYPE_ENGINEER);
            Put16(module, unit + 0x6EE, 2);
            Marshal.WriteInt32(module, unit + 0x6F0, 100 + id);
            Put16(module, unit + 0x930, 5);
            Marshal.WriteInt32(module, unit + 0x93C, 700);
            Put16(module, unit + 0xA82, HarassmentEngineerRole);
        }
        alter?.Invoke(module);
        byte[] groupBefore = ReadNative(module, AiGroup, TribeStride);
        byte[] unitsBefore = ReadNative(module, UnitManagerRva + UnitStride + 0x65C, 2 * UnitStride);
        ulong image = unchecked((ulong)module.ToInt64());
        const int entry = 0x200000, finish = 0x201000;
        foreach (var target in new[] { (RetryRva, 1), (IndexInRangeRva, 0), (AiCrewReadinessPatch.Site.End, 0) })
        {
            var jump = new Assembler(64);
            jump.jmp(image + (uint)(target.Item2 == 0 ? 0x201100 : 0x201120));
            WriteNativeCode(module, target.Item1, jump);
        }
        foreach (int result in new[] { 0, 1 })
        {
            var marker = new Assembler(64);
            marker.mov(__dword_ptr[rsp + 8], result);
            marker.jmp(image + finish);
            WriteNativeCode(module, result == 0 ? 0x201100 : 0x201120, marker);
        }
        var a = new Assembler(64);
        foreach (var register in new[] { rbx, rbp, rsi, rdi, r12, r13, r14, r15 }) a.push(register);
        a.sub(rsp, 16);
        a.mov(__qword_ptr[rsp], rdx);
        a.mov(r13, rcx);
        a.mov(ebx, 2 * 0x583C);
        a.mov(ebp, 2);
        a.mov(esi, 5);
        a.mov(r14d, 5 * TribeStride);
        a.mov(edi, 123);
        a.mov(rax, RaxSentinel);
        a.mov(rcx, 0x1234);
        a.mov(rdx, 0x2345);
        a.mov(r8, 0x3456);
        a.mov(r9, 0x4567);
        a.mov(r10, 0x5678);
        a.mov(r11, 0x6789);
        a.mov(r12, 0x789A);
        a.mov(r15, 0x89AB);
        a.cmp(edi, 124);
        AiCrewReadinessPatch.Emit(a, unchecked((ulong)activation.ToInt64()), image);
        a.jmp(image + (uint)AiCrewReadinessPatch.Site.End);
        WriteNativeCode(module, entry, a);

        var done = new Assembler(64);
        done.pushfq();
        foreach (var register in new[] { rax, rcx, rdx, r8, r9, r10, r11, r12, r13, r14, r15, rbx, rbp, rsi, rdi }) done.push(register);
        done.mov(rdi, __qword_ptr[rsp + 128]);
        for (int index = 0; index < 16; index++)
        {
            done.mov(rax, __qword_ptr[rsp + index * 8]);
            done.mov(__qword_ptr[rdi + index * 8], rax);
        }
        done.mov(eax, __dword_ptr[rsp + 136]);
        done.add(rsp, 144);
        foreach (var register in new[] { r15, r14, r13, r12, rdi, rsi, rbp, rbx }) done.pop(register);
        done.ret();
        WriteNativeCode(module, finish, done);
        Check(FlushInstructionCache(GetCurrentProcess(), module, (UIntPtr)0x210000), "AI native fixture flush");
        int actual = Marshal.GetDelegateForFunctionPointer<RunReadiness>(IntPtr.Add(module, entry))(module, output);
        Check(actual == expected, "AI readiness decision");
        long[] registers = { expected == 1 ? 123 : 0, 5, 2, 2 * 0x583C, 0x89AB, 5 * TribeStride,
            module.ToInt64(), 0x789A, 0x6789, 0x5678, 0x4567, 0x3456, 0x2345,
            expected == 1 ? 0x1234 : machineIndex, unchecked((long)RaxSentinel) };
        for (int index = 0; index < registers.Length; index++)
            Check(Marshal.ReadInt64(output, index * 8) == registers[index], "AI register " + index);
        ulong flags = unchecked((ulong)Marshal.ReadInt64(output, 120));
        Check((flags & 0x8D5) == (expected == 1 ? 0x95UL : machineIndex == 0 ? 0x91UL : 0x44UL), "AI live flags");
        Check(groupBefore.SequenceEqual(ReadNative(module, AiGroup, TribeStride)), "AI membership unchanged");
        Check(unitsBefore.SequenceEqual(ReadNative(module, UnitManagerRva + UnitStride + 0x65C, 2 * UnitStride)), "AI unit records unchanged");
    }

    private static byte[] ReadNative(IntPtr image, int offset, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(IntPtr.Add(image, offset), bytes, 0, length);
        return bytes;
    }

    private static void WriteNativeCode(IntPtr image, int offset, Assembler assembler)
    {
        using (var stream = new MemoryStream())
        {
            assembler.Assemble(new StreamCodeWriter(stream), unchecked((ulong)image.ToInt64()) + (uint)offset);
            byte[] bytes = stream.ToArray();
            Check(bytes.Length < 4096, "AI fixture block size");
            Marshal.Copy(bytes, 0, IntPtr.Add(image, offset), bytes.Length);
        }
    }
}
