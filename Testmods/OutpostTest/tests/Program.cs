using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace OutpostTest
{
    internal static class Program
    {
        private const string Game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        private static int count;
        private static int Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
                string file = new AssemblyName(e.Name).Name + ".dll";
                foreach (string dir in new[] { @"BepInEx\plugins\000shcdese", @"BepInEx\plugins\APIShared_Serp", @"BepInEx\core", @"Stronghold Crusader Definitive Edition_Data\Managed" })
                { string p = Path.Combine(Game, dir, file); if (File.Exists(p)) return Assembly.LoadFrom(p); }
                return null;
            };
            try { Run(); Console.WriteLine($"OutpostTest: {count} checks passed (static/native synthetic tests, not a game test)."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); count++; }
        private static void Run()
        {
            OutpostNative.ValidateLayouts(); count++;
            var s = new OutpostSchedule();
            var e = s.Observe(1, 10, 1, 106, 100);
            e.Adopted = true;
            Check(s.Observe(1, 10, 1, 106, 101).Adopted, "same outpost is adopted only once");
            Check(!OutpostSchedule.TakeWave(e, 299), "no early spawn");
            Check(OutpostSchedule.TakeWave(e, 300), "first 200 ticks");
            Check(!OutpostSchedule.TakeWave(e, 300), "no duplicate wave");
            Check(OutpostSchedule.TakeWave(e, 1500) && e.NextTick == 1700, "no backlog");
            Check(s.Observe(1, 11, 1, 106, 1500).NextTick == 1700, "reused slot resets clock");
            Check(!s.Observe(1, 11, 2, 106, 1600).Adopted, "owner change must re-adopt");
            s.Prune(1700); Check(s.Observe(1, 11, 2, 106, 1700).NextTick == 1900, "deleted outpost removed");
            s.Clear(); Check(s.Observe(1, 11, 2, 106, 0).NextTick == 200, "save load resets timer");
            var wrap = s.Observe(2, 1, 1, 2, int.MaxValue - 100);
            Check(!OutpostSchedule.TakeWave(wrap, int.MinValue + 50), "tick wrap not due");
            Check(OutpostSchedule.TakeWave(wrap, int.MinValue + 99), "tick wrap due");
            Check(OutpostSchedule.IsOutpost(2) && OutpostSchedule.IsOutpost(106) && OutpostSchedule.IsOutpost(107) && !OutpostSchedule.IsOutpost(8), "three variants only");
            int spawned = 0;
            while (spawned < 5 && OutpostSchedule.HasCapacity(1, 98, spawned, 100)) spawned++;
            Check(spawned == 2, "partial wave respects remaining capacity");
            Check(!OutpostSchedule.HasCapacity(1, 100, 0, 100), "full cap no wave");
            Check(OutpostSchedule.HasCapacity(0, 100, 0, 100) && OutpostSchedule.HasCapacity(99, 100, 0, 100), "vanilla mode exceptions");
            byte[] dll = File.ReadAllBytes(Path.Combine(Game, @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"));
            using (var sha = SHA256.Create()) Check(BitConverter.ToString(sha.ComputeHash(dll)).Replace("-", "") == Shared.DebugLogHelper.CurrentNativeSha256, "native hash");
            Check(ReadRva(dll, OutpostGate.Rva, 16).SequenceEqual(OutpostGate.Bytes), "gate bytes");
            byte[] body = ReadRva(dll, 0xABB90, 0xACDE8 - 0xABB90);
            OutpostNative.ValidateControlFlow(body, 0x180000000); count++;
            // An external predecessor targeting the gate's interior must be rejected.
            body[0] = 0xE9;
            Array.Copy(BitConverter.GetBytes(0xABC80 - (0xABB90 + 5)), 0, body, 1, 4);
            bool rejected = false;
            try { OutpostNative.ValidateControlFlow(body, 0x180000000); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "incoming branch regression");
            ExecuteGate();
        }
        private static byte[] ReadRva(byte[] file, int rva, int size)
        {
            int pe = BitConverter.ToInt32(file, 0x3C), n = BitConverter.ToUInt16(file, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < n; i++)
            {
                int p = table + i * 40, start = BitConverter.ToInt32(file, p + 12), raw = BitConverter.ToInt32(file, p + 16);
                if (rva >= start && rva + size <= start + raw)
                { byte[] result = new byte[size]; Array.Copy(file, BitConverter.ToInt32(file, p + 20) + rva - start, result, 0, size); return result; }
            }
            throw new Exception("RVA outside file-backed section");
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Probe(int unused, int mode);
        private static void ExecuteGate()
        {
            IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (memory == IntPtr.Zero) throw new Exception("VirtualAlloc failed");
            try
            {
                ulong b = unchecked((ulong)memory.ToInt64());
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(OutpostGate.Bytes), b + 0x200);
                var original = new[] { decoder.Decode(), decoder.Decode(), decoder.Decode() };
                original[0].MemoryDisplacement64 = b + 0xE20;
                original[1].NearBranch64 = b + 0x600;
                // Real installed RedBird decoder, on copied bytes, without enabling a hook.
                Marshal.Copy(OutpostGate.Bytes, 0, (IntPtr)(b + 0x200), 16);
                using (var probe = new X64InlineHook(b + 0x200, 7))
                    Check(probe.DisplacedByteCount == 16, "installed backend expands 7 to 16");
                var entry = new Assembler(64);
                entry.push(r15); entry.mov(r15d, edx); entry.mov(r11d, 1);
                entry.mov(rax, 0x12345UL); entry.AddUnrestrictedJmp(b + 0x800);
                Emit(entry, b);
                var exit = new Assembler(64); var bad = exit.CreateLabel();
                exit.cmp(rax, 0x12345); exit.jne(bad); exit.mov(eax, 10); exit.pop(r15); exit.ret();
                exit.Label(ref bad); exit.mov(eax, 99); exit.pop(r15); exit.ret(); Emit(exit, b + 0x600);
                var back = new Assembler(64); var zero = back.CreateLabel();
                back.je(zero); back.mov(eax, 21); back.pop(r15); back.ret();
                back.Label(ref zero); back.mov(eax, 20); back.pop(r15); back.ret(); Emit(back, b + 0x210);
                var gate = new Assembler(64);
                OutpostGate.Generate(gate, original, b + 0x210, b + 0xE00, b + 0x600);
                Emit(gate, b + 0x800);
                if (!FlushInstructionCache(GetCurrentProcess(), memory, (UIntPtr)4096)) throw new Exception("FlushInstructionCache failed");
                var run = Marshal.GetDelegateForFunctionPointer<Probe>(memory);
                foreach (int enabled in new[] { 0, 1 })
                foreach (int gameMode in new[] { 0, 1, 6 })
                foreach (int r15Mode in new[] { 0, 99 })
                {
                    Marshal.WriteInt32((IntPtr)(b + 0xE00), enabled);
                    Marshal.WriteInt64((IntPtr)(b + 0xE08), 0);
                    Marshal.WriteInt32((IntPtr)(b + 0xE20), gameMode);
                    int expected = enabled != 0 || gameMode == 1 ? 10 : r15Mode == 0 ? 20 : 21;
                    Check(run(0, r15Mode) == expected, $"executable gate enabled={enabled} editor={gameMode} r15={r15Mode}");
                    Check(Marshal.ReadInt64((IntPtr)(b + 0xE08)) == enabled, "native gate counter");
                }
            }
            finally { VirtualFree(memory, UIntPtr.Zero, 0x8000); }
        }
        private static void Emit(Assembler a, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                if (!a.TryAssemble(new StreamCodeWriter(stream), address, out string error, out _)) throw new Exception(error);
                byte[] bytes = stream.ToArray();
                // RedBird's AddUnrestrictedJmp emits FF 25 00000000 followed by
                // dq(target), not eight additional instruction bytes. Validate
                // every instruction and literal separately (ASLR-independent).
                var boundaries = new System.Collections.Generic.HashSet<ulong>();
                var branches = new System.Collections.Generic.List<ulong>();
                for (int offset = 0; offset < bytes.Length;)
                {
                    boundaries.Add(address + (ulong)offset);
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes.Skip(offset).ToArray()), address + (ulong)offset);
                    var instruction = decoder.Decode();
                    Check(!instruction.IsInvalid, "generated instruction decodes");
                    if (offset + 6 <= bytes.Length && bytes[offset] == 0xFF && bytes[offset+1] == 0x25 &&
                        BitConverter.ToInt32(bytes, offset+2) == 0)
                    {
                        Check(offset + 14 <= bytes.Length && instruction.Length == 6 && instruction.FlowControl == FlowControl.IndirectBranch,
                            "RedBird absolute jump instruction/literal contract");
                        Check(BitConverter.ToUInt64(bytes, offset+6) != 0, "absolute jump target is nonzero");
                        branches.Add(BitConverter.ToUInt64(bytes, offset+6));
                        offset += 14;
                    }
                    else
                    {
                        if (instruction.FlowControl == FlowControl.ConditionalBranch || instruction.FlowControl == FlowControl.UnconditionalBranch)
                            branches.Add(instruction.NearBranchTarget);
                        offset += instruction.Length;
                    }
                }
                foreach (ulong target in branches)
                    if (target >= address && target < address + (ulong)bytes.Length)
                        Check(boundaries.Contains(target), "generated jump does not enter literal/instruction interior");
                Marshal.Copy(bytes, 0, (IntPtr)address, bytes.Length);
            }
        }
        [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protection);
        [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }
}
