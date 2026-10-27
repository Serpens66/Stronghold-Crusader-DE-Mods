using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class PersonalResetRuntimeTests
{
    internal static void Run()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ExtendedData/src/TrailMissionSettingsCoordinator.cs")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
        string Methods(string path, params string[] names) => string.Join("\n",
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path))).GetRoot()
                .DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => names.Contains(m.Identifier.Text)).Select(m => m.ToFullString()));
        // Execute the actual provider, individual selector and global confirmation paths.
        // Doubles replace endpoint/backend and UI boundaries; actual persistence, scope
        // filtering and restart application are separately covered by APIShared/consumer tests.
        string fixture = """
using System; using System.Collections.Generic; using System.Collections.ObjectModel; using System.IO; using System.Linq;
using APIShared.ModSettings;
namespace Shared { static class DebugLogHelper { public static void LogErrorLimited(object log,string key,string text){} } }
namespace APIShared.Localization {static class UiText {public static string English(string key)=>key;}}
namespace APIShared.ModSettings {
 public interface IModSettingsPresetEndpoint {}
 public interface IModSettingsWorkingCopyEndpoint:IModSettingsPresetEndpoint {
  bool IsMissionPresetActive{get;} Dictionary<string,byte[]> System_CreatePlayerMissionPresetSnapshot();
  Dictionary<string,byte[]> System_CreateCurrentMissionPresetSnapshot(); void System_ApplyMissionPresetSnapshot(Dictionary<string,byte[]> s,string label);
  Dictionary<string,byte[]> System_CreateCurrentWorkingSnapshot(); void System_LoadModDefaults(); void System_ApplyWorkingSnapshot(Dictionary<string,byte[]> s);
 }
 public class PresetLobbyModSettingsViewModel:IModSettingsWorkingCopyEndpoint {
  public bool IsMissionPresetActive{get;set;}=true; public bool MissionPresetEditable{get;set;}=true;
  public int Own=23,Value=91,Active=81,Commits; public int FailRead,FailApply,FailCommit; public bool NullRead,NullBuffer; public string Label="Map";
  public byte[] PersonalBuffer; public Action BeforeApply;
  public Dictionary<string,byte[]> System_CreatePlayerMissionPresetSnapshot(){if(FailRead-->0)throw new IOException("read");if(NullRead)return null;PersonalBuffer=BitConverter.GetBytes(Own);return new(){{"value",NullBuffer?null:PersonalBuffer}};}
  public Dictionary<string,byte[]> System_CreateCurrentMissionPresetSnapshot()=>new(){{"value",BitConverter.GetBytes(Value)}};
  public void System_ApplyMissionPresetSnapshot(Dictionary<string,byte[]> s,string label){BeforeApply?.Invoke();if(FailApply-->0)throw new IOException("apply");Value=BitConverter.ToInt32(s["value"]);Label=label;}
  public void System_CommitConfiguration(){Commits++;if(FailCommit-->0)throw new IOException("commit");Active=Value;}
  public Dictionary<string,byte[]> System_CreateCurrentWorkingSnapshot()=>System_CreateCurrentMissionPresetSnapshot();
  public void System_LoadModDefaults(){Value=5;}public void System_ApplyWorkingSnapshot(Dictionary<string,byte[]> s)=>System_ApplyMissionPresetSnapshot(s,Label);
 }
 static class ModSettingsApplication {public static bool Commit(string id)=>false;}
 static class ModSettingsSearchUpdateScope {public static IDisposable Begin(object o)=>new Scope();class Scope:IDisposable{public void Dispose(){}}}
}
class Base {protected void OnPropertyChanged(string n){}}
class ModSettingsDefinition {public int Value; public static ModSettingsDefinition CreateModDefaults()=>new(){Value=5};}
class TrailSource {public bool IsAvailable;public string ContextId="trail-a";public ModSettingsDefinition Capture()=>new(){Value=93};}
static class SerpLocalization {public const string SerpsModsResetSettingsFailed="failed";public static string Get(string key,params string[] args)=>key;}
class Controller {public string TargetGuid="a";}
class ResetTarget {public string Guid;public IModSettingsWorkingCopyEndpoint Endpoint;}
class Fixture:Base,IModSettingsWorkingSourceProvider {
 public event Action SourcesChanged;
 object log=new(); bool trailContext=true,workingContextEditable=true;string activeContextLabel="Map",workingSourceContextId="map-a";
 TrailSource trailSource=new();ModSettingsDefinition mapSourceDocument=new(){Value=91};
 Dictionary<string,IModSettingsPresetEndpoint> participants=new();
 Action<string,ModSettingsDefinition> applyEditorModesForMod;Action<ModSettingsDefinition,bool> applyEditorModes;
 int modeChanges;ModSettingsDefinition CaptureDocument(){modeChanges++;return new();}
 Dictionary<string,IModSettingsPresetEndpoint> FindCompatibleViewModels()=>participants;
 static ModSettingsDefinition CloneDocument(ModSettingsDefinition s)=>new(){Value=s.Value};
 Dictionary<string,byte[]> MaterializeSource(string id,IModSettingsWorkingCopyEndpoint e,ModSettingsDefinition s,bool playable)=>new(){{"value",BitConverter.GetBytes(s.Value)}};
 Controller presetController=new();ObservableCollection<ModSettingsWorkingSource> settingsSources=new();ModSettingsWorkingSource selectedSettingsSource;
 string preferredSettingsSourceToken="";bool System_CanLoadSettingsSource=>selectedSettingsSource!=null&&workingContextEditable;
 int refreshes,uiCommits;bool uiError;public object System_SettingsSources,System_SelectedSettingsSource,System_SettingsSourceVisibility;
 void System_RefreshOwnConfiguration(){refreshes++;((PresetLobbyModSettingsViewModel)participants["a"]).Value=23;}
 void System_LoadModDefaults()=>Apply("a",ModSettingsWorkingSourceRegistry.ModDefaultsId);
 void System_CommitConfiguration(){uiCommits++;}void DismissPresetStatus(){uiError=false;}void RaiseAccessProperties(){}
 string ResolveSettingsUiTextSafe(string k,string f)=>k;void SetPresetStatus(string s,bool error){uiError=error;}
 ObservableCollection<ModSettingsWorkingSource> globalSources=new();ModSettingsWorkingSource selectedGlobalSource,pendingGlobalSource;
 string preferredGlobalSourceToken="",globalResetStatus="";bool globalResetConfirmationVisible,globalResetStatusIsError,IsLocalSettingsHost=true;
 bool CanLoadGlobalSettingsSource=>GlobalSettingsResetPolicy.CanReset(globalResetConfirmationVisible,IsLocalSettingsHost,selectedGlobalSource!=null,participants.Count,participants.Values.OfType<PresetLobbyModSettingsViewModel>().Any(e=>!e.MissionPresetEditable));
 public object GlobalSettingsSources,SelectedGlobalSettingsSource;
 ResetTarget[] GetTargets()=>participants.Select(p=>new ResetTarget{Guid=p.Key,Endpoint=(IModSettingsWorkingCopyEndpoint)p.Value}).ToArray();
 bool IsReadOnlyMissionEndpoint(IModSettingsWorkingCopyEndpoint e)=>e is PresetLobbyModSettingsViewModel m&&!m.MissionPresetEditable;
 void RaiseGlobalSettingsResetProperties(){}void DismissGlobalSettingsResetStatus(){}void RecordError(string s){}
 __PROVIDER__
 __SELECTOR__
 __GLOBAL__
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Reject(Action a){try{a();}catch(InvalidOperationException){return;}catch(IOException){return;}catch(InvalidDataException){return;}throw new Exception("Expected reset failure");}
 public static void Run(){
  var f=new Fixture();var a=new PresetLobbyModSettingsViewModel();var b=new PresetLobbyModSettingsViewModel{Own=24,Value=92};
  f.participants.Add("a",a);f.participants.Add("b",b);f.applyEditorModesForMod=(_,__)=>f.modeChanges++;f.applyEditorModes=(_,__)=>f.modeChanges++;
  ModSettingsWorkingSourceRegistry.Register(f);
  try{
   var personal=ModSettingsWorkingSourceRegistry.PersonalSettingsId;
   Check(f.GetSources("a").Count(s=>s.Id==personal)==1,"Personal source missing/duplicated");
   f.RebuildSettingsSources();Check(f.selectedSettingsSource.Id=="map","Map preference changed");
   f.selectedSettingsSource=f.settingsSources.Single(s=>s.Id==personal);Check(a.Value==91,"Selecting reset applied it");
   f.LoadSelectedSettingsSource();Check(a.Value==23&&a.Active==23&&a.Commits==1&&a.Label=="Personal Settings"&&!f.uiError,"Individual personal reset failed");
   Check(f.refreshes==0&&f.uiCommits==0&&f.modeChanges==0&&a.Own==23,"Personal reset refreshed too early, double committed or changed author rules");
   f.RebuildSettingsSources();Check(f.selectedSettingsSource.Id==personal,"Refresh replaced explicit personal selection");
   f.workingSourceContextId="map-b";f.RebuildSettingsSources();Check(f.selectedSettingsSource.Id=="map","New map did not restore preferred source");
   f.Apply("a","map");Check(a.Value==91,"Map source was destroyed");
   f.trailSource.IsAvailable=true;f.RebuildSettingsSources();Check(f.selectedSettingsSource.Id=="trail","Trail preference lost");
   f.Apply("a",personal);f.Apply("a","trail");Check(a.Value==93&&a.Own==23,"Trail reset changed personal/source values");
   f.RefreshGlobalSources();f.selectedGlobalSource=f.globalSources.Single(s=>s.Id==personal);int before=a.Value;
   f.RequestGlobalSettingsReset();Check(f.globalResetConfirmationVisible&&a.Value==before,"Global request applied before confirmation");
   f.CancelGlobalSettingsReset();f.ConfirmGlobalSettingsReset();Check(a.Value==before,"Cancellation applied reset");
   f.RequestGlobalSettingsReset();f.ConfirmGlobalSettingsReset();Check(a.Value==23&&b.Value==24&&!f.globalResetStatusIsError,"Global personal reset failed");
   a.Value=101;b.Value=102;b.FailRead=1;Reject(()=>f.ApplyMany(new[]{"a","b"},personal));Check(a.Value==101&&b.Value==102,"Read failure partially applied");
   b.NullRead=true;Reject(()=>f.ApplyMany(new[]{"a","b"},personal));b.NullRead=false;
   b.NullBuffer=true;Reject(()=>f.ApplyMany(new[]{"a","b"},personal));b.NullBuffer=false;
   int aCommits=a.Commits,bCommits=b.Commits,oldBActive=b.Active;
   b.FailApply=1;Reject(()=>f.ApplyMany(new[]{"a","b"},personal));Check(a.Value==101&&b.Value==102&&a.Commits==aCommits&&b.Commits==bCommits,"Apply failure lost draft rollback or applied untouched backends");
   b.FailCommit=1;Reject(()=>f.ApplyMany(new[]{"a","b"},personal));Check(a.Value==101&&b.Value==102&&a.Active==101&&b.Active==oldBActive&&b.Commits==bCommits+1,"Backend failure lost draft rollback or replaced a failed backend's previous package");
   b.BeforeApply=()=>{if(b.PersonalBuffer!=null)b.PersonalBuffer[0]=255;};
   f.ApplyMany(new[]{"a","b"},personal);Check(b.Value==24,"Personal snapshot aliases provider buffers");b.BeforeApply=null;
   b.MissionPresetEditable=false;a.Value=111;Reject(()=>f.ApplyMany(new[]{"a","b"},personal));Check(a.Value==111,"Read-only target partially applied global reset");b.MissionPresetEditable=true;
   b.IsMissionPresetActive=false;Reject(()=>f.ApplyMany(new[]{"a","b"},personal));b.IsMissionPresetActive=true;
   f.workingContextEditable=false;Reject(()=>f.Apply("a",personal));f.workingContextEditable=true;
   f.IsLocalSettingsHost=false;f.RequestGlobalSettingsReset();Check(!f.globalResetConfirmationVisible,"Client obtained global reset confirmation");f.IsLocalSettingsHost=true;
   f.RefreshGlobalSources();f.selectedGlobalSource=f.globalSources.Single(s=>s.Id==personal);f.RequestGlobalSettingsReset();f.trailSource.IsAvailable=false;f.mapSourceDocument=null;
   Check(f.GetSources("a").Count==0,"Stale personal source survived context exit");f.ConfirmGlobalSettingsReset();Check(f.globalResetStatusIsError&&a.Value==111,"Unavailable source silently applied");
  }finally{ModSettingsWorkingSourceRegistry.Unregister(f);}
 }
}
""";
        fixture = fixture.Replace("__PROVIDER__", Methods("ExtendedData/src/TrailMissionSettingsCoordinator.cs",
            "GetSources", "Apply", "ApplyMany", "CopyPersonalSnapshot", "CommitPersonalSource", "ResolveWorkingSource", "DescribeWorkingSource"))
            .Replace("__SELECTOR__", Methods("APIShared/src/ModSettings/Presets/PresetLobbyModSettingsViewModel.Sources.cs", "RebuildSettingsSources", "LoadSelectedSettingsSource"))
            .Replace("__GLOBAL__", Methods("SerpsModsHost/src/SerpsModsDiagnosticsViewModel.cs", "RefreshGlobalSources", "RequestGlobalSettingsReset", "ConfirmGlobalSettingsReset", "CancelGlobalSettingsReset"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var trees = new[] { CSharpSyntaxTree.ParseText(fixture),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared/src/ModSettings/Presets/ModSettingsWorkingSources.cs"))),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "SerpsModsHost/src/GlobalSettingsResetPolicy.cs"))) };
        // The global production helper is internal in its own namespace.
        trees[0] = CSharpSyntaxTree.ParseText("using SerpsModsHost;\n" + fixture);
        var compilation = CSharpCompilation.Create("PersonalResetFixture", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("Fixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw(); }
    }
}
