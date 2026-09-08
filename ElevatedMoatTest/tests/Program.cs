using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using ElevatedMoatTest;
using RedBird.X64.Hooks;

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
        Check(BitConverter.ToInt32(ElevatedMoatNativeContract.HeightWriterBytes, 2) ==
            ElevatedMoatNativeContract.PlacementBlockedOffset &&
            BitConverter.ToInt32(ElevatedMoatNativeContract.HeightWriterBytes, 6) ==
            ElevatedMoatNativeContract.PlacementBlockedValue,
            "first 10-byte MOV writes blocked status 1");
        Check(BitConverter.ToInt32(ElevatedMoatNativeContract.HeightWriterBytes, 12) ==
            ElevatedMoatNativeContract.PlacementFailureReasonOffset &&
            BitConverter.ToInt32(ElevatedMoatNativeContract.HeightWriterBytes, 16) ==
            ElevatedMoatNativeContract.PlacementFailureReason,
            "second 10-byte MOV writes failure reason 24");
        Check(ElevatedMoatNativeContract.RequiredPrefix[8] == ElevatedMoatNativeContract.MapperMoat &&
            BitConverter.ToInt32(ElevatedMoatNativeContract.RequiredPrefix, 13) ==
            ElevatedMoatNativeContract.MaximumFootprintHeightOffset &&
            ElevatedMoatNativeContract.RequiredPrefix[17] ==
            ElevatedMoatNativeContract.MaximumVanillaTerrainHeight,
            "prefix encodes MAPPER_MOAT and maxHeight comparison");
        Check(ElevatedMoatNativeContract.RequiredPrefix[9] == 0x75 &&
            ElevatedMoatNativeContract.RequiredPrefix[10] == 0x1D &&
            ElevatedMoatNativeContract.RequiredPrefix[18] == 0x7E &&
            ElevatedMoatNativeContract.RequiredPrefix[19] == 0x14,
            "both comparison branches land after the 20-byte writer");
        Check(ElevatedMoatNativeContract.TileValidationResultRva == 0x7888E &&
            ElevatedMoatNativeContract.TileValidationResultLength == 14 &&
            ElevatedMoatNativeContract.TileValidationResultBytes.Length == 14,
            "audited 14-byte tile-result hook boundary");
        Check(ElevatedMoatNativeContract.AivHeightGateRva == 0x59827 &&
            ElevatedMoatNativeContract.AivHeightGateLength == 22 &&
            ElevatedMoatNativeContract.AivHeightGateBytes.Length == 22,
            "audited 22-byte AIV height-gate boundary");
        Check(ElevatedMoatNativeContract.AivCreatePathRva == 0x599B3 &&
            ElevatedMoatNativeContract.AivCreatePathLength == 16 &&
            ElevatedMoatNativeContract.AivCreatePathBytes.Length == 16,
            "audited 16-byte AIV creation-path boundary");
        Check(ElevatedMoatNativeContract.HumanMoatWriterResultRva == 0x73B24 &&
            ElevatedMoatNativeContract.HumanMoatWriterResultLength == 15 &&
            ElevatedMoatNativeContract.HumanMoatWriterResultBytes.Length == 15,
            "audited 15-byte human writer-result boundary");
        Check(ElevatedMoatNativeContract.SharedHeightGateRva == 0x704CC &&
            ElevatedMoatNativeContract.SharedHeightGateLength == 14 &&
            ElevatedMoatNativeContract.SharedHeightGateBytes.Length == 14,
            "audited shared editor/planning height gate");
        Check(ElevatedMoatNativeContract.TileDefaultHeightGridOffset == 0xDCCAC0 &&
            ElevatedMoatNativeContract.MoatDepth == 8,
            "audited default-height grid and Vanilla moat depth");
        foreach (int height in Enumerable.Range(0, 9))
            Check(ElevatedMoatNativeContract.CalculateCompletedHeight((byte)height) == 0,
                $"adaptive moat height clamps {height} to zero");
        Check(ElevatedMoatNativeContract.CalculateCompletedHeight(12) == 4,
            "adaptive moat height maps 12 to 4");
        Check(ElevatedMoatNativeContract.CalculateCompletedHeight(13) == 5,
            "adaptive moat height maps 13 to 5");
        Check(ElevatedMoatNativeContract.CalculateCompletedHeight(80) == 72,
            "adaptive moat height maps 80 to 72");
        Check(ElevatedMoatNativeContract.CalculateCompletedHeight(130) == 122,
            "adaptive moat height maps 130 to 122");
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

        List<int> tileResultMatches = FindAll(
            file,
            ElevatedMoatNativeContract.TileValidationResultResolutionBytes);
        Check(tileResultMatches.Count == 1, "contextual tile-result pattern occurs exactly once");
        if (tileResultMatches.Count == 1)
            Check(FileOffsetToRva(file, tileResultMatches[0]) ==
                ElevatedMoatNativeContract.TileValidationResultRva,
                "unique tile-result pattern maps to RVA 0x7888E");
        byte[] image = MapPeImage(file);
        ExpectNoThrow(() => ElevatedMoatNativeContract.ValidateTileValidationResultHook(
            image, ElevatedMoatNativeContract.TileValidationResultRva),
            "tile-result CALL target, branch target, and continuation");

        List<int> aivGateMatches = FindAll(file, ElevatedMoatNativeContract.AivHeightGateBytes);
        Check(aivGateMatches.Count == 1, "AIV height-gate pattern occurs exactly once");
        if (aivGateMatches.Count == 1)
            Check(FileOffsetToRva(file, aivGateMatches[0]) == ElevatedMoatNativeContract.AivHeightGateRva,
                "unique AIV height gate maps to RVA 0x59827");

        List<int> aivCreateMatches = FindAll(
            file,
            ElevatedMoatNativeContract.AivCreatePathResolutionBytes);
        Check(aivCreateMatches.Count == 1, "AIV creation-path pattern occurs exactly once");
        if (aivCreateMatches.Count == 1)
            Check(FileOffsetToRva(file, aivCreateMatches[0]) == ElevatedMoatNativeContract.AivCreatePathRva,
                "unique AIV creation path maps to RVA 0x599B3");
        ExpectNoThrow(() => ElevatedMoatNativeContract.ValidateAivHooks(
            image,
            ElevatedMoatNativeContract.AivHeightGateRva,
            ElevatedMoatNativeContract.AivCreatePathRva),
            "AIV function boundaries and creation-path branch target");
        byte[] changedAivImage = (byte[])image.Clone();
        changedAivImage[ElevatedMoatNativeContract.AivHeightGateRva] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateAivHooks(
            changedAivImage,
            ElevatedMoatNativeContract.AivHeightGateRva,
            ElevatedMoatNativeContract.AivCreatePathRva),
            "changed AIV height gate fails closed");

        List<int> humanResultMatches = FindAll(
            file,
            ElevatedMoatNativeContract.HumanMoatWriterResultResolutionBytes);
        Check(humanResultMatches.Count == 1, "human writer-result pattern occurs exactly once");
        if (humanResultMatches.Count == 1)
            Check(FileOffsetToRva(file, humanResultMatches[0]) ==
                ElevatedMoatNativeContract.HumanMoatWriterResultRva,
                "unique human writer result maps to RVA 0x73B24");
        ExpectNoThrow(() => ElevatedMoatNativeContract.ValidateHumanMoatWriterResultHook(
            image,
            ElevatedMoatNativeContract.HumanMoatWriterResultRva),
            "human writer CALL target, function boundary, and continuation");
        byte[] changedHumanImage = (byte[])image.Clone();
        changedHumanImage[ElevatedMoatNativeContract.HumanMoatWriterCallRva + 1] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateHumanMoatWriterResultHook(
            changedHumanImage,
            ElevatedMoatNativeContract.HumanMoatWriterResultRva),
            "changed human moat writer CALL target fails closed");

        CheckUniquePattern(file, ElevatedMoatNativeContract.SharedHeightGatePattern,
            ElevatedMoatNativeContract.SharedHeightGateRva, "shared editor/planning height gate");
        CheckUniquePattern(file, ElevatedMoatNativeContract.AivCompletedHeightPattern,
            ElevatedMoatNativeContract.AivCompletedHeightRva, "AIV completed-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.ExcavationCompletedHeightPattern,
            ElevatedMoatNativeContract.ExcavationCompletedHeightRva, "excavated-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.RebuildCompletedHeightPattern,
            ElevatedMoatNativeContract.RebuildCompletedHeightRva, "rebuilt-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.DirectCompletedHeightPattern,
            ElevatedMoatNativeContract.DirectCompletedHeightRva, "direct completed-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.GenericCompletedHeightPattern,
            ElevatedMoatNativeContract.GenericCompletedHeightRva, "generic completed-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.DirectRemovalHeightPattern,
            ElevatedMoatNativeContract.DirectRemovalHeightRva, "direct moat-removal height");
        ExpectNoThrow(() => ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(image),
            "adaptive-height function, byte, branch, and call contracts");
        byte[] changedAdaptiveImage = (byte[])image.Clone();
        changedAdaptiveImage[ElevatedMoatNativeContract.DirectCompletedHeightRva + 1] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(changedAdaptiveImage),
            "changed adaptive-height block fails closed");
        TestInstalledRedBirdDisplacement(image);
    }

    private static void TestInstalledRedBirdDisplacement(byte[] image)
    {
        CheckRedBirdDisplacement(
            image,
            ElevatedMoatNativeContract.AivHeightGateRva,
            ElevatedMoatNativeContract.AivHeightGateLength,
            "installed RedBird displaces exactly 22 bytes at the AIV height gate");
        CheckRedBirdDisplacement(
            image,
            ElevatedMoatNativeContract.HumanMoatWriterResultRva,
            ElevatedMoatNativeContract.HumanMoatWriterResultLength,
            "installed RedBird displaces exactly 15 bytes at the human result hook");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.SharedHeightGateRva,
            ElevatedMoatNativeContract.SharedHeightGateLength,
            "installed RedBird displaces exactly 14 bytes at the shared height gate");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.AivCompletedHeightRva,
            ElevatedMoatNativeContract.AivCompletedHeightLength,
            "installed RedBird displaces exactly 17 bytes at the AIV height block");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.ExcavationCompletedHeightRva,
            ElevatedMoatNativeContract.ExcavationCompletedHeightLength,
            "installed RedBird displaces exactly 16 bytes at the excavation height block");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.RebuildCompletedHeightRva,
            ElevatedMoatNativeContract.RebuildCompletedHeightLength,
            "installed RedBird displaces exactly 15 bytes at the rebuild height block");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.DirectCompletedHeightRva,
            ElevatedMoatNativeContract.DirectCompletedHeightLength,
            "installed RedBird displaces exactly 22 bytes at the direct height block");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.GenericCompletedHeightRva,
            ElevatedMoatNativeContract.GenericCompletedHeightLength,
            "installed RedBird displaces exactly 17 bytes at the generic height block");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.DirectRemovalHeightRva,
            ElevatedMoatNativeContract.DirectRemovalHeightLength,
            "installed RedBird displaces exactly 16 bytes at the removal height block");
    }

    private static void CheckRedBirdDisplacement(
        byte[] image,
        int rva,
        int requestedLength,
        string name)
    {
        IntPtr buffer = Marshal.AllocHGlobal(64);
        try
        {
            Marshal.Copy(image, rva, buffer, 64);
            using var hook = new X64InlineHook(
                unchecked((ulong)buffer.ToInt64()),
                requestedLength);
            Check(hook.DisplacedByteCount == requestedLength, name);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            Check(false, name);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void TestSourceContracts(string modRoot)
    {
        string runtime = File.ReadAllText(Path.Combine(modRoot, "src", "ElevatedMoatOverride.cs"));
        string plugin = File.ReadAllText(Path.Combine(modRoot, "src", "ElevatedMoatTestPlugin.cs"));
        string project = File.ReadAllText(Path.Combine(modRoot, "ElevatedMoatTest.csproj"));

        Check(runtime.Contains("Registers = X64SmartCPUContextRegs.All"), "all GPRs are preserved");
        Check(runtime.Contains("HookSize = ElevatedMoatNativeContract.HeightWriterLength"), "explicit hook size");
        Check(runtime.Contains("Placement = OverwrittenInstructionPlacement.Suppress"), "writer is suppressed");
        Check(runtime.Contains("BuildingR3EventHooks.OnPlacementValidation.Observable") &&
            runtime.Contains("EventHookPhase.Pre") && runtime.Contains("EventHookPhase.Post"),
            "public placement event supplies pre/post diagnostics");
        Check(runtime.Contains("ObserveTileValidationResult") &&
            runtime.Contains("Placement = OverwrittenInstructionPlacement.AfterCallback"),
            "passive tile-validator result diagnostics");
        Check(runtime.Contains("GenerateAivHeightBypass") &&
            runtime.Contains("AddUnrestrictedJmp(createPathAddress)") &&
            runtime.Contains("AIV_MOAT_CREATE_ATTEMPT"),
            "AIV height gate jumps to its audited creation path with diagnostics");
        Check(runtime.Contains("BuildingR3EventHooks.OnBuildStructure.Observable") &&
            runtime.Contains("ObserveHumanMoatWriterResult") &&
            runtime.Contains("MOAT_BUILD:"),
            "human moat builder and writer result are diagnosed");
        Check(runtime.Contains("args.Mappers != eMappers.MAPPER_MOAT") &&
            !runtime.Contains("MAPPER_DRAWBRIDGE"),
            "diagnostics remain restricted to moat placement");
        Check(runtime.Contains("DisplacedByteCount != ElevatedMoatNativeContract.HeightWriterLength"),
            "actual displaced length is checked");
        Check(runtime.Contains("tileValidationResultHook.Hook.DisplacedByteCount !=") &&
            runtime.Contains("ElevatedMoatNativeContract.TileValidationResultLength"),
            "actual tile-result displaced length is checked");
        Check(runtime.Contains("FailureMode = TransactionFailureMode.RollbackAndThrow") &&
            runtime.Contains("OwnsHooks = true") && runtime.Contains("pending.Dispose()"),
            "hook errors roll back the owned transaction");
        Check(!runtime.Contains("Marshal.Write") &&
            !runtime.Contains("CustomValidationRules =") &&
            !runtime.Contains("ForceBlockPlacementState ="),
            "no broad managed placement-state override is introduced");
        Check(!runtime.Contains("AddDetour") && runtime.Contains("IsAIPlayer(playerId)"),
            "diagnostics avoid an overlapping detour of the shared moat writer");
        Check(runtime.Contains("RequireInstalledHookLength(") &&
            runtime.Contains("ElevatedMoatNativeContract.AivHeightGateLength") &&
            runtime.Contains("ElevatedMoatNativeContract.HumanMoatWriterResultLength"),
            "all new installed hook lengths are checked before activation");
        Check(runtime.Contains("SuppressSharedHeightGate") &&
            runtime.Contains("MOAT_NATIVE_ACTION:") &&
            runtime.Contains("OverwrittenInstructionPlacement.Suppress"),
            "shared editor/planning height gate is suppressed with native diagnostics");
        Check(runtime.Contains("ApplyCompletedHeight") &&
            runtime.Contains("TileDefaultHeightGridOffset") &&
            runtime.Contains("MOAT_HEIGHT_APPLIED:") &&
            runtime.Contains("OverwrittenInstructionPlacement.BeforeCallback"),
            "completed moats use post-Vanilla adaptive height correction");
        Check(runtime.Contains("RestoreDirectRemovalHeight") &&
            runtime.Contains("*current = defaultHeight"),
            "direct moat removal restores the original terrain height");
        Check(plugin.Contains("requireCurrentVersion: true") &&
            plugin.Contains("if (!referenceHashMatches)"), "native hash mismatch fails closed");
        Check(project.Contains(@"$(GameDir)\BepInEx\plugins\000shcdese") &&
            !project.Contains("LocalScriptExtender"), "build targets only the installed Script Extender");
        Check(project.Contains("<Reference Include=\"R3\">") &&
            project.Contains("<Private>false</Private>"), "R3 event dependency is not privately packaged");
        Check(project.Contains("<Reference Include=\"Iced\">") &&
            project.Contains(@"$(ExtenderDir)\Iced.dll"),
            "Iced instruction validation uses the installed Script Extender dependency");
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

    private static void CheckUniquePattern(byte[] file, string pattern, int expectedRva, string name)
    {
        byte[] bytes = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Convert.ToByte(value, 16)).ToArray();
        List<int> matches = FindAll(file, bytes);
        Check(matches.Count == 1, $"{name} contextual pattern occurs exactly once");
        if (matches.Count == 1)
            Check(FileOffsetToRva(file, matches[0]) == expectedRva,
                $"{name} maps to RVA 0x{expectedRva:X}");
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

    private static byte[] MapPeImage(byte[] file)
    {
        int peOffset = BitConverter.ToInt32(file, 0x3C);
        ushort sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
        ushort optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
        int optionalHeader = peOffset + 24;
        int sizeOfImage = BitConverter.ToInt32(file, optionalHeader + 56);
        int sizeOfHeaders = BitConverter.ToInt32(file, optionalHeader + 60);
        byte[] image = new byte[sizeOfImage];
        Array.Copy(file, 0, image, 0, Math.Min(sizeOfHeaders, file.Length));
        int sectionTable = optionalHeader + optionalHeaderSize;
        for (int section = 0; section < sectionCount; section++)
        {
            int header = sectionTable + section * 40;
            int virtualAddress = BitConverter.ToInt32(file, header + 12);
            int rawSize = BitConverter.ToInt32(file, header + 16);
            int rawStart = BitConverter.ToInt32(file, header + 20);
            int copyLength = Math.Min(rawSize, Math.Min(file.Length - rawStart, image.Length - virtualAddress));
            if (rawStart >= 0 && virtualAddress >= 0 && copyLength > 0)
                Array.Copy(file, rawStart, image, virtualAddress, copyLength);
        }
        return image;
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
