using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class FormationStartupTests
{
    internal static void Validate(string root)
    {
        string[] paths = {
            "BugfixesAndQoL/src/FormationFeature.cs", "BugfixesAndQoL/src/FormationMenuViewModel.cs",
            "BugfixesAndQoL/src/FormationSelectionMigration.cs", "APIShared/src/UnitCommands/FormationModel.cs",
            "APIShared/src/UnitCommands/FormationPresentation.cs", "BugfixesAndQoL/src/ArrangementTooltipFontConverter.cs"
        };
        var gestureUpdate = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "APIShared/src/UnitCommands/FormationRuntime.cs"))).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "UpdateGesture").ToFullString();
        string fixture = """
using System; using System.Reflection; using System.Collections.Generic; using System.ComponentModel;
using APIShared.UnitCommands; using BepInEx.Configuration; using BepInEx.Logging;
public static class SerpLocalization { public static string Language="en"; public static string Get(string key)=>key=="BugfixesAndQoL.ArrangementTooltip"?(Language=="de"?"Aufstellung":"Arrangement"):key; }
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
        public Dictionary<string,object> Stored=new Dictionary<string,object>();
        public ConfigEntry<T> Bind<T>(string section,string key,T initial,object description) {
            var entry=new ConfigEntry<T>(Stored.TryGetValue(key,out var saved)?(T)saved:initial);Entries[key]=entry;return entry;
        }
        public int Saves; public void Save() {
            Saves++;foreach(var pair in Entries)Stored[pair.Key]=pair.Value.GetType().GetProperty("Value").GetValue(pair.Value);
        }
    }
}
namespace Noesis {
    public enum Visibility { Visible,Hidden,Collapsed }
    public class MouseEventArgs : EventArgs {}
    public delegate void MouseEventHandler(object sender,MouseEventArgs args);
    public class TextBlock { public Visibility Visibility=Visibility.Hidden; }
    public class Button {
        private MouseEventHandler enter,leave;
        public int EnterHandlers,LeaveHandlers;
        public event MouseEventHandler MouseEnter { add {enter+=value;EnterHandlers++;} remove {enter-=value;EnterHandlers--;} }
        public event MouseEventHandler MouseLeave { add {leave+=value;LeaveHandlers++;} remove {leave-=value;LeaveHandlers--;} }
        public void Enter()=>enter?.Invoke(this,new MouseEventArgs());
        public void Leave()=>leave?.Invoke(this,new MouseEventArgs());
    }
    public interface IValueConverter {
        object Convert(object v,Type t,object p,System.Globalization.CultureInfo c);
        object ConvertBack(object v,Type t,object p,System.Globalization.CultureInfo c);
    }
    public struct Color { public static Color FromArgb(byte a,byte r,byte g,byte b)=>default; }
    public class SolidColorBrush { public SolidColorBrush(Color color) {} }
}
namespace CrusaderDE {
    public class MainViewModel : INotifyPropertyChanged {
        public static bool viewModelLoaded=true; public static MainViewModel Instance=new MainViewModel();
        public bool Show_HUD_Troops=true; public bool Show_HUD_ControlGroups;
        public string TroopsPanelRollover {get;set;}
        public HUD_Troops HUDTroopPanel=new HUD_Troops();
        public string TroopsPanelRollover_AmountReq1,TroopsPanelRollover_AmountGot1;
        public object TroopsPanelRollover_GoodsImage1;
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify(string name) { PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name)); }
    }
    public class HUD_Troops {
        public Noesis.Button OpenButton=new Noesis.Button();
        public Noesis.TextBlock RefTroopsPanelRollover=new Noesis.TextBlock(),RefTroopsPanelRollover2=new Noesis.TextBlock();
        public object FindName(string name)=>name=="BugfixesAndQoLFormationOpenButton"?OpenButton:null;
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
namespace APIShared.Internal {
    public static class UnityMainThreadDispatch { public static void TryRunInlineOrEnqueue(Action a)=>a(); }
    public static class DebugLogHelper {
        public static bool IsCurrentNativeLibraryVersion()=>true;
        public static void LogDebug(ManualLogSource l,string m) {}
        public static void LogWarning(ManualLogSource l,string m) {}
        public static void LogError(ManualLogSource l,string m) {}
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
            var main=CrusaderDE.MainViewModel.Instance;main.Show_HUD_Troops=true;
            var panel=main.HUDTroopPanel;var button=panel.OpenButton;
            menu.RefreshHostState();menu.RefreshHostState();
            Require(button.EnterHandlers==1 && button.LeaveHandlers==1,"Duplicate direct handlers");
            main.TroopsPanelRollover_AmountReq1="cost";main.TroopsPanelRollover_AmountGot1="body";main.TroopsPanelRollover_GoodsImage1=new object();
            panel.RefTroopsPanelRollover2.Visibility=Noesis.Visibility.Visible;button.Enter();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible && panel.RefTroopsPanelRollover2.Visibility==Noesis.Visibility.Hidden && main.TroopsPanelRollover=="Arrangement" && !menu.RolloverVisible,"Direct event did not show short Vanilla rollover");
            Require(main.TroopsPanelRollover_AmountReq1=="" && main.TroopsPanelRollover_AmountGot1=="" && main.TroopsPanelRollover_GoodsImage1==null,"Old costs leaked");
            button.Leave();Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden,"Leave did not hide");
            SerpLocalization.Language="de";button.Enter();Require(main.TroopsPanelRollover=="Aufstellung","German hover missing");SerpLocalization.Language="en";
            main.TroopsPanelRollover="Different Vanilla button";button.Leave();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible,"Foreign short hover hidden");
            button.Enter();main.TroopsPanelRollover="Knight";panel.RefTroopsPanelRollover.Visibility=Noesis.Visibility.Hidden;panel.RefTroopsPanelRollover2.Visibility=Noesis.Visibility.Visible;button.Leave();
            Require(panel.RefTroopsPanelRollover2.Visibility==Noesis.Visibility.Visible,"Knight hover hidden");
            button.Enter();menu.CloseMenu();Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden,"Close did not hide");
            button.Enter();FormationRuntime.Last.Failed=true;menu.RefreshHostState();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden && button.EnterHandlers==0,"Runtime failure left hover/handlers");
            FormationRuntime.Last.Failed=false;menu.RefreshHostState();button.Enter();
            CrusaderDE.FatControler.currentScene=CrusaderDE.Enums.SceneIDS.Other;menu.RefreshHostState();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden && button.EnterHandlers==0,"Scene change left hover/handlers");
            CrusaderDE.FatControler.currentScene=CrusaderDE.Enums.SceneIDS.ActualMainGame;menu.RefreshHostState();button.Enter();
            main.Show_HUD_Troops=false;main.Notify("Show_HUD_Troops");
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden && button.EnterHandlers==0,"Deselection left hover/handlers");
            main.Show_HUD_Troops=true;menu.RefreshHostState();button.Enter();
            var replacement=new CrusaderDE.HUD_Troops();main.HUDTroopPanel=replacement;main.TroopsPanelRollover="Replacement button";replacement.RefTroopsPanelRollover.Visibility=Noesis.Visibility.Visible;
            menu.RefreshHostState();menu.RefreshHostState();
            Require(button.EnterHandlers==0 && button.LeaveHandlers==0 && replacement.OpenButton.EnterHandlers==1 && replacement.OpenButton.LeaveHandlers==1,"HUD replacement handlers incorrect");
            Require(replacement.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible,"Replacement hover hidden");
            button.Enter();Require(main.TroopsPanelRollover=="Replacement button","Old button still active");
            replacement.OpenButton.Enter();Require(main.TroopsPanelRollover=="Arrangement","New HUD hover inactive");replacement.OpenButton.Leave();
            var newMain=new CrusaderDE.MainViewModel();CrusaderDE.MainViewModel.Instance=newMain;menu.RefreshHostState();
            Require(replacement.OpenButton.EnterHandlers==0 && newMain.HUDTroopPanel.OpenButton.EnterHandlers==1,"Viewmodel replacement handlers incorrect");
            newMain.HUDTroopPanel.OpenButton.Enter();main.Show_HUD_Troops=false;main.Notify("Show_HUD_Troops");
            Require(newMain.HUDTroopPanel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible,"Old viewmodel subscription remained");
            newMain.HUDTroopPanel.OpenButton.Leave();
            var converter=new ArrangementTooltipFontConverter();
            foreach(float size in new[]{16f,20f,36f}) {
                object doubled=converter.Convert(size,typeof(float),null,System.Globalization.CultureInfo.InvariantCulture);
                Require(doubled is float f && f==size*2f,"Scaled tooltip font is not doubled float");
            }
            RememberedRowsFixture.Run(menu,config,options);
        }
    }
}
""";
        fixture += """
namespace UnityEngine {
    public static class Time { public static int frameCount; }
    public static class Input { public static (float x,float y) mouseScrollDelta; }
}
namespace APIShared.UnitCommands {
    using UnityEngine;
    internal sealed class RuntimeGestureFixture {
        private readonly object stateSync=new object(); private ActiveDrag drag;
        private int lastWheelFrame=-1;private const int MinimumDragTileDistance=2;
        private IFormationPresentation menuViewModel;private bool valid=true;
        private struct GroundTarget { internal int NativeX,NativeY; }
        private sealed class Gate { internal bool CanModify=true; }
        private sealed class ActiveDrag {
            internal FormationKind Kind=FormationKind.Block;
            internal FormationGestureState Geometry;
            internal Gate ReleaseGate=new Gate();internal int Rows=>Geometry.Rows;
            internal GroundTarget Target;
            internal AuthorizationFixture Authorization=new AuthorizationFixture();
        }
        private sealed class AuthorizationFixture { internal bool IsConfirmed=true; }
        private bool ValidateActiveDrag(ActiveDrag state)=>valid;
        private bool TryCaptureTarget(out GroundTarget target) { target=new GroundTarget {NativeX=10,NativeY=0};return true; }
        private void PublishPreview(ActiveDrag state,bool force) {}
        private void AbortDrag(string reason) { drag=null; }
        private void LogDebugNoThrow(string message) {}
        __PRODUCTION_UPDATE__
        internal static void Run(IFormationPresentation menu) {
            var f=new RuntimeGestureFixture {menuViewModel=menu};
            f.drag=new ActiveDrag {Geometry=new FormationGestureState(FormationKind.Block,31,0,menu.GetRememberedRows(FormationKind.Block))};
            int start=f.drag.Rows;
            Time.frameCount=100;Input.mouseScrollDelta=(0,-0.5f);f.UpdateGesture(f.drag);
            if(menu.GetRememberedRows(FormationKind.Block)!=0 || f.drag.Rows!=start)throw new Exception("Partial wheel saved automatic rows");
            Time.frameCount=101;f.UpdateGesture(f.drag);
            if(f.drag.Rows!=start+1 || menu.GetRememberedRows(FormationKind.Block)!=start+1)throw new Exception("Production wheel change was not remembered");
            int direction=f.drag.Geometry.Direction;
            Time.frameCount=102;Input.mouseScrollDelta=(0,3);f.UpdateGesture(f.drag);
            if(f.drag.Rows!=Math.Max(1,start-2) || direction!=f.drag.Geometry.Direction)throw new Exception("Multi-step wheel changed facing or wrong rows");
            int saved=f.drag.Rows;f.valid=false;Time.frameCount=103;f.UpdateGesture(f.drag);
            if(f.drag!=null || menu.GetRememberedRows(FormationKind.Block)!=saved)throw new Exception("Abort lost remembered wheel selection");
        }
    }
}
namespace BugfixesAndQoL {
    internal static class RememberedRowsFixture {
        private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
        internal static void Run(FormationMenuViewModel menu,ConfigFile config,BugfixesAndQoLViewModel options) {
            Check(menu.RememberRows && menu.GetRememberedRows(FormationKind.Block)==0,"Remember default or automatic start failed");
            RuntimeGestureFixture.Run(menu);
            menu.RememberSelectedRows(FormationKind.Block,3);
            Check(new FormationGestureState(FormationKind.Block,31,0,menu.GetRememberedRows(FormationKind.Block)).Rows==3,"31-unit rows not restored");
            Check(new FormationGestureState(FormationKind.Block,2,0,menu.GetRememberedRows(FormationKind.Block)).Rows==2,"Small group was not clamped");
            Check(menu.GetRememberedRows(FormationKind.Block)==3 && new FormationGestureState(FormationKind.Block,31,0,menu.GetRememberedRows(FormationKind.Block)).Rows==3,"Clamp overwrote remembered count");
            foreach(var kind in new[]{FormationKind.Line,FormationKind.Column,FormationKind.Wedge}) {
                Check(menu.GetRememberedRows(kind)==0,"Another kind inherited block rows");
                menu.RememberSelectedRows(kind,4+(int)kind);
                Check(menu.GetRememberedRows(kind)==4+(int)kind && menu.GetRememberedRows(FormationKind.Block)==3,"Kinds not independent");
            }
            int saves=config.Saves;
            menu.RememberSelectedRows(FormationKind.Circle,12);menu.RememberSelectedRows(FormationKind.Vanilla,12);
            menu.RememberSelectedRows(FormationKind.Block,0);menu.RememberSelectedRows(FormationKind.Block,10001);
            menu.RememberSelectedRows(FormationKind.Block,3);
            Check(config.Saves==saves,"Fixed shapes, invalid or unchanged rows were saved");
            var notifications=new List<string>();menu.PropertyChanged+=(_,e)=>notifications.Add(e.PropertyName);
            menu.RememberRows=false;
            Check(!menu.RememberRows && notifications.Contains("RememberRows"),"Checkbox did not notify/configure");
            menu.RememberSelectedRows(FormationKind.Block,9);
            Check(menu.GetRememberedRows(FormationKind.Block)==0 && ((ConfigEntry<int>)config.Entries["RememberedBlockRows"]).Value==3,"Disabled remembering overwrote stored rows");
            Check(new FormationGestureState(FormationKind.Block,31,0,menu.GetRememberedRows(FormationKind.Block)).Rows==FormationModel.ResolveAutomaticRows(FormationKind.Block,31),"Disabled mode not automatic");
            var reload=new ConfigFile {Stored=new Dictionary<string,object>(config.Stored)};
            FormationFeature.Configure(new ManualLogSource(),reload,options);
            var restored=(FormationMenuViewModel)SHCDESE.API.GameXAMLManagerAPI.Instance.Hosts["BugfixesAndQoLFormationMenuHost"];
            Check(!restored.RememberRows,"Checkbox persistence failed");restored.RememberRows=true;
            Check(restored.GetRememberedRows(FormationKind.Block)==3 && restored.GetRememberedRows(FormationKind.Wedge)==8,"Rows persistence or re-enable failed");
            foreach(int count in new[]{1,2,31,10000})foreach(var kind in Enum.GetValues<FormationKind>()) {
                var g=new FormationGestureState(kind,count,7,restored.GetRememberedRows(kind));
                int rows=g.Rows;g.UpdateDirection(15,4,2);Check(g.Rows==rows,"Facing changed remembered rows");
                int direction=g.Direction;g.ApplyWheel(10001,1);Check(g.Direction==direction && g.Rows>=1 && g.Rows<=count,"Wheel limits or facing failed");
                if(kind==FormationKind.Circle || kind==FormationKind.Vanilla)Check(g.Rows==rows,"Fixed shape geometry changed");
            }
            reload.Stored["RememberedBlockRows"]=-1;reload.Stored["RememberedLineRows"]=10001;
            var invalid=new ConfigFile {Stored=new Dictionary<string,object>(reload.Stored)};
            FormationFeature.Configure(new ManualLogSource(),invalid,options);
            var repaired=(FormationMenuViewModel)SHCDESE.API.GameXAMLManagerAPI.Instance.Hosts["BugfixesAndQoLFormationMenuHost"];
            Check(repaired.GetRememberedRows(FormationKind.Block)==0 && repaired.GetRememberedRows(FormationKind.Line)==0,"Invalid config did not select automatic rows");
        }
    }
}
""".Replace("__PRODUCTION_UPDATE__", gestureUpdate);
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
        Console.WriteLine("PASS: actual Configure/Initialize/menu commands executed: independent early hosts, initialization/runtime failure, checkbox, six icons/config changes, close/open and map/selection recovery; production wheel update, remembered rows, group clamping, independent kinds and config reload; localized Vanilla hover ownership and doubled Noesis font presets.");
    }
}
