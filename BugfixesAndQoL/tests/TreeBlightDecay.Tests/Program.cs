using BugfixesAndQoL.TreeBlightDecay;
using Iced.Intel;
using RedBird.X64.Hooks;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static Iced.Intel.AssemblerRegisters;

// Executes the production assembler in private memory, never in the game process.
// The explicit native offsets and branch/flag expectations are the regression contract.
internal static class Program
{
    [DllImport("kernel32", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr n, uint a, uint protect);
    [DllImport("kernel32")] private static extern bool VirtualFree(IntPtr p, UIntPtr n, uint a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Run();
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static ulong Address(IntPtr p) => unchecked((ulong)p.ToInt64());

    private static int Main(string[] args)
    {
        try
        {
            string dll = args.Length > 0 ? args[0] : @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
            byte[] pe = File.ReadAllBytes(dll);
            using (var hash = SHA256.Create()) Check(BitConverter.ToString(hash.ComputeHash(pe)).Replace("-", "") == NativeCode.Hash, "canonical native hash");
            Check(Read(pe, NativeCode.EventRva, 14).SequenceEqual(NativeCode.EventBytes), "event reference bytes");
            Check(Read(pe, NativeCode.GrowthRva, 14).SequenceEqual(NativeCode.GrowthBytes), "growth reference bytes");
            Check(Read(pe, NativeCode.EventRva + 14, 2).SequenceEqual(NativeCode.EventFollowing), "event first continuation instruction");
            Check(Read(pe, NativeCode.GrowthRva + 14, 7).SequenceEqual(NativeCode.GrowthFollowing), "growth first continuation instruction");
            foreach (var f in new[] { Tuple.Create(0x107A00, 0x7D), Tuple.Create(0x108320, 0x33A) })
                foreach (var i in NativeCode.Decode(Read(pe, f.Item1, f.Item2), 0x180000000UL + (uint)f.Item1))
                    if (i.FlowControl == FlowControl.ConditionalBranch || i.FlowControl == FlowControl.UnconditionalBranch || i.FlowControl == FlowControl.Call)
                        foreach (int site in new[] { NativeCode.EventRva, NativeCode.GrowthRva })
                            Check(!(i.NearBranchTarget > 0x180000000UL + (uint)site && i.NearBranchTarget < 0x180000000UL + (uint)site + 14), "no incoming interior branch");
            for (int n = 0; n < 15; n++)
            {
                uint target = BitConverter.ToUInt32(Read(pe, 0x10865C + n * 4, 4), 0);
                Check(!(target > NativeCode.GrowthRva && target < NativeCode.GrowthRva + 14), "growth switch has no interior target");
            }
            byte[] table = Read(pe, NativeCode.TableRva, 20 * 7 * 4);
            for (int t = 1; t <= 4; t++) Check(BitConverter.ToInt32(table, (t * 7 + 5) * 4) == (t <= 2 ? 120 : t == 3 ? 100 : 80), "audited stage-5 threshold");
            foreach (bool growth in new[] { false, true })
            using (var f = new Fixture(growth, table))
            {
                for (int type = 1; type <= 4; type++)
                foreach (int health in new[] { 0, 2, 5 })
                foreach (int felled in new[] { 0, 1, 2 })
                foreach (int progress in new[] { 0, 48, type <= 2 ? 120 : type == 3 ? 100 : 80, 127 })
                {
                    f.Reset(type, 5, progress, health, felled);
                    f.Expect(growth ? 2 : 1, Math.Max(progress, f.Begin(type)), "dead-tree state matrix");
                }
                foreach (int type in new[] { 0, 5, 10, 15, 19 })
                { f.Reset(type); f.Expect(1, 1, "non-woodland vegetation"); }
                foreach (int stage in new[] { 0, 1, 2, 3, 4, 6 })
                { f.Reset(1, stage); f.Expect(1, 1, "non-dead stage"); }
                foreach (int mode in new[] { 0, 2, 3, 4, 11, 13 })
                { f.Reset(1); f.Mode(mode); f.Expect(growth ? 2 : 1, 120, "native gameplay mode " + mode); }
                foreach (int mode in new[] { 1, 6 })
                { f.Reset(1); f.Mode(mode); f.Expect(1, 1, "editor mode unchanged"); }
                f.Reset(1); f.Flag(false); f.Expect(1, 1, "disabled option");
                f.Reset(1, 3, 1, 2, 0); f.Expect(growth ? 0 : 1, 1, "living damaged tree retains health gate");
                f.Reset(1, 3, 1, 0, 1); f.Expect(growth ? 0 : 1, 1, "ordinary felled wood retains gate");
                f.Reset(1); f.Alive(0); f.Expect(1, 1, "unused slot");
                if (growth)
                {
                    f.Reset(1); f.ManagerEnabled(false); f.Expect(1, 1, "native manager disabled");
                    f.Reset(1); f.Screen(12); f.Expect(1, 1, "native frontend gate");
                    f.Reset(1); f.Alive(3); f.Expect(1, 1, "terminal slot untouched");
                }
                // These slots represent the actual per-event pool bound, independent of 800x800 tile count.
                f.Reset(1); f.Benchmark();
            }
            Console.WriteLine("PASS: TreeBlightDecay production native stubs, state matrices, register/flags/stack/SIMD and PE contracts: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static byte[] Read(byte[] pe, int rva, int count)
    {
        int header = BitConverter.ToInt32(pe, 0x3C), sections = BitConverter.ToUInt16(pe, header + 6);
        int section = header + 24 + BitConverter.ToUInt16(pe, header + 20);
        for (int n = 0; n < sections; n++, section += 40)
        {
            int start = BitConverter.ToInt32(pe, section + 12), size = BitConverter.ToInt32(pe, section + 16);
            if (rva >= start && (long)rva + count <= (long)start + size)
                return pe.Skip(BitConverter.ToInt32(pe, section + 20) + rva - start).Take(count).ToArray();
        }
        throw new Exception("Unmapped native RVA " + rva.ToString("X"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly bool growth;
        private readonly byte[] tableBytes;
        private readonly IntPtr record, manager, state, table, mode, screen, result, code;
        private readonly Run run;
        private int felled, health;
        internal Fixture(bool growth, byte[] nativeTable)
        {
            this.growth = growth; tableBytes = nativeTable;
            record = Marshal.AllocHGlobal(0x200); manager = Marshal.AllocHGlobal(32); state = Marshal.AllocHGlobal(32);
            table = Marshal.AllocHGlobal(nativeTable.Length); mode = Marshal.AllocHGlobal(4); screen = Marshal.AllocHGlobal(4); result = Marshal.AllocHGlobal(192);
            Marshal.Copy(nativeTable, 0, table, nativeTable.Length);
            Marshal.Copy(new byte[32], 0, state, 32);
            Marshal.WriteInt64(state, 8, table.ToInt64()); Marshal.WriteInt64(state, 16, mode.ToInt64()); Marshal.WriteInt64(state, 24, screen.ToInt64());
            code = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x4000, 0x3000, 0x40); Check(code != IntPtr.Zero, "private test code");
            ulong ip = Address(code), fail = ip + 0x1800, bypass = ip + 0x1000;
            // Test the real backend's minimum span without installing a hook.
            Marshal.Copy(growth ? NativeCode.GrowthBytes : NativeCode.EventBytes, 0, code, 14);
            using (var probe = new X64InlineHook(ip, 14)) Check(probe.DisplacedByteCount == 14 && !probe.IsInstalled, "installed RedBird 14-byte contract");
            var a = new Assembler(64);
            a.push(rbx); a.push(rdi); a.push(rsi); a.push(rbp); a.push(r12); a.push(r13); a.push(r14); a.push(r15);
            a.mov(rbx, Address(record)); a.mov(rdi, Address(manager)); a.mov(rsi, 0xABCDUL); a.mov(rbp, 0xBCDEUL);
            a.mov(r12, 0xCDEFUL); a.mov(r13, 0xDEF0UL); a.mov(r14, 0xEF01UL); a.mov(r15, 0xF012UL);
            a.mov(rax, Address(result)); a.mov(__qword_ptr[rax + 112], rsp);
            a.mov(rax, 0x789UL); a.mov(rcx, 0x123UL); a.mov(rdx, 0x234UL);
            a.mov(r8, Address(record) + 0x6A); a.mov(r9d, 1); a.mov(r10, Address(manager)); a.mov(r11, 0x678UL);
            a.movq(xmm0, r11); a.push(0xED7); a.popfq();
            var original = NativeCode.Decode(growth ? NativeCode.GrowthBytes : NativeCode.EventBytes, ip);
            if (growth) original[1].NearBranch64 = fail;
            NativeCode.Emit(a, growth, Address(state), bypass, original);
            if (growth)
            {
                // Unmodified Vanilla health instructions following the displaced block.
                a.movzx(eax, __word_ptr[rbx + 0x98]); a.cmp(__word_ptr[rbx + 0x96], ax); a.jl(fail);
            }
            Finish(a, 1); Write(a, ip);
            a = new Assembler(64); Finish(a, 2); Write(a, bypass);
            a = new Assembler(64); Finish(a, 0); Write(a, fail);
            run = (Run)Marshal.GetDelegateForFunctionPointer(code, typeof(Run));
        }

        private void Write(Assembler a, ulong ip)
        {
            byte[] b = NativeCode.Assemble(a, ip); Check(b.Length < 0x1000, "bounded fixture code");
            Marshal.Copy(b, 0, new IntPtr(unchecked((long)ip)), b.Length);
        }

        private void Finish(Assembler a, int answer)
        {
            a.push(rax); a.mov(rax, Address(result));
            a.mov(__qword_ptr[rax], rcx); a.mov(__qword_ptr[rax + 8], rdx); a.mov(__qword_ptr[rax + 16], r8);
            a.mov(__qword_ptr[rax + 24], r9); a.mov(__qword_ptr[rax + 32], r10); a.mov(__qword_ptr[rax + 40], r11);
            a.pushfq(); a.pop(rdx); a.mov(__qword_ptr[rax + 48], rdx); a.pop(rdx); a.mov(__qword_ptr[rax + 56], rdx);
            a.mov(__qword_ptr[rax + 64], rbx); a.mov(__qword_ptr[rax + 72], rdi); a.mov(__qword_ptr[rax + 80], rsi);
            a.mov(__qword_ptr[rax + 88], rbp); a.mov(__qword_ptr[rax + 96], r12); a.mov(__qword_ptr[rax + 104], r13);
            a.mov(__qword_ptr[rax + 120], rsp); a.mov(__qword_ptr[rax + 128], r14); a.mov(__qword_ptr[rax + 136], r15);
            a.movdqu(__[rax + 144], xmm0); a.cld();
            a.mov(eax, answer); a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12); a.pop(rbp); a.pop(rsi); a.pop(rdi); a.pop(rbx); a.ret();
        }

        internal int Begin(int type) => BitConverter.ToInt32(tableBytes, (type * 7 + 5) * 4);
        internal void Reset(int type, int stage = 5, int progress = 1, int hp = 5, int fell = 0)
        {
            Marshal.Copy(new byte[0x200], 0, record, 0x200); Marshal.Copy(new byte[32], 0, manager, 32);
            Flag(true); Mode(3); Screen(40); ManagerEnabled(true); Marshal.WriteInt32(manager, 16, 3);
            Alive(2); Word(0x6A, type); Word(0x96, hp); Word(0x98, 5); Word(0x9A, fell); Word(0x9C, 9);
            Marshal.WriteInt32(record, 0xA4, stage); Marshal.WriteInt32(record, 0xA8, progress);
            health = hp; felled = fell;
        }
        internal void Flag(bool v) => Marshal.WriteInt32(state, v ? 1 : 0);
        internal void Mode(int v) => Marshal.WriteInt32(mode, v);
        internal void Screen(int v) => Marshal.WriteInt32(screen, v);
        internal void Alive(int v) => Word(0x68, v);
        internal void ManagerEnabled(bool v) => Marshal.WriteInt32(manager, 4, v ? 1 : 0);
        private void Word(int offset, int v) => Marshal.WriteInt16(record, offset, (short)v);
        internal void Expect(int answer, int progress, string label)
        {
            byte[] before = new byte[0x200]; Marshal.Copy(record, before, 0, before.Length);
            Check(run() == answer, label + " branch");
            Check(Marshal.ReadInt32(record, 0xA8) == progress, label + " progress");
            byte[] after = new byte[0x200]; Marshal.Copy(record, after, 0, after.Length);
            Array.Copy(before, 0xA8, after, 0xA8, 4); Check(before.SequenceEqual(after), label + " only progress may change");
            long[] expected = { 0x123,0x234,unchecked((long)Address(record)) + 0x6A + (growth ? 0 : 0x9C),growth ? 1 : 2,manager.ToInt64(),0x678 };
            for (int n = 0; n < expected.Length; n++) Check(Marshal.ReadInt64(result, n * 8) == expected[n], label + " preserved register " + n);
            Check(Marshal.ReadInt64(result, 64) == record.ToInt64() && Marshal.ReadInt64(result, 72) == manager.ToInt64(), label + " record/manager bases");
            foreach (var pair in new[] { Tuple.Create(80,0xABCDL),Tuple.Create(88,0xBCDEL),Tuple.Create(96,0xCDEFL),Tuple.Create(104,0xDEF0L),Tuple.Create(128,0xEF01L),Tuple.Create(136,0xF012L) })
                Check(Marshal.ReadInt64(result, pair.Item1) == pair.Item2, label + " preserved nonvolatile");
            Check(Marshal.ReadInt64(result, 112) == Marshal.ReadInt64(result, 120), label + " balanced stack");
            Check(Marshal.ReadInt64(result, 144) == 0x678 && Marshal.ReadInt64(result, 152) == 0, label + " SIMD untouched");
            long flags = !growth ? 0x495 : answer == 2 ? 0xCD5 : (felled == 0 && health >= 5) ? 0x444 : (felled != 0 ? 0x400 : 0x491);
            Check((Marshal.ReadInt64(result, 48) & 0xCD5) == flags, label + " exact arithmetic/DF flags");
            Check(Marshal.ReadInt64(result, 56) == (growth && answer != 2 && felled == 0 ? 5 : 0x789), label + " original RAX contract");
        }

        internal void Benchmark()
        {
            for (int i = 0; i < 10000; i++) run();
            for (int pass = 0; pass < 4; pass++)
            {
                Flag(pass % 2 != 0); var timer = Stopwatch.StartNew();
                for (int i = 0; i < 200000; i++) run(); timer.Stop();
                Console.WriteLine($"FIXTURE_PERF growth={growth}; enabled={pass % 2 != 0}; 3999 callbacks upper estimate ms={timer.Elapsed.TotalMilliseconds / 200000 * 3999:F3}; includes test delegate/harness overhead");
            }
        }
        public void Dispose()
        {
            VirtualFree(code, UIntPtr.Zero, 0x8000);
            foreach (IntPtr p in new[] { record,manager,state,table,mode,screen,result }) Marshal.FreeHGlobal(p);
        }
    }
}
