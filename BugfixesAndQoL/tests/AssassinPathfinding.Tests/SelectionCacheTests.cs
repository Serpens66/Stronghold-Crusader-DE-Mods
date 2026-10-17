using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    private static void TestSelectionCache(string root)
    {
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/AssassinClimbRuntime.cs"))).GetRoot();
        var runtime = syntax.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(type => type.Identifier.Text == "AssassinClimbRuntime");
        string[] methods = { "CaptureSelectionState", "IsOwnAssassin", "InvalidateRenderState" };
        string members = string.Join("\n", runtime.Members.Where(member =>
            member is MethodDeclarationSyntax method && methods.Contains(method.Identifier.Text) ||
            member is FieldDeclarationSyntax field && field.Declaration.Variables.Any(variable =>
                variable.Identifier.Text.StartsWith("lastSelection", StringComparison.Ordinal) ||
                variable.Identifier.Text.StartsWith("lastRender", StringComparison.Ordinal) ||
                variable.Identifier.Text == "renderStateKnown")).Select(member => member.ToFullString()));
        string extender = Environment.GetEnvironmentVariable("SHCDESE_EXTENDER_DIR") ??
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        var unitType = Assembly.LoadFrom(Path.Combine(extender, "SHCDESE.dll")).GetType("SHCDESE.Interop.eChimps", true);
        string enumSource = "public enum eChimps : ushort {" + string.Join(",", unitType
            .GetFields(BindingFlags.Static | BindingFlags.Public).Select(field =>
                field.Name + "=" + Convert.ToInt64(field.GetRawConstantValue()))) + "}";
        // Compile the actual selection and invalidation methods. Only the selected
        // snapshot, SDK unit storage and UnitAccess service are boundary doubles;
        // this fixture does not reimplement the production cache algorithm.
        string fixture = """
using System;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.Interop;
public sealed class HUD_Troops {}
namespace SHCDESE.Interop {
 ENUM
 public struct GameUnit { public bool Live; public eChimps r_UnitChimp; public int r_ControllableForPlayerId; public int r_UnitSelected; }
}
namespace SHCDESE.API {
 public unsafe sealed class GameUnitManagerAPI {
  public static GameUnitManagerAPI Instance = new();
  public GameUnit* Units = (GameUnit*)Marshal.AllocHGlobal(sizeof(GameUnit) * 2);
  public Span<GameUnit> GetUnitsAsSpan() => new(Units, 2);
 }
}
namespace APIShared {
 public readonly record struct Entry(int UnitId, int UnitType);
 public sealed class LocalSelectionSnapshot {
  public Entry[] Entries; public int IndexReads;
  public int Count => Entries.Length;
  public Entry this[int index] { get { IndexReads++; return Entries[index]; } }
 }
 public static class LocalSelectionAPI {
  public static bool Available=true; public static LocalSelectionSnapshot Snapshot;
  // Match LocalSelectionAPI's documented/implemented failure-out contract.
  public static bool TryCapture(int player, out LocalSelectionSnapshot selected) { selected=Available?Snapshot:null; return Available; }
 }
 public static unsafe class UnitAccess {
  public static int Reads;
  public static bool TryGetById(GameUnitManagerAPI api, int id, out GameUnit* unit, out string error) {
   Reads++; error=null; unit=id is >=1 and <=2 ? api.Units+id-1 : null; return unit!=null;
  }
  public static bool IsReallyAlive(GameUnit* unit)=>unit!=null&&unit->Live;
  public static bool IsReallyAlive(in GameUnit unit)=>unit.Live;
 }
}
namespace APIShared.GameModes { public static class GameModeHelper { public static bool Editor; public static bool IsMapEditor()=>Editor; } }
public unsafe sealed class SelectionFixture {
 MEMBERS
 static void Require(bool value,string message) { if(!value)throw new Exception(message); }
 public static void Run() {
  var runtime=new SelectionFixture(); var api=GameUnitManagerAPI.Instance;
  try {
   api.Units[0]=new GameUnit { Live=true, r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN, r_ControllableForPlayerId=1, r_UnitSelected=1 };
   api.Units[1]=default;
   APIShared.LocalSelectionAPI.Snapshot=new() { Entries=new[] { new APIShared.Entry(1,(int)eChimps.CHIMP_TYPE_ARAB_ASSASIN) } };
   bool Capture(int player=1) { runtime.CaptureSelectionState(player,out bool own);return own; }
   Require(Capture(),"Own living Assassin was rejected");
   APIShared.LocalSelectionAPI.Snapshot.IndexReads=0;
   Require(Capture()&&APIShared.LocalSelectionAPI.Snapshot.IndexReads==0,"Unchanged valid selection did not reuse the snapshot");
   api.Units[0].r_ControllableForPlayerId=2;
   Require(!Capture(),"Cached selection survived an ownership change");
   api.Units[0].r_ControllableForPlayerId=1;
   Require(Capture(),"Ownership restoration did not revalidate");
   api.Units[0].Live=false;Require(!Capture(),"Cached dead unit stayed selectable");
   api.Units[0].Live=true;Require(Capture(),"Living replacement did not revalidate");
   APIShared.LocalSelectionAPI.Available=false;Require(!Capture(),"Failed snapshot reused the previous selection");
   APIShared.LocalSelectionAPI.Available=true;Require(Capture(),"Snapshot recovery stayed stale");
   runtime.InvalidateRenderState();APIShared.LocalSelectionAPI.Snapshot.IndexReads=0;
   Require(Capture()&&APIShared.LocalSelectionAPI.Snapshot.IndexReads>0,"Invalidation did not retire the selection cache");
   int reads=APIShared.UnitAccess.Reads;
   Require(!Capture(0)&&!Capture(9)&&APIShared.UnitAccess.Reads==reads,"Invalid player reached unit storage");
   APIShared.LocalSelectionAPI.Snapshot=new() { Entries=Array.Empty<APIShared.Entry>() };
   Require(!Capture(),"Empty selection retained its previous Assassin");
   APIShared.LocalSelectionAPI.Snapshot=new() { Entries=new[] { new APIShared.Entry(0,(int)eChimps.CHIMP_TYPE_ARAB_ASSASIN) } };
   Require(!Capture(),"Invalid selected unit ID was accepted");
   APIShared.GameModes.GameModeHelper.Editor=true;APIShared.LocalSelectionAPI.Available=false;
   Require(Capture(),"Editor native selection fallback was lost");
   api.Units[0].r_ControllableForPlayerId=2;Require(!Capture(),"Editor fallback accepted a foreign unit");
  } finally { Marshal.FreeHGlobal((IntPtr)api.Units); }
 }
}
""";
        fixture = fixture.Replace("ENUM", enumSource).Replace("MEMBERS", members);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("ActualSelectionCache", new[] { CSharpSyntaxTree.ParseText(fixture) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream();
        var emitted = compilation.Emit(output);
        Check(emitted.Success, "production selection cache fixture compiles: " +
            string.Join("\n", emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("SelectionFixture").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        Check(true, "production selection cache handles ownership, death, failed snapshots, invalidation and editor fallback");
    }
}
