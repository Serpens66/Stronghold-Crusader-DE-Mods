// Feature: Restore the portable shield's ordinary wall and tower pathing flag.
using BepInEx.Logging;
using System;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal sealed class PortableShieldClimbOverride : IDisposable
    {
        private const string SetDestinationPattern =
            "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC ?? 48 63 F2 45 33 D2 48 69 FE 90 04 00 00 " +
            "4D 63 F0 48 8D 15 ?? ?? ?? ?? 48 03 F9 49 63 E9 4C 8B F9 " +
            "48 0F BF 87 E6 06 00 00 8B 84 82 ?? ?? ?? ?? " +
            "89 84 24 80 00 00 00";
        private const int SetDestinationReferenceRva = 0x196280;
        private const int UnitClimbTableRvaOperandOffset = 0x3F;
        private const int PortableShieldType = 60;
        private const int SiegeTowerType = 58;
        private const int BatteringRamType = 59;
        private const int BallistaType = 61;
        private const int OrdinaryClimbValue = 1;

        private readonly ManualLogSource log;
        private readonly IntPtr portableShieldEntry;
        private readonly int vanillaValue;
        private bool enabled;
        private bool ownsOverride;
        private bool disposed;

        public PortableShieldClimbOverride(
            ManualLogSource log,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.");

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                SetDestinationPattern,
                SetDestinationReferenceRva,
                referenceHashMatches,
                "portable-shield setDestinationForUnit table lookup",
                log);

            int tableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                checked(resolution.Rva + UnitClimbTableRvaOperandOffset));
            ValidateTable(memory, tableRva);

            vanillaValue = ReadTableValue(memory, tableRva, PortableShieldType);
            portableShieldEntry = IntPtr.Add(
                libraryHandle,
                checked(tableRva + PortableShieldType * sizeof(int)));

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features portable-shield wall pathing resolved disabled: " +
                $"setDestinationRva=0x{resolution.Rva:X}, unitClimbTableRva=0x{tableRva:X}, " +
                $"unitType={PortableShieldType}, vanillaValue={vanillaValue}.");
        }

        public void SetEnabled(bool value)
        {
            ThrowIfDisposed();
            if (enabled == value)
                return;

            int currentValue = Marshal.ReadInt32(portableShieldEntry);
            if (value)
            {
                if (currentValue == vanillaValue)
                {
                    Marshal.WriteInt32(portableShieldEntry, OrdinaryClimbValue);
                    ownsOverride = true;
                }
                else if (currentValue == OrdinaryClimbValue)
                {
                    // Do not claim or later undo an identical override installed by another mod.
                    ownsOverride = false;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Extra Features found portable-shield wall pathing already enabled by another component; no native value was overwritten.");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Portable-shield climb value changed unexpectedly from {vanillaValue} to {currentValue}.");
                }
            }
            else
            {
                RestoreVanillaValue();
            }

            enabled = value;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Extra Features portable shields on walls is now {(value ? "enabled" : "disabled")}; " +
                $"nativeValue={Marshal.ReadInt32(portableShieldEntry)}, ownsOverride={ownsOverride}.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            RestoreVanillaValue();
            enabled = false;
            disposed = true;
        }

        private void RestoreVanillaValue()
        {
            if (!ownsOverride)
                return;

            int currentValue = Marshal.ReadInt32(portableShieldEntry);
            if (currentValue == OrdinaryClimbValue)
            {
                Marshal.WriteInt32(portableShieldEntry, vanillaValue);
            }
            else if (currentValue != vanillaValue)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extra Features did not restore portable-shield wall pathing because its native value changed unexpectedly to {currentValue}.");
            }

            ownsOverride = false;
        }

        private static void ValidateTable(ReadOnlySpan<byte> memory, int tableRva)
        {
            int lastRequiredEntry = Math.Max(PortableShieldType, BallistaType);
            int requiredEnd = checked(tableRva + (lastRequiredEntry + 1) * sizeof(int));
            if (tableRva <= 0 || requiredEnd > memory.Length)
                throw new InvalidOperationException("The resolved DAT_UNIT_CLIMB table lies outside the native image.");

            AssertTableValue(memory, tableRva, SiegeTowerType, 0, "siege tower");
            AssertTableValue(memory, tableRva, BatteringRamType, 0, "battering ram");
            AssertTableValue(memory, tableRva, PortableShieldType, 0, "portable shield");
            AssertTableValue(memory, tableRva, BallistaType, OrdinaryClimbValue, "ballista");
        }

        private static void AssertTableValue(
            ReadOnlySpan<byte> memory,
            int tableRva,
            int unitType,
            int expected,
            string name)
        {
            int actual = ReadTableValue(memory, tableRva, unitType);
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    $"Resolved DAT_UNIT_CLIMB failed semantic validation for {name}: expected {expected}, found {actual}.");
            }
        }

        private static int ReadTableValue(ReadOnlySpan<byte> memory, int tableRva, int unitType) =>
            Shared.NativePatternResolver.ReadInt32(
                memory,
                checked(tableRva + unitType * sizeof(int)));

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PortableShieldClimbOverride));
        }
    }
}
