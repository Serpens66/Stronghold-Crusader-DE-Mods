using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    /// <summary>
    /// Reads the installed game's per-unit update handlers without calling or patching them.
    /// It deliberately returns every statically reachable AI-state-101 speed-bonus value;
    /// callers must treat that set conservatively rather than guessing a unit profile.
    /// </summary>
    internal sealed unsafe class NativeMovementCadenceResolver
    {
        private const int UnitTypeUpdateDispatchRva = 0x18410C;
        private const int MaximumUnitTypeHandlerLength = 0x5000;
        private const int MaximumCadenceCaseLength = 0x240;
        private const ushort IndividualFastMovementAiState = 101;
        private const int ManagerRelativeAiStateOffset = 0x918;
        private const int ManagerRelativeSpeedBonusOffset = 0x916;
        private const int MaximumPlausibleProfiles = 4;
        private const int MaximumNativeSpeedBonus = 32;

        private const string UnitTypeUpdateDispatchPattern =
            "41 FF 94 C6 ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? 48 63 C2 48 69 C8 90 04 00 00";

        private readonly ulong libraryBase;
        private readonly ulong moduleEnd;
        private readonly ulong nativeUnitManager;
        private readonly HandlerProfile[] profiles;

        public NativeMovementCadenceResolver(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong nativeUnitManager,
            ManualLogSource log)
        {
            this.libraryBase = libraryBase;
            moduleEnd = libraryBase + unchecked((ulong)memory.Length);
            this.nativeUnitManager = nativeUnitManager;

            Shared.NativeResolution dispatch = Shared.NativePatternResolver.ResolveUnique(
                memory,
                UnitTypeUpdateDispatchPattern,
                UnitTypeUpdateDispatchRva,
                referenceHashMatches: true,
                name: "unit-type update dispatch for movement cadence",
                log: log);
            ulong dispatchAddress = libraryBase + unchecked((ulong)dispatch.Rva);
            int dispatchTableOffset = *(int*)(dispatchAddress + 4);
            ulong dispatchTable = libraryBase + unchecked((uint)dispatchTableOffset);
            int unitTypeCount = (int)eChimps.CHIMP_NUM_TYPES;
            if (!IsModuleRange(dispatchTable, checked(unitTypeCount * sizeof(ulong))))
                throw new InvalidOperationException("The unit-type update dispatch table is outside the game module.");

            var handlersByType = new ulong[unitTypeCount];
            var uniqueHandlers = new SortedSet<ulong>();
            ulong* nativeHandlers = (ulong*)dispatchTable;
            for (int unitType = 0; unitType < unitTypeCount; unitType++)
            {
                ulong handler = nativeHandlers[unitType];
                if (handler < libraryBase || handler >= moduleEnd)
                    continue;
                handlersByType[unitType] = handler;
                uniqueHandlers.Add(handler);
            }

            var sortedHandlers = new List<ulong>(uniqueHandlers);
            var decodedByHandler = new Dictionary<ulong, HandlerProfile>();
            profiles = new HandlerProfile[unitTypeCount];
            int resolvedTypes = 0;
            for (int unitType = 0; unitType < unitTypeCount; unitType++)
            {
                ulong handler = handlersByType[unitType];
                if (handler == 0)
                    continue;
                if (!decodedByHandler.TryGetValue(handler, out HandlerProfile profile))
                {
                    int handlerIndex = sortedHandlers.BinarySearch(handler);
                    ulong handlerEnd = handlerIndex >= 0 && handlerIndex + 1 < sortedHandlers.Count
                        ? sortedHandlers[handlerIndex + 1]
                        : Math.Min(handler + MaximumUnitTypeHandlerLength, moduleEnd);
                    if (handlerEnd <= handler || handlerEnd - handler > MaximumUnitTypeHandlerLength)
                        handlerEnd = Math.Min(handler + MaximumUnitTypeHandlerLength, moduleEnd);
                    profile = DecodeHandler(handler, checked((int)(handlerEnd - handler)));
                    decodedByHandler.Add(handler, profile);
                }
                profiles[unitType] = profile;
                if (profile != null)
                    resolvedTypes++;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bugfixes and QoL stage=friendly-moat-movement-weighted-cadence-resolver dispatchRva=0x{dispatch.Rva:X} " +
                $"tableRva=0x{dispatchTable - libraryBase:X} handlers={uniqueHandlers.Count} " +
                $"resolvedTypes={resolvedTypes}/{unitTypeCount} mode=read-only-no-audited-type-table.");
        }

        public bool TryGetPlausibleSpeedBonuses(
            int unitType,
            int runtimeSpeedBonus,
            out int[] values,
            out ulong handlerRva,
            out string rejectionReason)
        {
            values = null;
            handlerRva = 0;
            rejectionReason = null;
            if (unitType < 0 || unitType >= profiles.Length || runtimeSpeedBonus < 0 ||
                runtimeSpeedBonus > short.MaxValue)
            {
                rejectionReason = "invalid-cadence-unit-or-runtime-bonus";
                return false;
            }

            HandlerProfile profile = profiles[unitType];
            if (profile == null || profile.SpeedBonuses == null)
            {
                rejectionReason = "native-cadence-handler-unresolved";
                return false;
            }

            var candidates = new SortedSet<int>(profile.SpeedBonuses) { runtimeSpeedBonus };
            if (candidates.Count > MaximumPlausibleProfiles)
            {
                rejectionReason = "native-cadence-handler-ambiguous";
                return false;
            }

            values = new int[candidates.Count];
            candidates.CopyTo(values);
            handlerRva = profile.HandlerAddress - libraryBase;
            return true;
        }

        private HandlerProfile DecodeHandler(ulong handlerAddress, int handlerLength)
        {
            if (handlerLength <= 0 || !IsModuleRange(handlerAddress, handlerLength))
                return null;

            byte[] bytes = new ReadOnlySpan<byte>((byte*)handlerAddress, handlerLength).ToArray();
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = handlerAddress;
            var instructions = new List<Instruction>(2048);
            var instructionIndexByIp = new Dictionary<ulong, int>();
            ulong end = handlerAddress + unchecked((ulong)handlerLength);
            while (decoder.IP < end && instructions.Count < 10000)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.Code == Code.INVALID)
                    break;
                instructionIndexByIp[instruction.IP] = instructions.Count;
                instructions.Add(instruction);
            }

            if (!TryResolveAiStateCaseTarget(
                    instructions,
                    IndividualFastMovementAiState,
                    out ulong caseTarget,
                    out int stateLoadIndex) ||
                !instructionIndexByIp.TryGetValue(caseTarget, out int caseStartIndex))
            {
                return null;
            }

            ulong caseEnd = Math.Min(caseTarget + MaximumCadenceCaseLength, end);
            Dictionary<Register, HashSet<long>> preSwitchConstants =
                FindPreSwitchConstants(instructions, stateLoadIndex);
            HashSet<int> bonuses = DiscoverReachableSpeedBonusWrites(
                instructions,
                instructionIndexByIp,
                caseStartIndex,
                caseEnd,
                preSwitchConstants);
            // A successfully resolved state branch with no speed-bonus store means that
            // this handler preserves the concrete runtime value for that branch.
            return new HandlerProfile(handlerAddress, ToSortedArray(bonuses));
        }

        private bool TryResolveAiStateCaseTarget(
            List<Instruction> instructions,
            ushort aiState,
            out ulong caseTarget,
            out int stateLoadIndex)
        {
            caseTarget = 0;
            stateLoadIndex = -1;
            for (int loadIndex = 0; loadIndex < instructions.Count; loadIndex++)
            {
                Instruction stateLoad = instructions[loadIndex];
                if ((stateLoad.Mnemonic != Mnemonic.Mov &&
                     stateLoad.Mnemonic != Mnemonic.Movzx &&
                     stateLoad.Mnemonic != Mnemonic.Movsx &&
                     stateLoad.Mnemonic != Mnemonic.Movsxd) ||
                    stateLoad.Op0Kind != OpKind.Register ||
                    stateLoad.Op1Kind != OpKind.Memory ||
                    !IsUnitFieldMemoryOperand(
                        instructions, loadIndex, ManagerRelativeAiStateOffset))
                {
                    continue;
                }

                Register stateRegister = NormalizeRegister(stateLoad.Op0Register);
                int searchEnd = Math.Min(loadIndex + 80, instructions.Count);
                for (int mapIndex = loadIndex + 1; mapIndex < searchEnd; mapIndex++)
                {
                    Instruction mapLoad = instructions[mapIndex];
                    if (mapLoad.Mnemonic != Mnemonic.Movzx ||
                        mapLoad.Op0Kind != OpKind.Register ||
                        mapLoad.Op1Kind != OpKind.Memory ||
                        NormalizeRegister(mapLoad.MemoryIndex) != stateRegister ||
                        !TryResolveMemoryTableAddress(
                            instructions, mapIndex, mapLoad, out ulong stateMap) ||
                        !IsModuleRange(stateMap + aiState, 1))
                    {
                        continue;
                    }

                    byte compressedCase = *((byte*)stateMap + aiState);
                    Register compressedRegister = NormalizeRegister(mapLoad.Op0Register);
                    int tableSearchEnd = Math.Min(mapIndex + 12, instructions.Count);
                    for (int tableIndex = mapIndex + 1; tableIndex < tableSearchEnd; tableIndex++)
                    {
                        Instruction tableLoad = instructions[tableIndex];
                        if ((tableLoad.Mnemonic != Mnemonic.Mov &&
                             tableLoad.Mnemonic != Mnemonic.Movsxd) ||
                            tableLoad.Op0Kind != OpKind.Register ||
                            tableLoad.Op1Kind != OpKind.Memory ||
                            NormalizeRegister(tableLoad.MemoryIndex) != compressedRegister ||
                            tableLoad.MemoryIndexScale != 4 ||
                            !TryResolveMemoryTableAddress(
                                instructions, tableIndex, tableLoad, out ulong jumpTable) ||
                            !IsModuleRange(jumpTable + unchecked((ulong)compressedCase * 4), 4))
                        {
                            continue;
                        }

                        uint targetRva = *(uint*)(jumpTable + unchecked((ulong)compressedCase * 4));
                        ulong target = libraryBase + targetRva;
                        if (target >= libraryBase && target < moduleEnd)
                        {
                            caseTarget = target;
                            stateLoadIndex = loadIndex;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private HashSet<int> DiscoverReachableSpeedBonusWrites(
            List<Instruction> instructions,
            Dictionary<ulong, int> instructionIndexByIp,
            int caseStartIndex,
            ulong caseEnd,
            Dictionary<Register, HashSet<long>> initialConstants)
        {
            var inputConstants = new Dictionary<int, Dictionary<Register, HashSet<long>>>();
            var pending = new Queue<int>();
            var bonuses = new HashSet<int>();
            inputConstants[caseStartIndex] = CloneConstants(initialConstants);
            pending.Enqueue(caseStartIndex);

            while (pending.Count != 0)
            {
                int index = pending.Dequeue();
                if (index < caseStartIndex || index >= instructions.Count ||
                    instructions[index].IP >= caseEnd)
                {
                    continue;
                }
                Instruction instruction = instructions[index];
                Dictionary<Register, HashSet<long>> constants =
                    CloneConstants(inputConstants[index]);

                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    IsUnitFieldMemoryOperand(
                        instructions, index, ManagerRelativeSpeedBonusOffset) &&
                    TryGetOperandConstants(instruction, 1, constants, out HashSet<long> storedValues))
                {
                    foreach (long storedValue in storedValues)
                    {
                        int value = unchecked((short)(ushort)storedValue);
                        if (value >= 0 && value <= MaximumNativeSpeedBonus)
                            bonuses.Add(value);
                    }
                }

                ApplyConstantTransfer(instruction, constants);
                if (instruction.FlowControl == FlowControl.Call ||
                    instruction.FlowControl == FlowControl.IndirectCall)
                {
                    RemoveVolatileRegisterConstants(constants);
                }

                if (instruction.FlowControl == FlowControl.ConditionalBranch)
                {
                    EnqueueSuccessor(index + 1, instructions, caseStartIndex, caseEnd,
                        constants, inputConstants, pending);
                    if (instructionIndexByIp.TryGetValue(
                            instruction.NearBranchTarget, out int branchIndex))
                    {
                        EnqueueSuccessor(branchIndex, instructions, caseStartIndex, caseEnd,
                            constants, inputConstants, pending);
                    }
                    continue;
                }
                if (instruction.FlowControl == FlowControl.UnconditionalBranch)
                {
                    if (instructionIndexByIp.TryGetValue(
                            instruction.NearBranchTarget, out int branchIndex))
                    {
                        EnqueueSuccessor(branchIndex, instructions, caseStartIndex, caseEnd,
                            constants, inputConstants, pending);
                    }
                    continue;
                }
                if (instruction.FlowControl == FlowControl.Return ||
                    instruction.FlowControl == FlowControl.IndirectBranch ||
                    instruction.FlowControl == FlowControl.Interrupt)
                {
                    continue;
                }
                EnqueueSuccessor(index + 1, instructions, caseStartIndex, caseEnd,
                    constants, inputConstants, pending);
            }
            return bonuses;
        }

        private static void EnqueueSuccessor(
            int successor,
            List<Instruction> instructions,
            int caseStart,
            ulong caseEnd,
            Dictionary<Register, HashSet<long>> constants,
            Dictionary<int, Dictionary<Register, HashSet<long>>> inputs,
            Queue<int> pending)
        {
            if (successor < caseStart || successor >= instructions.Count ||
                instructions[successor].IP >= caseEnd)
            {
                return;
            }
            if (!inputs.TryGetValue(successor, out Dictionary<Register, HashSet<long>> existing))
            {
                inputs[successor] = CloneConstants(constants);
                pending.Enqueue(successor);
                return;
            }
            if (MergeConstants(existing, constants))
                pending.Enqueue(successor);
        }

        private bool IsUnitFieldMemoryOperand(
            List<Instruction> instructions,
            int instructionIndex,
            int fieldOffset)
        {
            Instruction memoryInstruction = instructions[instructionIndex];
            if (memoryInstruction.MemoryDisplacement64 == unchecked((ulong)fieldOffset))
                return true;

            Register baseRegister = NormalizeRegister(memoryInstruction.MemoryBase);
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
                if ((instruction.Mnemonic == Mnemonic.Add || instruction.Mnemonic == Mnemonic.Sub) &&
                    instruction.Op1Kind == OpKind.Register)
                {
                    additiveRegisters.Add(NormalizeRegister(instruction.Op1Register));
                    continue;
                }
                if (instruction.Mnemonic != Mnemonic.Lea)
                    return false;

                ulong fieldAddress;
                if (instruction.IsIPRelativeMemoryOperand)
                    fieldAddress = instruction.IPRelativeMemoryAddress;
                else if (instruction.MemoryIndex == Register.None &&
                         TryResolveRegisterAddress(
                             instructions, index - 1,
                             NormalizeRegister(instruction.MemoryBase), out ulong baseAddress))
                    fieldAddress = baseAddress + instruction.MemoryDisplacement64;
                else if (instruction.MemoryDisplacement64 == unchecked((ulong)fieldOffset))
                {
                    additiveRegisters.Add(NormalizeRegister(instruction.MemoryBase));
                    if (instruction.MemoryIndex != Register.None)
                        additiveRegisters.Add(NormalizeRegister(instruction.MemoryIndex));
                    foreach (Register component in additiveRegisters)
                    {
                        if (TryResolveRegisterAddress(
                                instructions, index - 1, component, out ulong componentAddress) &&
                            componentAddress == nativeUnitManager)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                else
                    return false;
                return fieldAddress == nativeUnitManager + unchecked((ulong)fieldOffset);
            }
            return false;
        }

        private static Dictionary<Register, HashSet<long>> FindPreSwitchConstants(
            List<Instruction> instructions, int stateLoadIndex)
        {
            var constants = new Dictionary<Register, HashSet<long>>();
            for (int index = 0; index < stateLoadIndex; index++)
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

        private static bool MergeConstants(
            Dictionary<Register, HashSet<long>> existing,
            Dictionary<Register, HashSet<long>> incoming)
        {
            bool changed = false;
            var registers = new List<Register>(existing.Keys);
            foreach (Register register in registers)
            {
                if (!incoming.TryGetValue(register, out HashSet<long> incomingValues))
                {
                    existing.Remove(register);
                    changed = true;
                    continue;
                }
                int oldCount = existing[register].Count;
                existing[register].UnionWith(incomingValues);
                if (existing[register].Count > 32)
                {
                    existing.Remove(register);
                    changed = true;
                }
                else if (existing[register].Count != oldCount)
                    changed = true;
            }
            return changed;
        }

        private static Dictionary<Register, HashSet<long>> CloneConstants(
            Dictionary<Register, HashSet<long>> source)
        {
            var clone = new Dictionary<Register, HashSet<long>>(source.Count);
            foreach (KeyValuePair<Register, HashSet<long>> pair in source)
                clone[pair.Key] = new HashSet<long>(pair.Value);
            return clone;
        }

        private static void ApplyConstantTransfer(
            Instruction instruction,
            Dictionary<Register, HashSet<long>> constants)
        {
            if (instruction.Op0Kind != OpKind.Register)
                return;
            Register destination = NormalizeRegister(instruction.Op0Register);
            if (instruction.Mnemonic == Mnemonic.Cmp || instruction.Mnemonic == Mnemonic.Test)
                return;
            if (instruction.Mnemonic.ToString().StartsWith("Cmov", StringComparison.Ordinal))
            {
                if (constants.TryGetValue(destination, out HashSet<long> oldValues) &&
                    TryGetOperandConstants(instruction, 1, constants, out HashSet<long> selected))
                {
                    var combined = new HashSet<long>(oldValues);
                    combined.UnionWith(selected);
                    SetRegisterConstants(constants, destination, combined);
                }
                else
                    constants.Remove(destination);
                return;
            }
            if (instruction.Mnemonic == Mnemonic.Lea)
            {
                if (TryEvaluateLeaConstants(instruction, constants, out HashSet<long> values))
                    SetRegisterConstants(constants, destination, values);
                else
                    constants.Remove(destination);
                return;
            }
            if (instruction.Mnemonic == Mnemonic.Mov ||
                instruction.Mnemonic == Mnemonic.Movzx ||
                instruction.Mnemonic == Mnemonic.Movsx ||
                instruction.Mnemonic == Mnemonic.Movsxd)
            {
                if (TryGetOperandConstants(instruction, 1, constants, out HashSet<long> values))
                    SetRegisterConstants(constants, destination, values);
                else
                    constants.Remove(destination);
                return;
            }
            if ((instruction.Mnemonic == Mnemonic.Xor || instruction.Mnemonic == Mnemonic.Sub) &&
                instruction.Op1Kind == OpKind.Register &&
                NormalizeRegister(instruction.Op1Register) == destination)
            {
                constants[destination] = new HashSet<long> { 0 };
                return;
            }
            if ((instruction.Mnemonic == Mnemonic.Add || instruction.Mnemonic == Mnemonic.Sub) &&
                constants.TryGetValue(destination, out HashSet<long> leftValues) &&
                TryGetOperandConstants(instruction, 1, constants, out HashSet<long> rightValues))
            {
                var results = new HashSet<long>();
                foreach (long left in leftValues)
                foreach (long right in rightValues)
                    results.Add(instruction.Mnemonic == Mnemonic.Add
                        ? unchecked(left + right)
                        : unchecked(left - right));
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
            values = new HashSet<long> { unchecked((long)instruction.MemoryDisplacement64) };
            Register baseRegister = NormalizeRegister(instruction.MemoryBase);
            if (baseRegister != Register.None)
            {
                if (!constants.TryGetValue(baseRegister, out HashSet<long> baseValues))
                {
                    values = null;
                    return false;
                }
                values = AddConstantProducts(values, baseValues, 1);
            }
            Register indexRegister = NormalizeRegister(instruction.MemoryIndex);
            if (indexRegister != Register.None)
            {
                if (!constants.TryGetValue(indexRegister, out HashSet<long> indexValues))
                {
                    values = null;
                    return false;
                }
                values = AddConstantProducts(values, indexValues, instruction.MemoryIndexScale);
            }
            return values.Count != 0 && values.Count <= 32;
        }

        private static HashSet<long> AddConstantProducts(
            HashSet<long> leftValues, HashSet<long> rightValues, int multiplier)
        {
            var results = new HashSet<long>();
            foreach (long left in leftValues)
            foreach (long right in rightValues)
                results.Add(unchecked(left + right * multiplier));
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
                values = new HashSet<long> { unchecked((long)instruction.GetImmediate(operand)) };
                return true;
            }
            if (kind == OpKind.Register &&
                constants.TryGetValue(
                    NormalizeRegister(instruction.GetOpRegister(operand)), out HashSet<long> found))
            {
                values = new HashSet<long>(found);
                return true;
            }
            values = null;
            return false;
        }

        private bool TryResolveMemoryTableAddress(
            List<Instruction> instructions,
            int instructionIndex,
            Instruction instruction,
            out ulong address)
        {
            address = instruction.MemoryDisplacement64;
            if (instruction.IsIPRelativeMemoryOperand)
            {
                address = instruction.IPRelativeMemoryAddress;
                return true;
            }
            Register baseRegister = NormalizeRegister(instruction.MemoryBase);
            if (baseRegister == Register.None)
                return address != 0;
            if (!TryResolveRegisterAddress(
                    instructions, instructionIndex - 1, baseRegister, out ulong baseAddress))
            {
                return false;
            }
            address = baseAddress + instruction.MemoryDisplacement64;
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
            for (int index = startIndex; index >= 0 && startIndex - index <= 80; index--)
            {
                Instruction instruction = instructions[index];
                if (instruction.Op0Kind != OpKind.Register ||
                    NormalizeRegister(instruction.Op0Register) != register)
                    continue;
                if (instruction.Mnemonic == Mnemonic.Lea && instruction.IsIPRelativeMemoryOperand)
                {
                    address = instruction.IPRelativeMemoryAddress;
                    return true;
                }
                if (instruction.Mnemonic == Mnemonic.Mov && IsImmediate(instruction.Op1Kind))
                {
                    address = instruction.GetImmediate(1);
                    return true;
                }
                return false;
            }
            return false;
        }

        private bool IsModuleRange(ulong address, int length) =>
            length >= 0 && address >= libraryBase && address <= moduleEnd &&
            unchecked((ulong)length) <= moduleEnd - address;

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

        private static bool IsImmediate(OpKind kind)
        {
            switch (kind)
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
                case Register.AL: case Register.AH: case Register.AX:
                case Register.EAX: case Register.RAX: return Register.RAX;
                case Register.CL: case Register.CH: case Register.CX:
                case Register.ECX: case Register.RCX: return Register.RCX;
                case Register.DL: case Register.DH: case Register.DX:
                case Register.EDX: case Register.RDX: return Register.RDX;
                case Register.BL: case Register.BH: case Register.BX:
                case Register.EBX: case Register.RBX: return Register.RBX;
                case Register.SPL: case Register.SP: case Register.ESP:
                case Register.RSP: return Register.RSP;
                case Register.BPL: case Register.BP: case Register.EBP:
                case Register.RBP: return Register.RBP;
                case Register.SIL: case Register.SI: case Register.ESI:
                case Register.RSI: return Register.RSI;
                case Register.DIL: case Register.DI: case Register.EDI:
                case Register.RDI: return Register.RDI;
                case Register.R8L: case Register.R8W: case Register.R8D:
                case Register.R8: return Register.R8;
                case Register.R9L: case Register.R9W: case Register.R9D:
                case Register.R9: return Register.R9;
                case Register.R10L: case Register.R10W: case Register.R10D:
                case Register.R10: return Register.R10;
                case Register.R11L: case Register.R11W: case Register.R11D:
                case Register.R11: return Register.R11;
                case Register.R12L: case Register.R12W: case Register.R12D:
                case Register.R12: return Register.R12;
                case Register.R13L: case Register.R13W: case Register.R13D:
                case Register.R13: return Register.R13;
                case Register.R14L: case Register.R14W: case Register.R14D:
                case Register.R14: return Register.R14;
                case Register.R15L: case Register.R15W: case Register.R15D:
                case Register.R15: return Register.R15;
                default: return register;
            }
        }

        private static int[] ToSortedArray(HashSet<int> values)
        {
            int[] result = new int[values.Count];
            values.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        private sealed class HandlerProfile
        {
            public HandlerProfile(ulong handlerAddress, int[] speedBonuses)
            {
                HandlerAddress = handlerAddress;
                SpeedBonuses = speedBonuses;
            }

            public ulong HandlerAddress { get; }
            public int[] SpeedBonuses { get; }
        }
    }
}
