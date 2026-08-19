using CustomCustomTrail.Core;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("bundled mission loads", TestBundledMission),
    ("path escape rejected", TestPathEscape),
    ("invalid rotation rejected", TestInvalidRotation),
    ("package catalog rejects invalid packages", TestCatalogIsolation),
    ("schema 1 is rejected", TestSchemaOneRejected),
    ("locally edited mission JSON reloads from the same slot", TestEditedMissionReload),
    ("invalid mod settings disable transaction", TestInvalidModSettings),
    ("first two active players become allied humans", TestHumanProjection),
    ("preferred AIV permits differing rotations", TestPreferredAiv),
    ("fourth trail tenth slot is addressable", TestLastCatalogSlot),
    ("package fingerprint detects content changes", TestPackageFingerprint),
    ("duplicate package IDs are rejected", TestDuplicatePackageIds),
    ("ordinal mapping covers four trails and ignores mission 41", TestOrdinalMapping),
    ("native mod-settings JSON roundtrip", TestNativeModSettingsRoundtrip),
    ("mod-settings registry has seven entries", TestModSettingsRegistry),
    ("missing mod entry becomes disabled", TestMissingModEntry),
    ("sidecar schema evolution keeps only current settings", TestSidecarSettingsSchemaEvolution),
    ("coop mission schema evolution keeps only current settings", TestCoopSettingsSchemaEvolution),
    ("invalid mod-settings documents are rejected", TestInvalidModSettingsDocuments),
    ("atomic sidecar write replaces existing file", TestAtomicSidecarWrite),
    ("Trail coordinator ownership is centralized", TestCoordinatorOwnership),
    ("local activation setting gates the complete runtime", TestLocalActivationSetting),
    ("Trail Maker Coop export is integrated", TestCoopExporterIntegration),
    ("Coop package JSON is Unity dependency-free", TestDependencyFreeCoopJson),
    ("mission and manifest JSON use CRLF", TestCoopJsonLineEndings),
};

int failed = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine("PASS " + name);
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine("FAIL " + name + ": " + ex);
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static void TestBundledMission()
{
    using Fixture fixture = Fixture.Create();
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(loaded.BundledFiles.Count == 3, "expected map, lord and AIV bundle files");
    Assert(loaded.Definition.Players.Where(player => player.Active).Take(2).Count() == 2, "human slots missing");
}

static void TestNativeModSettingsRoundtrip()
{
    ModSettingsDefinition document = ModSettingsDefinition.CreateDisabled();
    document.Mods["StartConditions_Serp"] = new ModSettingsEntry
    {
        Enabled = true,
        Settings = new Dictionary<string, object>
        {
            ["Bool"] = true,
            ["Int"] = 42,
            ["Double"] = 1.25,
            ["String"] = "Wood=10\r\nStone=-1",
        },
    };
    string json = ModSettingsJson.Serialize(document);
    Assert(json.Contains("\r\n"), "serialized JSON has no CRLF");
    Assert(!json.Replace("\r\n", string.Empty).Contains('\n'), "serialized JSON contains naked LF");
    ModSettingsEntry entry = ModSettingsJson.ParseObject(json).Mods["StartConditions_Serp"];
    Assert(entry.Enabled && (bool)entry.Settings["Bool"], "bool changed");
    Assert(Convert.ToInt32(entry.Settings["Int"]) == 42, "int changed");
    Assert(Math.Abs(Convert.ToDouble(entry.Settings["Double"]) - 1.25) < 0.0001, "double changed");
    Assert((string)entry.Settings["String"] == "Wood=10\r\nStone=-1", "complex string changed");
}

static void TestModSettingsRegistry()
{
    string[] expected =
    {
        "BuildingCosts_Serp", "BuildingLimit_Serp", "ExtraFeatures_Serp",
        "RandomEvents_Serp", "StartConditions_Serp", "UnitCosts_Serp", "UnitLimit_Serp",
    };
    Assert(ModSettingsDefinition.TargetModIds.SequenceEqual(expected), "central target-mod registry changed");
}

static void TestMissingModEntry()
{
    ModSettingsDefinition parsed = ModSettingsJson.ParseObject("{\"schemaVersion\":1,\"mods\":{}}");
    Assert(parsed.Mods.Count == 7 && parsed.Mods.Values.All(entry => !entry.Enabled), "missing entries were not disabled");
}

static void TestSidecarSettingsSchemaEvolution()
{
    ModSettingsDefinition parsed = ModSettingsJson.ParseObject(
        "{\"schemaVersion\":1,\"mods\":{\"ExtraFeatures_Serp\":{\"enabled\":true,\"settings\":{\"CurrentSetting\":7,\"RemovedSetting\":99}}}}");
    string[] removed = ModSettingsJson.RemoveUnknownSettings(
        parsed,
        "ExtraFeatures_Serp",
        new[] { "CurrentSetting", "NewSetting" });

    Assert(removed.SequenceEqual(new[] { "RemovedSetting" }), "obsolete sidecar setting was not identified");
    Assert(parsed.Mods["ExtraFeatures_Serp"].Settings.ContainsKey("CurrentSetting"), "current sidecar setting was removed");
    Assert(!parsed.Mods["ExtraFeatures_Serp"].Settings.ContainsKey("NewSetting"), "missing new setting was fabricated instead of using the ViewModel default");
    string serialized = ModSettingsJson.Serialize(parsed);
    Assert(!serialized.Contains("RemovedSetting", StringComparison.Ordinal), "obsolete sidecar setting was written again");
}

static void TestCoopSettingsSchemaEvolution()
{
    using Fixture fixture = Fixture.Create(includeLegacyModSetting: true);
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    string[] removed = ModSettingsJson.RemoveUnknownSettings(
        loaded.Definition.ModSettings,
        "StartConditions_Serp",
        new[] { "SetStartGoldHuman", "NewSetting" });

    ModSettingsEntry entry = loaded.Definition.ModSettings.Mods["StartConditions_Serp"];
    Assert(removed.SequenceEqual(new[] { "RemovedSetting" }), "obsolete coop mission setting was not identified");
    Assert(Convert.ToInt32(entry.Settings["SetStartGoldHuman"]) == 500, "current coop mission setting changed");
    Assert(!entry.Settings.ContainsKey("NewSetting"), "missing coop mission setting was fabricated instead of using the ViewModel default");
}

static void TestInvalidModSettingsDocuments()
{
    ExpectFailure(() => ModSettingsJson.ParseObject("broken"), "corrupt JSON was accepted");
    ExpectFailure(() => ModSettingsJson.ParseObject("{\"schemaVersion\":2,\"mods\":{}}"), "schema 2 sidecar was accepted");
    ExpectFailure(
        () => ModSettingsJson.ParseObject("{\"schemaVersion\":1,\"mods\":{\"UnitLimit_Serp\":{\"enabled\":true,\"settings\":{\"Limit\":{\"bad\":1}}}}}"),
        "object setting was accepted");
}

static void TestAtomicSidecarWrite()
{
    string root = Path.Combine(Path.GetTempPath(), "CustomCustomTrailSidecarTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        string path = Path.Combine(root, "Trail_Mission_1.modjson");
        File.WriteAllText(path, "old");
        ModSettingsDefinition document = ModSettingsDefinition.CreateDisabled();
        document.Mods["UnitLimit_Serp"].Enabled = true;
        ModSettingsJson.WriteAtomic(path, document);
        Assert(ModSettingsJson.Read(path).Mods["UnitLimit_Serp"].Enabled, "replacement was not readable");
        Assert(!Directory.GetFiles(root, "*.tmp-*").Any(), "temporary file remained");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestCoordinatorOwnership()
{
    string projectRoot = FindProjectRoot();
    string workspaceRoot = Directory.GetParent(projectRoot)?.FullName ?? throw new InvalidOperationException("workspace root missing");
    string[] mods = { "BuildingCosts", "BuildingLimit", "ExtraFeatures", "RandomEvents", "StartConditions", "UnitCosts", "UnitLimit" };
    foreach (string mod in mods)
    {
        string project = File.ReadAllText(Path.Combine(workspaceRoot, mod, mod + ".csproj"));
        Assert(!project.Contains("TrailModSettings") && !project.Contains("ModSettingsJson"), mod + " still compiles Trail runtime code");
    }

    string coordinator = File.ReadAllText(Path.Combine(projectRoot, "src", "TrailMissionSettingsCoordinator.cs"));
    string sharedPresetSystem = File.ReadAllText(Path.Combine(workspaceRoot, "Shared", "PresetLobbyModSettingsViewModel.cs"));
    for (int trail = 1; trail <= 4; trail++)
        Assert(CountOccurrences(coordinator, "FRONT_CoopTrail" + trail + ".Instance") == 1, "Coop Trail " + trail + " button registration is not singular");
    Assert(coordinator.Contains("Margin = new Thickness(0, 0, 0, -30)"),
        "Coop Customize is not positioned below the Vanilla kick button");
    string runtime = File.ReadAllText(Path.Combine(projectRoot, "src", "CustomCustomTrailRuntime.cs"));
    string obsoleteBridgeName = "TrailModSettings" + "Bridge";
    Assert(!runtime.Contains("SerializeModSettings") && !runtime.Contains(obsoleteBridgeName), "Coop settings still use the old bridge roundtrip");
    Assert(runtime.Contains("missionSettingsCoordinator?.ExitContext(force: true)"), "map unload does not leave the mission preset context");
    Assert(coordinator.Contains("CaptureDocument(requireLoadedEndpoints: true)"),
        "Trail saves do not validate their synchronous pre-save settings capture");
    Assert(coordinator.Contains("System_CreateDisabledMissionPresetSnapshot") &&
        coordinator.Contains("RemoveUnknownSettings"),
        "Trail loading does not combine current defaults with schema cleanup");
    Assert(sharedPresetSystem.Contains("CopyProperties(defaults, hostProperties)") &&
        sharedPresetSystem.Contains("defaults.TryGetValue(property.Name, out bytes)"),
        "the shared mission preset no longer supplies defaults for missing current host settings");
    Assert(!coordinator.Contains("pendingTrailMakerSaveDocument"),
        "Trail saves still retain a snapshot for the next save operation");
    Assert(coordinator.Contains("!openingCustomTrailSetup") &&
        coordinator.Contains("openingCustomTrailSetup = true") &&
        coordinator.Contains("openingCustomTrailSetup = false"),
        "Custom Trail setup does not suppress transient selection-sidecar loads");
    Assert(!coordinator.Contains("all Trail mods will be disabled"),
        "capture failures can still overwrite a sidecar with an all-disabled fallback");
}

static void TestLocalActivationSetting()
{
    string root = FindProjectRoot();
    string viewModel = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailSettingsViewModel.cs"));
    string plugin = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailPlugin.cs"));
    string runtime = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailRuntime.cs"));
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string xaml = File.ReadAllText(Path.Combine(root, "Override", "ScriptExtenderUI", "CustomCustomTrailSettings.xaml"));

    Assert(viewModel.Contains("[PersistLocal]"), "activation setting is not local-only persisted");
    int enableAttribute = viewModel.IndexOf("[PersistLocal]", StringComparison.Ordinal);
    int enableProperty = viewModel.IndexOf("public bool EnableMod", StringComparison.Ordinal);
    Assert(enableAttribute >= 0 && enableAttribute < enableProperty, "activation setting is not local-only persisted");
    Assert(viewModel.Contains("[SyncHostOnly]") && viewModel.Contains("ActiveCoopPackageId"), "package selection is not host-synchronised");
    Assert(viewModel.Contains("[SyncPerPlayer, DoNotPersist]") && viewModel.Contains("CoopPackageStatusData"), "package validation is not reported per player");
    Assert(plugin.Contains("Settings.EnableModChanged += runtime.SetEnabled"), "runtime does not observe activation changes");
    Assert(runtime.Contains("RestoreVanillaMissions()"), "disabling cannot restore replaced Vanilla Coop slots");
    Assert(coordinator.Contains("if (!enabled)"), "sidecar/customization hooks are not activation-gated");
    Assert(xaml.Contains("ToolTipService.ShowDuration=\"60000\""), "activation control tooltip duration is missing");
    Assert(xaml.Contains("PracticalEffectsText") && viewModel.Contains("CustomCustomTrail.PracticalEffects"),
        "player-facing practical-effects text is not bound below the activation setting");
    Assert(!xaml.Contains("SelectedPreset") && !xaml.Contains("PresetOptions"), "activation UI unexpectedly exposes presets");
    Assert(xaml.Contains("CoopPackageOptions") && xaml.Contains("CanEditCoopPackage"), "host package dropdown is missing");
    Assert(xaml.Contains("SelectedItem=\"{Binding SelectedCoopPackage, Mode=TwoWay}\"") &&
        !xaml.Contains("SelectedCoopPackageIndex"),
        "Coop package dropdown does not use the stable SpawnCastle-style SelectedItem binding");
    Assert(viewModel.Contains("coopPackageIds = new[] { string.Empty }") &&
        viewModel.Contains("CustomCustomTrail.VanillaPackage"),
        "Coop package dropdown does not initialize with Vanilla");
    Assert(!viewModel.Contains("PackagesRefreshRequested?.Invoke()") &&
        viewModel.Contains("if (unchanged)"),
        "Coop package dropdown still replaces its ItemsSource reentrantly");
}

static void TestCoopExporterIntegration()
{
    string root = FindProjectRoot();
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string exporter = File.ReadAllText(Path.Combine(root, "src", "CoopTrailPackageExporter.cs"));
    string runtime = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailRuntime.cs"));
    Assert(coordinator.Contains("CustomCustomTrailCoopExport") && coordinator.Contains("cooptrail.enabled"), "Trail Maker Coop checkbox/marker is missing");
    Assert(coordinator.Contains("Orientation = Orientation.Horizontal") && coordinator.Contains("host.Children.Remove(anchor)"),
        "Trail Maker Coop and Backup options are not arranged side by side");
    Assert(coordinator.Contains("prepared.Publish(destination)") && coordinator.IndexOf("Prepare(", StringComparison.Ordinal) < coordinator.LastIndexOf("exportOriginal(self, destination)", StringComparison.Ordinal),
        "Coop package is not validated before Vanilla export");
    Assert(exporter.Contains("ordinal < 40") && exporter.Contains("activeSlots.Count < 2"), "export limits or two-human validation are missing");
    Assert(exporter.Contains("ModSettingsJson.Read(sidecar)") && exporter.Contains("ModSettingsDefinition.CreateDisabled()"), "mission mod-settings embedding is missing");
    Assert(exporter.Contains("restart.selectedHeader.display_filename") && runtime.Contains("CoopMissionTitle = selected.Loaded.Definition.DisplayName"),
        "exported map names are not shown as Coop mission titles");
    Assert(coordinator.Contains("SetCoopPackagePresentation") && coordinator.Contains("TEXT_COOP_0"),
        "package display names do not replace occupied Vanilla Coop Trail headings");
    Assert(runtime.Contains("ReadyLock") && runtime.Contains("AreAllHumanPlayersPackageReady"), "Ready/Play package validation is missing");
    Assert(!runtime.Contains("Path.Combine(pluginRoot, \"CoopTrails\")"), "legacy plugin-local package layout is still active");
}

static void TestDependencyFreeCoopJson()
{
    string root = FindProjectRoot();
    string core = Path.Combine(root, "CustomCustomTrail.Core");
    string[] offenders = Directory.GetFiles(core, "*.cs", SearchOption.TopDirectoryOnly)
        .Where(path => File.ReadAllText(path).Contains("System.Runtime.Serialization", StringComparison.Ordinal) ||
            File.ReadAllText(path).Contains("DataContractJsonSerializer", StringComparison.Ordinal))
        .ToArray();
    Assert(offenders.Length == 0, "runtime JSON serializer dependency remains: " + string.Join(", ", offenders.Select(Path.GetFileName)));
    string modSettingsJson = File.ReadAllText(Path.Combine(core, "ModSettingsJson.cs"));
    Assert(modSettingsJson.Contains("Shared.DependencyFreeJson.Serialize", StringComparison.Ordinal),
        "ModSettingsJson does not use the shared serializer");
    Assert(!modSettingsJson.Contains("class JsonParser", StringComparison.Ordinal) &&
        !modSettingsJson.Contains("AppendString", StringComparison.Ordinal),
        "ModSettingsJson still contains a private JSON implementation");
}

static void TestCoopJsonLineEndings()
{
    using Fixture fixture = Fixture.Create();
    string mission = File.ReadAllText(fixture.JsonPath);
    Assert(mission.Contains("\r\n") && !mission.Replace("\r\n", string.Empty).Contains('\n'), "mission JSON line endings changed");

    var manifest = new CoopTrailPackageManifest
    {
        SchemaVersion = 1,
        PackageId = Guid.NewGuid().ToString("D"),
        DisplayName = "Test",
        MissionCount = 1,
        ContentFingerprint = new string('a', 64),
    };
    string json = CoopTrailPackageManifestJson.Serialize(manifest);
    Assert(json.Contains("\r\n") && !json.Replace("\r\n", string.Empty).Contains('\n'), "manifest JSON line endings changed");
}

static string FindProjectRoot()
{
    foreach (string seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        DirectoryInfo directory = new DirectoryInfo(seed);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CustomCustomTrail.csproj")))
                return directory.FullName;
            string child = Path.Combine(directory.FullName, "CustomCustomTrail", "CustomCustomTrail.csproj");
            if (File.Exists(child))
                return Path.GetDirectoryName(child)!;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("CustomCustomTrail project root was not found.");
}

static int CountOccurrences(string text, string value)
{
    int count = 0;
    for (int index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
        count++;
    return count;
}

static void TestPathEscape()
{
    using Fixture fixture = Fixture.Create();
    string outside = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "outside.map");
    File.WriteAllBytes(outside, new byte[] { 1 });
    try
    {
        ExpectFailure(() => MissionLoader.ResolveBundledPath(fixture.Root, "..\\outside.map", ".map"), "escape accepted");
    }
    finally
    {
        File.Delete(outside);
    }
}

static void TestInvalidRotation()
{
    using Fixture fixture = Fixture.Create(aivRotation: 2);
    ExpectFailure(() => new MissionLoader().Load(fixture.JsonPath, 1, 1), "native orientation value accepted as degrees");
}

static void TestCatalogIsolation()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    string package = CreatePackage(fixture, root, "Broken", 2);
    File.WriteAllText(Path.Combine(package, "CoopMissions", "02.coopmission.json"), "{}", new UTF8Encoding(false));
    var errors = new List<string>();
    var packages = new CoopTrailPackageCatalog();
    packages.Scan(root, null, errors.Add);
    Assert(packages.Packages.Count == 0, "partially invalid package was selectable");
    Assert(errors.Count == 1, "invalid package error was not reported once");
}

static void TestSchemaOneRejected()
{
    using Fixture fixture = Fixture.Create(schemaVersion: 1);
    ExpectFailure(() => new MissionLoader().Load(fixture.JsonPath, 1, 1), "schema 1 was accepted");
}

static void TestEditedMissionReload()
{
    using Fixture fixture = Fixture.Create();
    string json = File.ReadAllText(fixture.JsonPath);
    string edited = json.Replace("\"displayName\": \"Test\"", "\"displayName\": \"Edited locally\"");
    Assert(!string.Equals(json, edited, StringComparison.Ordinal), "test fixture displayName was not found");
    File.WriteAllText(fixture.JsonPath, edited, new UTF8Encoding(false));
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(loaded.Definition.DisplayName == "Edited locally", "text-editor change was not reloaded");
    Assert(Path.GetFileName(loaded.JsonPath) == "01.coopmission.json", "mission slot filename changed");
}

static void TestInvalidModSettings()
{
    using Fixture fixture = Fixture.Create();
    string json = File.ReadAllText(fixture.JsonPath).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
    File.WriteAllText(fixture.JsonPath, json, new UTF8Encoding(false));
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(!string.IsNullOrWhiteSpace(loaded.Definition.ModSettingsError), "invalid block was not reported");
    Assert(loaded.Definition.ModSettings.Mods.Values.All(entry => !entry.Enabled), "invalid block was partially retained");
}

static void TestHumanProjection()
{
    using Fixture fixture = Fixture.Create();
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    MissionProjection projection = MissionProjection.Create(loaded.Definition);
    Assert(projection.Teams[0] == 1 && projection.Teams[1] == 1, "guest was not moved to host team");
    Assert(projection.Teams[2] == 2, "AI team changed");
    Assert(projection.KeepOrder.Take(3).SequenceEqual(new[] { 1, 2, 3 }), "keep order changed");
}

static void TestPreferredAiv()
{
    using Fixture fixture = Fixture.Create(aivRotation: 90, secondAivRotation: 180, preferredAiv: 1);
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(loaded.Definition.Players[2].PreferredAiv == 1, "preferred AIV was not retained");
}

static void TestLastCatalogSlot()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    CoopTrailPackage package = CoopTrailPackageCatalog.Load(CreatePackage(fixture, root, "Forty", 40));
    var catalog = new MissionCatalog();
    catalog.Load(package, null, null);
    Assert(catalog.TryGet(3, 10, out _), "Trail4 mission 10 was not loaded");
}

static void TestPackageFingerprint()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    string package = CreatePackage(fixture, root, "Changed", 1);
    File.AppendAllText(Path.Combine(package, "CoopMissions", "map.map"), "changed");
    ExpectFailure(() => CoopTrailPackageCatalog.Load(package), "changed package content passed its fingerprint");
}

static void TestDuplicatePackageIds()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    string id = Guid.NewGuid().ToString("D");
    CreatePackage(fixture, root, "One", 1, id);
    CreatePackage(fixture, root, "Two", 1, id);
    var errors = new List<string>();
    var catalog = new CoopTrailPackageCatalog();
    catalog.Scan(root, null, errors.Add);
    Assert(catalog.Packages.Count == 0, "duplicate package ID remained selectable");
    Assert(errors.Any(message => message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)), "duplicate package ID was not diagnosed");
}

static void TestOrdinalMapping()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    CoopTrailPackage package = CoopTrailPackageCatalog.Load(CreatePackage(fixture, root, "Mapping", 40));
    var catalog = new MissionCatalog();
    catalog.Load(package, null, null);
    Assert(catalog.TryGet(0, 10, out _), "ordinal 10 mapping failed");
    Assert(catalog.TryGet(1, 1, out _), "ordinal 11 mapping failed");
    Assert(catalog.TryGet(1, 10, out _), "ordinal 20 mapping failed");
    Assert(catalog.TryGet(2, 1, out _), "ordinal 21 mapping failed");
    Assert(catalog.TryGet(2, 10, out _), "ordinal 30 mapping failed");
    Assert(catalog.TryGet(3, 1, out _), "ordinal 31 mapping failed");
    Assert(catalog.TryGet(3, 10, out _), "ordinal 40 mapping failed");
    Assert(!catalog.TryGet(4, 1, out _), "ordinal 41 was mapped into a fifth Coop Trail");
}

static string CreatePackage(Fixture fixture, string customTrailsRoot, string name, int missionCount, string packageId = null)
{
    string root = Path.Combine(customTrailsRoot, name);
    string missions = Path.Combine(root, "CoopMissions");
    Directory.CreateDirectory(missions);
    File.Copy(Path.Combine(fixture.Root, "map.map"), Path.Combine(missions, "map.map"));
    File.Copy(Path.Combine(fixture.Root, "lord.lordjson"), Path.Combine(missions, "lord.lordjson"));
    File.Copy(Path.Combine(fixture.Root, "castle.aivjson"), Path.Combine(missions, "castle.aivjson"));
    var fingerprintFiles = new List<string>
    {
        Path.Combine(missions, "map.map"),
        Path.Combine(missions, "lord.lordjson"),
        Path.Combine(missions, "castle.aivjson"),
    };
    for (int ordinal = 1; ordinal <= missionCount; ordinal++)
    {
        string target = Path.Combine(missions, ordinal.ToString("00") + ".coopmission.json");
        File.Copy(fixture.JsonPath, target);
        fingerprintFiles.Add(target);
    }
    var manifest = new CoopTrailPackageManifest
    {
        SchemaVersion = 1,
        PackageId = packageId ?? Guid.NewGuid().ToString("D"),
        DisplayName = name,
        MissionCount = missionCount,
        ContentFingerprint = CoopTrailPackageFingerprint.Compute(root, fingerprintFiles),
    };
    CoopTrailPackageManifestJson.WriteAtomic(Path.Combine(root, "cooptrail.json"), manifest);
    return root;
}

static void ExpectFailure(Action action, string message)
{
    try
    {
        action();
    }
    catch
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class Fixture : IDisposable
{
    public string Root { get; private set; }
    public string JsonPath { get; private set; }

    public static Fixture Create(int aivRotation = 90, int? secondAivRotation = null, int preferredAiv = -1, int schemaVersion = 2, int startGold = 500, bool includeLegacyModSetting = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "CustomCustomTrailTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "map.map"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(root, "lord.lordjson"), "{\"lord\":{}}", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "castle.aivjson"), "{}", new UTF8Encoding(false));

        var definition = new CoopMissionDefinition
        {
            SchemaVersion = schemaVersion,
            DisplayName = "Test",
            Map = new MapReference { Source = "bundled", File = "map.map" },
            Players = new List<PlayerDefinition>
            {
                new PlayerDefinition { KeepPosition = 1, Team = 1, Colour = 0 },
                new PlayerDefinition { KeepPosition = 2, Team = 4, Colour = 1 },
                new PlayerDefinition
                {
                    KeepPosition = 3,
                    Team = 2,
                    Colour = 2,
                    Lord = new LordReference { Source = "bundled", File = "lord.lordjson", BaseLordId = 0 },
                    Aivs = new List<AivReference>
                    {
                        new AivReference { Source = "bundled", File = "castle.aivjson", Rotation = aivRotation },
                    },
                    PreferredAiv = preferredAiv,
                },
            },
            ModSettings = ModSettingsDefinition.CreateDisabled(),
        };
        definition.ModSettings.Mods["StartConditions_Serp"] = new ModSettingsEntry
        {
            Enabled = true,
            Settings = new Dictionary<string, object> { ["SetStartGoldHuman"] = startGold },
        };
        if (includeLegacyModSetting)
            definition.ModSettings.Mods["StartConditions_Serp"].Settings["RemovedSetting"] = 99;
        if (secondAivRotation.HasValue)
            definition.Players[2].Aivs.Add(new AivReference { Source = "bundled", File = "castle.aivjson", Rotation = secondAivRotation.Value });
        string jsonPath = Path.Combine(root, "01.coopmission.json");
        int requestedSchemaVersion = definition.SchemaVersion;
        int requestedAivRotation = definition.Players[2].Aivs[0].Rotation;
        definition.SchemaVersion = MissionLoader.CurrentSchemaVersion;
        if (requestedAivRotation != 0 && requestedAivRotation != 90 && requestedAivRotation != 180 && requestedAivRotation != 270)
            definition.Players[2].Aivs[0].Rotation = 90;
        MissionLoader.WriteAtomic(jsonPath, definition);
        if (requestedSchemaVersion != MissionLoader.CurrentSchemaVersion || requestedAivRotation != definition.Players[2].Aivs[0].Rotation)
        {
            string json = File.ReadAllText(jsonPath, Encoding.UTF8);
            if (requestedSchemaVersion != MissionLoader.CurrentSchemaVersion)
                json = json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": " + requestedSchemaVersion, StringComparison.Ordinal);
            if (requestedAivRotation != definition.Players[2].Aivs[0].Rotation)
                json = json.Replace("\"rotation\": 90", "\"rotation\": " + requestedAivRotation, StringComparison.Ordinal);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
        }
        return new Fixture { Root = root, JsonPath = jsonPath };
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
    }
}
