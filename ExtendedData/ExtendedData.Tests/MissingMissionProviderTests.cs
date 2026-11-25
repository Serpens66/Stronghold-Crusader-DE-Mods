using ExtendedData;
using ExtendedData.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class MissingMissionProviderTests
{
    internal static void Run()
    {
        RetainedDocument();
        CoordinatorPreflight();
    }

    private static void RetainedDocument()
    {
        var state = new TrailMissingProviderState();
        var document = new ModSettingsDefinition();
        document.Mods["available"] = new ModSettingsEntry { Overrides = new() { ["Enabled"] = true } };
        // Even an explicit empty entry is a required owner under the APIShared contract.
        document.Mods["missing"] = new ModSettingsEntry();
        Check(state.Check(document, new[] { "available" }, "first"), "empty missing owner was ignored");
        Check(state.Missing.SequenceEqual(new[] { "missing" }), "wrong provider list");
        document.Mods["available"].Overrides["Enabled"] = false;
        document.Mods.Remove("missing");
        var captured = state.Capture();
        Check((bool)captured.Mods["available"].Overrides["Enabled"] && captured.Mods.ContainsKey("missing"),
            "blocked document aliased the caller or lost installed overrides");
        captured.Mods.Clear();
        state.Missing[0] = "changed";
        Check(state.Capture().Mods.Count == 2 && state.Missing[0] == "missing", "returned evidence was mutable");
        Check(state.Check(document, new[] { "AVAILABLE" }, "second") && state.Source == "second" &&
            state.Missing.SequenceEqual(new[] { "available" }), "replacement or ordinal identity failed");
        Check(!state.Check(document, new[] { "available" }, "resolved") && !state.IsBlocked && state.Capture() == null,
            "successful new selection did not clear blocked state");
        state.Check(document, Array.Empty<string>(), "again");
        state.Clear();
        Check(!state.IsBlocked && state.Missing.Length == 0 && state.Source.Length == 0 && state.Capture() == null,
            "exit cleanup retained a provider block");
    }

    private static void CoordinatorPreflight()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ExtendedData/src/TrailMissionSettingsCoordinator.cs")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "ExtendedData/src/TrailMissionSettingsCoordinator.cs"))).GetRoot();
        string[] names = { "Enter", "PreflightProviders", "RequireAvailableProviders", "ValidateStrict",
            "PrepareCoopSettings", "CaptureWorkingDraft", "ApplyWorkingDraft", "CaptureDocument", "SetMapSourceDocument",
            "ObserveMapProviderSelection" };
        string methods = string.Join("\n", syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(item => names.Contains(item.Identifier.Text)).Select(item => item.ToFullString()));
        string properties = string.Join("\n", syntax.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Where(item => new[] { "HasMissingProviders", "MissingProviderGuids", "ProviderBlockReason" }
                .Contains(item.Identifier.Text)).Select(item => item.ToFullString()));
        // Execute the actual coordinator entry/strict/draft/capture methods. Only endpoint,
        // context, logging and UI boundaries are doubled; there is no Unity/Noesis startup.
        string fixture = """
using System; using System.Collections.Generic; using System.Linq; using System.IO;
using ExtendedData; using ExtendedData.Core; using APIShared.ModSettings;
namespace Shared {
 static class DevDiag { public static void Log(Func<string> text) {} }
 static class DebugLogHelper { public static void LogErrorLimited(object log,string key,string text) {} }
}
namespace ExtendedData {
 static class SerpLocalization { public static string Get(string key)=>"Missing: {Mods}"; }
 static class ModSettingsApplication {
  public static bool HasApplicationEndpoints, HasRestartPreparation, HasActivationFailures;
  public static int ContextCalls;
  public static void EnterContext(string context) { ContextCalls++; }
 }
 public class ProviderFixture {
  readonly TrailMissingProviderState missingProviders=new();
  string mapProviderSelectionId;
  readonly TrailSettingsSourceState trailSource=new();
  readonly CoopSettingsDraftState coopSettingsDraft=new();
  ModSettingsDefinition activeCreatorDocument;
  ModSettingsDefinition mapSourceDocument; string workingSourceContextId;
  Dictionary<string,IModSettingsPresetEndpoint> participants=new();
  object log; bool trailContext, launchedThroughCustomize;
  string activeContextLabel="Trail";
  int applications, captures, validations, changes;
  Action SourcesChanged;
  __PROPERTIES__
  __METHODS__
  Dictionary<string,IModSettingsPresetEndpoint> FindCompatibleViewModels()=>participants;
  static ModSettingsDefinition CloneDocument(ModSettingsDefinition doc)=>MissionSettingsMigration.Migrate(doc);
  static string BuildRestartContext(string source,ModSettingsDefinition doc,string path=null)=>source;
  static string[] GetMissingMentionedMods(ModSettingsDefinition doc)=>Array.Empty<string>();
  void ValidateDocumentValues(ModSettingsDefinition doc) { validations++; }
  void FreezeWorkingDraft() { RequireAvailableProviders(); }
  void ReopenWorkingDraft() { RequireAvailableProviders(); }
  Dictionary<string,Dictionary<string,byte[]>> CaptureCurrentSnapshots(Dictionary<string,IModSettingsPresetEndpoint> p) {
   captures++; return new();
  }
  TrailSettingMode getPropertyMode(string id,string name)=>TrailSettingMode.Fixed;
  void ApplyDocument(ModSettingsDefinition document,bool editable,string presetLabel="Trail",
   bool preserveCurrentValues=false,Dictionary<string,Dictionary<string,byte[]>> startingSnapshots=null,
   bool materializeCurrentValues=false,bool preserveEditorModes=false) { applications++; trailContext=true; }
  static void Require(bool value,string text) { if(!value)throw new Exception(text); }
  static void Blocked(Action action) {
   try { action(); } catch(MissingMissionSettingsProviderException) { return; }
   throw new Exception("Missing provider was allowed to mutate/capture a context.");
  }
  public static void Run() {
   var f=new ProviderFixture(); f.SourcesChanged=()=>f.changes++;
   f.participants["known"]=null;
   var old=new ModSettingsDefinition(); old.Mods["known"]=new();
   f.Enter(old,false,"old");
   var active=f.activeCreatorDocument;
   int contexts=ModSettingsApplication.ContextCalls, applied=f.applications;
   var missing=new ModSettingsDefinition();
   missing.Mods["known"]=new(){Overrides=new(){{"Enabled",true}}};
   missing.Mods["missing"]=new();
   Require(f.Enter(missing,false,"new").SequenceEqual(new[]{"missing"}),"Enter did not report missing owner");
   Require(f.applications==applied && ModSettingsApplication.ContextCalls==contexts &&
    ReferenceEquals(active,f.activeCreatorDocument) && f.trailSource.ContextId=="old",
    "preflight mutated an endpoint, context, active document or previous source");
   Require(f.changes>0 && f.ProviderBlockReason.Contains("missing"),"block was not observable");
   Blocked(()=>f.CaptureDocument());
   Blocked(()=>f.CaptureWorkingDraft());
   Blocked(()=>f.ApplyWorkingDraft(null,"old",true));
   Require(f.captures==0 && f.applications==applied,"blocked capture/draft touched endpoints");
   Blocked(()=>f.ValidateStrict(missing,"strict"));
   Require(f.validations==0 && ModSettingsApplication.ContextCalls==contexts,"strict preflight applied values");
   f.ObserveMapProviderSelection("map.m4x:1");
   f.SetMapSourceDocument(missing,"map.m4x");
   Require(f.HasMissingProviders && f.mapSourceDocument==null && ModSettingsApplication.ContextCalls==contexts,
    "map source changed context before provider preflight");
   Blocked(()=>f.ValidateStrict(missing,"embedded Map",MissionProviderScope.Map));
   f.ObserveMapProviderSelection(null);
   f.SetMapSourceDocument(null);
   f.ObserveMapProviderSelection("map.m4x:1");
   Require(f.HasMissingProviders,"null row or same map identity erased required providers");
   f.ObserveMapProviderSelection("vanilla.m4x:2");
   f.SetMapSourceDocument(null);
   Require(!f.HasMissingProviders,"concrete Vanilla map inherited previous map block");
   f.Enter(missing,false,"coop");
   f.ObserveMapProviderSelection(null);
   f.SetMapSourceDocument(null);
   f.ObserveMapProviderSelection("different-list-row:3");
   Require(f.HasMissingProviders,"map scope cleanup erased a pending Coop requirement");
   Require(f.PrepareCoopSettings(missing,"slot2",false,false).SequenceEqual(new[]{"missing"}),"Coop lost error");
   f.participants["missing"]=null;
   Require(f.PrepareCoopSettings(missing,"slot2",false,false).Length==0 && !f.HasMissingProviders,
    "same identity did not retry after provider recovery");
   applied=f.applications;
   f.PrepareCoopSettings(missing,"slot2",false,false);
   Require(f.applications==applied,"cached Ready reapplied source");
   f.participants.Remove("missing");
   f.PrepareCoopSettings(missing,"slot3",false,false);
   Require(f.HasMissingProviders,"new slot did not block");
   f.PrepareCoopSettings(old,"slot2",false,false);
   Require(!f.HasMissingProviders && f.applications==applied+1,"return to previous slot retained stale blocked identity");
   f.Enter(new ModSettingsDefinition(),false,"vanilla");
   Require(!f.HasMissingProviders,"empty vanilla context inherited missing requirements");
  }
 }
 enum TrailSettingMode { Player, Fixed, ModDefault }
}
""";
        var sources = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(fixture.Replace("__METHODS__", methods)
            .Replace("__PROPERTIES__", properties)) };
        foreach (string file in new[] { "TrailMissingProviderState.cs", "TrailSettingsSourceState.cs",
            "MissionSettingsMigration.cs", "CoopSettingsDraftState.cs" })
            sources.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "ExtendedData/src", file))));
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Concat(new[] { typeof(ModSettingsDefinition).Assembly.Location,
                typeof(APIShared.ModSettings.MissionModSettingsDocument).Assembly.Location }).Distinct();
        var compilation = CSharpCompilation.Create("MissingProviderCoordinator", sources,
            paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("ExtendedData.ProviderFixture").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw(); }
    }

    private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
}
