from pathlib import Path
root=Path.cwd(); folder=root/'_inspect/MacemenDiagnosticTests';folder.mkdir(exist_ok=True)
def write(p,s):
 p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode())
refs={'System':None,'System.Core':None,'BepInEx':r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\core\BepInEx.dll',
'SHCDESE':r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll',
'APIShared':str(root/'APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll'),
'System.Memory':r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\System.Memory.dll'}
files=['src/MacemenCommandDiagnostics.cs','src/MacemenDiagnosticAggregate.cs','src/EnemyGatePathfindingPolicy.cs','src/RouteTilePolicySnapshot.cs','src/GateEdgeOwnership.cs']
s='''<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><PropertyGroup><OutputType>Exe</OutputType><TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion><LangVersion>latest</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks><OutputPath>bin\\</OutputPath><PlatformTarget>x64</PlatformTarget><AssemblyName>MacemenDiagnosticTests</AssemblyName></PropertyGroup><ItemGroup>'''
for name,path in refs.items():s+='<Reference Include="'+name+'">'+('' if path is None else '<HintPath>'+path+'</HintPath><Private>true</Private>')+'</Reference>'
s+='</ItemGroup><ItemGroup><Compile Include="Program.cs"/><Compile Include="LogDouble.cs"/>'
for file in files:s+='<Compile Include="..\\..\\Testmods\\EnemyGatePathfindingTest\\'+file.replace('/','\\')+'"/>'
s+='</ItemGroup><Import Project="$(MSBuildToolsPath)\\Microsoft.CSharp.targets"/></Project>'
write(folder/'MacemenDiagnosticTests.csproj',s)
write(folder/'LogDouble.cs','''using BepInEx.Logging;
namespace Shared { internal static class DebugLogHelper { internal static void LogInfo(ManualLogSource log,string text) { } } }
''')
write(folder/'Program.cs','''using System;
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
  d.End();return 0;
 }
}
''')
print('Prepared actual Macemen observer fixture with SDK boundary doubles.')