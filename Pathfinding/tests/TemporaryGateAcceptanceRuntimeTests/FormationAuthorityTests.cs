using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Execute production authorization, schema validation, receiver and dispatch together.
// Native authentication itself belongs to APIShared's independent contract tests.
internal static class FormationAuthorityTests
{
    internal static void Run(string root)
    {
        SyntaxNode Parse(string path) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path))).GetRoot();
        var network = Parse("Pathfinding/src/Formations/FormationRuntime.NetworkCommands.cs");
        var names = new[] { "IsAuthorizedFormationMessage", "OnPacketReceived", "TryDispatch", "ValidatePacket" };
        string methods = string.Join("\n", names.Select(name => network.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == name).ToFullString()));
        var state = Parse("Pathfinding/src/Formations/FormationRuntime.State.cs");
        var constants = new[] { "ProtocolVersion", "MapWidth", "MaximumUnitCount", "MaximumTribeCount" };
        string fields = string.Join("\n", state.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(f => f.Declaration.Variables.Any(v => constants.Contains(v.Identifier.ValueText))));
        var packet = Parse("Pathfinding/src/Formations/FormationOrderPacket.cs").DescendantNodes()
            .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "FormationOrderPacket");
        packet = packet.WithAttributeLists(default).WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(
            packet.Members.OfType<FieldDeclarationSyntax>().Select(f => f.WithAttributeLists(default))));
        string fixture = Fixture.Replace("/* METHODS */", methods).Replace("/* CONSTANTS */", fields)
            .Replace("/* PACKET */", packet.ToFullString());
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("FormationAuthorityActualMethods", new[] {
            CSharpSyntaxTree.ParseText(fixture),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Pathfinding/src/Formations/FormationModel.cs")))
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
            optimizationLevel: OptimizationLevel.Release));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join(Environment.NewLine,
            result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(stream.ToArray()).GetType("Pathfinding.UnitCommands.AuthorityFixture")!.GetMethod("Run")!.Invoke(null, null);
        Console.WriteLine("PASS: actual formation authorization/schema/receiver and synchronous dispatch; foreign/dead/unknown tribes rejected, no MP local apply.");
    }

    private const string Fixture = """
using System;
namespace APIShared.Networking {
 public sealed class AuthenticatedChoreMessage<T> { public T Packet; public int SenderPlayerId; }
 // Deliberately no Send or IsAvailable: the production dispatch can run on the Sim thread.
 public sealed class Channel { public short PacketId=71; }
 public static class ChoreTransport {
  public static int Calls; public static object Last; public static short Id; public static bool Ready,Accept=true,Throw;
  public static bool TrySend<T>(T packet,short id,bool ready,out int bytes,out string rejection) {
   Calls++;Last=packet;Id=id;Ready=ready;bytes=18;rejection=Accept?null:"transport rejected";
   if(Throw)throw new InvalidOperationException("transport exception");return Accept;
  }
 }
}
namespace SHCDESE.GameGlobals {
 public sealed class GameGlobalsManager { public static readonly GameGlobalsManager Instance=new GameGlobalsManager();public long ChoreManagerVA=1; }
}
namespace Pathfinding.UnitCommands {
 /* PACKET */
 internal enum AliveState { IsAlive=1,Dead=2 }
 internal enum TribeMoveType { DefaultInSync=2,Fast=3 }
 internal struct GameTribe { public AliveState r_AliveState;public int r_PlayerIdOwner; }
 internal static class GameNetworkAPI { public static bool Multiplayer;public static bool IsMultiplayerGame()=>Multiplayer; }
 internal unsafe sealed class GameTribeManagerAPI {
  public static readonly GameTribeManagerAPI Instance=new GameTribeManagerAPI();
  public GameTribe* Tribe;public int Lookups;
  public bool TryGetTribeById(int id,out GameTribe* tribe) { Lookups++;tribe=id==17?Tribe:null;return tribe!=null; }
 }
 public unsafe sealed class AuthorityFixture {
  /* CONSTANTS */
  private enum DispatchDisposition { Rejected,Accepted }
  private APIShared.Networking.Channel packetChannel=new APIShared.Networking.Channel();
  private int applied,errors;private bool applyResult=true,throwApply;private string source;
  private bool ApplyPacket(FormationOrderPacket packet,string origin) { applied++;source=origin;if(throwApply)throw new Exception("apply");return applyResult; }
  private void LogErrorNoThrow(string message) { errors++; }
  /* METHODS */
  private static void Check(bool condition,string reason) { if(!condition)throw new Exception(reason); }
  private static FormationOrderPacket Valid()=>new FormationOrderPacket { ProtocolVersion=ProtocolVersion,OperationId=1,
   TribeId=17,TargetX=42,TargetY=24,Density=1,Formation=(byte)FormationKind.Block,UnitCount=3,
   SelectionHash=123,IsNewOrder=1,MoveType=(int)TribeMoveType.DefaultInSync };
  public static void Run() {
   var f=new AuthorityFixture();var tribe=new GameTribe{r_AliveState=AliveState.IsAlive,r_PlayerIdOwner=1};
   GameTribeManagerAPI.Instance.Tribe=&tribe;
   var m=new APIShared.Networking.AuthenticatedChoreMessage<FormationOrderPacket>{Packet=Valid(),SenderPlayerId=2};
   Check(!IsAuthorizedFormationMessage(m),"foreign owner admitted");f.OnPacketReceived(m);Check(f.applied==0,"foreign order applied");
   m.SenderPlayerId=1;Check(IsAuthorizedFormationMessage(m),"own live tribe rejected");
   f.OnPacketReceived(m);Check(f.applied==1&&f.source=="multiplayer-chore","own order missing");
   tribe.r_AliveState=AliveState.Dead;f.OnPacketReceived(m);Check(f.applied==1,"dead tribe applied");tribe.r_AliveState=AliveState.IsAlive;
   m.Packet.TribeId=18;f.OnPacketReceived(m);Check(f.applied==1,"unknown tribe applied");
   foreach(Action<FormationOrderPacket> invalidate in new Action<FormationOrderPacket>[] {
    p=>p.ProtocolVersion++,p=>p.OperationId=0,p=>p.TribeId=0,p=>p.TribeId=MaximumTribeCount,
    p=>p.TargetX=-1,p=>p.TargetY=MapWidth,p=>p.Formation=255,p=>p.Density=0,p=>p.Density=5,
    p=>p.PlacementMode=255,p=>p.DirectionSector=8,p=>p.UnitCount=0,p=>p.UnitCount=MaximumUnitCount+1,
    p=>p.SelectionHash=0,p=>p.IsNewOrder=2,p=>p.MoveType=-1,p=>p.AspectWidth=1 }) {
     m.Packet=Valid();invalidate(m.Packet);int lookups=GameTribeManagerAPI.Instance.Lookups;
     f.OnPacketReceived(m);Check(f.applied==1&&GameTribeManagerAPI.Instance.Lookups==lookups,"schema failure reached tribe/apply");
   }
   m.Packet=null;f.OnPacketReceived(m);f.OnPacketReceived(null);Check(f.applied==1,"null delivery applied");
   m.Packet=Valid();f.applyResult=false;f.OnPacketReceived(m);Check(f.errors==1,"apply rejection not reported");
   f.throwApply=true;f.OnPacketReceived(m);Check(f.errors==2,"apply exception escaped");f.throwApply=false;f.applyResult=true;
   GameNetworkAPI.Multiplayer=true;int before=f.applied;var packet=Valid();
   Check(f.TryDispatch(packet,out var rejection)==DispatchDisposition.Accepted&&rejection==null,"MP send rejected");
   Check(f.applied==before&&APIShared.Networking.ChoreTransport.Calls==1&&ReferenceEquals(APIShared.Networking.ChoreTransport.Last,packet)
    &&APIShared.Networking.ChoreTransport.Id==71&&APIShared.Networking.ChoreTransport.Ready,"MP send bypassed synchronized transport");
   APIShared.Networking.ChoreTransport.Accept=false;Check(f.TryDispatch(packet,out rejection)==DispatchDisposition.Rejected&&rejection=="transport rejected","send rejection lost");
   APIShared.Networking.ChoreTransport.Throw=true;Check(f.TryDispatch(packet,out rejection)==DispatchDisposition.Rejected&&rejection=="transport exception","send exception escaped");
   APIShared.Networking.ChoreTransport.Throw=false;int sends=APIShared.Networking.ChoreTransport.Calls;
   f.packetChannel=null;Check(f.TryDispatch(packet,out rejection)==DispatchDisposition.Rejected&&rejection=="packet hook is unavailable","missing channel accepted");
   f.packetChannel=new APIShared.Networking.Channel();SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA=0;
   Check(f.TryDispatch(packet,out rejection)==DispatchDisposition.Rejected&&rejection=="Chore manager is unavailable","missing Chore manager accepted");
   Check(APIShared.Networking.ChoreTransport.Calls==sends&&f.applied==before,"unavailable MP sent/applied");
   GameNetworkAPI.Multiplayer=false;Check(f.TryDispatch(packet,out rejection)==DispatchDisposition.Accepted&&f.source=="singleplayer"&&f.applied==before+1,"SP apply lost");
   f.applyResult=false;Check(f.TryDispatch(packet,out rejection)==DispatchDisposition.Rejected&&rejection!=null,"SP rejection lost");
   GameTribeManagerAPI.Instance.Tribe=null;
  }
 }
}
""";
}
