using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class CircleProjectionTests
{
    internal static void Validate(string root)
    {
        static string Extract(string text, string[] methods, string[] types) => string.Join("\n",
            CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().Where(node =>
                node is MethodDeclarationSyntax method && methods.Contains(method.Identifier.Text) ||
                node is BaseTypeDeclarationSyntax type && types.Contains(type.Identifier.Text)).Select(node => node.ToFullString()));
        string projection = Extract(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/FormationPreviewOverlay.cs")),
            new[] { "TryProject", "RenderArrow", "SetLine", "HideArrow" }, Array.Empty<string>());
        string destinations = Extract(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/Formations/FormationRuntime.Destinations.cs")),
            new[] { "BuildManagedDestinations", "BuildDirectionIndicator", "SnapSlots" }, new[] { "NativeDestination" });
        string fixture = """
using System;using System.Linq;using System.Collections.Generic;
using BugfixesAndQoL.UnitCommands;using UnityEngine;using Noesis;
namespace Shared {internal static class DevDiag {internal static void Count(string name){}}}
namespace UnityEngine {
    public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
    public class Camera {public Vector3 WorldToScreenPoint(Vector3 world)=>new Vector3(50+world.x,50+world.y,0);}
    public static class Screen {public static int width=100,height=100;}
}
namespace Noesis {
    public enum Visibility {Visible,Collapsed}
    public class Canvas {public float ActualWidth=100,ActualHeight=100;}
    public class Line {public float X1,Y1,X2,Y2,StrokeThickness,Opacity;public object Stroke;public Visibility Visibility;}
}
public class GameMapTile {}
public class GameMap {
    public static GameMap instance=new GameMap();public int Rotation;public bool MissingTile;
    public void mapGameTileToTilemapCoord(int x,int y,out int mx,out int my) {
        mx=x;my=y;
        for(int i=0;i<Rotation;i++){int old=mx;mx=20-my;my=old;}
    }
    public GameMapTile getMapTile(int x,int y)=>!MissingTile&&x>0&&y>0&&x<20&&y<20?new GameMapTile():null;
}
public class CameraControls2D {public static CameraControls2D instance=new CameraControls2D();}
namespace SHCDESE {
    public static class CoordinateConverter {
        public static int Calls;public static bool ReturnZero;
        public static System.Numerics.Vector2 ConvertLocalTileToCameraWorld(System.Numerics.Vector2 point) {
            GameMap.instance.mapGameTileToTilemapCoord((int)point.X,(int)point.Y,out int x,out int y);
            if(GameMap.instance.getMapTile(x,y)==null)throw new Exception("Invalid tile reached warning-producing converter");
            Calls++;return ReturnZero?System.Numerics.Vector2.Zero:point;
        }
    }
}
namespace BugfixesAndQoL {
    public static class ProjectionFixture {
        private static readonly Line[] ArrowLines={new Line(),new Line(),new Line()};private static object arrowBrush=new object();
        __PROJECTION__
        public static void Run() {
            var camera=new Camera();var canvas=new Canvas();
            for(int rotation=0;rotation<4;rotation++) {
                GameMap.instance.Rotation=rotation;
                foreach(var point in new[]{(0,10),(20,10),(10,0),(10,20),(-1,10),(10,21)}) {
                    int calls=SHCDESE.CoordinateConverter.Calls;
                    if(TryProject(camera,canvas,point.Item1,point.Item2,out _,out _)||SHCDESE.CoordinateConverter.Calls!=calls)
                        throw new Exception("Invalid edge projection accepted");
                }
                SHCDESE.CoordinateConverter.ReturnZero=true;
                if(!TryProject(camera,canvas,10,10,out float x,out float y)||x!=50||y!=50)
                    throw new Exception("Valid zero world position rejected");
                SHCDESE.CoordinateConverter.ReturnZero=false;
                RenderArrow(camera,canvas,new FormationDirectionIndicator(true,10,10,10,14,true));
                if(ArrowLines.Any(line=>line.Visibility!=Visibility.Visible))throw new Exception("Valid arrow hidden");
                RenderArrow(camera,canvas,new FormationDirectionIndicator(true,10,10,10,21,true));
                if(ArrowLines.Any(line=>line.Visibility!=Visibility.Collapsed))throw new Exception("Invalid endpoint leaves stale arrow");
                RenderArrow(camera,canvas,new FormationDirectionIndicator(true,-1,10,10,14,true));
                if(ArrowLines.Any(line=>line.Visibility!=Visibility.Collapsed))throw new Exception("Invalid start leaves arrow");
            }
            GameMap.instance.MissingTile=true;
            if(TryProject(camera,canvas,10,10,out _,out _))throw new Exception("Null map slot accepted");
            GameMap.instance=null;
            if(TryProject(camera,canvas,10,10,out _,out _))throw new Exception("Missing map accepted");
            GameMap.instance=new GameMap();CameraControls2D.instance=null;
            if(TryProject(camera,canvas,10,10,out _,out _))throw new Exception("Missing controls accepted");
        }
    }
}
namespace BugfixesAndQoL.UnitCommands {
    public class DestinationFixture {
        private const int MaximumPreviewCandidates=100000;private bool Blocked;
        private bool IsAssassinOnly(FormationUnit[] units)=>false;
        private NativeDestination[] CaptureVanillaDestinations(int x,int y,int n,int d,bool a)=>throw new Exception("Circle unexpectedly became Vanilla");
        private List<FormationPoint> BuildVanillaSlotMetadata(int x,int y,NativeDestination[] slots,int direction)=>throw new Exception("Vanilla metadata used");
        private List<NativeDestination> CaptureReachableCandidates(int x,int y,int n,bool a,IReadOnlyList<FormationPoint> slots)=>
            FormationPlacementModel.EnumerateReachabilityProbes(slots)
                .Select(p=>new NativeDestination((x+p.X)*1000+y+p.Y,x+p.X,y+p.Y,FormationRole.Neutral))
                .Where(p=>!Blocked||p.X!=x).ToList();
        __DESTINATIONS__
        public static void Run() {
            var f=new DestinationFixture();
            foreach(int count in new[]{2,31,76})foreach(int density in new[]{1,2,3,4})
            foreach(RangedPlacementMode mode in Enum.GetValues<RangedPlacementMode>()) {
                var units=Enumerable.Range(1,count).Select(i=>new FormationUnit(i,(uint)i,1,(FormationRole)(i%4))).ToArray();
                var baseline=f.BuildManagedDestinations(200,200,FormationKind.Circle,density,0,1,mode,units,false,out _);
                for(int direction=0;direction<8;direction++)foreach(bool explicitDirection in new[]{false,true}) {
                    var result=f.BuildManagedDestinations(200,200,FormationKind.Circle,density,direction,1,mode,units,explicitDirection,out var arrow);
                    if(result.Length!=count||!result.Select(p=>(p.X,p.Y)).ToHashSet().SetEquals(baseline.Select(p=>(p.X,p.Y))))
                        throw new Exception("Production planning changes circle footprint");
                    if(arrow.ExplicitDirection!=explicitDirection)throw new Exception("Direction flag lost");
                    if(mode!=RangedPlacementMode.Rear&&!result.Select(p=>(p.X,p.Y)).SequenceEqual(baseline.Select(p=>(p.X,p.Y))))
                        throw new Exception("Center/Off assignment changes with direction");
                }
                f.Blocked=true;
                var blocked=f.BuildManagedDestinations(200,200,FormationKind.Circle,density,3,1,mode,units,true,out _);
                if(blocked.Any(p=>p.X==200)||blocked.Select(p=>(p.X,p.Y)).Distinct().Count()!=count)
                    throw new Exception("Blocked placements not uniquely replaced");
                f.Blocked=false;
            }
        }
    }
}
""";
        fixture = fixture.Replace("__PROJECTION__", projection).Replace("__DESTINATIONS__", destinations);
        var sources = new[] { "BugfixesAndQoL/src/Formations/FormationModel.cs", "BugfixesAndQoL/src/Formations/FormationPresentation.cs" };
        var trees = sources.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path))))
            .Append(CSharpSyntaxTree.ParseText(fixture));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("ProductionCircleProjection", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        var assembly = Assembly.Load(output.ToArray());
        foreach (string name in new[] { "BugfixesAndQoL.ProjectionFixture", "BugfixesAndQoL.UnitCommands.DestinationFixture" })
        {
            try { assembly.GetType(name)!.GetMethod("Run")!.Invoke(null, null); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }
        Console.WriteLine("PASS: production TryProject/RenderArrow reject missing edge tiles without calling converter; zero world accepted; production BuildManagedDestinations/SnapSlots preserve circle across direction/explicit flags and replace blocked slots.");
    }
}
