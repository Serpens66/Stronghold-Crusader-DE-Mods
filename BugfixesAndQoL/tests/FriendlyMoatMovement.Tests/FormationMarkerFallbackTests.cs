using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class FormationMarkerFallbackTests
{
    internal static void Validate(string root)
    {
        static string Members(string source, string[] methods, string[] types) => string.Join("\n",
            CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes()
                .Where(n => n is MethodDeclarationSyntax m && methods.Contains(m.Identifier.Text) ||
                    n is BaseTypeDeclarationSyntax t && types.Contains(t.Identifier.Text))
                .Select(n => n.ToFullString()));
        string fallback = Members(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/Formations/FormationRuntime.OrderEvents.cs")),
            new[] { "RunUnitFallbacks", "LogFormationOrderResult" },
            new[] { "UnitFallbackAttempt", "UnitFallbackRequest", "UnitFallbackSummary" });
        string overlay = Members(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/FormationPreviewOverlay.cs")),
            new[] { "Render", "Publish", "Clear", "Refresh", "SetNativeAllowed", "SetRoleMarkersVisible", "HideAll", "HideArrow" },
            new[] { "PreviewSnapshot" });
        string fixture = """
using System;using System.Linq;using System.Collections.Generic;
using BugfixesAndQoL.UnitCommands;
namespace SHCDESE.EventAPI {public enum EventHookPhase {Pre,Post}}
namespace SHCDESE.EventAPI.Units {
    public class UnitMoveHereEventArgs {
        public SHCDESE.EventAPI.EventHookPhase Phase;
        public int UnitId,TileX,TileY,Unknown;
    }
}
namespace SHCDESE.Interop {public struct GameUnit {public ushort r_TargetTilePositionX,r_TargetTilePositionY;public int r_TribeId;}}
namespace APIShared {
    public unsafe static class UnitAccess {
        internal static SHCDESE.Interop.GameUnit* Unit;
        internal static bool Available=true;
        public static bool TryGetById(int id,out SHCDESE.Interop.GameUnit* unit,out int index) {unit=Unit;index=id-1;return Available;}
    }
}
namespace SHCDESE.API {
    public class GameUnitManagerAPI {
        public static GameUnitManagerAPI Instance=new GameUnitManagerAPI();
        internal Action<int,int,int,int> Move;
        public void MoveToTile(int unit,int x,int y,int unknown)=>Move(unit,x,y,unknown);
    }
}
namespace BugfixesAndQoL.UnitCommands {
    using SHCDESE.API;using SHCDESE.Interop;using SHCDESE.EventAPI;using SHCDESE.EventAPI.Units;
    public unsafe class FallbackFixture {
        private readonly object stateSync=new object();private UnitFallbackAttempt unitFallbackAttempt;
        private readonly List<string> Warnings=new List<string>(),Debug=new List<string>();
        private bool IdentityMatches=true;
        private void LogWarningNoThrow(string text)=>Warnings.Add(text);
        private void LogDebugNoThrow(string text)=>Debug.Add(text);
        private bool TryGetMatchingUnit(int id,uint global,out GameUnit* unit) {unit=APIShared.UnitAccess.Unit;return IdentityMatches;}
        private class Packet {internal int OperationId=15;}
        private class Pending {internal string Source="singleplayer";internal Packet Packet=new Packet();}
        private class ActiveFormationCommand {
            internal Pending Pending=new Pending();internal int TargetX=562,TargetY=360,TribeId=415;
            internal string AssignmentPath="common";internal int AssignedCount,TerminalAttemptCount,ExpectedCount;
            internal int ObservedTerminalCount,SkippedTerminalCount;
            internal UnitFallbackRequest[] Requests;internal UnitFallbackRequest[] GetFallbackRequests()=>Requests;
        }
        __FALLBACK__
        private static void Check(bool value,string message) {if(!value)throw new Exception(message);}
        private static FallbackFixture Scenario(int[] returns,int formationSucceeded=0,bool feedback=true,
            bool throws=false,bool targetMatches=true,bool identity=true,bool available=true) {
            var f=new FallbackFixture {IdentityMatches=identity};
            var unit=new GameUnit {r_TribeId=415};APIShared.UnitAccess.Unit=&unit;APIShared.UnitAccess.Available=available;
            int calls=0;
            GameUnitManagerAPI.Instance.Move=(id,x,y,u)=>{
                calls++;
                if(throws)throw new InvalidOperationException("fixture failure");
                APIShared.UnitAccess.Unit->r_TargetTilePositionX=(ushort)(targetMatches?x:x+1);
                APIShared.UnitAccess.Unit->r_TargetTilePositionY=(ushort)y;
                if(feedback)f.unitFallbackAttempt.Observe(returns[id-1]);
            };
            var command=new ActiveFormationCommand {ExpectedCount=formationSucceeded+returns.Length,
                TerminalAttemptCount=formationSucceeded+returns.Length,
                Requests=Enumerable.Range(1,returns.Length).Select(id=>new UnitFallbackRequest(id,1,0)).ToArray()};
            var summary=f.RunUnitFallbacks(command);
            f.LogFormationOrderResult(command,formationSucceeded,summary);
            Check(f.unitFallbackAttempt==null,"Fallback scope leaked");
            Check(calls==(identity&&available?returns.Length:0),"Move count changed");
            bool technical=throws||!feedback||!targetMatches||!identity||!available||returns.Any(r=>r!=0&&r!=1);
            Check((summary.TechnicalErrors>0)==technical,"Technical summary incorrect");
            Check((f.Warnings.Count>0)==technical,"Normal rejection warned, or technical error hidden");
            if(!technical) {
                Check(summary.Succeeded==returns.Count(r=>r>0)&&summary.Failed==returns.Count(r=>r==0),"Fallback counts changed");
                Check(f.Debug.Count==returns.Length+1,"Expected debug-only diagnostics missing");
            }
            if(!feedback||!available)Check(f.Warnings.Any(w=>w.Contains("missing-feedback")),"Missing return treated as native zero");
            if(feedback&&!throws&&identity&&available&&returns.Contains(0))
                Check(f.Debug.Any(d=>d.Contains("observed=True")&&d.Contains("vanilla-rejected")),"Observed zero not identified");
            return f;
        }
        public static void Run() {
            Scenario(new[]{0},68); // Original 68/69 warning case.
            Scenario(new[]{0,0,0}); // Fully immobile group.
            Scenario(new[]{1,1});Scenario(new[]{1,0},2);
            Scenario(new[]{0},feedback:false);Scenario(new[]{0},available:false);
            Scenario(new[]{0},throws:true);Scenario(new[]{1},targetMatches:false);
            Scenario(new[]{0},identity:false);Scenario(new[]{-1});Scenario(new[]{2});
            APIShared.UnitAccess.Unit=null;
        }
    }
}
namespace Noesis {
    public enum Visibility {Visible,Collapsed}
    public class Ellipse {public Visibility Visibility;public float Width,Height;public object Fill;}
    public class Line {public Visibility Visibility;}
    public class Canvas {public float ActualWidth=100,ActualHeight=100;public static void SetLeft(Ellipse e,float x){}public static void SetTop(Ellipse e,float y){}}
}
namespace UnityEngine {public class Camera {}}
namespace Shared {
    public static class UnityMainThreadDispatch {public static bool TryRunInlineOrEnqueue(Action action){action();return true;}}
    public static class DebugLogHelper {public static void LogDebug(object l,string t){}public static void LogError(object l,string t){throw new Exception(t);}}
}
namespace BugfixesAndQoL {
    using Noesis;using UnityEngine;
    public static class OverlayFixture {
        private static readonly object Sync=new object();
        private static readonly List<Ellipse> PointPool=new List<Ellipse>();
        private static readonly Line[] ArrowLines={new Line(),new Line(),new Line()};
        private static PreviewSnapshot snapshot=PreviewSnapshot.Empty;
        private static object log;private static int nextGeneration,renderedGeneration;
        private static bool renderFailureLogged,showRoleMarkers=true,nativeAllowed;
        private const float PointSize=11,ProtectedPointSize=13;
        private static Canvas host=new Canvas();private static Camera camera=new Camera();
        private static FormationDirectionIndicator LastArrow;
        private static bool TryResolveCanvas(out Canvas canvas){canvas=host;return host!=null;}
        private static Camera ResolveCamera()=>camera;
        private static void EnsureBrushes(){}private static object BrushFor(FormationRole role)=>role;
        private static void EnsurePointPool(Canvas canvas,int count){while(PointPool.Count<count)PointPool.Add(new Ellipse());}
        private static bool TryProject(Camera c,Canvas canvas,int px,int py,out float x,out float y){x=px;y=py;return true;}
        private static void RenderArrow(Camera c,Canvas canvas,FormationDirectionIndicator direction){LastArrow=direction;}
        __OVERLAY__
        private static void Check(bool value,string message){if(!value)throw new Exception(message);}
        private static int Visible=>PointPool.Count(p=>p.Visibility==Visibility.Visible);
        private static FormationPreviewPoint[] Points={new FormationPreviewPoint(10,10,FormationRole.Front),new FormationPreviewPoint(11,10,FormationRole.Protected)};
        private static FormationDirectionIndicator Direction(bool explicitDirection)=>new FormationDirectionIndicator(true,10,10,10,14,explicitDirection);
        public static void Run() {
            Clear();SetRoleMarkersVisible(true);Publish(Points,Direction(false));SetNativeAllowed(true);
            Check(Visible==0,"Short click/still hold shows roles");
            Check(LastArrow.Visible&&!LastArrow.ExplicitDirection,"Arrow publication changed");
            var gesture=new FormationGestureState(FormationKind.Line,31,0);
            gesture.UpdateDirection(1,0,2);Publish(Points,Direction(gesture.ExplicitDirection));Check(Visible==0,"Below threshold shows roles");
            gesture.UpdateDirection(2,0,2);Publish(Points,Direction(gesture.ExplicitDirection));Check(Visible==2,"Explicit direction hides roles");
            gesture.UpdateDirection(0,0,2);Publish(Points,Direction(gesture.ExplicitDirection));Check(Visible==2,"Returning to anchor hides roles");
            Publish(Points,Direction(false));Check(Visible==0,"Next short click leaves old pooled roles visible");
            Publish(Points,Direction(true));SetRoleMarkersVisible(false);Check(Visible==0,"Disabled setting shows roles");
            SetRoleMarkersVisible(true);Check(Visible==2,"Setting re-enable failed");
            SetNativeAllowed(false);Check(Visible==0,"Unconfirmed/invalid runtime shows roles");
            SetNativeAllowed(true);Check(Visible==2,"Authorization restore failed");
            Clear();Check(Visible==0&&!nativeAllowed,"Release/map/selection Clear leaves roles");
        }
    }
}
""";
        fixture = fixture.Replace("__FALLBACK__",fallback).Replace("__OVERLAY__",overlay);
        var sources=new[] { "BugfixesAndQoL/src/Formations/FormationModel.cs",
            "BugfixesAndQoL/src/Formations/FormationPresentation.cs" };
        var trees=sources.Select(p=>CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,p))))
            .Append(CSharpSyntaxTree.ParseText(fixture));
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("ProductionFormationMarkerFallback",trees,references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
        using var output=new MemoryStream();var result=compilation.Emit(output);
        if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        var assembly=Assembly.Load(output.ToArray());
        foreach(string name in new[]{"BugfixesAndQoL.UnitCommands.FallbackFixture","BugfixesAndQoL.OverlayFixture"}) {
            try {assembly.GetType(name)!.GetMethod("Run")!.Invoke(null,null);}
            catch(TargetInvocationException e){throw e.InnerException??e;}
        }
        Console.WriteLine("PASS: production fallback calls and summaries distinguish observed Vanilla refusal from missing feedback/errors; production overlay Render/Publish/Clear gates roles by explicit direction without changing arrow publication.");
    }
}
