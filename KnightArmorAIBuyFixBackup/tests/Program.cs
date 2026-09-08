using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace KnightArmorAIBuyFixBackup
{
    internal static class Program
    {
        private const string DllPath =
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private static int assertions;

        private static int Main()
        {
            TestManagedContracts();
            TestSourceContract();
            TestCanonicalNativePattern();
            Console.WriteLine($"KnightArmorAIBuyFixBackup tests: {assertions} assertions passed.");
            return 0;
        }

        private static void TestManagedContracts()
        {
            Check((int)eGoods.STORED_NULL == 0, "STORED_NULL value");
            Check((int)eGoods.STORED_SWORDS == 22, "STORED_SWORDS value");
            Check((int)eGoods.STORED_METAL_ARMOUR == 24, "STORED_METAL_ARMOUR value");
            Check((int)eGoods.Count == 25, "eGoods Count value");
            Check(Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.r_RecruitmentResultFailureReason)).ToInt32() == 0x650,
                "recruitment failure offset");
            Check(Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.r_RecruitmentResultMissingGoodId)).ToInt32() == 0x654,
                "missing-good offset");
            Check(Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.EmptyUnitFillValue)).ToInt32() == 0x658,
                "empty-fill offset");
            Check(Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.LastOrderedUnit)).ToInt32() == 0x65C,
                "LastOrderedUnit offset");
            Check(Marshal.SizeOf(typeof(GameUnitManager)) == 0xF7C, "GameUnitManager size");
        }

        private static void TestSourceContract()
        {
            string root = Directory.GetCurrentDirectory();
            string runtime = File.ReadAllText(Path.Combine(root, "src", "KnightArmorAIBuyFixBackupRuntime.cs"));
            string plugin = File.ReadAllText(Path.Combine(root, "src", "KnightArmorAIBuyFixBackupPlugin.cs"));
            string project = File.ReadAllText(Path.Combine(root, "KnightArmorAIBuyFixBackup.csproj"));
            string assemblyInfo = File.ReadAllText(Path.Combine(root, "Properties", "AssemblyInfo.cs"));
            Check(runtime.Contains("HookTarget.FromAddress"), "shared recruitment entry is detoured");
            Check(runtime.Contains("(int)eGoods.STORED_NULL"), "missing-good output uses the enum sentinel");
            Check(!runtime.Contains("x >=") && !runtime.Contains("x < 25"), "no consumer-specific numeric range");
            Check(runtime.Contains("TransactionFailureMode.RollbackAndThrow") && runtime.Contains("OwnsHooks = true"),
                "atomic owned hook transaction");
            Check(runtime.Contains("if (!referenceHashMatches)"), "unknown DLL hashes fail closed");
            Check(!plugin.Contains("ConfigEntry") && !plugin.Contains("RegisterLobbyModSettings"), "no mod settings");
            Check(project.Contains("Properties\\AssemblyInfo.cs") &&
                  assemblyInfo.Contains("AssemblyVersion(\"0.1.0.0\")") &&
                  assemblyInfo.Contains("AssemblyFileVersion(\"0.1.0.0\")") &&
                  assemblyInfo.Contains("AssemblyInformationalVersion(\"0.1.0\")"),
                "assembly versions match the plugin and manifest version");
        }

        private static void TestCanonicalNativePattern()
        {
            byte[] file = File.ReadAllBytes(DllPath);
            Check(Hash(file) == KnightArmorAIBuyFixNativeDefinition.ReferenceSha256, "canonical DLL hash");
            PeImage image = PeImage.Load(file);
            PatternToken[] pattern = ParsePattern(KnightArmorAIBuyFixNativeDefinition.RecruitEuropeanUnitPattern);
            List<int> matches = image.FindExecutableMatches(pattern);
            Check(matches.Count == 1, "recruitment entry pattern is unique");
            Check(matches[0] == KnightArmorAIBuyFixNativeDefinition.RecruitEuropeanUnitRva,
                "recruitment entry pattern resolves to the audited RVA");
        }

        private static PatternToken[] ParsePattern(string pattern)
        {
            string[] parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            PatternToken[] result = new PatternToken[parts.Length];
            for (int index = 0; index < parts.Length; index++)
                result[index] = parts[index] == "??" ? new PatternToken(0, true) : new PatternToken(Convert.ToByte(parts[index], 16), false);
            return result;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct PatternToken
        {
            internal PatternToken(byte value, bool wildcard)
            {
                Value = value;
                Wildcard = wildcard;
            }

            internal byte Value { get; }
            internal bool Wildcard { get; }
        }

        private sealed class PeImage
        {
            private readonly byte[] image;
            private readonly List<Section> executableSections;

            private PeImage(byte[] image, List<Section> executableSections)
            {
                this.image = image;
                this.executableSections = executableSections;
            }

            internal static PeImage Load(byte[] file)
            {
                int peOffset = BitConverter.ToInt32(file, 0x3C);
                ushort sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
                ushort optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
                int optionalHeader = peOffset + 24;
                byte[] image = new byte[BitConverter.ToInt32(file, optionalHeader + 56)];
                int headerSize = BitConverter.ToInt32(file, optionalHeader + 60);
                Buffer.BlockCopy(file, 0, image, 0, Math.Min(headerSize, file.Length));
                int sectionTable = optionalHeader + optionalHeaderSize;
                List<Section> executable = new List<Section>();
                for (int index = 0; index < sectionCount; index++)
                {
                    int entry = sectionTable + index * 40;
                    int virtualSize = BitConverter.ToInt32(file, entry + 8);
                    int virtualAddress = BitConverter.ToInt32(file, entry + 12);
                    int rawSize = BitConverter.ToInt32(file, entry + 16);
                    int rawOffset = BitConverter.ToInt32(file, entry + 20);
                    int copyLength = Math.Min(rawSize, Math.Min(file.Length - rawOffset, image.Length - virtualAddress));
                    if (copyLength > 0)
                        Buffer.BlockCopy(file, rawOffset, image, virtualAddress, copyLength);
                    uint characteristics = BitConverter.ToUInt32(file, entry + 36);
                    if ((characteristics & 0x20000000u) != 0)
                        executable.Add(new Section(virtualAddress, Math.Max(virtualSize, rawSize)));
                }
                return new PeImage(image, executable);
            }

            internal List<int> FindExecutableMatches(PatternToken[] pattern)
            {
                List<int> matches = new List<int>();
                foreach (Section section in executableSections)
                {
                    int end = Math.Min(image.Length, section.Start + section.Length) - pattern.Length;
                    for (int offset = section.Start; offset <= end; offset++)
                    {
                        bool match = true;
                        for (int index = 0; index < pattern.Length; index++)
                        {
                            if (!pattern[index].Wildcard && image[offset + index] != pattern[index].Value)
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                            matches.Add(offset);
                    }
                }
                return matches;
            }

            private readonly struct Section
            {
                internal Section(int start, int length)
                {
                    Start = start;
                    Length = length;
                }

                internal int Start { get; }
                internal int Length { get; }
            }
        }
    }
}
