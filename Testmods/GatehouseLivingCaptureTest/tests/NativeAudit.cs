using Iced.Intel;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GatehouseLivingCaptureTest
{
    internal static class NativeAudit
    {
        internal static void Run()
        {
            const string game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            byte[] file = File.ReadAllBytes(Path.Combine(game,
                @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"));
            Require(Hash(file) == NativeDefinition.Sha256, "native DLL hash");
            byte[] handler = ReadRva(file, 0xB73D0, 0x915);
            Require(Hash(handler) == "F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8", "complete gatehouse handler hash");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(handler)); decoder.IP = 0xB73D0;
            bool backedge = false;
            while (decoder.IP < 0xB7CE5)
            {
                decoder.Decode(out Instruction instruction);
                Require(instruction.Code != Code.INVALID && decoder.IP <= 0xB7CE5, "complete handler instruction boundaries");
                if (instruction.Op0Kind != OpKind.NearBranch64) continue;
                ulong target = instruction.NearBranchTarget;
                Require(!(target > NativeDefinition.HookRva && target < NativeDefinition.ContinueRva), "no incoming edge to hook interior");
                if (instruction.IP == 0xB75C2)
                    backedge = instruction.Mnemonic == Mnemonic.Jne && target == NativeDefinition.HookRva;
            }
            Require(backedge, "tile-chain backedge enters hook start");
            byte[] block = ReadRva(file, NativeDefinition.HookRva, 20);
            Require(block.Take(18).SequenceEqual(NativeDefinition.Original) && block[18] == 0x74 && block[19] == 0x63, "block and original JE");
            // The semantically guarded fallback must be unique in executable PE sections.
            string[] pattern = NativeDefinition.Pattern.Split(' ');
            int pe = BitConverter.ToInt32(file, 0x3C), sections = BitConverter.ToUInt16(file, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(file, pe + 20), matches = 0;
            for (int section = 0; section < sections; section++)
            {
                int header = table + section * 40;
                if ((BitConverter.ToUInt32(file, header + 36) & 0x20000000) == 0) continue;
                int size = BitConverter.ToInt32(file, header + 16), raw = BitConverter.ToInt32(file, header + 20);
                for (int start = raw; start <= raw + size - pattern.Length; start++)
                {
                    int i = 0;
                    while (i < pattern.Length && (pattern[i] == "?" || file[start + i] == Convert.ToByte(pattern[i], 16))) i++;
                    if (i == pattern.Length) matches++;
                }
            }
            Require(matches == 1, "unique executable-section fallback");
            Console.WriteLine("PASS: exact native/full-handler hashes, all handler branch boundaries, chain backedge, unique executable signature.");
        }
        private static byte[] ReadRva(byte[] file, int rva, int length)
        {
            int pe = BitConverter.ToInt32(file, 0x3C), count = BitConverter.ToUInt16(file, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < count; i++)
            {
                int header = table + i * 40, start = BitConverter.ToInt32(file, header + 12);
                int size = BitConverter.ToInt32(file, header + 16);
                if (rva < start || rva + length > start + size) continue;
                int offset = BitConverter.ToInt32(file, header + 20) + rva - start;
                byte[] result = new byte[length]; Buffer.BlockCopy(file, offset, result, 0, length); return result;
            }
            throw new InvalidOperationException("RVA outside file-backed section.");
        }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Require(bool valid, string reason)
        { if (!valid) throw new InvalidOperationException("Native capture audit failed: " + reason); }
    }
}
