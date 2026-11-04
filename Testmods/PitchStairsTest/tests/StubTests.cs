using Iced.Intel;
using PitchStairsTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

// Executes every displaced Vanilla block and its stub in one harness. Disabled stubs must equal
// Vanilla exactly (registers, flags, stack, memory, exit); enabled stubs may differ only where
// variant B intends it. Also checks the site bytes in the installed CrusaderDE.dll file image.
internal static unsafe class StubTests
{
    private const int Rax = 0, Rbx = 1, Rcx = 2, Rdx = 3, Rsi = 4, Rdi = 5, Rbp = 6, R8 = 7, R9 = 8, R10 = 9;
    private const int R11 = 10, R12 = 11, R13 = 12, R14 = 13, R15 = 14, Flags = 15, Rsp = 16, Exit = 17, ContextSlots = 18;
    private const ulong FlagMask = 0x8D5; // CF PF AF ZF SF OF
    private const int CodeSize = 0x200000, SiteOffset = 0x100000, StubOffset = 0x180000, TileSize = 0x2050000;
    private const int Tile = 0x40; // packed tile index used by all cases
    private const int StateDataOffset = 0x10000; // Outside the row fixture at data+0x8000.

    private static byte* code, tiles, data;
    private static ulong* input, output, hostRsp;
    private static int* callRecord; // rcx, rdx, rsp low, return value
    private static int failures, checks;
    private static bool diagCounters; // emit option for the next Run, see FireSource/FireNeighbors

    private static int Main(string[] args)
    {
        string dll = args.Length > 0 ? args[0] :
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        code = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)CodeSize, 0x3000, 0x40);
        tiles = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)TileSize, 0x3000, 0x04);
        data = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)0x60000, 0x3000, 0x04);
        if (code == null || tiles == null || data == null) throw new InvalidOperationException("VirtualAlloc failed.");
        input = (ulong*)data; output = input + 32; hostRsp = output + 32; callRecord = (int*)(hostRsp + 4);

        CheckFileImage(dll);
        Lowering(); AivRestore(); ManualRestore(); LoadNormalization(); Render(); Placement(); FireSource(); FireNeighbors();
        BurnoutRestore(); PitchChain(); LoadBootstrap(); MarkerAndBounds();
        DiagVariants(); ReferenceRule(); NativeLifecycle(dll);
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
                Marker(Tile) = 7;
                Set(Rdx, height); Set(R9, 0); Set(R8, Tile); Set(Rsi, (ulong)tiles);
                // Flags of Vanilla's preceding cmp dl,4.
                Set(Flags, CmpFlags(height, 4));
            }, enabled => new Expect
            {
                Memory = { [HeightAddress(Tile)] = enabled ? height : (byte)Math.Max(0, height - 4) },
                SameFlags = true,
                StateBytes = { [NativeCode.RestoredPitchOffset + Tile] = enabled ? (byte)0 : (byte)7 },
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
            Compare(site, edge ? "visible pitch" : "hidden pitch", () =>
            {
                Misc(Tile) = (ushort)(edge ? NativeCode.PitchVisibleBit : 0);
                // rbp is the image base in Vanilla; the misc grid sits at image + TileManager + MiscDisplay.
                Set(Rbp, (ulong)tiles - NativeCode.TileManagerRva); Set(R13, Tile); Set(Rsi, 8); Set(Flags, 0x202);
            }, enabled => new Expect
            {
                Registers = { [Rsi] = (ulong)(edge || enabled ? 8 : 12) },
                SameFlags = edge,
                Exit = 1,
            });
    }

    // The DEVDIAG fire-path counters must not change any register, flag, memory or exit result.
    private static void FireSource()
    {
        var site = Find(NativeCode.SiteKind.FireSource);
        // The visibility bit (0x2000) differs per client and must not change the result.
        var cases = new (byte Height, int Logic, ushort Misc, ushort Direction, ulong Expected)[]
        {
            (8, 0x8, 0, 0, 4), (8, 0x0, 0, 0, 8), (8, 0x8, 0x2000, 0, 4), (8, 0x0, 0x2000, 0, 8), (2, 0x8, 0, 0, 0), (8, 0x8, 0, 4, 4),
        };
        foreach (bool diag in new[] { false, true })
        foreach (var c in cases)
        {
            diagCounters = diag;
            bool pitch = (c.Logic & NativeCode.PitchTrapFlag) != 0, visible = pitch && c.Misc != 0;
            Compare(site, $"height {c.Height} logic 0x{c.Logic:X} misc 0x{c.Misc:X} direction {c.Direction} diag={diag}", () =>
            {
                Height(Tile) = c.Height; Logic(Tile) = c.Logic; Misc(Tile) = c.Misc;
                Set(Rcx, Tile); Set(R15, (ulong)tiles); Set(Rax, c.Direction); Set(R11, 0x1111); Set(Flags, 0x202);
            }, enabled => new Expect
            {
                Registers = { [Rdi] = enabled ? c.Expected : c.Height },
                SameFlags = true,
                Exit = c.Direction >= 4 ? 2 : 1,
                Counters = FireCounters(adjusted: enabled && pitch, sourceCalls: diag,
                    sourcePitch: diag && enabled && pitch, sourceVisible: diag && enabled && visible, neighborCalls: false),
            });
        }
        diagCounters = false;
    }

    private static Dictionary<int, long> FireCounters(bool adjusted, bool sourceCalls, bool sourcePitch, bool sourceVisible, bool neighborCalls,
        bool neighborAdjusted = false) => new Dictionary<int, long>
    {
        [NativeCode.FireSourceAdjustedOffset] = adjusted ? 1 : 0,
        [NativeCode.FireNeighborAdjustedOffset] = neighborAdjusted ? 1 : 0,
        [NativeCode.FireSourceCallsOffset] = sourceCalls ? 1 : 0,
        [NativeCode.FireSourcePitchOffset] = sourcePitch ? 1 : 0,
        [NativeCode.FireSourcePitchVisibleOffset] = sourceVisible ? 1 : 0,
        [NativeCode.FireNeighborCallsOffset] = neighborCalls ? 1 : 0,
    };

    private static void FireNeighbors()
    {
        foreach (bool diag in new[] { false, true })
        foreach (var site in NativeCode.Sites.Where(s => s.Kind == NativeCode.SiteKind.FireNeighbor))
        {
            diagCounters = diag;
            bool rowSite = site.InstructionLengths.Length == 3;
            var cases = new (int Logic, ushort Misc, ushort Structure, int Returned, int Expected)[]
            {
                (0x8, 0, 0, 8, 4), (0x0, 0, 0, 8, 8), (0x8, 0x2000, 0, 8, 4), (0x0, 0x2000, 0, 8, 8), (0x8, 0, 5, 30, 30), (0x8, 0, 0, 2, 0),
            };
            foreach (var c in cases)
                Compare(site, $"logic 0x{c.Logic:X} misc 0x{c.Misc:X} structure {c.Structure} returned {c.Returned} diag={diag}", () =>
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
                    Counters = FireCounters(adjusted: false, sourceCalls: false, sourcePitch: false, sourceVisible: false, neighborCalls: diag,
                        neighborAdjusted: enabled && c.Logic != 0 && c.Structure == 0),
                });
        }
        diagCounters = false;
    }

    private static void BurnoutRestore()
    {
        foreach (byte ground in new byte[] { 0, 3, 4, 8 })
            Compare(Find(NativeCode.SiteKind.BurnoutRestore), "default " + ground, () =>
            {
                Default(Tile) = ground; Height(Tile) = 0; Marker(Tile) = 7;
                Set(Rcx, Tile); Set(R13, (ulong)tiles - NativeCode.TileManagerRva); Set(Flags, 0xA93);
            }, enabled => new Expect
            {
                Registers = { [Rax] = ground }, Memory = { [HeightAddress(Tile)] = ground }, SameFlags = true,
                StateBytes = { [NativeCode.RestoredPitchOffset + Tile] = enabled ? (byte)1 : (byte)7 },
            });
    }

    private static void PitchChain()
    {
        foreach (int source in new[] { 0, 3, 8, 28, 255 })
        foreach (byte target in new byte[] { 0, 3, 27, 28, 32, 33, 231 })
            Compare(Find(NativeCode.SiteKind.PitchChain), $"{source}/{target}", () =>
            {
                Height(Tile) = target; Set(R14, Tile); Set(R13, (ulong)tiles - NativeCode.TileManagerRva);
                Set(R12, (ulong)source); Set(Flags, 0xA93);
            }, enabled => new Expect
            {
                Registers =
                {
                    [Rax] = (uint)((enabled ? Math.Max(0, source - 4) : source) - (enabled ? Math.Max(0, target - 4) : target)),
                    [Rcx] = (ulong)(enabled ? Math.Max(0, target - 4) : target),
                },
                SameFlags = !enabled,
            });
    }

    private static void LoadBootstrap()
    {
        foreach (int limit in new[] { -1, 0, 1, 2, 3, 4000, int.MaxValue })
            Compare(Find(NativeCode.SiteKind.LoadBootstrap), "high-water " + limit, () =>
            {
                new Span<byte>(data + StateDataOffset + NativeCode.RestoredPitchOffset, NativeCode.TileCapacity).Fill(7);
                new Span<byte>(tiles + NativeCode.PitchRecordsOffset, NativeCode.PitchSlotCapacity * NativeCode.PitchRecordStride).Clear();
                *(int*)(tiles + NativeCode.PitchLimitOffset) = limit;
                Default(Tile) = 3;
                PitchRecord(1, Tile, 1, -1); PitchRecord(2, Tile + 1, 1, -1); Default(Tile + 1) = 4;
                PitchRecord(3996, NativeCode.TileCapacity - 1, 1, -1); Default(NativeCode.TileCapacity - 1) = 3;
                PitchRecord(3997, -1, 1, -1); PitchRecord(3998, int.MaxValue, 1, -1);
                PitchRecord(3999, Tile + 2, 0, -1); Default(Tile + 2) = 3;
                Set(Flags, 0xA93);
            }, enabled => new Expect
            {
                Registers = { [R9] = (ulong)(tiles + NativeCode.HeightOffset), [R11] = NativeCode.TileCapacity },
                SameFlags = true,
                StateBytes =
                {
                    [NativeCode.RestoredPitchOffset + Tile] = enabled ? (byte)(limit > 1 ? 1 : 0) : (byte)7,
                    [NativeCode.RestoredPitchOffset + Tile + 1] = enabled ? (byte)0 : (byte)7,
                    [NativeCode.RestoredPitchOffset + Tile + 2] = enabled ? (byte)0 : (byte)7,
                    [NativeCode.RestoredPitchOffset] = enabled ? (byte)0 : (byte)7,
                    [NativeCode.RestoredPitchOffset + NativeCode.TileCapacity - 1] = enabled ? (byte)(limit > 3996 ? 1 : 0) : (byte)7,
                },
            });
    }

    private static void MarkerAndBounds()
    {
        var source = Find(NativeCode.SiteKind.FireSource);
        foreach (bool diag in new[] { false, true })
        {
            diagCounters = diag;
            Compare(source, "restored source diag=" + diag, () =>
            {
                Height(Tile) = 8; Logic(Tile) = NativeCode.PitchTrapFlag; Marker(Tile) = 1;
                Set(Rcx, Tile); Set(R15, (ulong)tiles); Set(Rax, 0);
            }, enabled => new Expect
            {
                Registers = { [Rdi] = 8 }, SameFlags = true,
                Counters = FireCounters(false, diag, diag && enabled, false, false),
            });
        }
        diagCounters = false;
        foreach (int tile in new[] { -1, NativeCode.TileCapacity, int.MaxValue })
        {
            Compare(source, "bounded source " + tile, () =>
            {
                Height(Tile) = 8; Marker(Tile) = 7;
                // Rebase the original read onto valid fixture storage; any extra invalid grid/map
                // read would still fail. Vanilla's original load remains outside our new guard.
                Set(Rcx, unchecked((ulong)(long)tile)); Set(R15, unchecked((ulong)tiles + Tile - (ulong)(long)tile)); Set(Rax, 0);
            }, enabled => new Expect { Registers = { [Rdi] = 8 }, SameFlags = true,
                StateBytes = { [NativeCode.RestoredPitchOffset + Tile] = 7 } });
            Compare(Find(NativeCode.SiteKind.BurnoutRestore), "bounded restore " + tile, () =>
            {
                Default(Tile) = 8; Marker(Tile) = 7;
                Set(Rcx, unchecked((ulong)(long)tile));
                Set(R13, unchecked((ulong)tiles - NativeCode.TileManagerRva + Tile - (ulong)(long)tile));
            }, enabled => new Expect { Registers = { [Rax] = 8 }, Memory = { [HeightAddress(Tile)] = 8 }, SameFlags = true,
                StateBytes = { [NativeCode.RestoredPitchOffset + Tile] = 7 } });
            Compare(Find(NativeCode.SiteKind.Lowering), "bounded placement " + tile, () =>
            {
                Height(Tile) = 8; Marker(Tile) = 7; Set(R8, unchecked((ulong)(long)tile));
                Set(Rsi, unchecked((ulong)tiles + Tile - (ulong)(long)tile)); Set(Rdx, 8); Set(R9, 0); Set(Flags, CmpFlags(8, 4));
            }, enabled => new Expect { Memory = { [HeightAddress(Tile)] = 4 }, SameFlags = true,
                StateBytes = { [NativeCode.RestoredPitchOffset + Tile] = 7 } });
        }
        foreach (var site in NativeCode.Sites.Where(s => s.Kind == NativeCode.SiteKind.FireNeighbor))
        foreach (int tile in new[] { -1, 0, NativeCode.TileCapacity - 1, NativeCode.TileCapacity, int.MaxValue })
        foreach (byte restored in new byte[] { 0, 1 })
        {
            bool row = site.InstructionLengths.Length == 3, valid = (uint)tile < NativeCode.TileCapacity;
            Compare(site, "bounded neighbor " + tile + " marker=" + restored, () =>
            {
                // Even invalid native neighbors may query padded data. Their own state must never be read.
                if (valid) { Logic(tile) = NativeCode.PitchTrapFlag; Marker(tile) = restored; }
                callRecord[3] = 8; Set(R15, (ulong)tiles); Set(Rdi, 32);
                if (row)
                {
                    int displacement = (int)NativeCode.Decode(site.Bytes, 0, site.InstructionLengths)[0].MemoryDisplacement64;
                    *(int*)(data + 0x8000) = 3;
                    Set(Rbx, (ulong)(data + 0x8000) - (ulong)displacement); Set(Rcx, 0); Set(Rdx, (uint)(tile - 3));
                }
                else Set(Rcx, (uint)(tile + (site.Bytes[2] == 0xFF ? 1 : -1)));
            }, enabled => new Expect
            {
                Registers = row ? new Dictionary<int, ulong> { [Rax] = (ulong)(enabled && valid && restored == 0 ? 4 : 8) } :
                    new Dictionary<int, ulong> { [Rax] = (ulong)(enabled && valid && restored == 0 ? 28 : 24), [Rdi] = (ulong)(enabled && valid && restored == 0 ? 28 : 24) },
                IgnoreRegisters = enabled && valid && restored == 0 ? new[] { Rcx } : null,
                SameFlags = !(enabled && valid && restored == 0), CallArguments = true,
            });
        }
    }

    private static void PitchRecord(int id, int tile, short owner, short state)
    {
        byte* record = tiles + NativeCode.PitchRecordsOffset + id * NativeCode.PitchRecordStride;
        *(int*)record = tile; *(short*)(record + NativeCode.PitchOwnerOffset) = owner;
        *(short*)(record + NativeCode.PitchStateOffset) = state;
    }

    // Release stubs (diagCounters false) of all non-fire sites must not depend on the DEVDIAG option.
    private static void DiagVariants()
    {
        foreach (var site in NativeCode.Sites)
        {
            ulong ip = (ulong)(code + StubOffset);
            Instruction[] original = NativeCode.Decode(site.Bytes, (ulong)(code + SiteOffset), site.InstructionLengths);
            byte[] Build(bool diag)
            {
                var a = new Assembler(64);
                NativeCode.Emit(a, site, original, 0x1000, (ulong)(code + SiteOffset) + (ulong)site.Length, diag);
                return NativeCode.Assemble(a, ip);
            }
            bool fire = site.Kind == NativeCode.SiteKind.FireSource || site.Kind == NativeCode.SiteKind.FireNeighbor;
            Check(Build(false).SequenceEqual(Build(true)) != fire, $"{site.Name}: diag option {(fire ? "adds counters" : "changes no bytes")}");
        }
    }

    private static void ReferenceRule()
    {
        Check(NativeCode.IsVanillaLoweredPitch(4, 8, 0x8, 0), "rule: lowered pitch");
        Check(!NativeCode.IsVanillaLoweredPitch(8, 8, 0x8, 0), "rule: B pitch");
        Check(NativeCode.IsVanillaLoweredPitch(0, 3, 0x8, 0), "rule: clamped low pitch");
        Check(!NativeCode.IsVanillaLoweredPitch(0, 3, 0x8, 7), "rule: structure on low pitch");
        Check(!NativeCode.IsVanillaLoweredPitch(0, 0, 0x8, 0), "rule: default zero");
        Check(!NativeCode.IsVanillaLoweredPitch(4, 8, 0x0, 0), "rule: no pitch");

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

    // Execute production generators in a private, relocated copy of the exact installed PE. These
    // are complete native constructor/load/update paths, not a second implementation of the rules.
    private static void NativeLifecycle(string dll)
    {
        var world = new NativeWorld(File.ReadAllBytes(dll));
        world.ProbeBackend();
        var loaded = new Dictionary<string, byte>();
        foreach (int mode in new[] { 0, 1, 2 }) // pristine, installed-disabled, installed-enabled
        {
            world.Rules(mode);
            foreach (byte ground in new byte[] { 0, 1, 2, 3, 4, 8 })
            foreach (short phase in new short[] { 1, 3, -1 })
            {
                world.Reset(); int tile = PackedTile(400, 400);
                world.Ground(tile, ground);
                int id = world.Create(400, 400);
                world.Phase(id, phase, 0);
                if (phase == -1) world.Height(tile) = ground;
                world.Markers.Fill(7); // Load must reconstruct, rather than preserve process history.
                world.Load();
                string key = ground + "/" + phase;
                if (mode == 0) loaded[key] = world.Height(tile);
                else
                {
                    byte effective = mode == 2 ? world.VirtualHeight(tile) : world.Height(tile);
                    Check(effective == loaded[key], $"native load mode={mode} {key}: {effective} != {loaded[key]}");
                    if (mode == 2)
                        Check(world.Marker(tile) == (phase == -1 && ground < 4 ? 1 : 0), $"native load marker {key}");
                    else Check(world.Markers.ToArray().All(v => v == 7), "native disabled load leaves marker map untouched");
                }
            }
        }

        var chainReference = new Dictionary<string, bool>();
        foreach (int mode in new[] { 0, 1, 2 })
        {
            world.Rules(mode);
            foreach (var pair in new[] { (0, 28), (3, 28), (28, 3), (8, 32), (8, 33), (3, 27), (255, 231) })
            {
                world.Reset(); int source = PackedTile(400, 400), target = PackedTile(401, 400);
                world.Ground(source, (byte)pair.Item1); world.Ground(target, (byte)pair.Item2);
                int a = world.Create(400, 400), b = world.Create(401, 400);
                world.Phase(a, 2, 19); world.Phase(b, 1, 0); world.Update();
                bool ignited = world.GetPhase(b) == 2;
                string key = pair.Item1 + "/" + pair.Item2;
                if (mode == 0) chainReference[key] = ignited;
                else Check(ignited == chainReference[key], $"native chain mode={mode} {key}");
            }
        }
        foreach (int mode in new[] { 0, 1, 2 })
        {
            world.Rules(mode);
            foreach (byte ground in new byte[] { 0, 3, 4, 8 })
            {
                world.Reset(); int tile = PackedTile(400, 400); world.Ground(tile, ground);
                int id = world.Create(400, 400); world.Phase(id, 3, 199); world.Update();
                Check(world.Height(tile) == ground && world.GetPhase(id) == -1 && world.HasPitch(tile), $"native terminal mode={mode} ground={ground}");
                Check(world.Marker(tile) == (mode == 2 ? 1 : 0), "native terminal marker follows exact restore");
                if (mode == 2) Check(world.VirtualHeight(tile) == ground, "native terminal fire plane remains restored");
                world.Update();
                Check(!world.HasPitch(tile), "native next-tick cleanup clears pitch");
                id = world.Create(400, 400);
                Check(id > 0 && world.Marker(tile) == 0, "native replacement clears stale terminal marker");
                Check(world.Height(tile) == (mode == 2 ? ground : Math.Max(0, ground - 4)), "native replacement height");
            }
        }
        Console.WriteLine("PASS: complete private native load, chain, burnout, cleanup and replacement fixtures.");
        world.PhysicsBoundaries();
    }

    private static int PackedTile(int x, int y) =>
        (y < 400 ? y * (y + 1) : NativeCode.TileCapacity - (800 - y) * (801 - y)) + x - (y < 400 ? 399 - y : y - 400);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong NativeCreatePitch(IntPtr manager, short player, uint x, uint y);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativePitchUpdate(IntPtr manager);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeUnitHeight(IntPtr manager, int id, byte moving);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeStandingHeight(IntPtr manager, int id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long NativeUnitQuery(IntPtr manager, int id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long NativeProjectileDamage(IntPtr manager, int id, int projectileSlot, int radius);

    private sealed class NativeWorld
    {
        private readonly byte* image;
        private readonly NativeCreatePitch create;
        private readonly NativePitchUpdate update;
        private readonly Action load;
        private bool enabled;
        internal NativeWorld(byte[] raw)
        {
            int pe = BitConverter.ToInt32(raw, 0x3C), optional = pe + 24;
            int size = BitConverter.ToInt32(raw, optional + 56), headers = BitConverter.ToInt32(raw, optional + 60);
            image = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)size, 0x3000, 0x40);
            if (image == null) throw new InvalidOperationException("Private native PE allocation failed.");
            Marshal.Copy(raw, 0, (IntPtr)image, headers);
            int table = optional + BitConverter.ToUInt16(raw, pe + 20), sections = BitConverter.ToUInt16(raw, pe + 6);
            for (int s = 0; s < sections; s++)
            {
                int h = table + s * 40;
                Marshal.Copy(raw, BitConverter.ToInt32(raw, h + 20), (IntPtr)(image + BitConverter.ToInt32(raw, h + 12)), BitConverter.ToInt32(raw, h + 16));
            }
            ulong delta = (ulong)image - BitConverter.ToUInt64(raw, optional + 24);
            int reloc = BitConverter.ToInt32(raw, optional + 112 + 5 * 8), length = BitConverter.ToInt32(raw, optional + 116 + 5 * 8);
            for (int offset = reloc; offset < reloc + length;)
            {
                int page = *(int*)(image + offset), bytes = *(int*)(image + offset + 4);
                if (bytes < 8) throw new InvalidOperationException("Private native PE relocation block differs.");
                for (int r = 8; r < bytes; r += 2)
                {
                    ushort fix = *(ushort*)(image + offset + r);
                    if (fix >> 12 == 10) *(ulong*)(image + page + (fix & 0xFFF)) += delta;
                }
                offset += bytes;
            }
            create = Marshal.GetDelegateForFunctionPointer<NativeCreatePitch>((IntPtr)(image + 0x59380));
            update = Marshal.GetDelegateForFunctionPointer<NativePitchUpdate>((IntPtr)(image + 0x7D280));
            load = Marshal.GetDelegateForFunctionPointer<Action>((IntPtr)(image + 0x804C0));
            // Isolate the chain decision from effect allocation/RNG. Both reference and patched
            // paths execute this same no-op leaf; native target-state writes remain original.
            image[0x9C430] = 0xC3;
            for (int y = 0; y < 800; y++)
            {
                int first = PackedTile(y < 400 ? 399 - y : y - 400, y), row = first - (y < 400 ? 399 - y : y - 400);
                *(int*)(image + 0x402FF2C + y * 12) = row;
                *(int*)(image + 0x402FF28 + y * 12) = first;
                int left = y < 400 ? 399 - y : y - 400, width = y < 400 ? 2 * y + 2 : 2 * (800 - y);
                new Span<byte>(image + 0x3A11EA4 + y * 800 + left, width).Fill(1);
                for (int t = first; t < first + width; t++) *(ushort*)(image + 0x3AAE2A4 + t * 2) = (ushort)y;
                int previous = y == 0 ? 0 : RowBase(y - 1) - row, next = y == 799 ? 0 : RowBase(y + 1) - row;
                int[] deltas = { previous, previous + 1, 1, next + 1, next, next - 1, -1, previous - 1 };
                for (int d = 0; d < deltas.Length; d++) *(int*)(Manager + y * 32 + d * 4) = deltas[d];
            }
            *(int*)(image + 0x402FF28 + 800 * 12) = NativeCode.TileCapacity;
            *(int*)(image + 0x60AD660) = 6;
        }
        private static int RowBase(int y)
        {
            int left = y < 400 ? 399 - y : y - 400;
            return PackedTile(left, y) - left;
        }
        internal Span<byte> Markers => new Span<byte>(data + StateDataOffset + NativeCode.RestoredPitchOffset, NativeCode.TileCapacity);
        private byte* Manager => image + NativeCode.TileManagerRva;
        internal ref byte Height(int tile) => ref Manager[NativeCode.HeightOffset + tile];
        internal ref byte Marker(int tile) => ref data[StateDataOffset + NativeCode.RestoredPitchOffset + tile];
        internal byte VirtualHeight(int tile) => Marker(tile) != 0 ? Height(tile) : (byte)Math.Max(0, Height(tile) - 4);
        internal void Ground(int tile, byte height) { Height(tile) = height; Manager[NativeCode.DefaultHeightOffset + tile] = height; }
        internal int Create(int x, int y) => (int)create((IntPtr)Manager, 1, (uint)x, (uint)y);
        internal void Phase(int id, short phase, short timer)
        {
            byte* record = Manager + NativeCode.PitchRecordsOffset + id * NativeCode.PitchRecordStride;
            *(short*)(record + NativeCode.PitchStateOffset) = phase; *(short*)(record + NativeCode.PitchStateOffset + 2) = timer;
        }
        internal short GetPhase(int id) => *(short*)(Manager + NativeCode.PitchRecordsOffset + id * NativeCode.PitchRecordStride + NativeCode.PitchStateOffset);
        internal bool HasPitch(int tile) => (*(int*)(Manager + NativeCode.LogicOffset + tile * 4) & NativeCode.PitchTrapFlag) != 0;
        internal void Update() => update((IntPtr)Manager);
        internal void Load() => load();
        internal void Reset()
        {
            new Span<byte>(Manager + NativeCode.HeightOffset, NativeCode.TileCapacity).Fill(8);
            new Span<byte>(Manager + NativeCode.DefaultHeightOffset, NativeCode.TileCapacity).Fill(8);
            new Span<byte>(Manager + NativeCode.StructureOffset, NativeCode.TileCapacity * 2).Clear();
            new Span<byte>(Manager + NativeCode.MiscDisplayOffset, NativeCode.TileCapacity * 2).Clear();
            for (int t = 0; t < NativeCode.TileCapacity; t++) *(int*)(Manager + NativeCode.LogicOffset + t * 4) = 0x8000;
            new Span<byte>(Manager + NativeCode.PitchRecordsOffset, NativeCode.PitchSlotCapacity * NativeCode.PitchRecordStride).Clear();
            *(int*)(Manager + NativeCode.PitchLimitOffset) = 1;
            new Span<byte>(data + StateDataOffset, NativeCode.StateSize).Clear();
            *(int*)(data + StateDataOffset) = enabled ? 1 : 0;
        }
        internal void ProbeBackend()
        {
            foreach (var site in NativeCode.Sites)
                using (var hook = new X64InlineHook((ulong)(image + site.Rva), site.Length))
                    Check(!hook.IsInstalled && hook.DisplacedByteCount == site.Length, "actual RedBird span: " + site.Name);
        }
        internal void Rules(int mode)
        {
            enabled = mode == 2;
            foreach (var site in NativeCode.Sites)
            {
                Marshal.Copy(site.Bytes, 0, (IntPtr)(image + site.Rva), site.Length);
                if (mode == 0) continue;
                ulong address = (ulong)(image + site.Rva), target = (ulong)VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
                if (target == 0) throw new InvalidOperationException("Private native stub allocation failed.");
                var a = new Assembler(64);
                NativeCode.Emit(a, site, NativeCode.Decode(site.Bytes, address, site.InstructionLengths),
                    (ulong)(data + StateDataOffset), address + (ulong)site.Length);
                byte[] bytes = NativeCode.Assemble(a, target); Marshal.Copy(bytes, 0, (IntPtr)(long)target, bytes.Length);
                byte* code = image + site.Rva;
                code[0] = 0xFF; code[1] = 0x25; *(uint*)(code + 2) = 0; *(ulong*)(code + 6) = target;
                for (int i = 14; i < site.Length; i++) code[i] = 0x90;
            }
        }

        // Ordinary height consumers deliberately keep real B terrain/standing heights. Compare
        // complete native paths with normal ground, including the old Vanilla depression as a
        // counterexample; do not add blanket virtual-height compensation to these consumers.
        internal void PhysicsBoundaries()
        {
            OrdinaryProjectileHeights(); DiseaseSurfaceAdmission(); RangedHeightCorrection();
        }

        private byte* Units => image + 0x67E8400;
        private byte* Unit(int id) => Units + id * 0x490; // Native sentinel-based manager, not span index.
        private byte* Projectile(int slot) => image + 0x2F7A680 + slot * 0xE8;
        private void ResetPhysics()
        {
            Reset();
            new Span<byte>(Units + 0x65C, 3 * 0x490).Clear();
            new Span<byte>(image + 0x4C559B0, NativeCode.TileCapacity * 2).Clear();
            new Span<byte>(image + 0x4CF23F0, NativeCode.TileCapacity * 2).Clear();
            new Span<byte>(Projectile(25), 0xE8).Clear();
            new Span<byte>(data + 0x9000, 32).Clear();
            *(int*)(image + 0x37EDF3C + 4) = 1; *(int*)(image + 0x37EDF3C + 8) = 2;
            *(int*)(image + 0x88E3D70) = 0;
            *(int*)(image + 0x379CF04 + 2 * 0x583C) = 0; // Source-player fear damage adjustment.
        }

        private void PrepareUnit(int id, int x, int y, short player, short type)
        {
            byte* unit = Unit(id);
            *(short*)(unit + 0x6E4) = 2; *(short*)(unit + 0x6E6) = type; *(short*)(unit + 0x6EE) = player;
            *(int*)(unit + 0x6F0) = id * 111;
            *(short*)(unit + 0x70E) = (short)(x * 8 + 4); *(short*)(unit + 0x710) = (short)(y * 8 + 4);
            *(short*)(unit + 0x71C) = (short)x; *(short*)(unit + 0x71E) = (short)y;
            *(int*)(unit + 0x72C) = PackedTile(x, y);
            *(int*)(unit + 0xA20) = 10000; *(int*)(unit + 0xA24) = 10000;
            var updateHeight = Marshal.GetDelegateForFunctionPointer<NativeUnitHeight>((IntPtr)(image + 0x19B260));
            // The actual movement updater interpolates small changes; let it reach its stable plane.
            for (int step = 0; step < 16; step++) updateHeight((IntPtr)Units, id, 0);
            Marshal.GetDelegateForFunctionPointer<NativeStandingHeight>((IntPtr)(image + 0x184FD0))((IntPtr)Units, id);
        }

        private byte[] UnitRecord(int id)
        {
            var bytes = new byte[0x490]; Marshal.Copy((IntPtr)(Unit(id) + 0x65C), bytes, 0, bytes.Length); return bytes;
        }

        private void OrdinaryProjectileHeights()
        {
            // Native hit/death processing requests audio/HUD output; those presentation managers
            // require game startup. Their sinks are outside the hit-band/damage behavior under test.
            ReplacePrivateLeaf(0x2960, a => { a.ret(); });
            ReplacePrivateLeaf(0x2940, a => { a.ret(); });
            ReplacePrivateLeaf(0x4180, a => { a.ret(); });
            var hit = Marshal.GetDelegateForFunctionPointer<NativeProjectileDamage>((IntPtr)(image + 0x192750));
            var ground = new Dictionary<int, byte[]>(); var oldHit = new Dictionary<int, bool>();
            // Ground reference first, then enabled pitch, then original depressed pitch.
            foreach (int scenario in new[] { 0, 1, 2 })
            {
                Rules(scenario == 2 ? 0 : 2);
                foreach (short elevation in new short[] { 3, 4, 7, 8, 46, 47, 50, 51 })
                {
                    ResetPhysics(); if (scenario != 0) Create(400, 400);
                    PrepareUnit(1, 400, 400, 1, (short)SHCDESE.Interop.eChimps.CHIMP_TYPE_PEASANT);
                    PrepareUnit(2, 401, 400, 2, (short)SHCDESE.Interop.eChimps.CHIMP_TYPE_PEASANT);
                    byte* projectile = Projectile(25);
                    *(short*)(projectile + 0x3E) = (short)SHCDESE.Interop.Enums.ProjectileType.ArcherArrow;
                    *(short*)(projectile + 0x40) = 2; *(short*)(projectile + 0x50) = elevation;
                    *(short*)(projectile + 0xB6) = 2; *(int*)(projectile + 0x60) = PackedTile(400, 400);
                    hit((IntPtr)Units, 1, 25, 0); // 25 is the native sentinel-based projectile slot.
                    bool damaged = *(int*)(Unit(1) + 0xA20) < 10000;
                    if (scenario == 0) ground[elevation] = UnitRecord(1);
                    else if (scenario == 1) Check(ground[elevation].SequenceEqual(UnitRecord(1)), "native ordinary projectile B equals ground: " + elevation);
                    else oldHit[elevation] = damaged;
                    Check(*(short*)(Unit(1) + 0x712) == (scenario == 2 ? 4 : 8), "native projectile unit reaches actual terrain plane");
                }
            }
            Check(oldHit[4] && oldHit[7] && !oldHit[47], "ordinary projectile reference retains old lower/upper hit band");
            Check(BitConverter.ToInt32(ground[7], 0xA20 - 0x65C) == 10000 && BitConverter.ToInt32(ground[47], 0xA20 - 0x65C) < 10000,
                "ordinary projectile B/ground moves the real hit band up by four");
            Console.WriteLine("PASS: complete native ordinary projectile damage: B pitch equals ground at lower/upper hit-band boundaries.");
        }

        private void ReplacePrivateLeaf(int rva, Action<Assembler> emit)
        {
            var a = new Assembler(64); emit(a); byte[] bytes = NativeCode.Assemble(a, (ulong)(image + rva));
            Marshal.Copy(bytes, 0, (IntPtr)(image + rva), bytes.Length);
        }

        private void DiseaseSurfaceAdmission()
        {
            // Admission only: record the original 9C730 -> 192750 call, do not pretend that this
            // fixture proves Disease damage or immunity. Center, neighbors and ring stay native.
            ReplacePrivateLeaf(0x192750, a =>
            {
                a.mov(r10, (ulong)(data + 0x9000)); a.inc(__dword_ptr[r10]); a.mov(__dword_ptr[r10 + 4], edx);
                a.mov(__dword_ptr[r10 + 8], r8d); a.mov(__dword_ptr[r10 + 12], r9d); a.xor(eax, eax); a.ret();
            });
            var register = Marshal.GetDelegateForFunctionPointer<NativeUnitQuery>((IntPtr)(image + 0x9C730));
            var reference = new Dictionary<string, int>(); var old = new Dictionary<string, int>();
            foreach (int scenario in new[] { 0, 1, 2 })
            {
                Rules(scenario == 2 ? 0 : 2);
                foreach (int distance in new[] { 1, 2 })
                foreach (byte targetHeight in new byte[] { 123, 124, 127, 128 })
                {
                    ResetPhysics(); int targetTile = PackedTile(400 + distance, 400);
                    if (scenario != 0) Create(400, 400);
                    Ground(targetTile, targetHeight); PrepareUnit(1, 400 + distance, 400, 2, (short)SHCDESE.Interop.eChimps.CHIMP_TYPE_PEASANT);
                    *(short*)(image + 0x4C559B0 + targetTile * 2) = 1;
                    byte* projectile = Projectile(25);
                    *(short*)(projectile + 0x3C) = 2; *(short*)(projectile + 0x3E) = (short)SHCDESE.Interop.Enums.ProjectileType.Disease;
                    *(short*)(projectile + 0x40) = 1; *(short*)(projectile + 0x4C) = 3204; *(short*)(projectile + 0x4E) = 3204;
                    *(short*)(projectile + 0x50) = 28;
                    register((IntPtr)(image + 0x2F7A680), 25);
                    int calls = *(int*)(data + 0x9000); string key = distance + "/" + targetHeight;
                    if (scenario == 0) reference[key] = calls;
                    else if (scenario == 1) Check(calls == reference[key], "native Disease B admission equals ground " + key);
                    else old[key] = calls;
                    if (calls != 0)
                        Check(calls == 1 && *(int*)(data + 0x9004) == 1 && *(int*)(data + 0x9008) == 25 && *(int*)(data + 0x900C) == distance,
                            "native Disease keeps unit ID, projectile slot and radius " + key);
                    Check(*(int*)(Unit(1) + 0xA20) == 10000, "Disease admission spy applies no damage");
                }
            }
            foreach (int distance in new[] { 1, 2 })
                Check(old[distance + "/123"] == 1 && old[distance + "/124"] == 0 && reference[distance + "/124"] == 1 && reference[distance + "/128"] == 0,
                    "native Disease strict120 boundary moves with actual pitch surface, radius=" + distance);
            Console.WriteLine("PASS: complete native Disease surface admission: neighbor/ring B pitch equals ground; strict120 boundary retained.");
        }

        private void RangedHeightCorrection()
        {
            // Isolate geometry/target predicates from the full original ranged target state machine.
            // The raycast returns a valid hit and captures its height arguments; this does not test LOS.
            ReplacePrivateLeaf(0x199D50, a => { a.xor(eax, eax); a.ret(); });
            ReplacePrivateLeaf(0x1867A0, a => { a.xor(eax, eax); a.ret(); });
            ReplacePrivateLeaf(0xA06F0, a =>
            {
                a.mov(r10, (ulong)(data + 0x9000)); a.mov(__dword_ptr[r10], r9d);
                a.mov(eax, __dword_ptr[rsp + 56]); a.mov(__dword_ptr[r10 + 4], eax); a.mov(eax, 1); a.ret();
            });
            var select = Marshal.GetDelegateForFunctionPointer<NativeUnitQuery>((IntPtr)(image + 0x18E9A0));
            var reference = new Dictionary<string, short>(); var original = new Dictionary<string, short>();
            foreach (int scenario in new[] { 0, 1, 2 })
            {
                Rules(scenario == 2 ? 0 : 2);
                foreach (var pair in new[] { (8, 74), (8, 75), (8, 78), (8, 79), (80, 8), (80, 9), (80, 10), (80, 11) })
                {
                    ResetPhysics(); Ground(PackedTile(400, 400), (byte)pair.Item1); Ground(PackedTile(401, 400), (byte)pair.Item2);
                    if (scenario != 0) Create(400, 400);
                    PrepareUnit(1, 400, 400, 1, (short)SHCDESE.Interop.eChimps.CHIMP_TYPE_ARCHER);
                    PrepareUnit(2, 401, 400, 2, (short)SHCDESE.Interop.eChimps.CHIMP_TYPE_PEASANT);
                    *(short*)(Unit(1) + 0x9F4) = 4; *(short*)(Unit(1) + 0x9F6) = 2; *(int*)(Unit(1) + 0x9F8) = 222;
                    long accepted = select((IntPtr)Units, 1); short correction = *(short*)(Unit(1) + 0x9E2);
                    string key = pair.Item1 + "/" + pair.Item2;
                    Check(accepted == 1, "native ranged retained target accepted " + key);
                    if (scenario == 0) reference[key] = correction;
                    else if (scenario == 1) Check(correction == reference[key], "native ranged B correction equals ground " + key);
                    else original[key] = correction;
                    Check(*(int*)(data + 0x9000) == *(short*)(Unit(1) + 0x712) + 30 && *(int*)(data + 0x9004) == pair.Item2 + 26,
                        "native raycast receives actual source/target elevation " + key);
                }
            }
            Check(original["8/75"] == -1 && reference["8/75"] == 0 && reference["8/78"] == 0 && reference["8/79"] == -1,
                "native ranged lower correction uses inclusive70 on actual elevation");
            Check(original["80/8"] == 0 && reference["80/8"] == 1 && reference["80/10"] == 0,
                "native ranged upper correction uses inclusive70 on actual elevation");
            Console.WriteLine("PASS: complete native ranged target/correction path: B pitch equals ground at +/-70; LOS predicates isolated.");
        }
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
        internal Dictionary<int, long> Counters; // state offset -> value after the stub run
        internal Dictionary<int, byte> StateBytes = new Dictionary<int, byte>();
    }

    private static void Compare(NativeCode.Site site, string name, Action arrange, Func<bool, Expect> expect)
    {
        ulong[] vanilla = Run(site, arrange, stub: false, enabled: false, out byte[] vanillaMemory, out int[] vanillaCall);
        foreach (bool enabled in new[] { false, true })
        {
            ulong[] actual = Run(site, arrange, stub: true, enabled: enabled, out byte[] memory, out int[] call);
            Expect e = expect(enabled);
            string label = $"{site.Name} [{name}] enabled={enabled}";
            foreach (var pair in e.StateBytes)
                Check(data[StateDataOffset + pair.Key] == pair.Value, $"{label}: state byte +{pair.Key}");
            if (e.Counters != null)
                foreach (var pair in e.Counters)
                    Check(*(long*)(data + StateDataOffset + pair.Key) == pair.Value,
                        $"{label}: counter +{pair.Key} = {*(long*)(data + StateDataOffset + pair.Key)} != {pair.Value}");
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
        ulong state = (ulong)(data + StateDataOffset);
        new Span<byte>((void*)state, NativeCode.StateSize).Clear();
        *(int*)state = enabled ? 1 : 0;
        arrange();

        ulong p = (ulong)(code + SiteOffset);
        Instruction[] original = NativeCode.Decode(site.Bytes, p, site.InstructionLengths);
        NativeCode.RequireSameInstructions(NativeCode.Decode(site.Bytes, p, site.InstructionLengths), original, site.Name);
        if (site.Kind == NativeCode.SiteKind.LoadBootstrap)
        {
            // Relocate the original RIP-relative LEA to this fixture's actual height grid in both paths.
            original[1].MemoryDisplacement64 = (ulong)(tiles + NativeCode.HeightOffset);
        }
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
            NativeCode.Emit(a, site, original, state, p + (ulong)site.Length, diagCounters);
            target = (ulong)(code + StubOffset);
            byte[] bytes = NativeCode.Assemble(a, target);
            Marshal.Copy(bytes, 0, (IntPtr)(long)target, bytes.Length);
        }
        else if (site.Kind == NativeCode.SiteKind.LoadBootstrap)
        {
            var a = new Assembler(64);
            foreach (Instruction instruction in original) { Instruction copy = instruction; copy.IP = 0; a.AddInstruction(copy); }
            byte[] bytes = NativeCode.Assemble(a, p);
            Check(bytes.Length == site.Length, "bootstrap fixture: relocated original span");
            Marshal.Copy(bytes, 0, (IntPtr)(long)p, bytes.Length);
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
        // Independent native instructions establish the record offsets used by the bootstrap.
        foreach (var anchor in new[]
        {
            (NativeCode.PitchLimitAnchorRva, 7, NativeCode.PitchLimitOffset, Code.Movsxd_r64_rm32),
            (NativeCode.PitchRecordsAnchorRva, 7, NativeCode.PitchRecordsOffset + NativeCode.PitchRecordStride, Code.Lea_r64_m),
            (NativeCode.PitchOwnerAnchorRva, 5, NativeCode.PitchOwnerOffset, Code.Cmp_rm16_imm8),
            (NativeCode.PitchStateAnchorRva, 8, NativeCode.PitchRecordsOffset + NativeCode.PitchStateOffset, Code.Cmp_rm16_imm8),
        })
        {
            var instruction = NativeCode.Decode(raw.Skip(FileOffset(raw, anchor.Item1)).Take(anchor.Item2).ToArray(),
                (ulong)anchor.Item1, new[] { anchor.Item2 })[0];
            Check(instruction.Code == anchor.Item4 && (long)instruction.MemoryDisplacement64 == anchor.Item3,
                $"file image: pitch-record layout anchor 0x{anchor.Item1:X}");
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
    private static ref byte Marker(int tile) => ref data[StateDataOffset + NativeCode.RestoredPitchOffset + tile];

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
