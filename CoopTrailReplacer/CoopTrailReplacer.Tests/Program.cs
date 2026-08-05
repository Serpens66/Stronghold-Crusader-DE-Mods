using CoopTrailReplacer.Core;
using System.Runtime.Serialization.Json;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("bundled mission load and stable hash", TestBundledMission),
    ("path escape rejected", TestPathEscape),
    ("invalid rotation rejected", TestInvalidRotation),
    ("catalog keeps valid slots and ignores invalid slots", TestCatalogIsolation),
    ("amount serialization is deterministic", TestAmountSerialization),
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

static void TestAmountSerialization()
{
    string value = MissionLoader.SerializeAmounts(new Dictionary<string, int>
    {
        ["B"] = 2,
        ["A"] = 1,
    });
    Assert(value == "A=1\r\nB=2", value);
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

    public static Fixture Create(int aivRotation = 90, int? secondAivRotation = null, int preferredAiv = -1)
    {
        string root = Path.Combine(Path.GetTempPath(), "CoopTrailReplacerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "map.map"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(root, "lord.lordjson"), "{\"lord\":{}}", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "castle.aivjson"), "{}", new UTF8Encoding(false));

        var definition = new CoopMissionDefinition
        {
            SchemaVersion = 1,
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
