using BugfixesAndQoL.UnitCommands;
using Iced.Intel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Security.Cryptography;

// Offline production-code fixtures. They do not assert Unity Mono/audio or actual multiplayer acceptance.
string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
FormationSpeechRequest Request(int x = 0, int y = 0) =>
    new(1, 2, 3, 4, eChimps.CHIMP_TYPE_SWORDSMAN, 0, 0, x, y, 123);
foreach (int x in new[] { -73, -72, 0, 72, 73 })
foreach (int y in new[] { -73, -72, 0, 72, 73 })
{
    bool near = Math.Abs(x) <= 72 && Math.Abs(y) <= 72;
    var request = Request(x, y);
    Check(request.Response == (near ? 6 : 10) && request.TargetTileId == (near ? 123 : 0), "Native inclusive axis distance");
}
Check(Request(int.MinValue).Response == 10, "Distance overflow");
foreach (var type in new[] { eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_XBOWMAN,
    eChimps.CHIMP_TYPE_CATAPULT, eChimps.CHIMP_TYPE_TREBUCHET, eChimps.CHIMP_TYPE_MANGONEL, eChimps.CHIMP_TYPE_BALLISTA })
    Check(!FormationSpeechRequest.AllowsVoice(type, TribeMoveType.NoChange), "Native exclusion " + type);
Check(FormationSpeechRequest.AllowsVoice(eChimps.CHIMP_TYPE_ARCHER_debug, TribeMoveType.NoChange), "Vanilla debug archer exception");
Check(FormationSpeechRequest.AllowsVoice(eChimps.CHIMP_TYPE_SWORDSMAN, TribeMoveType.DefaultInSync), "Swordsman allowed");
Check((int)TribeMoveType.NoChange == 0 && (int)TribeMoveType.DefaultInSync == 1 &&
    (int)TribeMoveType.Fast == -255, "Installed movement mode contract");
foreach (eChimps voice in Enum.GetValues<eChimps>())
foreach (TribeMoveType mode in new[] { TribeMoveType.NoChange, TribeMoveType.DefaultInSync, TribeMoveType.Fast })
{
    int v = (int)voice;
    bool expected = v != 40 && v != 41 && v != 61 &&
        !(mode == TribeMoveType.NoChange && (v == 22 || v == 23 || v == 39));
    Check(FormationSpeechRequest.AllowsVoice(voice, mode) == expected, "Native voice/mode matrix");
}
Check(System.Runtime.InteropServices.Marshal.OffsetOf<GameUnit>(nameof(GameUnit.N000000C4)).ToInt32() == 0x388,
    "Installed engineer exclusion field");
// These values are an audited machine contract, verified against the real referenced Extender enum.
Check((int)eChimps.CHIMP_TYPE_ARCHER == 22 && (int)eChimps.CHIMP_TYPE_XBOWMAN == 23 &&
    (int)eChimps.CHIMP_TYPE_ARCHER_debug == 38 && (int)eChimps.CHIMP_TYPE_CATAPULT == 39 &&
    (int)eChimps.CHIMP_TYPE_TREBUCHET == 40 && (int)eChimps.CHIMP_TYPE_MANGONEL == 41 &&
    (int)eChimps.CHIMP_TYPE_BALLISTA == 61 && (int)eChimps.CHIMP_TYPE_ENGINEER == 30, "Installed enum machine contract");

var queue = new FormationSpeechQueue();
int emitted = 0, failures = 0;
queue.Enqueue(Request());
queue.OnNativeFrame(false, _ => true, _ => emitted++, _ => failures++);
Check(emitted == 0, "No buffer/no active native frame retains feedback");
queue.OnNativeFrame(true, _ => true, _ => emitted++, _ => failures++);
queue.OnNativeFrame(true, _ => true, _ => emitted++, _ => failures++);
Check(emitted == 1, "Exactly once across render callbacks");
queue.Enqueue(Request()); queue.OnNativeFrame(true, _ => false, _ => emitted++, _ => failures++);
queue.OnNativeFrame(true, _ => true, _ => emitted++, _ => failures++);
Check(emitted == 1, "Invalid context consumed");
queue.Enqueue(Request()); queue.Enqueue(Request());
queue.OnNativeFrame(true, _ => true, _ => throw new Exception("audio"), _ => { failures++; queue.Clear(); });
Check(failures == 1, "Audio disable clears remaining entries without another dequeue");
queue.Enqueue(Request()); queue.Clear(); queue.OnNativeFrame(true, _ => true, _ => emitted++, _ => failures++);
Check(emitted == 1, "Mission reset discards pending feedback");
long now = 0;
var latest = new FormationSpeechQueue(() => now, 1000);
for (int i = 0; i < 1300; i++) latest.Enqueue(Request(i, 0));
latest.OnNativeFrame(true, _ => true, r => Check(r.Response == 10, "Latest gesture retained"), _ => failures++);
Check(!latest.HasPending, "No backlog after rapid commands");
latest.Enqueue(Request()); now = 1000;
latest.OnNativeFrame(true, _ => true, _ => throw new Exception("Expired voice emitted"), _ => failures++);
Check(failures == 1 && !latest.HasPending, "One-second monotonic expiry");
latest.Enqueue(Request()); latest.Enqueue(null);
Check(!latest.HasPending, "Ineligible new gesture clears old cue");

string nativePath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
byte[] raw = File.ReadAllBytes(nativePath);
Check(Convert.ToHexString(SHA256.HashData(raw)) == "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2", "Canonical native hash");
// Bind private members by reflection only, and prove the contract against the REAL game assembly.
using (var managed = new PEReader(File.OpenRead(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(nativePath))), "Managed", "Assembly-CSharp.dll"))))
{
    var metadata = managed.GetMetadataReader();
    var engine = metadata.TypeDefinitions.Select(h => metadata.GetTypeDefinition(h))
        .Single(t => metadata.GetString(t.Name) == "EngineInterface" && metadata.GetString(t.Namespace) == "");
    var engineLock = engine.GetFields().Select(h => metadata.GetFieldDefinition(h)).Single(f => metadata.GetString(f.Name) == "threadLock");
    Check((engineLock.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private &&
        (engineLock.Attributes & FieldAttributes.Static) != 0 &&
        metadata.GetBlobBytes(engineLock.Signature).SequenceEqual(new byte[] { 6, 0x1C }), "Real private static object engine lock");
}
using var pe = new PEReader(new MemoryStream(raw));
var headers = pe.PEHeaders;
byte[] image = new byte[headers.PEHeader.SizeOfImage];
raw.AsSpan(0, headers.PEHeader.SizeOfHeaders).CopyTo(image);
foreach (var section in headers.SectionHeaders)
    raw.AsSpan(section.PointerToRawData, section.SizeOfRawData).CopyTo(image.AsSpan(section.VirtualAddress));
for (int n = 0; n < FormationSpeechNative.Patterns.Length; n++)
{
    int rva = FormationSpeechNative.ReferenceRvas[n];
    string pattern = FormationSpeechNative.Patterns[n];
    Check(FormationSpeechNative.Resolve(image, rva, pattern, out string method) == rva && method == "reference-rva", "Native entry unique");
    byte[] bytes = image.AsSpan(rva, pattern.Split(' ').Length).ToArray();
    var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
    int decoded = 0;
    while (decoded < bytes.Length) { decoder.Decode(out var instruction); Check(!instruction.IsInvalid, "Valid instruction"); decoded += instruction.Length; }
    Check(decoded == bytes.Length, "Whole-instruction signature");
    // A valid executable fallback still resolves when the requested reference is wrong.
    Check(FormationSpeechNative.Resolve(image, rva + 1, pattern, out method) == rva && method == "executable-pattern-fallback", "Reference failure fallback");
    var code = headers.SectionHeaders.First(s => ((int)s.SectionCharacteristics & 0x20000000) != 0);
    int duplicate = code.VirtualAddress + code.VirtualSize - bytes.Length;
    byte[] saved = image.AsSpan(duplicate, bytes.Length).ToArray();
    bytes.CopyTo(image, duplicate);
    try { FormationSpeechNative.Resolve(image, rva, pattern, out _); Check(false, "Ambiguity accepted"); }
    catch (InvalidOperationException) { Check(true, "Ambiguity rejected"); }
    saved.CopyTo(image, duplicate);
    byte original = image[rva]; image[rva] = 0xCC;
    try { FormationSpeechNative.Resolve(image, rva, pattern, out _); Check(false, "Missing target accepted"); }
    catch (InvalidOperationException) { Check(true, "Missing target rejected after fallback"); }
    image[rva] = original;
}

NativeAdapterTests.Run(image, Check);

// Compile actual release/engine/speech methods with doubles only at external boundaries.
// This exercises sequencing and failure isolation, rather than matching source strings.
string Read(string path) => File.ReadAllText(Path.Combine(root, path));
string Methods(string file, params string[] names) => string.Join("\n",
    CSharpSyntaxTree.ParseText(Read(file)).GetRoot().DescendantNodes().Where(m => m is MethodDeclarationSyntax method && names.Contains(method.Identifier.Text) || m is EnumDeclarationSyntax type && names.Contains(type.Identifier.Text)).Select(m => m.ToFullString()));
string engineMethods = Methods("BugfixesAndQoL/src/Formations/FormationRuntime.EngineAndCameraHooks.cs",
    "EngineRunHook", "RunOriginalAfterReleaseConsumed", "RunOriginalOnce", "PrepareFormationRelease", "ConsumeGestureRelease", "RunOriginalWithDeferredRelease", "FormationReleaseDisposition", "DiscardDeferredRelease");
string rendererMethod = Methods("BugfixesAndQoL/src/Formations/Markers/LargeMoveTargetMarkerRenderer.cs", "ResetDrawList");
Type enumType = typeof(eChimps);
string enumSource = "namespace SHCDESE.Interop { public enum eChimps : ushort {" +
    string.Join(",", enumType.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => f.Name + "=" + Convert.ToInt64(f.GetRawConstantValue()))) + "} }";
string fixture = Read("BugfixesAndQoL/tests/FormationSpeech.Tests/RuntimeFixture.cs.txt")
    .Replace("__ENGINE__", engineMethods).Replace("__RENDERER__", rendererMethod);
string[] sources = { fixture, enumSource, Read("BugfixesAndQoL/src/Formations/FormationRuntime.Speech.cs"),
    Read("BugfixesAndQoL/src/Formations/FormationSpeechModel.cs"), Read("BugfixesAndQoL/src/Formations/FormationNativeRunContext.cs"), Read("BugfixesAndQoL/src/Formations/FormationSpeechNative.cs"),
    Read("BugfixesAndQoL/src/Formations/FormationFailurePolicy.cs") };
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
    .Where(p => Path.GetDirectoryName(p) == Path.GetDirectoryName(typeof(object).Assembly.Location))
    .Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("SpeechRuntimeCases", sources.Select(s => CSharpSyntaxTree.ParseText(s)),
    references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
using var output = new MemoryStream();
var result = compilation.Emit(output);
if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
try { Assembly.Load(output.ToArray()).GetType("BugfixesAndQoL.UnitCommands.FormationRuntime").GetMethod("Run").Invoke(null, null); }
catch (TargetInvocationException ex) { throw ex.InnerException; }
Console.WriteLine($"PASS: {assertions} speech/model/native checks; production engine, capture, frame and renderer fixtures passed.");
