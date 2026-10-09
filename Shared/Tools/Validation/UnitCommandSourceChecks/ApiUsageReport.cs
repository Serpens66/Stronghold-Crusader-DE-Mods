using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Optional compiler-bound evidence for ownership review. Direct references do not
// prove runtime execution, transitive reachability or the absence of reflection.
internal sealed class ApiUsageReport
{
    private readonly string root;
    private readonly SortedDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    internal ApiUsageReport(string root) { this.root = root; }

    internal void Add(Compilation consumer)
    {
        foreach (var tree in consumer.SyntaxTrees)
        {
            var model = consumer.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(node).Symbol;
                if (symbol?.ContainingAssembly?.Name != "APIShared" || symbol.Kind == SymbolKind.Namespace) continue;
                var location = node.GetLocation().GetLineSpan();
                foreach (var declaration in symbol.DeclaringSyntaxReferences)
                {
                    string target = Path.GetRelativePath(root, declaration.SyntaxTree.FilePath);
                    var entry = new Entry {
                        Consumer = consumer.AssemblyName,
                        Source = Path.GetRelativePath(root, tree.FilePath),
                        Line = location.StartLinePosition.Line + 1,
                        Symbol = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Kind = symbol.Kind.ToString(),
                        Accessibility = symbol.DeclaredAccessibility.ToString(),
                        Declaration = target,
                    };
                    string key = string.Join("\t", entry.Consumer, entry.Source, entry.Line, entry.Symbol, target);
                    entries[key] = entry;
                }
            }
        }
    }

    internal void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string text = JsonSerializer.Serialize(new {
            Scope = "Direct compiler-resolved references in the selected error-free consumer compilations; not runtime reachability or reflection coverage.",
            Entries = entries.Values.ToArray(),
        }, new JsonSerializerOptions { WriteIndented = true });
        text = text.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n";
        File.WriteAllText(path, text, new UTF8Encoding(false));
        if (File.ReadAllText(path) != text) throw new IOException("API usage report readback mismatch.");
        Console.WriteLine("API usage report: " + entries.Count + " compiler-resolved references; " + path);
    }

    private sealed class Entry
    {
        public string Consumer { get; set; }
        public string Source { get; set; }
        public int Line { get; set; }
        public string Symbol { get; set; }
        public string Kind { get; set; }
        public string Accessibility { get; set; }
        public string Declaration { get; set; }
    }
}
