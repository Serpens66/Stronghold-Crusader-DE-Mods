using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class RuntimeSignatures
{
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }

    private static IPropertySymbol Property(ITypeSymbol type, string name)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            var property = current.GetMembers(name).OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic);
            if (property != null) return property;
        }
        return null;
    }

    private static ITypeSymbol ResolvePath(ITypeSymbol context, string path, bool twoWay = false)
    {
        if (string.IsNullOrWhiteSpace(path) || path == ".") return context;
        var segments = path.Split('.');
        for (int index = 0; index < segments.Length; index++)
        {
            var property = Property(context, segments[index]);
            Check(property?.GetMethod?.DeclaredAccessibility == Accessibility.Public,
                "Missing readable XAML binding: " + context?.ToDisplayString() + "." + path);
            if (twoWay && index == segments.Length - 1)
                Check(property.SetMethod?.DeclaredAccessibility == Accessibility.Public,
                    "Missing writable XAML binding: " + context.ToDisplayString() + "." + path);
            context = property.Type;
        }
        return context;
    }

    private static string BindingPath(string markup)
    {
        if (markup == null || !markup.StartsWith("{Binding", StringComparison.Ordinal) ||
            markup.Contains("RelativeSource=") || markup.Contains("ElementName=")) return null;
        var match = Regex.Match(markup, @"^\{Binding\s+(?:Path=)?([A-Za-z_][A-Za-z0-9_.]*)");
        return match.Success ? match.Groups[1].Value : ".";
    }

    private static ITypeSymbol ItemType(ITypeSymbol source)
    {
        if (source is IArrayTypeSymbol array) return array.ElementType;
        var sequence = (source as INamedTypeSymbol)?.AllInterfaces
            .FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
        Check(sequence != null, "Cannot infer DataTemplate item context from " + source?.ToDisplayString());
        return sequence.TypeArguments[0];
    }

    // Follow actual DataContexts and ItemsSource element types. A preset row is not
    // a property of the page ViewModel. No binding-name whitelist or UI adjacency is assumed.
    private static void CheckBindings(XElement element, ITypeSymbol context)
    {
        string dataContext = BindingPath((string)element.Attribute("DataContext"));
        if (dataContext != null) context = ResolvePath(context, dataContext);
        foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName != "DataContext"))
        {
            string path = BindingPath(attribute.Value);
            if (path != null) ResolvePath(context, path, Regex.IsMatch(attribute.Value, @"\bMode\s*=\s*TwoWay\b"));
        }
        if (element.Name.LocalName == "Binding" && element.Attribute("RelativeSource") == null && element.Attribute("ElementName") == null)
            ResolvePath(context, (string)element.Attribute("Path"));
        foreach (var child in element.Elements())
        {
            var childContext = context;
            if (child.Name.LocalName == "DataTemplate")
            {
                var owner = child.Ancestors().FirstOrDefault(a => a.Attribute("ItemsSource") != null);
                Check(owner != null, "DataTemplate requires a typed ItemsSource for offline validation.");
                string itemsPath = BindingPath((string)owner.Attribute("ItemsSource"));
                Check(itemsPath != null, "DataTemplate ItemsSource binding is not supported by this checker.");
                childContext = ItemType(ResolvePath(context, itemsPath));
            }
            CheckBindings(child, childContext);
        }
    }

    private static void ExpectInvalidBinding(string xaml, ITypeSymbol context)
    {
        bool rejected = false;
        try { CheckBindings(XElement.Parse(xaml), context); }
        catch (Exception) { rejected = true; }
        Check(rejected, "Binding checker accepted an invalid member: " + xaml);
    }
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
            string apiRoot=Path.GetFullPath(Path.Combine(mod, "../../APIShared"));
            string sdk=Environment.GetEnvironmentVariable("SHCDE_API_SHARED_DIR") ?? Path.Combine(apiRoot,"BepInEx/plugins/APIShared_Serp");
            var paths = new HashSet<string>(Directory.GetFiles(framework, "*.dll", SearchOption.AllDirectories).Where(p => {
                using var stream = File.OpenRead(p); using var pe = new PEReader(stream);
                return pe.HasMetadata && pe.GetMetadataReader().IsAssembly;
            }), StringComparer.OrdinalIgnoreCase);
            foreach (var reference in project.Descendants(ns + "Reference"))
            {
                string hint = (string)reference.Element(ns + "HintPath"); if (hint == null) continue;
                hint = hint.Replace("$(GameDir)", game).Replace("$(ExtenderDir)", game + "/BepInEx/plugins/000shcdese")
                    .Replace("$(ApiSharedDir)", sdk);
                Check(File.Exists(hint), "Missing reference: " + hint); paths.Add(Path.GetFullPath(hint));
            }
            var compilation = CSharpCompilation.Create("PopularityTest.Preflight", trees,
                paths.Select(p => MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            string[] errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
            Check(errors.Length == 0, "Actual dependency signature errors:\n" + string.Join("\n", errors));
            foreach (var pair in new[] {
                ("RationsMode", new[] { "None", "Half", "Full", "Extra", "Double" }),
                ("TaxesMode", new[] { "HugeDonation", "BigDonation", "SmallDonation", "None", "Low", "Moderate", "High", "Mean", "Usurious", "Cruel", "MostCruel", "Cruellest" }) })
            {
                var enumType = compilation.GetTypeByMetadataName("SHCDESE.Interop.Enums." + pair.Item1);
                for (int index = 0; index < pair.Item2.Length; index++)
                    Check(enumType.GetMembers(pair.Item2[index]).OfType<IFieldSymbol>().Any(f => f.HasConstantValue && Convert.ToInt64(f.ConstantValue) == index),
                        "Installed enum contract: " + pair.Item1 + "." + pair.Item2[index]);
            }
            var model = compilation.GetTypeByMetadataName("PopularityTest.PopularitySettings");
            var xaml = XDocument.Load(Path.Combine(mod, "Override/ScriptExtenderUI/PopularitySettings.xaml"));
            CheckBindings(xaml.Root, model);
            // These counterexamples prevent accidentally skipping item bindings or
            // validating them against the root (which also has EnableMod).
            ExpectInvalidBinding("<Grid Tag='{Binding MissingMember}'/>", model);
            ExpectInvalidBinding("<ItemsControl ItemsSource='{Binding System_PresetSaveSettings}'><ItemsControl.ItemTemplate><DataTemplate><CheckBox IsChecked='{Binding EnableMod}'/></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>", model);
            Console.WriteLine("PASS: all production sources and XAML bindings against installed SE/selected APIShared SDK and true game assembly; no runtime DLL emitted.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
