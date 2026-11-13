using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Iced.Intel;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

static class Fixture
{
    private const int Shape = 0x10063, Slot = 7, Chore = 0xB0BF8 + Slot * 0x500, Task = 0x1F3EE30 + 9 * 16;
    private const int X = 160, Y = 271, Tile = 91234, Actor = 2, Sender = 3, Command = 300000081;
    private const long Rax = 0x1234567812345678, Rcx = 0x2234567822345678, Rdx = 0x3234567832345678;
    private const long R10 = 0x4234567842345678, R11 = 0x5234567852345678, Flags = 0xAD7;
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void RunStub();

    private static long Alloc(int size, bool executable = false)
    {
        long address = VirtualAlloc(IntPtr.Zero, (UIntPtr)(uint)size, 0x3000, executable ? 0x40u : 4u).ToInt64();
        if (address == 0) throw new InvalidOperationException("VirtualAlloc failed.");
        return address;
    }

    private static void Write32(long address, int offset, int value) => Marshal.WriteInt32((IntPtr)address, offset, value);
    private static void Write16(long address, int offset, int value) => Marshal.WriteInt16((IntPtr)address, offset, (short)value);
    private static void Write8(long address, int offset, int value) => Marshal.WriteByte((IntPtr)address, offset, (byte)value);
    private static long Read64(long address, int offset) => Marshal.ReadInt64((IntPtr)address, offset);
    private static int Read32(long address, int offset) => Marshal.ReadInt32((IntPtr)address, offset);

    private static void Write(Assembler asm, long address)
    {
        using var stream = new MemoryStream();
        if (!asm.TryAssemble(new StreamCodeWriter(stream), (ulong)address, out string error, out _))
            throw new InvalidOperationException(error);
        var bytes = stream.ToArray();
        Marshal.Copy(bytes, 0, (IntPtr)address, bytes.Length);
    }

    private static void Capture(Assembler asm, int branch)
    {
        asm.mov(__dword_ptr[r9 + 4], branch);
        asm.mov(__qword_ptr[r9 + 16], rsp);
        asm.mov(__qword_ptr[r9 + 24], rax);
        asm.mov(__qword_ptr[r9 + 32], rcx);
        asm.mov(__qword_ptr[r9 + 40], rdx);
        asm.mov(__qword_ptr[r9 + 48], r10);
        asm.mov(__qword_ptr[r9 + 56], r11);
        asm.mov(__dword_ptr[r9 + 64], r12d);
        asm.mov(__dword_ptr[r9 + 68], r13d);
        asm.mov(__qword_ptr[r9 + 72], rsi);
        asm.mov(__dword_ptr[r9 + 80], r14d);
        asm.mov(__dword_ptr[r9 + 84], r15d);
        asm.mov(__dword_ptr[r9 + 88], edi);
        asm.mov(__qword_ptr[r9 + 96], rbx);
        asm.movq(__qword_ptr[r9 + 112], xmm0);
        asm.movq(__qword_ptr[r9 + 120], xmm1);
        asm.mov(__dword_ptr[r9 + 128], ebp);
        asm.pushfq();
        asm.pop(rax);
        asm.mov(__qword_ptr[r9 + 104], rax);
        asm.add(rsp, 0x100);
        asm.pop(r15); asm.pop(r14); asm.pop(r13); asm.pop(r12); asm.pop(rdi); asm.pop(rsi); asm.pop(rbp); asm.pop(rbx);
        asm.ret();
    }


    private static void VerifyMoatCalls(Type type, object runtime, long module, long tiles, object cell)
    {
        long code = Alloc(0x1000, true), capture = Alloc(0x1000);
        var nativeCall = new Assembler(64);
        nativeCall.mov(rax, (ulong)capture);
        nativeCall.mov(__qword_ptr[rax], rcx);
        nativeCall.mov(__dword_ptr[rax + 8], edx);
        nativeCall.mov(__dword_ptr[rax + 12], r8d);
        nativeCall.mov(__dword_ptr[rax + 16], r9d);
        for (int i = 0; i < 3; i++)
        {
            nativeCall.mov(r10d, __dword_ptr[rsp + 40 + i * 8]);
            nativeCall.mov(__dword_ptr[rax + 20 + i * 4], r10d);
        }
        nativeCall.movzx(r10d, __byte_ptr[rsp + 64]);
        nativeCall.mov(__dword_ptr[rax + 32], r10d);
        nativeCall.inc(__dword_ptr[rax + 96]);
        nativeCall.ret();
        Write(nativeCall, code);
        var refresh = new Assembler(64);
        refresh.mov(rax, (ulong)capture);
        refresh.mov(__qword_ptr[rax + 40], rcx);
        refresh.mov(__dword_ptr[rax + 48], edx);
        refresh.mov(__dword_ptr[rax + 52], r8d);
        refresh.mov(__dword_ptr[rax + 56], r9d);
        refresh.mov(r10d, __dword_ptr[rax + 96]);
        refresh.mov(__dword_ptr[rax + 60], r10d);
        refresh.inc(__dword_ptr[rax + 100]);
        refresh.ret();
        Write(refresh, code + 0x300);
        var placeField = type.GetField("placeMoat", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pathField = type.GetField("refreshMoatPath", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var place = Marshal.GetDelegateForFunctionPointer((IntPtr)code, placeField.FieldType);
        var path = Marshal.GetDelegateForFunctionPointer((IntPtr)(code + 0x300), pathField.FieldType);
        placeField.SetValue(runtime, place);
        pathField.SetValue(runtime, path);
        type.GetField("tileManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runtime, (IntPtr)tiles);
        var apply = type.GetMethod("ApplyMoat", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (bool completed in new[] { false, true })
        {
            Write32(capture, 96, 0); Write32(capture, 100, 0);
            apply.Invoke(runtime, [cell, Tile, Sender, completed]);
            if (Read64(capture, 0) != tiles || Read32(capture, 8) != Sender ||
                Read32(capture, 12) != Tile || Read32(capture, 16) != Y ||
                Read32(capture, 20) != 1 || Read32(capture, 24) != 0x40000000 ||
                Read32(capture, 28) != (completed ? 2 : 0) || Read32(capture, 32) != 0 ||
                Read64(capture, 40) != module + 0x60AD660 || Read32(capture, 48) != 9 ||
                Read32(capture, 52) != X || Read32(capture, 56) != Y ||
                Read32(capture, 60) != 1 || Read32(capture, 96) != 1 || Read32(capture, 100) != 1)
                throw new Exception("Production moat invocation ABI, coordinate conversion, variant or refresh order.");
        }
        GC.KeepAlive(place); GC.KeepAlive(path);
        Console.WriteLine("PASS actual production planned/completed moat native invocation, stack arguments and radius9 refresh.");
        var admission = type.GetMethod("IsBatchMoatTileAllowed", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Type flagType = admission.GetParameters()[1].ParameterType;
        bool Admit(int flags) => (bool)admission.Invoke(runtime, [Tile, Enum.ToObject(flagType, flags), false])!;
        Write8(tiles, 0xD7E5A0 + Tile, 20);
        if (!Admit(0)) throw new Exception("Planned batch masks an optional live elevated-moat gate.");
        Write16(tiles, 0xA6F260 + Tile * 2, 1);
        if (Admit(0)) throw new Exception("Planned batch accepted vegetation before terminal validation.");
        Write16(tiles, 0xA6F260 + Tile * 2, 0);
        Write16(tiles, 0xB0BCA0 + Tile * 2, 1);
        if (Admit(0)) throw new Exception("Planned batch accepted a structure.");
        Write16(tiles, 0xB0BCA0 + Tile * 2, 0);
        if (Admit(0x10000400)) throw new Exception("Planned batch accepted blocked geometry.");
        Console.WriteLine("PASS planned moat height delegates to live terminal rules while retaining geometry guards.");
        var actorAllowed = type.GetMethod("IsBatchActorAllowed", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var captureActor = type.GetMethod("TryGetBatchActor", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool ActorAllowed(int sender, int actor, bool editor) =>
            (bool)actorAllowed.Invoke(runtime, [sender, actor, editor])!;
        Write32(module, 0x3665ED8, 12); Write32(module, 0x88E3D78, Actor);
        object[] captureArgs = [Sender, 0];
        if (!(bool)captureActor.Invoke(runtime, captureArgs)! || (int)captureArgs[1] != Actor ||
            !ActorAllowed(Sender, Actor, true) || ActorAllowed(Sender, Actor, false))
            throw new Exception("Editor actor capture/authorization differs from Vanilla12.");
        Write32(module, 0x88E3D78, 8);
        if (!ActorAllowed(Sender, (int)captureArgs[1], true))
            throw new Exception("Later editor selection revoked the captured actor.");
        Write32(module, 0x3665ED8, 0);
        if (ActorAllowed(Sender, Actor, true) || !ActorAllowed(Sender, Sender, false) ||
            ActorAllowed(0, Actor, true) || ActorAllowed(Sender, 9, true))
            throw new Exception("Native actor range or non-editor owner restriction.");
        var operationAllowed = type.GetMethod("IsBatchOperationAllowed", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var operationType = operationAllowed.GetParameters()[0].ParameterType;
        bool OperationAllowed(string name) =>
            (bool)operationAllowed.Invoke(runtime, [Enum.Parse(operationType, name)])!;
        foreach (int mode in new[] { 0, 1, 6 })
        {
            Write32(module, 0x3665F28, mode);
            bool completed = mode == 1 || mode == 6;
            if (!OperationAllowed("Pitch") || OperationAllowed("CompletedMoat") != completed ||
                OperationAllowed("PlannedMoat") == completed)
                throw new Exception("Native operation mode permits the wrong moat variant.");
        }
        Console.WriteLine("PASS native captured editor actor and planning/completed mode policies.");
    }
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--network") { NetworkContractTests.Run(); return; }
        if (args.Length != 2) throw new ArgumentException("Expected mod assembly and installed Extender directory.");
        NetworkContractTests.Run();
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string file = Path.Combine(args[1], name.Name + ".dll");
            return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
        };
        var type = Assembly.LoadFrom(args[0]).GetType("BugfixesAndQoL.PitchMoatLines.NativePlacement", true)!;
        var emit = type.GetMethod("EmitPitchMoatStub", BindingFlags.NonPublic | BindingFlags.Static)!;
        var validate = type.GetMethod("VerifyEmittedPitchMoatStub", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var hookField = type.GetField("pitchMoatHook", BindingFlags.NonPublic | BindingFlags.Instance)!;
        long code = Alloc(0x1000, true), tile = Alloc(0x2050000), chore = Alloc(0x374000), state = Alloc(0x1000);
        long aux = state + 0x100, result = state + 0x200;
        long seam = code + 0x300, occupied = seam + 16, vanilla = seam + 0x157, preserve = seam + 0x18A;

        var enter = new Assembler(64);
        enter.push(rbx); enter.push(rbp); enter.push(rsi); enter.push(rdi); enter.push(r12); enter.push(r13); enter.push(r14); enter.push(r15);
        enter.sub(rsp, 0x100);
        enter.mov(r9, (ulong)result);
        enter.mov(__qword_ptr[r9 + 8], rsp);
        enter.mov(rbx, (ulong)tile);
        enter.mov(edi, Actor);
        enter.mov(r14d, Y); enter.mov(r15d, X);
        enter.mov(r13d, 1);
        enter.mov(r12d, 0x44);
        enter.mov(rsi, Tile);
        enter.mov(rax, Rax); enter.mov(rcx, Rcx); enter.mov(rdx, Rdx); enter.mov(r10, R10); enter.mov(r11, R11);
        enter.movq(xmm0, r10); enter.movq(xmm1, r11);
        enter.push((int)Flags); enter.popfq();
        enter.jmp((ulong)seam);
        Write(enter, code);
        byte[] original = Convert.FromHexString("0FBFAC73006CBF0085ED0F8447010000");
        Marshal.Copy(original, 0, (IntPtr)seam, original.Length);
        var emptyJump = new Assembler(64); emptyJump.jmp((ulong)(code + 0x600)); Write(emptyJump, vanilla);
        var occupiedJump = new Assembler(64); occupiedJump.jmp((ulong)(code + 0x800)); Write(occupiedJump, occupied);
        var preserveJump = new Assembler(64); preserveJump.jmp((ulong)(code + 0xA00)); Write(preserveJump, preserve);
        var v = new Assembler(64); Capture(v, 1); Write(v, code + 0x600);
        var p = new Assembler(64); Capture(p, 2); Write(p, code + 0xA00);
        var f = new Assembler(64); Capture(f, 3); Write(f, code + 0x800);

        var hook = new X64InlineHook((ulong)seam, 16, NullLogger.Instance, "pitch and moat fixture");
        if (hook.DisplacedByteCount != 16) throw new Exception("fixture displacement");
        object[] addresses = [(ulong)state, (ulong)chore, (ulong)tile, (ulong)occupied, (ulong)vanilla, (ulong)preserve];
        hook.Generate((assembler, _, _) => emit.Invoke(null, [assembler, .. addresses]));
        object runtime = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        hookField.SetValue(runtime, hook);
        validate.Invoke(runtime, addresses);
        hook.Enable();
        var run = Marshal.GetDelegateForFunctionPointer<RunStub>((IntPtr)code);

        void Reset()
        {
            Write32(state, 0, 1); Write32(state, 4, Slot); Write32(state, 8, Sender); Write32(state, 12, Command);
            Write32(state, 16, Tile); Write32(state, 20, X | Y << 16); Write32(state, 24, Actor); Write32(state, 28, 0); Write32(state, 32, 106);
            Write32(chore, 0x84CC8, Slot); Write32(chore, 0x84CCC, 0); Write32(chore, 0x370BF8, 10);
            Write32(chore, 0x36FA4C, Sender);
            Write32(chore, Chore, 30); Write32(chore, Chore + 4, Sender); Write8(chore, Chore + 8, 106);
            Write32(chore, Chore + 12, Command); Write32(chore, Chore + 17, X | Y << 16);
            Write32(chore, Chore + 21, Shape); Write16(chore, Chore + 25, 17); Write16(aux, 0, 17);
            Write16(tile, 0xBF6C00 + Tile * 2, 0);
            Write32(tile, 0x898400 + Tile * 4, 0x4000);
            Write16(tile, 0x1EA23F0 + Tile * 2, 9); Write32(tile, 0x2038E30, 10);
            Write32(tile, Task, Tile); Write16(tile, Task + 4, X); Write16(tile, Task + 6, Y);
            Write8(tile, Task + 12, 8); Write8(tile, Task + 13, 3); Write8(tile, Task + 14, 1);
        }
        int passed = 0;
        void Verify(string name, int branch, int outcome, Action mutation)
        {
            Reset(); mutation(); run();
            int occupantValue = Marshal.ReadInt16((IntPtr)tile, 0xBF6C00 + Tile * 2);
            int status = occupantValue == 0 ? 0x44 : occupantValue < 0 ? 0x84 : 0;
            if (Read32(result, 4) != branch || Read32(state, 28) != outcome ||
                Read32(state, 0) != (outcome == 0 ? 1 : 0) && !name.StartsWith("dormant", StringComparison.Ordinal))
                throw new Exception($"{name}: branch={Read32(result, 4)}, outcome={Read32(state, 28)}, active={Read32(state, 0)}");
            if (Read64(result, 8) != Read64(result, 16) || Read64(result, 24) != Rax ||
                Read64(result, 32) != Rcx || Read64(result, 40) != Rdx || Read64(result, 48) != R10 ||
                Read64(result, 56) != R11 || Read32(result, 64) != 0x44 || Read32(result, 68) != 1 ||
                Read64(result, 72) != Tile || Read32(result, 80) != Y || Read32(result, 84) != X ||
                Read32(result, 88) != Actor || Read64(result, 96) != tile || (Read64(result, 104) & 0x8C5) != status || Read32(result, 128) != occupantValue ||
                Read64(result, 112) != R10 || Read64(result, 120) != R11)
                throw new Exception($"{name}: ABI mismatch.");
            Console.WriteLine($"PASS {name}");
            passed++;
        }
        Verify("exact delayed, active near-complete foreign moat", 2, 2, () => {});
        Verify("exact immediate slot", 2, 2, () => Write32(chore, Chore, 0));
        Verify("dormant with no occupant", 1, 0, () => Write32(state, 0, 0));
        Verify("dormant with positive occupant", 3, 0, () => { Write32(state, 0, 0); Write16(tile, 0xBF6C00 + Tile * 2, 1); });
        Verify("dormant with signed occupant", 3, 0, () => { Write32(state, 0, 0); Write16(tile, 0xBF6C00 + Tile * 2, 0x8000); });
        Verify("preserve with occupied tile", 2, 2, () => Write16(tile, 0xBF6C00 + Tile * 2, 1));
        Verify("unrelated occupied command", 3, 0, () => { Write32(chore, Chore + 12, Command + 1); Write16(tile, 0xBF6C00 + Tile * 2, 1); });
        Verify("different exact command", 1, 0, () => Write32(chore, Chore + 12, Command + 1));
        Verify("different slot", 1, 0, () => Write32(chore, 0x84CC8, Slot + 1));
        Verify("different sender", 1, 0, () => Write32(chore, 0x36FA4C, Sender + 1));
        Verify("different raw sender", 1, 0, () => Write32(chore, Chore + 4, Sender + 1));
        Verify("different opcode", 1, 0, () => Write8(chore, Chore + 8, 21));
        Verify("different actor", 1, 0, () => Write32(state, 24, Actor + 1));
        Verify("different packed field", 1, 0, () => Write32(state, 16, Tile + 1));
        Verify("different captured coordinates", 1, 0, () => Write32(state, 20, X + 1 | Y << 16));
        Verify("wrong phase", 1, 0, () => Write32(chore, 0x84CCC, 1));
        Verify("clean field", 1, 1, () => Write32(tile, 0x898400 + Tile * 4, 0));
        Verify("finished moat", 1, 1, () => Write32(tile, 0x898400 + Tile * 4, 0x40000000));
        Verify("planned and finished", 1, 1, () => Write32(tile, 0x898400 + Tile * 4, 0x40004000));
        Verify("missing task", 1, 1, () => Write16(tile, 0x1EA23F0 + Tile * 2, 0));
        Verify("beyond live task limit", 1, 1, () => Write32(tile, 0x2038E30, 9));
        Verify("corrupt task limit", 1, 1, () => Write32(tile, 0x2038E30, -1));
        Verify("task packed mismatch", 1, 1, () => Write32(tile, Task, Tile + 1));
        Verify("task XY mismatch", 1, 1, () => Write16(tile, Task + 4, X + 1));
        Verify("task owner absent", 1, 1, () => Write8(tile, Task + 12, 0));
        Verify("task progress complete", 1, 1, () => Write8(tile, Task + 13, 4));
        Verify("task type fill", 1, 1, () => Write8(tile, Task + 14, 2));
        Verify("authenticated106 preserves exact tile without28 payload", 2, 2, () =>
        {
            Write32(state, 32, 106); Write8(chore, Chore + 8, 106);
            Write32(chore, 0x370BF8, 37); Write32(chore, Chore + 17, -1); Write32(chore, Chore + 21, -1);
        });
        Verify("authenticated106 clean tile still marks execution", 1, 1, () =>
        {
            Write32(state, 32, 106); Write8(chore, Chore + 8, 106);
            Write32(chore, 0x370BF8, 37); Write32(tile, 0x898400 + Tile * 4, 0);
        });
        Verify("authenticated106 wrong command cannot preserve", 1, 0, () =>
        { Write32(state, 32, 106); Write8(chore, Chore + 8, 106); Write32(chore, Chore + 12, Command + 1); });
        Verify("authenticated106 scope cannot affect ordinary28", 1, 0, () => Write8(chore, Chore + 8, 28));
        Verify("unknown scope opcode", 1, 0, () => Write32(state, 32, 99));
        Console.WriteLine($"{passed} native branch and ABI scenarios passed.");
        // Exercise the managed half of the production command token against independent native memory.
        long module = Alloc(0x8900000);
        type.GetField("module", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runtime, (IntPtr)module);
        type.GetField("choreManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runtime, (IntPtr)chore);
        type.GetField("pitchMoatToken", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runtime, (IntPtr)state);
        type.GetProperty("PitchMoatReady", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runtime, true);
        var clearMethod = type.GetMethod("ClearPitchMoatScope", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object cell = Activator.CreateInstance(type.Assembly.GetType("BugfixesAndQoL.PitchMoatLines.LineTile", true)!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { X, Y }, null)!;
        void Clear() => clearMethod.Invoke(runtime, null);
        var armNetwork = type.GetMethod("TryArmNetworkPitchMoat", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Reset(); Clear(); Write8(chore, Chore + 8, 106);
        if (!(bool)armNetwork.Invoke(runtime, [cell, Tile, Sender, Actor, Command])! || Read32(state, 32) != 106 ||
            Read32(state, 24) != Actor || (bool)armNetwork.Invoke(runtime, [cell, Tile, Sender, Actor, Command])!)
            throw new Exception("authenticated receiver scope must bind issuer and refuse nesting");
        Clear();
        if ((bool)armNetwork.Invoke(runtime, [cell, Tile, Sender, Actor, Command + 1])!)
            throw new Exception("authenticated scope accepted a foreign command");
        Write8(chore, Chore + 8, 28);
        if ((bool)armNetwork.Invoke(runtime, [cell, Tile, Sender, Actor, Command])!)
            throw new Exception("authenticated scope accepted Vanilla28");
        Console.WriteLine("PASS synchronous106 arm, exact issuer/command/opcode and finally-clear boundary.");

        var prepare = type.GetMethod("TryPrepareCommand", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pendingMethod = type.GetMethod("IsCommandPending", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Write32(chore, 0xDA0, Command - Sender * 100000000);
        Write32(module, 0x88E3D70, Sender); Write32(chore, 0x370BF4, Slot - 1); Write8(chore, Chore + 9, 0);
        object[] pendingArgs = [(byte)106, Sender, null!];
        if (!(bool)prepare.Invoke(runtime, pendingArgs)!) throw new Exception("queue receipt preflight");
        object receipt = pendingArgs[2];
        Write8(chore, Chore + 8, 106); Write8(chore, Chore + 9, 1);
        if (!(bool)pendingMethod.Invoke(runtime, [receipt])!) throw new Exception("live queue receipt");
        Write8(chore, Chore + 9, 9);
        if (!(bool)pendingMethod.Invoke(runtime, [receipt])!) throw new Exception("all native pre-execution states remain pending");
        Write8(chore, Chore + 9, 10);
        if ((bool)pendingMethod.Invoke(runtime, [receipt])!) throw new Exception("terminal queue receipt");
        Write8(chore, Chore + 9, 1); Write32(chore, Chore + 12, Command + 1);
        if ((bool)pendingMethod.Invoke(runtime, [receipt])!) throw new Exception("recycled queue receipt");
        Console.WriteLine("PASS live, terminal and recycled native queue receipt identity.");
        VerifyMoatCalls(type, runtime, module, tile, cell);
        GC.KeepAlive(hook);
    }
}
