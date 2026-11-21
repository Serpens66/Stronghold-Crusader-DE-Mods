using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BepInEx.Logging;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using Shared;

// Offline backend contract of AuditedEntryDetour (the runtime has no live probe): runs the installed RedBird
// NativeX64 backend and the real helper on copies of the real CrusaderDE.dll entries. Each copy holds
// the backend's whole scan window and ends directly in front of a
// PAGE_NOACCESS page, so a read beyond it faults instead of passing. Per entry: an audited entry that
// differs from the load image is rejected, a foreign jump inside the live span blocks the install before
// any byte is written, and the clean entry is published as an Indirect detour whose trampoline replays
// (or, for a conditional branch, relocates) the entry and continues behind it.
internal static class Program
{
    private const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const int PageSize = 4096;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Callback(IntPtr argument);
    private static readonly Callback Probe = argument => { };
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
        new Site("ExtremePowers dispatcher", 0xCD630, @"ExtremePowers\api\Native\NativeExtremePowersSignatures.cs", new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x6C, 0x24, 0x18 }),
        new Site("ExtremePowers selection", 0x105510, @"ExtremePowers\api\Native\NativeExtremePowersSignatures.cs", new byte[] { 0x40, 0x53, 0x48, 0x83, 0xEC, 0x20 }),
        new Site("ExtremePowers resource update", 0xCDB20, @"ExtremePowers\api\Native\NativeExtremePowersSignatures.cs", new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C, 0x24, 0x10 }),
        new Site("RandomEvents presentation", 0x1031B0, @"RandomEvents\src\NativeVanillaEventDispatcher.cs", new byte[] { 0x83, 0x39, 0x00, 0x0F, 0x84, 0xB0, 0x00, 0x00, 0x00 }),
        new Site("RandomEvents sound", 0x2940, @"RandomEvents\src\NativeVanillaEventDispatcher.cs", new byte[] { 0x41, 0xB9, 0x40, 0x00, 0x00, 0x00 }),
        new Site("RandomEvents action point", 0xF4D40, @"RandomEvents\src\NativeWildlifeEventDispatcher.cs", new byte[] { 0x40, 0x53, 0x48, 0x83, 0xEC, 0x20 }),
        new Site("AreaConstructionTest empty storage cap", 0xB8140, @"Testmods\AreaConstructionTest\src\StorageEmptyLimitPatch.cs", new byte[] { 0x4C, 0x63, 0x51, 0x50, 0x45, 0x33, 0xC9 }),
    };

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
        int pages = (length + PageSize - 1) / PageSize;
        IntPtr region = VirtualAlloc(IntPtr.Zero, (UIntPtr)((pages + 1) * PageSize), 0x3000, 0x04);
        if (region == IntPtr.Zero) throw new Exception("VirtualAlloc failed.");
        if (!VirtualProtect(region + pages * PageSize, (UIntPtr)PageSize, 0x01, out _))
            throw new Exception("Guard page could not be protected.");
        // The region stays allocated: a published detour and its stubs live until the process exits.
        IntPtr copy = region + pages * PageSize - length;
        Marshal.Copy(bytes, 0, copy, length);
        ulong moduleBase = unchecked((ulong)copy.ToInt64() - (ulong)site.Rva);
        // Unchanged load image: the real bytes at their RVA (the helper and the guard read only there).
        var loadImage = new byte[site.Rva + length];
        Buffer.BlockCopy(bytes, 0, loadImage, site.Rva, length);
        NativePatchGuard.Bind(ScanRegion.FromMemory(loadImage, moduleBase, "test load image"), log);

        byte[] changed = (byte[])site.Entry.Clone();
        changed[changed.Length - 1] ^= 0x01;
        ExpectThrow(() => AuditedEntryDetour.Prepare(loadImage, moduleBase, site.Rva, changed, Probe, site.Name),
            "load image differs", site.Name + ": a changed audited entry is rejected");

        // A foreign jump inside the live span blocks the install; nothing is written.
        byte[] original = Read(copy, site.Entry.Length);
        Marshal.Copy(new byte[] { 0xE9, 0xFB, 0xFF, 0xFF, 0xFF }, 0, copy + 1, 5);
        byte[] patched = Read(copy, site.Entry.Length);
        int errors = Errors.Count;
        ExpectThrow(() => AuditedEntryDetour.Prepare(loadImage, moduleBase, site.Rva, site.Entry, Probe, site.Name),
            "NATIVE_FOREIGN_PATCH:", site.Name + ": a foreign jump inside the span blocks the install");
        Check(Same(patched, Read(copy, site.Entry.Length)) && Errors.Count > errors,
            site.Name + ": the occupied span stays untouched and an Error is logged");
        Marshal.Copy(original, 0, copy, original.Length);

        var detour = AuditedEntryDetour.Prepare(loadImage, moduleBase, site.Rva, site.Entry, Probe, site.Name);
        Check(detour.Original != null && !detour.IsInstalled && detour.DisplacedByteCount == site.Entry.Length,
            site.Name + ": prepared Indirect/" + site.Entry.Length + " candidate with its original delegate");
        int watched = NativePatchGuard.WatchedCount;
        AuditedEntryDetour.Publish(detour, Probe, site.Name);
        byte[] live = Read(copy, site.Entry.Length);
        bool padded = true;
        for (int index = 6; index < live.Length; index++) padded &= live[index] == 0x90;
        Check(detour.IsInstalled && live[0] == 0xFF && live[1] == 0x25 && padded && NativePatchGuard.WatchedCount == watched + 1,
            site.Name + ": published as jmp [rip+slot] with nop padding and watched");
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
