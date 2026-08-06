using TrailEditor.Core;

string realTrail = RealTrailPath();
string customTrail = CustomTrailPath();

if (!File.Exists(realTrail) || !File.Exists(customTrail))
{
    Console.Error.WriteLine($"Test fixture is missing: {(!File.Exists(realTrail) ? realTrail : customTrail)}");
    return 1;
}

var tests = new List<(string Name, Action Body)>
{
    ("Setup -12 round-trip", TestSetupRoundTrip),
    ("Hidden 500-gold setup level round-trip", TestHiddenLowGoldLevel),
    ("Hidden setup semantics and validation", TestHiddenSetupSemantics),
    ("Real trail byte-identical container round-trip", TestRealContainerRoundTrip),
    ("Real trail bundle round-trip", TestRealBundleRoundTrip),
    ("Game-created custom lord trail round-trip", TestRealCustomTrailRoundTrip),
    ("Custom AIV/AIC bundle round-trip", TestCustomDataRoundTrip),
    ("Custom lord config version 1 defaults", TestLordConfigVersion1),
    ("Reject unknown restart version and bundle path escape", TestValidationFailures)
};

int failed = 0;
foreach ((string name, Action body) in tests)
{
    try
    {
        body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {ex}");
    }
}
Console.WriteLine($"{tests.Count - failed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

static void TestSetupRoundTrip()
{
    TrailData data = LoadReal();
    string text = RestartCodec.EncodeSetup(data.Setup);
    Assert(text.StartsWith("-12,", StringComparison.Ordinal), "setup version");
    MultiplayerSetupData parsed = RestartCodec.DecodeSetup(text);
    Assert(RestartCodec.EncodeSetup(parsed) == text, "setup text changed");
}

static void TestHiddenLowGoldLevel()
{
    TrailData data = LoadReal();
    data.Setup.StartingGoodsLevel = 4;
    data.Setup.Fairness = 3;

    MultiplayerSetupData parsed = RestartCodec.DecodeSetup(RestartCodec.EncodeSetup(data.Setup));
    Assert(parsed.StartingGoodsLevel == 4, "hidden starting-goods level changed");
    Assert(parsed.Fairness == 3, "fairness changed");
}

static void TestHiddenSetupSemantics()
{
    TrailData data = LoadReal();
    data.Setup.StartingGoodsLevel = 4;
    data.Setup.Fairness = 3;
    data.Setup.NoGold = 0;
    data.CustomisedExtremeTrail = false;
    StartingGoldValues normal = SetupSemantics.GetStartingGold(data);
    Assert(normal.Human == 500 && normal.Computer == 500 && normal.Multiplier == 1, "hidden low-gold values");

    data.CustomisedExtremeTrail = true;
    StartingGoldValues extreme = SetupSemantics.GetStartingGold(data);
    Assert(extreme.Human == 1500 && extreme.Computer == 1500 && extreme.Multiplier == 3, "Extreme multiplier");

    data.Setup.StartingGoodsLevel = 5;
    AssertThrows(() => RestartCodec.Encode(data), "unsafe starting-goods level was accepted");
}

static void TestRealContainerRoundTrip()
{
    TrailContainerDocument original = TrailContainerCodec.ReadTrail(RealTrailPath());
    TrailData decoded = RestartCodec.Decode(original.RestartData);
    Assert(decoded.FormatVersion == 60, "restart version");
    Assert(decoded.Map.FileName == "Target Zone", "map name");
    Assert(original.Map.Directory?.DirectoryTag == 4036, "directory tag");
    Assert(original.Map.Directory?.Capacity == 200, "directory capacity");
    byte[] encoded = RestartCodec.Encode(decoded);
    Assert(encoded.SequenceEqual(original.RestartData), "restart codec is not byte-identical");
    byte[] rebuilt = TrailContainerCodec.BuildTrail(TrailContainerCodec.ExtractMap(original), encoded);
    Assert(rebuilt.SequenceEqual(original.Bytes), "container is not byte-identical");
}

static void TestRealBundleRoundTrip()
{
    string root = NewTempDirectory();
    try
    {
        string bundle = Path.Combine(root, "mission");
        var service = new BundleService();
        service.Export(RealTrailPath(), bundle);
        byte[] rebuilt = service.Build(Path.Combine(bundle, "trail.json"));
        byte[] original = File.ReadAllBytes(RealTrailPath());
        Assert(rebuilt.SequenceEqual(original), "bundle round-trip is not byte-identical");
        Assert(File.ReadAllText(Path.Combine(bundle, "trail.json")).Contains("\r\n"), "JSON is not CRLF");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void TestRealCustomTrailRoundTrip()
{
    TrailContainerDocument original = TrailContainerCodec.ReadTrail(CustomTrailPath());
    TrailData decoded = RestartCodec.Decode(original.RestartData);
    TrailAiSlot[] customSlots = decoded.AiSlots.Where(slot => !slot.BuiltInLord).ToArray();
    Assert(customSlots.Length > 0, "real custom lord slot was not decoded");
    foreach (TrailAiSlot customSlot in customSlots)
    {
        Assert(customSlot.LordConfig?.ConfigVersion == 2, "real custom lord config version");
        Assert(customSlot.Aivs.Count > 0, "real custom AIV was not decoded");
    }
    Assert(RestartCodec.Encode(decoded).SequenceEqual(original.RestartData), "real custom restart is not byte-identical");

    string root = NewTempDirectory();
    try
    {
        string bundle = Path.Combine(root, "custom-real");
        var service = new BundleService();
        service.Export(CustomTrailPath(), bundle);
        byte[] rebuilt = service.Build(Path.Combine(bundle, "trail.json"));
        Assert(rebuilt.SequenceEqual(original.Bytes), "real custom bundle round-trip is not byte-identical");
        string[] internalsFiles = Directory.GetFiles(Path.Combine(bundle, "lords"), "*.internals.json");
        Assert(internalsFiles.Length == customSlots.Length, "custom lord internals file count");
        foreach (string internalsFile in internalsFiles)
        {
            string internals = File.ReadAllText(internalsFile);
            Assert(internals.Contains("opponent_type_for_speech", StringComparison.Ordinal), "speech field was not preserved");
            Assert(!internals.Contains("extendedLordParent", StringComparison.Ordinal), "runtime-only parent field was exported");
            Assert(!internals.Contains("free04", StringComparison.Ordinal), "runtime-only free field was exported");
        }
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void TestCustomDataRoundTrip()
{
    TrailContainerDocument original = TrailContainerCodec.ReadTrail(RealTrailPath());
    TrailData data = RestartCodec.Decode(original.RestartData);
    TrailAiSlot slot = data.AiSlots[0];
    slot.Aivs.Add(new CustomAivData
    {
        LordType = 0,
        Name = "RoundTripAiv",
        Data = new short[] { 10, 1, 0, 1, 150, 5050, 0 }
    });
    slot.Aivs.Add(new CustomAivData
    {
        LordType = 0,
        BuiltIn = true,
        Checksum = 51,
        Name = "BuiltInCommunityAiv",
        Data = new short[] { 10, 1, 0, 1, 150, 5050, 0 }
    });
    var config = new SHCDESE.AICDecoder.InternalAIC
    {
        opponent_type = 16,
        opponent_type_for_speech = 7,
        lord_gfx_type = 3,
        extendedLordParent = 123456,
        siege_max_troops = 260,
        siege_normal_wave_multiplier = 9,
        siege_high_gold_wave_multiplier = 25,
        free04 = 654321
    };
    slot.BuiltInLord = false;
    slot.LordConfig = new CustomLordData
    {
        LordType = 0,
        Name = "RoundTripLord",
        Config = config
    };
    byte[] encodedRestart = RestartCodec.Encode(data);
    TrailData decodedRestart = RestartCodec.Decode(encodedRestart);
    Assert(decodedRestart.AiSlots[0].Aivs[1].Checksum == 51, "built-in AIV catalogue checksum changed");
    SHCDESE.AICDecoder.InternalAIC decodedConfig = decodedRestart.AiSlots[0].LordConfig!.Config;
    Assert(decodedConfig.opponent_type_for_speech == 7, "speech opponent type changed");
    Assert(decodedConfig.siege_max_troops == 260, "siege maximum shifted");
    Assert(decodedConfig.siege_normal_wave_multiplier == 9, "normal siege multiplier shifted");
    Assert(decodedConfig.siege_high_gold_wave_multiplier == 25, "high-gold siege multiplier shifted");
    Assert(decodedConfig.extendedLordParent == 0, "runtime-only parent field entered the trail payload");
    Assert(decodedConfig.free04 == 0, "runtime-only free field entered the trail payload");
    byte[] synthetic = TrailContainerCodec.BuildTrail(TrailContainerCodec.ExtractMap(original), encodedRestart);
    string root = NewTempDirectory();
    try
    {
        string trailPath = Path.Combine(root, "custom.trail");
        File.WriteAllBytes(trailPath, synthetic);
        string bundle = Path.Combine(root, "custom");
        var service = new BundleService();
        service.Export(trailPath, bundle);
        byte[] rebuilt = service.Build(Path.Combine(bundle, "trail.json"));
        Assert(rebuilt.SequenceEqual(synthetic), "custom bundle round-trip is not byte-identical");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void TestLordConfigVersion1()
{
    TrailData data = LoadReal();
    TrailAiSlot slot = data.AiSlots[0];
    slot.BuiltInLord = false;
    slot.LordConfig = new CustomLordData
    {
        LordType = 0,
        Name = "VersionOneLord",
        ConfigVersion = 1,
        Config = new SHCDESE.AICDecoder.InternalAIC
        {
            opponent_type = 3,
            opponent_type_for_speech = 4,
            siege_max_troops = 999,
            siege_normal_wave_multiplier = 998,
            siege_high_gold_wave_multiplier = 997
        }
    };

    byte[] version1 = RestartCodec.Encode(data);
    TrailData decoded = RestartCodec.Decode(version1);
    SHCDESE.AICDecoder.InternalAIC config = decoded.AiSlots[0].LordConfig!.Config;
    Assert(decoded.AiSlots[0].LordConfig!.ConfigVersion == 1, "config version 1 changed");
    Assert(config.opponent_type_for_speech == 4, "version 1 speech opponent type changed");
    Assert(config.siege_max_troops == 200, "version 1 siege maximum default");
    Assert(config.siege_normal_wave_multiplier == 5, "version 1 normal siege multiplier default");
    Assert(config.siege_high_gold_wave_multiplier == 7, "version 1 high-gold siege multiplier default");

    slot.LordConfig.ConfigVersion = 2;
    byte[] version2 = RestartCodec.Encode(data);
    Assert(version2.Length == version1.Length + 12, "config version sizes do not differ by 12 bytes");
}

static void TestValidationFailures()
{
    byte[] restart = TrailContainerCodec.ReadTrail(RealTrailPath()).RestartData;
    restart[0] = 59;
    AssertThrows(() => RestartCodec.Decode(restart), "unknown restart version was accepted");
    string root = NewTempDirectory();
    try
    {
        AssertThrows(() => BundleService.ResolveBundlePath(root, "../escape.bin"), "bundle path escape was accepted");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static TrailData LoadReal() => RestartCodec.Decode(TrailContainerCodec.ReadTrail(RealTrailPath()).RestartData);

static string RealTrailPath() => Path.Combine(AppContext.BaseDirectory, "TestData", "Trail_Mission_1.trail");

static string CustomTrailPath() => Path.Combine(AppContext.BaseDirectory, "TestData", "Custom_Lord_Trail.trail");

static string NewTempDirectory()
{
    string path = Path.Combine(Path.GetTempPath(), "TrailEditorTests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertThrows(Action action, string message)
{
    try
    {
        action();
    }
    catch (Exception)
    {
        return;
    }
    throw new InvalidOperationException(message);
}
