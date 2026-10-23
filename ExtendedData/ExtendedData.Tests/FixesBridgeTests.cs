using ExtendedData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
internal static class FixesBridgeTests
{
    internal static void Run()
    {
        TestSources();
        var plugin=new Fixes.Boostrap.Plugin();
        BepInEx.Bootstrap.Chainloader.PluginInfos["fixes"]=new BepInEx.Bootstrap.FakePluginInfo{Instance=plugin};
        var original=new Fixes.Config.CustomLordPreferencesEntry{Value=17};
        plugin.CustomLordPreferences["MixedName"]=original;
        Fixes.Config.Preferences.SetExtended(4,new Fixes.Config.CustomLordPreferencesEntry{Value=23});
        var bridge=new FixesLordPreferencesBridge();
        if(!bridge.Capture("mixedname",-1).Contains("17"))throw new Exception("Case insensitive capture.");
        bridge.Apply(LordDataSnapshot.Create("bridge",true,new[] {
            new LordDataSlot{PlayerId=2,LordType=-1,LordName="MIXEDNAME",ConfigName="a",ConfigChecksum="1",FixesJson="{\"Value\":99}"},
            new LordDataSlot{PlayerId=3,LordType=-1,LordName="mixedname",ConfigName="b",ConfigChecksum="2",FixesJson="{\"Value\":99}"},
            new LordDataSlot{PlayerId=4,LordType=4,LordName="WolfAlias",ConfigName="c",ConfigChecksum="3",FixesJson="{\"Value\":45}"},
        }));
        if(plugin.CustomLordPreferences.Count!=1 || plugin.CustomLordPreferences["MixedName"].Value!=99 ||
            !bridge.Capture("DifferentAlias",4).Contains("45"))throw new Exception("Custom aliases / extended type ownership.");
        Fixes.Config.Preferences.SetSlot(4,new Fixes.Config.CustomLordPreferencesEntry{Value=72});
        if(!bridge.HasMapOverride(4) || Fixes.Config.Preferences.GetSlot(4).Value!=72)throw new Exception("Map override precedence touched.");
        bridge.Restore();
        if(!ReferenceEquals(plugin.CustomLordPreferences["mixedname"],original) || !bridge.Capture("WolfAlias",4).Contains("23"))
            throw new Exception("Original preference storage restoration.");
        bridge.Apply(LordDataSnapshot.Create("bridge-removal",true,new[] {new LordDataSlot{PlayerId=2,LordType=-1,LordName="mixedname",ConfigName="a",ConfigChecksum="1"}}));
        if(plugin.CustomLordPreferences.ContainsKey("MixedName"))throw new Exception("Absent snapshot value must remove touched default.");
        bridge.Restore();
        ConfigSettings.extendedLordPaths=new[]{"Wolf","Rat"};
        CustomisationFileManager.Instance.Configs=new[]{new CustomisationFileManager.CustomLordConfig{lordType=0,name="a",checksum=4},new CustomisationFileManager.CustomLordConfig{lordType=1,name="a",checksum=5}};
        if(FixesLordPreferencesBridge.ResolveLordType("legacy","a","4")!=0)throw new Exception("Legacy checksum identity.");
        bool ambiguous=false;try{FixesLordPreferencesBridge.ResolveLordType("legacy","a");}catch(InvalidDataException){ambiguous=true;}
        if(!ambiguous)throw new Exception("Legacy ambiguous type guessed.");
        var defaults=(InitializedPreferences)TypedPreferenceSnapshotCodec.Restore(typeof(InitializedPreferences),"{\"Nested\":{\"Value\":7}}");
        if(defaults.Added!=123 || defaults.Nested.Retained!=42 || defaults.Nested.Value!=7 || InitializedPreferences.Shared.Value!=0)throw new Exception("Nested constructor defaults lost.");
        bool tinyLoss=false;try{TypedPreferenceSnapshotCodec.Restore(typeof(FloatPreferences),"{\"Value\":1.234567891e-29}");}catch(InvalidDataException){tinyLoss=true;}
        if(!tinyLoss)throw new Exception("Subdecimal float rounding accepted.");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action, string message)
    { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }
    private static LordDataSlot Slot(string name, int player = 1, int type = -1) => new LordDataSlot
    { PlayerId = player, LordType = type, LordName = name, ConfigName = "cfg", ConfigChecksum = "42" };
    private static void TestSources()
    {
        var plugin = new Fixes.Boostrap.Plugin();
        BepInEx.Bootstrap.Chainloader.PluginInfos["fixes"] = new BepInEx.Bootstrap.FakePluginInfo { Instance = plugin };
        var bridge = new FixesLordPreferencesBridge();
        var rat = new Fixes.Config.CustomLordPreferencesEntry { Value = 71 };
        plugin.CustomLordPreferences["RAT"] = rat;
        var inherited = bridge.CaptureSources("Custom", -1, (int)SHCDESE.Interop.AILords.SK_RAT);
        Require(inherited.Count == 1 && inherited[0].CustomName == "RAT" && inherited[0].Json.Contains("71"), "Base-name priority lost.");
        plugin.CustomLordPreferences["Custom"] = new Fixes.Config.CustomLordPreferencesEntry { Value = 99 };
        Require(bridge.CaptureSources("Custom", -1, 1).Count == 0, "Own Custom preferences must stop inheritance.");
        plugin.CustomLordPreferences.Remove("Custom"); plugin.CustomLordPreferences.Remove("RAT");
        Fixes.Config.Preferences.SetExtended(0, new Fixes.Config.CustomLordPreferencesEntry { Value = 62 });
        inherited = bridge.CaptureSources("Custom", -1, 1);
        Require(inherited.Count == 2 && inherited[0].Json == null && inherited[1].LordType == 0 && inherited[1].Json.Contains("62"), "Missing custom source / Extended base lost.");
        Require(bridge.CaptureSources("Custom", -1, 999).Count == 0, "Invalid base identity was clamped.");
        var first = Slot("Custom"); first.FixesSources = inherited;
        var second = Slot("Custom", 2); second.FixesSources = bridge.CaptureSources("Custom", -1, 2);
        var snapshot = LordDataSnapshot.Parse(LordDataSnapshot.Create("sources", true, new[] { first, second }).WireJson);
        Require(snapshot.GetSlot(1).FixesSources.Count == 2 && snapshot.GetSlot(2).FixesSources[0].CustomName == "SNAKE", "Per-configuration sources lost in transit.");
        plugin.CustomLordPreferences["RAT"] = rat; // Explicit host absence must remove this client value.
        bridge.Apply(snapshot);
        Require(!plugin.CustomLordPreferences.ContainsKey("RAT"), "Explicit source absence was ignored.");
        Fixes.Config.Preferences.SetSlot(1, new Fixes.Config.CustomLordPreferencesEntry { Value = 72 });
        Require(bridge.HasMapOverride(1) && Fixes.Config.Preferences.GetSlot(1).Value == 72, "Map overrides were changed.");
        var conflict = Slot("Other", 3); conflict.FixesSources = new[] { new FixesPreferenceSource { CustomName = "RAT", Json = "{\"Value\":11}" } };
        Reject(() => bridge.Apply(LordDataSnapshot.Create("conflict", true, new[] { first, conflict })), "Shared-source conflict accepted.");
        Require(!plugin.CustomLordPreferences.ContainsKey("RAT"), "Rejected application changed the previous overlay.");
        var bad = Slot("Bad"); bad.FixesSources = new[] { new FixesPreferenceSource { CustomName = "RAT", Json = "{\"Unknown\":4}" } };
        Reject(() => bridge.Apply(LordDataSnapshot.Create("bad", true, new[] { bad })), "Unsupported property accepted.");
        Require(!plugin.CustomLordPreferences.ContainsKey("RAT"), "Failed validation partially applied values.");
        bridge.Restore(); Require(ReferenceEquals(plugin.CustomLordPreferences["RAT"], rat), "Original source object was not restored.");
        Require(bridge.CaptureSources("WolfAlias", 3, 1).Single().CustomName == "RAT",
            "Extended selection did not resolve its AIC base before its own last fallback.");
        var overlay = Slot("RAT"); overlay.FixesJson = "{\"Value\":88}";
        bridge.Apply(LordDataSnapshot.Create("previous", true, new[] { overlay }));
        var legacyDuringOverlay = bridge.CompleteSources(LordDataSnapshot.Create("next", true, new[] { Slot("New") }), _ => 1);
        Require(legacyDuringOverlay.Slots[0].FixesSources.Single().Json.Contains("71"),
            "Migration captured a previous session overlay instead of current host originals.");
        bridge.Restore();
        // Inject a one-shot dictionary write fault at the external boundary, after the first write.
        var faulting = new FaultingDictionary((System.Collections.IDictionary)plugin.CustomLordPreferences);
        typeof(FixesLordPreferencesBridge).GetField("custom", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, faulting);
        var one = Slot("First"); one.FixesJson = "{\"Value\":1}";
        var two = Slot("Second", 2); two.FixesJson = "{\"Value\":2}";
        bool fault = false;
        try { bridge.Apply(LordDataSnapshot.Create("fault", true, new[] { one, two })); }
        catch (IOException) { fault = true; }
        Require(fault && !plugin.CustomLordPreferences.ContainsKey("First") && !plugin.CustomLordPreferences.ContainsKey("Second") &&
            ReferenceEquals(plugin.CustomLordPreferences["RAT"], rat), "Failed write did not roll back objects and original absences.");
        bridge.Apply(LordDataSnapshot.Create("retry", true, new[] { one }));
        bridge.Apply(LordDataSnapshot.Create("switch", true, Array.Empty<LordDataSlot>()));
        Require(!plugin.CustomLordPreferences.ContainsKey("First"), "Selection change did not restore an originally absent entry.");
        // A saved direct source wins while filling missing historical dependencies of another Lord.
        var savedRat = Slot("RAT"); savedRat.FixesJson = "{\"Value\":15}";
        var savedCustom = Slot("Legacy", 2);
        var old = LordDataSnapshot.Create("legacy", true, new[] { savedRat, savedCustom });
        var migrated = bridge.CompleteSources(old, _ => 1);
        Require(old.GetSlot(2).FixesSources == null && migrated.GetSlot(2).FixesSources.Single().Json.Contains("15"), "Migration discarded saved direct preferences.");
        Require(migrated.GetSlot(1).FixesJson == savedRat.FixesJson && !ReferenceEquals(old, migrated), "Migration changed saved data in place.");
        bridge.Apply(migrated); bridge.Restore();
        Require(ReferenceEquals(plugin.CustomLordPreferences["RAT"], rat), "Migration could not restore the host values.");
        var previousConfigs = CustomisationFileManager.Instance.Configs;
        try
        {
            CustomisationFileManager.Instance.Configs = new[] {
                new CustomisationFileManager.CustomLordConfig { lordType = -1, name = "first", checksum = 42, path = "author",
                    lordData = new EngineInterface.AILordConfigTransferData { opponent_type = 1 } },
                new CustomisationFileManager.CustomLordConfig { lordType = -1, name = "second", checksum = 43, path = "author",
                    lordData = new EngineInterface.AILordConfigTransferData { opponent_type = 2 } },
            };
            var a = Slot("SourceIdentityOnly"); a.ConfigName = "first";
            var b = Slot("SourceIdentityOnly", 2); b.ConfigName = "second"; b.ConfigChecksum = "43";
            var configurations = bridge.CompleteSources(LordDataSnapshot.Create("configs", true, new[] { a, b }));
            Require(configurations.GetSlot(1).FixesSources[0].CustomName == "RAT" &&
                configurations.GetSlot(2).FixesSources[0].CustomName == "SNAKE", "Migration ignored the selected checksum-bound AIC base.");
            b.ConfigChecksum = "missing";
            Reject(() => bridge.CompleteSources(LordDataSnapshot.Create("missing-config", true, new[] { b })),
                "Unresolvable AIC identity was guessed.");
        }
        finally { CustomisationFileManager.Instance.Configs = previousConfigs; }
        var empty = Slot("Empty"); empty.FixesSources = Array.Empty<FixesPreferenceSource>();
        var emptyRoundTrip = LordDataSnapshot.Parse(LordDataSnapshot.Create("empty", true, new[] { empty }).WireJson);
        Require(emptyRoundTrip.GetSlot(1).FixesSources != null && FixesPreferenceSources.Digest(empty.FixesSources) != null, "Empty complete capture became legacy null.");
        Reject(() => FixesPreferenceSources.Validate(new[] { new FixesPreferenceSource { CustomName = "RAT", LordType = 0 } }), "Ambiguous source key accepted.");
        Reject(() => LordDataSnapshot.Create("absent", false, new[] { empty }), "Sources accepted without Fixes.");
        var package = LordPackageManifest.Create("manifest", false, new[] { new LordPackageSlot { PlayerId = 1,
            LordName = "Custom", ConfigName = "cfg", FileDigest = "file", FixesDigest = "own", FixesSourcesDigest = FixesPreferenceSources.Digest(inherited) } });
        var parsed = LordPackageManifest.Parse(package.WireJson);
        Require(parsed.Digest == package.Digest && parsed.Slots[0].FixesDigest == "own" && parsed.Slots[0].FixesSourcesDigest != null, "Manifest source digest roundtrip failed.");
        // Keep the external dictionary boundary clean for subsequent tests.
        plugin.CustomLordPreferences.Clear(); Fixes.Config.Preferences.Reset();
    }
    public sealed class InitializedPreferences { public int Added{get;set;}=123; public static readonly NestedPreferences Shared=new NestedPreferences{Retained=42}; public NestedPreferences Nested{get;set;}=Shared; }
    public sealed class NestedPreferences { public int Retained{get;set;}=10; public int Value{get;set;} }
    public sealed class FloatPreferences { public float Value{get;set;} }
    private sealed class FaultingDictionary : System.Collections.IDictionary
    {
        private readonly System.Collections.IDictionary inner;
        private int writes;
        internal FaultingDictionary(System.Collections.IDictionary inner) { this.inner = inner; }
        public object this[object key] { get => inner[key]; set { if (++writes == 2) throw new IOException("Injected external write failure."); inner[key] = value; } }
        public void Add(object key, object value) => inner.Add(key, value);
        public bool Contains(object key) => inner.Contains(key);
        public void Clear() => inner.Clear();
        public void Remove(object key) => inner.Remove(key);
        public System.Collections.IDictionaryEnumerator GetEnumerator() => inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => inner.GetEnumerator();
        public System.Collections.ICollection Keys => inner.Keys;
        public System.Collections.ICollection Values => inner.Values;
        public int Count => inner.Count;
        public bool IsReadOnly => inner.IsReadOnly;
        public bool IsFixedSize => inner.IsFixedSize;
        public object SyncRoot => inner.SyncRoot;
        public bool IsSynchronized => inner.IsSynchronized;
        public void CopyTo(Array array, int index) => inner.CopyTo(array, index);
    }
}
namespace BepInEx.Bootstrap
{
    internal sealed class FakePluginInfo { public object Instance {get;set;} }
    internal static class Chainloader { public static readonly Dictionary<string,FakePluginInfo> PluginInfos=new(); }
}
namespace Fixes.Boostrap
{
    public sealed class Plugin
    {
        public static Plugin Instance;
        public Dictionary<string,Fixes.Config.CustomLordPreferencesEntry> CustomLordPreferences {get;private set;}=new(StringComparer.OrdinalIgnoreCase);
        public Plugin(){Instance=this;}
    }
}
namespace Fixes.Config
{
    public sealed class CustomLordPreferencesEntry { public int? Value{get;set;} public int NewSetting{get;set;}=3; }
    internal static class Preferences
    {
        private static readonly Dictionary<int,CustomLordPreferencesEntry> ExtendedLords=new();
        private static CustomLordPreferencesEntry[] _slots=new CustomLordPreferencesEntry[9];
        internal static void SetExtended(int type,CustomLordPreferencesEntry entry)=>ExtendedLords[type+1]=entry;
        internal static void SetSlot(int id,CustomLordPreferencesEntry entry)=>_slots[id]=entry;
        internal static CustomLordPreferencesEntry GetSlot(int id)=>_slots[id];
        internal static void Reset() { ExtendedLords.Clear(); _slots = new CustomLordPreferencesEntry[9]; }
        private static bool TryGetBaseLordPreferences(int playerId, out CustomLordPreferencesEntry preferences)
        { preferences = null; throw new NotSupportedException("Reflection contract only; the bridge must never invoke this runtime lookup in the lobby."); }
    }
}
internal static class ConfigSettings
{
    internal static string[] extendedLordPaths=System.Array.Empty<string>();
    internal static string MediaPath=System.IO.Path.GetTempPath();
    public static string GetUserCustomMediaPath()=>MediaPath;
}

// External enum boundary: names/values used by the audited Fixes base-lord lookup.
namespace SHCDESE.Interop
{
    public enum AILords : uint { SK_NULL, SK_RAT, SK_SNAKE, SK_PIG, SK_WOLF, SK_X1 = 30 }
}
