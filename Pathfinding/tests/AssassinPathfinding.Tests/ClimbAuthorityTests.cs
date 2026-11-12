using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    private static void TestClimbAuthority(string root)
    {
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "Pathfinding/src/Assassins/AssassinClimbRuntime.cs"))).GetRoot();
        string[] names = { "IsValidClimbPacket", "IsAuthorizedClimbMessage", "OnPacketReceived", "ApplyState" };
        string methods = string.Join("\n", syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
        string extender = Environment.GetEnvironmentVariable("SHCDESE_EXTENDER_DIR") ??
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        string extenderAssembly = Path.Combine(extender, "SHCDESE.dll");
        var playerLimit = Assembly.LoadFrom(extenderAssembly).GetType("SHCDESE.API.GamePlayerManagerAPI", true)!
            .GetField("MAX_PLAYERS", BindingFlags.Public | BindingFlags.Static);
        if (playerLimit == null || !playerLimit.IsLiteral || playerLimit.FieldType != typeof(int))
            throw new Exception("Installed Extender must expose its public constant player bound.");
        string fixture = """
using System;
using SHCDESE.API;
namespace APIShared.Networking {
 // Public authenticated-message boundary only: native authentication is tested by APIShared.
 public sealed class AuthenticatedChoreMessage<T> where T:class {
  public T Packet {get;set;} public int SenderPlayerId {get;set;}
 }
}
namespace Shared {
 public static class DebugLogHelper { public static bool DiagnosticsEnabled; }
 public static class UnityMainThreadDispatch {
  public static int Requests; public static bool TryEnqueue(Action action) { Requests++;return true; }
 }
}
public sealed class AssassinClimbStatePacket {
 public int ProtocolVersion,PlayerId,OperationId; public bool AllowClimbing;
}
public sealed class ClimbAuthorityFixture {
 const int ChoreProtocolVersion=1;
 readonly bool[] climbingAllowed=new bool[GamePlayerManagerAPI.MAX_PLAYERS+1]; readonly int[] lastOperationIds=new int[GamePlayerManagerAPI.MAX_PLAYERS+1];
 void RefreshButtonVisibility() {} void LogDebug(string text) {}
 METHODS
 static void Require(bool condition,string message) {if(!condition)throw new Exception(message);}
 public static void Run() {
  var r=new ClimbAuthorityFixture(); Array.Fill(r.climbingAllowed,true);
  var packet=new AssassinClimbStatePacket {ProtocolVersion=1,PlayerId=2,OperationId=int.MaxValue,AllowClimbing=false};
  var forged=new APIShared.Networking.AuthenticatedChoreMessage<AssassinClimbStatePacket> {Packet=packet,SenderPlayerId=1};
  Require(!IsAuthorizedClimbMessage(forged),"foreign issuer validator");
  r.OnPacketReceived(forged);
  Require(r.climbingAllowed[2]&&r.lastOperationIds[2]==0&&Shared.UnityMainThreadDispatch.Requests==0,"foreign issuer must not mutate/dedup/UI");
  packet.OperationId=1; forged.SenderPlayerId=2;
  Require(IsAuthorizedClimbMessage(forged),"own issuer validator"); r.OnPacketReceived(forged);
  Require(!r.climbingAllowed[2]&&r.lastOperationIds[2]==1,"own issuer applies");
  packet.AllowClimbing=true; r.OnPacketReceived(forged);
  Require(!r.climbingAllowed[2],"duplicate ignored"); packet.OperationId=2;r.OnPacketReceived(forged);
  Require(r.climbingAllowed[2]&&r.lastOperationIds[2]==2,"later own operation applies");
  foreach(int id in new[]{0,GamePlayerManagerAPI.MAX_PLAYERS+1}) {packet.PlayerId=id;Require(!IsValidClimbPacket(packet),"invalid player");r.OnPacketReceived(forged);}
  packet.PlayerId=2;packet.OperationId=0;Require(!IsValidClimbPacket(packet),"invalid operation");r.OnPacketReceived(forged);
  packet.OperationId=3;packet.ProtocolVersion=2;Require(!IsValidClimbPacket(packet),"invalid protocol");r.OnPacketReceived(forged);
  Require(!IsValidClimbPacket(null)&&!IsAuthorizedClimbMessage(null),"nulls");
  Require(r.lastOperationIds[2]==2,"invalid inputs never advance dedup state");
 }
}
""".Replace("METHODS", methods);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(extenderAssembly));
        var compilation = CSharpCompilation.Create("ActualClimbAuthority", new[] { CSharpSyntaxTree.ParseText(fixture) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics));
        Assembly.Load(output.ToArray()).GetType("ClimbAuthorityFixture")!.GetMethod("Run")!.Invoke(null,null);
        Console.WriteLine("PASS: actual Assassin climb receive methods reject foreign issuer/invalid payload, preserve own updates and dedup.");
    }
}
