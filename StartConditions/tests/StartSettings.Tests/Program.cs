using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Executes the production StartConditions lobby view model with boundary doubles (Issue 6).
// No Unity/Noesis or native simulation is started; gameplay acceptance is separate.
string root = args.Length >= 1 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
bool baseline = args.Contains("--baseline");
string Read(string path)
{
    if (!baseline || path != "StartConditions/src/StartConditionsLobbyViewModel.cs")
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
string enums = "using System; namespace SHCDESE.Interop {" + EnumSource("eGoods") + EnumSource("eChimps") + "}";
string vm = Read("StartConditions/src/StartConditionsLobbyViewModel.cs");
string keys = string.Join("\n", Parse(vm).DescendantNodes().OfType<MemberAccessExpressionSyntax>()
    .Where(m => m.Expression.ToString() == "SerpLocalization" && m.Name.ToString() != "Get")
    .Select(m => m.Name.ToString()).Distinct().Select(n => $"public const string {n}=\"{n}\";"));
string settingsDoubles = """
using System; using System.ComponentModel; using System.Runtime.CompilerServices;
namespace Noesis { public class ImageSource {} public static class GUI { public static System.Collections.Generic.Dictionary<string,object> GetApplicationResources()=>null; } }
namespace SHCDESE.NoesisUtil {}
namespace SHCDESE.ViewModels { public class RelayCommand { public RelayCommand(Action a) {} } }
namespace SHCDESE.API.Components.Network { public class SyncHostOnlyAttribute:Attribute {} }
namespace CrusaderDE { public class MainViewModel { public static bool viewModelLoaded=false; public static MainViewModel Instance=>null; public Noesis.ImageSource getSmallGoodsIcon(int id)=>null; } }
namespace APIShared.ModSettings {
 public class PresetLobbyModSettingsViewModel:INotifyPropertyChanged {
  public event PropertyChangedEventHandler PropertyChanged;
  public bool CanEditHostSettings=>true;
  protected virtual string ResolveSettingsUiText(string k,string f)=>f;
  protected bool CanMutateSetting(string p)=>true;
  protected bool CanMutateSettingWithDependents(string p, params string[] d)=>true;
  protected void OnPropertyChanged([CallerMemberName]string p=null)=>PropertyChanged?.Invoke(this,new(p));
 }
}
namespace StartConditions {
 public static class SerpLocalization { __KEYS__ public static string Get(string k,params object[] a)=>k; }
 public static class StartConditionsRuntime {
  public static bool IsConfigurableStoredGood(SHCDESE.Interop.eGoods g)=>g>SHCDESE.Interop.eGoods.STORED_NULL&&g<SHCDESE.Interop.eGoods.Count;
  public const int DelayedStartTroopCountMilliseconds=0, DelayedStartTroopCountSeconds=0;
  public static void TryGetLocalizedGoodName(SHCDESE.Interop.eGoods g,out string n,out string k,out bool f) { n=g.ToString();k=n;f=true; }
  public static string GetLocalizedUnitName(SHCDESE.Interop.eChimps u)=>u.ToString();
 }
 public static class SettingsCases {
  static void Check(bool b,string m) { if(!b)throw new Exception(m); }
  public static void Run() {
   foreach(bool ai in new[]{false,true}) foreach(bool troops in new[]{false,true}) {
    var model=new StartConditionsLobbyViewModel();
    var entries=troops?model.StartTroopEntries:model.StartGoodEntries;
    string first=troops?"CHIMP_TYPE_ARCHER":"STORED_RAW_WHEAT";
    string second=troops?"CHIMP_TYPE_SPEARMAN":"STORED_WOOD_PLANKS";
    string Value()=>troops?(ai?model.AddStartTroopsAI:model.AddStartTroopsHuman):(ai?model.StartGoodsAI:model.StartGoodsHuman);
    void Set(string s) { if(troops) { if(ai)model.AddStartTroopsAI=s;else model.AddStartTroopsHuman=s; }
     else { if(ai)model.StartGoodsAI=s;else model.StartGoodsHuman=s; } }
    var a=System.Linq.Enumerable.Single(entries,e=>e.Key==first);
    var b=System.Linq.Enumerable.Single(entries,e=>e.Key==second);
    int Amount()=>ai?a.AIAmount:a.HumanAmount;
    Set(first+"=25;"+second+"=10");Check(Amount()==25,"Complete settings not applied");
    Set(second+"=30");Check(Amount()==(troops?0:-1),"Sparse settings retained prior preset amount");
    if(ai)b.AIAmountText="31";else b.HumanAmountText="31";
    Check(Value().Contains(first+"="+(troops?0:-1)),"Editing another row resurrected the previous amount");
    Set("");Check(Amount()==(troops?0:-1),"Empty settings retained prior preset amount");
    Set(first+"=0");Check(Amount()==0,"Explicit zero was lost");
    if(!troops) { Set(first+"=-1");Check(Amount()==-1,"Unchanged sentinel was lost"); }
   }
  }
 }
}
""";
Run("Settings", new[] { vm, Read("StartConditions/src/IStartConditionsSettings.cs"), enums,
    settingsDoubles.Replace("__KEYS__", keys) }, "StartConditions.SettingsCases");

// Production readiness handle, broker, native-read decisions, clock math and event registry.
// Doubles replace engine pointers, roster/memory readiness and the upstream SE publisher only.
if (!baseline) Run("SimulationReadiness", new[] {
    Read("Shared/Adapters/APIShared/ActivePlayerKeepReadiness.cs"),
    Read("APIShared/src/Simulation/SimulationClock.cs"),
    Read("APIShared/src/Simulation/SimulationMutationScope.cs"),
    Read("APIShared/src/Simulation/SimulationTickRegistry.cs"),
    Read("APIShared/src/Core/Events/OwnedCallbacks.cs"),
    """
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
namespace SHCDESE.Interop { public enum eStructs { STRUCT_KEEP_ONE,STRUCT_KEEP_TWO,STRUCT_KEEP_THREE,STRUCT_KEEP_FOUR,STRUCT_KEEP_FIVE }
 public struct GameBuilding { public int r_PlayerIdOwner; public eStructs r_BuildingType; public Enums.AliveState r_AliveState; } }
namespace SHCDESE.Interop.Enums { public enum AliveState { NeedsInit,IsAlive } }
namespace SHCDESE.API {
 public class GameTimeManagerAPI { public static readonly GameTimeManagerAPI Instance=new(); public event Action<int> OnTick; }
 public class GamePlayerManagerAPI { public static readonly GamePlayerManagerAPI Instance=new(); public bool IsPlayerIdValid(int p)=>p==1; public int GetPlayerKeepId(int p)=>1; }
 public unsafe class GameBuildingManagerAPI { public static readonly GameBuildingManagerAPI Instance=new();
  public bool TryGetBuildingById(int id,out SHCDESE.Interop.GameBuilding* b) { b=(SHCDESE.Interop.GameBuilding*)Shared.World.Building; return Shared.World.KeepReady; } }
}
namespace APIShared { public sealed class MissionContext {} }
namespace APIShared.Simulation {
 internal static class SimulationTickSource { internal static bool TryRead(APIShared.MissionContext c,out int t) { t=Shared.World.Tick;return Shared.World.Available; } internal static void Report(string s,Exception e) {} }
 public static class SimulationTickEvents {
  internal static readonly SimulationTickRegistry Registry=new((out long session,out int tick)=>{session=Shared.World.Session;tick=Shared.World.Tick;return Shared.World.Available;}, cb=>Shared.World.Source=cb);
  public static bool TryRegister(string owner,string id,Action<int> cb,out string reason,int order=0)=>Registry.TryRegister(owner,id,cb,order,out reason);
 }
}
namespace Shared {
 internal static class MissionEvents { internal static bool TryGetCurrentMapTick(out int t) {t=World.Tick;return World.Available;} }
 public static class ActivePlayerHelper { public static int[] GetActivePlayerIds()=>new[]{1}; }
 public static class World {
  public static int Tick; public static long Session; public static bool Available=true,KeepReady; public static Action<int> Source;
  public static readonly IntPtr Building=Marshal.AllocHGlobal(Marshal.SizeOf<SHCDESE.Interop.GameBuilding>());
  public static void Emit(int native,int local){Tick=native;Source(local);}
 }
 public static class ReadinessCases {
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static ActivePlayerKeepWaitHandle Wait(int ticks,Action<ActivePlayerKeepSnapshot> cb=null)=>ActivePlayerKeepReadiness.WaitForSimulationTicks(cb??(_=>{}),ticks,null,"test",null);
  static void Begin(int tick){World.Session++;World.Tick=tick;World.KeepReady=false;World.Available=true;}
  public static unsafe void Run() {
   *(SHCDESE.Interop.GameBuilding*)World.Building=new(){r_PlayerIdOwner=1,r_BuildingType=SHCDESE.Interop.eStructs.STRUCT_KEEP_ONE,r_AliveState=SHCDESE.Interop.Enums.AliveState.IsAlive};
   ActivePlayerKeepReadiness.InitializeSimulationTicks("test");
   Begin(0);var timeout=Wait(1200);World.Emit(1199,7000);Check(!timeout.IsCompleted,"early timeout");
   World.Emit(1199,8000);Check(!timeout.IsCompleted,"pause consumed duration");World.Emit(1200,9000);Check(timeout.Result.Status==ActivePlayerKeepWaitStatus.TimedOut,"deadline boundary");
   Begin(800);var saved=Wait(1200);World.Emit(1999,4);Check(!saved.IsCompleted,"save-relative deadline");World.Emit(2000,5);Check(saved.Result.Status==ActivePlayerKeepWaitStatus.TimedOut,"save deadline");
   Begin(int.MaxValue-600);var wrapped=Wait(1200);World.Emit(unchecked(World.Tick+1199),1);Check(!wrapped.IsCompleted,"wrap early timeout");World.Emit(unchecked(int.MaxValue+600),2);Check(wrapped.Result.Status==ActivePlayerKeepWaitStatus.TimedOut,"wrap timeout");
   Begin(0);var calls=new List<string>();var atBoundary=Wait(1200,_=>calls.Add("start"));
   APIShared.Simulation.SimulationTickEvents.TryRegister("test","gold",_=>calls.Add("gold"),out _,700);
   World.KeepReady=true;World.Emit(1200,83);Check(atBoundary.Result.Succeeded,"readiness wins exact boundary");Check(string.Join(",",calls)=="start,gold","callback dependency order");
   Begin(10);var cancelled=Wait(1200);cancelled.Dispose();World.KeepReady=true;World.Emit(11,42);Check(cancelled.Result.Status==ActivePlayerKeepWaitStatus.Cancelled,"cancelled work replayed");
   Begin(10);var unavailable=Wait(1200);World.Available=false;World.KeepReady=true;World.Emit(11,500);Check(!unavailable.IsCompleted,"invented unavailable notification");World.Available=true;World.Emit(11,501);Check(unavailable.Result.Succeeded,"availability recovery");
   Begin(0);var failing=Wait(1200,_=>throw new Exception("callback"));var other=Wait(1200);World.KeepReady=true;World.Emit(1,1);Check(failing.Result.Status==ActivePlayerKeepWaitStatus.CallbackFailed&&other.Result.Succeeded,"wait callback isolation");
   string Peer(int factor){Begin(400);var payments=new List<int>();var wait=Wait(1200,_=>payments.Add(World.Tick));foreach(int t in new[]{401,799,800}){World.KeepReady=t>=800;World.Emit(t,t*factor+19);}Check(wait.Result.Succeeded,"peer readiness");return string.Join(",",payments);}
   Check(Peer(1)==Peer(3),"peer local counters changed gameplay");
   Marshal.FreeHGlobal(World.Building);
  }
 }
}
"""
}, "Shared.ReadinessCases");

void Run(string name, IEnumerable<string> sources, string typeName)
{
    var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
        .Select(p => MetadataReference.CreateFromFile(p));
    var compilation = CSharpCompilation.Create("StartSettings" + name,
        sources.Select(s => CSharpSyntaxTree.ParseText(s)), refs,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe:true));
    using var stream = new MemoryStream();var result=compilation.Emit(stream);
    if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
    try { Assembly.Load(stream.ToArray()).GetType(typeName).GetMethod("Run").Invoke(null,null); }
    catch(TargetInvocationException e){
        const string expected="Sparse settings retained prior preset amount";
        if(baseline && e.InnerException?.Message==expected) {Console.WriteLine("REPRODUCED release defect: "+expected);return;}
        throw e.InnerException??e;
    }
    if(baseline)throw new Exception("Release baseline unexpectedly passed: "+name);
    Console.WriteLine("PASS: "+name+" production regression cases");
}
