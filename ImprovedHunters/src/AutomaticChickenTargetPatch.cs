using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
    internal sealed class AutomaticChickenTargetPatch : IDisposable
    {
        private const int TargetSelectionTypeDispatchRva = 0x18F2B2;
        private const int DispatchTargetTableRva = 0x18FA14;
        private const int TypeDispatchTableRva = 0x18FA30;
        private const int HunterOnlyCaseRva = 0x18F2DE;
        private const int CommonAcceptanceCaseRva = 0x18F3F6;
        private const int CandidateRejectionCaseRva = 0x18F7A4;
        private const int ManualAttackCommandRva = 0x18EB36;
        private const int ManualAttackTargetAssignmentRva = 0x18ED96;
        private const eChimps DeerType = eChimps.CHIMP_TYPE_DEER;
        private const eChimps ChickenType = eChimps.CHIMP_TYPE_CHICKEN;
        private const int ChickenDispatchEntryRva = TypeDispatchTableRva + (int)ChickenType - (int)DeerType;
        private const byte HunterOnlyDispatchIndex = 0;
        private const byte GeneralAcceptanceDispatchIndex = 6;
        private const int TypeTableDisplacementOffset = 0x1C;
        private const int TargetTableDisplacementOffset = 0x23;
        private const int DispatchReaderOffset = 0x18;
        private const int DispatchReaderDisplacedBytes = 15;
        private const string TargetSelectionTypeDispatchPattern =
            "46 0F BF 8C 07 E6 06 00 00 41 8D 41 D4 83 F8 2B " +
            "0F 87 ?? ?? ?? ?? 48 98 0F B6 84 02 ?? ?? ?? ?? " +
            "8B 8C 82 ?? ?? ?? ?? 48 03 CA FF E1";
        private const string HunterOnlyCasePattern =
            "66 42 83 BC 06 E6 06 00 00 06 0F 84 ?? ?? ?? ?? E9 ?? ?? ?? ??";
        private const string ManualAttackCommandPattern =
            "42 0F B7 84 06 F4 09 00 00 66 83 F8 04 0F 85 ?? ?? ?? ?? " +
            "4E 0F BF B4 06 F6 09 00 00";
        private const string ManualAttackTargetAssignmentPattern =
            "66 46 89 B4 26 9C 09 00 00 41 8B 8C 2C F0 06 00 00";

        private readonly ManualLogSource log;
        private readonly PermanentDispatchIndexOverride dispatchOverride;
        private bool available = true;
        private bool applied;
        private bool disposed;

        public AutomaticChickenTargetPatch(
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
                imageBase + TargetSelectionTypeDispatchRva + DispatchReaderOffset,
                DispatchReaderDisplacedBytes,
                (int)ChickenType,
                HunterOnlyDispatchIndex,
                sourceTypeInR9: true);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Automatic chicken targeting permanent reader hook installed: entryRva=0x{entryRva:X}, " +
                $"readerRva=0x{TargetSelectionTypeDispatchRva + DispatchReaderOffset:X}, displaced={DispatchReaderDisplacedBytes}.");
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
                Shared.DebugLogHelper.LogError(log, $"Automatic chicken targeting was disabled logically: {ex}");
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
            int dispatchRva = Shared.NativePatternResolver.ResolveUnique(
                memory, TargetSelectionTypeDispatchPattern, TargetSelectionTypeDispatchRva,
                referenceHashMatches, "automatic ranged target type dispatch", log).Rva;
            int typeTableRva = Shared.NativePatternResolver.ReadInt32(memory, dispatchRva + TypeTableDisplacementOffset);
            int targetTableRva = Shared.NativePatternResolver.ReadInt32(memory, dispatchRva + TargetTableDisplacementOffset);
            if (referenceHashMatches &&
                (typeTableRva != TypeDispatchTableRva || targetTableRva != DispatchTargetTableRva))
                throw new InvalidOperationException("The ranged target dispatch tables changed.");
            int chickenEntryRva = checked(typeTableRva + (int)ChickenType - (int)DeerType);
            ValidateRange(memory, targetTableRva, (GeneralAcceptanceDispatchIndex + 1) * sizeof(int));
            ValidateRange(memory, chickenEntryRva, sizeof(byte));
            int hunterTarget = Shared.NativePatternResolver.ReadInt32(memory, targetTableRva);
            int generalTarget = Shared.NativePatternResolver.ReadInt32(
                memory, targetTableRva + GeneralAcceptanceDispatchIndex * sizeof(int));
            if ((referenceHashMatches &&
                    (hunterTarget != HunterOnlyCaseRva || generalTarget != CommonAcceptanceCaseRva ||
                     chickenEntryRva != ChickenDispatchEntryRva)) ||
                memory[chickenEntryRva] != GeneralAcceptanceDispatchIndex)
                throw new InvalidOperationException("The ranged target dispatch semantics changed.");
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, hunterTarget, HunterOnlyCasePattern))
                throw new InvalidOperationException("The hunter-only target case failed byte validation.");
            int acceptanceTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, hunterTarget + 12, hunterTarget + 16);
            int rejectionTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, hunterTarget + 17, hunterTarget + 21);
            if (acceptanceTarget != generalTarget ||
                (referenceHashMatches && rejectionTarget != CandidateRejectionCaseRva))
                throw new InvalidOperationException("The hunter-only target branches changed.");
            int manualCommand = Shared.NativePatternResolver.ResolveUnique(
                memory, ManualAttackCommandPattern, ManualAttackCommandRva,
                referenceHashMatches, "explicit AttackUnit command path", log).Rva;
            int manualAssignment = Shared.NativePatternResolver.ResolveUnique(
                memory, ManualAttackTargetAssignmentPattern, ManualAttackTargetAssignmentRva,
                referenceHashMatches, "explicit AttackUnit target assignment", log).Rva;
            if (manualCommand >= manualAssignment || manualAssignment >= dispatchRva)
                throw new InvalidOperationException("The explicit AttackUnit path no longer precedes automatic target dispatch.");
            return chickenEntryRva;
        }

        private static void ValidateRange(ReadOnlySpan<byte> memory, int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset > memory.Length - length)
                throw new InvalidOperationException("A ranged target dispatch table lies outside the game module.");
        }
    }
}
