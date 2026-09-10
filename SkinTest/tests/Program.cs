using SHCDESE.Interop;
using SkinTest;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
        Check(SkinSelectionPolicy.ClassifyLordMaterial(GM.GM_BODY_LORD) == LordCulture.European,
            "An actual European lord material must classify as European.");
        Check(SkinSelectionPolicy.ClassifyLordMaterial(GM.GM_BODY_ARABIC_LORD_FEMALE) == LordCulture.NonEuropean,
            "An actual Arabic female lord material must classify as non-European.");
        Check(SkinSelectionPolicy.ClassifyLordMaterial(GM.GM_BODY_SWORDSMAN) == LordCulture.Unknown,
            "An unrelated material must not be treated as a lord culture.");
        foreach (int graphicsType in new[] { 0, 3, 4, 5 })
            Check(SkinSelectionPolicy.ClassifyLordGraphicsType(graphicsType) == LordCulture.European,
                $"Lord graphics type {graphicsType} must classify as European.");
        foreach (int graphicsType in new[] { 1, 2, 6, 7 })
            Check(SkinSelectionPolicy.ClassifyLordGraphicsType(graphicsType) == LordCulture.NonEuropean,
                $"Lord graphics type {graphicsType} must classify as non-European.");
        Check(SkinSelectionPolicy.ClassifyLordGraphicsType(-1) == LordCulture.Unknown &&
              SkinSelectionPolicy.ClassifyLordGraphicsType(8) == LordCulture.Unknown,
            "Unknown lord graphics types must fail closed.");
        Check(SkinSelectionPolicy.CanReplaceVanilla(true, true,
                SkinSelectionPolicy.HasEligibleOwner(true, 1, LordCulture.European), SkinFrameChoice.Normal),
            "An European swordsman owned by an European lord must use the SH1DE skin.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(true, true,
                SkinSelectionPolicy.HasEligibleOwner(true, 2, LordCulture.NonEuropean), SkinFrameChoice.Normal),
            "An European swordsman owned by an Arabic lord must retain Vanilla beside an eligible unit.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(true, true,
                SkinSelectionPolicy.HasEligibleOwner(true, 3, LordCulture.NonEuropean), SkinFrameChoice.Normal),
            "An European swordsman owned by a Bedouin lord must retain Vanilla beside an eligible unit.");

        Check(SkinSelectionPolicy.HasEligibleOwner(true, 1, LordCulture.European), "A safely resolved European owner must be eligible.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(false, 1, LordCulture.European), "A missing unit must fail closed.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(true, 0, LordCulture.European), "An ownerless unit must fail closed.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(true, 1, LordCulture.Unknown), "An unresolved culture must fail closed.");
        Check(!SkinSelectionPolicy.HasEligibleOwner(true, 1, LordCulture.NonEuropean), "A non-European culture must fail closed.");

        Check(SkinSelectionPolicy.ToAtlasFrameIndex(1) == 0, "Body image 1 must map to atlas frame 0.");
        Check(SkinSelectionPolicy.ToAtlasFrameIndex(128) == 127, "Body image 128 must map to alternate atlas frame 127.");
        Check(SkinSelectionPolicy.ToAtlasFrameIndex(1088) == 1087, "Body image 1088 must map to normal atlas frame 1087.");
        Check(SkinSelectionPolicy.ToAtlasFrameIndex(0) == -1, "Body image 0 must fail closed.");
        Check(SkinSelectionPolicy.ToAtlasFrameIndex(-1) == -1, "Negative body images must fail closed.");

        Check(SkinSelectionPolicy.SelectFrame(false, true, false) == SkinFrameChoice.Normal, "Normal request must select the normal frame.");
        Check(SkinSelectionPolicy.SelectFrame(true, true, true) == SkinFrameChoice.Alternate, "Available alternate frame must be selected.");
        Check(SkinSelectionPolicy.SelectFrame(true, true, false) == SkinFrameChoice.Normal, "Missing alternate frame must fall back to normal.");
        Check(SkinSelectionPolicy.SelectFrame(true, false, false) == SkinFrameChoice.None, "Missing alternate and normal frames must fail closed.");
        Check(SkinSelectionPolicy.CanReplaceVanilla(true, true, true, SkinFrameChoice.Normal), "Untouched eligible swordsman must be replaceable.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(true, false, true, SkinFrameChoice.Normal), "A prior sprite replacement must win.");
        Check(!SkinSelectionPolicy.CanReplaceVanilla(false, true, true, SkinFrameChoice.Normal), "Other unit types must retain Vanilla.");
        Check(SkinSelectionPolicy.ReconcileEarlyAndActualCulture(LordCulture.European, LordCulture.Unknown) == LordCulture.European,
            "An unknown actual lord material must not erase a safely detected early culture.");
        Check(SkinSelectionPolicy.ReconcileEarlyAndActualCulture(LordCulture.European, LordCulture.NonEuropean) == LordCulture.NonEuropean,
            "A recognized actual lord culture must authoritatively replace the early culture.");
        Check(SkinSelectionPolicy.ShouldUseEuropeanHud(true, false, 1, LordCulture.European) &&
              SkinSelectionPolicy.ShouldUseEuropeanHud(true, false, 8, LordCulture.European),
            "European gameplay HUD must support all eight valid colour endpoints.");
        Check(!SkinSelectionPolicy.ShouldUseEuropeanHud(true, true, 4, LordCulture.European) &&
              !SkinSelectionPolicy.ShouldUseEuropeanHud(true, false, 4, LordCulture.NonEuropean) &&
              !SkinSelectionPolicy.ShouldUseEuropeanHud(false, false, 4, LordCulture.European),
            "Arabic, non-European and inactive-map HUD states must retain Vanilla.");
        Check(SkinSelectionPolicy.CanReplaceBuilding(true, true, 1, LordCulture.European, true),
            "An untouched available European round-tower frame must be replaceable.");
        Check(!SkinSelectionPolicy.CanReplaceBuilding(false, true, 1, LordCulture.European, true) &&
              !SkinSelectionPolicy.CanReplaceBuilding(true, false, 1, LordCulture.European, true) &&
              !SkinSelectionPolicy.CanReplaceBuilding(true, true, 2, LordCulture.NonEuropean, true) &&
              !SkinSelectionPolicy.CanReplaceBuilding(true, true, 1, LordCulture.European, false),
            "Tower replacement must fail closed for conflicts, other buildings, non-European owners and sparse gaps.");
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

        int removedColourPixels = 0;
        int removedMaskPixels = 0;
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

            var mesh = payload["_SkinTestMesh"] as Dictionary<string, object>;
            Check(mesh != null, $"Mesh reconstruction provenance is missing: {name}");
            var bounds = (Dictionary<string, object>)mesh["sourceBounds"];
            int left = Convert.ToInt32(bounds["left"], CultureInfo.InvariantCulture);
            int bottom = Convert.ToInt32(bounds["bottom"], CultureInfo.InvariantCulture);
            int right = Convert.ToInt32(bounds["right"], CultureInfo.InvariantCulture);
            int top = Convert.ToInt32(bounds["top"], CultureInfo.InvariantCulture);
            Check(right - left == width && top - bottom == height && left >= 0 && bottom >= 0 && right <= 8192 && top <= 8192,
                $"Mesh-derived source bounds are invalid: {name}");
            var vertices = (List<object>)mesh["vertices"];
            var triangles = (List<object>)mesh["triangles"];
            Check(vertices.Count >= 3 && triangles.Count >= 3 && triangles.Count % 3 == 0,
                $"Mesh topology is invalid: {name}");
            foreach (object vertexObject in vertices)
            {
                var vertex = (Dictionary<string, object>)vertexObject;
                double x = Convert.ToDouble(vertex["x"], CultureInfo.InvariantCulture);
                double y = Convert.ToDouble(vertex["y"], CultureInfo.InvariantCulture);
                Check(x >= -0.001 && y >= -0.001 && x <= width + 0.001 && y <= height + 0.001,
                    $"Mesh vertex is outside its reconstructed frame: {name}");
            }
            foreach (object indexObject in triangles)
            {
                int index = Convert.ToInt32(indexObject, CultureInfo.InvariantCulture);
                Check(index >= 0 && index < vertices.Count, $"Mesh triangle index is invalid: {name}");
            }
            Check(string.Equals((string)mesh["colourSha256"], Sha256(colourPath), StringComparison.Ordinal) &&
                  string.Equals((string)mesh["maskSha256"], Sha256(maskPath), StringComparison.Ordinal),
                $"A reconstructed frame differs from its mesh-validated output: {name}");
            removedColourPixels += Convert.ToInt32(mesh["removedColourAlphaPixels"], CultureInfo.InvariantCulture);
            removedMaskPixels += Convert.ToInt32(mesh["removedMaskAlphaPixels"], CultureInfo.InvariantCulture);
        }
        Check(removedColourPixels > 0 && removedMaskPixels > 0,
            "Mesh reconstruction must remove foreign alpha pixels from both colour and mask sources.");

        string castleAssets = Path.Combine(root, "Assets", "CrusaderRoundTower");
        string castleAtlas = Path.Combine(castleAssets, "atlas.png");
        string castleJson = Path.Combine(castleAssets, "atlas.json");
        Check(File.Exists(castleAtlas) && File.Exists(castleJson), "Private sparse castle atlas is incomplete.");
        (int castleWidth, int castleHeight) = ReadPngSize(castleAtlas);
        SparseAtlasManifest castleManifest = SparseAtlasManifest.ParseAndValidate(
            File.ReadAllText(castleJson), castleWidth, castleHeight, "tile_castle ", 1467, 1569);
        int[] castleIndices = castleManifest.Frames.Select(frame => frame.Index).OrderBy(index => index).ToArray();
        Check(castleIndices.Length == 1467 && castleIndices.Distinct().Count() == 1467 &&
              castleIndices.First() == 1 && castleIndices.Last() == 1569,
            "Sparse castle atlas source-index contract differs.");
        Check(Enumerable.Range(1, 1569).Except(castleIndices).Count() == 102,
            "Sparse castle atlas must retain exactly the expected 102 gaps inside 1..1569.");
        var castleRoot = (Dictionary<string, object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(castleJson));
        Check(((List<object>)castleRoot["sourceOnlyIndices"]).Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture))
              .SequenceEqual(Enumerable.Range(812, 260)) &&
              Convert.ToInt32(castleRoot["targetFrameCount"], CultureInfo.InvariantCulture) == 1234 &&
              Convert.ToInt32(castleRoot["targetMaximumIndex"], CultureInfo.InvariantCulture) == 1596,
            "SH1DE-only and installed SHCDE castle-index provenance differs.");
        string[] castleMetadata = Directory.GetFiles(Path.Combine(root, "AtlasSource", "CastleMetadata"), "tile_castle *.json");
        Check(castleMetadata.Length == 1467,
            "Corrected FullRect castle provenance is incomplete.");
        foreach (string metadataPath in castleMetadata)
        {
            var payload = (Dictionary<string, object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(metadataPath));
            var rect = (Dictionary<string, object>)payload["m_Rect"];
            var pivot = (Dictionary<string, object>)payload["m_Pivot"];
            var sourceBounds = (Dictionary<string, object>)payload["_SkinTestSource"];
            int width = Convert.ToInt32(rect["m_Width"], CultureInfo.InvariantCulture);
            int height = Convert.ToInt32(rect["m_Height"], CultureInfo.InvariantCulture);
            int left = Convert.ToInt32(sourceBounds["left"], CultureInfo.InvariantCulture);
            int bottom = Convert.ToInt32(sourceBounds["bottom"], CultureInfo.InvariantCulture);
            int right = Convert.ToInt32(sourceBounds["right"], CultureInfo.InvariantCulture);
            int top = Convert.ToInt32(sourceBounds["top"], CultureInfo.InvariantCulture);
            Check(width > 0 && height > 0 && right - left == width && top - bottom == height &&
                  left >= 0 && bottom >= 0 && right <= 8192 && top <= 8192 &&
                  Convert.ToSingle(payload["m_PixelsToUnits"], CultureInfo.InvariantCulture) == 64f &&
                  Finite(Convert.ToSingle(pivot["m_X"], CultureInfo.InvariantCulture)) &&
                  Finite(Convert.ToSingle(pivot["m_Y"], CultureInfo.InvariantCulture)),
                $"Castle FullRect provenance is invalid: {Path.GetFileName(metadataPath)}");
        }
        for (int leftIndex = 0; leftIndex < castleManifest.Frames.Count; leftIndex++)
            for (int rightIndex = leftIndex + 1; rightIndex < castleManifest.Frames.Count; rightIndex++)
            {
                AtlasFrame leftFrame = castleManifest.Frames[leftIndex];
                AtlasFrame rightFrame = castleManifest.Frames[rightIndex];
                bool overlaps = leftFrame.X < rightFrame.X + rightFrame.Width &&
                    leftFrame.X + leftFrame.Width > rightFrame.X &&
                    leftFrame.Y < rightFrame.Y + rightFrame.Height &&
                    leftFrame.Y + leftFrame.Height > rightFrame.Y;
                Check(!overlaps, $"Sparse castle atlas rectangles overlap: {leftFrame.Name} / {rightFrame.Name}");
            }
        TestInvalidSparseAtlasDocument(castleJson, castleWidth, castleHeight);

        string uiAssets = Path.Combine(root, "Assets", "CrusaderUI");
        string uiSource = Path.Combine(root, "AtlasSource", "UI");
        string[] troopNames = { "UIBuildingsO011", "UIBuildingsO012", "UIButtonsK007", "UIButtonsK008" };
        foreach (string name in troopNames)
        {
            string sourceImage = Path.Combine(uiSource, name + ".png");
            string mask = Path.Combine(uiSource, name + "_team-mask.png");
            Check(File.Exists(sourceImage) && File.Exists(mask) && ReadPngSize(sourceImage) == ReadPngSize(mask),
                $"Explicit HUD team mask is missing or has wrong dimensions: {name}");
            for (int colour = 1; colour <= 8; colour++)
            {
                string variant = Path.Combine(uiAssets, name + "_colour" + colour + ".png");
                Check(File.Exists(variant) && ReadPngSize(variant) == ReadPngSize(sourceImage),
                    $"HUD colour variant is missing or has wrong dimensions: {name}, colour={colour}");
            }
        }
        foreach (string towerName in new[] { "UIBuildingsK009", "UIBuildingsK010" })
            Check(File.Exists(Path.Combine(uiAssets, towerName + ".png")), $"Tower HUD asset is missing: {towerName}");
        Check(Directory.GetFiles(uiAssets, "*.png").Length == 34,
            "Private UI asset inventory must contain 32 troop variants and two tower images.");
        var provenance = Shared.DependencyFreeJson.Parse(File.ReadAllText(Path.Combine(uiSource, "provenance.json"))) as Dictionary<string, object>;
        Check(provenance != null && ((List<object>)provenance["sourceDimensions"])
              .Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).SequenceEqual(new[] { 8192, 4096 }),
            "UI source provenance or source dimensions are invalid.");
        var provenanceRects = (Dictionary<string, object>)provenance["rectangles"];
        foreach (string name in troopNames)
        {
            var record = (Dictionary<string, object>)provenanceRects[name];
            Check(string.Equals((string)record["sha256"], Sha256(Path.Combine(uiSource, name + ".png")), StringComparison.Ordinal) &&
                  string.Equals((string)record["teamMaskSha256"], Sha256(Path.Combine(uiSource, name + "_team-mask.png")), StringComparison.Ordinal),
                $"UI source or explicit team mask differs from provenance: {name}");
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
        Match extenderVersion = Regex.Match(plugin, @"ScriptExtenderVersion\s*=\s*""([^""]+)""");
        Check(extenderVersion.Success, "Plugin Script Extender version constant is missing.");
        Check(manifest.Contains("\"NetworkMode\": 0") &&
              manifest.Contains("\"MinimumScriptExtenderVersion\": \"" + extenderVersion.Groups[1].Value + "\""),
            "Manifest must declare a visual client mod matching the plugin's Script Extender version.");
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
        Check(runtime.Contains("GetAILord(ownerPlayerId)") && runtime.Contains("GetAICArray()") &&
              runtime.Contains("GetValue(aicIndex).lord_gfx_type") &&
              runtime.Contains("GameData.Instance.lastGameState.lord_Type") &&
              runtime.Contains("ConfigSettings.Settings_LordType"),
            "Safe pre-spawn culture sources for AI and the local player are incomplete.");
        Check(project.Contains("<Reference Include=\"RedBird.Core\"><HintPath>$(ExtenderDir)\\RedBird.Core.dll</HintPath><Private>false</Private></Reference>"),
            "The AIC array dependency must reference installed RedBird.Core without private packaging.");
        Check(runtime.Contains("cultureByPlayer") && runtime.Contains("Authoritative lord culture differs from early culture") &&
              runtime.Contains("Early culture resolved before lord spawn") &&
              runtime.Contains("ReconcileEarlyAndActualCulture") && runtime.Contains("unknown graphics material"),
            "Per-map early culture caching and authoritative reconciliation are missing.");
        Check(runtime.Contains("ai-culture-error:") && runtime.Contains("local-game-state-culture-error:") &&
              runtime.Contains("local-settings-culture-error:"),
            "AI, local game-state and local settings fallbacks require separate diagnostic failure paths.");
        Check(!Regex.IsMatch(runtime, @"r_GameMaterialIndex\s*="), "The visual mod must not write r_GameMaterialIndex.");
        Check(runtime.IndexOf("trampoline(renderer", StringComparison.Ordinal) < runtime.IndexOf("renderer.sprite =", StringComparison.Ordinal),
            "Vanilla and the existing hook chain must run before replacement.");
        Check(project.Contains("Assets\\**\\*") && !Directory.Exists(Path.Combine(root, "Override")),
            "Assets must remain private and must not install a global Override/Atlas replacement.");
        Check(runtime.Contains("SetBodySprite detour confirmed") &&
              runtime.Contains("int unitId = 0;") &&
              runtime.Contains("int frameIndex = SkinSelectionPolicy.ToAtlasFrameIndex(image);") &&
              runtime.Contains("Swordsman SetBodySprite callback") &&
              runtime.Contains("Swordsman sprite callback has no renderer binding") &&
              runtime.Contains("Bound swordsman unit could not be resolved") &&
              runtime.Contains("Swordsman has no controllable owner") &&
              runtime.Contains("Swordsman culture is not yet safely resolvable") &&
              runtime.Contains("Vanilla retained for non-European lord culture") &&
              runtime.Contains("SH1DE skin applied"),
            "Bounded culture decision diagnostics must remain present.");
        Check(runtime.Contains("GetGMSprite(GameGM.GM_BODY_SWORDSMAN, frameIndex, alternateFrame)") &&
              !Regex.IsMatch(runtime, @"(?:normalSprites|alternateSprites)\s*\[\s*image\s*\]") &&
              runtime.Contains("expected={DescribeSprite(expected)}") && runtime.Contains("actual={DescribeSprite(renderer.sprite)}"),
            "Swordsman sprite lookup, replacement and conflict diagnostics must use the zero-based atlas frame index.");
        Check(runtime.Contains("unitByRenderer[args.SpriteRenderer] = args.UnitId") &&
              runtime.Contains("SetBodySprite detour confirmed") && runtime.Contains("Early culture resolved before lord spawn"),
            "The renderer must be bound before the first early-culture sprite decision.");
        Check(runtime.Contains("troopHudTrampoline(instance, colour, arabic);") &&
              runtime.IndexOf("troopHudTrampoline(instance, colour, arabic);", StringComparison.Ordinal) < runtime.IndexOf("instance.UIBuildingsO011 =", StringComparison.Ordinal) &&
              runtime.Contains("SH1DE swordsman HUD activated"),
            "Troop HUD replacement must run after the existing hook chain and expose a runtime marker.");
        Check(runtime.Contains("buildingTrampoline(tile, file, image, light);") &&
              runtime.Contains("GetTileBuildingId(tileId)") && runtime.Contains("TryGetBuildingById(buildingId") &&
              runtime.Contains("STRUCT_TOWER5_DESTROYED") && runtime.Contains("SH1DE round-tower skin applied"),
            "Round-tower replacement, one-based building resolution or diagnostics are incomplete.");
        Check(runtime.Contains("FindName(\"ButtonBuildTowerE\")") && runtime.Contains("PropEx.SetSprite1") &&
              runtime.Contains("PropEx.SetSprite2") && runtime.Contains("RestoreTowerHud"),
            "Round-tower build HUD replacement/restoration is incomplete.");
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

    private static void TestInvalidSparseAtlasDocument(string atlasJson, int atlasWidth, int atlasHeight)
    {
        var root = (Dictionary<string, object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(atlasJson));
        var frames = (List<object>)root["frames"];
        var first = (Dictionary<string, object>)frames[0];
        var second = (Dictionary<string, object>)frames[1];
        object originalName = second["name"];
        object originalIndex = second["index"];
        second["name"] = first["name"];
        second["index"] = first["index"];
        ExpectFailure(() => SparseAtlasManifest.ParseAndValidate(Shared.DependencyFreeJson.Serialize(root), atlasWidth, atlasHeight,
            "tile_castle ", 1467, 1569), "A duplicated sparse castle frame must be rejected.");
        second["name"] = originalName;
        second["index"] = originalIndex;
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

    private static string Sha256(string path)
    {
        using (var algorithm = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
