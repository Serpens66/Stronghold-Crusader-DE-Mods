using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Windows;

namespace ImprovedHunters
{
    /// <summary>
    /// Routes the Hunter's Hut through Vanilla's normal building-height case.
    /// Vanilla otherwise ignores this building only when geometry callers ask
    /// for the obstacle-aware height used by line-of-sight checks.
    /// </summary>
    internal sealed class HunterHutVisibilityPatch : IDisposable
    {
        private const int HeightHelperRva = 0x6B990;
        private const int HeightHelperTypeSwitchRva = 0x6B9F8;
        private const int DispatchTargetTableRva = 0x6BAB4;
        private const int TypeDispatchTableRva = 0x6BAC4;
        private const int BuildingBlockHeightTableRva = 0x2E8C60;
        private const eStructs HunterHutType = eStructs.STRUCT_HUNTERS_HUT;
        private const int HunterHutBlockHeight = 40;

        private const int TypeSwitchToHeightHelperOffset = 0x68;
        private const int TypeDispatchTableOperandOffset = 0x19;
        private const int DispatchTargetTableOperandOffset = 0x21;
        private const int SpecialCaseOffset = 0xAD;
        private const int NormalHeightCaseOffset = 0xB1;
        private const int BuildingHeightTableOperandOffset = 0xB5;

        private const byte IgnoreBuildingWhenObstacleAware = 0;
        private const byte NormalBuildingHeight = 3;

        private const string HeightHelperTypeSwitchPattern =
            "4E 0F BF 9C 11 2E 01 00 00 41 8D 43 F9 83 F8 47 77 37 48 98 " +
            "41 0F B6 84 00 ?? ?? ?? ?? 41 8B 8C 80 ?? ?? ?? ?? 49 03 C8 FF E1";
        private const string HunterHutSpecialCasePattern =
            "85 F6 75 ?? 47 8B 8C 98 ?? ?? ?? ?? EB ??";

        private readonly ManualLogSource log;
        private readonly int dispatchEntryRva;
        private readonly IntPtr dispatchEntryAddress;
        private bool available = true;
        private bool ownsPatch;
        private bool applied;
        private bool disposed;

        public HunterHutVisibilityPatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || imageBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            dispatchEntryRva = ValidateNativeSemantics(memory, referenceHashMatches);
            dispatchEntryAddress = new IntPtr(checked((long)imageBase + dispatchEntryRva));

            byte currentValue = Marshal.ReadByte(dispatchEntryAddress);
            if (currentValue != IgnoreBuildingWhenObstacleAware)
            {
                throw new InvalidOperationException(
                    $"The Hunter's Hut visibility dispatch entry has an unexpected initial value: " +
                    $"expected={IgnoreBuildingWhenObstacleAware}, actual={currentValue}.");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters Hunter's Hut visibility patch initialized: " +
                $"rva=0x{dispatchEntryRva:X}, original={IgnoreBuildingWhenObstacleAware}, " +
                $"normalBuildingCase={NormalBuildingHeight}, blockerHeight={HunterHutBlockHeight}.");
        }

        public bool IsApplied => available && applied && ownsPatch && !disposed;
        public bool IsAvailable => available && !disposed;

        public bool TrySetEnabled(bool enabled)
        {
            if (disposed || !available)
                return false;

            byte expected = ownsPatch
                ? NormalBuildingHeight
                : IgnoreBuildingWhenObstacleAware;
            byte current;
            try
            {
                current = Marshal.ReadByte(dispatchEntryAddress);
            }
            catch (Exception exception)
            {
                available = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters Hunter's Hut visibility state read failed: " +
                    $"requestedEnabled={enabled}, expected={expected}, error={exception}");
                return false;
            }

            if (current != expected)
            {
                available = false;
                applied = false;
                ownsPatch = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters disabled its Hunter's Hut visibility patch because the native entry " +
                    $"changed externally: rva=0x{dispatchEntryRva:X}, expected={expected}, actual={current}.");
                return false;
            }

            if (enabled == applied)
                return enabled ? IsApplied : true;

            byte desired = enabled
                ? NormalBuildingHeight
                : IgnoreBuildingWhenObstacleAware;
            bool ownedBeforeWrite = ownsPatch;
            try
            {
                WriteAndVerify(expected, desired);
                ownsPatch = enabled;
                applied = enabled;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters Hunter's Hut visibility patch {(enabled ? "applied" : "restored")}: " +
                    $"rva=0x{dispatchEntryRva:X}, previous={expected}, current={desired}.");
                return true;
            }
            catch (Exception exception)
            {
                byte actual;
                try
                {
                    actual = Marshal.ReadByte(dispatchEntryAddress);
                }
                catch (Exception readException)
                {
                    ownsPatch = ownedBeforeWrite || enabled;
                    applied = ownsPatch;
                    available = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters Hunter's Hut visibility patch failed and its final value " +
                        $"could not be read: requestedEnabled={enabled}, writeError={exception}, " +
                        $"readError={readException}");
                    return false;
                }

                ownsPatch = actual == NormalBuildingHeight && (ownedBeforeWrite || enabled);
                applied = ownsPatch;
                available = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters Hunter's Hut visibility patch is unavailable: " +
                    $"expected={expected}, desired={desired}, actual={actual}, error={exception}");
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (ownsPatch)
            {
                try
                {
                    byte current = Marshal.ReadByte(dispatchEntryAddress);
                    if (current == NormalBuildingHeight)
                    {
                        WriteAndVerify(NormalBuildingHeight, IgnoreBuildingWhenObstacleAware);
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"Improved Hunters Hunter's Hut visibility patch restored during disposal: " +
                            $"rva=0x{dispatchEntryRva:X}.");
                    }
                    else
                    {
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Improved Hunters did not restore its Hunter's Hut visibility entry because " +
                            $"it changed externally: expected={NormalBuildingHeight}, actual={current}.");
                    }
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters could not restore its Hunter's Hut visibility patch: {exception}");
                }
            }

            ownsPatch = false;
            applied = false;
            available = false;
            disposed = true;
        }

        private void WriteAndVerify(byte expected, byte desired)
        {
            byte current = Marshal.ReadByte(dispatchEntryAddress);
            if (current != expected)
            {
                throw new InvalidOperationException(
                    $"The Hunter's Hut visibility entry changed before writing: " +
                    $"expected={expected}, actual={current}.");
            }

            UIntPtr size = (UIntPtr)1;
            if (!Kernel32.VirtualProtect(
                    dispatchEntryAddress,
                    size,
                    Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                throw new InvalidOperationException(
                    "VirtualProtect failed for the Hunter's Hut visibility dispatch entry.");
            }

            try
            {
                Marshal.WriteByte(dispatchEntryAddress, desired);
            }
            finally
            {
                if (!Kernel32.VirtualProtect(dispatchEntryAddress, size, oldProtection, out _))
                {
                    throw new InvalidOperationException(
                        "Restoring memory protection failed for the Hunter's Hut visibility dispatch entry.");
                }
            }

            byte verified = Marshal.ReadByte(dispatchEntryAddress);
            if (verified != desired)
            {
                throw new InvalidOperationException(
                    $"The Hunter's Hut visibility patch verification failed: " +
                    $"expected={desired}, actual={verified}.");
            }
        }

        private int ValidateNativeSemantics(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            int typeSwitchRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HeightHelperTypeSwitchPattern,
                HeightHelperTypeSwitchRva,
                referenceHashMatches,
                "Hunter's Hut visibility height dispatch",
                log).Rva;
            int heightHelperRva = checked(typeSwitchRva - TypeSwitchToHeightHelperOffset);
            int typeDispatchTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                typeSwitchRva + TypeDispatchTableOperandOffset);
            int dispatchTargetTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                typeSwitchRva + DispatchTargetTableOperandOffset);
            int buildingHeightTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                heightHelperRva + BuildingHeightTableOperandOffset);

            ValidateRange(memory, typeDispatchTableRva, 2);
            ValidateRange(memory, dispatchTargetTableRva, 4 * sizeof(int));
            ValidateRange(memory, buildingHeightTableRva, ((int)HunterHutType + 1) * sizeof(int));

            int specialCaseRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                dispatchTargetTableRva);
            int normalHeightCaseRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                dispatchTargetTableRva + NormalBuildingHeight * sizeof(int));
            byte hunterHutDispatch = memory[typeDispatchTableRva];
            byte nextBuildingDispatch = memory[typeDispatchTableRva + 1];
            int hunterHutHeight = Shared.NativePatternResolver.ReadInt32(
                memory,
                buildingHeightTableRva + (int)HunterHutType * sizeof(int));

            if (!Shared.NativePatternResolver.MatchesPatternAt(
                    memory,
                    heightHelperRva + SpecialCaseOffset,
                    HunterHutSpecialCasePattern))
            {
                throw new InvalidOperationException(
                    "The Hunter's Hut visibility special case failed byte validation.");
            }

            if (specialCaseRva != heightHelperRva + SpecialCaseOffset ||
                normalHeightCaseRva != heightHelperRva + NormalHeightCaseOffset ||
                hunterHutDispatch != IgnoreBuildingWhenObstacleAware ||
                nextBuildingDispatch != NormalBuildingHeight ||
                hunterHutHeight != HunterHutBlockHeight)
            {
                throw new InvalidOperationException(
                    $"The Hunter's Hut visibility height semantics changed: " +
                    $"specialCase=0x{specialCaseRva:X}, normalCase=0x{normalHeightCaseRva:X}, " +
                    $"hunterDispatch={hunterHutDispatch}, nextDispatch={nextBuildingDispatch}, " +
                    $"hunterHeight={hunterHutHeight}.");
            }

            if (referenceHashMatches &&
                (heightHelperRva != HeightHelperRva ||
                    typeSwitchRva != HeightHelperTypeSwitchRva ||
                    typeDispatchTableRva != TypeDispatchTableRva ||
                    dispatchTargetTableRva != DispatchTargetTableRva ||
                    buildingHeightTableRva != BuildingBlockHeightTableRva))
            {
                throw new InvalidOperationException(
                    $"The Hunter's Hut visibility RVAs changed on the audited DLL: " +
                    $"helper=0x{heightHelperRva:X}, switch=0x{typeSwitchRva:X}, " +
                    $"typeTable=0x{typeDispatchTableRva:X}, targetTable=0x{dispatchTargetTableRva:X}, " +
                    $"heightTable=0x{buildingHeightTableRva:X}.");
            }

            return typeDispatchTableRva;
        }

        private static void ValidateRange(ReadOnlySpan<byte> memory, int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset > memory.Length - length)
                throw new InvalidOperationException("A Hunter's Hut visibility table lies outside the game module.");
        }
    }
}
