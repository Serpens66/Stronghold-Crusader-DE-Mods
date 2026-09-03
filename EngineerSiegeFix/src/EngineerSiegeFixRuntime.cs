using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

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

        private const ushort EngineerType = (ushort)eChimps.CHIMP_TYPE_ENGINEER;
        private const ushort CatapultType = (ushort)eChimps.CHIMP_TYPE_CATAPULT;
        private const ushort TrebuchetType = (ushort)eChimps.CHIMP_TYPE_TREBUCHET;
        private const ushort MangonelType = (ushort)eChimps.CHIMP_TYPE_MANGONEL;
        private const ushort SiegeTentUnitType = (ushort)eChimps.CHIMP_SIEGE_TENT;
        private const ushort SiegeTowerType = (ushort)eChimps.CHIMP_TYPE_SIEGE_TOWER;
        private const ushort BatteringRamType = (ushort)eChimps.CHIMP_TYPE_BATTERING_RAM;
        private const ushort PortableShieldType = (ushort)eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
        private const ushort BallistaType = (ushort)eChimps.CHIMP_TYPE_BALLISTA;
        private const ushort ArabBallistaType = (ushort)eChimps.CHIMP_TYPE_ARAB_BALLISTA;
        private const int SlotTransitionLimit = 320;
        private const int EngineerTransitionLimit = 480;
        private const int HandoffVerdictLimit = 80;
        private const int TickHeartbeatLimit = 3;

        private readonly ManualLogSource log;
        private readonly Dictionary<int, UnitObservation> siegeSlotObservations =
            new Dictionary<int, UnitObservation>();
        private readonly Dictionary<int, UnitObservation> engineerObservations =
            new Dictionary<int, UnitObservation>();
        private readonly Dictionary<int, HandoffTracker> handoffTrackers =
            new Dictionary<int, HandoffTracker>();
        private IntPtr observedManager;
        private uint lastObservedTick = uint.MaxValue;
        private int tickCallbackCount;
        private int slotTransitionCount;
        private int engineerTransitionCount;
        private int handoffVerdictCount;
        private bool slotLimitReported;
        private bool engineerLimitReported;
        private bool tickSubscribed;
        private bool diagnosticsDisabled;
        private bool disposed;

        public EngineerSiegeFixRuntime(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (memory.IsEmpty || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            try
            {
                // Unlike the early BepInEx component, this Script Extender publisher
                // survives SHCDE's normal startup cleanup and roots the runtime.
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "SIEGE_ROUTE_DIAGNOSTIC_INSTALLED: diagnosticMode=true, correctionActive=false, " +
                    "lifecycleRoot=static-runtime-and-GameTimeManagerAPI.OnTick, " +
                    "observationMode=read-only-GameTimeManagerAPI.OnTick-snapshots, " +
                    "activeObservationHooks=0, unsafeNativeHookSetDisabled=true, " +
                    $"automaticHandoffVerificationTicks={EngineerHandoffDiagnosticPolicy.VerificationTimeoutTicks}, " +
                    "shadowIdleFaultInjection=true, gameStateFaultInjection=false.");
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
            if (tickSubscribed)
            {
                GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
                tickSubscribed = false;
            }
        }

        private void OnGameTick(int tick)
        {
            if (disposed || diagnosticsDisabled)
                return;

            uint currentTick = unchecked((uint)tick);
            lastObservedTick = currentTick;
            tickCallbackCount++;
            if (tickCallbackCount <= TickHeartbeatLimit)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"SIEGE_TICK_HEARTBEAT: callback={tickCallbackCount}, tick={currentTick}, " +
                    "source=GameTimeManagerAPI.OnTick.");
            }

            PollRuntimeDiagnostics(currentTick);
        }

        private void PollRuntimeDiagnostics(uint currentTick)
        {
            try
            {
                IntPtr manager = (IntPtr)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
                if (manager == IntPtr.Zero)
                    return;

                if (observedManager != manager)
                {
                    observedManager = manager;
                    siegeSlotObservations.Clear();
                    engineerObservations.Clear();
                    handoffTrackers.Clear();
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
                    // Command/target offsets are part of the hypothesis under test.
                    // Track every engineer so a wrong hypothesis cannot hide the handoff.
                    bool relevant = observation.Type == EngineerType || wasTracked;
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

                DiscoverAndEvaluateHandoffs(manager, nextUnitId, currentTick);
            }
            catch (Exception exception)
            {
                diagnosticsDisabled = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SIEGE_ROUTE_DIAGNOSTIC_DISABLED: no game state was changed; error={exception}");
            }
        }

        private void DiscoverAndEvaluateHandoffs(IntPtr manager, int nextUnitId, uint currentTick)
        {
            foreach (UnitObservation device in siegeSlotObservations.Values)
            {
                int requiredCrew = RequiredVerifiedCrew(device.Type);
                if (requiredCrew == 0)
                    continue;

                if (!handoffTrackers.TryGetValue(device.UnitId, out HandoffTracker existing) ||
                    existing.DeviceGlobalId != device.GlobalId)
                {
                    StartHandoffTracker(device, requiredCrew, currentTick);
                }
            }

            foreach (HandoffTracker tracker in handoffTrackers.Values)
            {
                if (tracker.Finalized)
                    continue;

                bool deviceIdentityPresent = TryCaptureIdentity(
                    manager,
                    nextUnitId,
                    tracker.DeviceUnitId,
                    tracker.DeviceGlobalId,
                    currentTick,
                    out UnitObservation device);
                bool deviceReady = deviceIdentityPresent &&
                    device.AliveState == EngineerHandoffDiagnosticPolicy.LiveUnitState;
                bool crewMatches = deviceIdentityPresent && tracker.CrewMatches(device);
                string crewStatus = "not-evaluated";
                bool crewBound = crewMatches && AreAllReferencedEngineersBound(
                    manager,
                    nextUnitId,
                    tracker,
                    currentTick,
                    out crewStatus);
                if (!crewMatches)
                    crewStatus = "device-crew-identities-changed";

                uint elapsedTicks = currentTick - tracker.StartTick;
                HandoffDiagnosticOutcome outcome = EngineerHandoffDiagnosticPolicy.Evaluate(
                    sessionContinues: true,
                    deviceIdentityPresent,
                    deviceReady,
                    crewMatches,
                    crewBound,
                    elapsedTicks);
                if (outcome == HandoffDiagnosticOutcome.Pending)
                    continue;

                tracker.Finalized = true;
                string marker = outcome == HandoffDiagnosticOutcome.Passed
                    ? "SIEGE_HANDOFF_PASSED"
                    : outcome == HandoffDiagnosticOutcome.Failed
                        ? "SIEGE_HANDOFF_FAILED"
                        : "SIEGE_HANDOFF_INCONCLUSIVE";
                LogHandoffVerdict(
                    marker,
                    tracker,
                    currentTick,
                    elapsedTicks,
                    deviceIdentityPresent,
                    deviceReady,
                    crewMatches,
                    crewBound,
                    crewStatus);
            }
        }

        private void StartHandoffTracker(UnitObservation device, int requiredCrew, uint currentTick)
        {
            var tracker = new HandoffTracker(device, requiredCrew, currentTick);
            handoffTrackers[device.UnitId] = tracker;

            HandoffDiagnosticOutcome normalOutcome = EngineerHandoffDiagnosticPolicy.Evaluate(
                true, true, true, true, true,
                EngineerHandoffDiagnosticPolicy.VerificationTimeoutTicks);
            HandoffDiagnosticOutcome injectedOutcome = EngineerHandoffDiagnosticPolicy.Evaluate(
                true, true, true, true, false,
                EngineerHandoffDiagnosticPolicy.VerificationTimeoutTicks);
            bool detectorWorks = normalOutcome == HandoffDiagnosticOutcome.Passed &&
                injectedOutcome == HandoffDiagnosticOutcome.Failed;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"SIEGE_HANDOFF_STARTED: tick={currentTick}, deviceId={tracker.DeviceUnitId}, " +
                $"deviceGlobal={tracker.DeviceGlobalId}, type=0x{tracker.DeviceType:X}, " +
                $"owner={tracker.Owner}, requiredCrew={requiredCrew}, crew={tracker.CrewSummary()}, " +
                $"initialAlive={device.AliveState}.");
            Shared.DebugLogHelper.LogInfo(
                log,
                $"SIEGE_HANDOFF_DETECTOR_SELF_TEST: deviceId={tracker.DeviceUnitId}, " +
                $"deviceGlobal={tracker.DeviceGlobalId}, normalShadow={normalOutcome}, " +
                $"simulatedIdleCrewShadow={injectedOutcome}, detectorWorks={detectorWorks}, " +
                "faultInjection=shadow-only,no-game-state-write.");

            if (!detectorWorks)
            {
                diagnosticsDisabled = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "SIEGE_ROUTE_DIAGNOSTIC_DISABLED: the handoff detector failed its shadow fault test; " +
                    "no game state was changed.");
            }
        }

        private bool AreAllReferencedEngineersBound(
            IntPtr manager,
            int nextUnitId,
            HandoffTracker tracker,
            uint currentTick,
            out string status)
        {
            var parts = new List<string>(tracker.RequiredCrew);
            bool allBound = true;
            for (int index = 0; index < tracker.RequiredCrew; index++)
            {
                int unitId = tracker.CrewIds[index];
                uint globalId = tracker.CrewGlobals[index];
                bool present = TryCaptureIdentity(
                    manager,
                    nextUnitId,
                    unitId,
                    globalId,
                    currentTick,
                    out UnitObservation engineer);
                bool bound = present && EngineerHandoffDiagnosticPolicy.IsReferencedEngineerBound(
                    unitId,
                    globalId,
                    tracker.Owner,
                    engineer.UnitId,
                    engineer.GlobalId,
                    engineer.AliveState,
                    engineer.Type,
                    engineer.Owner,
                    engineer.State);
                allBound &= bound;
                parts.Add(present
                    ? $"{unitId}/{globalId}:alive={engineer.AliveState},type=0x{engineer.Type:X}," +
                      $"owner={engineer.Owner},mainState=0x{engineer.MainState:X},bound={bound}"
                    : $"{unitId}/{globalId}:identity-missing-or-reused,bound=false");
            }
            status = "[" + string.Join(";", parts) + "]";
            return allBound;
        }

        private static bool TryCaptureIdentity(
            IntPtr manager,
            int nextUnitId,
            int unitId,
            uint globalId,
            uint currentTick,
            out UnitObservation observation)
        {
            observation = null;
            if (manager == IntPtr.Zero || unitId <= 0 || unitId >= nextUnitId || globalId == 0)
                return false;

            observation = Capture(Unit(manager, unitId), unitId, currentTick);
            return observation.GlobalId == globalId;
        }

        private void LogHandoffVerdict(
            string marker,
            HandoffTracker tracker,
            uint currentTick,
            uint elapsedTicks,
            bool deviceIdentityPresent,
            bool deviceReady,
            bool crewMatches,
            bool crewBound,
            string crewStatus)
        {
            if (handoffVerdictCount >= HandoffVerdictLimit)
                return;

            handoffVerdictCount++;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"{marker}: tick={currentTick}, elapsedTicks={elapsedTicks}, " +
                $"deviceId={tracker.DeviceUnitId}, deviceGlobal={tracker.DeviceGlobalId}, " +
                $"type=0x{tracker.DeviceType:X}, owner={tracker.Owner}, " +
                $"deviceIdentityPresent={deviceIdentityPresent}, deviceReady={deviceReady}, " +
                $"crewMatches={crewMatches}, allReferencedEngineersBound={crewBound}, " +
                $"crew={tracker.CrewSummary()}, crewStatus={crewStatus}.");
        }

        private static int RequiredVerifiedCrew(ushort type)
        {
            if (type == CatapultType)
                return 2;
            if (type == TrebuchetType)
                return 3;
            return 0;
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
            type == CatapultType || type == TrebuchetType || type == MangonelType ||
            type == SiegeTentUnitType || type == SiegeTowerType || type == BatteringRamType ||
            type == PortableShieldType || type == BallistaType || type == ArabBallistaType;

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
            public ushort MainState => unchecked((ushort)State);
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

        private sealed class HandoffTracker
        {
            public HandoffTracker(UnitObservation device, int requiredCrew, uint startTick)
            {
                DeviceUnitId = device.UnitId;
                DeviceGlobalId = device.GlobalId;
                DeviceType = device.Type;
                Owner = device.Owner;
                RequiredCrew = requiredCrew;
                StartTick = startTick;
                CrewIds = new[] { device.CrewId0, device.CrewId1, device.CrewId2 };
                CrewGlobals = new[] { device.CrewGlobal0, device.CrewGlobal1, device.CrewGlobal2 };
            }

            public int DeviceUnitId { get; }
            public uint DeviceGlobalId { get; }
            public ushort DeviceType { get; }
            public byte Owner { get; }
            public int RequiredCrew { get; }
            public uint StartTick { get; }
            public ushort[] CrewIds { get; }
            public uint[] CrewGlobals { get; }
            public bool Finalized { get; set; }

            public bool CrewMatches(UnitObservation device)
            {
                ushort[] ids = { device.CrewId0, device.CrewId1, device.CrewId2 };
                uint[] globals = { device.CrewGlobal0, device.CrewGlobal1, device.CrewGlobal2 };
                return EngineerHandoffDiagnosticPolicy.AreCrewIdentitiesValidAndStable(
                    RequiredCrew,
                    device.CrewCount,
                    CrewIds,
                    CrewGlobals,
                    ids,
                    globals);
            }

            public string CrewSummary()
            {
                var parts = new string[RequiredCrew];
                for (int index = 0; index < RequiredCrew; index++)
                    parts[index] = $"{CrewIds[index]}/{CrewGlobals[index]}";
                return "[" + string.Join(",", parts) + "]";
            }
        }
    }
}
