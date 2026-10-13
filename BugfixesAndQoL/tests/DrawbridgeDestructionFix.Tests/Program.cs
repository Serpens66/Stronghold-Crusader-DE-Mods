using BugfixesAndQoL.DrawbridgeDestructionFix;
using Iced.Intel;
using RedBird.X64.Hooks;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protect);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint mode);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ExecuteDelegate(IntPtr result);
    private static int cases;
    private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }

    private static int Main(string[] args)
    {
        try
        {
            NativeContract.ValidateLayouts();
            foreach (bool mod in new[] { false, true }) foreach (bool fix in new[] { false, true })
                Require(NativeContract.IsEnabled(mod, fix) == (mod && fix), "mod/host option activation");
            byte[] function = NativeContract.ReadReferenceFunction(args[0]);
            Require(function.Length == NativeContract.FunctionSize, "reference function length");
            SpanAndResolver();
            foreach (eStructs type in new[] { eStructs.STRUCT_GATE_MAIN, eStructs.STRUCT_GATE_INNER, eStructs.STRUCT_DRAWBRIDGE, eStructs.STRUCT_GATE_WOOD, eStructs.STRUCT_GATE_POSTERN })
            foreach (int flag in new[] { 0, 1, 2 })
            foreach (short hp in new short[] { 994, 1, 0, -1, -50 })
            foreach (bool enabled in new[] { false, true })
            {
                ulong[] vanilla = Run(type, flag, hp, false, true, out int originalSuppressed);
                ulong[] actual = Run(type, flag, hp, enabled, false, out int suppressed);
                bool protect = enabled && flag == 1 && (type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER) && hp > 0;
                Require(suppressed == (protect ? 1 : 0), "suppression predicate");
                if (!protect)
                    for (int n = 0; n < actual.Length; n++) Require(actual[n] == vanilla[n], "original machine state differs at " + n);
                else
                {
                    Require(actual[0] == 0, "nonlethal hit reached early bridge deletion");
                    Require(actual[1] == (ulong)type, "skip clobbered R8");
                    Require(actual[2] == 0x1122334455667788UL && actual[4] == (ulong)flag && actual[5] == 0x32C, "saved register clobber");
                    Require((actual[3] & 0x8D5) == (0x246UL & 0x8D5), "skip clobbered flags");
                }
                cases++;
            }
            Console.WriteLine("PASS: " + cases + " native execution cases; inactive/non-target/lethal paths match the original registers, flags and branch outcome; real RedBird span=15; resolver absent/ambiguous/mutated cases rejected.");
            Console.WriteLine("LIMIT: private native fixture validates the decision and ABI, not complete game removal/rendering; 0/1/2 attached bridges and open/raised states require in-game acceptance.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void SpanAndResolver()
    {
        IntPtr copy = Marshal.AllocHGlobal(64), state = Marshal.AllocHGlobal(12);
        try
        {
            Marshal.Copy(NativeContract.Signature, 0, copy, NativeContract.Signature.Length);
            using (var probe = new X64InlineHook(unchecked((ulong)copy.ToInt64()), 15, name: "UnpublishedCopiedBufferProbe"))
            {
                Require(probe.DisplacedByteCount == 15, "actual installed RedBird displacement");
                probe.Generate((asm, original, continuation) => NativeContract.Emit(asm, original, continuation,
                    unchecked((ulong)copy.ToInt64()) + (NativeContract.SkipRva - NativeContract.HookRva), unchecked((ulong)state.ToInt64())));
                Require(probe.StubAddress != IntPtr.Zero, "RedBird production generator did not compile");
            }
        }
        finally { Marshal.FreeHGlobal(copy); Marshal.FreeHGlobal(state); }
        byte[] image = new byte[NativeContract.HookRva + 256];
        image[0] = (byte)'M'; image[1] = (byte)'Z'; Put(image, 0x3C, 0x80);
        image[0x80] = (byte)'P'; image[0x81] = (byte)'E'; image[0x86] = 1;
        Put(image, 0x98 + 8, image.Length - 0x100); Put(image, 0x98 + 12, 0x100); Put(image, 0x98 + 36, 0x20000000);
        Buffer.BlockCopy(NativeContract.Signature, 0, image, NativeContract.HookRva, NativeContract.Signature.Length);
        Require(NativeContract.Resolve(image, true, out string method) == NativeContract.HookRva && method == "reference-rva", "reference resolution");
        Require(NativeContract.Resolve(image, false, out method) == NativeContract.HookRva && method == "executable-pattern-fallback", "fallback resolution");
        Buffer.BlockCopy(NativeContract.Signature, 0, image, 0x200, NativeContract.Signature.Length);
        ExpectRejected(() => NativeContract.Resolve(image, false, out method));
        image[NativeContract.HookRva] = 0;
        Require(NativeContract.Resolve(image, true, out method) == 0x200, "RVA failure must still try fallback");
        image[0x200] = 0;
        ExpectRejected(() => NativeContract.Resolve(image, true, out method));
    }
    private static void Put(byte[] buffer, int at, int value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, at, 4);
    private static void ExpectRejected(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected fail-closed rejection."); }

    private static ulong[] Run(eStructs type, int attackFlag, short hp, bool enabled, bool baseline, out int suppressed)
    {
        IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
        if (code == IntPtr.Zero) throw new Exception("VirtualAlloc failed: " + Marshal.GetLastWin32Error());
        IntPtr record = Marshal.AllocHGlobal(0x700), state = Marshal.AllocHGlobal(12), output = Marshal.AllocHGlobal(56);
        try
        {
            for (int n = 0; n < 3; n++) Marshal.WriteInt32(state, n * 4, 0);
            Marshal.WriteInt32(state, enabled ? 1 : 0);
            Marshal.WriteInt16(record, 0x32C + NativeContract.TypeOffset, (short)type);
            Marshal.WriteInt16(record, 0x32C + NativeContract.HealthOffset, hp);
            ulong entry = unchecked((ulong)code.ToInt64()), skip = entry + 0x900, continuation = entry + 0x800;
            var asm = new Assembler(64);
            asm.push(rbp); asm.push(r13); asm.push(r15);
            asm.mov(r11, rcx); asm.mov(r15, unchecked((ulong)record.ToInt64())); asm.mov(rbp, 0x32C);
            asm.mov(r13d, attackFlag); asm.mov(r8d, (int)type); asm.mov(rax, 0x1122334455667788UL);
            asm.push(0x246); asm.popfq();
            Instruction[] original = NativeContract.DecodeSpan(entry);
            original[1].NearBranch64 = skip;
            if (baseline) { asm.AddInstructions(original); asm.jmp(continuation); }
            else NativeContract.Emit(asm, original, continuation, skip, unchecked((ulong)state.ToInt64()));
            byte[] bytes = Assemble(asm, entry); Require(bytes.Length < 0x800, "fixture overlap"); Write(code, bytes);
            // Original instructions immediately after the displaced block decide whether to delete bridges.
            var next = new Assembler(64); next.cmp(r8w, r13w); next.ja(skip); Output(next, 1);
            Write(new IntPtr(unchecked((long)continuation)), Assemble(next, continuation));
            var skipped = new Assembler(64); Output(skipped, 0);
            Write(new IntPtr(unchecked((long)skip)), Assemble(skipped, skip));
            Marshal.GetDelegateForFunctionPointer<ExecuteDelegate>(code)(output);
            var result = new ulong[7]; for (int n = 0; n < result.Length; n++) result[n] = unchecked((ulong)Marshal.ReadInt64(output, n * 8));
            Require(result[6] == unchecked((ulong)record.ToInt64()), "R15 manager pointer clobber");
            result[6] = 1;
            suppressed = Marshal.ReadInt32(state, 8);
            if (!baseline) Require(Marshal.ReadInt32(state, 4) == 1, "actual hook entry counter");
            return result;
        }
        finally { Marshal.FreeHGlobal(record); Marshal.FreeHGlobal(state); Marshal.FreeHGlobal(output); VirtualFree(code, UIntPtr.Zero, 0x8000); }
    }
    private static void Output(Assembler asm, int route)
    {
        asm.mov(__qword_ptr[r11], route); asm.mov(__qword_ptr[r11 + 8], r8); asm.mov(__qword_ptr[r11 + 16], rax);
        asm.pushfq(); asm.pop(rax); asm.mov(__qword_ptr[r11 + 24], rax);
        asm.mov(__qword_ptr[r11 + 32], r13); asm.mov(__qword_ptr[r11 + 40], rbp); asm.mov(__qword_ptr[r11 + 48], r15);
        asm.pop(r15); asm.pop(r13); asm.pop(rbp); asm.ret();
    }
    private static byte[] Assemble(Assembler asm, ulong address)
    {
        using (var stream = new MemoryStream())
        {
            asm.Assemble(new StreamCodeWriter(stream), address);
            byte[] bytes = stream.ToArray();
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
            int count = 0; while (count < bytes.Length) { decoder.Decode(out Instruction instruction); Require(!instruction.IsInvalid, "generator emitted invalid instruction"); count += instruction.Length; }
            Require(count == bytes.Length, "partial instruction at end"); return bytes;
        }
    }
    private static void Write(IntPtr target, byte[] bytes) => Marshal.Copy(bytes, 0, target, bytes.Length);
}
