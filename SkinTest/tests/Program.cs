using SHCDESE.Interop;
using SkinTest;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--runtime-assembly")
        {
            TestRuntimeAssemblyDependencies(args[1]);
            Console.WriteLine("SkinTest compiled runtime dependency test passed.");
            return 0;
        }
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
        TestPolicy();
        TestAssets(root);
        TestStaticContracts(root);
        TestCrlf(root);
        Console.WriteLine("SkinTest policy, asset and static contract tests passed.");
        return 0;
    }

    private static void TestPolicy()
    {
        Check(SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_LORD), "Standard European lord must be eligible.");
        Check(SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_SCRIBE_LORD), "Scribe lord must be eligible.");
        Check(SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_LORD_FEMALE), "Female European lord must be eligible.");
        Check(SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_LORD_BESSY), "Bessy lord must be eligible.");
        Check(!SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_ARABIC_LORD), "Arabic lord must retain Vanilla.");
        Check(!SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_BEDOUIN_LORD), "Bedouin lord must retain Vanilla.");
        Check(SkinSelectionPolicy.CanReplaceVanilla(true, true,
                SkinSelectionPolicy.HasEligibleOwner(true, 1, 12, true,
                    SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_LORD)), SkinFrameChoice.Normal),
            "An European swordsman owned by an European lord must use the SH1DE skin.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(true, true,
                SkinSelectionPolicy.HasEligibleOwner(true, 2, 13, true,
                    SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_ARABIC_LORD)), SkinFrameChoice.Normal),
            "An European swordsman owned by an Arabic lord must retain Vanilla beside an eligible unit.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(true, true,
                SkinSelectionPolicy.HasEligibleOwner(true, 3, 14, true,
                    SkinSelectionPolicy.IsEuropeanLordMaterial(GM.GM_BODY_BEDOUIN_LORD)), SkinFrameChoice.Normal),
            "An European swordsman owned by a Bedouin lord must retain Vanilla beside an eligible unit.");

        Check(SkinSelectionPolicy.HasEligibleOwner(true, 1, 12, true, true), "A complete European owner chain must be eligible.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(false, 1, 12, true, true), "A missing unit must fail closed.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(true, 0, 12, true, true), "An ownerless unit must fail closed.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(true, 1, 0, true, true), "A missing lord ID must fail closed.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(true, 1, 12, false, true), "A missing lord unit must fail closed.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(true, 1, 12, true, false), "A non-European lord must fail closed.");

        Check(SkinSelectionPolicy.SelectFrame(false, true, false) == SkinFrameChoice.Normal, "Normal request must select the normal frame.");
        Check(SkinSelectionPolicy.SelectFrame(true, true, true) == SkinFrameChoice.Alternate, "Available alternate frame must be selected.");
        Check(SkinSelectionPolicy.SelectFrame(true, true, false) == SkinFrameChoice.Normal, "Missing alternate frame must fall back to normal.");
        Check(SkinSelectionPolicy.SelectFrame(true, false, false) == SkinFrameChoice.None, "Missing alternate and normal frames must fail closed.");
        Check(SkinSelectionPolicy.CanReplaceVanilla(true, true, true, SkinFrameChoice.Normal), "Untouched eligible swordsman must be replaceable.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(true, false, true, SkinFrameChoice.Normal), "A prior sprite replacement must win.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(false, true, true, SkinFrameChoice.Normal), "Other unit types must retain Vanilla.");
    }

    private static void TestAssets(string root)
    {
        string assets = Path.Combine(root, "Assets", "CrusaderSwordsman");
        string colourAtlas = Path.Combine(assets, "atlas.png");
        string maskAtlas = Path.Combine(assets, "atlas_m.png");
        string atlasJson = Path.Combine(assets, "atlas.json");
        Check(File.Exists(colourAtlas) && File.Exists(maskAtlas) && File.Exists(atlasJson), "Private atlas triplet is incomplete.");
        (int atlasWidth, int atlasHeight) = ReadPngSize(colourAtlas);
        Check(ReadPngSize(maskAtlas) == (atlasWidth, atlasHeight), "Atlas and mask dimensions differ.");
        AtlasManifest manifest = AtlasManifest.ParseAndValidate(File.ReadAllText(atlasJson), atlasWidth, atlasHeight);
        Check(manifest.Frames.Count(frame => !frame.Alternate) == AtlasManifest.NormalFrameCount, "Atlas normal-frame count differs.");
        Check(manifest.Frames.Count(frame => frame.Alternate) == AtlasManifest.AlternateFrameCount, "Atlas alternate-frame count differs.");
        TestInvalidAtlasDocuments(atlasJson, atlasWidth, atlasHeight);

        string colourSource = Path.Combine(root, "AtlasSource", "Colour");
        string metadataSource = Path.Combine(root, "AtlasSource", "Metadata");
        string[] colours = Directory.GetFiles(colourSource, "body_swordsman-*.png")
            .Where(path => !path.EndsWith("_m.png", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] masks = Directory.GetFiles(colourSource, "body_swordsman-*_m.png");
        string[] metadata = Directory.GetFiles(metadataSource, "body_swordsman-*.json");
        Check(colours.Length == 1216 && masks.Length == 1216 && metadata.Length == 1216,
            "AtlasSource must contain 1216 colour frames, masks and corrected metadata files.");

        foreach (string metadataPath in metadata)
        {
            var payload = Shared.DependencyFreeJson.Parse(File.ReadAllText(metadataPath)) as Dictionary<string, object>;
            Check(payload != null, $"Invalid source metadata: {metadataPath}");
            string name = (string)payload["m_Name"];
            var rect = (Dictionary<string, object>)payload["m_Rect"];
            var pivot = (Dictionary<string, object>)payload["m_Pivot"];
            int width = Convert.ToInt32(rect["m_Width"], CultureInfo.InvariantCulture);
            int height = Convert.ToInt32(rect["m_Height"], CultureInfo.InvariantCulture);
            float pivotX = Convert.ToSingle(pivot["m_X"], CultureInfo.InvariantCulture);
            float pivotY = Convert.ToSingle(pivot["m_Y"], CultureInfo.InvariantCulture);
            Check(width > 0 && height > 0 && Finite(pivotX) && Finite(pivotY), $"Invalid corrected rectangle or pivot: {name}");
            string colourPath = Path.Combine(colourSource, name + ".png");
            string maskPath = Path.Combine(colourSource, name + "_m.png");
            Check(ReadPngSize(colourPath) == (width, height), $"Colour dimensions differ from metadata: {name}");
            Check(ReadPngSize(maskPath) == (width, height), $"Mask dimensions differ from metadata: {name}");
        }
    }

    private static void TestStaticContracts(string root)
    {
        string runtime = File.ReadAllText(Path.Combine(root, "src", "SwordsmanSkinRuntime.cs"));
        string plugin = File.ReadAllText(Path.Combine(root, "src", "SkinTestPlugin.cs"));
        string assemblyInfo = File.ReadAllText(Path.Combine(root, "Properties", "AssemblyInfo.cs"));
        string project = File.ReadAllText(Path.Combine(root, "SkinTest.csproj"));
        string testsProject = File.ReadAllText(Path.Combine(root, "tests", "SkinTest.Tests.csproj"));
        string atlasManifest = File.ReadAllText(Path.Combine(root, "src", "AtlasManifest.cs"));
        string tests = File.ReadAllText(Path.Combine(root, "tests", "Program.cs"));
        string manifest = File.ReadAllText(Path.Combine(root, "info.json"));
        Check(manifest.Contains("\"NetworkMode\": 0") && manifest.Contains("\"MinimumScriptExtenderVersion\": \"2.3.0\""),
            "Manifest must declare a visual client mod for Script Extender 2.3.0.");
        Check(plugin.Contains("[BepInDependency(ScriptExtenderGuid, ScriptExtenderVersion)]") &&
              plugin.Contains("PluginVersion = \"0.1.0\""), "Plugin dependency/version contract differs.");
        Check(plugin.Contains("private static ManualLogSource persistentLog") &&
              plugin.Contains("private static SwordsmanSkinRuntime runtime") &&
              plugin.Contains("private static bool librarySubscriptionInstalled"),
            "The visual runtime must remain statically rooted after startup component destruction.");
        Check(!plugin.Contains("OnDestroy") && !plugin.Contains("OnDisable") && !plugin.Contains("OnApplicationQuit") &&
              !plugin.Contains("CrusaderLibrary.Instance.LibraryLoaded -=") &&
              !plugin.Contains("runtime?.Dispose()"),
            "Normal plugin lifecycle paths must not tear down process-wide hooks or subscriptions.");
        Check(plugin.Contains("if (runtime != null)") && plugin.Contains("if (librarySubscriptionInstalled)"),
            "Plugin and LibraryLoaded initialization must remain idempotent.");
        Check(plugin.Contains("candidate?.Dispose()"),
            "Runtime disposal must remain available only for failed initialization rollback.");
        Check(Regex.Matches(plugin, @"\.Dispose\s*\(").Count == 1,
            "The plugin may dispose only the unpublished failed initialization candidate.");
        Check(assemblyInfo.Contains("AssemblyVersion(\"0.1.0\")") &&
              assemblyInfo.Contains("AssemblyFileVersion(\"0.1.0\")") &&
              assemblyInfo.Contains("AssemblyInformationalVersion(\"0.1.0\")"),
            "Assembly version metadata must match the active mod version.");
        Check(runtime.Contains("GetModFileBinaryContent") && runtime.Contains("GetModFileTextContent"),
            "Assets must be loaded through the archive-compatible asset index.");
        Check(project.Contains("Shared\\DependencyFreeJson.cs") && testsProject.Contains("Shared\\DependencyFreeJson.cs") &&
              atlasManifest.Contains("Shared.DependencyFreeJson.Parse") && tests.Contains("Shared.DependencyFreeJson.Parse"),
            "Runtime and tests must use the shared dependency-free JSON codec.");
        string combinedJsonContract = project + testsProject + atlasManifest;
        Check(!combinedJsonContract.Contains("System.Web.Extensions") &&
              !combinedJsonContract.Contains("JavaScriptSerializer") &&
              !combinedJsonContract.Contains("System.Text.Json") &&
              !combinedJsonContract.Contains("Newtonsoft.Json") &&
              !combinedJsonContract.Contains("DataContractJsonSerializer") &&
              !combinedJsonContract.Contains("JsonUtility"),
            "No external or Unity JSON serializer may remain in SkinTest runtime code.");
        Check(runtime.Contains("TryGetUnitById(unitId") && runtime.Contains("TryGetUnitById(lordUnitId"),
            "One-based unit IDs must pass unchanged to TryGetUnitById.");
        Check(runtime.Contains("r_ControllableForPlayerId") && runtime.Contains("GetLordUnitId(ownerPlayerId)"),
            "Owner-to-lord resolution contract is missing.");
        Check(!Regex.IsMatch(runtime, @"r_GameMaterialIndex\s*="), "The visual mod must not write r_GameMaterialIndex.");
        Check(runtime.IndexOf("trampoline(renderer", StringComparison.Ordinal) < runtime.IndexOf("renderer.sprite =", StringComparison.Ordinal),
            "Vanilla and the existing hook chain must run before replacement.");
        Check(project.Contains("Assets\\**\\*") && !Directory.Exists(Path.Combine(root, "Override")),
            "Assets must remain private and must not install a global Override/Atlas replacement.");
        Check(runtime.Contains("SetBodySprite detour confirmed") &&
              runtime.Contains("int unitId = 0;") &&
              runtime.Contains("Swordsman sprite callback has no renderer binding") &&
              runtime.Contains("Bound swordsman unit could not be resolved") &&
              runtime.Contains("Swordsman has no controllable owner") &&
              runtime.Contains("Swordsman owner has no lord unit ID") &&
              runtime.Contains("Swordsman lord unit could not be resolved") &&
              runtime.Contains("Vanilla retained for non-European lord culture") &&
              runtime.Contains("SH1DE skin applied"),
            "Bounded culture decision diagnostics must remain present.");
    }

    private static void TestInvalidAtlasDocuments(string atlasJson, int atlasWidth, int atlasHeight)
    {
        var root = (Dictionary<string, object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(atlasJson));
        var frames = (List<object>)root["frames"];

        object removed = frames[0];
        frames.RemoveAt(0);
        ExpectFailure(() => AtlasManifest.ParseAndValidate(Shared.DependencyFreeJson.Serialize(root), atlasWidth, atlasHeight),
            "A missing atlas frame must be rejected.");
        frames.Insert(0, removed);

        var first = (Dictionary<string, object>)frames[0];
        var second = (Dictionary<string, object>)frames[1];
        object originalSecondName = second["name"];
        second["name"] = first["name"];
        ExpectFailure(() => AtlasManifest.ParseAndValidate(Shared.DependencyFreeJson.Serialize(root), atlasWidth, atlasHeight),
            "A duplicated atlas frame must be rejected.");
        second["name"] = originalSecondName;

        var rect = (Dictionary<string, object>)first["rect"];
        object originalWidth = rect["w"];
        rect["w"] = 0;
        ExpectFailure(() => AtlasManifest.ParseAndValidate(Shared.DependencyFreeJson.Serialize(root), atlasWidth, atlasHeight),
            "An invalid atlas rectangle must be rejected.");
        rect["w"] = originalWidth;
    }

    private static void TestRuntimeAssemblyDependencies(string assemblyPath)
    {
        Check(File.Exists(assemblyPath), $"Compiled runtime assembly is missing: {assemblyPath}");
        string[] forbidden = { "System.Web.Extensions", "System.Text.Json", "Newtonsoft.Json", "System.Runtime.Serialization" };
        string[] references = Assembly.ReflectionOnlyLoadFrom(Path.GetFullPath(assemblyPath))
            .GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        foreach (string name in forbidden)
            Check(!references.Contains(name, StringComparer.OrdinalIgnoreCase),
                $"Compiled runtime assembly has a forbidden JSON dependency: {name}");
        Check(!references.Any(name => name.IndexOf("Json", StringComparison.OrdinalIgnoreCase) >= 0),
            "Compiled runtime assembly must not reference a JSON serializer assembly.");
    }

    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (Exception) { return; }
        throw new InvalidOperationException(message);
    }

    private static void TestCrlf(string root)
    {
        string[] extensions = { ".cs", ".csproj", ".json", ".py", ".bat" };
        foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
        {
            byte[] bytes = File.ReadAllBytes(path);
            for (int index = 0; index < bytes.Length; index++)
                if (bytes[index] == 0x0A && (index == 0 || bytes[index - 1] != 0x0D))
                    throw new InvalidOperationException($"Text file contains a bare LF: {path}");
        }
    }

    private static (int Width, int Height) ReadPngSize(string path)
    {
        byte[] header = new byte[24];
        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.Read(header, 0, header.Length) != header.Length)
                throw new InvalidOperationException($"PNG header is truncated: {path}");
        }
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        Check(signature.SequenceEqual(header.Take(8)), $"File is not a PNG: {path}");
        return (ReadBigEndian(header, 16), ReadBigEndian(header, 20));
    }

    private static int ReadBigEndian(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
