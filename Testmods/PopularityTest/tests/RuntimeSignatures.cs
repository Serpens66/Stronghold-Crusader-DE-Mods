using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

internal static class RuntimeSignatures
{
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
    private static int Main(string[] args)
    {
        try
        {
            string mod = Path.GetFullPath(args[0]);
            string game = "E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition";
            string framework = "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8.1";
            var project = XDocument.Load(Path.Combine(mod, "PopularityTest.csproj")); XNamespace ns = project.Root.Name.Namespace;
            var trees = project.Descendants(ns + "Compile").Select(e => Path.GetFullPath(Path.Combine(mod, (string)e.Attribute("Include"))))
                .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p)).ToList();
            var paths = new HashSet<string>(Directory.GetFiles(framework, "*.dll", SearchOption.AllDirectories).Where(p => {
                using var stream = File.OpenRead(p); using var pe = new PEReader(stream);
                return pe.HasMetadata && pe.GetMetadataReader().IsAssembly;
            }), StringComparer.OrdinalIgnoreCase);
            foreach (var reference in project.Descendants(ns + "Reference"))
            {
                string hint = (string)reference.Element(ns + "HintPath"); if (hint == null) continue;
                hint = hint.Replace("$(GameDir)", game).Replace("$(ExtenderDir)", game + "/BepInEx/plugins/000shcdese")
                    .Replace("$(ApiSharedDir)", game + "/BepInEx/plugins/APIShared_Serp");
                Check(File.Exists(hint), "Missing reference: " + hint); paths.Add(Path.GetFullPath(hint));
            }
            var compilation = CSharpCompilation.Create("PopularityTest.Preflight", trees,
                paths.Select(p => MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            string[] errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
            Check(errors.Length == 0, "Actual dependency signature errors:\n" + string.Join("\n", errors));
            var names = new HashSet<string>();
            for (var type = compilation.GetTypeByMetadataName("PopularityTest.PopularitySettings"); type != null; type = type.BaseType)
                foreach (var member in type.GetMembers()) if (member.DeclaredAccessibility == Accessibility.Public) names.Add(member.Name);
            string xaml = File.ReadAllText(Path.Combine(mod, "Override/ScriptExtenderUI/PopularitySettings.xaml"));
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(xaml, @"\{Binding ([A-Za-z_][A-Za-z0-9_]*)"))
                Check(names.Contains(match.Groups[1].Value), "Missing XAML binding: " + match.Groups[1].Value);
            Console.WriteLine("PASS: all production sources and XAML bindings against installed SE/APIShared 0.9.2 and true game assembly; no runtime DLL emitted.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
