using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DepositRestorationTest;
using Iced.Intel;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

internal static unsafe class Program
{
    private const int Tile = 17;
    private static IntPtr state, logic, code, undo;
    private static byte* Remembered => (byte*)state + DepositHooks.HistoryOffset + Tile;
    private static int* TileFlags => (int*)logic + Tile;
    private const int Wall = (int)TilePropertyFlag.IsWall;
    private const int Land = (int)TilePropertyFlag.IsLand;
    private const int Stone = (int)TilePropertyFlag.HasStone;
    private const int Iron = (int)TilePropertyFlag.HasIron;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Run();

    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the installed CrusaderDE.dll path.");
        var image = File.ReadAllBytes(args[0]);
        using (var sha = SHA256.Create())
            Check(BitConverter.ToString(sha.ComputeHash(image)).Replace("-", "") == DepositHooks.NativeHash, "installed game hash");
        state = Marshal.AllocHGlobal(DepositHooks.HistoryOffset + DepositHooks.Capacity);
        logic = Marshal.AllocHGlobal(DepositHooks.Capacity * sizeof(int));
        undo = Marshal.AllocHGlobal(8);
        code = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x04);
        if (code == IntPtr.Zero) throw new InvalidOperationException("Could not allocate private test code.");
        try
        {
            foreach (var site in DepositHooks.Sites)
            {
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(site.Bytes));
                while (decoder.IP < (ulong)site.Bytes.Length)
                {
                    decoder.Decode(out var instruction);
                    Check(!instruction.IsInvalid && instruction.FlowControl == FlowControl.Next, site.Name + " displaced instructions");
                }
                Check(decoder.IP == (ulong)site.Bytes.Length, site.Name + " instruction boundary");
                CheckBackend(site, image);

                byte deposit = (byte)(Iron >> DepositHooks.FlagShift);
                *Remembered = deposit;
                *TileFlags = Land;
                Execute(site, enabled: false);
                Check(*Remembered == deposit && *TileFlags == Land, site.Name + " dormant");
                Execute(site, enabled: true, tile: DepositHooks.Capacity);
                Check(*Remembered == deposit && *TileFlags == Land, site.Name + " bounds");

                switch (site.Action)
                {
                    case DepositHooks.Operation.CaptureManual:
                    case DepositHooks.Operation.CaptureAi:
                        *Remembered = 0;
                        *TileFlags = Land | Stone;
                        Execute(site, enabled: true);
                        Check(*Remembered == Stone >> DepositHooks.FlagShift && *TileFlags == (Land | Stone), site.Name + " captures stone");
                        *TileFlags = Land | Wall;
                        Execute(site, enabled: true);
                        Check(*Remembered == Stone >> DepositHooks.FlagShift && *TileFlags == (Land | Wall), site.Name + " repeated placement");
                        *TileFlags = Land | Iron;
                        Execute(site, enabled: true);
                        Check(*Remembered == deposit && *TileFlags == (Land | Iron), site.Name + " captures iron");
                        break;
                    case DepositHooks.Operation.Undo:
                        Execute(site, enabled: true, undoFlags: Wall);
                        Check(*Remembered == deposit && *TileFlags == Land, "undo retaining wall keeps history");
                        goto default;
                    case DepositHooks.Operation.ReplaceTerrain:
                        Execute(site, enabled: true);
                        Check(*Remembered == 0 && *TileFlags == Land, "map terrain replacement discards wall history");
                        break;
                    default:
                        Execute(site, enabled: true);
                        Check(*Remembered == 0 && *TileFlags == (Land | Iron), site.Name + " restores captured deposit");
                        *TileFlags = Land;
                        Execute(site, enabled: true);
                        Check(*TileFlags == Land, site.Name + " consumes history once");
                        break;
                }
            }
            Console.WriteLine($"PASS: emitted machine code, instruction boundaries and installed RedBird backend for {DepositHooks.Sites.Length} sites");
            return 0;
        }
        finally
        {
            VirtualFree(code, UIntPtr.Zero, 0x8000);
            Marshal.FreeHGlobal(undo);
            Marshal.FreeHGlobal(logic);
            Marshal.FreeHGlobal(state);
        }
    }

    private static void CheckBackend(DepositHooks.Site site, byte[] image)
    {
        const int page = 4096;
        int length = NativeDetourBackend.Instance.Options.FunctionScanLimit + 16;
        int readable = (length + page - 1) / page * page;
        int offset = FileOffset(image, site.Rva, length);
        for (int i = 0; i < site.Bytes.Length; i++)
            Check(site.Bytes[i] == image[offset + i], site.Name + " installed bytes");
        var allocation = VirtualAlloc(IntPtr.Zero, (UIntPtr)(readable + page), 0x3000, 0x04);
        Check(allocation != IntPtr.Zero, "allocate guarded backend test memory");
        try
        {
            Check(VirtualProtect(allocation + readable, (UIntPtr)page, 0x01, out _), "install guard page");
            var copy = allocation + readable - length;
            Marshal.Copy(image, offset, copy, length);
            using (var hook = new X64InlineHook((ulong)copy, site.Bytes.Length, name: site.Name))
            {
                Check(hook.DisplacedByteCount == site.Bytes.Length && !hook.IsInstalled, site.Name + " backend preparation");
                hook.Generate((asm, original, _) => DepositHooks.Emit(asm, site, (ulong)state, (ulong)logic, original));
                Check(hook.StubAddress != IntPtr.Zero, site.Name + " backend stub");
                hook.Enable();
                var patch = (byte*)copy;
                Check(hook.IsInstalled && patch[0] == 0xFF && patch[1] == 0x25 && *(uint*)(patch + 2) == 0 &&
                    *(ulong*)(patch + 6) == (ulong)hook.StubAddress, site.Name + " backend jump");
                for (int i = 14; i < site.Bytes.Length; i++)
                    Check(patch[i] == 0x90, site.Name + " backend padding");
            }
        }
        finally { VirtualFree(allocation, UIntPtr.Zero, 0x8000); }
    }

    private static int FileOffset(byte[] image, int rva, int size)
    {
        int pe = BitConverter.ToInt32(image, 0x3C);
        int first = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
        int count = BitConverter.ToUInt16(image, pe + 6);
        for (int i = 0; i < count; i++)
        {
            int section = first + 40 * i;
            int delta = rva - BitConverter.ToInt32(image, section + 12);
            if (delta >= 0 && delta + size <= BitConverter.ToInt32(image, section + 16))
                return BitConverter.ToInt32(image, section + 20) + delta;
        }
        throw new InvalidOperationException("RVA is outside the PE file sections.");
    }

    private static void Execute(DepositHooks.Site site, bool enabled, int tile = Tile, int undoFlags = 0)
    {
        Marshal.WriteByte(state, enabled ? (byte)1 : (byte)0);
        Marshal.WriteInt32(undo, 4, undoFlags);
        var asm = new Assembler(64);
        asm.push(rbx); asm.push(rsi); asm.push(rdi); asm.push(r12); asm.push(r14);
        asm.mov(rsi, (ulong)undo);
        asm.mov(site.Tile, tile);
        asm.mov(eax, *TileFlags);
        // Execute the production bookkeeping against private grids. No game continuation runs.
        DepositHooks.Emit(asm, site, (ulong)state, (ulong)logic, ReadOnlySpan<Instruction>.Empty);
        asm.pop(r14); asm.pop(r12); asm.pop(rdi); asm.pop(rsi); asm.pop(rbx);
        asm.ret();
        using (var buffer = new MemoryStream())
        {
            asm.Assemble(new StreamCodeWriter(buffer), (ulong)code);
            var bytes = buffer.ToArray();
            Check(VirtualProtect(code, (UIntPtr)4096, 0x04, out _), "write private fixture");
            Marshal.Copy(bytes, 0, code, bytes.Length);
            Check(VirtualProtect(code, (UIntPtr)4096, 0x20, out _), "execute private fixture");
            Check(FlushInstructionCache(new IntPtr(-1), code, (UIntPtr)bytes.Length), "flush private fixture");
        }
        Marshal.GetDelegateForFunctionPointer<Run>(code)();
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr bytes, uint type, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr address, UIntPtr bytes, uint protection, out uint old);
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, UIntPtr bytes, uint type);
}
