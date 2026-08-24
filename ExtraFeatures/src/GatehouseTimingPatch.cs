// Feature: Configure Vanilla gatehouse enemy distances and reopening delays.
using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zhuqiaomon.Windows;

namespace ExtraFeatures
{
    internal sealed class GatehouseTimingPatch : IDisposable
    {
        public const double MinimumHumanDelaySeconds = 0.0;
        public const double MaximumHumanDelaySeconds = 30.0;
        public const double MinimumAiDelaySeconds = 0.0;
        public const double MaximumAiDelaySeconds = 120.0;
        public const double MinimumDistanceTiles = 5.0;
        public const double MaximumDistanceTiles = 50.0;
        public const double VanillaHumanDelaySeconds = 2.5;
        public const double VanillaAiDelaySeconds = 30.0;
        public const double VanillaHumanDistanceTiles = 17.5;
        public const double VanillaAiDistanceTiles = 25.0;
        public const int TicksPerReferenceSecond = 40;
        public const int NativeUnitsPerTile = 8;

        private const int VanillaAiDistance = 200;
        private const int VanillaAiDelay = 1200;
        private const int VanillaHumanDistance = 140;
        private const int VanillaHumanDelay = 100;
        private const int DistanceBlockRva = 0xB7BBB;
        private const int HumanDelayPatternRva = 0xB7C32;
        private const int AiDistanceOffset = 8;
        private const int AiDelayOffset = 15;
        private const int HumanDistanceOffset = 24;
        private const int HumanDelayOffset = 3;

        private const string DistanceBlockPattern =
            "40 84 F6 75 10 41 81 F8 ?? ?? ?? ?? 7D 10 B8 ?? ?? ?? ?? EB 69 " +
            "41 81 F8 ?? ?? ?? ?? 7C 5B 48 8D 2D ?? ?? ?? ?? 49 FF C6 49 83 C3 02";
        private const string HumanDelayPattern =
            "EB 50 B8 ?? ?? ?? ?? 48 8D 2D ?? ?? ?? ?? 66 89 84 2B ?? ?? ?? ?? " +
            "80 BC 2B ?? ?? ?? ?? 00";

        private readonly IntPtr aiDistanceAddress;
        private readonly IntPtr aiDelayAddress;
        private readonly IntPtr humanDistanceAddress;
        private readonly IntPtr humanDelayAddress;
        private int expectedAiDistance = VanillaAiDistance;
        private int expectedAiDelay = VanillaAiDelay;
        private int expectedHumanDistance = VanillaHumanDistance;
        private int expectedHumanDelay = VanillaHumanDelay;
        private bool disposed;

        public GatehouseTimingPatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int distanceBlock = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DistanceBlockPattern,
                DistanceBlockRva,
                referenceHashMatches,
                "gatehouse distance/delay decision block",
                log).Rva;
            int humanDelayPattern = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HumanDelayPattern,
                HumanDelayPatternRva,
                referenceHashMatches,
                "gatehouse human reopening delay",
                log).Rva;
            if (humanDelayPattern <= distanceBlock || humanDelayPattern - distanceBlock > 0x100)
                throw new InvalidOperationException("The gatehouse native value blocks are not in the same decision region.");

            aiDistanceAddress = ResolveImmediate(libraryHandle, memory, distanceBlock + AiDistanceOffset, VanillaAiDistance, "AI distance");
            aiDelayAddress = ResolveImmediate(libraryHandle, memory, distanceBlock + AiDelayOffset, VanillaAiDelay, "AI delay");
            humanDistanceAddress = ResolveImmediate(libraryHandle, memory, distanceBlock + HumanDistanceOffset, VanillaHumanDistance, "human distance");
            humanDelayAddress = ResolveImmediate(libraryHandle, memory, humanDelayPattern + HumanDelayOffset, VanillaHumanDelay, "human delay");

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features gatehouse native values initialized: " +
                $"aiDistanceRva=0x{distanceBlock + AiDistanceOffset:X}, aiDelayRva=0x{distanceBlock + AiDelayOffset:X}, " +
                $"humanDistanceRva=0x{distanceBlock + HumanDistanceOffset:X}, humanDelayRva=0x{humanDelayPattern + HumanDelayOffset:X}, " +
                $"referenceTicksPerSecond={TicksPerReferenceSecond}, nativeUnitsPerTile={NativeUnitsPerTile}.");
        }

        public void Apply(
            double humanDelaySeconds,
            double aiDelaySeconds,
            double humanDistanceTiles,
            double aiDistanceTiles,
            bool enabled)
        {
            ThrowIfDisposed();
            int desiredHumanDelay = enabled ? SecondsToTicks(humanDelaySeconds) : VanillaHumanDelay;
            int desiredAiDelay = enabled ? SecondsToTicks(aiDelaySeconds) : VanillaAiDelay;
            int desiredHumanDistance = enabled ? TilesToNativeUnits(humanDistanceTiles) : VanillaHumanDistance;
            int desiredAiDistance = enabled ? TilesToNativeUnits(aiDistanceTiles) : VanillaAiDistance;
            SetValues(desiredAiDistance, desiredAiDelay, desiredHumanDistance, desiredHumanDelay);
        }

        public void RestoreVanilla()
        {
            ThrowIfDisposed();
            SetValues(VanillaAiDistance, VanillaAiDelay, VanillaHumanDistance, VanillaHumanDelay);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            RestoreVanilla();
            disposed = true;
        }

        public static int SecondsToTicks(double seconds) =>
            checked((int)Math.Round(seconds * TicksPerReferenceSecond, MidpointRounding.AwayFromZero));

        public static int TilesToNativeUnits(double tiles) =>
            checked((int)Math.Round(tiles * NativeUnitsPerTile, MidpointRounding.AwayFromZero));

        private void SetValues(int aiDistance, int aiDelay, int humanDistance, int humanDelay)
        {
            if (aiDistance == expectedAiDistance && aiDelay == expectedAiDelay &&
                humanDistance == expectedHumanDistance && humanDelay == expectedHumanDelay)
            {
                return;
            }

            VerifyExpectedValues();
            long first = Math.Min(Math.Min(aiDistanceAddress.ToInt64(), aiDelayAddress.ToInt64()),
                Math.Min(humanDistanceAddress.ToInt64(), humanDelayAddress.ToInt64()));
            long last = Math.Max(Math.Max(aiDistanceAddress.ToInt64(), aiDelayAddress.ToInt64()),
                Math.Max(humanDistanceAddress.ToInt64(), humanDelayAddress.ToInt64()));
            IntPtr region = new IntPtr(first);
            UIntPtr size = (UIntPtr)checked((ulong)(last - first + sizeof(int)));
            if (!Kernel32.VirtualProtect(region, size, Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                throw new InvalidOperationException("VirtualProtect failed for the gatehouse value patch.");
            }

            int oldAiDistance = expectedAiDistance;
            int oldAiDelay = expectedAiDelay;
            int oldHumanDistance = expectedHumanDistance;
            int oldHumanDelay = expectedHumanDelay;
            bool written = false;
            try
            {
                Marshal.WriteInt32(aiDistanceAddress, aiDistance);
                Marshal.WriteInt32(aiDelayAddress, aiDelay);
                Marshal.WriteInt32(humanDistanceAddress, humanDistance);
                Marshal.WriteInt32(humanDelayAddress, humanDelay);
                VerifyValue(aiDistanceAddress, aiDistance, "AI distance");
                VerifyValue(aiDelayAddress, aiDelay, "AI delay");
                VerifyValue(humanDistanceAddress, humanDistance, "human distance");
                VerifyValue(humanDelayAddress, humanDelay, "human delay");
                written = true;
            }
            catch
            {
                // Keep the four related immediates transactional if a write or verification fails.
                Marshal.WriteInt32(aiDistanceAddress, oldAiDistance);
                Marshal.WriteInt32(aiDelayAddress, oldAiDelay);
                Marshal.WriteInt32(humanDistanceAddress, oldHumanDistance);
                Marshal.WriteInt32(humanDelayAddress, oldHumanDelay);
                throw;
            }
            finally
            {
                if (!Kernel32.VirtualProtect(region, size, oldProtection, out _))
                    throw new InvalidOperationException("Restoring memory protection failed for the gatehouse value patch.");
            }

            if (written)
            {
                expectedAiDistance = aiDistance;
                expectedAiDelay = aiDelay;
                expectedHumanDistance = humanDistance;
                expectedHumanDelay = humanDelay;
            }

            if (!MinWinAPI.FlushInstructionCache(Process.GetCurrentProcess().Handle, region, size))
                throw new InvalidOperationException("Flushing the instruction cache failed for the gatehouse value patch.");
            VerifyExpectedValues();
        }

        private void VerifyExpectedValues()
        {
            VerifyValue(aiDistanceAddress, expectedAiDistance, "AI distance");
            VerifyValue(aiDelayAddress, expectedAiDelay, "AI delay");
            VerifyValue(humanDistanceAddress, expectedHumanDistance, "human distance");
            VerifyValue(humanDelayAddress, expectedHumanDelay, "human delay");
        }

        private static IntPtr ResolveImmediate(IntPtr libraryHandle, ReadOnlySpan<byte> memory, int rva, int expected, string name)
        {
            if (rva < 0 || rva + sizeof(int) > memory.Length)
                throw new InvalidOperationException($"The gatehouse {name} immediate lies outside the game module.");
            IntPtr address = IntPtr.Add(libraryHandle, rva);
            VerifyValue(address, expected, name);
            return address;
        }

        private static void VerifyValue(IntPtr address, int expected, string name)
        {
            int actual = Marshal.ReadInt32(address);
            if (actual != expected)
                throw new InvalidOperationException($"The gatehouse {name} changed unexpectedly: expected={expected}, actual={actual}.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GatehouseTimingPatch));
        }
    }
}
