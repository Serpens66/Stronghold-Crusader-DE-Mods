using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

internal static class Program
{
    private const string ExpectedDllHash =
        "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const string DllPath =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";

    private static readonly Dictionary<string, int> PatternRvas =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ConstructingFailureStatusPattern"] = 0x9129E,
            ["EuropeanPlacementRejectPattern"] = 0x929D3,
            ["MercenaryPlacementRejectPattern"] = 0x928E0,
            ["EngineerPlacementRejectPattern"] = 0x926FA,
            ["TunnelerPlacementRejectPattern"] = 0x912E0,
            ["KnightPlacementRejectPattern"] = 0x913CF,
            ["BedouinPlacementRejectPattern"] = 0x927ED,
            ["CreateHerdPattern"] = 0xD17D0,
            ["PopularityExitPattern"] = 0xCB55C,
            ["AreaTreatmentPattern"] = 0xA0470,
            ["DiseaseSearchPattern"] = 0x9F700,
            ["HealerUpdateExitPattern"] = 0x1501A7,
            ["PeriodicDiseaseFoundPattern"] = 0x14F8CC,
            ["WorkingBuildingExitReferencePattern"] = 0x14F768,
            ["SpearmanMovementDecisionPattern"] = 0x143BD9,
            ["PreTerrainSpeedAdjustmentPattern"] = 0x19B506,
            ["UnitTypeUpdateDispatchPattern"] = 0x18410C,
            ["MovementCadencePattern"] = 0x184203,
            ["MarketValidatorPattern"] = 0xD7080,
            ["MarketPacketTailPattern"] = 0xD7324,
            ["MarketStorageCallPattern"] = 0xD7119,
            ["AutoMarketSellStatisticPattern"] = 0xD0484,
            ["RecruitEuropeanUnitPattern"] = 0x190CA0,
            ["SellerReservePattern"] = 0x3F14F,
            ["AivSlotLayoutPattern"] = 0x5068A,
            ["AivStepLayoutPattern"] = 0x517C2,
            ["AivHighestFramePattern"] = 0x55F64,
            ["AivInitialFirstBuildStatePattern"] = 0x53F0B,
            ["AivResourceShortageReturnPattern"] = 0x51842,
            ["AivFirstBuildSuccessPattern"] = 0x5216D,
            ["AivPlacementRetryPattern"] = 0x5217A,
            ["NarrowRuinClassifierPattern"] = 0x5D055,
            ["BroadRuinClassifierPattern"] = 0x5D025,
            ["MapperSelectionPattern"] = 0x5CEAB,
            ["BroadBlockerLoadPattern"] = 0x5D016,
            ["NarrowBlockerLoadPattern"] = 0x5D045,
            ["AssassinBuilderPattern"] = 0xD9C40,
            ["EndpointBuildingGuardsPattern"] = 0xE19D4,
            ["DispatcherAssassinBranchPattern"] = 0xF4B0C,
            ["State106CombatFinishCallSequence"] = 0x16DFCE,
            ["CombatFinishHelperSequence"] = 0x1853F0,
            ["PostCombatRepathPrologueSequence"] = 0x1976C0,
            ["PostCombatPathRequestSequence"] = 0x197702,
            ["CommonPathContextReadSequence"] = 0x1964EE,
            ["CommonPathSuccessClearSequence"] = 0x196734,
            ["CommonPathFailureClearSequence"] = 0x19676C
        };

    private static readonly FunctionContract[] Functions =
    {
        new FunctionContract(0x3EE10, 1105, "B1F7DF14291D0D4C0AE544204E279BC57BBC8E617C29E3A269EBB405FF114765"),
        new FunctionContract(0x50680, 159, "B6DAA534A93D19F9EFC032A8CA604E12C3E6087A61D3615EC4E1476D0708283E"),
        new FunctionContract(0x51790, 2774, "69731F77776995C9FC452A7A9A41408385B757B461F0E7FAB76E291BE64C3ECF"),
        new FunctionContract(0x55F50, 144, "707C57D1FEBE76D9AF6E535B4D4A7068B5FC2D305C901E7CB3CC3582163AB502"),
        new FunctionContract(0x5CD90, 1077, "099D5E8B4AB0B93EB2BE39501D06AE0FC38F481035AF50650654F6F233B23A17"),
        new FunctionContract(0x90CD0, 8126, "A47403466994BB1D1D3476C81E9511AAC9653A3CFC475FFA6D76E65E150110B6"),
        new FunctionContract(0x9F700, 525, "D4C059E5AED1B7FFCFA334E0A361EDA4DC7B49EF1FBAE9F8972E231FC4A0BC6A"),
        new FunctionContract(0xA0470, 198, "5AA2550296CF94BD7180F240144C0353A7ECE97021975CA94F3DC85F25B3202A"),
        new FunctionContract(0xCB090, 1891, "A131D1CA8B25B95C2AF694CD94D3A4CBFA92DEBE1F12990647146D44E4FEAE05"),
        new FunctionContract(0xD0380, 555, "F61D65B94E3089FA60BE490EF828FA48375B2A226C9FFEB4CA54B01864BC7CC0"),
        new FunctionContract(0xD17D0, 494, "9ED8D8B10616413BC5FC3F2CEB060E56964CA0147FD6146992D2B300289C55F6"),
        new FunctionContract(0xD7080, 734, "3A931C5FEB5FB9D324C12CE53ADE9648D2E26FFB9EF62B75D0C3BD8AAAA3C924"),
        new FunctionContract(0xD9C40, 990, "5596B8DBF622F8C44085BAE06C5E318A61B84BE6F4D9A0F2A73113C616B3A65E"),
        new FunctionContract(0x107160, 50, "4A83B91AC728B7DB6E746997635D2B96B8895D81B67B2D8DC32598B4C5D4FF44"),
        new FunctionContract(0x143400, 7001, "F39AFDE7543E274058168DD080F96621592AEABE0CDE897BCFBDB3A983F25C53"),
        new FunctionContract(0x14F3C0, 3588, "EE4650DA6F0D11CFAAB97A1CD8124A7DD1291C0E89B8D6FB3EDA6B00A8BE4602"),
        new FunctionContract(0x182B00, 9137, "F640FE9609EEC3199B9C675B91CCF488310B0A23B832BD010FDC80AB00DF153F"),
        new FunctionContract(0x1853F0, 55, "A7B2D84B7487FA73BF4A94C91536BB89F15E898F4525CCCD08B4818980DEA82E"),
        new FunctionContract(0x190CA0, 938, "8F397249E08A12338327322581CC17F0B9FE6507A426D2B2A29079A102471C6B"),
        new FunctionContract(0x196280, 1293, "D81EEBC55A1FB0CFEEB25D0B0D1CCEDE5C9F545E3CCCCAC5119A556C7B43E9E1"),
        new FunctionContract(0x196810, 33, "FA0090EB160121E461BDBD72FAF66A24F519A7049BE8BA5C2FC7DEBAE554FA8A"),
        new FunctionContract(0x1976C0, 211, "39E4EE6EF688BA664742C592585D2EFF99FF0CBDA16E60B6093DF9BBA64A0469"),
        new FunctionContract(0x19B260, 966, "15CEB13D6FF56A004CF35CB77A868035410076F63742231CAD5C999AB9B45A9C")
    };

    private static int assertions;

    private static int Main()
    {
        try
        {
            string workspace = FindWorkspace();
            byte[] file = File.ReadAllBytes(DllPath);
            Check(Hash(file) == ExpectedDllHash, "canonical DLL hash");
            PeImage pe = PeImage.Load(file);
            CheckFunctions(pe.Image);
            CheckProductionPatterns(workspace, pe);
            CheckCriticalSpans(pe.Image);
            CheckUnknownHashPolicy(workspace);
            Console.WriteLine($"PASS: BugfixesAndQoL native tests ({assertions} assertions, {PatternRvas.Count} signatures).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void CheckFunctions(byte[] image)
    {
        foreach (FunctionContract function in Functions)
        {
            Check(function.Rva >= 0 && function.Rva <= image.Length - function.Size,
                $"function 0x{function.Rva:X} bounds");
            byte[] bytes = new byte[function.Size];
            Buffer.BlockCopy(image, function.Rva, bytes, 0, function.Size);
            string actualHash = Hash(bytes);
            Check(actualHash == function.Hash,
                $"function 0x{function.Rva:X} hash: expected {function.Hash}, actual {actualHash}");
        }
    }

    private static void CheckProductionPatterns(string workspace, PeImage pe)
    {
        Dictionary<string, string> patterns = ReadConstStrings(
            Path.Combine(workspace, "BugfixesAndQoL", "src"));
        foreach (KeyValuePair<string, int> contract in PatternRvas)
        {
            Check(patterns.TryGetValue(contract.Key, out string pattern), contract.Key + " source constant");
            PatternToken[] tokens = ParsePattern(pattern);
            Check(Matches(pe.Image, contract.Value, tokens), contract.Key + " reference RVA");
            Check(pe.IsExecutable(contract.Value, tokens.Length), contract.Key + " executable section");
            List<int> matches = FindMatches(pe, tokens);
            Check(matches.Count == 1 && matches[0] == contract.Value,
                contract.Key + " unique executable match");
        }
    }

    private static void CheckCriticalSpans(byte[] image)
    {
        CheckBytes(image, 0x912B4, "0F 44 D8", "assembly preview original span");
        foreach (int rva in new[] { 0x929D5, 0x928E2, 0x926FC, 0x912E2, 0x913D1, 0x927EF })
            CheckBytes(image, rva, "0F 84", $"assembly rejection original span 0x{rva:X}");
        CheckBytes(image, 0x3F156,
            "42 8D 14 18 45 85 E4 7E 34 41 81 BE CC F0 12 00 F4 01 00 00",
            "AI stone full 20-byte hook span");
        CheckBytes(image, 0x19B506,
            "0F B6 83 C8 06 00 00 45 85 C9 74 42 3C 18",
            "pre-terrain movement full 14-byte hook span");
        CheckBytes(image, 0x197716,
            "66 89 8B 4E 07 00 00 89 4C 24 20 48 8B CE",
            "Assassin combat-context full 14-byte hook span");
        CheckBytes(image, 0xE19D8, "0F 85 B1 00 00 00", "Assassin current-tile rejection jump");
        CheckBytes(image, 0xE19F9, "0F 85 88 00 00 00", "Assassin neighbor-tile rejection jump");
    }

    private static void CheckUnknownHashPolicy(string workspace)
    {
        string plague = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "PlagueNativePatternValidator.cs"));
        string recruitment = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "AiRecruitmentHorseDemandFix.cs"));
        Check(plague.Contains("if (!referenceHashMatches)"), "plague fixed-layout unknown-hash gate");
        Check(recruitment.Contains("if (!referenceHashMatches)"), "AI recruitment result-layout unknown-hash gate");
    }

    private static Dictionary<string, string> ReadConstStrings(string sourceDirectory)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var declaration = new Regex(
            "const\\s+string\\s+(?<name>[A-Za-z0-9_]+)\\s*=\\s*(?<value>(?:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*\\+?)+)\\s*;",
            RegexOptions.Singleline);
        var literal = new Regex("\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");
        foreach (string file in Directory.GetFiles(sourceDirectory, "*.cs"))
        {
            string source = File.ReadAllText(file);
            foreach (Match match in declaration.Matches(source))
            {
                string name = match.Groups["name"].Value;
                string value = string.Concat(literal.Matches(match.Groups["value"].Value)
                    .Cast<Match>().Select(x => Regex.Unescape(x.Groups["text"].Value)));
                result[name] = value;
            }
        }
        return result;
    }

    private static PatternToken[] ParsePattern(string pattern) =>
        pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x == "?" || x == "??"
                ? new PatternToken(0, true)
                : new PatternToken(Convert.ToByte(x, 16), false))
            .ToArray();

    private static List<int> FindMatches(PeImage pe, PatternToken[] pattern)
    {
        var result = new List<int>();
        foreach (Section section in pe.Sections.Where(x => x.Executable))
        {
            int end = Math.Min(pe.Image.Length, section.Rva + section.Size) - pattern.Length;
            for (int rva = section.Rva; rva <= end; rva++)
            {
                if (Matches(pe.Image, rva, pattern))
                    result.Add(rva);
            }
        }
        return result;
    }

    private static bool Matches(byte[] image, int rva, PatternToken[] pattern)
    {
        if (rva < 0 || rva > image.Length - pattern.Length)
            return false;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (!pattern[i].Wildcard && image[rva + i] != pattern[i].Value)
                return false;
        }
        return true;
    }

    private static void CheckBytes(byte[] image, int rva, string expected, string label)
    {
        byte[] bytes = expected.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray();
        Check(rva >= 0 && rva <= image.Length - bytes.Length, label + " bounds");
        for (int i = 0; i < bytes.Length; i++)
            Check(image[rva + i] == bytes[i], label + $" byte +0x{i:X}");
    }

    private static string FindWorkspace()
    {
        DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "BugfixesAndQoL", "src")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Workspace root not found.");
    }

    private static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }

    private static int ReadInt32(byte[] value, int offset) =>
        value[offset] | value[offset + 1] << 8 | value[offset + 2] << 16 | value[offset + 3] << 24;
    private static int ReadUInt16(byte[] value, int offset) => value[offset] | value[offset + 1] << 8;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct FunctionContract
    {
        public FunctionContract(int rva, int size, string hash) { Rva = rva; Size = size; Hash = hash; }
        public int Rva { get; }
        public int Size { get; }
        public string Hash { get; }
    }

    private readonly struct PatternToken
    {
        public PatternToken(byte value, bool wildcard) { Value = value; Wildcard = wildcard; }
        public byte Value { get; }
        public bool Wildcard { get; }
    }

    private readonly struct Section
    {
        public Section(int rva, int size, bool executable) { Rva = rva; Size = size; Executable = executable; }
        public int Rva { get; }
        public int Size { get; }
        public bool Executable { get; }
    }

    private sealed class PeImage
    {
        private PeImage(byte[] image, List<Section> sections) { Image = image; Sections = sections; }
        public byte[] Image { get; }
        public List<Section> Sections { get; }
        public bool IsExecutable(int rva, int length) => Sections.Any(x => x.Executable && rva >= x.Rva && rva + length <= x.Rva + x.Size);

        public static PeImage Load(byte[] file)
        {
            int pe = ReadInt32(file, 0x3C);
            int count = ReadUInt16(file, pe + 6);
            int optionalSize = ReadUInt16(file, pe + 20);
            int optional = pe + 24;
            int imageSize = ReadInt32(file, optional + 56);
            int headers = ReadInt32(file, optional + 60);
            byte[] image = new byte[imageSize];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(headers, file.Length));
            int table = optional + optionalSize;
            var sections = new List<Section>();
            for (int i = 0; i < count; i++)
            {
                int h = table + i * 40;
                int virtualSize = ReadInt32(file, h + 8);
                int rva = ReadInt32(file, h + 12);
                int rawSize = ReadInt32(file, h + 16);
                int raw = ReadInt32(file, h + 20);
                int characteristics = ReadInt32(file, h + 36);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, raw, image, rva, Math.Min(rawSize, file.Length - raw));
                sections.Add(new Section(rva, Math.Max(virtualSize, rawSize), (characteristics & 0x20000000) != 0));
            }
            return new PeImage(image, sections);
        }
    }
}
