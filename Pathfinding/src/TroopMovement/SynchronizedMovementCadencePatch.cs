// Feature: Own the shared native movement-speed and animation-cadence hooks.
using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using static Iced.Intel.AssemblerRegisters;
using static Pathfinding.UnitCommands.NativeMovementCadenceResolver;
using static Pathfinding.MovementCadenceNativeDiscovery;

namespace Pathfinding
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

        private const int RallyEntrySize = 32;
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
        // IsReallyAlive low word at GameUnit+0x29C, relative to the audited manager base.
        private const int UnitDeathMarkerManagerOffset = 0x8F8;
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

        private const ushort UnitInitializationAiState = 109;
        internal const int UnitSpeedBonusOffset = 0x916;

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
        private const int TerrainSnapshotSize = 16;
        private const int TerrainGlobalIdOffset = 0;
        private const int TerrainTypeOffset = 4;
        private const int TerrainDelayOffset = 6;
        private const int TerrainEpochOffset = 8;
        private const int TerrainValidOffset = 12;
        private const int RallyMovementAiState = 105;

        private HookTransaction transaction;
        private HookTransaction terrainTransaction;
        private readonly HookHandle<X64InlineHook> terrainCaptureHook = new HookHandle<X64InlineHook>();
        private byte* terrainSnapshots;
        // Low bit enables the fix; the remaining bits invalidate snapshots across toggles.
        private int* terrainEnabledFlag;
        private bool terrainAvailable;
        private readonly Dictionary<eChimps, AnimationTransitions>
            animationTransitionsByType;
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
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            // The semantic decoder needs the manager-relative address used by
            // native unit handlers; rally tracking itself lives elsewhere.
            GameUnit* unitArray = GameUnitManagerAPI.Instance.GetUnitArray()._array;
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
            animationTransitionsByType = MovementCadenceNativeDiscovery.DiscoverRunningAnimationTransitions(
                memory,
                libraryBase,
                libraryBase + unchecked((ulong)dispatchRva),
                referenceHashMatches,
                unitArray);

            bool terrainValidated = false;
            try
            {
                ValidateTerrainCaptureHook(memory, libraryBase, dispatchRva);
                terrainValidated = true;
            }
            catch (Exception ex)
            {
                log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Rally terrain " +
                    $"capture validation failed; only the terrain fix remains inactive: {ex}");
            }

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
                terrainSnapshots = AllocateZeroed((MaximumTrackedUnitId + 1) * TerrainSnapshotSize);
                terrainEnabledFlag = (int*)AllocateZeroed(sizeof(int));
                PublishNativeProfiles();

                transaction = PathfindingHookInfrastructure.CreateOwnedTransaction(region);

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
                    !movementCadenceHook.Success ||
                    movementSpeedAdjustmentHook.Hook.DisplacedByteCount != PreTerrainSpeedAdjustmentHookLength ||
                    movementCadenceHook.Hook.DisplacedByteCount != 23)
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

            // The optional capture hook has its own transaction. Failure leaves the
            // published speed/cadence hooks active with the terrain flag disabled.
            if (!terrainValidated)
                return;
            try
            {
                terrainTransaction = PathfindingHookInfrastructure.CreateOwnedTransaction(region);
                terrainTransaction.AddInline(terrainCaptureHook,
                    HookTarget.FromAddress(libraryBase + (uint)dispatchRva),
                    GenerateTerrainCaptureFastPath, hookSize: 14);
                CommitResult result = terrainTransaction.Commit();
                if (!result.IsCompleteSuccess || !terrainCaptureHook.Success ||
                    terrainCaptureHook.Hook.DisplacedByteCount != 14)
                    throw new InvalidOperationException("Unexpected rally terrain capture displacement.");
                terrainAvailable = true;
            }
            catch (Exception ex)
            {
                terrainTransaction?.Dispose(); // Unpublished optional initialization rollback only.
                log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Rally terrain fix " +
                    $"remains inactive; existing movement features remain available: {ex}");
            }

            if (terrainAvailable)
                Shared.DebugLogHelper.LogInfo(log, "Rally terrain hook: " +
                    $"RVA 0x{dispatchRva:X}, 14 bytes, continuation 0x{dispatchRva + 14:X}; " +
                    "native capture/restore, all owners, no recruit tracking.");

    }

    internal bool IsTerrainAvailable => terrainAvailable;

        internal void SetTerrainEnabled(bool enabled)
        {
            if (terrainEnabledFlag == null)
                return;
            enabled &= terrainAvailable;
            int current;
            do
            {
                current = Volatile.Read(ref *terrainEnabledFlag);
                if (((current & 1) != 0) == enabled)
                    return;
            }
            while (Interlocked.CompareExchange(ref *terrainEnabledFlag,
                unchecked(current + 1), current) != current);
        }

        private static void EmitTerrainEligibility(Assembler assembler,
            AssemblerRegister64 unit, Label rejected)
        {
            assembler.cmp(__word_ptr[unit + UnitAliveStateManagerOffset], (int)AliveState.IsAlive);
            assembler.jne(rejected);
            assembler.cmp(__word_ptr[unit + UnitDeathMarkerManagerOffset], 0);
            assembler.jne(rejected);
            assembler.cmp(__word_ptr[unit + UnitAiStateManagerOffset], RallyMovementAiState);
            assembler.jne(rejected);
            // All audited recruitment types; no owner or newly-recruited gate.
            assembler.movzx(ecx, __word_ptr[unit + UnitTypeManagerOffset]);
            Label allowed = assembler.CreateLabel();
            Label next = assembler.CreateLabel();
            assembler.cmp(ecx, 5);
            assembler.je(allowed);
            assembler.cmp(ecx, 37);
            assembler.je(allowed);
            assembler.cmp(ecx, 22);
            assembler.jl(next);
            assembler.cmp(ecx, 30);
            assembler.jle(allowed);
            assembler.Label(ref next);
            next = assembler.CreateLabel();
            assembler.cmp(ecx, 70);
            assembler.jl(next);
            assembler.cmp(ecx, 76);
            assembler.jle(allowed);
            assembler.Label(ref next);
            assembler.cmp(ecx, 78);
            assembler.jl(rejected);
            assembler.cmp(ecx, 85);
            assembler.jg(rejected);
            assembler.Label(ref allowed);
            // A following instruction avoids binding a second label here.
            assembler.nop();
        }

        private void GenerateTerrainCaptureFastPath(Assembler assembler,
            ReadOnlySpan<Instruction> instructions, ulong returnAddress)
        {
            if (instructions.Length != 2 || instructions[0].Length != 8 ||
                instructions[1].Length != 6 || returnAddress != instructions[0].IP + 14)
                throw new InvalidOperationException("Unexpected terrain capture boundary.");
            Label replay = assembler.CreateLabel();
            // Insert before the original call. Restore the original stack, flags
            // and all scratch registers before replaying the indirect handler.
            assembler.pushfq();
            assembler.push(rax);
            assembler.push(rcx);
            assembler.push(rdx);
            assembler.lea(rdx, __[rbx + rcx]);
            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.cmp(eax, 1);
            assembler.jl(replay);
            assembler.cmp(eax, MaximumTrackedUnitId);
            assembler.jg(replay);
            assembler.imul(rax, rax, TerrainSnapshotSize);
            assembler.mov(rcx, unchecked((ulong)terrainSnapshots));
            assembler.add(rax, rcx);
            assembler.mov(__byte_ptr[rax + TerrainValidOffset], 0);
            assembler.mov(rcx, unchecked((ulong)terrainEnabledFlag));
            assembler.mov(ecx, __dword_ptr[rcx]);
            assembler.test(ecx, 1);
            assembler.je(replay);
            assembler.mov(__dword_ptr[rax + TerrainEpochOffset], ecx);
            EmitTerrainEligibility(assembler, rdx, replay);
            assembler.mov(__word_ptr[rax + TerrainTypeOffset], cx);
            assembler.mov(ecx, __dword_ptr[rdx + UnitGlobalIdManagerOffset]);
            assembler.mov(__dword_ptr[rax + TerrainGlobalIdOffset], ecx);
            assembler.mov(cx, __word_ptr[rdx + UnitCurrentSpeed2ManagerOffset]);
            assembler.mov(__word_ptr[rax + TerrainDelayOffset], cx);
            assembler.mov(__byte_ptr[rax + TerrainValidOffset], 1);
            assembler.Label(ref replay);
            assembler.pop(rdx);
            assembler.pop(rcx);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in instructions)
                assembler.AddInstruction(instruction);
        }

        private void EmitTerrainRestore(Assembler assembler)
        {
            Label done = assembler.CreateLabel();
            // RAX/RCX/R10 are overwritten by the displaced cadence instructions.
            // Flags have already been saved by GenerateCadenceFastPath.
            assembler.mov(rax, currentUnitIdAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.cmp(eax, 1);
            assembler.jl(done);
            assembler.cmp(eax, MaximumTrackedUnitId);
            assembler.jg(done);
            assembler.imul(rax, rax, TerrainSnapshotSize);
            assembler.mov(rcx, unchecked((ulong)terrainSnapshots));
            assembler.add(rax, rcx);
            assembler.cmp(__byte_ptr[rax + TerrainValidOffset], 0);
            assembler.je(done);
            assembler.mov(__byte_ptr[rax + TerrainValidOffset], 0);
            assembler.mov(rcx, unchecked((ulong)terrainEnabledFlag));
            assembler.mov(ecx, __dword_ptr[rcx]);
            assembler.test(ecx, 1);
            assembler.je(done);
            assembler.cmp(__dword_ptr[rax + TerrainEpochOffset], ecx);
            assembler.jne(done);
            EmitTerrainEligibility(assembler, r8, done);
            assembler.cmp(__word_ptr[rax + TerrainTypeOffset], cx);
            assembler.jne(done);
            assembler.mov(ecx, __dword_ptr[r8 + UnitGlobalIdManagerOffset]);
            assembler.cmp(__dword_ptr[rax + TerrainGlobalIdOffset], ecx);
            assembler.jne(done);
            assembler.mov(cx, __word_ptr[rax + TerrainDelayOffset]);
            assembler.mov(__word_ptr[r8 + UnitCurrentSpeed2ManagerOffset], cx);
            assembler.Label(ref done);
            assembler.nop();
        }

        private static void ValidateTerrainCaptureHook(ReadOnlySpan<byte> memory,
            ulong libraryBase, int hookRva)
        {
            const int functionRva = 0x182B00;
            const int functionLength = 0x23B1;
            if (hookRva < functionRva || hookRva + 14 > functionRva + functionLength ||
                functionRva + functionLength > memory.Length)
                throw new InvalidOperationException("Terrain capture is outside the audited dispatcher.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(memory.Slice(hookRva, 32).ToArray()));
            decoder.IP = libraryBase + (uint)hookRva;
            Instruction call = decoder.Decode();
            Instruction load = decoder.Decode();
            Instruction next = decoder.Decode();
            if (call.IsInvalid || call.Length != 8 || call.FlowControl != FlowControl.IndirectCall ||
                call.MemoryBase != Register.R14 || call.MemoryIndex != Register.RAX || call.MemoryIndexScale != 8 ||
                call.MemoryDisplacement64 != 0x321CB0 || load.IsInvalid || load.Length != 6 ||
                load.Mnemonic != Mnemonic.Mov || load.Op0Register != Register.EDX ||
                load.MemoryBase != Register.RIP || load.IPRelativeMemoryAddress != libraryBase + 0x9302C4 ||
                next.IsInvalid || next.IP != libraryBase + (uint)hookRva + 14 ||
                next.Mnemonic != Mnemonic.Movsxd || next.Op0Register != Register.RAX || next.Op1Register != Register.EDX)
                throw new InvalidOperationException("Terrain capture call/load/continuation contract changed.");
            decoder = Decoder.Create(64, new ByteArrayCodeReader(memory.Slice(functionRva, functionLength).ToArray()));
            decoder.IP = libraryBase + functionRva;
            ulong end = decoder.IP + functionLength;
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid)
                    throw new InvalidOperationException("Invalid instruction in the unit dispatcher audit.");
                if (IsNearBranch(instruction.Op0Kind) &&
                    instruction.NearBranchTarget > call.IP && instruction.NearBranchTarget < next.IP &&
                    (instruction.IP < call.IP || instruction.IP >= next.IP))
                    throw new InvalidOperationException("Control flow enters the terrain capture interior.");
            }
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
                for (int offset = 0; offset < RallyEntrySize; offset++)
                    entry[offset] = 0;
            }
        }

        internal void ClearAllRallyTracking()
        {
            ZeroMemory(
                rallyEntries,
                checked((MaximumTrackedUnitId + 1) * RallyEntrySize));
        }

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
            SetTerrainEnabled(false);

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
            assembler.mov(rax, unchecked((ulong)terrainEnabledFlag));
            assembler.test(__dword_ptr[rax], 1);
            assembler.jne(restoreAndReplay);
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
            assembler.cmp(__word_ptr[rbx + UnitDeathMarkerManagerOffset], 0);
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
            EmitTerrainRestore(assembler);
            // Match the former managed callback's outer gate exactly:
            // transitional or deleted units reach neither rally tracking nor
            // synchronization, and their tracking entry remains untouched.
            assembler.cmp(
                __word_ptr[r8 + UnitAliveStateManagerOffset],
                (int)AliveState.IsAlive);
            assembler.jne(replayVanilla);
            assembler.cmp(__word_ptr[r8 + UnitDeathMarkerManagerOffset], 0);
            assembler.jne(replayVanilla);
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

            assembler.mov(ecx, __dword_ptr[rax + RallyGenerationGlobalIdOffset]);
            assembler.test(ecx, ecx);
            assembler.je(rallyGlobalMatches);
            assembler.cmp(
                __dword_ptr[r8 + UnitGlobalIdManagerOffset],
                ecx);
            assembler.jne(clearRallyAndTrySynchronization);
            assembler.Label(ref rallyGlobalMatches);
            assembler.mov(ecx, __dword_ptr[rax + RallyOwnerOffset]);
            assembler.cmp(
                __byte_ptr[r8 + UnitOwnerManagerOffset],
                cl);
            assembler.jne(clearRallyAndTrySynchronization);
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
            assembler.jmp(clearRallyAndTrySynchronization);

            assembler.Label(ref rallyIdentityMatches);
            assembler.test(
                __word_ptr[r8 + UnitPathStateManagerOffset],
                2);
            assembler.jne(rallyPathActive);
            assembler.mov(__byte_ptr[rax + RallyMovingOffset], 0);
            assembler.jmp(rallyHandled);

            assembler.Label(ref rallyPathActive);
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
            assembler.jne(clearRallyAndTrySynchronization);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetYManagerOffset]);
            assembler.cmp(cx, __word_ptr[rax + RallyTargetYOffset]);
            assembler.jne(clearRallyAndTrySynchronization);

            assembler.Label(ref rallyTargetAccepted);
            assembler.mov(__byte_ptr[rax + RallyMovingOffset], 1);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetXManagerOffset]);
            assembler.mov(__word_ptr[rax + RallyTargetXOffset], cx);
            assembler.movzx(ecx, __word_ptr[r8 + UnitTargetYManagerOffset]);
            assembler.mov(__word_ptr[rax + RallyTargetYOffset], cx);
            assembler.jmp(applyRallyProfile);

            assembler.Label(ref clearRallyAndTrySynchronization);
            assembler.mov(__byte_ptr[rax + RallyActiveOffset], 0);

            assembler.Label(ref trySynchronization);
            assembler.mov(rax, unchecked((ulong)synchronizationEnabledFlag));
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(replayVanilla);
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
            assembler.jmp(completed);
        }

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
            if (terrainSnapshots != null)
                Marshal.FreeHGlobal(new IntPtr(terrainSnapshots));
            if (terrainEnabledFlag != null)
                Marshal.FreeHGlobal(new IntPtr(terrainEnabledFlag));
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
    }
}
