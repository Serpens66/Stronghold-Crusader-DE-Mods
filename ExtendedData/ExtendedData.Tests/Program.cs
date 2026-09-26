using ExtendedData.Core;
using ExtendedData;
using Shared;
using System.Text;
using System.Text.Json;

var tests = new (string Name, Action Run)[]
{
    ("bundled mission loads", TestBundledMission),
    ("Coop mission modsettings use an optional sidecar", TestCoopMissionModSettingsSidecar),
    ("path escape rejected", TestPathEscape),
    ("invalid rotation rejected", TestInvalidRotation),
    ("package catalog rejects invalid packages", TestCatalogIsolation),
    ("package catalog ignores in-progress Coop Trail exports", TestCatalogIgnoresExportStaging),
    ("package catalog logs only state changes", TestCatalogStateChangeLogging),
    ("package catalog reports new and resolved errors", TestCatalogErrorTransitions),
    ("old mission schemas are rejected", TestOldMissionSchemasRejected),
    ("old Coop package schema is rejected", TestOldPackageSchemaRejected),
    ("locally edited mission JSON reloads from the same slot", TestEditedMissionReload),
    ("invalid mod settings are isolated", TestInvalidModSettings),
    ("first two active players become allied humans", TestHumanProjection),
    ("source alliances are normalized for Vanilla Coop", TestCanonicalTeamProjection),
    ("complete multiplayer setup roundtrips", TestMultiplayerSetupRoundtrip),
    ("malformed multiplayer setup is rejected", TestInvalidMultiplayerSetup),
    ("preferred AIV permits differing rotations", TestPreferredAiv),
    ("fourth trail tenth slot is addressable", TestLastCatalogSlot),
    ("package fingerprint detects content changes", TestPackageFingerprint),
    ("Coop Workshop staging filters modsettings", TestCoopWorkshopStaging),
    ("Coop Lord requirements remain mandatory when modsettings are excluded", TestCoopLordRequirementStaging),
    ("duplicate package IDs are rejected", TestDuplicatePackageIds),
    ("identical local and Workshop replicas are merged", TestIdenticalPackageReplicas),
    ("ordinal mapping covers four trails and ignores mission 41", TestOrdinalMapping),
    ("native mod-settings JSON roundtrip", TestNativeModSettingsRoundtrip),
    ("dynamic third-party mod ids are preserved", TestModSettingsRegistry),
    ("missing mod entry uses the mod-defined default", TestMissingModEntry),
    ("Trail mod compatibility contract is validated", TestTrailModCompatibilityContract),
    ("explicit plugin opt-out marker is honored", TestExplicitPluginOptOut),
    ("sidecar schema evolution keeps only current settings", TestSidecarSettingsSchemaEvolution),
    ("coop mission schema evolution keeps only current settings", TestCoopSettingsSchemaEvolution),
    ("invalid mod-settings documents are rejected", TestInvalidModSettingsDocuments),
    ("atomic sidecar write replaces existing file", TestAtomicSidecarWrite),
    ("Trail coordinator ownership is centralized", TestCoordinatorOwnership),
    ("mission preset lifecycle preserves only expected replacements", TestMissionPresetLifecycleState),
    ("Trail Maker authoring preset survives test returns", TestTrailMakerAuthoringSessionIntegration),
    ("BugfixesAndQoL Customize button delegation is optional", TestCustomizeButtonDelegation),
    ("customized launch origin is persisted and fail-closed", TestCustomizedLaunchOriginIntegration),
    ("customized launch origin save roundtrip", TestCustomizedLaunchOriginRoundtrip),
    ("Built-in Customize origin packet roundtrip", TestBuiltInCustomizeOriginPacketRoundtrip),
    ("Map mod-settings packet roundtrip", TestMapModSettingsPacketRoundtrip),
    ("Map mod-settings runtime integration is fail-closed", TestMapModSettingsRuntimeIntegration),
    ("lobby packets retain main-thread ordering", TestLobbyPacketThreadMarshalling),
    ("Steam Workshop discovery waits for Steamworks", TestSteamWorkshopReadinessGate),
    ("local activation setting gates the complete runtime", TestLocalActivationSetting),
    ("Script Extender manifest range contract is explicit", TestScriptExtenderManifestRangeContract),
    ("Trail Maker Coop export is integrated", TestCoopExporterIntegration),
    ("Coop package JSON is Unity dependency-free", TestDependencyFreeCoopJson),
    ("mission and manifest JSON use CRLF", TestCoopJsonLineEndings),
    ("Workshop Trail staging includes nested JSON", TestWorkshopTrailSidecars),
    ("Workshop Trail staging preserves existing JSON", TestWorkshopTrailJsonConflict),
    ("Workshop Trail staging rejects overlapping roots", TestWorkshopTrailJsonUnsafeRoots),
    ("Workshop upload checkbox is unified", TestWorkshopUploadCheckbox),
    ("editor saves require an explicit modsettings decision", TestEditorSaveModSettingsOptions),
    ("mod-data namespaces are isolated and immutable", TestModDataNamespaces),
    ("invalid mod-data containers fail closed", TestInvalidModDataContainers),
    ("map mod-data API distinguishes file and namespace absence", TestMapModDataApi),
    ("Custom Lord mod-data API supports paths and configs", TestLordModDataApi),
    ("selected Lord data uses host snapshots without local files", TestSelectedLordDataSnapshot),
    ("Lord data rejects altered and oversized snapshots", TestInvalidLordDataSnapshot),
    ("Fixes preference snapshots retain every current and future property", TestFixesPreferenceCodec),
    ("Fixes preference snapshots reject incompatible schemas and lossy values", TestFixesPreferenceIncompatibility),
    ("Lord sync diagnostics identify state without logging JSON values", TestLordSyncDiagnostics),
    ("deferred Lord confirmations reject stale lobby and selection", TestDeferredLordPublication),
    ("Lord sync accepts a confirmed lobby before game mode becomes multiplayer", TestLordSyncLobbyGate),
    ("selected Lord fingerprint ignores media and detects gameplay files", TestLordPackageFingerprint),
    ("Trail Lord requirements bind the mission and survive optional-media changes", TestTrailLordRequirements),
    ("Trail Lord selection distinguishes embedded, replaced Custom, and Vanilla Lords", TestTrailLordSelection),
    ("Lord Workshop metadata never requires local multiplayer files", TestLordWorkshopMetadataFingerprint),
    ("selected Lord package manifest binds identities and mode", TestLordPackageManifest),
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

static void TestModDataNamespaces()
{
    const string json = "{\"Other.Mod\":{\"untouched\":true},\"Author.Example-Mod\":{\"schemaVersion\":1,\"nested\":{\"values\":[1,2]}}}";
    ExtendedDataModDataReadResult result = ModDataNamespaceReader.Read(json, "author.example-mod", "memory");

    Assert(result.Success, result.Diagnostic);
    Assert(result.ModGuid == "Author.Example-Mod", "the document's canonical GUID casing was not retained");
    Assert(result.Data.Count == 2 && !result.Data.ContainsKey("untouched"), "foreign namespace data leaked into the result");
    Assert(!result.Json.Contains("Other.Mod", StringComparison.Ordinal), "foreign namespace leaked into namespace JSON");
    Assert(json.Contains("\"Other.Mod\":{\"untouched\":true}", StringComparison.Ordinal), "source document was modified");

    var dictionary = (System.Collections.IDictionary)result.Data;
    AssertThrows<NotSupportedException>(() => dictionary.Add("changed", true), "top-level snapshot was mutable");
    var nested = (IReadOnlyDictionary<string, object>)result.Data["nested"];
    var values = (IReadOnlyList<object>)nested["values"];
    AssertThrows<NotSupportedException>(
        () => ((System.Collections.IList)values).Add(3),
        "nested snapshot was mutable");
}

static void TestInvalidModDataContainers()
{
    Assert(ModDataNamespaceReader.Read("[]", "author.mod", "memory").Status == ExtendedDataModDataReadStatus.InvalidDocument,
        "array root was accepted");
    Assert(ModDataNamespaceReader.Read("{", "author.mod", "memory").Status == ExtendedDataModDataReadStatus.InvalidDocument,
        "invalid JSON was accepted");
    Assert(ModDataNamespaceReader.Read("{\"author.mod\":1}", "author.mod", "memory").Status == ExtendedDataModDataReadStatus.InvalidDocument,
        "primitive namespace was accepted");
    Assert(ModDataNamespaceReader.Read("{\"Author.Mod\":{},\"author.mod\":{}}", "author.mod", "memory").Status == ExtendedDataModDataReadStatus.InvalidDocument,
        "case-insensitive GUID collision was accepted");
    Assert(ModDataNamespaceReader.Read("{\"other.mod\":{}}", "author.mod", "memory").Status == ExtendedDataModDataReadStatus.NamespaceNotFound,
        "missing namespace was not reported");
}

static void TestMapModDataApi()
{
    SHCDESE.API.GameMapArchiveManagerAPI.Instance.Reset();
    Assert(ExtendedDataModDataApi.ReadCurrentMapNamespace(" ").Status == ExtendedDataModDataReadStatus.InvalidRequest,
        "empty map mod GUID was not rejected");
    ExtendedDataModDataReadResult missingFile = ExtendedDataModDataApi.ReadCurrentMapNamespace("author.mod");
    Assert(missingFile.Status == ExtendedDataModDataReadStatus.FileNotFound, "missing modmap.json was not reported");

    SHCDESE.API.GameMapArchiveManagerAPI.Instance.CurrentFilePath = @"C:\Maps\test.map";
    SHCDESE.API.GameMapArchiveManagerAPI.Instance.ModMapBytes = Encoding.UTF8.GetBytes("{\"other.mod\":{}}");
    ExtendedDataModDataReadResult missingNamespace = ExtendedDataModDataApi.ReadCurrentMapNamespace("author.mod");
    Assert(missingNamespace.Status == ExtendedDataModDataReadStatus.NamespaceNotFound, "missing map namespace was not reported");
    Assert(missingNamespace.Source.EndsWith("test.map::modmap.json", StringComparison.Ordinal), "map source was not identified");

    SHCDESE.API.GameMapArchiveManagerAPI.Instance.ModMapBytes = Encoding.UTF8.GetBytes("{\"AUTHOR.MOD\":{\"value\":7}}");
    ExtendedDataModDataReadResult success = ExtendedDataModDataApi.ReadCurrentMapNamespace("author.mod");
    Assert(success.Success && Convert.ToInt32(success.Data["value"]) == 7, "map namespace was not read");

    SHCDESE.API.GameMapArchiveManagerAPI.Instance.ModMapBytes = new byte[] { 0xFF };
    Assert(ExtendedDataModDataApi.ReadCurrentMapNamespace("author.mod").Status == ExtendedDataModDataReadStatus.InvalidDocument,
        "invalid UTF-8 was accepted");

    SHCDESE.API.GameMapArchiveManagerAPI.Instance.ModMapBytes = new UTF8Encoding(true).GetPreamble()
        .Concat(Encoding.UTF8.GetBytes("{\"author.mod\":{}}"))
        .ToArray();
    Assert(ExtendedDataModDataApi.ReadCurrentMapNamespace("author.mod").Success, "UTF-8 BOM was not accepted");
}

static void TestLordModDataApi()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataModLordTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        string lordPath = Path.Combine(root, "aggressive.v2.lordjson");
        string sidecarPath = Path.Combine(root, "aggressive.v2.modlord.json");
        File.WriteAllText(sidecarPath, "{\"author.mod\":{\"schemaVersion\":1}}", new UTF8Encoding(false));

        ExtendedDataModDataReadResult byPath = ExtendedDataModDataApi.ReadLordNamespace(lordPath, "AUTHOR.MOD");
        Assert(byPath.Success && byPath.Source == sidecarPath, "Lord namespace path lookup failed");

        var config = new CustomisationFileManager.CustomLordConfig { path = root, name = "aggressive.v2" };
        ExtendedDataModDataReadResult byConfig = ExtendedDataModDataApi.ReadLordNamespace(config, "author.mod");
        Assert(byConfig.Success, "CustomLordConfig lookup failed: " + byConfig.Diagnostic);

        ExtendedDataModDataReadResult absent = ExtendedDataModDataApi.ReadLordNamespace(
            Path.Combine(root, "absent.lordjson"),
            "author.mod");
        Assert(absent.Status == ExtendedDataModDataReadStatus.FileNotFound, "missing Lord sidecar was not reported");
        ExtendedDataModDataApi.SetVerifiedLocalLords(new Dictionary<int, string> { [3] = lordPath });
        Assert(ExtendedDataModDataApi.ReadSelectedLordNamespace(3, "author.mod").Success,
            "verified local Lord was not readable by player ID");
        Assert(ExtendedDataModDataApi.ReadLordNamespace("aggressive.v2.lordjson", "author.mod").Success,
            "verified local configuration could not be resolved without the host path");
        Assert(ExtendedDataModDataApi.ReadLordNamespace("absent.lordjson", "author.mod").Status ==
            ExtendedDataModDataReadStatus.HostDataUnavailable,
            "unselected local Lord bypassed the verified selection");
    }
    finally
    {
        ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
        Directory.Delete(root, true);
    }
}

static void TestSelectedLordDataSnapshot()
{
    var first = new LordDataSlot
    {
        PlayerId = 2, LordName = "workshop\\Lord", ConfigName = "aggressive", ConfigChecksum = "19",
        ModLordJson = "{\"example.mod\":{\"aggression\":4}}",
        FixesJson = "{\"EnableHopsFarmFix\":true}",
    };
    var second = new LordDataSlot
    {
        PlayerId = 3, LordName = "workshop\\Lord", ConfigName = "defensive", ConfigChecksum = "20",
        ModLordJson = null, FixesJson = first.FixesJson,
    };
    LordDataSnapshot snapshot = LordDataSnapshot.Parse(
        LordDataSnapshot.Create("lobby-1", true, new[] { second, first }).WireJson);
    Assert(snapshot.Slots.Count == 2 && snapshot.GetSlot(2).ConfigName == "aggressive", "slot order changed");
    ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
    try
    {
        ExtendedDataModDataReadResult selected = ExtendedDataModDataApi.ReadSelectedLordNamespace(2, "example.mod");
        Assert(selected.Success && selected.Json.Contains("aggression"), "host namespace was not readable");
        var config = new CustomisationFileManager.CustomLordConfig { name = "defensive", checksum = 20 };
        Assert(ExtendedDataModDataApi.ReadLordNamespace(config, "example.mod").Status ==
            ExtendedDataModDataReadStatus.FileNotFound, "explicit host absence fell back to a local sidecar");
        Assert(ExtendedDataModDataApi.ReadLordNamespace("aggressive.lordjson", "example.mod").Status ==
            ExtendedDataModDataReadStatus.Success, "selected path did not use host data");
    }
    finally
    {
        ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
    }
}

static void TestInvalidLordDataSnapshot()
{
    LordDataSnapshot snapshot = LordDataSnapshot.Create("lobby-2", false, new[]
    {
        new LordDataSlot { PlayerId = 2, LordName = "Lord", ConfigName = "a", ConfigChecksum = "1",
            ModLordJson = "{\"example.mod\":{}}" },
    });
    AssertThrows<Exception>(() => LordDataSnapshot.Parse(snapshot.WireJson.Replace("example.mod", "other.mod")),
        "modified data passed the snapshot checksum");
    AssertThrows<Exception>(() => LordDataSnapshot.Create("lobby-3", false, new[]
    {
        new LordDataSlot { PlayerId = 2, LordName = "Lord", ConfigName = "a", ConfigChecksum = "1",
            ModLordJson = "{\"example.mod\":{\"text\":\"" + new string('x', LordDataSnapshot.MaxSidecarBytes) + "\"}}" },
    }), "oversized sidecar was accepted");
    AssertThrows<Exception>(() => LordDataSnapshot.Create("lobby-3", false, new[]
    {
        new LordDataSlot { PlayerId = 2, LordName = "Lord", ConfigName = "a", ConfigChecksum = "1",
            ModLordJson = "{\"Example.Mod\":{},\"example.mod\":{}}" },
    }), "ambiguous GUID casing was accepted");
    AssertThrows<Exception>(() => LordDataSnapshot.Create("lobby-3", false, new[]
    {
        new LordDataSlot { PlayerId = 2, LordName = "Lord", ConfigName = "a", ConfigChecksum = "1",
            FixesJson = "{}" },
    }), "Fixes preferences without Fixes were accepted");
}

static void TestFixesPreferenceCodec()
{
    var host = new FutureFixesPreferences
    {
        Field01 = true, Field02 = 37, Field03 = -2, Field04 = 4,
        Field05 = 5, Field06 = 6, Field07 = 7, Field08 = 8,
        Field09 = 9, Field10 = 10, Field11 = 11, Field12 = 12,
        Field13 = 13, Field14 = 14, Field15 = 15, Field16 = 16,
        Field17 = 17, Field18 = 18,
        Nested = new FutureFixesNested { Weight = 2.5, Enabled = false },
        Values = Enumerable.Range(0, 100).ToDictionary(index => "setting" + index, index => new List<int> { index, -index }),
    };
    string json = TypedPreferenceSnapshotCodec.Capture(host);
    var client = (FutureFixesPreferences)TypedPreferenceSnapshotCodec.Restore(typeof(FutureFixesPreferences), json);
    Assert(client.Field01 == true && client.Field18 == 18 && client.Nested.Weight == 2.5,
        "scalar or nested Fixes values were lost");
    Assert(client.Values.Count == 100 && client.Values["setting99"].SequenceEqual(new[] { 99, -99 }),
        "the preference field count or nested dictionary was truncated");
    var reverse = new FutureFixesPreferences { Values = host.Values.Reverse().ToDictionary(item => item.Key, item => item.Value),
        Field01 = host.Field01, Field02 = host.Field02, Field03 = host.Field03, Field04 = host.Field04,
        Field05 = host.Field05, Field06 = host.Field06, Field07 = host.Field07, Field08 = host.Field08,
        Field09 = host.Field09, Field10 = host.Field10, Field11 = host.Field11, Field12 = host.Field12,
        Field13 = host.Field13, Field14 = host.Field14, Field15 = host.Field15, Field16 = host.Field16,
        Field17 = host.Field17, Field18 = host.Field18, Nested = host.Nested };
    Assert(TypedPreferenceSnapshotCodec.Capture(reverse) == json,
        "dictionary insertion order changed the transferred preferences");

    LordDataSnapshot snapshot = LordDataSnapshot.Parse(LordDataSnapshot.Create("lobby-fixes", true, new[]
    {
        new LordDataSlot { PlayerId = 2, LordName = "Lord", ConfigName = "a", ConfigChecksum = "1", FixesJson = json },
        new LordDataSlot { PlayerId = 3, LordName = "Lord", ConfigName = "b", ConfigChecksum = "2", FixesJson = json },
        new LordDataSlot { PlayerId = 4, LordName = "Absent", ConfigName = "c", ConfigChecksum = "3", FixesJson = null },
    }).WireJson);
    Assert(snapshot.GetSlot(2).FixesJson == json && snapshot.GetSlot(3).FixesJson == json &&
        snapshot.GetSlot(4).FixesJson == null, "selected configurations or missing preferences changed in transit");
}

static void TestFixesPreferenceIncompatibility()
{
    string json = TypedPreferenceSnapshotCodec.Capture(new FutureFixesPreferences());
    AssertThrows<InvalidDataException>(() => TypedPreferenceSnapshotCodec.Restore(typeof(OlderFixesPreferences), json),
        "a client missing host properties accepted the snapshot");
    AssertThrows<InvalidDataException>(() => TypedPreferenceSnapshotCodec.Restore(typeof(FutureFixesPreferences),
        json.Replace("\"Field18\": null", "\"Field18\": \"bad\"")),
        "a client with an incompatible property type accepted the snapshot");
    AssertThrows<InvalidDataException>(() => TypedPreferenceSnapshotCodec.Restore(typeof(ChangedFixesPreferences),
        TypedPreferenceSnapshotCodec.Capture(new OlderFixesPreferences { Field01 = true })),
        "a changed Fixes property type was accepted");
    AssertThrows<InvalidDataException>(() => TypedPreferenceSnapshotCodec.Restore(typeof(FutureFixesPreferences),
        json.Replace("\"Field18\"", "\"UnknownField\"")),
        "a renamed host property was ignored");
    AssertThrows<InvalidDataException>(() => TypedPreferenceSnapshotCodec.Capture(new UnsupportedFixesPreferences()),
        "an unsupported future property was silently omitted");
    AssertThrows<InvalidDataException>(() => TypedPreferenceSnapshotCodec.Capture(new DecimalFixesPreferences
        { Value = 0.1234567890123456789012345678m }),
        "a decimal value that the JSON parser cannot reproduce was accepted");
}

static void TestLordSyncDiagnostics()
{
    const string privateValue = "PRIVATE_VALUE_NOT_FOR_LOGS";
    const string fixesJson = "{\"PrivateField\":\"" + privateValue + "\"}";
    LordDataSnapshot snapshot = LordDataSnapshot.Create("test-lobby", true, new[]
    {
        new LordDataSlot { PlayerId = 3, LordName = "Example Lord", ConfigName = "aggressive",
            ConfigChecksum = "123", ModLordJson = "{\"example.mod\":{\"value\":\"" + privateValue + "\"}}",
            FixesJson = fixesJson },
        new LordDataSlot { PlayerId = 4, LordName = "No Preferences", ConfigName = "default",
            ConfigChecksum = "456", ModLordJson = null, FixesJson = null },
    });
    string summary = LordDataSyncDiagnostics.DescribeSnapshot(snapshot);
    Assert(summary.Contains(snapshot.Digest, StringComparison.Ordinal) &&
        summary.Contains("lord=Example Lord", StringComparison.Ordinal) &&
        summary.Contains("fields=1", StringComparison.Ordinal) &&
        summary.Contains("fixes=absent", StringComparison.Ordinal) &&
        summary.Contains(LordDataSyncDiagnostics.Hash(fixesJson), StringComparison.Ordinal),
        "the summary omitted the information needed to compare host and client");
    Assert(!summary.Contains(privateValue, StringComparison.Ordinal) &&
        !summary.Contains("PrivateField", StringComparison.Ordinal) &&
        !summary.Contains("example.mod", StringComparison.Ordinal),
        "a JSON key or value leaked into the diagnostic summary");
    Assert(LordDataSyncDiagnostics.DescribeStatus("READY|" + snapshot.Digest, snapshot.Digest) == "ready:match" &&
        LordDataSyncDiagnostics.DescribeStatus("READY|old", snapshot.Digest)
            .StartsWith("ready:awaiting-current-snapshot:") &&
        LordDataSyncDiagnostics.DescribeStatus("ERROR|" + privateValue, snapshot.Digest) == "error" &&
        LordDataSyncDiagnostics.DescribeStatus(null, snapshot.Digest) == "missing",
        "the acknowledgement diagnostic misclassified a player state or exposed an error value");
    LordPackageManifest valuesOnly = LordPackageManifest.Create("test-lobby", false, new[]
    {
        new LordPackageSlot { PlayerId = 3, LordType = 1, LordName = "Example Lord",
            ConfigName = "aggressive", ConfigChecksum = "123", FileDigest = snapshot.Digest,
            NeedsSnapshot = true, NeedsLocalFiles = false },
    });
    Assert(LordDataSyncDiagnostics.DescribePackageMode(valuesOnly).Contains("mode=values-sync") &&
        LordDataSyncDiagnostics.DescribePackageMode(valuesOnly).Contains("requiredLocalSlots=0") &&
        LordDataSyncDiagnostics.DescribePackageStatus("LOCAL|" + valuesOnly.Digest + "|0",
            valuesOnly) == "values-sync:local-copies=0" &&
        LordDataSyncDiagnostics.DescribePackageStatus("LOCAL|" + valuesOnly.Digest + "|4",
            valuesOnly) == "values-sync:local-copies=1" &&
        LordDataSyncDiagnostics.DescribePackageStatus(null, valuesOnly) ==
            "values-sync:local-copy-status-optional",
        "a client without an optional Lord folder was described as blocked or its local fallback was hidden");
    LordPackageManifest localRequired = LordPackageManifest.Create("test-lobby", true,
        valuesOnly.Slots);
    Assert(LordDataSyncDiagnostics.DescribePackageMode(localRequired).Contains("mode=local-values") &&
        LordDataSyncDiagnostics.DescribePackageStatus("LOCAL|" + localRequired.Digest + "|0",
            localRequired) == "required-local-files-missing" &&
        LordDataSyncDiagnostics.DescribePackageStatus("LOCAL|" + localRequired.Digest + "|4",
            localRequired) == "required-local-files-match" &&
        LordDataSyncDiagnostics.DescribePackageStatus("LOCAL|" + valuesOnly.Digest + "|4",
            localRequired) == "awaiting-current-manifest",
        "required Lord files or an outdated selection were misreported");
    LordPackageManifest extraFiles = LordPackageManifest.Create("test-lobby", false, new[]
    {
        new LordPackageSlot { PlayerId = 3, LordType = 1, LordName = "Example Lord",
            ConfigName = "aggressive", ConfigChecksum = "123", FileDigest = snapshot.Digest,
            NeedsSnapshot = true, NeedsLocalFiles = true },
    });
    Assert(LordDataSyncDiagnostics.DescribePackageMode(extraFiles).Contains("requiredLocalSlots=1") &&
        LordDataSyncDiagnostics.DescribePackageStatus("LOCAL|" + extraFiles.Digest + "|0",
            extraFiles) == "required-local-files-missing" &&
        LordDataSyncDiagnostics.DescribePackageStatus("LOCAL|" + extraFiles.Digest + "|4",
            extraFiles) == "required-local-files-match",
        "extra gameplay files were reported as optional during values sync");
    string skipped = LordDataSyncDiagnostics.DescribeHostGate(true, true, false,
        false, true, true, false);
    Assert(skipped.Contains("eligible=False", StringComparison.Ordinal) &&
        skipped.Contains("active-lobby-mismatch", StringComparison.Ordinal),
        "a skipped host capture omitted its reason");
}

static void TestLordSyncLobbyGate()
{
    Assert(LordDataSyncDiagnostics.IsHostLobby(true, true, false, true, true, true),
        "a confirmed network lobby must be eligible even before an active multiplayer game exists");
    Assert(!LordDataSyncDiagnostics.IsHostLobby(true, true, false, true, false, true) &&
        !LordDataSyncDiagnostics.IsHostLobby(true, true, false, false, true, true) &&
        !LordDataSyncDiagnostics.IsHostLobby(true, true, false, true, true, false) &&
        !LordDataSyncDiagnostics.IsHostLobby(true, true, true, true, true, true),
        "stale, unobserved, local-skirmish and single-player lobbies must not publish");
    string waitingRoom = LordDataSyncDiagnostics.DescribeHostGate(true, true, false,
        true, true, true, false);
    Assert(waitingRoom.Contains("eligible=True", StringComparison.Ordinal) &&
        waitingRoom.Contains("mapModeRealMultiplayer=False", StringComparison.Ordinal),
        "the diagnostic conflated lobby readiness with active map mode");
    LordDataSnapshot empty = LordDataSnapshot.Create("lobby", true, Array.Empty<LordDataSlot>());
    LordDataSnapshot selected = LordDataSnapshot.Create("lobby", true, new[]
    {
        new LordDataSlot { PlayerId = 3, LordName = "Example", ConfigName = "one",
            ConfigChecksum = "1" },
    });
    Assert(!LordDataSyncDiagnostics.MatchesSelectedSlots(empty, new[] { 3 }) &&
        LordDataSyncDiagnostics.MatchesSelectedSlots(selected, new[] { 3 }) &&
        !LordDataSyncDiagnostics.MatchesSelectedSlots(selected, new[] { 4 }),
        "an empty or stale snapshot was accepted for a selected Custom Lord");
}

static void TestDeferredLordPublication()
{
    const string session = "lobby-1", digest = "digest-1", wire = "wire-1", status = "READY|digest-1";
    Assert(LordDataSyncDiagnostics.MatchesDeferredPublication(session, digest, wire, status,
        session, digest, wire, status), "current Lord confirmation was rejected");
    Assert(!LordDataSyncDiagnostics.MatchesDeferredPublication(session, digest, wire, status,
        "lobby-2", digest, wire, status), "an old lobby confirmation was accepted");
    Assert(!LordDataSyncDiagnostics.MatchesDeferredPublication(session, digest, wire, status,
        session, "digest-2", wire, status), "an old selection digest was accepted");
    Assert(!LordDataSyncDiagnostics.MatchesDeferredPublication(session, digest, wire, status,
        session, digest, "wire-2", status), "a replaced Modsetting was accepted");
    Assert(!LordDataSyncDiagnostics.MatchesDeferredPublication(session, digest, wire, status,
        session, digest, wire, "ERROR|APPLY"), "an unapplied Lord confirmation was accepted");
}

static void TestLordPackageFingerprint()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataLordFingerprint-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        File.WriteAllText(Path.Combine(root, "example.lordjson"), "{}");
        File.WriteAllText(Path.Combine(root, "example1.aivjson"), "{}");
        File.WriteAllText(Path.Combine(root, "info.json"), "{\"NetworkMode\":0}");
        File.WriteAllText(Path.Combine(root, "example.modlord.json"), "{}");
        Directory.CreateDirectory(Path.Combine(root, "Override", "Fixes"));
        File.WriteAllText(Path.Combine(root, "Override", "Fixes", "preferences.json"), "{}");
        Directory.CreateDirectory(Path.Combine(root, "Override", "fx"));
        string media = Path.Combine(root, "Override", "fx", "speech.wav");
        File.WriteAllText(media, "first sound");
        string rootText = Path.Combine(root, "notes.TXT");
        File.WriteAllText(rootText, "first note");
        Directory.CreateDirectory(Path.Combine(root, "Scripts", "assets"));
        string nestedText = Path.Combine(root, "Scripts", "assets", "readme.txt");
        string readme = Path.Combine(root, "README.md");
        string misplacedMedia = Path.Combine(root, "Scripts", "assets", "portrait.PnG");
        File.WriteAllText(nestedText, "nested note");
        File.WriteAllText(readme, "instructions");
        File.WriteAllText(misplacedMedia, "first image");
        LordPackageFileState baseState = LordPackageFingerprint.Capture(root, "example");
        Assert(baseState.SynchronizedPaths.Contains("example.modlord.json") &&
            baseState.SynchronizedPaths.Contains("Override/Fixes/preferences.json"),
            "Lord mod values were not classified as synchronized values");
        Assert(!baseState.HasUnsupportedGameplayFiles &&
            !baseState.GameplayPaths.Contains("notes.TXT") &&
            !baseState.GameplayPaths.Contains("README.md") &&
            !baseState.GameplayPaths.Contains("Scripts/assets/readme.txt") &&
            !baseState.GameplayPaths.Contains("Scripts/assets/portrait.PnG"),
            "text or media outside conventional paths was classified as gameplay");
        File.WriteAllText(media, "another sound");
        File.WriteAllText(rootText, "another note");
        File.WriteAllText(nestedText, "another nested note");
        File.WriteAllText(readme, "updated instructions");
        File.WriteAllText(misplacedMedia, "another image");
        Assert(LordPackageFingerprint.Capture(root, "example").Digest == baseState.Digest,
            "text or media differences changed the gameplay fingerprint");
        File.WriteAllText(Path.Combine(root, "example.modlord.json"), "{\"test\":{}}");
        File.WriteAllText(Path.Combine(root, "Override", "Fixes", "preferences.json"), "{\"changed\":true}");
        Assert(LordPackageFingerprint.Capture(root, "example").Digest == baseState.Digest,
            "synchronized Lord values changed the required gameplay fingerprint");
        string script = Path.Combine(root, "Scripts", "init.lua");
        File.WriteAllText(script, "first script");
        LordPackageFileState scripted = LordPackageFingerprint.Capture(root, "example");
        Assert(scripted.HasUnsupportedGameplayFiles && scripted.UnsupportedPaths.Contains("Scripts/init.lua") &&
            scripted.Digest != baseState.Digest, "Lua was not recognized as an additional gameplay file");
        File.WriteAllText(script, "changed script");
        Assert(LordPackageFingerprint.Capture(root, "example").Digest != scripted.Digest,
            "changed gameplay content did not change the fingerprint");
        File.WriteAllText(Path.Combine(root, "Override", "Fixes", "future.json"), "{}");
        Assert(LordPackageFingerprint.Capture(root, "example").UnsupportedPaths.Contains("Override/Fixes/future.json"),
            "unsupported Override JSON was not detected");
    }
    finally
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        Assert(Path.GetFullPath(root).StartsWith(temp, StringComparison.OrdinalIgnoreCase),
            "unsafe test cleanup path");
        Directory.Delete(root, true);
    }
}

static void TestLordWorkshopMetadataFingerprint()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataLordControl-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        File.WriteAllText(Path.Combine(root, "example.lordjson"), "{}");
        File.WriteAllText(Path.Combine(root, "example1.aivjson"), "{}");
        LordPackageFileState baseline = LordPackageFingerprint.Capture(root, "example");
        string nested = Path.Combine(root, "Scripts", "metadata");
        Directory.CreateDirectory(nested);
        string[] controls =
        {
            Path.Combine(root, "example.data"),
            Path.Combine(root, "other.LDATA"),
            Path.Combine(nested, "workshop.DaTa"),
            Path.Combine(nested, "config.ldata"),
        };
        foreach (string path in controls)
            File.WriteAllText(path, "host metadata");
        LordPackageFileState withControls = LordPackageFingerprint.Capture(root, "example");
        Assert(withControls.Digest == baseline.Digest &&
            !withControls.HasUnsupportedGameplayFiles &&
            withControls.GameplayPaths.SequenceEqual(baseline.GameplayPaths) &&
            withControls.UnsupportedPaths.Count == 0,
            "Workshop control files changed the Lord gameplay fingerprint or required local files");
        foreach (string path in controls)
            File.WriteAllText(path, "different client metadata");
        Assert(LordPackageFingerprint.Capture(root, "example").Digest == baseline.Digest,
            "different Workshop metadata changed the multiplayer digest");
        foreach (string path in controls)
            File.Delete(path);
        Assert(LordPackageFingerprint.Capture(root, "example").Digest == baseline.Digest,
            "removing Workshop metadata changed the multiplayer digest");

        string script = Path.Combine(nested, "init.lua");
        File.WriteAllText(script, "return true");
        LordPackageFileState withScript = LordPackageFingerprint.Capture(root, "example");
        Assert(withScript.HasUnsupportedGameplayFiles &&
            withScript.UnsupportedPaths.Contains("Scripts/metadata/init.lua") &&
            withScript.Digest != baseline.Digest,
            "gameplay Lua no longer requires local files");
        string json = Path.Combine(nested, "unknown.json");
        File.WriteAllText(json, "{}");
        Assert(LordPackageFingerprint.Capture(root, "example").UnsupportedPaths.Contains("Scripts/metadata/unknown.json"),
            "unknown JSON no longer requires local files");
    }
    finally
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        Assert(Path.GetFullPath(root).StartsWith(temp, StringComparison.OrdinalIgnoreCase),
            "unsafe test cleanup path");
        Directory.Delete(root, true);
    }
}

static void TestTrailLordRequirements()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataTrailLord-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        string mission = Path.Combine(root, "Trail_Mission_01.trail");
        File.WriteAllText(mission, "embedded mission");
        var slot = new TrailLordSlot
        {
            PlayerId = 3,
            LordName = "Author Lord",
            ConfigName = "author",
            ConfigChecksum = "123",
            AivChecksums = new[] { "456", "789" },
            RequiresInstalledPackage = false,
            ModLordJson = "{\"example.mod\":{\"value\":1}}",
        };
        TrailLordRequirements.Create(mission, new[] { slot }).Write(mission);
        TrailLordRequirements read = TrailLordRequirements.Read(mission);
        Assert(read.Slots.Count == 1 && read.Slots[0].ModLordJson == slot.ModLordJson &&
            read.Slots[0].AivChecksums.SequenceEqual(slot.AivChecksums),
            "Trail Lord values or AIV checksums were lost");
        File.AppendAllText(mission, " changed");
        ExpectFailure(() => TrailLordRequirements.Read(mission),
            "Lord requirements were accepted for a different Trail mission");
        string coop = Path.Combine(root, "01.coopmission.json");
        File.WriteAllText(coop, "mission");
        TrailLordRequirements.Create(coop, new[] { slot }).Write(coop);
        Assert(TrailLordRequirements.Read(coop).Slots.Single().PlayerId == 3,
            "Coop Lord requirements did not roundtrip");
    }
    finally
    {
        string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        Assert(Path.GetFullPath(root).StartsWith(temporary, StringComparison.OrdinalIgnoreCase),
            "unsafe test cleanup path");
        Directory.Delete(root, true);
    }
}

static void TestTrailLordSelection()
{
    var embedded = new TrailLordSlot
    {
        PlayerId = 3,
        LordName = "Local Lord",
        ConfigChecksum = "100",
        AivChecksums = new[] { "200", "300" },
    };
    Assert(TrailLordSelectionPolicy.UsesEmbeddedLord(embedded, false, "Local Lord"),
        "the original embedded Lord was treated as replaced");
    Assert(TrailLordSelectionPolicy.UsesEmbeddedLord(embedded, false, "Local Lord",
        "3693412090\\Local Lord"), "a changed configuration or AIV of the same Lord lost its Trail values");
    Assert(TrailLordSelectionPolicy.UsesEmbeddedLord(embedded, false,
        "3693412090\\Local Lord", "3693412090\\Local Lord"),
        "the media provider alias was treated as another Lord");
    Assert(!TrailLordSelectionPolicy.UsesEmbeddedLord(embedded, true, "Local Lord"),
        "a Vanilla Lord retained the old Trail requirement");
    Assert(!TrailLordSelectionPolicy.UsesEmbeddedLord(embedded, false, "Different Lord"),
        "another Custom Lord with identical configuration and AIV checksums retained the Trail values");
    Assert(!TrailLordSelectionPolicy.UsesEmbeddedLord(embedded, false,
        "3693412090\\Local Lord"), "an unverified media alias was accepted as the original Lord");
}

static void TestLordPackageManifest()
{
    LordPackageManifest first = LordPackageManifest.Create("session-a", false, new[]
    {
        new LordPackageSlot { PlayerId = 3, LordType = -1, LordName = "example", ConfigName = "one",
            ConfigChecksum = "123", FileDigest = new string('A', 64), NeedsSnapshot = true },
    });
    LordPackageManifest parsed = LordPackageManifest.Parse(first.WireJson);
    Assert(parsed.Digest == first.Digest && parsed.Slots.Count == 1 && parsed.Slots[0].NeedsSnapshot,
        "manifest did not survive transport");
    LordPackageManifest switched = LordPackageManifest.Create("session-a", true, first.Slots);
    Assert(switched.Digest != first.Digest, "local fallback did not invalidate old acknowledgements");
    Assert(LordPackageManifest.Create("session-b", false, first.Slots).Digest != first.Digest,
        "new lobby did not invalidate old acknowledgements");
    AssertThrows<InvalidDataException>(() => LordPackageManifest.Parse(first.WireJson.Replace("example", "different")),
        "tampered manifest was accepted");
}

static void TestBundledMission()
{
    using Fixture fixture = Fixture.Create();
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(loaded.BundledFiles.Count == 3, "expected map, lord and AIV bundle files");
    Assert(loaded.Definition.Players.Where(player => player.Active).Take(2).Count() == 2, "human slots missing");
    string slotRoot = Path.Combine(fixture.Root, "Assets", "01", "AI-03");
    Directory.CreateDirectory(slotRoot);
    File.Copy(Path.Combine(fixture.Root, "lord.lordjson"), Path.Combine(slotRoot, "lord.lordjson"));
    File.Copy(Path.Combine(fixture.Root, "castle.aivjson"), Path.Combine(slotRoot, "castle.aivjson"));
    File.WriteAllText(Path.Combine(slotRoot, "second.aivjson"), "{}", new UTF8Encoding(false));
    PlayerDefinition ai = loaded.Definition.Players[2];
    ai.Lord.File = "Assets/01/AI-03/lord.lordjson";
    ai.Aivs[0].File = "Assets/01/AI-03/castle.aivjson";
    ai.Aivs.Add(new AivReference { Source = "bundled", File = "Assets/01/AI-03/second.aivjson", Rotation = 90 });
    MissionLoader.WriteAtomic(fixture.JsonPath, loaded.Definition);
    LoadedMission nested = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(nested.BundledFiles.Count == 4 &&
        nested.BundledFiles.All(File.Exists) &&
        nested.Definition.Players[2].Aivs.Select(aiv => Path.GetFileName(aiv.File))
            .SequenceEqual(new[] { "castle.aivjson", "second.aivjson" }),
        "nested Coop Lord and multiple original AIV names did not survive mission loading");
}

static void TestCoopMissionModSettingsSidecar()
{
    using Fixture fixture = Fixture.Create();
    string missionJson = File.ReadAllText(fixture.JsonPath);
    Assert(!missionJson.Contains("modSettings", StringComparison.Ordinal), "mission JSON still embeds modsettings");
    Assert(Path.GetFileName(fixture.SidecarPath) == "01.modtrail.json", "Coop sidecar filename is not canonical");
    Assert(Path.GetFileName(MissionLoader.GetTrailModSettingsPath("Trail_Mission_1.trail")) ==
        "Trail_Mission_1.modtrail.json", "Trail sidecar filename is not canonical");
    Assert(Path.GetFileName(MissionLoader.GetTrailPathFromModSettingsPath("Trail_Mission_1.modtrail.json")) ==
        "Trail_Mission_1.trail", "Trail filename cannot be recovered from its sidecar");

    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(loaded.ModSettingsPath == fixture.SidecarPath, "mission sidecar path was not retained");
    Assert(loaded.Definition.ModSettings.Mods.ContainsKey("StartConditions_Serp"), "mission sidecar was not loaded");

    File.Delete(fixture.SidecarPath);
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(fixture.SidecarPath), "01.modjson"), "legacy");
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
    ModSettingsDefinition document = ModSettingsDefinition.CreateModDefaults();
    document.Mods["StartConditions_Serp"] = new ModSettingsEntry
    {
        PlayerSettings = new[] { "PlayerChoice", "EnableMod", "PlayerChoice" },
        Overrides = new Dictionary<string, object>
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
    Assert(entry.PlayerSettings.SequenceEqual(new[] { "EnableMod", "PlayerChoice" }), "player settings changed");
    Assert((bool)entry.Overrides["Bool"], "bool changed");
    Assert(Convert.ToInt32(entry.Overrides["Int"]) == 42, "int changed");
    Assert(Math.Abs(Convert.ToDouble(entry.Overrides["Double"]) - 1.25) < 0.0001, "double changed");
    Assert((string)entry.Overrides["String"] == "Wood=10\r\nStone=-1", "complex string changed");
    Assert(entry.Overrides["DoubleArray"] is List<object> values &&
        values.Count == 3 && Math.Abs(Convert.ToDouble(values[2]) - 1.25) < 0.0001,
        "double array changed");
}

static void TestWorkshopTrailSidecars()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataTests", Guid.NewGuid().ToString("N"));
    string source = Path.Combine(root, "source");
    string stagingRoot = Path.Combine(root, "staging");
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(stagingRoot);
    try
    {
        File.WriteAllText(Path.Combine(source, "01.trail"), "trail");
        File.WriteAllText(Path.Combine(source, "01.modtrail.json"), "included");
        File.WriteAllText(Path.Combine(source, "orphan.modtrail.json"), "included");
        File.WriteAllText(Path.Combine(source, "01.modjson"), "legacy");
        string nested = Path.Combine(source, "Overrides", "Fixes");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "preferences.JSON"), "nested");
        Assert(WorkshopUploadStaging.TryResetDirectChild(
                stagingRoot,
                "Trail",
                out string destination,
                out string resetError),
            resetError);
        File.WriteAllText(Path.Combine(destination, "01.trail"), "vanilla");

        Assert(WorkshopUploadStaging.TryStageTrailJsonFiles(
                source,
                destination,
                out int copied,
                out string stageError),
            stageError);
        Assert(copied == 3, "unexpected JSON count: " + copied);
        Assert(File.Exists(Path.Combine(destination, "01.modtrail.json")), "matching sidecar was not staged");
        Assert(File.Exists(Path.Combine(destination, "orphan.modtrail.json")), "other JSON was not staged");
        Assert(File.Exists(Path.Combine(destination, "Overrides", "Fixes", "preferences.JSON")), "nested JSON was not staged");
        Assert(!File.Exists(Path.Combine(destination, "01.modjson")), "legacy sidecar was staged");
        Assert(File.ReadAllText(Path.Combine(destination, "01.trail")) == "vanilla", "Vanilla Trail was changed");
        Assert(WorkshopUploadStaging.TryStageTrailJsonFiles(source, destination, out int retryCopied, out string retryError), retryError);
        Assert(retryCopied == 0, "identical retry copied JSON again");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestWorkshopTrailJsonConflict()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataTests", Guid.NewGuid().ToString("N"));
    string source = Path.Combine(root, "source");
    string destination = Path.Combine(root, "staging");
    Directory.CreateDirectory(Path.Combine(source, "nested"));
    Directory.CreateDirectory(Path.Combine(destination, "nested"));
    try
    {
        File.WriteAllText(Path.Combine(source, "nested", "a.json"), "new");
        File.WriteAllText(Path.Combine(source, "nested", "z.json"), "source");
        File.WriteAllText(Path.Combine(destination, "nested", "z.json"), "existing");
        Assert(!WorkshopUploadStaging.TryStageTrailJsonFiles(source, destination, out _, out string error),
            "conflicting JSON was accepted");
        Assert(error.Contains("different JSON destination", StringComparison.OrdinalIgnoreCase), "wrong conflict error");
        Assert(!File.Exists(Path.Combine(destination, "nested", "a.json")), "new JSON survived rollback");
        Assert(File.ReadAllText(Path.Combine(destination, "nested", "z.json")) == "existing", "existing JSON changed");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestWorkshopTrailJsonUnsafeRoots()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataTests", Guid.NewGuid().ToString("N"));
    string source = Path.Combine(root, "source");
    string destination = Path.Combine(source, "staging");
    Directory.CreateDirectory(destination);
    try
    {
        File.WriteAllText(Path.Combine(source, "config.json"), "source");
        Assert(!WorkshopUploadStaging.TryStageTrailJsonFiles(source, destination, out _, out string error),
            "overlapping roots were accepted");
        Assert(error.Contains("overlap", StringComparison.OrdinalIgnoreCase), "wrong overlapping-root error");
        Assert(!File.Exists(Path.Combine(destination, "config.json")), "unsafe staging copied JSON");
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
    Assert(xaml.Contains("IsChecked=\"{Binding IncludeExtendedData, Mode=TwoWay}\"", StringComparison.Ordinal) &&
        !xaml.Contains("CanChangeOption", StringComparison.Ordinal),
        "Workshop modsettings checkbox is not uniformly editable");
    Assert(xaml.Contains("Foreground=\"Black\"", StringComparison.Ordinal) &&
        xaml.Contains("ToolTipService.ShowDuration=\"60000\"", StringComparison.Ordinal) &&
        xaml.Contains("seui:ToolTipResolutionScale.Enabled=\"True\"", StringComparison.Ordinal) &&
        xaml.Contains("FontSize=\"{TemplateBinding FontSize}\"", StringComparison.Ordinal) &&
        xaml.Contains("MaxWidth=\"{TemplateBinding MaxWidth}\"", StringComparison.Ordinal) &&
        xaml.Contains("Padding=\"{TemplateBinding Padding}\"", StringComparison.Ordinal),
        "Workshop checkbox presentation does not match the required tooltip style");
    Assert(viewModel.Contains("WorkshopUpload.IncludeModSettings", StringComparison.Ordinal) &&
        viewModel.Contains("WorkshopUpload.IncludeAdditionalFiles", StringComparison.Ordinal) &&
        viewModel.Contains("includeExtendedData = true", StringComparison.Ordinal) &&
        !viewModel.Contains("coopPackage", StringComparison.Ordinal),
        "Workshop checkbox model does not expose the contextual Trail/Lord choice");
    Assert(coordinator.Contains("UploadCoopTrailPackage(self, selectedRow.trail, uploadOptions.IncludeExtendedData)", StringComparison.Ordinal) &&
        coordinator.Contains("CoopWorkshopPackageStaging.Stage", StringComparison.Ordinal),
        "Coop upload does not honor the unified modsettings choice");
    Assert(coordinator.Contains("ArmUploadDecision(uploadRoot, itemName, uploadOptions.IncludeExtendedData)", StringComparison.Ordinal) &&
        coordinator.Contains("decision?.IncludeExtendedData == true", StringComparison.Ordinal) &&
        coordinator.Contains("requirementsOnly: true", StringComparison.Ordinal) &&
        coordinator.Contains("Action terminalSuccess = WrapTerminalCallback", StringComparison.Ordinal) &&
        coordinator.Contains("Action terminalFailure = WrapTerminalCallback", StringComparison.Ordinal),
        "the upload choice is not retained through Vanilla's recursive retry until a terminal callback");
}

static void TestModSettingsRegistry()
{
    ModSettingsDefinition parsed = ModSettingsJson.ParseObject(
        "{\"schemaVersion\":3,\"mods\":{\"ThirdParty.DynamicMod\":{\"playerSettings\":[\"EnableMod\"],\"overrides\":{\"Value\":7}}}}");
    Assert(parsed.Mods.ContainsKey("ThirdParty.DynamicMod") &&
        parsed.Mods["ThirdParty.DynamicMod"].PlayerSettings.SequenceEqual(new[] { "EnableMod" }) &&
        Convert.ToInt32(parsed.Mods["ThirdParty.DynamicMod"].Overrides["Value"]) == 7,
        "dynamic third-party mod id was not preserved");
}

static void TestMissingModEntry()
{
    ModSettingsDefinition parsed = ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{}}");
    Assert(parsed.Mods.Count == 0, "an absent mod entry is not treated as the mod-defined default");
}

static void TestTrailModCompatibilityContract()
{
    var compatible = new CompatibleTrailSettingsViewModel();
    TrailModCompatibilityResult result = EvaluateCompatibility(compatible);
    Assert(result.IsCompatible, "valid mission-preset contract was rejected: " + result.IncompatibilityReason);
    Assert(result.Properties.Select(property => property.Name).SequenceEqual(new[] { "EnableMod", "Label", "Strength" }),
        "persistent host properties were not selected deterministically or DoNotPersist was included");

    TrailModCompatibilityResult missingApi = EvaluateCompatibility(new MissingMissionApiViewModel());
    Assert(!missingApi.IsCompatible && missingApi.IncompatibilityReason.Contains("typed APIShared", StringComparison.Ordinal),
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
        compatible.System_CreateDisabledMissionPresetSnapshot,
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
        CreateCompatibilitySnapshotFactory(viewModel),
        (property, value) => EncodeCompatibilityValue(property.PropertyType, value),
        DecodeCompatibilityValue);

static Func<Dictionary<string, byte[]>> CreateCompatibilitySnapshotFactory(object viewModel)
{
    IModSettingsPresetEndpoint endpoint = viewModel as IModSettingsPresetEndpoint;
    return endpoint == null
        ? null
        : () => endpoint.System_CreateDisabledMissionPresetSnapshot();
}

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
        "{\"schemaVersion\":3,\"mods\":{\"ExtraFeatures_Serp\":{\"playerSettings\":[\"EnableMod\",\"RemovedPlayerSetting\"],\"overrides\":{\"CurrentSetting\":7,\"RemovedSetting\":99}}}}");
    string[] removed = ModSettingsJson.RemoveUnknownSettings(
        parsed,
        "ExtraFeatures_Serp",
        new[] { "EnableMod", "CurrentSetting", "NewSetting" });

    Assert(removed.SequenceEqual(new[] { "RemovedPlayerSetting", "RemovedSetting" }), "obsolete sidecar settings were not identified");
    Assert(parsed.Mods["ExtraFeatures_Serp"].PlayerSettings.SequenceEqual(new[] { "EnableMod" }), "current player setting was removed");
    Assert(parsed.Mods["ExtraFeatures_Serp"].Overrides.ContainsKey("CurrentSetting"), "current sidecar setting was removed");
    Assert(!parsed.Mods["ExtraFeatures_Serp"].Overrides.ContainsKey("NewSetting"), "missing new setting was fabricated instead of inheriting the host value");
    string serialized = ModSettingsJson.Serialize(parsed);
    Assert(!serialized.Contains("RemovedSetting", StringComparison.Ordinal), "obsolete sidecar setting was written again");

    ModSettingsDefinition activation = ModSettingsJson.ParseObject(
        "{\"schemaVersion\":3,\"mods\":{\"ExtraFeatures_Serp\":{\"playerSettings\":[],\"overrides\":{\"EnableMod\":false}}}}");
    string[] activationRemoved = ModSettingsJson.RemoveUnknownSettings(
        activation,
        "ExtraFeatures_Serp",
        new[] { "EnableMod", "CurrentSetting" });
    Assert(activationRemoved.Length == 0 &&
        activation.Mods.TryGetValue("ExtraFeatures_Serp", out ModSettingsEntry activationEntry) &&
        activationEntry.Overrides.TryGetValue("EnableMod", out object enabled) &&
        enabled is bool enabledValue && !enabledValue,
        "EnableMod override was incorrectly removed as obsolete");
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
    Assert(Convert.ToInt32(entry.Overrides["SetStartGoldHuman"]) == 500, "current coop mission setting changed");
    Assert(!entry.Overrides.ContainsKey("NewSetting"), "missing coop mission setting was fabricated instead of inheriting the host value");
}

static void TestInvalidModSettingsDocuments()
{
    ExpectFailure(() => ModSettingsJson.ParseObject("broken"), "corrupt JSON was accepted");
    ExpectFailure(() => ModSettingsJson.ParseObject("{\"schemaVersion\":1,\"mods\":{}}"), "schema 1 sidecar was accepted");
    ExpectFailure(() => ModSettingsJson.ParseObject("{\"schemaVersion\":2,\"mods\":{}}"), "schema 2 sidecar was accepted");
    ExpectFailure(
        () => ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{\"Broken.Mod\":{\"overrides\":{}}}}"),
        "mod entry without playerSettings was accepted");
    ExpectFailure(
        () => ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{\"\":{\"playerSettings\":[],\"overrides\":{\"Value\":1}}}}"),
        "empty mod id was accepted");
    ExpectFailure(
        () => ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{\"UnitLimit_Serp\":{\"playerSettings\":[],\"overrides\":{\"Limit\":{\"bad\":1}}}}}"),
        "object setting was accepted");
    ExpectFailure(
        () => ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{\"UnitLimit_Serp\":{\"playerSettings\":[\"Limit\"],\"overrides\":{\"Limit\":1000}}}}"),
        "setting selected as both player and fixed was accepted");
    ExpectFailure(
        () => ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{\"UnitLimit_Serp\":{\"playerSettings\":[1],\"overrides\":{}}}}"),
        "non-string player setting was accepted");
    ExpectFailure(
        () => ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{\"UnitLimit_Serp\":{\"playerSettings\":[\" \"],\"overrides\":{}}}}"),
        "blank player setting was accepted");
    ExpectFailure(
        () => ModSettingsJson.Serialize(new ModSettingsDefinition { SchemaVersion = 1 }),
        "non-current schema was silently serialized as schema 3");
}

static void TestAtomicSidecarWrite()
{
    string root = Path.Combine(Path.GetTempPath(), "ExtendedDataSidecarTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        string path = Path.Combine(root, "Trail_Mission_1.modtrail.json");
        File.WriteAllText(path, "old");
        ModSettingsDefinition document = ModSettingsDefinition.CreateModDefaults();
        document.Mods["UnitLimit_Serp"] = new ModSettingsEntry
        {
            Overrides = new Dictionary<string, object> { ["UnitLimit"] = 1000 },
        };
        ModSettingsJson.WriteAtomic(path, document);
        Assert(Convert.ToInt32(ModSettingsJson.Read(path).Mods["UnitLimit_Serp"].Overrides["UnitLimit"]) == 1000, "replacement was not readable");
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
    string sharedPresetSystem = File.ReadAllText(Path.Combine(workspaceRoot, "APIShared", "src", "PresetLobbyModSettingsViewModel.cs"));
    string sharedGameMode = File.ReadAllText(Path.Combine(workspaceRoot, "APIShared", "src", "MissionModePolicy.cs"));
    Assert(CountOccurrences(coordinator, "InjectCoopCustomizeButton(pages[index]);") == 1,
        "Coop Trail button registration is not centralized and singular");
    Assert(CountOccurrences(coordinator, "nameof(Platform_Workshop.UploadWorkshopMap)") == 1 &&
        CountOccurrences(coordinator, "typeof(FRONT_EditorSetup).GetMethod") == 1 &&
        !File.Exists(Path.Combine(projectRoot, "src", "LordUpload", "CustomLordUploadHook.cs")),
        "Workshop uploads are not owned by exactly one editor hook and one upload hook");
    Assert(coordinator.Contains("Margin = new Thickness(0, 0, 0, -30)"),
        "Coop Customize is not positioned below the Vanilla kick button");
    string runtime = File.ReadAllText(Path.Combine(projectRoot, "src", "ExtendedDataRuntime.cs"));
    string obsoleteBridgeName = "TrailModSettings" + "Bridge";
    Assert(!runtime.Contains("SerializeModSettings") && !runtime.Contains(obsoleteBridgeName), "Coop settings still use the old bridge roundtrip");
    Assert(runtime.Contains("missionSettingsCoordinator?.ExitContext(force: true)"), "map unload does not leave the mission preset context");
    Assert(coordinator.Contains("document = CaptureDocument();") &&
        !coordinator.Contains("requireLoadedEndpoints") &&
        coordinator.Contains("TrailModCompatibilityContract.Evaluate"),
        "Trail saves do not use validated synchronous settings capture");
    Assert(coordinator.Contains("System_CreateDisabledMissionPresetSnapshot") &&
        coordinator.Contains("preserveCurrentValues ? CaptureCurrentSnapshots(allParticipants) : null") &&
        coordinator.Contains("? Array.Empty<string>()") &&
        coordinator.Contains(": entry.PlayerSettings)") &&
        coordinator.Contains("Fixed Trail values have final precedence") &&
        coordinator.Contains("RemoveUnknownSettings"),
        "Trail loading does not preserve editor values while retaining playable Trail precedence and schema cleanup");
    int defaultLayer = coordinator.IndexOf("System_CreateDisabledMissionPresetSnapshot", StringComparison.Ordinal);
    int playerLayer = coordinator.IndexOf(": entry.PlayerSettings)", defaultLayer, StringComparison.Ordinal);
    int fixedLayer = coordinator.IndexOf("foreach (KeyValuePair<string, object> setting in entry.Overrides)", playerLayer, StringComparison.Ordinal);
    Assert(defaultLayer >= 0 && playerLayer > defaultLayer && fixedLayer > playerLayer &&
        coordinator.Contains("foreach (KeyValuePair<string, IModSettingsPresetEndpoint> participant in allParticipants)"),
        "Trail setting source precedence or the all-participant baseline changed");
    Assert(sharedPresetSystem.Contains("CopyProperties(defaults, hostProperties)") &&
        sharedPresetSystem.Contains("defaults.TryGetValue(property.Name, out bytes)"),
        "the shared mission preset no longer supplies defaults for missing current host settings");
    Assert(sharedPresetSystem.Contains("class PerPlayerLobbySettingsCoordinator") &&
        sharedPresetSystem.Contains("FinalizeRosterForMapTransition") &&
        sharedPresetSystem.Contains("TryGetLobbyState") &&
        sharedPresetSystem.Contains("API_SHARED_LOBBY_OBSERVER") &&
        !sharedPresetSystem.Contains("Application.onBeforeRender") &&
        !sharedPresetSystem.Contains("PerPlayerIdentityHookAnchor") &&
        !sharedPresetSystem.Contains("ScriptExtenderMultiplayerSyncWorkaround") &&
        !sharedPresetSystem.Contains("EnsureInstalled"),
        "shared per-player convergence does not exclusively consume the process-wide APIShared observer");
    Assert(sharedGameMode.Contains("bool realMultiplayer = realMultiplayerOverride;") &&
        sharedGameMode.Contains("APIShared.MissionLifecycleService.Snapshot") &&
        !sharedGameMode.Contains("multiplayerSave ||"),
        "mission network identity must come from the common operation, without stale roster fallback");
    Assert(!coordinator.Contains("pendingTrailMakerSaveDocument"),
        "Trail saves still retain a snapshot for the next save operation");
    Assert(coordinator.Contains("!openingCustomTrailSetup") &&
        coordinator.Contains("openingCustomTrailSetup = true") &&
        coordinator.Contains("openingCustomTrailSetup = false"),
        "Custom Trail setup does not suppress transient selection-sidecar loads");
    Assert(!coordinator.Contains("all Trail mods will be disabled"),
        "capture failures can still overwrite a sidecar with an all-disabled fallback");
}

static void TestTrailMakerAuthoringSessionIntegration()
{
    string projectRoot = FindProjectRoot();
    string coordinator = File.ReadAllText(Path.Combine(projectRoot, "src", "TrailMissionSettingsCoordinator.cs"));
    string runtime = File.ReadAllText(Path.Combine(projectRoot, "src", "ExtendedDataRuntime.cs"));

    Assert(coordinator.Contains("private ModSettingsDefinition trailMakerWorkingDocument;") &&
        coordinator.Contains("private string pendingTrailMakerTrailPath;") &&
        coordinator.Contains("private bool pendingTrailMakerLoad;") &&
        coordinator.Contains("private bool trailMakerAuthoringActive;") &&
        coordinator.Contains("MissionPresetLifecycleState missionPresetLifecycle") &&
        !coordinator.Contains("trailMakerTransitionPending") &&
        !coordinator.Contains("trailMakerResumePending"),
        "Trail Maker authoring state is incomplete");
    Assert(coordinator.Contains("pendingTrailMakerTrailPath = loadedHeader?.filePath;") &&
        coordinator.Contains("ActivateTrailMakerLobby(restartInfo);") &&
        coordinator.Contains("EnterSidecar(requestedTrailPath, editable: true)"),
        "loaded Trail Maker missions are not activated from their selected sidecar exactly at lobby entry");
    Assert(coordinator.Contains("trailMakerWorkingDocument ??") &&
        coordinator.Contains("ModSettingsDefinition.CreateModDefaults();") &&
        coordinator.Contains("startingSnapshots: trailMakerWorkingSnapshots") &&
        coordinator.Contains("preserveCurrentValues: editable") &&
        coordinator.Contains("preserveCurrentValues: true") &&
        coordinator.Contains("trailMakerWorkingSnapshots = CaptureCurrentSnapshots(FindCompatibleViewModels());") &&
        coordinator.Contains("snapshot[property.Name] = MessagePackSerializer.Serialize(") &&
        coordinator.Contains("property.PropertyType, property.GetValue(participant.Value)") &&
        coordinator.Contains("\"new mission defaults\""),
        "Trail Maker authoring does not preserve current settings across new missions, sidecar loads, and test returns");

    int startIndex = coordinator.IndexOf("private void StartSkirmishGameHook", StringComparison.Ordinal);
    int frontendOpenIndex = coordinator.IndexOf("private void FrontendOpenCustomTrailHook", startIndex, StringComparison.Ordinal);
    Assert(startIndex >= 0 && frontendOpenIndex > startIndex, "Trail Maker launch hook could not be isolated");
    string startHook = coordinator.Substring(startIndex, frontendOpenIndex - startIndex);
    int vanillaStartIndex = startHook.LastIndexOf("startSkirmishGameOriginal(self, customTrailRestartInfo);", StringComparison.Ordinal);
    Assert(startHook.Contains("self?.trailMakerMode == true") &&
        startHook.Contains("PrepareTrailMakerTestLaunch()") && vanillaStartIndex >= 0 &&
        coordinator.Contains("CaptureTrailMakerWorkingDocument(\"test launch\")") &&
        coordinator.Contains("MissionPresetLaunchKind.TrailMakerTest") &&
        coordinator.Contains("ApplyDocument("),
        "the editable Trail Maker draft is not captured before Vanilla starts a test mission");
    Assert(runtime.Contains("string.Equals(command, \"TMTest\"") &&
        runtime.Contains("PrepareTrailMakerTestLaunch()"),
        "Trail Maker test launch is not armed before the lifecycle replacement can fire");

    Assert(runtime.Contains("MissionEvents.Ended.Subscribe(OnMissionEnded)") &&
        runtime.Contains("missionSettingsCoordinator?.HandleMissionEnded(notification)") &&
        coordinator.Contains("Suspended the Trail Maker mission preset") &&
        coordinator.Contains("restartInfo?.customTestMission == true") &&
        coordinator.Contains("\"test return draft\""),
        "mission end and test return do not preserve and reactivate the Trail Maker draft");
    Assert(coordinator.Contains("UpdateTrailMakerWorkingDocument(document, trailPath);") &&
        coordinator.Contains("Saved Trail mod settings") &&
        coordinator.Contains("ClearTrailMakerAuthoringState();") &&
        coordinator.Contains("if (!trailMaker)") &&
        coordinator.Contains("ExitContext(force: true);"),
        "saving does not refresh the draft or genuine context exits do not discard it");
    Assert(coordinator.Contains("applying editable defaults") &&
        coordinator.Contains("fail-closed defaults") &&
        coordinator.Contains("Could not fully clean up the failed Trail Maker context"),
        "Trail Maker restoration failures are not fail-closed and diagnosable");
}

static void TestMissionPresetLifecycleState()
{
    var state = new MissionPresetLifecycleState();

    state.Prepare(MissionPresetLaunchKind.CustomTrail);
    Assert(state.End(MissionPresetEndKind.Replaced) == MissionPresetEndAction.Preserve,
        "a prepared direct Custom Trail was not preserved across replacement");
    Assert(state.ConfirmStarted(MissionPresetLaunchKind.CustomTrail),
        "the direct Custom Trail did not become active");
    Assert(state.End(MissionPresetEndKind.SceneChanged) == MissionPresetEndAction.Exit,
        "a genuinely exited Custom Trail retained its preset");

    state.Prepare(MissionPresetLaunchKind.TrailMakerTest);
    Assert(state.End(MissionPresetEndKind.Replaced) == MissionPresetEndAction.Preserve,
        "the initial Trail Maker test replacement was not preserved");
    Assert(state.ConfirmStarted(MissionPresetLaunchKind.TrailMakerTest),
        "the Trail Maker test did not become active");
    Assert(state.End(MissionPresetEndKind.Replaced) == MissionPresetEndAction.SuspendTrailMaker &&
        state.AwaitingTrailMakerReturn,
        "a Trail Maker test restart did not suspend its authoring draft");
    state.Prepare(MissionPresetLaunchKind.TrailMakerTest);
    Assert(state.End(MissionPresetEndKind.Replaced) == MissionPresetEndAction.Preserve &&
        state.ConfirmStarted(MissionPresetLaunchKind.TrailMakerTest),
        "the restarted Trail Maker test did not reactivate its preset");
    Assert(state.End(MissionPresetEndKind.Unloaded) == MissionPresetEndAction.SuspendTrailMaker &&
        state.AwaitingTrailMakerReturn,
        "the Trail Maker draft was not retained for the authoring return");
    state.CompleteTrailMakerReturn();
    Assert(!state.AwaitingTrailMakerReturn,
        "the Trail Maker return remained pending after lobby activation");

    state.Prepare(MissionPresetLaunchKind.CoopTrail);
    Assert(state.End(MissionPresetEndKind.Replaced) == MissionPresetEndAction.Preserve &&
        state.ConfirmStarted(MissionPresetLaunchKind.CoopTrail),
        "the initial Coop launch did not retain its preset");
    Assert(state.End(MissionPresetEndKind.Replaced) == MissionPresetEndAction.Exit,
        "an unprepared Coop restart incorrectly retained stale settings");
    state.Prepare(MissionPresetLaunchKind.CoopTrail);
    Assert(state.ConfirmStarted(MissionPresetLaunchKind.CoopTrail),
        "the revalidated Coop restart did not become active");
    Assert(state.End(MissionPresetEndKind.Failed) == MissionPresetEndAction.Exit,
        "a failed Coop restart retained its preset");
}

static void TestCustomizedLaunchOriginIntegration()
{
    string projectRoot = FindProjectRoot();
    string workspaceRoot = Directory.GetParent(projectRoot)?.FullName ??
        throw new InvalidOperationException("workspace root missing");
    string api = File.ReadAllText(Path.Combine(projectRoot, "src", "ExtendedDataLaunchOriginApi.cs"));
    string coordinator = File.ReadAllText(Path.Combine(projectRoot, "src", "TrailMissionSettingsCoordinator.cs"));
    string runtime = File.ReadAllText(Path.Combine(projectRoot, "src", "ExtendedDataRuntime.cs"));
    string originPacket = File.ReadAllText(Path.Combine(projectRoot, "src", "BuiltInCustomizeOriginPacket.cs"));
    string sharedGameMode = File.ReadAllText(Path.Combine(workspaceRoot, "APIShared", "src", "MissionModePolicy.cs"));
    string project = File.ReadAllText(Path.Combine(projectRoot, "ExtendedData.csproj"));

    Assert(project.Contains("ExtendedDataLaunchOriginApi.cs") &&
        project.Contains("BuiltInCustomizeOriginPacket.cs"),
        "the runtime project does not compile the public origin API");
    Assert(api.Contains("public static class ExtendedDataLaunchOriginApi") &&
        api.Contains("public static ExtendedDataLaunchOriginKind Origin") &&
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
        runtime.Contains("ExtendedDataLaunchOriginApi.MarkMapStarted()"),
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
    Assert(coordinator.Contains("fromNew && skirmishSetup && restartInfo == null && !coopSetup && !trailMaker") &&
        multiplayerOpenHook.IndexOf("multiplayerOpenOriginal(", StringComparison.Ordinal) <
            multiplayerOpenHook.IndexOf("CaptureBuiltInCustomizeOrigin(customiseTrailType, customiseTrailId)", StringComparison.Ordinal) &&
        multiplayerOpenHook.IndexOf("CaptureBuiltInCustomizeOrigin(customiseTrailType, customiseTrailId)", StringComparison.Ordinal) <
            multiplayerOpenHook.IndexOf("if (!enabled)", StringComparison.Ordinal) &&
        coordinator.Contains("FRONT_Multiplayer.customizedTrail") &&
        coordinator.Contains("FRONT_Multiplayer.customizedTrailType == trailType") &&
        coordinator.Contains("FRONT_Multiplayer.customizedTrailID == missionId") &&
        coordinator.Contains("ReferenceEquals(Platform_Multiplayer.Instance?.activeLobby, self.currentLobby)") &&
        coordinator.Contains("MainViewModel.Instance?.Show_MultiplayerSetup == true") &&
        coordinator.Split(new[] { "CaptureBuiltInCustomizeOrigin(" }, StringSplitOptions.None).Length == 4,
        "built-in Customize origin is not captured independently after a confirmed doOpen transition");
    Assert(coordinator.Contains("IsBuiltInTrailOpenCommand(command)") &&
        coordinator.Contains("string.Equals(command, \"Trail\"") &&
        coordinator.Contains("string.Equals(command, \"Sands8\"") &&
        coordinator.Contains("BroadcastBuiltInCustomizeOrigin(self)") &&
        coordinator.Contains("GetPacketEventFor<BuiltInCustomizeOriginPacket>") &&
        coordinator.Contains("OnBuiltInCustomizeOriginPacket") &&
        coordinator.Contains("senderSteamId != host.Value") &&
        coordinator.Contains("ProcessBuiltInCustomizeOriginPacket(packet, new CSteamID(senderSteamId))") &&
        coordinator.Contains("GameNetworkAPI.SendPacketToAllLobby") &&
        originPacket.Contains("IMessagePackFormatter<BuiltInCustomizeOriginPacket>"),
        "Built-in Customize back-navigation cleanup or authenticated host/client origin synchronization is missing");
    Assert(coordinator.Contains("if (!preserveContextForLaunch)") &&
        coordinator.Contains("restartInfo.customTrailLevel == missionId") &&
        coordinator.Contains("ExtendedDataLaunchOriginApi.MarkRestartPending()") &&
        coordinator.Contains("ExtendedDataLaunchOriginApi.Clear()"),
        "direct follow-up and restarted Custom Trails do not separate stale from active origin");
    int customTrailHookIndex = coordinator.IndexOf("private void StartCustomTrailHook", StringComparison.Ordinal);
    int multiplayerOpenIndex = coordinator.IndexOf("private void MultiplayerOpenHook", StringComparison.Ordinal);
    Assert(customTrailHookIndex >= 0 && multiplayerOpenIndex > customTrailHookIndex,
        "the direct Custom Trail hook could not be isolated");
    string customTrailHook = coordinator.Substring(
        customTrailHookIndex,
        multiplayerOpenIndex - customTrailHookIndex);
    int validSidecarIndex = customTrailHook.IndexOf("bool validSidecar = EnterSidecar", StringComparison.Ordinal);
    int directOriginIndex = customTrailHook.IndexOf(
        "ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(",
        validSidecarIndex,
        StringComparison.Ordinal);
    int vanillaStartIndex = customTrailHook.LastIndexOf(
        "startCustomTrailOriginal(self, trailName, missionId, difficulty);",
        StringComparison.Ordinal);
    Assert(validSidecarIndex >= 0 &&
        customTrailHook.Contains("ResolveCustomTrailHeader(trailName, missionId)") &&
        customTrailHook.Contains("EnterSidecar(header.filePath, editable: false)") &&
        customTrailHook.Contains("if (validSidecar && !customizedRestart)") &&
        customTrailHook.Contains("FrontendMenus.CurrentSelectedTrail") &&
        directOriginIndex > validSidecarIndex &&
        vanillaStartIndex > directOriginIndex,
        "a verified direct Custom Trail sidecar does not establish its origin before Vanilla starts");
    int directCatchIndex = customTrailHook.IndexOf("catch (Exception exception)", validSidecarIndex, StringComparison.Ordinal);
    int failClosedClearIndex = customTrailHook.IndexOf(
        "ExtendedDataLaunchOriginApi.Clear();",
        directCatchIndex,
        StringComparison.Ordinal);
    int failClosedDefaultsIndex = customTrailHook.IndexOf(
        "ApplyDocument(ModSettingsDefinition.CreateModDefaults(), editable: false);",
        directCatchIndex,
        StringComparison.Ordinal);
    Assert(directCatchIndex > validSidecarIndex &&
        failClosedClearIndex > directCatchIndex &&
        failClosedDefaultsIndex > failClosedClearIndex,
        "an invalid direct Custom Trail sidecar does not clear its origin before applying defaults");
    Assert(coordinator.Contains("private static FileHeader ResolveCustomTrailHeader") &&
        coordinator.Contains("missionId <= 0") &&
        coordinator.Contains("IOPath.GetExtension(trailPath), \".trail\"") &&
        coordinator.Contains("!File.Exists(trailPath)"),
        "direct Custom Trail starts do not validate their name, 1-based mission number, and .trail path");
    int enterSidecarIndex = coordinator.IndexOf("private bool EnterSidecar", StringComparison.Ordinal);
    int captureDocumentIndex = coordinator.IndexOf("private ModSettingsDefinition CaptureDocument", StringComparison.Ordinal);
    Assert(enterSidecarIndex >= 0 && captureDocumentIndex > enterSidecarIndex,
        "the sidecar loader could not be isolated");
    string enterSidecar = coordinator.Substring(
        enterSidecarIndex,
        captureDocumentIndex - enterSidecarIndex);
    Assert(enterSidecar.Contains("bool exists = File.Exists(sidecar);") &&
        enterSidecar.Split(new[] { "return exists;" }, StringSplitOptions.None).Length == 3,
        "fresh and cached sidecar loads do not report the same validated existence status");
    Assert(sharedGameMode.Contains("BugfixesAndQoL.TrailCustomizationLaunchOriginApi, BugfixesAndQoL") &&
        sharedGameMode.Contains("ExtendedData.ExtendedDataLaunchOriginApi, ExtendedData") &&
        sharedGameMode.Contains("if (hasActive)") &&
        sharedGameMode.Contains("candidate.IsInvalid") &&
        sharedGameMode.Contains("bool hasLaunchPending = TryReadStaticBool") &&
        sharedGameMode.Contains("ExternalOriginMatchesKind") &&
        sharedGameMode.Contains("RestoredCustomizedSave"),
        "GameModeHelper does not select exactly one valid launch-origin provider fail-closed");
}

static void TestBuiltInCustomizeOriginPacketRoundtrip()
{
    var expected = new ExtendedData.BuiltInCustomizeOriginPacket
    {
        ProtocolVersion = ExtendedData.BuiltInCustomizeOriginPacket.CurrentProtocolVersion,
        TrailType = ExtendedData.ExtendedDataLaunchOriginApi.LastSandsOfTimeTrailType,
        MissionId = 4,
    };
    byte[] bytes = MessagePack.MessagePackSerializer.Serialize(expected);
    ExtendedData.BuiltInCustomizeOriginPacket actual =
        MessagePack.MessagePackSerializer.Deserialize<ExtendedData.BuiltInCustomizeOriginPacket>(bytes);
    Assert(actual != null && actual.ProtocolVersion == expected.ProtocolVersion &&
        actual.TrailType == expected.TrailType && actual.MissionId == expected.MissionId,
        "Built-in Customize origin packet changed during MessagePack roundtrip");
    Assert(ExtendedData.BuiltInCustomizeOriginPacket.IsValid(actual),
        "valid Built-in Customize origin packet was rejected");
    actual.ProtocolVersion++;
    Assert(!ExtendedData.BuiltInCustomizeOriginPacket.IsValid(actual),
        "mismatched Built-in Customize protocol was accepted");
    actual.ProtocolVersion = expected.ProtocolVersion;
    actual.TrailType = 10;
    Assert(!ExtendedData.BuiltInCustomizeOriginPacket.IsValid(actual),
        "invalid Built-in Customize Trail type was accepted");
    actual.TrailType = expected.TrailType;
    actual.MissionId = -1;
    Assert(!ExtendedData.BuiltInCustomizeOriginPacket.IsValid(actual),
        "invalid Built-in Customize mission index was accepted");
    byte[] truncatedBytes = MessagePack.MessagePackSerializer.Serialize(new[]
    {
        ExtendedData.BuiltInCustomizeOriginPacket.CurrentProtocolVersion,
    });
    ExtendedData.BuiltInCustomizeOriginPacket truncated =
        MessagePack.MessagePackSerializer.Deserialize<ExtendedData.BuiltInCustomizeOriginPacket>(truncatedBytes);
    Assert(truncated == null, "truncated Built-in Customize origin packet was accepted");
}

static void TestMapModSettingsPacketRoundtrip()
{
    var expected = new MapModSettingsPacket
    {
        ProtocolVersion = MapModSettingsPacket.CurrentProtocolVersion,
        Apply = true,
        MapFileName = "Workshop_Test.map",
        MapCrc = 0xDEADBEEFu,
        Json = ModSettingsJson.Serialize(ModSettingsDefinition.CreateModDefaults()),
    };
    byte[] bytes = MessagePack.MessagePackSerializer.Serialize(expected);
    MapModSettingsPacket actual = MessagePack.MessagePackSerializer.Deserialize<MapModSettingsPacket>(bytes);
    Assert(actual != null && actual.ProtocolVersion == expected.ProtocolVersion &&
        actual.Apply == expected.Apply && actual.MapFileName == expected.MapFileName &&
        actual.MapCrc == expected.MapCrc && actual.Json == expected.Json,
        "Map mod-settings packet changed during MessagePack roundtrip");
}

static void TestMapModSettingsRuntimeIntegration()
{
    string projectRoot = FindProjectRoot();
    string coordinator = File.ReadAllText(Path.Combine(projectRoot, "src", "MapModSettingsCoordinator.cs"));
    string trailCoordinator = File.ReadAllText(Path.Combine(projectRoot, "src", "TrailMissionSettingsCoordinator.cs"));
    string runtime = File.ReadAllText(Path.Combine(projectRoot, "src", "ExtendedDataRuntime.cs"));
    string oldMapButtonXaml = Path.Combine(projectRoot, "Patches", "Assets", "GUI", "XAMLResources", "FRONT_Multiplayer.xaml");

    Assert(coordinator.Contains("SaveDataIdentifier = \"ExtendedData-MapModSettings\"") &&
        coordinator.Contains("context.IsMapEditorSave") && coordinator.Contains("context.IsSaveFile") &&
        coordinator.Contains("pendingMapSavePayload == null") && coordinator.Contains("return null;"),
        "Map archive capture is not restricted to successful editor map captures");
    Assert(coordinator.Contains("new UTF8Encoding(false, true)") &&
        coordinator.Contains("MapArchive.TryLoad(header.filePath") &&
        coordinator.Contains("ModSettingsJson.ParseObject") &&
        coordinator.Contains("EnterStrict("),
        "Map archive loading is not strict and transactional");
    Assert(coordinator.Contains("GetHostSteamId") &&
        coordinator.Contains("sender != host.Value") &&
        coordinator.Contains("MatchesLobby(packet, lobby)") &&
        coordinator.Contains("ProtocolVersion != MapModSettingsPacket.CurrentProtocolVersion"),
        "Map packets are not authenticated and bound to the selected map");
    Assert(coordinator.Contains("mapList.SelectionChanged += OnMapListSelectionChanged") &&
        coordinator.Contains("selected != null && !MatchesActiveMap(selected)") &&
        coordinator.Contains("LeaveLobbyHook") && coordinator.Contains("StartSkirmishGameHook") &&
        coordinator.Contains("!launchInProgress && !mapMissionActive") &&
        coordinator.Contains("MissionEvents.Ended") &&
        coordinator.Contains("BroadcastCurrentState(apply: true)"),
        "Map preset cleanup or late-join convergence is incomplete");
    Assert(coordinator.Contains("!MatchesLobby(packet, lobby) && !MatchesActiveContext(packet)") &&
        coordinator.Contains("UnregisterModDataHandler(SaveDataIdentifier)") &&
        coordinator.Contains("payload.Length > MaxPayloadBytes"),
        "Map clear ordering, initialization rollback, or capture size validation is incomplete");
    int mapContractResolution = coordinator.IndexOf("MethodInfo saveMethod = RequireInstanceMethod(", StringComparison.Ordinal);
    int mapHandlerRegistration = coordinator.IndexOf("RegisterModDataHandler(", StringComparison.Ordinal);
    Assert(mapContractResolution >= 0 && mapContractResolution < mapHandlerRegistration &&
        coordinator.Contains("BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic") &&
        coordinator.Contains("\"LeaveLobby\",\r\n                typeof(bool), typeof(bool)") &&
        coordinator.Contains("\"StartSkirmishGame\",\r\n                typeof(HUD_IngameMenu.RestartSkirmishMapInfo)") &&
        !coordinator.Contains("BindingFlags.Instance | BindingFlags.Public,\r\n                null,\r\n                new[] { typeof(bool), typeof(bool) }"),
        "Map hook contracts are not resolved exactly before initialization side effects");
    Assert(runtime.Contains("RequireMethod(\"UpdateHostInfo\", typeof(bool))") &&
        runtime.Contains("UpdateHostInfoMethod.Invoke(self, new object[] { false })") &&
        !runtime.Contains("UpdateHostInfoMethod?.Invoke"),
        "UpdateHostInfo(bool) is not treated as a required exact runtime contract");
    Assert(trailCoordinator.Contains("EnterStrict") &&
        trailCoordinator.Contains("internal ModSettingsDefinition ValidateStrict") &&
        trailCoordinator.Contains("ValidateDocumentValues") &&
        trailCoordinator.Contains("sourceEndpoint.System_SetExplicitMissionSettings(item.Item4)") &&
        trailCoordinator.Contains("item.Item2.System_EnterMissionPreset(item.Item3, presetLabel, editable)"),
        "the shared Trail/Map preset service does not validate or expose contextual labels");
    Assert(!runtime.Contains("mapSettingsCoordinator?.TryHandleCommand") &&
        runtime.Contains("ActivateSelectedMissionSettingsUnlessMap") &&
        runtime.Contains("mapSettingsCoordinator?.IsActiveForLobby(lobby) == true") &&
        coordinator.Contains("settingsCoordinator.ValidateStrict(document, \"embedded Map\")") &&
        coordinator.Contains("SetMapSourceDocument(document, selected.filePath)") &&
        coordinator.Contains("SetMapSourceDocument(document, packet.MapFileName + \":\" + packet.MapCrc)") &&
        coordinator.Contains("editable: true") &&
        coordinator.Contains("CaptureCurrentDocument()") &&
        !File.Exists(oldMapButtonXaml) &&
        !coordinator.Contains("ExtendedDataUseMapModSettings"),
        "Map settings must use the common source selector, remain editable in Customize, and lock the materialized copy at launch");
    string settingsXaml = File.ReadAllText(Path.Combine(projectRoot, "Override", "ScriptExtenderUI", "ExtendedDataSettings.xaml"));
    Assert(settingsXaml.Contains("TextWrapping=\"Wrap\"\r\n                 Width=\"580\"") &&
        settingsXaml.Contains("Width=\"580\" HorizontalAlignment=\"Left\"") &&
        settingsXaml.Contains("<ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"175\"/><ColumnDefinition Width=\"28\"/>") &&
        settingsXaml.Contains("<ComboBox Grid.Column=\"1\"") &&
        settingsXaml.Contains("<ToggleButton Grid.Column=\"2\"") &&
        settingsXaml.Contains("Width=\"530\" HorizontalAlignment=\"Left\"") &&
        settingsXaml.Contains("HorizontalScrollBarVisibility=\"Auto\"") &&
        !settingsXaml.Contains("Width=\"723\""),
        "Map/Trail controls should fit normally while allowing long names to scroll horizontally");
    Assert(!coordinator.Contains("modmap.json", StringComparison.OrdinalIgnoreCase),
        "Map presets were mixed into modmap.json");
    string[] mapLocaleKeys =
    {
        "ExtendedData.MapModSettingsErrorTitle=",
        "ExtendedData.MapModSettingsUnavailable=",
        "ExtendedData.MapModSettingsMissingTitle=",
        "ExtendedData.MapModSettingsMissing=",
    };
    foreach (string localePath in Directory.GetFiles(Path.Combine(projectRoot, "Locales"), "*.txt"))
    {
        string locale = File.ReadAllText(localePath);
        string localeName = Path.GetFileName(localePath);
        foreach (string key in mapLocaleKeys)
        {
            Assert(CountOccurrences(locale, key) == 1,
                localeName + " does not define exactly one " + key);
        }

        Assert(!locale.Contains("ExtendedData.UseMapModSettings=") &&
            !locale.Contains("ExtendedData.UseMapModSettingsHelp=") &&
            !locale.Contains("ExtendedData.MapModSettingsActive="),
            localeName + " still contains labels for the removed separate Map button");
    }
}

static void TestEditorSaveModSettingsOptions()
{
    string root = FindProjectRoot();
    string mapCoordinator = File.ReadAllText(Path.Combine(root, "src", "MapModSettingsCoordinator.cs"));
    string trailCoordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string viewModel = File.ReadAllText(Path.Combine(root, "src", "EditorModSettingsSaveOptionsViewModel.cs"));
    string project = File.ReadAllText(Path.Combine(root, "ExtendedData.csproj"));
    string mapXamlPath = Path.Combine(root, "Patches", "Assets", "GUI", "XAMLResources", "HUD_LoadSaveRequester.xaml");
    string trailXamlPath = Path.Combine(root, "Patches", "Assets", "GUI", "XAMLResources", "FRONT_ManageTrail.xaml");
    string mapXaml = File.ReadAllText(mapXamlPath);
    string trailXaml = File.ReadAllText(trailXamlPath);

    Assert(mapCoordinator.Contains("requesterType != Enums.RequesterTypes.SaveEditorMap") &&
        mapCoordinator.Contains("ArmMapEditorSave(") &&
        mapCoordinator.Contains("pendingMapSaveDecision") &&
        mapCoordinator.Contains("pendingMapSaveIncludesSettings") &&
        mapCoordinator.Contains("string.Equals(pendingMapSavePath, normalizedPath, StringComparison.OrdinalIgnoreCase)") &&
        mapCoordinator.Contains("VerifySavedMapSettings(") &&
        mapCoordinator.Contains("RemoveActiveMapSettingsEntry(") &&
        mapCoordinator.Contains("TryRollbackMapFile("),
        "Map Editor saves are not explicitly armed, path-bound, verified, and rollback-safe");
    Assert(mapCoordinator.Contains("TryReadBinaryFile(ArchiveEntryName, ignoreCase: true) != null") &&
        viewModel.Contains("IncludeMapModSettings = hasExistingEntry") &&
        viewModel.Contains("includeTrailModSettings = true"),
        "Map/Trail checkbox defaults do not match existing-source and Trail authoring rules");
    Assert(trailCoordinator.Contains("editorSaveOptions.IncludeTrailModSettings") &&
        trailCoordinator.Contains("ModSettingsJson.WriteAtomic(sidecar, document)") &&
        trailCoordinator.Contains("File.Delete(sidecar)") &&
        trailCoordinator.Contains("trailSourceDocument = null"),
        "Trail Maker save does not explicitly write or remove only its sidecar");
    Assert(project.Contains("Patches\\Assets\\GUI\\XAMLResources\\HUD_LoadSaveRequester.xaml") &&
        project.Contains("Patches\\Assets\\GUI\\XAMLResources\\FRONT_ManageTrail.xaml"),
        "Editor save-option XAML patches are not included in the distributable ExtendedData package");
    Assert(mapXaml.Contains("ExtendedDataMapEditorSaveOptionsHost") &&
        mapXaml.Contains("IncludeMapModSettings, Mode=TwoWay") &&
        mapXaml.Contains("MapHelpText") &&
        mapXaml.Contains("Width=\"220\"") &&
        mapXaml.Contains("Height=\"23\"") &&
        mapXaml.Contains("Margin=\"45,360,0,0\"") &&
        mapXaml.Contains("<CheckBox HorizontalAlignment=\"Center\"") &&
        trailXaml.Contains("ExtendedDataTrailMakerSaveOptionsHost") &&
        trailXaml.Contains("IncludeTrailModSettings, Mode=TwoWay") &&
        trailXaml.Contains("TrailHelpText") &&
        trailXaml.Contains("Width=\"300\"") &&
        trailXaml.Contains("Height=\"23\"") &&
        trailXaml.Contains("Margin=\"350,130,0,0\"") &&
        trailXaml.Contains("<CheckBox HorizontalAlignment=\"Center\""),
        "editor save checkboxes, their explanatory tooltips, or their collision-free positions are missing");

    foreach (string xamlPath in new[] { mapXamlPath, trailXamlPath })
    {
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(xamlPath);
        foreach (System.Xml.Linq.XElement content in document.Descendants().Where(element => element.Name.LocalName == "Content"))
            Assert(content.Elements().Count() == 1, Path.GetFileName(xamlPath) + " has a Content operation without exactly one root");
    }

    string[] keys =
    {
        "EditorSave.IncludeModSettings=",
        "EditorSave.IncludeMapModSettingsHelp=",
        "EditorSave.IncludeTrailModSettingsHelp=",
        "EditorSave.ModSettingsSaveFailedTitle=",
        "EditorSave.ModSettingsSaveFailed=",
    };
    foreach (string localePath in Directory.GetFiles(Path.Combine(root, "Locales"), "*.txt"))
    {
        string locale = File.ReadAllText(localePath);
        foreach (string key in keys)
            Assert(CountOccurrences(locale, key) == 1, Path.GetFileName(localePath) + " does not define exactly one " + key);
    }
}

static void TestLobbyPacketThreadMarshalling()
{
    string root = FindProjectRoot();
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string project = File.ReadAllText(Path.Combine(root, "ExtendedData.csproj"));
    Assert(coordinator.Contains("new CoopCustomizePacket", StringComparison.Ordinal) &&
        coordinator.Contains("new BuiltInCustomizeOriginPacket", StringComparison.Ordinal) &&
        coordinator.Contains("UnityMainThreadDispatch.TryRunInlineOrEnqueue", StringComparison.Ordinal) &&
        coordinator.Contains("ProcessCoopCustomizePacket(packet, new CSteamID(senderSteamId))", StringComparison.Ordinal) &&
        coordinator.Contains("ProcessBuiltInCustomizeOriginPacket(packet, new CSteamID(senderSteamId))", StringComparison.Ordinal),
        "lobby packet callbacks do not copy and conditionally dispatch before UI/lobby access");
    Assert(project.Contains("Shared\\UnityMainThreadDispatch.cs", StringComparison.Ordinal),
        "ExtendedData does not source-link the validated main-thread dispatcher");
}

static void TestCustomizedLaunchOriginRoundtrip()
{
    SHCDESE.API.ModSaveDataAPI saveApi = SHCDESE.API.ModSaveDataAPI.Instance;
    ExtendedData.ExtendedDataLaunchOriginApi.Initialize(null);
    Assert(saveApi.SaveCallback != null && saveApi.LoadCallback != null && saveApi.OnUnloadCallback != null,
        "origin save-data callbacks were not registered");

    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(90, 3);
    byte[] saved = saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(
        isSaveFile: true,
        isMapEditorSave: false));
    Assert(saved != null && saved.Length > 0, "customized Custom Trail origin was not serialized");
    Assert(saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(
        isSaveFile: true,
        isMapEditorSave: true)) == null,
        "origin data was written into a Map Editor file");

    ExtendedData.ExtendedDataLaunchOriginApi.Clear();
    saveApi.LoadCallback(saved, new SHCDESE.API.Components.SaveData.LoadContext(isSaveFile: true));
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
            ExtendedData.ExtendedDataLaunchOriginKind.CustomizedCustomTrail &&
        ExtendedData.ExtendedDataLaunchOriginApi.TrailId == 90 &&
        ExtendedData.ExtendedDataLaunchOriginApi.MissionId == 3 &&
        ExtendedData.ExtendedDataLaunchOriginApi.RestoredFromSave,
        "process-restart-style Custom Trail roundtrip lost its restored origin");

    // Both Script Extender unload phases occur before OnStartMap during a launch.
    saveApi.OnUnloadCallback();
    saveApi.OnUnloadCallback();
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin !=
        ExtendedData.ExtendedDataLaunchOriginKind.None,
        "launch origin was cleared between unload and map-start events");
    ExtendedData.ExtendedDataLaunchOriginApi.MarkMapStarted();
    saveApi.OnUnloadCallback();
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
        ExtendedData.ExtendedDataLaunchOriginKind.None,
        "origin survived a real context unload");

    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(3, 10);
    saved = saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(true, false));
    ExtendedData.ExtendedDataLaunchOriginApi.Clear();
    saveApi.LoadCallback(saved, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
            ExtendedData.ExtendedDataLaunchOriginKind.CustomizedCoopTrail &&
        ExtendedData.ExtendedDataLaunchOriginApi.TrailId == 3 &&
        ExtendedData.ExtendedDataLaunchOriginApi.MissionId == 10,
        "Coop Trail origin did not survive a save roundtrip");

    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedVanillaTrail(1, 1, 0);
    saved = saveApi.SaveCallback(new SHCDESE.API.Components.SaveData.SaveContext(true, false));
    ExtendedData.ExtendedDataLaunchOriginApi.Clear();
    saveApi.LoadCallback(saved, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
            ExtendedData.ExtendedDataLaunchOriginKind.CustomizedVanillaTrail &&
        ExtendedData.ExtendedDataLaunchOriginApi.TrailType == 1 &&
        ExtendedData.ExtendedDataLaunchOriginApi.MissionId == 0,
        "Vanilla Trail Customize origin did not survive a save roundtrip");

    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedSandsOfTime(11, 11, 4);
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
            ExtendedData.ExtendedDataLaunchOriginKind.CustomizedSandsOfTime &&
        ExtendedData.ExtendedDataLaunchOriginApi.LaunchPending,
        "Sands of Time Customize origin was not exposed as pending");

    byte[] legacy = MessagePack.MessagePackSerializer.Serialize(
        new ExtendedData.ExtendedDataLaunchOriginApi.LaunchOriginSaveData
        {
            Version = 1,
            Origin = (int)ExtendedData.ExtendedDataLaunchOriginKind.CustomizedCustomTrail,
            TrailType = -1,
            TrailId = 90,
            MissionId = 2,
        });
    saveApi.LoadCallback(legacy, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
            ExtendedData.ExtendedDataLaunchOriginKind.CustomizedCustomTrail &&
        ExtendedData.ExtendedDataLaunchOriginApi.MissionId == 2,
        "version-1 launch origin was not read compatibly");

    saveApi.LoadCallback(new byte[] { 0xc1 }, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
        ExtendedData.ExtendedDataLaunchOriginKind.None,
        "corrupt MessagePack origin did not fail closed");

    byte[] mismatched = MessagePack.MessagePackSerializer.Serialize(
        new ExtendedData.ExtendedDataLaunchOriginApi.LaunchOriginSaveData
        {
            Version = 999,
            Origin = (int)ExtendedData.ExtendedDataLaunchOriginKind.CustomizedCustomTrail,
            TrailId = 90,
            MissionId = 1,
        });
    saveApi.LoadCallback(mismatched, new SHCDESE.API.Components.SaveData.LoadContext(true));
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
        ExtendedData.ExtendedDataLaunchOriginKind.None,
        "unknown saved-origin schema did not fail closed");

    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(89, 1);
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
        ExtendedData.ExtendedDataLaunchOriginKind.None,
        "invalid Custom Trail identity became active");
    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(0, 11);
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
        ExtendedData.ExtendedDataLaunchOriginKind.None,
        "invalid Coop mission identity became active");
    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedVanillaTrail(0, 0, -1);
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
        ExtendedData.ExtendedDataLaunchOriginKind.None,
        "invalid Vanilla Customize mission identity became active");
    ExtendedData.ExtendedDataLaunchOriginApi.SetCustomizedSandsOfTime(10, 10, 0);
    Assert(ExtendedData.ExtendedDataLaunchOriginApi.Origin ==
        ExtendedData.ExtendedDataLaunchOriginKind.None,
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
    string viewModel = File.ReadAllText(Path.Combine(root, "src", "ExtendedDataSettingsViewModel.cs"));
    string plugin = File.ReadAllText(Path.Combine(root, "src", "ExtendedDataPlugin.cs"));
    string runtime = File.ReadAllText(Path.Combine(root, "src", "ExtendedDataRuntime.cs"));
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string compatibilityContract = File.ReadAllText(Path.Combine(root, "ExtendedData.Core", "TrailModCompatibilityContract.cs"));
    string workspaceRoot = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("workspace root missing");
    string hostPlugin = File.ReadAllText(Path.Combine(workspaceRoot, "SerpsModsHost", "src", "SerpsModsHostPlugin.cs"));
    string xaml = File.ReadAllText(Path.Combine(root, "Override", "ScriptExtenderUI", "ExtendedDataSettings.xaml"));

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
        xaml.Contains("Style=\"{StaticResource ModSettingsToolTipStyle}\"") &&
        xaml.Contains("Content=\"{Binding HelpText}\"") &&
        xaml.Contains("SelectedIndex=\"{Binding SelectedModeIndex, Mode=TwoWay}\"") &&
        xaml.Contains("ItemsSource=\"{Binding Settings}\""),
        "dynamic mode selectors do not use the shared modsettings tooltip design");
    Assert(xaml.Contains("PracticalEffectsText") && viewModel.Contains("ExtendedData.PracticalEffects"),
        "player-facing practical-effects text is not bound below the activation setting");
    int descriptionPosition = xaml.IndexOf("PracticalEffectsText", StringComparison.Ordinal);
    int guidePosition = xaml.IndexOf("OpenCompatibilityGuideCommand", StringComparison.Ordinal);
    int hostOptionsPosition = xaml.IndexOf("HostOptionsText", StringComparison.Ordinal);
    int modSelectionPosition = xaml.IndexOf("SupportedTrailSettingsTitle", StringComparison.Ordinal);
    Assert(descriptionPosition >= 0 && descriptionPosition < guidePosition &&
        guidePosition < modSelectionPosition &&
        modSelectionPosition < hostOptionsPosition,
        "the ExtendedData guide or local Trail settings are not shown in the intended order");
    Assert(viewModel.Contains("https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main/Guides/ExtendedData") &&
        !viewModel.Contains("Mod%20Compatibilty%20ExtendedData.md"),
        "the settings guide link does not target the canonical ExtendedData guide");
    foreach (string localePath in Directory.GetFiles(Path.Combine(root, "Locales"), "*.txt"))
    {
        string locale = File.ReadAllText(localePath);
        bool isGerman = string.Equals(Path.GetFileName(localePath), "de-DE.txt", StringComparison.Ordinal);
        string customLordText = isGerman
            ? "lädt beim Hochladen von Custom Lords auch deren Custom Data hoch"
            : "uploads custom data together with Custom Lords";
        string guideText = isGerman
            ? "ExtendedData-Guide öffnen"
            : "Open the ExtendedData guide";
        Assert(locale.Contains(customLordText, StringComparison.Ordinal) &&
            locale.Contains("ExtendedData.CompatibilityGuide=" + guideText + "\r\n", StringComparison.Ordinal),
            Path.GetFileName(localePath) + " does not describe Custom Lord data uploads or link the guide clearly");
        Assert(!locale.Contains("ExtendedData.TrailSettingModeSummary=", StringComparison.Ordinal) &&
            !locale.Contains("Fixed Trail value", StringComparison.Ordinal) &&
            !locale.Contains("Fester Trailwert", StringComparison.Ordinal),
            Path.GetFileName(localePath) + " retains obsolete Trail-only mode text or a removed counter.");
    }
    Assert(xaml.Contains("CompatibleTrailMods") && xaml.Contains("IncompatibleTrailModsText") &&
        viewModel.Contains("PlayerTrailPropertyIds") &&
        viewModel.Contains("FixedTrailPropertyIds") &&
        viewModel.Contains("TrailSettingMode.ModDefault") &&
        viewModel.Contains("TrailSettingMode.Player") &&
        viewModel.Contains("TrailSettingMode.Fixed") &&
        runtime.Contains("DiscoverModCompatibility()"),
        "the dynamic compatible/incompatible Trail-mod catalog is not shown or persisted");
    Assert(viewModel.Contains("useFixedDefaultsForNewProperties") &&
        viewModel.Contains("initializedTrailPropertyIds.Add(id)") &&
        viewModel.Contains("SetModDefaultValue(nameof(PlayerTrailPropertyIds), Array.Empty<string>())") &&
        viewModel.Contains("SetModDefaultValue(nameof(FixedTrailPropertyIds), fixedDefaults)") &&
        coordinator.Contains("useFixedDefaults: !exists && editable") &&
        coordinator.Contains("applyEditorModes?.Invoke(document, useFixedDefaults)"),
        "new and reset Map/Trail settings must default to Fixed without reinterpreting loaded documents.");
    Assert(xaml.Contains("Width=\"580\" HorizontalAlignment=\"Left\"") &&
        xaml.Contains("<ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"175\"/><ColumnDefinition Width=\"28\"/>") &&
        xaml.Contains("<ComboBox Grid.Column=\"1\"") &&
        xaml.Contains("<ToggleButton Grid.Column=\"2\"") &&
        !xaml.Contains("<Grid.RowDefinitions><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"Auto\"/></Grid.RowDefinitions>") &&
        xaml.Contains("Width=\"530\" HorizontalAlignment=\"Left\"") &&
        xaml.Contains("<ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"175\"/>") &&
        xaml.Contains("HorizontalScrollBarVisibility=\"Auto\"") &&
        !xaml.Contains("{Binding SummaryText}"),
        "Trail mod and feature selectors are not arranged as a compact left-aligned table");
    Assert(coordinator.Contains("getPropertyMode(participant.Key, property.Name)") &&
        coordinator.Contains("TrailSettingMode.Player") &&
        coordinator.Contains("TrailSettingMode.Fixed") &&
        coordinator.Contains("entry.Overrides") &&
        compatibilityContract.Contains("DoNotPersistAttribute") &&
        compatibilityContract.Contains("deserializationProbe"),
        "dynamic Trail compatibility does not enforce the safe mission-preset contract");
    Assert(coordinator.Contains("GetRegistrationGroups()") && coordinator.Contains("group.Skip(1).Any()"),
        "duplicate mod-settings registrations are not rejected per plugin GUID");
    Assert(coordinator.Contains("IsPreferred = true") &&
        coordinator.Contains("IsPreferred = trailSourceDocument == null") &&
        coordinator.Contains("PreferenceContextId = workingSourceContextId") &&
        coordinator.Contains("workingSourceContextId = \"trail:\" + IOPath.GetFullPath(sidecar)"),
        "Trail and Map sources do not expose the required Trail-first mission-context preference");
    Assert(coordinator.Contains("DebugLogHelper.LogInfo(") &&
        coordinator.Contains("are not included in creator presets") &&
        !coordinator.Contains("Trail mod-settings compatibility rejected"),
        "unsupported Map/Trail settings are not reported as informational exclusions");
    Assert(plugin.Contains("ScheduleDeferredCompatibilityRefresh()") &&
        plugin.Contains("Application.onBeforeRender += RefreshCompatibilityAfterRegistrations") &&
        plugin.Contains("Application.onBeforeRender -= RefreshCompatibilityAfterRegistrations"),
        "Trail compatibility is not refreshed once after all LibraryLoaded registrations");
    Assert(coordinator.Contains("IsRegistrationGroupOptedOut(group)") &&
        plugin.Contains("public const bool ExtendedDataModSettingsOptOut = true;") &&
        hostPlugin.Contains("public const bool ExtendedDataModSettingsOptOut = true;"),
        "explicit opt-out is not applied to discovery, ExtendedData, and SerpsModsHost");
    Assert(coordinator.Contains("ExitActiveParticipants") &&
        coordinator.Contains("activeParticipantIds.Add(item.Item1)"),
        "Trail lifecycle is not limited to participants whose preset entry completed");
    Assert(xaml.Contains("System_OpenPresetLoadCommand") &&
        xaml.Contains("System_OpenPresetSaveCommand") &&
        xaml.Contains("System_PresetLoadEntries") &&
        !xaml.Contains("{Binding SelectedPreset") && !xaml.Contains("{Binding PresetOptions") &&
        plugin.Contains("LobbyModSettingsPresetRegistration.Register"),
        "shared preset UI or registration is missing");
    Assert(xaml.Contains("CoopPackageOptions") && xaml.Contains("CanEditCoopPackage"), "host package dropdown is missing");
    Assert(xaml.Contains("SelectedItem=\"{Binding SelectedCoopPackage, Mode=TwoWay}\"") &&
        !xaml.Contains("SelectedCoopPackageIndex"),
        "Coop package dropdown does not use the stable CastlePlanner-style SelectedItem binding");
    Assert(viewModel.Contains("coopPackageIds = new[] { string.Empty }") &&
        viewModel.Contains("ExtendedData.VanillaPackage"),
        "Coop package dropdown does not initialize with Vanilla");
    Assert(!viewModel.Contains("PackagesRefreshRequested?.Invoke()") &&
        viewModel.Contains("if (unchanged)"),
        "Coop package dropdown still replaces its ItemsSource reentrantly");
}

static void TestScriptExtenderManifestRangeContract()
{
    string root = FindProjectRoot();
    string workspaceRoot = Directory.GetParent(root)?.FullName ??
        throw new InvalidOperationException("workspace root missing");
    string plugin = File.ReadAllText(Path.Combine(root, "src", "ExtendedDataPlugin.cs"));
    string project = File.ReadAllText(Path.Combine(root, "ExtendedData.csproj"));
    string info = File.ReadAllText(Path.Combine(root, "info.json"));
    string sharedPreset = File.ReadAllText(
        Path.Combine(workspaceRoot, "APIShared", "src", "PresetLobbyModSettingsViewModel.cs"));
    using JsonDocument manifestJson = JsonDocument.Parse(info);
    string minimumExtenderVersion = manifestJson.RootElement
        .GetProperty("MinimumScriptExtenderVersion").GetString() ?? string.Empty;
    string maximumExtenderVersion = manifestJson.RootElement
        .GetProperty("MaximumScriptExtenderVersion").GetString() ?? string.Empty;

    Assert(Version.TryParse(minimumExtenderVersion, out Version minimum),
        "Manifest minimum Script Extender version is invalid");
    Assert(string.IsNullOrEmpty(maximumExtenderVersion) ||
        (Version.TryParse(maximumExtenderVersion, out Version maximum) && maximum >= minimum),
        "Manifest maximum Script Extender version is invalid or below the minimum");
    Assert(plugin.Contains($"[BepInDependency(\"000shcdese\", \"{minimumExtenderVersion}\")]"),
        "SHCDESE minimum dependency does not match the manifest");
    Assert(plugin.Contains("OnLibraryLoaded(CrusaderLibraryLoadContext context)") &&
        !plugin.Contains("OnLibraryLoaded(IntPtr") &&
        !plugin.Contains("ReadOnlySpan<byte> memory"),
        "LibraryLoaded handler does not use the Script Extender load context");
    Assert(!project.Contains("Zhuqiaomon", StringComparison.OrdinalIgnoreCase),
        "ExtendedData retains a stale Zhuqiaomon project reference");
    Assert(info.Contains("\"NetworkMode\": 1"),
        "ExtendedData does not explicitly declare gameplay NetworkMode 1");
    Assert(!sharedPreset.Contains("ScriptExtenderMultiplayerSyncWorkaround") &&
        !sharedPreset.Contains("EnsureInstalled"),
        "obsolete shared Script Extender settings workaround remains active");
}

static void TestCoopExporterIntegration()
{
    string root = FindProjectRoot();
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string exporter = File.ReadAllText(Path.Combine(root, "src", "CoopTrailPackageExporter.cs"));
    string runtime = File.ReadAllText(Path.Combine(root, "src", "ExtendedDataRuntime.cs"));
    string lordDataCoordinator = File.ReadAllText(Path.Combine(root, "src", "LordDataSyncCoordinator.cs"));
    string viewModel = File.ReadAllText(Path.Combine(root, "src", "ExtendedDataSettingsViewModel.cs"));
    string packet = File.ReadAllText(Path.Combine(root, "src", "CoopCustomizePacket.cs"));
    string project = File.ReadAllText(Path.Combine(root, "ExtendedData.csproj"));
    Assert(exporter.Contains("Path.GetFileName(lordSource)") &&
        exporter.Contains("Path.GetFileName(aivSource)") &&
        exporter.Contains("ToMissionRelative(lordTarget, missionsRoot)") &&
        exporter.Contains("ToMissionRelative(aivTarget, missionsRoot)") &&
        !exporter.Contains("\"lord-\" + playerNumber") &&
        !exporter.Contains("\"aiv-\" + playerNumber"),
        "Coop export no longer preserves the source names used by Vanilla Lord and AIV checksums");
    Assert(coordinator.Contains("ExtendedDataCoopExport") && coordinator.Contains("cooptrail.enabled"), "Trail Maker Coop checkbox/marker is missing");
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
        coordinator.Contains("ModSettingsPresetJson.ConvertValue(value, targetType)") &&
        coordinator.Contains("ModSettingsJson.IsSupportedValue(value)") &&
        coordinator.Contains("MessagePackSerializer.Serialize(propertyType, value)") &&
        coordinator.Contains("participant.Value.System_CreateDisabledMissionPresetSnapshot()"),
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
        coordinator.Contains("UploadCoopTrailPackage(self, selectedRow.trail, uploadOptions.IncludeExtendedData)") &&
        coordinator.Contains("CoopWorkshopPackageStaging.Stage(") &&
        coordinator.Contains("CoopTrailPackageCatalog.Load(source)"),
        "Coop packages are not validated, listed, and staged through the unified Workshop uploader");
    Assert(exporter.Contains("ordinal < 40") && exporter.Contains("activeSlots.Count < 2"), "export limits or two-human validation are missing");
    Assert(exporter.Contains("MissionProjection.Create(definition)") &&
        exporter.Contains("Colour = Math.Max(1, Math.Min(8, colour))") &&
        exporter.Contains("NativePreferredAiv = restart.MPsetupData.preferredAIVs[slot]") &&
        exporter.Contains("MultiplayerSetup = new MultiplayerSetupSettings"),
        "Coop export does not preserve normalized teams, colours, preferred AIVs, and complete setup data");
    Assert(exporter.Contains("ModSettingsJson.Read(sidecar)") &&
        exporter.Contains("ModSettingsJson.WriteAtomic(MissionLoader.GetModSettingsPath(jsonPath), modSettings)") &&
        !File.ReadAllText(Path.Combine(root, "ExtendedData.Core", "MissionDefinitionJson.cs")).Contains("[\"modSettings\"]"),
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
    string buttonHook = runtime.Substring(runtime.IndexOf("private void ButtonClickedHook(", StringComparison.Ordinal),
        runtime.IndexOf("private void UpdateHostInfoHook(", StringComparison.Ordinal) -
        runtime.IndexOf("private void ButtonClickedHook(", StringComparison.Ordinal));
    Assert(buttonHook.Contains("if (IsStartCommand(command))") &&
        runtime.Contains("ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(") &&
        runtime.IndexOf("ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(", StringComparison.Ordinal) <
            runtime.LastIndexOf("buttonTrampoline(self, command)", StringComparison.Ordinal) &&
        runtime.Contains("ApplyMultiplayerSetup(setupData, selected.Loaded.Definition.Settings.MultiplayerSetup)") &&
        !buttonHook.Contains("ApplySelectedMission(self, false)"),
        "direct Coop start does not retain the edited setup and establish its launch origin before Vanilla starts");
    Assert(runtime.Contains("self.currentLobby.AIVDataChecksum()") &&
        runtime.Contains("Lord media selection changed; wait for the updated lobby selection") &&
        runtime.Contains("UpdateHostInfoMethod.Invoke(self, new object[] { false })"),
        "Coop readiness does not follow Vanilla's transmitted Lord selection after media remapping");
    Assert(runtime.Contains("selectedInfo.lordConfig = transmitted.lordConfig") &&
        runtime.Contains("PreparedTrailMediaAlias(selected.Loaded.LordRequirements") &&
        runtime.Contains("lordDataCoordinator.RemapTrailMedia(localInfos)") &&
        runtime.Contains("settings.LordPackageManifestChanged += OnLordPackageManifestChangedForSelection") &&
        lordDataCoordinator.Contains("ConfigChecksum = infos[slot.PlayerId].lordConfig.checksum.ToString()") &&
        !lordDataCoordinator.Contains("SequenceEqual(slot.AivChecksums)"),
        "same-Lord configuration changes are not preserved with Trail values on both players");
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
        !runtime.Contains("SerpLocalization.Get(\"ExtendedData.ErrorParticipantNotReady\")"),
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
        runtime.Contains("source: \"custom Coop mission setup\"") &&
        runtime.Contains("ActivateSelectedMissionSettingsUnlessMap("),
        "Coop Customize does not reapply the mission Trail preset after rebuilding the setup UI");
    Assert(coordinator.Contains("GetPacketEventFor<CoopCustomizePacket>") &&
        coordinator.Contains("GameNetworkAPI.GetHostSteamId()") &&
        coordinator.Contains("senderSteamId != host.Value") &&
        coordinator.Contains("ProcessCoopCustomizePacket(packet, new CSteamID(senderSteamId))") &&
        coordinator.Contains("BroadcastCoopCustomize(trailId, mission)") &&
        coordinator.Contains("BroadcastCoopLaunch") &&
        coordinator.Contains("CoopLaunchReceived?.Invoke") &&
        coordinator.Contains("source: \"authenticated host packet\"") &&
        packet.Contains("IMessagePackFormatter<CoopCustomizePacket>") &&
        packet.Contains("[Key(0)]") && packet.Contains("[Key(3)]") &&
        project.Contains("src\\CoopCustomizePacket.cs"),
        "Coop setup/launch transitions are not synchronized from the authenticated lobby host to clients");
    Assert(runtime.Contains("RequireMethod(\"InitCoopMissions\")") &&
        runtime.Contains("selected != null && IsLaunchCommand(command)") &&
        runtime.Contains("source: \"custom Coop mission \" + command") &&
        runtime.Contains("ActivateSelectedMissionSettingsUnlessMap(") &&
        runtime.Contains("CoopLaunchReceived += OnCoopLaunchReceived") &&
        runtime.Contains("source: \"authenticated host Coop launch\"") &&
        runtime.Contains("coopLaunchPending") && runtime.Contains("OnMapStarted()") &&
        runtime.Contains("OnMissionEnded(MissionLifecycleNotification notification)"),
        "direct Coop launch does not retain the shared Trail preset across the map transition");
    Assert(runtime.Contains("if (coopLaunchPending)") && runtime.Contains("BlockLaunch(command") &&
        coordinator.Contains("MpLocalReadyField") && coordinator.Contains("MpLocalReadyLockedField") &&
        coordinator.Contains(".SetValue(self, false)"),
        "Coop launch refresh retention, visible blocking, or Customize ready-state reset is missing");
    Assert(coordinator.Contains("SinglePlayerCoopStarting") &&
        runtime.Contains("PrepareSinglePlayerCoopStart") &&
        runtime.Contains("packageCatalog.Scan(roots, LogInfo, LogError)") &&
        runtime.Contains("source: \"single-player Coop restart\"") &&
        runtime.Contains("missionSettingsCoordinator.PrepareCoopMissionLaunch()") &&
        runtime.Contains("Blocked custom single-player Coop restart"),
        "single-player Coop restart does not revalidate and reactivate its package mission");
    Assert(!runtime.Contains("Path.Combine(pluginRoot, \"CoopTrails\")"), "legacy plugin-local package layout is still active");
}

static void TestCustomizeButtonDelegation()
{
    string root = FindProjectRoot();
    string bridge = File.ReadAllText(Path.Combine(root, "src", "BugfixesAndQoLTrailCustomizationBridge.cs"));
    string coordinator = File.ReadAllText(Path.Combine(root, "src", "TrailMissionSettingsCoordinator.cs"));
    string plugin = File.ReadAllText(Path.Combine(root, "src", "ExtendedDataPlugin.cs"));
    Assert(bridge.Contains("TrailCustomizationProviderHostApi, BugfixesAndQoL") &&
        bridge.Contains("typeof(Func<bool>)") && bridge.Contains("TryRegister("),
        "optional BugfixesAndQoL provider handshake is missing");
    Assert(coordinator.Contains("externalButtonOwner = customizationBridge.TryRegister(") &&
        coordinator.Contains("if (!externalButtonOwner &&") &&
        coordinator.Contains("if (externalButtonOwner)") &&
        coordinator.Contains("HandleExternalCustomTrailCustomize") &&
        coordinator.Contains("HandleExternalCoopTrailCustomize"),
        "ExtendedData does not retain standalone behavior while delegating shared button ownership");
    Assert(plugin.Contains("[BepInDependency(\"BugfixesAndQoL_Serp\", BepInDependency.DependencyFlags.SoftDependency)]"),
        "optional button owner is not ordered as a soft dependency");
}

static void TestDependencyFreeCoopJson()
{
    string root = FindProjectRoot();
    string core = Path.Combine(root, "ExtendedData.Core");
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
            if (File.Exists(Path.Combine(directory.FullName, "ExtendedData.csproj")))
                return directory.FullName;
            string child = Path.Combine(directory.FullName, "ExtendedData", "ExtendedData.csproj");
            if (File.Exists(child))
                return Path.GetDirectoryName(child)!;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("ExtendedData project root was not found.");
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

static void TestCatalogIgnoresExportStaging()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    string published = CreatePackage(fixture, root, "Published", 2);
    string staging = Path.Combine(root, ".cooptrail-build-3d0a8f6545ed42ed81e82bc11d250638");
    Directory.CreateDirectory(staging);
    File.Copy(Path.Combine(published, "cooptrail.json"), Path.Combine(staging, "cooptrail.json"));

    var info = new List<string>();
    var errors = new List<string>();
    var catalog = new CoopTrailPackageCatalog();
    catalog.Scan(root, info.Add, errors.Add);

    Assert(errors.Count == 0, "moved Coop missions in the export staging folder were reported as a broken package");
    Assert(catalog.Packages.Count == 1 && catalog.Packages.Values.Single().Missions.Count == 2,
        "the published two-mission Coop Trail was not discovered");
    Assert(info.Count(message => message.StartsWith("Found Coop Trail package", StringComparison.Ordinal)) == 1,
        "the published Coop Trail was not reported exactly once");
}

static void TestCatalogStateChangeLogging()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    string package = CreatePackage(fixture, root, "Logged", 1);
    var info = new List<string>();
    var errors = new List<string>();
    var catalog = new CoopTrailPackageCatalog();

    catalog.Scan(root, info.Add, errors.Add);
    Assert(info.Count(message => message.StartsWith("Found Coop Trail package", StringComparison.Ordinal)) == 1,
        "initial package discovery was not logged once");
    info.Clear();

    catalog.Scan(root, info.Add, errors.Add);
    Assert(info.Count == 0 && errors.Count == 0, "unchanged package scan repeated diagnostics");

    string manifestPath = Path.Combine(package, "cooptrail.json");
    File.WriteAllText(
        manifestPath,
        File.ReadAllText(manifestPath).Replace("\"displayName\": \"Logged\"", "\"displayName\": \"Logged Updated\""),
        new UTF8Encoding(false));
    catalog.Scan(root, info.Add, errors.Add);
    Assert(info.Count(message => message.StartsWith("Updated Coop Trail package", StringComparison.Ordinal)) == 1,
        "changed package was not logged");
    info.Clear();

    Directory.Delete(package, recursive: true);
    catalog.Scan(root, info.Add, errors.Add);
    Assert(info.Count(message => message.StartsWith("Removed Coop Trail package", StringComparison.Ordinal)) == 1,
        "removed package was not logged");
}

static void TestCatalogErrorTransitions()
{
    using Fixture fixture = Fixture.Create();
    string root = Path.Combine(fixture.Root, "CustomTrails");
    string package = CreatePackage(fixture, root, "Recoverable", 1);
    string missionPath = Path.Combine(package, "CoopMissions", "01.coopmission.json");
    string original = File.ReadAllText(missionPath);
    var info = new List<string>();
    var errors = new List<string>();
    var catalog = new CoopTrailPackageCatalog();

    catalog.Scan(root, info.Add, errors.Add);
    info.Clear();
    File.AppendAllText(missionPath, "changed");
    catalog.Scan(root, info.Add, errors.Add);
    Assert(errors.Count == 1, "new package scan error was not logged once");

    catalog.Scan(root, info.Add, errors.Add);
    Assert(errors.Count == 1, "unchanged package scan error was logged repeatedly");

    File.WriteAllText(missionPath, original, new UTF8Encoding(false));
    catalog.Scan(root, info.Add, errors.Add);
    Assert(info.Count(message => message.StartsWith("Resolved Coop Trail package scan issue", StringComparison.Ordinal)) == 1,
        "resolved package scan error was not logged");
}

static void TestOldMissionSchemasRejected()
{
    using Fixture schemaOne = Fixture.Create(schemaVersion: 1);
    ExpectFailure(() => new MissionLoader().Load(schemaOne.JsonPath, 1, 1), "mission schema 1 was accepted");
    using Fixture schemaTwo = Fixture.Create(schemaVersion: 2);
    ExpectFailure(() => new MissionLoader().Load(schemaTwo.JsonPath, 1, 1), "mission schema 2 was accepted");
    using Fixture schemaThree = Fixture.Create(schemaVersion: 3);
    ExpectFailure(() => new MissionLoader().Load(schemaThree.JsonPath, 1, 1), "mission schema 3 was accepted");
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
    string json = File.ReadAllText(fixture.SidecarPath).Replace("\"schemaVersion\": 3", "\"schemaVersion\": 99");
    File.WriteAllText(fixture.SidecarPath, json, new UTF8Encoding(false));
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(!string.IsNullOrWhiteSpace(loaded.Definition.ModSettingsError), "invalid sidecar was not reported");
    Assert(loaded.Definition.ModSettings.Mods.Count == 0, "invalid sidecar was partially retained instead of using mod defaults");
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

static void TestCanonicalTeamProjection()
{
    using Fixture fixture = Fixture.Create();
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    loaded.Definition.Players = new List<PlayerDefinition>
    {
        new PlayerDefinition { Active = true, Team = 2, KeepPosition = 1 },
        new PlayerDefinition { Active = true, Team = 2, KeepPosition = 2 },
        new PlayerDefinition { Active = true, Team = 1, KeepPosition = 3, Lord = new LordReference { Source = "builtIn", Id = 1 } },
        new PlayerDefinition { Active = true, Team = 1, KeepPosition = 4, Lord = new LordReference { Source = "builtIn", Id = 2 } },
        new PlayerDefinition { Active = true, Team = 3, KeepPosition = 5, Lord = new LordReference { Source = "builtIn", Id = 3 } },
    };
    MissionProjection projection = MissionProjection.Create(loaded.Definition);
    Assert(projection.Teams.Take(5).SequenceEqual(new[] { 1, 1, 2, 2, 3 }),
        "source team equivalence was not normalized to Vanilla Coop team ids");
}

static void TestMultiplayerSetupRoundtrip()
{
    using Fixture fixture = Fixture.Create();
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    MultiplayerSetupSettings setup = loaded.Definition.Settings.MultiplayerSetup;
    Assert(setup.StartingGameSpeed == 40 && setup.PeaceTime == 15 && setup.PreBuild == 1 &&
        setup.Eunuchs == 1 && setup.ImprovedSieging2 == 1,
        "multiplayer setup scalar values did not roundtrip");
    Assert(setup.BuildingsAvailable.Length == 13 && setup.GoodsAvailable.Length == 25 &&
        setup.TroopsAvailable.Length == 32 && setup.BuildingsAvailable.All(value => value == 1) &&
        setup.GoodsAvailable.All(value => value == 1) && setup.TroopsAvailable.All(value => value == 1),
        "multiplayer availability arrays did not roundtrip");
    Assert(loaded.Definition.Players[0].Colour == 8, "player colour 8 did not roundtrip");
    Assert(loaded.Definition.Players[2].NativePreferredAiv == 100,
        "native preferred AIV did not roundtrip");
}

static void TestInvalidMultiplayerSetup()
{
    using Fixture fixture = Fixture.Create();
    string json = File.ReadAllText(fixture.JsonPath);
    string edited = json.Replace("\"buildingsAvailable\": [", "\"buildingsAvailable\": [2,");
    Assert(!string.Equals(json, edited, StringComparison.Ordinal), "test fixture buildings array was not found");
    File.WriteAllText(fixture.JsonPath, edited, new UTF8Encoding(false));
    ExpectFailure(() => new MissionLoader().Load(fixture.JsonPath, 1, 1),
        "invalid multiplayer availability value was accepted");
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
    File.AppendAllText(Path.Combine(package, "CoopMissions", "01.modtrail.json"), "changed");
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
    File.WriteAllText(Path.Combine(trailMakerSource, "Trail_Mission_01.modtrail.json"), "editable sidecar");
    File.WriteAllText(Path.Combine(trailMakerSource, "Trail_Mission_01.modjson"), "legacy sidecar");
    File.WriteAllText(Path.Combine(source, "Upload.data"), "metadata");
    CoopTrailPackage package = CoopTrailPackageCatalog.Load(source);

    string included = Path.Combine(fixture.Root, "staging-included");
    CoopTrailPackage includedPackage = CoopWorkshopPackageStaging.Stage(
        package, included, "Upload.data", includeModSettings: true, out int includedCount);
    Assert(includedCount == 3, "not all Coop and Trail Maker sidecars were staged");
    Assert(File.Exists(Path.Combine(included, "CoopMissions", "01.modtrail.json")), "Coop sidecar was omitted");
    Assert(File.Exists(Path.Combine(included, "TrailMakerSource", "Trail_Mission_01.modtrail.json")), "Trail Maker sidecar was omitted");
    Assert(!File.Exists(Path.Combine(included, "TrailMakerSource", "Trail_Mission_01.modjson")), "legacy sidecar was staged");
    Assert(!File.Exists(Path.Combine(included, "Upload.data")), "Workshop metadata was copied into content");

    string excluded = Path.Combine(fixture.Root, "staging-excluded");
    CoopTrailPackage excludedPackage = CoopWorkshopPackageStaging.Stage(
        package, excluded, "Upload.data", includeModSettings: false, out int excludedCount);
    Assert(excludedCount == 0, "excluded Coop staging reported copied sidecars");
    Assert(!Directory.GetFiles(excluded, "*.modtrail.json", SearchOption.AllDirectories).Any(), "excluded Coop staging contains modsettings");
    Assert(!Directory.GetFiles(excluded, "*.modjson", SearchOption.AllDirectories).Any(), "excluded Coop staging contains legacy modsettings");
    Assert(includedPackage.Manifest.ContentFingerprint != excludedPackage.Manifest.ContentFingerprint,
        "including mission sidecars did not affect the staged package fingerprint");
    Assert(CoopTrailPackageCatalog.Load(included).Missions[0].Definition.ModSettings.Mods.Count == 1,
        "included staged package lost mission modsettings");
    Assert(CoopTrailPackageCatalog.Load(excluded).Missions[0].Definition.ModSettings.Mods.Count == 0,
        "excluded staged package retained mission modsettings");
}

static void TestCoopLordRequirementStaging()
{
    using Fixture fixture = Fixture.Create();
    string root = CreatePackage(fixture, Path.Combine(fixture.Root, "CustomTrails"), "LordTrail", 1);
    string mission = Path.Combine(root, "CoopMissions", "01.coopmission.json");
    TrailLordRequirements.Create(mission, new[]
    {
        new TrailLordSlot
        {
            PlayerId = 3, LordName = "Author", ConfigName = "lord",
            ConfigChecksum = "123", AivChecksums = new[] { "456" },
            RequiresInstalledPackage = true, PackageDigest = new string('A', 64),
            ModLordJson = "{\"test.mod\":{\"enabled\":true}}",
        },
    }).Write(mission);
    LoadedMission loaded = new MissionLoader().Load(mission, 1, 1);
    CoopTrailPackageManifest manifest = CoopTrailPackageManifestJson.Read(Path.Combine(root, "cooptrail.json"));
    manifest.ContentFingerprint = CoopTrailPackageFingerprint.Compute(root,
        new[] { mission, loaded.ModSettingsPath, loaded.LordRequirementsPath }
            .Concat(loaded.BundledFiles));
    CoopTrailPackageManifestJson.WriteAtomic(Path.Combine(root, "cooptrail.json"), manifest);
    CoopTrailPackage package = CoopTrailPackageCatalog.Load(root);
    string destination = Path.Combine(fixture.Root, "staged-lords");
    CoopTrailPackage staged = CoopWorkshopPackageStaging.Stage(package, destination,
        "LordTrail.data", includeModSettings: false, out int copied);
    Assert(copied == 0 && staged.Missions[0].LordRequirements != null &&
        staged.Missions[0].LordRequirements.Slots.Single().RequiresInstalledPackage,
        "required Lord data was removed with optional mod settings");
    File.AppendAllText(TrailLordRequirements.SidecarPath(Path.Combine(destination,
        "CoopMissions", "01.coopmission.json")), "changed");
    ExpectFailure(() => CoopTrailPackageCatalog.Load(destination),
        "modified Lord requirements passed the Coop package fingerprint");
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
        string sidecar = Path.Combine(missions, ordinal.ToString("00") + ".modtrail.json");
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

static void AssertThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

public sealed class FutureFixesNested
{
    public double Weight { get; set; }
    public bool Enabled { get; set; }
}

public sealed class FutureFixesPreferences
{
    public bool? Field01 { get; set; }
    public int? Field02 { get; set; }
    public int? Field03 { get; set; }
    public int? Field04 { get; set; }
    public int? Field05 { get; set; }
    public int? Field06 { get; set; }
    public int? Field07 { get; set; }
    public int? Field08 { get; set; }
    public int? Field09 { get; set; }
    public int? Field10 { get; set; }
    public int? Field11 { get; set; }
    public int? Field12 { get; set; }
    public int? Field13 { get; set; }
    public int? Field14 { get; set; }
    public int? Field15 { get; set; }
    public int? Field16 { get; set; }
    public int? Field17 { get; set; }
    public int? Field18 { get; set; }
    public FutureFixesNested Nested { get; set; }
    public Dictionary<string, List<int>> Values { get; set; }
}

public sealed class OlderFixesPreferences
{
    public bool? Field01 { get; set; }
}

public sealed class UnsupportedFixesPreferences
{
    public object Unknown { get; set; }
}

public sealed class ChangedFixesPreferences
{
    public string Field01 { get; set; }
}

public sealed class DecimalFixesPreferences
{
    public decimal Value { get; set; }
}

sealed class Fixture : IDisposable
{
    public string Root { get; private set; }
    public string JsonPath { get; private set; }
    public string SidecarPath { get; private set; }

    public static Fixture Create(int aivRotation = 90, int? secondAivRotation = null, int preferredAiv = -1, int schemaVersion = MissionLoader.CurrentSchemaVersion, int startGold = 500, bool includeLegacyModSetting = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "ExtendedDataTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "map.map"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(root, "lord.lordjson"), "{\"lord\":{}}", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "castle.aivjson"), "{}", new UTF8Encoding(false));

        var definition = new CoopMissionDefinition
        {
            SchemaVersion = schemaVersion,
            DisplayName = "Test",
            Map = new MapReference { Source = "bundled", File = "map.map" },
            Settings = new CoopSettings
            {
                Fairness = 3,
                StartingGoodsLevel = 1,
                MultiplayerSetup = new MultiplayerSetupSettings
                {
                    StartingGameSpeed = 40,
                    WinCondition = 0,
                    AllowAutoTrading = 1,
                    NoKnockdownWalls = 1,
                    AutoSave = 10,
                    PeaceTime = 15,
                    AdvancedSkirmishOptions = 1,
                    PreBuild = 1,
                    ImprovedArabSwordsmen = 1,
                    ImprovedLaddermen = 1,
                    ImprovedSpearmen = 1,
                    RebalancedHorseArchers = 1,
                    ImprovedFletchers = 1,
                    ImprovedSieging = 1,
                    Healers = 1,
                    Eunuchs = 1,
                    ImprovedSieging2 = 1,
                    BuildingsAvailable = Enumerable.Repeat(1, 13).ToArray(),
                    GoodsAvailable = Enumerable.Repeat(1, 25).ToArray(),
                    TroopsAvailable = Enumerable.Repeat(1, 32).ToArray(),
                },
            },
            Players = new List<PlayerDefinition>
            {
                new PlayerDefinition { KeepPosition = 1, Team = 1, Colour = 8 },
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
                    NativePreferredAiv = 100,
                },
            },
            ModSettings = ModSettingsDefinition.CreateModDefaults(),
        };
        definition.ModSettings.Mods["StartConditions_Serp"] = new ModSettingsEntry
        {
            Overrides = new Dictionary<string, object> { ["SetStartGoldHuman"] = startGold },
        };
        if (includeLegacyModSetting)
            definition.ModSettings.Mods["StartConditions_Serp"].Overrides["RemovedSetting"] = 99;
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
                json = json.Replace("\"schemaVersion\": " + MissionLoader.CurrentSchemaVersion, "\"schemaVersion\": " + requestedSchemaVersion, StringComparison.Ordinal);
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

sealed class CompatibleTrailSettingsViewModel : IModSettingsPresetEndpoint
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

sealed class NonBooleanEnableModViewModel : IModSettingsPresetEndpoint
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
    public const bool ExtendedDataModSettingsOptOut = true;
}

sealed class NotOptedOutPlugin
{
    public const bool ExtendedDataModSettingsOptOut = false;
}

sealed class RuntimeOptOutFieldPlugin
{
    public static bool ExtendedDataModSettingsOptOut = true;
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
    public sealed class GameMapArchiveManagerAPI
    {
        public static GameMapArchiveManagerAPI Instance { get; } = new GameMapArchiveManagerAPI();
        public string CurrentFilePath { get; set; }
        public byte[] ModMapBytes { get; set; }

        public string GetCurrentFilePath() => CurrentFilePath;

        public byte[] TryReadBinaryFile(string fileName, bool ignoreCase = true) =>
            string.Equals(fileName, "modmap.json", ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                ? ModMapBytes
                : null;

        public void Reset()
        {
            CurrentFilePath = null;
            ModMapBytes = null;
        }
    }

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

public sealed class CustomisationFileManager
{
    public sealed class CustomLordConfig
    {
        public string name;
        public string path;
        public ulong checksum;
    }
}
