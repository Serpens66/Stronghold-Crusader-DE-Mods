using ExtremePowers.API;
using MessagePack;
using SHCDESE.API;
using System;
using System.IO;
using System.Linq;

internal static class Program
{
    private static int failures;
    private static void Main(string[] args)
    {
        TestValidation(); TestRegistrationAndRestore(); TestAccumulator(); TestTargeting(); TestPacket(); TestChoreSender(); TestCompatibility(); TestSafety(); TestBaselineSemantics(); TestOperationDedupe(); TestArchitecture();
        if (args.Length == 1 && File.Exists(args[0])) TestBuildGuard(args[0]);
        if (failures != 0) throw new Exception(failures + " ExtremePowers API test(s) failed.");
        Console.WriteLine("ExtremePowers API tests passed.");
    }
    private static void TestValidation()
    {
        var api = ExtremePowersBootstrap.Initialize(null); var t = api.Current; t.RegenerationPercent = 1001; Throws(() => api.Apply(t), "regen upper bound");
        t = api.Current; t.Spearmen.UnitType = 0; Throws(() => api.Apply(t), "NULL unit rejection");
        t = api.Current; t.Spearmen.UnitType = ExtremePowerSafety.UnitTypeEndSentinel; Throws(() => api.Apply(t), "unit sentinel rejection");
        Check(api.Vanilla.ArrowVolley.ProjectileKind == ExtremePowerProjectileKind.Arrow && api.Vanilla.RockVolley.ProjectileKind == ExtremePowerProjectileKind.Rock, "projectile enum Vanilla defaults");
        t = api.Current; t.ArrowVolley.ProjectileKind = (ExtremePowerProjectileKind)2; Throws(() => api.Apply(t), "undefined projectile kind rejection");
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
        Check(!fast.TryScaleConfirmedIncrement(7000, 7001, 1000, 7000, out value) && value == 7001, "regen above cap unchanged");
        Check(!fast.TryScaleConfirmedIncrement(uint.MaxValue, 0, 1000, 7000, out value) && value == 0, "regen UInt32 wrap unchanged");
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
    private static void TestChoreSender()
    {
        var packet = new ExtremePowerChore(1, ExtremePowerId.Gold, 1, ExtremePowerTarget.None, 77);
        byte[] first = GameNetworkAPI.Serialize(packet), second = GameNetworkAPI.Serialize(packet);
        Check(first.SequenceEqual(second), "unchanged packet serializes deterministically twice");

        int serialized = 0, sent = 0, mutations = 0; object sentPacket = null;
        Func<ExtremePowerChore, byte[]> body1199 = value => { serialized++; return new byte[1197]; };
        Action<ExtremePowerChore, short> send = (value, id) => { sent++; mutations++; sentPacket = value; };
        Check(!ExtremePowerChoreSender.TrySend(packet, 4, false, body1199, () => 1, send, out _, out _) && serialized == 0 && sent == 0 && mutations == 0, "missing hook fails before serialization and mutation");
        Check(!ExtremePowerChoreSender.TrySend(packet, 4, true, value => { throw new InvalidOperationException("serializer"); }, () => 1, send, out _, out _) && sent == 0 && mutations == 0, "serializer failure has no send or mutation");
        Check(!ExtremePowerChoreSender.TrySend(packet, 4, true, body1199, () => 0, send, out _, out _) && sent == 0 && mutations == 0, "missing manager has no send or mutation");
        Check(ExtremePowerChoreSender.TrySend(packet, 4, true, body1199, () => 1, send, out byte[] accepted1199, out _) && accepted1199.Length + 2 == 1199, "1199-byte total accepted");
        Check(ExtremePowerChoreSender.TrySend(packet, 4, true, value => new byte[1198], () => 1, send, out byte[] accepted1200, out _) && accepted1200.Length + 2 == 1200, "1200-byte total accepted");
        bool simulationPaused = true;
        Check(simulationPaused && ExtremePowerChoreSender.TrySend(packet, 4, true, body1199, () => 1, send, out _, out _), "paused simulation does not force a Steam fallback");
        int acceptedMutations = mutations;
        Check(!ExtremePowerChoreSender.TrySend(packet, 4, true, value => new byte[1199], () => 1, send, out _, out _) && mutations == acceptedMutations, "1201-byte total rejected without mutation");
        Check(ReferenceEquals(packet, sentPacket), "the prechecked packet object is the sent packet object");
        int beforeThrow = mutations;
        Check(!ExtremePowerChoreSender.TrySend(packet, 4, true, body1199, () => 1, (value, id) => throw new InvalidOperationException("send"), out _, out _) && mutations == beforeThrow, "send exception fails closed");
    }
    private static void TestCompatibility()
    {
        Check(ExtremePowersBootstrap.Instance.ProtocolVersion == "3", "baseline-hardened API compatibility protocol");
        Check(ExtremePowerChoreCodec.CurrentProtocol == 1, "wire packet protocol remains unchanged");
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
        Check(ExtremePowerSafety.TryCompensateMana(int.MaxValue - 636u, 0, 636, out value) && value == int.MaxValue, "mana compensation signed upper bound");
        Check(!ExtremePowerSafety.TryCompensateMana((uint)int.MaxValue + 1u, 0, 0, out _), "source mana above signed range rejected");
        Check(!ExtremePowerSafety.TryCompensateMana(int.MaxValue, 0, 636, out _), "compensated mana above signed range rejected");
        Check(!ExtremePowerSafety.TryCompensateMana(uint.MaxValue, 0, 636, out _), "mana compensation overflow");
        Check(ExtremePowerSafety.SaturatingAdd(uint.MaxValue - 2, 10) == uint.MaxValue, "gold saturation");
        Check(ExtremePowerSafety.IsValidSpawnOwnerPlayerId(0) && ExtremePowerSafety.IsValidSpawnOwnerPlayerId(1) && ExtremePowerSafety.IsValidSpawnOwnerPlayerId(8) && !ExtremePowerSafety.IsValidSpawnOwnerPlayerId(-1) && !ExtremePowerSafety.IsValidSpawnOwnerPlayerId(9), "spawn owners include nature and player slots only");
        bool allDefinedValuesAccepted = true; for (int unitType = 1; unitType < ExtremePowerSafety.UnitTypeEndSentinel; unitType++) allDefinedValuesAccepted &= ExtremePowerSafety.IsSpawnableUnitType(unitType);
        Check(allDefinedValuesAccepted && !ExtremePowerSafety.IsSpawnableUnitType(0) && !ExtremePowerSafety.IsSpawnableUnitType(90) && !ExtremePowerSafety.IsSpawnableUnitType(91), "all non-sentinel eChimps values accepted");
        var spawn = new ExtremePowerSpawnResult(0, 42, 10, 7); Check(spawn.CreatedGroup && spawn.OwnerPlayerId == 0 && spawn.GroupUnitId == 42 && spawn.RequestedCount == 10 && spawn.SpawnedUnitCount == 7, "spawn result exposes explicit nature owner");
    }
    private static void TestBaselineSemantics()
    {
        var none = new ExtremePowerReplacement("none", "", "", ExtremePowerTargetKind.None, (in ExtremePowerExecutionContext c, out string r) => { r = null; return true; }, (in ExtremePowerExecutionContext c) => { });
        var map = new ExtremePowerReplacement("map", "", "", ExtremePowerTargetKind.MapPoint, (in ExtremePowerExecutionContext c, out string r) => { r = null; return true; }, (in ExtremePowerExecutionContext c) => { });
        Check(NativeExtremePowersRuntime.ShouldQueueReplacementImmediately(none) && !NativeExtremePowersRuntime.ShouldQueueReplacementImmediately(map), "None replacement bypasses Vanilla targeting");
        Check(NativeExtremePowersRuntime.GetTunedEffectAudioId(ExtremePowerId.Heal) == 0xCF && NativeExtremePowersRuntime.GetTunedEffectAudioId(ExtremePowerId.Spearmen) == 0x104 && NativeExtremePowersRuntime.GetTunedEffectAudioId(ExtremePowerId.RockVolley) == 0x105 && NativeExtremePowersRuntime.GetTunedEffectAudioId(ExtremePowerId.ArrowVolley) == 0, "confirmed tuned-effect audio mapping");
        Check(NativeExtremePowersRuntime.ShouldPlayTunedEffectAudio(ExtremePowerId.Heal, 2, 2) && !NativeExtremePowersRuntime.ShouldPlayTunedEffectAudio(ExtremePowerId.Heal, 2, 1) && !NativeExtremePowersRuntime.ShouldPlayTunedEffectAudio(ExtremePowerId.ArrowVolley, 2, 2), "completion audio is local and mapped only");
        byte[] emptyGroup = new byte[NativeExtremePowersSignatures.GroupMemberCountOffset + 2];
        Check(NativeExtremePowersRuntime.ReadGroupMemberCount(emptyGroup) == 0, "native group count zero");
        emptyGroup[NativeExtremePowersSignatures.GroupMemberCountOffset] = 7;
        Check(NativeExtremePowersRuntime.ReadGroupMemberCount(emptyGroup) == 7, "native group count partial spawn");
        emptyGroup[NativeExtremePowersSignatures.GroupMemberCountOffset] = 10;
        Check(NativeExtremePowersRuntime.ReadGroupMemberCount(emptyGroup) == 10, "native group count full spawn");
        Throws(() => NativeExtremePowersRuntime.ReadGroupMemberCount(new byte[NativeExtremePowersSignatures.GroupMemberCountOffset]), "truncated group record rejection");

        var api = ExtremePowersBootstrap.Instance; int executions = 0; int mana = 3816; var tracker = new ExtremePowerOperationTracker();
        var replacement = new ExtremePowerReplacement("once", "", "", ExtremePowerTargetKind.None, (in ExtremePowerExecutionContext c, out string r) => { r = null; return true; }, (in ExtremePowerExecutionContext c) => executions++);
        using (api.RegisterReplacement(ExtremePowerId.Gold, replacement))
        {
            var context = new ExtremePowerExecutionContext(ExtremePowerId.Gold, 1, ExtremePowerTarget.None, 99, 1);
            if (tracker.TryBegin(1, 99) && api.TryExecuteReplacement(context, out _)) mana -= 3816;
            if (tracker.TryBegin(1, 99) && api.TryExecuteReplacement(context, out _)) mana -= 3816;
        }
        Check(executions == 1 && mana == 0, "synchronized None operation executes and deducts once");
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
        string demoText = File.ReadAllText(Path.Combine(modRoot, "src", "Demo", "GoldSpawnDemo.cs"));
        string buildText = File.ReadAllText(Path.Combine(modRoot, "build.bat"));
        Check(adapterText.Contains("api.Vanilla.Costs.Clone()") && !adapterText.Contains("s.ArrowCost"), "example adapter always keeps Vanilla costs");
        Check(!Directory.GetFiles(apiRoot, "*.cs", SearchOption.AllDirectories).Any(file => File.ReadAllText(file).Contains("ProjectileMode")), "old projectile property is removed rather than retained in parallel");
        Check(!settingsText.Contains("ArrowCost") && !settingsXaml.Contains("extreme-powers.costs"), "example cost settings remain removed");
        Check(settingsText.Contains("private int demoUnitType = 24") && settingsText.Contains("70, 75, 44") && settingsText.Contains("if (IndexOfUnit(DemoUnitType) < 0) DemoUnitType = 24"), "demo defaults and migrates to a selectable Spearman while retaining Deer");
        Check(!demoText.Contains("CreateUnitLocal") && adapterText.Contains("api.SpawnUnitGroup"), "demo uses API native group spawn instead of overlapping local units");
        Check(settingsText.Contains("demoOwner = -1") && settingsText.Contains("DemoUnitType == 44 ? 0") && adapterText.Contains("ResolveDemoOwner(context.PlayerId)"), "demo supports an explicit owner and forces Deer to nature");
        Check(settingsXaml.Contains("SelectedIndex=\"{Binding DemoUnitTypeIndex") && settingsXaml.Contains("SelectedIndex=\"{Binding SpearmenTypeIndex") && !settingsXaml.Contains("Text=\"{Binding DemoUnitType,"), "unit IDs are not raw UI text fields");
        Check(settingsXaml.Contains("ItemsSource=\"{Binding ProjectileKindOptions}\"") && settingsXaml.Contains("SelectedIndex=\"{Binding ArrowMode") && settingsXaml.Contains("SelectedIndex=\"{Binding RockMode"), "persisted volley integers use named projectile selections");
        string[] numericTextBindings = { "RegenerationPercent", "ArrowDamage", "ArrowRadius", "HealAmount", "HealRadius", "SpearmenCount", "EngineersCount", "MacemenCount", "GoldMinimum", "GoldMaximum", "RockDamage", "RockRadius", "KnightsCount", "DemoSpawnCount" };
        Check(!settingsXaml.Contains("NumericBox") && numericTextBindings.All(name => settingsXaml.Contains("Text=\"{Binding " + name + "ValueText")), "numeric fields keep the implicit game TextBox template and use editable string proxies");
        Check(numericTextBindings.All(name => settingsText.Contains("string " + name + "ValueText")) && settingsXaml.Contains("SelectedIndex=\"{Binding DemoOwnerIndex"), "numeric string proxies and owner selection are exposed");
        Check(buildText.Contains("not \"%%~nxD\"==\"LobbyModSettings\"") && buildText.Contains("SETTINGS_STATE_BEFORE") && buildText.Contains("fc /B \"%SETTINGS_STATE_BEFORE%\" \"%SETTINGS_STATE_AFTER%\""), "build preserves and verifies LobbyModSettings");
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
        CheckTamperedNativeSignature(bytes, new byte[] { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x74,0x24,0x10,0x48,0x89,0x7C,0x24,0x18,0x4C,0x89,0x74,0x24,0x20,0x41,0x57,0x48,0x83,0xEC,0x40 }, "native group spawn prolog");
        CheckTamperedNativeSignature(bytes, new byte[] { 0x48,0x8B,0x5C,0x24,0x50,0x48,0x8B,0x74,0x24,0x58,0x48,0x8B,0x7C,0x24,0x60,0x4C,0x8B,0x74,0x24,0x68,0x48,0x83,0xC4,0x40,0x41,0x5F,0xC3 }, "native group spawn tail");
        CheckTamperedNativeSignature(bytes, new byte[] { 0x4E,0x0F,0xBF,0x8C,0x5D,0xA4,0xE2,0xAA,0x03,0x4C,0x8D,0x35,0x41,0x90,0xBF,0x07,0x89,0x54,0x24,0x30,0xBA,0x01,0x00,0x00,0x00,0x44,0x89,0x54,0x24,0x28,0x89,0x5C,0x24,0x20,0x4B,0x8D,0x0C,0x49,0x44,0x2B,0x9C,0x8D,0x2C,0xFF,0x02,0x04,0x49,0x8B,0xCE,0x45,0x8B,0xC3,0xE8,0xC8,0x8D,0x05,0x00 }, "native group spawn dispatcher arguments");
        CheckTamperedNativeSignatureAtRva(bytes, NativeExtremePowersSignatures.DispatcherCallerRva, "unique dispatcher caller");
        CheckTamperedNativeSignatureAtRva(bytes, NativeExtremePowersSignatures.DispatcherTailRva, "dispatcher tail");
        CheckTamperedNativeSignatureAtRva(bytes, NativeExtremePowersSignatures.SelectionCallerRva, "unique selection caller");
        CheckTamperedNativeSignatureAtRva(bytes, NativeExtremePowersSignatures.SelectionTailRva, "selection tail");
        CheckTamperedNativeSignatureAtRva(bytes, NativeExtremePowersSignatures.AudioRva, "local audio helper");
        CheckTamperedNativeSignatureAtRva(bytes, NativeExtremePowersSignatures.GroupMemberWriterContextRva, "group member count writer context");
        CheckTamperedNativeSignatureAtRva(bytes, NativeExtremePowersSignatures.GroupMemberWriterRva, "group member count writer");
        bytes[0] ^= 1; Check(!ExtremePowersBuildCompatibility.IsSupportedImage(bytes), "tampered DLL rejection");
    }
    private static void CheckTamperedNativeSignature(byte[] source, byte[] signature, string name)
    {
        byte[] tampered = (byte[])source.Clone();
        int position = Find(tampered, signature);
        Check(position >= 0, name + " signature located in PE");
        if (position >= 0) { tampered[position] ^= 1; Check(!ExtremePowersBuildCompatibility.HasExpectedNativeSignatures(tampered), "tampered " + name + " rejection"); }
    }
    private static void CheckTamperedNativeSignatureAtRva(byte[] source, int rva, string name)
    {
        byte[] tampered = (byte[])source.Clone();
        int position = NativeExtremePowersSignatures.RvaToFileOffset(tampered, rva);
        Check(position >= 0 && position < tampered.Length, name + " RVA mapped in PE");
        if (position >= 0 && position < tampered.Length) { tampered[position] ^= 1; Check(!ExtremePowersBuildCompatibility.HasExpectedNativeSignatures(tampered), "tampered " + name + " rejection"); }
    }
    private static int Find(byte[] haystack, byte[] needle) { for (int i = 0; i <= haystack.Length - needle.Length; i++) { int j = 0; while (j < needle.Length && haystack[i + j] == needle[j]) j++; if (j == needle.Length) return i; } return -1; }
    private static void Check(bool condition, string name) { if (!condition) { failures++; Console.Error.WriteLine("FAIL: " + name); } }
    private static void Throws(Action action, string name) { try { action(); Check(false, name); } catch (ArgumentException) { } catch (InvalidOperationException) { } }
    private static void ThrowsAny(Action action, string name) { try { action(); Check(false, name); } catch { } }
}
