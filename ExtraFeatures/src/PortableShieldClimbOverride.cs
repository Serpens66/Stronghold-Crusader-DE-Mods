// Feature: Restore the portable shield's ordinary wall and tower pathing flag.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

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
        private const string CanAUnitClimbPattern =
            "44 8B 15 ?? ?? ?? ?? 4C 8B C1 C7 05 ?? ?? ?? ?? 01 00 00 00 " +
            "83 39 01 7E ?? BA 01 00 00 00 8B C2 4C 63 C8 48 63 C2 48 69 C8 90 04 00 00 " +
            "66 42 83 BC 01 E4 06 00 00 02 75 34 49 69 C9 90 04 00 00 49 03 C8 " +
            "66 83 B9 F8 08 00 00 00 75 20 66 83 B9 8C 06 00 00 00 74 16 " +
            "0F BF 81 EE 06 00 00 41 3B C2 75 19 66 83 B9 B8 09 00 00 00";
        private const int CanAUnitClimbReferenceRva = 0x18DC40;
        private static readonly long DiagnosticIntervalTicks = Math.Max(1L, Stopwatch.Frequency * 2L);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CanAUnitClimbDelegate(IntPtr unitManager);

        private readonly ManualLogSource log;
        private readonly IntPtr portableShieldEntry;
        private readonly IntPtr portableShieldTowerClimbEntry;
        private readonly int vanillaValue;
        private readonly int vanillaTowerClimbValue;
        private HookTransaction hookTransaction;
        private HookRef<X64ManagedFunctionDetourAOB<CanAUnitClimbDelegate>> canAUnitClimbHook =
            new HookRef<X64ManagedFunctionDetourAOB<CanAUnitClimbDelegate>>();
        private long nextDiagnosticTimestamp;
        private int callbackFailureLogged;
        private volatile bool enabled;
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

            Shared.NativeResolution canClimbResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CanAUnitClimbPattern,
                CanAUnitClimbReferenceRva,
                referenceHashMatches,
                "portable-shield selection climb validator",
                log);
            ValidateManagedUnitLayout();
            InstallCanAUnitClimbHook(libraryHandle, memory, canClimbResolution);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features portable-shield wall pathing resolved disabled: " +
                $"setDestinationRva=0x{resolution.Rva:X}, unitClimbTableRva=0x{tableRva:X}, " +
                $"towerClimbInitializationRva=0x{towerClimbResolution.Rva:X}, " +
                $"towerClimbTableRva=0x{towerClimbTableRva:X}, unitType={PortableShieldType}, " +
                $"canAUnitClimbRva=0x{canClimbResolution.Rva:X}, " +
                $"vanillaValues={vanillaValue}/{vanillaTowerClimbValue}.");
        }

        public void SetEnabled(bool value)
        {
            ThrowIfDisposed();
            if (enabled == value)
                return;

            int currentValue = Marshal.ReadInt32(portableShieldEntry);
            int currentTowerClimbValue = Marshal.ReadInt32(portableShieldTowerClimbEntry);
            if (value)
            {
                if (currentValue == vanillaValue && currentTowerClimbValue == vanillaTowerClimbValue)
                {
                    Marshal.WriteInt32(portableShieldEntry, OrdinaryClimbValue);
                    Marshal.WriteInt32(portableShieldTowerClimbEntry, OrdinaryClimbValue);
                    ownsOverride = true;
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
            hookTransaction?.Unload();
            hookTransaction?.Dispose();
            hookTransaction = null;
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

        private void InstallCanAUnitClimbHook(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            Shared.NativeResolution resolution)
        {
            HookTransaction pending = null;
            try
            {
                ulong imageBase = unchecked((ulong)libraryHandle.ToInt64());
                pending = new HookTransaction(
                    memory,
                    imageBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                pending.AddDetour(
                    ref canAUnitClimbHook,
                    imageBase + unchecked((ulong)resolution.Rva),
                    CanSelectedUnitClimb);
                pending.Commit();
                if (!canAUnitClimbHook.Success)
                    throw new InvalidOperationException("The portable-shield selection validator detour was not installed.");

                hookTransaction = pending;
                pending = null;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Extra Features portable-shield selection climb validator installed at RVA 0x{resolution.Rva:X}; disabled settings pass Vanilla through.");
            }
            catch
            {
                if (pending != null)
                {
                    try { pending.Unload(); } catch { }
                    try { pending.Dispose(); } catch { }
                }
                throw;
            }
        }

        private int CanSelectedUnitClimb(IntPtr unitManager)
        {
            int vanillaResult = canAUnitClimbHook.Value.Hook.Trampoline(unitManager);
            if (!enabled)
                return vanillaResult;

            try
            {
                PortableShieldSelectionSnapshot snapshot = CaptureSelection();
                bool overrideVanilla = PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(
                    true,
                    vanillaResult,
                    snapshot.OwnMovableShieldCount,
                    snapshot.OwnOtherCount,
                    snapshot.ForeignCount,
                    snapshot.NonMovableShieldCount);
                LogSelectionDiagnosticIfDue(snapshot, vanillaResult, overrideVanilla);
                return overrideVanilla ? 1 : vanillaResult;
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref callbackFailureLogged, 1) == 0)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Extra Features portable-shield selection validator callback failed; Vanilla is used: {ex}");
                }
                return vanillaResult;
            }
        }

        private PortableShieldSelectionSnapshot CaptureSelection()
        {
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            var shieldIds = new List<int>();
            int ownOtherCount = 0;
            int foreignCount = 0;
            int nonMovableShieldCount = 0;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int unitId = 1; unitId < units.Length; unitId++)
            {
                ref GameUnit unit = ref units[unitId];
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_UnitSelected == 0)
                    continue;

                bool ownUnit = localPlayerId >= 1 && localPlayerId <= 8 &&
                    unit.r_ControllableForPlayerId == localPlayerId;
                if (!ownUnit)
                {
                    foreignCount++;
                    continue;
                }

                if (unit.r_UnitChimp != (eChimps)PortableShieldType)
                {
                    ownOtherCount++;
                    continue;
                }

                // This is the same eligibility field checked by Vanilla canAUnitClimb.
                if (unit.N0000019A != 0)
                {
                    nonMovableShieldCount++;
                    continue;
                }

                shieldIds.Add(unitId);
            }

            return new PortableShieldSelectionSnapshot(
                shieldIds,
                ownOtherCount,
                foreignCount,
                nonMovableShieldCount);
        }

        private void LogSelectionDiagnosticIfDue(
            PortableShieldSelectionSnapshot snapshot,
            int vanillaResult,
            bool overrideVanilla)
        {
            if (snapshot.OwnMovableShieldCount == 0 && snapshot.NonMovableShieldCount == 0)
                return;

            long now = Stopwatch.GetTimestamp();
            if (now < nextDiagnosticTimestamp)
                return;

            nextDiagnosticTimestamp = now + DiagnosticIntervalTicks;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Extra Features portable-shield climb selection: shieldIds=[{string.Join(",", snapshot.ShieldIds)}], " +
                $"ownOther={snapshot.OwnOtherCount}, foreign={snapshot.ForeignCount}, " +
                $"nonMovableShields={snapshot.NonMovableShieldCount}, vanillaResult={vanillaResult}, " +
                $"override={overrideVanilla}, nativeValues={Marshal.ReadInt32(portableShieldEntry)}/" +
                $"{Marshal.ReadInt32(portableShieldTowerClimbEntry)}.");
        }

        private sealed class PortableShieldSelectionSnapshot
        {
            public PortableShieldSelectionSnapshot(
                List<int> shieldIds,
                int ownOtherCount,
                int foreignCount,
                int nonMovableShieldCount)
            {
                ShieldIds = shieldIds;
                OwnOtherCount = ownOtherCount;
                ForeignCount = foreignCount;
                NonMovableShieldCount = nonMovableShieldCount;
            }

            public List<int> ShieldIds { get; }
            public int OwnMovableShieldCount => ShieldIds.Count;
            public int OwnOtherCount { get; }
            public int ForeignCount { get; }
            public int NonMovableShieldCount { get; }
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

        private static void ValidateManagedUnitLayout()
        {
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_UnitSelected)).ToInt32() != 0x30 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AliveState)).ToInt32() != 0x88 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_UnitChimp)).ToInt32() != 0x8A ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N0000019A)).ToInt32() != 0x29C ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N000001CA)).ToInt32() != 0x35C)
            {
                throw new InvalidOperationException(
                    "The managed GameUnit layout does not match the audited portable-shield selection validator.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PortableShieldClimbOverride));
        }
    }
}
