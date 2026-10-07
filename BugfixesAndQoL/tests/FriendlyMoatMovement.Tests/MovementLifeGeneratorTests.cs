using System.Reflection;
using System.Reflection.PortableExecutable;
using Iced.Intel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class MovementLifeGeneratorTests
{
    internal static void Validate(string root)
    {
        string path = Path.Combine(root, "BugfixesAndQoL/src/TroopMovementFix3SynchronizedMovementCadencePatch.cs");
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        var type = syntax.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "SynchronizedMovementCadencePatch");
        var methods = new HashSet<string> { "GeneratePreTerrainSpeedFastPath", "GenerateCadenceFastPath",
            "EmitProfileAddress", "EmitStateMappings", "EmitRallyRunningMappings" };
        var fields = new HashSet<string> { "rallyEntries", "synchronizationEntries", "nativeProfiles",
            "rallyEnabledFlag", "synchronizationEnabledFlag", "currentUnitIdAddress", "improvedSpearmanFlagAddress" };
        var members = type.Members.Where(m => m is MethodDeclarationSyntax method && methods.Contains(method.Identifier.Text) ||
            m is FieldDeclarationSyntax field && (field.Modifiers.Any(SyntaxKind.ConstKeyword) ||
                field.Declaration.Variables.Any(v => fields.Contains(v.Identifier.Text))));
        string source = "using System; using Iced.Intel; using SHCDESE.Interop; using SHCDESE.Interop.Enums; " +
            "using static Iced.Intel.AssemblerRegisters; public unsafe class ActualMovementGenerator {" +
            string.Join("\n", members.Select(m => m.ToFullString())) + @"
            public static byte[][] Emit(Instruction[] speed, Instruction[] cadence, ulong address) {
                var fixture = new ActualMovementGenerator();
                var a = new Assembler(64);
                fixture.GeneratePreTerrainSpeedFastPath(a, speed, speed[3].NextIP);
                var b = new Assembler(64);
                fixture.GenerateCadenceFastPath(b, cadence, cadence[2].NextIP);
                var aw = new Writer(); a.Assemble(aw, address);
                var bw = new Writer(); b.Assemble(bw, address);
                return new[] { aw.ToArray(), bw.ToArray() };
            }
            private sealed class Writer : CodeWriter {
                private readonly System.Collections.Generic.List<byte> bytes = new System.Collections.Generic.List<byte>();
                public override void WriteByte(byte value) { bytes.Add(value); }
                public byte[] ToArray() => bytes.ToArray();
            }}";
        string extender = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Assembler).Assembly.Location).Append(extender).Distinct()
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualMovementLifeGenerators", new[] { CSharpSyntaxTree.ParseText(source) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        string gameDll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        using var stream = File.OpenRead(gameDll);
        using var pe = new PEReader(stream);
        ulong imageBase = pe.PEHeaders.PEHeader!.ImageBase;
        var speed = Original(0x19B506, 4);
        // The production pattern resolver establishes this common dispatcher site.
        int cadenceRva = (int)type.Members.OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables)
            .Where(v => v.Identifier.Text == "MovementCadenceRva")
            .Select(v => Convert.ToInt32(v.Initializer!.Value.ToString(), 16)).Single();
        var cadence = Original(cadenceRva, 3);
        ulong address = imageBase + 0x4000000;
        var fixture = Assembly.Load(output.ToArray()).GetType("ActualMovementGenerator")!;
        byte[][] stubs;
        try { stubs = (byte[][])fixture.GetMethod("Emit")!.Invoke(null, new object[] { speed, cadence, address })!; }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        foreach (byte[] bytes in stubs)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
            bool marker = false;
            while (decoder.IP < address + (uint)bytes.Length)
            {
                decoder.Decode(out var instruction);
                if (instruction.IsInvalid) throw new Exception("Invalid instruction in actual movement generator.");
                if (instruction.Mnemonic == Mnemonic.Cmp && instruction.MemoryDisplacement64 == 0x8F8)
                {
                    if (instruction.MemorySize != MemorySize.UInt16) throw new Exception("Death marker must be WORD, not DWORD.");
                    decoder.Decode(out var branch);
                    if (branch.Mnemonic != Mnemonic.Jne) throw new Exception("Missing Vanilla replay branch after death marker.");
                    marker = true;
                }
            }
            if (!marker) throw new Exception("Actual movement generator omitted the death-marker guard.");
        }
        Console.WriteLine("PASS: both complete production movement generators assembled and decoded; WORD death guards branch to replay.");

        Instruction[] Original(int rva, int count)
        {
            var bytes = pe.GetSectionData(rva).GetContent(0, 64).ToArray();
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = imageBase + (uint)rva;
            var instructions = new Instruction[count];
            for (int i = 0; i < count; i++) { decoder.Decode(out instructions[i]); if (instructions[i].IsInvalid) throw new Exception("Invalid native source."); }
            return instructions;
        }
    }
}
