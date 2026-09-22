using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
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
        private const int DispatchReaderOffset = 0x14;
        private const int DispatchReaderDisplacedBytes = 17;
        private const byte IgnoreBuildingWhenObstacleAware = 0;
        private const byte NormalBuildingHeight = 3;
        private const string HeightHelperTypeSwitchPattern =
            "4E 0F BF 9C 11 2E 01 00 00 41 8D 43 F9 83 F8 47 77 37 48 98 " +
            "41 0F B6 84 00 ?? ?? ?? ?? 41 8B 8C 80 ?? ?? ?? ?? 49 03 C8 FF E1";
        private const string HunterHutSpecialCasePattern =
            "85 F6 75 ?? 47 8B 8C 98 ?? ?? ?? ?? EB ??";

        private readonly ManualLogSource log;
        private readonly PermanentDispatchIndexOverride dispatchOverride;
        private bool available = true;
        private bool applied;
        private bool disposed;

        public HunterHutVisibilityPatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || imageBase == 0) throw new ArgumentException("The Crusader library is unavailable.");
            int entryRva = ValidateNativeSemantics(memory, referenceHashMatches);
            dispatchOverride = new PermanentDispatchIndexOverride(
                region,
                imageBase + HeightHelperTypeSwitchRva + DispatchReaderOffset,
                DispatchReaderDisplacedBytes,
                (int)HunterHutType,
                NormalBuildingHeight,
                sourceTypeInR9: false);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Hunter's Hut visibility permanent reader hook installed: entryRva=0x{entryRva:X}, " +
                $"readerRva=0x{HeightHelperTypeSwitchRva + DispatchReaderOffset:X}, displaced={DispatchReaderDisplacedBytes}.");
        }

        public bool IsApplied => available && applied && !disposed && dispatchOverride.IsInstalled;
        public bool IsAvailable => available && !disposed;

        public bool TrySetEnabled(bool enabled)
        {
            if (disposed || !available) return false;
            try
            {
                dispatchOverride.SetEnabled(enabled);
                applied = enabled;
                return true;
            }
            catch (Exception ex)
            {
                applied = false;
                available = false;
                Shared.DebugLogHelper.LogError(log, $"Hunter's Hut visibility was disabled logically: {ex}");
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            dispatchOverride.SetEnabled(false);
            applied = false;
            available = false;
            disposed = true;
        }

        private int ValidateNativeSemantics(ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            int typeSwitchRva = Shared.NativePatternResolver.ResolveUnique(
                memory, HeightHelperTypeSwitchPattern, HeightHelperTypeSwitchRva,
                referenceHashMatches, "Hunter's Hut visibility height dispatch", log).Rva;
            int heightHelperRva = checked(typeSwitchRva - TypeSwitchToHeightHelperOffset);
            int typeTableRva = Shared.NativePatternResolver.ReadInt32(
                memory, typeSwitchRva + TypeDispatchTableOperandOffset);
            int targetTableRva = Shared.NativePatternResolver.ReadInt32(
                memory, typeSwitchRva + DispatchTargetTableOperandOffset);
            int heightTableRva = Shared.NativePatternResolver.ReadInt32(
                memory, heightHelperRva + BuildingHeightTableOperandOffset);
            ValidateRange(memory, typeTableRva, 2);
            ValidateRange(memory, targetTableRva, 4 * sizeof(int));
            ValidateRange(memory, heightTableRva, ((int)HunterHutType + 1) * sizeof(int));
            int specialCaseRva = Shared.NativePatternResolver.ReadInt32(memory, targetTableRva);
            int normalCaseRva = Shared.NativePatternResolver.ReadInt32(
                memory, targetTableRva + NormalBuildingHeight * sizeof(int));
            byte hunterDispatch = memory[typeTableRva];
            byte nextDispatch = memory[typeTableRva + 1];
            int hunterHeight = Shared.NativePatternResolver.ReadInt32(
                memory, heightTableRva + (int)HunterHutType * sizeof(int));
            if (!Shared.NativePatternResolver.MatchesPatternAt(
                memory, heightHelperRva + SpecialCaseOffset, HunterHutSpecialCasePattern))
                throw new InvalidOperationException("The Hunter's Hut visibility special case failed byte validation.");
            if (specialCaseRva != heightHelperRva + SpecialCaseOffset ||
                normalCaseRva != heightHelperRva + NormalHeightCaseOffset ||
                hunterDispatch != IgnoreBuildingWhenObstacleAware ||
                nextDispatch != NormalBuildingHeight || hunterHeight != HunterHutBlockHeight)
                throw new InvalidOperationException("The Hunter's Hut visibility height semantics changed.");
            if (referenceHashMatches &&
                (heightHelperRva != HeightHelperRva || typeSwitchRva != HeightHelperTypeSwitchRva ||
                 typeTableRva != TypeDispatchTableRva || targetTableRva != DispatchTargetTableRva ||
                 heightTableRva != BuildingBlockHeightTableRva))
                throw new InvalidOperationException("The Hunter's Hut visibility RVAs changed.");
            return typeTableRva;
        }

        private static void ValidateRange(ReadOnlySpan<byte> memory, int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset > memory.Length - length)
                throw new InvalidOperationException("A Hunter's Hut visibility table lies outside the game module.");
        }
    }
}
