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
using Zhuqiaomon.Memory.Scanners;

namespace SomeSettings
{
    internal enum SynchronizedMovementCadence : byte
    {
        Walking,
        Running
    }

    /// <summary>
    /// Corrects only the walk/run cadence selected by the native unit-type
    /// handlers. Effective movement speed remains entirely controlled by
    /// Vanilla's tribe MovementSpeed and per-unit terrain/state calculation.
    /// </summary>
    internal sealed unsafe class SynchronizedMovementCadencePatch : IDisposable
    {
        private const int MaximumUnitTypeHandlerLength = 0x5000;

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

        private readonly ManualLogSource log;
        private readonly TryGetCadenceDelegate tryGetCadence;
        private readonly HookTransaction transaction;
        private readonly Dictionary<eChimps, AnimationTransitions>
            animationTransitionsByType =
                new Dictionary<eChimps, AnimationTransitions>(
                    (int)eChimps.CHIMP_NUM_TYPES);

        private HookRef<X64InlineHook> movementCadenceHook =
            new HookRef<X64InlineHook>();
        private bool callbackFailureLogged;
        private bool disposed;

        internal delegate bool TryGetCadenceDelegate(
            int tribeId,
            out SynchronizedMovementCadence cadence,
            out ushort runningSpeedBonus);

        public SynchronizedMovementCadencePatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            TryGetCadenceDelegate tryGetCadence)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.tryGetCadence =
                tryGetCadence ?? throw new ArgumentNullException(nameof(tryGetCadence));

            DiscoverRunningAnimationTransitions(memory, libraryBase);

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddContextHook(
                ref movementCadenceHook,
                MovementCadencePattern,
                SynchronizeMovementCadence,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.Commit();

            if (!movementCadenceHook.Success)
            {
                throw new InvalidOperationException(
                    "The native movement cadence calculation was not found.");
            }

            TroopMovementFix3ModLog.Debug(
                log,
                $"Native synchronized-cadence hook installed; " +
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
            if (unitType == eChimps.CHIMP_TYPE_SPEARMAN)
                return improvedSpearmen ? (ushort)1 : (ushort)0;

            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    return GameUnitManagerAPI.Instance
                        .GetDefaultCavalryRunSpeedBonus(unitType);
            }

            if (animationTransitionsByType.TryGetValue(
                    unitType,
                    out AnimationTransitions animationTransitions) &&
                animationTransitions.NativeRunningSpeedBonus.HasValue)
            {
                return animationTransitions.NativeRunningSpeedBonus.Value;
            }

            // An unknown positive value could make a unit exceed its native
            // maximum. Zero is therefore the safe fallback.
            return 0;
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

        private void SynchronizeMovementCadence(
            NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                GameUnit* unit = (GameUnit*)(registers->R8 + 0x65CUL);
                if (unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId == 0 ||
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
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                TroopMovementFix3ModLog.Error(
                    log,
                    $"The synchronized-cadence callback failed; affected " +
                    $"units keep Vanilla cadence: {ex}");
            }
        }

        private void DiscoverRunningAnimationTransitions(
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            DataScanner scanner = DataScanner.Create(memory, libraryBase);
            scanner.Scan(UnitTypeUpdateDispatchPattern);
            if (scanner.CurrentAddress == 0)
            {
                throw new InvalidOperationException(
                    "The native unit-type update dispatch table was not found.");
            }

            int dispatchTableOffset = *(int*)(scanner.CurrentAddress + 4);
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
                    Dictionary<uint, uint> transitions =
                        FindRunningAnimationTransitions(
                            (byte*)handlerStart,
                            handlerLength);

                    if (transitions.Count != 0)
                    {
                        ushort? nativeRunningSpeedBonus =
                            TryFindNativeRunningSpeedBonus(
                                (byte*)handlerStart,
                                handlerLength,
                                out ushort discoveredRunningSpeedBonus)
                                ? discoveredRunningSpeedBonus
                                : (ushort?)null;

                        animationTransitions =
                            new AnimationTransitions(
                                transitions,
                                nativeRunningSpeedBonus);
                    }

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

            if (animationTransitionsByType.Count == 0)
            {
                throw new InvalidOperationException(
                    "No native walking-to-running animation transitions " +
                    "were found.");
            }
        }

        private static Dictionary<uint, uint>
            FindRunningAnimationTransitions(byte* code, int length)
        {
            Dictionary<uint, uint> transitions =
                new Dictionary<uint, uint>();

            // Native form:
            // C7 /0 [manager + unitOffset + 0x660], immediateAnimationState
            for (int index = 3; index + 8 <= length; index++)
            {
                if (code[index - 3] != 0xC7 ||
                    code[index] != 0x60 ||
                    code[index + 1] != 0x06 ||
                    code[index + 2] != 0x00 ||
                    code[index + 3] != 0x00)
                {
                    continue;
                }

                uint runningState =
                    code[index + 4] |
                    ((uint)code[index + 5] << 8) |
                    ((uint)code[index + 6] << 16) |
                    ((uint)code[index + 7] << 24);

                if ((runningState & 0xFF) != 0x81 ||
                    runningState > 0x1000)
                {
                    continue;
                }

                transitions[runningState & ~0x80u] = runningState;
            }

            return transitions;
        }

        private static bool TryFindNativeRunningSpeedBonus(
            byte* code,
            int length,
            out ushort runningSpeedBonus)
        {
            runningSpeedBonus = 0;
            byte[] codeBytes =
                new ReadOnlySpan<byte>(code, length).ToArray();
            Decoder decoder =
                Decoder.Create(64, new ByteArrayCodeReader(codeBytes));
            List<Instruction> instructions =
                new List<Instruction>(2048);

            while (decoder.IP < unchecked((ulong)length) &&
                   instructions.Count < 10000)
            {
                Instruction instruction = decoder.Decode();
                instructions.Add(instruction);
                if (instruction.Mnemonic == Mnemonic.Ret)
                    break;
            }

            bool found = false;
            for (int runningIndex = 0;
                 runningIndex < instructions.Count;
                 runningIndex++)
            {
                Instruction runningInstruction =
                    instructions[runningIndex];
                if (runningInstruction.Mnemonic != Mnemonic.Mov ||
                    runningInstruction.Op0Kind != OpKind.Memory ||
                    runningInstruction.MemoryDisplacement64 != 0x660 ||
                    runningInstruction.Op1Kind != OpKind.Immediate32)
                {
                    continue;
                }

                uint animationState =
                    unchecked((uint)runningInstruction.GetImmediate(1));
                if ((animationState & 0xFF) != 0x81 ||
                    animationState > 0x1000)
                {
                    continue;
                }

                bool cadenceResolvedForState = false;
                for (int distance = 1;
                     distance <= 12 && !cadenceResolvedForState;
                     distance++)
                {
                    int precedingIndex = runningIndex - distance;
                    int followingIndex = runningIndex + distance;

                    if (precedingIndex >= 0 &&
                        TryResolveNearbySpeedBonus(
                            instructions,
                            precedingIndex,
                            out ushort precedingSpeedBonus))
                    {
                        if (found &&
                            runningSpeedBonus != precedingSpeedBonus)
                        {
                            return false;
                        }

                        runningSpeedBonus = precedingSpeedBonus;
                        found = true;
                        cadenceResolvedForState = true;
                        continue;
                    }

                    if (followingIndex < instructions.Count &&
                        TryResolveNearbySpeedBonus(
                            instructions,
                            followingIndex,
                            out ushort followingSpeedBonus))
                    {
                        if (found &&
                            runningSpeedBonus != followingSpeedBonus)
                        {
                            return false;
                        }

                        runningSpeedBonus = followingSpeedBonus;
                        found = true;
                        cadenceResolvedForState = true;
                    }
                }
            }

            return found;
        }

        private static bool TryResolveNearbySpeedBonus(
            List<Instruction> instructions,
            int storeIndex,
            out ushort speedBonus)
        {
            speedBonus = 0;
            Instruction storeInstruction = instructions[storeIndex];
            if (storeInstruction.Mnemonic != Mnemonic.Mov ||
                storeInstruction.Op0Kind != OpKind.Memory ||
                storeInstruction.MemoryDisplacement64 != 0x916)
            {
                return false;
            }

            if (IsImmediate(storeInstruction.Op1Kind))
            {
                speedBonus =
                    unchecked(
                        (ushort)storeInstruction.GetImmediate(1));
                return true;
            }

            if (storeInstruction.Op1Kind != OpKind.Register)
                return false;

            Register sourceRegister =
                NormalizeRegister(storeInstruction.Op1Register);
            for (int instructionIndex = storeIndex - 1;
                 instructionIndex >= 0;
                 instructionIndex--)
            {
                Instruction instruction =
                    instructions[instructionIndex];
                if (instruction.Op0Kind != OpKind.Register ||
                    NormalizeRegister(instruction.Op0Register) !=
                        sourceRegister)
                {
                    continue;
                }

                if (instruction.Mnemonic == Mnemonic.Mov &&
                    IsImmediate(instruction.Op1Kind))
                {
                    speedBonus =
                        unchecked(
                            (ushort)instruction.GetImmediate(1));
                    return true;
                }

                if ((instruction.Mnemonic == Mnemonic.Xor ||
                     instruction.Mnemonic == Mnemonic.Sub) &&
                    instruction.Op1Kind == OpKind.Register &&
                    NormalizeRegister(instruction.Op1Register) ==
                        sourceRegister)
                {
                    speedBonus = 0;
                    return true;
                }

                return false;
            }

            return false;
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

        private sealed class AnimationTransitions
        {
            private readonly Dictionary<uint, uint> walkingToRunning;
            private readonly Dictionary<uint, uint> runningToWalking;

            public AnimationTransitions(
                Dictionary<uint, uint> walkingToRunning,
                ushort? nativeRunningSpeedBonus)
            {
                this.walkingToRunning =
                    walkingToRunning ??
                    throw new ArgumentNullException(
                        nameof(walkingToRunning));
                NativeRunningSpeedBonus = nativeRunningSpeedBonus;
                runningToWalking =
                    new Dictionary<uint, uint>(walkingToRunning.Count);

                foreach (KeyValuePair<uint, uint> transition
                         in walkingToRunning)
                {
                    runningToWalking[transition.Value] = transition.Key;
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
        }
    }
}
