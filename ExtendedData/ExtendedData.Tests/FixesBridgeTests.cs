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
    public sealed class InitializedPreferences { public int Added{get;set;}=123; public static readonly NestedPreferences Shared=new NestedPreferences{Retained=42}; public NestedPreferences Nested{get;set;}=Shared; }
    public sealed class NestedPreferences { public int Retained{get;set;}=10; public int Value{get;set;} }
    public sealed class FloatPreferences { public float Value{get;set;} }
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
    }
}
internal static class ConfigSettings { internal static string[] extendedLordPaths=System.Array.Empty<string>(); }
