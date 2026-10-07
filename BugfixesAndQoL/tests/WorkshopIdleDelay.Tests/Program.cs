using BugfixesAndQoL;
using Iced.Intel;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static Iced.Intel.AssemblerRegisters;

internal static class Program
{
    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint operation);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RunStub();
    private const int Worker = 1, Workshop = 1, Stockpile = 2, Cow = 2;
    private static int checks;

    private static int Main(string[] args)
    {
        try
        {
            Check(IntPtr.Size == 8, "x64 native test process");
            CheckInstalled(args[0]);
            foreach (bool tanner in new[] { false, true }) RunCases(tanner);
            Console.WriteLine("PASS: workshop idle native execution, RedBird decode, resource predicates and register/flag preservation; checks=" + checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void CheckInstalled(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        using (var sha = SHA256.Create())
            Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == WorkshopIdleDelayNative.NativeSha256, "native hash");
        int pe = BitConverter.ToInt32(file, 0x3C);
        int section = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
        int sections = BitConverter.ToUInt16(file, pe + 6);
        foreach (bool tanner in new[] { false, true })
        {
            int rva = tanner ? WorkshopIdleDelayNative.TannerRva : WorkshopIdleDelayNative.PoleRva;
            int exit = tanner ? WorkshopIdleDelayNative.TannerExitRva : WorkshopIdleDelayNative.PoleExitRva;
            byte[] bytes = tanner ? WorkshopIdleDelayNative.TannerBytes : WorkshopIdleDelayNative.PoleBytes;
            int matches = 0;
            for (int s = 0; s < sections; s++)
            {
                int header = section + s * 40;
                int start = BitConverter.ToInt32(file, header + 12), size = BitConverter.ToInt32(file, header + 16);
                int raw = BitConverter.ToInt32(file, header + 20);
                if ((BitConverter.ToUInt32(file, header + 36) & 0x20000000) == 0) continue;
                for (int i = 0; i <= size - bytes.Length; i++)
                    if (Matches(file, raw + i, bytes))
                    { matches++; Check(start + i == rva, "signature at audited RVA"); }
            }
            Check(matches == 1, "unique executable signature");
            WorkshopIdleDelayNative.ValidateInstructions(WorkshopIdleDelayNative.Decode(bytes, 0x180000000UL + (uint)rva), 0x180000000UL + (uint)exit, tanner);
            IntPtr fixture = Marshal.AllocHGlobal(64);
            try
            {
                Marshal.Copy(bytes, 0, fixture, bytes.Length);
                using (var probe = new X64InlineHook(unchecked((ulong)fixture.ToInt64()), 14))
                    Check(probe.DisplacedByteCount == 17 && !probe.IsInstalled, "actual installed inline backend, 17-byte decode only");
                byte[] after = new byte[bytes.Length]; Marshal.Copy(fixture, after, 0, after.Length);
                Check(after.SequenceEqual(bytes), "decode probe leaves fixture unchanged");
            }
            finally { Marshal.FreeHGlobal(fixture); }
        }
    }

    private static void RunCases(bool tanner)
    {
        using (var f = new Fixture(tanner))
        {
            f.Reset(); f.Expect(0, "no material");
            f.Wood(1); f.CowReady(); f.Expect(1, "own material available");
            f.WorkerWord(0x8F8, 1); f.Expect(0, "dying worker replays Vanilla");
            f.WorkerInt(0x8F8, 0x10000); f.Expect(1, "worker death predicate ignores upper word");
            f.WorkerInt(0x8F8, 0);
            f.Flag(false); f.Expect(0, "disabled exact Vanilla exit");
            f.Flag(true); f.WorkerWord(0x956, 1); f.Expect(0, "ongoing entertainment");
            f.WorkerWord(0x956, 0); f.WorkerWord(0x918, 4); f.Expect(0, "outside state 1");
            f.WorkerWord(0x918, 1); f.WorkerWord(0x990, 0); f.Expect(0, "invalid workshop ID");
            f.WorkerWord(0x990, Workshop); f.WorkerInt(0x9C0, 99); f.Expect(0, "recycled workshop slot");
            f.WorkerInt(0x9C0, 42);
            if (tanner)
            {
                f.CowWord(0x6EE, 2); f.Expect(0, "foreign cow");
                f.CowWord(0x6EE, 1); f.CowWord(0x8F8, 1); f.Expect(0, "cow native exclusion flag");
                f.CowInt(0x8F8, 0x10000); f.Expect(1, "cow predicate reads low short only");
                f.CowWord(0x918, 6); f.Expect(0, "slaughtered cow");
                f.CowWord(0x918, 0); f.CowWord(0x71C, 10100); f.Expect(0, "distance exactly 10000");
                f.CowWord(0x71C, 10099); f.CowWord(0x71E, 10099); f.Expect(1, "Chebyshev, not Manhattan");
                f.CowWord(0x71C, -9900); f.Expect(0, "negative distance exactly -10000");
                f.CowWord(0x71C, -9899); f.Expect(1, "negative signed coordinates");
                f.CowWord(0x6E4, 0); f.Hides(1); f.Expect(1, "workshop hides without cow");
                f.Hides(unchecked((int)0x80000000)); f.Expect(0, "signed hides comparison");
                f.Hides(0); f.UnitsCount(10001); f.Expect(0, "unit scan bound fails closed");
            }
            else
            {
                f.StockpileWord(0x132, 2); f.Expect(0, "foreign stockpile");
                f.StockpileWord(0x132, 1); f.StockpileWord(0x12E, (int)eStructs.STRUCT_ARMOURY); f.Expect(0, "wrong storage type");
                f.StockpileWord(0x12E, (int)eStructs.STRUCT_GOODS_YARD); f.Wood(unchecked((int)0x80000000)); f.Expect(0, "signed wood comparison");
                f.Wood(1); f.BuildingsCount(4001); f.Expect(0, "building scan bound fails closed");
            }
        }
        using (var completed = new Fixture(tanner, 1))
        {
            completed.Reset(); completed.Flag(false); completed.Expect(1, "completed animation, option off", false);
            completed.Flag(true); completed.Expect(1, "completed animation, option on", false);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly bool tanner;
        private readonly IntPtr units, buildings, flag, result, code;
        private readonly RunStub run;
        internal Fixture(bool tanner, int finished = 0)
        {
            this.tanner = tanner;
            units = Marshal.AllocHGlobal(0x660 + 4 * WorkshopIdleDelayNative.UnitStride);
            buildings = Marshal.AllocHGlobal(0x60 + 4 * WorkshopIdleDelayNative.BuildingStride);
            flag = Marshal.AllocHGlobal(4); result = Marshal.AllocHGlobal(96);
            code = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x4000, 0x3000, 0x40);
            Check(code != IntPtr.Zero, "private executable fixture allocation");
            ulong start = Address(code), exit = start + 0x1800;
            var a = new Assembler(64);
            a.push(rbx); a.push(r12); a.push(rdi); a.push(rbp);
            a.mov(rbx, (ulong)WorkshopIdleDelayNative.UnitStride); a.mov(r12, Address(units));
            a.mov(edi, finished); a.mov(ebp, finished);
            a.mov(rcx, 0x123UL); a.mov(rdx, 0x234UL); a.mov(r8, 0x345UL);
            a.mov(r9, 0x456UL); a.mov(r10, 0x567UL); a.mov(r11, 0x678UL);
            a.mov(rax, 0x789UL);
            var original = WorkshopIdleDelayNative.Decode(tanner ? WorkshopIdleDelayNative.TannerBytes : WorkshopIdleDelayNative.PoleBytes, start);
            original[1].NearBranch64 = exit;
            WorkshopIdleDelayNative.Emit(a, original, Address(flag), Address(units), Address(buildings), tanner);
            Finish(a, 1);
            byte[] generated = Assemble(a, start);
            Check(generated.Length < 0x1800, "stub fits private fixture");
            Marshal.Copy(generated, 0, code, generated.Length);
            a = new Assembler(64); Finish(a, 0); generated = Assemble(a, exit);
            Marshal.Copy(generated, 0, IntPtr.Add(code, 0x1800), generated.Length);
            run = (RunStub)Marshal.GetDelegateForFunctionPointer(code, typeof(RunStub));
        }
        private void Finish(Assembler a, int answer)
        {
            a.push(rax); a.mov(rax, Address(result));
            a.mov(__qword_ptr[rax], rcx); a.mov(__qword_ptr[rax + 8], rdx);
            a.mov(__qword_ptr[rax + 16], r8); a.mov(__qword_ptr[rax + 24], r9);
            a.mov(__qword_ptr[rax + 32], r10); a.mov(__qword_ptr[rax + 40], r11);
            a.mov(__qword_ptr[rax + 64], rbx); a.mov(__qword_ptr[rax + 72], r12);
            a.mov(__qword_ptr[rax + 80], rbp); a.mov(__qword_ptr[rax + 88], rdi);
            a.pushfq(); a.pop(rdx); a.mov(__qword_ptr[rax + 48], rdx);
            a.pop(rdx); a.mov(__qword_ptr[rax + 56], rdx);
            a.mov(eax, answer); a.pop(rbp); a.pop(rdi); a.pop(r12); a.pop(rbx); a.ret();
        }
        internal void Reset()
        {
            Marshal.Copy(new byte[0x660 + 4 * WorkshopIdleDelayNative.UnitStride], 0, units, 0x660 + 4 * WorkshopIdleDelayNative.UnitStride);
            Marshal.Copy(new byte[0x60 + 4 * WorkshopIdleDelayNative.BuildingStride], 0, buildings, 0x60 + 4 * WorkshopIdleDelayNative.BuildingStride);
            Flag(true); UnitsCount(3); BuildingsCount(3);
            WorkerWord(0x6E4, (int)AliveState.IsAlive); WorkerWord(0x6E6, (int)(tanner ? eChimps.CHIMP_TYPE_TANNER : eChimps.CHIMP_TYPE_POLETURNER)); WorkerWord(0x6EE, 1);
            WorkerWord(0x918, 1); WorkerWord(0x990, Workshop); WorkerInt(0x9C0, 42);
            WorkerWord(0x71C, 100); WorkerWord(0x71E, 100); WorkerWord(0x9D8, 0x66);
            BuildingWord(Workshop, 0x12C, (int)AliveState.IsAlive); BuildingWord(Workshop, 0x12E, (int)(tanner ? eStructs.STRUCT_TANNERS_WORKSHOP : eStructs.STRUCT_POLETURNERS_WORKSHOP)); BuildingWord(Workshop, 0x132, 1);
            BuildingInt(Workshop, 0x134, 42);
            StockpileWord(0x12C, (int)AliveState.IsAlive); StockpileWord(0x12E, (int)eStructs.STRUCT_GOODS_YARD); StockpileWord(0x132, 1);
        }
        internal void Expect(int answer, string label, bool zero = true)
        {
            byte[] beforeUnits = new byte[0x660 + 4 * WorkshopIdleDelayNative.UnitStride], beforeBuildings = new byte[0x60 + 4 * WorkshopIdleDelayNative.BuildingStride];
            Marshal.Copy(units, beforeUnits, 0, beforeUnits.Length); Marshal.Copy(buildings, beforeBuildings, 0, beforeBuildings.Length);
            Check(run() == answer, (tanner ? "tanner " : "pole ") + label);
            foreach (var pair in new[] { Tuple.Create(0,0x123L),Tuple.Create(8,0x234L),Tuple.Create(16,0x345L),Tuple.Create(24,0x456L),Tuple.Create(32,0x567L),Tuple.Create(40,0x678L) })
                Check(Marshal.ReadInt64(result, pair.Item1) == pair.Item2, "scratch register preserved: " + label);
            // Original test determines ZF; the added material comparison must not leak its flags.
            Check((Marshal.ReadInt64(result, 48) & 0x40) == (zero ? 0x40 : 0), "original ZF preserved: " + label);
            Check((Marshal.ReadInt64(result, 48) & 0x8C5) == (zero ? 0x44 : 0), "original TEST CF/PF/ZF/SF/OF: " + label);
            Check(Marshal.ReadInt64(result, 56) == (answer == 1 ? 0x66 : 0x789), "original RAX/movzx contract");
            Check(Marshal.ReadInt64(result, 64) == WorkshopIdleDelayNative.UnitStride &&
                Marshal.ReadInt64(result, 72) == units.ToInt64() &&
                Marshal.ReadInt64(result, 80) == (zero ? 0 : 1) &&
                Marshal.ReadInt64(result, 88) == (zero ? 0 : 1), "live nonvolatile registers");
            byte[] after = new byte[beforeUnits.Length]; Marshal.Copy(units, after, 0, after.Length);
            Check(beforeUnits.SequenceEqual(after), "unit data read only");
            after = new byte[beforeBuildings.Length]; Marshal.Copy(buildings, after, 0, after.Length);
            Check(beforeBuildings.SequenceEqual(after), "building data read only");
        }
        internal void Flag(bool active) => Marshal.WriteInt32(flag, active ? 1 : 0);
        internal void UnitsCount(int value) => Marshal.WriteInt32(units, value);
        internal void BuildingsCount(int value) => Marshal.WriteInt32(buildings, 0x50, value);
        internal void WorkerWord(int offset, int value) => Marshal.WriteInt16(units, Worker * WorkshopIdleDelayNative.UnitStride + offset, (short)value);
        internal void WorkerInt(int offset, int value) => Marshal.WriteInt32(units, Worker * WorkshopIdleDelayNative.UnitStride + offset, value);
        internal void CowWord(int offset, int value) => Marshal.WriteInt16(units, Cow * WorkshopIdleDelayNative.UnitStride + offset, (short)value);
        internal void CowInt(int offset, int value) => Marshal.WriteInt32(units, Cow * WorkshopIdleDelayNative.UnitStride + offset, value);
        private void BuildingWord(int id, int offset, int value) => Marshal.WriteInt16(buildings, id * WorkshopIdleDelayNative.BuildingStride + offset, (short)value);
        private void BuildingInt(int id, int offset, int value) => Marshal.WriteInt32(buildings, id * WorkshopIdleDelayNative.BuildingStride + offset, value);
        internal void StockpileWord(int offset, int value) => BuildingWord(Stockpile, offset, value);
        internal void Wood(int value) => BuildingInt(Stockpile, 0x184, value);
        internal void Hides(int value) => BuildingInt(Workshop, 0x190, value);
        internal void CowReady() { CowWord(0x6E4, (int)AliveState.IsAlive); CowWord(0x6E6, (int)eChimps.CHIMP_TYPE_COW); CowWord(0x6EE, 1); CowWord(0x71C, 100); CowWord(0x71E, 100); }
        public void Dispose()
        {
            // Private test buffers only; no hook is ever generated or published in this process.
            VirtualFree(code, UIntPtr.Zero, 0x8000);
            Marshal.FreeHGlobal(units); Marshal.FreeHGlobal(buildings); Marshal.FreeHGlobal(flag); Marshal.FreeHGlobal(result);
        }
    }
    private static bool Matches(byte[] file, int offset, byte[] expected)
    {
        for (int i = 0; i < expected.Length; i++) if (file[offset + i] != expected[i]) return false;
        return true;
    }
    private static ulong Address(IntPtr value) => unchecked((ulong)value.ToInt64());
    private static byte[] Assemble(Assembler a, ulong ip)
    {
        using (var stream = new MemoryStream())
        {
            Check(a.TryAssemble(new StreamCodeWriter(stream), ip, out string error, out _), "Iced assemble: " + error);
            byte[] bytes = stream.ToArray(); var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = ip;
            while (decoder.IP < ip + (ulong)bytes.Length) Check(!decoder.Decode().IsInvalid, "complete generated decode");
            return bytes;
        }
    }
    private static void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidOperationException(label); }
}
