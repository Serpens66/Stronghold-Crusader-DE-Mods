using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using SerpsModsHost;

internal static class TweakerProviderTests
{
    internal static void Run()
    {
        foreach (int count in new[] { 4233, 12000, 16384 })
        {
            ProviderProbe.ConfigurationApi.Reset(count);
            var provider = new StatsTweakerConfigurationProvider(typeof(ProviderProbe.ConfigurationApi));
            var model = new Model(provider);
            string path = Path.Combine(Path.GetTempPath(), "TweakerProviderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            model.PreparePresets(null, Path.Combine(path, "probe.dll"), "Probe", "Probe", new Version(1, 0));
            model.ActivatePresets();
            provider.EnableRestartManagedSynchronization();
            Check(ProviderProbe.ConfigurationApi.Reads == 1 && ProviderProbe.ConfigurationApi.Validations == 1,
                "initialization read/validated the same configuration repeatedly");
            var descriptors = model.System_GetPresetSettingDescriptors();
            descriptors[0].RequiresRestart = false;
            Check(model.System_GetPresetSettingDescriptors()[0].RequiresRestart, "caller mutated cached metadata");
            provider.GetSettings()[0].RequiresRestart = false;
            Check(!model.System_GetPresetSettingDescriptors().Single(x => x.PropertyName == "key0").RequiresRestart,
                "metadata cache hid changed capability");
            var copy = provider.ReadValues(); copy["key0"] = -999L;
            bool failed = false;
            try { provider.ReplaceValues(copy); } catch (InvalidDataException) { failed = true; }
            Check(failed && (long)provider.ReadValue("key0") == 0L, "invalid candidate replaced verified state");
            ProviderProbe.ConfigurationApi.Own["key0"] = 42L;
            Check((long)provider.ReadOwnValues()["key0"] == 42L && ProviderProbe.ConfigurationApi.Reads == 2,
                "later personal read returned cached file values");
        }
        ProviderProbe.ConfigurationApi.Reset(3);
        var lateProvider = new StatsTweakerConfigurationProvider(typeof(ProviderProbe.ConfigurationApi));
        ProviderProbe.ConfigurationApi.RejectEnable = true;
        bool enableRejected = false, activeRejected = false;
        try { lateProvider.EnableRestartManagedSynchronization(); } catch (System.Reflection.TargetInvocationException) { enableRejected = true; }
        try { lateProvider.ReadActiveValues(); } catch (InvalidOperationException) { activeRejected = true; }
        Check(enableRejected && activeRejected, "late activation exposed invalid startup evidence");
        Console.WriteLine("PASS: actual Tweaker adapter initializes once, validates changes, refreshes personal values and exposes live metadata at 4233/12000/16384 options");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Model : PresetLobbyModSettingsViewModel
    {
        private readonly StatsTweakerConfigurationProvider provider;
        internal Model(StatsTweakerConfigurationProvider provider) { this.provider = provider; }
        protected override IDynamicPresetSettingsProvider DynamicSettingsProvider => provider;
    }
}
namespace ProviderProbe
{
    public sealed class ConfigurationCapabilities { public bool IsReady => true; public bool CanApplyWithoutRestart => false; public bool CanStageForNextStart => true; }
    public sealed class ConfigurationOption
    {
        public string Key { get; set; }
        public string Name => Key;
        public string Group => "Test";
        public string File => "Test.toml";
        public string ValueType => "Integer";
        public object DefaultValue => 0L;
        public bool RequiresRestart => true;
        public bool IsSupported => true;
        public bool IsLocal => false;
    }
    public sealed class ConfigurationProblem { public string Key => "key0"; public string Message => "Invalid test value"; }
    public sealed class ConfigurationSnapshot { public Dictionary<string, object> Values { get; set; } public string Revision => "test"; public string ContextId => ""; }
    public static class ConfigurationApi
    {
        internal static Dictionary<string, object> Own;
        internal static int Reads, Validations;
        internal static bool RejectEnable;
        internal static void Reset(int count) { RejectEnable = false; Reads = Validations = 0; Own = Enumerable.Range(0,count).ToDictionary(i => "key"+i, i => (object)0L); }
        public static void EnableRestartManagedSynchronization() { if (RejectEnable) throw new InvalidOperationException("Restart required after legacy host application."); }
        public static int ApiVersion => 1;
        public static ConfigurationCapabilities GetCapabilities() => new ConfigurationCapabilities();
        public static ConfigurationOption[] GetOptions() => Own.Keys.Select(key => new ConfigurationOption { Key = key }).ToArray();
        public static ConfigurationSnapshot ReadOwnConfiguration() { Reads++; return GetLoadedConfiguration(); }
        public static ConfigurationSnapshot GetLoadedConfiguration() => new ConfigurationSnapshot { Values = new Dictionary<string, object>(Own) };
        public static ConfigurationSnapshot GetPendingConfiguration() => null;
        public static ConfigurationProblem[] ValidateConfiguration(IDictionary<string, object> values)
        { Validations++; return values.Count != Own.Count || values.Values.Any(x => !(x is long) || (long)x < 0) ? new[] { new ConfigurationProblem() } : new ConfigurationProblem[0]; }
        public static void DiscardPendingConfiguration() { }
        public static void StageConfiguration(IDictionary<string, object> values, string revision) { }
        public static void StageContextConfiguration(IDictionary<string, object> values, string context, string revision) { }
        public static void StageReturnToOwnConfiguration() { }
        public static bool IsNetworkConfigurationClient() => false;
        public static bool PrepareNetworkConfiguration() => true;
    }
}
