using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using Shared;

// Four trade-entry offline backend contracts (the runtime has no live probe): runs the installed RedBird
// NativeX64 backend and the real helper on copies of the real CrusaderDE.dll entries. Each copy holds
// the backend's whole scan window and ends directly in front of a
// PAGE_NOACCESS page, so a read beyond it faults instead of passing. Per entry: an audited entry that
// differs from the load image is rejected, a foreign jump inside the live span blocks the install before
// any byte is written, and the clean entry is published as an Indirect detour whose trampoline replays
// (or, for a conditional branch, relocates) the entry and continues behind it.
// Executes all actual callback ABIs against bounded private continuations; it proves
// entry/trampoline semantics, not the full game's trade behavior or resource safety.
internal static class Program
{
    private const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const int PageSize = 4096;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PairCall(IntPtr root, int player);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BuyCall(IntPtr root, int player, int good, int signedAmount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SellCall(IntPtr root, int player, int good, int signedAmount);
    private static readonly List<string> Errors = new List<string>();
    private static int failures;

    private sealed class Site
    {
        internal Site(string name, int rva, string source, byte[] entry) { Name = name; Rva = rva; Source = source; Entry = entry; }
        internal string Name { get; }
        internal int Rva { get; }
        internal string Source { get; }
        internal byte[] Entry { get; }
    }

    // Mirrors the audited constants in the mods; the source check below keeps both in step.
    private static readonly Site[] Sites =
    {
        new Site("purchase decision", 0x3EC90, Source, new byte[] { 0x40, 0x56, 0x41, 0x54, 0x41, 0x55 }),
        new Site("recruitment", 0x40740, Source, new byte[] { 0x40, 0x55, 0x56, 0x41, 0x56, 0x48, 0x81, 0xEC, 0x60, 0x01, 0x00, 0x00 }),
        new Site("buy", 0x29650, Source, new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C, 0x24, 0x10 }),
        new Site("sell", 0x29700, Source, new byte[] { 0x45, 0x85, 0xC9, 0x0F, 0x88, 0x96, 0x00, 0x00, 0x00 }),
    };
    private const string Source = @"Testmods\AIMarketOverbuyDiagnostic\src\MarketTradeTrace.DevDiag.cs";

    private static int Main()
    {
        try
        {
            string game = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            byte[] dll = File.ReadAllBytes(Path.Combine(game,
                @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"));
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(dll)).Replace("-", "") != NativeSha256)
                    throw new Exception("Installed CrusaderDE.dll differs from the audited hash; re-audit the entry detours.");
            string workspace = FindWorkspace();
            var log = new ManualLogSource("AuditedEntryDetourTests");
            log.LogEvent += (sender, args) =>
            {
                if ((args.Level & LogLevel.Error) != 0) Errors.Add(Convert.ToString(args.Data));
            };
            foreach (Site site in Sites)
            {
                string text = File.ReadAllText(Path.Combine(workspace, site.Source));
                string literal = string.Join(", ", site.Entry.Select(value => "0x" + value.ToString("X2")));
                Check(text.Contains("{ " + literal + " }"), site.Name + ": audited entry constant is unchanged in " + site.Source);
                Match rvas = Regex.Match(text, @"EntryRvas\s*=\s*\{([^}]+)\}");
                string[] sourceRvas = Regex.Matches(rvas.Groups[1].Value, @"0x[0-9A-Fa-f]+").Cast<Match>().Select(m => m.Value).ToArray();
                Check(sourceRvas.Length == Sites.Length && Convert.ToInt32(sourceRvas[Array.IndexOf(Sites, site)].Substring(2), 16) == site.Rva,
                    site.Name + ": productive RVA/order matches exact entry");
                Check(text.Contains("delegate void PairCall(IntPtr profileRoot, int player)") &&
                    text.Contains("delegate int BuyCall(IntPtr profileRoot, int player, int good, int signedAmount)") &&
                    text.Contains("delegate void SellCall(IntPtr profileRoot, int player, int good, int signedAmount)") &&
                    Regex.Matches(text, @"UnmanagedFunctionPointer\(CallingConvention.Cdecl\)").Count == 3,
                    site.Name + ": productive Windows-x64 callback ABI matches tests");
                RunSite(dll, site, log);
            }
            Check(NativePatchGuard.Recheck() == 0, "published copies are not reported as overwritten");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: unexpected " + error);
            failures++;
        }
        if (failures != 0)
        {
            Console.Error.WriteLine("FAIL: " + failures + " AuditedEntryDetour check(s) failed.");
            return 1;
        }
        Console.WriteLine("PASS: installed RedBird backend publishes the audited Indirect entry detours and refuses changed or foreign-patched entries.");
        return 0;
    }

    private static void RunSite(byte[] dll, Site site, ManualLogSource log)
    {
        // The backend may decode the whole FunctionScanLimit window; +16 covers a last instruction.
        int length = NativeDetourBackend.Instance.Options.FunctionScanLimit + 16;
        var bytes = new byte[length];
        Buffer.BlockCopy(dll, FileOffset(dll, site.Rva, length), bytes, 0, length);
        Check(Same(site.Entry, bytes.Take(site.Entry.Length).ToArray()), site.Name + ": exact entry bytes match installed native PE");
        IntPtr counts = Marshal.AllocHGlobal(8);
        Marshal.WriteInt32(counts, 0); Marshal.WriteInt32(counts + 4, 0);
        // Only private copies execute. Audited entry is unchanged; continuation is
        // a bounded observable stub, never the game's full function with fake state.
        byte[] suffix = site.Rva == 0x3EC90 ? new byte[] { 0x41,0x5D,0x41,0x5C,0x5E,0xC3 } :
            site.Rva == 0x40740 ? new byte[] { 0x48,0x81,0xC4,0x60,0x01,0,0,0x41,0x5E,0x5E,0x5D,0xC3 } :
            site.Rva == 0x29650 ? new byte[] { 0x89,0xD0,0x44,0x01,0xC0,0x44,0x01,0xC8,0xC3 } : new byte[] { 0xC3 };
        byte[] fixture = Increment(counts).Concat(suffix).ToArray();
        Buffer.BlockCopy(fixture, 0, bytes, site.Entry.Length, fixture.Length);
        if (site.Rva == 0x29700)
        {
            byte[] negative = Increment(counts + 4).Concat(new byte[] { 0xC3 }).ToArray();
            Buffer.BlockCopy(negative, 0, bytes, 0x9F, negative.Length);
        }
        int pages = (length + PageSize - 1) / PageSize;
        IntPtr region = VirtualAlloc(IntPtr.Zero, (UIntPtr)((pages + 1) * PageSize), 0x3000, 0x04);
        if (region == IntPtr.Zero) throw new Exception("VirtualAlloc failed.");
        if (!VirtualProtect(region + pages * PageSize, (UIntPtr)PageSize, 0x01, out _))
            throw new Exception("Guard page could not be protected.");
        // The region stays allocated: a published detour and its stubs live until the process exits.
        IntPtr copy = region + pages * PageSize - length;
        Marshal.Copy(bytes, 0, copy, length);
        if (!VirtualProtect(copy, (UIntPtr)length, 0x40, out _)) throw new Exception("Executable private fixture protection failed.");
        ulong moduleBase = unchecked((ulong)copy.ToInt64() - (ulong)site.Rva);
        // Unchanged load image: the real bytes at their RVA (the helper and the guard read only there).
        var loadImage = new byte[site.Rva + length];
        Buffer.BlockCopy(bytes, 0, loadImage, site.Rva, length);
        NativePatchGuard.Bind(ScanRegion.FromMemory(loadImage, moduleBase, "test load image"), log);

        if (site.Rva == 0x29650)
        {
            BuyCall originalCall = null; int callbacks = 0;
            BuyCall callback = (root, player, good, amount) =>
            { CheckArguments(root, player, good, amount); callbacks++; return originalCall(root, player, good, amount); };
            originalCall = PrepareAndPublish(site, copy, loadImage, moduleBase, callback).Original;
            var call = Marshal.GetDelegateForFunctionPointer<BuyCall>(copy);
            int result = call(new IntPtr(0x12345678), 7, 19, -3);
            Check(callbacks == 1 && Marshal.ReadInt32(counts) == 1 && result == 23,
                site.Name + ": correct four-argument ABI, signed amount, original exactly once, int return preserved");
        }
        else if (site.Rva == 0x29700)
        {
            SellCall originalCall = null; int callbacks = 0;
            SellCall callback = (root, player, good, amount) =>
            { CheckArguments(root, player, good, amount); callbacks++; originalCall(root, player, good, amount); };
            originalCall = PrepareAndPublish(site, copy, loadImage, moduleBase, callback).Original;
            var call = Marshal.GetDelegateForFunctionPointer<SellCall>(copy);
            call(new IntPtr(0x12345678), 7, 19, 3);
            call(new IntPtr(0x12345678), 7, 19, -3);
            Check(callbacks == 2 && Marshal.ReadInt32(counts) == 1 && Marshal.ReadInt32(counts + 4) == 1,
                site.Name + ": void four-argument ABI, original once per call, JS positive/negative paths preserved");
        }
        else
        {
            PairCall originalCall = null; int callbacks = 0;
            PairCall callback = (root, player) =>
            { Check(root == new IntPtr(0x12345678) && player == 7, site.Name + ": pair arguments preserved"); callbacks++; originalCall(root, player); };
            originalCall = PrepareAndPublish(site, copy, loadImage, moduleBase, callback).Original;
            Marshal.GetDelegateForFunctionPointer<PairCall>(copy)(new IntPtr(0x12345678), 7);
            Check(callbacks == 1 && Marshal.ReadInt32(counts) == 1, site.Name + ": pair void ABI, original exactly once, stack restored");
        }
        // Counter remains allocated together with process-lifetime published copies.
    }

    private static byte[] Increment(IntPtr counter) => new byte[] { 0x48, 0xB8 }
        .Concat(BitConverter.GetBytes(counter.ToInt64())).Concat(new byte[] { 0xFF, 0x00 }).ToArray();

    private static void CheckArguments(IntPtr root, int player, int good, int amount) =>
        Check(root == new IntPtr(0x12345678) && player == 7 && good == 19 && (amount == 3 || amount == -3), "four native arguments preserved");

    private static NativeDetour<T> PrepareAndPublish<T>(Site site, IntPtr copy, byte[] loadImage, ulong moduleBase, T callback) where T : Delegate
    {

        byte[] changed = (byte[])site.Entry.Clone();
        changed[changed.Length - 1] ^= 0x01;
        ExpectThrow(() => AuditedEntryDetour.Prepare(loadImage, moduleBase, site.Rva, changed, callback, site.Name),
            "load image differs", site.Name + ": a changed audited entry is rejected");

        // A foreign jump inside the live span blocks the install; nothing is written.
        byte[] original = Read(copy, site.Entry.Length);
        Marshal.Copy(new byte[] { 0xE9, 0xFB, 0xFF, 0xFF, 0xFF }, 0, copy + 1, 5);
        byte[] patched = Read(copy, site.Entry.Length);
        int errors = Errors.Count;
        ExpectThrow(() => AuditedEntryDetour.Prepare(loadImage, moduleBase, site.Rva, site.Entry, callback, site.Name),
            "NATIVE_FOREIGN_PATCH:", site.Name + ": a foreign jump inside the span blocks the install");
        Check(Same(patched, Read(copy, site.Entry.Length)) && Errors.Count > errors,
            site.Name + ": the occupied span stays untouched and an Error is logged");
        Marshal.Copy(original, 0, copy, original.Length);

        var detour = AuditedEntryDetour.Prepare(loadImage, moduleBase, site.Rva, site.Entry, callback, site.Name);
        Check(detour.Original != null && !detour.IsInstalled && detour.DisplacedByteCount == site.Entry.Length,
            site.Name + ": prepared Indirect/" + site.Entry.Length + " candidate with its original delegate");
        int watched = NativePatchGuard.WatchedCount;
        AuditedEntryDetour.Publish(detour, callback, site.Name);
        byte[] live = Read(copy, site.Entry.Length);
        bool padded = true;
        for (int index = 6; index < live.Length; index++) padded &= live[index] == 0x90;
        Check(detour.IsInstalled && live[0] == 0xFF && live[1] == 0x25 && padded && NativePatchGuard.WatchedCount == watched + 1,
            site.Name + ": published as jmp [rip+slot] with nop padding and watched");
        Check(detour.Scheme.ToString() == "Indirect" && detour.ChainDepth == 1 &&
            copy.ToInt64() + 6 + BitConverter.ToInt32(live, 2) == detour.PointerSlot.ToInt64() &&
            Marshal.ReadInt64(detour.PointerSlot) == detour.HookEntryPointAddress.ToInt64() &&
            detour.TrampolineAddress != IntPtr.Zero && detour.OriginalEntryPointAddress != IntPtr.Zero,
            site.Name + ": pointer slot, hook entry, original and audited trampoline/continuation contract verified");
        return detour;
    }

    private static string FindWorkspace()
    {
        string directory = AppDomain.CurrentDomain.BaseDirectory;
        while (directory != null && !File.Exists(Path.Combine(directory, "AGENTS.md")))
            directory = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar));
        return directory ?? throw new DirectoryNotFoundException("Workspace root was not found.");
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

    private static int FileOffset(byte[] pe, int rva, int length)
    {
        int header = BitConverter.ToInt32(pe, 0x3C);
        int sections = BitConverter.ToUInt16(pe, header + 6);
        int table = header + 24 + BitConverter.ToUInt16(pe, header + 20);
        for (int index = 0; index < sections; index++)
        {
            int entry = table + index * 40;
            int virtualAddress = BitConverter.ToInt32(pe, entry + 12);
            int size = Math.Max(BitConverter.ToInt32(pe, entry + 8), BitConverter.ToInt32(pe, entry + 16));
            if (rva >= virtualAddress && rva + length <= virtualAddress + size)
                return BitConverter.ToInt32(pe, entry + 20) + rva - virtualAddress;
        }
        throw new Exception("RVA 0x" + rva.ToString("X") + " is outside the native image sections.");
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
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);
}
