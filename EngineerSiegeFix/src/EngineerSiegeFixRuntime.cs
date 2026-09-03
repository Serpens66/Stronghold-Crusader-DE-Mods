using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

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
        private const int HeightOffset = 0xB6;
        private const int HeightFineOffset = 0xB8;
        private const int InternalAssignmentOffset = 0x29C;
        private const int AnimationTimerOffset = 0x2AC;
        private const int AiStateOffset = 0x2BC;
        private const int EngineerTransitionTimerOffset = 0x2C4;
        private const int PendingUnitTypeOffset = 0x2C6;
        private const int PendingAiStateOffset = 0x2C8;
        private const int AssignedEngineerIdsOffset = 0x310;
        private const int AssignedEngineerGlobalsOffset = 0x318;
        private const int EngineerFadeOffset = 0x32A;
        private const int CommandOffset = 0x398;
        private const int TargetUnitIdOffset = 0x39A;
        private const int CrewCountOffset = 0x3B0;
        private const int AiRoleOffset = 0x426;
        private const int TribeLeaderUnitIdOffset = 0x2D2;
        private const int Alive = 2;
        private const int PendingConversion = 4;
        private const ushort SiegeTentType = 0x32;
        private const uint EngineerConsumeState = 0x0005006D;
        private const ushort EngineerFadeValue = 0x0200;
        private const ushort ConsumedEngineerCommand = 3;
        private const int AiControllerArrayRva = 0x8574BCC;
        private const int BuildingManagerRva = 0x7CC6720;
        private const int GameTickRva = 0x37ED4D0;

        private readonly ManualLogSource log;
        private readonly ulong libraryBase;
        private readonly HookTransaction transaction;
        private readonly ClearSelectedUnitDelegate clearSelectedUnit;
        private readonly RemoveUnitFromGroupsDelegate removeUnitFromGroups;
        private readonly AiCrewBookkeepingDelegate aiCrewBookkeeping;
        private readonly List<PendingHandoffDiagnostic> pendingDiagnostics =
            new List<PendingHandoffDiagnostic>();
        private HookRef<X64ManagedFunctionDetourAOB<SiegeTentTickDelegate>> tentTickHook =
            new HookRef<X64ManagedFunctionDetourAOB<SiegeTentTickDelegate>>();
        private bool correctionDisabled;
        private bool diagnosticsDisabled;
        private bool disposed;
        private long completedHandoffs;
        private long passedDiagnostics;
        private long failedDiagnostics;
        private long inconclusiveDiagnostics;
        private uint lastDiagnosticTick = uint.MaxValue;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SiegeTentTickDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClearSelectedUnitDelegate(IntPtr unitManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RemoveUnitFromGroupsDelegate(
            IntPtr unitManager,
            int playerId,
            int unitId,
            int value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AiCrewBookkeepingDelegate(
            IntPtr buildingManager,
            int engineerUnitId,
            int tribeLeaderUnitId);

        public EngineerSiegeFixRuntime(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.libraryBase = libraryBase;
            if (memory.IsEmpty || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            Shared.NativeResolution tent = Resolve(
                memory,
                EngineerSiegeFixNativeDefinition.SiegeTentTickPattern,
                EngineerSiegeFixNativeDefinition.SiegeTentTickRva,
                "siege tent completion");
            Shared.NativeResolution aiBookkeeping = Resolve(
                memory,
                EngineerSiegeFixNativeDefinition.AiCrewBookkeepingPattern,
                EngineerSiegeFixNativeDefinition.AiCrewBookkeepingRva,
                "AI siege-engine crew bookkeeping");
            Shared.NativeResolution clearSelection = Resolve(
                memory,
                EngineerSiegeFixNativeDefinition.ClearSelectedUnitPattern,
                EngineerSiegeFixNativeDefinition.ClearSelectedUnitRva,
                "selected-unit cleanup");
            Shared.NativeResolution removeGroups = Resolve(
                memory,
                EngineerSiegeFixNativeDefinition.RemoveUnitFromGroupsPattern,
                EngineerSiegeFixNativeDefinition.RemoveUnitFromGroupsRva,
                "unit-group cleanup");

            clearSelectedUnit = Marshal.GetDelegateForFunctionPointer<ClearSelectedUnitDelegate>(
                Address(clearSelection.Rva));
            removeUnitFromGroups = Marshal.GetDelegateForFunctionPointer<RemoveUnitFromGroupsDelegate>(
                Address(removeGroups.Rva));
            aiCrewBookkeeping = Marshal.GetDelegateForFunctionPointer<AiCrewBookkeepingDelegate>(
                Address(aiBookkeeping.Rva));

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(ref tentTickHook, libraryBase + unchecked((ulong)tent.Rva), TickSiegeTent);
                transaction.Commit();
                if (!tentTickHook.Success)
                    throw new InvalidOperationException("The siege-tent completion detour was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Engineer siege handoff fix installed: " +
                    $"tentRva=0x{tent.Rva:X}, aiBookkeepingRva=0x{aiBookkeeping.Rva:X}, " +
                    $"clearSelectionRva=0x{clearSelection.Rva:X}, removeGroupsRva=0x{removeGroups.Rva:X}.");
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
            if (diagnosticsDisabled || pendingDiagnostics.Count == 0 || disposed)
                return;

            try
            {
                uint currentTick = *(uint*)(libraryBase + GameTickRva);
                if (currentTick == lastDiagnosticTick)
                    return;
                lastDiagnosticTick = currentTick;

                IntPtr manager = (IntPtr)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
                int nextUnitId = manager == IntPtr.Zero ? 0 : *(int*)manager.ToPointer();
                for (int index = pendingDiagnostics.Count - 1; index >= 0; index--)
                {
                    PendingHandoffDiagnostic diagnostic = pendingDiagnostics[index];
                    HandoffDiagnosticOutcome outcome = EvaluateDiagnostic(
                        diagnostic,
                        manager,
                        nextUnitId,
                        currentTick,
                        out string details);
                    if (outcome == HandoffDiagnosticOutcome.Pending)
                        continue;

                    pendingDiagnostics.RemoveAt(index);
                    LogDiagnosticOutcome(outcome, diagnostic, currentTick, details);
                }
            }
            catch (Exception exception)
            {
                diagnosticsDisabled = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"RUNTIME_VALIDATION_DISABLED: pending={pendingDiagnostics.Count}, error={exception}");
            }
        }

        private void TickSiegeTent()
        {
            TentIdentity before = default;
            bool captured = false;
            try
            {
                captured = TryCaptureTentBeforeTick(out before);
            }
            catch (Exception exception)
            {
                DisableCorrection("pre-tick validation failed", exception);
            }

            tentTickHook.Value.Hook.Trampoline();

            if (!captured || correctionDisabled)
                return;

            try
            {
                TryCommitCompletedHandoff(before);
            }
            catch (Exception exception)
            {
                DisableCorrection("post-tick handoff failed", exception);
            }
        }

        private bool TryCaptureTentBeforeTick(out TentIdentity identity)
        {
            identity = default;
            int unitId = GameUnitManagerAPI.Instance.GetCurrentContextUnitId();
            IntPtr manager = (IntPtr)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
            int nextUnitId = manager == IntPtr.Zero ? 0 : *(int*)manager.ToPointer();
            if (unitId <= 0 || unitId >= nextUnitId)
                return false;

            byte* unit = Unit(manager, unitId);
            if (ReadInt16(unit, AliveStateOffset) != Alive ||
                ReadUInt16(unit, UnitTypeOffset) != SiegeTentType)
            {
                return false;
            }

            uint globalId = ReadUInt32(unit, GlobalIdOffset);
            if (globalId == 0)
                return false;

            identity = new TentIdentity(unitId, globalId, manager);
            return true;
        }

        private void TryCommitCompletedHandoff(TentIdentity before)
        {
            IntPtr currentManager = (IntPtr)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
            if (currentManager == IntPtr.Zero || currentManager != before.Manager)
                return;

            int nextUnitId = *(int*)currentManager.ToPointer();
            if (before.UnitId <= 0 || before.UnitId >= nextUnitId)
                return;

            byte* device = Unit(currentManager, before.UnitId);
            ushort targetType = ReadUInt16(device, PendingUnitTypeOffset);
            if (ReadInt16(device, AliveStateOffset) != PendingConversion ||
                ReadUInt16(device, UnitTypeOffset) != SiegeTentType ||
                ReadUInt32(device, GlobalIdOffset) != before.GlobalId ||
                ReadUInt16(device, PendingAiStateOffset) != 6 ||
                EngineerCrewHandoffPolicy.RequiredCrew(targetType) == 0)
            {
                return;
            }

            byte ownerId = *(device + OwnerIdOffset);
            bool aiControlled = ownerId <= 8 &&
                *(int*)(libraryBase + AiControllerArrayRva + unchecked((uint)ownerId * sizeof(int))) == -1;
            var deviceSnapshot = new DeviceSnapshot(
                before.UnitId,
                before.GlobalId,
                ownerId,
                targetType,
                aiControlled,
                ReadUInt16(device, WorldXOffset),
                ReadUInt16(device, WorldYOffset),
                ReadUInt16(device, HeightOffset),
                ReadInt16(device, HeightFineOffset));

            var units = new List<EngineerSnapshot>();
            for (int unitId = 1; unitId < nextUnitId; unitId++)
                units.Add(CaptureUnit(currentManager, unitId));

            if (!EngineerCrewHandoffPolicy.TrySelect(deviceSnapshot, units, out EngineerSnapshot[] crew))
                return;

            // Every identity and field needed below was captured before the first write.
            // Conversion happens after the unit loop and preserves these crew slots.
            for (int index = 0; index < crew.Length; index++)
            {
                WriteUInt16(device, AssignedEngineerIdsOffset + index * sizeof(ushort), (ushort)crew[index].UnitId);
                WriteUInt32(device, AssignedEngineerGlobalsOffset + index * sizeof(uint), crew[index].GlobalId);
            }
            WriteUInt16(device, CrewCountOffset, (ushort)crew.Length);
            WriteUInt16(device, PendingAiStateOffset, 0);

            for (int index = 0; index < crew.Length; index++)
            {
                byte* engineer = Unit(currentManager, crew[index].UnitId);
                WriteUInt32(engineer, AnimationTimerOffset, 0);
                WriteUInt32(engineer, AiStateOffset, EngineerConsumeState);
                WriteUInt16(engineer, EngineerFadeOffset, EngineerFadeValue);
                WriteUInt16(engineer, EngineerTransitionTimerOffset, 0);
                WriteUInt16(engineer, CommandOffset, ConsumedEngineerCommand);
            }

            for (int index = 0; index < crew.Length; index++)
            {
                EngineerSnapshot engineer = crew[index];
                if (aiControlled && engineer.AiRole == EngineerCrewHandoffPolicy.AiSiegeEngineerRole)
                {
                    aiCrewBookkeeping(
                        new IntPtr(unchecked((long)(libraryBase + BuildingManagerRva))),
                        engineer.UnitId,
                        engineer.TribeLeaderUnitId);
                }
                clearSelectedUnit(currentManager, engineer.UnitId);
                removeUnitFromGroups(currentManager, engineer.OwnerId, engineer.UnitId, 0);
            }

            completedHandoffs++;
            uint commitTick = *(uint*)(libraryBase + GameTickRva);
            bool immediateValid = ValidateImmediateCommit(device, crew, currentManager, out string immediateDetails);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"ENGINEER_HANDOFF_COMMITTED: count={completedHandoffs}, deviceId={before.UnitId}, " +
                $"deviceGlobal={before.GlobalId}, targetType=0x{targetType:X}, owner={ownerId}, " +
                $"ai={aiControlled}, crew={string.Join(",", Array.ConvertAll(crew, item => item.UnitId.ToString()))}.");
            if (immediateValid)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"RUNTIME_VALIDATION_IMMEDIATE_PASS: deviceId={before.UnitId}, " +
                    $"deviceGlobal={before.GlobalId}, tick={commitTick}, crewCount={crew.Length}.");
            }
            else
            {
                failedDiagnostics++;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"RUNTIME_VALIDATION_FAILED: stage=immediate, deviceId={before.UnitId}, " +
                    $"deviceGlobal={before.GlobalId}, tick={commitTick}, details={immediateDetails}, " +
                    $"failedTotal={failedDiagnostics}.");
            }

            pendingDiagnostics.Add(new PendingHandoffDiagnostic(
                before.Manager,
                before.UnitId,
                before.GlobalId,
                targetType,
                ownerId,
                commitTick,
                crew));
        }

        private static bool ValidateImmediateCommit(
            byte* device,
            EngineerSnapshot[] crew,
            IntPtr manager,
            out string details)
        {
            if (ReadInt16(device, AliveStateOffset) != PendingConversion ||
                ReadUInt16(device, UnitTypeOffset) != SiegeTentType ||
                ReadUInt16(device, PendingAiStateOffset) != 0 ||
                ReadUInt16(device, CrewCountOffset) != crew.Length ||
                !CrewSlotsMatch(device, crew))
            {
                details = DescribeDevice(device, crew);
                return false;
            }

            for (int index = 0; index < crew.Length; index++)
            {
                byte* engineer = Unit(manager, crew[index].UnitId);
                bool valid =
                    ReadUInt32(engineer, GlobalIdOffset) == crew[index].GlobalId &&
                    ReadInt16(engineer, AliveStateOffset) == Alive &&
                    ReadUInt16(engineer, UnitTypeOffset) == EngineerCrewHandoffPolicy.EngineerType &&
                    ReadUInt32(engineer, AiStateOffset) == EngineerConsumeState &&
                    ReadUInt16(engineer, EngineerFadeOffset) == EngineerFadeValue &&
                    ReadUInt16(engineer, EngineerTransitionTimerOffset) == 0 &&
                    ReadUInt16(engineer, CommandOffset) == ConsumedEngineerCommand;
                if (!valid)
                {
                    details = DescribeEngineer(engineer, crew[index]);
                    return false;
                }
            }

            details = "all immediate device and engineer fields match";
            return true;
        }

        private static HandoffDiagnosticOutcome EvaluateDiagnostic(
            PendingHandoffDiagnostic diagnostic,
            IntPtr manager,
            int nextUnitId,
            uint currentTick,
            out string details)
        {
            uint elapsedTicks = unchecked(currentTick - diagnostic.CommitTick);
            bool sessionContinues =
                manager != IntPtr.Zero &&
                manager == diagnostic.Manager &&
                elapsedTicks <= int.MaxValue;
            if (!sessionContinues)
            {
                details = "native session or tick sequence changed before verification";
                return HandoffDiagnosticOutcome.Inconclusive;
            }

            bool deviceIdInRange = diagnostic.DeviceUnitId > 0 && diagnostic.DeviceUnitId < nextUnitId;
            byte* device = deviceIdInRange ? Unit(manager, diagnostic.DeviceUnitId) : null;
            bool deviceIdentityPresent =
                device != null && ReadUInt32(device, GlobalIdOffset) == diagnostic.DeviceGlobalId;
            if (!deviceIdentityPresent)
            {
                details = "device identity disappeared before verification";
                return HandoffDiagnosticOutcome.Inconclusive;
            }

            bool deviceReady =
                ReadInt16(device, AliveStateOffset) == Alive &&
                ReadUInt16(device, UnitTypeOffset) == diagnostic.TargetType &&
                ReadUInt16(device, AiStateOffset) == 0;
            bool crewMatches =
                ReadUInt16(device, CrewCountOffset) == diagnostic.Crew.Length &&
                CrewSlotsMatch(device, diagnostic.Crew);
            bool allEngineersGone = AllOriginalEngineersGone(
                manager,
                nextUnitId,
                diagnostic.Crew,
                out string engineerDetails);

            HandoffDiagnosticOutcome outcome = EngineerHandoffDiagnosticPolicy.Evaluate(
                sessionContinues,
                deviceIdentityPresent,
                deviceReady,
                crewMatches,
                allEngineersGone,
                elapsedTicks);
            details =
                $"elapsedTicks={elapsedTicks}, deviceReady={deviceReady}, " +
                $"crewMatches={crewMatches}, allOriginalEngineersGone={allEngineersGone}, " +
                $"device={DescribeDevice(device, diagnostic.Crew)}, engineers={engineerDetails}";
            return outcome;
        }

        private static bool AllOriginalEngineersGone(
            IntPtr manager,
            int nextUnitId,
            EngineerSnapshot[] crew,
            out string details)
        {
            var states = new string[crew.Length];
            bool allGone = true;
            for (int index = 0; index < crew.Length; index++)
            {
                EngineerSnapshot expected = crew[index];
                if (expected.UnitId <= 0 || expected.UnitId >= nextUnitId)
                {
                    states[index] = $"{expected.UnitId}:out-of-range";
                    continue;
                }

                byte* unit = Unit(manager, expected.UnitId);
                uint globalId = ReadUInt32(unit, GlobalIdOffset);
                short aliveState = ReadInt16(unit, AliveStateOffset);
                bool originalStillAlive = globalId == expected.GlobalId && aliveState == Alive;
                if (originalStillAlive)
                    allGone = false;
                states[index] = originalStillAlive
                    ? DescribeEngineer(unit, expected)
                    : $"{expected.UnitId}/{expected.GlobalId}:gone(currentGlobal={globalId},alive={aliveState})";
            }

            details = string.Join(";", states);
            return allGone;
        }

        private static bool CrewSlotsMatch(byte* device, EngineerSnapshot[] crew)
        {
            for (int index = 0; index < crew.Length; index++)
            {
                if (ReadUInt16(device, AssignedEngineerIdsOffset + index * sizeof(ushort)) != crew[index].UnitId ||
                    ReadUInt32(device, AssignedEngineerGlobalsOffset + index * sizeof(uint)) != crew[index].GlobalId)
                {
                    return false;
                }
            }
            return true;
        }

        private static string DescribeDevice(byte* device, EngineerSnapshot[] expectedCrew)
        {
            var slots = new string[expectedCrew.Length];
            for (int index = 0; index < expectedCrew.Length; index++)
            {
                slots[index] =
                    $"{ReadUInt16(device, AssignedEngineerIdsOffset + index * sizeof(ushort))}/" +
                    $"{ReadUInt32(device, AssignedEngineerGlobalsOffset + index * sizeof(uint))}";
            }
            return
                $"global={ReadUInt32(device, GlobalIdOffset)},alive={ReadInt16(device, AliveStateOffset)}," +
                $"type=0x{ReadUInt16(device, UnitTypeOffset):X},state={ReadUInt16(device, AiStateOffset)}," +
                $"pendingType=0x{ReadUInt16(device, PendingUnitTypeOffset):X}," +
                $"pendingState={ReadUInt16(device, PendingAiStateOffset)}," +
                $"crewCount={ReadUInt16(device, CrewCountOffset)},slots=[{string.Join(",", slots)}]";
        }

        private static string DescribeEngineer(byte* engineer, EngineerSnapshot expected) =>
            $"{expected.UnitId}/{expected.GlobalId}:(currentGlobal={ReadUInt32(engineer, GlobalIdOffset)}," +
            $"alive={ReadInt16(engineer, AliveStateOffset)},type=0x{ReadUInt16(engineer, UnitTypeOffset):X}," +
            $"state={ReadUInt16(engineer, AiStateOffset)},next={ReadUInt16(engineer, AiStateOffset + 2)}," +
            $"command={ReadUInt16(engineer, CommandOffset)})";

        private void LogDiagnosticOutcome(
            HandoffDiagnosticOutcome outcome,
            PendingHandoffDiagnostic diagnostic,
            uint currentTick,
            string details)
        {
            string identity =
                $"deviceId={diagnostic.DeviceUnitId}, deviceGlobal={diagnostic.DeviceGlobalId}, " +
                $"targetType=0x{diagnostic.TargetType:X}, owner={diagnostic.OwnerId}, " +
                $"commitTick={diagnostic.CommitTick}, observedTick={currentTick}";
            if (outcome == HandoffDiagnosticOutcome.Passed)
            {
                passedDiagnostics++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"RUNTIME_VALIDATION_PASS: {identity}, details={details}, " +
                    $"passedTotal={passedDiagnostics}, failedTotal={failedDiagnostics}, " +
                    $"inconclusiveTotal={inconclusiveDiagnostics}.");
                return;
            }

            if (outcome == HandoffDiagnosticOutcome.Inconclusive)
            {
                inconclusiveDiagnostics++;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"RUNTIME_VALIDATION_INCONCLUSIVE: {identity}, details={details}, " +
                    $"passedTotal={passedDiagnostics}, failedTotal={failedDiagnostics}, " +
                    $"inconclusiveTotal={inconclusiveDiagnostics}.");
                return;
            }

            failedDiagnostics++;
            Shared.DebugLogHelper.LogError(
                log,
                $"RUNTIME_VALIDATION_FAILED: stage=eventual, {identity}, details={details}, " +
                $"passedTotal={passedDiagnostics}, failedTotal={failedDiagnostics}, " +
                $"inconclusiveTotal={inconclusiveDiagnostics}.");
        }

        private static EngineerSnapshot CaptureUnit(IntPtr manager, int unitId)
        {
            byte* unit = Unit(manager, unitId);
            return new EngineerSnapshot(
                unitId,
                ReadUInt32(unit, GlobalIdOffset),
                *(unit + OwnerIdOffset),
                ReadUInt16(unit, UnitTypeOffset),
                ReadInt16(unit, AliveStateOffset) == Alive,
                ReadInt32(unit, InternalAssignmentOffset),
                ReadUInt16(unit, CommandOffset),
                ReadUInt16(unit, TargetUnitIdOffset),
                ReadUInt16(unit, AiRoleOffset),
                ReadUInt16(unit, TribeLeaderUnitIdOffset),
                ReadUInt16(unit, WorldXOffset),
                ReadUInt16(unit, WorldYOffset),
                ReadUInt16(unit, HeightOffset),
                ReadInt16(unit, HeightFineOffset));
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

        private IntPtr Address(int rva) =>
            new IntPtr(unchecked((long)(libraryBase + unchecked((ulong)rva))));

        private void DisableCorrection(string reason, Exception exception)
        {
            if (correctionDisabled)
                return;
            correctionDisabled = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Engineer siege handoff correction disabled for this process: reason={reason}, error={exception}");
        }

        private static byte* Unit(IntPtr manager, int unitId) =>
            (byte*)manager.ToPointer() + UnitArrayBias + unitId * UnitSlotSize;

        private static short ReadInt16(byte* pointer, int offset) => *(short*)(pointer + offset);
        private static ushort ReadUInt16(byte* pointer, int offset) => *(ushort*)(pointer + offset);
        private static int ReadInt32(byte* pointer, int offset) => *(int*)(pointer + offset);
        private static uint ReadUInt32(byte* pointer, int offset) => *(uint*)(pointer + offset);
        private static void WriteUInt16(byte* pointer, int offset, ushort value) => *(ushort*)(pointer + offset) = value;
        private static void WriteUInt32(byte* pointer, int offset, uint value) => *(uint*)(pointer + offset) = value;

        private readonly struct TentIdentity
        {
            public TentIdentity(int unitId, uint globalId, IntPtr manager)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Manager = manager;
            }

            public int UnitId { get; }
            public uint GlobalId { get; }
            public IntPtr Manager { get; }
        }

        private sealed class PendingHandoffDiagnostic
        {
            public PendingHandoffDiagnostic(
                IntPtr manager,
                int deviceUnitId,
                uint deviceGlobalId,
                ushort targetType,
                byte ownerId,
                uint commitTick,
                EngineerSnapshot[] crew)
            {
                Manager = manager;
                DeviceUnitId = deviceUnitId;
                DeviceGlobalId = deviceGlobalId;
                TargetType = targetType;
                OwnerId = ownerId;
                CommitTick = commitTick;
                Crew = (EngineerSnapshot[])crew.Clone();
            }

            public IntPtr Manager { get; }
            public int DeviceUnitId { get; }
            public uint DeviceGlobalId { get; }
            public ushort TargetType { get; }
            public byte OwnerId { get; }
            public uint CommitTick { get; }
            public EngineerSnapshot[] Crew { get; }
        }
    }
}
