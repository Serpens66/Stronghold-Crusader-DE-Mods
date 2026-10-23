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
