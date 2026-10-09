using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Test seam: discover the production type by identity, independent of its file layout.
internal static class FeatureSourceModel
{
    internal static string Read(string workspace, string typeName)
    {
        var parts = Directory.GetFiles(Path.Combine(workspace, "APIShared", "src"), "*.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .SelectMany(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>())
            .Where(c => c.Identifier.Text == typeName).ToArray();
        if (parts.Length == 0) throw new InvalidOperationException("Production type not found: " + typeName);
        return parts[0].WithMembers(SyntaxFactory.List(parts.SelectMany(c => c.Members))).ToFullString();
    }
}
