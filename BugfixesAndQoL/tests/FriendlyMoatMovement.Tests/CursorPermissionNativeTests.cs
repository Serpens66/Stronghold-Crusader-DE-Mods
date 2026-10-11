using Iced.Intel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RedBird.X64.Assembly;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

internal static class CursorPermissionNativeTests
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate long Decision(IntPtr selection, long original, int site, int permission);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate long Entry();
    private static readonly List<object> roots = new();

    internal static void Validate(string root)
    {
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/UnitCommands/Integration/AssassinSelectionAdapters.cs")));
        var methods = syntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var emitter = methods.Single(m => m.Identifier.Text == "EmitSelectionCallAdapter");
        string source = "using System; using Iced.Intel; using static Iced.Intel.AssemblerRegisters; " +
            "public static class ActualCursorEmitter {" + emitter.ToFullString() +
            "public static void Emit(Assembler a, Instruction[] i, ulong c, int s) => EmitSelectionCallAdapter(a,i,c,s);}";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Assembler).Assembly.Location).Distinct().Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualCursorEmitter",
            new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        var emit = Assembly.Load(output.ToArray()).GetType("ActualCursorEmitter")!.GetMethod("Emit")!
            .CreateDelegate<Action<Assembler, Instruction[], ulong, int>>();

        // Verify the exact producer bytes consumed by the production validator.
        using var native = File.OpenRead(@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
        using var pe = new PEReader(native);
        var producers = methods.Single(m => m.Identifier.Text == "AddSelectionCallAdapters")
            .DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString() == "ValidateExactBytes").Take(2).ToArray();
        if (producers.Length != 2) throw new Exception("Missing cursor permission producer validation");
        foreach (var producer in producers)
        {
            int rva = (int)((LiteralExpressionSyntax)producer.ArgumentList.Arguments[1].Expression).Token.Value!;
            var values = producer.ArgumentList.Arguments[2].Expression.DescendantNodes().OfType<LiteralExpressionSyntax>()
                .Select(n => Convert.ToByte(n.Token.Value)).ToArray();
            if (!pe.GetSectionData(rva).GetContent(0, values.Length).SequenceEqual(values))
                throw new Exception("Native cursor permission producer differs at " + rva.ToString("X"));
        }
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(pe.GetSectionData(0x8F206).GetContent(0, 0x8F325-0x8F206).ToArray()));
        decoder.IP = 0x18008F206;
        while (decoder.IP < 0x18008F325)
        {
            var instruction = decoder.Decode();
            if (instruction.IsInvalid || instruction.Op0Register is Register.R15 or Register.R15D)
                throw new Exception("Ground permission register is no longer preserved before its consumer");
        }

        int executions = 0;
        foreach (int site in new[] { 0x8D724, 0x8E2B8, 0x8E550, 0x8F325, 0xB7161, 0xB7321 })
        foreach (int permission in new[] { 0, 1 })
        foreach (long originalResult in new[] { 0L, 7L, 0x100000001L, -1L })
        {
            var original = ExecutableFunction.Create(a => { a.mov(rax, unchecked((ulong)originalResult)); a.ret(); });
            int calls = 0;
            Decision decision = (selection, observedResult, observedSite, observedPermission) =>
            {
                calls++;
                if (selection != new IntPtr(123) || observedResult != originalResult || observedSite != site ||
                    observedPermission != (site == 0x8F325 ? permission : 1))
                    throw new Exception("Production adapter corrupted Win64 decision arguments");
                return observedResult;
            };
            ulong callback = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(decision).ToInt64());
            var call = Instruction.CreateBranch(Code.Call_rel32_64, original.Address);
            var fixture = ExecutableFunction.Create(a =>
            {
                // Match the aligned native callsite, preserving the caller's R15.
                a.push(r15); a.mov(r15d, permission); a.mov(rcx, 123);
                emit(a, new[] { call }, callback, site);
                a.pop(r15); a.ret();
            });
            roots.Add(original); roots.Add(decision); roots.Add(fixture);
            if (fixture.GetDelegate<Entry>()() != originalResult || calls != 1)
                throw new Exception("Production selection adapter changed result width or call count");
            executions++;
        }
        Console.WriteLine($"PASS: native ground permission producers and {executions} executed production Win64 adapters; six sites, R15D, full-width results.");
    }
}
