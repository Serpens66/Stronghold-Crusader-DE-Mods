using System;
using System.Diagnostics;
using System.Reflection;
using EnemyGatePathfindingTest;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop.Enums;
class Program
{
 static int checks;
 static void Check(bool ok,string cause) { checks++;if(!ok)throw new Exception(cause); }
 static MacemenUnit Unit(int id,uint global=100,int command=3,int state=1,bool maceman=true) =>
    new MacemenUnit(id,global,2,5,500,command,state,20,20,2,0,maceman);
 static MacemenDiagnosticAggregate Rows(MacemenCommandDiagnostics d) =>
    (MacemenDiagnosticAggregate)typeof(MacemenCommandDiagnostics).GetField("aggregate",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d);
 static int Main()
 {
  MacemenUnit live=Unit(10), leader=live;
  RouteTilePolicySnapshot snapshot=RouteTilePolicySnapshot.Empty;
  var d=new MacemenCommandDiagnostics(null,()=>snapshot,()=>NativeGateAccessSnapshot.Empty,
     id=>id==10?live:default,id=>id==5?leader:default);
  d.Begin(1);
  var order=new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Pre,5,TribeAICommand.AttackBuilding,134,6658,0);
  d.TargetOrder(order);
  var unit=new UnitMoveHereEventArgs(EventHookPhase.Pre,10,50,60,0);
  d.UnitMove(unit);d.BeginSearch(2,0);d.EndSearch(true,true,2);
  live=Unit(10,100,9,3);
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,50,60,0){ReturnValue=1});
  leader=live;
  d.TargetOrder(new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Post,5,TribeAICommand.AttackBuilding,134,6658,0){ReturnValue=1});
  Check(d.Observations==3,"order+unit+search");
  int rows=0;Rows(d).Drain((key,bucket)=>{rows++;Check(key.Command==9,"incoming attack retained");
     if(key.Stage==MacemenStage.UnitMove) {Check(key.BeforeCommand==3&&key.AfterCommand==9,"stored vs incoming command");Check(bucket.Unstable==0,"stable identity");}});
  Check(rows==3,"stages distinct");
  // A skipped original has no Post. Its mutable event reference cannot pollute the next native call.
  var skipped=new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Pre,5,TribeAICommand.AttackBuilding,134,6658,0);
  d.TargetOrder(skipped);skipped.SkipOriginalFunction=true;
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,10,11,0));
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,10,11,0));
  Check(d.Skipped==1,"skipped original pruned");
  // Same numeric Unit-ID with different Global-ID is not a proved outcome.
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,50,60,0));live=Unit(10,101);
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,50,60,0));
  bool reused=false;Rows(d).Drain((key,row)=>{if(row.Unstable>0)reused=true;});Check(reused,"reused ID remains unverified");
  // A new snapshot during the same search invalidates its diagnostic proof.
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,50,60,0));
  d.BeginSearch(2,0);snapshot=new RouteTilePolicySnapshot(new byte[9][],1);d.EndSearch(true,true,0);
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,50,60,0));
  bool changed=false;Rows(d).Drain((key,row)=>{if(key.Stage==MacemenStage.Search&&row.Unstable>0)changed=true;});Check(changed,"snapshot switch");
  // A mixed-group leader is only a representative; actual Macemen children qualify the group.
  leader=Unit(20,200,3,1,false);live=Unit(10);
  d.GroupMove(new TribeIssueOrderMoveHereEventArgs(EventHookPhase.Pre,5,100,101,0,true,(TribeMoveType)0));
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,100,101,0));
  d.BeginSearch(2,0);d.BeginSearch(2,1);d.EndSearch(true,false,1);d.EndSearch(true,true,2);
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,100,101,0));
  d.GroupMove(new TribeIssueOrderMoveHereEventArgs(EventHookPhase.Post,5,100,101,0,true,(TribeMoveType)0));
  Check(d.Errors==0&&d.UnmatchedPost==0&&d.MissingPost==0,"nested/mixed pairs");
  // A non-Maceman callback must not inherit the group's unit classification.
  long before=d.Observations;
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,11,1,2,0));d.BeginSearch(2,0);d.EndSearch(true,true,0);
  d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,11,1,2,0));Check(d.Observations==before,"other type excluded");
  d.End();Check(d.ActiveCombinations==0,"end drains all rows");
  before=d.Observations;d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,1,2,0));Check(d.Observations==before,"inactive is passive");
  d.Begin(2);Check(d.Observations==0,"map epoch reset");
  long start=Stopwatch.GetTimestamp();
  for(int i=0;i<100000;i++) { d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,i%100,(i+1)%100,0));
     d.BeginSearch(2,0);d.EndSearch(true,true,0);d.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,i%100,(i+1)%100,0)); }
  Check(d.Observations==200000,"long run counts");Check(d.ActiveCombinations==2,"coordinates do not grow history");
  Console.WriteLine("PASS: actual Macemen observer "+checks+" checks;100000 command+search pairs; "+
     ((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency).ToString("F2")+"ms with SDK boundary doubles.");
  d.End();
  bool throwUnit=false,throwPolicy=false;
  var failing=new MacemenCommandDiagnostics(null,()=>throwPolicy?throw new InvalidOperationException():snapshot,
     ()=>NativeGateAccessSnapshot.Empty,id=>throwUnit?throw new InvalidOperationException():live,id=>leader);
  failing.Begin(3);leader=live;
  failing.TargetOrder(new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Pre,5,TribeAICommand.AttackBuilding,134,6658,0));
  throwUnit=true;failing.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,50,60,0));throwUnit=false;
  failing.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,50,60,0));
  failing.TargetOrder(new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Post,5,TribeAICommand.AttackBuilding,134,6658,0));
  Check(failing.Errors==1&&failing.UnmatchedPost==0,"failed child capture preserves parent pairing");
  failing.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,50,60,0));failing.BeginSearch(2,0);
  throwPolicy=true;failing.BeginSearch(2,1);throwPolicy=false;
  failing.EndSearch(true,true,0);failing.EndSearch(true,true,0);
  failing.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,50,60,0));
  Check(failing.Errors==2&&failing.UnmatchedPost==0,"failed nested search preserves parent search");
  failing.End();Console.WriteLine("PASS: fallible child reads preserve synchronous stacks; total checks="+checks);
  foreach(int command in new[]{3,9,36,38}) {
   var ids=new System.Collections.Generic.Dictionary<int,RouteTilePolicySnapshot.GateIdentity> {
     {134,new RouteTilePolicySnapshot.GateIdentity(6658,1,0)} };
   RouteTilePolicySnapshot published=command==3?RouteTilePolicySnapshot.Empty:
     new RouteTilePolicySnapshot(new byte[9][],42,gateIdentities:ids);
   var nested=new MacemenCommandDiagnostics(null,()=>published,()=>NativeGateAccessSnapshot.Empty,
      id=>live,id=>leader,(id,g,s)=>s.GateIdentities.TryGetValue(id,out var gi)&&gi.Global==g?
        new MacemenGate(id,g,gi.Owner,gi.Capturer,1,0):default);
   nested.Begin(4);live=Unit(10);leader=live;
   nested.TargetOrder(new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Pre,5,(TribeAICommand)command,134,6658,0));
   nested.GroupMove(new TribeIssueOrderMoveHereEventArgs(EventHookPhase.Pre,5,50,60,0,true,(TribeMoveType)1));
   nested.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Pre,10,51,61,0));
   nested.BeginSearch(2,0);nested.GateComparison(2,999,123,false,true,1,1,0);
   Check(nested.UnattributedGateComparisons==1&&nested.Errors==0,"unattributed gate is coverage");
   nested.EndSearch(true,true,0);
   nested.UnitMove(new UnitMoveHereEventArgs(EventHookPhase.Post,10,51,61,0){ReturnValue=1});
   nested.GroupMove(new TribeIssueOrderMoveHereEventArgs(EventHookPhase.Post,5,50,60,0,true,(TribeMoveType)1){ReturnValue=1});
   nested.TargetOrder(new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Post,5,(TribeAICommand)command,134,6658,0){ReturnValue=1});
   int seen=0;Rows(nested).Drain((key,row)=> {
    seen++;Check(key.Command==command,"incoming order survives group and unit wrappers");
    Check(row.First.OrderTarget1==134&&row.First.OrderTarget2==6658,"order target separate from movement coordinates");
    Check(row.Unstable==0,"nested identity stable");
    if(key.Stage!=MacemenStage.TargetOrder)Check(key.MoveType==1,"group move type preserved separately");
    if(command==38)Check(key.Target==MacemenTarget.GateApproach&&key.Gate.Id==134,"38 is exact gate approach, not attack");
   });Check(seen==4,"target/group/unit/search all counted");
   if(command==38) {
    published=new RouteTilePolicySnapshot(new byte[9][],43,gateIdentities:
      new System.Collections.Generic.Dictionary<int,RouteTilePolicySnapshot.GateIdentity> {
       {134,new RouteTilePolicySnapshot.GateIdentity(6658,1,2)} });
    nested.TargetOrder(new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Pre,5,(TribeAICommand)38,134,6658,0));
    nested.TargetOrder(new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Post,5,(TribeAICommand)38,134,6658,0));
    Rows(nested).Drain((key,row)=>Check(key.Gate.Capturer==2,"functional capture publication observed"));
   }
   nested.End();nested.Begin(5);Check(nested.Observations==0&&nested.UnattributedGateComparisons==0,"epoch clears diagnosis");nested.End();
  }
  Console.WriteLine("PASS: nested orders 3/9/36/38, exact gate identity/capture, coverage and epoch; total checks="+checks);
  return 0;
 }
}
