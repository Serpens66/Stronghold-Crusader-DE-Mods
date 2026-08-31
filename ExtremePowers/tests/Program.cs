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
        TestValidation(); TestRegistrationAndRestore(); TestAccumulator(); TestTargeting(); TestPacket(); TestArchitecture();
        if (args.Length == 1 && File.Exists(args[0])) TestBuildGuard(args[0]);
        if (failures != 0) throw new Exception(failures + " ExtremePowers API test(s) failed.");
        Console.WriteLine("ExtremePowers API tests passed.");
    }
    private static void TestValidation() { var api = ExtremePowersBootstrap.Initialize(null); var t = api.Current; t.RegenerationPercent = 1001; Throws(() => api.Apply(t), "regen upper bound"); }
    private static void TestRegistrationAndRestore()
    {
        var api = ExtremePowersBootstrap.Instance; int called = 0; var replacement = new ExtremePowerReplacement("test", "", "", ExtremePowerTargetKind.None, (in ExtremePowerExecutionContext c, out string r) => { r = null; return true; }, (in ExtremePowerExecutionContext c) => called++);
        using (api.RegisterReplacement(ExtremePowerId.Gold, replacement)) { Throws(() => api.RegisterReplacement(ExtremePowerId.Gold, replacement), "exclusive registration"); Check(api.TryExecuteReplacement(new ExtremePowerExecutionContext(ExtremePowerId.Gold, 1, ExtremePowerTarget.None, 1, 1), out _), "execute"); Check(!api.TryExecuteReplacement(new ExtremePowerExecutionContext(ExtremePowerId.Gold, 0, ExtremePowerTarget.None, 2, 1), out _), "invalid player rejection"); }
        Check(called == 1 && !api.TryGetReplacement(ExtremePowerId.Gold, out _), "registration dispose"); var t = api.Current; t.Costs[0] = 1; api.Apply(t); api.RestoreVanilla(); Check(api.Current.Costs[0] == 636, "restore");
    }
    private static void TestAccumulator() { var a = new RegenerationAccumulator(); Check(a.ScaleDelta(1, 50) == 0 && a.ScaleDelta(1, 50) == 1, "accumulator remainder"); Check(a.ScaleDelta(2, 1000) == 20, "accumulator 1000%"); }
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
    private static void TestArchitecture()
    {
        string modRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
        string apiRoot = Path.Combine(modRoot, "api");
        string[] forbidden = { "ExtremePowers.Settings", "ExtremePowers.Demo", "SerpLocalization", ".xaml", "Locales" };
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
        bytes[0] ^= 1; Check(!ExtremePowersBuildCompatibility.IsSupportedImage(bytes), "tampered DLL rejection");
    }
    private static int Find(byte[] haystack, byte[] needle) { for (int i = 0; i <= haystack.Length - needle.Length; i++) { int j = 0; while (j < needle.Length && haystack[i + j] == needle[j]) j++; if (j == needle.Length) return i; } return -1; }
    private static void Check(bool condition, string name) { if (!condition) { failures++; Console.Error.WriteLine("FAIL: " + name); } }
    private static void Throws(Action action, string name) { try { action(); Check(false, name); } catch (ArgumentException) { } catch (InvalidOperationException) { } }
    private static void ThrowsAny(Action action, string name) { try { action(); Check(false, name); } catch { } }
}
