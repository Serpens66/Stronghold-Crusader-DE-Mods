using APIShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SHCDESE.API;
using SHCDESE.Interop;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Contains("--scan")) { Scan(); return; }
unsafe
{
    foreach (var state in Enum.GetValues<SHCDESE.Interop.Enums.AliveState>())
    foreach (uint marker in new uint[] { 0, 1, 0xFFFF, 0x10000, 0xFFFF0000, 0xFFFFFFFF })
    {
        GameUnit life = new GameUnit { r_AliveState = state, N0000019A = marker, r_CurrentHealth = 0 };
        bool expected = state == SHCDESE.Interop.Enums.AliveState.IsAlive && (marker & 0xFFFF) == 0;
        Check(UnitAccess.IsReallyAlive(in life) == expected, "reference life predicate");
        Check(UnitAccess.IsReallyAlive(&life) == expected, "pointer life predicate");
        life.r_CurrentHealth = uint.MaxValue;
        Check(UnitAccess.IsReallyAlive(in life) == expected, "health is not an additional rule");
    }
    Check(!UnitAccess.IsReallyAlive((GameUnit*)null), "null life view");
    GameUnit record = new GameUnit { Alive = 4, GlobalId = 0 }; // NeedsInit is still resolvable.
    var manager = new GameUnitManagerAPI { Pointer = &record };
    GameUnitManagerAPI.Current = manager;
    foreach (int id in new[] { int.MinValue, -1, 0 })
    {
        int before = GameUnitManagerAPI.InstanceReads;
        Check(!UnitAccess.TryGetById(id, out GameUnit* unit, out var reason) && unit == null && reason == UnitLookupFailure.InvalidId, "invalid ID");
        Check(GameUnitManagerAPI.InstanceReads == before && manager.Reads == 0 && manager.Validations == 0, "invalid ID never touches manager");
    }
    Check(!UnitAccess.TryGetById(null, 1, out GameUnit* absent, out var missing) && absent == null && missing == UnitLookupFailure.ManagerUnavailable, "missing manager");
    foreach (int id in new[] { 10000, int.MaxValue })
    {
        Check(!UnitAccess.TryGetById(manager, id, out GameUnit* unit, out var reason) && unit == null && reason == UnitLookupFailure.OutOfRange, "upper bound");
        Check(manager.Reads == 0, "out-of-range never enters SDK lookup");
    }
    foreach (int id in new[] { 1, 9999 })
    {
        Check(UnitAccess.TryGetById(manager, id, out GameUnit* unit, out var reason) && unit == &record && reason == UnitLookupFailure.None, "inclusive bounds");
        Check(manager.LastId == id && unit->Alive == 4 && unit->GlobalId == 0, "no ID conversion or life/identity filtering");
    }
    manager.Pointer = null;
    Check(!UnitAccess.TryGetById(manager, 1, out GameUnit* unloaded, out var unavailable) && unloaded == null && unavailable == UnitLookupFailure.Unavailable, "unavailable pointer after unload");
    manager.Pointer = &record; manager.ReturnFalse = true;
    Check(!UnitAccess.TryGetById(manager, 1, out GameUnit* failed, out var failure) && failed == null && failure == UnitLookupFailure.Unavailable, "false SDK result clears even non-null pointer");
    manager.ReturnFalse = false;
    Check(UnitAccess.TryGetById(manager, 1, out GameUnit* reloaded, out _) && reloaded == &record, "reload uses current manager storage");

    var log = new BepInEx.Logging.ManualLogSource();
    UnitAccess.InitializeDiagnostics(log);
    for (int i=0; i<100; i++) UnitAccess.TryGetById(manager, 0, out _, out _, "Mod/src/Worker.cs", "OnEvent", 12);
    Check(log.Lines.Count == 1 && log.Lines[0].Contains("mod=Mod") && log.Lines[0].Contains("reason=InvalidId"), "bounded attributed debug diagnostic");
    for (int i=0; i<300; i++) UnitAccess.TryGetById(manager, 0, out _, out _, "Mod/src/Worker.cs", "OnEvent", 100+i);
    Check(log.Lines.Count == 128, "global diagnostic bound");
}
Console.WriteLine("PASS: production UnitAccess boundaries, no invalid SDK calls, NeedsInit/identity neutrality, unload/reload, failure pointer clearing and bounded diagnostics.");

static void Check(bool value, string label) { if (!value) throw new Exception(label); }
static void Scan()
{
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
    var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "BepInEx", "tests", "packages", ".inspect", "_inspect", ".tools", ".native-analysis", ".release-output", "shcde-script-extender", "UCP", "x86_64" };
    int checkedFiles=0;
    foreach (string file in RuntimeFiles(root))
    {
        string relative=Path.GetRelativePath(root,file);
        if (relative.Split(Path.DirectorySeparatorChar).Any(p=>excluded.Contains(p)||p.EndsWith(".Tests",StringComparison.OrdinalIgnoreCase))) continue;
        var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
        checkedFiles++;
        foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax member) continue;
            if (member.Name.Identifier.Text is "TryGetUnitById" or "TryGetUnitByIdEx")
                Check(relative.Replace('\\','/')=="APIShared/src/UnitAccess.cs", $"Unprotected direct unit lookup: {relative}:{call.GetLocation().GetLineSpan().StartLinePosition.Line+1}");
            if (member.Expression.ToString().Contains("UnitAccess") && member.Name.Identifier.Text=="TryGetById")
                Check(call.Parent is not ExpressionStatementSyntax, $"Ignored lookup result: {relative}:{call.GetLocation().GetLineSpan().StartLinePosition.Line+1}");
            var indirectNames = new HashSet<string> {
                "DeleteUnit", "DeleteUnitSafe", "GetGlobalId", "GetTribe", "GetOwner", "GetType",
                "DamageUnitMelee", "DamageUnitRanged", "DamageUnitEx", "DamageUnitEx2",
                "GetCurrentHealth", "SetCurrentHealth", "GetMaxHealth", "SetMaxHealth", "GetSpeed", "SetSpeed",
                "KillUnit", "MoveToTile", "IsRendered", "GetCurrentUnityPosition", "GetCurrentLocalTilePosition", "GetCurrentWorldTilePosition",
                "SetCurrentLocalTilePosition", "SetCurrentWorldTilePosition", "IsSiegeEngineManned", "GetIsInvisible", "SetIsInvisible",
                "IsVisualHidden", "SetVisualHidden", "GetSpriteScale", "SetSpriteScale", "ResetSpriteScale",
                "GetGameMaterial", "SetGameMaterial", "GetShieldCurrentHealth", "SetShieldHealth", "SetSelectable", "IsSelectable", "GetDirection", "SetDirection"
            };
            string receiver=member.Expression.ToString();
            if (indirectNames.Contains(member.Name.Identifier.Text) && receiver is "GameUnitManagerAPI.Instance" or "api" or "unitApi" or "units")
            {
                ExpressionSyntax condition = call.Parent switch
                {
                    ConditionalExpressionSyntax ternary when ternary.WhenTrue==call => ternary.Condition,
                    BinaryExpressionSyntax binary when binary.Right==call && binary.IsKind(SyntaxKind.LogicalAndExpression) => binary.Left,
                    ExpressionStatementSyntax statement when statement.Parent is IfStatementSyntax branch => branch.Condition,
                    _ => null
                };
                var guard=condition as InvocationExpressionSyntax;
                Check(guard?.Expression.ToString()=="APIShared.UnitAccess.TryGetById", $"Indirect lookup lacks immediate checked guard: {relative}:{call.GetLocation().GetLineSpan().StartLinePosition.Line+1}");
                int idArgument=receiver=="GameUnitManagerAPI.Instance" ? 0 : 1;
                Check(guard.ArgumentList.Arguments[idArgument].Expression.ToString()==call.ArgumentList.Arguments[0].Expression.ToString(), $"Indirect guard checks a different ID: {relative}");
            }
        }
    }
    Console.WriteLine($"PASS: {checkedFiles} runtime source files; SDK unit lookups are confined to UnitAccess and callers consume their boolean results.");
    using var inventory=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"_inspect/UnitIdAccess/inventory.json")));
    foreach (var projectEntry in inventory.RootElement.GetProperty("projects").EnumerateObject())
    {
        string projectPath=Path.Combine(root,projectEntry.Name);
        string projectRoot=Path.GetDirectoryName(projectPath)!;
        var project=XDocument.Load(projectPath);
        var compileFiles=project.Descendants().Where(e=>e.Name.LocalName=="Compile").Select(e=>(string)e.Attribute("Include"))
            .Where(p=>p!=null && !p.Contains('*')).Select(p=>Path.GetFullPath(Path.Combine(projectRoot,p))).Where(File.Exists).ToList();
        compileFiles.Add(projectPath);
        foreach (string file in compileFiles.Distinct())
        {
            string text=File.ReadAllText(file);
            Check(!Regex.IsMatch(text,@"System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json"), "Forbidden runtime JSON: "+file);
            if (!file.EndsWith(".cs")) continue;
            var syntax=CSharpSyntaxTree.ParseText(text).GetRoot();
            foreach(var cls in syntax.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                bool plugin=cls.BaseList?.ToString().Contains("BaseUnityPlugin")==true;
                foreach(var method in cls.Members.OfType<MethodDeclarationSyntax>())
                {
                    string name=method.Identifier.Text;
                    if(plugin) Check(name is not ("Update" or "LateUpdate" or "FixedUpdate" or "OnEnable" or "Start"), "Plugin long-lived callback: "+file+":"+name);
                    if(name is "OnDestroy" or "OnDisable" or "OnApplicationQuit" or "OnApplicationPause")
                        Check(!Regex.IsMatch(method.ToString(),@"\b(?:Dispose|Stop|Undo|Disable|DisposeRuntime)\s*\("),"Teardown reaches persistent runtime: "+file);
                    if(plugin) Check(!Regex.IsMatch(method.ToString(),@"\b(?:StartCoroutine|InvokeRepeating|Invoke)\s*\("),"Plugin MonoBehaviour scheduling: "+file);
                }
            }
        }
        foreach(string file in Directory.EnumerateFiles(projectRoot,"*.xaml",SearchOption.AllDirectories))
            foreach(var content in XDocument.Load(file).Descendants().Where(e=>e.Name.LocalName=="Content"))
                Check(content.Elements().Count()==1,"XAML Content requires exactly one root: "+file);
    }
    Console.WriteLine("PASS: affected runtime projects and linked sources checked for JSON, plugin scheduling, lifecycle teardown and XAML roots.");
    IEnumerable<string> RuntimeFiles(string directory)
    {
        foreach(string file in Directory.EnumerateFiles(directory,"*.cs",SearchOption.TopDirectoryOnly)) yield return file;
        foreach(string child in Directory.EnumerateDirectories(directory))
        {
            string name=Path.GetFileName(child);
            if(excluded.Contains(name)||name.EndsWith(".Tests",StringComparison.OrdinalIgnoreCase)) continue;
            foreach(string file in RuntimeFiles(child)) yield return file;
        }
    }
}

namespace SHCDESE.Interop.Enums { public enum AliveState : short { None, NeedsInit, IsAlive, MarkedForDeletion, Unknown, Unknown5, Paused } }
namespace SHCDESE.Interop { public struct GameUnit { public int Alive, GlobalId; public SHCDESE.Interop.Enums.AliveState r_AliveState; public uint N0000019A, r_CurrentHealth; } }
namespace SHCDESE.API
{
    public unsafe class GameUnitManagerAPI
    {
        public static GameUnitManagerAPI Current;
        public static int InstanceReads;
        public static GameUnitManagerAPI Instance { get { InstanceReads++; return Current!; } }
        public GameUnit* Pointer;
        public int Reads, Validations, LastId;
        public bool ReturnFalse;
        public bool IsValidId(int id) { Validations++; return id>0 && id<=9999; }
        public bool TryGetUnitById(int id, out GameUnit* unit) { Reads++; LastId=id; unit=Pointer; return !ReturnFalse; }
    }
}
namespace BepInEx.Logging
{
    public class ManualLogSource { public readonly List<string> Lines=new(); public void LogDebug(object line) => Lines.Add(line.ToString()!); }
}
