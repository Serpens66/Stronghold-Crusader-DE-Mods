using APIShared.NativeKnowledge;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// Offline authoring executable. Compiles the real catalog; never loads APIShared/game assemblies.
if (args.Length != 2) throw new ArgumentException("Expected source-directory output.json");
string source = Path.GetFullPath(args[0]);
var files = Directory.GetFiles(source).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
options.Converters.Add(new JsonStringEnumConverter());
string functionPath = Path.Combine(source, "functions.json");
using var functionDocument = JsonDocument.Parse(File.Exists(functionPath) ? File.ReadAllText(functionPath) : "{\"functions\":[]}");
if (File.Exists(functionPath) && (functionDocument.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
    functionDocument.RootElement.GetProperty("binaryHash").GetString() != NativeFieldCatalog.CapturedNativeHash))
    throw new InvalidOperationException("Function metadata schema/image does not match the compiled field catalog.");
var documents = files.Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
    .Select(p => new { name = Path.GetFileName(p), text = File.ReadAllText(p) }).ToList();
documents.Add(new { name = "API contracts", text = string.Join("\n\n", files.Where(p => p.EndsWith(".cs"))
    .Select(p => Path.GetFileName(p) + "\n" + string.Join("\n", Regex.Matches(File.ReadAllText(p), @"(?m)^\s*/// ?(.*)$")
        .Select(m => m.Groups[1].Value)))) });
var value = new {
    schemaVersion = 1,
    nativeHash = NativeFieldCatalog.CapturedNativeHash,
    analysisGeneration = NativeFieldCatalog.CapturedAnalysisGeneration,
    baselineExtenderCommit = NativeFieldCatalog.BaselineExtenderCommit,
    capturedSourceCommit = NativeFieldCatalog.CapturedSourceCommit,
    sourceFiles = files.Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }),
    records = typeof(NativeRecordTypes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string)).ToDictionary(f => (string)f.GetRawConstantValue(), f => f.Name),
    fields = NativeFieldCatalog.Default.Fields,
    functions = functionDocument.RootElement.GetProperty("functions"),
    documents
};
File.WriteAllText(args[1], JsonSerializer.Serialize(value, options).Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n");
Console.WriteLine($"KNOWLEDGE_EXPORT_COMPLETE fields={NativeFieldCatalog.Default.Fields.Count}");
