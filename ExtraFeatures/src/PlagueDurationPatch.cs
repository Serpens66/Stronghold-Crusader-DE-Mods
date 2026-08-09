// Feature: Scale the native lifetime of all plague-cloud projectiles.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Zhuqiaomon.Windows;

namespace ExtraFeatures
{
    internal sealed class PlagueDurationPatch : IDisposable
    {
        public const double MinimumMultiplier = 0.5;
        public const double MaximumMultiplier = 20.0;

        private const int VanillaLifetime = 800;
        private const int LifetimeImmediateOffset = 9;

        // Disease update signature at reference RVA 0x9A114; the lifetime opcode
        // starts at RVA 0x9A11C and its immediate at RVA 0x9A11D for CrusaderDE.dll SHA-256
        // 1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B.
        // The lifetime immediate is wildcarded so a moved function can still be found,
        // while the surrounding age comparison and fade transition remain validated.
        private const string LifetimePattern =
            "41 0F BF 44 18 18 03 D0 B8 ?? ?? ?? ?? 41 89 54 18 14 " +
            "66 41 39 84 18 D0 00 00 00 7C 06 66 45 89 4C 18 28 " +
            "41 0F B7 44 18 14 49 8D 0C 18 66 83 C0 10 66 41 89 44 18 34";

        private readonly IntPtr lifetimeAddress;
        private int expectedLifetime = VanillaLifetime;
        private bool disposed;

        public PlagueDurationPatch(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int matchOffset = FindUniquePattern(memory, LifetimePattern);
            int immediateOffset = checked(matchOffset + LifetimeImmediateOffset);
            if (immediateOffset < 0 || immediateOffset + sizeof(int) > memory.Length)
                throw new InvalidOperationException("The plague lifetime immediate lies outside the game module.");

            lifetimeAddress = IntPtr.Add(libraryHandle, immediateOffset);
            int currentLifetime = Marshal.ReadInt32(lifetimeAddress);
            if (currentLifetime != VanillaLifetime)
            {
                throw new InvalidOperationException(
                    $"The plague lifetime has an unexpected native value: expected={VanillaLifetime}, actual={currentLifetime}.");
            }
        }

        public void Apply(double multiplier, bool enabled)
        {
            ThrowIfDisposed();
            int desiredLifetime = enabled
                ? checked((int)Math.Round(VanillaLifetime * ClampMultiplier(multiplier), MidpointRounding.AwayFromZero))
                : VanillaLifetime;
            SetLifetime(desiredLifetime);
        }

        public void RestoreVanilla()
        {
            ThrowIfDisposed();
            SetLifetime(VanillaLifetime);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            RestoreVanilla();
            disposed = true;
        }

        private void SetLifetime(int desiredLifetime)
        {
            if (desiredLifetime == expectedLifetime)
                return;

            int currentLifetime = Marshal.ReadInt32(lifetimeAddress);
            if (currentLifetime != expectedLifetime)
            {
                throw new InvalidOperationException(
                    $"The plague lifetime bytes changed unexpectedly: expected={expectedLifetime}, actual={currentLifetime}.");
            }

            UIntPtr size = (UIntPtr)sizeof(int);
            if (!Kernel32.VirtualProtect(
                    lifetimeAddress,
                    size,
                    Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                throw new InvalidOperationException("VirtualProtect failed for the plague lifetime patch.");
            }

            bool valueWritten = false;
            try
            {
                Marshal.WriteInt32(lifetimeAddress, desiredLifetime);
                valueWritten = true;
            }
            finally
            {
                if (valueWritten)
                    expectedLifetime = desiredLifetime;

                if (!Kernel32.VirtualProtect(lifetimeAddress, size, oldProtection, out _))
                    throw new InvalidOperationException("Restoring memory protection failed for the plague lifetime patch.");
            }

            if (!MinWinAPI.FlushInstructionCache(Process.GetCurrentProcess().Handle, lifetimeAddress, size))
                throw new InvalidOperationException("Flushing the instruction cache failed for the plague lifetime patch.");

            int verifiedLifetime = Marshal.ReadInt32(lifetimeAddress);
            if (verifiedLifetime != desiredLifetime)
            {
                throw new InvalidOperationException(
                    $"The plague lifetime patch verification failed: expected={desiredLifetime}, actual={verifiedLifetime}.");
            }
        }

        private static int FindUniquePattern(ReadOnlySpan<byte> memory, string pattern)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int[] expected = new int[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
            {
                if (tokens[index] == "??")
                {
                    expected[index] = -1;
                    continue;
                }

                if (!byte.TryParse(tokens[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                    throw new InvalidOperationException($"Invalid plague lifetime AOB token '{tokens[index]}'.");
                expected[index] = value;
            }

            int matchOffset = -1;
            int matchCount = 0;
            for (int offset = 0; offset <= memory.Length - expected.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < expected.Length; index++)
                {
                    if (expected[index] >= 0 && memory[offset + index] != expected[index])
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                    continue;

                matchOffset = offset;
                matchCount++;
                if (matchCount > 1)
                    break;
            }

            if (matchCount != 1)
            {
                throw new InvalidOperationException(
                    $"The plague lifetime signature matched {matchCount} times instead of exactly once.");
            }

            return matchOffset;
        }

        private static double ClampMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;

            return Math.Max(MinimumMultiplier, Math.Min(MaximumMultiplier, value));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PlagueDurationPatch));
        }
    }
}
