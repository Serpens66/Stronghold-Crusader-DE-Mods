// Feature: Restore the portable shield's ordinary wall and tower pathing flag.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
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
        private const string UnitTowerClimbInitializationPattern =
            "41 0F B7 84 BE ?? ?? ?? ?? 66 89 83 B8 09 00 00";
        private const int UnitTowerClimbInitializationReferenceRva = 0x19A3EE;
        private const int UnitTowerClimbTableRvaOperandOffset = 0x5;
        private const int PortableShieldType = 60;
        private const int SiegeTowerType = 58;
        private const int BatteringRamType = 59;
        private const int BallistaType = 61;
        private const int OrdinaryClimbValue = 1;

        private readonly ManualLogSource log;
        private readonly IntPtr portableShieldEntry;
        private readonly IntPtr portableShieldTowerClimbEntry;
        private readonly int vanillaValue;
        private readonly int vanillaTowerClimbValue;
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

            Shared.NativeResolution towerClimbResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                UnitTowerClimbInitializationPattern,
                UnitTowerClimbInitializationReferenceRva,
                referenceHashMatches,
                "portable-shield tower-climb unit initialization",
                log);
            int towerClimbTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                checked(towerClimbResolution.Rva + UnitTowerClimbTableRvaOperandOffset));
            ValidateTowerClimbTable(memory, towerClimbTableRva);

            vanillaValue = ReadTableValue(memory, tableRva, PortableShieldType);
            vanillaTowerClimbValue = ReadTableValue(memory, towerClimbTableRva, PortableShieldType);
            portableShieldEntry = IntPtr.Add(
                libraryHandle,
                checked(tableRva + PortableShieldType * sizeof(int)));
            portableShieldTowerClimbEntry = IntPtr.Add(
                libraryHandle,
                checked(towerClimbTableRva + PortableShieldType * sizeof(int)));

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features portable-shield wall pathing resolved disabled: " +
                $"setDestinationRva=0x{resolution.Rva:X}, unitClimbTableRva=0x{tableRva:X}, " +
                $"towerClimbInitializationRva=0x{towerClimbResolution.Rva:X}, " +
                $"towerClimbTableRva=0x{towerClimbTableRva:X}, unitType={PortableShieldType}, " +
                $"vanillaValues={vanillaValue}/{vanillaTowerClimbValue}.");
        }

        public void SetEnabled(bool value)
        {
            ThrowIfDisposed();
            if (enabled == value)
            {
                if (value && ownsOverride)
                    RefreshExistingPortableShields(OrdinaryClimbValue);
                return;
            }

            int currentValue = Marshal.ReadInt32(portableShieldEntry);
            int currentTowerClimbValue = Marshal.ReadInt32(portableShieldTowerClimbEntry);
            if (value)
            {
                if (currentValue == vanillaValue && currentTowerClimbValue == vanillaTowerClimbValue)
                {
                    Marshal.WriteInt32(portableShieldEntry, OrdinaryClimbValue);
                    Marshal.WriteInt32(portableShieldTowerClimbEntry, OrdinaryClimbValue);
                    ownsOverride = true;
                    RefreshExistingPortableShields(OrdinaryClimbValue);
                }
                else if (currentValue == OrdinaryClimbValue && currentTowerClimbValue == OrdinaryClimbValue)
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
                        $"Portable-shield climb values changed unexpectedly from " +
                        $"{vanillaValue}/{vanillaTowerClimbValue} to {currentValue}/{currentTowerClimbValue}.");
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
                $"nativeValues={Marshal.ReadInt32(portableShieldEntry)}/" +
                $"{Marshal.ReadInt32(portableShieldTowerClimbEntry)}, ownsOverride={ownsOverride}.");
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
            int currentTowerClimbValue = Marshal.ReadInt32(portableShieldTowerClimbEntry);
            if (currentValue == OrdinaryClimbValue && currentTowerClimbValue == OrdinaryClimbValue)
            {
                Marshal.WriteInt32(portableShieldEntry, vanillaValue);
                Marshal.WriteInt32(portableShieldTowerClimbEntry, vanillaTowerClimbValue);
                RefreshExistingPortableShields(vanillaTowerClimbValue);
            }
            else if (currentValue != vanillaValue || currentTowerClimbValue != vanillaTowerClimbValue)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extra Features did not restore portable-shield wall pathing because its native values changed unexpectedly " +
                    $"to {currentValue}/{currentTowerClimbValue}.");
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

        private static void ValidateTowerClimbTable(ReadOnlySpan<byte> memory, int tableRva)
        {
            int requiredEnd = checked(tableRva + (PortableShieldType + 1) * sizeof(int));
            if (tableRva <= 0 || requiredEnd > memory.Length)
                throw new InvalidOperationException("The resolved tower-climb table lies outside the native image.");

            // These alternating light/heavy troop values distinguish this table from DAT_UNIT_CLIMB.
            AssertTableValue(memory, tableRva, 22, 1, "archer tower climb");
            AssertTableValue(memory, tableRva, 23, 0, "crossbowman tower climb");
            AssertTableValue(memory, tableRva, 24, 1, "spearman tower climb");
            AssertTableValue(memory, tableRva, 25, 0, "pikeman tower climb");
            AssertTableValue(memory, tableRva, PortableShieldType, 0, "portable-shield tower climb");
        }

        private void RefreshExistingPortableShields(int climbValue)
        {
            int updated = 0;
            try
            {
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                for (int index = 0; index < units.Length; index++)
                {
                    ref GameUnit unit = ref units[index];
                    if ((unit.r_AliveState != AliveState.IsAlive && unit.r_AliveState != AliveState.NeedsInit) ||
                        unit.r_UnitChimp != (eChimps)PortableShieldType ||
                        unit.N000001CA == climbValue)
                    {
                        continue;
                    }

                    unit.N000001CA = checked((ushort)climbValue);
                    updated++;
                }

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features refreshed the cached wall/tower permission of {updated} existing portable shield(s) to {climbValue}.");
            }
            catch (Exception ex)
            {
                // The table still initializes future units correctly when no map/unit manager is active yet.
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extra Features could not refresh existing portable shields at this lifecycle point: {ex.Message}");
            }
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
