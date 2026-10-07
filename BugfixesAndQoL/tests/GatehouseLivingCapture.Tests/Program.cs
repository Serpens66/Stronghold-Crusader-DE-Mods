using APIShared;
using RedBird.X64.Assembly;
using Iced.Intel;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.GatehouseLivingCapture
{
    internal static unsafe class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Harness(IntPtr word, IntPtr results, int comparison, int unitId);
        // Published test hooks/pages/delegates remain rooted until process exit.
        private static readonly List<object> Roots = new List<object>();
        private const ulong Sentinel = 0x123456789ABCDE00;

        private static int Main()
        {
            try
            {
                NativeDefinition.ValidateLayout();
                NativeAudit.Run();
                TestLifePredicate();
                TestActivation();
                TestAdapter();
                Console.WriteLine("PASS: installed GameUnit layout, life helper, real RedBird span/patch/JE, registers, XMM, stack, entry/backedge, inactive/invalid/error paths.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void TestLifePredicate()
        {
            foreach (AliveState state in Enum.GetValues(typeof(AliveState)))
            foreach (uint marker in new uint[] { 0, 1, 0xFFFF, 0x10000, 0xFFFF0000, 0xFFFFFFFF })
            foreach (uint health in new uint[] { 0, 100 })
            {
                GameUnit unit = new GameUnit { r_AliveState = state, N0000019A = marker, r_CurrentHealth = health };
                bool expected = state == AliveState.IsAlive && (marker & 0xFFFFu) == 0;
                Require(UnitAccess.IsReallyAlive(in unit) == expected, "reference predicate");
                Require(UnitAccess.IsReallyAlive(&unit) == expected, "pointer predicate");
            }
            Require(!UnitAccess.IsReallyAlive((GameUnit*)null), "null predicate");
        }

        private static void TestActivation()
        {
            GameUnit dead = new GameUnit { r_AliveState = AliveState.IsAlive, N0000019A = 1 };
            foreach (bool mod in new[] { false, true })
            foreach (bool fix in new[] { false, true })
            {
                X64SmartCPUContext context = default;
                context.R11 = Sentinel | 1;
                CaptureDecision.Apply(&context, &dead, CaptureDecision.IsEnabled(mod, fix));
                Require(context.R11 == (mod && fix ? Sentinel : Sentinel | 1), "main mod/feature activation");
            }
            var runtime = new CaptureRuntime(null);
            foreach (bool enabled in new[] { true, false, true })
            {
                runtime.SetEnabled(true, enabled);
                var field = typeof(CaptureRuntime).GetField("active", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Require((int)field.GetValue(runtime) == 0, "unpublished candidate remains inactive");
            }
            typeof(CaptureRuntime).GetField("published", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(runtime, true);
            foreach (bool enabled in new[] { true, false, true })
            {
                runtime.SetEnabled(true, enabled);
                Require((int)typeof(CaptureRuntime).GetField("active", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(runtime) == (enabled ? 1 : 0), "atomic reactivation");
            }
            runtime.SetEnabled(false, true);
            Require((int)typeof(CaptureRuntime).GetField("active", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(runtime) == 0, "main switch disables correction");
            Console.WriteLine("PASS: feature/main switches, unpublished state, disable and reactivation.");
        }

        private static void TestAdapter()
        {
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            Require(page != IntPtr.Zero, "executable test allocation"); Roots.Add(page);
            ulong ip = unchecked((ulong)page.ToInt64());
            byte[] harnessBytes = BuildHarness(ip);
            int blockOffset = Find(harnessBytes, NativeDefinition.Original);
            Marshal.Copy(harnessBytes, 0, page, harnessBytes.Length);
            int callbackCalls = 0, errors = 0;
            bool active = true, nullUnit = false, syntheticFailure = false;
            GameUnit sample = default;
            GameUnit* samplePointer = &sample;
            CaptureCallback callback = address =>
            {
                X64SmartCPUContext* context = (X64SmartCPUContext*)address;
                ulong original = context->R11;
                callbackCalls++;
                try
                {
                    CaptureDecision.Apply(context, nullUnit ? null : samplePointer, active);
                    if (syntheticFailure) throw new InvalidOperationException("synthetic failure after filtering");
                }
                catch { context->R11 = original; errors++; }
            };
            Roots.Add(callback);
            ulong callbackIp = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64());
            CaptureAdapterEmitter.AssembleAndValidate(NativeDefinition.Original, ip + (ulong)blockOffset, callbackIp, ip + 0x1000);
            // Use the SAME installed X64InlineHook backend as production, on a private copy.
            var hook = new X64InlineHook(ip + (ulong)blockOffset, NativeDefinition.HookLength);
            Require(hook.DisplacedByteCount == 18 && hook.TargetAddress == ip + (ulong)blockOffset, "actual RedBird span/target");
            hook.Generate((a, original, ret) => CaptureAdapterEmitter.Emit(a, original, callbackIp));
            hook.Enable(); Roots.Add(hook);
            byte* patch = (byte*)page + blockOffset;
            Require(patch[0] == 0xFF && patch[1] == 0x25 && *(uint*)(patch + 2) == 0 && *(ulong*)(patch + 6) != 0,
                "actual RedBird absolute-indirect patch and destination");
            for (int i = 14; i < 18; i++) Require(patch[i] == 0x90, "actual RedBird padding");
            byte* stub = (byte*)*(ulong*)(patch + 6);
            byte[] expectedStub = CaptureAdapterEmitter.AssembleAndValidate(NativeDefinition.Original,
                ip + (ulong)blockOffset, callbackIp, unchecked((ulong)stub));
            for (int i = 0; i < expectedStub.Length; i++) Require(stub[i] == expectedStub[i], "actual generated stub bytes");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(Copy(stub + expectedStub.Length, 16)));
            decoder.IP = unchecked((ulong)stub) + (ulong)expectedStub.Length;
            decoder.Decode(out Instruction continuation);
            Require(continuation.Mnemonic == Mnemonic.Jmp, "actual backend return jump");
            ulong returnTarget = continuation.IsIPRelativeMemoryOperand
                ? *(ulong*)continuation.IPRelativeMemoryAddress : continuation.NearBranchTarget;
            Require(returnTarget == ip + (ulong)blockOffset + 18, "actual trampoline continuation");
            Require(FlushInstructionCache(GetCurrentProcess(), page, (UIntPtr)harnessBytes.Length), "instruction cache flush");
            var invoke = (Harness)Marshal.GetDelegateForFunctionPointer(page, typeof(Harness)); Roots.Add(invoke);
            IntPtr word = Marshal.AllocHGlobal(8), results = Marshal.AllocHGlobal(256);
            int cases = 0;
            try
            {
                foreach (int id in new[] { 1, 2, 9999 })
                foreach (short eligibility in new short[] { 0, 1, -1 })
                foreach (int mode in new[] { 0, 1, 2, 3 })
                foreach (AliveState state in new[] { AliveState.IsAlive, AliveState.NeedsInit, AliveState.MarkedForDeletion })
                foreach (uint death in new uint[] { 0, 1, 0x10000 })
                foreach (int iterations in new[] { 1, 2 })
                {
                    sample = new GameUnit { r_AliveState = state, N0000019A = death, r_CurrentHealth = 0 };
                    active = mode != 1; nullUnit = mode == 2; syntheticFailure = mode == 3;
                    Marshal.WriteInt16(word, eligibility);
                    Marshal.WriteInt32(results, 240, id); Marshal.WriteInt32(results, 248, iterations);
                    int before = callbackCalls;
                    int actual = invoke(word, results, 0, id);
                    bool originalEligible = eligibility != 0;
                    bool eligible = originalEligible &&
                        (mode != 0 || UnitAccess.IsReallyAlive(in sample));
                    Require(actual == (eligible ? 1 : 2), "original JE decision");
                    Require(callbackCalls - before == iterations, "first entry and native-style chain backedge");
                    Require(Marshal.ReadInt64(results, 8) == Marshal.ReadInt64(results, 16), "stack balance");
                    Require(unchecked((ulong)Marshal.ReadInt64(results, 24)) == Sentinel + 11, "R11 restored");
                    Require(Marshal.ReadInt64(results, 32) == id, "CDQE output RAX");
                    Require(Marshal.ReadInt64(results, 40) == id * NativeDefinition.UnitSize, "IMUL output RDI");
                    Require(Marshal.ReadInt64(results, 48) == word.ToInt64() - id * NativeDefinition.UnitSize - (long)NativeDefinition.EligibilityDisplacement, "R13 base");
                    Require(Marshal.ReadInt64(results, 56) == 0, "R14 comparison");
                    int[] saved = { 1, 2, 3, 4, 6, 9, 10, 12, 15 };
                    for (int i = 0; i < saved.Length; i++)
                        Require(unchecked((ulong)Marshal.ReadInt64(results, 64 + i * 8)) == Sentinel + (ulong)saved[i], "GPR " + saved[i]);
                    for (int i = 0; i < 12; i++) Require(Marshal.ReadInt64(results, 144 + i * 8) == -1, "XMM0..5 preserved");
                    cases++;
                }
                Require(errors != 0, "synthetic callback errors exercised");
                Console.WriteLine("PASS: " + cases + " native machine cases; callback errors=" + errors);
            }
            finally { Marshal.FreeHGlobal(word); Marshal.FreeHGlobal(results); }
        }

        private static byte[] BuildHarness(ulong ip)
        {
            var a = new Assembler(64);
            a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi);
            a.push(r12); a.push(r13); a.push(r14); a.push(r15); a.sub(rsp, 64);
            a.mov(r14d, r8d); a.mov(r8, rdx); a.mov(eax, r9d);
            a.mov(__qword_ptr[r8 + 8], rsp);
            a.imul(rdi, rax, NativeDefinition.UnitSize);
            a.mov(r13, rcx); a.sub(r13, rdi); a.sub(r13, (int)NativeDefinition.EligibilityDisplacement);
            a.mov(rbx, Sentinel + 1); a.mov(rcx, Sentinel + 2); a.mov(rdx, Sentinel + 3);
            a.mov(rsi, Sentinel + 4); a.mov(rbp, Sentinel + 6); a.mov(r9, Sentinel + 9);
            a.mov(r10, Sentinel + 10); a.mov(r11, Sentinel + 11); a.mov(r12, Sentinel + 12); a.mov(r15, Sentinel + 15);
            a.pcmpeqd(xmm0, xmm0); a.pcmpeqd(xmm1, xmm1); a.pcmpeqd(xmm2, xmm2);
            a.pcmpeqd(xmm3, xmm3); a.pcmpeqd(xmm4, xmm4); a.pcmpeqd(xmm5, xmm5);
            Label block = a.CreateLabel(), skip = a.CreateLabel(), collect = a.CreateLabel();
            a.Label(ref block); a.db(NativeDefinition.Original);
            a.je(skip); a.mov(__dword_ptr[r8], 1); a.jmp(collect);
            a.Label(ref skip); a.mov(__dword_ptr[r8], 2);
            a.Label(ref collect);
            a.mov(__qword_ptr[r8 + 16], rsp); a.mov(__qword_ptr[r8 + 24], r11);
            a.mov(__qword_ptr[r8 + 32], rax); a.mov(__qword_ptr[r8 + 40], rdi);
            a.mov(__qword_ptr[r8 + 48], r13); a.mov(__qword_ptr[r8 + 56], r14);
            a.mov(__qword_ptr[r8 + 64], rbx); a.mov(__qword_ptr[r8 + 72], rcx); a.mov(__qword_ptr[r8 + 80], rdx);
            a.mov(__qword_ptr[r8 + 88], rsi); a.mov(__qword_ptr[r8 + 96], rbp); a.mov(__qword_ptr[r8 + 104], r9);
            a.mov(__qword_ptr[r8 + 112], r10); a.mov(__qword_ptr[r8 + 120], r12); a.mov(__qword_ptr[r8 + 128], r15);
            a.movdqu(__xmmword_ptr[r8 + 144], xmm0); a.movdqu(__xmmword_ptr[r8 + 160], xmm1);
            a.movdqu(__xmmword_ptr[r8 + 176], xmm2); a.movdqu(__xmmword_ptr[r8 + 192], xmm3);
            a.movdqu(__xmmword_ptr[r8 + 208], xmm4); a.movdqu(__xmmword_ptr[r8 + 224], xmm5);
            a.mov(eax, __dword_ptr[r8 + 240]); a.dec(__dword_ptr[r8 + 248]); a.jne(block);
            a.mov(eax, __dword_ptr[r8]); a.add(rsp, 64);
            a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12);
            a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx); a.ret();
            using (var stream = new MemoryStream()) { a.Assemble(new StreamCodeWriter(stream), ip); return stream.ToArray(); }
        }
        private static byte[] Copy(byte* source, int length)
        { byte[] bytes = new byte[length]; Marshal.Copy((IntPtr)source, bytes, 0, length); return bytes; }
        private static int Find(byte[] bytes, byte[] pattern)
        {
            for (int i = 0; i <= bytes.Length - pattern.Length; i++)
            { int j = 0; while (j < pattern.Length && bytes[i + j] == pattern[j]) j++; if (j == pattern.Length) return i; }
            throw new InvalidOperationException("Harness block not found.");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Capture test failed: " + message); }
    }
}
