using SHCDESE.Interop;
using SkinTest;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static int Main()
    {
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

        string colourSource = Path.Combine(root, "AtlasSource", "Colour");
        string metadataSource = Path.Combine(root, "AtlasSource", "Metadata");
        string[] colours = Directory.GetFiles(colourSource, "body_swordsman-*.png")
            .Where(path => !path.EndsWith("_m.png", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] masks = Directory.GetFiles(colourSource, "body_swordsman-*_m.png");
        string[] metadata = Directory.GetFiles(metadataSource, "body_swordsman-*.json");
        Check(colours.Length == 1216 && masks.Length == 1216 && metadata.Length == 1216,
            "AtlasSource must contain 1216 colour frames, masks and corrected metadata files.");

        var serializer = new JavaScriptSerializer();
        foreach (string metadataPath in metadata)
        {
            var payload = serializer.DeserializeObject(File.ReadAllText(metadataPath)) as Dictionary<string, object>;
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
        string manifest = File.ReadAllText(Path.Combine(root, "info.json"));
        Check(manifest.Contains("\"NetworkMode\": 0") && manifest.Contains("\"MinimumScriptExtenderVersion\": \"2.3.0\""),
            "Manifest must declare a visual client mod for Script Extender 2.3.0.");
        Check(plugin.Contains("[BepInDependency(ScriptExtenderGuid, ScriptExtenderVersion)]") &&
              plugin.Contains("PluginVersion = \"0.1.0\""), "Plugin dependency/version contract differs.");
        Check(assemblyInfo.Contains("AssemblyVersion(\"0.1.0\")") &&
              assemblyInfo.Contains("AssemblyFileVersion(\"0.1.0\")") &&
              assemblyInfo.Contains("AssemblyInformationalVersion(\"0.1.0\")"),
            "Assembly version metadata must match the active mod version.");
        Check(runtime.Contains("GetModFileBinaryContent") && runtime.Contains("GetModFileTextContent"),
            "Assets must be loaded through the archive-compatible asset index.");
        Check(runtime.Contains("TryGetUnitById(unitId") && runtime.Contains("TryGetUnitById(lordUnitId"),
            "One-based unit IDs must pass unchanged to TryGetUnitById.");
        Check(runtime.Contains("r_ControllableForPlayerId") && runtime.Contains("GetLordUnitId(ownerPlayerId)"),
            "Owner-to-lord resolution contract is missing.");
        Check(!Regex.IsMatch(runtime, @"r_GameMaterialIndex\s*="), "The visual mod must not write r_GameMaterialIndex.");
        Check(runtime.IndexOf("trampoline(renderer", StringComparison.Ordinal) < runtime.IndexOf("renderer.sprite =", StringComparison.Ordinal),
            "Vanilla and the existing hook chain must run before replacement.");
        Check(project.Contains("Assets\\**\\*") && !Directory.Exists(Path.Combine(root, "Override")),
            "Assets must remain private and must not install a global Override/Atlas replacement.");
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
