using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class FormationDispatchOutcomeTests
{
    internal static void Validate(string root)
    {
        var syntax=CSharpSyntaxTree.ParseText(FeatureSourceModel.Read(root,"FormationRuntime","BugfixesAndQoL/src/Formations")).GetRoot();
        string members=string.Join("\n",syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m=>m.Identifier.Text is "ApplyPacket" or "TryIssuePacketVanillaFallback").Select(m=>m.ToFullString()));
        var playerSyntax=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Native/PermanentCommandHooks.cs"))).GetRoot();
        members += playerSyntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="DispatchPlayerMove").ToFullString();
        var frameSyntax=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Runtime/ManualGroupAdmission.cs"))).GetRoot();
        members += frameSyntax.DescendantNodes().OfType<StructDeclarationSyntax>().Single(m=>m.Identifier.Text=="ManualPlayerMoveFrame").ToFullString();
        string fixture="""
using System;using System.Runtime.InteropServices;
namespace SHCDESE.Interop.Enums {internal enum TribeMoveType {DefaultInSync=0,Fast=1}}
namespace SHCDESE.API {
    internal sealed class GameTribeManagerAPI {
        internal static GameTribeManagerAPI Instance=new();internal Func<bool> Move;internal int Calls;
        internal bool IssueMoveHereCommand(int t,int x,int y,bool isPatrolPath,int bIsNewOrder,SHCDESE.Interop.Enums.TribeMoveType tribeMoveType){Calls++;return Move();}
    }
}
namespace BugfixesAndQoL.UnitCommands {
    using SHCDESE.API;using SHCDESE.Interop.Enums;
    internal sealed class FormationOrderPacket {internal int TribeId=1,TargetX=20,TargetY=20,IsNewOrder=1,MoveType,OperationId=1;}
    public unsafe sealed class OutcomeFixture {
        private readonly object stateSync=new();private bool Enabled=true;private const int MaximumTribeCount=4500,MapWidth=800;
        private PendingFormationCommand pendingCommand;private ActiveFormationCommand activeCommand,commonGroupCommand;private int dispatchDepth;
        private sealed class PendingFormationCommand {internal PendingFormationCommand(FormationOrderPacket p,string s){Packet=p;}internal FormationOrderPacket Packet;internal bool NativeDispatchObserved;}
        private sealed class ActiveFormationCommand {internal PendingFormationCommand Pending;}
        private bool Valid=true;private bool ValidatePacket(FormationOrderPacket p,out string reason){reason="fixture";return Valid;}
        private ManualPlayerMoveFrame manualPlayerMove;
        private bool manualAdmissionReady=true,TraversalEnabled;private IntPtr nativeTribeManager;private const int TribeRecordSize=0x688;
        private long playerMoveEntries;private Action<IntPtr,int,int,int,int,int> nativePlayerMove,traversalMoveProvider;
        private void ReportNativeDetourBarrier(string n,Exception e){}
        private void LogErrorNoThrow(string m){}private void LogWarningNoThrow(string m){}private void ClearUnitAssignmentFrames(object o){}
        __MEMBERS__
        public static void Run(){
            foreach(bool observed in new[]{false,true})foreach(bool result in new[]{false,true}){
                var f=new OutcomeFixture();var api=GameTribeManagerAPI.Instance;api.Calls=0;
                api.Move=()=>{if(observed)f.pendingCommand.NativeDispatchObserved=true;return result;};
                bool consumed=f.ApplyPacket(new(),"fixture");
                if(consumed!=(observed||result)||api.Calls!=1||f.dispatchDepth!=0||f.pendingCommand!=null)
                    throw new Exception("Native rejection/partial application must not replay an observed dispatch");
            }
            foreach(bool enabled in new[]{false,true}){
                var f=new OutcomeFixture{Enabled=enabled,Valid=false};var api=GameTribeManagerAPI.Instance;api.Calls=0;api.Move=()=>false;
                if(!f.ApplyPacket(new(),"fallback")||api.Calls!=1||f.dispatchDepth!=0)
                    throw new Exception("Already issued fallback cannot authorize another whole-group command");
            }
            IntPtr tribes=Marshal.AllocHGlobal(0x2000);
            try{
                foreach(bool reject in new[]{false,true}){
                    var f=new OutcomeFixture{nativeTribeManager=tribes};ushort* mode=(ushort*)((byte*)tribes+TribeRecordSize+0x56C);*mode=17;
                    f.nativePlayerMove=(m,t,x,y,p,n)=>{f.playerMoveEntries++;*mode=0;f.manualPlayerMove.GroupSeen=true;f.manualPlayerMove.Rejected=reject;};
                    f.DispatchPlayerMove(tribes,1,20,20,0,0);
                    if(*mode!=(reject?17:0)||f.manualPlayerMove.Valid)throw new Exception("Player flag restore/teardown mismatch");
                }
                var nested=new OutcomeFixture{nativeTribeManager=tribes};ushort* flag=(ushort*)((byte*)tribes+TribeRecordSize+0x56C);*flag=17;int calls=0;
                nested.nativePlayerMove=(m,t,x,y,p,n)=>{nested.playerMoveEntries++;calls++;*flag=0;
                    nested.manualPlayerMove.GroupSeen=true;
                    if(calls==1)nested.DispatchPlayerMove(tribes,1,20,20,0,0);else nested.manualPlayerMove.Rejected=true;};
                nested.DispatchPlayerMove(tribes,1,20,20,0,0);
                if(*flag!=0||calls!=2||nested.manualPlayerMove.Valid)throw new Exception("Child rejection rolled back an accepted parent player's order");
            }finally{Marshal.FreeHGlobal(tribes);}
        }
    }
}
""";
        fixture=fixture.Replace("__MEMBERS__",members);
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("ProductionFormationDispatchOutcome",new[]{CSharpSyntaxTree.ParseText(fixture)},references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
        using var output=new MemoryStream();var result=compilation.Emit(output);
        if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        try{Assembly.Load(output.ToArray()).GetType("BugfixesAndQoL.UnitCommands.OutcomeFixture")!.GetMethod("Run")!.Invoke(null,null);}
        catch(TargetInvocationException e){throw e.InnerException??e;}
        Console.WriteLine("PASS: production formation dispatch consumes observed rejection/partial application once; no whole-group replay.");
    }
}
