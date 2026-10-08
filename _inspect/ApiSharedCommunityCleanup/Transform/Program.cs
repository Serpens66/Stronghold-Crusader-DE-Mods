using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
var output = args[0];
var rows = args.Skip(1).Select(path => {
    var text = File.ReadAllText(path).Replace("\r\n", "\n");
    var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
    var type = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "Program");
    return new { path, usings = string.Join("\n", root.Usings), members = type.Members.Select(m => new {
        name = m is MethodDeclarationSyntax method ? method.Identifier.Text : "",
        kind = m.Kind().ToString(), text = m.ToFullString(),
        statements = (m as MethodDeclarationSyntax)?.Body?.Statements.Select(s => new {
            kind = s.Kind().ToString(), text = s.ToFullString(),
            inner = s is TryStatementSyntax t ? t.Block.Statements.Select(b => b.ToFullString()).ToArray() : null
        }).ToArray()
    }).ToArray() };
}).ToArray();
File.WriteAllText(output, JsonSerializer.Serialize(rows));
