using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class FormationStartupTests
{
    internal static void Validate(string root)
    {
        string[] paths = {
            "BugfixesAndQoL/src/FormationFeature.cs", "BugfixesAndQoL/src/FormationMenuViewModel.cs",
            "BugfixesAndQoL/src/FormationSelectionMigration.cs", "APIShared/src/UnitCommands/FormationModel.cs",
            "APIShared/src/UnitCommands/FormationPresentation.cs"
        };
        const string fixture = """
using System; using System.Reflection; using System.Collections.Generic; using System.ComponentModel;
using APIShared.UnitCommands; using BepInEx.Configuration; using BepInEx.Logging;
namespace BepInEx { public static class Paths { public static string ConfigPath="__missing_formation_fixture__"; } }
namespace BepInEx.Logging { public class ManualLogSource {} }
namespace BepInEx.Configuration {
    public class ConfigDescription { public ConfigDescription(string s,object range) {} }
    public class AcceptableValueRange<T> { public AcceptableValueRange(T a,T b) {} }
    public class ConfigEntry<T> {
        private T value;
        public ConfigEntry(T initial) { value=initial; }
        public T Value {get=>value;set {this.value=value;SettingChanged?.Invoke(this,EventArgs.Empty);} }
        public event EventHandler SettingChanged;
    }
    public class ConfigFile {
        public string ConfigFilePath=>"__missing_formation_fixture__.cfg";
        public readonly Dictionary<string,object> Entries=new Dictionary<string,object>();
        public ConfigEntry<T> Bind<T>(string section,string key,T initial,object description) {
            var entry=new ConfigEntry<T>(initial);Entries[key]=entry;return entry;
        }
        public int Saves; public void Save() { Saves++; }
    }
}
namespace Noesis {
    public struct Color { public static Color FromArgb(byte a,byte r,byte g,byte b)=>default; }
    public class SolidColorBrush { public SolidColorBrush(Color color) {} }
}
namespace CrusaderDE {
    public class MainViewModel : INotifyPropertyChanged {
        public static bool viewModelLoaded=true; public static MainViewModel Instance=new MainViewModel();
        public bool Show_HUD_Troops=true; public bool Show_HUD_ControlGroups;
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify(string name) { PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name)); }
    }
    public static class Enums { public enum SceneIDS { ActualMainGame,Other } }
    public static class FatControler { public static Enums.SceneIDS currentScene=Enums.SceneIDS.ActualMainGame; }
}
namespace SHCDESE.API.LowLevel { public class CrusaderLibraryLoadContext {} }
namespace SHCDESE.API {
    public class GameXAMLManagerAPI {
        public static GameXAMLManagerAPI Instance=new GameXAMLManagerAPI();
        public readonly Dictionary<string,object> Hosts=new Dictionary<string,object>();
        public readonly List<string> Attempts=new List<string>(); public string ThrowOnHost;
        public void RegisterBinding(string host,object vm) {
            Attempts.Add(host);if(host==ThrowOnHost)throw new InvalidOperationException("Host failure");Hosts[host]=vm;
        }
    }
}
namespace Shared {
    public static class UnityMainThreadDispatch { public static void TryRunInlineOrEnqueue(Action a)=>a(); }
    public static class DebugLogHelper {
        public static bool IsCurrentNativeLibraryVersion()=>true;
        public static void LogDebug(ManualLogSource l,string m) {}
        public static void LogWarning(ManualLogSource l,string m) {}
        public static void LogError(ManualLogSource l,string m) {}
    }
}
namespace APIShared.UnitCommands {
    internal static class UnitCommandPathAPI { internal static object Runtime,MoveMarkers; }
    internal sealed class FormationRuntime {
        internal static FormationRuntime Last; internal static bool ThrowOnInitialize;
        private readonly BugfixesAndQoL.BugfixesAndQoLViewModel settings; internal bool Failed;
        internal bool Enabled=>!Failed && settings.EnableMod && settings.EnableMoveFormationEnhancements;
        internal FormationRuntime(ManualLogSource l,SHCDESE.API.LowLevel.CrusaderLibraryLoadContext c,
            ConfigEntry<FormationKind> k,ConfigEntry<int> d,ConfigEntry<RangedPlacementMode> p,
            IFormationPresentation m,BugfixesAndQoL.BugfixesAndQoLViewModel s,object commands,object markers) { settings=s;Last=this; }
        internal void Initialize() { if(ThrowOnInitialize) { Failed=true;throw new InvalidOperationException("Runtime failure"); } }
        internal void ResetTransientState() {}
    }
}
namespace BugfixesAndQoL {
    internal class BugfixesAndQoLViewModel : INotifyPropertyChanged {
        public bool EnableMod=true,EnableMoveFormationEnhancements=true;
        public event PropertyChangedEventHandler PropertyChanged;
        internal void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(EnableMoveFormationEnhancements)));
    }
    internal static class FormationPreviewOverlay {
        internal static void Initialize(ManualLogSource l,bool roles) {}
        internal static void SetNativeAllowed(bool value) {} internal static void Refresh() {} internal static void Clear() {}
        internal static void Publish(FormationPreviewPoint[] p,FormationDirectionIndicator d) {}
        internal static void SetRoleMarkersVisible(bool value) {}
    }
    public static class StartupFixture {
        private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
        private static FormationMenuViewModel Configure(ConfigFile config,BugfixesAndQoLViewModel options) {
            FormationFeature.Configure(new ManualLogSource(),config,options);
            return (FormationMenuViewModel)SHCDESE.API.GameXAMLManagerAPI.Instance.Hosts["BugfixesAndQoLFormationMenuHost"];
        }
        private static void Reset() {
            typeof(FormationFeature).GetField("runtime",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
        }
        public static void Run() {
            var manager=SHCDESE.API.GameXAMLManagerAPI.Instance;
            var options=new BugfixesAndQoLViewModel(); var config=new ConfigFile();
            manager.ThrowOnHost="BugfixesAndQoLFormationButtonHost";
            var menu=Configure(config,options);
            Require(manager.Attempts.Count==3 && manager.Hosts.Count==2,"Registration failure prevented other hosts");
            Require(!menu.FeatureAvailable && !menu.MenuVisible && !menu.RolloverVisible,"Unavailable menu starts visible");
            FormationFeature.Initialize(new SHCDESE.API.LowLevel.CrusaderLibraryLoadContext());
            menu.ToggleMenuCommand.Execute(null);menu.ShowRolloverCommand.Execute("Aufstellung");
            Require(!menu.MenuVisible && !menu.RolloverVisible,"Missing shared capability opened menu");
            manager.ThrowOnHost=null;manager.Hosts.Clear();manager.Attempts.Clear();
            menu=Configure(config,options);
            Require(manager.Hosts.Count==3 && manager.Attempts.Count==3,"Early registration needs successful native initialization");
            UnitCommandPathAPI.Runtime=new object();UnitCommandPathAPI.MoveMarkers=new object();
            FormationRuntime.ThrowOnInitialize=true;
            FormationFeature.Initialize(new SHCDESE.API.LowLevel.CrusaderLibraryLoadContext());
            Require(!menu.FeatureAvailable && !menu.MenuVisible && !menu.RolloverVisible,"Failed runtime made menu available");
            Reset();FormationRuntime.ThrowOnInitialize=false;
            var changed=new List<string>();menu.PropertyChanged+=(_,e)=>changed.Add(e.PropertyName);
            FormationFeature.Initialize(new SHCDESE.API.LowLevel.CrusaderLibraryLoadContext());
            Require(menu.FeatureAvailable && changed.Contains("FeatureAvailable"),"Successful initialization did not notify availability");
            menu.ToggleMenuCommand.Execute(null);Require(menu.MenuVisible,"Open command failed");
            menu.ToggleMenuCommand.Execute(null);Require(!menu.MenuVisible,"Close command failed");
            foreach(var kind in Enum.GetValues<FormationKind>()) {
                menu.SelectFormationCommand.Execute(kind.ToString());
                Require(((ConfigEntry<FormationKind>)config.Entries["Kind"]).Value==kind,"Selection button failed");
                bool[] icons={menu.IsVanilla,menu.IsBlock,menu.IsLine,menu.IsColumn,menu.IsWedge,menu.IsCircle};
                Require(icons[(int)kind] && Array.FindAll(icons,v=>v).Length==1,"Selected icon mismatch");
                ((ConfigEntry<FormationKind>)config.Entries["Kind"]).Value=kind;
                Require(changed.Contains("Is"+kind),"Config change did not notify selected icon");
                changed.Clear();
            }
            menu.SelectDensityCommand.Execute("4");menu.SelectPlacementCommand.Execute("Rear");menu.SelectRoleMarkersCommand.Execute("False");
            Require(((ConfigEntry<int>)config.Entries["Density"]).Value==4 &&
                ((ConfigEntry<RangedPlacementMode>)config.Entries["RangedPlacement"]).Value==RangedPlacementMode.Rear &&
                !((ConfigEntry<bool>)config.Entries["ShowRoleMarkers"]).Value,"Window setting commands failed");
            menu.ToggleMenuCommand.Execute(null);menu.ShowRolloverCommand.Execute("Aufstellung");
            Require(menu.MenuVisible && menu.RolloverVisible,"Healthy menu/rollover did not open");
            FormationRuntime.Last.Failed=true;menu.RefreshHostState();
            Require(!menu.FeatureAvailable && !menu.MenuVisible && !menu.RolloverVisible,"Later runtime failure left UI active");
            menu.SelectFormationCommand.Execute("Block");
            Require(menu.IsCircle,"Unavailable selection changed config");
            FormationRuntime.Last.Failed=false;menu.RefreshHostState();menu.ToggleMenuCommand.Execute(null);
            options.EnableMoveFormationEnhancements=false;options.Notify();
            Require(!menu.FeatureAvailable && !menu.MenuVisible && !menu.RolloverVisible,"Checkbox did not close unavailable menu");
            options.EnableMoveFormationEnhancements=true;menu.RefreshHostState();menu.ToggleMenuCommand.Execute(null);
            CrusaderDE.FatControler.currentScene=CrusaderDE.Enums.SceneIDS.Other;menu.RefreshHostState();
            Require(!menu.MenuVisible,"Scene change did not close menu");
            CrusaderDE.FatControler.currentScene=CrusaderDE.Enums.SceneIDS.ActualMainGame;
            CrusaderDE.MainViewModel.Instance=new CrusaderDE.MainViewModel();menu.RefreshHostState();menu.ToggleMenuCommand.Execute(null);
            Require(menu.MenuVisible,"Menu did not recover after map/host replacement");
            CrusaderDE.MainViewModel.Instance.Show_HUD_Troops=false;
            CrusaderDE.MainViewModel.Instance.Notify("Show_HUD_Troops");
            Require(!menu.MenuVisible,"Deselection did not close menu");
        }
    }
}
""";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var trees = paths.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,p))))
            .Append(CSharpSyntaxTree.ParseText(fixture));
        var compilation = CSharpCompilation.Create("ActualFormationStartupFixture",trees,references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();var result=compilation.Emit(output);
        if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("BugfixesAndQoL.StartupFixture")!.GetMethod("Run")!.Invoke(null,null); }
        catch(TargetInvocationException e) { throw e.InnerException??e; }
        Console.WriteLine("PASS: actual Configure/Initialize/menu commands executed: independent early hosts, initialization/runtime failure, checkbox, six icons/config changes, close/open and map/selection recovery.");
    }
}
