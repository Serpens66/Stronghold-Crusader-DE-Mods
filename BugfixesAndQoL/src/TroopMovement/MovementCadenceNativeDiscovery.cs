// Feature: Read each unit type's native fast-movement cadence from its update handler.
using Iced.Intel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using static BugfixesAndQoL.UnitCommands.NativeMovementCadenceResolver;

namespace BugfixesAndQoL
{
    /// <summary>
    /// Statically analyzes the installed DLL's unit-type update handlers once
    /// during hook installation and holds no state of its own.
    /// </summary>
    internal static unsafe class MovementCadenceNativeDiscovery
    {
        private const int MaximumUnitTypeHandlerLength = 0x5000;
        private const ulong UnitRecordOffset = 0x65CUL;
        private const ushort IndividualFastMovementAiState = 101;
        private const int UnitAnimationStateOffset = 0x660;
        private const int MaximumCadenceCaseLength = 0x240;
        private const int MaximumCadencePairDistance = 20;
        private const int DirectCadencePairDistance = 4;

        internal static Dictionary<eChimps, AnimationTransitions>
            DiscoverRunningAnimationTransitions(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong dispatchInstructionAddress,
            bool referenceHashMatches,
            GameUnit* unitArray)
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
            var animationTransitionsByType =
                new Dictionary<eChimps, AnimationTransitions>(
                    (int)eChimps.CHIMP_NUM_TYPES);
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
                            memory,
                            handlerStart,
                            handlerLength,
                            libraryBase,
                            moduleEnd,
                            unitArray);

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
                ApplyAuditedIndividualFastMovementProfiles(animationTransitionsByType);

            return animationTransitionsByType;
        }

        private static void ApplyAuditedIndividualFastMovementProfiles(
            Dictionary<eChimps, AnimationTransitions> animationTransitionsByType)
        {
            // These are the exact AIState-101 fast-movement pairs audited in
            // the reference DLL. They avoid merging mutually exclusive native
            // branches, notably the horse archer's conditional state 0x111.
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_ARCHER, 1, 0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_SPEARMAN, 1, 0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_MACEMAN, 1, 0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_KNIGHT, 2, 0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_LADDERMAN, 1, 0x1);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_MONK, 1, 0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_ARAB_BOW, 1, 0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_ARAB_SLAVE, 1, 0x1);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_ARAB_SLINGER, 1, 0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_ARAB_HORSEMAN, 2, 0x1);
            SetAuditedProfile(
                animationTransitionsByType,
                eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
                2,
                0x81);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1, 0x5C1);
            SetAuditedProfile(
                animationTransitionsByType,
                eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
                1,
                0x101,
                0x181);
            SetAuditedProfile(
                animationTransitionsByType,
                eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
                2,
                0x1);
            SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, 1, 0x81);
        }

        private static void SetAuditedProfile(
            Dictionary<eChimps, AnimationTransitions> animationTransitionsByType,
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

        private static AnimationTransitions TryExtractIndividualFastMovementCadence(
            ReadOnlySpan<byte> memory,
            ulong handlerStart,
            int handlerLength,
            ulong libraryBase,
            ulong moduleEnd,
            GameUnit* unitArray)
        {
            byte[] codeBytes = NativeHandlerSnapshot.Copy(
                memory,
                handlerStart,
                handlerLength,
                libraryBase);
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
                CloneConstants(preSwitchConstants);
            pending.Enqueue(caseStartIndex);

            while (pending.Count != 0)
            {
                int index = pending.Dequeue();
                Instruction instruction = instructions[index];
                Dictionary<Register, HashSet<long>> constants =
                    CloneConstants(inputConstantsByIndex[index]);

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
                                 SynchronizedMovementCadencePatch.UnitSpeedBonusOffset,
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
                    EnqueueSuccessor(
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
                        EnqueueSuccessor(
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
                        EnqueueSuccessor(
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

                EnqueueSuccessor(
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

        internal sealed class AnimationTransitions
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
