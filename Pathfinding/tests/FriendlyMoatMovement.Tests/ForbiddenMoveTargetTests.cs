using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class ForbiddenMoveTargetTests
{
    internal static void Validate(string root)
    {
        var dispatch = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "Pathfinding/src/UnitCommands/Native/PermanentCommandHooks.cs"))).GetRoot();
        var guard = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "Pathfinding/src/UnitCommands/Runtime/ForbiddenMoveTargets.cs"))).GetRoot();
        string members = string.Join("\n", dispatch.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text is "DispatchPlayerMove" or "DispatchMoveEvent" or "DispatchAppend")
            .Concat(guard.DescendantNodes().OfType<MethodDeclarationSyntax>()).Select(m => m.ToFullString()));
        var frame = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "Pathfinding/src/UnitCommands/Runtime/ManualGroupAdmission.cs"))).GetRoot();
        members += frame.DescendantNodes().OfType<StructDeclarationSyntax>().Single(m => m.Identifier.Text == "ManualPlayerMoveFrame").ToFullString();
        string fixture = """
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
using APIShared.Pathfinding; using SHCDESE.API; using SHCDESE.Interop; using SHCDESE.Interop.Enums;
namespace SHCDESE.Interop.Enums { internal enum AliveState { Dead, IsAlive } }
namespace SHCDESE.Interop { internal struct GameTribe { internal AliveState r_AliveState; internal int r_PlayerIdOwner; } }
namespace SHCDESE.API {
    internal unsafe sealed class GameTribeManagerAPI {
        internal static readonly GameTribeManagerAPI Instance = new(); internal GameTribe* Tribe; internal bool Throw;
        internal bool TryGetTribeById(int id, out GameTribe* tribe) {
            if(Throw)throw new InvalidOperationException("boundary failure"); tribe=id==1?Tribe:null; return tribe!=null;
        }
    }
}
namespace SHCDESE.EventAPI { internal enum EventHookPhase { Pre, Post } }
namespace SHCDESE.EventAPI.Tribes {
    internal sealed class TribeIssueOrderMoveHereEventArgs {
        internal SHCDESE.EventAPI.EventHookPhase Phase; internal int TribeId=1, TileX=20, TileY=20;
        internal bool SkipOriginalFunction; internal long ReturnValue=7;
    }
}
namespace Pathfinding.UnitCommands {
    using SHCDESE.EventAPI; using SHCDESE.EventAPI.Tribes;
    public unsafe sealed class ForbiddenFixture {
        private const int MapWidth=800, MaximumTribeCount=4500, TribeRecordSize=0x688;
        private bool EngineEnabled=true, TraversalEnabled=true, manualAdmissionReady=true;
        private IntPtr nativeTribeManager; private long playerMoveEntries;
        private ManualPlayerMoveFrame manualPlayerMove;
        private Action<IntPtr,int,int,int,int,int> nativePlayerMove, traversalMoveProvider;
        private Action<IntPtr,int,ushort,ushort,int,short> queueAppendProvider, nativeAppend;
        private long appendEntries; private int appendWrites;
        private void ContinueQueueAppend(IntPtr manager,int tribe,ushort x,ushort y,int index,short mode){appendWrites++;}
        private Stack<bool> moveEventObservers; private Stack<int> moveEventDepths;
        private Stack<ManualRegionAdmissionBatch> moveAdmissionParents; private List<int> manualCommandContexts=new();
        private sealed class ManualRegionAdmissionBatch {}
        private sealed class Formation { internal Action<TribeIssueOrderMoveHereEventArgs> Callback; internal void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args)=>Callback(args); }
        private Formation formationRuntime; private Action<TribeIssueOrderMoveHereEventArgs> queueMoveEvent;
        private int admissions, ends, destinations, observations, queueChanges, nativeCalls, captures, errors;
        private ManualRegionAdmissionBatch BeginManualGroupAdmission(TribeIssueOrderMoveHereEventArgs args) {
            admissions++;
            if(manualPlayerMove.Valid&&!manualPlayerMove.GroupSeen&&manualPlayerMove.Tribe==args.TribeId) {
                manualPlayerMove.GroupSeen=true;manualPlayerMove.GroupArguments=args;
            }
            return null;
        }
        private void EndManualGroupAdmission(ManualRegionAdmissionBatch p){ends++;}
        private void PrepareDefaultGroupDestinations(TribeIssueOrderMoveHereEventArgs args){destinations++;}
        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args){observations++;}
        private void RestoreManualCommandContext(){manualCommandContexts.RemoveAt(manualCommandContexts.Count-1);}
        private void ReportNativeDetourBarrier(string site,Exception error){errors++;}
        __MEMBERS__
        public static void Run(){
            int checks=0; void Check(bool ok,string why){checks++;if(!ok)throw new Exception("forbidden target: "+why);}
            Check(RouteTileRules.TryRegister("tests.pathfinding","forbidden-dispatch",out var rules,out _),"registration");
            IntPtr raw=Marshal.AllocHGlobal(0x3000);
            GameTribe group=new(){r_AliveState=AliveState.IsAlive,r_PlayerIdOwner=1};
            GameTribeManagerAPI.Instance.Tribe=&group;
            try{
                rules.Set(21,20,RouteTileEffect.Forbidden,players:1U<<1);
                var f=new ForbiddenFixture{nativeTribeManager=raw};
                ushort* mode=(ushort*)((byte*)raw+TribeRecordSize+0x56C); *mode=17;
                f.nativePlayerMove=(m,t,x,y,p,n)=>{f.playerMoveEntries++;f.nativeCalls++;*mode=99;};
                f.traversalMoveProvider=(m,t,x,y,p,n)=>{f.captures++;f.nativePlayerMove(m,t,x,y,p,n);};
                f.queueMoveEvent=a=>f.queueChanges++;
                f.formationRuntime=new(){Callback=a=>f.queueChanges+=100};
                f.DispatchPlayerMove(raw,1,21,20,0,128);
                Check(f.nativeCalls==0&&f.captures==0&&*mode==17&&!f.manualPlayerMove.Valid,
                    "player rejection precedes capture, native writes and frame mutation");
                f.queueAppendProvider=(m,t,x,y,i,n)=>f.appendWrites++;
                f.nativeAppend=(m,t,x,y,i,n)=>f.appendWrites++;
                f.DispatchAppend(raw,1,21,20,0,0);
                Check(f.appendWrites==0,"forbidden waypoint never modifies the existing queue");
                f.DispatchAppend(raw,1,20,20,0,0);
                Check(f.appendWrites==1,"allowed waypoint retains the existing queue insertion");
                var reject=new TribeIssueOrderMoveHereEventArgs{TileX=21};
                f.DispatchMoveEvent(reject);
                Check(reject.SkipOriginalFunction&&reject.ReturnValue==0&&f.admissions==0&&f.destinations==0&&
                    f.queueChanges==0&&f.observations==0&&f.moveAdmissionParents==null,
                    "direct rejection precedes admission, formation, queue and observed-order mutation");

                // The allowed command may originate on a Forbidden tile: only its destination matters.
                var accepted=new TribeIssueOrderMoveHereEventArgs();
                f.DispatchMoveEvent(accepted);
                Check(!accepted.SkipOriginalFunction&&f.admissions==1&&f.queueChanges==101&&f.observations==1,
                    "allowed destination leaves a forbidden origin and retains normal dispatch");
                var child=new TribeIssueOrderMoveHereEventArgs{TileX=21}; f.DispatchMoveEvent(child);
                Check(child.SkipOriginalFunction&&f.moveAdmissionParents.Count==1&&f.moveEventObservers.Count==1,
                    "nested rejected event does not pop accepted parent's scopes");
                accepted.Phase=EventHookPhase.Post; f.DispatchMoveEvent(accepted);
                Check(f.moveAdmissionParents.Count==0&&f.moveEventObservers.Count==0&&f.ends==1,
                    "accepted parent completes normally after rejected child");

                // The rule may become Forbidden between wrapper entry and its group event.
                rules.Clear(21,20); *mode=17;
                f.traversalMoveProvider=null;
                f.nativePlayerMove=(m,t,x,y,p,n)=>{
                    f.playerMoveEntries++;*mode=99;
                    rules.Set(x,y,RouteTileEffect.Forbidden,players:1U<<1);
                    f.DispatchMoveEvent(new(){TileX=x,TileY=y});
                };
                f.DispatchPlayerMove(raw,1,20,20,0,128);
                Check(*mode==17&&!f.manualPlayerMove.Valid,"late group veto restores only the wrapper's own earlier flag write");
                rules.Clear(20,20); rules.Set(21,20,RouteTileEffect.Forbidden,players:1U<<1); *mode=17;
                f.nativePlayerMove=(m,t,x,y,p,n)=>{
                    f.playerMoveEntries++;*mode=99;
                    var outer=new TribeIssueOrderMoveHereEventArgs();f.DispatchMoveEvent(outer);
                    f.DispatchMoveEvent(new(){TileX=21});
                    f.DispatchPlayerMove(raw,1,21,20,0,0);
                    outer.Phase=EventHookPhase.Post;f.DispatchMoveEvent(outer);
                };
                f.DispatchPlayerMove(raw,1,20,20,0,128);
                Check(*mode==99&&!f.manualPlayerMove.Valid,"nested forbidden event/wrapper never rolls back accepted parent's flag");

                f.nativePlayerMove=(m,t,x,y,p,n)=>{f.playerMoveEntries++;f.nativeCalls++;};
                int before=f.nativeCalls;
                rules.Set(20,20,RouteTileEffect.Penalty,254,1U<<1);f.DispatchPlayerMove(raw,1,20,20,0,0);
                Check(f.nativeCalls==before+1,"penalty is not mistaken for forbidden");
                group.r_PlayerIdOwner=2;before=f.nativeCalls;f.DispatchPlayerMove(raw,1,21,20,0,0);
                Check(f.nativeCalls==before+1,"player mask uses actual tribe owner");group.r_PlayerIdOwner=1;
                f.EngineEnabled=false;before=f.nativeCalls;f.DispatchPlayerMove(raw,1,21,20,0,0);
                Check(f.nativeCalls==before+1,"disabled engine leaves Vanilla command unchanged");f.EngineEnabled=true;
                foreach(int invalid in new[]{0,9}){
                    group.r_PlayerIdOwner=invalid;before=f.nativeCalls;f.DispatchPlayerMove(raw,1,21,20,0,0);
                    Check(f.nativeCalls==before+1,"invalid owner retains Vanilla handling");
                }
                group.r_PlayerIdOwner=1;group.r_AliveState=AliveState.Dead;before=f.nativeCalls;f.DispatchPlayerMove(raw,1,21,20,0,0);
                Check(f.nativeCalls==before+1,"dead tribe is not read as an active rule owner");group.r_AliveState=AliveState.IsAlive;
                before=f.nativeCalls;f.DispatchPlayerMove(raw,0,21,20,0,0);f.DispatchPlayerMove(raw,4500,21,20,0,0);
                f.DispatchPlayerMove(raw,1,-1,20,0,0);f.DispatchPlayerMove(raw,1,800,20,0,0);
                Check(f.nativeCalls==before+4,"invalid coordinates and tribe IDs retain Vanilla handling");
                GameTribeManagerAPI.Instance.Throw=true;before=f.nativeCalls;int oldErrors=f.errors;
                f.DispatchPlayerMove(raw,1,21,20,0,0);
                Check(f.nativeCalls==before+1&&f.errors==oldErrors+1,"boundary error is contained and Vanilla runs exactly once");
                Console.WriteLine("PASS: "+checks+" production forbidden-target dispatch, queue preservation and nested-frame checks.");
            }finally{rules.ClearAll();GameTribeManagerAPI.Instance.Throw=false;GameTribeManagerAPI.Instance.Tribe=null;Marshal.FreeHGlobal(raw);}
        }
    }
}
""";
        fixture = fixture.Replace("__MEMBERS__", members);
        string apiPath = Environment.GetEnvironmentVariable("MOAT_TEST_API_SHARED_DLL") ??
            Path.Combine(root, "APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll");
        Assembly.LoadFrom(Path.GetFullPath(apiPath));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).Append(MetadataReference.CreateFromFile(apiPath));
        var compilation = CSharpCompilation.Create("ProductionForbiddenMoveTargets", new[] { CSharpSyntaxTree.ParseText(fixture) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var bytes = new MemoryStream(); var result = compilation.Emit(bytes);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(bytes.ToArray()).GetType("Pathfinding.UnitCommands.ForbiddenFixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw(); }
    }
}
