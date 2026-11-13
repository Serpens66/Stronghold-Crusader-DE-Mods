using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class AnchorMoveVetoTests
{
    // Both production subscribers run in their actual registration order. Game/native
    // boundaries are counters; the SE publisher contract supplies no Post after a veto.
    internal static void Run(string root)
    {
        string ReadMethod(string file, string method) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, file)))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == method).ToFullString();
        string members = ReadMethod("Pathfinding/src/UnitCommands/Native/PermanentCommandHooks.cs", "DispatchMoveEvent") +
            ReadMethod("Pathfinding/src/UnitCommands/Runtime/ForbiddenMoveTargets.cs", "IsForbiddenMoveTarget") +
            ReadMethod("Pathfinding/src/Engine/FastMovementScheduler.cs", "OnAnchorMoveHere");
        string fixture = """
using System; using System.Collections.Generic; using System.Diagnostics;
using APIShared.Pathfinding; using SHCDESE.API; using SHCDESE.Interop; using SHCDESE.Interop.Enums;
using SHCDESE.EventAPI; using SHCDESE.EventAPI.Tribes;
namespace SHCDESE.Interop.Enums { internal enum AliveState { Dead, IsAlive } internal enum TribeMoveType { Default } }
namespace SHCDESE.Interop { internal struct GameTribe { internal AliveState r_AliveState; internal int r_PlayerIdOwner; } }
namespace SHCDESE.API {
    internal unsafe sealed class GameTribeManagerAPI {
        internal static readonly GameTribeManagerAPI Instance=new(); internal GameTribe* Tribe;
        internal bool TryGetTribeById(int id,out GameTribe* tribe){tribe=id==1?Tribe:null;return tribe!=null;}
    }
}
namespace SHCDESE.EventAPI { internal enum EventHookPhase { Pre,Post } }
namespace SHCDESE.EventAPI.Tribes {
    internal sealed class TribeIssueOrderMoveHereEventArgs {
        internal EventHookPhase Phase; internal int TribeId=1,TileX=20,TileY=20;
        internal short IsPatrolPath; internal bool IsNewOrder=true,SkipOriginalFunction;
        internal long ReturnValue; internal TribeMoveType MoveType;
    }
}
namespace Pathfinding.UnitCommands {
    internal static class QueueNativeContract { internal static bool TryDecodeQueuedMoveType(int type,out int value){value=0;return false;} }
    public unsafe sealed class VetoFixture {
        private const int MapWidth=800,MaximumTribeCount=4500;
        private bool EngineEnabled=true,anchorAvailable=true;
        private int anchorSimThread=Environment.CurrentManagedThreadId,anchorEndedUnreached;
        private long anchorPreTicks,anchorPreMaxTicks;
        private sealed class Runtime { internal bool EngineEnabled=true; internal int Errors; internal void TryLogDiagnosticFailure(string label,Exception error){Errors++;} }
        private readonly Runtime runtime=new();
        private struct ManualPlayerMoveFrame { internal bool Valid,GroupSeen,Rejected; internal int Tribe; internal object GroupArguments; }
        private ManualPlayerMoveFrame manualPlayerMove;
        private Stack<bool> moveEventObservers; private Stack<int> moveEventDepths;
        private Stack<ManualRegionAdmissionBatch> moveAdmissionParents;
        private readonly List<int> manualCommandContexts=new();
        private sealed class ManualRegionAdmissionBatch {}
        private sealed class Formation {
            internal bool Veto; internal void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args){if(Veto&&args.Phase==EventHookPhase.Pre)args.SkipOriginalFunction=true;}
        }
        private Formation formationRuntime=new();
        private Action<TribeIssueOrderMoveHereEventArgs> queueMoveEvent;
        private readonly List<int> anchorFrames=new();
        private int sequence,captures,opens,closes,nativeCalls,observedCommands;
        private sealed class AnchorTaskSpec {}
        private ManualRegionAdmissionBatch BeginManualGroupAdmission(TribeIssueOrderMoveHereEventArgs args)=>null;
        private void EndManualGroupAdmission(ManualRegionAdmissionBatch parent){}
        private void PrepareDefaultGroupDestinations(TribeIssueOrderMoveHereEventArgs args){}
        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args){observedCommands++;}
        private void RestoreManualCommandContext(){manualCommandContexts.RemoveAt(manualCommandContexts.Count-1);}
        private void ReportNativeDetourBarrier(string label,Exception error){runtime.Errors++;}
        private void ObserveTribeOrder(int id,bool move,int x,int y,short patrol,bool newOrder,TribeMoveType type,bool queued){sequence++;}
        private void TryCaptureAnchorMoveSpecs(int id,int x,int y,out AnchorTaskSpec digger,out AnchorTaskSpec ground,out int[] members){captures++;digger=ground=null;members=null;}
        private void BeginAnchorFrame(int id,int task,int ground,int[] members,AnchorTaskSpec digger,AnchorTaskSpec groundSpec){opens++;anchorFrames.Add(id);}
        private void EndAnchorFrame(int id){closes++;anchorFrames.RemoveAt(anchorFrames.Count-1);}
        private void LogAnchorUnreached(){}
        __MEMBERS__
        private void Pre(TribeIssueOrderMoveHereEventArgs args){DispatchMoveEvent(args);OnAnchorMoveHere(args);}
        private void Post(TribeIssueOrderMoveHereEventArgs args){args.Phase=EventHookPhase.Post;DispatchMoveEvent(args);OnAnchorMoveHere(args);}
        private void Publish(TribeIssueOrderMoveHereEventArgs args){Pre(args);if(!args.SkipOriginalFunction){nativeCalls++;Post(args);}}
        public static void Run(){
            int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("anchor veto: "+why);}
            Check(RouteTileRules.TryRegister("tests.pathfinding","anchor-veto",out var rules,out _),"registration");
            GameTribe tribe=new(){r_AliveState=AliveState.IsAlive,r_PlayerIdOwner=1};
            GameTribeManagerAPI.Instance.Tribe=&tribe;
            try{
                rules.Set(21,20,RouteTileEffect.Forbidden,players:1U<<1);
                var f=new VetoFixture();
                f.Publish(new(){TileX=21});
                Check(f.nativeCalls==0&&f.sequence==0&&f.captures==0&&f.opens==0&&f.closes==0&&f.anchorFrames.Count==0,
                    "forbidden group event reaches both subscribers but changes no observed order or anchor scope");
                Check(f.moveEventObservers==null&&f.moveAdmissionParents==null,"forbidden event creates no command scope");
                var parent=new TribeIssueOrderMoveHereEventArgs();f.Pre(parent);
                Check(f.sequence==1&&f.captures==1&&f.anchorFrames.Count==1&&f.moveEventObservers.Count==1,"accepted parent opens both scopes");
                f.Publish(new(){TileX=21});
                Check(f.sequence==1&&f.captures==1&&f.opens==1&&f.closes==0&&f.anchorFrames.Count==1&&f.moveEventObservers.Count==1,
                    "rejected nested command preserves accepted parent's order and frame until its Post");
                f.Post(parent);
                Check(f.closes==1&&f.anchorFrames.Count==0&&f.moveEventObservers.Count==0&&f.moveAdmissionParents.Count==0,
                    "accepted parent Post still closes its own scopes after a rejected child");
                f.formationRuntime.Veto=true;int sequence=f.sequence,opens=f.opens;
                f.Publish(new());
                Check(f.sequence==sequence&&f.opens==opens&&f.anchorFrames.Count==0&&f.moveEventObservers.Count==0,
                    "formation veto before the later engine subscriber also receives no unmatched anchor frame");
                f.formationRuntime.Veto=false;f.Publish(new());
                Check(f.nativeCalls==1&&f.sequence==sequence+1&&f.opens==opens+1&&f.closes==2&&f.anchorFrames.Count==0,
                    "next accepted command retains its regular observation/capture/Pre-Post behavior");
                Check(f.runtime.Errors==0,"all subscriber paths complete without hidden errors");
                Console.WriteLine("PASS: "+checks+" integrated production command/anchor subscriber veto, prior-order and nested-frame checks.");
            }finally{rules.ClearAll();GameTribeManagerAPI.Instance.Tribe=null;}
        }
    }
}
""";
        fixture = fixture.Replace("__MEMBERS__", members);
        string api = Environment.GetEnvironmentVariable("MOAT_TEST_API_SHARED_DLL") ?? Path.Combine(root, "APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll");
        Assembly.LoadFrom(Path.GetFullPath(api));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).Append(MetadataReference.CreateFromFile(api));
        var compilation = CSharpCompilation.Create("IntegratedAnchorVetoFixture", new[] { CSharpSyntaxTree.ParseText(fixture) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var bytes = new MemoryStream(); var result = compilation.Emit(bytes);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(bytes.ToArray()).GetType("Pathfinding.UnitCommands.VetoFixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw(); }
    }
}
