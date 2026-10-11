using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class FormationMovementEligibilityTests
{
    internal static void Validate(string root)
    {
        // Compile the actual command records, Pre/Post handling and fallback engine.
        // Only external publishers/game memory are simulated; permission is never
        // implemented by a duplicate test predicate or an invented type blacklist.
        var syntax = CSharpSyntaxTree.ParseText(FeatureSourceModel.Read(root,
            "FormationRuntime", "BugfixesAndQoL/src/Formations")).GetRoot();
        string[] methods = { "OnUnitMoveHere", "PruneUnitAssignmentFrames",
            "ClearUnitAssignmentFrames", "TryGetMatchingUnit", "FinishUnitAssignmentFrame",
            "RunUnitFallbacks", "LogFormationOrderResult" };
        string[] types = { "ActiveFormationCommand", "UnitAssignmentFrame", "NativeDestination",
            "UnitFallbackAttempt", "UnitFallbackRequest", "UnitFallbackSummary" };
        string members = string.Join("\n", syntax.DescendantNodes()
            .Where(n => n is MethodDeclarationSyntax m && m.ExplicitInterfaceSpecifier == null && methods.Contains(m.Identifier.Text) ||
                n is BaseTypeDeclarationSyntax t && types.Contains(t.Identifier.Text))
            .Select(n => n.ToFullString()));
        string fixture = """
using System;using System.Linq;using System.Collections.Generic;
using SHCDESE.Interop;using SHCDESE.API;using SHCDESE.EventAPI;using SHCDESE.EventAPI.Units;
namespace SHCDESE.EventAPI {public enum EventHookPhase {Pre,Post}}
namespace SHCDESE.EventAPI.Tribes {public class TribeIssueOrderMoveHereEventArgs {}}
namespace SHCDESE.EventAPI.Units {
    public class UnitMoveHereEventArgs {
        public EventHookPhase Phase;public int UnitId,TileX,TileY,Unknown;
        public bool SkipOriginalFunction;public long ReturnValue;
    }
}
namespace SHCDESE.Interop {
    public struct GameUnit {
        public uint r_GlobalId;public int r_TribeId;public bool Alive;
        public ushort r_TargetTilePositionX,r_TargetTilePositionY,r_AttackMoveToTargetTileX,r_AttackMoveToTargetTileY;
    }
}
namespace APIShared {
    public unsafe static class UnitAccess {
        public static GameUnit* Units;
        public static bool TryGetById(int id,out GameUnit* unit,out int index) {
            index=id-1;unit=id>=1&&id<=8?Units+index:null;return unit!=null;
        }
        public static bool IsReallyAlive(GameUnit* unit)=>unit->Alive;
    }
}
namespace SHCDESE.API {
    public class GameUnitManagerAPI {
        public static GameUnitManagerAPI Instance=new GameUnitManagerAPI();
        public Action<int,int,int,int> Move;
        public void MoveToTile(int id,int x,int y,int unknown)=>Move(id,x,y,unknown);
    }
}
namespace BugfixesAndQoL.UnitCommands {
    using SHCDESE.EventAPI.Tribes;
    public unsafe class EligibilityFixture {
        private readonly object stateSync=new object();private bool Enabled=true;
        private ActiveFormationCommand activeCommand;private UnitAssignmentFrame unitAssignmentFrame;
        private UnitFallbackAttempt unitFallbackAttempt;
        private readonly List<string> Logs=new List<string>();
        private void LogDebugNoThrow(string text)=>Logs.Add(text);
        private void LogWarningNoThrow(string text)=>Logs.Add(text);
        private void DisableAfterNativeFailure(string contract,Exception e)=>throw new Exception(contract,e);
        private class Packet {public int TribeId=415,TargetX=562,TargetY=360,IsNewOrder=1,OperationId=12;}
        private class PendingFormationCommand {
            public Packet Packet=new Packet();public string Source="fixture";
            public bool Matches(TribeIssueOrderMoveHereEventArgs args)=>true;
        }
        __MEMBERS__
        private static void Check(bool value,string message) {if(!value)throw new Exception(message);}
        private ActiveFormationCommand CreateCommand(params int[] types) {
            var units=types.Select((type,index)=>new FormationUnit(index+1,(uint)(index+101),type,FormationRole.Neutral)).ToArray();
            var destinations=units.Select(u=>new NativeDestination(0,570+u.UnitId,361,FormationRole.Neutral)).ToArray();
            return activeCommand=new ActiveFormationCommand(new PendingFormationCommand(),FormationKind.Line,2,units,destinations);
        }
        private void Complete(int id,long result,bool veto=false,bool missingPost=false,bool changedArgs=false,bool targetMatches=true) {
            var pre=new UnitMoveHereEventArgs {Phase=EventHookPhase.Pre,UnitId=id,TileX=562,TileY=360,Unknown=7};
            OnUnitMoveHere(pre);
            Check(pre.TileX==570+id,"Formation target was not assigned to permitted member");
            if(veto)pre.SkipOriginalFunction=true;
            if(changedArgs)pre.Unknown=8;
            if(!veto&&!missingPost) {
                var unit=APIShared.UnitAccess.Units+id-1;
                if(result>0) {unit->r_TargetTilePositionX=(ushort)(targetMatches?pre.TileX:599);unit->r_TargetTilePositionY=(ushort)pre.TileY;}
                OnUnitMoveHere(new UnitMoveHereEventArgs {Phase=EventHookPhase.Post,UnitId=id,TileX=562,TileY=360,Unknown=7,ReturnValue=result});
            }
        }
        private UnitFallbackSummary Finish(ActiveFormationCommand command) {
            ClearUnitAssignmentFrames(command);activeCommand=null;
            return RunUnitFallbacks(command);
        }
        public static void Run() {
            GameUnit* memory=stackalloc GameUnit[8];APIShared.UnitAccess.Units=memory;
            for(int i=0;i<8;i++)memory[i]=new GameUnit {Alive=true,r_GlobalId=(uint)(i+101),r_TribeId=415};
            var calls=new List<(int Id,int Unknown)>();EligibilityFixture current=null;
            GameUnitManagerAPI.Instance.Move=(id,x,y,unknown)=>{
                calls.Add((id,unknown));memory[id-1].r_TargetTilePositionX=(ushort)x;memory[id-1].r_TargetTilePositionY=(ushort)y;
                current.OnUnitMoveHere(new UnitMoveHereEventArgs {Phase=EventHookPhase.Post,UnitId=id,TileX=x,TileY=y,Unknown=unknown,ReturnValue=1});
            };
            // Standard/Assassin selectors and the native moat common path all end
            // at these events. Native-excluded members receive no terminal event.
            foreach(string path in new[]{"standard","assassin","common"}) {
                current=new EligibilityFixture();calls.Clear();var command=current.CreateCommand(0x3d,0x28,0x29,22,23,24);
                if(path=="common")command.CommonPathEntered=true;else command.RecordSelectorAssignment(path=="assassin");
                current.Complete(4,1); // Allowed soldier succeeds, three stationary engines are skipped.
                current.Complete(5,0); // Allowed soldier fails only at its formation slot.
                // Slot 6 models any other native state exclusion: no call, no inferred permission.
                var summary=current.Finish(command);
                Check(calls.SequenceEqual(new[]{(5,7)}),path+": skipped/successful member moved or parameter lost");
                Check(command.SkippedTerminalCount==4&&command.ObservedTerminalCount==2,"Native skipped accounting");
                Check(summary.Succeeded==1&&summary.TechnicalErrors==0,"Allowed fallback failed");
                current.LogFormationOrderResult(command,1,summary);
                Check(current.Logs.Last().Contains("vanillaSkipped=4"),"Skipped members hidden in diagnostics");
            }
            foreach(string reason in new[]{"veto","missing-post","changed-args","success-different-target","unexpected-return"}) {
                current=new EligibilityFixture();calls.Clear();var command=current.CreateCommand(22);
                current.Complete(1,reason=="success-different-target"?1:reason=="unexpected-return"?-1:0,
                    veto:reason=="veto",missingPost:reason=="missing-post",changedArgs:reason=="changed-args",targetMatches:false);
                current.Finish(command);Check(calls.Count==0,"Unauthorized retry: "+reason);
            }
            foreach(string reason in new[]{"global-id","tribe","dead"}) {
                current=new EligibilityFixture();calls.Clear();var command=current.CreateCommand(22);current.Complete(1,0);
                if(reason=="global-id")memory[0].r_GlobalId++;if(reason=="tribe")memory[0].r_TribeId++;if(reason=="dead")memory[0].Alive=false;
                current.Finish(command);Check(calls.Count==0,"Identity revalidation failed: "+reason);
                memory[0]=new GameUnit {Alive=true,r_GlobalId=101,r_TribeId=415};
            }
            current=new EligibilityFixture();calls.Clear();var repeated=current.CreateCommand(22);
            current.Complete(1,0);current.Complete(1,0);current.Finish(repeated);
            Check(calls.SequenceEqual(new[]{(1,7)}),"Repeated failed native calls produced multiple retries");
            current=new EligibilityFixture();calls.Clear();var nested=current.CreateCommand(22,23);
            current.Complete(1,0,missingPost:true);current.Complete(2,0);current.Finish(nested);
            Check(calls.SequenceEqual(new[]{(2,7)}),"Unobserved parent borrowed child feedback");
            current=new EligibilityFixture();calls.Clear();var abandoned=current.CreateCommand(22);
            current.Complete(1,0,missingPost:true);current.Complete(1,0);current.Finish(abandoned);
            Check(calls.Count==0,"Ambiguous same-unit parent acquired fallback permission");
            current=new EligibilityFixture();calls.Clear();var empty=current.CreateCommand(0x3d,0x28,0x29);
            current.Finish(empty);Check(calls.Count==0&&empty.SkippedTerminalCount==3,"Stationary-only group moved");
            APIShared.UnitAccess.Units=null;
        }
    }
}
""";
        fixture = fixture.Replace("__MEMBERS__", members);
        var trees = new[] { "FormationModel.cs", "FormationPresentation.cs" }
            .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
                "BugfixesAndQoL/src/Formations", p))))
            .Append(CSharpSyntaxTree.ParseText(fixture));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ProductionFormationMovementEligibility", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream();var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("BugfixesAndQoL.UnitCommands.EligibilityFixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        Console.WriteLine("PASS: production formation command/event/fallback flow preserves native skipped members, vetoes, missing feedback, successes and current identity; only observed zero gets one retry.");
    }
}
