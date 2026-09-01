using ExtremePowers.API;
using MessagePack;
using System;
using System.IO;
using System.Linq;

internal static class Program
{
    private static int failures;
    private static void Main(string[] args)
    {
        TestValidation(); TestRegistrationAndRestore(); TestAccumulator(); TestTargeting(); TestPacket(); TestCompatibility(); TestSafety(); TestOperationDedupe(); TestArchitecture();
        if (args.Length == 1 && File.Exists(args[0])) TestBuildGuard(args[0]);
        if (failures != 0) throw new Exception(failures + " ExtremePowers API test(s) failed.");
        Console.WriteLine("ExtremePowers API tests passed.");
    }
    private static void TestValidation()
    {
        var api = ExtremePowersBootstrap.Initialize(null); var t = api.Current; t.RegenerationPercent = 1001; Throws(() => api.Apply(t), "regen upper bound");
        t = api.Current; t.Spearmen.UnitType = 0; Throws(() => api.Apply(t), "NULL unit rejection");
        t = api.Current; t.Spearmen.UnitType = ExtremePowerSafety.UnitTypeEndSentinel; Throws(() => api.Apply(t), "unit sentinel rejection");
    }
    private static void TestRegistrationAndRestore()
    {
        var api = ExtremePowersBootstrap.Instance; int called = 0; var replacement = new ExtremePowerReplacement("test", "", "", ExtremePowerTargetKind.None, (in ExtremePowerExecutionContext c, out string r) => { r = null; return true; }, (in ExtremePowerExecutionContext c) => called++);
        using (api.RegisterReplacement(ExtremePowerId.Gold, replacement)) { Throws(() => api.RegisterReplacement(ExtremePowerId.Gold, replacement), "exclusive registration"); Check(api.TryExecuteReplacement(new ExtremePowerExecutionContext(ExtremePowerId.Gold, 1, ExtremePowerTarget.None, 1, 1), out _), "execute"); Check(!api.TryExecuteReplacement(new ExtremePowerExecutionContext(ExtremePowerId.Gold, 0, ExtremePowerTarget.None, 2, 1), out _), "invalid player rejection"); }
        Check(called == 1 && !api.TryGetReplacement(ExtremePowerId.Gold, out _), "registration dispose"); var t = api.Current; t.Costs[0] = 1; api.Apply(t); api.RestoreVanilla(); Check(api.Current.Costs[0] == 636, "restore");
        var rejected = new ExtremePowerReplacement("reject", "", "", ExtremePowerTargetKind.None, (in ExtremePowerExecutionContext c, out string r) => { r = "expected"; return false; }, (in ExtremePowerExecutionContext c) => called++);
        using (api.RegisterReplacement(ExtremePowerId.Gold, rejected)) Check(!api.TryExecuteReplacement(new ExtremePowerExecutionContext(ExtremePowerId.Gold, 1, ExtremePowerTarget.None, 3, 1), out string reason) && reason == "expected", "CanExecute rejection reason");
        var throwing = new ExtremePowerReplacement("throw", "", "", ExtremePowerTargetKind.None, (in ExtremePowerExecutionContext c, out string r) => { r = null; return true; }, (in ExtremePowerExecutionContext c) => throw new InvalidOperationException("boom"));
        using (api.RegisterReplacement(ExtremePowerId.Gold, throwing)) Check(!api.TryExecuteReplacement(new ExtremePowerExecutionContext(ExtremePowerId.Gold, 1, ExtremePowerTarget.None, 4, 1), out string reason) && reason.Contains("boom"), "callback exception rejection");
        var unit = new ExtremePowerReplacement("unit", "", "", ExtremePowerTargetKind.Unit, (in ExtremePowerExecutionContext c, out string r) => { r = null; return true; }, (in ExtremePowerExecutionContext c) => { });
        ThrowsAny(() => api.RegisterReplacement(ExtremePowerId.Heal, unit), "unsupported unit replacement rejection");
    }
    private static void TestAccumulator()
    {
        var zero = new RegenerationAccumulator(); Check(zero.TryScaleConfirmedIncrement(100, 101, 0, 7000, out uint value) && value == 100, "regen 0%");
        var half = new RegenerationAccumulator(); Check(half.TryScaleConfirmedIncrement(100, 101, 50, 7000, out value) && value == 100 && half.TryScaleConfirmedIncrement(100, 101, 50, 7000, out value) && value == 101, "regen 50% remainder");
        var vanilla = new RegenerationAccumulator(); Check(vanilla.TryScaleConfirmedIncrement(100, 101, 100, 7000, out value) && value == 101, "regen 100%");
        var fast = new RegenerationAccumulator(); Check(fast.TryScaleConfirmedIncrement(100, 101, 1000, 7000, out value) && value == 110, "regen 1000%");
        Check(fast.TryScaleConfirmedIncrement(6999, 7000, 1000, 7000, out value) && value == 7000, "regen cap");
        Check(!fast.TryScaleConfirmedIncrement(100, 105, 1000, 7000, out value) && value == 105, "external delta unchanged");
    }
    private static void TestTargeting() { Check(ExtremePowerTargetValidator.IsValid(ExtremePowerTarget.None), "none target"); Check(ExtremePowerTargetValidator.IsValid(ExtremePowerTarget.MapPoint(0)), "map target"); Check(!ExtremePowerTargetValidator.IsValid(ExtremePowerTarget.Unit(0)), "unit target invalid id"); }
    private static void TestPacket()
    {
        var source = new ExtremePowerChore(1, ExtremePowerId.Heal, 2, ExtremePowerTarget.Unit(42), 123);
        byte[] bytes = ExtremePowerChoreCodec.Serialize(source);
        Check(bytes.Length == 23 && ExtremePowerChoreCodec.TryDeserialize(bytes, out var parsed) && parsed.Target.UnitId == 42 && parsed.OperationId == 123, "binary packet roundtrip");
        bytes[0] = 99; Check(!ExtremePowerChoreCodec.TryDeserialize(bytes, out _), "binary packet protocol rejection");
        byte[] messagePack = MessagePackSerializer.Serialize(source);
        ExtremePowerChore unpacked = MessagePackSerializer.Deserialize<ExtremePowerChore>(messagePack);
        Check(unpacked.Power == source.Power && unpacked.PlayerId == 2 && unpacked.Target.UnitId == 42 && unpacked.OperationId == 123, "MessagePack formatter roundtrip");
        byte[] malformed = (byte[])messagePack.Clone(); malformed[0] = 0x96;
        ThrowsAny(() => MessagePackSerializer.Deserialize<ExtremePowerChore>(malformed), "MessagePack field-count rejection");
    }
    private static void TestCompatibility()
    {
        string token = ExtremePowersCompatibility.CreateToken("1", "HASH", true, 1113);
        Check(ExtremePowersCompatibility.EvaluateSession(false, token, null, null).Ready, "singleplayer readiness without report");
        string[] reports = new string[9]; reports[1] = token; reports[2] = token;
        Check(ExtremePowersCompatibility.EvaluateSession(true, token, reports, new[] { 1, 2 }).Ready, "multiplayer matching reports");
        reports[2] = null; Check(!ExtremePowersCompatibility.EvaluateSession(true, token, reports, new[] { 1, 2 }).Ready, "multiplayer missing report");
        reports[2] = ExtremePowersCompatibility.CreateToken("2", "HASH", true, 1113); Check(!ExtremePowersCompatibility.EvaluateSession(true, token, reports, new[] { 1, 2 }).Ready, "protocol mismatch");
        reports[2] = ExtremePowersCompatibility.CreateToken("1", "OTHER", true, 1113); Check(!ExtremePowersCompatibility.EvaluateSession(true, token, reports, new[] { 1, 2 }).Ready, "DLL mismatch");
        reports[2] = ExtremePowersCompatibility.CreateToken("1", "HASH", false, 1113); Check(!ExtremePowersCompatibility.EvaluateSession(true, token, reports, new[] { 1, 2 }).Ready, "backend mismatch");
        reports[2] = ExtremePowersCompatibility.CreateToken("1", "HASH", true, 1114); Check(!ExtremePowersCompatibility.EvaluateSession(true, token, reports, new[] { 1, 2 }).Ready, "packet id mismatch");
    }
    private static void TestSafety()
    {
        Check(ExtremePowerSafety.TryCompensateMana(100, 50, 636, out uint value) && value == 686, "mana compensation");
        Check(!ExtremePowerSafety.TryCompensateMana(uint.MaxValue, 0, 636, out _), "mana compensation overflow");
        Check(ExtremePowerSafety.SaturatingAdd(uint.MaxValue - 2, 10) == uint.MaxValue, "gold saturation");
        Check(ExtremePowerSafety.IsSpawnableUnitType(1) && !ExtremePowerSafety.IsSpawnableUnitType(0) && !ExtremePowerSafety.IsSpawnableUnitType(90), "unit type range");
    }
    private static void TestOperationDedupe()
    {
        var tracker = new ExtremePowerOperationTracker(); Check(tracker.TryBegin(1, 42) && !tracker.TryBegin(1, 42) && tracker.TryBegin(2, 42), "operation deduplication per player");
        tracker.Reset(); Check(tracker.TryBegin(1, 42), "operation dedupe reset on map unload");
    }
    private static void TestArchitecture()
    {
        string modRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
        string apiRoot = Path.Combine(modRoot, "api");
        string[] forbidden = { "ExtremePowers.Settings", "ExtremePowers.Demo", "SerpLocalization", "Shared.", ".xaml", "Locales" };
        foreach (string file in Directory.GetFiles(apiRoot, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file); Check(!forbidden.Any(text.Contains), "architecture " + file);
        }
        string project = File.ReadAllText(Path.Combine(modRoot, "ExtremePowers.API.csproj"));
        Check(!project.Contains("Include=\"src\\") && !project.Contains("Include=\"Locales\\") && !project.Contains("Include=\"Override\\") && !project.Contains("Include=\"Patches\\") && !project.Contains("Include=\"..\\Shared\\"), "extractable API project inputs");
        foreach (string line in project.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(value => value.Contains("<Compile Include=")))
            Check(line.Contains("Include=\"api\\"), "all API compile inputs live below api/");
        string integrationAdapter = Path.GetFullPath(Path.Combine(modRoot, "src", "Integration", "LocalExtremePowersApiClient.cs"));
        foreach (string file in Directory.GetFiles(Path.Combine(modRoot, "src"), "*.cs", SearchOption.AllDirectories).Where(value => File.ReadAllText(value).Contains("ExtremePowers.API")))
            Check(string.Equals(Path.GetFullPath(file), integrationAdapter, StringComparison.OrdinalIgnoreCase), "API types are isolated to the replaceable adapter: " + file);
        string adapterText = File.ReadAllText(integrationAdapter);
        string settingsText = File.ReadAllText(Path.Combine(modRoot, "src", "Settings", "ExtremePowersSettings.cs"));
        string settingsXaml = File.ReadAllText(Path.Combine(modRoot, "Override", "ScriptExtenderUI", "ExtremePowersSettings.xaml"));
        Check(adapterText.Contains("api.Vanilla.Costs.Clone()") && !adapterText.Contains("s.ArrowCost"), "example adapter always keeps Vanilla costs");
        Check(!settingsText.Contains("ArrowCost") && !settingsXaml.Contains("extreme-powers.costs"), "example cost settings remain removed");
    }
    private static void TestBuildGuard(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Check(ExtremePowersBuildCompatibility.IsSupportedImage(bytes), "canonical DLL hash");
        Check(ExtremePowersBuildCompatibility.HasExpectedNativeSignatures(bytes), "canonical native signatures");
        byte[] tamperedSignature = (byte[])bytes.Clone();
        byte[] dispatcher = { 0x48,0x89,0x5C,0x24,0x10,0x48,0x89,0x6C,0x24,0x18,0x48,0x89,0x74,0x24,0x20,0x57,0x48,0x83,0xEC,0x40 };
        int position = Find(tamperedSignature, dispatcher); Check(position >= 0, "dispatcher signature located in PE");
        if (position >= 0) { tamperedSignature[position] ^= 1; Check(!ExtremePowersBuildCompatibility.HasExpectedNativeSignatures(tamperedSignature), "tampered signature rejection"); }
        byte[] tamperedRegen = (byte[])bytes.Clone(); byte[] regen = { 0x45,0x8B,0x88,0x50,0x39,0x00,0x00,0x41,0x81,0xF9,0x58,0x1B,0x00,0x00,0x7D,0x35 };
        position = Find(tamperedRegen, regen); Check(position >= 0, "regeneration signature located in PE");
        if (position >= 0) { tamperedRegen[position] ^= 1; Check(!ExtremePowersBuildCompatibility.HasExpectedNativeSignatures(tamperedRegen), "tampered regeneration signature rejection"); }
        CheckTamperedNativeSignature(bytes, new byte[] { 0x89,0x1D,0xA7,0x7F,0xFA,0x05,0xFF,0xCB,0xC7,0x05,0x93,0x7F,0xFA,0x05,0x05,0x00,0x00,0x00 }, "pending target writer");
        CheckTamperedNativeSignature(bytes, new byte[] { 0x8B,0x3D,0x7C,0x0A,0x02,0x06,0x4C,0x8D,0x15,0xB1,0x22,0xFD,0x03 }, "pending target reader");
        CheckTamperedNativeSignature(bytes, new byte[] { 0xB2,0x77,0x89,0x3D,0x1A,0x45,0x63,0x08,0x48,0x8D,0x0D,0x07,0x75,0x4E,0x08,0x44,0x89,0x25,0x10,0x45,0x63,0x08,0x44,0x89,0x3D,0x0D,0x45,0x63,0x08 }, "pending target chore transfer");
        bytes[0] ^= 1; Check(!ExtremePowersBuildCompatibility.IsSupportedImage(bytes), "tampered DLL rejection");
    }
    private static void CheckTamperedNativeSignature(byte[] source, byte[] signature, string name)
    {
        byte[] tampered = (byte[])source.Clone();
        int position = Find(tampered, signature);
        Check(position >= 0, name + " signature located in PE");
        if (position >= 0) { tampered[position] ^= 1; Check(!ExtremePowersBuildCompatibility.HasExpectedNativeSignatures(tampered), "tampered " + name + " rejection"); }
    }
    private static int Find(byte[] haystack, byte[] needle) { for (int i = 0; i <= haystack.Length - needle.Length; i++) { int j = 0; while (j < needle.Length && haystack[i + j] == needle[j]) j++; if (j == needle.Length) return i; } return -1; }
    private static void Check(bool condition, string name) { if (!condition) { failures++; Console.Error.WriteLine("FAIL: " + name); } }
    private static void Throws(Action action, string name) { try { action(); Check(false, name); } catch (ArgumentException) { } catch (InvalidOperationException) { } }
    private static void ThrowsAny(Action action, string name) { try { action(); Check(false, name); } catch { } }
}
