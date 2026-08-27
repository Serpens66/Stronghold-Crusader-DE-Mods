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
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ChoreTestModPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "ChoreTestMod_Serp";
        private const string PluginName = "Chore Test Mod";
        private const string PluginVersion = "1.0.0";

        private const int ProtocolVersion = 1;
        private const int PlayerId = 1;
        private const int LordGlobalId = 4254;
        private const int ProbeCount = 127;
        private const int FirstProbeDelayTicks = 100;
        private const int ProbeIntervalTicks = 10;
        private const int SummaryDelayTicks = 200;

        private static readonly HashSet<int> MatchingOperationIds = new HashSet<int>();
        private static readonly HashSet<int> ReceivedOperationIds = new HashSet<int>();
        private static ManualLogSource diagnosticLog;
        private static R3PacketEventHook<ChoreTestPacket> packetHook;
        private static IDisposable packetSubscription;
        private static IDisposable mapStartSubscription;
        private static IDisposable mapUnloadSubscription;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool serializerSelfTestPassed;
        private static bool mapActive;
        private static bool firstTickCaptured;
        private static bool sendSummaryLogged;
        private static bool receiveSummaryLogged;
        private static bool transportUnavailableLogged;
        private static int mapStartTick;
        private static int nextOperationId;
        private static int sendSuccessCount;
        private static int sendFailureCount;
        private static int receiveCount;
        private static int matchCount;
        private static int corruptionCount;
        private static int duplicateCount;
        private static int decodeFailureCount;

        private void Awake()
        {
            diagnosticLog = Logger;
            LogInfo($"STARTUP: modVersion={PluginVersion}, observerOnly=true, stateMutation=false.");

            if (libraryLoadedSubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            libraryLoadedSubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (packetHook != null)
                return;

            try
            {
                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<ChoreTestPacket>();
                packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => OnMapStart());
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => OnMapUnload());
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;

                serializerSelfTestPassed = RunSerializerSelfTest();
                string extenderVersion = typeof(ChoreNetworkTransport).Assembly.GetName().Version?.ToString() ?? "<unknown>";
                LogInfo(
                    $"INITIALIZED: packetId={packetHook.GetPacketId()}, extenderAssemblyVersion={extenderVersion}, " +
                    $"transportAvailable={ChoreNetworkTransport.IsAvailable}, serializerSelfTestPassed={serializerSelfTestPassed}, " +
                    $"probeCount={ProbeCount}, firstProbeDelayTicks={FirstProbeDelayTicks}, " +
                    $"probeIntervalTicks={ProbeIntervalTicks}.");
            }
            catch (Exception exception)
            {
                serializerSelfTestPassed = false;
                LogError($"INITIALIZATION_FAILED: {exception}");
            }
        }

        private static bool RunSerializerSelfTest()
        {
            byte[] firstBody = SerializeProbe(1);
            byte[] expectedFirstBody = BuildExpectedBody(1);
            byte[] lastBody = SerializeProbe(ProbeCount);
            byte[] expectedLastBody = BuildExpectedBody(ProbeCount);

            bool passed = BytesEqual(firstBody, expectedFirstBody) &&
                BytesEqual(lastBody, expectedLastBody) &&
                firstBody.Length == 7 &&
                lastBody.Length == 7;

            string marker = passed ? "SERIALIZER_SELF_TEST_PASSED" : "SERIALIZER_SELF_TEST_FAILED";
            LogInfo(
                $"{marker}: firstBodyHex={ToHex(firstBody)}, expectedFirstBodyHex={ToHex(expectedFirstBody)}, " +
                $"lastBodyHex={ToHex(lastBody)}, expectedLastBodyHex={ToHex(expectedLastBody)}.");
            return passed;
        }

        private static void OnMapStart()
        {
            ResetSession();
            mapActive = true;
            LogInfo(
                $"MAP_START: localHost={GameNetworkAPI.IsLocalHost()}, " +
                $"transportAvailable={ChoreNetworkTransport.IsAvailable}, serializerSelfTestPassed={serializerSelfTestPassed}.");
        }

        private static void OnMapUnload()
        {
            if (mapActive)
                LogReceiveSummary("map-unload");

            mapActive = false;
            firstTickCaptured = false;
            LogInfo("MAP_UNLOAD.");
        }

        private static void OnGameTick(int currentTick)
        {
            if (!mapActive || !serializerSelfTestPassed)
                return;

            if (!firstTickCaptured)
            {
                firstTickCaptured = true;
                mapStartTick = currentTick;
                LogInfo($"TICK_BASELINE: mapStartTick={mapStartTick}.");
            }

            int relativeTick = currentTick - mapStartTick;
            if (relativeTick < 0)
                return;

            if (GameNetworkAPI.IsLocalHost())
                TrySendScheduledProbe(relativeTick);

            int finalProbeTick = FirstProbeDelayTicks + ((ProbeCount - 1) * ProbeIntervalTicks);
            if (!receiveSummaryLogged && relativeTick >= finalProbeTick + SummaryDelayTicks)
                LogReceiveSummary("scheduled-summary");
        }

        private static void TrySendScheduledProbe(int relativeTick)
        {
            if (nextOperationId > ProbeCount)
            {
                if (!sendSummaryLogged)
                    LogSendSummary();
                return;
            }

            int scheduledTick = FirstProbeDelayTicks + ((nextOperationId - 1) * ProbeIntervalTicks);
            if (relativeTick < scheduledTick)
                return;

            if (!ChoreNetworkTransport.IsAvailable || ChoreNetworkTransport.SendRawBlob == null)
            {
                if (!transportUnavailableLogged)
                {
                    transportUnavailableLogged = true;
                    LogError("TRANSPORT_UNAVAILABLE: probe series is waiting and no payload was queued.");
                }
                return;
            }

            transportUnavailableLogged = false;
            int operationId = nextOperationId++;
            byte[] body = SerializeProbe(operationId);
            byte[] expectedBody = BuildExpectedBody(operationId);
            if (!BytesEqual(body, expectedBody))
            {
                sendFailureCount++;
                LogError(
                    $"SEND_BODY_MISMATCH: operationId={operationId}, bodyHex={ToHex(body)}, " +
                    $"expectedBodyHex={ToHex(expectedBody)}.");
                return;
            }

            short packetId = packetHook.GetPacketId();
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);

            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            bool queued = sendRawBlob != null && sendRawBlob(blob);
            if (queued)
                sendSuccessCount++;
            else
                sendFailureCount++;

            LogInfo(
                $"PROBE_SEND: operationId={operationId}, packetId={packetId}, queued={queued}, " +
                $"bodyBytes={body.Length}, blobBytes={blob.Length}, bodyHex={ToHex(body)}, blobHex={ToHex(blob)}.");

            if (nextOperationId > ProbeCount)
                LogSendSummary();
        }

        private static void OnPacketReceived(ReceiveCustomPacketEventArgs<ChoreTestPacket> args)
        {
            receiveCount++;
            ChoreTestPacket packet = args?.Packet;
            byte[] receivedBody = packet?.ReceivedBody;

            bool operationInRange = packet != null && packet.OperationId >= 1 && packet.OperationId <= ProbeCount;
            byte[] expectedBody = operationInRange ? BuildExpectedBody(packet.OperationId) : null;
            bool exactMatch = packet != null &&
                !args.SenderSteamId.HasValue &&
                packet.ProtocolVersion == ProtocolVersion &&
                packet.PlayerId == PlayerId &&
                packet.LordGlobalId == LordGlobalId &&
                operationInRange &&
                BytesEqual(receivedBody, expectedBody);

            bool duplicate = operationInRange && !ReceivedOperationIds.Add(packet.OperationId);
            string status;
            if (exactMatch && !duplicate)
            {
                MatchingOperationIds.Add(packet.OperationId);
                matchCount++;
                status = "MATCH";
            }
            else
            {
                corruptionCount++;
                if (duplicate)
                    duplicateCount++;
                status = duplicate ? "DUPLICATE_OR_CORRUPT" : "CORRUPT";
            }

            string marker = exactMatch && !duplicate
                ? "PROBE_RECEIVE"
                : "CHORE_PAYLOAD_CORRUPTION_REPRODUCED";
            LogInfo(
                $"{marker}: status={status}, receiveIndex={receiveCount}, packetId={args?.PacketId}, " +
                $"senderSteamIdPresent={args?.SenderSteamId.HasValue}, " +
                $"protocolVersion={packet?.ProtocolVersion}, playerId={packet?.PlayerId}, " +
                $"operationId={packet?.OperationId}, lordGlobalId={packet?.LordGlobalId}, " +
                $"bodyBytes={receivedBody?.Length ?? -1}, bodyHex={ToHex(receivedBody)}, " +
                $"expectedBodyHex={ToHex(expectedBody)}.");
        }

        internal static void ReportDecodeFailure(byte[] receivedBody, Exception exception)
        {
            decodeFailureCount++;
            corruptionCount++;
            LogError(
                $"CHORE_PAYLOAD_CORRUPTION_REPRODUCED: status=DECODE_FAILURE, " +
                $"decodeFailureCount={decodeFailureCount}, bodyBytes={receivedBody?.Length ?? -1}, " +
                $"bodyHex={ToHex(receivedBody)}, exception={exception.GetType().FullName}: {exception.Message}");
        }

        private static byte[] SerializeProbe(int operationId)
        {
            return GameNetworkAPI.Serialize(new ChoreTestPacket
            {
                ProtocolVersion = ProtocolVersion,
                PlayerId = PlayerId,
                OperationId = operationId,
                LordGlobalId = LordGlobalId
            });
        }

        private static byte[] BuildExpectedBody(int operationId)
        {
            if (operationId < 1 || operationId > 127)
                throw new ArgumentOutOfRangeException(nameof(operationId));

            return new byte[]
            {
                0x94,
                ProtocolVersion,
                PlayerId,
                (byte)operationId,
                0xCD,
                0x10,
                0x9E
            };
        }

        private static void ResetSession()
        {
            MatchingOperationIds.Clear();
            ReceivedOperationIds.Clear();
            firstTickCaptured = false;
            sendSummaryLogged = false;
            receiveSummaryLogged = false;
            transportUnavailableLogged = false;
            mapStartTick = 0;
            nextOperationId = 1;
            sendSuccessCount = 0;
            sendFailureCount = 0;
            receiveCount = 0;
            matchCount = 0;
            corruptionCount = 0;
            duplicateCount = 0;
            decodeFailureCount = 0;
        }

        private static void LogSendSummary()
        {
            if (sendSummaryLogged)
                return;

            sendSummaryLogged = true;
            LogInfo(
                $"SEND_SUMMARY: attempted={sendSuccessCount + sendFailureCount}, " +
                $"queued={sendSuccessCount}, failed={sendFailureCount}, expected={ProbeCount}.");
        }

        private static void LogReceiveSummary(string reason)
        {
            if (receiveSummaryLogged)
                return;

            receiveSummaryLogged = true;
            int missingCount = ProbeCount - MatchingOperationIds.Count;
            string status = corruptionCount == 0 && duplicateCount == 0 &&
                decodeFailureCount == 0 && missingCount == 0
                ? "MATCH"
                : "MISMATCH";
            LogInfo(
                $"RECEIVE_SUMMARY: status={status}, reason={reason}, received={receiveCount}, " +
                $"matches={matchCount}, corruptions={corruptionCount}, duplicates={duplicateCount}, " +
                $"decodeFailures={decodeFailureCount}, missing={missingCount}, expected={ProbeCount}.");
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }

            return true;
        }

        private static string ToHex(byte[] bytes)
        {
            return bytes == null ? "<null>" : BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        private static void LogInfo(string message)
        {
            diagnosticLog?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        private static void LogError(string message)
        {
            diagnosticLog?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }
    }
}
