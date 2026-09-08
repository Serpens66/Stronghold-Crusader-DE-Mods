using System.Security.Cryptography;
using System.Text.Json;
using ElevatedMoatTest;

internal static class Program
{
    private const string CanonicalDll =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\" +
        @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
    private static int failures;

    private static int Main()
    {
        string modRoot = FindModRoot();
        TestNativeContractConstants();
        TestNativeContractValidation();
        TestCanonicalBinary();
        TestSourceContracts(modRoot);
        TestManifest(modRoot);

        Console.WriteLine(failures == 0
            ? "All ElevatedMoatTest checks passed."
            : $"ElevatedMoatTest checks failed: {failures}.");
        return failures == 0 ? 0 : 1;
    }

    private static void TestNativeContractConstants()
    {
        Check(ElevatedMoatNativeContract.HeightWriterRva == 0x7870B, "audited writer RVA");
        Check(ElevatedMoatNativeContract.HeightWriterLength == 20, "20-byte hook boundary");
        Check(ElevatedMoatNativeContract.HeightWriterBytes.Length == 20, "writer byte count");
        Check(ElevatedMoatNativeContract.MapperMoat == 105, "MAPPER_MOAT value");
        Check(ElevatedMoatNativeContract.MaximumVanillaTerrainHeight == 12, "Vanilla height threshold");
        Check(ElevatedMoatNativeContract.PlacementFailureReason == 24, "Vanilla failure reason");
    }

    private static void TestNativeContractValidation()
    {
        const int writerOffset = 64;
        byte[] image = new byte[160];
        Array.Copy(ElevatedMoatNativeContract.RequiredPrefix, 0, image,
            writerOffset - ElevatedMoatNativeContract.RequiredPrefix.Length,
            ElevatedMoatNativeContract.RequiredPrefix.Length);
        Array.Copy(ElevatedMoatNativeContract.HeightWriterBytes, 0, image,
            writerOffset, ElevatedMoatNativeContract.HeightWriterBytes.Length);
        Array.Copy(ElevatedMoatNativeContract.RequiredSuffix, 0, image,
            writerOffset + ElevatedMoatNativeContract.HeightWriterLength,
            ElevatedMoatNativeContract.RequiredSuffix.Length);

        ExpectNoThrow(() => ElevatedMoatNativeContract.Validate(image, writerOffset),
            "complete native validation window");
        image[writerOffset - 1] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.Validate(image, writerOffset),
            "changed comparison branch fails closed");
    }

    private static void TestCanonicalBinary()
    {
        Check(File.Exists(CanonicalDll), "canonical installed CrusaderDE.dll exists");
        if (!File.Exists(CanonicalDll))
            return;

        byte[] file = File.ReadAllBytes(CanonicalDll);
        string sha256 = Convert.ToHexString(SHA256.HashData(file));
        Check(sha256 == ElevatedMoatNativeContract.ReferenceSha256, "canonical DLL SHA-256");

        List<int> matches = FindAll(file, ElevatedMoatNativeContract.HeightWriterBytes);
        Check(matches.Count == 1, "writer pattern occurs exactly once");
        if (matches.Count == 1)
            Check(FileOffsetToRva(file, matches[0]) == ElevatedMoatNativeContract.HeightWriterRva,
                "unique writer maps to RVA 0x7870B");
    }

    private static void TestSourceContracts(string modRoot)
    {
        string runtime = File.ReadAllText(Path.Combine(modRoot, "src", "ElevatedMoatOverride.cs"));
        string plugin = File.ReadAllText(Path.Combine(modRoot, "src", "ElevatedMoatTestPlugin.cs"));
        string project = File.ReadAllText(Path.Combine(modRoot, "ElevatedMoatTest.csproj"));

        Check(runtime.Contains("Registers = X64SmartCPUContextRegs.All"), "all GPRs are preserved");
        Check(runtime.Contains("HookSize = ElevatedMoatNativeContract.HeightWriterLength"), "explicit hook size");
        Check(runtime.Contains("Placement = OverwrittenInstructionPlacement.Suppress"), "writer is suppressed");
        Check(runtime.Contains("DisplacedByteCount != ElevatedMoatNativeContract.HeightWriterLength"),
            "actual displaced length is checked");
        Check(runtime.Contains("FailureMode = TransactionFailureMode.RollbackAndThrow") &&
            runtime.Contains("OwnsHooks = true") && runtime.Contains("pending.Dispose()"),
            "hook errors roll back the owned transaction");
        Check(plugin.Contains("requireCurrentVersion: true") &&
            plugin.Contains("if (!referenceHashMatches)"), "native hash mismatch fails closed");
        Check(project.Contains(@"$(GameDir)\BepInEx\plugins\000shcdese") &&
            !project.Contains("LocalScriptExtender"), "build targets only the installed Script Extender");
    }

    private static void TestManifest(string modRoot)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(modRoot, "info.json")));
        JsonElement root = document.RootElement;
        string plugin = File.ReadAllText(Path.Combine(modRoot, "src", "ElevatedMoatTestPlugin.cs"));

        Check(root.GetProperty("GUID").GetString() == "ElevatedMoatTest_Serp", "manifest GUID");
        Check(root.GetProperty("Version").GetString() == "0.1.0" &&
            plugin.Contains("PluginVersion = \"0.1.0\""), "manifest/plugin version consistency");
        Check(root.GetProperty("MinimumScriptExtenderVersion").GetString() == "2.3.0" &&
            plugin.Contains("[BepInDependency(ScriptExtenderGuid, \"2.3.0\")]"),
            "Script Extender 2.3.0 contract");
        Check(root.GetProperty("NetworkMode").GetInt32() == 1, "gameplay NetworkMode");
    }

    private static string FindModRoot()
    {
        DirectoryInfo current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ElevatedMoatTest.csproj")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the ElevatedMoatTest root.");
    }

    private static List<int> FindAll(byte[] haystack, byte[] needle)
    {
        var matches = new List<int>();
        for (int offset = 0; offset <= haystack.Length - needle.Length; offset++)
        {
            int index = 0;
            while (index < needle.Length && haystack[offset + index] == needle[index])
                index++;
            if (index == needle.Length)
                matches.Add(offset);
        }
        return matches;
    }

    private static int FileOffsetToRva(byte[] pe, int fileOffset)
    {
        int peOffset = BitConverter.ToInt32(pe, 0x3C);
        ushort sectionCount = BitConverter.ToUInt16(pe, peOffset + 6);
        ushort optionalHeaderSize = BitConverter.ToUInt16(pe, peOffset + 20);
        int sectionTable = peOffset + 24 + optionalHeaderSize;
        for (int section = 0; section < sectionCount; section++)
        {
            int header = sectionTable + section * 40;
            int virtualAddress = BitConverter.ToInt32(pe, header + 12);
            int rawSize = BitConverter.ToInt32(pe, header + 16);
            int rawStart = BitConverter.ToInt32(pe, header + 20);
            if (fileOffset >= rawStart && fileOffset < rawStart + rawSize)
                return checked(virtualAddress + fileOffset - rawStart);
        }
        throw new InvalidOperationException("Pattern file offset is outside all PE sections.");
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
            Console.WriteLine($"PASS: {name}");
        else
        {
            failures++;
            Console.WriteLine($"FAIL: {name}");
        }
    }

    private static void ExpectNoThrow(Action action, string name)
    {
        try
        {
            action();
            Check(true, name);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            Check(false, name);
        }
    }

    private static void ExpectThrows(Action action, string name)
    {
        try
        {
            action();
            Check(false, name);
        }
        catch (InvalidOperationException)
        {
            Check(true, name);
        }
    }
}
