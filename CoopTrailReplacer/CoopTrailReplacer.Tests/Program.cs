using CoopTrailReplacer.Core;
using System.Runtime.Serialization.Json;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("bundled mission load and stable hash", TestBundledMission),
    ("path escape rejected", TestPathEscape),
    ("invalid rotation rejected", TestInvalidRotation),
    ("catalog keeps valid slots and ignores invalid slots", TestCatalogIsolation),
    ("schema 1 is rejected", TestSchemaOneRejected),
    ("mod settings affect canonical hash", TestModSettingsHash),
    ("mod settings hash is canonical", TestCanonicalModSettingsHash),
    ("invalid mod settings disable transaction", TestInvalidModSettings),
    ("first two active players become allied humans", TestHumanProjection),
    ("asset bytes affect the mission hash", TestAssetHash),
    ("preferred AIV permits differing rotations", TestPreferredAiv),
    ("fourth trail tenth slot is addressable", TestLastCatalogSlot),
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
    LoadedMission first = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    LoadedMission second = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(first.Hash == second.Hash, "hash changed between identical loads");
    Assert(first.BundledFiles.Count == 3, "expected map, lord and AIV bundle files");
    Assert(first.Definition.Players.Where(player => player.Active).Take(2).Count() == 2, "human slots missing");
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
    string root = Path.Combine(fixture.Root, "CoopTrails");
    Directory.CreateDirectory(Path.Combine(root, "Trail1"));
    File.Copy(fixture.JsonPath, Path.Combine(root, "Trail1", "01.coopmission.json"));
    File.WriteAllText(Path.Combine(root, "Trail1", "02.coopmission.json"), "{}", new UTF8Encoding(false));
    File.Copy(Path.Combine(fixture.Root, "map.map"), Path.Combine(root, "Trail1", "map.map"));
    File.Copy(Path.Combine(fixture.Root, "lord.lordjson"), Path.Combine(root, "Trail1", "lord.lordjson"));
    File.Copy(Path.Combine(fixture.Root, "castle.aivjson"), Path.Combine(root, "Trail1", "castle.aivjson"));
    var errors = new List<string>();
    var catalog = new MissionCatalog();
    catalog.Load(root, null, errors.Add);
    Assert(catalog.TryGet(0, 1, out _), "valid slot not loaded");
    Assert(!catalog.TryGet(0, 2, out _), "invalid slot loaded");
    Assert(errors.Count == 1, "invalid slot error not isolated");
}

static void TestSchemaOneRejected()
{
    using Fixture fixture = Fixture.Create(schemaVersion: 1);
    ExpectFailure(() => new MissionLoader().Load(fixture.JsonPath, 1, 1), "schema 1 was accepted");
}

static void TestModSettingsHash()
{
    using Fixture first = Fixture.Create(startGold: 500);
    using Fixture second = Fixture.Create(startGold: 750);
    Assert(new MissionLoader().Load(first.JsonPath, 1, 1).Hash != new MissionLoader().Load(second.JsonPath, 1, 1).Hash,
        "modSettings did not affect the canonical mission hash");
}

static void TestInvalidModSettings()
{
    using Fixture fixture = Fixture.Create();
    string json = File.ReadAllText(fixture.JsonPath).Replace("\"schemaVersion\":1,\"mods\"", "\"schemaVersion\":99,\"mods\"");
    File.WriteAllText(fixture.JsonPath, json, new UTF8Encoding(false));
    LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
    Assert(!string.IsNullOrWhiteSpace(loaded.Definition.ModSettingsError), "invalid block was not reported");
    Assert(loaded.Definition.ModSettings.Mods.Values.All(entry => !entry.Enabled), "invalid block was partially retained");
}

static void TestCanonicalModSettingsHash()
{
    using Fixture first = Fixture.Create();
    using Fixture second = Fixture.Create();
    var firstSettings = new Dictionary<string, object> { ["Z"] = true, ["A"] = 5 };
    var secondSettings = new Dictionary<string, object> { ["A"] = 5, ["Z"] = true };
    RewriteStartConditionsSettings(first.JsonPath, firstSettings);
    RewriteStartConditionsSettings(second.JsonPath, secondSettings);
    Assert(new MissionLoader().Load(first.JsonPath, 1, 1).Hash == new MissionLoader().Load(second.JsonPath, 1, 1).Hash,
        "dictionary insertion order changed the hash");
}

static void RewriteStartConditionsSettings(string path, Dictionary<string, object> values)
{
    var serializer = new DataContractJsonSerializer(typeof(CoopMissionDefinition), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
    CoopMissionDefinition mission;
    using (FileStream input = File.OpenRead(path)) mission = (CoopMissionDefinition)serializer.ReadObject(input);
    mission.ModSettings.Mods["StartConditions_Serp"].Settings = values;
    using (FileStream output = File.Create(path)) serializer.WriteObject(output, mission);
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

static void TestAssetHash()
{
    byte[] json = Encoding.UTF8.GetBytes("{}");
    string first = MissionHash.Compute(json, new[] { new MissionAsset("map", new byte[] { 1 }) });
    string second = MissionHash.Compute(json, new[] { new MissionAsset("map", new byte[] { 2 }) });
    Assert(first != second, "asset mismatch did not change the mission hash");
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
    string root = Path.Combine(fixture.Root, "CoopTrails");
    string trail = Path.Combine(root, "Trail4");
    Directory.CreateDirectory(trail);
    File.Copy(fixture.JsonPath, Path.Combine(trail, "10.coopmission.json"));
    File.Copy(Path.Combine(fixture.Root, "map.map"), Path.Combine(trail, "map.map"));
    File.Copy(Path.Combine(fixture.Root, "lord.lordjson"), Path.Combine(trail, "lord.lordjson"));
    File.Copy(Path.Combine(fixture.Root, "castle.aivjson"), Path.Combine(trail, "castle.aivjson"));
    var catalog = new MissionCatalog();
    catalog.Load(root, null, null);
    Assert(catalog.TryGet(3, 10, out _), "Trail4 mission 10 was not loaded");
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

    public static Fixture Create(int aivRotation = 90, int? secondAivRotation = null, int preferredAiv = -1, int schemaVersion = 2, int startGold = 500)
    {
        string root = Path.Combine(Path.GetTempPath(), "CoopTrailReplacerTests", Guid.NewGuid().ToString("N"));
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
        if (secondAivRotation.HasValue)
            definition.Players[2].Aivs.Add(new AivReference { Source = "bundled", File = "castle.aivjson", Rotation = secondAivRotation.Value });
        string jsonPath = Path.Combine(root, "01.coopmission.json");
        var serializer = new DataContractJsonSerializer(typeof(CoopMissionDefinition), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        using (FileStream stream = File.Create(jsonPath))
            serializer.WriteObject(stream, definition);
        return new Fixture { Root = root, JsonPath = jsonPath };
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
    }
}
