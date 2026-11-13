using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class OutsideBuilderDiagnosticsTests
{
    internal static void Run(string root)
    {
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "Pathfinding/src/Engine/AnchorRouting.cs"))).GetRoot();
        var method = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "CallVanillaBuilderTimed");
        var bypass = method.DescendantNodes().OfType<IfStatementSyntax>()
            .Single(i => i.Condition.ToString() == "!Shared.DebugLogHelper.DiagnosticsEnabled");
        string original = method.RemoveNode(bypass, SyntaxRemoveOptions.KeepNoTrivia)!.ToFullString()
            .Replace("CallVanillaBuilderTimed(", "Original(");
        var context = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "Pathfinding/src/UnitCommands/Movement/UnitMovementContext.cs"))).GetRoot();
        string cleanup = string.Join("\n", new[] { "GetCurrentUnitMoveFrame", "AbandonUnitMoveFrame" }
            .Select(name => context.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Single(m => m.Identifier.ValueText == name).ToFullString()));
        string fixture = Fixture.Replace("/* METHODS */", method.ToFullString() + original + cleanup);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualOutsideBuilderDiagnostics",
            new[] { CSharpSyntaxTree.ParseText(fixture) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
                optimizationLevel: OptimizationLevel.Release));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics));
        Assembly.Load(output.ToArray()).GetType("OutsideFixture")!.GetMethod("Run")!.Invoke(null, null);
    }

    private const string Fixture = """
using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace Shared { public static class DebugLogHelper { public static bool DiagnosticsEnabled; } }
namespace APIShared { public static class EnemyGatePathPolicyBridge { public static object Current; } }
public sealed class PlanScope { public bool AttackMovementQualified,MoatWorkMovement,ExactRouteEndpoints; }
public unsafe sealed class GroundFailureProofs {
 public struct BuildState {}
 public bool Answer; public int Observed;
 public bool TryAnswer(byte* p,int c,int f,bool verify,out int known,out bool predicted,out BuildState before)
 { known=19;predicted=false;before=default;return Answer; }
 public void Observe(byte* p,int c,int f,int result,bool predicted,BuildState before,object policy) {Observed++;}
}
public unsafe sealed class OutsideFixture {
 private OutsideFixture runtime=>this;
 private int anchorDispatchTask,anchorGroundTask,anchorDecline=2,anchorUnreachableUnit;
 private bool anchorAvailable=true,GroundFailureVerify;
 private long anchorKnownFailureTicks,anchorVanillaCalls,anchorVanillaTicks,anchorVanillaFails,anchorVanillaFailTicks,
  anchorVanillaAfterUnreachable,anchorVanillaAfterUnreachableTicks,anchorVanillaFoundAfterUnreachable;
 private readonly Dictionary<string,long[]> anchorOutsideBuilds=new();
 private readonly Dictionary<int,long> anchorVanillaWhy=new();
 private readonly GroundFailureProofs groundFailureProofs=new();
 private IntPtr nativePathManager;
 private int Calls,Value=7,Tick,Invalidate; private bool Publish;private Exception Failure;
 private int mapEpoch;
 private MoveCommandScope activeMoveCommand;
 private UnitMoveFrame unitMoveFrame;
 private Exception PlacementFailure;
 internal sealed class MoveCommandScope { public int UnitMoveAbandoned; }
 internal sealed class FrameArgs { public bool SkipOriginalFunction; }
 internal sealed class PlacementUnit { public int Finishes; public bool Success; }
 internal sealed class UnitMoveFrame {
  public FrameArgs Args=new();public UnitMoveFrame Parent;public int MapEpoch,Tick;
  public MoveCommandScope Command;public PlacementUnit Placement=new();
 }
 private int CaptureCurrentGameTick()=>Tick;
 private void FinishPlacement(PlacementUnit placement,bool success) {
  placement.Finishes++;placement.Success=success;if(PlacementFailure!=null)throw PlacementFailure;
 }
 private UnitMoveFrame SetLiveFrame() {
  activeMoveCommand=new MoveCommandScope();
  return unitMoveFrame=new UnitMoveFrame {MapEpoch=mapEpoch,Tick=Tick,Command=activeMoveCommand};
 }
 private IntPtr SeenManager;private int SeenClass,SeenProfile;private bool SeenReconstruction;
 private bool TryPublishAnchorSingle(IntPtr p,PlanScope plan,bool reconstruction,int profile,out int single) {
  GetCurrentUnitMoveFrame();single=23;return Publish;
 }
 private int CallVanillaBuilder(IntPtr p,int c,int f,bool reconstruction)=>CallVanillaBuilderMoatProven(p,c,f,reconstruction);
 private int CallVanillaBuilderMoatProven(IntPtr p,int c,int f,bool reconstruction) {
  Calls++;SeenManager=p;SeenClass=c;SeenProfile=f;SeenReconstruction=reconstruction;
  if(Invalidate==1)Tick++;
  if(Invalidate==2)activeMoveCommand=new MoveCommandScope();
  if(Invalidate==3)mapEpoch++;
  if(Invalidate==4)unitMoveFrame.Args.SkipOriginalFunction=true;
  if(Failure!=null)throw Failure;return Value;
 }
 private void LogAnchorUnreachableFound(IntPtr p,int unit,int result) {}
 /* METHODS */
 static void Require(bool condition,string message) {if(!condition)throw new Exception(message);}
 public static void Run() {
  byte* memory=stackalloc byte[256];new Span<byte>(memory,256).Clear();
  var r=new OutsideFixture {nativePathManager=(IntPtr)memory};
  foreach(bool diagnostic in new[]{false,true})foreach(bool reconstruction in new[]{false,true})
   foreach(int value in new[]{-1,0,7})foreach(bool publish in new[]{false,true})foreach(bool available in new[]{false,true}) {
    Shared.DebugLogHelper.DiagnosticsEnabled=diagnostic;r.Value=value;r.Publish=publish;r.anchorAvailable=available;
    r.Calls=0;int before=r.Original((IntPtr)memory,3,4,reconstruction,0);int calls=r.Calls;
    r.Calls=0;int after=r.CallVanillaBuilderTimed((IntPtr)memory,3,4,reconstruction,0);
    Require(before==after&&r.Calls==calls,"return/call count parity");
    if(calls!=0)Require(r.SeenManager==(IntPtr)memory&&r.SeenClass==3&&r.SeenProfile==4&&r.SeenReconstruction==reconstruction,"builder argument parity");
   }
  r.Publish=false;r.anchorAvailable=true;r.Failure=new InvalidOperationException("same exception");
  foreach(bool diagnostic in new[]{false,true})foreach(bool original in new[]{false,true}) {
   Shared.DebugLogHelper.DiagnosticsEnabled=diagnostic;r.Calls=0;
   try {if(original)r.Original((IntPtr)memory,3,4,false,0);else r.CallVanillaBuilderTimed((IntPtr)memory,3,4,false,0);throw new Exception("lost exception");}
   catch(InvalidOperationException error) {Require(ReferenceEquals(error,r.Failure)&&r.Calls==1,"exception/side-effect parity");}
  }
  r.Failure=null;r.Value=7;
  // These execute the production context getter and abandonment, not a pure stand-in.
  foreach(bool diagnostic in new[]{false,true})foreach(bool original in new[]{false,true})
   foreach(int invalidate in new[]{1,2,3,4})foreach(bool builderThrows in new[]{false,true}) {
    Shared.DebugLogHelper.DiagnosticsEnabled=diagnostic;r.Invalidate=invalidate;r.Calls=0;
    var frame=r.SetLiveFrame();r.Failure=builderThrows?new InvalidOperationException("builder failure"):null;
    try {
     int value=original?r.Original((IntPtr)memory,3,4,false,0):r.CallVanillaBuilderTimed((IntPtr)memory,3,4,false,0);
     Require(!builderThrows&&value==7&&r.Calls==1,"cleanup result and original count");
     Require(r.unitMoveFrame==null&&frame.Placement.Finishes==1&&!frame.Placement.Success&&frame.Command.UnitMoveAbandoned==1,
      "post-builder stale context must abandon placement and pop frame");
    }
    catch(InvalidOperationException error) {
     Require(builderThrows&&ReferenceEquals(error,r.Failure)&&r.Calls==1,"same builder exception");
     Require(ReferenceEquals(r.unitMoveFrame,frame)&&frame.Placement.Finishes==0&&frame.Command.UnitMoveAbandoned==0,
      "throwing builder must not run later cleanup");
    }
    r.Failure=null;r.unitMoveFrame=null;
   }
  foreach(bool diagnostic in new[]{false,true})foreach(bool original in new[]{false,true}) {
   Shared.DebugLogHelper.DiagnosticsEnabled=diagnostic;r.Invalidate=1;r.Calls=0;
   var frame=r.SetLiveFrame();r.PlacementFailure=new InvalidOperationException("placement failure");
   try {
    if(original)r.Original((IntPtr)memory,3,4,false,0);else r.CallVanillaBuilderTimed((IntPtr)memory,3,4,false,0);
    throw new Exception("lost placement exception");
   }
   catch(InvalidOperationException error) {
    Require(ReferenceEquals(error,r.PlacementFailure)&&r.Calls==1&&frame.Placement.Finishes==1&&
     frame.Command.UnitMoveAbandoned==0&&ReferenceEquals(r.unitMoveFrame,frame),"cleanup exception ordering parity");
   }
   r.PlacementFailure=null;r.unitMoveFrame=null;
  }
  r.Invalidate=0;r.SetLiveFrame();r.anchorDispatchTask=1;
  foreach(bool diagnostic in new[]{false,true})foreach(bool proof in new[]{false,true}) {
   Shared.DebugLogHelper.DiagnosticsEnabled=diagnostic;r.groundFailureProofs.Answer=proof;
   r.Calls=0;int old=r.Original((IntPtr)memory,3,4,false,0);int calls=r.Calls,observed=r.groundFailureProofs.Observed;
   r.Calls=0;int current=r.CallVanillaBuilderTimed((IntPtr)memory,3,4,false,0);
   Require(old==current&&calls==r.Calls&&r.groundFailureProofs.Observed-observed==(proof?0:1),"active-group proof path unchanged");
  }
  r.anchorDispatchTask=0;Shared.DebugLogHelper.DiagnosticsEnabled=false;
  for(int i=0;i<1000;i++){r.Original((IntPtr)memory,3,4,false,0);r.CallVanillaBuilderTimed((IntPtr)memory,3,4,false,0);}
  const int count=100000;long beforeBytes=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
  int sum=0;for(int i=0;i<count;i++)sum+=r.Original((IntPtr)memory,3,4,false,0);
  long oldTicks=Stopwatch.GetTimestamp()-start,oldBytes=GC.GetAllocatedBytesForCurrentThread()-beforeBytes;
  beforeBytes=GC.GetAllocatedBytesForCurrentThread();start=Stopwatch.GetTimestamp();
  for(int i=0;i<count;i++)sum+=r.CallVanillaBuilderTimed((IntPtr)memory,3,4,false,0);
  long newTicks=Stopwatch.GetTimestamp()-start,newBytes=GC.GetAllocatedBytesForCurrentThread()-beforeBytes;
  Require(sum==count*14&&oldBytes>0&&newBytes==0,"warmed disabled diagnostics removes category allocations without result changes");
  Console.WriteLine($"PASS actual outside-builder parity; warmed {count} calls oldBytes={oldBytes} newBytes={newBytes} oldMs={oldTicks*1000.0/Stopwatch.Frequency:F2} newMs={newTicks*1000.0/Stopwatch.Frequency:F2} (isolated CLR fixture, no game FPS claim).");
 }
}
""";
}
