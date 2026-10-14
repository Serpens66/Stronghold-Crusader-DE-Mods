using APIShared.GateRouting.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Iced.Intel;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.GateRouting
{
    internal static class NativeTraceContractTests
    {
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        private static readonly List<IntPtr> Pages = new List<IntPtr>();
        private static readonly List<object> Roots = new List<object>();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void HeightProbe(IntPtr unitManager, int unitId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ClosedGateProbe(IntPtr unitManager, int bypass);
        private static unsafe void RunClosedGate(Func<int, int, byte[]> read)
        {
            // Execute installed predicate/clear bytes. 198ED0 is a no-op fixture here;
            // the continuation executes installed DCE60 before returning distinct result markers.
            IntPtr module = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x6800000, 0x3000, 0x40);
            if (module == IntPtr.Zero) throw new Exception("Closed-gate fixture allocation");
            Pages.Add(module); ulong m = (ulong)module.ToInt64();
            void Copy(int rva, byte[] bytes) => Marshal.Copy(bytes, 0, module + rva, bytes.Length);
            Copy(0x1858DF, read(0x1858DF, 0x185934 - 0x1858DF));
            Copy(0x185934, read(0x185934, 0x185973 - 0x185934));
            // Real native step, then distinct success/failure markers. No search is executed.
            Copy(0x185973, new byte[] { 0x85,0xC0,0x74,10,0xB8,2,0,0,0,0x48,0x83,0xC4,56,0xC3,0xB8,3,0,0,0,0x48,0x83,0xC4,56,0xC3 });
            Copy(0xDCE60, read(0xDCE60, 759));
            Copy(0x185D55, read(0x185D55, 20));
            Copy(0x185D69, new byte[] { 0xB8,1,0,0,0,0x48,0x83,0xC4,56,0xC3 });
            Copy(0x198ED0, new byte[] { 0xC3 });
            Copy(0x197950, read(0x197950, 36));
            var a = new Assembler(64);
            a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi); a.push(r12); a.push(r14); a.sub(rsp, 72);
            a.mov(rdi, rcx); a.mov(ebp, edx); a.xor(esi, esi); a.mov(r12, 0x490); a.mov(ebx, 1); a.mov(r11, m);
            a.mov(r14d, 0); a.xor(edx, edx); a.xor(r9d, r9d);
            a.call(m + 0x1000);
            a.add(rsp, 72); a.pop(r14); a.pop(r12); a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx); a.ret();
            using (var stream = new MemoryStream()) { a.Assemble(new StreamCodeWriter(stream), m); Copy(0, stream.ToArray()); }
            var frame = new Assembler(64); frame.sub(rsp,56); frame.jmp(m + 0x1858DF);
            using (var stream = new MemoryStream()) { frame.Assemble(new StreamCodeWriter(stream), m + 0x1000); Copy(0x1000,stream.ToArray()); }
            IntPtr unit = module + 0x67E8400, enabled = Marshal.AllocHGlobal(4); Pages.Add(enabled);
            for (int i = 0; i < 4096; i++) Marshal.WriteByte(unit, i, 0);
            Marshal.WriteInt32(unit, 0x490 + 0x72C, 1);
            Marshal.WriteInt16(module, 0x4B6AA50 + 2, 1);
            Marshal.WriteInt16(module, 0x64CCBB0 + 0x32C + 0x12E, 45);
            Marshal.WriteInt16(module, 0x67E8AE6 + 0x490, 73);
            Marshal.WriteInt32(module, 0x405EDB0, 1); // direction 0/current row 0: source tile 1 -> 2
            Marshal.WriteByte(module, 0x312620, 1); Marshal.WriteByte(module, 0x312624, 16);
            Marshal.WriteInt32(module, 0x48F71B0 + 4, 0x100);
            Marshal.WriteByte(module, 0x4DDD350 + 1, 10);
            int callbacks = 0, decisions = 0; bool permit = false, throwDecision = false;
            CapturerComparisonCallback guard = p => {
                var context = (RedBird.X64.Assembly.X64SmartCPUContext*)p;
                context->R11 = 0; decisions++;
                try { if (throwDecision) throw new InvalidOperationException();
                    if (permit && AssassinGateDescentRule.Allows((int)context->R14, 0,
                        *(ushort*)(context->RDI + context->R12 + 0x752), *(ushort*)(context->RDI + context->R12 + 0x754),
                        (int)context->RDX, (ushort)context->R9, 0, 101, true, 0,0,1,16,0x100,0)) context->R11 = 1;
                } catch (InvalidOperationException) { context->R11 = 0; }
            }; Roots.Add(guard);
            IntPtr diag = Marshal.AllocHGlobal(4); Pages.Add(diag); Marshal.WriteInt32(diag, 1);
            CapturerComparisonCallback cb = p => { callbacks++; }; Roots.Add(cb);
            ulong callback = (ulong)Marshal.GetFunctionPointerForDelegate(cb).ToInt64();
            var hook = new X64InlineHook(m + 0x185D55, 20);
            hook.Generate((asm, original, ret) => AssassinGateDescentAdapter.Emit(asm, original, (ulong)Marshal.GetFunctionPointerForDelegate(guard).ToInt64(), (ulong)enabled.ToInt64(), m + 0x185934, callback, callback, (ulong)diag.ToInt64(), m));
            hook.Enable(); Roots.Add(hook);
            var invoke = (ClosedGateProbe)Marshal.GetDelegateForFunctionPointer(module, typeof(ClosedGateProbe));
            foreach (int gateType in new[] {45,46}) foreach (bool allow in new[] { false, true }) foreach (bool fail in new[] { false, true }) foreach (int detail in new[] { 0,1 })
            foreach (int on in new[] { 0, 1 }) foreach (int closed in new[] { 0, 2 })
            foreach (int raw in new[] { 0, 1 }) foreach (int bypass in new[] { 0, 1 })
            {
                Marshal.WriteInt16(module, 0x64CCBB0 + 0x32C + 0x12E, (short)gateType);
                Marshal.WriteInt32(enabled, on); Marshal.WriteInt32(diag, detail); callbacks = decisions = 0; permit = allow; throwDecision = fail;
                Marshal.WriteInt16(unit, 0x490 + 0x918, 101);
                Marshal.WriteByte(module, 0x64CCBB0 + 0x32C + 0x2FE, (byte)closed);
                Marshal.WriteByte(unit, 0x490 + 0xA5A, (byte)raw);
                Marshal.WriteInt32(unit, 0x490 + 0x752, 0x00100003);
                int result = invoke(unit, bypass);
                bool stop = closed == 2 && raw == 1 && bypass == 0;
                bool skip = stop && on == 1 && allow && !fail;
                if (result != (stop && !skip ? 1 : 2) || Marshal.ReadInt32(unit, 0x490 + 0x752) != (stop && !skip ? 0 : 0x00100003) || callbacks != (stop && !skip && detail == 1 ? 2 : 0) || decisions != (stop && on == 1 ? 1 : 0) ||
                    Marshal.ReadInt16(unit, 0x490 + 0x918) != (stop && !skip ? 101 : 128))
                    throw new Exception("Native closed-gate predicate/clear/observer differs");
            }
            // The actual caller saves/restores the entire unit around its speculative call.
            // Reproduce both observed index/length pairs and retain all original bytes on restore.
            foreach (var pair in new[] { Tuple.Create(16,19), Tuple.Create(11,13) }) {
                Marshal.WriteInt32(enabled, 1); permit = true; throwDecision = false;
                Marshal.WriteInt32(unit, 0x490 + 0x752, pair.Item1 | (pair.Item2 << 16));
                Marshal.WriteInt16(unit, 0x490 + 0x918, 101);
                byte[] saved = new byte[0x490]; Marshal.Copy(unit + 0x490 + 0x65C, saved, 0, saved.Length);
                if (invoke(unit, 1) != 2) throw new Exception("Speculative descent failed");
                Marshal.Copy(saved, 0, unit + 0x490 + 0x65C, saved.Length);
                if (Marshal.ReadInt16(unit, 0x490 + 0x918) != 101 || invoke(unit, 0) != 2 ||
                    Marshal.ReadInt32(unit, 0x490 + 0x752) != (pair.Item1 | (pair.Item2 << 16))) throw new Exception("Restored regular descent failed");
            }
            // Execute the complete installed height-offset reader with controlled terrain.
            // This proves a native data dependency, NOT that these terrain values occurred ingame.
            Copy(0x184FD0, read(0x184FD0, 437));
            var height = (HeightProbe)Marshal.GetDelegateForFunctionPointer(module + 0x184FD0, typeof(HeightProbe));
            Marshal.WriteInt32(module, 0x9302C4, 1);
            Marshal.WriteByte(unit, 0x490 + 0xA5A, 0);
            Marshal.WriteByte(unit, 0x490 + 0xA5B, 0);
            Marshal.WriteInt16(unit, 0x490 + 0xA70, 0);
            Marshal.WriteInt32(unit, 0x490 + 0x72C, 1);
            Marshal.WriteInt16(unit, 0x490 + 0x712, 10);
            height(unit, 1);
            if (Marshal.ReadInt16(unit, 0x490 + 0x714) != 40)
                throw new Exception("Native building/wall presentation offset differs");
            Marshal.WriteInt32(unit, 0x490 + 0x72C, 2);
            Marshal.WriteInt16(unit, 0x490 + 0x712, 0);
            Marshal.WriteInt16(unit, 0x490 + 0xA70, 10);
            height(unit, 1);
            if (Marshal.ReadInt16(unit, 0x490 + 0x714) != 10)
                throw new Exception("Native ground presentation must use climb progress without roof offset");
            Console.WriteLine("PASS: installed 184FD0 separates roof offset (40) from climb progress (10); synthetic terrain, runtime heights still require observation.");
            Console.WriteLine("PASS: real installed closed predicate, 197950 clear and DCE60 descent; functional guard and diagnostics independently on/off; rejection/exception preserves clear; simulated caller restore; 198ED0 unrelated cleanup fixture stubbed.");
        }
        internal static int Run()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "src", "GateRouting", "NativeAssassinDiagnosticHooks.cs"))) root = Directory.GetParent(root).FullName;
            string source = MigrationSources.Read(Path.Combine(root, "src", "GateRouting", "NativeAssassinDiagnosticHooks.cs"));
            int[] sites = Regex.Matches(Regex.Match(source, @"Sites = \{([^}]+)\}").Groups[1].Value, @"0x([0-9A-F]+)")
                .Cast<Match>().Select(m => Convert.ToInt32(m.Groups[1].Value, 16)).ToArray();
            string[] hex = Regex.Matches(Regex.Match(source, "Bytes = \\{([^}]+)\\}").Groups[1].Value, "\"([0-9a-f]+)\"")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            if (sites.Length != 19 || sites.Length != hex.Length) throw new Exception("Incomplete trace contract");
            byte[] image = File.ReadAllBytes(@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
            byte[] Read(int rva, int count)
            {
                int pe = BitConverter.ToInt32(image, 0x3c), sections = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
                for (int i = 0; i < BitConverter.ToUInt16(image, pe + 6); i++)
                {
                    int p = sections + 40 * i, va = BitConverter.ToInt32(image, p + 12), size = BitConverter.ToInt32(image, p + 16), raw = BitConverter.ToInt32(image, p + 20);
                    if (rva >= va && (long)rva + count <= (long)va + size) return image.Skip(raw + rva - va).Take(count).ToArray();
                }
                throw new Exception("Unmapped trace contract");
            }
            var spans = new List<Tuple<int, int>>();
            CapturerComparisonCallback callback = p => { };
            IntPtr enabled = Marshal.AllocHGlobal(4); Pages.Add(enabled); Marshal.WriteInt32(enabled, 0);
            for (int i = 0; i < sites.Length; i++)
            {
                byte[] expected = Enumerable.Range(0, hex[i].Length / 2).Select(n => Convert.ToByte(hex[i].Substring(n * 2, 2), 16)).ToArray();
                if (!Read(sites[i], expected.Length).SequenceEqual(expected)) throw new Exception("Installed trace bytes differ");
                spans.Add(Tuple.Create(sites[i], sites[i] + expected.Length));
                IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40); Pages.Add(page);
                if (page == IntPtr.Zero) throw new Exception("Trace probe allocation");
                Marshal.Copy(expected, 0, page, expected.Length);
                using (var hook = new X64InlineHook((ulong)page.ToInt64(), expected.Length))
                {
                    if (hook.DisplacedByteCount != expected.Length) throw new Exception("Actual backend displacement changed");
                    hook.Generate((a, original, ret) => {
                        ulong cb = (ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64();
                        if (i == 10) AssassinGateDescentAdapter.Emit(a, original, cb, (ulong)enabled.ToInt64(), 0x185934, cb, cb, (ulong)enabled.ToInt64(), 0x180000000);
                        else if (i < 10) NativeCallbackAdapterEmitter.Emit(a, original, cb, (ulong)enabled.ToInt64());
                        else NativeCallbackAdapterEmitter.EmitExecution(a, original, cb, i == 18 ? 0UL : cb,
                            (ulong)enabled.ToInt64(), 0x180000000, i == 14 || i == 15 || i == 17 || i == 18);
                    });
                    // Unpublished probe: generate/assemble through the installed real backend, never enable.
                }
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(expected), (ulong)sites[i]);
                var originalInstructions = new List<Instruction>();
                while (decoder.IP < (ulong)(sites[i] + expected.Length)) originalInstructions.Add(decoder.Decode());
                var assembler = new Assembler(64);
                if (i == 10) AssassinGateDescentAdapter.Emit(assembler, originalInstructions.ToArray(), 0x1000000, 0x2000000, 0x185934, 0x1000000, 0x1000010, 0x2000000, 0x180000000);
                else if (i < 10) NativeCallbackAdapterEmitter.Emit(assembler, originalInstructions.ToArray(), 0x1000000, 0x2000000);
                else NativeCallbackAdapterEmitter.EmitExecution(assembler, originalInstructions.ToArray(), 0x1000000, i == 18 ? 0UL : 0x1000010UL, 0x2000000, 0x180000000, i == 14 || i == 15 || i == 17 || i == 18);
                using (var output = new MemoryStream())
                {
                    assembler.Assemble(new StreamCodeWriter(output), 0x3000000);
                    byte[] stub = output.ToArray(); var decoded = Decoder.Create(64, new ByteArrayCodeReader(stub), 0x3000000);
                    while (decoded.IP < 0x3000000UL + (ulong)stub.Length)
                        if (decoded.Decode().Code == Code.INVALID) throw new Exception("Invalid observer stub");
                    if (decoded.IP != 0x3000000UL + (ulong)stub.Length) throw new Exception("Observer instruction boundary");
                }
            }
            foreach (var body in new[] { Tuple.Create(0xF4930, 0xF4C45), Tuple.Create(0xE1640, 0xE1C2F), Tuple.Create(0x1855A0, 0x186020), Tuple.Create(0x16CD70, 0x16E369) })
            {
                var d = Decoder.Create(64, new ByteArrayCodeReader(Read(body.Item1, body.Item2 - body.Item1)), (ulong)body.Item1);
                while (d.IP < (ulong)body.Item2)
                {
                    var ins = d.Decode(); if (ins.Code == Code.INVALID) throw new Exception("Invalid full native body");
                    if (ins.FlowControl != FlowControl.ConditionalBranch && ins.FlowControl != FlowControl.UnconditionalBranch && ins.FlowControl != FlowControl.Call) continue;
                    foreach (var span in spans)
                        if (ins.NearBranchTarget > (ulong)span.Item1 && ins.NearBranchTarget < (ulong)span.Item2 &&
                            (ins.IP < (ulong)span.Item1 || ins.IP >= (ulong)span.Item2)) throw new Exception("Incoming edge inside trace span");
                }
            }
            GC.KeepAlive(callback);
            RunClosedGate(Read);
            Console.WriteLine("PASS: 19 installed native spans, actual RedBird preparation, full stub decoding and all four complete containing bodies/incoming branches.");
            return 19;
        }
    }
}
