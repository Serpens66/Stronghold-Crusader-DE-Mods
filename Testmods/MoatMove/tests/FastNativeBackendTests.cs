using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class FastNativeBackendTests
{
    internal static void Validate(string root)
    {
        string src = Path.Combine(root, "Testmods/MoatMove/src");
        string extender = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        var trees = new[] { "IFastRouteField.cs", "FastRouteField.cs", "FastNativeKernel.cs", "FastNativeRouteField.cs" }
            .Select(name => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(src, name)))).ToList();
        var edge = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(src, "MoatSearchKernel.cs")))
            .GetRoot().DescendantNodes().OfType<DelegateDeclarationSyntax>().Single(d => d.Identifier.Text == "MoatSearchEdge");
        trees.Add(CSharpSyntaxTree.ParseText("namespace MoatMove {" + edge + "}"));
        trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Testmods/MoatMove/tests/FastNativeFixtures.cs"))));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).ToList();
        foreach (string name in new[] { "Microsoft.Extensions.Logging.Abstractions", "Iced", "RedBird.Abstractions", "RedBird.Core" })
        {
            string path = Path.Combine(extender, name + ".dll");
            refs.Add(MetadataReference.CreateFromFile(path)); Assembly.LoadFrom(path);
        }
        var compilation = CSharpCompilation.Create("MoatMoveNativeBackendFixtures", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        using var output = new MemoryStream(); var emitted = compilation.Emit(output);
        if (!emitted.Success) throw new Exception(string.Join("\n", emitted.Diagnostics));
        Assembly.Load(output.ToArray()).GetType("MoatMove.FastNativeFixtures")!.GetMethod("Run")!.Invoke(null, new object[] { root });
    }
}
