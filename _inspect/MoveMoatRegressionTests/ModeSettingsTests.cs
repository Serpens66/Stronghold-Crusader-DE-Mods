using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;
using MoveMoatTest;
using SHCDESE.API;
using SHCDESE.ViewModels;
public static class ModeSettingsTests
{
    public static void Run(string root)
    {
        Directory.CreateDirectory(root);
        var vm=new MoveMoatSettings();
        vm.PreparePresets(null,Path.Combine(root,"MoveMoatTest.dll"),"MoveMoatTest_Serp");
        vm.ActivatePresets();
        void Check(bool valid,string text){if(!valid)throw new Exception("SETTINGS: "+text);}
        Check(vm.EnableMod&&vm.RouteMode==1,"required-only defaults");
        vm.RouteMode=0;vm.SelectedPreset=1;Check(vm.RouteMode==1,"new preset defaults");
        vm.SelectedPreset=0;Check(vm.RouteMode==0,"restore exact preset");
        vm.RouteMode=92;Check(vm.RouteMode==0,"invalid mode");vm.RouteMode=1;
        var snapshot=new Dictionary<string,byte[]> { ["RouteMode"]=MessagePackSerializer.Serialize(0),["EnableMod"]=MessagePackSerializer.Serialize(true) };
        vm.System_EnterMissionPreset(snapshot,"Trail",false);
        vm.RouteMode=1;Check(vm.RouteMode==0&&!vm.CanEditHostSettings,"Trail blocks local write");
        vm.SelectedPreset=0;Check(vm.RouteMode==1&&vm.CanEditHostSettings,"local preset leaves Trail lock");
        vm.SelectedPreset=2;Check(vm.RouteMode==0,"Trail snapshot preserved");vm.System_ExitMissionPreset();
        string file=Path.Combine(root,"LobbyModSettings","MoveMoatTest_Serp.msgpack");
        byte[] before=File.ReadAllBytes(file);
        GameNetworkAPI.LocalHost=false;vm.System_RefreshSettingsAccess();
        vm.RouteMode=0;Check(vm.RouteMode==1&&!vm.CanEditHostSettings,"client edit blocked");
        GameXAMLManagerAPI.Instance.SetSync(true);vm.BeginAuthorisedUpdate();
        try { vm.RouteMode=0; } finally { vm.EndAuthorisedUpdate();GameXAMLManagerAPI.Instance.SetSync(false); }
        Check(vm.RouteMode==0&&before.SequenceEqual(File.ReadAllBytes(file)),"host update accepted without local persistence");
        vm.ResetToDefaultCommand.Execute(null);Check(vm.RouteMode==0,"client reset blocked");
        GameNetworkAPI.LocalHost=true;vm.System_RefreshSettingsAccess();
        vm.RouteMode=0;vm.ResetToDefaultCommand.Execute(null);Check(vm.RouteMode==1&&vm.EnableMod,"host reset");
        Console.WriteLine("PASS: actual MoveMoat required-only settings/preset controller/2.0.2 base: defaults, invalid mode, presets, Trail, client rejection, authorised update and local file isolation.");
    }
}
public static class SerpLocalization { public const string ResetToDefault="reset";public static string Get(string key)=>key; }
namespace Noesis {public enum Visibility { Visible,Hidden,Collapsed } public sealed class ComboBoxItem {public object Content{get;set;}public Visibility Visibility{get;set;}}}
namespace SHCDESE.NoesisUtil {public sealed class RelayCommand {private readonly Action action;public RelayCommand(Action a){action=a;}public void Execute(object ignored)=>action();}}
namespace BepInEx {public class BaseUnityPlugin {public PluginInfo Info{get;}=new PluginInfo();}public sealed class PluginInfo{public string Location{get;set;}}}
namespace BepInEx.Logging {public sealed class ManualLogSource {}}
namespace SHCDESE.BepInEx.Bootstrap {public static class Plugin {}}
namespace SHCDESE.Logging {public static class LogHelper {public static void Warning(string text){}}}
namespace SHCDESE.API.Components.Network {
 [AttributeUsage(AttributeTargets.Property)] public sealed class SyncHostOnlyAttribute:Attribute{}
 [AttributeUsage(AttributeTargets.Property)] public sealed class SyncPerPlayerAttribute:Attribute{}
 public static class Extensions {public static bool IsHostOnly(this PropertyInfo p)=>p.GetCustomAttribute<SyncHostOnlyAttribute>()!=null;}
}
namespace SHCDESE.API.Components.ModManager {
 [AttributeUsage(AttributeTargets.Property)]public sealed class DoNotPersistAttribute:Attribute{}
 public static class LobbyModSettingsStorage {public const string STORAGE_FOLDER_NAME="LobbyModSettings",FILE_EXTENSION=".msgpack";}
}
namespace SHCDESE.API {
 public static class GameNetworkAPI {public static bool LocalHost=true;public static bool IsLocalHost()=>LocalHost;public static bool IsNetworkedEnvironment()=>true;}
 public sealed class GameXAMLManagerAPI {
  private bool _isProcessingNetworkSync; public void SetSync(bool value){_isProcessingNetworkSync=value;}
  public static GameXAMLManagerAPI Instance{get;}=new GameXAMLManagerAPI();
  public List<Registration> RegisteredModSettings{get;}=new List<Registration>();
  public sealed class Registration {public object ViewModel,View;}
  public void RegisterLobbyModSettings(global::BepInEx.BaseUnityPlugin plugin,string name,object vm,string xaml){}
 }
}
namespace CrusaderDE {public sealed class Translate {public static Translate Instance{get;}=new Translate();public Dictionary<string,string> GameTexts=new Dictionary<string,string>();}}
namespace Shared {public static class GameModeHelper {public static bool IsRealMultiplayer()=>true;}
 public static class DebugLogHelper {public static void LogInfo(BepInEx.Logging.ManualLogSource log,string text){}public static void LogWarning(BepInEx.Logging.ManualLogSource log,string text){}public static void LogError(BepInEx.Logging.ManualLogSource log,string text){throw new Exception(text);}}
}
