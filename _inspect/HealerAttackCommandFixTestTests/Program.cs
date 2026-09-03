using HealerAttackCommandFixTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

internal static class Program
{
    private const string ExpectedHash =
        "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const string DllPath =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";

    private static int assertions;

    private static int Main()
    {
        try
        {
            byte[] file = File.ReadAllBytes(DllPath);
            Check(Hash(file) == ExpectedHash, "canonical native hash");
            PeImage pe = PeImage.Load(file);
            byte[] image = pe.Image;

            CheckPattern(pe, HealerAttackCommandFixNativeDefinition.FirstClassifierPattern,
                HealerAttackCommandFixNativeDefinition.FirstClassifierRva,
                "first AttackUnit classifier signature");
            CheckPattern(pe, HealerAttackCommandFixNativeDefinition.SecondClassifierPattern,
                HealerAttackCommandFixNativeDefinition.SecondClassifierRva,
                "second AttackUnit classifier signature");
            CheckResolvedTables(image);
            CheckClassificationContracts(image);
            CheckSourceContracts(FindWorkspace());

            Console.WriteLine($"PASS: HealerAttackCommandFixTest tests ({assertions} assertions).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void CheckResolvedTables(byte[] image)
    {
        int firstTable = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.FirstClassifierRva +
                HealerAttackCommandFixNativeDefinition.FirstTableInstructionOffset +
                HealerAttackCommandFixNativeDefinition.TableDisplacementOffset);
        int secondTable = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.SecondClassifierRva +
                HealerAttackCommandFixNativeDefinition.SecondTableInstructionOffset +
                HealerAttackCommandFixNativeDefinition.TableDisplacementOffset);
        int firstDispatch = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.FirstClassifierRva +
                HealerAttackCommandFixNativeDefinition.FirstDispatchInstructionOffset +
                HealerAttackCommandFixNativeDefinition.DispatchDisplacementOffset);
        int secondDispatch = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.SecondClassifierRva +
                HealerAttackCommandFixNativeDefinition.SecondDispatchInstructionOffset +
                HealerAttackCommandFixNativeDefinition.DispatchDisplacementOffset);

        Check(firstTable == HealerAttackCommandFixNativeDefinition.FirstTableRva,
            "first classifier resolves its audited table");
        Check(secondTable == HealerAttackCommandFixNativeDefinition.SecondTableRva,
            "second classifier resolves its audited table");
        Check(firstDispatch == HealerAttackCommandFixNativeDefinition.FirstDispatchTableRva,
            "first classifier resolves its audited dispatch table");
        Check(secondDispatch == HealerAttackCommandFixNativeDefinition.SecondDispatchTableRva,
            "second classifier resolves its audited dispatch table");
    }

    private static void CheckClassificationContracts(byte[] image)
    {
        int engineerIndex = HealerAttackCommandFixNativeDefinition.EngineerType -
            HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;
        int healerIndex = HealerAttackCommandFixNativeDefinition.BedouinHealerType -
            HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;

        Check(HealerAttackCommandFixNativeDefinition.AttackUnitCommand == 4,
            "TribeAICommand.AttackUnit remains command 4");
        Check(image[HealerAttackCommandFixNativeDefinition.FirstTableRva + engineerIndex] ==
            HealerAttackCommandFixNativeDefinition.FirstNoOpClass,
            "Engineer already uses first no-op class");
        Check(image[HealerAttackCommandFixNativeDefinition.SecondTableRva + engineerIndex] ==
            HealerAttackCommandFixNativeDefinition.SecondNoOpClass,
            "Engineer already uses second no-op class");
        Check(image[HealerAttackCommandFixNativeDefinition.FirstTableRva + healerIndex] ==
            HealerAttackCommandFixNativeDefinition.FirstVanillaHealerClass,
            "Bedouin Healer initially uses first melee class");
        Check(image[HealerAttackCommandFixNativeDefinition.SecondTableRva + healerIndex] ==
            HealerAttackCommandFixNativeDefinition.SecondVanillaHealerClass,
            "Bedouin Healer initially uses second melee class");
        Check(HealerAttackCommandFixNativeDefinition.FirstTableRva + healerIndex ==
            HealerAttackCommandFixNativeDefinition.FirstHealerEntryRva,
            "first Healer table-entry RVA");
        Check(HealerAttackCommandFixNativeDefinition.SecondTableRva + healerIndex ==
            HealerAttackCommandFixNativeDefinition.SecondHealerEntryRva,
            "second Healer table-entry RVA");

        Check(ReadInt32(image, HealerAttackCommandFixNativeDefinition.FirstDispatchTableRva) ==
            HealerAttackCommandFixNativeDefinition.FirstMeleeTargetRva,
            "first class zero enters melee-group counting");
        Check(ReadInt32(image,
            HealerAttackCommandFixNativeDefinition.FirstDispatchTableRva +
                HealerAttackCommandFixNativeDefinition.FirstNoOpClass * sizeof(int)) ==
            HealerAttackCommandFixNativeDefinition.FirstNoOpTargetRva,
            "first replacement class enters no-op branch");
        Check(ReadInt32(image, HealerAttackCommandFixNativeDefinition.SecondDispatchTableRva) ==
            HealerAttackCommandFixNativeDefinition.SecondMeleeTargetRva,
            "second class zero assigns a melee-group position");
        Check(ReadInt32(image,
            HealerAttackCommandFixNativeDefinition.SecondDispatchTableRva +
                HealerAttackCommandFixNativeDefinition.SecondNoOpClass * sizeof(int)) ==
            HealerAttackCommandFixNativeDefinition.SecondNoOpTargetRva,
            "second replacement class enters no-op branch");

        Check(HashSlice(image, HealerAttackCommandFixNativeDefinition.FirstTableRva, 81) ==
            "0C7BFCEC367534FD52395382F291EDBE8F444FB9B906205C7823DD3FC32FAE9F",
            "complete first classifier table is unchanged in the canonical DLL");
        Check(HashSlice(image, HealerAttackCommandFixNativeDefinition.SecondTableRva, 81) ==
            "5B7439039A0725E57D8840DDF234CD59B48C2FC6CE2F35C079446CB8144D8C3E",
            "complete second classifier table is unchanged in the canonical DLL");
    }

    private static void CheckSourceContracts(string workspace)
    {
        string runtime = File.ReadAllText(Path.Combine(
            workspace,
            "HealerAttackCommandFixTest",
            "src",
            "HealerAttackCommandFixTestRuntime.cs"));
        string plugin = File.ReadAllText(Path.Combine(
            workspace,
            "HealerAttackCommandFixTest",
            "src",
            "HealerAttackCommandFixTestPlugin.cs"));

        Check(runtime.Contains("FindUniquePattern"), "runtime requires unique code signatures");
        Check(runtime.Contains("ReadAbsoluteTableRva"), "runtime derives table RVAs from native instructions");
        Check(runtime.Contains("ValidateDispatchTargets"), "runtime validates classification semantics");
        Check(runtime.Contains("ValidateUnitTypeContracts"), "runtime validates Script Extender unit-type values");
        Check(!runtime.Contains("memory.ToArray()"), "runtime retains no full native-image copy");
        Check(runtime.Contains("firstHealerEntry"), "runtime patches the first Healer classification");
        Check(runtime.Contains("secondHealerEntry"), "runtime patches the second Healer classification");
        Check(runtime.Contains("HEALER_ATTACK_GROUP_FIX_RUNTIME_CONFIRMED"),
            "runtime emits post-startup tick evidence");
        Check(!runtime.Contains("X64InlineHook"), "runtime uses no cursor or inline hook");
        Check(!runtime.Contains("AddDetour"), "runtime uses no native detour");
        Check(plugin.Contains("requireCurrentVersion: true"), "unknown native hashes fail closed");
        Check(plugin.Contains("private static HealerAttackCommandFixTestRuntime persistentRuntime"),
            "runtime remains rooted after startup cleanup");
        Check(!plugin.Contains("persistentRuntime?.Dispose"),
            "startup cleanup cannot remove the process-wide patch");
    }

    private static void CheckPattern(PeImage pe, string value, int expectedRva, string label)
    {
        PatternByte[] pattern = ParsePattern(value);
        List<int> matches = new List<int>();
        foreach (Section section in pe.ExecutableSections)
        {
            int end = section.Rva + section.Length - pattern.Length;
            for (int offset = section.Rva; offset <= end; offset++)
            {
                bool match = true;
                for (int index = 0; index < pattern.Length; index++)
                {
                    if (!pattern[index].Wildcard && pe.Image[offset + index] != pattern[index].Value)
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    matches.Add(offset);
            }
        }
        Check(matches.Count == 1 && matches[0] == expectedRva, label + " is unique at the audited RVA");
    }

    private static PatternByte[] ParsePattern(string pattern)
    {
        string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        PatternByte[] result = new PatternByte[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            bool wildcard = tokens[index] == "??" || tokens[index] == "?";
            result[index] = new PatternByte(wildcard ? (byte)0 : Convert.ToByte(tokens[index], 16), wildcard);
        }
        return result;
    }

    private static int ReadInt32(byte[] image, int rva) => BitConverter.ToInt32(image, rva);

    private static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }

    private static string HashSlice(byte[] image, int offset, int length)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(image, offset, length)).Replace("-", string.Empty);
    }

    private static string FindWorkspace()
    {
        DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "HealerAttackCommandFixTest")) &&
                Directory.Exists(Path.Combine(directory.FullName, "_inspect")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Workspace root was not found.");
    }

    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Assertion failed: " + label);
    }

    private readonly struct PatternByte
    {
        public PatternByte(byte value, bool wildcard)
        {
            Value = value;
            Wildcard = wildcard;
        }

        public byte Value { get; }
        public bool Wildcard { get; }
    }

    private sealed class PeImage
    {
        private PeImage(byte[] image, List<Section> executableSections)
        {
            Image = image;
            ExecutableSections = executableSections;
        }

        public byte[] Image { get; }
        public List<Section> ExecutableSections { get; }

        public static PeImage Load(byte[] file)
        {
            int peOffset = BitConverter.ToInt32(file, 0x3C);
            int sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
            int optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
            int optionalHeader = peOffset + 24;
            int sizeOfImage = BitConverter.ToInt32(file, optionalHeader + 56);
            int sizeOfHeaders = BitConverter.ToInt32(file, optionalHeader + 60);
            int sectionTable = optionalHeader + optionalHeaderSize;
            byte[] image = new byte[sizeOfImage];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(sizeOfHeaders, file.Length));
            List<Section> executable = new List<Section>();

            for (int index = 0; index < sectionCount; index++)
            {
                int header = sectionTable + index * 40;
                int virtualSize = BitConverter.ToInt32(file, header + 8);
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int rawOffset = BitConverter.ToInt32(file, header + 20);
                uint characteristics = BitConverter.ToUInt32(file, header + 36);
                int copyLength = Math.Min(rawSize, Math.Min(file.Length - rawOffset, image.Length - virtualAddress));
                if (copyLength > 0)
                    Buffer.BlockCopy(file, rawOffset, image, virtualAddress, copyLength);
                if ((characteristics & 0x20000000U) != 0)
                    executable.Add(new Section(virtualAddress, Math.Max(virtualSize, rawSize)));
            }

            return new PeImage(image, executable);
        }
    }

    private readonly struct Section
    {
        public Section(int rva, int length)
        {
            Rva = rva;
            Length = length;
        }

        public int Rva { get; }
        public int Length { get; }
    }
}
