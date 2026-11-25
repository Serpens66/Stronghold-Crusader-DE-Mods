using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class PreparedSaveReadinessTests
{
    internal static void Run()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ExtendedData/src/LordDataSyncCoordinator.SavePreparation.cs")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Workspace not found.");
        string productionMethod = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "ExtendedData/src/LordDataSyncCoordinator.SavePreparation.cs"))).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.Text == "PreparedSaveReadiness").ToFullString();
        // Compile the actual complete production decision. Only external SDK, filesystem
        // provenance, author verification and settings/roster storage are isolated below.
        // In particular there is no copied peer/error/READY or file-mask policy.
        string fixture = """
using System; using System.Collections.Generic; using System.Linq; using System.IO;
using APIShared.Lobby; using APIShared.Savegames;
namespace APIShared.Lobby {
 public enum LobbyLaunchGuardState { Ready, Waiting, Rejected }
 public readonly struct LobbyLaunchGuardResult {
  public readonly LobbyLaunchGuardState State; public readonly string Reason;
  public LobbyLaunchGuardResult(LobbyLaunchGuardState state,string reason) { State=state;Reason=reason; }
  public static LobbyLaunchGuardResult Ready=>default;
  public static LobbyLaunchGuardResult Waiting(string reason)=>new(LobbyLaunchGuardState.Waiting,reason);
  public static LobbyLaunchGuardResult Rejected(string reason)=>new(LobbyLaunchGuardState.Rejected,reason);
 }
}
namespace APIShared.Savegames {
 public class MultiplayerSavePreparationSnapshot {
  public string OperationId="operation",LocalPath=Path.GetFullPath("fixture.sav"),LocalHash="hash";
  public ulong LobbyId=10; public bool IsHost=true;
  public IReadOnlyList<ulong> HumanPeers=new ulong[]{1001,1002};
 }
}
namespace ExtendedData {
 public class Fixture {
  private class Scope { public string Operation="operation",Path=System.IO.Path.GetFullPath("fixture.sav"),Hash="hash",Failure;
   public bool SeenPublication=true,ExactDigests=true,Frozen;
   public string ManifestDigest="package-new",SnapshotDigest="snapshot-new"; }
  private class Slot { public int PlayerId=3; public bool NeedsLocalFiles,NeedsSnapshot=true; }
  private class Manifest { public string SessionId="10",Digest="package-new"; public bool UseLocalValues;
   public List<Slot> Slots=new(){new Slot()}; }
  private class Snapshot { public string Digest="snapshot-new"; }
  private class Settings {
   public string LordPackageStatus="LOCAL|package-new|4",LordDataStatus="READY|snapshot-new";
   public string[] LordPackageStatusData=new string[9],LordDataStatusData=new string[9];
  }
  private Scope preparedLordSave=new(); private Manifest packageManifest=new();
  private Snapshot active=new(); private Settings settings=new();
  private bool ValidatePreparedAuthor(Scope scope,Manifest manifest,Snapshot snapshot)=>true;
  private bool HasVerifiedSnapshot(out string reason) { reason=""; return true; }
  __METHOD__
  private static void Require(bool condition,string reason) { if(!condition)throw new Exception(reason); }
  public static void Run() {
   var fixture=new Fixture(); var preparation=new MultiplayerSavePreparationSnapshot();
   fixture.settings.LordPackageStatusData[2]="LOCAL|package-new|4";
   fixture.settings.LordDataStatusData[2]="ERROR|previous selection failed";
   // The client's operation-bound SDK ACK can be handled before its separately
   // queued per-player READY publication. Exercise both actual guard invocations.
   Action deferredReady=()=>fixture.settings.LordDataStatusData[2]="READY|snapshot-new";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Waiting,
    "Stale undigested remote ERROR aborted a newly acknowledged save.");
   deferredReady();
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Ready,
    "Matching saved-snapshot READY did not complete the pending admission.");
   foreach(string stale in new[]{null,"","READY|snapshot-old","ERROR|unbound remote error"}) {
    fixture.settings.LordDataStatusData[2]=stale;
    Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Waiting,
     "Missing, stale or undigested remote status admitted or rejected the save.");
   }
   fixture.settings.LordDataStatusData[2]="READY|snapshot-new";
   fixture.preparedLordSave.Failure="current local apply failure";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Rejected,
    "An operation-bound own failure was weakened to Waiting.");
   fixture.preparedLordSave.Failure=null;
   fixture.settings.LordDataStatus="ERROR|current local error";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Rejected,
    "Own ERROR was treated as an unbound remote report.");
   fixture.settings.LordDataStatus="READY|snapshot-new";
   fixture.packageManifest.Slots[0].NeedsLocalFiles=true;
   fixture.settings.LordPackageStatusData[2]="LOCAL|package-new|0";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Rejected,
    "A matching package report lacking required files was weakened to Waiting.");
   fixture.settings.LordPackageStatusData[2]="LOCAL|package-old|4";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Waiting,
    "A stale package digest was accepted as current required files.");
   fixture.settings.LordPackageStatusData[2]="LOCAL|package-new|4";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Ready,
    "Correct package, file mask and exact READY failed admission.");
   preparation.IsHost=false;
   fixture.settings.LordDataStatusData[2]="ERROR|remote old selection";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Ready,
    "The client's own readiness unexpectedly started enforcing remote reports.");
   preparation.OperationId="retired-operation";
   Require(fixture.PreparedSaveReadiness(preparation).State==LobbyLaunchGuardState.Rejected,
    "An unrelated operation inherited the prepared Lord scope.");
  }
 }
 internal static class PlayerIdentityHelper {
  public readonly struct Identity { public bool IsResolved=>true; public int PlayerId=>1; }
  public static Identity CaptureLocalPlayerId(bool ignored)=>default;
  public static bool TryCaptureHumanRoster(bool a,bool b,out Dictionary<int,ulong> humans,out string error,out object context) {
   humans=new(){{1,1001},{2,1002}};error="";context=null;return true;
  }
 }
}
""";
        string[] platformReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator);
        var compilation = CSharpCompilation.Create("PreparedSaveReadinessFixture",
            new[] { CSharpSyntaxTree.ParseText(fixture.Replace("__METHOD__", productionMethod)),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "ExtendedData/src/LordSaveFileRead.cs"))) },
            platformReferences.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics));
        try { Assembly.Load(output.ToArray()).GetType("ExtendedData.Fixture").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
}
