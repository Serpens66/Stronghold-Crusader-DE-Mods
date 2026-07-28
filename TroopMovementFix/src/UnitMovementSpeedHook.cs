using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;
using Zhuqiaomon.Memory.Scanners;

namespace TroopMovementFix
{
    internal enum MovementCadenceMode
    {
        SynchronizedWalking,
        SynchronizedRunning,
        UncappedRunning
    }

    internal readonly struct UnitMovementDirective
    {
        public UnitMovementDirective(
            MovementCadenceMode movementMode,
            ushort synchronizedSpeed,
            ushort runningSpeedBonus)
        {
            MovementMode = movementMode;
            SynchronizedSpeed = synchronizedSpeed;
            RunningSpeedBonus = runningSpeedBonus;
        }

        public MovementCadenceMode MovementMode { get; }
        public ushort SynchronizedSpeed { get; }
        public ushort RunningSpeedBonus { get; }
    }

    internal sealed unsafe class UnitMovementSpeedHook : IDisposable
    {
        private const int MaximumUnitTypeHandlerLength = 0x5000;

        // updateUnits pass 4:
        // call qword ptr [moduleBase + unitType * 8 + dispatchTableOffset]
        // mov edx, [currentUnitId]
        // movsxd rax, edx
        // imul rcx, rax, sizeof(GameUnit)
        private const string UnitTypeUpdateDispatchPattern =
            "41 FF 94 C6 ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? 48 63 C2 48 69 C8 90 04 00 00";

        // c_game_unit_calculate_movement_speed:
        // prologue followed by unitId * sizeof(GameUnit) (0x490).
        private const string CalculateMovementSpeedPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 " +
            "4C 8D 35 ?? ?? ?? ?? 48 63 C2 48 69 D8 90 04 00 00";

        // Common movement cadence:
        // movsx eax, word ptr [r8+916h] ; movement sub-step bonus
        // movsx ecx, word ptr [r8+9A2h] ; effective speed delay
        // mov r10d, dword ptr [r8+9A8h]
        //
        // The bonus participates in the cadence threshold before it is passed to
        // processUnitMove. Synchronizing it here keeps the interval and the number
        // of executed sub-steps consistent.
        private const string MovementCadencePattern =
            "41 0F BF 80 16 09 00 00 41 0F BF 88 A2 09 00 00 45 8B 90 A8 09 00 00";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CalculateMovementSpeedDelegate(
            NativePointer<GameUnitManager> unitManager,
            int unitId,
            byte preserveCurrentHeight);

        private readonly ManualLogSource log;
        private readonly TryGetMovementDirectiveDelegate tryGetMovementDirective;
        private readonly HookTransaction transaction;
        private readonly GameUnit* unitArray;
        private readonly int unitArrayLength;
        private readonly Dictionary<eChimps, AnimationTransitions> animationTransitionsByType =
            new Dictionary<eChimps, AnimationTransitions>();
        private HookRef<X64ManagedFunctionDetourAOB<CalculateMovementSpeedDelegate>> hook =
            new HookRef<X64ManagedFunctionDetourAOB<CalculateMovementSpeedDelegate>>();
        private HookRef<X64InlineHook> movementCadenceHook = new HookRef<X64InlineHook>();

        private bool callbackFailureLogged;
        private bool cadenceCallbackFailureLogged;
        private bool disposed;

        internal delegate bool TryGetMovementDirectiveDelegate(
            int unitId,
            out UnitMovementDirective movementDirective);

        public UnitMovementSpeedHook(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            TryGetMovementDirectiveDelegate tryGetMovementDirective)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.tryGetMovementDirective =
                tryGetMovementDirective ?? throw new ArgumentNullException(nameof(tryGetMovementDirective));

            var units = GameUnitManagerAPI.Instance.GetUnitArray();
            unitArray = units._array;
            unitArrayLength = units.Length;
            if (unitArray == null || unitArrayLength <= 0)
                throw new InvalidOperationException("The native unit array is not available.");

            DiscoverRunningAnimationTransitions(memory, libraryBase);

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddDetour(
                ref hook,
                CalculateMovementSpeedPattern,
                CalculateMovementSpeed);

            transaction.AddContextHook(
                ref movementCadenceHook,
                MovementCadencePattern,
                SynchronizeMovementCadence,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.Commit();

            if (!hook.Success)
                throw new InvalidOperationException("The native unit movement-speed calculation function was not found.");

            if (!movementCadenceHook.Success)
                throw new InvalidOperationException("The native movement cadence calculation was not found.");

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Native movement-speed and movement-cadence hooks installed successfully; " +
                $"cachedUnitArrayLength={unitArrayLength}.");
        }

        public bool SupportsSynchronizedRunning(eChimps unitType)
        {
            return animationTransitionsByType.ContainsKey(unitType);
        }

        public ushort GetNativeRunningSpeedBonus(eChimps unitType, bool improvedSpearmen)
        {
            if (unitType == eChimps.CHIMP_TYPE_SPEARMAN)
                return improvedSpearmen ? (ushort)1 : (ushort)0;

            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    return GameUnitManagerAPI.Instance.GetDefaultCavalryRunSpeedBonus(unitType);
            }

            if (animationTransitionsByType.TryGetValue(
                    unitType,
                    out AnimationTransitions animationTransitions) &&
                animationTransitions.NativeRunningSpeedBonus.HasValue)
            {
                return animationTransitions.NativeRunningSpeedBonus.Value;
            }

            // No value is safer than a generic bonus: an unknown positive value can
            // make a unit exceed its native maximum. Native handlers still control
            // types for which no running animation transition was discovered.
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

        private void SynchronizeMovementCadence(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                GameUnit* unit = (GameUnit*)(registers->R8 + 0x65CUL);
                if (unit == null || unit->r_AliveState != AliveState.IsAlive)
                    return;

                if (unitArray == null || unit < unitArray)
                    return;

                long unitIndex = unit - unitArray;
                if (unitIndex < 0 || unitIndex >= unitArrayLength)
                    return;

                int unitId = checked((int)unitIndex + 1);
                if (!tryGetMovementDirective(unitId, out UnitMovementDirective movementDirective))
                    return;

                MovementCadenceMode movementMode = movementDirective.MovementMode;
                uint originalAnimationState = unit->N000000F4;
                animationTransitionsByType.TryGetValue(
                    unit->r_UnitChimp,
                    out AnimationTransitions animationTransitions);

                if (movementMode != MovementCadenceMode.SynchronizedWalking)
                {
                    if (animationTransitions != null)
                        unit->r_SpeedBonus = movementDirective.RunningSpeedBonus;

                    // Type handlers use several animation families. Their native running
                    // state is the corresponding walking state plus the 0x80 run flag,
                    // for example 0x1 -> 0x81, 0x101 -> 0x181, or 0x201 -> 0x281.
                    // Only apply a transition which was actually found in this unit
                    // type's native handler.
                    if (animationTransitions != null &&
                        animationTransitions.TryGetRunningState(
                            originalAnimationState,
                            out uint runningAnimationState))
                    {
                        unit->N000000F4 = runningAnimationState;
                    }
                }
                else
                {
                    // Per-type handlers run before this common cadence code and can
                    // restore their own running state and bonus. Improved Spearmen are
                    // the confirmed example. Undo any such late override generically
                    // when this tracked group must walk.
                    unit->r_SpeedBonus = 0;
                    if (animationTransitions != null &&
                        animationTransitions.TryGetWalkingState(
                            originalAnimationState,
                            out uint walkingAnimationState))
                    {
                        unit->N000000F4 = walkingAnimationState;
                    }
                }
            }
            catch (Exception ex)
            {
                if (cadenceCallbackFailureLogged)
                    return;

                cadenceCallbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"The movement-cadence synchronization callback failed; affected units keep vanilla cadence: {ex}");
            }
        }

        private void DiscoverRunningAnimationTransitions(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            DataScanner scanner = DataScanner.Create(memory, libraryBase);
            scanner.Scan(UnitTypeUpdateDispatchPattern);
            if (scanner.CurrentAddress == 0)
                throw new InvalidOperationException("The native unit-type update dispatch table was not found.");

            int dispatchTableOffset = *(int*)(scanner.CurrentAddress + 4);
            ulong dispatchTableAddress = libraryBase + unchecked((uint)dispatchTableOffset);
            ulong moduleEnd = libraryBase + unchecked((ulong)memory.Length);
            int unitTypeCount = (int)eChimps.CHIMP_NUM_TYPES;

            if (dispatchTableAddress < libraryBase ||
                dispatchTableAddress + unchecked((ulong)(unitTypeCount * sizeof(ulong))) > moduleEnd)
            {
                throw new InvalidOperationException("The native unit-type update dispatch table is outside the game module.");
            }

            ulong* handlers = (ulong*)dispatchTableAddress;
            ulong[] handlerByType = new ulong[unitTypeCount];
            SortedSet<ulong> uniqueHandlers = new SortedSet<ulong>();

            for (int unitTypeValue = 0; unitTypeValue < unitTypeCount; unitTypeValue++)
            {
                ulong handler = handlers[unitTypeValue];
                if (handler < libraryBase || handler >= moduleEnd)
                    continue;

                handlerByType[unitTypeValue] = handler;
                uniqueHandlers.Add(handler);
            }

            List<ulong> sortedHandlers = new List<ulong>(uniqueHandlers);
            int nativeRunningSpeedBonusCount = 0;

            for (int unitTypeValue = 0; unitTypeValue < unitTypeCount; unitTypeValue++)
            {
                ulong handlerStart = handlerByType[unitTypeValue];
                if (handlerStart == 0)
                    continue;

                int handlerIndex = sortedHandlers.BinarySearch(handlerStart);
                ulong handlerEnd = handlerIndex >= 0 && handlerIndex + 1 < sortedHandlers.Count
                    ? sortedHandlers[handlerIndex + 1]
                    : Math.Min(handlerStart + MaximumUnitTypeHandlerLength, moduleEnd);

                if (handlerEnd <= handlerStart ||
                    handlerEnd - handlerStart > MaximumUnitTypeHandlerLength)
                {
                    handlerEnd = Math.Min(handlerStart + MaximumUnitTypeHandlerLength, moduleEnd);
                }

                Dictionary<uint, uint> transitions = FindRunningAnimationTransitions(
                    (byte*)handlerStart,
                    checked((int)(handlerEnd - handlerStart)));
                if (transitions.Count == 0)
                    continue;

                ushort? nativeRunningSpeedBonus = TryFindNativeRunningSpeedBonus(
                    (byte*)handlerStart,
                    checked((int)(handlerEnd - handlerStart)),
                    out ushort discoveredRunningSpeedBonus)
                    ? discoveredRunningSpeedBonus
                    : (ushort?)null;
                if (nativeRunningSpeedBonus.HasValue)
                    nativeRunningSpeedBonusCount++;

                eChimps unitType = (eChimps)unitTypeValue;
                animationTransitionsByType[unitType] =
                    new AnimationTransitions(transitions, nativeRunningSpeedBonus);
            }

            if (animationTransitionsByType.Count == 0)
                throw new InvalidOperationException("No native walking-to-running animation transitions were found.");

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Discovered native running-animation capabilities for {animationTransitionsByType.Count} unit types " +
                $"and statically resolved the native running cadence for {nativeRunningSpeedBonusCount} of them.");
        }

        private static Dictionary<uint, uint> FindRunningAnimationTransitions(byte* code, int length)
        {
            Dictionary<uint, uint> transitions = new Dictionary<uint, uint>();

            // Native form:
            // C7 /0 [manager + unitOffset + 0x660], immediateAnimationState
            //
            // 0x660 is GameUnit.N000000F4 relative to GameUnitManager. Running
            // movement states end in 0x81; clearing bit 0x80 yields the matching
            // walking state while preserving the animation family.
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

                if ((runningState & 0xFF) != 0x81 || runningState > 0x1000)
                    continue;

                uint walkingState = runningState & ~0x80u;
                transitions[walkingState] = runningState;
            }

            return transitions;
        }

        private static bool TryFindNativeRunningSpeedBonus(
            byte* code,
            int length,
            out ushort runningSpeedBonus)
        {
            runningSpeedBonus = 0;
            byte[] codeBytes = new ReadOnlySpan<byte>(code, length).ToArray();
            Iced.Intel.Decoder decoder =
                Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(codeBytes));
            List<Instruction> instructions = new List<Instruction>(2048);

            while (decoder.IP < unchecked((ulong)length) && instructions.Count < 10000)
            {
                Instruction instruction = decoder.Decode();
                instructions.Add(instruction);
                if (instruction.Mnemonic == Mnemonic.Ret)
                    break;
            }

            bool found = false;
            for (int runningIndex = 0; runningIndex < instructions.Count; runningIndex++)
            {
                Instruction runningInstruction = instructions[runningIndex];
                if (runningInstruction.Mnemonic != Mnemonic.Mov ||
                    runningInstruction.Op0Kind != OpKind.Memory ||
                    runningInstruction.MemoryDisplacement64 != 0x660 ||
                    runningInstruction.Op1Kind != OpKind.Immediate32)
                {
                    continue;
                }

                uint animationState = unchecked((uint)runningInstruction.GetImmediate(1));
                if ((animationState & 0xFF) != 0x81 || animationState > 0x1000)
                    continue;

                bool cadenceResolvedForState = false;
                for (int distance = 1; distance <= 12 && !cadenceResolvedForState; distance++)
                {
                    int precedingIndex = runningIndex - distance;
                    int followingIndex = runningIndex + distance;

                    if (precedingIndex >= 0 &&
                        TryResolveNearbySpeedBonus(
                            instructions,
                            precedingIndex,
                            out ushort precedingSpeedBonus))
                    {
                        if (found && runningSpeedBonus != precedingSpeedBonus)
                            return false;

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
                        if (found && runningSpeedBonus != followingSpeedBonus)
                            return false;

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
                speedBonus = unchecked((ushort)storeInstruction.GetImmediate(1));
                return true;
            }

            if (storeInstruction.Op1Kind != OpKind.Register)
                return false;

            Register sourceRegister = NormalizeRegister(storeInstruction.Op1Register);
            for (int instructionIndex = storeIndex - 1; instructionIndex >= 0; instructionIndex--)
            {
                Instruction instruction = instructions[instructionIndex];
                if (instruction.Op0Kind != OpKind.Register ||
                    NormalizeRegister(instruction.Op0Register) != sourceRegister)
                {
                    continue;
                }

                if (instruction.Mnemonic == Mnemonic.Mov && IsImmediate(instruction.Op1Kind))
                {
                    speedBonus = unchecked((ushort)instruction.GetImmediate(1));
                    return true;
                }

                if ((instruction.Mnemonic == Mnemonic.Xor ||
                     instruction.Mnemonic == Mnemonic.Sub) &&
                    instruction.Op1Kind == OpKind.Register &&
                    NormalizeRegister(instruction.Op1Register) == sourceRegister)
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

        private void CalculateMovementSpeed(
            NativePointer<GameUnitManager> unitManager,
            int unitId,
            byte preserveCurrentHeight)
        {
            hook.Value.Hook.Trampoline(unitManager, unitId, preserveCurrentHeight);

            try
            {
                // This hook is reached for many unrelated units. The directive lookup
                // is the cheapest rejection path and avoids resolving a native unit
                // pointer unless TroopMovementFix is actually tracking the unit.
                if (!tryGetMovementDirective(unitId, out UnitMovementDirective movementDirective))
                    return;

                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    return;
                }

                if (movementDirective.MovementMode == MovementCadenceMode.UncappedRunning)
                {
                    // Fast does not undo a synchronized group's already calculated
                    // effective delay. Restore the unit type's own maximum-speed delay
                    // explicitly for uncapped movement. Speed levels are delays, so this
                    // changes a fast archer from the swordsman's 4 back to its own 1.
                    unit->r_CurrentSpeed2 = unit->r_CurrentSpeed;
                    return;
                }

                // Speed levels are delays: larger values are slower. Preserve terrain and
                // state penalties calculated by the game, but cap faster members at the
                // slowest member's normal maximum speed.
                ushort synchronizedSpeed = movementDirective.SynchronizedSpeed;
                if (unit->r_CurrentSpeed2 < synchronizedSpeed)
                    unit->r_CurrentSpeed2 = synchronizedSpeed;
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"The movement-speed synchronization callback failed; affected units keep vanilla speed behavior: {ex}");
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
                    walkingToRunning ?? throw new ArgumentNullException(nameof(walkingToRunning));
                NativeRunningSpeedBonus = nativeRunningSpeedBonus;
                runningToWalking = new Dictionary<uint, uint>(walkingToRunning.Count);

                foreach (KeyValuePair<uint, uint> transition in walkingToRunning)
                    runningToWalking[transition.Value] = transition.Key;
            }

            public ushort? NativeRunningSpeedBonus { get; }

            public bool TryGetRunningState(uint currentState, out uint runningState)
            {
                if (walkingToRunning.TryGetValue(currentState, out runningState))
                    return true;

                if (runningToWalking.ContainsKey(currentState))
                {
                    runningState = currentState;
                    return true;
                }

                runningState = currentState;
                return false;
            }

            public bool TryGetWalkingState(uint currentState, out uint walkingState)
            {
                if (runningToWalking.TryGetValue(currentState, out walkingState))
                    return true;

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
