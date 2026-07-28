using BepInEx.Logging;
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
            ushort synchronizedSpeed)
        {
            MovementMode = movementMode;
            SynchronizedSpeed = synchronizedSpeed;
        }

        public MovementCadenceMode MovementMode { get; }
        public ushort SynchronizedSpeed { get; }
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
                    // Synchronized running deliberately gives every compatible member
                    // the same running cadence. Uncapped Ctrl movement instead keeps
                    // the cadence just calculated by the unit's native type handler:
                    // units such as Assassins can have a different native run cadence
                    // even when their stored maximum-speed delay resembles an Archer's.
                    if (movementMode == MovementCadenceMode.SynchronizedRunning &&
                        animationTransitions != null)
                    {
                        unit->r_SpeedBonus = 1;
                    }

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

                eChimps unitType = (eChimps)unitTypeValue;
                animationTransitionsByType[unitType] = new AnimationTransitions(transitions);
            }

            if (animationTransitionsByType.Count == 0)
                throw new InvalidOperationException("No native walking-to-running animation transitions were found.");

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Discovered native running-animation capabilities for {animationTransitionsByType.Count} unit types.");
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

            public AnimationTransitions(Dictionary<uint, uint> walkingToRunning)
            {
                this.walkingToRunning =
                    walkingToRunning ?? throw new ArgumentNullException(nameof(walkingToRunning));
                runningToWalking = new Dictionary<uint, uint>(walkingToRunning.Count);

                foreach (KeyValuePair<uint, uint> transition in walkingToRunning)
                    runningToWalking[transition.Value] = transition.Key;
            }

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
