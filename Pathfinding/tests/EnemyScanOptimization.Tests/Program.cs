using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Iced.Intel;
using Microsoft.Extensions.Logging.Abstractions;
using Pathfinding.EnemyScanOptimization;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

// Executes only private buffers. The installed DLL is read, never loaded or patched.
string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
Audit.Verify(root);
long checks = 0;
var random = new Random(86237);
var score = new Fixture(0);
var yMajor = new Fixture(1);
var xMajor = new Fixture(2);
int[] edges = { int.MinValue, int.MinValue + 1, -1000000, -451, -10, -6, -5, -4, -2, -1, 0, 1, 2, 4, 5, 6, 10, 449, 450, int.MaxValue - 1, int.MaxValue };
foreach (int divisor in new[] { 1, 2, 5 })
{
    foreach (int n in edges) foreach (int index in new[] { -9, -1, 0, 1, 9 })
        foreach (int start in new[] { int.MinValue, -1, 0, 100000, int.MaxValue }) TestScore(n, divisor, index, start);
    for (int i = 0; i < 100000; i++) TestScore(unchecked((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1)), divisor, random.Next(-9, 10), random.Next());
}
for (int major = 1; major <= 65535; major++)
{
    foreach (int minor in new[] { 0, 1, Math.Min(2, major), major / 2, major - 1, major }) TestDistance(minor, major);
}
for (int minor = 0; minor <= 65535; minor++) TestDistance(minor, 65535);
for (int i = 0; i < 100000; i++) { int major = random.Next(1, 65536); TestDistance(random.Next(major + 1), major); }
Console.WriteLine($"PASS installed backend private buffers: {checks} assertions; score full signed inputs/live table, both distance branches, quotient/remainder/result/flags/registers, dormant/active guard.");

var timings = new List<object>();
foreach (int divisor in new[] { 1, 2, 5 })
{
    score.Input(0, 143, divisor, 450);
    timings.Add(score.Benchmark("score/divisor-" + divisor));
}
foreach (var pair in new[] { (0, 5000), (2, 5000), (50, 5000), (4000, 5000), (65535, 65535) })
{
    yMajor.Input(pair.Item1, 0, pair.Item2, 0);
    timings.Add(yMajor.Benchmark($"distance/{pair.Item1}/{pair.Item2}"));
}
Console.WriteLine(JsonSerializer.Serialize(new { observedUtc = DateTime.UtcNow, nativeHash = Audit.Hash, checks,
    hostRuntime = Environment.Version.ToString(), processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
    iced = typeof(Assembler).Assembly.GetName().Version?.ToString(), redbird = typeof(X64InlineHook).Assembly.GetName().Version?.ToString(),
    timings, scope = "isolated actual installed X64InlineHook including jump, logical guard and wrapper; one managed call per native batch; no game/FPS/whole-function gain claim" }));

void TestScore(int n, int divisor, int index, int start)
{
    score.Input(index, start, divisor, n);
    var actual = score.Compare(); checks += actual;
    if (Marshal.ReadInt32(score.State, 16) != n / divisor || Marshal.ReadInt32(score.State, 20) != n % divisor ||
        Marshal.ReadInt32(score.State, 24) != unchecked(start + n / divisor)) throw new Exception("score scalar oracle mismatch");
    checks += 3;
}
void TestDistance(int minor, int major)
{
    foreach (var fixture in new[] { yMajor, xMajor })
    {
        fixture.Input(minor, 0, major, 0); checks += fixture.Compare();
        int p = minor * 2 / 5 * minor;
        if (Marshal.ReadInt32(fixture.State, 16) != p / major || Marshal.ReadInt32(fixture.State, 20) != p % major ||
            Marshal.ReadInt32(fixture.State, 24) != major + p / major) throw new Exception("distance scalar oracle mismatch");
        checks += 3;
    }
}

static class Audit
{
    internal const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    internal static void Verify(string root)
    {
        string baseline = Path.Combine(root, "_inspect/CrusaderDE-Native-Baseline");
        using var current = JsonDocument.Parse(File.ReadAllText(Path.Combine(baseline, "CURRENT.json")));
        if (current.RootElement.GetProperty("currentNativeHash").GetString() != Hash) throw new Exception("candidate native identity differs");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(baseline, current.RootElement.GetProperty("databaseManifest").GetString()!)));
        if (manifest.RootElement.GetProperty("identities").GetProperty("currentNativeHash").GetString() != Hash) throw new Exception("manifest native identity differs");
        string dll = manifest.RootElement.GetProperty("binaries").EnumerateArray().Single(v => v.GetProperty("role").GetString() == "current-native").GetProperty("sourcePath").GetString()!;
        byte[] image = File.ReadAllBytes(dll);
        if (Convert.ToHexString(SHA256.HashData(image)) != Hash) throw new Exception("installed canonical DLL changed");
        using var pe = new PEReader(new MemoryStream(image));
        var bodySection = pe.PEHeaders.SectionHeaders.Single(s => 0x18C160 >= s.VirtualAddress && 0x18C160 + 2665 <= s.VirtualAddress + s.SizeOfRawData);
        if (Convert.ToHexString(SHA256.HashData(image.AsSpan(bodySection.PointerToRawData + 0x18C160 - bodySection.VirtualAddress, 2665))) !=
            "53FF884138B941ECD72D0CAC182228680D3702B5D94B2A991DFFE2B8230C22D0") throw new Exception("full scan body differs from audited extent");
        var sites = new[] { (EnemyScanArithmetic.ScoreSite, EnemyScanArithmetic.ScoreOriginal), (EnemyScanArithmetic.DistanceYMajorSite, EnemyScanArithmetic.DistanceYMajorOriginal), (EnemyScanArithmetic.DistanceXMajorSite, EnemyScanArithmetic.DistanceXMajorOriginal) };
        foreach (var (rva, bytes) in sites)
        {
            var section = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva + bytes.Length <= s.VirtualAddress + s.SizeOfRawData);
            if ((section.SectionCharacteristics & System.Reflection.PortableExecutable.SectionCharacteristics.MemExecute) == 0 ||
                !image.AsSpan(section.PointerToRawData + rva - section.VirtualAddress, bytes.Length).SequenceEqual(bytes)) throw new Exception($"canonical site mismatch {rva:X}");
            var d = Decoder.Create(64, new ByteArrayCodeReader(bytes)); d.IP = 0x180000000UL + (uint)rva;
            while (d.IP < 0x180000000UL + (uint)rva + (uint)bytes.Length) { Instruction i = d.Decode(); if (i.IsInvalid || i.FlowControl != FlowControl.Next) throw new Exception("non-linear displaced block"); }
        }
        string xrefs = Path.Combine(baseline, Hash, "exports/xrefs.jsonl");
        foreach (string line in File.ReadLines(xrefs))
        {
            using var entry = JsonDocument.Parse(line);
            string targetText = entry.RootElement.GetProperty("toRva").GetString(); if (string.IsNullOrEmpty(targetText)) continue;
            if (!ulong.TryParse(targetText[2..], System.Globalization.NumberStyles.HexNumber, null, out ulong target)) continue;
            foreach (var (site, bytes) in sites)
                if (target > (ulong)site && target < (ulong)(site + bytes.Length)) throw new Exception($"incoming interior edge/reference {line}");
        }
        Console.WriteLine("PASS native hash, exact canonical 14/32/32-byte instruction blocks, executable sections and all exported incoming xrefs.");
    }
}

sealed class Fixture
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Run(IntPtr state);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Batch(IntPtr state, int count);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
    internal readonly IntPtr State = Marshal.AllocHGlobal(4096);
    private readonly IntPtr demand = Marshal.AllocHGlobal(8);
    private readonly Run original, candidate;
    private readonly Batch originalBatch, candidateBatch;
    private readonly X64InlineHook hook;
    private readonly int kind;
    private static readonly List<object> Roots = new();
    internal Fixture(int kind)
    {
        this.kind = kind;
        byte[] block = kind == 0 ? EnemyScanArithmetic.ScoreOriginal : kind == 1 ? EnemyScanArithmetic.DistanceYMajorOriginal : EnemyScanArithmetic.DistanceXMajorOriginal;
        IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)8192, 0x3000, 0x40);
        if (page == IntPtr.Zero) throw new Exception("private fixture allocation failed");
        byte[] code = Wrapper(block);
        Marshal.Copy(code, 0, page, code.Length); Marshal.Copy(code, 0, page + 4096, code.Length);
        int site = Find(code, block);
        Marshal.WriteInt64(demand, 0);
        hook = new X64InlineHook(unchecked((ulong)(page + 4096 + site).ToInt64()), block.Length, NullLogger.Instance, "enemy-scan-private-" + kind);
        if (hook.DisplacedByteCount != block.Length) throw new Exception("installed backend displacement differs");
        hook.Generate((a, displaced, back) => EnemyScanArithmetic.GenerateGuarded(a, displaced, unchecked((ulong)demand.ToInt64()),
            active => { if (kind == 0) EnemyScanArithmetic.GenerateScore(active); else EnemyScanArithmetic.GenerateDistance(active, kind == 1); }));
        hook.Enable();
        original = Marshal.GetDelegateForFunctionPointer<Run>(page); candidate = Marshal.GetDelegateForFunctionPointer<Run>(page + 4096);
        originalBatch = MakeBatch(page); candidateBatch = MakeBatch(page + 4096);
        Roots.Add(page); Roots.Add(State); Roots.Add(demand); Roots.Add(hook); Roots.Add(original); Roots.Add(candidate);
    }
    internal void Input(int a, int c, int b, int table)
    {
        Marshal.WriteInt32(State, 0, a); Marshal.WriteInt32(State, 4, c); Marshal.WriteInt32(State, 8, b);
        if (kind == 0) Marshal.WriteInt32(State, 0x100 + a * 4, table);
    }
    internal long Compare()
    {
        original(State);
        byte[] expected = new byte[64]; Marshal.Copy(State + 16, expected, 0, expected.Length);
        foreach (byte active in new byte[] { 0, 1 })
        {
            Marshal.WriteByte(demand, active); candidate(State);
            byte[] actual = new byte[64]; Marshal.Copy(State + 16, actual, 0, actual.Length);
            if (!expected.AsSpan().SequenceEqual(actual)) throw new Exception($"machine output/ABI mismatch kind={kind} active={active}: {Convert.ToHexString(expected)} != {Convert.ToHexString(actual)}");
        }
        return 2;
    }
    internal object Benchmark(string label)
    {
        const int calls = 1000000;
        Marshal.WriteByte(demand, 1);
        originalBatch(State, 20000); candidateBatch(State, 20000);
        var before = new List<double>(); var after = new List<double>();
        for (int repeat = 0; repeat < 8; repeat++)
        {
            if ((repeat & 1) == 0) { before.Add(Time(originalBatch)); after.Add(Time(candidateBatch)); }
            else { after.Add(Time(candidateBatch)); before.Add(Time(originalBatch)); }
        }
        before.Sort(); after.Sort();
        double baseline = (before[3] + before[4]) / 2, changed = (after[3] + after[4]) / 2;
        Console.WriteLine($"BENCH {label} baselineMs={baseline:F3} candidateMs={changed:F3} ratio={changed / baseline:F3}");
        return new { label, calls, baselineMs = baseline, candidateMs = changed, ratio = changed / baseline, baselineRuns = before, candidateRuns = after };
        double Time(Batch run) { long start = Stopwatch.GetTimestamp(); run(State, calls); return Stopwatch.GetElapsedTime(start).TotalMilliseconds; }
    }
    private static Batch MakeBatch(IntPtr target)
    {
        IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
        if (page == IntPtr.Zero) throw new Exception("private batch allocation failed");
        var a = new Assembler(64);
        a.push(rbx); a.push(rsi); a.push(rdi); a.sub(rsp, 32);
        a.mov(rbx, rcx); a.mov(esi, edx); a.mov(rdi, unchecked((ulong)target.ToInt64()));
        var loop = a.CreateLabel(); a.Label(ref loop); a.mov(rcx, rbx); a.call(rdi); a.dec(esi); a.jnz(loop);
        a.add(rsp, 32); a.pop(rdi); a.pop(rsi); a.pop(rbx); a.ret();
        using var stream = new MemoryStream(); a.Assemble(new StreamCodeWriter(stream), 0);
        byte[] bytes = stream.ToArray(); Marshal.Copy(bytes, 0, page, bytes.Length);
        var run = Marshal.GetDelegateForFunctionPointer<Batch>(page); Roots.Add(page); Roots.Add(run); return run;
    }
    private byte[] Wrapper(byte[] block)
    {
        var a = new Assembler(64);
        a.push(rbp); a.sub(rsp, 0x60); a.mov(r11, rcx);
        a.movsxd(rax, __dword_ptr[r11]); a.mov(ecx, __dword_ptr[r11 + 4]); a.mov(edx, __dword_ptr[r11 + 8]);
        a.mov(__dword_ptr[rsp + 0x3C], edx);
        if (kind == 0)
        {
            a.lea(rbp, __[r11 + 0x100]); a.sub(rbp, 0x3219C0);
            a.mov(r8, 0x1122334455667788UL); a.mov(r9, 0x8877665544332211UL);
        }
        else if (kind == 1) { a.mov(r8d, eax); a.mov(r9d, edx); }
        else { a.mov(r8d, edx); a.mov(r9d, eax); }
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(block));
        var instructions = new List<Instruction>(); while (decoder.IP < (uint)block.Length) instructions.Add(decoder.Decode());
        a.AddInstructions(instructions.ToArray());
        // Distance IDIV flags are undefined, and the native continuation overwrites them immediately.
        if (kind != 0) a.cmp(ecx, 0x1234567);
        a.pushfq(); a.pop(r10);
        a.mov(__dword_ptr[r11 + 16], eax); a.mov(__dword_ptr[r11 + 20], edx); a.mov(__dword_ptr[r11 + 24], ecx);
        a.mov(__qword_ptr[r11 + 40], r10); a.mov(__qword_ptr[r11 + 48], r8); a.mov(__qword_ptr[r11 + 56], r9);
        a.add(rsp, 0x60); a.pop(rbp); a.ret();
        using var stream = new MemoryStream(); a.Assemble(new StreamCodeWriter(stream), 0); return stream.ToArray();
    }
    private static int Find(byte[] code, byte[] block)
    {
        int found = -1;
        for (int i = 0; i <= code.Length - block.Length; i++) if (code.AsSpan(i, block.Length).SequenceEqual(block))
        { if (found >= 0) throw new Exception("ambiguous private span"); found = i; }
        return found >= 0 ? found : throw new Exception("private block not found");
    }
}
