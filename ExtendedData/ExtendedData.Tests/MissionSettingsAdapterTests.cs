using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using ExtendedData.Core;

internal static class MissionSettingsAdapterTests
{
    internal static void Run()
    {
        MissionClassification();
        PreparedEntriesKeepNewOwners();
        var entry = new ModSettingsEntry { PlayerSettings = new[] { "z", "a", "z" } };
        var document = new ModSettingsDefinition {
            Mods = new Dictionary<string, ModSettingsEntry>(StringComparer.OrdinalIgnoreCase) {
                ["mod"] = entry, ["alias"] = entry,
                ["z-invalid"] = new ModSettingsEntry { PlayerSettings = new[] { " padded" } }
            }
        };
        var original = document.Mods;
        Reject(() => ModSettingsJson.NormalizeAndValidate(document, "source"));
        Require(ReferenceEquals(original, document.Mods), "failed validation replaced the original dictionary");
        Require(entry.PlayerSettings.SequenceEqual(new[] { "a", "z" }), "partial normalization was lost");
        document.Mods.Remove("z-invalid");
        Require(ReferenceEquals(document, ModSettingsJson.NormalizeAndValidate(document, "source")), "document identity changed");
        Require(!ReferenceEquals(original, document.Mods) && ReferenceEquals(entry, document.Mods["alias"]), "normalization lost entry aliases");
        Require(ReferenceEquals(document.Mods.Comparer, StringComparer.Ordinal), "normalization lost ordinal identity");

        // Pruning keeps the caller's dictionary/comparer and override object, including
        // case-insensitive mod lookup. A shared entry exposes changes through both keys.
        document.Mods = new Dictionary<string, ModSettingsEntry>(StringComparer.OrdinalIgnoreCase) { ["MOD"] = entry, ["alias"] = entry };
        original = document.Mods;
        entry.PlayerSettings = new[] { "z", "remove", "a", "z" };
        entry.Overrides = new Dictionary<string, object> { ["fixed"] = 1, ["obsolete"] = false };
        var overrides = entry.Overrides;
        Require(ModSettingsJson.RemoveUnknownSettings(document, "mod", new[] { "z", "a", "fixed" }).SequenceEqual(new[] { "obsolete", "remove" }), "wrong removed names");
        Require(ReferenceEquals(original, document.Mods) && ReferenceEquals(overrides, entry.Overrides), "pruning replaced dictionaries");
        Require(entry.PlayerSettings.SequenceEqual(new[] { "z", "a", "z" }) && !overrides.ContainsKey("obsolete"), "pruning normalized ordering or failed to mutate overrides");
        ModSettingsJson.RemoveUnknownSettings(document, "mod", null);
        Require(!original.ContainsKey("MOD") && ReferenceEquals(entry, original["alias"]) && entry.Overrides.Count == 0, "pruning lost alias/removal semantics");

        document = new ModSettingsDefinition { SchemaVersion = 1, Mods = null };
        Reject(() => ModSettingsJson.NormalizeAndValidate(document, null));
        Require(document.Mods == null, "rejected schema initialized Mods");
        document.SchemaVersion = 3;
        ModSettingsJson.NormalizeAndValidate(document, null);
        Require(document.Mods != null && document.Mods.Count == 0, "null Mods did not normalize");

        entry.PlayerSettings = new[] { "z", "a", "z" };
        entry.Overrides = new Dictionary<string, object> { ["huge"] = new string('x', ModSettingsJson.MaximumBytes) };
        document.Mods["mod"] = entry;
        original = document.Mods;
        Reject(() => ModSettingsJson.Serialize(document));
        Require(!ReferenceEquals(original, document.Mods) && entry.PlayerSettings.SequenceEqual(new[] { "a", "z" }), "failed serialization hid normalization");

        const string json = "{\"schemaVersion\":3,\"mods\":{\"third.party\":{\"playerSettings\":[\"value\"],\"overrides\":{\"fixed\":true}}}}";
        Require(ModSettingsJson.Serialize(ModSettingsJson.ParseObject(json)) ==
            APIShared.ModSettings.MissionModSettingsJson.Serialize(APIShared.ModSettings.MissionModSettingsJson.ParseObject(json)), "legacy and Content wire output differs");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("Expected InvalidDataException");
    }

    private static void PreparedEntriesKeepNewOwners()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ExtendedData/src/TrailMissionSettingsCoordinator.cs")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
        string method = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "ExtendedData/src/TrailMissionSettingsCoordinator.cs"))).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(item => item.Identifier.Text == "CopyPreparedEntries").ToFullString();
        string fixture = """
using System; using System.Collections.Generic; using System.Linq;
using APIShared.ModSettings; using ExtendedData.Core;
public class CopyFixture {
 __METHOD__
 public static void Run() {
  var source = new MissionModSettingsDocument();
  source.Mods["new.owner"] = new MissionModSettingsEntry { PlayerSettings=new[]{"Moved"}, Overrides=new(){{"EnableMod",true}} };
  var target = new ModSettingsDefinition(); target.Mods["retired.owner"] = new ModSettingsEntry();
  CopyPreparedEntries(source,target);
  if(target.Mods.ContainsKey("retired.owner") || !target.Mods.ContainsKey("new.owner") ||
     !target.Mods["new.owner"].PlayerSettings.Contains("Moved") || !(bool)target.Mods["new.owner"].Overrides["EnableMod"])
   throw new Exception("Prepared document lost a newly synthesized owner or retained a removed owner.");
 }
}
""";
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Concat(new[] { typeof(ModSettingsDefinition).Assembly.Location,
                typeof(APIShared.ModSettings.MissionModSettingsDocument).Assembly.Location }).Distinct();
        var compilation = CSharpCompilation.Create("PreparedOwnerCopy", new[] { CSharpSyntaxTree.ParseText(
            fixture.Replace("__METHOD__", method)) }, paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("CopyFixture").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw(); }
    }

    private static void MissionClassification()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ExtendedData/src/TrailMissionSettingsCoordinator.cs")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
        string Methods(string path, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(root, path))).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => names.Contains(method.Identifier.Text)).Select(method => method.ToFullString()));
        // Execute the production catalog and group builders. The diagnostic service is the
        // boundary double; APIShared's separate tests verify its actual attribute filtering.
        string fixture = """
using System; using System.Text; using System.Collections.Generic; using System.Linq; using System.Reflection; using ExtendedData;
class LobbyModSettingsEntry { public string Guid; public object ViewModel; }
class Report { public string ModGuid,Diagnostic; public bool IsCompatible=true; public int Status;
 public object Endpoint; public IReadOnlyList<PropertyInfo> HostProperties; }
enum ModSettingsParticipantPurpose { MissionAuthoring }
static class SavegameModSettings { public static List<Report> Reports=new(); public static IEnumerable<Report> GetParticipantDiagnostics(ModSettingsParticipantPurpose p)=>Reports; }
static class ExtendedDataPlugin { public const string PluginGuid="ExtendedData"; }
static class SerpLocalization { public static string Get(string key)=>key; }
class TrailSettingGroupDefinition { public string Key,DisplayName; public string[] PropertyNames;
 public TrailSettingGroupDefinition(string key,string display,string[] names){Key=key;DisplayName=display;PropertyNames=names;} }
class Fixture {
 class Properties { public int UnitCosts {get;set;} public int HumanExtraUnitCosts {get;set;} public int Local {get;set;} }
 List<LobbyModSettingsEntry> registrations=new();
 IEnumerable<IGrouping<string,LobbyModSettingsEntry>> GetRegistrationGroups()=>registrations.GroupBy(item=>item.Guid);
 bool IsRegistrationGroupOptedOut(IEnumerable<LobbyModSettingsEntry> entries)=>false;
 string GetModDisplayName(LobbyModSettingsEntry entry)=>entry.Guid;
 void LogChangedCompatibilityFailures(IEnumerable<TrailModCompatibilityInfo> catalog){}
 __DISCOVER__
 __GROUPS__
 public static void Run(){
  var fixture=new Fixture();var endpoint=new Properties();
  fixture.registrations.Add(new(){Guid="UnitCosts_Serp",ViewModel=endpoint});
  fixture.registrations.Add(new(){Guid="OnlyIndependent",ViewModel=endpoint});
  SavegameModSettings.Reports.Add(new(){ModGuid="UnitCosts_Serp",Endpoint=endpoint,HostProperties=new[]{typeof(Properties).GetProperty("UnitCosts")}});
  SavegameModSettings.Reports.Add(new(){ModGuid="OnlyIndependent",Endpoint=endpoint,HostProperties=Array.Empty<PropertyInfo>()});
  var catalog=fixture.DiscoverModCompatibility();
  if(catalog.Count!=1 || catalog[0].ModId!="UnitCosts_Serp")throw new Exception("Empty mission provider remained in the classification catalog.");
  var groups=BuildSettingGroups(catalog[0]);
  if(groups.SelectMany(group=>group.PropertyNames).Any(name=>name!="UnitCosts") || groups.Length!=1)
   throw new Exception("Known setting group reintroduced an excluded property.");
 }
}
""";
        fixture = fixture.Replace("__DISCOVER__", Methods("ExtendedData/src/TrailMissionSettingsCoordinator.cs", "DiscoverModCompatibility"))
            .Replace("__GROUPS__", Methods("ExtendedData/src/ExtendedDataSettingsViewModel.cs", "BuildSettingGroups", "AddKnownGroup", "PropertyOrder", "HumanizePropertyName"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("MissionClassification", new[] { CSharpSyntaxTree.ParseText(fixture),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "ExtendedData/src/TrailModCompatibilityInfo.cs"))) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("Fixture").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw(); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
