using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

string root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "RandomEvents/src/RandomEventsRuntime.cs")))
    root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
var runtime = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "RandomEvents/src/RandomEventsRuntime.cs"))).GetRoot();
var members = runtime.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "RandomEventsRuntime").Members
    .Where(m => m is MethodDeclarationSyntax method && method.Identifier.Text == "ActivatePendingBanditGroup" ||
        m is StructDeclarationSyntax type && new[] { "PendingBanditGroup", "BanditMoveTarget", "BanditUnitReference" }.Contains(type.Identifier.Text));
var gate = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Shared/Runtime/Diagnostics/DebugLogHelper.cs"))).GetRoot()
    .DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "LogRateGate");
// A real omitted-unit regression: execute the entire production activation method
// and limiter. Only game lookups/orders are simulated; counters expose log-only rereads.
string fixture = """
using System;using System.Linq;using System.Collections.Generic;using System.Diagnostics;using System.Runtime.InteropServices;
enum AliveState{IsAlive=2,Dead=0}enum eStructs{Ordinary,Excluded}enum TribeStance{Aggressive}enum TribeMoveType{DefaultInSync}
struct GamePlayerResources{public int r_LordUnitId;}
struct GameBuilding{public uint r_GlobalId;public AliveState r_AliveState;public int r_PlayerIdOwner;public eStructs r_BuildingType;}
struct GameUnit{public uint r_GlobalId;public int r_ControllableForPlayerId,r_TribeId,r_TribeLeaderUnitId;public bool Alive;}
struct GameTribe{public uint r_GlobalId;public int r_PlayerIdOwner;public TribeStance r_TribeStance;}
unsafe class GamePlayerManagerAPI{public static GamePlayerManagerAPI Instance=new();private GamePlayerResources* p=(GamePlayerResources*)Marshal.AllocHGlobal(sizeof(GamePlayerResources));public GamePlayerManagerAPI(){*p=default;}public bool TryGetPlayerResourcesById(int id,out GamePlayerResources* r){r=p;return true;}public int GetPlayerTeam(int id)=>9;public bool IsPlayerAlliedTo(int a,int b)=>false;}
unsafe class GameBuildingManagerAPI{public static GameBuildingManagerAPI Instance=new();public bool Found=true;public int Reads;public GameBuilding* P=(GameBuilding*)Marshal.AllocHGlobal(sizeof(GameBuilding));public bool TryGetBuildingById(int id,out GameBuilding* p){Reads++;p=P;return Found;}}
class GameUnitManagerAPI{public static GameUnitManagerAPI Instance=new();}
unsafe class GameTribeManagerAPI{public static GameTribeManagerAPI Instance=new();public int Creates,Moves,Assignments;public GameTribe* P=(GameTribe*)Marshal.AllocHGlobal(sizeof(GameTribe));public uint Create(int owner){Creates++;*P=new(){r_GlobalId=99,r_PlayerIdOwner=owner};return 7;}public bool TryGetTribeById(int id,out GameTribe* p){p=P;return true;}public bool AssignUnit(int tribe,int unit){Assignments++;APIShared.UnitAccess.Units[unit].r_TribeId=tribe;return true;}public bool SetStance(int id,TribeStance s){P->r_TribeStance=s;return true;}public bool IssueMoveHereCommand(int id,int x,int y,bool isPatrolPath,int bIsNewOrder,TribeMoveType tribeMoveType){Moves++;return true;}}
namespace APIShared{enum UnitLookupFailure{None,Unavailable}unsafe static class UnitAccess{public static GameUnit* Units=(GameUnit*)Marshal.AllocHGlobal(sizeof(GameUnit)*32);public static int Reads,LifeReads;public static bool TryGetById(GameUnitManagerAPI manager,int id,out GameUnit* p,out UnitLookupFailure failure){Reads++;p=id==4?null:Units+id;failure=p==null?UnitLookupFailure.Unavailable:UnitLookupFailure.None;return p!=null;}public static bool IsReallyAlive(GameUnit* p){LifeReads++;return p->Alive;}}}
static class BanditTargetEligibility{public static bool IsEligibleStructureType(eStructs type)=>type!=eStructs.Excluded;}
namespace Shared{static class DebugLogHelper{public static bool DiagnosticsEnabled=>false;}__GATE__}
public unsafe class Fixture{
 private long currentSessionId=17;private long now;
 private Shared.LogRateGate banditActivationLogGate;
 private List<string> lines=new();private List<bool> warnings=new();
 public Fixture(){banditActivationLogGate=new Shared.LogRateGate(()=>now,1,16);}
 private void LogWarning(string m){lines.Add(m);warnings.Add(true);}private void LogSupportInfo(string m){lines.Add(m);warnings.Add(false);}private void LogDebug(string m){}
 __MEMBERS__
 private static void Check(bool b,string m){if(!b)throw new Exception(m);}
 private static void ResetUnits(){for(int id=1;id<=31;id++)APIShared.UnitAccess.Units[id]=new(){r_GlobalId=(uint)id,r_ControllableForPlayerId=8,Alive=true};APIShared.UnitAccess.Units[2].Alive=false;APIShared.UnitAccess.Units[3].r_GlobalId=30;}
 public static void Run(){
  var f=new Fixture();*GameBuildingManagerAPI.Instance.P=new(){r_GlobalId=50,r_AliveState=AliveState.IsAlive,r_PlayerIdOwner=1};
  ResetUnits();var pending=new PendingBanditGroup(8,9,new[]{1},1,new[]{new BanditUnitReference(1,1),new BanditUnitReference(2,2),new BanditUnitReference(3,3),new BanditUnitReference(4,4)},new BanditMoveTarget(5,50,10,20),100);
  f.ActivatePendingBanditGroup(pending);
  Check(f.lines.Count==1&&f.warnings[0]&&f.lines[0].Contains("session=17")&&f.lines[0].Contains("scheduledTick=100")&&f.lines[0].Contains("living=1, omitted=3, lookupFailures=1"),"Group support log lost decision/session/counts");
  Check(f.lines[0].Contains("unit=3, expectedGlobal=3, actualGlobal=30")&&f.lines[0].Contains("lookup=Unavailable")&&f.lines[0].Contains("liveness=not-living"),"Lookup, replaced identity and dead unit are not distinguishable");
  Check(APIShared.UnitAccess.Reads==5&&APIShared.UnitAccess.LifeReads==2&&GameBuildingManagerAPI.Instance.Reads==1,"Logging added world/liveness lookups");
  Check(GameTribeManagerAPI.Instance.Moves==1&&GameTribeManagerAPI.Instance.Assignments==1,"Logging changed surviving group orders");
  for(int id=5;id<=25;id++){ResetUnits();var group=new PendingBanditGroup(8,9,new[]{1},1,new[]{new BanditUnitReference(id,999)},pending.Target,100);f.ActivatePendingBanditGroup(group);}
  Check(f.lines.Count==1,"Changing unit IDs bypassed suppression");
  f.now=31;ResetUnits();f.ActivatePendingBanditGroup(pending);
  Check(f.lines.Count==2&&f.lines[1].Contains("suppressedGroups=21"),"Suppression count lost");
  f.now=62;ResetUnits();var dead=new PendingBanditGroup(8,9,new[]{1},1,new[]{new BanditUnitReference(2,2)},pending.Target,100);f.ActivatePendingBanditGroup(dead);
  Check(f.lines.Count==3&&!f.warnings[2]&&f.lines[2].Contains("living=0"),"Expected deaths should be bounded info, not technical warnings");
  int reads=APIShared.UnitAccess.Reads;GameBuildingManagerAPI.Instance.P->r_GlobalId=51;f.ActivatePendingBanditGroup(pending);
  Check(f.lines.Count==4&&f.lines[3].Contains("reason=target-identity-changed")&&f.lines[3].Contains("expectedGlobal=50, actualGlobal=51")&&APIShared.UnitAccess.Reads==reads,"Target rejection lost observed identity or queried unrelated units");
 }
}
""";
fixture = fixture.Replace("__MEMBERS__", string.Join("\n", members.Select(m => m.ToFullString()))).Replace("__GATE__", gate.ToFullString());
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("BanditFixture", new[] { CSharpSyntaxTree.ParseText(fixture) }, references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
using var output = new MemoryStream();
var result = compilation.Emit(output);
if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
try { Assembly.Load(output.ToArray()).GetType("Fixture")!.GetMethod("Run")!.Invoke(null, null); }
catch (TargetInvocationException e) { throw e.InnerException ?? e; }
Console.WriteLine("PASS: production bandit activation distinguishes lookup, identity and life; bounded groups and suppression counts, unchanged orders and world-query counts.");
