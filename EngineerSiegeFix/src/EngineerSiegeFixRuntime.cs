using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace EngineerSiegeFix
{
    internal sealed unsafe class EngineerSiegeFixRuntime : IDisposable
    {
        private const int UnitSlotSize = 0x490;
        private const int UnitArrayBias = 0x65C;
        private const int AliveStateOffset = 0x88;
        private const int UnitTypeOffset = 0x8A;
        private const int OwnerIdOffset = 0x92;
        private const int GlobalIdOffset = 0x94;
        private const int WorldXOffset = 0xB2;
        private const int WorldYOffset = 0xB4;
        private const int AiStateOffset = 0x2BC;
        private const int InternalAssignmentOffset = 0x29C;
        private const int PendingUnitTypeOffset = 0x2C6;
        private const int PendingAiStateOffset = 0x2C8;
        private const int AssignedEngineerIdsOffset = 0x310;
        private const int AssignedEngineerGlobalsOffset = 0x318;
        private const int CommandOffset = 0x398;
        private const int TargetUnitIdOffset = 0x39A;
        private const int CrewCountOffset = 0x3B0;
        private const int GameTickRva = 0x37ED4D0;

        private const ushort EngineerType = 0x1E;
        private const ushort CatapultType = 0x27;
        private const ushort TrebuchetType = 0x28;
        private const ushort SiegeTentUnitType = 0x32;
        private const ushort BuildSiegeEngineCommand = 0x10;
        private const int HandlerTransitionLimit = 160;
        private const int SlotTransitionLimit = 320;
        private const int EngineerTransitionLimit = 480;

        private readonly ManualLogSource log;
        private readonly ulong libraryBase;
        private readonly HookTransaction transaction;
        private readonly Dictionary<int, UnitObservation> handlerObservations =
            new Dictionary<int, UnitObservation>();
        private readonly Dictionary<int, UnitObservation> siegeSlotObservations =
            new Dictionary<int, UnitObservation>();
        private readonly Dictionary<int, UnitObservation> engineerObservations =
            new Dictionary<int, UnitObservation>();
        private HookRef<X64InlineHook> catapultHandlerHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> trebuchetHandlerHook = new HookRef<X64InlineHook>();
        private IntPtr observedManager;
        private uint lastPollTick = uint.MaxValue;
        private int handlerTransitionCount;
        private int slotTransitionCount;
        private int engineerTransitionCount;
        private bool handlerLimitReported;
        private bool slotLimitReported;
        private bool engineerLimitReported;
        private bool diagnosticsDisabled;
        private bool disposed;

        public EngineerSiegeFixRuntime(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.libraryBase = libraryBase;
            if (memory.IsEmpty || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            Shared.NativeResolution catapultHandler = Resolve(
                memory,
                EngineerSiegeFixNativeDefinition.CatapultHandlerPattern,
                EngineerSiegeFixNativeDefinition.CatapultHandlerRva,
                "catapult unit handler entry");
            Shared.NativeResolution trebuchetHandler = Resolve(
                memory,
                EngineerSiegeFixNativeDefinition.TrebuchetHandlerPattern,
                EngineerSiegeFixNativeDefinition.TrebuchetHandlerRva,
                "trebuchet unit handler entry");

            // The baseline proves that the central dispatcher indexes this table by
            // unit type. Validate the relocated entries before installing hooks.
            ulong catapultTableTarget = ReadHandlerTableTarget(CatapultType);
            ulong trebuchetTableTarget = ReadHandlerTableTarget(TrebuchetType);
            ValidateHandlerTableTarget(CatapultType, catapultTableTarget, catapultHandler.Rva);
            ValidateHandlerTableTarget(TrebuchetType, trebuchetTableTarget, trebuchetHandler.Rva);

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref catapultHandlerHook,
                    libraryBase + unchecked((ulong)catapultHandler.Rva),
                    ObserveCatapultHandler,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: 5,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.AddContextHook(
                    ref trebuchetHandlerHook,
                    libraryBase + unchecked((ulong)trebuchetHandler.Rva),
                    ObserveTrebuchetHandler,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: 5,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.Commit();
                if (!catapultHandlerHook.Success || !catapultHandlerHook.Value.IsActive ||
                    !trebuchetHandlerHook.Success || !trebuchetHandlerHook.Value.IsActive)
                {
                    throw new InvalidOperationException("The siege-engine handler hooks were not both activated.");
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "SIEGE_ROUTE_DIAGNOSTIC_INSTALLED: diagnosticMode=true, correctionActive=false, " +
                    $"dispatcherRva=0x{EngineerSiegeFixNativeDefinition.UnitDispatcherRva:X}, " +
                    $"dispatchTypeLoadRva=0x{EngineerSiegeFixNativeDefinition.UnitDispatchTypeLoadRva:X}, " +
                    $"dispatchCallRva=0x{EngineerSiegeFixNativeDefinition.UnitDispatchCallRva:X}, " +
                    $"handlerTableRva=0x{EngineerSiegeFixNativeDefinition.UnitHandlerTableRva:X}, " +
                    $"catapultHandlerRva=0x{catapultHandler.Rva:X}, " +
                    $"catapultTableTargetRva=0x{catapultTableTarget - libraryBase:X}, " +
                    $"trebuchetHandlerRva=0x{trebuchetHandler.Rva:X}, " +
                    $"trebuchetTableTargetRva=0x{trebuchetTableTarget - libraryBase:X}, hookLength=5.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction?.Unload();
            transaction?.Dispose();
        }

        public void PollRuntimeDiagnostics()
        {
            if (disposed || diagnosticsDisabled)
                return;

            try
            {
                uint currentTick = *(uint*)(libraryBase + GameTickRva);
                if (currentTick == lastPollTick)
                    return;
                lastPollTick = currentTick;

                IntPtr manager = (IntPtr)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
                if (manager == IntPtr.Zero)
                    return;

                if (observedManager != manager)
                {
                    observedManager = manager;
                    handlerObservations.Clear();
                    siegeSlotObservations.Clear();
                    engineerObservations.Clear();
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"SIEGE_DIAGNOSTIC_SESSION: tick={currentTick}, unitManager=0x{manager.ToInt64():X}.");
                }

                int nextUnitId = *(int*)manager.ToPointer();
                if (nextUnitId <= 1 || nextUnitId > 10001)
                    return;

                var currentSiegeIds = new HashSet<int>();
                for (int unitId = 1; unitId < nextUnitId; unitId++)
                {
                    byte* unit = Unit(manager, unitId);
                    if (!IsSiegeType(ReadUInt16(unit, UnitTypeOffset)))
                        continue;

                    UnitObservation observation = Capture(unit, unitId, currentTick);
                    currentSiegeIds.Add(unitId);
                    ObserveTransition(
                        siegeSlotObservations,
                        observation,
                        "SIEGE_SLOT_TRANSITION",
                        ref slotTransitionCount,
                        SlotTransitionLimit,
                        ref slotLimitReported);
                }
                RemoveExitedSlots(
                    siegeSlotObservations,
                    currentSiegeIds,
                    manager,
                    nextUnitId,
                    currentTick,
                    "SIEGE_SLOT_LEFT_SCOPE",
                    ref slotTransitionCount,
                    SlotTransitionLimit,
                    ref slotLimitReported);

                var currentEngineerIds = new HashSet<int>();
                for (int unitId = 1; unitId < nextUnitId; unitId++)
                {
                    bool wasTracked = engineerObservations.ContainsKey(unitId);
                    byte* unit = Unit(manager, unitId);
                    ushort type = ReadUInt16(unit, UnitTypeOffset);
                    if (type != EngineerType && !wasTracked)
                        continue;

                    UnitObservation observation = Capture(unit, unitId, currentTick);
                    bool relevant = observation.Type == EngineerType &&
                        (observation.Command == BuildSiegeEngineCommand ||
                         currentSiegeIds.Contains(observation.TargetUnitId) ||
                         wasTracked);
                    if (!relevant)
                        continue;

                    currentEngineerIds.Add(unitId);
                    ObserveTransition(
                        engineerObservations,
                        observation,
                        "SIEGE_ENGINEER_TRANSITION",
                        ref engineerTransitionCount,
                        EngineerTransitionLimit,
                        ref engineerLimitReported);
                }
                RemoveExitedSlots(
                    engineerObservations,
                    currentEngineerIds,
                    manager,
                    nextUnitId,
                    currentTick,
                    "SIEGE_ENGINEER_LEFT_SCOPE",
                    ref engineerTransitionCount,
                    EngineerTransitionLimit,
                    ref engineerLimitReported);
            }
            catch (Exception exception)
            {
                diagnosticsDisabled = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SIEGE_ROUTE_DIAGNOSTIC_DISABLED: no game state was changed; error={exception}");
            }
        }

        private void ObserveCatapultHandler(NativePointer<X64SmartCPUContext> context) =>
            ObserveHandlerEntry(CatapultType, "catapult", EngineerSiegeFixNativeDefinition.CatapultHandlerRva);

        private void ObserveTrebuchetHandler(NativePointer<X64SmartCPUContext> context) =>
            ObserveHandlerEntry(TrebuchetType, "trebuchet", EngineerSiegeFixNativeDefinition.TrebuchetHandlerRva);

        private void ObserveHandlerEntry(ushort expectedType, string deviceName, int handlerRva)
        {
            if (disposed || diagnosticsDisabled)
                return;

            try
            {
                IntPtr manager = (IntPtr)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
                int unitId = GameUnitManagerAPI.Instance.GetCurrentContextUnitId();
                int nextUnitId = manager == IntPtr.Zero ? 0 : *(int*)manager.ToPointer();
                if (unitId <= 0 || unitId >= nextUnitId)
                {
                    throw new InvalidOperationException(
                        $"Handler context unit ID is invalid: unitId={unitId}, nextUnitId={nextUnitId}.");
                }

                uint currentTick = *(uint*)(libraryBase + GameTickRva);
                UnitObservation observation = Capture(Unit(manager, unitId), unitId, currentTick);
                if (observation.Type != expectedType)
                {
                    throw new InvalidOperationException(
                        $"Handler/type mismatch: handlerRva=0x{handlerRva:X}, expectedType=0x{expectedType:X}, " +
                        $"unitId={unitId}, actualType=0x{observation.Type:X}.");
                }

                if (handlerObservations.TryGetValue(unitId, out UnitObservation previous) &&
                    previous.HasSameIdentityAndState(observation))
                {
                    return;
                }
                handlerObservations[unitId] = observation;

                if (handlerTransitionCount < HandlerTransitionLimit)
                {
                    handlerTransitionCount++;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"SIEGE_HANDLER_ENTRY: device={deviceName}, handlerRva=0x{handlerRva:X}, " +
                        $"dispatchTargetRva=0x{ReadHandlerTableTarget(expectedType) - libraryBase:X}, " +
                        $"observation={observation.Describe()}, transition={handlerTransitionCount}.");
                }
                else if (!handlerLimitReported)
                {
                    handlerLimitReported = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"SIEGE_HANDLER_LOG_LIMIT: limit={HandlerTransitionLimit}; diagnostics remain active.");
                }
            }
            catch (Exception exception)
            {
                diagnosticsDisabled = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SIEGE_ROUTE_DIAGNOSTIC_DISABLED: handlerRva=0x{handlerRva:X}, " +
                    $"no game state was changed; error={exception}");
            }
        }

        private void ObserveTransition(
            Dictionary<int, UnitObservation> observations,
            UnitObservation current,
            string marker,
            ref int count,
            int limit,
            ref bool limitReported)
        {
            if (observations.TryGetValue(current.UnitId, out UnitObservation previous) &&
                previous.HasSameIdentityAndState(current))
            {
                return;
            }

            observations[current.UnitId] = current;
            LogTransition(marker, previous, current, ref count, limit, ref limitReported);
        }

        private void RemoveExitedSlots(
            Dictionary<int, UnitObservation> observations,
            HashSet<int> currentIds,
            IntPtr manager,
            int nextUnitId,
            uint tick,
            string marker,
            ref int count,
            int limit,
            ref bool limitReported)
        {
            var removedIds = new List<int>();
            foreach (KeyValuePair<int, UnitObservation> item in observations)
            {
                if (currentIds.Contains(item.Key))
                    continue;

                UnitObservation current = item.Key > 0 && item.Key < nextUnitId
                    ? Capture(Unit(manager, item.Key), item.Key, tick)
                    : UnitObservation.OutOfRange(item.Key, tick);
                LogTransition(marker, item.Value, current, ref count, limit, ref limitReported);
                removedIds.Add(item.Key);
            }
            foreach (int unitId in removedIds)
                observations.Remove(unitId);
        }

        private void LogTransition(
            string marker,
            UnitObservation previous,
            UnitObservation current,
            ref int count,
            int limit,
            ref bool limitReported)
        {
            if (count < limit)
            {
                count++;
                string previousText = previous == null ? "none" : previous.Describe();
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"{marker}: previous={previousText}, current={current.Describe()}, transition={count}.");
            }
            else if (!limitReported)
            {
                limitReported = true;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"{marker}_LOG_LIMIT: limit={limit}; diagnostics remain active.");
            }
        }

        private static UnitObservation Capture(byte* unit, int unitId, uint tick)
        {
            return new UnitObservation(
                tick,
                unitId,
                ReadUInt32(unit, GlobalIdOffset),
                ReadInt16(unit, AliveStateOffset),
                ReadUInt16(unit, UnitTypeOffset),
                *(unit + OwnerIdOffset),
                ReadUInt32(unit, AiStateOffset),
                ReadInt32(unit, InternalAssignmentOffset),
                ReadUInt16(unit, CommandOffset),
                ReadUInt16(unit, TargetUnitIdOffset),
                ReadUInt16(unit, PendingUnitTypeOffset),
                ReadUInt16(unit, PendingAiStateOffset),
                ReadUInt16(unit, CrewCountOffset),
                ReadUInt16(unit, AssignedEngineerIdsOffset),
                ReadUInt16(unit, AssignedEngineerIdsOffset + 2),
                ReadUInt16(unit, AssignedEngineerIdsOffset + 4),
                ReadUInt32(unit, AssignedEngineerGlobalsOffset),
                ReadUInt32(unit, AssignedEngineerGlobalsOffset + 4),
                ReadUInt32(unit, AssignedEngineerGlobalsOffset + 8),
                ReadUInt16(unit, WorldXOffset),
                ReadUInt16(unit, WorldYOffset));
        }

        private static bool IsSiegeType(ushort type) =>
            type == CatapultType || type == TrebuchetType || type == SiegeTentUnitType;

        private ulong ReadHandlerTableTarget(ushort unitType) =>
            *(ulong*)(libraryBase + EngineerSiegeFixNativeDefinition.UnitHandlerTableRva +
                unchecked((ulong)unitType * sizeof(ulong)));

        private void ValidateHandlerTableTarget(ushort unitType, ulong actual, int expectedRva)
        {
            ulong expected = libraryBase + unchecked((ulong)expectedRva);
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    $"Unit handler table mismatch for type 0x{unitType:X}: " +
                    $"expected=0x{expected:X}, actual=0x{actual:X}.");
            }
        }

        private Shared.NativeResolution Resolve(
            ReadOnlySpan<byte> memory,
            string pattern,
            int rva,
            string name) =>
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                rva,
                referenceHashMatches: true,
                name,
                log);

        private static byte* Unit(IntPtr manager, int unitId) =>
            (byte*)manager.ToPointer() + UnitArrayBias + unitId * UnitSlotSize;

        private static short ReadInt16(byte* pointer, int offset) => *(short*)(pointer + offset);
        private static ushort ReadUInt16(byte* pointer, int offset) => *(ushort*)(pointer + offset);
        private static int ReadInt32(byte* pointer, int offset) => *(int*)(pointer + offset);
        private static uint ReadUInt32(byte* pointer, int offset) => *(uint*)(pointer + offset);

        private sealed class UnitObservation
        {
            public UnitObservation(
                uint tick, int unitId, uint globalId, short aliveState, ushort type, byte owner,
                uint state, int assignment, ushort command, ushort targetUnitId,
                ushort pendingType, ushort pendingState, ushort crewCount,
                ushort crewId0, ushort crewId1, ushort crewId2,
                uint crewGlobal0, uint crewGlobal1, uint crewGlobal2,
                ushort worldX, ushort worldY)
            {
                Tick = tick;
                UnitId = unitId;
                GlobalId = globalId;
                AliveState = aliveState;
                Type = type;
                Owner = owner;
                State = state;
                Assignment = assignment;
                Command = command;
                TargetUnitId = targetUnitId;
                PendingType = pendingType;
                PendingState = pendingState;
                CrewCount = crewCount;
                CrewId0 = crewId0;
                CrewId1 = crewId1;
                CrewId2 = crewId2;
                CrewGlobal0 = crewGlobal0;
                CrewGlobal1 = crewGlobal1;
                CrewGlobal2 = crewGlobal2;
                WorldX = worldX;
                WorldY = worldY;
            }

            public uint Tick { get; }
            public int UnitId { get; }
            public uint GlobalId { get; }
            public short AliveState { get; }
            public ushort Type { get; }
            public byte Owner { get; }
            public uint State { get; }
            public int Assignment { get; }
            public ushort Command { get; }
            public ushort TargetUnitId { get; }
            public ushort PendingType { get; }
            public ushort PendingState { get; }
            public ushort CrewCount { get; }
            public ushort CrewId0 { get; }
            public ushort CrewId1 { get; }
            public ushort CrewId2 { get; }
            public uint CrewGlobal0 { get; }
            public uint CrewGlobal1 { get; }
            public uint CrewGlobal2 { get; }
            public ushort WorldX { get; }
            public ushort WorldY { get; }

            public bool HasSameIdentityAndState(UnitObservation other) =>
                other != null &&
                GlobalId == other.GlobalId && AliveState == other.AliveState && Type == other.Type &&
                Owner == other.Owner && State == other.State && Assignment == other.Assignment &&
                Command == other.Command && TargetUnitId == other.TargetUnitId &&
                PendingType == other.PendingType && PendingState == other.PendingState &&
                CrewCount == other.CrewCount &&
                CrewId0 == other.CrewId0 && CrewId1 == other.CrewId1 && CrewId2 == other.CrewId2 &&
                CrewGlobal0 == other.CrewGlobal0 && CrewGlobal1 == other.CrewGlobal1 &&
                CrewGlobal2 == other.CrewGlobal2;

            public string Describe() =>
                $"tick={Tick},unitId={UnitId},global={GlobalId},alive={AliveState},type=0x{Type:X}," +
                $"owner={Owner},state=0x{State:X8},assignment={Assignment},command=0x{Command:X}," +
                $"target={TargetUnitId},pendingType=0x{PendingType:X},pendingState={PendingState}," +
                $"crewCount={CrewCount},crewIds=[{CrewId0},{CrewId1},{CrewId2}]," +
                $"crewGlobals=[{CrewGlobal0},{CrewGlobal1},{CrewGlobal2}],position={WorldX}/{WorldY}";

            public static UnitObservation OutOfRange(int unitId, uint tick) =>
                new UnitObservation(tick, unitId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0);
        }
    }
}
