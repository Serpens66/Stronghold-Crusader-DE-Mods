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
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace MPTest
{
    internal sealed class ChoreTrafficCapture
    {
        private const int MaximumCapturedRecords = 256;
        private const int MaximumContexts = 8;
        private const int MaximumHexBytes = 4096;
        private const int MinimumDurationMilliseconds = 1000;
        private const int MaximumDurationMilliseconds = 60000;

        private delegate int GameActionDelegate(
            Enums.GameActionCommand command,
            int structureId,
            int state,
            int value2);

        private readonly ManualLogSource log;
        private readonly Func<bool> isEnabled;
        private readonly Func<int> getDurationMilliseconds;
        private readonly object captureLock = new object();
        private readonly List<string> contexts = new List<string>();
        private Hook gameActionHook;
        private GameActionDelegate gameActionTrampoline;
        private long captureUntilTimestamp;
        private int capturedRecordCount;
        private bool captureLimitLogged;
        private bool initialized;

        public ChoreTrafficCapture(
            ManualLogSource log,
            Func<bool> isEnabled,
            Func<int> getDurationMilliseconds)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            this.getDurationMilliseconds =
                getDurationMilliseconds ?? throw new ArgumentNullException(nameof(getDurationMilliseconds));
        }

        public void Initialize()
        {
            if (initialized)
                return;

            Hook installedHook = null;
            try
            {
                installedHook = new Hook(
                    FindGameActionMethod(),
                    (GameActionDelegate)GameActionHook);
                gameActionTrampoline = installedHook.GenerateTrampoline<GameActionDelegate>();
                // MPTest's process-lifetime runtime keeps this hook rooted.
                gameActionHook = installedHook;
                initialized = gameActionHook != null;
                LogInfo("event=traffic-capture-initialized trigger=MPTest-button enabled-by-default=false");
            }
            catch (Exception ex)
            {
                installedHook?.Dispose();
                LogError($"event=traffic-capture-disabled reason=game-action-hook-failed exception={Sanitize(ex.ToString())}");
            }
        }

        public bool Arm(string context)
        {
            if (!initialized || !GetEnabled())
                return false;

            int durationMilliseconds = GetDurationMilliseconds();
            string safeContext = Sanitize(context);
            lock (captureLock)
            {
                if (contexts.Count >= MaximumContexts)
                    contexts.RemoveAt(0);
                contexts.Add(safeContext);
                captureUntilTimestamp = Stopwatch.GetTimestamp() +
                    (long)durationMilliseconds * Stopwatch.Frequency / 1000L;
                capturedRecordCount = 0;
                captureLimitLogged = false;
            }

            LogInfo(
                $"event=traffic-capture-armed role={GetPeerRole()} context={safeContext} " +
                $"durationMs={durationMilliseconds} maximumRecords={MaximumCapturedRecords} " +
                $"currentTick={GetCurrentMapTick()} localPlayer={GetLocalPlayerId()}");
            return true;
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

        public void Observe(
            byte[] data,
            int offset,
            int length,
            string direction,
            int senderPlayerId,
            int targetPlayerId)
        {
            if (!TryReserveRecord() || data == null || offset < 0 || length < 1 || offset > data.Length - length)
                return;

            byte[] payload = new byte[length];
            Buffer.BlockCopy(data, offset, payload, 0, length);
            int opcode = payload[0];
            int scheduledTick = length >= 4 ? ReadUInt24(payload, 1) : -1;
            int commandId = length >= 8 ? ReadInt32(payload, 4) : -1;
            int displayedLength = Math.Min(length, MaximumHexBytes);
            string bytes = ToHex(payload, displayedLength);
            LogInfo(
                $"event=traffic role={GetPeerRole()} contexts={GetContexts()} direction={direction} " +
                $"sender={senderPlayerId} target={targetPlayerId} localPlayer={GetLocalPlayerId()} " +
                $"opcode={opcode} scheduledTick={scheduledTick} commandId={commandId} length={length} " +
                $"sha256={HashBytes(payload)} displayedBytes={displayedLength} " +
                $"bytesTruncated={(displayedLength != length).ToString().ToLowerInvariant()} bytes={bytes} " +
                $"currentTick={GetCurrentMapTick()} threadId={Thread.CurrentThread.ManagedThreadId}");
        }

        private int GameActionHook(
            Enums.GameActionCommand command,
            int structureId,
            int state,
            int value2)
        {
            if (!IsCaptureActive())
                return gameActionTrampoline(command, structureId, state, value2);

            int localPlayerBefore = GetLocalPlayerId();
            int tickBefore = GetCurrentMapTick();
            LogInfo(
                $"event=game-action-enter role={GetPeerRole()} contexts={GetContexts()} " +
                $"command={(int)command} structureId={structureId} state={state} value2={value2} " +
                $"currentTick={tickBefore} localPlayer={localPlayerBefore} " +
                $"threadId={Thread.CurrentThread.ManagedThreadId}");
            int result = gameActionTrampoline(command, structureId, state, value2);
            LogInfo(
                $"event=game-action-return role={GetPeerRole()} contexts={GetContexts()} " +
                $"command={(int)command} result={result} tickBefore={tickBefore} " +
                $"tickAfter={GetCurrentMapTick()} localPlayerBefore={localPlayerBefore} " +
                $"localPlayerAfter={GetLocalPlayerId()} threadId={Thread.CurrentThread.ManagedThreadId}");
            return result;
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
                        $"event=traffic-capture-limit role={GetPeerRole()} contexts={GetContextsLocked()} " +
                        $"maximumRecords={MaximumCapturedRecords}");
                }
                return false;
            }
        }

        private bool IsCaptureActive()
        {
            lock (captureLock)
                return captureUntilTimestamp != 0 && Stopwatch.GetTimestamp() <= captureUntilTimestamp;
        }

        private bool GetEnabled()
        {
            try
            {
                return isEnabled();
            }
            catch (Exception ex)
            {
                LogError($"event=traffic-capture-config-failed setting=enabled exception={Sanitize(ex.ToString())}");
                return false;
            }
        }

        private int GetDurationMilliseconds()
        {
            int requested;
            try
            {
                requested = getDurationMilliseconds();
            }
            catch (Exception ex)
            {
                LogError($"event=traffic-capture-config-failed setting=duration exception={Sanitize(ex.ToString())}");
                requested = 20000;
            }
            return Math.Max(MinimumDurationMilliseconds, Math.Min(MaximumDurationMilliseconds, requested));
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

        private static MethodInfo FindGameActionMethod()
        {
            MethodInfo method = typeof(EngineInterface).GetMethod(
                "GameAction",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                null);
            return method ?? throw new MissingMethodException(typeof(EngineInterface).FullName, "GameAction");
        }

        private static int GetCurrentMapTick()
        {
            try { return GameTimeManagerAPI.Instance.GetElapsedMapTicks(); }
            catch { return -1; }
        }

        private static int GetLocalPlayerId()
        {
            try
            {
                IntPtr address = new IntPtr(unchecked((long)GameGlobalsManager.Instance.LocalPlayerIdVA));
                return address == IntPtr.Zero ? -1 : Marshal.ReadInt32(address);
            }
            catch { return -1; }
        }

        private static string GetPeerRole()
        {
            try
            {
                if (!GameNetworkAPI.IsNetworkedEnvironment()) return "singleplayer";
                return GameNetworkAPI.IsLocalHost() ? "host" : "client";
            }
            catch { return "unknown"; }
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24;
        }

        private static int ReadUInt24(byte[] data, int offset)
        {
            return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16;
        }

        private static string HashBytes(byte[] data)
        {
            using (SHA256 sha256 = SHA256.Create())
                return ToHex(sha256.ComputeHash(data), 32);
        }

        private static string ToHex(byte[] data, int length)
        {
            var builder = new StringBuilder(length * 2);
            for (int index = 0; index < length; index++) builder.Append(data[index].ToString("X2"));
            return builder.ToString();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "none";
            return value.Replace("\r", "|").Replace("\n", "|").Replace(" ", "_");
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}
