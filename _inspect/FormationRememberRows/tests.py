from pathlib import Path
import shutil
root=Path.cwd()
def read(p):
    f=root/p; b=root/'_inspect/FormationRememberRows/before'/p
    if not b.exists():b.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(f,b)
    return f.read_text(encoding='utf-8-sig')
def write(p,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');(root/p).write_bytes(data);assert (root/p).read_bytes()==data
p='BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/FormationStartupTests.cs';s=read(p)
s=s.replace('using Microsoft.CodeAnalysis.CSharp;','using Microsoft.CodeAnalysis.CSharp;\nusing Microsoft.CodeAnalysis.CSharp.Syntax;')
s=s.replace('        const string fixture =', '''        var gestureUpdate = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "APIShared/src/UnitCommands/FormationRuntime.cs"))).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "UpdateGesture").ToFullString();
        string fixture =''')
s=s.replace('namespace BepInEx {','public static class SerpLocalization { public static string Get(string key)=>key; }\nnamespace BepInEx {')
s=s.replace('public readonly Dictionary<string,object> Entries=new Dictionary<string,object>();','''public readonly Dictionary<string,object> Entries=new Dictionary<string,object>();
        public Dictionary<string,object> Stored=new Dictionary<string,object>();''')
s=s.replace('var entry=new ConfigEntry<T>(initial);','var entry=new ConfigEntry<T>(Stored.TryGetValue(key,out var saved)?(T)saved:initial);')
s=s.replace('public int Saves; public void Save() { Saves++; }','''public int Saves; public void Save() {
            Saves++;foreach(var pair in Entries)Stored[pair.Key]=pair.Value.GetType().GetProperty("Value").GetValue(pair.Value);
        }''')
s=s.replace('            Require(!menu.MenuVisible,"Deselection did not close menu");','''            Require(!menu.MenuVisible,"Deselection did not close menu");
            RememberedRowsFixture.Run(menu,config,options);''')
extra='''
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
        }
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
'''
import json
s=s.replace('        var references =', '        fixture += '+json.dumps(extra)+'.Replace("__PRODUCTION_UPDATE__", gestureUpdate);\n        var references =')
s=s.replace('config changes, close/open and map/selection recovery.','config changes, close/open and map/selection recovery; production wheel update, remembered rows, group clamping, independent kinds and config reload.')
write(p,s)
p='BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p)
row=s.index('<Grid Grid.Row="3">')
s=s[:row]+s[row:].replace('<Button Width="76"','<Button Width="64"').replace('<CheckBox Width="150"','<CheckBox Width="166" Height="28" Foreground="#FFF1E2B7" FlowDirection="{Binding GlobalTextFlowAL2R, Source={x:Static local:MainViewModel.Instance}}"')
write(p,s)
write('_inspect/FormationRememberRows/tests.py',Path(__file__).read_text())
