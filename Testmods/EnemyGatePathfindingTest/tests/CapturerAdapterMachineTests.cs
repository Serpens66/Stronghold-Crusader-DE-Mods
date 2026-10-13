using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using static Iced.Intel.AssemblerRegisters;

namespace EnemyGatePathfindingTest
{
    internal static unsafe class CapturerAdapterMachineTests
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RunHarness(IntPtr record, IntPtr captureBase, IntPtr results, int compareValue);
        // Published test hooks, delegates and executable pages live until this test
        // process exits. They are never disabled or freed while executable.
        private static readonly List<X64InlineHook> Hooks = new List<X64InlineHook>();
        private static readonly List<Delegate> Callbacks = new List<Delegate>();
        private static readonly List<IntPtr> Pages = new List<IntPtr>();
        private const ulong Sentinel = 0x123456789ABCDEF0;

        internal static void Run()
        {
            if (IntPtr.Size != 8) throw new InvalidOperationException("Native tests require x64.");
            string[] fields = { "RAX", "RBX", "RCX", "RDX", "RSI", "RDI", "RBP", "RSP",
                "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "Rflags" };
            for (int i = 0; i < fields.Length; i++)
                Require(Marshal.OffsetOf(typeof(X64SmartCPUContext), fields[i]).ToInt32() == i * 8,
                    "installed context field layout: " + fields[i]);
            foreach (bool builder in new[] { false, true }) RunSite(builder);
            RunTransparentTrace(false);
            RunTransparentTrace(true);
            RunTransparentTrace(true, true);
            RunTransparentTrace(false, false, true);
            Console.WriteLine("PASS: both installed-RedBird capturer adapters execute the real JE, preserve registers/XMM/stack, and fail open.");
        }

        private static void RunSite(bool builder)
        {
            byte[] originalBytes = Hex(builder
                ? "49 63 49 F4 48 69 D1 2C 03 00 00 66 42 39 84 2A D2 CE 4C 06"
                : "49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00");
            int queryPlayer = 2;
            bool throwInsideCallback = false;
            NativeGateAccessSnapshot snapshot = NativeGateAccessSnapshot.Empty;
            int callbackErrors = 0;
            CapturerComparisonCallback callback = context =>
            {
                X64SmartCPUContext* c = (X64SmartCPUContext*)context;
                ulong originalBoolean = c->R11;
                try
                {
                    if (throwInsideCallback) throw new InvalidOperationException("synthetic callback failure");
                    byte* table = (byte*)(builder ? c->R13 : c->RAX);
                    int captured = *(ushort*)(table + c->RDX +
                        EnemyGatePathfindingNativeDefinition.CapturedByPlayerTableDisplacement);
                    if (snapshot.Evaluate(queryPlayer, (int)c->RCX, 1, captured) ==
                        NativeGateSnapshotDecision.ExcludeForeignCapture)
                        c->R11 &= ~0xFFUL;
                }
                catch { c->R11 = originalBoolean; callbackErrors++; }
            };
            Callbacks.Add(callback);
            ulong callbackAddress = (ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64();
            IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (code == IntPtr.Zero) throw new InvalidOperationException("VirtualAlloc failed.");
            Pages.Add(code);
            ulong codeAddress = (ulong)code.ToInt64();
            byte[] harness = BuildHarness(originalBytes, codeAddress, builder);
            int offset = Find(harness, originalBytes);
            Marshal.Copy(harness, 0, code, harness.Length);
            CapturerComparisonAdapterEmitter.AssembleAndValidate(originalBytes,
                codeAddress + (ulong)offset, callbackAddress, codeAddress + 0x1000);
            var hook = new X64InlineHook(codeAddress + (ulong)offset, 20);
            if (hook.DisplacedByteCount != 20 || hook.TargetAddress != codeAddress + (ulong)offset)
                throw new InvalidOperationException("Unexpected real RedBird hook span/target.");
            hook.Generate((a, original, ret) => CapturerComparisonAdapterEmitter.Emit(a, original, callbackAddress));
            hook.Enable();
            Hooks.Add(hook);
            FlushInstructionCache(GetCurrentProcess(), code, (UIntPtr)harness.Length);
            RunHarness invoke = (RunHarness)Marshal.GetDelegateForFunctionPointer(code, typeof(RunHarness));

            IntPtr records = Marshal.AllocHGlobal(0x700);
            IntPtr word = Marshal.AllocHGlobal(8);
            IntPtr results = Marshal.AllocHGlobal(256);
            try
            {
                // Current open-field pointer, with a separately populated predecessor.
                IntPtr r9 = records + 0x300;
                Marshal.WriteInt32(r9, -12, 3);
                foreach (int relative in new[] { 0x1C, 0x20, 0x1D0 })
                {
                    Marshal.WriteInt32(r9, relative, relative + 1000);
                    Marshal.WriteInt32(r9, relative - 0x204, relative + 2000);
                }
                foreach (int relative in new[] {
                    EnemyGatePathfindingNativeDefinition.RecordFirstPclOffset,
                    EnemyGatePathfindingNativeDefinition.RecordSecondPclOffset,
                    EnemyGatePathfindingNativeDefinition.RecordThirdPclOffset })
                    Require(Marshal.ReadInt32(r9, relative) == relative + 1000, "current record PCL, not predecessor");
                long baseAddress = word.ToInt64() - 3 * 0x32C -
                    EnemyGatePathfindingNativeDefinition.CapturedByPlayerTableDisplacement;
                foreach (int captured in new[] { 0, 3 })
                foreach (int comparison in new[] { 0, 3 })
                foreach (int scenario in new[] { 0, 1, 2, 3, 4 })
                {
                    queryPlayer = scenario == 2 ? 3 : scenario == 4 ? 0 : 2;
                    throwInsideCallback = scenario == 3;
                    var recordsById = new NativeGateAccessRecord[4];
                    recordsById[3] = new NativeGateAccessRecord(true, 1, captured, 2,
                        (ushort)(1 << captured), (ushort)(1 << 2));
                    snapshot = scenario == 0 ? NativeGateAccessSnapshot.Empty :
                        new NativeGateAccessSnapshot(recordsById, 1);
                    Marshal.WriteInt16(word, (short)captured);
                    int actual = invoke(r9, new IntPtr(baseAddress), results, comparison);
                    bool nativeEqual = captured == (builder ? comparison : 0);
                    bool rejected = nativeEqual ||
                        (scenario == 1 && captured != 0);
                    Require(actual == (rejected ? 2 : 1), "native branch result");
                    Require(Marshal.ReadInt64(results, 8) == Marshal.ReadInt64(results, 16), "stack balance");
                    Require(unchecked((ulong)Marshal.ReadInt64(results, 24)) == Sentinel, "R11 restored");
                    Require(unchecked((ulong)Marshal.ReadInt64(results, 32)) == Sentinel + 1, "R10 preserved");
                    Require(Marshal.ReadInt64(results, 40) == 3 && Marshal.ReadInt64(results, 48) == 3 * 0x32C,
                        "original RCX/RDX outputs");
                    Require(Marshal.ReadInt64(results, 56) == r9.ToInt64(), "R9 preserved");
                    Require(Marshal.ReadInt64(results, 64) == (builder ? comparison : baseAddress), "RAX preserved");
                    Require(Marshal.ReadInt64(results, 72) == baseAddress, "R13 preserved");
                    for (int i = 0; i < 7; i++)
                        Require(unchecked((ulong)Marshal.ReadInt64(results, 80 + i * 8)) == Sentinel + (ulong)i + 2,
                            "nonvolatile register " + i);
                    for (int i = 0; i < 12; i++) Require(Marshal.ReadInt64(results, 144 + i * 8) == -1, "volatile XMM preserved");
                }
                Require(callbackErrors == 4, "all callback failures observed");
                // Same machine stub and memory, only the registered snapshot differs.
                // This proves the local comparison change, not a complete AI order.
                queryPlayer = 2; throwInsideCallback = false;
                Marshal.WriteInt16(word, 3);
                snapshot = NativeGateAccessSnapshot.Empty;
                int nativeForeignCapture = invoke(r9, new IntPtr(baseAddress), results, 0);
                var foreignRecords = new NativeGateAccessRecord[4];
                foreignRecords[3] = new NativeGateAccessRecord(true, 1, 3, 2, (ushort)(1 << 3), (ushort)(1 << 2));
                snapshot = new NativeGateAccessSnapshot(foreignRecords, 1);
                int policyForeignCapture = invoke(r9, new IntPtr(baseAddress), results, 0);
                queryPlayer = 3;
                int permittedCapture = invoke(r9, new IntPtr(baseAddress), results, 0);
                Require(nativeForeignCapture == 1 && policyForeignCapture == 2 && permittedCapture == 1,
                    "controlled local comparison: third-party capture rejected, actual capturer preserved");
                Console.WriteLine("PASS controlled " + (builder ? "builder" : "graph") +
                    " comparison: owner1/capturer3/query2 native=accept, policy=reject; query3=accept. Full AI order not measured.");
            }
            finally { Marshal.FreeHGlobal(records); Marshal.FreeHGlobal(word); Marshal.FreeHGlobal(results); }
        }

        private static void RunTransparentTrace(bool nativeCall, bool execution = false, bool descent = false)
        {
            byte[] original = new byte[14]; for (int i = 0; i < original.Length; i++) original[i] = 0x90;
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40); Pages.Add(page);
            Require(page != IntPtr.Zero, "trace test allocation");
            byte[] harness = BuildHarness(original, (ulong)page.ToInt64(), true, true);
            int offset = Find(harness, original);
            if (nativeCall)
            {
                int target = harness.Length;
                byte[] helper = { 0x41,0xFF,0x80,0xF0,0,0,0,0x83,0xF8,0,0xC3 }; // increment native-call count; CMP EAX,0; RET
                Array.Resize(ref harness, target + helper.Length); Array.Copy(helper, 0, harness, target, helper.Length);
                harness[offset] = 0xE8; Array.Copy(BitConverter.GetBytes(target - offset - 5), 0, harness, offset + 1, 4);
            }
            Marshal.Copy(harness, 0, page, harness.Length);
            IntPtr enabled = Marshal.AllocHGlobal(4), results = Marshal.AllocHGlobal(256);
            Pages.Add(enabled); // embedded pointer remains rooted with the published test stub
            IntPtr module = execution ? VirtualAlloc(IntPtr.Zero, (UIntPtr)0x67F0000, 0x3000, 0x04) : IntPtr.Zero;
            if (execution) { Require(module != IntPtr.Zero, "execution memory"); Pages.Add(module); Marshal.WriteInt32(module, 0x9302C4, 1); }
            int calls = 0; bool fail = false;
            CapturerComparisonCallback callback = p => { var c = (X64SmartCPUContext*)p;
                if (descent) c->R11 = 0;
                try { calls++; if (fail) throw new InvalidOperationException("fixture"); if (descent) c->R11 = c->RAX == 0 ? 1UL : 0; } catch { } };
            Callbacks.Add(callback);
            IntPtr noDiagnostics = Marshal.AllocHGlobal(4); Pages.Add(noDiagnostics); Marshal.WriteInt32(noDiagnostics, 0);
            var hook = new X64InlineHook((ulong)page.ToInt64() + (uint)offset, 14);
            hook.Generate((a, instructions, ret) => {
                ulong cb = (ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64();
                if (descent) AssassinGateDescentAdapter.Emit(a, instructions, cb, (ulong)enabled.ToInt64(), (ulong)page.ToInt64() + (uint)offset + 14, cb, cb, (ulong)noDiagnostics.ToInt64(), 0);
                else if (execution) NativeCallbackAdapterEmitter.EmitExecution(a, instructions, cb, cb, (ulong)enabled.ToInt64(), (ulong)module.ToInt64(), true);
                else NativeCallbackAdapterEmitter.Emit(a, instructions, cb, (ulong)enabled.ToInt64());
            });
            Require(hook.DisplacedByteCount == 14, "trace backend exact displacement");
            hook.Enable(); Hooks.Add(hook);
            var run = (RunHarness)Marshal.GetDelegateForFunctionPointer(page, typeof(RunHarness));
            try
            {
                foreach (int type in execution ? new[] { 0, 73 } : new[] { 0 })
                foreach (int on in new[] { 0, 1 }) foreach (int value in new[] { 0, 1 }) foreach (bool error in new[] { false, true })
                {
                    Marshal.WriteInt32(enabled, on); fail = error;
                    if (execution) Marshal.WriteInt16(module, 0x67E8AE6 + 0x490, (short)type);
                    Marshal.WriteInt32(results, 240, 0);
                    Require(run((IntPtr)123, (IntPtr)456, results, value) == (value == 0 ? 2 : 1), "incoming ZF survives observer and native branch");
                    Require(Marshal.ReadInt32(results, 240) == (nativeCall ? 1 : 0), "displaced native call occurs exactly once");
                    Require(Marshal.ReadInt64(results, 8) == Marshal.ReadInt64(results, 16), "trace stack preserved");
                    Require((ulong)Marshal.ReadInt64(results, 24) == Sentinel && (ulong)Marshal.ReadInt64(results, 32) == Sentinel + 1, "trace volatile registers");
                    Require(Marshal.ReadInt64(results, 40) == 123 && Marshal.ReadInt64(results, 48) == 456 &&
                        Marshal.ReadInt64(results, 56) == 123 && Marshal.ReadInt64(results, 64) == value, "trace argument registers");
                    for (int i = 0; i < 7; i++) Require((ulong)Marshal.ReadInt64(results, 80 + i * 8) == Sentinel + (ulong)i + 2, "trace nonvolatile");
                    for (int i = 0; i < 12; i++) Require(Marshal.ReadInt64(results, 144 + i * 8) == -1, "trace XMM");
                }
                Require(calls == (execution ? 8 : 4), "inactive/ordinary trace performs no managed callback; execution is paired");
            }
            finally { Marshal.FreeHGlobal(results); }
            Console.WriteLine("PASS: installed RedBird read-only trace stub preserves incoming flags/GPR/XMM/stack; inactive and callback-failure branches.");
        }
        private static byte[] BuildHarness(byte[] originalBytes, ulong ip, bool builder, bool trace = false)
        {
            var a = new Assembler(64);
            a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi);
            a.push(r12); a.push(r13); a.push(r14); a.push(r15);
            a.sub(rsp, 64);
            a.mov(__qword_ptr[r8 + 8], rsp);
            a.mov(r13, rdx);
            if (builder) a.mov(eax, r9d); else a.mov(rax, rdx);
            a.mov(r9, rcx);
            a.mov(r11, Sentinel); a.mov(r10, Sentinel + 1);
            a.mov(rbx, Sentinel + 2); a.mov(rbp, Sentinel + 3);
            a.mov(rsi, Sentinel + 4); a.mov(rdi, Sentinel + 5);
            a.mov(r12, Sentinel + 6); a.mov(r14, Sentinel + 7); a.mov(r15, Sentinel + 8);
            a.pcmpeqd(xmm0, xmm0); a.pcmpeqd(xmm1, xmm1); a.pcmpeqd(xmm2, xmm2);
            a.pcmpeqd(xmm3, xmm3); a.pcmpeqd(xmm4, xmm4); a.pcmpeqd(xmm5, xmm5);
            if (trace) a.cmp(eax, 0);
            a.db(originalBytes);
            Label reject = a.CreateLabel(), collect = a.CreateLabel();
            a.je(reject);
            a.mov(__dword_ptr[r8], 1);
            a.jmp(collect);
            a.Label(ref reject);
            a.mov(__dword_ptr[r8], 2);
            a.Label(ref collect);
            a.mov(__qword_ptr[r8 + 16], rsp);
            a.mov(__qword_ptr[r8 + 24], r11); a.mov(__qword_ptr[r8 + 32], r10);
            a.mov(__qword_ptr[r8 + 40], rcx); a.mov(__qword_ptr[r8 + 48], rdx);
            a.mov(__qword_ptr[r8 + 56], r9); a.mov(__qword_ptr[r8 + 64], rax);
            a.mov(__qword_ptr[r8 + 72], r13);
            a.mov(__qword_ptr[r8 + 80], rbx); a.mov(__qword_ptr[r8 + 88], rbp);
            a.mov(__qword_ptr[r8 + 96], rsi); a.mov(__qword_ptr[r8 + 104], rdi);
            a.mov(__qword_ptr[r8 + 112], r12); a.mov(__qword_ptr[r8 + 120], r14);
            a.mov(__qword_ptr[r8 + 128], r15);
            a.movdqu(__xmmword_ptr[r8 + 144], xmm0); a.movdqu(__xmmword_ptr[r8 + 160], xmm1);
            a.movdqu(__xmmword_ptr[r8 + 176], xmm2); a.movdqu(__xmmword_ptr[r8 + 192], xmm3);
            a.movdqu(__xmmword_ptr[r8 + 208], xmm4); a.movdqu(__xmmword_ptr[r8 + 224], xmm5);
            a.mov(eax, __dword_ptr[r8]);
            a.add(rsp, 64);
            a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12);
            a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx);
            a.ret();
            using (var stream = new MemoryStream())
            { a.Assemble(new StreamCodeWriter(stream), ip); return stream.ToArray(); }
        }

        private static int Find(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                int j = 0; while (j < pattern.Length && data[i + j] == pattern[j]) j++;
                if (j == pattern.Length) return i;
            }
            throw new InvalidOperationException("Harness block not found.");
        }
        private static byte[] Hex(string value) => Array.ConvertAll(value.Split(' '), s => Convert.ToByte(s, 16));
        private static void Require(bool value, string reason)
        { if (!value) throw new InvalidOperationException("Capturer machine regression: " + reason); }
    }
}
