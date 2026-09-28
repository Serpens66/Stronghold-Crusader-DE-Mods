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

            byte[] entry = new byte[64];
            Array.Copy(image, 0x181340, entry, 0, entry.Length);
            byte[] expected = { 0x48, 0x89, 0x5C, 0x24, 0x10,
                                0x48, 0x89, 0x6C, 0x24, 0x20 };
            Check(entry.Take(expected.Length).SequenceEqual(expected), "Audited healer entry bytes.");
            IntPtr copy = Marshal.AllocHGlobal(entry.Length);
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
                Marshal.FreeHGlobal(copy);
                GC.KeepAlive(callback);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
