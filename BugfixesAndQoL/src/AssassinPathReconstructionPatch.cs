// Feature: Let Vanilla reconstruct a validated Assassin climb route through a walkable reservation.
using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using RedBird.Core.Memory;

namespace BugfixesAndQoL
{
    internal sealed class AssassinPathReconstructionPatch
    {
        private static readonly byte[] RelaxedRejectJump =
            { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

        private readonly ManualLogSource log;
        private readonly PatchSite[] sites;
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
                AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPattern,
                AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPatternRva,
                referenceHashMatches,
                "Assassin path reconstruction endpoint-building guards",
                log);
            sites = new[]
            {
                CreateSite(
                    libraryHandle,
                    memory,
                    resolution.Rva,
                    AssassinPathReconstructionNativeDefinition.CurrentTileRejectJumpOffset,
                    AssassinPathReconstructionNativeDefinition.OriginalCurrentTileRejectJump,
                    "current route tile building guard"),
                CreateSite(
                    libraryHandle,
                    memory,
                    resolution.Rva,
                    AssassinPathReconstructionNativeDefinition.NeighborTileRejectJumpOffset,
                    AssassinPathReconstructionNativeDefinition.OriginalNeighborTileRejectJump,
                    "neighbor route tile building guard")
            };

            foreach (PatchSite site in sites)
                VerifyCurrentBytes(site, site.OriginalBytes, "initialize");
        }

        public bool IsApplied => applied;

        public void SetEnabled(bool enabled)
        {
            if (enabled == applied)
                return;

            byte[] expected = enabled ? null : RelaxedRejectJump;
            foreach (PatchSite site in sites)
                VerifyCurrentBytes(site, expected ?? site.OriginalBytes, enabled ? "apply" : "restore");

            int attemptedSite = -1;
            try
            {
                for (int index = 0; index < sites.Length; index++)
                {
                    attemptedSite = index;
                    WriteBytes(sites[index], enabled ? RelaxedRejectJump : sites[index].OriginalBytes);
                }
            }
            catch (Exception transitionException)
            {
                Exception rollbackException = RollBackAttemptedSites(
                    attemptedSite,
                    enabled ? RelaxedRejectJump : null,
                    enabled);
                if (rollbackException != null)
                {
                    throw new AggregateException(
                        "Assassin path reconstruction patch transition and rollback both failed.",
                        transitionException,
                        rollbackException);
                }

                throw;
            }

            applied = enabled;
            LogDebug(enabled ? "enabled" : "disabled");
        }

        private static PatchSite CreateSite(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            int patternRva,
            int jumpOffset,
            byte[] originalBytes,
            string label)
        {
            int patchRva = checked(patternRva + jumpOffset);
            if (patchRva < 0 || patchRva + originalBytes.Length > memory.Length ||
                !memory.Slice(patchRva, originalBytes.Length).SequenceEqual(originalBytes))
            {
                throw new InvalidOperationException(
                    $"Assassin path reconstruction {label} did not match the validated Vanilla bytes.");
            }

            return new PatchSite(label, IntPtr.Add(libraryHandle, patchRva), originalBytes);
        }

        private Exception RollBackAttemptedSites(
            int attemptedSite,
            byte[] commonTransitionBytes,
            bool enabling)
        {
            Exception firstFailure = null;
            for (int index = attemptedSite; index >= 0; index--)
            {
                PatchSite site = sites[index];
                byte[] transitionBytes = commonTransitionBytes ?? site.OriginalBytes;
                byte[] rollbackBytes = enabling ? site.OriginalBytes : RelaxedRejectJump;
                try
                {
                    byte[] current = ReadBytes(site.Address, transitionBytes.Length);
                    if (current.AsSpan().SequenceEqual(rollbackBytes))
                        continue;
                    if (!current.AsSpan().SequenceEqual(transitionBytes))
                    {
                        throw new InvalidOperationException(
                            $"Cannot roll back Assassin path reconstruction {site.Label} because its bytes changed.");
                    }
                    WriteBytes(site, rollbackBytes);
                }
                catch (Exception ex)
                {
                    if (firstFailure == null)
                        firstFailure = ex;
                }
            }
            return firstFailure;
        }

        private void VerifyCurrentBytes(PatchSite site, byte[] expected, string operation)
        {
            byte[] current = ReadBytes(site.Address, expected.Length);
            if (!current.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"Cannot {operation} Assassin path reconstruction {site.Label} because the native bytes changed: " +
                    $"expected={ToHex(expected)}, actual={ToHex(current)}.");
            }
        }

        private void WriteBytes(PatchSite site, byte[] bytes)
        {
            CodePatch.Write(unchecked((ulong)site.Address.ToInt64()), bytes);

            VerifyCurrentBytes(site, bytes, "verify");
        }

        private void LogDebug(string state)
        {
            log.LogDebug(
                $"[{TimestampNow()}] Bugfixes and QoL Assassin path reconstruction patch {state} " +
                $"at currentTileAddress=0x{sites[0].Address.ToInt64():X}, " +
                $"neighborTileAddress=0x{sites[1].Address.ToInt64():X}.");
        }

        private static byte[] ReadBytes(IntPtr address, int length)
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(address, bytes, 0, length);
            return bytes;
        }

        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace('-', ' ');

        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private sealed class PatchSite
        {
            public PatchSite(string label, IntPtr address, byte[] originalBytes)
            {
                Label = label;
                Address = address;
                OriginalBytes = originalBytes;
            }

            public string Label { get; }
            public IntPtr Address { get; }
            public byte[] OriginalBytes { get; }
        }
    }
}
