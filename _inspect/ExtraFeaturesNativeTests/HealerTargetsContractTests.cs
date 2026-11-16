using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal static class HealerTargetsContractTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void Builder(IntPtr manager, int playerId);

        internal static void Run(byte[] image, string workspace)
        {
            Check(!HealerListBounds.IsUsableNextUnitId(0) &&
                  HealerListBounds.IsUsableNextUnitId(1) &&
                  HealerListBounds.IsUsableNextUnitId(10000) &&
                  !HealerListBounds.IsUsableNextUnitId(10001),
                "Transient unit-manager bounds must not disable a later valid list.");
            Check(!HealerListBounds.IsUsableListCount(-1) &&
                  HealerListBounds.IsUsableListCount(0) &&
                  HealerListBounds.IsUsableListCount(9999) &&
                  !HealerListBounds.IsUsableListCount(10000),
                "Native candidate-list capacity must be respected.");

            string source = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "src", "HealerTargetsRuntime.cs"));
            Check(source.Contains("listBuilder.Original(manager, playerId);") &&
                  source.Contains("Volatile.Write(ref callbackDisabled, 1);") &&
                  source.Contains("HealerListBounds.IsUsableNextUnitId(nextUnitId)") &&
                  source.Contains("HealerListBounds.IsUsableListCount(currentCount)"),
                "Vanilla forwarding, callback fail-open and transient bounds guards remain.");

            byte[] entrySource = new byte[Shared.CopiedProbeImage.SourceLength];
            Array.Copy(image, 0x181340, entrySource, 0, entrySource.Length);
            byte[] expected = { 0x48, 0x89, 0x5C, 0x24, 0x10,
                                0x48, 0x89, 0x6C, 0x24, 0x20 };
            Check(entrySource.Take(expected.Length).SequenceEqual(expected), "Audited healer entry bytes.");
            // Production probe image: RedBird may decode the whole FunctionScanLimit window. The image
            // ends directly in front of a PAGE_NOACCESS page, so any read beyond it faults here.
            byte[] entry = Shared.CopiedProbeImage.Build(entrySource, NativeDetourBackend.Instance.Options.FunctionScanLimit);
            const int pageSize = 4096;
            int pages = (entry.Length + pageSize - 1) / pageSize;
            IntPtr region = VirtualAlloc(IntPtr.Zero, (UIntPtr)((pages + 1) * pageSize), 0x3000, 0x04);
            Check(region != IntPtr.Zero && VirtualProtect(region + pages * pageSize, (UIntPtr)pageSize, 0x01, out _),
                "Guarded healer probe region could not be prepared.");
            IntPtr copy = region + pages * pageSize - entry.Length;
            NativeDetour<Builder> probe = null;
            Builder callback = (_, __) => { };
            try
            {
                Marshal.Copy(entry, 0, copy, entry.Length);
                var request = new DetourRequest<Builder>
                {
                    Name = "ExtraFeatures healer copied-entry test",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = callback
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<Builder>;
                Check(probe != null && probe.Scheme.ToString() == "Indirect" &&
                      probe.DisplacedByteCount == 10 && !probe.IsInstalled &&
                      probe.TargetAddress == unchecked((ulong)copy.ToInt64()) &&
                      probe.PointerSlot != IntPtr.Zero && probe.HookEntryPointAddress != IntPtr.Zero,
                    "Installed NativeX64 backend must produce the audited Indirect/10 detour.");
                probe.Enable();
                byte[] patch = new byte[6];
                Marshal.Copy(copy, patch, 0, patch.Length);
                Check(probe.IsInstalled && patch[0] == 0xFF && patch[1] == 0x25 &&
                      copy.ToInt64() + 6 + BitConverter.ToInt32(patch, 2) == probe.PointerSlot.ToInt64() &&
                      Marshal.ReadInt64(probe.PointerSlot) == probe.HookEntryPointAddress.ToInt64(),
                    "Installed FF25 jump must lead through the pointer slot to the hook entry.");
            }
            finally
            {
                probe?.Dispose();
                byte[] restored = new byte[entry.Length];
                Marshal.Copy(copy, restored, 0, restored.Length);
                Check(restored.SequenceEqual(entry), "Copied backend probe restores all original bytes.");
                VirtualFree(region, UIntPtr.Zero, 0x8000);
                GC.KeepAlive(callback);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
