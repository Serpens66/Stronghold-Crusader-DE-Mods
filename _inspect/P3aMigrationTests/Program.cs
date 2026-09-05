using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

internal static class Program
{
    private const string NativePath =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
    private const string NativeHash =
        "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private static int assertions;

    private readonly record struct PatternCase(string Name, string Pattern, int ExpectedRva, int Offset = 0);

    private static int Main()
    {
        try
        {
            string root = FindWorkspaceRoot();
            VerifySources(root);
            VerifyManifests(root);
            VerifyNativePatterns();
            Console.WriteLine($"P3a migration: {assertions} assertions passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void VerifySources(string root)
    {
        string[] mods =
        {
            "ActiveAIVDetector", "EnemyGatePathfindingTest", "HunterQueryTargetDiagnostic"
        };
        string production = string.Join("\n", mods.SelectMany(mod =>
            Directory.EnumerateFiles(Path.Combine(root, mod), "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText)));

        Equal(3, Count(production, "[BepInDependency(ScriptExtenderGuid, \"2.0.2\")]"),
            "all three runtime dependencies pin 2.0.2");
        Equal(3, Count(production, "OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)"),
            "all callbacks consume the load context");
        Equal(0, Count(production, "OnCrusaderLibraryLoaded(IntPtr"), "old callbacks are absent");
        Equal(0, Count(production, "Zhuqiaomon"), "Zhuqiaomon production references are absent");
        Equal(0, Count(production, "PolyHook2"), "PolyHook production references are absent");
        Equal(0, Count(production, "HookRef<"), "legacy HookRef handles are absent");
        Equal(8, Count(production, "private readonly DetourHandle<"),
            "all eight function detours use RedBird handles");
        Equal(4, Count(production, "private readonly HookHandle<X64InlineHook>"),
            "all four inline hooks use RedBird handles");
        Equal(12, Count(production, "HookTarget.FromAddress("), "all twelve hooks have explicit address targets");
        Equal(4, Count(production, "FailureMode = TransactionFailureMode.RollbackAndThrow"),
            "every transaction rolls back atomically");
        Equal(4, Count(production, "OwnsHooks = false"),
            "every process-lifetime transaction declares non-owning disposal semantics");
        Equal(4, Count(production, "IsCompleteSuccess"), "every commit result is checked");
        Equal(4, Count(production, "new ContextHookOptions"),
            "every inline hook supplies explicit context options");
        Assert(!Regex.IsMatch(production, @"context\.(Region|Memory)\.Dispose\s*\("),
            "extender-owned context memory is never disposed");

        string selected = File.ReadAllText(Path.Combine(root,
            "EnemyGatePathfindingTest", "src", "SamePclBridgeDiagnostics.cs"));
        Assert(selected.Contains("SelectedUnitInfo[] selected =", StringComparison.Ordinal),
            "selected units use the 2.0.2 result type");
        Assert(selected.Contains("selected[index].UnitId", StringComparison.Ordinal),
            "one-based UnitId is projected explicitly");
        Assert(!selected.Contains("int[] selected = GamePlayerManagerAPI.Instance.GetSelectedChimps()",
            StringComparison.Ordinal), "old selected-unit result type is absent");

        string nativeDefinition = File.ReadAllText(Path.Combine(root,
            "EnemyGatePathfindingTest", "src", "EnemyGatePathfindingNativeDefinition.cs"));
        Assert(nativeDefinition.Contains("AuditedScriptExtenderVersion = \"2.0.2\"", StringComparison.Ordinal),
            "EnemyGate audit version is current");
        Assert(nativeDefinition.Contains("6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79", StringComparison.Ordinal),
            "EnemyGate audit commit is current");
    }

    private static void VerifyManifests(string root)
    {
        VerifyMode(root, "ActiveAIVDetector", 0, packageOnly: true);
        VerifyMode(root, "EnemyGatePathfindingTest", 1, packageOnly: false);
        VerifyMode(root, "HunterQueryTargetDiagnostic", 0, packageOnly: false);
    }

    private static void VerifyMode(string root, string mod, int expected, bool packageOnly)
    {
        string[] paths = Directory.EnumerateFiles(Path.Combine(root, mod), "info.json",
            SearchOption.AllDirectories).ToArray();
        Equal(packageOnly ? 1 : 2, paths.Length, mod + " manifest-copy count");
        foreach (string path in paths)
        {
            string json = File.ReadAllText(path);
            Match match = Regex.Match(json, "\\\"NetworkMode\\\"\\s*:\\s*(\\d+)");
            Assert(match.Success && int.Parse(match.Groups[1].Value) == expected,
                mod + " NetworkMode " + expected);
        }
    }

    private static void VerifyNativePatterns()
    {
        byte[] raw = File.ReadAllBytes(NativePath);
        Equal(NativeHash, Convert.ToHexString(SHA256.HashData(raw)), "canonical native SHA-256");
        byte[] image = MapPeImage(raw);
        PatternCase[] cases =
        {
            new("prepare AIV layout", "44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 68", 0x53D00),
            new("select best fit", "44 88 44 24 18 89 54 24 10 55 56 41 54 41 55 41 56 41 57 48 83 EC 58", 0x54F60),
            new("test specific candidate", "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 41 8B F0 48 63 EA 48 8B F9 4C 8D 89 44 98 1B 00", 0x54DE0),
            new("load candidate", "40 53 56 57 41 55 48 83 EC 38 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 41 8B D8 48 63 FA 85 C0", 0x55320),
            new("apply rotation", "85 D2 0F 84 ?? ?? ?? ?? 53 48 83 EC 20 48 89 74 24 30 48 8B D9 48 89 7C 24 38 83 FA 06", 0x56670),
            new("evaluate candidate fit", "89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 45 33 C9 48 8D 81 44 98 1B 00", 0x57080),
            new("building placement target", "48 8D 35 ?? ?? ?? ?? 44 8B 81 28 E7 04 02 45 8B F1 4C 63 CA 44 8B D0 44 0F 45 94 24 90 00 00 00", 0x7B060, -0x18),
            new("execute build step", "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2", 0x51790),
            new("organism record table reference", "48 8D 05 ?? ?? ?? ?? 41 B8 9C 00 00 00 48 03 D0", 0x15A27),
            new("active layout index reference", "48 63 F2 48 8D 05 ?? ?? ?? ?? 4C 69 CE 3C 58 00 00", 0x55F64),
            new("PCL captured compare", "49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00 74 11", 0xE2710, 11),
            new("builder captured compare", "49 63 49 F4 48 69 D1 2C 03 00 00 66 42 39 84 2A D2 CE 4C 06 74 0D", 0xE302F, 11),
            new("cursor coordinate loads", "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? 85 C0 74 11 44 8B BC 24 C0 00 00 00", 0x8F3A8),
            new("cursor PCL decision", "E8 ?? ?? ?? ?? 85 C0 48 8D 3D E3 FB FC 03 B8 01 00 00 00", 0x8F1C4, 5),
            new("shared command PCL decision", "E8 ?? ?? ?? ?? 41 8B D7 85 C0 75 52 48 8D 0D ?? ?? ?? ?? E8", 0x11B75A, 5),
            new("Hunter state-7 writer", "B8 07 00 00 00 66 42 89 84 2A 18 09 00 00 E9 ?? ?? ?? ??", 0x12FEC1)
        };
        foreach (PatternCase item in cases)
        {
            (byte[] bytes, bool[] exact) = ParsePattern(item.Pattern);
            List<int> matches = FindAll(image, bytes, exact);
            Equal(1, matches.Count, item.Name + " unique match count");
            Equal(item.ExpectedRva, matches[0] + item.Offset, item.Name + " resolved RVA");
        }
    }

    private static byte[] MapPeImage(byte[] raw)
    {
        using var stream = new MemoryStream(raw, writable: false);
        using var reader = new PEReader(stream);
        int size = reader.PEHeaders.PEHeader?.SizeOfImage ?? throw new InvalidDataException("PE header missing");
        byte[] image = new byte[size];
        int headers = Math.Min(reader.PEHeaders.PEHeader!.SizeOfHeaders, raw.Length);
        Array.Copy(raw, image, headers);
        foreach (var section in reader.PEHeaders.SectionHeaders)
        {
            int count = Math.Min(section.SizeOfRawData,
                Math.Min(raw.Length - section.PointerToRawData, image.Length - section.VirtualAddress));
            if (count > 0)
                Array.Copy(raw, section.PointerToRawData, image, section.VirtualAddress, count);
        }
        return image;
    }

    private static (byte[] Bytes, bool[] Exact) ParsePattern(string pattern)
    {
        string[] tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[tokens.Length];
        bool[] exact = new bool[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            exact[index] = tokens[index] != "??" && tokens[index] != "?";
            if (exact[index]) bytes[index] = Convert.ToByte(tokens[index], 16);
        }
        return (bytes, exact);
    }

    private static List<int> FindAll(byte[] image, byte[] pattern, bool[] exact)
    {
        var matches = new List<int>();
        for (int start = 0; start <= image.Length - pattern.Length; start++)
        {
            int index = 0;
            while (index < pattern.Length && (!exact[index] || image[start + index] == pattern[index])) index++;
            if (index == pattern.Length) matches.Add(start);
        }
        return matches;
    }

    private static string FindWorkspaceRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "UpdatePlan-SHCDESE-2.0.2.md"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Workspace root not found.");
    }

    private static int Count(string text, string value) =>
        Regex.Matches(text, Regex.Escape(value), RegexOptions.CultureInvariant).Count;

    private static void Equal<T>(T expected, T actual, string message) where T : notnull =>
        Assert(EqualityComparer<T>.Default.Equals(expected, actual),
            $"{message}: expected={expected}, actual={actual}");

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
