using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Executes production queue entrypoints with boundary doubles (Issue 6 infinite patrol).
// No Unity/Noesis or native simulation is started; gameplay acceptance is separate.
string root = args.Length >= 1 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
bool baseline = args.Contains("--baseline");
string Read(string path)
{
    if (!baseline || path != "BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs")
        return File.ReadAllText(Path.Combine(root, path));
    var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory=root, RedirectStandardOutput=true, UseShellExecute=false };
    start.ArgumentList.Add("show"); start.ArgumentList.Add("SerpsMods/v1.0.28:"+path);
    using var process=System.Diagnostics.Process.Start(start);
    string source=process.StandardOutput.ReadToEnd();process.WaitForExit();
    if(process.ExitCode!=0)throw new Exception("Cannot load release baseline");return source;
}
CompilationUnitSyntax Parse(string text) => CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
string extenderDirectory = Environment.GetEnvironmentVariable("SHCDESE_EXTENDER_DIR") ??
    @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
var extender = Assembly.LoadFrom(Path.Combine(extenderDirectory, "SHCDESE.dll"));
string EnumSource(string name)
{
    var type = extender.GetType("SHCDESE.Interop." + name) ?? extender.GetType("SHCDESE.Interop.Enums." + name, true);
    string fields = string.Join(",", type.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => f.Name + "=" + Convert.ToInt64(f.GetRawConstantValue())));
    return "public enum " + name + " : " + Enum.GetUnderlyingType(type).FullName + " {" + fields + "}";
}
string enums = "using System; namespace SHCDESE.Interop {" + EnumSource("eGoods") + EnumSource("eChimps") + "}"
    + "namespace SHCDESE.Interop.Enums {" + EnumSource("TribePatrolMode") + EnumSource("TribeMoveType") + "}";
var queueTree = Parse(Read("BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs"));
string[] names = { "HandleMoveChore", "ShouldMarkOutgoingMultiplayerOrder", "OnMoveOrderCore",
    "AppendMovementWaypointCore", "TryEnqueueSynchronizedCommand", "CancelQueueForPatrol" };
string members = string.Join("\n", queueTree.DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => names.Contains(m.Identifier.Text)).Select(m => m.ToFullString()));
string queueDoubles = """
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
using SHCDESE.Interop.Enums;
namespace SHCDESE.EventAPI { public enum EventHookPhase { Pre,Post } }
namespace SHCDESE.Interop { public struct GameTribe {} }
namespace SHCDESE.EventAPI.Tribes {
 public class TribeIssueOrderMoveHereEventArgs {
  public SHCDESE.EventAPI.EventHookPhase Phase=SHCDESE.EventAPI.EventHookPhase.Pre;
  public int TribeId=7,TileX=10,TileY=20;public short IsPatrolPath;public bool IsNewOrder=true,SkipOriginalFunction;
  public TribeMoveType MoveType=TribeMoveType.DefaultInSync;public long ReturnValue;
 }
}
namespace Shared { public static class CrashBreadcrumbDiagnostics { public static void Record(string m,int outcome=0){} }
 public static class DebugLogHelper { public static bool DiagnosticsEnabled => true;  public static void LogErrorLimited(object l,string p,string m){} } }
namespace BugfixesAndQoL.UnitCommands {
 static class UnitCommandPathAPI { public static bool FormationDispatchActive; }
 enum QueueCommandKind { Move }
 class QueueCommand { public QueueCommand(QueueCommandKind k,int x,int y,int mode){} }
 class RuntimeExpectedMove { public int TribeId;public RuntimeExpectedMove(int t,QueueCommand c,int expires){TribeId=t;} }
 unsafe class QueueFixture {
  __MEMBERS__
  bool installed=true,internalDispatch=false,multiplayerSynchronizationReady=true,multiplayerMarkerFailureLogged;
  bool FeatureEnabled=true,Multiplayer,Shift=true,Ai,Selected=true;int currentTick,ExpectedMoveChoreLifetimeTicks=2;
  object log;IntPtr choreModePointer,choreTribeIdPointer,choreCommandOrTileXPointer,choreMoveTypePointer,chorePatrolModePointer;
  List<RuntimeExpectedMove> expectedMoveChores=new(),expectedMoveEvents=new();HashSet<int> observedAttacks=new();
  Dictionary<int,int> queued=new(); int OriginalCalls,LastMarker,AppendCalls; (int,int,ushort,ushort,int,short) Appended;
  Action<IntPtr,int,ushort,ushort,int,short> originalSharedAppend;
  bool IsRealMultiplayer()=>Multiplayer;bool IsShiftPressed()=>Shift;bool IsAiOwnedAliveTribe(int id)=>Ai;
  bool IsLocalSelectedTribe(int id,out SHCDESE.Interop.GameTribe* tribe){tribe=null;return Selected;}
  bool TryGetAliveTribe(int id,out SHCDESE.Interop.GameTribe* tribe){tribe=null;return true;}
  bool TryApplyQueuedCommand(int id,SHCDESE.Interop.GameTribe* t,QueueCommand c){queued[id]=queued.GetValueOrDefault(id)+1;return true;}
  void CancelQueuesForTribeUnits(int id)=>queued.Remove(id);
  bool HasQueuedUnitInTribe(int id)=>queued.ContainsKey(id);
  bool TryConsumeExpectedMoveSignal(List<RuntimeExpectedMove> s,int id,QueueCommand c)=>false;
  void RecordQueueFull(int id,QueueCommand c)=>throw new Exception("Unexpected queue full");
  bool ShouldLogUnexpectedOnce(string s)=>true;
  void LogMultiplayerMarkerFailure(string s,Exception e)=>throw new Exception(s,e);
  void InvokeOriginalMoveChore(int mode,ref bool entered){entered=true;OriginalCalls++;LastMarker=Marshal.ReadInt32(choreMoveTypePointer);}
  static void Check(bool b,string m){if(!b)throw new Exception(m);}
  public static void Run(){
   foreach(bool mp in new[]{false,true}) {
    var f=new QueueFixture{Multiplayer=mp}; f.queued[7]=3;f.queued[8]=2;
    f.expectedMoveChores.Add(new(7,null,0));f.expectedMoveEvents.Add(new(7,null,0));
    var a=new SHCDESE.EventAPI.Tribes.TribeIssueOrderMoveHereEventArgs{IsPatrolPath=(short)TribePatrolMode.PatrolInfinite,
      MoveType=(TribeMoveType)(mp?0x41:1)};
    f.OnMoveOrderCore(a);
    Check(!a.SkipOriginalFunction && (int)a.MoveType==1,"Patrol first order was consumed or marked");
    Check(!f.queued.ContainsKey(7)&&f.queued[8]==2&&f.expectedMoveChores.Count==0&&f.expectedMoveEvents.Count==0,"Stale queue/dedup state survived or another tribe was cleared");
    f.originalSharedAppend=(m,t,x,y,i,mode)=>{f.AppendCalls++;f.Appended=((int)m,t,x,y,i,mode);};
    for(int i=1;i<=3;i++)f.AppendMovementWaypointCore((IntPtr)123,7,(ushort)(10+i),(ushort)(20+i),i,(short)TribePatrolMode.PatrolInfinite);
    Check(f.AppendCalls==3&&f.Appended==(123,7,13,23,3,(short)TribePatrolMode.PatrolInfinite)&&!f.queued.ContainsKey(7),"Patrol points did not reach Vanilla unchanged");
    f.AppendMovementWaypointCore((IntPtr)123,7,14,24,4,(short)TribePatrolMode.PatrolOnce);
    Check(f.AppendCalls==3&&f.queued[7]==1,"Ordinary Shift waypoint stopped using the queue");
    var normal=new SHCDESE.EventAPI.Tribes.TribeIssueOrderMoveHereEventArgs{MoveType=(TribeMoveType)(mp?0x41:1)};
    f.OnMoveOrderCore(normal);Check(normal.SkipOriginalFunction&&f.queued[7]==2,"Ordinary queued move changed");
   }
   var marker=new QueueFixture{Multiplayer=true};IntPtr mem=Marshal.AllocHGlobal(24);
   try {
    marker.choreModePointer=mem;marker.choreTribeIdPointer=mem+4;marker.choreCommandOrTileXPointer=mem+8;
    marker.choreMoveTypePointer=mem+12;marker.chorePatrolModePointer=mem+16;
    Marshal.WriteInt32(mem,QueueNativeContract.ChorePackMode);Marshal.WriteInt32(mem+4,7);
    foreach(int patrol in new[]{(int)TribePatrolMode.PatrolInfinite,(int)TribePatrolMode.PatrolOnce,(int)TribePatrolMode.None}) {
     Marshal.WriteInt32(mem+12,1);Marshal.WriteInt32(mem+16,patrol);marker.HandleMoveChore();
     Check(marker.LastMarker==(patrol==(int)TribePatrolMode.PatrolInfinite?1:0x41),"Outgoing patrol/queue classification failed");
     Check(Marshal.ReadInt32(mem+12)==1,"Outgoing temporary marker not restored");
    }
   }finally{Marshal.FreeHGlobal(mem);}
  }
 }
}
""";
string queueUsings = "using SHCDESE.Interop;using SHCDESE.EventAPI;using SHCDESE.EventAPI.Tribes;";
Run("Patrol", new[] { enums, Read("BugfixesAndQoL/src/UnitCommands/Movement/QueueNativeContract.cs"),
    queueUsings + queueDoubles.Replace("__MEMBERS__", members) }, "BugfixesAndQoL.UnitCommands.QueueFixture");


void Run(string name, IEnumerable<string> sources, string typeName)
{
    var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
        .Select(p => MetadataReference.CreateFromFile(p));
    var compilation = CSharpCompilation.Create("Issue6" + name,
        sources.Select(s => CSharpSyntaxTree.ParseText(s)), refs,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe:true));
    using var stream = new MemoryStream();var result=compilation.Emit(stream);
    if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
    try { Assembly.Load(stream.ToArray()).GetType(typeName).GetMethod("Run").Invoke(null,null); }
    catch(TargetInvocationException e){
        const string expected="Patrol first order was consumed or marked";
        if(baseline && e.InnerException?.Message==expected) {Console.WriteLine("REPRODUCED release defect: "+expected);return;}
        throw e.InnerException??e;
    }
    if(baseline)throw new Exception("Release baseline unexpectedly passed: "+name);
    Console.WriteLine("PASS: "+name+" production regression cases");
}
