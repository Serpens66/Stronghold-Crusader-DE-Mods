using APIShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SHCDESE.API;
using SHCDESE.Interop;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Contains("--scan")) { Scan(args); return; }
unsafe
{
    foreach (var state in Enum.GetValues<SHCDESE.Interop.Enums.AliveState>())
    foreach (uint marker in new uint[] { 0, 1, 0xFFFF, 0x10000, 0xFFFF0000, 0xFFFFFFFF })
    {
        GameUnit life = new GameUnit { r_AliveState = state, r_IsKilledByProjectile = (ushort)marker, r_InterestingTodo4MaybeRandomPathing = (ushort)(marker >> 16), r_CurrentHealth = 0 };
        bool expected = state == SHCDESE.Interop.Enums.AliveState.IsAlive && (marker & 0xFFFF) == 0;
        Check(UnitAccess.IsReallyAlive(in life) == expected, "reference life predicate");
        Check(UnitAccess.IsReallyAlive(&life) == expected, "pointer life predicate");
        life.r_CurrentHealth = uint.MaxValue;
        Check(UnitAccess.IsReallyAlive(in life) == expected, "health is not an additional rule");
    }
    Check(!UnitAccess.IsReallyAlive((GameUnit*)null), "null life view");
    var queryManager = new GameUnitManagerAPI();
    GameUnitManagerAPI.Current = queryManager;
    Check(UnitAccess.GetAllReallyAliveUnits().Length == 0, "empty query");
    queryManager.QueryRecords = new[] {
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.IsAlive },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.IsAlive, r_IsKilledByProjectile = 1 },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.NeedsInit },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.IsAlive, r_IsKilledByProjectile = 0, r_InterestingTodo4MaybeRandomPathing = 1 },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.MarkedForDeletion },
    };
    int[] livingIds = UnitAccess.GetAllReallyAliveUnits();
    Check(livingIds.SequenceEqual(new[] { 1, 4 }), "query filters death low word and returns ordered one-based IDs");
    queryManager.QueryRecords[0].r_IsKilledByProjectile = 1;
    Check(livingIds.SequenceEqual(new[] { 1, 4 }) && UnitAccess.GetAllReallyAliveUnits().SequenceEqual(new[] { 4 }),
        "query is a momentary copy, later life changes require revalidation");
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

    for (int i = 0; i < 4; i++) RejectedLookups();
    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    bool rejected = RejectedLookups();
    long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    Check(rejected && allocated == 0, "failure reasons remain available without shared diagnostic allocations");
}
Console.WriteLine("PASS: production UnitAccess boundaries, no invalid SDK calls, NeedsInit/identity neutrality, unload/reload, failure pointer clearing and allocation-free rejection.");

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static unsafe bool RejectedLookups()
{
    bool rejected = true;
    for (int i = 0; i < 10000; i++)
    {
        bool found = UnitAccess.TryGetById(null, 0, out var unit, out var failure,
            "Mod/src/Worker.cs", "OnEvent", i);
        rejected &= !found && unit == null && failure == UnitLookupFailure.InvalidId;
    }
    return rejected;
}

static void Check(bool value, string label) { if (!value) throw new Exception(label); }
static void Scan(string[] arguments)
{
    VerifyLookupFixtures();
    VerifyRuntimeFixtures();
    var ancestor = new DirectoryInfo(AppContext.BaseDirectory);
    while (ancestor != null && !File.Exists(Path.Combine(ancestor.FullName, "_inspect/UnitIdAccess/inventory.json"))) ancestor = ancestor.Parent;
    Check(ancestor != null, "Workspace inventory not found above test output");
    string root = ancestor!.FullName;
    string modRoot = null;
    if (arguments.Contains("--mod-directory"))
    {
        int index = Array.IndexOf(arguments, "--mod-directory");
        Check(index + 1 < arguments.Length, "Missing mod directory");
        modRoot = Path.GetFullPath(Path.Combine(root, arguments[index + 1]));
        Check(Directory.Exists(modRoot) && modRoot.StartsWith(root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Mod directory must be inside the workspace");
    }
    var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "BepInEx", "tests", "packages", ".inspect", "_inspect", ".tools", ".native-analysis", ".work", ".release-output", "shcde-script-extender", "UCP", "x86_64" };
    int checkedFiles=0;
    var scanRoots = modRoot == null ? new[] { root } : new[] { modRoot, Path.Combine(root,"APIShared/src") };
    // Only source links actually compiled into this consumer, not every Shared helper.
    var linked = modRoot == null ? Enumerable.Empty<string>() : Directory.EnumerateFiles(modRoot, "*.csproj", SearchOption.TopDirectoryOnly)
        .SelectMany(p => XDocument.Load(p).Descendants().Where(e => e.Name.LocalName == "Compile")
            .Select(e => (string)e.Attribute("Include")).Where(i => i != null && !i.Contains('$') && !i.Contains('*'))
            .Select(i => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(p)!, i!)))).Where(File.Exists);
    foreach (string file in scanRoots.SelectMany(RuntimeFiles).Concat(linked).Distinct())
    {
        string relative=Path.GetRelativePath(root,file);
        // Public third-party examples are documentation, not workspace runtime sources.
        // Their public SDK calls cannot use our internal UnitAccess implementation.
        // Match the project inventory's existing exclusion of APIShared/examples.
        if (relative.Replace('\\','/').StartsWith("APIShared/examples/", StringComparison.OrdinalIgnoreCase)) continue;
        if (relative.Split(Path.DirectorySeparatorChar).Any(p=>excluded.Contains(p)||p.EndsWith(".Tests",StringComparison.OrdinalIgnoreCase))) continue;
        checkedFiles++;
        CheckLookupSyntax(File.ReadAllText(file), relative);
    }
    Console.WriteLine($"PASS: {checkedFiles} runtime source files; SDK unit lookups are confined to UnitAccess and callers consume their boolean results.");
    using var inventory=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"_inspect/UnitIdAccess/inventory.json")));
    foreach (var projectEntry in inventory.RootElement.GetProperty("projects").EnumerateObject())
    {
        string projectPath=Path.Combine(root,projectEntry.Name);
        if (modRoot != null && !Path.GetFullPath(projectPath).StartsWith(modRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !projectEntry.Name.Replace('\\','/').StartsWith("APIShared/", StringComparison.OrdinalIgnoreCase)) continue;
        if (!File.Exists(projectPath) && projectEntry.Name.Replace('\\','/').StartsWith("Testmods/", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"SKIP: inventory testmod project no longer present: {projectEntry.Name}");
            continue;
        }
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
            CheckRuntimeSyntax(text, file);
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

// Syntax guardrail: catches direct startup teardown and scheduling. It does not
// prove indirect lifetime, native publication or in-game shutdown behavior.
static void CheckRuntimeSyntax(string text, string file)
{
    Check(!Regex.IsMatch(text,@"System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json"), "Forbidden runtime JSON: "+file);
    var syntax=CSharpSyntaxTree.ParseText(text).GetRoot();
    foreach(var cls in syntax.DescendantNodes().OfType<ClassDeclarationSyntax>())
    {
        bool plugin=cls.BaseList?.ToString().Contains("BaseUnityPlugin")==true;
        foreach(var method in cls.Members.OfType<MethodDeclarationSyntax>())
        {
            string name=method.Identifier.Text;
            if(plugin) Check(name is not ("Update" or "LateUpdate" or "FixedUpdate"), "Plugin long-lived callback: "+file+":"+name);
            // A real shutdown may release ordinary resources. Published native hooks
            // remain governed by the independent permanent-hook audit.
            if(name is "OnDestroy" or "OnDisable" or "OnApplicationPause")
                Check(!Regex.IsMatch(method.ToString(),@"\b(?:Dispose|Stop|Undo|Disable|DisposeRuntime)\s*\("),"Startup teardown reaches persistent runtime: "+file);
            if(plugin) Check(!Regex.IsMatch(method.ToString(),@"\b(?:StartCoroutine|InvokeRepeating|Invoke)\s*\("),"Plugin MonoBehaviour scheduling: "+file);
        }
    }
}

static void VerifyRuntimeFixtures()
{
    CheckRuntimeSyntax("class P : BaseUnityPlugin { void OnApplicationQuit() { resource.Dispose(); } }", "ordinary-shutdown.cs");
    CheckRuntimeSyntax("class P : BaseUnityPlugin { void OnDestroy() { Observe(); } void Observe() {} }", "harmless-startup-observer.cs");
    foreach (string source in new[] {
        "class P : BaseUnityPlugin { void OnDestroy() { runtime.Dispose(); } }",
        "class P : BaseUnityPlugin { void Update() { Work(); } }",
        "class P : BaseUnityPlugin { void Awake() { StartCoroutine(Work()); } }",
        "class P { System.Text.Json.JsonSerializerOptions options; }" })
    {
        bool rejected=false;
        try { CheckRuntimeSyntax(source, "negative-runtime.cs"); } catch (Exception) { rejected=true; }
        Check(rejected, "Runtime guardrail accepted a known regression");
    }
    Console.WriteLine("PASS: JSON, plugin scheduling, startup teardown and ordinary shutdown scanner counterexamples.");
}

static void CheckLookupSyntax(string text, string relative)
{
    var syntax=CSharpSyntaxTree.ParseText(text).GetRoot();
        foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax member) continue;
            if (member.Name.Identifier.Text is "TryGetUnitById" or "TryGetUnitByIdEx")
                Check(relative.Replace('\\','/')=="APIShared/src/Units/UnitAccess.cs", $"Direkter SDK-Lookup umgeht UnitAccess: {relative}:{call.GetLocation().GetLineSpan().StartLinePosition.Line+1}");
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

static void VerifyLookupFixtures()
{
    string guarded = "class C { bool Read() => APIShared.UnitAccess.TryGetById(1, out var unit, out var failure); }";
    CheckLookupSyntax(guarded, "consumer.cs");
    string direct = "class C { bool Read() => GameUnitManagerAPI.Instance.TryGetUnitById(1, out var unit); }";
    CheckLookupSyntax(direct, "APIShared/src/Units/UnitAccess.cs");
    bool denied = false;
    try { CheckLookupSyntax(direct, "consumer.cs"); }
    catch (Exception ex) { denied = ex.Message.StartsWith("Direkter SDK-Lookup umgeht UnitAccess:"); }
    Check(denied, "Direct SDK fixture must fail the real scanner rule");
    bool ignored = false;
    try { CheckLookupSyntax("class C { void Read() { APIShared.UnitAccess.TryGetById(1, out var unit, out var failure); } }", "consumer.cs"); }
    catch (Exception ex) { ignored = ex.Message.StartsWith("Ignored lookup result:"); }
    Check(ignored, "Ignored UnitAccess result fixture must fail the real scanner rule");
    Console.WriteLine("PASS: UnitAccess scanner fixtures: guarded consumer and central owner accepted; direct SDK and ignored result rejected.");
}


namespace SHCDESE.Interop.Enums { public enum AliveState : short { None, NeedsInit, IsAlive, MarkedForDeletion, Unknown, Unknown5, Paused } }
namespace SHCDESE.Interop { public struct GameUnit { public int Alive, GlobalId; public SHCDESE.Interop.Enums.AliveState r_AliveState; public ushort r_IsKilledByProjectile, r_InterestingTodo4MaybeRandomPathing; public uint r_CurrentHealth; } }
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
        public GameUnit[] QueryRecords = Array.Empty<GameUnit>();
        public UnitQuery QueryUnits() => new UnitQuery(QueryRecords);
        public delegate bool UnitPredicate(in GameUnit unit);
        public sealed class UnitQuery
        {
            private readonly GameUnit[] records;
            private UnitPredicate predicate;
            public UnitQuery(GameUnit[] records) { this.records = records; }
            public UnitQuery Where(UnitPredicate filter) { predicate = filter; return this; }
            public void ToIdList(List<int> ids)
            {
                for (int spanIndex = 0; spanIndex < records.Length; spanIndex++)
                    if (predicate == null || predicate(in records[spanIndex])) ids.Add(spanIndex + 1);
            }
        }
        public bool IsValidId(int id) { Validations++; return id>0 && id<=9999; }
        public bool TryGetUnitById(int id, out GameUnit* unit) { Reads++; LastId=id; unit=Pointer; return !ReturnFalse; }
    }
}
namespace BepInEx.Logging
{
    public class ManualLogSource { public readonly List<string> Lines=new(); public void LogDebug(object line) => Lines.Add(line.ToString()!); }
}
