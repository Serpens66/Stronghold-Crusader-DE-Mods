using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
internal static class TargetAcceptedActionsTests {
 internal static void Run(string root){
  string Method(string path,string name)=>CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,path))).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text==name).ToFullString();
  string fixture="""
using System;using System.Collections.Generic;using System.Runtime.InteropServices;using Pathfinding.Engine;using SHCDESE.EventAPI;using SHCDESE.EventAPI.Tribes;using SHCDESE.Interop;using SHCDESE.Interop.Enums;
namespace R3 {}
namespace BepInEx.Logging {class ManualLogSource{}}
namespace SHCDESE.EventAPI {enum EventHookPhase {Pre,Post}}
namespace SHCDESE.Interop.Enums {enum TribeAICommand {UnitStop=3,AttackUnit=4}enum TribeMoveType {NoChange}enum eChimps {CHIMP_TYPE_ARAB_ASSASIN=73}}
namespace SHCDESE.EventAPI.Tribes {class TribeIssueOrderWithTargetEventArgs {public bool SkipOriginalFunction;public EventHookPhase Phase;public int TribeId=1;public TribeAICommand AICommand=TribeAICommand.UnitStop;}}
namespace SHCDESE.Interop {
 [StructLayout(LayoutKind.Explicit,Size=0x500)] struct GameUnit {[FieldOffset(0)]public ushort r_AIState;[FieldOffset(2)]public ushort r_MovementSubstep;[FieldOffset(4)]public ushort r_AnimationTimer;[FieldOffset(6)]public ushort r_CurrentSpriteAnimationFrame;[FieldOffset(8)]public ushort N00000061;[FieldOffset(12)]public eChimps r_UnitChimp;}
 [StructLayout(LayoutKind.Explicit,Size=0x688)]struct GameTribe {[FieldOffset(0)]public ushort r_UnitIdsInGroupBitfield;}
}
namespace SHCDESE.API {
 unsafe sealed class GameTribeManagerAPI {public static readonly GameTribeManagerAPI Instance=new();public GameTribe* Tribe;public bool IsValidId(int id)=>id==1;public bool TryGetTribeById(int id,out GameTribe* value){value=id==1?Tribe:null;return value!=null;}}
 unsafe sealed class GameUnitManagerAPI {public static readonly GameUnitManagerAPI Instance=new();public GameUnit* Unit;}
}
namespace APIShared {unsafe static class UnitAccess {public static bool TryGetById(SHCDESE.API.GameUnitManagerAPI api,int id,out GameUnit* value,out int index){index=0;value=id==1?api.Unit:null;return value!=null;}public static bool IsReallyAlive(GameUnit* unit)=>unit!=null;}}
namespace Shared {static class DebugLogHelper {public static void LogDebug(object log,string message){}public static void LogErrorLimited(object log,string key,string message){throw new Exception(message);}public static void LogWarningLimited(object log,string key,string message){throw new Exception(message);}}}
namespace Pathfinding {
 sealed class PathfindingViewModel {public bool EnableMod=true,EnableImprovedAssassinPathfinding=true;}
 static class TribeCommandDispatchBoundary {
  internal static bool IsAvailable=true;static Action<TribeIssueOrderWithTargetEventArgs> cancelClimb,cancelMovement,reconcileQueue;
  internal static void BindClimb(Action<TribeIssueOrderWithTargetEventArgs> callback)=>cancelClimb=callback;
  internal static void BindMovement(Action<TribeIssueOrderWithTargetEventArgs> callback)=>cancelMovement=callback;
  internal static void Fire(TribeIssueOrderWithTargetEventArgs args)=>AcceptedTarget(args);
  static void Report(string feature,Exception error)=>throw new Exception(feature,error);
  __ACCEPTED__
 }
 public unsafe class ActionFixture {
  bool anchorAvailable=true,fastSchedulerInitialized=true,fastDispatching;int anchorSimThread=Environment.CurrentManagedThreadId,fastPlayerActionDepth=1,sequence;
  FastCommandQueue fastCommands=new();Dictionary<FastUnitIdentity,object> fastUnitDistributions=new();
  static FastUnitIdentity Selected=>new(1,100);static FastUnitIdentity Other=>new(2,200);
  class Runtime {public void TryLogDiagnosticFailure(string name,Exception error)=>throw new Exception(name,error);}Runtime runtime=new();
  void ObserveTribeOrder(int id,bool move,int x,int y,short patrol,bool newOrder,TribeMoveType mode){sequence++;}
  IEnumerable<FastUnitIdentity> CaptureFastMembers(int tribe)=>new[]{Selected};
  __ENGINE__
  public static void Run(){int checks=0;void Check(bool value,string why){checks++;if(!value)throw new Exception("accepted actions: "+why);}
   var climb=new AssassinClimbCancellationRuntime(new BepInEx.Logging.ManualLogSource(),new PathfindingViewModel());climb.Initialize();climb.SetFixedUnitLayoutValidated(true);
   var engine=new ActionFixture();TribeCommandDispatchBoundary.BindMovement(engine.CancelFastPlayerTarget);
   GameTribe tribe=default;tribe.r_UnitIdsInGroupBitfield=2;GameUnit unit=default;SHCDESE.API.GameTribeManagerAPI.Instance.Tribe=&tribe;SHCDESE.API.GameUnitManagerAPI.Instance.Unit=&unit;
   foreach(ushort state in new ushort[]{126,127,128,129}){
    unit=default;unit.r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN;unit.r_AIState=state;unit.r_MovementSubstep=17;unit.r_AnimationTimer=18;unit.r_CurrentSpriteAnimationFrame=19;unit.N00000061=20;byte* raw=(byte*)&unit;raw[0x40f]=1;*(ushort*)(raw+0x414)=33;*(ushort*)(raw+0x416)=44;
    byte[] before=new byte[0x500];Marshal.Copy((IntPtr)raw,before,0,before.Length);
    engine.fastCommands.Enqueue(1,1,9,3,4,0,1,7,new[]{Selected,Other});engine.fastUnitDistributions[Selected]=new();engine.fastUnitDistributions[Other]=new();int sequence=engine.sequence;
    var args=new TribeIssueOrderWithTargetEventArgs{SkipOriginalFunction=true};TribeCommandDispatchBoundary.Fire(args);
    byte[] after=new byte[0x500];Marshal.Copy((IntPtr)raw,after,0,after.Length);Check(System.Linq.Enumerable.SequenceEqual(before,after),"final veto keeps every unit byte in climbing state "+state);
    Check(engine.sequence==sequence&&engine.fastCommands.HasPending(Selected)&&engine.fastUnitDistributions.Count==2,"final veto retains real Fast commands, distributions and order sequence");
    args.SkipOriginalFunction=false;TribeCommandDispatchBoundary.Fire(args);
    Check(unit.r_AIState==101&&unit.r_MovementSubstep==8&&unit.r_AnimationTimer==0&&unit.r_CurrentSpriteAnimationFrame==0&&unit.N00000061==0&&raw[0x40f]==0&&*(ushort*)(raw+0x414)==0&&*(ushort*)(raw+0x416)==0x20,"late unveto executes actual climb transition");
    Check(engine.sequence==sequence+1&&!engine.fastCommands.HasPending(Selected)&&engine.fastCommands.HasPending(Other)&&!engine.fastUnitDistributions.ContainsKey(Selected)&&engine.fastUnitDistributions.ContainsKey(Other),"accepted target supersedes only selected Fast members");
   }
   engine.fastPlayerActionDepth=0;int old=engine.sequence;TribeCommandDispatchBoundary.Fire(new(){AICommand=TribeAICommand.AttackUnit});Check(engine.sequence==old+1,"queued followup still updates order while outside player Chore");
   Console.WriteLine("PASS: "+checks+" actual final callback/Engine cancellation/Assassin unit-byte regression checks.");
  }
 }
}
""";
  fixture=fixture.Replace("__ACCEPTED__",Method("Pathfinding/src/Core/TribeCommandDispatchBoundary.cs","AcceptedTarget")).Replace("__ENGINE__",Method("Pathfinding/src/Engine/FastMovementScheduler.cs","CancelFastPlayerTarget"));
  string[] files={"Pathfinding/src/Assassins/AssassinClimbCancellationRuntime.cs","Pathfinding/src/Assassins/AssassinClimbCancellationPolicy.cs","Pathfinding/src/Engine/FastCommandQueue.cs"};
  var trees=files.Select(p=>CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,p)))).Prepend(CSharpSyntaxTree.ParseText(fixture));var refs=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p=>MetadataReference.CreateFromFile(p));
  var compilation=CSharpCompilation.Create("ActualTargetAcceptedActions",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));using var bytes=new MemoryStream();var result=compilation.Emit(bytes);if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
  try{Assembly.Load(bytes.ToArray()).GetType("Pathfinding.ActionFixture")!.GetMethod("Run")!.Invoke(null,null);}catch(TargetInvocationException error){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException??error).Throw();}
 }
}
