using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ChoreTestMod
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ChoreTestMod_Serp", "Chore Test Mod", "1.0.0")]
    public sealed class ChoreTestModPlugin : BaseUnityPlugin
    {
        private const int AttemptCount = 12;
        private const int FirstAttemptTick = 100;
        private const int AttemptIntervalTicks = 5 * 40;
        private const int TargetDelayTicks = 1;
        private const int SummaryDelayTicks = 200;

        private const string FirstPreconditionBodyHex = "9401010001";
        private const string ExpectedTargetBodyHex = "94010101CD109E";

        private static readonly List<IDisposable> Subscriptions = new List<IDisposable>();
        private static ManualLogSource log;
        private static R3PacketEventHook<ChoreTestPacket> packetHook;
        private static bool serializerReady;
        private static bool mapActive;
        private static bool localHostAtMapStart;
        private static bool tickBaselineCaptured;
        private static bool summaryLogged;
        private static int tickBaseline;
        private static int nextAttempt = 1;
        private static int pendingTargetAttempt;
        private static int pendingTargetTick;
        private static int preconditionSends;
        private static int targetSends;
        private static int preconditionReceives;
        private static int targetReceives;
        private static int targetMatches;
        private static int targetCorruptions;

        private void Awake()
        {
            log = Logger;
            LogInfo("STARTUP: observerOnly=true, stateMutation=false.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (packetHook != null)
                return;

            try
            {
                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<ChoreTestPacket>();
                Subscriptions.Add(packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived));
                Subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => OnMapStart()));
                Subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => OnMapUnload()));
                GameTimeManagerAPI.Instance.OnTick += OnTick;

                string preconditionHex = ToHex(SerializePacket(0, 1));
                string targetHex = ToHex(SerializePacket(1, 4254));
                serializerReady = preconditionHex == FirstPreconditionBodyHex &&
                    targetHex == ExpectedTargetBodyHex;
                LogInfo(
                    $"SERIALIZER_SELF_TEST_{(serializerReady ? "PASSED" : "FAILED")}: " +
                    $"packetId={packetHook.GetPacketId()}, preconditionBodyHex={preconditionHex}, " +
                    $"expectedPreconditionBodyHex={FirstPreconditionBodyHex}, targetBodyHex={targetHex}, " +
                    $"expectedTargetBodyHex={ExpectedTargetBodyHex}, " +
                    $"transportAvailable={ChoreNetworkTransport.IsAvailable}.");
            }
            catch (Exception exception)
            {
                LogError($"INITIALIZATION_FAILED: {exception}");
            }
        }

        private static void OnMapStart()
        {
            mapActive = true;
            localHostAtMapStart = GameNetworkAPI.IsLocalHost();
            tickBaselineCaptured = false;
            summaryLogged = false;
            nextAttempt = 1;
            pendingTargetAttempt = 0;
            pendingTargetTick = 0;
            preconditionSends = 0;
            targetSends = 0;
            preconditionReceives = 0;
            targetReceives = 0;
            targetMatches = 0;
            targetCorruptions = 0;
            LogInfo(
                $"MAP_START: localHost={localHostAtMapStart}, serializerReady={serializerReady}, " +
                $"transportAvailable={ChoreNetworkTransport.IsAvailable}, attempts={AttemptCount}, " +
                $"intervalSeconds=5.");
        }

        private static void OnMapUnload()
        {
            if (mapActive && !summaryLogged)
                LogSummary("map-unload");

            mapActive = false;
        }

        private static void OnTick(int currentTick)
        {
            if (!mapActive || !serializerReady)
                return;

            if (!tickBaselineCaptured)
            {
                tickBaselineCaptured = true;
                tickBaseline = currentTick;
            }

            int relativeTick = currentTick - tickBaseline;
            if (nextAttempt <= AttemptCount &&
                relativeTick >= FirstAttemptTick + ((nextAttempt - 1) * AttemptIntervalTicks))
            {
                int attempt = nextAttempt++;
                string expectedBodyHex = $"94010100{attempt:X2}";
                // A local seven-byte blob leaves the shared native packed-size field at 11.
                if (!SendPacket("PRECONDITION", attempt, 0, attempt, expectedBodyHex))
                {
                    mapActive = false;
                    return;
                }

                preconditionSends++;
                if (localHostAtMapStart)
                {
                    pendingTargetAttempt = attempt;
                    pendingTargetTick = relativeTick + TargetDelayTicks;
                }
            }

            if (localHostAtMapStart && pendingTargetAttempt > 0 && relativeTick >= pendingTargetTick)
            {
                int attempt = pendingTargetAttempt;
                pendingTargetAttempt = 0;
                if (!SendPacket("TARGET", attempt, 1, 4254, ExpectedTargetBodyHex))
                {
                    mapActive = false;
                    return;
                }

                targetSends++;
            }

            int finalTargetTick = FirstAttemptTick +
                ((AttemptCount - 1) * AttemptIntervalTicks) + TargetDelayTicks;
            if (!summaryLogged && relativeTick >= finalTargetTick + SummaryDelayTicks)
                LogSummary("test-complete");
        }

        private static bool SendPacket(
            string kind,
            int attempt,
            int operationId,
            int lordGlobalId,
            string expectedBodyHex)
        {
            Func<byte[], bool> send = ChoreNetworkTransport.SendRawBlob;
            if (send == null)
            {
                mapActive = false;
                LogError($"{kind}_SEND_FAILED: transport unavailable; test aborted.");
                return false;
            }

            byte[] body = SerializePacket(operationId, lordGlobalId);
            string bodyHex = ToHex(body);
            if (bodyHex != expectedBodyHex)
            {
                serializerReady = false;
                LogError(
                    $"{kind}_SEND_FAILED: serialized body changed; bodyHex={bodyHex}, " +
                    $"expectedBodyHex={expectedBodyHex}.");
                return false;
            }

            short packetId = packetHook.GetPacketId();
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            bool queued = send(blob);

            LogInfo(
                $"{kind}_SEND: attempt={attempt}/{AttemptCount}, queued={queued}, " +
                $"localHostAtMapStart={localHostAtMapStart}, " +
                $"packetId={packetId}, bodyBytes={body.Length}, blobBytes={blob.Length}, " +
                $"nativePackedBytes={sizeof(int) + blob.Length}, bodyHex={bodyHex}, blobHex={ToHex(blob)}.");
            return queued;
        }

        private static void OnPacketReceived(ReceiveCustomPacketEventArgs<ChoreTestPacket> args)
        {
            ChoreTestPacket packet = args?.Packet;
            string bodyHex = ToHex(packet?.ReceivedBody);
            bool isTarget = packet != null &&
                packet.ProtocolVersion == 1 &&
                packet.PlayerId == 1 &&
                packet.OperationId == 1;

            if (!isTarget)
            {
                preconditionReceives++;
                LogInfo(
                    $"PRECONDITION_RECEIVE: encodedAttempt={packet?.LordGlobalId}, " +
                    $"senderSteamIdPresent={args?.SenderSteamId.HasValue}, " +
                    $"values=[{packet?.ProtocolVersion},{packet?.PlayerId},{packet?.OperationId},{packet?.LordGlobalId}], " +
                    $"bodyHex={bodyHex}.");
                return;
            }

            targetReceives++;
            bool match = !args.SenderSteamId.HasValue &&
                packet.LordGlobalId == 4254 &&
                bodyHex == ExpectedTargetBodyHex;
            if (match)
                targetMatches++;
            else
                targetCorruptions++;

            string marker = match ? "TARGET_RECEIVE_MATCH" : "CHORE_PAYLOAD_CORRUPTION_REPRODUCED";
            LogInfo(
                $"{marker}: receiveAttempt={targetReceives}/{AttemptCount}, " +
                $"senderSteamIdPresent={args.SenderSteamId.HasValue}, " +
                $"values=[{packet.ProtocolVersion},{packet.PlayerId},{packet.OperationId},{packet.LordGlobalId}], " +
                $"bodyHex={bodyHex}, expectedBodyHex={ExpectedTargetBodyHex}.");
        }

        internal static void ReportDecodeFailure(byte[] body, Exception exception)
        {
            string bodyHex = ToHex(body);
            bool targetPrefix = body != null && body.Length >= 4 &&
                body[0] == 0x94 && body[1] == 0x01 && body[2] == 0x01 && body[3] == 0x01;
            if (targetPrefix)
            {
                targetReceives++;
                targetCorruptions++;
                LogError(
                    $"CHORE_PAYLOAD_CORRUPTION_REPRODUCED: target decode failed; bodyHex={bodyHex}, " +
                    $"decodeError={exception.GetType().Name}: {exception.Message}");
                return;
            }

            LogError(
                $"PRECONDITION_DECODE_FAILURE: bodyHex={bodyHex}, " +
                $"decodeError={exception.GetType().Name}: {exception.Message}");
        }

        private static byte[] SerializePacket(int operationId, int lordGlobalId)
        {
            return GameNetworkAPI.Serialize(new ChoreTestPacket
            {
                ProtocolVersion = 1,
                PlayerId = 1,
                OperationId = operationId,
                LordGlobalId = lordGlobalId
            });
        }

        private static void LogSummary(string reason)
        {
            summaryLogged = true;
            bool reproduced = !localHostAtMapStart && targetCorruptions > 0;
            int missingTargets = Math.Max(0, AttemptCount - targetReceives);
            bool complete = missingTargets == 0;
            LogInfo(
                $"SUMMARY: reason={reason}, localHostAtMapStart={localHostAtMapStart}, " +
                $"reproduced={reproduced}, complete={complete}, attempts={AttemptCount}, " +
                $"preconditionSends={preconditionSends}, targetSends={targetSends}, " +
                $"preconditionReceives={preconditionReceives}, targetReceives={targetReceives}, " +
                $"targetMatches={targetMatches}, targetCorruptions={targetCorruptions}, " +
                $"missingTargets={missingTargets}.");
        }

        private static string ToHex(byte[] bytes) =>
            bytes == null ? "<null>" : BitConverter.ToString(bytes).Replace("-", string.Empty);

        private static void LogInfo(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private static void LogError(string message) =>
            log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
