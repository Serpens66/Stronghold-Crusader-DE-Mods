using Iced.Intel;
using System;
using System.IO;
using System.Security.Cryptography;

namespace SkirmishGameOptionsTest
{
    internal static class Program
    {
        private static int assertions;

        private static int Main()
        {
            TestWorkingCopyRouting();
            TestGameplayGuards();
            TestAdvancedFlagConversion();
            TestNoDogsNativeContract();
            TestInstalledNativeBinary();
            Console.WriteLine(
                $"SkirmishGameOptionsTest tests: {assertions} assertions passed.");
            return 0;
        }

        private static void TestWorkingCopyRouting()
        {
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Fairness3"),
                "fairness is transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("GameType2"),
                "starting goods are transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Settings_ExtremePowers"),
                "extreme powers are transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Settings_Adv_NoGold"),
                "gameplay options are transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("STRUCT_CHURCH"),
                "building availability is transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("TROOPS_31"),
                "troop availability is transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("GOODS_24"),
                "trade availability is transactional");
            Assert(!SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Setup"),
                "open has dedicated handling");
            Assert(!SkirmishGameOptionsPolicy.IsWorkingCopyCommand("ApplySettings"),
                "apply has dedicated handling");
            Assert(!SkirmishGameOptionsPolicy.IsWorkingCopyCommand("CancelSettings"),
                "cancel has dedicated handling");
        }

        private static void TestGameplayGuards()
        {
            Assert(SkirmishGameOptionsPolicy.ShouldBlockCow(true, true),
                "cow action blocked in local Skirmish");
            Assert(!SkirmishGameOptionsPolicy.ShouldBlockCow(false, true),
                "cow action untouched outside local Skirmish");
            Assert(SkirmishGameOptionsPolicy.ShouldBlockAutoTrading(true, false),
                "auto trading blocked in local Skirmish");
            Assert(!SkirmishGameOptionsPolicy.ShouldBlockAutoTrading(true, true),
                "allowed auto trading remains available");
        }

        private static void TestAdvancedFlagConversion()
        {
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(0) == 0,
                "disabled advanced options remain disabled");
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(1) == 1,
                "enabled advanced options select mode-99 flag");
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(7) == 1,
                "nonzero advanced value normalizes to one");
        }

        private static void TestNoDogsNativeContract()
        {
            byte[] image = new byte[NoDogsNativeContract.PatchRva + 16];
            Array.Copy(
                NoDogsNativeContract.ExpectedBytes,
                0,
                image,
                NoDogsNativeContract.PatchRva,
                NoDogsNativeContract.ExpectedBytes.Length);
            NoDogsNativeContract.Validate(
                image,
                NoDogsNativeContract.PreferredImageBase,
                referenceHashMatches: true);
            assertions++;

            var vanillaDecoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(NoDogsNativeContract.ExpectedBytes));
            vanillaDecoder.IP = NoDogsNativeContract.PreferredImageBase +
                NoDogsNativeContract.PatchRva;
            Assert(vanillaDecoder.Decode().FlowControl == FlowControl.ConditionalBranch,
                "Vanilla bytes decode as the audited mode-99 bypass branch");

            var patchedDecoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(NoDogsNativeContract.ReplacementBytes));
            patchedDecoder.IP = NoDogsNativeContract.PreferredImageBase +
                NoDogsNativeContract.PatchRva;
            Instruction first = patchedDecoder.Decode();
            Instruction second = patchedDecoder.Decode();
            Assert(first.Mnemonic == Mnemonic.Nop && second.Mnemonic == Mnemonic.Nop,
                "replacement decodes completely as two one-byte NOPs");

            image[NoDogsNativeContract.PatchRva] = 0x75;
            AssertThrows(
                () => NoDogsNativeContract.Validate(
                    image,
                    NoDogsNativeContract.PreferredImageBase,
                    referenceHashMatches: true),
                "changed native bytes fail closed");
            AssertThrows(
                () => NoDogsNativeContract.Validate(
                    image,
                    NoDogsNativeContract.PreferredImageBase,
                    referenceHashMatches: false),
                "unknown native hash fails closed");
        }

        private static void TestInstalledNativeBinary()
        {
            string gameDirectory = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string nativePath = Path.Combine(
                gameDirectory,
                @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
            Assert(File.Exists(nativePath), "installed native DLL exists");

            byte[] file = File.ReadAllBytes(nativePath);
            string hash;
            using (SHA256 sha256 = SHA256.Create())
                hash = BitConverter.ToString(sha256.ComputeHash(file)).Replace("-", string.Empty);
            Assert(
                string.Equals(hash, NoDogsNativeContract.ReferenceSha256, StringComparison.Ordinal),
                "installed native DLL hash matches audited baseline");

            int fileOffset = RvaToFileOffset(file, NoDogsNativeContract.PatchRva);
            byte[] bytes = new byte[NoDogsNativeContract.ExpectedBytes.Length];
            Array.Copy(file, fileOffset, bytes, 0, bytes.Length);
            Assert(
                bytes[0] == NoDogsNativeContract.ExpectedBytes[0] &&
                bytes[1] == NoDogsNativeContract.ExpectedBytes[1],
                "installed native byte window matches audited No Dogs branch");

            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = NoDogsNativeContract.PreferredImageBase + NoDogsNativeContract.PatchRva;
            Instruction instruction = decoder.Decode();
            Assert(
                instruction.Code == Code.Je_rel8_64 && instruction.Length == bytes.Length,
                "installed native bytes decode as one complete JE rel8 instruction");
        }

        private static int RvaToFileOffset(byte[] image, int rva)
        {
            using (var stream = new MemoryStream(image, writable: false))
            using (var reader = new BinaryReader(stream))
            {
                stream.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550)
                    throw new InvalidOperationException("Installed native DLL has no PE signature.");

                stream.Position = peOffset + 6;
                ushort sectionCount = reader.ReadUInt16();
                stream.Position = peOffset + 20;
                ushort optionalHeaderSize = reader.ReadUInt16();
                long sectionTable = peOffset + 24L + optionalHeaderSize;

                for (int section = 0; section < sectionCount; section++)
                {
                    stream.Position = sectionTable + section * 40L + 8;
                    uint virtualSize = reader.ReadUInt32();
                    uint virtualAddress = reader.ReadUInt32();
                    uint rawSize = reader.ReadUInt32();
                    uint rawPointer = reader.ReadUInt32();
                    uint mappedSize = Math.Max(virtualSize, rawSize);
                    if ((uint)rva >= virtualAddress && (uint)rva < virtualAddress + mappedSize)
                        return checked((int)(rawPointer + (uint)rva - virtualAddress));
                }
            }

            throw new InvalidOperationException($"RVA 0x{rva:X} is outside the installed PE sections.");
        }

        private static void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertThrows(Action action, string message)
        {
            assertions++;
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
