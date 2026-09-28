using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.Linq;
using System.Runtime.InteropServices;

internal static class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void Builder(IntPtr manager, int playerId);

    private static void Main()
    {
        byte[] entry = Convert.FromHexString(
            "48895C241048896C24205741544155415641574883EC30" +
            "33DB4C63FA488D159DECE7FF4C8BF14969");
        IntPtr memory = Marshal.AllocHGlobal(entry.Length);
        NativeDetour<Builder>? probe = null;
        Builder callback = (_, _) => { };
        try
        {
            Marshal.Copy(entry, 0, memory, entry.Length);
            var request = new DetourRequest<Builder>
            {
                Name = "ExpandedHealerTargets copied-list-entry test",
                TargetAddress = unchecked((ulong)memory.ToInt64()),
                Callback = callback
            };
            probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<Builder>;
            Check(probe != null, "NativeX64 must create the runtime detour type.");
            Check(probe!.Scheme.ToString() == "Indirect", "Only audited Indirect is accepted.");
            Check(probe.DisplacedByteCount == 10, "Two complete five-byte instructions must be displaced.");
            Check(probe.TargetAddress == unchecked((ulong)memory.ToInt64()), "Target must be the copied entry.");
            Check(probe.PointerSlot != IntPtr.Zero && probe.HookEntryPointAddress != IntPtr.Zero,
                "Indirect must provide a pointer slot and hook entry.");
            Check(!probe.IsInstalled, "Probe must initially be unpublished.");
            probe.Enable();
            Check(probe.IsInstalled, "Probe must install on the copied buffer.");
            var patch = new byte[6];
            Marshal.Copy(memory, patch, 0, patch.Length);
            Check(patch[0] == 0xFF && patch[1] == 0x25, "Patch must start with FF 25.");
            Check(memory.ToInt64() + 6 + BitConverter.ToInt32(patch, 2) == probe.PointerSlot.ToInt64(),
                "Patch displacement must resolve to the RedBird pointer slot.");
            Check(Marshal.ReadInt64(probe.PointerSlot) == probe.HookEntryPointAddress.ToInt64(),
                "Pointer slot must lead to hook entry.");
            Console.WriteLine("NativeX64 copied-entry probe passed: Indirect/10, target, FF25, pointer slot, hook entry.");
        }
        finally
        {
            probe?.Dispose();
            byte[] restored = new byte[entry.Length];
            Marshal.Copy(memory, restored, 0, restored.Length);
            Check(restored.SequenceEqual(entry), "Probe must restore all copied entry bytes.");
            Marshal.FreeHGlobal(memory);
            GC.KeepAlive(callback);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
