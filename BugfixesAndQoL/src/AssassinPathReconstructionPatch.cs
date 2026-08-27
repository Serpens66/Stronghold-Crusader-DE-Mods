// Feature: Let Vanilla reconstruct a validated Assassin climb route through a walkable reservation.
using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Zhuqiaomon.Windows;

namespace BugfixesAndQoL
{
    internal sealed class AssassinPathReconstructionPatch
    {
        private const int NeighborBuildingGuardPatternRva = 0xE19F0;
        private const int RejectJumpOffset = 9;
        private const string NeighborBuildingGuardPattern =
            "66 45 39 9C 4F 50 AA B6 04 0F 85 88 00 00 00 45 85 C9 75 1B 41 F7 84 8F B0 71 8F 04 00 01 00 00 74 75";

        private static readonly byte[] OriginalRejectJump =
            { 0x0F, 0x85, 0x88, 0x00, 0x00, 0x00 };
        private static readonly byte[] RelaxedRejectJump =
            { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

        private readonly ManualLogSource log;
        private readonly IntPtr address;
        private bool applied;

        public AssassinPathReconstructionPatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero)
                throw new ArgumentException("native library handle is null", nameof(libraryHandle));

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                NeighborBuildingGuardPattern,
                NeighborBuildingGuardPatternRva,
                referenceHashMatches,
                "Assassin path reconstruction neighbor-building guard",
                log);
            int patchRva = checked(resolution.Rva + RejectJumpOffset);
            if (patchRva < 0 || patchRva + OriginalRejectJump.Length > memory.Length ||
                !memory.Slice(patchRva, OriginalRejectJump.Length).SequenceEqual(OriginalRejectJump))
            {
                throw new InvalidOperationException(
                    "Assassin path reconstruction reject jump did not match the validated Vanilla bytes.");
            }

            address = IntPtr.Add(libraryHandle, patchRva);
            VerifyCurrentBytes(OriginalRejectJump, "initialize");
        }

        public bool IsApplied => applied;

        public void SetEnabled(bool enabled)
        {
            if (enabled == applied)
                return;

            if (enabled)
            {
                VerifyCurrentBytes(OriginalRejectJump, "apply");
                // Mode 3 reconstructs the managed route backwards. Its prior zero-building
                // guard makes this equality reject the reserved predecessor of a wall tile.
                WriteBytes(RelaxedRejectJump);
                applied = true;
                LogDebug("enabled");
                return;
            }

            VerifyCurrentBytes(RelaxedRejectJump, "restore");
            WriteBytes(OriginalRejectJump);
            applied = false;
            LogDebug("disabled");
        }

        private void VerifyCurrentBytes(byte[] expected, string operation)
        {
            byte[] current = new byte[expected.Length];
            Marshal.Copy(address, current, 0, current.Length);
            if (!current.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"Cannot {operation} Assassin path reconstruction patch because the native bytes changed: " +
                    $"expected={ToHex(expected)}, actual={ToHex(current)}.");
            }
        }

        private void WriteBytes(byte[] bytes)
        {
            UIntPtr size = unchecked((UIntPtr)(uint)bytes.Length);
            if (!Kernel32.VirtualProtect(
                    address,
                    size,
                    Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                throw new InvalidOperationException(
                    "VirtualProtect failed for the Assassin path reconstruction patch.");
            }

            try
            {
                Marshal.Copy(bytes, 0, address, bytes.Length);
            }
            finally
            {
                if (!Kernel32.VirtualProtect(address, size, oldProtection, out _))
                {
                    throw new InvalidOperationException(
                        "Restoring memory protection failed for the Assassin path reconstruction patch.");
                }
            }

            if (!MinWinAPI.FlushInstructionCache(
                    Process.GetCurrentProcess().Handle,
                    address,
                    size))
            {
                throw new InvalidOperationException(
                    "Flushing the instruction cache failed for the Assassin path reconstruction patch.");
            }

            VerifyCurrentBytes(bytes, "verify");
        }

        private void LogDebug(string state)
        {
            log.LogDebug(
                $"[{TimestampNow()}] Bugfixes and QoL Assassin path reconstruction patch {state} " +
                $"at address=0x{address.ToInt64():X}.");
        }

        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace('-', ' ');

        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
