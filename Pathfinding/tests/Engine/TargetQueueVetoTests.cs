using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
internal static class TargetQueueVetoTests {
 internal static void Run(string root){
  var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"Pathfinding/src/CommandQueue/ExtendedShiftCommandQueueRuntime.cs"))).GetRoot();
  string[] methods={"OnTargetOrderCore","OnFinalAcceptedTarget","CancelQueuesForTribeUnits","RentWorkBuffers","MaintainCommandBoundary","MaintainMultiplayerQueue","ResetMapState"};
  string members=string.Join("\n",syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>methods.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
  members+=syntax.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="ObservedAttack").ToFullString();
  string fixture="""
using System;using System.Collections.Generic;using System.Runtime.CompilerServices;
namespace Pathfinding {
 static class TribeCommandDispatchBoundary {internal static bool IsAvailable=true;}
 static class SimulationAgreement {internal static int Faults;internal static void ReportFault(string reason)=>Faults++;}
 enum EventHookPhase {Pre,Post} enum TribeAICommand {AttackUnit=4,AttackBuilding=5}
 class TribeIssueOrderWithTargetEventArgs {public bool SkipOriginalFunction;public EventHookPhase Phase;public TribeAICommand AICommand=TribeAICommand.AttackUnit;public int TribeId=1,TargetValue1=30,TargetValue2=31,a6=32;public long ReturnValue=45;}
 struct GameTribe {public uint r_GlobalId;}
 static class QueueNativeContract {public static bool TryDecodeQueuedTargetCommand(int value,out int decoded){decoded=value&~128;return (value&128)!=0;}}
 public unsafe class Fixture {
 bool installed=true,internalDispatch,mp,ai,MultiplayerQueueAdmitted=true,IsMultiplayerReady=true,FeatureEnabled=true,selected=true,shift;
 static readonly object VetoedSynchronizedTarget=new();
 int log,currentTick,enqueued,nextCohortId;GameTribe* tribe;bool commandBoundaryFailed;
 List<int> expectedMoveChores=new(),expectedMoveEvents=new();Dictionary<int,int> pendingTribeSourcesByTargetId=new();
 class Markers {public int Resets;public void Reset(){Resets++;}}Markers largeMoveTargets=new();
 ConditionalWeakTable<TribeIssueOrderWithTargetEventArgs,object> previouslyVetoedTargets=new();
 Dictionary<int,ObservedAttack> observedAttacks=new();HashSet<int> loggedUnsupportedCommands=new();
 Dictionary<long,TribeQueueState> cohorts=new();Dictionary<QueueUnitIdentity,long> unitToCohort=new();
 HashSet<long> loggedPredecessorRedispatchFailures=new(),loggedIsolationFailures=new();Stack<QueueWorkBuffers> workBufferPool=new();
 class QueueWorkBuffers {public List<QueueUnitIdentity> Members=new();public void Clear()=>Members.Clear();}
 bool IsRealMultiplayer()=>mp;bool IsAiOwnedAliveTribe(int id)=>ai;bool IsShiftPressed()=>shift;
 bool IsLocalSelectedTribe(int id,out GameTribe* value){value=tribe;return selected;}
 bool TryGetAliveTribe(int id,out GameTribe* value){value=tribe;return value!=null;}
 void TryEnqueueSynchronizedCommand(int id,GameTribe* value,QueueCommand command){enqueued++;}
 bool TryApplyQueuedCommand(int id,GameTribe* value,QueueCommand command){enqueued++;return true;}
 void RecordQueueFull(int id,QueueCommand command){}
 void CaptureTribeMembers(int id,List<QueueUnitIdentity> output){output.Add(new QueueUnitIdentity(1,100));}
 __MEMBERS__
 void Seed(){var member=new QueueUnitIdentity(1,100);var state=new TribeQueueState(9,128,new[]{member},1,7,1);state.ExternalAttack=new QueueCommand(QueueCommandKind.AttackUnit,10,11,12);state.TryEnqueue(new QueueCommand(QueueCommandKind.AttackBuilding,20,21,22));cohorts.Add(7,state);unitToCohort.Add(member,7);observedAttacks.Add(1,new ObservedAttack(9,state.ExternalAttack));}
 public static void Run(){int checks=0;void Check(bool value,string why){checks++;if(!value)throw new Exception("target queue veto: "+why);}
  GameTribe live=new(){r_GlobalId=9};
  foreach(bool mp in new[]{false,true})foreach(bool marked in new[]{false,true}){
   var f=new Fixture{mp=mp,tribe=&live};f.Seed();var original=f.observedAttacks[1];var args=new TribeIssueOrderWithTargetEventArgs{SkipOriginalFunction=true,AICommand=(TribeAICommand)(4+(marked?128:0))};
   f.OnTargetOrderCore(args);
   Check(f.cohorts.Count==1&&f.cohorts[7].PendingCount==1&&f.unitToCohort.Count==1&&ReferenceEquals(f.observedAttacks[1],original)&&f.enqueued==0&&args.ReturnValue==45,"prior veto preserves real cohort A/B and native return");
   Check((int)args.AICommand==(mp?4:(marked?132:4)),"MP marker normalization independent of admission");
   if(!mp&&marked)continue;
   args.SkipOriginalFunction=false;f.OnFinalAcceptedTarget(args);
   Check(f.cohorts.Count==0&&f.unitToCohort.Count==0&&f.observedAttacks[1].Command.Argument1==30&&f.enqueued==0,"late native acceptance retires A/B and records C without enqueuing");
   f.OnFinalAcceptedTarget(args);Check(f.cohorts.Count==0,"reconciliation marker consumed exactly once");
  }
  foreach(int mode in new[]{0,1,2,3}){var f=new Fixture{tribe=&live,ai=mode==0,FeatureEnabled=mode!=1,selected=mode!=2,mp=mode==3,MultiplayerQueueAdmitted=mode!=3};f.Seed();var args=new TribeIssueOrderWithTargetEventArgs{SkipOriginalFunction=true};f.OnTargetOrderCore(args);args.SkipOriginalFunction=false;f.OnFinalAcceptedTarget(args);Check(f.cohorts.Count==1,"AI/disabled SP/unselected/unadmitted MP eligibility preserved");}
  var regular=new Fixture{tribe=&live};regular.Seed();regular.OnTargetOrderCore(new());Check(regular.cohorts.Count==0&&regular.observedAttacks[1].Command.Argument1==30,"ordinary accepted target retains immediate replacement");
  var queued=new Fixture{tribe=&live,shift=true};queued.Seed();var qargs=new TribeIssueOrderWithTargetEventArgs();queued.OnTargetOrderCore(qargs);Check(qargs.SkipOriginalFunction&&qargs.ReturnValue==1&&queued.enqueued==1&&queued.cohorts.Count==1,"own Shift veto remains queue insertion and preserves predecessor");
  var failed=new Fixture{tribe=&live};failed.Seed();var oldArgs=new TribeIssueOrderWithTargetEventArgs{SkipOriginalFunction=true};failed.OnTargetOrderCore(oldArgs);TribeCommandDispatchBoundary.IsAvailable=false;
  Check(!failed.MaintainCommandBoundary()&&failed.cohorts.Count==0&&failed.unitToCohort.Count==0&&failed.observedAttacks.Count==0,"boundary loss clears actual cohort state before dispatch on simulation thread");
  Check(!failed.previouslyVetoedTargets.TryGetValue(oldArgs,out _),"boundary loss discards weak event metadata");
  Check(!failed.MaintainCommandBoundary()&&failed.largeMoveTargets.Resets==1,"boundary failure cleanup occurs once");TribeCommandDispatchBoundary.IsAvailable=true;
  foreach(bool agreed in new[]{false,true})foreach(bool ready in new[]{false,true}){
   if(agreed&&!ready)continue;
   var peer=new Fixture{mp=true,tribe=&live,MultiplayerQueueAdmitted=agreed,IsMultiplayerReady=ready,FeatureEnabled=false};
   var command=new TribeIssueOrderWithTargetEventArgs{AICommand=(TribeAICommand)(agreed?132:4)};
   peer.OnTargetOrderCore(command);
   Check(command.SkipOriginalFunction==agreed&&peer.enqueued==(agreed?1:0)&&(int)command.AICommand==4,"Both peers follow the agreed target command path despite different local state");
  }
  var lateLoss=new Fixture{mp=true,tribe=&live,IsMultiplayerReady=false};
  var markedOrder=new TribeIssueOrderWithTargetEventArgs{AICommand=(TribeAICommand)132};
  lateLoss.OnTargetOrderCore(markedOrder);
  Check(markedOrder.SkipOriginalFunction&&(int)markedOrder.AICommand==4&&lateLoss.enqueued==0&&SimulationAgreement.Faults==1,"Late hook loss vetoes target execution and reports a session fault");
  var rejected=new Fixture{mp=true,tribe=&live,MultiplayerQueueAdmitted=false};
  var revertedVeto=new TribeIssueOrderWithTargetEventArgs{AICommand=(TribeAICommand)132,SkipOriginalFunction=true};
  rejected.OnTargetOrderCore(revertedVeto);revertedVeto.SkipOriginalFunction=false;rejected.OnFinalAcceptedTarget(revertedVeto);
  Check(revertedVeto.SkipOriginalFunction&&rejected.enqueued==0&&SimulationAgreement.Faults==2,"A later subscriber cannot restore immediate execution of a marked target in an unadmitted session");
  Console.WriteLine("PASS: "+checks+" actual target queue admission/reconciliation and real cohort state checks.");
 }
 }
}
namespace Shared {static class DebugLogHelper {public static void LogWarningLimited(object log,string key,string message){}}static class CrashBreadcrumbDiagnostics {public static void Record(string text,params int[] values){}}}
""";
  fixture=fixture.Replace("__MEMBERS__",members);
  var refs=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p=>MetadataReference.CreateFromFile(p));
  var trees=new[]{CSharpSyntaxTree.ParseText(fixture),CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"Pathfinding/src/CommandQueue/ExtendedShiftCommandQueueModel.cs")))};
  var compilation=CSharpCompilation.Create("ActualTargetQueueVeto",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));using var bytes=new MemoryStream();var result=compilation.Emit(bytes);if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
  try{Assembly.Load(bytes.ToArray()).GetType("Pathfinding.Fixture")!.GetMethod("Run")!.Invoke(null,null);}catch(TargetInvocationException error){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException??error).Throw();}
 }
}
