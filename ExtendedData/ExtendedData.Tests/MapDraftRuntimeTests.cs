using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class MapDraftRuntimeTests
{
    internal static void Run()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ExtendedData/src/MapModSettingsCoordinator.cs")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "ExtendedData/src/MapModSettingsCoordinator.cs"))).GetRoot();
        string[] names = { "OnMapSelectionChanged", "ApplySelectedMap", "StartSkirmishGameHook", "HandleMissionEnded",
            "HandleMissionInitialization", "FreezeMapDraft", "ReopenMapDraft", "ExitMapContext", "IsMapContextCurrent", "MatchesActiveMap", "LogMapContext" };
        var methods = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.Text));
        // Execute production coordination, with file/UI/endpoint boundaries replaced.
        // Separate APIShared tests cover actual setting application and disk persistence.
        string fixture = """
using System; using System.Collections.Generic; using IOPath=System.IO.Path;
namespace Shared {static class DebugLogHelper {public static List<string> Lines=new();public static void LogInfo(object l,string m)=>Lines.Add(m);public static void LogErrorLimited(object l,string s,string m)=>throw new Exception(m);}static class DevDiag{public static void Log(Func<string> m){}}}
namespace ExtendedData {
class ModSettingsDefinition {public int Value;}
class FileHeader {public string filePath,fileName;public uint crc;public ModSettingsDefinition Document;}
class FRONT_Multiplayer {public bool trailMakerMode;public FileHeader Selected;}
class HUD_IngameMenu {public class RestartSkirmishMapInfo {}}
class MissionLifecycleNotification {public Context Context=new();public Context ReplacementContext;public MissionEndReason EndReason;public MissionInitializationPhase Phase;}
class Context {public long SessionId,RestartedFromSessionId;public bool IsEditor;}
enum MissionInitializationPhase {BeforeLoad,BeforeNativeStart,AfterNativeStart}
enum MissionEndReason {SceneChanged,ApplicationExit,Replaced}
static class MissionRestart {public static bool IsInProgress;}
static class SerpLocalization {public static string Get(string s)=>s;}
class SettingsBoundary {
 public bool Active,Editable,Prepared=true,FailFreeze;public int Personal=23,Value=23,Applications,Freezes,Reopens;
 public bool IsContextActive(string s)=>Active&&s=="Map";
 public void SetMapSourceDocument(ModSettingsDefinition d,string id=null){}
 public string[] EnterStrict(ModSettingsDefinition d,bool editable,string source,string presetLabel){Active=true;Editable=editable;Value=d.Value;Applications++;return Array.Empty<string>();}
 public void ExitContext(bool force){Active=false;Editable=false;Value=Personal;}
 public Dictionary<string,Dictionary<string,byte[]>> CaptureWorkingDraft()=>new(){{"mod",new(){{"value",BitConverter.GetBytes(Value)}}}};
 public void ApplyWorkingDraft(Dictionary<string,Dictionary<string,byte[]>> s,string label,bool editable,ModSettingsDefinition source){if(!editable&&FailFreeze){FailFreeze=false;throw new InvalidOperationException("freeze fixture");}Active=true;Editable=editable;Value=BitConverter.ToInt32(s["mod"]["value"]);if(editable)Reopens++;else Freezes++;}
 public bool TryPrepareRestartSettings()=>Prepared;
}
public class MapFixture {
 private object log=new();private bool enabled=true,mapContextActive,mapMissionActive,launchInProgress;
 private string activeMapFileName="",activeMapPath="",activeJson="",activeDraftIdentity="";private uint activeMapCrc;
 private ModSettingsDefinition activeDocument;private CoopSettingsDraftState mapDraft=new();
 private Dictionary<string,Dictionary<string,byte[]>> workingSnapshots;private SettingsBoundary settingsCoordinator=new();
 private Action<FRONT_Multiplayer,HUD_IngameMenu.RestartSkirmishMapInfo> startSkirmishGameOriginal;
 private int reads,launches;
 private bool HasLocalAuthority(FRONT_Multiplayer l)=>true;
 private bool IsHostLobby(FRONT_Multiplayer l=null)=>false;
 private FileHeader GetSelectedHeader(FRONT_Multiplayer l)=>l.Selected;
 private string NormalizePath(string p)=>p;
 private string GetLobbyMapName(FRONT_Multiplayer l,FileHeader h)=>h.fileName;
 private bool TryReadDocument(FileHeader h,out ModSettingsDefinition d,out string json,bool logFailure){reads++;d=h.Document;json=d==null?"":"embedded";return d!=null;}
 private void ShowMessage(string title,string body)=>throw new Exception(body);
 private void BroadcastCurrentState(bool apply,string mapFileName=null,uint mapCrc=0){}
 __METHODS__
 static void Check(bool b,string text){if(!b)throw new Exception(text);}
 public static void Run(){
  var f=new MapFixture();var s=f.settingsCoordinator;var l=new FRONT_Multiplayer{Selected=new FileHeader{filePath="map-a",fileName="a.map",crc=42,Document=new ModSettingsDefinition{Value=91}}};
  f.startSkirmishGameOriginal=(_,__)=>{f.launches++;f.mapMissionActive=true;};
  f.OnMapSelectionChanged(l);Check(s.Value==91&&s.Editable&&s.Applications==1,"Embedded source not applied once");
  s.Value=99;int reads=f.reads;
  for(int i=0;i<3;i++)f.OnMapSelectionChanged(l);
  l.Selected=null;f.OnMapSelectionChanged(l);
  Check(s.Value==99&&f.reads==reads&&s.Applications==1,"Lobby rebuild overwrote draft or reread files");
  l.Selected=new FileHeader{filePath="map-a",fileName="a.map",crc=42,Document=new ModSettingsDefinition{Value=91}};
  s.FailFreeze=true;bool failed=false;try{f.StartSkirmishGameHook(l,null);}catch(InvalidOperationException){failed=true;}Check(failed&&s.Value==99&&s.Editable&&f.launches==0,"Failed launch did not recover the editable draft");s.Reopens=0;f.StartSkirmishGameHook(l,null);Check(s.Value==99&&!s.Editable&&s.Freezes==1,"Start did not freeze working draft");
  MissionRestart.IsInProgress=true;f.StartSkirmishGameHook(l,null);MissionRestart.IsInProgress=false;
  f.HandleMissionEnded(new MissionLifecycleNotification{Context=new Context{SessionId=7},ReplacementContext=new Context{RestartedFromSessionId=7}});
  Check(s.Value==99&&s.Freezes==1&&f.launches==2,"True restart re-resolved embedded settings");
  f.HandleMissionEnded(new MissionLifecycleNotification{Context=new Context{SessionId=8}});
  Check(s.Value==23&&!s.Active&&f.workingSnapshots!=null,"End did not restore personal settings and retain detached draft");
  f.OnMapSelectionChanged(l);Check(s.Value==99&&s.Editable&&s.Applications==1&&s.Reopens==1,"Matching return lost detached working draft");
  s.Value=101;f.StartSkirmishGameHook(l,null);f.HandleMissionEnded(new MissionLifecycleNotification());f.OnMapSelectionChanged(l);
  Check(s.Value==101&&s.Reopens==2,"Second complete round reset settings");
  l.Selected=new FileHeader{filePath="vanilla",fileName="v.map",crc=43};f.OnMapSelectionChanged(l);
  Check(s.Value==23&&!s.Active&&!f.mapDraft.HasDraft&&f.workingSnapshots==null,"Vanilla map retained temporary settings");
  l.Selected=new FileHeader{filePath="map-a",fileName="a.map",crc=42,Document=new ModSettingsDefinition{Value=91}};f.OnMapSelectionChanged(l);
  Check(s.Value==91&&s.Applications==2,"Returning after a real source change retained the discarded draft");
  f.StartSkirmishGameHook(l,null);f.HandleMissionEnded(new MissionLifecycleNotification{Context=new Context{SessionId=8},ReplacementContext=new Context{SessionId=9}});
  Check(!f.mapContextActive&&f.workingSnapshots==null&&s.Value==23,"Unrelated replacement retained map ownership");
  f.OnMapSelectionChanged(l);s.Value=117;
  var init=new MissionLifecycleNotification{Context=new Context{SessionId=10},Phase=MissionInitializationPhase.BeforeNativeStart};
  f.HandleMissionInitialization(init);f.HandleMissionInitialization(init);
  Check(s.Value==117&&!s.Editable&&f.workingSnapshots!=null,"Remote/common start did not freeze late synchronized values");
  f.HandleMissionEnded(init);f.OnMapSelectionChanged(l);
  Check(s.Value==117&&s.Editable,"Failed common start lost the detached draft");
  s.ExitContext(true);f.OnMapSelectionChanged(l);
  Check(s.Active&&s.Value==91&&s.Editable,"Re-entering an explicitly left live context skipped source application");
  Check(Shared.DebugLogHelper.Lines.Exists(x=>x.Contains("state=frozen"))&&Shared.DebugLogHelper.Lines.Exists(x=>x.Contains("session=8")),"Support log lost transition/session");
 }
}
}
""";
        fixture = fixture.Replace("__METHODS__", string.Join("\n", methods.Select(m => m.ToFullString())));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("MapDraftFixture", new[] {
            CSharpSyntaxTree.ParseText(fixture), CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "ExtendedData/src/CoopSettingsDraftState.cs")))
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("ExtendedData.MapFixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }
}
