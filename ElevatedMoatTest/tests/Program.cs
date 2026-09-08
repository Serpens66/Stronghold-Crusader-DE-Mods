using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using ElevatedMoatTest;
using RedBird.X64.Hooks;
using SHCDESE.Interop;

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
        Check(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterRva == 0x7870B,
            "audited drawbridge writer RVA");
        Check(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength == 20,
            "20-byte drawbridge hook boundary");
        Check(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes.Length == 20,
            "drawbridge writer byte count");
        Check((int)eMappers.MAPPER_DRAWBRIDGE == 105 &&
            (int)eMappers.MAPPER_MOAT == 106 &&
            (int)eMappers.MAPPER_ANTIMOAT == 107,
            "installed Script Extender mapper enum contract");
        Check(ElevatedMoatNativeContract.MaximumVanillaTerrainHeight == 12, "Vanilla height threshold");
        Check(ElevatedMoatNativeContract.PlacementFailureReason == 24, "Vanilla failure reason");
        Check(BitConverter.ToInt32(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes, 2) ==
            ElevatedMoatNativeContract.PlacementBlockedOffset &&
            BitConverter.ToInt32(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes, 6) ==
            ElevatedMoatNativeContract.PlacementBlockedValue,
            "first 10-byte MOV writes blocked status 1");
        Check(BitConverter.ToInt32(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes, 12) ==
            ElevatedMoatNativeContract.PlacementFailureReasonOffset &&
            BitConverter.ToInt32(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes, 16) ==
            ElevatedMoatNativeContract.PlacementFailureReason,
            "second 10-byte MOV writes failure reason 24");
        Check(ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix[8] ==
            checked((byte)eMappers.MAPPER_DRAWBRIDGE) &&
            BitConverter.ToInt32(ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix, 13) ==
            ElevatedMoatNativeContract.MaximumFootprintHeightOffset &&
            ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix[17] ==
            ElevatedMoatNativeContract.MaximumVanillaTerrainHeight,
            "prefix encodes eMappers.MAPPER_DRAWBRIDGE and maxHeight comparison");
        Check(ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix[9] == 0x75 &&
            ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix[10] == 0x1D &&
            ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix[18] == 0x7E &&
            ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix[19] == 0x14,
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
        Check(ElevatedMoatNativeContract.DrawbridgeWriterResultRva == 0x73B24 &&
            ElevatedMoatNativeContract.DrawbridgeWriterResultLength == 15 &&
            ElevatedMoatNativeContract.DrawbridgeWriterResultBytes.Length == 15,
            "audited 15-byte drawbridge writer-result boundary");
        Check(ElevatedMoatNativeContract.MoatCommandHeightGateRva == 0x5CC1E &&
            ElevatedMoatNativeContract.MoatCommandHeightGateLength == 14 &&
            ElevatedMoatNativeContract.MoatCommandHeightGateBytes.Length == 14,
            "audited MAPPER_MOAT/MAPPER_ANTIMOAT command height gate");
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
        Array.Copy(ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix, 0, image,
            writerOffset - ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix.Length,
            ElevatedMoatNativeContract.DrawbridgeHeightFailurePrefix.Length);
        Array.Copy(ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes, 0, image,
            writerOffset, ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes.Length);
        Array.Copy(ElevatedMoatNativeContract.DrawbridgeHeightFailureSuffix, 0, image,
            writerOffset + ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength,
            ElevatedMoatNativeContract.DrawbridgeHeightFailureSuffix.Length);

        ExpectNoThrow(() => ElevatedMoatNativeContract.ValidateDrawbridgeHeightFailure(image, writerOffset),
            "complete native validation window");
        image[writerOffset - 1] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateDrawbridgeHeightFailure(image, writerOffset),
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

        List<int> matches = FindAll(file, ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterBytes);
        Check(matches.Count == 1, "drawbridge writer pattern occurs exactly once");
        if (matches.Count == 1)
            Check(FileOffsetToRva(file, matches[0]) ==
                ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterRva,
                "unique drawbridge writer maps to RVA 0x7870B");

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

        List<int> drawbridgeResultMatches = FindAll(
            file,
            ElevatedMoatNativeContract.DrawbridgeWriterResultResolutionBytes);
        Check(drawbridgeResultMatches.Count == 1, "drawbridge writer-result pattern occurs exactly once");
        if (drawbridgeResultMatches.Count == 1)
            Check(FileOffsetToRva(file, drawbridgeResultMatches[0]) ==
                ElevatedMoatNativeContract.DrawbridgeWriterResultRva,
                "unique drawbridge writer result maps to RVA 0x73B24");
        ExpectNoThrow(() => ElevatedMoatNativeContract.ValidateDrawbridgeWriterResultHook(
            image,
            ElevatedMoatNativeContract.DrawbridgeWriterResultRva),
            "drawbridge writer CALL target, function boundary, and continuation");
        byte[] changedDrawbridgeImage = (byte[])image.Clone();
        changedDrawbridgeImage[ElevatedMoatNativeContract.DrawbridgeWriterCallRva + 1] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateDrawbridgeWriterResultHook(
            changedDrawbridgeImage,
            ElevatedMoatNativeContract.DrawbridgeWriterResultRva),
            "changed drawbridge writer CALL target fails closed");

        CheckUniquePattern(file, ElevatedMoatNativeContract.SharedHeightGatePattern,
            ElevatedMoatNativeContract.SharedHeightGateRva, "shared editor/planning height gate");
        CheckUniquePattern(file, ElevatedMoatNativeContract.MoatCommandHeightGatePattern,
            ElevatedMoatNativeContract.MoatCommandHeightGateRva,
            "MAPPER_MOAT/MAPPER_ANTIMOAT command height gate");
        CheckUniquePattern(file, ElevatedMoatNativeContract.AivCompletedHeightPattern,
            ElevatedMoatNativeContract.AivCompletedHeightRva, "AIV completed-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.ExcavationCompletedHeightPattern,
            ElevatedMoatNativeContract.ExcavationCompletedHeightRva, "excavated-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.RebuildCompletedHeightPattern,
            ElevatedMoatNativeContract.RebuildCompletedHeightRva, "rebuilt-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.DirectCompletedHeightPattern,
            ElevatedMoatNativeContract.DirectCompletedHeightRva, "direct completed-moat height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.DrawbridgeCompletedHeightPattern,
            ElevatedMoatNativeContract.DrawbridgeCompletedHeightRva, "completed-drawbridge height");
        CheckUniquePattern(file, ElevatedMoatNativeContract.PlannedFillRestorePattern,
            ElevatedMoatNativeContract.PlannedFillRestoreRva, "planned moat-fill height restoration");
        CheckUniquePattern(file, ElevatedMoatNativeContract.DirectRemovalHeightPattern,
            ElevatedMoatNativeContract.DirectRemovalHeightRva, "direct moat-removal height");
        ExpectNoThrow(() => ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(image),
            "adaptive-height function, byte, branch, and call contracts");
        byte[] changedAdaptiveImage = (byte[])image.Clone();
        changedAdaptiveImage[ElevatedMoatNativeContract.DirectCompletedHeightRva + 1] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(changedAdaptiveImage),
            "changed adaptive-height block fails closed");
        byte[] changedMoatMapperImage = (byte[])image.Clone();
        changedMoatMapperImage[ElevatedMoatNativeContract.MoatCommandHeightGateRva + 17] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(changedMoatMapperImage),
            "changed eMappers.MAPPER_MOAT immediate fails closed");
        byte[] changedAntiMoatMapperImage = (byte[])image.Clone();
        changedAntiMoatMapperImage[ElevatedMoatNativeContract.MoatCommandHeightGateRva + 0x6E] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(changedAntiMoatMapperImage),
            "changed eMappers.MAPPER_ANTIMOAT immediate fails closed");
        byte[] changedPlannedFillImage = (byte[])image.Clone();
        changedPlannedFillImage[ElevatedMoatNativeContract.PlannedFillRestoreRva - 1] ^= 1;
        ExpectThrows(() => ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(changedPlannedFillImage),
            "changed planned-fill mode/flag prefix fails closed");
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
            ElevatedMoatNativeContract.DrawbridgeWriterResultRva,
            ElevatedMoatNativeContract.DrawbridgeWriterResultLength,
            "installed RedBird displaces exactly 15 bytes at the drawbridge result hook");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.MoatCommandHeightGateRva,
            ElevatedMoatNativeContract.MoatCommandHeightGateLength,
            "installed RedBird displaces exactly 14 bytes at the moat-command height gate");
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
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.DrawbridgeCompletedHeightRva,
            ElevatedMoatNativeContract.DrawbridgeCompletedHeightLength,
            "installed RedBird displaces exactly 17 bytes at the drawbridge height block");
        CheckRedBirdDisplacement(image, ElevatedMoatNativeContract.PlannedFillRestoreRva,
            ElevatedMoatNativeContract.PlannedFillRestoreLength,
            "installed RedBird displaces exactly 15 bytes at the planned moat-fill restoration block");
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
        Check(runtime.Contains("HookSize = ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength"),
            "explicit drawbridge hook size");
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
            runtime.Contains("ObserveDrawbridgeWriterResult") &&
            runtime.Contains("DRAWBRIDGE_BUILD:") &&
            runtime.Contains("args.Mappers != eMappers.MAPPER_DRAWBRIDGE"),
            "the formerly misidentified structure path is named and filtered as drawbridge");
        Check(runtime.Contains("eMappers.MAPPER_MOAT") &&
            runtime.Contains("eMappers.MAPPER_ANTIMOAT") &&
            runtime.Contains("GenerateMoatCommandHeightBypass") &&
            runtime.Contains("foreach (Instruction instruction in overwrittenInstructions)"),
            "actual moat planning and filling use Extender enum symbols with fail-closed Vanilla fallback");
        Check(runtime.Contains("DisplacedByteCount !=") &&
            runtime.Contains("ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength"),
            "actual drawbridge displaced length is checked");
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
            runtime.Contains("ElevatedMoatNativeContract.DrawbridgeWriterResultLength") &&
            runtime.Contains("ElevatedMoatNativeContract.MoatCommandHeightGateLength") &&
            runtime.Contains("ElevatedMoatNativeContract.PlannedFillRestoreLength"),
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
            runtime.Contains("RestorePlannedFillHeight") &&
            runtime.Contains("RestoreOriginalHeight") &&
            runtime.Contains("*current = defaultHeight"),
            "direct removal and MAPPER_ANTIMOAT filling restore the original terrain height");
        Check(runtime.Contains("GameTileManagerAPI.MAX_WIDTH * GameTileManagerAPI.MAX_HEIGHT") &&
            !runtime.Contains("tileId < 64000"),
            "tile bounds derive from Script Extender dimensions instead of a moat-slot literal");
        string nativeContract = File.ReadAllText(
            Path.Combine(modRoot, "src", "ElevatedMoatNativeContract.cs"));
        string forbiddenNumericMapperName = "Mapper" + "Moat";
        Check(!nativeContract.Contains("const int " + forbiddenNumericMapperName) &&
            !nativeContract.Contains(forbiddenNumericMapperName + " ="),
            "a numeric mod-owned MapperMoat constant cannot be reintroduced");
        Check(nativeContract.Contains("eMappers.MAPPER_DRAWBRIDGE") &&
            nativeContract.Contains("eMappers.MAPPER_MOAT") &&
            nativeContract.Contains("eMappers.MAPPER_ANTIMOAT"),
            "native immediates are statically tied to Extender mapper symbols");
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
