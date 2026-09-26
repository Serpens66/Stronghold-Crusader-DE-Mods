using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Hooks;

namespace KeepCampfireGroundPreserveTest
{
    internal static class Program
    {
        private const string Game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        private static int checks;
        private static int Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) => {
                string path = Path.Combine(Game, @"BepInEx\plugins\000shcdese",
                    new AssemblyName(args.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            try { Run(); Console.WriteLine("Campground hook checks passed: " + checks); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); checks++; }

        private static void Run()
        {
            string path = Path.Combine(Game,
                @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
            byte[] file = File.ReadAllBytes(path);
            byte[] bytes = ReadRva(file, CampgroundVisualGate.HookRva, 64);
            Check(bytes.Take(16).SequenceEqual(CampgroundVisualGate.HookBytes), "native graphic-store bytes");
            Check(bytes[0x38] == 0x8B && bytes[0x39] == 0x74 &&
                bytes[0x3A] == 0x24 && bytes[0x3B] == 0x60, "loop-tail bytes");
            const ulong site = 0x18006F0A0;
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes), site);
            var original = new[] { decoder.Decode(), decoder.Decode() };
            Check(original[1].NextIP == site + 16, "two complete displaced instructions");

            IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (memory == IntPtr.Zero) throw new Exception("VirtualAlloc failed");
            try
            {
                ulong address = unchecked((ulong)memory.ToInt64());
                Marshal.Copy(bytes, 0, memory, bytes.Length);
                using (var probe = new X64InlineHook(address, 16))
                    Check(probe.DisplacedByteCount == 16, "installed RedBird X64InlineHook span");
            }
            finally { VirtualFree(memory, UIntPtr.Zero, 0x8000); }

            var assembler = new Assembler(64);
            CampgroundVisualGate.Generate(assembler, original, site + 16,
                0x180100000, 0x18006F0D8);
            using (var stream = new MemoryStream())
            {
                Check(assembler.TryAssemble(new StreamCodeWriter(stream), 0x180200000,
                    out string error, out _), "Iced assembly: " + error);
                byte[] gate = stream.ToArray();
                var starts = new System.Collections.Generic.HashSet<ulong>();
                var targets = new System.Collections.Generic.List<ulong>();
                for (int offset = 0; offset < gate.Length;)
                {
                    ulong ip = 0x180200000 + (ulong)offset;
                    starts.Add(ip);
                    var item = Decoder.Create(64,
                        new ByteArrayCodeReader(gate.Skip(offset).ToArray()), ip).Decode();
                    Check(!item.IsInvalid && item.Length > 0, "generated instruction decodes");
                    if (offset + 14 <= gate.Length && gate[offset] == 0xFF &&
                        gate[offset + 1] == 0x25 && BitConverter.ToInt32(gate, offset + 2) == 0)
                    {
                        Check(item.Length == 6 && item.FlowControl == FlowControl.IndirectBranch,
                            "RedBird unrestricted-jump encoding");
                        targets.Add(BitConverter.ToUInt64(gate, offset + 6));
                        offset += 14;
                    }
                    else
                    {
                        if (item.FlowControl == FlowControl.ConditionalBranch)
                            targets.Add(item.NearBranchTarget);
                        offset += item.Length;
                    }
                }
                Check(targets.Contains(0x18006F0D8) && targets.Contains(site + 16),
                    "campground skip and Vanilla continuation present");
                Check(targets.Where(t => t >= 0x180200000 && t < 0x180200000 +
                    (ulong)gate.Length).All(starts.Contains), "all local branches land on instructions");
            }
        }

        private static byte[] ReadRva(byte[] file, int rva, int size)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int sections = BitConverter.ToUInt16(file, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < sections; i++)
            {
                int section = table + i * 40;
                int start = BitConverter.ToInt32(file, section + 12);
                int length = BitConverter.ToInt32(file, section + 16);
                if (rva >= start && rva + size <= start + length)
                {
                    byte[] result = new byte[size];
                    Array.Copy(file, BitConverter.ToInt32(file, section + 20) + rva - start,
                        result, 0, size);
                    return result;
                }
            }
            throw new Exception("RVA outside file-backed section");
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size,
            uint allocationType, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
    }
}
