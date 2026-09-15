// Feature: Own the shared native movement-speed and animation-cadence hooks.
using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal enum SynchronizedMovementCadence : byte
    {
        Walking,
        Running
    }

    /// <summary>
    /// Provides the native speed and cadence hooks shared by recruit rally
    /// movement and mixed-group synchronization.
    /// </summary>
    internal sealed unsafe class SynchronizedMovementCadencePatch : IDisposable
    {
        internal const int MaximumTrackedUnitId = 10000;
        internal const int MaximumTrackedTribeId = 4500;

        private const int MaximumUnitTypeHandlerLength = 0x5000;
        private const ulong UnitRecordOffset = 0x65CUL;

        private const int RallyEntrySize = 96;
        private const int RallyOwnerOffset = 0;
        // Vanilla's monotonically assigned GlobalId is the generation token
        // which distinguishes a reused 1-based unit-array slot.
        private const int RallyGenerationGlobalIdOffset = 4;
        private const int RallyUnitTypeOffset = 8;
        private const int RallyTargetXOffset = 10;
        private const int RallyTargetYOffset = 12;
        private const int RallyActiveOffset = 14;
        private const int RallyObservedOffset = 15;
        private const int RallyMovingOffset = 16;

        // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
        // Temporary, tick-log-free instrumentation. Remove the complete
        // RALLY_ANIMATION_DIAGNOSTICS surface after the runtime cause is
        // confirmed and covered by a permanent semantic emitter test.
        private const int RallyDiagnosticsStatusOffset = 17;
        private const int RallyDiagnosticsAbortReasonOffset = 18;
        private const int RallyDiagnosticsObservedAnimationOffset = 20;
        private const int RallyDiagnosticsWrittenAnimationOffset = 24;
        private const int RallyDiagnosticsWrittenBonusOffset = 28;
        private const int RallyDiagnosticsCadenceVisitsOffset = 30;
        private const int RallyDiagnosticsFirstCurrentUnitIdOffset = 32;
        private const int RallyDiagnosticsLastCurrentUnitIdOffset = 36;
        private const int RallyDiagnosticsFirstGlobalIdOffset = 40;
        private const int RallyDiagnosticsLastGlobalIdOffset = 44;
        private const int RallyDiagnosticsFirstAnimationOffset = 48;
        private const int RallyDiagnosticsLastAnimationOffset = 52;
        private const int RallyDiagnosticsFirstAliveStateOffset = 56;
        private const int RallyDiagnosticsLastAliveStateOffset = 58;
        private const int RallyDiagnosticsFirstUnitTypeOffset = 60;
        private const int RallyDiagnosticsLastUnitTypeOffset = 62;
        private const int RallyDiagnosticsFirstAiStateOffset = 64;
        private const int RallyDiagnosticsLastAiStateOffset = 66;
        private const int RallyDiagnosticsFirstTransformTypeOffset = 68;
        private const int RallyDiagnosticsLastTransformTypeOffset = 70;
        private const int RallyDiagnosticsFirstPathFlagsOffset = 72;
        private const int RallyDiagnosticsLastPathFlagsOffset = 74;
        private const int RallyDiagnosticsFirstTargetXOffset = 76;
        private const int RallyDiagnosticsFirstTargetYOffset = 78;
        private const int RallyDiagnosticsLastTargetXOffset = 80;
        private const int RallyDiagnosticsLastTargetYOffset = 82;
        private const int RallyDiagnosticsSnapshotCountOffset = 84;
        private const int RallyDiagnosticsTransitionCountOffset = 86;
        private const int RallyDiagnosticsFirstOwnerOffset = 88;
        private const int RallyDiagnosticsLastOwnerOffset = 89;
        private const byte RallyDiagnosticsRegistered = 1 << 0;
        private const byte RallyDiagnosticsIdentityConfirmed = 1 << 1;
        private const byte RallyDiagnosticsPathObserved = 1 << 2;
        private const byte RallyDiagnosticsProfileResolved = 1 << 3;
        private const byte RallyDiagnosticsCadenceWritten = 1 << 4;
        private const byte RallyDiagnosticsSnapshotCaptured = 1 << 5;
        private const byte RallyDiagnosticsAbortDead = 1;
        private const byte RallyDiagnosticsAbortGeneration = 2;
        private const byte RallyDiagnosticsAbortOwner = 3;
        private const byte RallyDiagnosticsAbortType = 4;
        private const byte RallyDiagnosticsAbortTarget = 5;
        private const uint RallyDiagnosticsUnsetAnimation = uint.MaxValue;
        // RALLY_ANIMATION_DIAGNOSTICS_END

        private const int SynchronizationEntrySize = 4;
        private const int SynchronizationActiveOffset = 0;
        private const int SynchronizationCadenceOffset = 1;
        private const int SynchronizationBonusOffset = 2;

        private const int MaximumNativeTransitionMappings = 16;
        private const int NativeProfileRunningCountOffset = 0;
        private const int NativeProfileWalkingCountOffset = 1;
        private const int NativeProfileAllowRallyFallbackOffset = 2;
        private const int NativeProfileRunningStateCountOffset = 3;
        private const int NativeProfileBonusOffset = 4;
        private const int NativeProfileSoleRunningStateOffset = 8;
        private const int NativeProfileRunningMappingsOffset = 16;
        private const int NativeTransitionMappingSize = 8;
        private const int NativeProfileWalkingMappingsOffset =
            NativeProfileRunningMappingsOffset +
            MaximumNativeTransitionMappings * NativeTransitionMappingSize;
        private const int NativeProfileSize =
            NativeProfileWalkingMappingsOffset +
            MaximumNativeTransitionMappings * NativeTransitionMappingSize;

        private const int UnitAnimationStateManagerOffset = 0x660;
        private const int UnitAliveStateManagerOffset = 0x6E4;
        private const int UnitTypeManagerOffset = 0x6E6;
        private const int UnitOwnerManagerOffset = 0x6EE;
        private const int UnitGlobalIdManagerOffset = 0x6F0;
        private const int UnitTargetXManagerOffset = 0x720;
        private const int UnitTargetYManagerOffset = 0x722;
        private const int UnitPathStateManagerOffset = 0x74E;
        private const int UnitAiStateManagerOffset = 0x918;
        private const int UnitTransformTypeManagerOffset = 0x922;
        private const int UnitTribeIdManagerOffset = 0x930;
        private const int UnitCurrentSpeed2ManagerOffset = 0x9A2;
        private const int UnitCurrentSpeedManagerOffset = 0x9A4;

        private const ushort IndividualFastMovementAiState = 101;
        private const ushort UnitInitializationAiState = 109;
        private const int UnitAnimationStateOffset = 0x660;
        private const int UnitSpeedBonusOffset = 0x916;
        private const int MaximumCadenceCaseLength = 0x240;
        private const int MaximumCadencePairDistance = 20;
        private const int DirectCadencePairDistance = 4;

        // c_game_unit_calculate_movement_speed after its base/group-speed
        // calculation and immediately before its late terrain/status stage.
        private const string PreTerrainSpeedAdjustmentPattern =
            "0F B6 83 C8 06 00 00 45 85 C9 74 ?? 3C 18 7D ?? " +
            "04 04 88 83 C8 06 00 00";

        // updateUnits pass 4:
        // call qword ptr [moduleBase + unitType * 8 + dispatchTableOffset]
        private const string UnitTypeUpdateDispatchPattern =
            "41 FF 94 C6 ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? 48 63 C2 48 69 C8 90 04 00 00";

        // Common movement cadence:
        // movsx eax, word ptr [r8+916h] ; movement sub-step bonus
        // movsx ecx, word ptr [r8+9A2h] ; effective speed delay
        // mov r10d, dword ptr [r8+9A8h]
        private const string MovementCadencePattern =
            "41 0F BF 80 16 09 00 00 41 0F BF 88 A2 09 00 00 45 8B 90 A8 09 00 00";
        private const string SpearmanMovementDecisionPattern =
            "66 42 39 BC 3B 14 09 00 00 75 2D " +
            "66 42 39 BC 3B 9E 09 00 00 " +
            "0F 85 ?? ?? ?? ?? 39 3D ?? ?? ?? ?? " +
            "74 16 41 83 FE 63";
        private const int ImprovedSpearmanFlagDisplacementOffset = 0x1C;
        private const int ImprovedSpearmanFlagInstructionEndOffset = 0x20;
        private const int CalculateMovementSpeedFunctionRva = 0x19B260;
        private const int CalculateMovementSpeedFunctionLength = 0x3C6;
        private const int PreTerrainSpeedAdjustmentRva = 0x19B506;
        private const int PreTerrainSpeedAdjustmentHookLength = 14;
        private const int UnitTypeUpdateDispatchRva = 0x18410C;
        private const int MovementCadenceRva = 0x184203;

        private readonly ManualLogSource log;
        private HookTransaction transaction;
        private readonly Dictionary<eChimps, AnimationTransitions>
            animationTransitionsByType =
                new Dictionary<eChimps, AnimationTransitions>(
                    (int)eChimps.CHIMP_NUM_TYPES);
        private readonly GameUnit* unitArray;
        private byte* rallyEntries;
        private byte* synchronizationEntries;
        private byte* nativeProfiles;
        private int* rallyEnabledFlag;
        private int* synchronizationEnabledFlag;
        private readonly ulong currentUnitIdAddress;
        private readonly ulong improvedSpearmanFlagAddress;
        private readonly HookHandle<X64InlineHook> movementSpeedAdjustmentHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> movementCadenceHook = new HookHandle<X64InlineHook>();
        private bool published;
        private bool disposed;

        public SynchronizedMovementCadencePatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));

            // The semantic decoder needs the manager-relative address used by
            // native unit handlers; rally tracking itself lives elsewhere.
            unitArray = GameUnitManagerAPI.Instance.GetUnitArray()._array;
            if (unitArray == null)
            {
                throw new InvalidOperationException(
                    "The native unit array is unavailable.");
            }

            int movementSpeedAdjustmentRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                PreTerrainSpeedAdjustmentPattern,
                PreTerrainSpeedAdjustmentRva,
                referenceHashMatches,
                "pre-terrain movement-speed adjustment",
                log).Rva;
            int dispatchRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                UnitTypeUpdateDispatchPattern,
                UnitTypeUpdateDispatchRva,
                referenceHashMatches,
                "unit-type update dispatch",
                log).Rva;
            int cadenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MovementCadencePattern,
                MovementCadenceRva,
                referenceHashMatches,
                "movement cadence",
                log).Rva;
            currentUnitIdAddress = ResolveCurrentUnitIdAddress(
                memory,
                libraryBase,
                dispatchRva);
            int spearmanDecisionRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                SpearmanMovementDecisionPattern,
                0x143BD9,
                referenceHashMatches,
                "Spearman movement decision for rally cadence",
                log).Rva;
            improvedSpearmanFlagAddress = ResolveImprovedSpearmanFlagAddress(
                memory,
                libraryBase,
                libraryBase + unchecked((ulong)spearmanDecisionRva));

            ValidatePreTerrainSpeedAdjustmentHook(
                memory,
                libraryBase,
                movementSpeedAdjustmentRva);
            ValidateMovementCadenceHook(
                memory,
                libraryBase,
                cadenceRva);
            DiscoverRunningAnimationTransitions(
                memory,
                libraryBase,
                libraryBase + unchecked((ulong)dispatchRva),
                referenceHashMatches);

            try
            {
                rallyEntries = AllocateZeroed(
                    checked((MaximumTrackedUnitId + 1) * RallyEntrySize));
                synchronizationEntries = AllocateZeroed(
                    checked((MaximumTrackedTribeId + 1) *
                            SynchronizationEntrySize));
                nativeProfiles = AllocateZeroed(
                    checked((int)eChimps.CHIMP_NUM_TYPES * NativeProfileSize));
                rallyEnabledFlag = (int*)AllocateZeroed(sizeof(int));
                synchronizationEnabledFlag =
                    (int*)AllocateZeroed(sizeof(int));
                PublishNativeProfiles();

                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);

                transaction.AddInline(
                    movementSpeedAdjustmentHook,
                    HookTarget.FromAddress(
                        libraryBase + unchecked((ulong)movementSpeedAdjustmentRva)),
                    (assembler, instructions, returnAddress) =>
                        GeneratePreTerrainSpeedFastPath(
                            assembler,
                            instructions,
                            returnAddress),
                    hookSize: PreTerrainSpeedAdjustmentHookLength);

                transaction.AddInline(
                    movementCadenceHook,
                    HookTarget.FromAddress(
                        libraryBase + unchecked((ulong)cadenceRva)),
                    (assembler, instructions, returnAddress) =>
                        GenerateCadenceFastPath(
                            assembler,
                            instructions,
                            returnAddress),
                    hookSize: MovementCadencePattern.Split(' ').Length);

                CommitResult commitResult = transaction.Commit();

                if (!commitResult.IsCompleteSuccess ||
                    !movementSpeedAdjustmentHook.Success ||
                    !movementCadenceHook.Success)
                {
                    throw new InvalidOperationException(
                        "The native movement-speed adjustment or movement " +
                        "cadence was not found.");
                }

                published = true;
            }
            catch
            {
                transaction?.Dispose();
                FreeUnpublishedTables();
                throw;
            }

            TroopMovementFix3ModLog.Debug(
                log,
                $"Native allocation-free movement-speed and cadence fastpaths installed; " +
                $"runCapableUnitTypes={animationTransitionsByType.Count}.");
        }

        internal void SetRallyTracking(
            int unitId,
            uint globalId,
            int ownerPlayerId,
            eChimps expectedUnitType)
        {
            if (unitId <= 0 || unitId > MaximumTrackedUnitId)
                return;

            byte* entry = rallyEntries + unitId * RallyEntrySize;
            for (int offset = 0; offset < RallyEntrySize; offset++)
                entry[offset] = 0;
            *(int*)(entry + RallyOwnerOffset) = ownerPlayerId;
            *(uint*)(entry + RallyGenerationGlobalIdOffset) = globalId;
            *(ushort*)(entry + RallyUnitTypeOffset) = (ushort)expectedUnitType;
            *(ushort*)(entry + RallyTargetXOffset) = 0;
            *(ushort*)(entry + RallyTargetYOffset) = 0;
            entry[RallyObservedOffset] = 0;
            entry[RallyMovingOffset] = 0;
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            entry[RallyDiagnosticsStatusOffset] =
                RallyDiagnosticsRegistered;
            *(uint*)(entry + RallyDiagnosticsObservedAnimationOffset) =
                RallyDiagnosticsUnsetAnimation;
            *(uint*)(entry + RallyDiagnosticsWrittenAnimationOffset) =
                RallyDiagnosticsUnsetAnimation;
            // RALLY_ANIMATION_DIAGNOSTICS_END
            entry[RallyActiveOffset] = 1;
        }

        internal void SetRallyEnabled(bool enabled)
        {
            if (rallyEnabledFlag == null)
                return;

            int requested = enabled ? 1 : 0;
            if (*rallyEnabledFlag == requested)
                return;

            if (enabled)
            {
                ClearAllRallyTracking();
                *rallyEnabledFlag = 1;
            }
            else
            {
                *rallyEnabledFlag = 0;
                ClearAllRallyTracking();
            }
        }

        internal void ClearRallyTracking(int unitId)
        {
            if (unitId > 0 && unitId <= MaximumTrackedUnitId)
            {
                byte* entry = rallyEntries + unitId * RallyEntrySize;
                LogRallyAnimationDiagnostics(unitId, entry);
                for (int offset = 0; offset < RallyEntrySize; offset++)
                    entry[offset] = 0;
            }
        }

        internal void ClearAllRallyTracking()
        {
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            if (rallyEntries != null)
            {
                for (int unitId = 1;
                     unitId <= MaximumTrackedUnitId;
                     unitId++)
                {
                    byte* entry = rallyEntries + unitId * RallyEntrySize;
                    LogRallyAnimationDiagnostics(unitId, entry);
                }
            }
            // RALLY_ANIMATION_DIAGNOSTICS_END
            ZeroMemory(
                rallyEntries,
                checked((MaximumTrackedUnitId + 1) * RallyEntrySize));
        }

        // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
        private void LogRallyAnimationDiagnostics(int unitId, byte* entry)
        {
            if (entry == null ||
                (entry[RallyDiagnosticsStatusOffset] &
                 RallyDiagnosticsRegistered) == 0)
            {
                return;
            }

            byte status = entry[RallyDiagnosticsStatusOffset];
            byte abortReason = entry[RallyDiagnosticsAbortReasonOffset];
            uint observedAnimation = *(uint*)(
                entry + RallyDiagnosticsObservedAnimationOffset);
            uint writtenAnimation = *(uint*)(
                entry + RallyDiagnosticsWrittenAnimationOffset);
            ushort writtenBonus = *(ushort*)(
                entry + RallyDiagnosticsWrittenBonusOffset);
            ushort cadenceVisits = *(ushort*)(
                entry + RallyDiagnosticsCadenceVisitsOffset);
            ushort snapshotCount = *(ushort*)(
                entry + RallyDiagnosticsSnapshotCountOffset);
            ushort transitionCount = *(ushort*)(
                entry + RallyDiagnosticsTransitionCountOffset);
            Shared.DebugLogHelper.LogInfo(
                log,
                "RALLY_ANIMATION_DIAGNOSTICS " +
                $"tableUnitId={unitId}, active={entry[RallyActiveOffset] != 0}, " +
                $"owner={*(int*)(entry + RallyOwnerOffset)}, " +
                $"globalId={*(uint*)(entry + RallyGenerationGlobalIdOffset)}, " +
                $"expectedType={*(ushort*)(entry + RallyUnitTypeOffset)}, " +
                $"status=0x{status:X2}, " +
                $"identity={(status & RallyDiagnosticsIdentityConfirmed) != 0}, " +
                $"path={(status & RallyDiagnosticsPathObserved) != 0}, " +
                $"profile={(status & RallyDiagnosticsProfileResolved) != 0}, " +
                $"written={(status & RallyDiagnosticsCadenceWritten) != 0}, " +
                $"abort={DescribeRallyDiagnosticsAbort(abortReason)}, " +
                $"visits={cadenceVisits}, " +
                $"snapshots={snapshotCount}, transitions={transitionCount}, " +
                $"currentUnitId={*(uint*)(entry + RallyDiagnosticsFirstCurrentUnitIdOffset)}->" +
                $"{*(uint*)(entry + RallyDiagnosticsLastCurrentUnitIdOffset)}, " +
                $"actualGlobalId={*(uint*)(entry + RallyDiagnosticsFirstGlobalIdOffset)}->" +
                $"{*(uint*)(entry + RallyDiagnosticsLastGlobalIdOffset)}, " +
                $"alive={*(ushort*)(entry + RallyDiagnosticsFirstAliveStateOffset)}->" +
                $"{*(ushort*)(entry + RallyDiagnosticsLastAliveStateOffset)}, " +
                $"actualType={*(ushort*)(entry + RallyDiagnosticsFirstUnitTypeOffset)}->" +
                $"{*(ushort*)(entry + RallyDiagnosticsLastUnitTypeOffset)}, " +
                $"actualOwner={entry[RallyDiagnosticsFirstOwnerOffset]}->" +
                $"{entry[RallyDiagnosticsLastOwnerOffset]}, " +
                $"aiState={*(ushort*)(entry + RallyDiagnosticsFirstAiStateOffset)}->" +
                $"{*(ushort*)(entry + RallyDiagnosticsLastAiStateOffset)}, " +
                $"transformType={*(ushort*)(entry + RallyDiagnosticsFirstTransformTypeOffset)}->" +
                $"{*(ushort*)(entry + RallyDiagnosticsLastTransformTypeOffset)}, " +
                $"pathFlags=0x{*(ushort*)(entry + RallyDiagnosticsFirstPathFlagsOffset):X}->" +
                $"0x{*(ushort*)(entry + RallyDiagnosticsLastPathFlagsOffset):X}, " +
                $"target=({*(ushort*)(entry + RallyDiagnosticsFirstTargetXOffset)}," +
                $"{*(ushort*)(entry + RallyDiagnosticsFirstTargetYOffset)})->" +
                $"({*(ushort*)(entry + RallyDiagnosticsLastTargetXOffset)}," +
                $"{*(ushort*)(entry + RallyDiagnosticsLastTargetYOffset)}), " +
                $"animation=0x{*(uint*)(entry + RallyDiagnosticsFirstAnimationOffset):X}->" +
                $"0x{*(uint*)(entry + RallyDiagnosticsLastAnimationOffset):X}, " +
                $"observedAnimation=0x{observedAnimation:X}, " +
                $"writtenAnimation=0x{writtenAnimation:X}, " +
                $"writtenBonus={writtenBonus}.");
        }

        private static string DescribeRallyDiagnosticsAbort(byte reason)
        {
            switch (reason)
            {
                case RallyDiagnosticsAbortDead:
                    return "dead";
                case RallyDiagnosticsAbortGeneration:
                    return "generation";
                case RallyDiagnosticsAbortOwner:
                    return "owner";
                case RallyDiagnosticsAbortType:
                    return "type";
                case RallyDiagnosticsAbortTarget:
                    return "target";
                default:
                    return "none";
            }
        }
        // RALLY_ANIMATION_DIAGNOSTICS_END

        internal void SetSynchronization(
            int tribeId,
            SynchronizedMovementCadence cadence,
            ushort runningSpeedBonus)
        {
            if (tribeId <= 0 || tribeId > MaximumTrackedTribeId)
                return;

            byte* entry =
                synchronizationEntries +
                tribeId * SynchronizationEntrySize;
            entry[SynchronizationActiveOffset] = 0;
            entry[SynchronizationCadenceOffset] =
                cadence == SynchronizedMovementCadence.Running
                    ? (byte)2
                    : (byte)1;
            *(ushort*)(entry + SynchronizationBonusOffset) =
                runningSpeedBonus;
            entry[SynchronizationActiveOffset] = 1;
        }

        internal void SetSynchronizationEnabled(bool enabled)
        {
            if (synchronizationEnabledFlag == null)
                return;

            int requested = enabled ? 1 : 0;
            if (*synchronizationEnabledFlag == requested)
                return;

            if (enabled)
            {
                ClearAllSynchronization();
                *synchronizationEnabledFlag = 1;
            }
            else
            {
                *synchronizationEnabledFlag = 0;
                ClearAllSynchronization();
            }
        }

        internal void ClearSynchronization(int tribeId)
        {
            if (tribeId > 0 && tribeId <= MaximumTrackedTribeId)
            {
                synchronizationEntries[
                    tribeId * SynchronizationEntrySize +
                    SynchronizationActiveOffset] = 0;
            }
        }

        internal void ClearAllSynchronization()
        {
            ZeroMemory(
                synchronizationEntries,
                checked((MaximumTrackedTribeId + 1) *
                        SynchronizationEntrySize));
        }

        public bool SupportsSynchronizedRunning(eChimps unitType)
        {
            return animationTransitionsByType.ContainsKey(unitType);
        }

        public ushort GetNativeRunningSpeedBonus(
            eChimps unitType,
            bool improvedSpearmen)
        {
            return TryGetNativeRunningSpeedBonus(
                unitType,
                improvedSpearmen,
                out ushort runningSpeedBonus)
                    ? runningSpeedBonus
                    : (ushort)0;
        }

        internal bool TryGetSynchronizedCadenceBonus(
            eChimps unitType,
            bool improvedSpearmen,
            out ushort cadenceBonus)
        {
            // These mobile siege types use the common sub-step bonus without
            // switching between the infantry walking/running animations.
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                    cadenceBonus = 1;
                    return true;
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    cadenceBonus = 0;
                    return true;
            }

            bool supportsSynchronizedRunning =
                SupportsSynchronizedRunning(unitType) &&
                (unitType != eChimps.CHIMP_TYPE_SPEARMAN ||
                 improvedSpearmen);
            cadenceBonus = supportsSynchronizedRunning
                ? GetNativeRunningSpeedBonus(unitType, improvedSpearmen)
                : (ushort)0;
            return supportsSynchronizedRunning;
        }

        internal bool TryGetNativeRunningSpeedBonus(
            eChimps unitType,
            bool improvedSpearmen,
            out ushort runningSpeedBonus)
        {
            runningSpeedBonus = 0;
            if (unitType == eChimps.CHIMP_TYPE_SPEARMAN)
            {
                if (!improvedSpearmen)
                    return false;
            }

            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    runningSpeedBonus = GameUnitManagerAPI.Instance
                        .GetDefaultCavalryRunSpeedBonus(unitType);
                    return true;
            }

            if (animationTransitionsByType.TryGetValue(
                    unitType,
                    out AnimationTransitions animationTransitions) &&
                animationTransitions.NativeRunningSpeedBonus.HasValue)
            {
                runningSpeedBonus =
                    animationTransitions.NativeRunningSpeedBonus.Value;
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            SetRallyEnabled(false);
            SetSynchronizationEnabled(false);

            // Published native hooks and their embedded table pointers are
            // process-lifetime state. Dispose is only allowed to roll back a
            // candidate which was never published.
            if (!published)
            {
                transaction?.Dispose();
                FreeUnpublishedTables();
            }
        }

        private void GeneratePreTerrainSpeedFastPath(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress)
        {
            if (overwrittenInstructions.Length != 4 ||
                returnAddress == 0)
            {
                throw new InvalidOperationException(
                    "Unexpected pre-terrain speed hook boundary.");
            }

            Label restoreAndReplay =
                assembler.CreateLabel("movementSpeedRestoreAndReplay");
            Label globalIdMatches =
                assembler.CreateLabel("movementSpeedGlobalIdMatches");

            // RBX is the audited manager-relative unit base. RAX is replaced
            // by Vanilla's first displaced instruction. Preserve RCX and the
            // incoming flags because neither belongs to the displaced span.
            assembler.pushfq();
            assembler.push(rcx);
            assembler.mov(rax, unchecked((ulong)rallyEnabledFlag));
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(restoreAndReplay);
            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.cmp(eax, 1);
            assembler.jl(restoreAndReplay);
            assembler.cmp(eax, MaximumTrackedUnitId);
            assembler.jg(restoreAndReplay);
            assembler.imul(rax, rax, RallyEntrySize);
            assembler.mov(rcx, unchecked((ulong)rallyEntries));
            assembler.add(rcx, rax);
            assembler.cmp(
                __byte_ptr[rcx + RallyActiveOffset],
                0);
            assembler.je(restoreAndReplay);
            assembler.cmp(
                __word_ptr[rbx + UnitAliveStateManagerOffset],
                (int)AliveState.IsAlive);
            assembler.jne(restoreAndReplay);
            assembler.mov(eax, __dword_ptr[rcx + RallyGenerationGlobalIdOffset]);
            assembler.test(eax, eax);
            assembler.je(globalIdMatches);
            assembler.cmp(
                __dword_ptr[rbx + UnitGlobalIdManagerOffset],
                eax);
            assembler.jne(restoreAndReplay);
            assembler.Label(ref globalIdMatches);
            assembler.mov(eax, __dword_ptr[rcx + RallyOwnerOffset]);
            assembler.cmp(
                __byte_ptr[rbx + UnitOwnerManagerOffset],
                al);
            assembler.jne(restoreAndReplay);
            assembler.movzx(eax, __word_ptr[rcx + RallyUnitTypeOffset]);
            assembler.cmp(
                __word_ptr[rbx + UnitTypeManagerOffset],
                ax);
            assembler.jne(restoreAndReplay);
            assembler.test(
                __word_ptr[rbx + UnitPathStateManagerOffset],
                2);
            assembler.je(restoreAndReplay);
            assembler.mov(
                ax,
                __word_ptr[rbx + UnitCurrentSpeedManagerOffset]);
            assembler.mov(
                __word_ptr[rbx + UnitCurrentSpeed2ManagerOffset],
                ax);

            assembler.Label(ref restoreAndReplay);
            assembler.pop(rcx);
            assembler.popfq();
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
        }

        private void GenerateCadenceFastPath(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress)
        {
            if (overwrittenInstructions.Length != 3 || returnAddress == 0)
            {
                throw new InvalidOperationException(
                    "Unexpected common movement-cadence hook boundary.");
            }

            Label replayVanilla = assembler.CreateLabel("cadenceReplayVanilla");
            Label trySynchronization =
                assembler.CreateLabel("cadenceTrySynchronization");
            Label clearRallyAndTrySynchronization =
                assembler.CreateLabel("cadenceClearRallyAndTrySynchronization");
            Label rallyGlobalMatches =
                assembler.CreateLabel("cadenceRallyGlobalMatches");
            Label rallyIdentityMatches =
                assembler.CreateLabel("cadenceRallyIdentityMatches");
            Label rallyHandled = assembler.CreateLabel("cadenceRallyHandled");
            Label rallyPathActive =
                assembler.CreateLabel("cadenceRallyPathActive");
            Label rallyPreviouslyObserved =
                assembler.CreateLabel("cadenceRallyPreviouslyObserved");
            Label rallyCaptureGlobalDone =
                assembler.CreateLabel("cadenceRallyCaptureGlobalDone");
            Label rallyTargetAccepted =
                assembler.CreateLabel("cadenceRallyTargetAccepted");
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            Label rallyAbortDead =
                assembler.CreateLabel("cadenceRallyDiagnosticsAbortDead");
            Label rallyAbortGeneration =
                assembler.CreateLabel("cadenceRallyDiagnosticsAbortGeneration");
            Label rallyAbortOwner =
                assembler.CreateLabel("cadenceRallyDiagnosticsAbortOwner");
            Label rallyAbortType =
                assembler.CreateLabel("cadenceRallyDiagnosticsAbortType");
            Label rallyAbortTarget =
                assembler.CreateLabel("cadenceRallyDiagnosticsAbortTarget");
            // RALLY_ANIMATION_DIAGNOSTICS_END
            Label synchronizationRunning =
                assembler.CreateLabel("cadenceSynchronizationRunning");
            Label synchronizationWalking =
                assembler.CreateLabel("cadenceSynchronizationWalking");
            Label applyRallyProfile =
                assembler.CreateLabel("cadenceApplyRallyProfile");
            Label rallyProfileAllowed =
                assembler.CreateLabel("cadenceRallyProfileAllowed");
            Label applyRunningProfile =
                assembler.CreateLabel("cadenceApplyRunningProfile");
            Label applyWalkingProfile =
                assembler.CreateLabel("cadenceApplyWalkingProfile");

            // RAX, RCX and R10 are safe scratch registers: the three exact
            // displaced Vanilla instructions overwrite EAX, ECX and R10D.
            // No other register, stack value or incoming flag is changed.
            assembler.pushfq();
            assembler.mov(rax, unchecked((ulong)rallyEnabledFlag));
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(trySynchronization);
            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.cmp(eax, 1);
            assembler.jl(trySynchronization);
            assembler.cmp(eax, MaximumTrackedUnitId);
            assembler.jg(trySynchronization);
            assembler.imul(rax, rax, RallyEntrySize);
            assembler.mov(rcx, unchecked((ulong)rallyEntries));
            assembler.add(rax, rcx);
            assembler.cmp(__byte_ptr[rax + RallyActiveOffset], 0);
            assembler.je(trySynchronization);
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            assembler.movzx(
                ecx,
                __word_ptr[rax + RallyDiagnosticsCadenceVisitsOffset]);
            assembler.add(ecx, 1);
            assembler.mov(
                __word_ptr[rax + RallyDiagnosticsCadenceVisitsOffset],
                cx);
            EmitRallyDiagnosticsSnapshot(assembler);
            // RALLY_ANIMATION_DIAGNOSTICS_END
            assembler.cmp(
                __word_ptr[r8 + UnitAliveStateManagerOffset],
                (int)AliveState.IsAlive);
            assembler.jne(rallyAbortDead);

            assembler.mov(ecx, __dword_ptr[rax + RallyGenerationGlobalIdOffset]);
            assembler.test(ecx, ecx);
            assembler.je(rallyGlobalMatches);
            assembler.cmp(
                __dword_ptr[r8 + UnitGlobalIdManagerOffset],
                ecx);
            assembler.jne(rallyAbortGeneration);
            assembler.Label(ref rallyGlobalMatches);
            assembler.mov(ecx, __dword_ptr[rax + RallyOwnerOffset]);
            assembler.cmp(
                __byte_ptr[r8 + UnitOwnerManagerOffset],
                cl);
            assembler.jne(rallyAbortOwner);
            assembler.movzx(ecx, __word_ptr[rax + RallyUnitTypeOffset]);
            assembler.cmp(
                __word_ptr[r8 + UnitTypeManagerOffset],
                cx);
            assembler.je(rallyIdentityMatches);
            assembler.cmp(
                __word_ptr[r8 + UnitAiStateManagerOffset],
                UnitInitializationAiState);
            assembler.je(rallyHandled);
            assembler.cmp(
                __word_ptr[r8 + UnitTransformTypeManagerOffset],
                cx);
            assembler.je(rallyHandled);
            assembler.jmp(rallyAbortType);

            assembler.Label(ref rallyIdentityMatches);
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            assembler.or(
                __byte_ptr[rax + RallyDiagnosticsStatusOffset],
                RallyDiagnosticsIdentityConfirmed);
            // RALLY_ANIMATION_DIAGNOSTICS_END
            assembler.test(
                __word_ptr[r8 + UnitPathStateManagerOffset],
                2);
            assembler.jne(rallyPathActive);
            assembler.mov(__byte_ptr[rax + RallyMovingOffset], 0);
            assembler.jmp(rallyHandled);

            assembler.Label(ref rallyPathActive);
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            assembler.or(
                __byte_ptr[rax + RallyDiagnosticsStatusOffset],
                RallyDiagnosticsPathObserved);
            assembler.mov(
                ecx,
                __dword_ptr[r8 + UnitAnimationStateManagerOffset]);
            assembler.mov(
                __dword_ptr[
                    rax + RallyDiagnosticsObservedAnimationOffset],
                ecx);
            // RALLY_ANIMATION_DIAGNOSTICS_END
            assembler.cmp(__byte_ptr[rax + RallyObservedOffset], 0);
            assembler.jne(rallyPreviouslyObserved);
            assembler.mov(__byte_ptr[rax + RallyObservedOffset], 1);
            assembler.cmp(
                __dword_ptr[rax + RallyGenerationGlobalIdOffset],
                0);
            assembler.jne(rallyCaptureGlobalDone);
            assembler.mov(
                ecx,
                __dword_ptr[r8 + UnitGlobalIdManagerOffset]);
            assembler.mov(
                __dword_ptr[rax + RallyGenerationGlobalIdOffset],
                ecx);
            assembler.Label(ref rallyCaptureGlobalDone);
            assembler.jmp(rallyTargetAccepted);

            assembler.Label(ref rallyPreviouslyObserved);
            assembler.cmp(__byte_ptr[rax + RallyMovingOffset], 0);
            assembler.jne(rallyTargetAccepted);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetXManagerOffset]);
            assembler.cmp(cx, __word_ptr[rax + RallyTargetXOffset]);
            assembler.jne(rallyAbortTarget);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetYManagerOffset]);
            assembler.cmp(cx, __word_ptr[rax + RallyTargetYOffset]);
            assembler.jne(rallyAbortTarget);

            assembler.Label(ref rallyTargetAccepted);
            assembler.mov(__byte_ptr[rax + RallyMovingOffset], 1);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetXManagerOffset]);
            assembler.mov(__word_ptr[rax + RallyTargetXOffset], cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetYManagerOffset]);
            assembler.mov(__word_ptr[rax + RallyTargetYOffset], cx);
            assembler.jmp(applyRallyProfile);

            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            assembler.Label(ref rallyAbortDead);
            assembler.mov(
                __byte_ptr[rax + RallyDiagnosticsAbortReasonOffset],
                RallyDiagnosticsAbortDead);
            assembler.jmp(clearRallyAndTrySynchronization);
            assembler.Label(ref rallyAbortGeneration);
            assembler.mov(
                __byte_ptr[rax + RallyDiagnosticsAbortReasonOffset],
                RallyDiagnosticsAbortGeneration);
            assembler.jmp(clearRallyAndTrySynchronization);
            assembler.Label(ref rallyAbortOwner);
            assembler.mov(
                __byte_ptr[rax + RallyDiagnosticsAbortReasonOffset],
                RallyDiagnosticsAbortOwner);
            assembler.jmp(clearRallyAndTrySynchronization);
            assembler.Label(ref rallyAbortType);
            assembler.mov(
                __byte_ptr[rax + RallyDiagnosticsAbortReasonOffset],
                RallyDiagnosticsAbortType);
            assembler.jmp(clearRallyAndTrySynchronization);
            assembler.Label(ref rallyAbortTarget);
            assembler.mov(
                __byte_ptr[rax + RallyDiagnosticsAbortReasonOffset],
                RallyDiagnosticsAbortTarget);
            assembler.jmp(clearRallyAndTrySynchronization);
            // RALLY_ANIMATION_DIAGNOSTICS_END

            assembler.Label(ref clearRallyAndTrySynchronization);
            assembler.mov(__byte_ptr[rax + RallyActiveOffset], 0);

            assembler.Label(ref trySynchronization);
            assembler.mov(rax, unchecked((ulong)synchronizationEnabledFlag));
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(replayVanilla);
            assembler.cmp(
                __word_ptr[r8 + UnitAliveStateManagerOffset],
                (int)AliveState.IsAlive);
            assembler.jne(replayVanilla);
            assembler.movzx(eax, __word_ptr[r8 + UnitTribeIdManagerOffset]);
            assembler.cmp(eax, 1);
            assembler.jl(replayVanilla);
            assembler.cmp(eax, MaximumTrackedTribeId);
            assembler.jg(replayVanilla);
            assembler.imul(rax, rax, SynchronizationEntrySize);
            assembler.mov(rcx, unchecked((ulong)synchronizationEntries));
            assembler.add(rax, rcx);
            assembler.cmp(
                __byte_ptr[rax + SynchronizationActiveOffset],
                0);
            assembler.je(replayVanilla);
            assembler.cmp(
                __byte_ptr[rax + SynchronizationCadenceOffset],
                2);
            assembler.je(synchronizationRunning);
            assembler.jmp(synchronizationWalking);

            assembler.Label(ref synchronizationRunning);
            assembler.movzx(
                ecx,
                __word_ptr[rax + SynchronizationBonusOffset]);
            assembler.mov(
                __word_ptr[r8 + UnitSpeedBonusOffset],
                cx);
            assembler.jmp(applyRunningProfile);

            assembler.Label(ref synchronizationWalking);
            assembler.mov(
                __word_ptr[r8 + UnitSpeedBonusOffset],
                0);
            assembler.jmp(applyWalkingProfile);

            assembler.Label(ref applyRallyProfile);
            assembler.movzx(eax, __word_ptr[r8 + UnitTypeManagerOffset]);
            assembler.cmp(eax, (int)eChimps.CHIMP_TYPE_SPEARMAN);
            assembler.jne(rallyProfileAllowed);
            assembler.mov(rax, improvedSpearmanFlagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(rallyHandled);
            assembler.Label(ref rallyProfileAllowed);
            EmitProfileAddress(assembler, rallyHandled);
            EmitRallyRunningMappings(assembler, rallyHandled);

            assembler.Label(ref applyRunningProfile);
            EmitProfileAddress(assembler, replayVanilla);
            EmitStateMappings(
                assembler,
                NativeProfileRunningCountOffset,
                NativeProfileRunningMappingsOffset,
                replayVanilla,
                applySpeedBonus: false);

            assembler.Label(ref applyWalkingProfile);
            EmitProfileAddress(assembler, replayVanilla);
            EmitStateMappings(
                assembler,
                NativeProfileWalkingCountOffset,
                NativeProfileWalkingMappingsOffset,
                replayVanilla,
                applySpeedBonus: false);

            assembler.Label(ref rallyHandled);
            assembler.jmp(replayVanilla);

            assembler.Label(ref replayVanilla);
            assembler.popfq();
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
        }

        private void EmitProfileAddress(
            Assembler assembler,
            Label unavailable)
        {
            assembler.movzx(
                eax,
                __word_ptr[r8 + UnitTypeManagerOffset]);
            assembler.cmp(eax, (int)eChimps.CHIMP_NUM_TYPES);
            assembler.jae(unavailable);
            assembler.imul(rax, rax, NativeProfileSize);
            assembler.mov(rcx, unchecked((ulong)nativeProfiles));
            assembler.add(rax, rcx);
        }

        private static void EmitStateMappings(
            Assembler assembler,
            int countOffset,
            int mappingsOffset,
            Label completed,
            bool applySpeedBonus)
        {
            assembler.movzx(r10d, __byte_ptr[rax + countOffset]);
            for (int index = 0;
                 index < MaximumNativeTransitionMappings;
                 index++)
            {
                Label next = assembler.CreateLabel(
                    $"cadenceMappingNext{mappingsOffset}_{index}");
                assembler.cmp(r10d, index + 1);
                assembler.jl(completed);
                int mappingOffset =
                    mappingsOffset +
                    index * NativeTransitionMappingSize;
                assembler.mov(
                    ecx,
                    __dword_ptr[r8 + UnitAnimationStateManagerOffset]);
                assembler.cmp(ecx, __dword_ptr[rax + mappingOffset]);
                assembler.jne(next);
                assembler.mov(ecx, __dword_ptr[
                    rax + mappingOffset + sizeof(uint)]);
                assembler.mov(
                    __dword_ptr[r8 + UnitAnimationStateManagerOffset],
                    ecx);
                if (applySpeedBonus)
                {
                    assembler.movzx(
                        ecx,
                        __word_ptr[rax + NativeProfileBonusOffset]);
                    assembler.mov(
                        __word_ptr[r8 + UnitSpeedBonusOffset],
                        cx);
                }
                assembler.jmp(completed);
                assembler.Label(ref next);
            }

            assembler.jmp(completed);
        }

        private void EmitRallyRunningMappings(
            Assembler assembler,
            Label completed)
        {
            Label fallback = assembler.CreateLabel("cadenceRallyFallback");
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            EmitRallyDiagnosticsProfileMarker(assembler);
            // RALLY_ANIMATION_DIAGNOSTICS_END
            assembler.movzx(
                r10d,
                __byte_ptr[rax + NativeProfileRunningCountOffset]);
            for (int index = 0;
                 index < MaximumNativeTransitionMappings;
                 index++)
            {
                bool isLast = index == MaximumNativeTransitionMappings - 1;
                Label next = isLast
                    ? fallback
                    : assembler.CreateLabel(
                        $"cadenceRallyMappingNext{index}");
                assembler.cmp(r10d, index + 1);
                assembler.jl(fallback);
                int mappingOffset =
                    NativeProfileRunningMappingsOffset +
                    index * NativeTransitionMappingSize;
                assembler.mov(
                    ecx,
                    __dword_ptr[r8 + UnitAnimationStateManagerOffset]);
                assembler.cmp(ecx, __dword_ptr[rax + mappingOffset]);
                assembler.jne(next);
                assembler.mov(ecx, __dword_ptr[
                    rax + mappingOffset + sizeof(uint)]);
                assembler.mov(
                    __dword_ptr[r8 + UnitAnimationStateManagerOffset],
                    ecx);
                assembler.movzx(
                    ecx,
                    __word_ptr[rax + NativeProfileBonusOffset]);
                assembler.mov(
                    __word_ptr[r8 + UnitSpeedBonusOffset],
                    cx);
                // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
                EmitRallyDiagnosticsWriteMarker(assembler);
                // RALLY_ANIMATION_DIAGNOSTICS_END
                assembler.jmp(completed);
                if (!isLast)
                    assembler.Label(ref next);
            }

            assembler.Label(ref fallback);
            assembler.cmp(
                __byte_ptr[
                    rax + NativeProfileAllowRallyFallbackOffset],
                0);
            assembler.je(completed);
            assembler.cmp(
                __byte_ptr[rax + NativeProfileRunningStateCountOffset],
                1);
            assembler.jne(completed);
            assembler.mov(
                ecx,
                __dword_ptr[
                    rax + NativeProfileSoleRunningStateOffset]);
            assembler.mov(
                __dword_ptr[r8 + UnitAnimationStateManagerOffset],
                ecx);
            assembler.movzx(
                ecx,
                __word_ptr[rax + NativeProfileBonusOffset]);
            assembler.mov(
                __word_ptr[r8 + UnitSpeedBonusOffset],
                cx);
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            EmitRallyDiagnosticsWriteMarker(assembler);
            // RALLY_ANIMATION_DIAGNOSTICS_END
            assembler.jmp(completed);
        }

        // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
        private void EmitRallyDiagnosticsProfileMarker(Assembler assembler)
        {
            assembler.mov(r10, rax);
            EmitRallyDiagnosticsEntryAddress(assembler);
            assembler.or(
                __byte_ptr[rax + RallyDiagnosticsStatusOffset],
                RallyDiagnosticsProfileResolved);
            assembler.mov(rax, r10);
        }

        private void EmitRallyDiagnosticsSnapshot(Assembler assembler)
        {
            Label compareLast = assembler.CreateLabel(
                "cadenceRallyDiagnosticsCompareLast");
            Label stateChanged = assembler.CreateLabel(
                "cadenceRallyDiagnosticsStateChanged");
            Label updateLast = assembler.CreateLabel(
                "cadenceRallyDiagnosticsUpdateLast");

            // Preserve the already resolved rally-entry pointer in R10. RAX,
            // RCX and R10 are overwritten by the displaced Vanilla block.
            assembler.mov(r10, rax);
            assembler.test(
                __byte_ptr[r10 + RallyDiagnosticsStatusOffset],
                RallyDiagnosticsSnapshotCaptured);
            assembler.jne(compareLast);
            assembler.or(
                __byte_ptr[r10 + RallyDiagnosticsStatusOffset],
                RallyDiagnosticsSnapshotCaptured);

            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.mov(
                __dword_ptr[r10 + RallyDiagnosticsFirstCurrentUnitIdOffset],
                eax);
            assembler.mov(ecx, __dword_ptr[r8 + UnitGlobalIdManagerOffset]);
            assembler.mov(
                __dword_ptr[r10 + RallyDiagnosticsFirstGlobalIdOffset],
                ecx);
            assembler.mov(
                ecx,
                __dword_ptr[r8 + UnitAnimationStateManagerOffset]);
            assembler.mov(
                __dword_ptr[r10 + RallyDiagnosticsFirstAnimationOffset],
                ecx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitAliveStateManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsFirstAliveStateOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTypeManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsFirstUnitTypeOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitAiStateManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsFirstAiStateOffset],
                cx);
            assembler.movzx(
                ecx,
                __word_ptr[r8 + UnitTransformTypeManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsFirstTransformTypeOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitPathStateManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsFirstPathFlagsOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetXManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsFirstTargetXOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetYManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsFirstTargetYOffset],
                cx);
            assembler.movzx(ecx, __byte_ptr[r8 + UnitOwnerManagerOffset]);
            assembler.mov(
                __byte_ptr[r10 + RallyDiagnosticsFirstOwnerOffset],
                cl);
            assembler.jmp(updateLast);

            assembler.Label(ref compareLast);
            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.cmp(
                __dword_ptr[r10 + RallyDiagnosticsLastCurrentUnitIdOffset],
                eax);
            assembler.jne(stateChanged);
            assembler.mov(ecx, __dword_ptr[r8 + UnitGlobalIdManagerOffset]);
            assembler.cmp(
                __dword_ptr[r10 + RallyDiagnosticsLastGlobalIdOffset],
                ecx);
            assembler.jne(stateChanged);
            assembler.mov(
                ecx,
                __dword_ptr[r8 + UnitAnimationStateManagerOffset]);
            assembler.cmp(
                __dword_ptr[r10 + RallyDiagnosticsLastAnimationOffset],
                ecx);
            assembler.jne(stateChanged);
            assembler.movzx(ecx, __word_ptr[r8 + UnitAliveStateManagerOffset]);
            assembler.cmp(
                __word_ptr[r10 + RallyDiagnosticsLastAliveStateOffset],
                cx);
            assembler.jne(stateChanged);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTypeManagerOffset]);
            assembler.cmp(
                __word_ptr[r10 + RallyDiagnosticsLastUnitTypeOffset],
                cx);
            assembler.jne(stateChanged);
            assembler.movzx(ecx, __word_ptr[r8 + UnitAiStateManagerOffset]);
            assembler.cmp(
                __word_ptr[r10 + RallyDiagnosticsLastAiStateOffset],
                cx);
            assembler.jne(stateChanged);
            assembler.movzx(
                ecx,
                __word_ptr[r8 + UnitTransformTypeManagerOffset]);
            assembler.cmp(
                __word_ptr[r10 + RallyDiagnosticsLastTransformTypeOffset],
                cx);
            assembler.jne(stateChanged);
            assembler.movzx(ecx, __word_ptr[r8 + UnitPathStateManagerOffset]);
            assembler.cmp(
                __word_ptr[r10 + RallyDiagnosticsLastPathFlagsOffset],
                cx);
            assembler.jne(stateChanged);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetXManagerOffset]);
            assembler.cmp(
                __word_ptr[r10 + RallyDiagnosticsLastTargetXOffset],
                cx);
            assembler.jne(stateChanged);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetYManagerOffset]);
            assembler.cmp(
                __word_ptr[r10 + RallyDiagnosticsLastTargetYOffset],
                cx);
            assembler.jne(stateChanged);
            assembler.movzx(ecx, __byte_ptr[r8 + UnitOwnerManagerOffset]);
            assembler.cmp(
                __byte_ptr[r10 + RallyDiagnosticsLastOwnerOffset],
                cl);
            assembler.je(updateLast);

            assembler.Label(ref stateChanged);
            assembler.movzx(
                ecx,
                __word_ptr[r10 + RallyDiagnosticsTransitionCountOffset]);
            assembler.add(ecx, 1);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsTransitionCountOffset],
                cx);

            assembler.Label(ref updateLast);
            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.mov(
                __dword_ptr[r10 + RallyDiagnosticsLastCurrentUnitIdOffset],
                eax);
            assembler.mov(ecx, __dword_ptr[r8 + UnitGlobalIdManagerOffset]);
            assembler.mov(
                __dword_ptr[r10 + RallyDiagnosticsLastGlobalIdOffset],
                ecx);
            assembler.mov(
                ecx,
                __dword_ptr[r8 + UnitAnimationStateManagerOffset]);
            assembler.mov(
                __dword_ptr[r10 + RallyDiagnosticsLastAnimationOffset],
                ecx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitAliveStateManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsLastAliveStateOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTypeManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsLastUnitTypeOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitAiStateManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsLastAiStateOffset],
                cx);
            assembler.movzx(
                ecx,
                __word_ptr[r8 + UnitTransformTypeManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsLastTransformTypeOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitPathStateManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsLastPathFlagsOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetXManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsLastTargetXOffset],
                cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetYManagerOffset]);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsLastTargetYOffset],
                cx);
            assembler.movzx(ecx, __byte_ptr[r8 + UnitOwnerManagerOffset]);
            assembler.mov(
                __byte_ptr[r10 + RallyDiagnosticsLastOwnerOffset],
                cl);
            assembler.movzx(
                ecx,
                __word_ptr[r10 + RallyDiagnosticsSnapshotCountOffset]);
            assembler.add(ecx, 1);
            assembler.mov(
                __word_ptr[r10 + RallyDiagnosticsSnapshotCountOffset],
                cx);
            assembler.mov(rax, r10);
        }

        private void EmitRallyDiagnosticsWriteMarker(Assembler assembler)
        {
            EmitRallyDiagnosticsEntryAddress(assembler);
            assembler.or(
                __byte_ptr[rax + RallyDiagnosticsStatusOffset],
                RallyDiagnosticsCadenceWritten);
            assembler.mov(
                ecx,
                __dword_ptr[r8 + UnitAnimationStateManagerOffset]);
            assembler.mov(
                __dword_ptr[
                    rax + RallyDiagnosticsWrittenAnimationOffset],
                ecx);
            assembler.movzx(
                ecx,
                __word_ptr[r8 + UnitSpeedBonusOffset]);
            assembler.mov(
                __word_ptr[rax + RallyDiagnosticsWrittenBonusOffset],
                cx);
        }

        private void EmitRallyDiagnosticsEntryAddress(Assembler assembler)
        {
            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.imul(rax, rax, RallyEntrySize);
            assembler.mov(rcx, unchecked((ulong)rallyEntries));
            assembler.add(rax, rcx);
        }
        // RALLY_ANIMATION_DIAGNOSTICS_END

        private void PublishNativeProfiles()
        {
            foreach (KeyValuePair<eChimps, AnimationTransitions> pair in
                     animationTransitionsByType)
            {
                int unitType = (int)pair.Key;
                if (unitType < 0 || unitType >= (int)eChimps.CHIMP_NUM_TYPES)
                    continue;

                byte* profile = nativeProfiles + unitType * NativeProfileSize;
                AnimationTransitions transitions = pair.Value;
                List<KeyValuePair<uint, uint>> runningMappings =
                    transitions.CreateRunningMappings();
                List<KeyValuePair<uint, uint>> walkingMappings =
                    transitions.CreateWalkingMappings();
                if (runningMappings.Count > MaximumNativeTransitionMappings ||
                    walkingMappings.Count > MaximumNativeTransitionMappings)
                {
                    throw new InvalidOperationException(
                        $"Unit type {pair.Key} exceeds the audited native " +
                        $"cadence-profile capacity.");
                }

                profile[NativeProfileRunningCountOffset] =
                    checked((byte)runningMappings.Count);
                profile[NativeProfileWalkingCountOffset] =
                    checked((byte)walkingMappings.Count);
                profile[NativeProfileAllowRallyFallbackOffset] =
                    transitions.AllowSoleStateFallbackForRally
                        ? (byte)1
                        : (byte)0;
                profile[NativeProfileRunningStateCountOffset] =
                    checked((byte)transitions.RunningStateCount);
                ushort nativeRunningSpeedBonus =
                    transitions.NativeRunningSpeedBonus ?? 0;
                switch (pair.Key)
                {
                    case eChimps.CHIMP_TYPE_KNIGHT:
                    case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                    case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                    case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                        nativeRunningSpeedBonus =
                            GameUnitManagerAPI.Instance
                                .GetDefaultCavalryRunSpeedBonus(pair.Key);
                        break;
                }
                *(ushort*)(profile + NativeProfileBonusOffset) =
                    nativeRunningSpeedBonus;
                *(uint*)(profile + NativeProfileSoleRunningStateOffset) =
                    transitions.SoleRunningState;
                WriteNativeMappings(
                    profile + NativeProfileRunningMappingsOffset,
                    runningMappings);
                WriteNativeMappings(
                    profile + NativeProfileWalkingMappingsOffset,
                    walkingMappings);
            }
        }

        private static void WriteNativeMappings(
            byte* destination,
            List<KeyValuePair<uint, uint>> mappings)
        {
            for (int index = 0; index < mappings.Count; index++)
            {
                byte* mapping =
                    destination + index * NativeTransitionMappingSize;
                *(uint*)mapping = mappings[index].Key;
                *(uint*)(mapping + sizeof(uint)) = mappings[index].Value;
            }
        }

        private static ulong ResolveCurrentUnitIdAddress(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int dispatchRva)
        {
            const int loadOffset = 8;
            const int displacementOffset = loadOffset + 2;
            const int instructionEndOffset = loadOffset + 6;
            if (dispatchRva < 0 ||
                dispatchRva + instructionEndOffset > memory.Length ||
                memory[dispatchRva + loadOffset] != 0x8B ||
                memory[dispatchRva + loadOffset + 1] != 0x15)
            {
                throw new InvalidOperationException(
                    "The audited current-unit-ID load is unavailable.");
            }

            int displacement = BitConverter.ToInt32(
                memory.Slice(displacementOffset + dispatchRva, 4).ToArray(),
                0);
            ulong address = unchecked((ulong)(
                (long)(libraryBase +
                       unchecked((ulong)(dispatchRva + instructionEndOffset))) +
                displacement));
            ulong moduleEnd = libraryBase + unchecked((ulong)memory.Length);
            if (address < libraryBase || address + sizeof(int) > moduleEnd)
            {
                throw new InvalidOperationException(
                    "The current-unit-ID global is outside the game module.");
            }

            return address;
        }

        private static ulong ResolveImprovedSpearmanFlagAddress(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong decisionAddress)
        {
            int decisionRva = checked((int)(decisionAddress - libraryBase));
            if (decisionRva < 0 ||
                decisionRva + ImprovedSpearmanFlagInstructionEndOffset >
                    memory.Length)
            {
                throw new InvalidOperationException(
                    "The Spearman movement decision is outside the module.");
            }

            int displacement = BitConverter.ToInt32(
                memory.Slice(
                    decisionRva + ImprovedSpearmanFlagDisplacementOffset,
                    4).ToArray(),
                0);
            ulong flagAddress = unchecked((ulong)(
                (long)(decisionAddress +
                       ImprovedSpearmanFlagInstructionEndOffset) +
                displacement));
            ulong moduleEnd = libraryBase + unchecked((ulong)memory.Length);
            if (flagAddress < libraryBase ||
                flagAddress + sizeof(int) > moduleEnd)
            {
                throw new InvalidOperationException(
                    "The Improved Spearman flag is outside the module.");
            }

            return flagAddress;
        }

        private static byte* AllocateZeroed(int byteCount)
        {
            IntPtr allocation = Marshal.AllocHGlobal(byteCount);
            if (allocation == IntPtr.Zero)
                throw new OutOfMemoryException();
            byte* bytes = (byte*)allocation.ToPointer();
            ZeroMemory(bytes, byteCount);
            return bytes;
        }

        private static void ZeroMemory(byte* bytes, int byteCount)
        {
            for (int index = 0; index < byteCount; index++)
                bytes[index] = 0;
        }

        private void FreeUnpublishedTables()
        {
            if (published)
                return;
            if (rallyEntries != null)
                Marshal.FreeHGlobal(new IntPtr(rallyEntries));
            if (synchronizationEntries != null)
                Marshal.FreeHGlobal(new IntPtr(synchronizationEntries));
            if (nativeProfiles != null)
                Marshal.FreeHGlobal(new IntPtr(nativeProfiles));
            if (rallyEnabledFlag != null)
                Marshal.FreeHGlobal(new IntPtr(rallyEnabledFlag));
            if (synchronizationEnabledFlag != null)
                Marshal.FreeHGlobal(new IntPtr(synchronizationEnabledFlag));
        }

        private void ValidatePreTerrainSpeedAdjustmentHook(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva)
        {
            if (hookRva < 0 ||
                hookRva + PreTerrainSpeedAdjustmentHookLength + 16 >
                    memory.Length ||
                CalculateMovementSpeedFunctionRva < 0 ||
                CalculateMovementSpeedFunctionRva +
                    CalculateMovementSpeedFunctionLength > memory.Length)
            {
                throw new InvalidOperationException(
                    "The pre-terrain speed hook or its containing function " +
                    "is outside the game module.");
            }

            ulong hookStart = libraryBase + unchecked((ulong)hookRva);
            var hookDecoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(
                    memory.Slice(hookRva, 32).ToArray()));
            hookDecoder.IP = hookStart;
            var overwritten = new List<Instruction>(4);
            int overwrittenLength = 0;
            while (overwrittenLength < PreTerrainSpeedAdjustmentHookLength)
            {
                Instruction instruction = hookDecoder.Decode();
                if (instruction.IsInvalid)
                {
                    throw new InvalidOperationException(
                        "The pre-terrain speed hook span contains an " +
                        "invalid instruction.");
                }

                overwritten.Add(instruction);
                overwrittenLength += instruction.Length;
            }

            ulong hookEnd =
                hookStart + unchecked((ulong)overwrittenLength);
            bool expectedSpan =
                overwrittenLength == PreTerrainSpeedAdjustmentHookLength &&
                overwritten.Count == 4 &&
                overwritten[0].Mnemonic == Mnemonic.Movzx &&
                NormalizeRegister(overwritten[0].Op0Register) == Register.RAX &&
                NormalizeRegister(overwritten[0].MemoryBase) == Register.RBX &&
                overwritten[0].MemoryDisplacement64 == 0x6C8 &&
                overwritten[1].Mnemonic == Mnemonic.Test &&
                NormalizeRegister(overwritten[1].Op0Register) == Register.R9 &&
                NormalizeRegister(overwritten[1].Op1Register) == Register.R9 &&
                overwritten[2].Mnemonic == Mnemonic.Je &&
                overwritten[2].NearBranchTarget ==
                    libraryBase + 0x19B554UL &&
                overwritten[3].Mnemonic == Mnemonic.Cmp &&
                overwritten[3].Op0Kind == OpKind.Register &&
                overwritten[3].Op0Register == Register.AL &&
                IsImmediate(overwritten[3].Op1Kind) &&
                overwritten[3].GetImmediate(1) == 0x18;
            if (!expectedSpan)
            {
                throw new InvalidOperationException(
                    "The pre-terrain speed hook instruction span no longer " +
                    "matches its audited semantics.");
            }

            int functionRva = CalculateMovementSpeedFunctionRva;
            var functionDecoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(
                    memory.Slice(
                        functionRva,
                        CalculateMovementSpeedFunctionLength).ToArray()));
            functionDecoder.IP =
                libraryBase + unchecked((ulong)functionRva);
            ulong functionEnd = functionDecoder.IP +
                unchecked((ulong)CalculateMovementSpeedFunctionLength);
            while (functionDecoder.IP < functionEnd)
            {
                Instruction instruction = functionDecoder.Decode();
                if (instruction.IsInvalid)
                {
                    throw new InvalidOperationException(
                        "The movement-speed function contains an invalid " +
                        "instruction before its audited end.");
                }

                if (instruction.FlowControl == FlowControl.IndirectBranch)
                {
                    throw new InvalidOperationException(
                        "The movement-speed function gained an indirect " +
                        "branch; the hook span requires a new control-flow audit.");
                }

                bool isDirectControlTransfer =
                    instruction.FlowControl == FlowControl.ConditionalBranch ||
                    instruction.FlowControl == FlowControl.UnconditionalBranch ||
                    instruction.FlowControl == FlowControl.Call;
                if (!isDirectControlTransfer ||
                    !IsNearBranch(instruction.Op0Kind))
                {
                    continue;
                }

                ulong target = instruction.NearBranchTarget;
                bool sourceOutsideSpan =
                    instruction.IP < hookStart || instruction.IP >= hookEnd;
                if (sourceOutsideSpan &&
                    target > hookStart && target < hookEnd)
                {
                    throw new InvalidOperationException(
                        $"Control flow from RVA 0x" +
                        $"{instruction.IP - libraryBase:X} enters the middle " +
                        $"of the speed hook at RVA 0x{target - libraryBase:X}.");
                }
            }

            TroopMovementFix3ModLog.Debug(
                log,
                $"Pre-terrain speed hook span validated: " +
                $"startRva=0x{hookStart - libraryBase:X}, " +
                $"endRva=0x{hookEnd - libraryBase:X}, " +
                $"instructionLengths=" +
                $"{string.Join(",", overwritten.ConvertAll(x => x.Length))}, " +
                $"nextRva=0x{hookEnd - libraryBase:X}.");
        }

        private static void ValidateMovementCadenceHook(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva)
        {
            const int hookLength = 23;
            if (hookRva < 0 || hookRva + hookLength > memory.Length)
            {
                throw new InvalidOperationException(
                    "The movement-cadence hook is outside the game module.");
            }

            var decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(
                    memory.Slice(hookRva, hookLength).ToArray()));
            decoder.IP = libraryBase + unchecked((ulong)hookRva);
            var instructions = new List<Instruction>(3);
            int decodedLength = 0;
            while (decodedLength < hookLength)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid)
                {
                    throw new InvalidOperationException(
                        "The movement-cadence hook contains an invalid instruction.");
                }
                instructions.Add(instruction);
                decodedLength += instruction.Length;
            }

            bool valid = decodedLength == hookLength &&
                instructions.Count == 3 &&
                instructions[0].Mnemonic == Mnemonic.Movsx &&
                NormalizeRegister(instructions[0].Op0Register) == Register.RAX &&
                NormalizeRegister(instructions[0].MemoryBase) == Register.R8 &&
                instructions[0].MemoryDisplacement64 == UnitSpeedBonusOffset &&
                instructions[1].Mnemonic == Mnemonic.Movsx &&
                NormalizeRegister(instructions[1].Op0Register) == Register.RCX &&
                NormalizeRegister(instructions[1].MemoryBase) == Register.R8 &&
                instructions[1].MemoryDisplacement64 == UnitCurrentSpeed2ManagerOffset &&
                instructions[2].Mnemonic == Mnemonic.Mov &&
                NormalizeRegister(instructions[2].Op0Register) == Register.R10 &&
                NormalizeRegister(instructions[2].MemoryBase) == Register.R8 &&
                instructions[2].MemoryDisplacement64 == 0x9A8;
            if (!valid)
            {
                throw new InvalidOperationException(
                    "The movement-cadence hook no longer matches its audited register and field contract.");
            }
        }

        private static bool IsNearBranch(OpKind kind)
        {
            return kind == OpKind.NearBranch16 ||
                   kind == OpKind.NearBranch32 ||
                   kind == OpKind.NearBranch64;
        }

        private void DiscoverRunningAnimationTransitions(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong dispatchInstructionAddress,
            bool referenceHashMatches)
        {
            int dispatchTableOffset = *(int*)(dispatchInstructionAddress + 4);
            ulong dispatchTableAddress =
                libraryBase + unchecked((uint)dispatchTableOffset);
            ulong moduleEnd =
                libraryBase + unchecked((ulong)memory.Length);
            int unitTypeCount = (int)eChimps.CHIMP_NUM_TYPES;

            if (dispatchTableAddress < libraryBase ||
                dispatchTableAddress +
                    unchecked((ulong)(unitTypeCount * sizeof(ulong))) >
                    moduleEnd)
            {
                throw new InvalidOperationException(
                    "The native unit-type update dispatch table is outside " +
                    "the game module.");
            }

            ulong* handlers = (ulong*)dispatchTableAddress;
            ulong[] handlerByType = new ulong[unitTypeCount];
            SortedSet<ulong> uniqueHandlers = new SortedSet<ulong>();

            for (int unitTypeValue = 0;
                 unitTypeValue < unitTypeCount;
                 unitTypeValue++)
            {
                ulong handler = handlers[unitTypeValue];
                if (handler < libraryBase || handler >= moduleEnd)
                    continue;

                handlerByType[unitTypeValue] = handler;
                uniqueHandlers.Add(handler);
            }

            List<ulong> sortedHandlers = new List<ulong>(uniqueHandlers);
            Dictionary<ulong, AnimationTransitions>
                animationTransitionsByHandler =
                    new Dictionary<ulong, AnimationTransitions>(
                        uniqueHandlers.Count);
            for (int unitTypeValue = 0;
                 unitTypeValue < unitTypeCount;
                 unitTypeValue++)
            {
                ulong handlerStart = handlerByType[unitTypeValue];
                if (handlerStart == 0)
                    continue;

                if (!animationTransitionsByHandler.TryGetValue(
                        handlerStart,
                        out AnimationTransitions animationTransitions))
                {
                    int handlerIndex =
                        sortedHandlers.BinarySearch(handlerStart);
                    ulong handlerEnd =
                        handlerIndex >= 0 &&
                        handlerIndex + 1 < sortedHandlers.Count
                            ? sortedHandlers[handlerIndex + 1]
                            : Math.Min(
                                handlerStart +
                                    MaximumUnitTypeHandlerLength,
                                moduleEnd);

                    if (handlerEnd <= handlerStart ||
                        handlerEnd - handlerStart >
                            MaximumUnitTypeHandlerLength)
                    {
                        handlerEnd = Math.Min(
                            handlerStart +
                                MaximumUnitTypeHandlerLength,
                            moduleEnd);
                    }

                    int handlerLength =
                        checked((int)(handlerEnd - handlerStart));
                    animationTransitions =
                        TryExtractIndividualFastMovementCadence(
                            handlerStart,
                            handlerLength,
                            libraryBase,
                            moduleEnd);

                    animationTransitionsByHandler.Add(
                        handlerStart,
                        animationTransitions);
                }

                if (animationTransitions != null)
                {
                    animationTransitionsByType[
                        (eChimps)unitTypeValue] =
                            animationTransitions;
                }
            }

            // These types exercise direct, register, branched and conditional
            // stores used by both European and mercenary handlers.
            // Missing one means the semantic decoder no longer understands
            // the installed DLL, so no partial native hook is committed.
            eChimps[] requiredTypes =
            {
                eChimps.CHIMP_TYPE_ARAB_BOW,
                eChimps.CHIMP_TYPE_ARAB_SLAVE,
                eChimps.CHIMP_TYPE_SPEARMAN,
                eChimps.CHIMP_TYPE_MACEMAN,
                eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
                eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
                eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
                eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
                eChimps.CHIMP_TYPE_BEDOUIN_SAPPER
            };
            foreach (eChimps requiredType in requiredTypes)
            {
                if (!animationTransitionsByType.ContainsKey(requiredType))
                {
                    throw new InvalidOperationException(
                        $"Native AIState 101 cadence could not be " +
                        $"extracted for {requiredType}.");
                }
            }

            if (referenceHashMatches)
                ApplyAuditedIndividualFastMovementProfiles();
        }

        private void ApplyAuditedIndividualFastMovementProfiles()
        {
            // These are the exact AIState-101 fast-movement pairs audited in
            // the reference DLL. They avoid merging mutually exclusive native
            // branches, notably the horse archer's conditional state 0x111.
            SetAuditedProfile(eChimps.CHIMP_TYPE_ARCHER, 1, 0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_SPEARMAN, 1, 0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_MACEMAN, 1, 0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_KNIGHT, 2, 0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_LADDERMAN, 1, 0x1);
            SetAuditedProfile(eChimps.CHIMP_TYPE_MONK, 1, 0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_ARAB_BOW, 1, 0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_ARAB_SLAVE, 1, 0x1);
            SetAuditedProfile(eChimps.CHIMP_TYPE_ARAB_SLINGER, 1, 0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_ARAB_HORSEMAN, 2, 0x1);
            SetAuditedProfile(
                eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
                2,
                0x81);
            SetAuditedProfile(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1, 0x5C1);
            SetAuditedProfile(
                eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
                1,
                0x101,
                0x181);
            SetAuditedProfile(
                eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
                2,
                0x1);
            SetAuditedProfile(eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, 1, 0x81);
        }

        private void SetAuditedProfile(
            eChimps unitType,
            ushort runningSpeedBonus,
            params uint[] runningStates)
        {
            animationTransitionsByType[unitType] =
                new AnimationTransitions(
                    new HashSet<uint>(runningStates),
                    runningSpeedBonus,
                    allowSoleStateFallbackForRally: true);
        }

        private AnimationTransitions TryExtractIndividualFastMovementCadence(
            ulong handlerStart,
            int handlerLength,
            ulong libraryBase,
            ulong moduleEnd)
        {
            byte[] codeBytes = new ReadOnlySpan<byte>(
                (byte*)handlerStart,
                handlerLength).ToArray();
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(codeBytes));
            decoder.IP = handlerStart;
            List<Instruction> instructions = new List<Instruction>(2048);
            Dictionary<ulong, int> instructionIndexByIp =
                new Dictionary<ulong, int>();

            while (decoder.IP < handlerStart + unchecked((ulong)handlerLength) &&
                   instructions.Count < 10000)
            {
                Instruction instruction = decoder.Decode();
                instructionIndexByIp[instruction.IP] = instructions.Count;
                instructions.Add(instruction);
            }

            // The public array begins at unit ID 1; native handlers address
            // that record as manager + 0x65C + one 0x490 unit stride.
            ulong unitManagerBase =
                unchecked((ulong)unitArray) -
                UnitRecordOffset -
                unchecked((ulong)sizeof(GameUnit));
            if (!TryResolveAiStateCaseTarget(
                    instructions,
                    libraryBase,
                    moduleEnd,
                    unitManagerBase,
                    IndividualFastMovementAiState,
                    out ulong caseTarget,
                    out int stateLoadIndex) ||
                !instructionIndexByIp.TryGetValue(
                    caseTarget,
                    out int caseStartIndex))
            {
                return null;
            }

            ulong caseEnd = Math.Min(
                caseTarget + MaximumCadenceCaseLength,
                handlerStart + unchecked((ulong)handlerLength));
            List<CadenceFieldWrite> animationWrites =
                new List<CadenceFieldWrite>();
            List<CadenceFieldWrite> speedBonusWrites =
                new List<CadenceFieldWrite>();
            Dictionary<Register, HashSet<long>> preSwitchConstants =
                FindPreSwitchConstants(instructions, stateLoadIndex);
            DiscoverReachableCadenceWrites(
                instructions,
                instructionIndexByIp,
                caseStartIndex,
                caseEnd,
                unitManagerBase,
                preSwitchConstants,
                animationWrites,
                speedBonusWrites);

            short fastestBonus = 0;
            foreach (CadenceFieldWrite write in speedBonusWrites)
            {
                foreach (long value in write.Values)
                {
                    short candidate = unchecked((short)(ushort)value);
                    if (candidate > fastestBonus && candidate <= 32)
                        fastestBonus = candidate;
                }
            }

            if (fastestBonus <= 0)
                return null;

            var runningStateDistances = new Dictionary<uint, int>();
            foreach (CadenceFieldWrite bonusWrite in speedBonusWrites)
            {
                if (!bonusWrite.ContainsSigned16(fastestBonus))
                    continue;

                int closestDistance = int.MaxValue;
                HashSet<uint> closestRunningStates =
                    new HashSet<uint>();
                foreach (CadenceFieldWrite animationWrite in animationWrites)
                {
                    int distance = Math.Abs(
                        animationWrite.InstructionIndex -
                        bonusWrite.InstructionIndex);
                    if (distance > MaximumCadencePairDistance ||
                        distance > closestDistance)
                        continue;

                    if (distance < closestDistance)
                    {
                        closestRunningStates.Clear();
                        closestDistance = distance;
                    }

                    foreach (long value in animationWrite.Values)
                    {
                        uint state = unchecked((uint)value);
                        if (state <= 0x10000)
                            closestRunningStates.Add(state);
                    }
                }

                foreach (uint state in closestRunningStates)
                {
                    if (!runningStateDistances.TryGetValue(
                            state,
                            out int previousDistance) ||
                        closestDistance < previousDistance)
                    {
                        runningStateDistances[state] = closestDistance;
                    }
                }
            }

            // Compilers often initialize the bonus before a later walking
            // branch. Prefer animation/bonus writes from the same small block
            // whenever such a direct native pair exists.
            bool hasDirectPair = false;
            foreach (int distance in runningStateDistances.Values)
            {
                if (distance <= DirectCadencePairDistance)
                {
                    hasDirectPair = true;
                    break;
                }
            }

            HashSet<uint> runningStates = new HashSet<uint>();
            foreach (KeyValuePair<uint, int> candidate in
                     runningStateDistances)
            {
                if (!hasDirectPair ||
                    candidate.Value <= DirectCadencePairDistance)
                {
                    runningStates.Add(candidate.Key);
                }
            }

            return runningStates.Count == 0
                ? null
                : new AnimationTransitions(
                    runningStates,
                    unchecked((ushort)fastestBonus),
                    allowSoleStateFallbackForRally: false);
        }

        private static bool TryResolveAiStateCaseTarget(
            List<Instruction> instructions,
            ulong libraryBase,
            ulong moduleEnd,
            ulong unitManagerBase,
            ushort aiState,
            out ulong caseTarget,
            out int stateLoadIndex)
        {
            caseTarget = 0;
            stateLoadIndex = -1;
            for (int candidateStateLoadIndex = 0;
                 candidateStateLoadIndex < instructions.Count;
                 candidateStateLoadIndex++)
            {
                Instruction stateLoad =
                    instructions[candidateStateLoadIndex];
                if ((stateLoad.Mnemonic != Mnemonic.Mov &&
                     stateLoad.Mnemonic != Mnemonic.Movzx &&
                     stateLoad.Mnemonic != Mnemonic.Movsx &&
                     stateLoad.Mnemonic != Mnemonic.Movsxd) ||
                    stateLoad.Op0Kind != OpKind.Register ||
                    stateLoad.Op1Kind != OpKind.Memory ||
                    !IsUnitFieldMemoryOperand(
                        instructions,
                        candidateStateLoadIndex,
                        0x918,
                        unitManagerBase))
                {
                    continue;
                }

                Register stateRegister = NormalizeRegister(
                    stateLoad.Op0Register);
                int searchEnd = Math.Min(
                    candidateStateLoadIndex + 80,
                    instructions.Count);
                for (int mapIndex = candidateStateLoadIndex + 1;
                     mapIndex < searchEnd;
                     mapIndex++)
                {
                    Instruction mapLoad = instructions[mapIndex];
                    if (mapLoad.Mnemonic != Mnemonic.Movzx ||
                        mapLoad.Op0Kind != OpKind.Register ||
                        mapLoad.Op1Kind != OpKind.Memory ||
                        NormalizeRegister(mapLoad.MemoryIndex) !=
                            stateRegister ||
                        !TryResolveMemoryTableAddress(
                            instructions,
                            mapIndex,
                            mapLoad,
                            out ulong stateMapAddress) ||
                        !IsModuleRange(
                            stateMapAddress + aiState,
                            1,
                            libraryBase,
                            moduleEnd))
                    {
                        continue;
                    }

                    byte compressedCase =
                        *((byte*)stateMapAddress + aiState);
                    Register compressedRegister = NormalizeRegister(
                        mapLoad.Op0Register);
                    int tableSearchEnd = Math.Min(
                        mapIndex + 12,
                        instructions.Count);
                    for (int tableIndex = mapIndex + 1;
                         tableIndex < tableSearchEnd;
                         tableIndex++)
                    {
                        Instruction tableLoad = instructions[tableIndex];
                        if ((tableLoad.Mnemonic != Mnemonic.Mov &&
                             tableLoad.Mnemonic != Mnemonic.Movsxd) ||
                            tableLoad.Op0Kind != OpKind.Register ||
                            tableLoad.Op1Kind != OpKind.Memory ||
                            NormalizeRegister(tableLoad.MemoryIndex) !=
                                compressedRegister ||
                            tableLoad.MemoryIndexScale != 4 ||
                            !TryResolveMemoryTableAddress(
                                instructions,
                                tableIndex,
                                tableLoad,
                                out ulong jumpTableAddress) ||
                            !IsModuleRange(
                                jumpTableAddress +
                                    unchecked((ulong)compressedCase * 4),
                                4,
                                libraryBase,
                                moduleEnd))
                        {
                            continue;
                        }

                        uint targetRva = *(uint*)(
                            jumpTableAddress +
                            unchecked((ulong)compressedCase * 4));
                        ulong target = libraryBase + targetRva;
                        if (target >= libraryBase && target < moduleEnd)
                        {
                            caseTarget = target;
                            stateLoadIndex = candidateStateLoadIndex;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static Dictionary<Register, HashSet<long>> FindPreSwitchConstants(
            List<Instruction> instructions,
            int stateLoadIndex)
        {
            Dictionary<Register, HashSet<long>> constants =
                new Dictionary<Register, HashSet<long>>();
            for (int index = 0;
                 index < stateLoadIndex;
                 index++)
            {
                Instruction instruction = instructions[index];
                ApplyConstantTransfer(instruction, constants);
                if (instruction.FlowControl == FlowControl.Call ||
                    instruction.FlowControl == FlowControl.IndirectCall)
                {
                    RemoveVolatileRegisterConstants(constants);
                }
            }

            return constants;
        }

        private static bool TryResolveMemoryTableAddress(
            List<Instruction> instructions,
            int instructionIndex,
            Instruction memoryInstruction,
            out ulong address)
        {
            address = memoryInstruction.MemoryDisplacement64;
            if (memoryInstruction.IsIPRelativeMemoryOperand)
            {
                address = memoryInstruction.IPRelativeMemoryAddress;
                return true;
            }

            Register baseRegister = NormalizeRegister(
                memoryInstruction.MemoryBase);
            if (baseRegister == Register.None)
                return address != 0;

            if (!TryResolveRegisterAddress(
                    instructions,
                    instructionIndex - 1,
                    baseRegister,
                    out ulong baseAddress))
            {
                return false;
            }

            address = baseAddress + memoryInstruction.MemoryDisplacement64;
            return true;
        }

        private static bool TryResolveRegisterAddress(
            List<Instruction> instructions,
            int startIndex,
            Register register,
            out ulong address)
        {
            address = 0;
            register = NormalizeRegister(register);
            for (int index = startIndex;
                 index >= 0 && startIndex - index <= 80;
                 index--)
            {
                Instruction instruction = instructions[index];
                if (instruction.Op0Kind != OpKind.Register ||
                    NormalizeRegister(instruction.Op0Register) != register)
                {
                    continue;
                }

                if (instruction.Mnemonic == Mnemonic.Lea &&
                    instruction.IsIPRelativeMemoryOperand)
                {
                    address = instruction.IPRelativeMemoryAddress;
                    return true;
                }

                if (instruction.Mnemonic == Mnemonic.Mov &&
                    IsImmediate(instruction.Op1Kind))
                {
                    address = instruction.GetImmediate(1);
                    return true;
                }

                return false;
            }

            return false;
        }

        private static bool IsModuleRange(
            ulong address,
            int length,
            ulong moduleStart,
            ulong moduleEnd)
        {
            return address >= moduleStart &&
                   address <= moduleEnd - unchecked((ulong)length);
        }

        private static bool IsUnitFieldStore(
            List<Instruction> instructions,
            int instructionIndex,
            int fieldOffset,
            ulong unitManagerBase)
        {
            return IsUnitFieldMemoryOperand(
                instructions,
                instructionIndex,
                fieldOffset,
                unitManagerBase);
        }

        private static bool IsUnitFieldMemoryOperand(
            List<Instruction> instructions,
            int instructionIndex,
            int fieldOffset,
            ulong unitManagerBase)
        {
            Instruction memoryInstruction = instructions[instructionIndex];
            if (memoryInstruction.MemoryDisplacement64 ==
                unchecked((ulong)fieldOffset))
                return true;

            Register baseRegister = NormalizeRegister(
                memoryInstruction.MemoryBase);
            var additiveRegisters = new HashSet<Register>();
            for (int index = instructionIndex - 1;
                 index >= 0 && instructionIndex - index <= 80;
                 index--)
            {
                Instruction instruction = instructions[index];
                if (instruction.Op0Kind != OpKind.Register ||
                    NormalizeRegister(instruction.Op0Register) != baseRegister)
                {
                    continue;
                }

                if ((instruction.Mnemonic == Mnemonic.Add ||
                      instruction.Mnemonic == Mnemonic.Sub) &&
                    instruction.Op1Kind == OpKind.Register)
                {
                    additiveRegisters.Add(NormalizeRegister(
                        instruction.Op1Register));
                    continue;
                }

                if (instruction.Mnemonic != Mnemonic.Lea)
                    return false;

                ulong fieldAddress;
                if (instruction.IsIPRelativeMemoryOperand)
                {
                    fieldAddress = instruction.IPRelativeMemoryAddress;
                }
                else if (instruction.MemoryIndex == Register.None &&
                         TryResolveRegisterAddress(
                             instructions,
                             index - 1,
                             NormalizeRegister(instruction.MemoryBase),
                             out ulong leaBaseAddress))
                {
                    fieldAddress =
                        leaBaseAddress + instruction.MemoryDisplacement64;
                }
                else if (instruction.MemoryDisplacement64 ==
                         unchecked((ulong)fieldOffset))
                {
                    // Unit handlers build aliases in both orders:
                    // manager + (unitIndex + field) and the commuted form.
                    additiveRegisters.Add(NormalizeRegister(
                        instruction.MemoryBase));
                    if (instruction.MemoryIndex != Register.None)
                    {
                        additiveRegisters.Add(NormalizeRegister(
                            instruction.MemoryIndex));
                    }

                    foreach (Register component in additiveRegisters)
                    {
                        if (TryResolveRegisterAddress(
                                instructions,
                                index - 1,
                                component,
                                out ulong componentAddress) &&
                            componentAddress == unitManagerBase)
                        {
                            return true;
                        }
                    }

                    return false;
                }
                else
                {
                    return false;
                }

                return fieldAddress ==
                       unitManagerBase + unchecked((ulong)fieldOffset);
            }

            return false;
        }

        private static void DiscoverReachableCadenceWrites(
            List<Instruction> instructions,
            Dictionary<ulong, int> instructionIndexByIp,
            int caseStartIndex,
            ulong caseEnd,
            ulong unitManagerBase,
            Dictionary<Register, HashSet<long>> preSwitchConstants,
            List<CadenceFieldWrite> animationWrites,
            List<CadenceFieldWrite> speedBonusWrites)
        {
            var inputConstantsByIndex =
                new Dictionary<int, Dictionary<Register, HashSet<long>>>();
            var pending = new Queue<int>();
            var animationWritesByIndex =
                new Dictionary<int, CadenceFieldWrite>();
            var speedBonusWritesByIndex =
                new Dictionary<int, CadenceFieldWrite>();

            inputConstantsByIndex[caseStartIndex] =
                CloneConstantState(preSwitchConstants);
            pending.Enqueue(caseStartIndex);

            while (pending.Count != 0)
            {
                int index = pending.Dequeue();
                Instruction instruction = instructions[index];
                Dictionary<Register, HashSet<long>> constants =
                    CloneConstantState(inputConstantsByIndex[index]);

                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    TryGetOperandConstants(
                        instruction,
                        1,
                        constants,
                        out HashSet<long> storedValues))
                {
                    if (IsUnitFieldStore(
                            instructions,
                            index,
                            UnitAnimationStateOffset,
                            unitManagerBase))
                    {
                        RecordCadenceFieldWrite(
                            animationWritesByIndex,
                            index,
                            instruction.IP,
                            storedValues);
                    }
                    else if (IsUnitFieldStore(
                                 instructions,
                                 index,
                                 UnitSpeedBonusOffset,
                                 unitManagerBase))
                    {
                        RecordCadenceFieldWrite(
                            speedBonusWritesByIndex,
                            index,
                            instruction.IP,
                            storedValues);
                    }
                }

                ApplyConstantTransfer(instruction, constants);
                if (instruction.FlowControl == FlowControl.Call ||
                    instruction.FlowControl == FlowControl.IndirectCall)
                {
                    // Windows x64 calls may replace volatile scratch values;
                    // nonvolatile unit-handler constants remain valid.
                    RemoveVolatileRegisterConstants(constants);
                }

                if (instruction.FlowControl == FlowControl.ConditionalBranch)
                {
                    EnqueueCadenceSuccessor(
                        index + 1,
                        instructions,
                        caseStartIndex,
                        caseEnd,
                        constants,
                        inputConstantsByIndex,
                        pending);
                    if (instructionIndexByIp.TryGetValue(
                            instruction.NearBranchTarget,
                            out int branchIndex))
                    {
                        EnqueueCadenceSuccessor(
                            branchIndex,
                            instructions,
                            caseStartIndex,
                            caseEnd,
                            constants,
                            inputConstantsByIndex,
                            pending);
                    }

                    continue;
                }

                if (instruction.FlowControl == FlowControl.UnconditionalBranch)
                {
                    if (instructionIndexByIp.TryGetValue(
                            instruction.NearBranchTarget,
                            out int branchIndex))
                    {
                        EnqueueCadenceSuccessor(
                            branchIndex,
                            instructions,
                            caseStartIndex,
                            caseEnd,
                            constants,
                            inputConstantsByIndex,
                            pending);
                    }

                    continue;
                }

                if (instruction.FlowControl == FlowControl.Return ||
                    instruction.FlowControl == FlowControl.IndirectBranch ||
                    instruction.FlowControl == FlowControl.Interrupt)
                {
                    continue;
                }

                EnqueueCadenceSuccessor(
                    index + 1,
                    instructions,
                    caseStartIndex,
                    caseEnd,
                    constants,
                    inputConstantsByIndex,
                    pending);
            }

            animationWrites.AddRange(animationWritesByIndex.Values);
            speedBonusWrites.AddRange(speedBonusWritesByIndex.Values);
        }

        private static void EnqueueCadenceSuccessor(
            int successorIndex,
            List<Instruction> instructions,
            int caseStartIndex,
            ulong caseEnd,
            Dictionary<Register, HashSet<long>> constants,
            Dictionary<int, Dictionary<Register, HashSet<long>>>
                inputConstantsByIndex,
            Queue<int> pending)
        {
            if (successorIndex < caseStartIndex ||
                successorIndex >= instructions.Count ||
                instructions[successorIndex].IP >= caseEnd)
            {
                return;
            }

            if (!inputConstantsByIndex.TryGetValue(
                    successorIndex,
                    out Dictionary<Register, HashSet<long>> existing))
            {
                inputConstantsByIndex[successorIndex] =
                    CloneConstantState(constants);
                pending.Enqueue(successorIndex);
                return;
            }

            if (MergeConstantStates(existing, constants))
                pending.Enqueue(successorIndex);
        }

        private static bool MergeConstantStates(
            Dictionary<Register, HashSet<long>> existing,
            Dictionary<Register, HashSet<long>> incoming)
        {
            bool changed = false;
            var knownRegisters = new List<Register>(existing.Keys);
            foreach (Register register in knownRegisters)
            {
                if (!incoming.TryGetValue(
                        register,
                        out HashSet<long> incomingValues))
                {
                    existing.Remove(register);
                    changed = true;
                    continue;
                }

                int previousCount = existing[register].Count;
                existing[register].UnionWith(incomingValues);
                if (existing[register].Count > 32)
                {
                    existing.Remove(register);
                    changed = true;
                }
                else if (existing[register].Count != previousCount)
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static Dictionary<Register, HashSet<long>> CloneConstantState(
            Dictionary<Register, HashSet<long>> source)
        {
            var clone = new Dictionary<Register, HashSet<long>>(source.Count);
            foreach (KeyValuePair<Register, HashSet<long>> entry in source)
                clone[entry.Key] = new HashSet<long>(entry.Value);
            return clone;
        }

        private static void RecordCadenceFieldWrite(
            Dictionary<int, CadenceFieldWrite> writesByIndex,
            int instructionIndex,
            ulong instructionPointer,
            HashSet<long> values)
        {
            if (writesByIndex.TryGetValue(
                    instructionIndex,
                    out CadenceFieldWrite existing))
            {
                existing.Values.UnionWith(values);
                return;
            }

            writesByIndex[instructionIndex] = new CadenceFieldWrite(
                instructionIndex,
                instructionPointer,
                new HashSet<long>(values));
        }

        private static void ApplyConstantTransfer(
            Instruction instruction,
            Dictionary<Register, HashSet<long>> constants)
        {
            if (instruction.Op0Kind != OpKind.Register)
                return;

            Register destination = NormalizeRegister(
                instruction.Op0Register);
            if (instruction.Mnemonic == Mnemonic.Cmp ||
                instruction.Mnemonic == Mnemonic.Test)
            {
                // Comparisons read operand zero but do not replace it.
                return;
            }

            if (instruction.Mnemonic.ToString().StartsWith(
                    "Cmov",
                    StringComparison.Ordinal))
            {
                if (constants.TryGetValue(
                        destination,
                        out HashSet<long> previousValues) &&
                    TryGetOperandConstants(
                        instruction,
                        1,
                        constants,
                        out HashSet<long> selectedValues))
                {
                    var combined = new HashSet<long>(previousValues);
                    combined.UnionWith(selectedValues);
                    SetRegisterConstants(constants, destination, combined);
                }
                else
                {
                    constants.Remove(destination);
                }

                return;
            }

            if (instruction.Mnemonic == Mnemonic.Lea)
            {
                if (TryEvaluateLeaConstants(
                        instruction,
                        constants,
                        out HashSet<long> addressValues))
                {
                    SetRegisterConstants(
                        constants,
                        destination,
                        addressValues);
                }
                else
                {
                    constants.Remove(destination);
                }

                return;
            }

            if (instruction.Mnemonic == Mnemonic.Mov ||
                instruction.Mnemonic == Mnemonic.Movzx ||
                instruction.Mnemonic == Mnemonic.Movsx ||
                instruction.Mnemonic == Mnemonic.Movsxd)
            {
                if (TryGetOperandConstants(
                        instruction,
                        1,
                        constants,
                        out HashSet<long> movedValues))
                {
                    SetRegisterConstants(constants, destination, movedValues);
                }
                else
                {
                    constants.Remove(destination);
                }

                return;
            }

            if ((instruction.Mnemonic == Mnemonic.Xor ||
                 instruction.Mnemonic == Mnemonic.Sub) &&
                instruction.Op1Kind == OpKind.Register &&
                NormalizeRegister(instruction.Op1Register) == destination)
            {
                constants[destination] = new HashSet<long> { 0 };
                return;
            }

            if ((instruction.Mnemonic == Mnemonic.Add ||
                 instruction.Mnemonic == Mnemonic.Sub) &&
                constants.TryGetValue(
                    destination,
                    out HashSet<long> destinationValues) &&
                TryGetOperandConstants(
                    instruction,
                    1,
                    constants,
                    out HashSet<long> operandValues))
            {
                var results = new HashSet<long>();
                foreach (long left in destinationValues)
                {
                    foreach (long right in operandValues)
                    {
                        results.Add(instruction.Mnemonic == Mnemonic.Add
                            ? unchecked(left + right)
                            : unchecked(left - right));
                    }
                }

                SetRegisterConstants(constants, destination, results);
                return;
            }

            constants.Remove(destination);
        }

        private static bool TryEvaluateLeaConstants(
            Instruction instruction,
            Dictionary<Register, HashSet<long>> constants,
            out HashSet<long> values)
        {
            values = new HashSet<long>
            {
                unchecked((long)instruction.MemoryDisplacement64)
            };

            Register baseRegister = NormalizeRegister(
                instruction.MemoryBase);
            if (baseRegister != Register.None)
            {
                if (!constants.TryGetValue(
                        baseRegister,
                        out HashSet<long> baseValues))
                {
                    values = null;
                    return false;
                }

                values = AddConstantProducts(values, baseValues, 1);
            }

            Register indexRegister = NormalizeRegister(
                instruction.MemoryIndex);
            if (indexRegister != Register.None)
            {
                if (!constants.TryGetValue(
                        indexRegister,
                        out HashSet<long> indexValues))
                {
                    values = null;
                    return false;
                }

                values = AddConstantProducts(
                    values,
                    indexValues,
                    instruction.MemoryIndexScale);
            }

            return values.Count != 0 && values.Count <= 32;
        }

        private static HashSet<long> AddConstantProducts(
            HashSet<long> leftValues,
            HashSet<long> rightValues,
            int multiplier)
        {
            var results = new HashSet<long>();
            foreach (long left in leftValues)
            {
                foreach (long right in rightValues)
                {
                    results.Add(unchecked(left + right * multiplier));
                }
            }

            return results;
        }

        private static bool TryGetOperandConstants(
            Instruction instruction,
            int operand,
            Dictionary<Register, HashSet<long>> constants,
            out HashSet<long> values)
        {
            OpKind kind = instruction.GetOpKind(operand);
            if (IsImmediate(kind))
            {
                values = new HashSet<long>
                {
                    unchecked((long)instruction.GetImmediate(operand))
                };
                return true;
            }

            if (kind == OpKind.Register &&
                constants.TryGetValue(
                    NormalizeRegister(instruction.GetOpRegister(operand)),
                    out HashSet<long> registerValues))
            {
                values = new HashSet<long>(registerValues);
                return true;
            }

            values = null;
            return false;
        }

        private static void SetRegisterConstants(
            Dictionary<Register, HashSet<long>> constants,
            Register register,
            HashSet<long> values)
        {
            if (values.Count == 0 || values.Count > 32)
                constants.Remove(register);
            else
                constants[register] = new HashSet<long>(values);
        }

        private static void RemoveVolatileRegisterConstants(
            Dictionary<Register, HashSet<long>> constants)
        {
            constants.Remove(Register.RAX);
            constants.Remove(Register.RCX);
            constants.Remove(Register.RDX);
            constants.Remove(Register.R8);
            constants.Remove(Register.R9);
            constants.Remove(Register.R10);
            constants.Remove(Register.R11);
        }

        private static bool IsImmediate(OpKind operandKind)
        {
            switch (operandKind)
            {
                case OpKind.Immediate8:
                case OpKind.Immediate8_2nd:
                case OpKind.Immediate16:
                case OpKind.Immediate32:
                case OpKind.Immediate64:
                case OpKind.Immediate8to16:
                case OpKind.Immediate8to32:
                case OpKind.Immediate8to64:
                case OpKind.Immediate32to64:
                    return true;
                default:
                    return false;
            }
        }

        private static Register NormalizeRegister(Register register)
        {
            switch (register)
            {
                case Register.AL:
                case Register.AH:
                case Register.AX:
                case Register.EAX:
                case Register.RAX:
                    return Register.RAX;
                case Register.CL:
                case Register.CH:
                case Register.CX:
                case Register.ECX:
                case Register.RCX:
                    return Register.RCX;
                case Register.DL:
                case Register.DH:
                case Register.DX:
                case Register.EDX:
                case Register.RDX:
                    return Register.RDX;
                case Register.BL:
                case Register.BH:
                case Register.BX:
                case Register.EBX:
                case Register.RBX:
                    return Register.RBX;
                case Register.SPL:
                case Register.SP:
                case Register.ESP:
                case Register.RSP:
                    return Register.RSP;
                case Register.BPL:
                case Register.BP:
                case Register.EBP:
                case Register.RBP:
                    return Register.RBP;
                case Register.SIL:
                case Register.SI:
                case Register.ESI:
                case Register.RSI:
                    return Register.RSI;
                case Register.DIL:
                case Register.DI:
                case Register.EDI:
                case Register.RDI:
                    return Register.RDI;
                case Register.R8L:
                case Register.R8W:
                case Register.R8D:
                case Register.R8:
                    return Register.R8;
                case Register.R9L:
                case Register.R9W:
                case Register.R9D:
                case Register.R9:
                    return Register.R9;
                case Register.R10L:
                case Register.R10W:
                case Register.R10D:
                case Register.R10:
                    return Register.R10;
                case Register.R11L:
                case Register.R11W:
                case Register.R11D:
                case Register.R11:
                    return Register.R11;
                case Register.R12L:
                case Register.R12W:
                case Register.R12D:
                case Register.R12:
                    return Register.R12;
                case Register.R13L:
                case Register.R13W:
                case Register.R13D:
                case Register.R13:
                    return Register.R13;
                case Register.R14L:
                case Register.R14W:
                case Register.R14D:
                case Register.R14:
                    return Register.R14;
                case Register.R15L:
                case Register.R15W:
                case Register.R15D:
                case Register.R15:
                    return Register.R15;
                default:
                    return register;
            }
        }

        private sealed class CadenceFieldWrite
        {
            public CadenceFieldWrite(
                int instructionIndex,
                ulong instructionPointer,
                HashSet<long> values)
            {
                InstructionIndex = instructionIndex;
                InstructionPointer = instructionPointer;
                Values = values;
            }

            public int InstructionIndex { get; }
            public ulong InstructionPointer { get; }
            public HashSet<long> Values { get; }

            public bool ContainsSigned16(short value)
            {
                foreach (long candidate in Values)
                {
                    if (unchecked((short)(ushort)candidate) == value)
                        return true;
                }

                return false;
            }
        }

        private sealed class AnimationTransitions
        {
            private readonly Dictionary<uint, uint> walkingToRunning;
            private readonly Dictionary<uint, uint> runningToWalking;
            private readonly List<uint> runningStates;
            private readonly bool allowSoleStateFallbackForRally;

            public AnimationTransitions(
                HashSet<uint> extractedRunningStates,
                ushort nativeRunningSpeedBonus,
                bool allowSoleStateFallbackForRally)
            {
                if (extractedRunningStates == null ||
                    extractedRunningStates.Count == 0)
                {
                    throw new ArgumentNullException(
                        nameof(extractedRunningStates));
                }

                runningStates = new List<uint>(extractedRunningStates);
                runningStates.Sort();
                walkingToRunning =
                    new Dictionary<uint, uint>(runningStates.Count);
                NativeRunningSpeedBonus = nativeRunningSpeedBonus;
                this.allowSoleStateFallbackForRally =
                    allowSoleStateFallbackForRally;
                runningToWalking =
                    new Dictionary<uint, uint>(runningStates.Count);

                foreach (uint runningState in runningStates)
                {
                    uint walkingState = InferWalkingState(
                        runningState,
                        runningStates.Count);
                    if (!walkingToRunning.ContainsKey(walkingState) ||
                        (walkingToRunning[walkingState] == walkingState &&
                         runningState != walkingState))
                    {
                        walkingToRunning[walkingState] = runningState;
                    }
                    runningToWalking[runningState] = walkingState;
                }
            }

            public ushort? NativeRunningSpeedBonus { get; }

            public bool AllowSoleStateFallbackForRally =>
                allowSoleStateFallbackForRally;

            public uint SoleRunningState =>
                runningStates.Count == 1 ? runningStates[0] : 0;

            public int RunningStateCount => runningStates.Count;

            public List<KeyValuePair<uint, uint>> CreateRunningMappings()
            {
                var mappings = new SortedDictionary<uint, uint>();
                foreach (KeyValuePair<uint, uint> pair in walkingToRunning)
                    mappings[pair.Key] = pair.Value;
                foreach (uint runningState in runningStates)
                {
                    if (!mappings.ContainsKey(runningState))
                        mappings[runningState] = runningState;
                }
                return new List<KeyValuePair<uint, uint>>(mappings);
            }

            public List<KeyValuePair<uint, uint>> CreateWalkingMappings()
            {
                var mappings = new SortedDictionary<uint, uint>();
                foreach (KeyValuePair<uint, uint> pair in runningToWalking)
                    mappings[pair.Key] = pair.Value;
                foreach (uint walkingState in walkingToRunning.Keys)
                {
                    if (!mappings.ContainsKey(walkingState))
                        mappings[walkingState] = walkingState;
                }
                return new List<KeyValuePair<uint, uint>>(mappings);
            }

            public bool TryGetRunningState(
                uint currentState,
                out uint runningState)
            {
                if (walkingToRunning.TryGetValue(
                        currentState,
                        out runningState))
                {
                    return true;
                }

                if (runningToWalking.ContainsKey(currentState))
                {
                    runningState = currentState;
                    return true;
                }

                // Do not force a merely similar or sole decoded state. Only
                // exact audited walking/running pairs are safe to translate.
                runningState = currentState;
                return false;
            }

            public bool TryGetRallyRunningState(
                uint currentState,
                out uint runningState)
            {
                if (TryGetRunningState(currentState, out runningState))
                    return true;

                if (allowSoleStateFallbackForRally &&
                    runningStates.Count == 1)
                {
                    runningState = runningStates[0];
                    return true;
                }

                runningState = currentState;
                return false;
            }

            public bool TryGetWalkingState(
                uint currentState,
                out uint walkingState)
            {
                if (runningToWalking.TryGetValue(
                        currentState,
                        out walkingState))
                {
                    return true;
                }

                if (walkingToRunning.ContainsKey(currentState))
                {
                    walkingState = currentState;
                    return true;
                }

                walkingState = currentState;
                return false;
            }

            private static uint InferWalkingState(
                uint runningState,
                int candidateCount)
            {
                if (runningState <= 0xFF &&
                    (runningState & 0x80) != 0)
                {
                    return runningState & ~0x80u;
                }

                if (runningState > 0xFF &&
                    ((runningState & 0xFF) == 0x01 ||
                     (runningState & 0xFF) == 0x81))
                {
                    return runningState - 0x100u;
                }

                // A single conditional fast state (for example the healer's
                // 0x5C1) is selected from the ordinary movement state 1.
                return candidateCount == 1 ? 1u : runningState;
            }
        }
    }
}
