using CustomCustomTrail.Core;
using Shared;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("bundled mission loads", TestBundledMission),
    ("Coop mission modsettings use an optional sidecar", TestCoopMissionModSettingsSidecar),
    ("path escape rejected", TestPathEscape),
    ("invalid rotation rejected", TestInvalidRotation),
    ("package catalog rejects invalid packages", TestCatalogIsolation),
    ("old mission schemas are rejected", TestOldMissionSchemasRejected),
    ("old Coop package schema is rejected", TestOldPackageSchemaRejected),
    ("locally edited mission JSON reloads from the same slot", TestEditedMissionReload),
    ("invalid mod settings are isolated", TestInvalidModSettings),
    ("first two active players become allied humans", TestHumanProjection),
    ("preferred AIV permits differing rotations", TestPreferredAiv),
    ("fourth trail tenth slot is addressable", TestLastCatalogSlot),
    ("package fingerprint detects content changes", TestPackageFingerprint),
    ("Coop Workshop staging filters modsettings", TestCoopWorkshopStaging),
    ("duplicate package IDs are rejected", TestDuplicatePackageIds),
    ("identical local and Workshop replicas are merged", TestIdenticalPackageReplicas),
    ("ordinal mapping covers four trails and ignores mission 41", TestOrdinalMapping),
    ("native mod-settings JSON roundtrip", TestNativeModSettingsRoundtrip),
    ("dynamic third-party mod ids are preserved", TestModSettingsRegistry),
    ("missing mod entry remains unmanaged", TestMissingModEntry),
    ("Trail mod compatibility contract is validated", TestTrailModCompatibilityContract),
    ("primitive enum migration policy is fail-closed", TestTrailSettingValueConversionPolicy),
    ("disabled Trail mod ids are normalized", TestDisabledTrailModIdNormalization),
    ("explicit plugin opt-out marker is honored", TestExplicitPluginOptOut),
    ("sidecar schema evolution keeps only current settings", TestSidecarSettingsSchemaEvolution),
    ("coop mission schema evolution keeps only current settings", TestCoopSettingsSchemaEvolution),
    ("invalid mod-settings documents are rejected", TestInvalidModSettingsDocuments),
    ("atomic sidecar write replaces existing file", TestAtomicSidecarWrite),
    ("Trail coordinator ownership is centralized", TestCoordinatorOwnership),
    ("customized launch origin is persisted and fail-closed", TestCustomizedLaunchOriginIntegration),
    ("customized launch origin save roundtrip", TestCustomizedLaunchOriginRoundtrip),
    ("Steam Workshop discovery waits for Steamworks", TestSteamWorkshopReadinessGate),
    ("local activation setting gates the complete runtime", TestLocalActivationSetting),
    ("Trail Maker Coop export is integrated", TestCoopExporterIntegration),
    ("Coop package JSON is Unity dependency-free", TestDependencyFreeCoopJson),
    ("mission and manifest JSON use CRLF", TestCoopJsonLineEndings),
    ("Workshop Trail staging filters sidecars", TestWorkshopTrailSidecars),
    ("Workshop upload checkbox is unified", TestWorkshopUploadCheckbox),
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

static void TestCoopMissionModSettingsSidecar()
{
    using Fixture fixture = Fixture.Create();
    string missionJson = File.ReadAllText(fixture.JsonPath);
    Assert(!missionJson.Contains("modSettings", StringComparison.Ordinal), "mission JSON still embeds modsettings");

    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(loaded.ModSettingsPath == fixture.SidecarPath, "mission sidecar path was not retained");
    Assert(loaded.Definition.ModSettings.Mods.ContainsKey("StartConditions_Serp"), "mission sidecar was not loaded");

    File.Delete(fixture.SidecarPath);
    loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(loaded.ModSettingsPath == null, "missing mission sidecar was reported as present");
    Assert(loaded.Definition.ModSettings.Mods.Count == 0, "missing mission sidecar did not remain unmanaged");

    int closingBrace = missionJson.LastIndexOf("\r\n}", StringComparison.Ordinal);
    Assert(closingBrace >= 0, "mission fixture has no root closing brace");
    string embedded = missionJson.Insert(closingBrace, ",\r\n  \"modSettings\": {}");
    File.WriteAllText(fixture.JsonPath, embedded, new UTF8Encoding(false));
    ExpectFailure(() => new MissionLoader().Load(fixture.JsonPath, 1, 1), "embedded schema-3 modsettings were accepted");
}

static void TestNativeModSettingsRoundtrip()
{
    ModSettingsDefinition document = ModSettingsDefinition.CreateUnmanaged();
    document.Mods["StartConditions_Serp"] = new ModSettingsEntry
    {
        Enabled = true,
        Settings = new Dictionary<string, object>
        {
            ["Bool"] = true,
            ["Int"] = 42,
            ["Double"] = 1.25,
            ["String"] = "Wood=10\r\nStone=-1",
            ["DoubleArray"] = new[] { 0.75, 1.0, 1.25 },
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
    Assert(entry.Settings["DoubleArray"] is List<object> values &&
        values.Count == 3 && Math.Abs(Convert.ToDouble(values[2]) - 1.25) < 0.0001,
        "double array changed");
}

static void TestWorkshopTrailSidecars()
{
    string root = Path.Combine(Path.GetTempPath(), "CustomCustomTrailTests", Guid.NewGuid().ToString("N"));
    string source = Path.Combine(root, "source");
    string stagingRoot = Path.Combine(root, "staging");
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(stagingRoot);
    try
    {
        File.WriteAllText(Path.Combine(source, "01.trail"), "trail");
        File.WriteAllText(Path.Combine(source, "01.modjson"), "included");
        File.WriteAllText(Path.Combine(source, "orphan.modjson"), "excluded");
        Assert(WorkshopUploadStaging.TryResetDirectChild(
                stagingRoot,
                "Trail",
                out string destination,
                out string resetError),
            resetError);
        File.WriteAllText(Path.Combine(destination, "01.trail"), "vanilla");

        Assert(WorkshopUploadStaging.TryStageTrailSidecars(
                source,
                destination,
                out int copied,
                out string stageError),
            stageError);
        Assert(copied == 1, "unexpected sidecar count: " + copied);
        Assert(File.Exists(Path.Combine(destination, "01.modjson")), "matching sidecar was not staged");
        Assert(!File.Exists(Path.Combine(destination, "orphan.modjson")), "orphan sidecar was staged");
        Assert(File.ReadAllText(Path.Combine(destination, "01.trail")) == "vanilla", "Vanilla Trail was changed");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestWorkshopUploadCheckbox()
{
    string root = FindProjectRoot();
    string xaml = File.ReadAllText(Path.Combine(root, "Patches", "Assets", "GUI", "XAMLResources", "FRONT_EditorSetup.xaml"));
    string viewModel = File.ReadAllText(Path.Combine(root, "src", "TrailWorkshopUploadOptionsViewModel.cs"));
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    Assert(xaml.Contains("IsChecked=\"{Binding IncludeModSettings, Mode=TwoWay}\"", StringComparison.Ordinal) &&
        !xaml.Contains("CanChangeOption", StringComparison.Ordinal),
        "Workshop modsettings checkbox is not uniformly editable");
    Assert(xaml.Contains("Foreground=\"Black\"", StringComparison.Ordinal) &&
        xaml.Contains("ToolTipService.ShowDuration=\"60000\"", StringComparison.Ordinal) &&
        xaml.Contains("ToolTipPresentation.MaximumWidth", StringComparison.Ordinal),
        "Workshop checkbox presentation does not match the required tooltip style");
    Assert(viewModel.Contains("WorkshopUpload.IncludeModSettings", StringComparison.Ordinal) &&
        viewModel.Contains("includeModSettings = true", StringComparison.Ordinal) &&
        !viewModel.Contains("IncludeAdditionalFiles", StringComparison.Ordinal) &&
        !viewModel.Contains("coopPackage", StringComparison.Ordinal),
        "Workshop checkbox model still uses additional-file or Coop-lock semantics");
    Assert(coordinator.Contains("UploadCoopTrailPackage(self, selectedRow.trail, uploadOptions.IncludeModSettings)", StringComparison.Ordinal) &&
        coordinator.Contains("CoopWorkshopPackageStaging.Stage", StringComparison.Ordinal),
        "Coop upload does not honor the unified modsettings choice");
}

static void TestTrailSettingValueConversionPolicy()
{
    Assert(TrailSettingValueConversionPolicy.ShouldUseMessagePackEnumConversion(true, typeof(DayOfWeek)),
        "Boolean source was not accepted for an enum target");
    Assert(TrailSettingValueConversionPolicy.ShouldUseMessagePackEnumConversion(2, typeof(DayOfWeek)),
        "integer source was not accepted for an enum target");
    Assert(TrailSettingValueConversionPolicy.ShouldUseMessagePackEnumConversion((long)1, typeof(DayOfWeek?)),
        "integer source was not accepted for a nullable enum target");
    Assert(!TrailSettingValueConversionPolicy.ShouldUseMessagePackEnumConversion(1.0, typeof(DayOfWeek)),
        "floating-point source was accepted for an enum target");
    Assert(!TrailSettingValueConversionPolicy.ShouldUseMessagePackEnumConversion("1", typeof(DayOfWeek)),
        "string source was accepted for an enum target");
    Assert(!TrailSettingValueConversionPolicy.ShouldUseMessagePackEnumConversion(1, typeof(int)),
        "non-enum target was accepted");

    string root = FindProjectRoot();
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    Assert(coordinator.Contains("TrailSettingValueConversionPolicy.ShouldUseMessagePackEnumConversion") &&
        coordinator.Contains("MessagePackSerializer.Serialize(value.GetType(), value)") &&
        coordinator.Contains("MessagePackSerializer.Deserialize(effectiveType, primitive)"),
        "ConvertJsonValue does not use the guarded MessagePack enum migration path");
}

static void TestModSettingsRegistry()
{
    ModSettingsDefinition parsed = ModSettingsJson.ParseObject(
        "{\"schemaVersion\":1,\"mods\":{\"ThirdParty.DynamicMod\":{\"enabled\":true,\"settings\":{\"Value\":7}}}}");
    Assert(parsed.Mods.ContainsKey("ThirdParty.DynamicMod") &&
        Convert.ToInt32(parsed.Mods["ThirdParty.DynamicMod"].Settings["Value"]) == 7,
        "dynamic third-party mod id was not preserved");
}

static void TestMissingModEntry()
{
    ModSettingsDefinition parsed = ModSettingsJson.ParseObject("{\"schemaVersion\":1,\"mods\":{}}");
    Assert(parsed.Mods.Count == 0, "an absent mod entry is not treated as unmanaged");
}

static void TestTrailModCompatibilityContract()
{
    var compatible = new CompatibleTrailSettingsViewModel();
    TrailModCompatibilityResult result = EvaluateCompatibility(compatible);
    Assert(result.IsCompatible, "valid mission-preset contract was rejected: " + result.IncompatibilityReason);
    Assert(result.Properties.Select(property => property.Name).SequenceEqual(new[] { "EnableMod", "Label", "Strength" }),
        "persistent host properties were not selected deterministically or DoNotPersist was included");

    TrailModCompatibilityResult missingApi = EvaluateCompatibility(new MissingMissionApiViewModel());
    Assert(!missingApi.IsCompatible && missingApi.IncompatibilityReason.Contains("mission snapshot", StringComparison.Ordinal),
        "missing mission API was accepted");

    TrailModCompatibilityResult nonBooleanEnable = EvaluateCompatibility(new NonBooleanEnableModViewModel());
    Assert(!nonBooleanEnable.IsCompatible && nonBooleanEnable.IncompatibilityReason.Contains("Boolean", StringComparison.Ordinal),
        "non-Boolean EnableMod was accepted");

    compatible.Label = null;
    TrailModCompatibilityResult nullValue = EvaluateCompatibility(compatible);
    Assert(!nullValue.IsCompatible && nullValue.IncompatibilityReason.Contains("Label is null", StringComparison.Ordinal),
        "null persistent value was accepted");
    compatible.Label = "ready";

    TrailModCompatibilityResult serializationFailure = TrailModCompatibilityContract.Evaluate(
        compatible,
        (property, value) =>
        {
            if (property.Name == nameof(compatible.Strength))
                throw new InvalidOperationException("probe failed");
        },
        DecodeCompatibilityValue);
    Assert(!serializationFailure.IsCompatible && serializationFailure.IncompatibilityReason.Contains("not serializable", StringComparison.Ordinal),
        "serialization failure was accepted");

    compatible.OmitStrengthFromSnapshot = true;
    TrailModCompatibilityResult missingSnapshotValue = EvaluateCompatibility(compatible);
    Assert(!missingSnapshotValue.IsCompatible && missingSnapshotValue.IncompatibilityReason.Contains("missing Strength", StringComparison.Ordinal),
        "incomplete mission snapshot was accepted");
    compatible.OmitStrengthFromSnapshot = false;

    compatible.EnabledInDisabledSnapshot = true;
    TrailModCompatibilityResult enabledSnapshot = EvaluateCompatibility(compatible);
    Assert(!enabledSnapshot.IsCompatible && enabledSnapshot.IncompatibilityReason.Contains("does not disable EnableMod", StringComparison.Ordinal),
        "enabled disabled-snapshot was accepted");
}

static void TestDisabledTrailModIdNormalization()
{
    string[] normalized = TrailModCompatibilityContract.NormalizeDisabledModIds(
        new[] { "Z.Mod", "", "CustomCustomTrail_Serp", "A.Mod", "Z.Mod", " " },
        "CustomCustomTrail_Serp");
    Assert(normalized.SequenceEqual(new[] { "A.Mod", "Z.Mod" }),
        "disabled mod ids were not filtered, de-duplicated and sorted");
    Assert(TrailModCompatibilityContract.NormalizeDisabledModIds(null, "CustomCustomTrail_Serp").Length == 0,
        "an empty selection did not keep every compatible mod enabled by default");
}

static void TestExplicitPluginOptOut()
{
    Assert(TrailModCompatibilityContract.IsExplicitlyOptedOut(new OptedOutPlugin()),
        "public true opt-out constant was not honored");
    Assert(!TrailModCompatibilityContract.IsExplicitlyOptedOut(new NotOptedOutPlugin()),
        "false opt-out constant excluded a plugin");
    Assert(!TrailModCompatibilityContract.IsExplicitlyOptedOut(new RuntimeOptOutFieldPlugin()),
        "mutable runtime field was accepted as an explicit compile-time opt-out");
    Assert(!TrailModCompatibilityContract.IsExplicitlyOptedOut(null),
        "missing plugin was treated as opted out");
}

static TrailModCompatibilityResult EvaluateCompatibility(object viewModel) =>
    TrailModCompatibilityContract.Evaluate(
        viewModel,
        (property, value) => EncodeCompatibilityValue(property.PropertyType, value),
        DecodeCompatibilityValue);

static byte[] EncodeCompatibilityValue(Type type, object value)
{
    if (type == typeof(bool))
        return BitConverter.GetBytes((bool)value);
    if (type == typeof(int))
        return BitConverter.GetBytes((int)value);
    if (type == typeof(string))
        return Encoding.UTF8.GetBytes((string)value);
    throw new InvalidOperationException("unsupported fake type " + type.FullName);
}

static object DecodeCompatibilityValue(Type type, byte[] bytes)
{
    if (type == typeof(bool))
        return BitConverter.ToBoolean(bytes, 0);
    if (type == typeof(int))
        return BitConverter.ToInt32(bytes, 0);
    if (type == typeof(string))
        return Encoding.UTF8.GetString(bytes);
    throw new InvalidOperationException("unsupported fake type " + type.FullName);
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
        ModSettingsDefinition document = ModSettingsDefinition.CreateUnmanaged();
        document.Mods["UnitLimit_Serp"] = new ModSettingsEntry { Enabled = true };
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
    string sharedGameMode = File.ReadAllText(Path.Combine(workspaceRoot, "Shared", "GameModeHelper.cs"));
    Assert(CountOccurrences(coordinator, "InjectCoopCustomizeButton(pages[index]);") == 1,
        "Coop Trail button registration is not centralized and singular");
    Assert(coordinator.Contains("Margin = new Thickness(0, 0, 0, -30)"),
        "Coop Customize is not positioned below the Vanilla kick button");
    string runtime = File.ReadAllText(Path.Combine(projectRoot, "src", "CustomCustomTrailRuntime.cs"));
    string obsoleteBridgeName = "TrailModSettings" + "Bridge";
    Assert(!runtime.Contains("SerializeModSettings") && !runtime.Contains(obsoleteBridgeName), "Coop settings still use the old bridge roundtrip");
    Assert(runtime.Contains("missionSettingsCoordinator?.ExitContext(force: true)"), "map unload does not leave the mission preset context");
    Assert(coordinator.Contains("document = CaptureDocument();") &&
        !coordinator.Contains("requireLoadedEndpoints") &&
        coordinator.Contains("TrailModCompatibilityContract.Evaluate"),
        "Trail saves do not use validated synchronous settings capture");
    Assert(coordinator.Contains("System_CreateDisabledMissionPresetSnapshot") &&
        coordinator.Contains("RemoveUnknownSettings"),
        "Trail loading does not combine current defaults with schema cleanup");
    Assert(sharedPresetSystem.Contains("CopyProperties(defaults, hostProperties)") &&
        sharedPresetSystem.Contains("defaults.TryGetValue(property.Name, out bytes)"),
        "the shared mission preset no longer supplies defaults for missing current host settings");
    Assert(sharedPresetSystem.Contains("PerPlayerIdentityHookAnchor") &&
        sharedPresetSystem.Contains("ApplyPerPlayerUpdate") &&
        sharedPresetSystem.Contains("ResolveAuthenticatedPerPlayerTarget") &&
        sharedPresetSystem.Contains("CaptureProvisionalPlayerIdDiagnostic") &&
        sharedPresetSystem.Contains("requireAuthoritativeLobbyRoster: true") &&
        sharedPresetSystem.Contains("waiting for an authoritative player slot without a deadline") &&
        sharedPresetSystem.Contains("The transport identity wins"),
        "the shared per-player workaround no longer queues authenticated updates until Vanilla's final slot wins");
    Assert(sharedGameMode.Contains("SCRIPT EXTENDER BUG WORKAROUND") &&
        sharedGameMode.Contains("Revalidate all source semantics after every Extender update"),
        "the shared Script Extender identity workaround is not marked for removal and update review");
    Assert(!coordinator.Contains("pendingTrailMakerSaveDocument"),
        "Trail saves still retain a snapshot for the next save operation");
    Assert(coordinator.Contains("!openingCustomTrailSetup") &&
        coordinator.Contains("openingCustomTrailSetup = true") &&
        coordinator.Contains("openingCustomTrailSetup = false"),
        "Custom Trail setup does not suppress transient selection-sidecar loads");
    Assert(!coordinator.Contains("all Trail mods will be disabled"),
        "capture failures can still overwrite a sidecar with an all-disabled fallback");
}

static void TestCustomizedLaunchOriginIntegration()
{
    string projectRoot = FindProjectRoot();
    string workspaceRoot = Directory.GetParent(projectRoot)?.FullName ??
        throw new InvalidOperationException("workspace root missing");
    string api = File.ReadAllText(Path.Combine(projectRoot, "src", "CustomCustomTrailLaunchOriginApi.cs"));
    string coordinator = File.ReadAllText(Path.Combine(projectRoot, "src", "TrailMissionSettingsCoordinator.cs"));
    string runtime = File.ReadAllText(Path.Combine(projectRoot, "src", "CustomCustomTrailRuntime.cs"));
    string sharedGameMode = File.ReadAllText(Path.Combine(workspaceRoot, "Shared", "GameModeHelper.cs"));
    string project = File.ReadAllText(Path.Combine(projectRoot, "CustomCustomTrail.csproj"));

    Assert(project.Contains("CustomCustomTrailLaunchOriginApi.cs"),
        "the runtime project does not compile the public origin API");
    Assert(api.Contains("public static class CustomCustomTrailLaunchOriginApi") &&
        api.Contains("public static CustomCustomTrailLaunchOriginKind Origin") &&
        api.Contains("public static int TrailId") &&
        api.Contains("public static int MissionId") &&
        api.Contains("public static bool RestoredFromSave") &&
        api.Contains("public static bool LaunchPending"),
        "the optional read-only origin surface is incomplete");
    Assert(api.Contains("RegisterModDataHandler") &&
        api.Contains("MessagePackSerializer.Serialize") &&
        api.Contains("MessagePackSerializer.Deserialize") &&
        api.Contains("!context.IsSaveFile || context.IsMapEditorSave"),
        "customized launch origin is not restricted to versioned save-file data");
    Assert(api.Contains("LegacyApiVersion") &&
        api.Contains("Ignored invalid Custom Trail launch-origin save data") &&
        api.Contains("Ignored unreadable Custom Trail launch-origin save data"),
        "missing or corrupt saved origin does not fail closed");
    Assert(api.Contains("if (launchPending)") &&
        api.Contains("MarkMapStarted") &&
        api.Contains("MarkRestartPending") &&
        runtime.Contains("CustomCustomTrailLaunchOriginApi.MarkMapStarted()"),
        "launch or restart unload can clear the origin before map start");
    Assert(coordinator.Contains("SetCustomizedCustomTrail(trailId, missionId)") &&
        coordinator.Contains("SetCustomizedCoopTrail(trailId, mission)") &&
        coordinator.Contains("SetCustomizedVanillaTrail") &&
        coordinator.Contains("SetCustomizedSandsOfTime") &&
        runtime.Contains("SetCustomizedCoopTrail(trailId, missionId)"),
        "host/client Customize transitions do not establish the origin");
    int multiplayerOpenHookIndex = coordinator.IndexOf("private void MultiplayerOpenHook", StringComparison.Ordinal);
    int startSkirmishHookIndex = coordinator.IndexOf("private void StartSkirmishGameHook", StringComparison.Ordinal);
    Assert(multiplayerOpenHookIndex >= 0 && startSkirmishHookIndex > multiplayerOpenHookIndex,
        "the multiplayer-open hook could not be isolated");
    string multiplayerOpenHook = coordinator.Substring(
        multiplayerOpenHookIndex,
        startSkirmishHookIndex - multiplayerOpenHookIndex);
    Assert(multiplayerOpenHook.Contains("fromNew && skirmishSetup && restartInfo == null && !coopSetup && !trailMaker") &&
        multiplayerOpenHook.IndexOf("multiplayerOpenOriginal(", StringComparison.Ordinal) <
            multiplayerOpenHook.IndexOf("CaptureBuiltInCustomizeOrigin(customiseTrailType, customiseTrailId)", StringComparison.Ordinal) &&
        coordinator.Split(new[] { "CaptureBuiltInCustomizeOrigin(" }, StringSplitOptions.None).Length == 3,
        "built-in Customize origin is not captured exclusively after the actual doOpen transition");
    Assert(coordinator.Contains("if (!preserveContextForLaunch)") &&
        coordinator.Contains("restartInfo.customTrailLevel == missionId") &&
        coordinator.Contains("CustomCustomTrailLaunchOriginApi.MarkRestartPending()") &&
        coordinator.Contains("CustomCustomTrailLaunchOriginApi.Clear()"),
        "direct follow-up and restarted Custom Trails do not separate stale from active origin");
    Assert(sharedGameMode.Contains("CustomCustomTrail.CustomCustomTrailLaunchOriginApi, CustomCustomTrail") &&
        sharedGameMode.Contains("ExternalOriginMatchesKind") &&
        sharedGameMode.Contains("RestoredCustomizedSave"),
        "GameModeHelper does not validate the optional saved origin against Vanilla mode state");
}

static void TestCustomizedLaunchOriginRoundtrip()
{
    SHCDESE.API.ModSaveDataAPI saveApi = SHCDESE.API.ModSaveDataAPI.Instance;
    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Initialize(null);
    Assert(saveApi.SaveCallback != null && saveApi.LoadCallback != null && saveApi.OnUnloadCallback != null,
        "origin save-data callbacks were not registered");

    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedCustomTrail(90, 3);
    byte[] saved = saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(
        isSaveFile: true,
        isMapEditorSave: false));
    Assert(saved != null && saved.Length > 0, "customized Custom Trail origin was not serialized");
    Assert(saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(
        isSaveFile: true,
        isMapEditorSave: true)) == null,
        "origin data was written into a Map Editor file");

    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Clear();
    saveApi.LoadCallback(saved, new SHCDESE.API.Components.SaveData.LoadContext(isSaveFile: true));
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
            CustomCustomTrail.CustomCustomTrailLaunchOriginKind.CustomizedCustomTrail &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.TrailId == 90 &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.MissionId == 3 &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.RestoredFromSave,
        "process-restart-style Custom Trail roundtrip lost its restored origin");

    // Both Script Extender unload phases occur before OnStartMap during a launch.
    saveApi.OnUnloadCallback();
    saveApi.OnUnloadCallback();
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin !=
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "launch origin was cleared between unload and map-start events");
    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.MarkMapStarted();
    saveApi.OnUnloadCallback();
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "origin survived a real context unload");

    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedCoopTrail(3, 10);
    saved = saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(true, false));
    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Clear();
    saveApi.LoadCallback(saved, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
            CustomCustomTrail.CustomCustomTrailLaunchOriginKind.CustomizedCoopTrail &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.TrailId == 3 &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.MissionId == 10,
        "Coop Trail origin did not survive a save roundtrip");

    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedVanillaTrail(1, 1, 0);
    saved = saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(true, false));
    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Clear();
    saveApi.LoadCallback(saved, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
            CustomCustomTrail.CustomCustomTrailLaunchOriginKind.CustomizedVanillaTrail &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.TrailType == 1 &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.MissionId == 0,
        "Vanilla Trail Customize origin did not survive a save roundtrip");

    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedSandsOfTime(11, 11, 4);
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
            CustomCustomTrail.CustomCustomTrailLaunchOriginKind.CustomizedSandsOfTime &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.LaunchPending,
        "Sands of Time Customize origin was not exposed as pending");

    byte[] legacy = MessagePack.MessagePackSerializer.Serialize(
        new CustomCustomTrail.CustomCustomTrailLaunchOriginApi.LaunchOriginSaveData
        {
            Version = 1,
            Origin = (int)CustomCustomTrail.CustomCustomTrailLaunchOriginKind.CustomizedCustomTrail,
            TrailType = -1,
            TrailId = 90,
            MissionId = 2,
        });
    saveApi.LoadCallback(legacy, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
            CustomCustomTrail.CustomCustomTrailLaunchOriginKind.CustomizedCustomTrail &&
        CustomCustomTrail.CustomCustomTrailLaunchOriginApi.MissionId == 2,
        "version-1 launch origin was not read compatibly");

    saveApi.LoadCallback(new byte[] { 0xc1 }, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "corrupt MessagePack origin did not fail closed");

    byte[] mismatched = MessagePack.MessagePackSerializer.Serialize(
        new CustomCustomTrail.CustomCustomTrailLaunchOriginApi.LaunchOriginSaveData
        {
            Version = 999,
            Origin = (int)CustomCustomTrail.CustomCustomTrailLaunchOriginKind.CustomizedCustomTrail,
            TrailId = 90,
            MissionId = 1,
        });
    saveApi.LoadCallback(mismatched, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "unknown saved-origin schema did not fail closed");

    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedCustomTrail(89, 1);
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "invalid Custom Trail identity became active");
    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedCoopTrail(0, 11);
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "invalid Coop mission identity became active");
    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedVanillaTrail(0, 0, -1);
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "invalid Vanilla Customize mission identity became active");
    CustomCustomTrail.CustomCustomTrailLaunchOriginApi.SetCustomizedSandsOfTime(10, 10, 0);
    Assert(CustomCustomTrail.CustomCustomTrailLaunchOriginApi.Origin ==
        CustomCustomTrail.CustomCustomTrailLaunchOriginKind.None,
        "invalid Sands Customize Trail type became active");
}

static void TestSteamWorkshopReadinessGate()
{
    string projectRoot = FindProjectRoot();
    string workspaceRoot = Directory.GetParent(projectRoot)?.FullName ??
        throw new InvalidOperationException("workspace root missing");
    string workshopPaths = File.ReadAllText(Path.Combine(workspaceRoot, "Shared", "WorkshopContentPaths.cs"));
    string castleSettings = File.ReadAllText(Path.Combine(
        workspaceRoot,
        "CastlePlanner",
        "src",
        "CastlePlannerSettingsViewModel.cs"));

    int readinessGate = workshopPaths.IndexOf("if (!IsSteamworksReady())", StringComparison.Ordinal);
    int workshopCall = workshopPaths.IndexOf(
        "Platform_Workshop.Instance.GetListOfSubscribedItemsPaths()",
        StringComparison.Ordinal);
    Assert(readinessGate >= 0 && workshopCall > readinessGate,
        "Workshop enumeration can still call Steam before the readiness gate");
    Assert(workshopPaths.Contains("SteamManagerInstanceField?.GetValue(null)") &&
        workshopPaths.Contains("SteamManagerInitializedField?.GetValue(instance)"),
        "Steam readiness no longer inspects Vanilla's existing manager without creating one");
    Assert(castleSettings.Contains("Shared.WorkshopContentPaths.IsSteamworksReady()") &&
        castleSettings.Contains("return candidate;"),
        "CastlePlanner can discard a saved Workshop AIV before the deferred refresh");
}

static void TestLocalActivationSetting()
{
    string root = FindProjectRoot();
    string viewModel = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailSettingsViewModel.cs"));
    string plugin = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailPlugin.cs"));
    string runtime = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailRuntime.cs"));
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string compatibilityContract = File.ReadAllText(Path.Combine(root, "CustomCustomTrail.Core", "TrailModCompatibilityContract.cs"));
    string workspaceRoot = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("workspace root missing");
    string hostPlugin = File.ReadAllText(Path.Combine(workspaceRoot, "SerpsModsHost", "src", "SerpsModsHostPlugin.cs"));
    string xaml = File.ReadAllText(Path.Combine(root, "Override", "ScriptExtenderUI", "CustomCustomTrailSettings.xaml"));

    Assert(viewModel.Contains("[Shared.PresetLocal]") && viewModel.Contains("public bool EnableClientFeatures"),
        "local activation setting is not preset-local");
    Assert(viewModel.Contains("[SyncHostOnly]") && viewModel.Contains("public bool EnableMod"),
        "host activation setting is not host-synchronised");
    Assert(viewModel.Contains("[SyncHostOnly]") && viewModel.Contains("ActiveCoopPackageId"), "package selection is not host-synchronised");
    Assert(viewModel.Contains("[SyncPerPlayer, DoNotPersist]") && viewModel.Contains("CoopPackageStatusData"), "package validation is not reported per player");
    Assert(plugin.Contains("Settings.RuntimeActivationChanged += runtime.SetEnabled"), "runtime does not observe effective activation changes");
    Assert(runtime.Contains("settings.IsRuntimeEnabled"), "runtime does not combine local and host activation");
    Assert(runtime.Contains("RestoreVanillaMissions()"), "disabling cannot restore replaced Vanilla Coop slots");
    Assert(runtime.Contains("RefreshVisibleCoopMissionAfterPackageChange") &&
        runtime.Contains("self.CoopMissionChanged(trailId, missionId, false)"),
        "late host package settings do not refresh the already selected Vanilla Coop mission");
    Assert(runtime.Contains("ShowLocalPackageBlockAfterSync") &&
        runtime.Contains("lastShownLocalBlockSignature"),
        "clients do not receive a deduplicated package failure popup immediately after settings sync");
    Assert(viewModel.Contains("MissingStatus = \"ERROR|MISSING\"") &&
        viewModel.Contains("MismatchStatus = \"ERROR|MISMATCH\"") &&
        runtime.Contains("ErrorParticipantsMissing") && runtime.Contains("ErrorParticipantsMismatch"),
        "host package validation cannot distinguish missing and mismatching participant packages");
    Assert(viewModel.Contains("ActiveCoopPackageDescriptor") &&
        runtime.Contains("ExpectedPackageDescriptor()") &&
        runtime.Contains("!string.Equals(settings.ActiveCoopPackageDescriptor, ExpectedPackageDescriptor()"),
        "independently synchronized package fields can be validated before one coherent host descriptor arrives");
    Assert(coordinator.Contains("if (!enabled)"), "sidecar/customization hooks are not activation-gated");
    Assert(xaml.Contains("ToolTipService.ShowDuration=\"60000\""), "activation control tooltip duration is missing");
    Assert(xaml.Contains("x:Key=\"ModSettingsToolTipStyle\"") &&
        xaml.Contains("<CheckBox.ToolTip>") &&
        xaml.Contains("Style=\"{StaticResource ModSettingsToolTipStyle}\"") &&
        xaml.Contains("Content=\"{Binding HelpText}\""),
        "dynamic mod checkbox tooltips do not explicitly use the shared modsettings tooltip design");
    Assert(xaml.Contains("PracticalEffectsText") && viewModel.Contains("CustomCustomTrail.PracticalEffects"),
        "player-facing practical-effects text is not bound below the activation setting");
    int descriptionPosition = xaml.IndexOf("PracticalEffectsText", StringComparison.Ordinal);
    int hostOptionsPosition = xaml.IndexOf("HostOptionsText", StringComparison.Ordinal);
    int modSelectionPosition = xaml.IndexOf("SupportedTrailSettingsTitle", StringComparison.Ordinal);
    Assert(descriptionPosition >= 0 && descriptionPosition < modSelectionPosition &&
        modSelectionPosition < hostOptionsPosition,
        "local Trail settings are not shown before host Coop Trail options");
    Assert(xaml.Contains("CompatibleTrailMods") && xaml.Contains("IncompatibleTrailModsText") &&
        viewModel.Contains("DisabledTrailModIds") && runtime.Contains("DiscoverModCompatibility()"),
        "the dynamic compatible/incompatible Trail-mod catalog is not shown or persisted");
    Assert(coordinator.Contains("FindCompatibleViewModels(selectedOnly: true)") &&
        coordinator.Contains("System_CreateDisabledMissionPresetSnapshot") &&
        compatibilityContract.Contains("DoNotPersistAttribute") &&
        compatibilityContract.Contains("deserializationProbe"),
        "dynamic Trail compatibility does not enforce the safe mission-preset contract");
    Assert(coordinator.Contains("GetRegistrationGroups()") && coordinator.Contains("group.Skip(1).Any()"),
        "duplicate mod-settings registrations are not rejected per plugin GUID");
    Assert(coordinator.Contains("IsRegistrationGroupOptedOut(group)") &&
        plugin.Contains("public const bool CustomCustomTrailModSettingsOptOut = true;") &&
        hostPlugin.Contains("public const bool CustomCustomTrailModSettingsOptOut = true;"),
        "explicit opt-out is not applied to discovery, CustomCustomTrail, and SerpsModsHost");
    Assert(coordinator.Contains("ExitActiveParticipants") &&
        coordinator.Contains("activeParticipantIds.Add(item.Item1)"),
        "Trail lifecycle is not limited to participants whose preset entry completed");
    Assert(xaml.Contains("SelectedPreset") && xaml.Contains("PresetOptions") &&
        plugin.Contains("LobbyModSettingsPresetRegistration.Register"),
        "shared preset UI or registration is missing");
    Assert(xaml.Contains("CoopPackageOptions") && xaml.Contains("CanEditCoopPackage"), "host package dropdown is missing");
    Assert(xaml.Contains("SelectedItem=\"{Binding SelectedCoopPackage, Mode=TwoWay}\"") &&
        !xaml.Contains("SelectedCoopPackageIndex"),
        "Coop package dropdown does not use the stable CastlePlanner-style SelectedItem binding");
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
    string viewModel = File.ReadAllText(Path.Combine(root, "src", "CustomCustomTrailSettingsViewModel.cs"));
    string packet = File.ReadAllText(Path.Combine(root, "src", "CoopCustomizePacket.cs"));
    string project = File.ReadAllText(Path.Combine(root, "CustomCustomTrail.csproj"));
    Assert(coordinator.Contains("CustomCustomTrailCoopExport") && coordinator.Contains("cooptrail.enabled"), "Trail Maker Coop checkbox/marker is missing");
    Assert(coordinator.Contains("Foreground = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 0, 0, 0))"),
        "Trail Maker Coop checkbox text is not black");
    Assert(coordinator.Contains("Orientation = Orientation.Horizontal") && coordinator.Contains("host.Children.Remove(anchor)"),
        "Trail Maker Coop and Backup options are not arranged side by side");
    Assert(coordinator.Contains("prepared.Publish(destination)") && coordinator.IndexOf("Prepare(", StringComparison.Ordinal) < coordinator.IndexOf("prepared.Publish(destination)", StringComparison.Ordinal),
        "Coop package is not validated before publication");
    Assert(coordinator.Contains("exportOriginal(self, trailMakerSource)") &&
        coordinator.Contains("RemoveNormalTrailFiles(destination)") &&
        coordinator.Contains("importOriginal(self, vanillaImportFolder)") &&
        !coordinator.Contains("CopyTrailFiles("),
        "Coop export is still visible as a normal Custom Trail or cannot be reimported into the Trail Maker");
    Assert(coordinator.Contains("CopySidecars(trailSource, ConfigSettings.GetUserTrailMakerPath(), overwrite: false)"),
        "Coop import can overwrite existing Trail mod-settings sidecars");
    Assert(coordinator.Contains("capturedDocumentsByTrailPath") &&
        coordinator.Contains("ReadModSettingsForExport") &&
        coordinator.Contains("TryReadModSettingsForExport(sourceTrail") &&
        coordinator.Contains("new CoopTrailPackageExporter().Prepare(") &&
        coordinator.Contains("ReadModSettingsForExport);") &&
        coordinator.Contains("effectiveType.IsArray") &&
        coordinator.Contains("Array.CreateInstance") &&
        coordinator.Contains("ModSettingsJson.IsSupportedValue(value)") &&
        coordinator.Contains("MessagePackSerializer.Serialize(propertyType, value)") &&
        coordinator.Contains("MessagePackSerializer.Deserialize(targetType, bytes)"),
        "normal and Coop exports do not share the synchronously captured mod-settings source");
    Assert(coordinator.Contains("AddCoopImportRows(self)") &&
        coordinator.Contains("GetImportableCoopSources(includeWorkshop: true).Any()") &&
        coordinator.Contains("Shared.WorkshopContentPaths.GetSubscribedItemRoots") &&
        coordinator.Contains("mappedImport") && coordinator.Contains("? trailSource") &&
        coordinator.Contains("ObservableCollection<FileRow>"),
        "local and Workshop Coop packages are not routed through Vanilla's safe in-game Trail import path");
    Assert(coordinator.Contains("AddCoopExportRows(self)") &&
        coordinator.Contains("GetImportableCoopSources(includeWorkshop: false)"),
        "local Coop packages are not added to Vanilla's in-game Trail export list or Workshop folders became writable destinations");
    Assert(coordinator.Contains("AddCoopWorkshopRows(self)") &&
        coordinator.Contains("UploadCoopTrailPackage(self, selectedRow.trail, uploadOptions.IncludeModSettings)") &&
        coordinator.Contains("CoopWorkshopPackageStaging.Stage(") &&
        coordinator.Contains("CoopTrailPackageCatalog.Load(source)"),
        "Coop packages are not validated, listed, and staged through the unified Workshop uploader");
    Assert(exporter.Contains("ordinal < 40") && exporter.Contains("activeSlots.Count < 2"), "export limits or two-human validation are missing");
    Assert(exporter.Contains("ModSettingsJson.Read(sidecar)") &&
        exporter.Contains("ModSettingsJson.WriteAtomic(MissionLoader.GetModSettingsPath(jsonPath), modSettings)") &&
        !File.ReadAllText(Path.Combine(root, "CustomCustomTrail.Core", "MissionDefinitionJson.cs")).Contains("[\"modSettings\"]"),
        "Coop mission modsettings are not stored exclusively in sidecars");
    Assert(exporter.Contains("restart.selectedHeader.display_filename") && runtime.Contains("CoopMissionTitle = selected.Loaded.Definition.DisplayName"),
        "exported map names are not shown as Coop mission titles");
    Assert(coordinator.Contains("SetCoopPackagePresentation") && coordinator.Contains("TEXT_COOP_0"),
        "package display names do not replace occupied Vanilla Coop Trail headings");
    Assert(coordinator.Contains("UpdateCoopSelectionTitles") && coordinator.Contains("FindDescendantButton") &&
        coordinator.Contains("\"Coop\", \"Coop2\", \"Coop3\", \"Coop4\"") &&
        coordinator.Contains("PropEx.SetTextCentre(button, packageOccupiesTrail ? coopPackageDisplayName : vanillaTitle)") &&
        coordinator.Contains("UpdateCoopSelectionTitles(null)") &&
        !coordinator.Contains("UpdateCoopSelectionTitles(MainViewModel.Instance"),
        "package display names do not replace occupied entries in the Coop Trail selection menu");
    Assert(coordinator.Contains("typeof(FRONT_CoopTrail1).GetConstructor(Type.EmptyTypes)") &&
        coordinator.Contains("typeof(FRONT_CoopTrail4).GetConstructor(Type.EmptyTypes)"),
        "Coop page presentation is not tied to Vanilla's completed page construction");
    Assert(coordinator.Contains("InitializeCoopPage(self, 0)") &&
        coordinator.Contains("InitializeCoopPage(self, 3)"),
        "Coop page initialization does not cover all four Trails");
    Assert(coordinator.Contains("LogicalTreeHelper.GetChildren(parent)") &&
        coordinator.Contains("BindingOperations.ClearBinding(title, TextBlock.TextProperty)"),
        "the unnameable Noesis title is not resolved through the logical tree and detached from its stale binding");
    Assert(!coordinator.Contains("UpdateCoopTrailTranslationTitles") &&
        !coordinator.Contains("LogCoopTitleState") &&
        !coordinator.Contains("FindDescendantTextBlock"),
        "obsolete title-source mutation, temporary diagnostics, or failed Visual Tree search remain");
    Assert(!coordinator.Contains("QueueDeferredCoopPageRefresh") &&
        !coordinator.Contains("Deferred first-visit Coop Trail title refresh"),
        "the ineffective timing-based first-visit refresh still exists");
    Assert(runtime.Contains("ReadyLock") && runtime.Contains("COOP_START") && runtime.Contains("AreAllHumanPlayersPackageReady"),
        "Ready/Play/COOP_START package validation is missing");
    Assert(runtime.Contains("PlayerIdentityHelper.TryCaptureHumanRoster") &&
        runtime.Contains("requireAuthoritativeLobbyRoster: true") &&
        runtime.Contains("PlayerIdentityHelper.ResolvePlayerIdForSteamId") &&
        runtime.Contains("preferInGameRoster: false") &&
        !runtime.Contains("GameNetworkAPI.GetPlayerIdForSteamId(member.id)") &&
        runtime.Contains("member.dummyToBeKicked") &&
        runtime.Contains("!member.SkirmishHumanMember && member.SkirmishMember") &&
        runtime.Contains("GetHumanPackageStates") &&
        runtime.Contains("ErrorParticipantsMissing") &&
        runtime.Contains("ErrorParticipantsMismatch") &&
        runtime.Contains("ErrorParticipantsNotReady") &&
        !runtime.Contains("SerpLocalization.Get(\"CustomCustomTrail.ErrorParticipantNotReady\")"),
        "participant package validation does not use SyncPerPlayer identities or distinguish failure reasons");
    Assert(viewModel.Contains("ConfigurePerPlayerLobbySettings") &&
        viewModel.Contains("RequireReport(") &&
        viewModel.Contains("private string coopPackageStatus = string.Empty") &&
        viewModel.Contains("get => coopPackageStatus") &&
        !viewModel.Contains("Math.Max(1, GameNetworkAPI.GetLocalPlayerId())") &&
        runtime.Contains("System_RequestPerPlayerSettingsPublish()") &&
        runtime.Contains("System_ArePerPlayerSettingsReady("),
        "client package readiness does not use Shared publication and fail-closed completeness validation");
    Assert(runtime.Contains("MainViewModelInstanceField") &&
        runtime.Contains("GetExistingMainViewModel()?.FRONTMultiplayer") &&
        !runtime.Contains("MainViewModel.Instance?.FRONTMultiplayer"),
        "early package refresh can still construct Vanilla's MainViewModel before the UI is ready");
    Assert(coordinator.Contains("CoopSetupOpened?.Invoke()") &&
        runtime.Contains("CoopSetupOpened += OnCoopSetupOpened") &&
        runtime.Contains("ActivateSelectedMissionSettings(editable: true, source: \"custom Coop mission setup\")"),
        "Coop Customize does not reapply the mission Trail preset after rebuilding the setup UI");
    Assert(coordinator.Contains("GetPacketEventFor<CoopCustomizePacket>") &&
        coordinator.Contains("GameNetworkAPI.GetHostSteamId()") &&
        coordinator.Contains("args.SenderSteamId.Value != host.Value") &&
        coordinator.Contains("BroadcastCoopCustomize(trailId, mission)") &&
        coordinator.Contains("BroadcastCoopLaunch") &&
        coordinator.Contains("CoopLaunchReceived?.Invoke") &&
        coordinator.Contains("source: \"authenticated host packet\"") &&
        packet.Contains("IMessagePackFormatter<CoopCustomizePacket>") &&
        packet.Contains("[Key(0)]") && packet.Contains("[Key(3)]") &&
        project.Contains("src\\CoopCustomizePacket.cs"),
        "Coop setup/launch transitions are not synchronized from the authenticated lobby host to clients");
    Assert(runtime.Contains("Type.EmptyTypes") &&
        runtime.Contains("selected != null && IsLaunchCommand(command)") &&
        runtime.Contains("ActivateSelectedMissionSettings(editable: false, source: \"custom Coop mission \" + command)") &&
        runtime.Contains("CoopLaunchReceived += OnCoopLaunchReceived") &&
        runtime.Contains("source: \"authenticated host Coop launch\"") &&
        runtime.Contains("coopLaunchPending") && runtime.Contains("OnMapStarted()") && runtime.Contains("OnMapUnloaded()"),
        "direct Coop launch does not retain the shared Trail preset across the map transition");
    Assert(runtime.Contains("if (!coopLaunchPending)") && runtime.Contains("BlockLaunch(command") &&
        coordinator.Contains("MpLocalReadyField") && coordinator.Contains("MpLocalReadyLockedField") &&
        coordinator.Contains(".SetValue(self, false)"),
        "Coop launch refresh retention, visible blocking, or Customize ready-state reset is missing");
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
        SchemaVersion = CoopTrailPackageManifestJson.CurrentSchemaVersion,
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

static void TestOldMissionSchemasRejected()
{
    using Fixture schemaOne = Fixture.Create(schemaVersion: 1);
    ExpectFailure(() => new MissionLoader().Load(schemaOne.JsonPath, 1, 1), "mission schema 1 was accepted");
    using Fixture schemaTwo = Fixture.Create(schemaVersion: 2);
    ExpectFailure(() => new MissionLoader().Load(schemaTwo.JsonPath, 1, 1), "mission schema 2 was accepted");
}

static void TestOldPackageSchemaRejected()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    string package = CreatePackage(fixture, root, "OldSchema", 1);
    string manifestPath = Path.Combine(package, "cooptrail.json");
    string manifest = File.ReadAllText(manifestPath).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1");
    File.WriteAllText(manifestPath, manifest, new UTF8Encoding(false));
    ExpectFailure(() => CoopTrailPackageCatalog.Load(package), "Coop package schema 1 was accepted");
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
    string json = File.ReadAllText(fixture.SidecarPath).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
    File.WriteAllText(fixture.SidecarPath, json, new UTF8Encoding(false));
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(!string.IsNullOrWhiteSpace(loaded.Definition.ModSettingsError), "invalid sidecar was not reported");
    Assert(loaded.Definition.ModSettings.Mods.Count == 0, "invalid sidecar was partially retained instead of remaining unmanaged");
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
    File.AppendAllText(Path.Combine(package, "CoopMissions", "01.modjson"), "changed");
    ExpectFailure(() => CoopTrailPackageCatalog.Load(package), "changed mission sidecar passed its fingerprint");
}

static void TestCoopWorkshopStaging()
{
    using Fixture fixture = Fixture.Create();
    string packagesRoot = Path.Combine(fixture.Root, "CustomTrails");
    string source = CreatePackage(fixture, packagesRoot, "Upload", 2);
    string trailMakerSource = Path.Combine(source, "TrailMakerSource");
    Directory.CreateDirectory(trailMakerSource);
    File.WriteAllText(Path.Combine(trailMakerSource, "Trail_Mission_01.trail"), "trail");
    File.WriteAllText(Path.Combine(trailMakerSource, "Trail_Mission_01.modjson"), "editable sidecar");
    File.WriteAllText(Path.Combine(source, "Upload.data"), "metadata");
    CoopTrailPackage package = CoopTrailPackageCatalog.Load(source);

    string included = Path.Combine(fixture.Root, "staging-included");
    CoopTrailPackage includedPackage = CoopWorkshopPackageStaging.Stage(
        package, included, "Upload.data", includeModSettings: true, out int includedCount);
    Assert(includedCount == 3, "not all Coop and Trail Maker sidecars were staged");
    Assert(File.Exists(Path.Combine(included, "CoopMissions", "01.modjson")), "Coop sidecar was omitted");
    Assert(File.Exists(Path.Combine(included, "TrailMakerSource", "Trail_Mission_01.modjson")), "Trail Maker sidecar was omitted");
    Assert(!File.Exists(Path.Combine(included, "Upload.data")), "Workshop metadata was copied into content");

    string excluded = Path.Combine(fixture.Root, "staging-excluded");
    CoopTrailPackage excludedPackage = CoopWorkshopPackageStaging.Stage(
        package, excluded, "Upload.data", includeModSettings: false, out int excludedCount);
    Assert(excludedCount == 0, "excluded Coop staging reported copied sidecars");
    Assert(!Directory.GetFiles(excluded, "*.modjson", SearchOption.AllDirectories).Any(), "excluded Coop staging contains modsettings");
    Assert(includedPackage.Manifest.ContentFingerprint != excludedPackage.Manifest.ContentFingerprint,
        "including mission sidecars did not affect the staged package fingerprint");
    Assert(CoopTrailPackageCatalog.Load(included).Missions[0].Definition.ModSettings.Mods.Count == 1,
        "included staged package lost mission modsettings");
    Assert(CoopTrailPackageCatalog.Load(excluded).Missions[0].Definition.ModSettings.Mods.Count == 0,
        "excluded staged package retained mission modsettings");
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

static void TestIdenticalPackageReplicas()
{
    using Fixture fixture = Fixture.Create();
    string localRoot = Path.Combine(fixture.Root, "CustomTrails");
    string workshopRoot = Path.Combine(fixture.Root, "WorkshopItem");
    string localPackage = CreatePackage(fixture, localRoot, "Replica", 1);
    string workshopPackage = Path.Combine(workshopRoot, "Replica");
    CopyTree(localPackage, workshopPackage);

    var catalog = new CoopTrailPackageCatalog();
    catalog.Scan(new[] { localRoot, workshopRoot }, null, null);
    Assert(catalog.Packages.Count == 1, "identical local and Workshop replicas were rejected");
    Assert(string.Equals(catalog.Packages.Values.Single().RootPath, Path.GetFullPath(localPackage), StringComparison.OrdinalIgnoreCase),
        "the first local package was not preferred over its identical Workshop replica");
}

static void CopyTree(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
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
        string sidecar = Path.Combine(missions, ordinal.ToString("00") + ".modjson");
        File.Copy(fixture.SidecarPath, sidecar);
        fingerprintFiles.Add(sidecar);
    }
    var manifest = new CoopTrailPackageManifest
    {
        SchemaVersion = CoopTrailPackageManifestJson.CurrentSchemaVersion,
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
    public string SidecarPath { get; private set; }

    public static Fixture Create(int aivRotation = 90, int? secondAivRotation = null, int preferredAiv = -1, int schemaVersion = 3, int startGold = 500, bool includeLegacyModSetting = false)
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
            ModSettings = ModSettingsDefinition.CreateUnmanaged(),
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
        string sidecarPath = MissionLoader.GetModSettingsPath(jsonPath);
        ModSettingsJson.WriteAtomic(sidecarPath, definition.ModSettings);
        if (requestedSchemaVersion != MissionLoader.CurrentSchemaVersion || requestedAivRotation != definition.Players[2].Aivs[0].Rotation)
        {
            string json = File.ReadAllText(jsonPath, Encoding.UTF8);
            if (requestedSchemaVersion != MissionLoader.CurrentSchemaVersion)
                json = json.Replace("\"schemaVersion\": 3", "\"schemaVersion\": " + requestedSchemaVersion, StringComparison.Ordinal);
            if (requestedAivRotation != definition.Players[2].Aivs[0].Rotation)
                json = json.Replace("\"rotation\": 90", "\"rotation\": " + requestedAivRotation, StringComparison.Ordinal);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
        }
        return new Fixture { Root = root, JsonPath = jsonPath, SidecarPath = sidecarPath };
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
    }
}

[AttributeUsage(AttributeTargets.Property)]
sealed class SyncHostOnlyAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
sealed class DoNotPersistAttribute : Attribute
{
}

sealed class CompatibleTrailSettingsViewModel
{
    [SyncHostOnly]
    public bool EnableMod { get; set; } = true;

    [SyncHostOnly]
    public int Strength { get; set; } = 42;

    [SyncHostOnly]
    public string Label { get; set; } = "ready";

    [SyncHostOnly, DoNotPersist]
    public int TransientStatus { get; set; } = 7;

    public bool OmitStrengthFromSnapshot { get; set; }
    public bool EnabledInDisabledSnapshot { get; set; }
    public bool IsMissionPresetActive { get; private set; }

    public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot()
    {
        var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [nameof(EnableMod)] = BitConverter.GetBytes(EnabledInDisabledSnapshot),
            [nameof(Label)] = Encoding.UTF8.GetBytes("default"),
        };
        if (!OmitStrengthFromSnapshot)
            snapshot[nameof(Strength)] = BitConverter.GetBytes(42);
        return snapshot;
    }

    public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable) =>
        IsMissionPresetActive = true;

    public void System_ExitMissionPreset() => IsMissionPresetActive = false;
}

sealed class MissingMissionApiViewModel
{
    [SyncHostOnly]
    public int Strength { get; set; } = 1;
}

sealed class NonBooleanEnableModViewModel
{
    [SyncHostOnly]
    public int EnableMod { get; set; } = 1;

    public bool IsMissionPresetActive => false;

    public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
        new Dictionary<string, byte[]> { [nameof(EnableMod)] = BitConverter.GetBytes(0) };

    public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
    {
    }

    public void System_ExitMissionPreset()
    {
    }
}

sealed class OptedOutPlugin
{
    public const bool CustomCustomTrailModSettingsOptOut = true;
}

sealed class NotOptedOutPlugin
{
    public const bool CustomCustomTrailModSettingsOptOut = false;
}

sealed class RuntimeOptOutFieldPlugin
{
    public static bool CustomCustomTrailModSettingsOptOut = true;
}

namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
    }
}

namespace Shared
{
    internal static class DebugLogHelper
    {
        internal static void LogError(BepInEx.Logging.ManualLogSource log, string message)
        {
        }

        internal static void LogWarning(BepInEx.Logging.ManualLogSource log, string message)
        {
        }

        internal static void LogInfo(BepInEx.Logging.ManualLogSource log, string message)
        {
        }
    }
}

namespace SHCDESE.API.Components.SaveData
{
    public sealed class SaveContext
    {
        public SaveContext(bool isSaveFile, bool isMapEditorSave)
        {
            IsSaveFile = isSaveFile;
            IsMapEditorSave = isMapEditorSave;
        }

        public bool IsSaveFile { get; }
        public bool IsMapEditorSave { get; }
    }

    public sealed class LoadContext
    {
        public LoadContext(bool isSaveFile) => IsSaveFile = isSaveFile;
        public bool IsSaveFile { get; }
    }
}

namespace SHCDESE.API
{
    public sealed class ModSaveDataAPI
    {
        public static ModSaveDataAPI Instance { get; } = new ModSaveDataAPI();
        public Func<Components.SaveData.SaveContext, byte[]> SaveCallback { get; private set; }
        public Action<byte[], Components.SaveData.LoadContext> LoadCallback { get; private set; }
        public Action OnUnloadCallback { get; private set; }

        public bool RegisterModDataHandler(
            string modIdentifier,
            Func<Components.SaveData.SaveContext, byte[]> saveCallback,
            Action<byte[], Components.SaveData.LoadContext> loadCallback,
            Action onUnloadCallback)
        {
            SaveCallback = saveCallback;
            LoadCallback = loadCallback;
            OnUnloadCallback = onUnloadCallback;
            return true;
        }
    }
}
