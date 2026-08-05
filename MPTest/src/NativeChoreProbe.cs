using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace MPTest
{
    internal sealed unsafe class NativeChoreProbe
    {
        private const byte ProbeOpcode = 111;
        private const byte SyncEventOpcode = 120;
        private const byte ResyncStartOpcode = 54;
        private const byte ResyncEndOpcode = 67;
        private const int ProbePayloadSize = 16;
        private const uint ProbeMagic = 0x31484353; // "SCH1" in little-endian byte order.
        private const ushort ProbeProtocolVersion = 1;
        private const uint ProbeSentinel = 0xC011AB1E;
        private const int MaximumDelayedChores = 128;
        private const int MaximumDelayMilliseconds = 2500;
        private const int MaximumPendingSlots = 500;
        private const int PendingSlotSize = 0x500;

        // These RVAs are validated below against CrusaderDE.dll SHA-256 1E6D4C2E...
        // before any pointer is called or modified.
        private const long QueueLocalChoreRva = 0x23990;
        private const long CopyChoreFieldRva = 0x1F5F0;
        private const long HandlerTableRva = 0x2C6A30;
        private const long OriginalProbeHandlerRva = 0xFC30;

        private const long CurrentSlotIndexOffset = 0x84CC8;
        private const long HandlerModeOffset = 0x84CCC;
        private const long HandlerPayloadSizeOffset = 0x84CD4;
        private const long PendingSlotsOffset = 0xB0BF8;

        private const long ExpectedCrusaderFileSize = 3450880;
        private const string ExpectedCrusaderSha256 =
            "1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B";

        private static readonly byte[] ExpectedOriginalProbeHandler =
        {
            0xC2, 0x00, 0x00, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC,
            0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC
        };

        private static readonly byte[] ExpectedQueueLocalChorePrologue =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C,
            0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x48
        };

        private static readonly byte[] ExpectedCopyChoreFieldPrologue =
        {
            0x45, 0x85, 0xC0, 0x0F, 0x8E, 0x93, 0x00, 0x00,
            0x00, 0x48, 0x89, 0x5C, 0x24, 0x08, 0x57, 0x48
        };

        private static readonly object StaticInstallLock = new object();
        private static NativeChoreProbe activeInstance;
        private static NativeChoreHandlerDelegate rootedNativeHandler;
        private static Hook rootedSendChoresHook;
        private static Hook rootedReceiveChoreHook;
        private static Hook rootedRunHook;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void NativeChoreHandlerDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void QueueLocalChoreDelegate(IntPtr choreManager, byte opcode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void CopyChoreFieldDelegate(
            IntPtr choreManager,
            IntPtr data,
            int size,
            int usePendingSlot,
            int deserialize);

        private delegate void SendChoresDelegate(Platform_Multiplayer self, byte[] choreBuffer);
        private delegate void ReceiveChoreDelegate(int playerId, byte[] data, int dataLength);
        private delegate int RunDelegate(bool multiplayerFrameSkip);

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        private struct ProbePayload
        {
            public byte SourcePlayerId;
            public byte Flags;
            public int RequestId;
        }

        private sealed class ChoreObservation
        {
            public byte SourcePlayerId;
            public int RequestId;
            public int CommandId;
            public int ScheduledTick;
        }

        private sealed class DelayedChore
        {
            public int PlayerId;
            public byte[] Data;
            public int DataLength;
            public int CommandId;
            public int InitialScheduledTick;
            public long DueTimestamp;
            public long HeldTimestamp;
            public int SourcePlayerId;
            public int RequestId;
            public int HeldAtMapTick;
            public int MaximumObservedMapTick;
            public int LastObservedMapTick;
            public int RunCallsWhileHeld;
            public int RepeatedTickRunCalls;
            public int BarrierWaitRunCalls;
            public bool BarrierObserved;
            public int BarrierTargetTick;
            public int BarrierSequence;
            public int BarrierObservedAtTick;
        }

        private readonly ManualLogSource log;
        private readonly Func<int> getIncomingDelayMilliseconds;
        private readonly object observationLock = new object();
        private readonly object delayedChoreLock = new object();
        private readonly Dictionary<int, ChoreObservation> observationsByCommandId =
            new Dictionary<int, ChoreObservation>();
        private readonly Dictionary<long, ChoreObservation> observationsByRequest =
            new Dictionary<long, ChoreObservation>();
        private readonly HashSet<int> executedCommandIds = new HashSet<int>();
        private readonly HashSet<long> executedRequests = new HashSet<long>();
        private readonly List<DelayedChore> delayedChores = new List<DelayedChore>();

        private IntPtr moduleBase;
        private IntPtr choreManager;
        private IntPtr handlerTableEntry;
        private IntPtr originalHandler;
        private IntPtr installedHandler;
        private QueueLocalChoreDelegate queueLocalChore;
        private CopyChoreFieldDelegate copyChoreField;
        private object engineThreadLock;
        private SendChoresDelegate sendChoresTrampoline;
        private ReceiveChoreDelegate receiveChoreTrampoline;
        private RunDelegate runTrampoline;
        private byte[] stagedPayload;
        private volatile bool supported;
        private volatile bool operational;
        private bool delayClampWarningLogged;
        private int executeSequence;

        public NativeChoreProbe(
            ManualLogSource log,
            Func<int> getIncomingDelayMilliseconds)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.getIncomingDelayMilliseconds =
                getIncomingDelayMilliseconds ?? throw new ArgumentNullException(nameof(getIncomingDelayMilliseconds));
        }

        public bool IsSupported => supported && operational;

        public void Initialize(IntPtr crusaderModuleBase)
        {
            if (supported)
                return;

            string compatibilityFailure = ValidateCompatibility(crusaderModuleBase);
            if (compatibilityFailure != null)
            {
                LogError($"event=disabled reason={Sanitize(compatibilityFailure)}");
                return;
            }

            lock (StaticInstallLock)
            {
                if (activeInstance != null && activeInstance != this)
                {
                    LogError("event=disabled reason=another-native-chore-probe-instance-is-already-installed");
                    return;
                }

                Hook sendHook = null;
                Hook receiveHook = null;
                Hook engineRunHook = null;
                try
                {
                    queueLocalChore = Marshal.GetDelegateForFunctionPointer<QueueLocalChoreDelegate>(
                        Add(moduleBase, QueueLocalChoreRva));
                    copyChoreField = Marshal.GetDelegateForFunctionPointer<CopyChoreFieldDelegate>(
                        Add(moduleBase, CopyChoreFieldRva));
                    engineThreadLock = ResolveEngineThreadLock();

                    sendHook = new Hook(
                        FindMethod(
                            typeof(Platform_Multiplayer),
                            "SendChores",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            typeof(byte[])),
                        (SendChoresDelegate)SendChoresHook);
                    sendChoresTrampoline = sendHook.GenerateTrampoline<SendChoresDelegate>();

                    receiveHook = new Hook(
                        FindMethod(
                            typeof(EngineInterface),
                            "ReceiveChore",
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                            typeof(int),
                            typeof(byte[]),
                            typeof(int)),
                        (ReceiveChoreDelegate)ReceiveChoreHook);
                    receiveChoreTrampoline = receiveHook.GenerateTrampoline<ReceiveChoreDelegate>();

                    engineRunHook = new Hook(
                        FindMethod(
                            typeof(EngineInterface),
                            "run",
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                            typeof(bool)),
                        (RunDelegate)RunHook);
                    runTrampoline = engineRunHook.GenerateTrampoline<RunDelegate>();

                    rootedNativeHandler = NativeChoreHandlerThunk;
                    installedHandler = Marshal.GetFunctionPointerForDelegate(rootedNativeHandler);
                    activeInstance = this;

                    long expected = originalHandler.ToInt64();
                    long replacement = installedHandler.ToInt64();
                    long observed = Interlocked.CompareExchange(
                        ref *(long*)handlerTableEntry.ToPointer(),
                        replacement,
                        expected);
                    if (observed != expected)
                    {
                        activeInstance = null;
                        throw new InvalidOperationException(
                            $"handler-table-entry-changed-before-install(expected=0x{expected:X},actual=0x{observed:X})");
                    }

                    rootedSendChoresHook = sendHook;
                    rootedReceiveChoreHook = receiveHook;
                    rootedRunHook = engineRunHook;
                    supported = true;
                    operational = true;

                    LogInfo(
                        $"event=initialized role={GetPeerRole()} moduleBase=0x{moduleBase.ToInt64():X} " +
                        $"handlerEntry=0x{handlerTableEntry.ToInt64():X} originalHandler=0x{originalHandler.ToInt64():X} " +
                        $"installedHandler=0x{installedHandler.ToInt64():X} hash={ExpectedCrusaderSha256} " +
                        $"delayMs={GetConfiguredDelayMilliseconds()}");
                }
                catch (Exception ex)
                {
                    supported = false;
                    operational = false;
                    activeInstance = null;
                    sendHook?.Dispose();
                    receiveHook?.Dispose();
                    engineRunHook?.Dispose();
                    sendChoresTrampoline = null;
                    receiveChoreTrampoline = null;
                    runTrampoline = null;
                    LogError($"event=disabled reason=installation-failed exception={Sanitize(ex.ToString())}");
                }
            }
        }

        public bool TryEnqueue(int sourcePlayerId, int requestId, out string failureReason)
        {
            failureReason = null;
            if (!IsSupported)
            {
                failureReason = "native-chore-probe-unsupported";
                return false;
            }

            if (sourcePlayerId < 1 || sourcePlayerId > 8)
            {
                failureReason = "source-player-id-out-of-range";
                return false;
            }

            if (requestId <= 0)
            {
                failureReason = "request-id-not-positive";
                return false;
            }

            byte[] payload = CreatePayload((byte)sourcePlayerId, requestId);
            try
            {
                lock (engineThreadLock)
                {
                    if (stagedPayload != null)
                    {
                        failureReason = "native-chore-enqueue-is-already-in-progress";
                        return false;
                    }

                    stagedPayload = payload;
                    try
                    {
                        LogInfo(
                            $"event=enqueue role={GetPeerRole()} source={sourcePlayerId} request={requestId} " +
                            $"currentTick={GetCurrentMapTick()} opcode={ProbeOpcode}");
                        queueLocalChore(choreManager, ProbeOpcode);
                    }
                    finally
                    {
                        stagedPayload = null;
                    }
                }

                if (!operational)
                {
                    failureReason = "native-handler-failed-during-enqueue";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                operational = false;
                failureReason = "native-enqueue-threw";
                LogError(
                    $"event=enqueue-failed role={GetPeerRole()} source={sourcePlayerId} request={requestId} " +
                    $"exception={Sanitize(ex.ToString())}");
                return false;
            }
        }

        public bool TryEnqueueBatch(
            int sourcePlayerId,
            IReadOnlyList<int> requestIds,
            out int enqueuedCount,
            out string failureReason)
        {
            enqueuedCount = 0;
            failureReason = null;
            if (requestIds == null || requestIds.Count == 0)
            {
                failureReason = "request-batch-is-empty";
                return false;
            }

            LogInfo(
                $"event=batch-start role={GetPeerRole()} source={sourcePlayerId} " +
                $"firstRequest={requestIds[0]} count={requestIds.Count} currentTick={GetCurrentMapTick()}");

            for (int index = 0; index < requestIds.Count; index++)
            {
                if (!TryEnqueue(sourcePlayerId, requestIds[index], out failureReason))
                {
                    LogError(
                        $"event=batch-failed role={GetPeerRole()} source={sourcePlayerId} " +
                        $"firstRequest={requestIds[0]} count={requestIds.Count} enqueued={enqueuedCount} " +
                        $"failedRequest={requestIds[index]} reason={Sanitize(failureReason)}");
                    return false;
                }

                enqueuedCount++;
            }

            LogInfo(
                $"event=batch-complete role={GetPeerRole()} source={sourcePlayerId} " +
                $"firstRequest={requestIds[0]} lastRequest={requestIds[requestIds.Count - 1]} " +
                $"count={requestIds.Count} currentTick={GetCurrentMapTick()}");
            return true;
        }

        public void ClearMapState()
        {
            int delayedCount;
            lock (observationLock)
            {
                observationsByCommandId.Clear();
                observationsByRequest.Clear();
                executedCommandIds.Clear();
                executedRequests.Clear();
                executeSequence = 0;
            }

            lock (delayedChoreLock)
            {
                delayedCount = delayedChores.Count;
                delayedChores.Clear();
            }

            LogInfo(
                $"event=map-state-cleared role={GetPeerRole()} discardedDelayed={delayedCount}");
        }

        private static void NativeChoreHandlerThunk()
        {
            activeInstance?.HandleNativeChore();
        }

        private void HandleNativeChore()
        {
            try
            {
                int mode = Marshal.ReadInt32(Add(choreManager, HandlerModeOffset));
                Marshal.WriteInt32(Add(choreManager, HandlerPayloadSizeOffset), ProbePayloadSize);

                switch (mode)
                {
                    case 0:
                        ExecuteNoOp();
                        break;
                    case 1:
                        SerializeStagedPayload();
                        break;
                    case 2:
                        bool slotValid = TryReadCurrentSlot(out ChoreObservation slot);
                        ChoreObservation correlated = null;
                        bool correlationValid =
                            slotValid &&
                            TryGetObservationByCommandId(slot.CommandId, out correlated);
                        LogInfo(
                            $"event=handler role={GetPeerRole()} mode=2 opcode={ProbeOpcode} " +
                            $"source={(correlationValid ? correlated.SourcePlayerId : 0)} " +
                            $"request={(correlationValid ? correlated.RequestId : 0)} " +
                            $"commandId={(slotValid ? slot.CommandId : 0)} " +
                            $"scheduledTick={(slotValid ? slot.ScheduledTick : -1)} " +
                            $"payloadSize={ProbePayloadSize} currentTick={GetCurrentMapTick()} " +
                            $"valid={correlationValid.ToString().ToLowerInvariant()} mutation=none");
                        break;
                    default:
                        operational = false;
                        LogError(
                            $"event=handler-failed role={GetPeerRole()} mode={mode} opcode={ProbeOpcode} " +
                            $"reason=unexpected-handler-mode");
                        break;
                }
            }
            catch (Exception ex)
            {
                operational = false;
                LogError(
                    $"event=handler-failed role={GetPeerRole()} opcode={ProbeOpcode} " +
                    $"exception={Sanitize(ex.ToString())}");
            }
        }

        private void SerializeStagedPayload()
        {
            byte[] payload = stagedPayload;
            if (payload == null || payload.Length != ProbePayloadSize)
            {
                operational = false;
                LogError(
                    $"event=handler-failed role={GetPeerRole()} mode=1 opcode={ProbeOpcode} " +
                    "reason=staged-payload-unavailable");
                return;
            }

            fixed (byte* payloadPointer = payload)
            {
                copyChoreField(choreManager, (IntPtr)payloadPointer, ProbePayloadSize, 1, 0);
            }

            TryParseProbePayload(payload, 0, payload.Length, out ProbePayload parsedPayload);
            bool slotValid = TryReadCurrentSlot(out ChoreObservation slot);
            if (slotValid)
            {
                slot.SourcePlayerId = parsedPayload.SourcePlayerId;
                slot.RequestId = parsedPayload.RequestId;
                RecordObservation(slot, "handler-mode1");
            }

            LogInfo(
                $"event=handler role={GetPeerRole()} mode=1 opcode={ProbeOpcode} " +
                $"source={parsedPayload.SourcePlayerId} request={parsedPayload.RequestId} " +
                $"commandId={(slotValid ? slot.CommandId : 0)} scheduledTick={(slotValid ? slot.ScheduledTick : -1)} " +
                $"currentTick={GetCurrentMapTick()} payloadSize={ProbePayloadSize} valid=true mutation=none");
        }

        private void ExecuteNoOp()
        {
            byte[] payload = new byte[ProbePayloadSize];
            fixed (byte* payloadPointer = payload)
            {
                copyChoreField(choreManager, (IntPtr)payloadPointer, ProbePayloadSize, 1, 1);
            }

            bool payloadValid = TryParseProbePayload(payload, 0, payload.Length, out ProbePayload parsedPayload);
            bool slotValid = TryReadCurrentSlot(out ChoreObservation slot);
            if (slotValid && payloadValid)
            {
                slot.SourcePlayerId = parsedPayload.SourcePlayerId;
                slot.RequestId = parsedPayload.RequestId;
                RecordObservation(slot, "handler-mode0");
            }
            else if (payloadValid &&
                TryGetObservation(parsedPayload.SourcePlayerId, parsedPayload.RequestId, out ChoreObservation correlated))
            {
                slot = correlated;
                slotValid = true;
            }

            int actualTick = GetCurrentMapTick();
            int sequence = 0;
            if (payloadValid && slotValid)
            {
                long requestKey = GetRequestKey(parsedPayload.SourcePlayerId, parsedPayload.RequestId);
                bool duplicateCommand;
                bool duplicateRequest;
                lock (observationLock)
                {
                    duplicateCommand = !executedCommandIds.Add(slot.CommandId);
                    duplicateRequest = !executedRequests.Add(requestKey);
                    sequence = ++executeSequence;
                }

                if (duplicateCommand || duplicateRequest)
                {
                    LogError(
                        $"event=duplicate-execute role={GetPeerRole()} source={parsedPayload.SourcePlayerId} " +
                        $"request={parsedPayload.RequestId} commandId={slot.CommandId} " +
                        $"duplicateCommand={duplicateCommand.ToString().ToLowerInvariant()} " +
                        $"duplicateRequest={duplicateRequest.ToString().ToLowerInvariant()} actualTick={actualTick}");
                }

                if (slot.ScheduledTick != actualTick)
                {
                    LogError(
                        $"event=execute-tick-mismatch role={GetPeerRole()} source={parsedPayload.SourcePlayerId} " +
                        $"request={parsedPayload.RequestId} commandId={slot.CommandId} " +
                        $"scheduledTick={slot.ScheduledTick} actualTick={actualTick}");
                }
            }
            else
            {
                LogError(
                    $"event=invalid-execute role={GetPeerRole()} source={parsedPayload.SourcePlayerId} " +
                    $"request={parsedPayload.RequestId} payloadValid={payloadValid.ToString().ToLowerInvariant()} " +
                    $"slotValid={slotValid.ToString().ToLowerInvariant()} actualTick={actualTick}");
            }

            LogInfo(
                $"event=execute role={GetPeerRole()} mode=0 opcode={ProbeOpcode} " +
                $"source={parsedPayload.SourcePlayerId} request={parsedPayload.RequestId} " +
                $"commandId={(slotValid ? slot.CommandId : 0)} scheduledTick={(slotValid ? slot.ScheduledTick : -1)} " +
                $"actualTick={actualTick} executeSequence={sequence} payloadSize={ProbePayloadSize} " +
                $"valid={(payloadValid && slotValid).ToString().ToLowerInvariant()} mutation=none");
        }

        private void SendChoresHook(Platform_Multiplayer self, byte[] choreBuffer)
        {
            try
            {
                InspectOutgoingChoreBuffer(choreBuffer);
            }
            catch (Exception ex)
            {
                LogError($"event=edge-inspection-failed direction=outgoing exception={Sanitize(ex.ToString())}");
            }

            sendChoresTrampoline(self, choreBuffer);
        }

        private void ReceiveChoreHook(int playerId, byte[] data, int dataLength)
        {
            ProbePayload parsedPayload = default;
            bool validProbe = false;
            try
            {
                InspectInnerChore(data, 0, dataLength, "incoming", playerId, -1);
                validProbe =
                    data != null &&
                    dataLength >= 8 + ProbePayloadSize &&
                    dataLength <= data.Length &&
                    data[0] == ProbeOpcode &&
                    TryParseProbePayload(data, 8, dataLength - 8, out parsedPayload);
            }
            catch (Exception ex)
            {
                LogError($"event=edge-inspection-failed direction=incoming exception={Sanitize(ex.ToString())}");
            }

            if (validProbe &&
                TryHoldIncomingProbe(
                    playerId,
                    data,
                    dataLength,
                    ReadInt32(data, 4),
                    ReadUInt24(data, 1),
                    parsedPayload))
            {
                return;
            }

            receiveChoreTrampoline(playerId, data, dataLength);
        }

        private int RunHook(bool multiplayerFrameSkip)
        {
            try
            {
                FlushDueChores();
            }
            catch (Exception ex)
            {
                LogError($"event=delay-flush-failed exception={Sanitize(ex.ToString())}");
            }

            return runTrampoline(multiplayerFrameSkip);
        }

        private void InspectOutgoingChoreBuffer(byte[] choreBuffer)
        {
            if (choreBuffer == null)
                return;

            int offset = 0;
            for (int recordIndex = 0; recordIndex < 10000; recordIndex++)
            {
                if (offset > choreBuffer.Length - 4)
                {
                    LogError(
                        $"event=malformed-buffer direction=outgoing reason=missing-record-length offset={offset} " +
                        $"bufferLength={choreBuffer.Length}");
                    return;
                }

                int payloadLength = ReadInt32(choreBuffer, offset);
                if (payloadLength < 0)
                    return;

                if (payloadLength < 1 ||
                    offset > choreBuffer.Length - 5 ||
                    payloadLength > choreBuffer.Length - offset - 5)
                {
                    LogError(
                        $"event=malformed-buffer direction=outgoing reason=invalid-record-length offset={offset} " +
                        $"payloadLength={payloadLength} bufferLength={choreBuffer.Length}");
                    return;
                }

                int targetPlayerId = choreBuffer[offset + 4];
                InspectInnerChore(
                    choreBuffer,
                    offset + 5,
                    payloadLength,
                    "outgoing",
                    -1,
                    targetPlayerId);
                offset += payloadLength + 5;
            }

            LogError("event=malformed-buffer direction=outgoing reason=record-limit-exceeded");
        }

        private void InspectInnerChore(
            byte[] data,
            int offset,
            int length,
            string direction,
            int senderPlayerId,
            int targetPlayerId)
        {
            if (data == null || offset < 0 || length < 1 || offset > data.Length - length)
                return;

            byte opcode = data[offset];
            if (opcode == ResyncStartOpcode || opcode == ResyncEndOpcode)
            {
                LogInfo(
                    $"event={(opcode == ResyncStartOpcode ? "resync-start" : "resync-end")} " +
                    $"role={GetPeerRole()} direction={direction} opcode={opcode} currentTick={GetCurrentMapTick()} " +
                    $"sender={senderPlayerId} target={targetPlayerId}");
                return;
            }

            if (opcode != ProbeOpcode && opcode != SyncEventOpcode)
                return;

            if (length < 8)
            {
                LogError(
                    $"event=malformed-chore direction={direction} opcode={opcode} reason=header-too-short length={length}");
                return;
            }

            int scheduledTick = ReadUInt24(data, offset + 1);
            int commandId = ReadInt32(data, offset + 4);
            int currentTick = GetCurrentMapTick();

            if (opcode == ProbeOpcode)
            {
                bool valid = TryParseProbePayload(data, offset + 8, length - 8, out ProbePayload payload);
                ChoreObservation observation = new ChoreObservation
                {
                    SourcePlayerId = payload.SourcePlayerId,
                    RequestId = payload.RequestId,
                    CommandId = commandId,
                    ScheduledTick = scheduledTick
                };
                if (valid)
                    RecordObservation(observation, $"edge-{direction}");

                LogInfo(
                    $"event=edge role={GetPeerRole()} direction={direction} opcode={opcode} " +
                    $"source={payload.SourcePlayerId} request={payload.RequestId} commandId={commandId} " +
                    $"scheduledTick={scheduledTick} currentTick={currentTick} sender={senderPlayerId} " +
                    $"target={targetPlayerId} valid={valid.ToString().ToLowerInvariant()}");
                return;
            }

            InspectSyncEvent(
                data,
                offset,
                length,
                direction,
                senderPlayerId,
                commandId,
                scheduledTick,
                currentTick);
        }

        private void InspectSyncEvent(
            byte[] data,
            int offset,
            int length,
            string direction,
            int senderPlayerId,
            int syncCommandId,
            int scheduledTick,
            int currentTick)
        {
            if (length < 20)
            {
                LogError(
                    $"event=malformed-chore direction={direction} opcode={SyncEventOpcode} " +
                    $"reason=sync-event-too-short length={length}");
                return;
            }

            int targetTick = ReadInt32(data, offset + 8);
            int count = ReadInt32(data, offset + 12);
            int sequence = ReadInt32(data, offset + 16);
            int availableIds = (length - 20) / 4;
            if (count < 0 || count > availableIds || count > MaximumPendingSlots)
            {
                LogError(
                    $"event=malformed-chore direction={direction} opcode={SyncEventOpcode} " +
                    $"reason=invalid-sync-count count={count} available={availableIds} length={length}");
                return;
            }

            List<int> allIds = new List<int>(count);
            List<int> matchedIds = new List<int>();
            for (int index = 0; index < count; index++)
            {
                int listedCommandId = ReadInt32(data, offset + 20 + index * 4);
                allIds.Add(listedCommandId);
                lock (observationLock)
                {
                    if (observationsByCommandId.ContainsKey(listedCommandId))
                        matchedIds.Add(listedCommandId);
                }
            }

            LogInfo(
                $"event=barrier role={GetPeerRole()} direction={direction} opcode={SyncEventOpcode} " +
                $"syncCommandId={syncCommandId} scheduledTick={scheduledTick} targetTick={targetTick} " +
                $"count={count} sequence={sequence} ids={JoinIds(allIds)} matched={JoinIds(matchedIds)} " +
                $"currentTick={currentTick} sender={senderPlayerId}");

            ObserveBarrierForDelayedChores(
                direction,
                allIds,
                targetTick,
                sequence,
                currentTick);
        }

        private bool TryHoldIncomingProbe(
            int playerId,
            byte[] data,
            int dataLength,
            int commandId,
            int initialScheduledTick,
            ProbePayload payload)
        {
            int delayMilliseconds = GetConfiguredDelayMilliseconds();
            if (delayMilliseconds <= 0 ||
                !GameNetworkAPI.IsNetworkedEnvironment() ||
                GameNetworkAPI.IsLocalHost())
            {
                return false;
            }

            if (data == null || dataLength <= 0 || dataLength > data.Length)
                return false;

            byte[] copy = new byte[dataLength];
            Buffer.BlockCopy(data, 0, copy, 0, dataLength);
            int currentTick = GetCurrentMapTick();
            long heldTimestamp = Stopwatch.GetTimestamp();
            long delayTicks = checked((long)delayMilliseconds * Stopwatch.Frequency / 1000L);
            DelayedChore delayed = new DelayedChore
            {
                PlayerId = playerId,
                Data = copy,
                DataLength = dataLength,
                CommandId = commandId,
                InitialScheduledTick = initialScheduledTick,
                DueTimestamp = heldTimestamp + delayTicks,
                HeldTimestamp = heldTimestamp,
                SourcePlayerId = payload.SourcePlayerId,
                RequestId = payload.RequestId,
                HeldAtMapTick = currentTick,
                MaximumObservedMapTick = currentTick,
                LastObservedMapTick = currentTick
            };

            lock (delayedChoreLock)
            {
                if (delayedChores.Count >= MaximumDelayedChores)
                {
                    LogError(
                        $"event=delay-bypassed role={GetPeerRole()} source={payload.SourcePlayerId} " +
                        $"request={payload.RequestId} reason=queue-full count={delayedChores.Count}");
                    return false;
                }

                delayedChores.Add(delayed);
            }

            LogInfo(
                $"event=delay-held role={GetPeerRole()} source={payload.SourcePlayerId} request={payload.RequestId} " +
                $"commandId={commandId} initialScheduledTick={initialScheduledTick} sender={playerId} " +
                $"delayMs={delayMilliseconds} heldAtTick={currentTick}");
            return true;
        }

        private void ObserveBarrierForDelayedChores(
            string direction,
            List<int> commandIds,
            int targetTick,
            int sequence,
            int currentTick)
        {
            if (!string.Equals(direction, "incoming", StringComparison.Ordinal) ||
                commandIds == null ||
                commandIds.Count == 0)
            {
                return;
            }

            List<DelayedChore> newlyObserved = null;
            lock (delayedChoreLock)
            {
                for (int delayedIndex = 0; delayedIndex < delayedChores.Count; delayedIndex++)
                {
                    DelayedChore delayed = delayedChores[delayedIndex];
                    if (delayed.BarrierObserved || !commandIds.Contains(delayed.CommandId))
                        continue;

                    delayed.BarrierObserved = true;
                    delayed.BarrierTargetTick = targetTick;
                    delayed.BarrierSequence = sequence;
                    delayed.BarrierObservedAtTick = currentTick;
                    if (newlyObserved == null)
                        newlyObserved = new List<DelayedChore>();
                    newlyObserved.Add(delayed);
                }
            }

            if (newlyObserved == null)
                return;

            for (int index = 0; index < newlyObserved.Count; index++)
            {
                DelayedChore delayed = newlyObserved[index];
                LogInfo(
                    $"event=delay-barrier-observed role={GetPeerRole()} source={delayed.SourcePlayerId} " +
                    $"request={delayed.RequestId} commandId={delayed.CommandId} targetTick={targetTick} " +
                    $"sequence={sequence} observedAtTick={currentTick} heldAtTick={delayed.HeldAtMapTick}");
            }
        }

        private void FlushDueChores()
        {
            List<DelayedChore> due = null;
            long now = Stopwatch.GetTimestamp();
            int currentTick = GetCurrentMapTick();

            lock (delayedChoreLock)
            {
                for (int index = delayedChores.Count - 1; index >= 0; index--)
                {
                    DelayedChore delayed = delayedChores[index];
                    delayed.RunCallsWhileHeld++;
                    if (currentTick == delayed.LastObservedMapTick)
                        delayed.RepeatedTickRunCalls++;
                    delayed.LastObservedMapTick = currentTick;
                    if (currentTick > delayed.MaximumObservedMapTick)
                        delayed.MaximumObservedMapTick = currentTick;
                    if (delayed.BarrierObserved && currentTick >= delayed.BarrierTargetTick)
                        delayed.BarrierWaitRunCalls++;

                    if (now < delayed.DueTimestamp)
                        continue;

                    if (due == null)
                        due = new List<DelayedChore>();
                    due.Add(delayed);
                    delayedChores.RemoveAt(index);
                }
            }

            if (due == null)
                return;

            due.Reverse();
            for (int index = 0; index < due.Count; index++)
            {
                DelayedChore delayed = due[index];
                long releasedTimestamp = Stopwatch.GetTimestamp();
                long elapsedMilliseconds =
                    (releasedTimestamp - delayed.HeldTimestamp) * 1000L / Stopwatch.Frequency;
                int releaseTick = GetCurrentMapTick();
                bool crossedBarrier =
                    delayed.BarrierObserved &&
                    delayed.MaximumObservedMapTick > delayed.BarrierTargetTick;
                LogInfo(
                    $"event=delay-released role={GetPeerRole()} source={delayed.SourcePlayerId} " +
                    $"request={delayed.RequestId} commandId={delayed.CommandId} sender={delayed.PlayerId} " +
                    $"elapsedMs={elapsedMilliseconds} heldAtTick={delayed.HeldAtMapTick} " +
                    $"maxObservedTick={delayed.MaximumObservedMapTick} releaseTick={releaseTick} " +
                    $"runCalls={delayed.RunCallsWhileHeld} repeatedTickRuns={delayed.RepeatedTickRunCalls} " +
                    $"barrierObserved={delayed.BarrierObserved.ToString().ToLowerInvariant()} " +
                    $"barrierTargetTick={delayed.BarrierTargetTick} barrierSequence={delayed.BarrierSequence} " +
                    $"barrierObservedAtTick={delayed.BarrierObservedAtTick} " +
                    $"barrierWaitRunCalls={delayed.BarrierWaitRunCalls} " +
                    $"crossedBarrier={crossedBarrier.ToString().ToLowerInvariant()}");
                receiveChoreTrampoline(delayed.PlayerId, delayed.Data, delayed.DataLength);
                LogInfo(
                    $"event=delay-injected role={GetPeerRole()} source={delayed.SourcePlayerId} " +
                    $"request={delayed.RequestId} commandId={delayed.CommandId} " +
                    $"injectedAtTick={GetCurrentMapTick()} elapsedMs={elapsedMilliseconds}");
            }
        }

        private string ValidateCompatibility(IntPtr crusaderModuleBase)
        {
            if (IntPtr.Size != 8)
                return "process-is-not-64-bit";
            if (crusaderModuleBase == IntPtr.Zero)
                return "crusader-module-base-is-zero";

            string modulePath;
            try
            {
                modulePath = GetModulePath(crusaderModuleBase);
            }
            catch (Exception ex)
            {
                return $"cannot-resolve-module-path({ex.Message})";
            }

            FileInfo fileInfo = new FileInfo(modulePath);
            if (!fileInfo.Exists)
                return $"crusader-module-file-not-found({modulePath})";
            if (fileInfo.Length != ExpectedCrusaderFileSize)
                return $"crusader-file-size-mismatch(expected={ExpectedCrusaderFileSize},actual={fileInfo.Length})";

            string actualHash;
            using (FileStream stream = fileInfo.OpenRead())
            using (SHA256 sha256 = SHA256.Create())
                actualHash = ToHex(sha256.ComputeHash(stream));

            if (!string.Equals(actualHash, ExpectedCrusaderSha256, StringComparison.OrdinalIgnoreCase))
                return $"crusader-sha256-mismatch(expected={ExpectedCrusaderSha256},actual={actualHash})";

            moduleBase = crusaderModuleBase;
            ulong choreManagerAddress = GameGlobalsManager.Instance.ChoreManagerVA;
            if (choreManagerAddress == 0 || choreManagerAddress > long.MaxValue)
                return "script-extender-chore-manager-address-is-invalid";
            choreManager = new IntPtr(unchecked((long)choreManagerAddress));
            handlerTableEntry = Add(moduleBase, HandlerTableRva + ProbeOpcode * IntPtr.Size);
            originalHandler = Add(moduleBase, OriginalProbeHandlerRva);

            if (!MatchesBytes(originalHandler, ExpectedOriginalProbeHandler))
                return "opcode-111-original-handler-bytes-mismatch";
            if (!MatchesBytes(Add(moduleBase, QueueLocalChoreRva), ExpectedQueueLocalChorePrologue))
                return "queue-local-chore-prologue-mismatch";
            if (!MatchesBytes(Add(moduleBase, CopyChoreFieldRva), ExpectedCopyChoreFieldPrologue))
                return "copy-chore-field-prologue-mismatch";

            IntPtr observedHandler = Marshal.ReadIntPtr(handlerTableEntry);
            if (observedHandler != originalHandler)
            {
                return
                    $"opcode-111-handler-pointer-mismatch(expected=0x{originalHandler.ToInt64():X}," +
                    $"actual=0x{observedHandler.ToInt64():X})";
            }

            if (!IsWritableCommittedMemory(handlerTableEntry))
                return "opcode-111-handler-table-entry-is-not-writable-committed-memory";

            return null;
        }

        private bool TryReadCurrentSlot(out ChoreObservation observation)
        {
            observation = null;
            int slotIndex = Marshal.ReadInt32(Add(choreManager, CurrentSlotIndexOffset));
            if (slotIndex < 0 || slotIndex >= MaximumPendingSlots)
                return false;

            IntPtr slot = Add(choreManager, PendingSlotsOffset + (long)slotIndex * PendingSlotSize);
            byte opcode = Marshal.ReadByte(Add(slot, 8));
            if (opcode != ProbeOpcode)
                return false;

            observation = new ChoreObservation
            {
                ScheduledTick = Marshal.ReadInt32(slot),
                SourcePlayerId = checked((byte)Marshal.ReadInt32(Add(slot, 4))),
                CommandId = Marshal.ReadInt32(Add(slot, 12))
            };
            return observation.CommandId > 0;
        }

        private void RecordObservation(ChoreObservation observation, string source)
        {
            if (observation == null ||
                observation.SourcePlayerId == 0 ||
                observation.RequestId <= 0 ||
                observation.CommandId <= 0)
            {
                return;
            }

            long requestKey = GetRequestKey(observation.SourcePlayerId, observation.RequestId);
            lock (observationLock)
            {
                bool identityConflict = false;
                int previousScheduledTick = int.MinValue;
                if (observationsByCommandId.TryGetValue(
                    observation.CommandId,
                    out ChoreObservation byCommand))
                {
                    if (byCommand.SourcePlayerId != observation.SourcePlayerId ||
                        byCommand.RequestId != observation.RequestId)
                    {
                        identityConflict = true;
                        LogError(
                            $"event=correlation-conflict kind=command sourcePath={source} commandId={observation.CommandId} " +
                            $"oldSource={byCommand.SourcePlayerId} oldRequest={byCommand.RequestId} " +
                            $"newSource={observation.SourcePlayerId} newRequest={observation.RequestId} " +
                            $"oldScheduledTick={byCommand.ScheduledTick} newScheduledTick={observation.ScheduledTick}");
                    }
                    else if (byCommand.ScheduledTick != observation.ScheduledTick)
                    {
                        previousScheduledTick = byCommand.ScheduledTick;
                    }
                }

                if (observationsByRequest.TryGetValue(
                    requestKey,
                    out ChoreObservation byRequest))
                {
                    if (byRequest.CommandId != observation.CommandId)
                    {
                        identityConflict = true;
                        LogError(
                            $"event=correlation-conflict kind=request sourcePath={source} " +
                            $"source={observation.SourcePlayerId} request={observation.RequestId} " +
                            $"oldCommandId={byRequest.CommandId} newCommandId={observation.CommandId} " +
                            $"oldScheduledTick={byRequest.ScheduledTick} newScheduledTick={observation.ScheduledTick}");
                    }
                    else if (byRequest.ScheduledTick != observation.ScheduledTick &&
                        previousScheduledTick == int.MinValue)
                    {
                        previousScheduledTick = byRequest.ScheduledTick;
                    }
                }

                if (!identityConflict &&
                    previousScheduledTick != int.MinValue &&
                    previousScheduledTick != observation.ScheduledTick)
                {
                    LogInfo(
                        $"event=schedule-updated role={GetPeerRole()} sourcePath={source} " +
                        $"source={observation.SourcePlayerId} request={observation.RequestId} " +
                        $"commandId={observation.CommandId} oldScheduledTick={previousScheduledTick} " +
                        $"newScheduledTick={observation.ScheduledTick}");
                }

                observationsByCommandId[observation.CommandId] = observation;
                observationsByRequest[requestKey] = observation;
            }
        }

        private bool TryGetObservation(
            int sourcePlayerId,
            int requestId,
            out ChoreObservation observation)
        {
            lock (observationLock)
                return observationsByRequest.TryGetValue(GetRequestKey(sourcePlayerId, requestId), out observation);
        }

        private bool TryGetObservationByCommandId(
            int commandId,
            out ChoreObservation observation)
        {
            lock (observationLock)
                return observationsByCommandId.TryGetValue(commandId, out observation);
        }

        private static byte[] CreatePayload(byte sourcePlayerId, int requestId)
        {
            byte[] payload = new byte[ProbePayloadSize];
            WriteUInt32(payload, 0, ProbeMagic);
            WriteUInt16(payload, 4, ProbeProtocolVersion);
            payload[6] = sourcePlayerId;
            payload[7] = 0;
            WriteInt32(payload, 8, requestId);
            WriteUInt32(payload, 12, ProbeSentinel);
            return payload;
        }

        private static bool TryParseProbePayload(
            byte[] data,
            int offset,
            int availableLength,
            out ProbePayload payload)
        {
            payload = default;
            if (data == null ||
                offset < 0 ||
                availableLength < ProbePayloadSize ||
                offset > data.Length - ProbePayloadSize)
            {
                return false;
            }

            payload.SourcePlayerId = data[offset + 6];
            payload.Flags = data[offset + 7];
            payload.RequestId = ReadInt32(data, offset + 8);
            return
                ReadUInt32(data, offset) == ProbeMagic &&
                ReadUInt16(data, offset + 4) == ProbeProtocolVersion &&
                payload.SourcePlayerId >= 1 &&
                payload.SourcePlayerId <= 8 &&
                payload.Flags == 0 &&
                payload.RequestId > 0 &&
                ReadUInt32(data, offset + 12) == ProbeSentinel;
        }

        private int GetConfiguredDelayMilliseconds()
        {
            int value;
            try
            {
                value = getIncomingDelayMilliseconds();
            }
            catch (Exception ex)
            {
                LogError($"event=config-read-failed exception={Sanitize(ex.ToString())}");
                return 0;
            }

            if (value <= 0)
                return 0;
            if (value <= MaximumDelayMilliseconds)
                return value;

            if (!delayClampWarningLogged)
            {
                delayClampWarningLogged = true;
                LogInfo(
                    $"event=config-clamped name=DelayIncomingProbeMs requested={value} " +
                    $"effective={MaximumDelayMilliseconds}");
            }

            return MaximumDelayMilliseconds;
        }

        private static MethodInfo FindMethod(
            Type declaringType,
            string name,
            BindingFlags bindingFlags,
            params Type[] parameterTypes)
        {
            MethodInfo method = declaringType.GetMethod(
                name,
                bindingFlags,
                null,
                parameterTypes,
                null);
            if (method == null)
                throw new MissingMethodException(declaringType.FullName, name);
            return method;
        }

        private static object ResolveEngineThreadLock()
        {
            FieldInfo field = typeof(EngineInterface).GetField(
                "threadLock",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(typeof(EngineInterface).FullName, "threadLock");

            object value = field.GetValue(null);
            if (value == null)
                throw new InvalidOperationException("EngineInterface.threadLock resolved to null.");
            return value;
        }

        private static string GetModulePath(IntPtr moduleHandle)
        {
            StringBuilder path = new StringBuilder(32768);
            int length = GetModuleFileName(moduleHandle, path, path.Capacity);
            if (length <= 0)
                throw new InvalidOperationException($"GetModuleFileName failed with Win32 error {Marshal.GetLastWin32Error()}.");
            if (length >= path.Capacity - 1)
                throw new PathTooLongException("CrusaderDE.dll module path exceeded the fixed buffer.");
            return path.ToString();
        }

        private static bool MatchesBytes(IntPtr address, byte[] expected)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                if (Marshal.ReadByte(address, index) != expected[index])
                    return false;
            }
            return true;
        }

        private static bool IsWritableCommittedMemory(IntPtr address)
        {
            UIntPtr result = VirtualQuery(
                address,
                out MemoryBasicInformation information,
                (UIntPtr)Marshal.SizeOf(typeof(MemoryBasicInformation)));
            if (result == UIntPtr.Zero || information.State != 0x1000)
                return false;

            const uint PageGuard = 0x100;
            const uint PageNoAccess = 0x01;
            if ((information.Protect & (PageGuard | PageNoAccess)) != 0)
                return false;

            uint basicProtection = information.Protect & 0xFF;
            return
                basicProtection == 0x04 ||
                basicProtection == 0x08 ||
                basicProtection == 0x40 ||
                basicProtection == 0x80;
        }

        private int GetCurrentMapTick()
        {
            try
            {
                return GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            }
            catch
            {
                return -1;
            }
        }

        private static string GetPeerRole()
        {
            try
            {
                if (!GameNetworkAPI.IsNetworkedEnvironment())
                    return "singleplayer";
                return GameNetworkAPI.IsLocalHost() ? "host" : "client";
            }
            catch
            {
                return "unknown";
            }
        }

        private static string JoinIds(List<int> ids)
        {
            return ids == null || ids.Count == 0 ? "none" : string.Join(",", ids);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "none";
            return value.Replace("\r", "|").Replace("\n", "|").Replace(" ", "_");
        }

        private static long GetRequestKey(int sourcePlayerId, int requestId)
        {
            return ((long)sourcePlayerId << 32) | (uint)requestId;
        }

        private static IntPtr Add(IntPtr pointer, long offset)
        {
            return new IntPtr(checked(pointer.ToInt64() + offset));
        }

        private static int ReadUInt24(byte[] data, int offset)
        {
            return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16;
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | data[offset + 1] << 8);
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return
                (uint)data[offset] |
                (uint)data[offset + 1] << 8 |
                (uint)data[offset + 2] << 16 |
                (uint)data[offset + 3] << 24;
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return unchecked((int)ReadUInt32(data, offset));
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt32(byte[] data, int offset, int value)
        {
            WriteUInt32(data, offset, unchecked((uint)value));
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                result.Append(bytes[index].ToString("X2"));
            return result.ToString();
        }

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, $"MPTest ChoreProbe: {message}");
        }

        private void LogError(string message)
        {
            Shared.DebugLogHelper.LogError(log, $"MPTest ChoreProbe: {message}");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetModuleFileName(
            IntPtr moduleHandle,
            StringBuilder fileName,
            int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr VirtualQuery(
            IntPtr address,
            out MemoryBasicInformation information,
            UIntPtr length);
    }
}
