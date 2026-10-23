using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
foreach (string section in new[] { "AllowExpandedDefensivePositionsPatch", "FixPlacementSelectionNotAccountingForFarmland" })
{
    foreach (string state in new[] { "absent", "enabled", "disabled", "missing", "exception", "instance-missing" })
    {
        Chainloader.PluginInfos.Clear();
        var config = new FakeConfig { Throw = state == "exception" };
        if (state == "enabled" || state == "disabled") config.Values[section] = state == "enabled";
        if (state != "absent") Chainloader.PluginInfos["fixes"] = new FakeInfo
            { Instance = state == "instance-missing" ? null : new FakePlugin { Config = config } };
        var errors = new List<string>();
        bool allowed = Shared.FixesStartupGate.CanInstall("optional diagnostic", section, errors.Add);
        bool expected = state == "absent" || state == "disabled";
        Check(allowed == expected, section + ": " + state);
        Check(errors.Count == (expected ? 0 : 1), "Unexpected diagnostic count.");
        if (!expected) Check(errors[0].Contains("[" + section + "] Enabled = false") && errors[0].Contains("restart"), "Missing actionable setting/restart instruction.");
        Check(config.Values.Count == (state == "enabled" || state == "disabled" ? 1 : 0), "Foreign config mutated.");
    }
}
IntPtr memory = Marshal.AllocHGlobal(8);
try
{
    byte[] expected = { 0x84, 0xDB, 0x0F, 0x84, 0x80, 0, 0, 0 };
    Marshal.Copy(expected, 0, memory, expected.Length);
    Shared.FixesStartupGate.RequireOriginal(memory, expected);
    Marshal.WriteByte(memory, 0, 0xE9);
    bool refused = false;
    try { Shared.FixesStartupGate.RequireOriginal(memory, expected); } catch (InvalidOperationException) { refused = true; }
    Check(refused, "Changed live bytes accepted.");
    Check(Marshal.ReadByte(memory) == 0xE9, "Validation mutated foreign bytes.");
}
finally { Marshal.FreeHGlobal(memory); }
// All nullable settings of the actual audited external schema must retain explicit null and values.
var preferencesType = typeof(Fixes.Config.CustomLordPreferencesEntry);
foreach (var property in preferencesType.GetProperties())
{
    var host = new Fixes.Config.CustomLordPreferencesEntry();
    Type primitive = Nullable.GetUnderlyingType(property.PropertyType) ?? throw new Exception("New non-nullable external setting requires audit.");
    object value = primitive == typeof(bool) ? (object)false : Convert.ChangeType(27, primitive);
    property.SetValue(host, value);
    string json = ExtendedData.TypedPreferenceSnapshotCodec.Capture(host);
    object restored = ExtendedData.TypedPreferenceSnapshotCodec.Restore(preferencesType, json);
    Check(Equals(property.GetValue(restored), value), "Current Fixes value lost: " + property.Name);
    property.SetValue(host, null);
    restored = ExtendedData.TypedPreferenceSnapshotCodec.Restore(preferencesType, ExtendedData.TypedPreferenceSnapshotCodec.Capture(host));
    Check(property.GetValue(restored) == null, "Current Fixes null lost: " + property.Name);
}
Console.WriteLine($"Fixes startup compatibility: {assertions} assertions passed.");

// Test doubles model BepInEx's config/chainloader boundary; the production guard is compiled above.
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T> { public T Value; }
    public sealed class FakeConfig
    {
        public readonly Dictionary<string, bool> Values = new();
        public bool Throw;
        public bool TryGetEntry<T>(string section, string key, out ConfigEntry<T> setting)
        {
            if (Throw) throw new InvalidOperationException("Unavailable config boundary.");
            setting = Values.TryGetValue(section, out bool value) ? new ConfigEntry<T> { Value = (T)(object)value } : null;
            return setting != null;
        }
    }
}
namespace BepInEx.Bootstrap
{
    public sealed class FakePlugin { public FakeConfig Config; }
    public sealed class FakeInfo { public FakePlugin Instance; }
    public static class Chainloader { public static readonly Dictionary<string, FakeInfo> PluginInfos = new(); }
}
