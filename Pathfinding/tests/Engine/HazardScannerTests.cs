using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Compile whole production scanners/model against fake game boundaries, plus the real
// rule registry and owner-policy union. No scanner logic is copied into the fixture.
internal static class HazardScannerTests
{
    internal static void Run(string root)
    {
        string Read(string path)
        {
            string source = Path.Combine(root, path);
            // External Extender sources stay in the coordinator for sparse release builds.
            string coordinator = Environment.GetEnvironmentVariable("SHCDE_MODPACK_COORDINATOR");
            if (path.StartsWith("shcde-script-extender/", StringComparison.Ordinal) &&
                !File.Exists(source) && !string.IsNullOrWhiteSpace(coordinator))
                source = Path.Combine(coordinator, path);
            return File.ReadAllText(source);
        }
        SyntaxNode Source(string path) => CSharpSyntaxTree.ParseText(Read(path)).GetRoot();
        var sources = new List<SyntaxTree>();
        foreach (string name in new[] { "FireRouteRules", "PlagueRouteRules", "PlagueDamageModel" })
            sources.Add(CSharpSyntaxTree.ParseText(Read("Pathfinding/src/Engine/" + name + ".cs"), path: name + ".cs"));
        var tiles = Source("Pathfinding/src/Engine/FireEscape.cs").DescendantNodes()
            .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "HazardTileSet");
        sources.Add(CSharpSyntaxTree.ParseText("using System; using APIShared.Pathfinding; namespace Pathfinding.Engine {" + tiles + "}"));
        foreach (string path in new[] {
            "APIShared/src/Core/Events/OrderedRegistrations.cs", "APIShared/src/Pathfinding/Routes/RouteTileRules.cs",
            "APIShared/src/Projectiles/PlagueHeightPolicies.cs",
            "shcde-script-extender/src/SHCDESE.BepInEx/Interop/EStructs.cs",
            "shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums/ProjectileType.cs",
            "Pathfinding/tests/Engine/HazardScannerFixture.cs" })
            sources.Add(CSharpSyntaxTree.ParseText(Read(path), path: path));
        var contracts = Source("APIShared/src/Projectiles/PlagueHeightContracts.cs").DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>().Where(c => c.Identifier.Text is "PlagueHeightPolicy" or "PlagueSurfaceCategory");
        sources.Add(CSharpSyntaxTree.ParseText("using System; namespace APIShared {" + string.Join("\n", contracts) + "}"));
        // Compare category classification to the actual native-stub category table, not
        // an independently maintained list of copied numeric enum values.
        var categories = Source("APIShared/src/Projectiles/PlagueHeightAssembly.cs").DescendantNodes()
            .OfType<FieldDeclarationSyntax>().Single(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "BuildingCategories"));
        sources.Add(CSharpSyntaxTree.ParseText("using SHCDESE.Interop; namespace APIShared { internal static class NativeCategoryContract {" + categories + "}}"));
        // Execute the unchanged production scheduling block with instrumented boundary
        // calls; the actual native AnchorTick is deliberately not simulated here.
        var scheduler = Source("Pathfinding/src/Engine/FastMovementScheduler.cs").DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "ProcessFastCommands");
        var block = scheduler.DescendantNodes().OfType<IfStatementSyntax>()
            .Single(i => i.Condition.ToString() == "runtime.EngineEnabled");
        sources.Add(CSharpSyntaxTree.ParseText("namespace Pathfinding.Engine { internal partial class SchedulerOrderFixture { public void Tick(int tick) {" + block + "}}}"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("HazardScannerProduction", sources, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        using var bytes = new MemoryStream();
        var emitted = compilation.Emit(bytes);
        if (!emitted.Success) throw new Exception(string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        Assembly assembly = Assembly.Load(bytes.ToArray());
        ValidateUsedEnums(root, assembly, sources);
        try { assembly.GetType("Pathfinding.Engine.HazardScannerFixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException ex) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw(); }
    }

    private static void ValidateUsedEnums(string root, Assembly fixture, IEnumerable<SyntaxTree> sources)
    {
        string game = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ?? @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        string extender = Environment.GetEnvironmentVariable("SHCDESE_EXTENDER_DIR") ?? Path.Combine(game, "BepInEx/plugins/000shcdese");
        using var file = File.OpenRead(Path.Combine(extender, "SHCDESE.dll"));
        using var pe = new PEReader(file); var reader = pe.GetMetadataReader();
        foreach (string name in new[] { "eStructs", "ProjectileType" })
        {
            var members = sources.SelectMany(t => t.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                .Where(m => m.Expression.ToString() == name).Select(m => m.Name.Identifier.Text).Distinct().ToArray();
            Type localType = fixture.GetType(name == "eStructs" ? "SHCDESE.Interop.eStructs" : "SHCDESE.Interop.Enums.ProjectileType")!;
            var definition = reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(t => reader.GetString(t.Name) == name);
            foreach (string member in members)
            {
                var field = definition.GetFields().Select(reader.GetFieldDefinition).Single(f => reader.GetString(f.Name) == member);
                var constant = reader.GetConstant(field.GetDefaultValue()); var value = reader.GetBlobReader(constant.Value);
                long actual = constant.TypeCode switch {
                    ConstantTypeCode.Byte => value.ReadByte(), ConstantTypeCode.SByte => value.ReadSByte(),
                    ConstantTypeCode.Int16 => value.ReadInt16(), ConstantTypeCode.UInt16 => value.ReadUInt16(),
                    ConstantTypeCode.Int32 => value.ReadInt32(), ConstantTypeCode.UInt32 => value.ReadUInt32(),
                    _ => throw new InvalidOperationException("Unsupported enum backing type: " + name)
                };
                if (Convert.ToInt64(Enum.Parse(localType, member)) != actual)
                    throw new Exception("Extender source/installed enum mismatch: " + name + "." + member);
            }
        }
    }
}
