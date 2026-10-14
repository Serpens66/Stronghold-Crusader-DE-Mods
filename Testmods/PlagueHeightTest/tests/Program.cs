using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using PlagueHeightTest;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

internal static class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Surface(int tile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Invoke();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long NativeDamage(IntPtr manager, int unit, int projectile, int radius);
    [DllImport("kernel32")] private static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr n, uint allocation, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(IntPtr p, UIntPtr n, uint mode);
    private static int checks;
    private static readonly System.Collections.Generic.List<PreparedNativeCode> exits = new System.Collections.Generic.List<PreparedNativeCode>();
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    private static byte[] Hex(string s) => Convert.FromHexString(s);
    private static void Put(IntPtr module, int offset, int value) => Marshal.WriteInt32(module, offset, value);
    private static void Word(IntPtr module, int offset, int value) => Marshal.WriteInt16(module, offset, (short)value);
    private static void Tile(IntPtr module, int tile, int height, TilePropertyFlag flags, int building = 0)
    {
        Marshal.WriteByte(module, PlagueHeightAssembly.HeightGrid + tile, (byte)height);
        Put(module, PlagueHeightAssembly.PropertiesGrid + tile * 4, (int)flags);
        Word(module, PlagueHeightAssembly.BuildingGrid + tile * 2, building);
    }
    private static void Main(string[] args)
    {
        if (IntPtr.Size != 8) throw new Exception("Tests require x64.");
        foreach (var item in new[] {
            (typeof(X64InlineHook).Assembly, "E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB"),
            (typeof(RedBird.Core.Memory.ScanRegion).Assembly, "EE4B036C486077F7D8898AE054F124EDDE73E6AE1A059E8991C361E2B720D29F"),
            (typeof(RedBird.Abstractions.Hooks.IHook).Assembly, "339CECE116F66F59ECC7DA5E9A5C0B2DFE24F4E7A5E7EFC005864FA77C027A1E"),
            (typeof(Assembler).Assembly, "CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F") })
            Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(item.Item1.Location))) == item.Item2,
                "actual loaded backend assembly: " + item.Item1.GetName().Name);
        ValidateRuntimeCompilation();
        var native = File.ReadAllBytes(args[0]);
        byte[] Get(int rva, int length)
        {
            int pe = BitConverter.ToInt32(native, 0x3C), sections = BitConverter.ToInt16(native, pe + 6);
            int table = pe + 24 + BitConverter.ToInt16(native, pe + 20);
            for (int i = 0; i < sections; i++)
            {
                int s = table + i * 40, va = BitConverter.ToInt32(native, s + 12), size = BitConverter.ToInt32(native, s + 16);
                if (rva >= va && rva + length <= va + size) return native.Skip(BitConverter.ToInt32(native, s + 20) + rva - va).Take(length).ToArray();
            }
            throw new Exception("RVA outside file section.");
        }
        Check(Marshal.SizeOf<GameUnit>() == 0x490 && Marshal.SizeOf<GameProjectile>() == 0xE8 && Marshal.SizeOf<GameBuilding>() == 0x32C, "real installed Extender sizes");
        Check(Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_HeightElevation)).ToInt32() == 0xB6 &&
              Marshal.OffsetOf<GameUnit>(nameof(GameUnit.N0000006A)).ToInt32() == 0xB8, "unit height offsets");
        Check((int)ProjectileType.Disease == 22 && (int)AliveState.IsAlive == 2, "installed domain enums");
        var module = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x7000000, 0x3000, 0x40);
        var state = Marshal.AllocHGlobal(128); var capture = Marshal.AllocHGlobal(128);
        if (module == IntPtr.Zero) throw new Exception("Fixture allocation failed.");
        try
        {
            for (int i = 0; i < 128; i += 8) { Marshal.WriteInt64(state, i, 0); Marshal.WriteInt64(capture, i, 0); }
            ulong image = (ulong)module.ToInt64(), data = (ulong)state.ToInt64();
            var rooftop = Get(0xC07C0, 0xBF); Marshal.Copy(rooftop, 0, module + 0xC07C0, rooftop.Length);
            var helper = new PreparedNativeCode(a => PlagueHeightAssembly.GenerateSurface(a, image, 320800, 3999));
            DecodeReachable(helper);
            try
            {
                var resolve = Marshal.GetDelegateForFunctionPointer<Surface>(helper.Address);
                Tile(module, 1, 10, 0); Check(resolve(1) == 10, "ground");
                Tile(module, 1, 10, TilePropertyFlag.IsStairs); Check(resolve(1) == 22, "stairs");
                Tile(module, 1, 90, TilePropertyFlag.IsWall); Check(resolve(1) == 90, "bare wall");
                Tile(module, 1, 90, TilePropertyFlag.IsWall, 1); Check(resolve(1) == 130, "building-backed wall/gate");
                Tile(module, 1, 10, TilePropertyFlag.IsBuilding, 1); Check(resolve(1) == 12, "ordinary building");
                foreach (var pair in new[] { (40,64), (41,92), (42,190), (43,0), (44,0), (45,128), (46,128), (74,296), (75,148), (76,180), (77,192), (78,192) })
                {
                    Word(module, PlagueHeightAssembly.BuildingManager + 0x32C + 0x12E, pair.Item1);
                    Word(module, PlagueHeightAssembly.BuildingManager + 0x32C + 0x148, 10);
                    Tile(module, 1, 0, TilePropertyFlag.IsElevated, 1);
                    Check(resolve(1) == 10 + pair.Item2, "native rooftop table " + pair.Item1);
                }
                Tile(module, 1, 10, TilePropertyFlag.IsElevated, 0); Check(resolve(1) == -1, "missing rooftop building");
                Check(resolve(-1) == -1 && resolve(320800) == -1, "tile bounds");
                Tile(module, 1, 10, TilePropertyFlag.IsElevated, 4000); Check(resolve(1) == -1, "building bounds");
                Tile(module, 1, 10, TilePropertyFlag.IsStairs | TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated, 1);
                Check(resolve(1) == 22, "Vanilla branch precedence");
                for (int index = 0; index < 4; index++)
                {
                    var bytes = Hex(PlagueHeightAssembly.OriginalHex[index]); int site = PlagueHeightAssembly.Sites[index];
                    Check(Get(site, bytes.Length).SequenceEqual(bytes), "installed site bytes " + index);
                    var probeMemory = Marshal.AllocHGlobal(80);
                    try
                    {
                        for (int j = 0; j < 80; j++) Marshal.WriteByte(probeMemory, j, 0x90);
                        Marshal.Copy(bytes, 0, probeMemory, bytes.Length);
                        using (var probe = new X64InlineHook((ulong)probeMemory.ToInt64(), bytes.Length))
                            Check(probe.DisplacedByteCount == bytes.Length && !probe.IsInstalled, "installed X64InlineHook decode-only " + index);
                    }
                    finally { Marshal.FreeHGlobal(probeMemory); }
                    var original = PlagueHeightAssembly.Decode(bytes, image + (uint)site);
                    var stub = new PreparedNativeCode(a => PlagueHeightAssembly.GenerateHook(a, original, image + (uint)site + (uint)bytes.Length,
                        index, image, data, (ulong)helper.Address.ToInt64()));
                    DecodeReachable(stub);
                    try { TestHook(index, module, state, capture, stub); }
                    finally { stub.RollbackUnpublished(); }
                }
                TestFullDiseaseRoutine(module, state, helper, Get);
            }
            finally { helper.RollbackUnpublished(); }
        }
        finally { foreach (var exit in exits) exit.RollbackUnpublished(); VirtualFree(module, UIntPtr.Zero, 0x8000); Marshal.FreeHGlobal(state); Marshal.FreeHGlobal(capture); }
        Console.WriteLine("PASS: " + checks + " native execution, installed-backend, height, activation, register and stack checks; no game hooks installed.");
    }
    private static void TestFullDiseaseRoutine(IntPtr module, IntPtr state, PreparedNativeCode helper, Func<int,int,byte[]> get)
    {
        ulong image = (ulong)module.ToInt64();
        var routine = get(0x192750, 0x1939B8 - 0x192750); // complete function plus building-exclusion jump tables
        Marshal.Copy(routine, 0, module + 0x192750, routine.Length);
        var immunity = get(0x32BC40, 512); Marshal.Copy(immunity, 0, module + 0x32BC40, immunity.Length);
        var original = PlagueHeightAssembly.Decode(Hex(PlagueHeightAssembly.OriginalHex[1]), image + 0x192809);
        var stub = new PreparedNativeCode(a => PlagueHeightAssembly.GenerateHook(a, original, image + 0x192823, 1, image,
            (ulong)state.ToInt64(), (ulong)helper.Address.ToInt64()));
        try
        {
            var jump = new Assembler(64); jump.AddUnrestrictedJmp((ulong)stub.Address.ToInt64());
            var patch = PlagueHeightAssembly.Assemble(jump, image + 0x192809);
            Marshal.Copy(patch, 0, module + 0x192809, patch.Length);
            for (int i = patch.Length; i < 26; i++) Marshal.WriteByte(module, 0x192809 + i, 0x90);
            var damage = Marshal.GetDelegateForFunctionPointer<NativeDamage>(module + 0x192750);
            int unit = 0x67E8400 + 0x490, projectile = PlagueHeightAssembly.ProjectileBase + 25 * 0xE8;
            void Reset(int height, int addition, int health = 10000)
            {
                Marshal.Copy(new byte[0x490], 0, module + unit + 0x65C, 0x490);
                Word(module, unit + 0x6E6, (int)eChimps.CHIMP_TYPE_PEASANT); Word(module, unit + 0x6EE, 1);
                Word(module, unit + 0x6E4, (int)AliveState.IsAlive); Put(module, unit + 0x72C, 1);
                Word(module, unit + 0x712, height); Word(module, unit + 0x714, addition);
                Put(module, unit + 0xA20, health); Put(module, unit + 0xA24, 10000);
                Word(module, projectile + 0x3E, (int)ProjectileType.Disease); Word(module, projectile + 0xB6, 0); Put(module, projectile + 0x60, 1);
            }
            byte[] Record() { var bytes = new byte[0x490]; Marshal.Copy(module + unit + 0x65C, bytes, 0, bytes.Length); return bytes; }
            foreach (int radius in new[] { 0, 1, 2 })
            foreach (int resistance in new[] { 0, 1, 2, 3 })
            foreach (int hp in new[] { 100, 10000 })
            {
                Tile(module, 1, 100, 0); Put(module, 0x379E6D0 + 0x583C, resistance); Reset(100, 0, hp);
                Marshal.WriteInt32(state, 0); damage(module + 0x67E8400, 1, 25, radius); var vanilla = Record();
                Reset(100, 0, hp); Marshal.WriteInt32(state, 1); damage(module + 0x67E8400, 1, 25, radius);
                Check(vanilla.SequenceEqual(Record()), "full native unit record parity: radius/resistance/hp " + radius + "/" + resistance + "/" + hp);
                int expectedDamage = radius == 0 ? 400 : radius == 1 ? 200 : 150;
                if (resistance != 0) expectedDamage = expectedDamage * (resistance == 1 ? 80 : resistance == 2 ? 60 : 40) / 100;
                Check(Marshal.ReadInt32(module, unit + 0xA20) == Math.Max(0, hp - expectedDamage), "native damage/resistance value");
                Check(Marshal.ReadInt16(module, unit + 0x8F8) == (hp <= expectedDamage ? 1 : 0), "native death marker");
            }
            Put(module, 0x379E6D0 + 0x583C, 0);
            foreach (int type in Enumerable.Range(40, 8).Concat(Enumerable.Range(74, 5)))
            {
                Word(module, PlagueHeightAssembly.BuildingManager + 0x32C + 0x12E, type);
                Tile(module, 1, 100, TilePropertyFlag.IsBuilding, 1); Reset(100, 2);
                Marshal.WriteInt32(state, 0); damage(module + 0x67E8400, 1, 25, 0);
                Check(Marshal.ReadInt32(module, unit + 0xA20) == 10000, "Vanilla building exemption " + type);
                Marshal.WriteInt32(state, 1); damage(module + 0x67E8400, 1, 25, 0);
                Check(Marshal.ReadInt32(module, unit + 0xA20) == 9600, "building exemption removed " + type);
            }
            Tile(module, 1, 90, TilePropertyFlag.IsWall, 1); Reset(90, 40); Marshal.WriteInt32(state, 1);
            damage(module + 0x67E8400, 1, 25, 1); Check(Marshal.ReadInt32(module, unit + 0xA20) == 9800, "actual wall damage, full function");
            Reset(90, 0); damage(module + 0x67E8400, 1, 25, 1); Check(Marshal.ReadInt32(module, unit + 0xA20) == 10000, "wall cloud rejects lower unit, full function");
            Tile(module, 1, 100, 0); Reset(100, 0); Word(module, projectile + 0xB6, 1);
            damage(module + 0x67E8400, 1, 25, 0); Check(Marshal.ReadInt32(module, unit + 0xA20) == 10000, "native source-unit self exclusion preserved");
            Word(module, projectile + 0xB6, 0); Word(module, unit + 0x8F8, 1);
            damage(module + 0x67E8400, 1, 25, 0); Check(Marshal.ReadInt32(module, unit + 0xA20) == 10000, "native dead-unit exclusion preserved");
            Reset(100, 0); Word(module, unit + 0x6E6, (int)eChimps.CHIMP_TYPE_NULL);
            damage(module + 0x67E8400, 1, 25, 0); Check(Marshal.ReadInt32(module, unit + 0xA20) == 10000, "native unit-type immunity preserved");
            Reset(100, 0); Word(module, projectile + 0x3E, (int)ProjectileType.ArabGrenadierOrBedouinAmbusherOrDiseaseCloud);
            long before = Marshal.ReadInt64(state, 8); damage(module + 0x67E8400, 1, 25, 0);
            Check(Marshal.ReadInt32(module, unit + 0xA20) == 10000 && Marshal.ReadInt64(state, 8) == before, "other projectile's original early exclusion bypasses height hook");
            Console.WriteLine("PASS: full native disease damage routine: exact unit-record parity, radius/resistance, death state, all building exemptions, wall height, self/dead rejection.");
        }
        finally { stub.RollbackUnpublished(); }
    }
    private static void ValidateRuntimeCompilation()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var xml = System.Xml.Linq.XDocument.Load(Path.Combine(root, "PlagueHeightTest.csproj"));
        var paths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var framework = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1";
        foreach (var name in new[] { "mscorlib.dll", "System.dll", "System.Core.dll", "Facades/netstandard.dll" }) paths.Add(Path.Combine(framework, name));
        var game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        foreach (var node in xml.Descendants().Where(x => x.Name.LocalName == "HintPath"))
            paths.Add(node.Value.Replace("$(ExtenderDir)", Path.Combine(game, "BepInEx/plugins/000shcdese")).Replace("$(GameDir)", game)
                .Replace("$(ApiSharedDir)", Path.GetFullPath(Path.Combine(root, "../../APIShared/BepInEx/plugins/APIShared_Serp"))));
        var sources = Directory.GetFiles(Path.Combine(root, "src"), "*.cs").Concat(Directory.GetFiles(Path.Combine(root, "Properties"), "*.cs"))
            .Concat(new[] { Path.GetFullPath(Path.Combine(root, "../../Shared/Runtime/Diagnostics/DebugLogHelper.cs")), Path.GetFullPath(Path.Combine(root, "../../Shared/Adapters/APIShared/MissionEventsAdapter.cs")) });
        var trees = sources.Select(p => Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(p),
            new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest), p));
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("PlagueHeightTestSemanticCheck", trees,
            paths.Select(p => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p)),
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
        Check(errors.Length == 0, "runtime semantic compilation without emitting a mod: " + string.Join("\n", errors.Select(e => e.ToString())));
        Console.WriteLine("PASS: complete runtime semantic compilation against real game assembly and .NET Framework 4.8.1 references; no mod emitted.");
    }
    private static void DecodeReachable(PreparedNativeCode code)
    {
        ulong start = (ulong)code.Address.ToInt64(), end = start + (uint)code.Bytes.Length;
        var queue = new System.Collections.Generic.Queue<ulong>(); var seen = new System.Collections.Generic.HashSet<ulong>();
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            ulong ip = queue.Dequeue(); if (ip < start || ip >= end || !seen.Add(ip)) continue;
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(code.Bytes.Skip((int)(ip - start)).ToArray())); decoder.IP = ip;
            var i = decoder.Decode(); Check(!i.IsInvalid && i.NextIP <= end, "generated reachable instruction");
            if (i.FlowControl == FlowControl.Return) continue;
            if (i.FlowControl == FlowControl.IndirectBranch || i.FlowControl == FlowControl.IndirectCall)
            {
                Check(i.IsIPRelativeMemoryOperand && i.IPRelativeMemoryAddress >= start && i.IPRelativeMemoryAddress + 8 <= end, "generated pointer slot");
                var target = BitConverter.ToUInt64(code.Bytes, (int)(i.IPRelativeMemoryAddress - start)); queue.Enqueue(target);
                if (i.FlowControl == FlowControl.IndirectCall) queue.Enqueue(i.NextIP);
            }
            else if (i.FlowControl == FlowControl.UnconditionalBranch) queue.Enqueue(i.NearBranchTarget);
            else { queue.Enqueue(i.NextIP); if (i.FlowControl == FlowControl.ConditionalBranch || i.FlowControl == FlowControl.Call) queue.Enqueue(i.NearBranchTarget); }
        }
        Check(seen.Count > 20, "generated code fully traversed");
    }
    private static void Exit(IntPtr module, int offset, int result, IntPtr capture)
    {
        var a = new Assembler(64);
        a.push(rax); a.push(r11); a.mov(r11, (ulong)capture.ToInt64());
        a.mov(__qword_ptr[r11 + 8], r8); a.mov(__qword_ptr[r11 + 16], r9); a.mov(__qword_ptr[r11 + 24], r10);
        a.mov(__qword_ptr[r11 + 32], rcx); a.mov(__qword_ptr[r11 + 40], rdx);
        a.mov(rax, __qword_ptr[rsp]); a.mov(__qword_ptr[r11 + 48], rax);
        a.mov(rax, __qword_ptr[rsp + 8]); a.mov(__qword_ptr[r11], rax);
        a.pushfq(); a.pop(rax); a.mov(__qword_ptr[r11 + 56], rax);
        a.pop(r11); a.pop(rax); a.mov(eax, result);
        a.add(rsp, 40); a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12); a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx); a.ret();
        var exit = new PreparedNativeCode(target => { foreach (var instruction in a.Instructions) target.AddInstruction(instruction); });
        exits.Add(exit);
        var jump = new Assembler(64); jump.AddUnrestrictedJmp((ulong)exit.Address.ToInt64());
        var b = PlagueHeightAssembly.Assemble(jump, (ulong)(module + offset).ToInt64()); Marshal.Copy(b, 0, module + offset, b.Length);
    }
    private static void TestHook(int index, IntPtr module, IntPtr state, IntPtr capture, PreparedNativeCode stub)
    {
        ulong image = (ulong)module.ToInt64(); int back = PlagueHeightAssembly.Sites[index] + (index == 0 ? 32 : 26);
        Exit(module, back, 1, capture);
        if (index == 1) { Exit(module, 0x192861, 1, capture); Exit(module, 0x1929D0, 0, capture); }
        if (index == 2) Exit(module, 0x9CB9D, 0, capture);
        if (index == 3) Exit(module, 0x9CC6A, 0, capture);
        var wrapper = new PreparedNativeCode(a =>
        {
            a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi); a.push(r12); a.push(r13); a.push(r14); a.push(r15); a.sub(rsp, 40);
            a.mov(rbx, image + PlagueHeightAssembly.ProjectileBase); a.mov(rsi, index == 1 ? 0x490UL : image);
            a.mov(r15, image + 0x67E8400); a.mov(r13d, 0xE8); a.mov(r14d, 0xE8); a.mov(r12d, 200); a.mov(ebp, 0);
            a.mov(rax, 0x246UL); a.push(rax); a.popfq();
            a.mov(rax, 0); a.mov(rcx, 0x2222UL); a.mov(edx, 1); a.mov(r8, image); a.mov(r9, 0x9999UL); a.mov(r10, 0xAAAAUL); a.mov(r11, 0xBBBBUL);
            a.AddUnrestrictedJmp((ulong)stub.Address.ToInt64());
        });
        try
        {
            var invoke = Marshal.GetDelegateForFunctionPointer<Invoke>(wrapper.Address);
            int projectile = PlagueHeightAssembly.ProjectileBase + 0xE8;
            Put(module, projectile + 0x60, 1); Word(module, projectile + 0x3C, (int)AliveState.IsAlive);
            for (int active = 0; active < 2; active++)
            {
                Marshal.WriteInt32(state, active);
                if (index == 1)
                {
                    foreach (int difference in new[] { -21, -20, -19, 0, 19, 20, 21 })
                    {
                        Tile(module, 1, 100, 0);
                        Word(module, 0x67E8400 + 0x490 + 0x712, 100); Word(module, 0x67E8400 + 0x490 + 0x714, difference);
                        Put(module, 0x67E8B2C + 0x490, 1);
                        Check(invoke() == (active == 0 || Math.Abs(difference) <= 20 ? 1 : 0), "damage tolerance " + active + "/" + difference);
                        if (active != 0) Check(Marshal.ReadInt64(capture) == 0 && Marshal.ReadInt64(capture, 56) == 0x246 && Marshal.ReadInt64(capture, 8) == (long)image &&
                            Marshal.ReadInt64(capture, 16) == 0x9999 && Marshal.ReadInt64(capture, 24) == 0xAAAA && Marshal.ReadInt64(capture, 48) == 0xBBBB &&
                            Marshal.ReadInt64(capture, 32) == 0x2222 && Marshal.ReadInt64(capture, 40) == 1, "damage volatile/flags preservation");
                    }
                    Tile(module, 1, 90, TilePropertyFlag.IsWall, 1); Word(module, 0x67E8400 + 0x490 + 0x712, 90); Word(module, 0x67E8400 + 0x490 + 0x714, 40);
                    Check(invoke() == active, "wall immunity removed only when enabled");
                    Tile(module, 1, 0, TilePropertyFlag.IsElevated, 1); Word(module, PlagueHeightAssembly.BuildingManager + 0x32C + 0x12E, 74);
                    Word(module, PlagueHeightAssembly.BuildingManager + 0x32C + 0x148, 10); Word(module, 0x67E8400 + 0x490 + 0x712, 10); Word(module, 0x67E8400 + 0x490 + 0x714, 296);
                    Check(invoke() == active, "tower immunity removed only when enabled");
                    Word(module, 0x67E8400 + 0x490 + 0x714, 0); Check(invoke() == 0, "tower cloud excludes ground-level unit");
                }
                else if (index == 0)
                {
                    Tile(module, 1, 90, TilePropertyFlag.IsWall, 1); Check(invoke() == 1, "update continuation");
                    Check(Marshal.ReadInt16(module, projectile + 0x50) == (active == 1 ? 150 : 110), "update height " + active);
                    Check(Marshal.ReadInt64(capture) == (active == 1 ? 150 : 110) && Marshal.ReadInt64(capture, 32) == 0xE8, "update original outputs");
                    Tile(module, 1, 5, 0); invoke(); Check(Marshal.ReadInt16(module, projectile + 0x50) == 25, "destruction/current surface reevaluated");
                    if (active == 1) { Tile(module, 1, 5, TilePropertyFlag.IsElevated); invoke(); Check(Marshal.ReadInt16(module, projectile + 0x50) == 25, "invalid roof update falls back to Vanilla"); }
                }
                else
                {
                    Check(invoke() == active, "neighbor tile-height bypass " + index + "/" + active);
                    Word(module, projectile + 0x3C, 3); Check(invoke() == 0, "neighbor alive gate " + index);
                    Word(module, projectile + 0x3C, (int)AliveState.IsAlive);
                }
            }
            Check(Marshal.ReadInt64(state, 8) == Marshal.ReadInt64(state, 16) + Marshal.ReadInt64(state, 24), "comparison invariant");
        }
        finally { wrapper.RollbackUnpublished(); }
    }
}
