// Feature: Own the shared native movement-speed and animation-cadence hooks.
using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

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
        private const int MaximumUnitTypeHandlerLength = 0x5000;
        private const ulong UnitRecordOffset = 0x65CUL;

        private const ushort IndividualFastMovementAiState = 101;
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
        private const int CalculateMovementSpeedFunctionRva = 0x19B210;
        private const int CalculateMovementSpeedFunctionLength = 0x3C6;
        private const int PreTerrainSpeedAdjustmentRva = 0x19B4B6;
        private const int PreTerrainSpeedAdjustmentHookLength = 14;
        private const int UnitTypeUpdateDispatchRva = 0x1840BC;
        private const int MovementCadenceRva = 0x1841B3;

        private readonly ManualLogSource log;
        private readonly TryGetCadenceDelegate tryGetCadence;
        private readonly ApplyRallyBaseSpeedDelegate
            applyFastRecruitRallyMaximumSpeed;
        private readonly TryApplyRallyCadenceDelegate
            tryApplyFastRecruitRallyCadence;
        private readonly HookTransaction transaction;
        private readonly Dictionary<eChimps, AnimationTransitions>
            animationTransitionsByType =
                new Dictionary<eChimps, AnimationTransitions>(
                    (int)eChimps.CHIMP_NUM_TYPES);
        private readonly GameUnit* unitArray;
        private HookRef<X64InlineHook> movementSpeedAdjustmentHook =
            new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> movementCadenceHook =
            new HookRef<X64InlineHook>();
        private bool movementSpeedCallbackFailureLogged;
        private bool cadenceCallbackFailureLogged;
        private bool disposed;

        internal delegate bool TryGetCadenceDelegate(
            int tribeId,
            out SynchronizedMovementCadence cadence,
            out ushort runningSpeedBonus);

        internal delegate bool TryApplyRallyCadenceDelegate(GameUnit* unit);
        internal delegate void ApplyRallyBaseSpeedDelegate(GameUnit* unit);

        public SynchronizedMovementCadencePatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            TryGetCadenceDelegate tryGetCadence,
            ApplyRallyBaseSpeedDelegate applyFastRecruitRallyMaximumSpeed,
            TryApplyRallyCadenceDelegate tryApplyFastRecruitRallyCadence,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.tryGetCadence =
                tryGetCadence ?? throw new ArgumentNullException(nameof(tryGetCadence));
            this.applyFastRecruitRallyMaximumSpeed =
                applyFastRecruitRallyMaximumSpeed ??
                throw new ArgumentNullException(
                    nameof(applyFastRecruitRallyMaximumSpeed));
            this.tryApplyFastRecruitRallyCadence =
                tryApplyFastRecruitRallyCadence ??
                throw new ArgumentNullException(
                    nameof(tryApplyFastRecruitRallyCadence));

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

            ValidatePreTerrainSpeedAdjustmentHook(
                memory,
                libraryBase,
                movementSpeedAdjustmentRva);
            DiscoverRunningAnimationTransitions(
                memory,
                libraryBase,
                libraryBase + unchecked((ulong)dispatchRva),
                referenceHashMatches);

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddContextHook(
                ref movementSpeedAdjustmentHook,
                libraryBase + unchecked((ulong)movementSpeedAdjustmentRva),
                ApplyFastRecruitBaseSpeedBeforeTerrain,
                regs: X64SmartCPUContextRegs.Volatile |
                    X64SmartCPUContextRegs.RBX,
                hookSize: PreTerrainSpeedAdjustmentHookLength,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.AddContextHook(
                ref movementCadenceHook,
                libraryBase + unchecked((ulong)cadenceRva),
                SynchronizeMovementCadence,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.Commit();

            if (!movementSpeedAdjustmentHook.Success ||
                !movementCadenceHook.Success)
            {
                transaction.Unload();
                transaction.Dispose();
                throw new InvalidOperationException(
                    "The native movement-speed adjustment or movement " +
                    "cadence was not found.");
            }

            TroopMovementFix3ModLog.Debug(
                log,
                $"Native movement-speed and cadence hooks installed; " +
                $"runCapableUnitTypes={animationTransitionsByType.Count}.");
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

        internal bool TryGetNativeRunningState(
            eChimps unitType,
            uint currentState,
            out uint runningState)
        {
            runningState = currentState;
            return animationTransitionsByType.TryGetValue(
                       unitType,
                       out AnimationTransitions animationTransitions) &&
                   animationTransitions.TryGetRallyRunningState(
                       currentState,
                       out runningState);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            animationTransitionsByType.Clear();
            transaction.Unload();
            transaction.Dispose();
        }

        private void ApplyFastRecruitBaseSpeedBeforeTerrain(
            NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                GameUnit* unit =
                    (GameUnit*)(registers->RBX + UnitRecordOffset);
                if (unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    return;
                }

                // Vanilla's later terrain/status block remains untouched.
                applyFastRecruitRallyMaximumSpeed(unit);
            }
            catch (Exception ex)
            {
                if (movementSpeedCallbackFailureLogged)
                    return;

                movementSpeedCallbackFailureLogged = true;
                TroopMovementFix3ModLog.Error(
                    log,
                    $"The recruit rally movement-speed callback failed; " +
                    $"affected units keep Vanilla speed: {ex}");
            }
        }

        private void SynchronizeMovementCadence(
            NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                GameUnit* unit =
                    (GameUnit*)(registers->R8 + UnitRecordOffset);
                if (unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    return;
                }

                if (tryApplyFastRecruitRallyCadence(unit))
                    return;

                if (unit->r_TribeId == 0 ||
                    !tryGetCadence(
                        unit->r_TribeId,
                        out SynchronizedMovementCadence cadence,
                        out ushort runningSpeedBonus))
                {
                    return;
                }

                animationTransitionsByType.TryGetValue(
                    unit->r_UnitChimp,
                    out AnimationTransitions animationTransitions);

                uint animationState = unit->N000000F4;
                if (cadence == SynchronizedMovementCadence.Running)
                {
                    if (unit->r_SpeedBonus != runningSpeedBonus)
                        unit->r_SpeedBonus = runningSpeedBonus;

                    if (animationTransitions != null &&
                        animationTransitions.TryGetRunningState(
                            animationState,
                            out uint runningState) &&
                        runningState != animationState)
                    {
                        unit->N000000F4 = runningState;
                    }

                    return;
                }

                if (unit->r_SpeedBonus != 0)
                    unit->r_SpeedBonus = 0;

                if (animationTransitions != null &&
                    animationTransitions.TryGetWalkingState(
                        animationState,
                        out uint walkingState) &&
                    walkingState != animationState)
                {
                    unit->N000000F4 = walkingState;
                }
            }
            catch (Exception ex)
            {
                if (cadenceCallbackFailureLogged)
                    return;

                cadenceCallbackFailureLogged = true;
                TroopMovementFix3ModLog.Error(
                    log,
                    $"The movement-cadence callback failed; affected " +
                    $"units keep Vanilla cadence: {ex}");
            }
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
                    libraryBase + 0x19B504UL &&
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
