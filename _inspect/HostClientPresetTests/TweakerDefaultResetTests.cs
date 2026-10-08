using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using SerpsModsHost;
using Api = DefaultResetProbe.ConfigurationApi;

internal static class TweakerDefaultResetTests
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "TweakerDefaultResetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ModSettingsApplication.ResetForTests(Path.Combine(root, "restart.json"));
        SHCDESE.API.GameNetworkAPI.LocalHost = true;
        Api.Initialize();
        var provider = new StatsTweakerConfigurationProvider(typeof(Api));
        var model = new Model(provider);
        model.PreparePresets(null, Path.Combine(root, "probe.dll"), "Defaults", "CrusaderDETweaker", new Version(2, 8));
        model.ActivatePresets();
        ModSettingsApplication.Register("CrusaderDETweaker", model, Path.Combine(root, "probe.dll"));
        provider.EnableRestartManagedSynchronization();
        var original = new Dictionary<string, object>(Api.Active);
        model.System_LoadModDefaults();
        Equal(Api.Defaults, Api.Pending, "reset must stage every catalog default, including local settings");
        Equal(original, Api.Active, "reset changed active values before restart");
        Check(Api.Stages == 1 && model.System_ConfigurationNeedsRestart(), "reset did not request exactly one restart package");
        Check(model.System_ApplicationNotice.Contains("Restart the game"), "missing restart feedback");
        Api.Restart();
        Equal(Api.Defaults, Api.Active, "restart did not load defaults");
        int stages = Api.Stages;
        model.System_LoadModDefaults();
        Check(!model.System_ConfigurationNeedsRestart() && Api.Stages == stages, "unchanged defaults requested another package");

        Check(model.System_ApplicationNotice.Contains("already match"), "unchanged reset did not explain no restart");
        Api.Pending = new Dictionary<string, object>(Api.Defaults) { ["MaxCount"] = 2L };
        model.System_LoadModDefaults();
        Check(Api.Pending == null && model.System_ApplicationNotice.Contains("discarded"), "discarded preparation reported as unchanged");
        Api.Active["MaxCount"] = 3L;
        Api.Pending = new Dictionary<string, object>(Api.Defaults) { ["MaxCount"] = 2L };
        model.System_LoadModDefaults();
        Check(model.System_ApplicationNotice.Contains("replaced") && model.System_ApplicationNotice.Contains("Restart the game"),
            "replaced preparation did not retain restart feedback");
        Api.Restart();
        Api.Own["MaxCount"] = 7L;
        Api.Own["LocalDebug"] = true;
        var mixed = Api.Defaults.ToDictionary(pair => pair.Key,
            pair => new PublishedPresetSetting { Mode = PublishedPresetValueMode.ModDefault });
        mixed["MaxCount"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Fixed, Value = 0L };
        mixed["LocalDebug"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Player };
        model.System_ImportPresetJson(ModSettingsPresetJson.Serialize("CrusaderDETweaker", "mixed-defaults", "Mixed", "", "", "", mixed));
        model.System_TestLoadPreset(model.System_TestPublishedPresets.Single(x => x.Id == "mixed-defaults").StableId);
        var expected = new Dictionary<string, object>(Api.Defaults) { ["MaxCount"] = 0L, ["LocalDebug"] = true };
        Equal(expected, Api.Pending, "mixed preset substituted a sentinel, zero, boolean, string or multiplier default");
        Equal(Api.Defaults, Api.Active, "mixed preset changed active values");
        Api.FailStage = true;
        try { model.System_LoadModDefaults(); throw new Exception("failed stage accepted"); }
        catch (System.Reflection.TargetInvocationException) { }
        Equal(expected, Api.Pending, "failed reset replaced a valid pending package");
        Check(model.System_ApplicationNotice.Length == 0, "failed reset retained a misleading success notice");
        Api.FailStage = false;
        model.System_LoadModDefaults();
        Equal(Api.Defaults, Api.Pending, "reset after mixed preset retained a previous selection");
        ModSettingsApplication.ResetForTests(Path.Combine(root, "unused.json"));
        Console.WriteLine("PASS: real Tweaker adapter and shared reset preserve typed defaults, mixed modes, restart isolation and failed replacements");
    }

    private static void Equal(Dictionary<string, object> expected, Dictionary<string, object> actual, string message)
    {
        Check(actual != null && expected.Count == actual.Count && expected.All(pair =>
            actual.TryGetValue(pair.Key, out var value) && Equals(pair.Value, value)), message);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class Model : PresetLobbyModSettingsViewModel
    {
        private readonly StatsTweakerConfigurationProvider provider;
        internal Model(StatsTweakerConfigurationProvider provider) { this.provider = provider; }
        protected override IDynamicPresetSettingsProvider DynamicSettingsProvider => provider;
    }
}

// Typed API fixture: validates the actual reflection adapter without invoking native game APIs.
namespace DefaultResetProbe
{
    public sealed class ConfigurationCapabilities
    {
        public bool IsReady => true;
        public bool CanApplyWithoutRestart => false;
        public bool CanStageForNextStart => true;
        public bool CanStageContextConfigurations => true;
    }
    public sealed class ConfigurationOption
    {
        public string Key { get; set; }
        public string Name => Key;
        public string Group => "Fixture";
        public string File => "Fixture.toml";
        public object DefaultValue => ConfigurationApi.Defaults[Key];
        public string ValueType => DefaultValue is bool ? "Boolean" : DefaultValue is string ? "String" : DefaultValue is double ? "Number" : "Integer";
        public bool RequiresRestart => true;
        public bool IsSupported => Key != "Gatehouse";
        public bool IsLocal => Key == "LocalDebug";
        public string Notice => IsSupported ? "" : "Preserved but not applied";
    }
    public sealed class ConfigurationProblem { public string Key => ""; public string Message => "Invalid fixture snapshot"; }
    public sealed class ConfigurationSnapshot
    {
        public Dictionary<string, object> Values { get; set; }
        public string Revision => "own-revision";
        public string ContextId => "";
        public string Source => "Fixture";
    }
    public static class ConfigurationApi
    {
        internal static Dictionary<string, object> Defaults, Own, Active, Pending;
        internal static int Stages;
        internal static bool FailStage;
        internal static void Initialize()
        {
            Defaults = new Dictionary<string, object> { ["NumericOverride"] = -1L, ["MaxCount"] = -1L,
                ["ValidZero"] = 0L, ["Enabled"] = false, ["DefaultTrue"] = true, ["Resource"] = "NONE",
                ["HealthMultiplier"] = 1d, ["WallMultiplier"] = 0.25d, ["Matrix"] = 13L,
                ["Gatehouse"] = 200L, ["LocalDebug"] = false };
            Own = Defaults.ToDictionary(pair => pair.Key, pair => pair.Value is bool flag ? (object)!flag :
                pair.Value is long number ? number + 5 : pair.Value is double real ? real + 2 : (object)"WOOD");
            Active = new Dictionary<string, object>(Own);
            Pending = null; Stages = 0; FailStage = false;
        }
        internal static void Restart() { Active = new Dictionary<string, object>(Pending); Own = new Dictionary<string, object>(Pending); Pending = null; }
        public static int ApiVersion => 1;
        public static ConfigurationCapabilities GetCapabilities() => new ConfigurationCapabilities();
        public static ConfigurationOption[] GetOptions() => Defaults.Keys.Select(key => new ConfigurationOption { Key = key }).ToArray();
        public static ConfigurationSnapshot ReadOwnConfiguration() => Snapshot(Own);
        public static ConfigurationSnapshot GetLoadedConfiguration() => Snapshot(Active);
        public static ConfigurationSnapshot GetPendingConfiguration() => Pending == null ? null : Snapshot(Pending);
        private static ConfigurationSnapshot Snapshot(Dictionary<string, object> values) => new ConfigurationSnapshot { Values = new Dictionary<string, object>(values) };
        public static ConfigurationProblem[] ValidateConfiguration(IDictionary<string, object> values) =>
            values.Count == Defaults.Count && Defaults.All(pair => values.TryGetValue(pair.Key, out var value) && value?.GetType() == pair.Value.GetType())
            ? new ConfigurationProblem[0] : new[] { new ConfigurationProblem() };
        public static void StageConfiguration(IDictionary<string, object> values, string revision)
        {
            if (FailStage) throw new IOException("Fixture write failure");
            if (revision != "own-revision" || ValidateConfiguration(values).Length != 0) throw new InvalidDataException("Invalid candidate");
            Pending = new Dictionary<string, object>(values); Stages++;
        }
        public static void StageContextConfiguration(IDictionary<string, object> values, string context, string revision) => StageConfiguration(values, revision);
        public static void StageReturnToOwnConfiguration() => StageConfiguration(Own, "own-revision");
        public static void DiscardPendingConfiguration() { Pending = null; }
        public static void EnableRestartManagedSynchronization() { }
        public static bool IsNetworkConfigurationClient() => false;
        public static bool PrepareNetworkConfiguration() => true;
    }
}
