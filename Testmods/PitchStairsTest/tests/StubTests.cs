using Iced.Intel;
using PitchStairsTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

// Executes every displaced Vanilla block and its stub in one harness. Disabled stubs must equal
// Vanilla exactly (registers, flags, stack, memory, exit); enabled stubs may differ only where
// variant B intends it. Also checks the site bytes in the installed CrusaderDE.dll file image.
internal static unsafe class StubTests
{
    private const int Rax = 0, Rbx = 1, Rcx = 2, Rdx = 3, Rsi = 4, Rdi = 5, Rbp = 6, R8 = 7, R9 = 8, R10 = 9;
    private const int R11 = 10, R12 = 11, R13 = 12, R14 = 13, R15 = 14, Flags = 15, Rsp = 16, Exit = 17, ContextSlots = 18;
    private const ulong FlagMask = 0x8D5; // CF PF AF ZF SF OF
    private const int CodeSize = 0x200000, SiteOffset = 0x100000, StubOffset = 0x180000, TileSize = 0x1000000;
    private const int Tile = 0x40; // packed tile index used by all cases

    private static byte* code, tiles, data;
    private static ulong* input, output, hostRsp;
    private static int* callRecord; // rcx, rdx, rsp low, return value
    private static int failures, checks;

    private static int Main(string[] args)
    {
        string dll = args.Length > 0 ? args[0] :
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        code = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)CodeSize, 0x3000, 0x40);
        tiles = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)TileSize, 0x3000, 0x04);
        data = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)0x10000, 0x3000, 0x04);
        if (code == null || tiles == null || data == null) throw new InvalidOperationException("VirtualAlloc failed.");
        input = (ulong*)data; output = input + 32; hostRsp = output + 32; callRecord = (int*)(hostRsp + 4);

        CheckFileImage(dll);
        Lowering(); AivRestore(); ManualRestore(); LoadNormalization(); Render(); Placement(); FireSource(); FireNeighbors();
        ReferenceRule();
        Console.WriteLine(failures == 0 ? $"PASS: {checks} pitch-stairs stub checks." : $"FAIL: {failures} of {checks} checks.");
        return failures == 0 ? 0 : 1;
    }

    // ---- cases ---------------------------------------------------------------------------

    private static void Lowering()
    {
        var site = Find(NativeCode.SiteKind.Lowering);
        foreach (byte height in new byte[] { 8, 4, 2, 0, 255 })
            Compare(site, $"height {height}", () =>
            {
                Height(Tile) = height;
                Set(Rdx, height); Set(R9, 0); Set(R8, Tile); Set(Rsi, (ulong)tiles);
                // Flags of Vanilla's preceding cmp dl,4.
                Set(Flags, CmpFlags(height, 4));
            }, enabled => new Expect
            {
                Memory = { [HeightAddress(Tile)] = enabled ? height : (byte)Math.Max(0, height - 4) },
                SameFlags = true,
            });
    }

    private static void AivRestore()
    {
        var site = Find(NativeCode.SiteKind.AivRestore);
        Compare(site, "stair over pitch", () =>
        {
            Height(Tile) = 24; Logic(Tile) = 0x8900;
            Set(Rdx, Tile); Set(Rdi, (ulong)tiles); Set(Flags, 0x202);
        }, enabled => new Expect { Memory = { [HeightAddress(Tile)] = (byte)(enabled ? 24 : 28) }, SameFlags = false });
    }

    private static void ManualRestore()
    {
        var site = Find(NativeCode.SiteKind.ManualRestore);
        foreach (bool pitch in new[] { true, false })
            Compare(site, pitch ? "pitch flag" : "no pitch flag", () =>
            {
                Height(Tile) = 82; Logic(Tile) = pitch ? 0x8908 : 0x8900;
                Set(Rax, (ulong)(uint)Logic(Tile)); Set(Rbx, (ulong)LogicAddress(Tile));
                Set(R12, Tile); Set(R13, (ulong)tiles); Set(Flags, 0x202);
            }, enabled => new Expect
            {
                Memory = { [HeightAddress(Tile)] = (byte)(pitch && !enabled ? 86 : 82) },
                // The je exits without the guard; only the skipped add may change flags.
                SameFlags = !pitch,
                Exit = pitch ? 1 : 2,
            });
    }

    private static void LoadNormalization()
    {
        var site = Find(NativeCode.SiteKind.LoadNormalization);
        var cases = new (byte Default, byte Height, int Logic, ushort Structure, byte Expected)[]
        {
            (8, 8, 0x8, 0, 8),       // B pitch stays at ground height
            (8, 4, 0x8, 0, 8),       // Vanilla-lowered pitch (map/save) is raised
            (40, 12, 0x8, 0, 40),    // any height: Vanilla also writes unconditionally here
            (3, 0, 0x8, 0, 3),       // clamped low ground is raised
            (3, 0, 0x108, 0, 0),     // wall: untouched
            (3, 0, 0x8, 7, 0),       // structure: untouched
            (0, 0, 0x8, 0, 0),       // default 0: nothing to raise
            (2, 2, 0x8, 0, 2),       // low B pitch unchanged
        };
        // The editor start applies the managed rule; it must equal the native stub case by case.
        foreach (var c in cases)
            Check(NativeCode.NormalizedPitchHeight(c.Height, c.Default, c.Logic, c.Structure) == c.Expected,
                $"editor rule: default {c.Default} height {c.Height} logic 0x{c.Logic:X} structure {c.Structure}");
        Check(NativeCode.NormalizedPitchHeight(5, 8, 0x0, 0) == 5, "editor rule: no pitch flag stays unchanged");
        foreach (var c in cases)
            Compare(site, $"default {c.Default} height {c.Height} logic 0x{c.Logic:X} structure {c.Structure}", () =>
            {
                Default(Tile) = c.Default; Height(Tile) = c.Height; Logic(Tile) = c.Logic; Structure(Tile) = c.Structure;
                Set(R9, (ulong)HeightAddress(Tile)); Set(R10, (ulong)LogicAddress(Tile)); Set(Rdx, (ulong)MiscAddress(Tile));
                Set(Flags, 0x202);
            }, enabled => new Expect
            {
                Memory = { [HeightAddress(Tile)] = enabled ? c.Expected : (c.Default >= 4 ? (byte)(c.Default - 4) : c.Height) },
                IgnoreRegisters = enabled ? new[] { Rax } : null, // eax is reloaded on the next tile; the return value is unused
                SameFlags = !enabled,
            });
    }

    private static void Render()
    {
        var site = Find(NativeCode.SiteKind.Render);
        for (int i = 0; i < 300; i++) *(int*)(tiles + NativeCode.HeightTableOffset + i * 4) = 1000 + i;
        foreach (byte height in new byte[] { 8, 4, 40 })
            Compare(site, $"height {height}", () =>
            {
                Height(Tile) = height; Set(Rbx, Tile); Set(R9, (ulong)tiles); Set(Flags, 0xA93);
            }, enabled => new Expect { Registers = { [Rax] = (ulong)(1000 + height + (enabled ? 0 : 4)) }, SameFlags = true });
    }

    private static void Placement()
    {
        var site = Find(NativeCode.SiteKind.Placement);
        foreach (bool edge in new[] { false, true })
            Compare(site, edge ? "footprint edge" : "free pitch", () =>
            {
                Misc(Tile) = (ushort)(edge ? NativeCode.FootprintEdgeBit : 0);
                // rbp is the image base in Vanilla; the misc grid sits at image + TileManager + MiscDisplay.
                Set(Rbp, (ulong)tiles - NativeCode.TileManagerRva); Set(R13, Tile); Set(Rsi, 8); Set(Flags, 0x202);
            }, enabled => new Expect
            {
                Registers = { [Rsi] = (ulong)(edge || enabled ? 8 : 12) },
                SameFlags = edge,
                Exit = 1,
            });
    }

    private static void FireSource()
    {
        var site = Find(NativeCode.SiteKind.FireSource);
        var cases = new (byte Height, int Logic, ushort Misc, ushort Direction, ulong Expected)[]
        {
            (8, 0x8, 0, 0, 4), (8, 0x0, 0, 0, 8), (8, 0x8, 0x2000, 0, 8), (2, 0x8, 0, 0, 0), (8, 0x8, 0, 4, 4),
        };
        foreach (var c in cases)
            Compare(site, $"height {c.Height} logic 0x{c.Logic:X} misc 0x{c.Misc:X} direction {c.Direction}", () =>
            {
                Height(Tile) = c.Height; Logic(Tile) = c.Logic; Misc(Tile) = c.Misc;
                Set(Rcx, Tile); Set(R15, (ulong)tiles); Set(Rax, c.Direction); Set(R11, 0x1111); Set(Flags, 0x202);
            }, enabled => new Expect
            {
                Registers = { [Rdi] = enabled ? c.Expected : c.Height },
                SameFlags = true,
                Exit = c.Direction >= 4 ? 2 : 1,
            });
    }

    private static void FireNeighbors()
    {
        foreach (var site in NativeCode.Sites.Where(s => s.Kind == NativeCode.SiteKind.FireNeighbor))
        {
            bool rowSite = site.InstructionLengths.Length == 3;
            var cases = new (int Logic, ushort Misc, ushort Structure, int Returned, int Expected)[]
            {
                (0x8, 0, 0, 8, 4), (0x0, 0, 0, 8, 8), (0x8, 0x2000, 0, 8, 8), (0x8, 0, 5, 30, 30), (0x8, 0, 0, 2, 0),
            };
            foreach (var c in cases)
                Compare(site, $"logic 0x{c.Logic:X} misc 0x{c.Misc:X} structure {c.Structure} returned {c.Returned}", () =>
                {
                    Logic(Tile) = c.Logic; Misc(Tile) = c.Misc; Structure(Tile) = c.Structure;
                    callRecord[3] = c.Returned;
                    Set(R15, (ulong)tiles); Set(Rdi, 32); Set(Flags, 0x202);
                    if (rowSite)
                    {
                        // add edx,[rbx+rcx*4+table]: rbx is the image base, rcx the row index.
                        int tableDisplacement = (int)NativeCode.Decode(site.Bytes, 0, site.InstructionLengths)[0].MemoryDisplacement64;
                        *(int*)(data + 0x8000) = 3;
                        Set(Rbx, (ulong)(data + 0x8000) - (ulong)tableDisplacement); Set(Rcx, 0); Set(Rdx, Tile - 3);
                    }
                    else
                        Set(Rcx, (ulong)(site.Bytes[2] == 0xFF ? Tile + 1 : Tile - 1));
                }, enabled => new Expect
                {
                    Registers = rowSite
                        ? new Dictionary<int, ulong> { [Rax] = (ulong)(uint)(enabled ? c.Expected : c.Returned) }
                        : new Dictionary<int, ulong> { [Rax] = (ulong)(uint)(32 - (enabled ? c.Expected : c.Returned)), [Rdi] = (ulong)(uint)(32 - (enabled ? c.Expected : c.Returned)) },
                    // The callee clobbers rcx; on the adjusted path the stub reuses it for the counter.
                    IgnoreRegisters = enabled ? new[] { Rcx } : null,
                    SameFlags = !enabled,
                    CallArguments = true,
                });
        }
    }

    private static void ReferenceRule()
    {
        Check(NativeCode.IsVanillaLoweredPitch(4, 8, 0x8, 0, 0), "rule: lowered pitch");
        Check(!NativeCode.IsVanillaLoweredPitch(8, 8, 0x8, 0, 0), "rule: B pitch");
        Check(!NativeCode.IsVanillaLoweredPitch(4, 8, 0x8, 0x2000, 0), "rule: footprint edge");
        Check(NativeCode.IsVanillaLoweredPitch(0, 3, 0x8, 0, 0), "rule: clamped low pitch");
        Check(!NativeCode.IsVanillaLoweredPitch(0, 0, 0x8, 0, 0), "rule: default zero");
        Check(!NativeCode.IsVanillaLoweredPitch(4, 8, 0x0, 0, 0), "rule: no pitch");

        // A backend decode at another IP (different branch target) or of other bytes must be refused.
        var manual = Find(NativeCode.SiteKind.ManualRestore);
        Check(Throws(() => NativeCode.RequireSameInstructions(NativeCode.Decode(manual.Bytes, 0x2000, manual.InstructionLengths),
            NativeCode.Decode(manual.Bytes, 0x1000, manual.InstructionLengths), manual.Name)), "backend guard: moved branch target");
        var aiv = Find(NativeCode.SiteKind.AivRestore);
        byte[] changed = (byte[])aiv.Bytes.Clone(); changed[7] = 0x08;
        Check(Throws(() => NativeCode.RequireSameInstructions(NativeCode.Decode(changed, 0x1000, aiv.InstructionLengths),
            NativeCode.Decode(aiv.Bytes, 0x1000, aiv.InstructionLengths), aiv.Name)), "backend guard: changed immediate");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    // ---- harness -------------------------------------------------------------------------

    private sealed class Expect
    {
        internal Dictionary<int, ulong> Registers = new Dictionary<int, ulong>();
        internal Dictionary<IntPtr, byte> Memory = new Dictionary<IntPtr, byte>();
        internal int[] IgnoreRegisters;
        internal bool SameFlags;
        internal int Exit;
        internal bool CallArguments;
    }

    private static void Compare(NativeCode.Site site, string name, Action arrange, Func<bool, Expect> expect)
    {
        ulong[] vanilla = Run(site, arrange, stub: false, enabled: false, out byte[] vanillaMemory, out int[] vanillaCall);
        foreach (bool enabled in new[] { false, true })
        {
            ulong[] actual = Run(site, arrange, stub: true, enabled: enabled, out byte[] memory, out int[] call);
            Expect e = expect(enabled);
            string label = $"{site.Name} [{name}] enabled={enabled}";
            if (!enabled)
            {
                for (int r = 0; r < ContextSlots; r++)
                    Check(actual[r] == vanilla[r] || r == Flags && ((actual[r] ^ vanilla[r]) & FlagMask) == 0,
                        $"{label}: slot {r} 0x{actual[r]:X} != Vanilla 0x{vanilla[r]:X}");
                Check(memory.SequenceEqual(vanillaMemory), $"{label}: memory differs from Vanilla");
                if (e.CallArguments) Check(call.Take(3).SequenceEqual(vanillaCall.Take(3)), $"{label}: call arguments differ");
            }
            foreach (var pair in e.Registers)
                Check(Mask(actual[pair.Key], pair.Key, pair.Value) == pair.Value, $"{label}: register {pair.Key} 0x{actual[pair.Key]:X} != 0x{pair.Value:X}");
            for (int r = 0; r < Flags; r++)
            {
                if (e.Registers.ContainsKey(r) || e.IgnoreRegisters != null && e.IgnoreRegisters.Contains(r)) continue;
                Check(actual[r] == vanilla[r], $"{label}: register {r} changed 0x{actual[r]:X} vs Vanilla 0x{vanilla[r]:X}");
            }
            if (e.SameFlags) Check(((actual[Flags] ^ vanilla[Flags]) & FlagMask) == 0, $"{label}: flags 0x{actual[Flags]:X} != Vanilla 0x{vanilla[Flags]:X}");
            Check(actual[Rsp] == vanilla[Rsp], $"{label}: rsp differs");
            Check((int)actual[Exit] == (int)vanilla[Exit], $"{label}: exit {actual[Exit]} != Vanilla {vanilla[Exit]}");
            if (e.Exit != 0) Check((int)actual[Exit] == e.Exit, $"{label}: exit {actual[Exit]} != expected {e.Exit}");
            foreach (var pair in e.Memory)
                Check(Offset(pair.Key) >= 0 && memory[Offset(pair.Key)] == pair.Value,
                    $"{label}: memory 0x{(long)pair.Key:X} = {memory[Offset(pair.Key)]} != {pair.Value}");
            if (e.CallArguments)
            {
                Check(call[2] == 8, $"{label}: callee entered with rsp%16={call[2]}");
                Check(call[0] == vanillaCall[0] && call[1] == vanillaCall[1], $"{label}: call arguments differ");
            }
        }
    }

    private static ulong Mask(ulong value, int register, ulong expected) => expected <= uint.MaxValue && register == Rax ? value & 0xFFFFFFFF : value;

    // Snapshot of the tile cells used by the cases.
    private static readonly int[] TileCells =
    {
        NativeCode.HeightOffset + Tile, NativeCode.DefaultHeightOffset + Tile,
        NativeCode.LogicOffset + Tile * 4, NativeCode.LogicOffset + Tile * 4 + 1, NativeCode.LogicOffset + Tile * 4 + 2, NativeCode.LogicOffset + Tile * 4 + 3,
        NativeCode.StructureOffset + Tile * 2, NativeCode.MiscDisplayOffset + Tile * 2,
    };
    private static int Offset(IntPtr address) => Array.IndexOf(TileCells, (int)((byte*)address - tiles));

    private static ulong[] Run(NativeCode.Site site, Action arrange, bool stub, bool enabled, out byte[] memory, out int[] call)
    {
        new Span<byte>(code, CodeSize).Fill(0xCC);
        new Span<byte>(tiles + NativeCode.HeightOffset, 0x100).Clear();
        new Span<byte>(tiles + NativeCode.DefaultHeightOffset, 0x100).Clear();
        new Span<byte>(tiles + NativeCode.LogicOffset, 0x400).Clear();
        new Span<byte>(tiles + NativeCode.StructureOffset, 0x200).Clear();
        new Span<byte>(tiles + NativeCode.MiscDisplayOffset, 0x200).Clear();
        for (int r = 0; r < 32; r++) { input[r] = 0x5A5A0000UL + (ulong)r * 0x111; output[r] = 0; }
        input[Flags] = 0x202;
        callRecord[0] = callRecord[1] = callRecord[2] = -1;
        arrange();

        ulong state = (ulong)(data + 0x4000);
        new Span<byte>((void*)state, NativeCode.StateSize).Clear();
        *(int*)state = enabled ? 1 : 0;

        ulong p = (ulong)(code + SiteOffset);
        Instruction[] original = NativeCode.Decode(site.Bytes, p, site.InstructionLengths);
        NativeCode.RequireSameInstructions(NativeCode.Decode(site.Bytes, p, site.InstructionLengths), original, site.Name);
        // Exits: continuation plus every branch target leaving the block.
        var exits = new List<ulong> { p + (ulong)site.Length };
        foreach (Instruction i in original)
            if (i.FlowControl == FlowControl.ConditionalBranch && i.NearBranchTarget != exits[0]) exits.Add(i.NearBranchTarget);
        ulong capture = (ulong)(code + 0x10000);
        for (int k = 0; k < exits.Count; k++) WriteCapture(capture + (ulong)k * 0x400, k + 1);
        for (int k = 0; k < exits.Count; k++)
        {
            ulong exit = exits[k], pad = capture + (ulong)k * 0x400;
            bool near = exits.Any(other => other > exit && other - exit < 5);
            if (near)
            {
                // Two exits two bytes apart: short jump to a nearby 5-byte relay.
                ulong relay = p + 0x40 + (ulong)k * 8;
                code[exit - (ulong)code] = 0xEB; code[exit - (ulong)code + 1] = (byte)(relay - (exit + 2));
                WriteJump(relay, pad);
            }
            else WriteJump(exit, pad);
        }
        foreach (Instruction i in original)
            if (i.Mnemonic == Mnemonic.Call) WriteFakeNeighborHeight(i.NearBranchTarget);

        ulong target = p;
        if (stub)
        {
            var a = new Assembler(64);
            NativeCode.Emit(a, site, original, state, p + (ulong)site.Length);
            target = (ulong)(code + StubOffset);
            byte[] bytes = NativeCode.Assemble(a, target);
            Marshal.Copy(bytes, 0, (IntPtr)(long)target, bytes.Length);
        }
        else Marshal.Copy(site.Bytes, 0, (IntPtr)(long)p, site.Length);

        ulong entry = (ulong)code;
        WriteEntry(entry, target);
        Marshal.GetDelegateForFunctionPointer<Action>((IntPtr)(long)entry)();

        memory = TileCells.Select(o => tiles[o]).ToArray();
        call = new[] { callRecord[0], callRecord[1], callRecord[2], callRecord[3] };
        var result = new ulong[ContextSlots];
        for (int r = 0; r < ContextSlots; r++) result[r] = output[r];
        result[Exit] = (ulong)ExitIndex(result[Exit], exits.Count);
        return result;
    }

    private static int ExitIndex(ulong value, int count) => value >= 1 && value <= (ulong)count ? (int)value : -1;

    private static readonly AssemblerRegister64[] Registers64 = { rax, rbx, rcx, rdx, rsi, rdi, rbp, r8, r9, r10, r11, r12, r13, r14, r15 };

    private static void WriteEntry(ulong at, ulong target)
    {
        var a = new Assembler(64);
        a.push(rbx); a.push(rbp); a.push(rdi); a.push(rsi); a.push(r12); a.push(r13); a.push(r14); a.push(r15);
        a.sub(rsp, 0x48); // block entry with rsp % 16 == 0, as inside the audited Vanilla functions
        a.mov(rax, (ulong)hostRsp); a.mov(__qword_ptr[rax], rsp);
        a.mov(rax, (ulong)input);
        a.push(__qword_ptr[rax + Flags * 8]); a.popfq();
        for (int r = 1; r < Registers64.Length; r++) a.mov(Registers64[r], __qword_ptr[rax + r * 8]);
        a.mov(rax, __qword_ptr[rax]);
        a.jmp(target);
        Write(a, at);
    }

    private static void WriteCapture(ulong at, int exit)
    {
        var a = new Assembler(64);
        a.push(rax); a.mov(rax, (ulong)output); a.pop(__qword_ptr[rax]);
        for (int r = 1; r < Registers64.Length; r++) a.mov(__qword_ptr[rax + r * 8], Registers64[r]);
        a.pushfq(); a.pop(__qword_ptr[rax + Flags * 8]);
        a.mov(__qword_ptr[rax + Rsp * 8], rsp);
        a.mov(__qword_ptr[rax + Exit * 8], exit);
        a.mov(rax, (ulong)hostRsp); a.mov(rsp, __qword_ptr[rax]);
        a.add(rsp, 0x48);
        a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12); a.pop(rsi); a.pop(rdi); a.pop(rbp); a.pop(rbx);
        a.ret();
        Write(a, at);
    }

    // Stand-in for 0x6B910: records its arguments and entry alignment, scribbles over its whole
    // home area like a compiled callee may, and returns the configured height in eax.
    private static void WriteFakeNeighborHeight(ulong at)
    {
        var a = new Assembler(64);
        a.mov(r10, (ulong)callRecord);
        a.mov(__dword_ptr[r10], ecx); a.mov(__dword_ptr[r10 + 4], edx);
        a.mov(r11, rsp); a.and(r11d, 15); a.mov(__dword_ptr[r10 + 8], r11d);
        a.mov(r11, 0x7777777777777777UL);
        a.mov(__qword_ptr[rsp + 8], r11); a.mov(__qword_ptr[rsp + 16], r11); a.mov(__qword_ptr[rsp + 24], r11); a.mov(__qword_ptr[rsp + 32], r11);
        a.mov(eax, __dword_ptr[r10 + 12]);
        a.mov(rcx, 0xDEAD); a.mov(rdx, 0xBEEF);
        a.ret();
        Write(a, at);
    }

    private static void WriteJump(ulong at, ulong target)
    {
        var a = new Assembler(64);
        a.jmp(target);
        Write(a, at);
    }

    private static void Write(Assembler a, ulong at)
    {
        byte[] bytes = NativeCode.Assemble(a, at);
        Marshal.Copy(bytes, 0, (IntPtr)(long)at, bytes.Length);
    }

    private static void CheckFileImage(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        using (var sha = System.Security.Cryptography.SHA256.Create())
            Check(BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "") == NativeCode.Hash, "file image: DLL hash differs from the audited contract");
        foreach (var site in NativeCode.Sites)
        {
            int offset = FileOffset(raw, site.Rva);
            Check(offset >= 0 && raw.Skip(offset).Take(site.Length).SequenceEqual(site.Bytes), $"file image: {site.Name} bytes differ");
            Check(offset >= 0 && raw.Skip(offset + site.Length).Take(site.Following.Length).SequenceEqual(site.Following), $"file image: {site.Name} continuation differs");
            NativeCode.Decode(site.Bytes, (ulong)site.Rva, site.InstructionLengths);
        }
    }

    private static int FileOffset(byte[] raw, int rva)
    {
        int pe = BitConverter.ToInt32(raw, 0x3C), count = BitConverter.ToUInt16(raw, pe + 6);
        int table = pe + 24 + BitConverter.ToUInt16(raw, pe + 20);
        for (int n = 0; n < count; n++)
        {
            int h = table + n * 40, start = BitConverter.ToInt32(raw, h + 12), size = BitConverter.ToInt32(raw, h + 16);
            if (rva >= start && rva < start + size) return BitConverter.ToInt32(raw, h + 20) + rva - start;
        }
        return -1;
    }

    // ---- small helpers -------------------------------------------------------------------

    private static NativeCode.Site Find(NativeCode.SiteKind kind) => NativeCode.Sites.Single(s => s.Kind == kind);
    private static void Set(int register, ulong value) => input[register] = value;
    private static ulong CmpFlags(byte left, byte right)
    {
        ulong flags = 0x202;
        if (left < right) flags |= 0x1;
        if (left == right) flags |= 0x40;
        return flags;
    }
    private static IntPtr HeightAddress(int tile) => (IntPtr)(tiles + NativeCode.HeightOffset + tile);
    private static IntPtr LogicAddress(int tile) => (IntPtr)(tiles + NativeCode.LogicOffset + tile * 4);
    private static IntPtr MiscAddress(int tile) => (IntPtr)(tiles + NativeCode.MiscDisplayOffset + tile * 2);
    private static ref byte Height(int tile) => ref tiles[NativeCode.HeightOffset + tile];
    private static ref byte Default(int tile) => ref tiles[NativeCode.DefaultHeightOffset + tile];
    private static ref int Logic(int tile) => ref *(int*)(tiles + NativeCode.LogicOffset + tile * 4);
    private static ref ushort Structure(int tile) => ref *(ushort*)(tiles + NativeCode.StructureOffset + tile * 2);
    private static ref ushort Misc(int tile) => ref *(ushort*)(tiles + NativeCode.MiscDisplayOffset + tile * 2);

    private static void Check(bool condition, string message)
    {
        checks++;
        if (condition) return;
        failures++;
        Console.WriteLine("FAIL: " + message);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
}
