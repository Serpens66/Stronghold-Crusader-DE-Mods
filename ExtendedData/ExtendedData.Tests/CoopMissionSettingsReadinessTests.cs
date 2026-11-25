using ExtendedData;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CoopMissionSettingsReadinessTests
{
    public static void Run()
    {
        ProductionRuntime();
        const string package = "OK|package|fingerprint", pending = "WAITING", invalid = "ERROR|INVALID|";
        string expected = CoopMissionSettingsReadiness.BindMission(package, 1, 2);
        Check(expected == "OK|package|fingerprint|settings:1/2", "mission identity changed the existing package fields");
        Check(expected != CoopMissionSettingsReadiness.BindMission(package, 1, 3), "another mission reused readiness");
        Check(expected != CoopMissionSettingsReadiness.BindMission(package, 2, 2), "another trail reused readiness");
        Check(CoopMissionSettingsReadiness.Resolve(expected, expected, pending, invalid, null, true) == expected,
            "prepared mission was not ready");
        Check(CoopMissionSettingsReadiness.Resolve(expected, expected, pending, invalid, "", true) == expected,
            "empty error blocked a prepared mission");
        Check(CoopMissionSettingsReadiness.Resolve(expected, expected, pending, invalid, null, false) == pending,
            "unprepared mission was published ready");
        foreach (bool prepared in new[] { true, false })
        foreach (string reason in new[] { "Missing owner", "Incompatible owner" })
            Check(CoopMissionSettingsReadiness.Resolve(expected, expected, pending, invalid, reason, prepared) == invalid + reason,
                "provider failure was hidden by preparation state");
        foreach (string proposed in new[] { "ERROR|MISSING", "ERROR|MISMATCH", "ERROR|INVALID|Lord", "ERROR|DISABLED",
            pending, package, CoopMissionSettingsReadiness.BindMission(package, 1, 3), expected.ToLowerInvariant(), "", null })
        foreach (bool prepared in new[] { true, false })
            Check(CoopMissionSettingsReadiness.Resolve(proposed, expected, pending, invalid, "Missing owner", prepared) == proposed,
                "an existing status was replaced or a different mission promoted");
    }

    private static void ProductionRuntime()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ExtendedData/src/ExtendedDataRuntime.cs")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
        var runtime = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "ExtendedData/src/ExtendedDataRuntime.cs"))).GetRoot();
        string[] names = { "ExpectedReadyStatus", "SetLocalPackageStatus", "SelectedMissionSettingsSource" };
        string methods = string.Join("\n", runtime.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => names.Contains(method.Identifier.Text)).Select(method => method.ToFullString()));
        var ai = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "ExtendedData/src/ExtendedDataRuntime.CoopAiStart.cs"))).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.Text == "CoopAiStartBlockReason");
        // Execute only the real admission prefix; the remaining method previews AI/native data.
        methods += ai.WithBody(SyntaxFactory.Block(ai.Body.Statements.TakeWhile(statement => statement is not TryStatementSyntax)
            .Concat(new[] { SyntaxFactory.ParseStatement("return null;") }))).ToFullString();
        string fixture = """
using System;
namespace APIShared.Savegames { static class MultiplayerSavePreparation { public static object Current; } }
namespace ExtendedData {
class FRONT_Multiplayer { public object currentLobby=new(); }
class View { public FRONT_Multiplayer FRONTMultiplayer=new(); }
class Activation { public bool MissionModSettings=true; }
class Settings { public string ActiveCoopPackageId="pkg",ActiveCoopPackageFingerprint="fp",Status;public int Reports;
 public void SetLocalPackageStatus(string value){Status=value;}public void System_RequestPerPlayerSettingsPublish(){Reports++;} }
class ExtendedDataSettingsViewModel { public const string WaitingStatus="WAITING",InvalidStatusPrefix="ERROR|INVALID|"; }
class Lords {public string MissionDigest="lord-digest";}
class Loaded { public int TrailNumber=1,MissionNumber=1;public Lords LordRequirements; }
class ResolvedMission {public Loaded Loaded=new();}
class Coordinator {public string ProviderBlockReason,PreparedSource;public bool HasPreparedSource(string source)=>PreparedSource==source;}
class Map {public bool Active;public bool IsActiveForLobby(FRONT_Multiplayer self)=>Active;}
public class RuntimeFixture {
 Activation activation=new();Settings settings=new();Coordinator missionSettingsCoordinator=new();Map mapSettingsCoordinator=new();
 ResolvedMission selected=new();string localPackageError;static View view=new();
 static View GetExistingMainViewModel()=>view;
 static string LordSelectionDigest(FRONT_Multiplayer self)=>"selection";
 __METHODS__
 static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
 public static void Run(){
  var f=new RuntimeFixture();string expected=f.ExpectedReadyStatus();
  f.SetLocalPackageStatus(expected);Require(f.settings.Status=="WAITING","unprepared source was ready");
  f.missionSettingsCoordinator.PreparedSource=f.SelectedMissionSettingsSource();
  f.SetLocalPackageStatus(expected);Require(f.settings.Status==expected,"valid empty/no-AI source was blocked");
  f.missionSettingsCoordinator.ProviderBlockReason="missing any.mod";
  f.SetLocalPackageStatus(expected);Require(f.settings.Status=="ERROR|INVALID|missing any.mod","missing client provider was published ready");
  Require(f.CoopAiStartBlockReason(view.FRONTMultiplayer)=="missing any.mod","fresh mission admission lost provider block");
  APIShared.Savegames.MultiplayerSavePreparation.Current=new();
  Require(f.CoopAiStartBlockReason(view.FRONTMultiplayer)==null,"old mission provider block rejected an independent save");
  APIShared.Savegames.MultiplayerSavePreparation.Current=null;
  f.missionSettingsCoordinator.ProviderBlockReason=null;f.SetLocalPackageStatus(expected);
  Require(f.settings.Status==expected,"resolved provider retained stale failure");
  f.selected.Loaded.MissionNumber=2;string next=f.ExpectedReadyStatus();
  Require(next!=expected,"mission without Lord requirements reused old peer acknowledgement");
  f.SetLocalPackageStatus(next);Require(f.settings.Status=="WAITING","old source acknowledged another mission");
  f.mapSettingsCoordinator.Active=true;f.SetLocalPackageStatus(next);
  Require(f.settings.Status==next,"explicit current Map context was falsely blocked");
  f.mapSettingsCoordinator.Active=false;f.missionSettingsCoordinator.PreparedSource=f.SelectedMissionSettingsSource();
  f.SetLocalPackageStatus(next);Require(f.settings.Status==next,"new valid mission never became ready");
  f.selected.Loaded.LordRequirements=new();string lord=f.ExpectedReadyStatus();
  Require(lord.Contains("lord-digest")&&lord.Contains("selection")&&lord.EndsWith("|settings:1/2"),"settings scope removed Lord readiness identity");
  f.SetLocalPackageStatus("ERROR|INVALID|lord failure");Require(f.settings.Status=="ERROR|INVALID|lord failure","existing Lord rejection was overwritten");
  f.selected=null;f.SetLocalPackageStatus("OK|pkg|fp");Require(f.settings.Status=="OK|pkg|fp","package navigation without mission was blocked");
  f.selected=new();f.activation.MissionModSettings=false;f.SetLocalPackageStatus(f.ExpectedReadyStatus());
  Require(f.settings.Status==f.ExpectedReadyStatus(),"disabled settings feature introduced its own source requirement");
 }
}
}
""";
        var sources = new[] { CSharpSyntaxTree.ParseText(fixture.Replace("__METHODS__", methods)),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "ExtendedData/src/CoopMissionSettingsReadiness.cs"))) };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CoopProviderRuntime", sources, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        try { assembly.GetType("ExtendedData.RuntimeFixture").GetMethod("Run").Invoke(null, null); }
        catch (System.Reflection.TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new System.Exception(message);
    }
}
