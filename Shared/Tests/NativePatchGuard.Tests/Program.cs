using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using Shared;

// Runs NativePatchGuard with the installed RedBird on a private executable page whose copy stands in for
// the Script Extender's load snapshot: clean spans install, a foreign patch inside a span blocks the
// install before any byte is written, a patch directly behind it does not, and a later overwrite of a
// published span is reported once.
internal static class Program
{
    private const int PageSize = 4096;
    // mov [rsp+8],rbx: a 5-byte instruction, so a 14-byte inline hook displaces 15 bytes.
    private static readonly byte[] Move = { 0x48, 0x89, 0x5C, 0x24, 0x08 };
    // mov [rsp+8],rbx; mov [rsp+16],rsi; push rdi; sub rsp,32; xor eax,eax; add rsp,32; pop rdi; ret
    private static readonly byte[] Function =
    {
        0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74, 0x24, 0x10, 0x57, 0x48, 0x83, 0xEC, 0x20,
        0x33, 0xC0, 0x48, 0x83, 0xC4, 0x20, 0x5F, 0xC3
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NoArguments();
    private static readonly NoArguments Callback = () => 0;
    private static readonly List<string> Errors = new List<string>();
    private static int failures;

    private static int Main()
    {
        IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)PageSize, 0x3000, 0x40);
        if (page == IntPtr.Zero) { Console.Error.WriteLine("FAIL: VirtualAlloc"); return 1; }
        ulong image = unchecked((ulong)page.ToInt64());
        try
        {
            for (int site = 0; site < 6; site++)
                for (int index = 0; index < 6; index++)
                    Marshal.Copy(Move, 0, page + site * 0x100 + index * Move.Length, Move.Length);
            Marshal.Copy(Function, 0, page + 0x600, Function.Length);
            Marshal.Copy(Function, 0, page + 0x700, Function.Length);
            var snapshot = new byte[PageSize];
            Marshal.Copy(page, snapshot, 0, PageSize);
            ScanRegion region = ScanRegion.FromMemory(snapshot, image, "test snapshot");
            var log = new ManualLogSource("NativePatchGuardTests");
            log.LogEvent += (sender, args) =>
            {
                Console.WriteLine("  log " + args.Level + ": " + args.Data);
                if ((args.Level & LogLevel.Error) != 0) Errors.Add(Convert.ToString(args.Data));
            };
            NativePatchGuard.Bind(region, log);

            // 1. Clean inline span: prepared, compared, enabled and watched.
            var clean = new HookHandle<X64InlineHook>();
            CommitResult result = NativePatchGuard.Commit(Inline(region, image, clean), "clean inline");
            Check(result.IsCompleteSuccess && clean.IsInstalled, "clean inline hook is installed");
            Check(Marshal.ReadByte(page) == 0xFF && Marshal.ReadByte(page + 1) == 0x25, "clean inline hook wrote its jump");
            Check(NativePatchGuard.WatchedCount == 1, "clean inline hook is watched");
            Check(NativePatchGuard.Recheck() == 0 && Errors.Count == 0, "an untouched published span is not reported");

            // 2. A later overwrite is reported once per change; restoring it clears the report.
            byte[] published = Read(page, 15);
            Marshal.WriteByte(page, 0xE9);
            Check(NativePatchGuard.Recheck() == 1 && Errors.Count == 1 && Errors[0].Contains("NATIVE_FOREIGN_PATCH_LATE"),
                "a later overwrite is reported as an Error");
            Check(NativePatchGuard.Recheck() == 0 && Errors.Count == 1, "the same overwrite is reported only once");
            Marshal.Copy(published, 0, page, published.Length);
            Check(NativePatchGuard.Recheck() == 0, "a restored span is no longer reported");
            Marshal.WriteByte(page, 0xE9);
            // RuntimeHealth folds an identical Error within its window; Recheck still counts the new deviation.
            Check(NativePatchGuard.Recheck() == 1, "a new overwrite after a restore is reported again");
            Marshal.Copy(published, 0, page, published.Length);

            // 3. A foreign jump inside the span blocks the install before any byte is written.
            int errorsBefore = Errors.Count;
            IntPtr occupied = page + 0x100;
            WriteJump(occupied + 5);
            byte[] before = Read(occupied, 15);
            var blocked = new HookHandle<X64InlineHook>();
            HookTransaction blockedTransaction = Inline(region, image + 0x100, blocked);
            ExpectThrow(() => NativePatchGuard.Commit(blockedTransaction, "occupied inline"), "NATIVE_FOREIGN_PATCH:",
                "a foreign jump inside the span blocks the install");
            Check(Same(before, Read(occupied, 15)) && !blocked.IsInstalled, "the occupied span stays untouched");
            Check(Errors.Count > errorsBefore && Errors[Errors.Count - 1].Contains("Jump at +5"), "the blocked install logs an Error with the jump");
            blockedTransaction.Dispose();

            // 4. A foreign jump directly behind the span is allowed.
            int errorsAfterBlock = Errors.Count;
            WriteJump(page + 0x200 + 15);
            var adjacent = new HookHandle<X64InlineHook>();
            result = NativePatchGuard.Commit(Inline(region, image + 0x200, adjacent), "adjacent inline");
            Check(result.IsCompleteSuccess && adjacent.IsInstalled && Errors.Count == errorsAfterBlock, "a foreign jump behind the span is allowed");

            // 5. Transactions that install on commit are rejected before RedBird writes anything.
            var eager = new HookTransaction(region, null,
                new HookTransactionOptions { InstallOnCommit = true, Backend = NativeDetourBackend.Instance, FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
            var eagerHandle = new HookHandle<X64InlineHook>();
            eager.AddInline(eagerHandle, HookTarget.FromAddress(image + 0x300), (assembler, original, back) => { }, 14);
            ExpectThrow(() => NativePatchGuard.Commit(eager, "eager"), "InstallOnCommit = false", "an installing transaction is rejected");
            Check(Marshal.ReadByte(page + 0x300) == 0x48 && eager.State == TransactionState.Building, "the rejected transaction wrote nothing");
            eager.Dispose();

            // 6. Detours are checked over their displaced prologue (generic NativeDetour<T>).
            var detour = new DetourHandle<NoArguments>();
            var detourTransaction = new HookTransaction(region, null, Options());
            detourTransaction.AddDetour(detour, HookTarget.FromAddress(image + 0x600), Callback);
            result = NativePatchGuard.Commit(detourTransaction, "clean detour");
            Check(result.IsCompleteSuccess && detour.IsInstalled && NativePatchGuard.WatchedCount == 3, "a clean detour is installed and watched");
            WriteJump(page + 0x700 + 5);
            byte[] detourBefore = Read(page + 0x700, 15);
            var blockedDetour = new DetourHandle<NoArguments>();
            var blockedDetourTransaction = new HookTransaction(region, null, Options());
            blockedDetourTransaction.AddDetour(blockedDetour, HookTarget.FromAddress(image + 0x700), Callback);
            ExpectThrow(() => NativePatchGuard.Commit(blockedDetourTransaction, "occupied detour"), "NATIVE_FOREIGN_PATCH:",
                "a foreign jump inside a detour prologue blocks the install");
            Check(Same(detourBefore, Read(page + 0x700, 15)) && !blockedDetour.IsInstalled, "the occupied prologue stays untouched");
            blockedDetourTransaction.Dispose();

            // 7. An owner that rolls back a candidate after a failed post-check restores the original
            //    bytes; that span is dropped, not reported as overwritten.
            var rolledBack = new HookHandle<X64InlineHook>();
            HookTransaction rollbackTransaction = Inline(region, image + 0x400, rolledBack);
            NativePatchGuard.Commit(rollbackTransaction, "rolled back inline");
            int watchedBeforeRollback = NativePatchGuard.WatchedCount, errorsBeforeRollback = Errors.Count;
            rollbackTransaction.Dispose();
            Check(Marshal.ReadByte(page + 0x400) == 0x48, "the rolled back candidate restored its original bytes");
            Check(NativePatchGuard.Recheck() == 0 && Errors.Count == errorsBeforeRollback &&
                NativePatchGuard.WatchedCount == watchedBeforeRollback - 1, "a rolled back candidate is dropped, not reported");

            // 8. The hint names the module an indirect jump leads into.
            IntPtr kernel = GetProcAddress(GetModuleHandleW("kernel32.dll"), "GetTickCount");
            var indirect = new byte[14];
            indirect[0] = 0xFF; indirect[1] = 0x25;
            BitConverter.GetBytes(kernel.ToInt64()).CopyTo(indirect, 6);
            string hint = NativePatchGuard.DescribeJump(indirect, 2, image + 0x500);
            Check(hint.IndexOf("kernel32", StringComparison.OrdinalIgnoreCase) >= 0, "the jump hint names the target module: " + hint);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: unexpected " + error);
            failures++;
        }
        // The page stays allocated: RedBird stubs may still point into it until the process exits.
        if (failures != 0)
        {
            Console.Error.WriteLine("FAIL: " + failures + " NativePatchGuard check(s) failed.");
            return 1;
        }
        Console.WriteLine("PASS: NativePatchGuard blocks foreign patches inside spans, allows adjacent ones and reports later overwrites once.");
        return 0;
    }

    private static HookTransactionOptions Options() => new HookTransactionOptions
    {
        InstallOnCommit = false,
        Backend = NativeDetourBackend.Instance,
        FailureMode = TransactionFailureMode.RollbackAndThrow,
        OwnsHooks = true
    };

    private static HookTransaction Inline(ScanRegion region, ulong address, HookHandle<X64InlineHook> handle)
    {
        var transaction = new HookTransaction(region, null, Options());
        transaction.AddInline(handle, HookTarget.FromAddress(address), (assembler, original, back) => { }, 14);
        return transaction;
    }

    // jmp rel32 to itself: a foreign hook entry at an instruction boundary.
    private static void WriteJump(IntPtr address)
    {
        Marshal.Copy(new byte[] { 0xE9, 0xFB, 0xFF, 0xFF, 0xFF }, 0, address, 5);
    }

    private static byte[] Read(IntPtr address, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(address, bytes, 0, length);
        return bytes;
    }

    private static bool Same(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
        return true;
    }

    private static void ExpectThrow(Action action, string text, string name)
    {
        try { action(); }
        catch (Exception error)
        {
            Check(error.Message.Contains(text), name + " (" + error.GetType().Name + ": " + error.Message + ")");
            return;
        }
        Check(false, name + " (no exception)");
    }

    private static void Check(bool condition, string name)
    {
        Console.WriteLine((condition ? "ok   " : "FAIL ") + name);
        if (!condition) failures++;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
}
