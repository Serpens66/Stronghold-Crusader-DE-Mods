using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Ownership evidence only: compiler-bound dependencies include delegate references
// and initializers, but are neither runtime traces nor proof that a member is unused.
internal sealed class CommandDependencyReport
{
    private readonly string root;
    private readonly Dictionary<string, Node> nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> edges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> roots = new(StringComparer.Ordinal);

    internal CommandDependencyReport(string root) { this.root = root; }

    internal void AddApi(Compilation api)
    {
        foreach (var tree in api.SyntaxTrees)
        {
            var model = api.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot().DescendantNodes())
            {
                var symbol = Canonical(Declared(model, declaration));
                if (IsApi(symbol)) AddNode(symbol);
                if (symbol is INamedTypeSymbol type)
                    foreach (var constructor in type.InstanceConstructors.Concat(type.StaticConstructors))
                    {
                        AddNode(constructor);
                        AddInitializers(constructor);
                        if (constructor.MethodKind != MethodKind.Constructor ||
                            constructor.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is ConstructorDeclarationSyntax explicitConstructor && explicitConstructor.Initializer != null)) continue;
                        var parentConstructor = type.BaseType?.InstanceConstructors.FirstOrDefault(parent => parent.Parameters.Length == 0);
                        if (IsApi(parentConstructor)) AddEdge(constructor, parentConstructor);
                    }
            }
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                ISymbol target = Canonical(model.GetSymbolInfo(name).Symbol);
                ISymbol owner = Owner(model, name);
                if (!IsApi(target) || !IsApi(owner)) continue;
                AddEdge(owner, target);
                AddInitializers(target);
            }
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                ISymbol target = Canonical(model.GetSymbolInfo(creation).Symbol);
                ISymbol owner = Owner(model, creation);
                if (!IsApi(target) || !IsApi(owner)) continue;
                AddEdge(owner, target);
                AddInitializers(target);
            }
            foreach (var initializer in tree.GetRoot().DescendantNodes().OfType<ConstructorInitializerSyntax>())
            {
                ISymbol target = Canonical(model.GetSymbolInfo(initializer).Symbol);
                ISymbol owner = Owner(model, initializer);
                if (IsApi(target) && IsApi(owner)) AddEdge(owner, target);
            }
        }
    }

    internal void AddConsumer(Compilation consumer)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        roots.Add(consumer.AssemblyName, selected);
        foreach (var tree in consumer.SyntaxTrees)
        {
            var model = consumer.GetSemanticModel(tree);
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                ISymbol target = Canonical(model.GetSymbolInfo(name).Symbol);
                if (!IsApi(target)) continue;
                selected.Add(AddNode(target));
                AddInitializers(target);
            }
            // A constructor with no arguments is not represented by a method name.
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                ISymbol target = Canonical(model.GetSymbolInfo(creation).Symbol);
                if (!IsApi(target)) continue;
                selected.Add(AddNode(target));
                AddInitializers(target);
            }
        }
    }

    private void AddInitializers(ISymbol member)
    {
        var type = member as INamedTypeSymbol ?? member.ContainingType;
        if (type == null) return;
        bool instance = member is IMethodSymbol method && method.MethodKind == MethodKind.Constructor;
        foreach (var candidate in type.GetMembers())
        {
            bool initialized = candidate.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() switch {
                VariableDeclaratorSyntax variable => variable.Initializer != null,
                PropertyDeclarationSyntax property => property.Initializer != null,
                _ => false,
            });
            if (initialized && (candidate.IsStatic || instance)) AddEdge(member, candidate);
        }
        foreach (var constructor in type.StaticConstructors) AddEdge(member, constructor);
    }

    private static ISymbol Owner(SemanticModel model, SyntaxNode reference)
    {
        foreach (var parent in reference.Ancestors())
        {
            ISymbol symbol = Declared(model, parent);
            if (symbol != null) return Canonical(symbol);
        }
        return null;
    }

    private static ISymbol Declared(SemanticModel model, SyntaxNode declaration) => declaration switch {
                VariableDeclaratorSyntax variable when variable.Parent?.Parent is BaseFieldDeclarationSyntax => model.GetDeclaredSymbol(variable),
                BaseMethodDeclarationSyntax method => model.GetDeclaredSymbol(method),
                AccessorDeclarationSyntax accessor => model.GetDeclaredSymbol(accessor),
                PropertyDeclarationSyntax property => model.GetDeclaredSymbol(property),
                IndexerDeclarationSyntax indexer => model.GetDeclaredSymbol(indexer),
                EventDeclarationSyntax eventDeclaration => model.GetDeclaredSymbol(eventDeclaration),
                BaseTypeDeclarationSyntax type => model.GetDeclaredSymbol(type),
                _ => null,
            };

    private static ISymbol Canonical(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method && method.AssociatedSymbol != null) symbol = method.AssociatedSymbol;
        if (symbol is IMethodSymbol reduced && reduced.ReducedFrom != null) symbol = reduced.ReducedFrom;
        return symbol?.OriginalDefinition;
    }

    private static bool IsApi(ISymbol symbol) => symbol != null &&
        symbol.ContainingAssembly?.Name == "APIShared" &&
        symbol.Kind is SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Field or SymbolKind.Property or SymbolKind.Event;

    private string AddNode(ISymbol symbol)
    {
        string id = symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (!nodes.ContainsKey(id))
        {
            var source = symbol.Locations.FirstOrDefault(location => location.IsInSource);
            var span = source?.GetLineSpan();
            nodes.Add(id, new Node {
                Id = id, Symbol = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Namespace = symbol.ContainingNamespace?.ToDisplayString(), Kind = symbol.Kind.ToString(),
                Source = source == null ? null : Path.GetRelativePath(root, span.Value.Path),
                Line = source == null ? 0 : span.Value.StartLinePosition.Line + 1,
            });
            edges.Add(id, new HashSet<string>(StringComparer.Ordinal));
        }
        return id;
    }

    private void AddEdge(ISymbol owner, ISymbol target) => edges[AddNode(owner)].Add(AddNode(target));

    internal HashSet<string> Reachable(string consumer)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(roots[consumer]);
        while (pending.Count != 0)
        {
            string current = pending.Dequeue();
            if (!visited.Add(current)) continue;
            foreach (string next in edges[current]) pending.Enqueue(next);
        }
        return visited;
    }

    internal void Write(string path)
    {
        var consumers = roots.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var reached = consumers.ToDictionary(value => value, Reachable);
        foreach (var node in nodes.Values)
            node.ReachedBy = consumers.Where(consumer => reached[consumer].Contains(node.Id)).ToArray();
        var commands = nodes.Values.Where(node => node.Namespace == "BugfixesAndQoL.UnitCommands").OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
        string text = JsonSerializer.Serialize(new {
            Scope = "Compiler-bound explicit member dependency closure, including named delegate references, constructors and initialization dependencies. References in branches and nested function bodies count regardless of activation. Implicit operation calls, reflection, dynamic dispatch and external/native entry points are not modeled. An unreferenced member is not proven unused; shared reachability is not proof of required API ownership.",
            Roots = consumers.Select(consumer => new { Consumer = consumer, Members = roots[consumer].OrderBy(value => value, StringComparer.Ordinal).ToArray() }),
            CommandMembers = commands,
            Edges = edges.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { From = pair.Key, To = pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray() }),
        }, new JsonSerializerOptions { WriteIndented = true });
        text = text.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
        if (File.ReadAllText(path) != text) throw new IOException("Dependency report readback mismatch.");
        Console.WriteLine("Command dependency report: " + commands.Length + " members; " + path);
        foreach (var group in commands.GroupBy(node => string.Join(",", node.ReachedBy)))
            Console.WriteLine("  " + (group.Key.Length == 0 ? "no modeled consumer root" : group.Key) + ": " + group.Count());
    }

    internal static void VerifyFixtures()
    {
        var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var api = CSharpCompilation.Create("APIShared", new[] { CSharpSyntaxTree.ParseText(@"
namespace BugfixesAndQoL.UnitCommands {
 public static class Kernel { public static int Common() => 1; public static int Callback() => 2; public static int Field() => 3; public static int StaticField() => 5; }
 public class Holder { private int value = Kernel.Field(); public Holder() {} public int Read() => value; }
 public class DerivedHolder : Holder {}
 public static class Cached { private static int value = Kernel.StaticField(); public static int Read() => value; }
 public static class Engine {
  public static int Shared() => Kernel.Common() + Cached.Read();
  public static int BugfixOnly() { System.Func<int> callback = Kernel.Callback; return Shared() + callback(); }
  public static int Creates() => new DerivedHolder().Read();
  public static int NeverReferenced() => 4;
 }
}", path: "api.cs") }, new[] { reference }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Compilation Consumer(string name, string body) => CSharpCompilation.Create(name,
            new[] { CSharpSyntaxTree.ParseText("public class Consumer { public int Run() { " + body + " } }", path: name + ".cs") },
            new MetadataReference[] { reference, api.ToMetadataReference() }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var bugfix = Consumer("BugfixesAndQoL", "return BugfixesAndQoL.UnitCommands.Engine.BugfixOnly();");
        var moat = Consumer("MoatMove", "return BugfixesAndQoL.UnitCommands.Engine.Shared() + BugfixesAndQoL.UnitCommands.Engine.Creates();");
        foreach (var compilation in new[] { api, bugfix, moat })
            if (compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                throw new InvalidOperationException("Dependency fixture does not compile: " + compilation.AssemblyName);
        var report = new CommandDependencyReport(Path.GetFullPath("."));
        report.AddApi(api); report.AddConsumer(bugfix); report.AddConsumer(moat);
        var first = report.Reachable("BugfixesAndQoL"); var second = report.Reachable("MoatMove");
        if (!report.nodes.ContainsKey("M:BugfixesAndQoL.UnitCommands.Engine.NeverReferenced") ||
            !first.Contains("M:BugfixesAndQoL.UnitCommands.Kernel.Common") || !second.Contains("M:BugfixesAndQoL.UnitCommands.Kernel.Common") ||
            !first.Contains("M:BugfixesAndQoL.UnitCommands.Kernel.Callback") || second.Contains("M:BugfixesAndQoL.UnitCommands.Kernel.Callback") ||
            !first.Contains("M:BugfixesAndQoL.UnitCommands.Kernel.StaticField") || !second.Contains("M:BugfixesAndQoL.UnitCommands.Kernel.StaticField") ||
            !second.Contains("M:BugfixesAndQoL.UnitCommands.Kernel.Field") || first.Contains("M:BugfixesAndQoL.UnitCommands.Engine.NeverReferenced") ||
            second.Contains("M:BugfixesAndQoL.UnitCommands.Engine.NeverReferenced"))
            throw new InvalidOperationException("Dependency closure fixture failed.");
        Console.WriteLine("PASS: dependency closure fixtures (shared, owner-only, delegate, constructor initializer and unreferenced member).");
    }

    private sealed class Node
    {
        public string Id { get; set; }
        public string Symbol { get; set; }
        public string Namespace { get; set; }
        public string Kind { get; set; }
        public string Source { get; set; }
        public int Line { get; set; }
        public string[] ReachedBy { get; set; }
    }
}
