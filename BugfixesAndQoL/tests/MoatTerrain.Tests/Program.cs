using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BugfixesAndQoL.MoatTerrain;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

internal static unsafe class Program
{
    private static int checks;
    private const int Tile = 117, OtherTile = 118, ManagerSize = 0x2050000;
    private static IntPtr manager, state, obj, output, code;
    private static ulong M => unchecked((ulong)manager.ToInt64());
    private static ulong S => unchecked((ulong)state.ToInt64());
    private static ulong O => unchecked((ulong)output.ToInt64());
    private static ulong C => unchecked((ulong)code.ToInt64());
    private static readonly AssemblerRegisterXMM[] Xmm = {
        xmm0, xmm1, xmm2, xmm3, xmm4, xmm5, xmm6, xmm7, xmm8, xmm9, xmm10, xmm11, xmm12, xmm13, xmm14, xmm15 };
    private static int inputFlags = 0x246;
    private static ushort* History => (ushort*)((byte*)state + TerrainState.DataOffset);
    private static byte* Soil => (byte*)manager + NativeDefinition.Soil;
    private static uint* Logic => (uint*)((byte*)manager + NativeDefinition.Logic);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RunCode();
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }

    public static int Main(string[] args)
    {
        try
        {
            NativeDefinition.ValidateLayouts();
            Check((int)TileType.OasisGrass == 0x10 && (int)eStructs.STRUCT_DRAWBRIDGE == 0x31, "installed enum parity");
            if (args.Length == 2 && args[0] == "--audit-out") { WriteAudit(args[1]); return 0; }
            manager = Marshal.AllocHGlobal(ManagerSize); state = Marshal.AllocHGlobal(TerrainState.BufferBytes);
            obj = Marshal.AllocHGlobal(256); output = Marshal.AllocHGlobal(1024);
            code = VirtualAlloc(IntPtr.Zero, (UIntPtr)16384, 0x3000, 0x40);
            if (code == IntPtr.Zero) throw new Exception("Private fixture VirtualAlloc failed.");
            new Span<byte>((void*)manager, ManagerSize).Clear();
            CodecAndLifecycle();
            PublicationFailures();
            Console.WriteLine("PASS: codec and lifecycle.");
            Operations();
            Console.WriteLine("PASS: native operations, register/flags/stack and bounds.");
            FullEmitterAndBackend();
            Timing();
            Console.WriteLine("PASS: " + checks + " moat terrain codec/lifecycle/native-machine checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if (code != IntPtr.Zero) VirtualFree(code, UIntPtr.Zero, 0x8000);
            foreach (IntPtr p in new[] { manager, state, obj, output }) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
        }
    }

    private static void PublicationFailures()
    {
        var pending = new TerrainPublication();
        int disables = 0, rollbacks = 0;
        pending.Fail(() => disables++, () => rollbacks++);
        Check(disables == 1 && rollbacks == 1 && !pending.IsReady, "unpublished candidate rolls back");
        pending.Fail(() => disables++, () => rollbacks++);
        Check(disables == 1 && rollbacks == 1, "failed candidate cleanup is idempotent");
        foreach (int failurePoint in new[] { 0, 1, 2, 3 })
        {
            var publication = new TerrainPublication();
            bool bufferFreed = false, active = false, wrote = false;
            var expected = new InvalidOperationException("fixture publication failure");
            try
            {
                if (failurePoint == 0)
                {
                    publication.RetainRegisteredResources();
                    throw expected; // Save registration owns a buffer callback before patch activation.
                }
                publication.Enable(() =>
                {
                    Check(!publication.IsReady, "publishing is not ready");
                    if (failurePoint == 1) throw expected;
                    wrote = true; active = true;
                    if (failurePoint == 2) throw expected; // A backend can write before throwing.
                }, () => { throw expected; }); // Post-write verification failed.
                throw new Exception("Fixture did not reject publication.");
            }
            catch (InvalidOperationException error)
            {
                Check(ReferenceEquals(error, expected), "publication failure remains visible");
                publication.Fail(() => active = false, () => bufferFreed = true);
            }
            Check(!publication.IsReady && !active && !bufferFreed, "registered or published failure retains disabled buffer");
            Check(wrote == (failurePoint >= 2), "fixture reached the intended patch-write boundary");
            bool retried = false;
            try { publication.Enable(() => retried = true, () => { }); } catch (InvalidOperationException) { }
            Check(!retried, "failed publication cannot be retried or reactivated");
        }
        var complete = new TerrainPublication();
        bool installed = false, validated = false;
        complete.Enable(() => installed = true, () => { Check(installed && !complete.IsReady, "validate before readiness"); validated = true; });
        Check(validated && complete.IsReady, "whole-feature validation publishes readiness");
        Console.WriteLine("PASS: production publication cleanup, registration retention, first/partial write and post-write failures.");
    }

    private static void WriteAudit(string path)
    {
        File.WriteAllLines(path, NativeDefinition.Sites.Select(s => s.Rva.ToString("X") + "\t" + s.Length + "\t" +
            s.FunctionRva.ToString("X") + "\t" + s.FunctionLength + "\t" + s.FunctionHash + "\t" +
            BitConverter.ToString(s.Bytes).Replace("-", "")));
    }

    private static void CodecAndLifecycle()
    {
        ushort[] tiles = new ushort[TerrainState.Capacity];
        Check(TerrainState.Encode(tiles, 400) == null, "empty state writes no known entry, not a stale record");
        tiles[0] = TerrainState.Dug; // A known original dirt value must survive.
        tiles[Tile] = (ushort)(TerrainState.Dug | (byte)TileType.OasisGrass);
        tiles[OtherTile] = (ushort)(TerrainState.Planned | 0xFF);
        tiles[tiles.Length - 1] = (ushort)(TerrainState.Dug | TerrainState.Bridge | 0x80);
        byte[] bytes = TerrainState.Encode(tiles, 400);
        Check(bytes.Length < 1024 * 1024, "maximum dense entry fits the public save limit");
        Check(tiles.SequenceEqual(TerrainState.Decode(bytes, out int size)) && size == 400, "every tile and full soil byte roundtrip");
        Check(bytes.SequenceEqual(TerrainState.Encode(tiles, 400)), "byte deterministic, independent of insertion order");
        byte[] invalid = (byte[])bytes.Clone(); invalid[17] = 8;
        Reject(() => TerrainState.Decode(invalid, out _), "transient state cannot load");
        Reject(() => TerrainState.Decode(bytes.Take(bytes.Length - 1).ToArray(), out _), "truncated state cannot load");
        invalid = (byte[])bytes.Clone(); invalid[0] ^= 1;
        Reject(() => TerrainState.Decode(invalid, out _), "foreign layout cannot load");
        var history = new TerrainHistory(state);
        TerrainFilter filter = view => { view[OtherTile] = 0; };
        history.Stage(bytes); history.Begin(1, 400, true, filter);
        Check(history.Active && history.Tiles[Tile] == tiles[Tile] && history.Tiles[OtherTile] == 0, "restore only validated live references");
        history.Tiles[Tile] = TerrainState.Dug | 1;
        history.Begin(1, 400, true, filter);
        Check(history.Tiles[Tile] == (TerrainState.Dug | 1), "same-session replay retains new history");
        history.Reset(); history.Stage(bytes); history.Begin(2, 400, false, filter);
        Check(!history.Active && history.Tiles[Tile] == 0 && history.Capture(false, filter) == null, "disabled load discards recorded soil");
        history.SetEnabled(true);
        Check(history.Active && history.Capture(true, filter) == null, "re-enabling cannot resurrect the discarded archive data");
        history.Reset(); history.Begin(3, 400, true, filter);
        Check(history.Capture(true, filter) == null, "new mission/NoRecord starts empty");
        history.Reset(); history.Stage(bytes); history.RejectLoad(); history.Begin(4, 400, true, filter);
        Check(!history.Active, "unreadable newer record blocks only this mission's tracking");
        history.Reset(); history.Stage(bytes);
        Reject(() => history.Begin(5, 700, true, filter), "different map layout cannot reuse pending history");
        Check(!history.Active, "mismatched restore stays disabled");
        history.Reset(); history.Begin(6, 400, true, filter); history.Tiles[Tile] = tiles[Tile];
        byte[] captured = history.Capture(true, filter);
        Check(TerrainState.Decode(captured, out _)[Tile] == tiles[Tile], "production capture reads the native buffer");
        history.Reset();
        Check(!history.Active && history.Tiles[Tile] == 0 && history.Captured == 0, "retirement clears buffer and execution markers");
    }

    private static void Reject(Action action, string label)
    {
        bool rejected = false;
        try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, label);
    }

    private static void Reset(bool active, byte soil, uint flags, ushort origin)
    {
        new Span<byte>((void*)state, TerrainState.BufferBytes).Clear();
        new Span<byte>((void*)output, 1024).Clear(); new Span<byte>((void*)obj, 256).Clear();
        *(int*)state = active ? 1 : 0;
        Soil[Tile] = soil; Logic[Tile] = flags; History[Tile] = origin;
        Soil[OtherTile] = 0xA5; History[OtherTile] = 0x235;
        *(int*)((byte*)obj + 0x8C) = Tile; *(int*)((byte*)obj + 0x24) = Tile;
        *(int*)((byte*)manager + 0x1F3EE40) = Tile;
        *(ushort*)((byte*)manager + 0x1EA23F0 + Tile * 2) = 0;
        *(int*)((byte*)manager + 0x2038E34) = 0;
        *(ushort*)((byte*)manager + NativeDefinition.Structure + Tile * 2) = 0;
    }

    private static void Setup(Assembler a, TerrainSiteKind kind, int mode, int tile, bool bridge)
    {
        a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi); a.push(r12); a.push(r13); a.push(r14); a.push(r15);
        a.sub(rsp, 0x100);
        for (int i = 6; i < 16; i++) a.movdqu(__xmmword_ptr[rsp + 0x60 + (i - 6) * 16], Xmm[i]);
        for (int i = 0; i < 16; i++) { a.mov(rax, 0xABCDEF0102030405UL + (ulong)i); a.movq(Xmm[i], rax); }
        a.mov(rax, 0x12345678ABCDEF01UL); a.mov(rcx, M); a.mov(rdx, 1UL);
        a.mov(rbx, M); a.mov(rbp, (ulong)tile); a.mov(rsi, M + 16); a.mov(rdi, (ulong)tile);
        a.mov(r8, O); a.mov(r9d, tile); a.mov(r10, 0xFEED987654321012UL); a.mov(r11, M - NativeDefinition.TileManager);
        a.mov(r12d, bridge ? (int)eStructs.STRUCT_DRAWBRIDGE : 1);
        a.mov(r13d, unchecked((int)TilePropertyFlag.IsMoat)); a.mov(r14, M + NativeDefinition.Logic + (ulong)tile * 4); a.mov(r15d, mode);
        a.mov(__dword_ptr[rsp + 0x50], mode);
        switch (kind)
        {
            case TerrainSiteKind.Filled: a.mov(rbx, M + 0x1F3EE40); a.mov(rdi, M + 16); break;
            case TerrainSiteKind.DirectPrepare:
            case TerrainSiteKind.BrushFinished:
            case TerrainSiteKind.Paint: a.mov(rbx, (ulong)(uint)tile); a.mov(rdi, M); break;
            case TerrainSiteKind.BridgeRemoved:
            case TerrainSiteKind.BridgePurged:
            case TerrainSiteKind.BuildingPrepare: a.mov(rsi, (ulong)(uint)tile); break;
            case TerrainSiteKind.AreaRemoved:
                a.mov(r14d, tile); a.mov(rdi, unchecked((ulong)obj.ToInt64()) + 0x50);
                a.mov(r15, unchecked((ulong)obj.ToInt64()) + 0x60); break;
            case TerrainSiteKind.Erased: a.mov(rdi, M); a.mov(r13, M - NativeDefinition.TileManager); break;
            case TerrainSiteKind.Vegetation: a.mov(rbx, unchecked((ulong)obj.ToInt64())); a.mov(rdi, M); break;
            case TerrainSiteKind.VegetationUpdate: a.mov(rbx, unchecked((ulong)obj.ToInt64())); a.mov(rsi, M); break;
        }
        a.push(inputFlags); a.popfq(); // Exercise live arithmetic flags independently of the payload.
        Snapshot(a, 0);
        SnapshotSimd(a, 256);
    }

    private static void Snapshot(Assembler a, int offset)
    {
        // R8 is an untouched output pointer in operation-only fixtures. Full displaced Vanilla
        // may change it, so those fixtures reload it without changing flags before the Post snapshot.
        a.mov(__qword_ptr[r8 + offset], rax); a.mov(__qword_ptr[r8 + offset + 8], rbx);
        a.mov(__qword_ptr[r8 + offset + 16], rcx); a.mov(__qword_ptr[r8 + offset + 24], rdx);
        a.mov(__qword_ptr[r8 + offset + 32], rsi); a.mov(__qword_ptr[r8 + offset + 40], rdi);
        a.mov(__qword_ptr[r8 + offset + 48], rbp); a.mov(__qword_ptr[r8 + offset + 56], rsp);
        a.mov(__qword_ptr[r8 + offset + 64], r9); a.mov(__qword_ptr[r8 + offset + 72], r10);
        a.mov(__qword_ptr[r8 + offset + 80], r11); a.mov(__qword_ptr[r8 + offset + 88], r12);
        a.mov(__qword_ptr[r8 + offset + 96], r13); a.mov(__qword_ptr[r8 + offset + 104], r14);
        a.mov(__qword_ptr[r8 + offset + 112], r15); a.pushfq(); a.pop(__qword_ptr[r8 + offset + 120]);
    }

    private static void Return(Assembler a)
    {
        for (int i = 6; i < 16; i++) a.movdqu(Xmm[i], __xmmword_ptr[rsp + 0x60 + (i - 6) * 16]);
        a.add(rsp, 0x100);
        a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12); a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx); a.ret();
    }
    private static void SnapshotSimd(Assembler a, int offset)
    { for (int i = 0; i < 16; i++) a.movdqu(__xmmword_ptr[r8 + offset + i * 16], Xmm[i]); }
    private static void Write(Assembler a, ulong address)
    {
        using (var bytes = new MemoryStream())
        {
            a.Assemble(new StreamCodeWriter(bytes), address);
            byte[] payload = bytes.ToArray(); Marshal.Copy(payload, 0, new IntPtr(unchecked((long)address)), payload.Length);
        }
    }
    private static void Execute(TerrainSiteKind kind, int mode = 0, int tile = Tile, bool bridge = false)
    {
        *(int*)((byte*)manager + 0x1F3EE40) = tile;
        *(int*)((byte*)obj + 0x8C) = tile; *(int*)((byte*)obj + 0x24) = tile;
        var a = new Assembler(64); Setup(a, kind, mode, tile, bridge);
        NativeCode.EmitOperation(a, kind, S, M);
        Snapshot(a, 128); SnapshotSimd(a, 512); Return(a); Write(a, C);
        Marshal.GetDelegateForFunctionPointer<RunCode>(code)();
        for (int n = 0; n < 16; n++) Check(Marshal.ReadInt64(output, n * 8) == Marshal.ReadInt64(output, 128 + n * 8), kind + " preserves register/flags/stack " + n);
        for (int n = 0; n < 32; n++) Check(Marshal.ReadInt64(output, 256 + n * 8) == Marshal.ReadInt64(output, 512 + n * 8), kind + " preserves XMM " + n / 2);
        Check(Soil[OtherTile] == 0xA5 && History[OtherTile] == 0x235, kind + " never writes a neighboring tile");
    }

    private static void Operations()
    {
        foreach (TerrainSiteKind kind in Enum.GetValues(typeof(TerrainSiteKind)))
        {
            Reset(false, 0x10, 0, 0x210); Execute(kind, 2);
            Check(Soil[Tile] == 0x10 && History[Tile] == 0x210 && *((int*)state + 1) == 0, kind + " dormant state is unchanged");
            Reset(true, 0x10, 0, 0); Execute(kind, 2, TerrainState.Capacity);
            Check(*((int*)state + 3) == 1 && History[Tile] == 0, kind + " invalid upper tile is rejected");
            Reset(true, 0x10, 0, 0); Execute(kind, 2, -1);
            Check(*((int*)state + 3) == 1 && History[Tile] == 0, kind + " negative tile is rejected unsigned");
            foreach (int flags in new[] { 0x202, 0xAD7 })
            {
                inputFlags = flags;
                Reset(true, 0x10, 0, 0); Execute(kind, 2);
            }
            inputFlags = 0x246;
        }
        foreach (int tile in new[] { 0, TerrainState.Capacity - 1 })
        {
            Reset(true, 0x10, 0, 0);
            Soil[tile] = 0x80; Logic[tile] = 0; History[tile] = 0;
            Execute(TerrainSiteKind.Excavated, 0, tile);
            Check(History[tile] == (TerrainState.Dug | 0x80), "first and last physical native tile can be captured");
        }
        foreach (byte soil in new byte[] { 0, 1, 2, 0x10, 0x40, 0x80, 0xFF })
        {
            Reset(true, soil, 0, 0); Execute(TerrainSiteKind.Excavated);
            Check(History[Tile] == (TerrainState.Dug | soil), "capture exact soil " + soil);
            Soil[Tile] = 0; Execute(TerrainSiteKind.Filled);
            Check(Soil[Tile] == soil && History[Tile] == 0, "fill restores then consumes soil " + soil);
        }
        Reset(true, 0, (uint)TilePropertyFlag.IsMoat, 0); Execute(TerrainSiteKind.Excavated);
        Check(History[Tile] == 0, "existing untracked moat never gains fabricated history");
        Reset(true, 0, (uint)TilePropertyFlag.IsMoat, 0);
        Execute(TerrainSiteKind.BuildingPrepare, 0, Tile, true);
        Check(History[Tile] == TerrainState.Bridge, "new bridge on an old unknown moat records no invented origin");
        Logic[Tile] = 0; Execute(TerrainSiteKind.TaskCreated, 1);
        Check(History[Tile] == TerrainState.Bridge, "task recreation cannot fabricate the missing bridge origin");
        *(ushort*)((byte*)manager + NativeDefinition.Structure + Tile * 2) = 1;
        Soil[Tile] = 2; Execute(TerrainSiteKind.Filled);
        Check(Soil[Tile] == 2 && History[Tile] == TerrainState.Bridge,
            "filling an unknown old moat under a surviving bridge cannot restore fabricated dirt");
        Soil[Tile] = 2; Execute(TerrainSiteKind.BridgeRemoved);
        Check(Soil[Tile] == 2 && History[Tile] == 0, "untracked old moat keeps Vanilla soil after bridge demolition");
        Reset(true, 0, 0, 0); *(ushort*)((byte*)manager + NativeDefinition.Structure + Tile * 2) = 1;
        Execute(TerrainSiteKind.TaskCreated, 1);
        Check(History[Tile] == 0, "legacy lowered bridge does not gain an origin when a task is allocated");
        Reset(true, 0x10, 0, 0); Execute(TerrainSiteKind.TaskCreated, 0);
        Check(History[Tile] == (TerrainState.Planned | 0x10), "new plan records a distinct stage");
        Soil[Tile] = 0x80; Execute(TerrainSiteKind.Excavated);
        Check(History[Tile] == (TerrainState.Dug | 0x80), "actual excavation captures the current, not stale planned soil");
        Reset(true, 0x40, 0, TerrainState.Planned | 0x10); Execute(TerrainSiteKind.BrushFinished, 1);
        Check(History[Tile] == 0 && Soil[Tile] == 0x40, "plan cancellation does not repaint soil");
        Reset(true, 0x10, 0, 0); Execute(TerrainSiteKind.DirectPrepare, 2);
        Check(History[Tile] == (TerrainState.Preparing | 0x10), "instant placement stages before Vanilla's zero write");
        Soil[Tile] = 0; Execute(TerrainSiteKind.TaskCreated, 1);
        Check(History[Tile] == (TerrainState.Dug | 0x10), "successful allocation retains pre-zero origin");
        Logic[Tile] = (uint)TilePropertyFlag.IsMoat; Execute(TerrainSiteKind.BrushFinished, 2);
        Check(History[Tile] == (TerrainState.Dug | 0x10), "instant success retains history");
        Reset(true, 0, 0, TerrainState.Preparing | 0x10); Execute(TerrainSiteKind.BrushFinished, 2);
        Check(Soil[Tile] == 0x10 && History[Tile] == 0, "allocation failure rolls back only erased soil");
        Reset(true, 0, 0, TerrainState.Planned | 0x10); Execute(TerrainSiteKind.BrushFinished, 3);
        Check(Soil[Tile] == 0x10 && History[Tile] == 0, "direct removal restores a plan if Vanilla erased its soil");
        Reset(true, 0, (uint)TilePropertyFlag.IsMoat, TerrainState.Dug | 0x10);
        Execute(TerrainSiteKind.BuildingPrepare, 0, Tile, true);
        Check(History[Tile] == (TerrainState.Dug | TerrainState.Bridge | 0x10), "bridge conversion preserves existing moat origin");
        Logic[Tile] = 0; Execute(TerrainSiteKind.TaskCreated, 1);
        Check(History[Tile] == (TerrainState.Dug | TerrainState.Bridge | 0x10), "bridge task recreation retains the original");
        Soil[Tile] = 2; Execute(TerrainSiteKind.BridgeRemoved);
        Check(Soil[Tile] == 0x10 && History[Tile] == 0, "bridge footprint final soil writer is corrected");
        Reset(true, 0x80, 0, 0); Execute(TerrainSiteKind.BuildingPrepare, 0, Tile, true);
        Check(History[Tile] == (TerrainState.Dug | TerrainState.Bridge | 0x80), "new bridge records non-moat footprint cells too");
        Reset(true, 0, (uint)TilePropertyFlag.IsMoat, TerrainState.Dug | TerrainState.Bridge | 0x10);
        Execute(TerrainSiteKind.BridgeRemoved);
        Check(History[Tile] != 0 && Soil[Tile] == 0, "surviving moat under a removed raised bridge remains tracked");
        Logic[Tile] = 0;
        *(ushort*)((byte*)manager + NativeDefinition.Structure + Tile * 2) = 1;
        Execute(TerrainSiteKind.Filled);
        Check(Soil[Tile] == 0x10 && History[Tile] == (TerrainState.Dug | TerrainState.Bridge | 0x10),
            "filled moat under a surviving bridge retains origin until bridge demolition");
        Soil[Tile] |= 2;
        Execute(TerrainSiteKind.BridgeRemoved);
        Check(Soil[Tile] == 0x10 && History[Tile] == 0, "subsequent bridge demolition restores the same original exactly");
        foreach (TerrainSiteKind kind in new[] { TerrainSiteKind.Paint, TerrainSiteKind.BuildingPrepare, TerrainSiteKind.Replaced,
            TerrainSiteKind.Erased, TerrainSiteKind.Vegetation, TerrainSiteKind.VegetationUpdate })
        {
            Reset(true, 0x40, 0, TerrainState.Dug | 0x10); Execute(kind);
            Check(History[Tile] == 0 && Soil[Tile] == 0x40, kind + " invalidates without overwriting the replacement");
        }
        foreach (TerrainSiteKind kind in new[] { TerrainSiteKind.Filled, TerrainSiteKind.BridgeRemoved, TerrainSiteKind.BridgePurged, TerrainSiteKind.AreaRemoved })
        {
            Reset(true, 0, 0, TerrainState.Dug | 0x10); Execute(kind);
            Check(History[Tile] == 0 && Soil[Tile] == 0x10, kind + " terminal restoration");
            Reset(true, 0, 0, 0); Execute(kind);
            Check(History[Tile] == 0 && Soil[Tile] == 0, kind + " unknown origin remains untouched");
        }
    }

    private static Instruction[] Original(NativeDefinition.Site site)
    {
        var reader = new ByteArrayCodeReader(site.Bytes);
        var decoder = Decoder.Create(64, reader); decoder.IP = (ulong)site.Rva;
        var instructions = new System.Collections.Generic.List<Instruction>();
        while (reader.CanReadByte)
        {
            decoder.Decode(out Instruction instruction);
            // Only external calls/branch destinations are fixture boundaries. The production
            // instructions and their order are retained, including native memory writes and CMP/Jcc.
            if (instruction.FlowControl == FlowControl.Call) instruction.NearBranch64 = C + 0x3000;
            if (instruction.FlowControl == FlowControl.ConditionalBranch || instruction.FlowControl == FlowControl.UnconditionalBranch)
                instruction.NearBranch64 = C + 0x1000;
            instructions.Add(instruction);
        }
        return instructions.ToArray();
    }

    private static ulong[] RunFull(NativeDefinition.Site site, bool useHook, int mode, bool active = false)
    {
        Reset(active, 0x10, (uint)TilePropertyFlag.IsMoat, TerrainState.Dug | 0x10);
        var a = new Assembler(64); Setup(a, site.Kind, mode, Tile, false);
        if (useHook) NativeCode.Emit(a, site.Kind, S, M, Original(site));
        else a.AddInstructions(Original(site));
        a.jmp(C + 0x2000); Write(a, C);
        var branch = new Assembler(64); branch.mov(r8, O); branch.mov(__dword_ptr[r8 + 800], 1); branch.jmp(C + 0x2000); Write(branch, C + 0x1000);
        var post = new Assembler(64); post.mov(r8, O); Snapshot(post, 128); SnapshotSimd(post, 512); Return(post); Write(post, C + 0x2000);
        // Native refresh boundary: counted once, no game code is executed by this isolated fixture.
        var call = new Assembler(64); call.push(rax); call.mov(rax, O); call.inc(__dword_ptr[rax + 804]); call.pop(rax); call.ret(); Write(call, C + 0x3000);
        Marshal.GetDelegateForFunctionPointer<RunCode>(code)();
        ulong[] result = new ulong[52];
        for (int i = 0; i < 16; i++) result[i] = unchecked((ulong)Marshal.ReadInt64(output, 128 + i * 8));
        result[16] = Soil[Tile]; result[17] = Logic[Tile]; result[18] = (uint)Marshal.ReadInt32(output, 800);
        result[19] = (uint)Marshal.ReadInt32(output, 804);
        for (int n = 0; n < 32; n++) result[20 + n] = unchecked((ulong)Marshal.ReadInt64(output, 512 + n * 8));
        return result;
    }

    private static void FullEmitterAndBackend()
    {
        foreach (NativeDefinition.Site site in NativeDefinition.Sites)
        {
            Console.WriteLine("Native backend/emitter fixture: " + site.Kind);
            Marshal.Copy(site.Bytes, 0, code, site.Bytes.Length);
            using (var probe = new X64InlineHook(C, site.Length, name: "Unpublished_MoatTerrainFixture"))
            {
                Check(probe.DisplacedByteCount == site.Length, site.Kind + " actual RedBird displacement");
                probe.Generate((a, original, continuation) => NativeCode.Emit(a, site.Kind, S, M, original));
                Check(probe.StubAddress != IntPtr.Zero, site.Kind + " actual RedBird production generator");
            }
            foreach (int mode in new[] { 2, 3 })
            {
                ulong[] baseline = RunFull(site, false, mode), disabled = RunFull(site, true, mode);
                Check(baseline.SequenceEqual(disabled), site.Kind + " disabled full emitter equals Vanilla, mode=" + mode);
                bool hasCall = Original(site).Any(i => i.FlowControl == FlowControl.Call);
                Check(disabled[19] == (hasCall ? 1UL : 0UL), site.Kind + " original call executes exactly once");
                ulong[] active = RunFull(site, true, mode, true);
                Check(baseline.Take(16).SequenceEqual(active.Take(16)) && baseline.Skip(18).SequenceEqual(active.Skip(18)),
                    site.Kind + " active full emitter preserves Vanilla registers, flags, XMM, branches and one original call");
            }
        }
    }

    private static void Timing()
    {
        Reset(true, 0x10, 0, 0);
        var a = new Assembler(64); Setup(a, TerrainSiteKind.Excavated, 0, Tile, false);
        a.mov(r9d, 1000000); Label loop = a.CreateLabel(); a.Label(ref loop);
        NativeCode.EmitOperation(a, TerrainSiteKind.Excavated, S, M); a.dec(r9d); a.jnz(loop); Return(a); Write(a, C);
        var run = Marshal.GetDelegateForFunctionPointer<RunCode>(code); run();
        foreach (int active in new[] { 0, 1, 0, 1 })
        {
            *(int*)state = active;
            var watch = Stopwatch.StartNew(); run(); watch.Stop();
            Console.WriteLine("Native fixture: 1,000,000 calls, active=" + active + ", ms=" +
                watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " (not an in-game FPS measurement).");
        }
        Check(History[Tile] == (TerrainState.Dug | 0x10), "steady-state native capture remains stable");
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
}
