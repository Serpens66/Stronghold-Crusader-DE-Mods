using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Iced.Intel.AssemblerRegisters;

internal static unsafe class RallyTerrainGeneratorTests
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Execute();
    [DllImport("kernel32")] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);

    internal static void Validate(string root)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            throw new Exception("Rally terrain native tests require Windows x64.");
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/TroopMovementFix3SynchronizedMovementCadencePatch.cs")));
        var type = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "SynchronizedMovementCadencePatch");
        var methods = new HashSet<string> { "GenerateTerrainCaptureFastPath", "EmitTerrainRestore", "EmitTerrainEligibility",
            "GeneratePreTerrainSpeedFastPath", "GenerateCadenceFastPath", "EmitProfileAddress", "EmitStateMappings", "EmitRallyRunningMappings",
            "ValidateTerrainCaptureHook", "IsNearBranch", "SetTerrainEnabled" };
        var fields = new HashSet<string> { "terrainSnapshots", "terrainEnabledFlag", "currentUnitIdAddress",
            "rallyEntries", "rallyEnabledFlag", "synchronizationEntries", "synchronizationEnabledFlag", "nativeProfiles", "improvedSpearmanFlagAddress", "terrainAvailable" };
        var members = type.Members.Where(m => m is MethodDeclarationSyntax method && methods.Contains(method.Identifier.Text) ||
            m is FieldDeclarationSyntax field && (field.Modifiers.Any(SyntaxKind.ConstKeyword) ||
                field.Declaration.Variables.Any(v => fields.Contains(v.Identifier.Text))));
        string source = "using System; using System.Threading; using Iced.Intel; using SHCDESE.Interop; using SHCDESE.Interop.Enums; " +
            "using static Iced.Intel.AssemblerRegisters; public unsafe class TerrainFixture {" +
            string.Join("\n", members.Select(m => m.ToFullString().Replace("private readonly ulong", "private ulong"))) + @"
            public static void Audit(byte[] image, ulong baseAddress) {
                ValidateTerrainCaptureHook(image, baseAddress, 0x18410C);
            }
            public static void Toggle(ulong flags) {
                var f = new TerrainFixture(); f.terrainEnabledFlag = (int*)flags;
                f.terrainAvailable = true; *f.terrainEnabledFlag = 0;
                f.SetTerrainEnabled(true); f.SetTerrainEnabled(true);
                if (*f.terrainEnabledFlag != 1) throw new Exception(""Enable is not idempotent"");
                f.SetTerrainEnabled(false); f.SetTerrainEnabled(true);
                if (*f.terrainEnabledFlag != 3) throw new Exception(""Toggle did not invalidate epoch"");
                f.terrainAvailable = false; f.SetTerrainEnabled(true);
                if (*f.terrainEnabledFlag != 4) throw new Exception(""Unavailable terrain fix enabled"");
            }
            public static byte[] Emit(int mode, ulong address, ulong unit, ulong flags, ulong entries,
                ulong tracking, ulong profiles, ulong handler, Instruction[] original) {
                var f = new TerrainFixture();
                f.terrainSnapshots = (byte*)entries; f.terrainEnabledFlag = (int*)flags;
                f.currentUnitIdAddress = flags + 4; f.rallyEnabledFlag = (int*)(flags + 8);
                f.synchronizationEnabledFlag = (int*)(flags + 12); f.improvedSpearmanFlagAddress = flags + 16;
                f.rallyEntries = (byte*)tracking; f.nativeProfiles = (byte*)profiles;
                f.synchronizationEntries = (byte*)tracking;
                var a = new Assembler(64);
                a.push(rbx); a.push(r14); a.sub(rsp, 0x28);
                a.mov(rbx, unit); a.mov(r14, handler - 0x321CB0);
                a.mov(eax, 26); a.xor(ecx, ecx); a.mov(edx, 0x12345678); a.stc();
                if (mode == 0) {
                    var load = original[1]; load.MemoryDisplacement64 = flags + 4;
                    f.GenerateTerrainCaptureFastPath(a, new[] { original[0], load }, original[0].IP + 14);
                    a.mov(r8, rbx);
                    f.GenerateCadenceFastPath(a, original.AsSpan(2, 3), 1);
                } else if (mode == 1) {
                    a.mov(r8, rbx); f.EmitTerrainRestore(a);
                } else {
                    f.GeneratePreTerrainSpeedFastPath(a, new[] { Instruction.Create(Code.Nopd),
                        Instruction.Create(Code.Nopd), Instruction.Create(Code.Nopd), Instruction.Create(Code.Nopd) }, 1);
                }
                a.add(rsp, 0x28); a.pop(r14); a.pop(rbx); a.ret();
                var w = new Writer(); a.Assemble(w, address); return w.Bytes.ToArray();
            }
            private sealed class Writer : CodeWriter {
                public readonly System.Collections.Generic.List<byte> Bytes = new System.Collections.Generic.List<byte>();
                public override void WriteByte(byte value) { Bytes.Add(value); }
            }}";
        const string extender = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Assembler).Assembly.Location).Append(Path.Combine(extender, "SHCDESE.dll"))
            .Distinct().Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualRallyTerrainGenerators", new[] { CSharpSyntaxTree.ParseText(source) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        var fixture = Assembly.Load(output.ToArray()).GetType("TerrainFixture")!;
        using var file = File.OpenRead(@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
        using var pe = new PEReader(file);
        Instruction[] dispatch = Decode(pe.GetSectionData(0x18410C).GetContent(0, 32).ToArray(), pe.PEHeaders.PEHeader!.ImageBase + 0x18410C, 2);
        Instruction[] cadence = Decode(pe.GetSectionData(0x184203).GetContent(0, 32).ToArray(), pe.PEHeaders.PEHeader.ImageBase + 0x184203, 3);
        var original = dispatch.Concat(cadence).ToArray();
        byte[] auditedImage = new byte[0x184F00];
        pe.GetSectionData(0x182B00).GetContent(0, 0x23B1).CopyTo(auditedImage, 0x182B00);
        fixture.GetMethod("Audit")!.Invoke(null, new object[] { auditedImage, pe.PEHeaders.PEHeader.ImageBase });
        // Probe the same installed inline backend over copied source bytes.
        IntPtr copy = Marshal.AllocHGlobal(64);
        try
        {
            Marshal.Copy(pe.GetSectionData(0x18410C).GetContent(0, 64).ToArray(), 0, copy, 64);
            var backend = Assembly.LoadFrom(Path.Combine(extender, "RedBird.X64.dll")).GetType("RedBird.X64.Hooks.X64InlineHook")!;
            using var probe = (IDisposable)Activator.CreateInstance(backend,
                new object[] { (ulong)copy.ToInt64(), 14, null!, "rally terrain decode-only test" })!;
            Check((int)backend.GetProperty("DisplacedByteCount")!.GetValue(probe)! == 14, "installed backend displacement");
        }
        finally { Marshal.FreeHGlobal(copy); }

        IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x100000, 0x3000, 0x40);
        if (memory == IntPtr.Zero) throw new Exception("Native fixture allocation failed.");
        try
        {
            ulong start = (ulong)memory.ToInt64();
            byte* unit = (byte*)memory;
            byte* flags = unit + 0x2000;
            byte* snapshots = unit + 0x3000;
            byte* tracking = unit + 0x4000;
            byte* profiles = unit + 0x6000;
            fixture.GetMethod("Toggle")!.Invoke(null, new object[] { (ulong)flags });
            ulong table = start + 0x20000, handler = start + 0x21000;
            *(ulong*)(table + 26 * 8) = handler;
            // Native handler records invocation and the original call's inputs,
            // then reproduces Vanilla's bad effective-delay reset.
            var h = new Assembler(64);
            h.pushfq(); h.pop(r10); h.mov(__qword_ptr[rbx + 0x1000], r10);
            h.mov(__dword_ptr[rbx + 0x1008], eax); h.mov(__dword_ptr[rbx + 0x100C], ecx);
            h.mov(__dword_ptr[rbx + 0x1010], edx);
            h.inc(__dword_ptr[rbx + 0x1014]);
            h.mov(ax, __word_ptr[rbx + 0x9A4]); h.mov(__word_ptr[rbx + 0x9A2], ax); h.ret();
            Copy(h, handler);
            foreach (bool terrain in new[] { false, true })
            foreach (bool running in new[] { false, true })
            foreach (ushort delay in new ushort[] { 4, 5, 7, 8, 10 })
            {
                Reset(terrain, running, delay);
                Run(0);
                Check(*(ushort*)(unit + 0x9A2) == (terrain ? delay : 4), "four-setting terrain matrix");
                Check(*(ushort*)(unit + 0x916) == (running ? 1 : 0), "independent running bonus");
                Check(*(uint*)(unit + 0x660) == (running ? 2u : 1u), "independent animation");
                Check(*(int*)(unit + 0x1014) == 1, "original handler exactly once");
                Check((*(ulong*)(unit + 0x1000) & 1) != 0 && *(int*)(unit + 0x1008) == 26 &&
                    *(int*)(unit + 0x100C) == 0 && *(int*)(unit + 0x1010) == 0x12345678,
                    "handler sees original flags and scratch registers");
                Check(snapshots[16 + 12] == 0, "snapshot consumed once");
                *(ushort*)(unit + 0x9A2) = 4; Run(1);
                Check(*(ushort*)(unit + 0x9A2) == 4, "no stale snapshot replay");
                Reset(terrain, running, delay); Run(2);
                Check(*(ushort*)(unit + 0x9A2) == (!terrain && running ? 4 : delay), "pre-terrain reset independently gated");
            }
            foreach (int owner in Enumerable.Range(0, 9))
            foreach (int kind in new[] { 5, 22, 23, 24, 25, 26, 27, 28, 29, 30, 37, 70, 71, 72, 73, 74, 75, 76, 78, 79, 80, 81, 82, 83, 84, 85 })
            {
                Reset(true, false, 10); *(ushort*)(unit + 0x6EE) = (ushort)owner;
                *(ushort*)(unit + 0x6E6) = (ushort)kind; Run(0);
                Check(*(ushort*)(unit + 0x9A2) == 10, "all audited types and owners, no spawn tracking");
            }
            foreach (string rejected in new[] { "dead", "dying", "other-state", "other-type", "global-id", "type-changed", "epoch", "disabled", "missing" })
            {
                Reset(true, false, 10);
                byte* entry = snapshots + 16;
                *(uint*)entry = 42; *(ushort*)(entry + 4) = 26; *(ushort*)(entry + 6) = 10;
                *(int*)(entry + 8) = 1; entry[12] = 1; *(ushort*)(unit + 0x9A2) = 4;
                switch (rejected) {
                    case "dead": *(ushort*)(unit + 0x6E4) = 3; break;
                    case "dying": *(ushort*)(unit + 0x8F8) = 1; break;
                    case "other-state": *(ushort*)(unit + 0x918) = 101; break;
                    case "other-type": *(ushort*)(unit + 0x6E6) = 1; break;
                    case "global-id": *(uint*)(unit + 0x6F0) = 43; break;
                    case "type-changed": *(ushort*)(unit + 0x6E6) = 27; break;
                    case "epoch": *(int*)flags = 3; break;
                    case "disabled": *(int*)flags = 2; break;
                    case "missing": entry[12] = 0; break;
                }
                Run(1); Check(*(ushort*)(unit + 0x9A2) == 4 && entry[12] == 0, "restore rejects " + rejected);
            }
            Reset(true, false, 10); *(uint*)(unit + 0x8F8) = 0xFFFF0000; Run(0);
            Check(*(ushort*)(unit + 0x9A2) == 10, "upper death-marker word ignored");
            Reset(true, false, 10); *(ushort*)(unit + 0x918) = 101; Run(0);
            Check(*(ushort*)(unit + 0x9A2) == 4 && snapshots[28] == 0, "capture rejects non-rally state");
            foreach (string rejected in new[] { "dead", "dying", "other-type", "invalid-id" }) {
                Reset(true, false, 10);
                if (rejected == "dead") *(ushort*)(unit + 0x6E4) = 3;
                if (rejected == "dying") *(ushort*)(unit + 0x8F8) = 1;
                if (rejected == "other-type") *(ushort*)(unit + 0x6E6) = 1;
                if (rejected == "invalid-id") *(int*)(flags + 4) = 10001;
                Run(0);
                Check(*(ushort*)(unit + 0x9A2) == 4 && *(int*)(unit + 0x1014) == 1,
                    "capture rejects " + rejected + " and still runs handler once");
            }
            Console.WriteLine("PASS: production rally capture/restore executed: 26 types, all owners, settings matrix, original call/registers/flags, identity/state/epoch rejection and single consumption.");

            void Reset(bool enabled, bool run, ushort delay)
            {
                new Span<byte>(unit, 0x2000).Clear(); new Span<byte>(flags, 32).Clear();
                new Span<byte>(snapshots, 64).Clear(); new Span<byte>(tracking, 64).Clear();
                new Span<byte>(profiles, 0x10000).Clear();
                *(int*)flags = enabled ? 1 : 0; *(int*)(flags + 4) = 1; *(int*)(flags + 8) = run ? 1 : 0;
                *(ushort*)(unit + 0x6E4) = 2; *(ushort*)(unit + 0x6E6) = 26;
                *(ushort*)(unit + 0x6EE) = 1; *(uint*)(unit + 0x6F0) = 42;
                *(ushort*)(unit + 0x918) = 105; *(ushort*)(unit + 0x74E) = 2;
                *(ushort*)(unit + 0x9A4) = 4; *(ushort*)(unit + 0x9A2) = delay; *(uint*)(unit + 0x660) = 1;
                byte* tracked = tracking + 32; *(int*)tracked = 1; *(uint*)(tracked + 4) = 42;
                *(ushort*)(tracked + 8) = 26; tracked[14] = 1; tracked[15] = 1; tracked[16] = 1;
                // NativeProfileSize is 272 bytes; running mapping 1 -> 2, bonus 1.
                byte* profile = profiles + 26 * 272; profile[0] = 1; *(ushort*)(profile + 4) = 1;
                *(uint*)(profile + 16) = 1; *(uint*)(profile + 20) = 2;
            }
            void Run(int mode)
            {
                ulong address = start + 0x30000;
                byte[] bytes;
                try { bytes = (byte[])fixture.GetMethod("Emit")!.Invoke(null,
                    new object[] { mode, address, start, (ulong)flags, (ulong)snapshots, (ulong)tracking, (ulong)profiles, table, original })!; }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                DecodeAll(bytes, address); Marshal.Copy(bytes, 0, (IntPtr)(long)address, bytes.Length);
                Marshal.GetDelegateForFunctionPointer<Execute>((IntPtr)(long)address)();
            }
        }
        finally { VirtualFree(memory, UIntPtr.Zero, 0x8000); }
    }
    private static void Check(bool valid, string name) { if (!valid) throw new Exception("Rally terrain: " + name); }
    private static Instruction[] Decode(byte[] bytes, ulong ip, int count)
    {
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = ip;
        var instructions = new Instruction[count];
        for (int i = 0; i < count; i++) { instructions[i] = decoder.Decode(); Check(!instructions[i].IsInvalid, "source decode"); }
        return instructions;
    }
    private static void DecodeAll(byte[] bytes, ulong ip)
    {
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = ip;
        while (decoder.IP < ip + (uint)bytes.Length) Check(!decoder.Decode().IsInvalid, "complete stub decode");
    }
    private static void Copy(Assembler assembler, ulong address)
    {
        var writer = new Writer(); assembler.Assemble(writer, address);
        Marshal.Copy(writer.Bytes.ToArray(), 0, (IntPtr)(long)address, writer.Bytes.Count);
    }
    private sealed class Writer : CodeWriter
    {
        internal readonly List<byte> Bytes = new();
        public override void WriteByte(byte value) => Bytes.Add(value);
    }
}
