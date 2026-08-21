using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RandomEvents
{
    internal sealed class RandomEventsVanillaChoreDiagnostics
    {
        private const int CaptureWindowMilliseconds = 20000;
        private const int MaximumCapturedRecords = 256;
        private const int MaximumContexts = 8;

        private delegate int GameActionDelegate(
            Enums.GameActionCommand command,
            int structureId,
            int state,
            int value2);

        private delegate void SendChoresDelegate(Platform_Multiplayer self, byte[] choreBuffer);
        private delegate void ReceiveChoreDelegate(int playerId, byte[] data, int dataLength);

        private readonly ManualLogSource log;
        private readonly object captureLock = new object();
        private readonly List<string> contexts = new List<string>();
        private Hook gameActionHook;
        private Hook sendChoresHook;
        private Hook receiveChoreHook;
        private GameActionDelegate gameActionTrampoline;
        private SendChoresDelegate sendChoresTrampoline;
        private ReceiveChoreDelegate receiveChoreTrampoline;
        private long captureUntilTimestamp;
        private int capturedRecordCount;
        private bool captureLimitLogged;
        private bool initialized;

        public RandomEventsVanillaChoreDiagnostics(ManualLogSource log)
        {
            this.log = log;
        }

        public void Initialize()
        {
            if (initialized)
                return;

            Hook newGameActionHook = null;
            Hook newSendChoresHook = null;
            Hook newReceiveChoreHook = null;
            try
            {
                newGameActionHook = new Hook(
                    FindMethod(
                        typeof(EngineInterface),
                        "GameAction",
                        BindingFlags.Static | BindingFlags.Public,
                        typeof(Enums.GameActionCommand),
                        typeof(int),
                        typeof(int),
                        typeof(int)),
                    (GameActionDelegate)GameActionHook);
                gameActionTrampoline = newGameActionHook.GenerateTrampoline<GameActionDelegate>();

                newSendChoresHook = new Hook(
                    FindMethod(
                        typeof(Platform_Multiplayer),
                        "SendChores",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        typeof(byte[])),
                    (SendChoresDelegate)SendChoresHook);
                sendChoresTrampoline = newSendChoresHook.GenerateTrampoline<SendChoresDelegate>();

                newReceiveChoreHook = new Hook(
                    FindMethod(
                        typeof(EngineInterface),
                        "ReceiveChore",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        typeof(int),
                        typeof(byte[]),
                        typeof(int)),
                    (ReceiveChoreDelegate)ReceiveChoreHook);
                receiveChoreTrampoline = newReceiveChoreHook.GenerateTrampoline<ReceiveChoreDelegate>();

                // These hooks must remain rooted for the entire process lifetime.
                gameActionHook = newGameActionHook;
                sendChoresHook = newSendChoresHook;
                receiveChoreHook = newReceiveChoreHook;
                initialized = true;
                LogDebug("Random Events Vanilla Chore diagnostics installed (capture is event-scoped).");
            }
            catch (Exception ex)
            {
                newReceiveChoreHook?.Dispose();
                newSendChoresHook?.Dispose();
                newGameActionHook?.Dispose();
                LogError($"Random Events Vanilla Chore diagnostics could not be installed: {ex}");
            }
        }

        public void Arm(
            int dueAbsoluteMonth,
            int actionIndex,
            string eventName,
            int textId,
            int strength,
            int targetPlayerId,
            bool isLocalHost)
        {
            if (!initialized)
                return;

            string context =
                $"due={dueAbsoluteMonth}/index={actionIndex}/event={eventName}/textId={textId}/" +
                $"strength={strength}/target={targetPlayerId}/role={(isLocalHost ? "host" : "client")}";
            lock (captureLock)
            {
                if (contexts.Count >= MaximumContexts)
                    contexts.RemoveAt(0);
                contexts.Add(context);
                captureUntilTimestamp = Stopwatch.GetTimestamp() +
                    (long)CaptureWindowMilliseconds * Stopwatch.Frequency / 1000L;
                capturedRecordCount = 0;
                captureLimitLogged = false;
            }

            LogDebug(
                $"Random Events Vanilla Chore capture armed: context={context}, " +
                $"mapTick={GetCurrentMapTick()}, localPlayerId={GetLocalPlayerId()}, " +
                $"threadId={Thread.CurrentThread.ManagedThreadId}, windowMs={CaptureWindowMilliseconds}.");
        }

        public void Clear()
        {
            lock (captureLock)
            {
                contexts.Clear();
                captureUntilTimestamp = 0;
                capturedRecordCount = 0;
                captureLimitLogged = false;
            }
        }

        private int GameActionHook(
            Enums.GameActionCommand command,
            int structureId,
            int state,
            int value2)
        {
            if (!IsCaptureActive() || command != Enums.GameActionCommand.FreeBuild_Event)
                return gameActionTrampoline(command, structureId, state, value2);

            int localPlayerIdBefore = GetLocalPlayerId();
            int mapTickBefore = GetCurrentMapTick();
            LogDebug(
                $"Random Events Vanilla GameAction entering: contexts={GetContexts()}, command={(int)command}, " +
                $"structureId={structureId}, state={state}, value2={value2}, mapTick={mapTickBefore}, " +
                $"localPlayerId={localPlayerIdBefore}, threadId={Thread.CurrentThread.ManagedThreadId}.");
            int result = gameActionTrampoline(command, structureId, state, value2);
            LogDebug(
                $"Random Events Vanilla GameAction returned: contexts={GetContexts()}, command={(int)command}, " +
                $"result={result}, mapTickBefore={mapTickBefore}, mapTickAfter={GetCurrentMapTick()}, " +
                $"localPlayerIdBefore={localPlayerIdBefore}, localPlayerIdAfter={GetLocalPlayerId()}, " +
                $"threadId={Thread.CurrentThread.ManagedThreadId}.");
            return result;
        }

        private void SendChoresHook(Platform_Multiplayer self, byte[] choreBuffer)
        {
            if (IsCaptureActive())
            {
                try
                {
                    InspectOutgoingBuffer(choreBuffer);
                }
                catch (Exception ex)
                {
                    LogError($"Random Events outgoing Vanilla Chore inspection failed: {ex}");
                }
            }

            sendChoresTrampoline(self, choreBuffer);
        }

        private void ReceiveChoreHook(int playerId, byte[] data, int dataLength)
        {
            bool activeBefore = IsCaptureActive();
            if (activeBefore)
                SafeInspectIncoming(playerId, data, dataLength, "incoming-before");

            receiveChoreTrampoline(playerId, data, dataLength);

            // The RandomEvents batch itself arms capture while this trampoline is executing.
            if (!activeBefore && IsCaptureActive())
                SafeInspectIncoming(playerId, data, dataLength, "incoming-trigger");
        }

        private void SafeInspectIncoming(int playerId, byte[] data, int dataLength, string direction)
        {
            try
            {
                InspectIncoming(playerId, data, dataLength, direction);
            }
            catch (Exception ex)
            {
                // Diagnostics must never interfere with Vanilla Chore delivery.
                LogError($"Random Events incoming Vanilla Chore inspection failed: direction={direction}, error={ex}");
            }
        }

        private void InspectOutgoingBuffer(byte[] choreBuffer)
        {
            if (choreBuffer == null)
            {
                LogDebug($"Random Events Vanilla Chore buffer observed: contexts={GetContexts()}, direction=outgoing, buffer=null.");
                return;
            }

            int offset = 0;
            for (int recordIndex = 0; recordIndex < 10000; recordIndex++)
            {
                if (offset > choreBuffer.Length - 4)
                {
                    LogError(
                        $"Random Events malformed outgoing Chore buffer: contexts={GetContexts()}, " +
                        $"reason=missing-length, offset={offset}, bufferLength={choreBuffer.Length}.");
                    return;
                }

                int payloadLength = ReadInt32(choreBuffer, offset);
                if (payloadLength < 0)
                    return;
                if (payloadLength < 1 || payloadLength > choreBuffer.Length - offset - 5)
                {
                    LogError(
                        $"Random Events malformed outgoing Chore buffer: contexts={GetContexts()}, " +
                        $"reason=invalid-length, offset={offset}, payloadLength={payloadLength}, " +
                        $"bufferLength={choreBuffer.Length}.");
                    return;
                }

                int targetPlayerId = choreBuffer[offset + 4];
                CaptureRecord("outgoing", -1, targetPlayerId, choreBuffer, offset + 5, payloadLength);
                offset += payloadLength + 5;
            }

            LogError($"Random Events malformed outgoing Chore buffer: contexts={GetContexts()}, reason=record-limit.");
        }

        private void InspectIncoming(int senderPlayerId, byte[] data, int dataLength, string direction)
        {
            if (data == null || dataLength < 1 || dataLength > data.Length)
            {
                LogError(
                    $"Random Events malformed incoming Chore: contexts={GetContexts()}, direction={direction}, " +
                    $"sender={senderPlayerId}, dataLength={dataLength}, bufferLength={(data == null ? -1 : data.Length)}.");
                return;
            }

            CaptureRecord(direction, senderPlayerId, -1, data, 0, dataLength);
        }

        private void CaptureRecord(
            string direction,
            int senderPlayerId,
            int targetPlayerId,
            byte[] data,
            int offset,
            int length)
        {
            if (!TryReserveRecord())
                return;

            byte[] payload = new byte[length];
            Buffer.BlockCopy(data, offset, payload, 0, length);
            int opcode = payload[0];
            int scheduledTick = length >= 4 ? ReadUInt24(payload, 1) : -1;
            int commandId = length >= 8 ? ReadInt32(payload, 4) : -1;
            LogDebug(
                $"Random Events Vanilla Chore observed: contexts={GetContexts()}, direction={direction}, " +
                $"sender={senderPlayerId}, target={targetPlayerId}, localPlayerId={GetLocalPlayerId()}, " +
                $"opcode={opcode}, scheduledTick={scheduledTick}, commandId={commandId}, length={length}, " +
                $"sha256={RandomEventsDiagnostics.HashBytes(payload)}, bytes={ToHex(payload)}, " +
                $"mapTick={GetCurrentMapTick()}, threadId={Thread.CurrentThread.ManagedThreadId}.");
        }

        private bool TryReserveRecord()
        {
            lock (captureLock)
            {
                if (captureUntilTimestamp == 0 || Stopwatch.GetTimestamp() > captureUntilTimestamp)
                    return false;
                if (capturedRecordCount < MaximumCapturedRecords)
                {
                    capturedRecordCount++;
                    return true;
                }
                if (!captureLimitLogged)
                {
                    captureLimitLogged = true;
                    LogError(
                        $"Random Events Vanilla Chore capture limit reached: contexts={GetContextsLocked()}, " +
                        $"maximumRecords={MaximumCapturedRecords}.");
                }
                return false;
            }
        }

        private bool IsCaptureActive()
        {
            lock (captureLock)
                return captureUntilTimestamp != 0 && Stopwatch.GetTimestamp() <= captureUntilTimestamp;
        }

        private string GetContexts()
        {
            lock (captureLock)
                return GetContextsLocked();
        }

        private string GetContextsLocked()
        {
            return contexts.Count == 0 ? "none" : string.Join("|", contexts.ToArray());
        }

        private static MethodInfo FindMethod(
            Type type,
            string name,
            BindingFlags flags,
            params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(name, flags, null, parameterTypes, null);
            return method ?? throw new MissingMethodException(type.FullName, name);
        }

        private static int GetCurrentMapTick()
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

        private static int GetLocalPlayerId()
        {
            try
            {
                IntPtr address = new IntPtr(unchecked((long)GameGlobalsManager.Instance.LocalPlayerIdVA));
                return address == IntPtr.Zero ? -1 : Marshal.ReadInt32(address);
            }
            catch
            {
                return -1;
            }
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset] |
                   data[offset + 1] << 8 |
                   data[offset + 2] << 16 |
                   data[offset + 3] << 24;
        }

        private static int ReadUInt24(byte[] data, int offset)
        {
            return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16;
        }

        private static string ToHex(byte[] data)
        {
            var builder = new StringBuilder(data.Length * 2);
            for (int index = 0; index < data.Length; index++)
                builder.Append(data[index].ToString("X2"));
            return builder.ToString();
        }

        private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}
