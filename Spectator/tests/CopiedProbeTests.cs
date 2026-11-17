using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// Offline backend contract (no live probe in the runtime): runs the installed RedBird NativeX64 backend
// on copies of the real entries. Each copy holds the real bytes for the backend's whole scan window and
// ends directly in front of a PAGE_NOACCESS page, so any read beyond it faults instead of passing.
internal static class CopiedProbeTests
{
    private const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const int PageSize = 4096;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ProbeCallback(IntPtr argument);
    private static readonly ProbeCallback Callback = argument => { };

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
                    throw new Exception("Installed CrusaderDE.dll differs from the audited Spectator hash.");
            // Entries and displaced lengths mirror SpectatorRuntime's audited constants.
            ProbeInstalledEntry(dll, "availability", 0xCC420, "48895C24204489442418", 10);
            ProbeInstalledEntry(dll, "visual map", 0x65830, "4C8BDC53554881EC88000000", 12);
            ProbeInstalledEntry(dll, "visual resource", 0x6E620, "48895C241855", 6);
            Console.WriteLine("PASS: installed RedBird backend produces the audited Indirect detours on the real entries (availability 10, map 12, resource 6 bytes).");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
    }

    private static void ProbeInstalledEntry(byte[] dll, string name, int rva, string prefixHex, int displaced)
    {
        // The backend may decode the whole FunctionScanLimit window; +16 covers a last instruction.
        int length = NativeDetourBackend.Instance.Options.FunctionScanLimit + 16;
        var image = new byte[length];
        Buffer.BlockCopy(dll, FileOffset(dll, rva, length), image, 0, length);
        for (int index = 0; index < prefixHex.Length / 2; index++)
            if (image[index] != Convert.ToByte(prefixHex.Substring(index * 2, 2), 16))
                throw new Exception(name + " entry differs from the audited prefix.");
        int pages = (image.Length + PageSize - 1) / PageSize;
        IntPtr region = VirtualAlloc(IntPtr.Zero, (UIntPtr)((pages + 1) * PageSize), 0x3000, 0x04);
        if (region == IntPtr.Zero) throw new Exception("VirtualAlloc failed.");
        try
        {
            if (!VirtualProtect(region + pages * PageSize, (UIntPtr)PageSize, 0x01, out _))
                throw new Exception("Guard page could not be protected.");
            IntPtr copy = region + pages * PageSize - image.Length;
            Marshal.Copy(image, 0, copy, image.Length);
            ulong target = unchecked((ulong)copy.ToInt64());
            var request = new DetourRequest<ProbeCallback> { Name = "Spectator offline backend test " + name,
                TargetAddress = target, Callback = Callback };
            var probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<ProbeCallback>;
            try
            {
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != displaced || probe.TargetAddress != target || probe.IsInstalled)
                    throw new Exception(name + " backend detour differs from the audited Indirect/" + displaced + " contract.");
                probe.Enable();
                var patch = new byte[6];
                Marshal.Copy(copy, patch, 0, patch.Length);
                if (!probe.IsInstalled || patch[0] != 0xFF || patch[1] != 0x25 ||
                    (long)target + 6 + BitConverter.ToInt32(patch, 2) != probe.PointerSlot.ToInt64())
                    throw new Exception(name + " copied probe patch form differs.");
            }
            finally { probe?.Dispose(); }
            var restored = new byte[image.Length];
            Marshal.Copy(copy, restored, 0, restored.Length);
            for (int index = 0; index < image.Length; index++)
                if (restored[index] != image[index]) throw new Exception(name + " copied probe was not restored.");
        }
        finally { VirtualFree(region, UIntPtr.Zero, 0x8000); }
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
}
