using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ChoreTestMod
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, "Chore Test Mod", "1.0.0")]
    public sealed unsafe class ChoreTestModPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "ChoreTestMod_Serp";
        private const string ExpectedBodyHex = "94010101CD109E";
        private const int ControlProbeCount = 1;
        private const int MethodProbeCount = 3;
        private const int ProbesPerRepair = MethodProbeCount * 2;
        private const int FirstControlTick = 100;
        // Multiplayer host actions use player slot 1 on every peer.
        private const int HostPlayerId = 1;

        private static readonly List<IDisposable> Subscriptions = new List<IDisposable>();
        private static ManualLogSource log;
        private static R3PacketEventHook<ChoreTestPacket> packetHook;
        private static bool serializerReady;
        private static bool mapActive;
        private static bool localHost;
        private static bool controlSent;
        private static bool tickBaselineCaptured;
        private static int tickBaseline;
        private static readonly Dictionary<int, eStructs> PendingTowers = new Dictionary<int, eStructs>();
        private static readonly HashSet<int> PreparedTowers = new HashSet<int>();
        private static int receivedProbes;
        private static int repairCycle;
        private static int eventSentCycle;
        private static int completedReceiveCycles;
        private static int controlCorruptions;
        private static int eventCorruptions;
        private static int postfixCorruptions;
        private static int cycleDecodeFailures;
        private static int totalCorruptions;

        private void Awake()
        {
            log = Logger;
            new Harmony(PluginGuid).PatchAll(Assembly.GetExecutingAssembly());
            LogInfo("STARTUP: repairEvent=true, repairButtonPostfix=true, automaticTowerDamage=true.");
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
                Subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnBuildingSpawn));
                Subscriptions.Add(BuildingR3EventHooks.OnBuildingRepair.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnBuildingRepair));
                GameTimeManagerAPI.Instance.OnTick += OnTick;

                string bodyHex = ToHex(SerializePacket());
                serializerReady = bodyHex == ExpectedBodyHex;
                LogInfo(
                    $"SERIALIZER_SELF_TEST_{(serializerReady ? "PASSED" : "FAILED")}: " +
                    $"packetId={packetHook.GetPacketId()}, bodyHex={bodyHex}, expected={ExpectedBodyHex}, " +
                    $"transportAvailable={ChoreNetworkTransport.IsAvailable}.");
            }
            catch (Exception exception)
            {
                serializerReady = false;
                LogError($"INITIALIZATION_FAILED: {exception}");
            }
        }

        private static void OnMapStart()
        {
            mapActive = true;
            localHost = GameNetworkAPI.IsLocalHost();
            controlSent = false;
            tickBaselineCaptured = false;
            tickBaseline = 0;
            PendingTowers.Clear();
            PreparedTowers.Clear();
            receivedProbes = 0;
            repairCycle = 0;
            eventSentCycle = 0;
            completedReceiveCycles = 0;
            controlCorruptions = 0;
            eventCorruptions = 0;
            postfixCorruptions = 0;
            cycleDecodeFailures = 0;
            totalCorruptions = 0;
            LogInfo(
                $"MAP_START: localHost={localHost}, controlProbes={ControlProbeCount}, " +
                $"eventProbes={MethodProbeCount}, postfixProbes={MethodProbeCount}.");
        }

        private static void OnMapUnload()
        {
            if (mapActive)
            {
                LogInfo(
                    $"SESSION_SUMMARY: repairCyclesSent={repairCycle}, receiveCyclesCompleted={completedReceiveCycles}, " +
                    $"receivedProbes={receivedProbes}, totalCorruptions={totalCorruptions}.");
            }
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

            PrepareTowerWhenAlive();
            if (localHost && !controlSent && currentTick - tickBaseline >= FirstControlTick)
            {
                controlSent = true;
                SendSeries("CONTROL", ControlProbeCount, 0);
                LogInfo("CONTROL_COMPLETE: place a tower, then select it and click Repair once.");
            }
        }

        private static void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!mapActive || args == null ||
                args.PlayerId != HostPlayerId || args.ReturnValue <= 0 || !IsTower(args.Building))
            {
                return;
            }

            int buildingId = unchecked((int)args.ReturnValue);
            if (!PreparedTowers.Contains(buildingId))
                PendingTowers[buildingId] = args.Building;
        }

        private static void PrepareTowerWhenAlive()
        {
            if (PendingTowers.Count == 0)
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            int towerId = -1;
            eStructs towerType = eStructs.STRUCT_NULL;
            foreach (KeyValuePair<int, eStructs> pending in PendingTowers)
            {
                if (buildingApi.TryGetBuildingById(pending.Key, out GameBuilding* candidate) &&
                    candidate->r_AliveState == AliveState.IsAlive)
                {
                    towerId = pending.Key;
                    towerType = pending.Value;
                    break;
                }
            }

            if (towerId < 0 || !buildingApi.TryGetBuildingById(towerId, out GameBuilding* building))
                return;

            int oldHealth = building->r_CurrentHealth;
            int maxHealth = building->r_MaxHealth;
            if (oldHealth <= 1 || maxHealth <= 1)
            {
                LogError($"TOWER_PREPARATION_FAILED: id={towerId}, health={oldHealth}/{maxHealth}.");
                PendingTowers.Remove(towerId);
                return;
            }

            int newHealth = oldHealth;
            if (oldHealth >= maxHealth)
            {
                // The same spawn event prepares the same tower independently on both peers.
                newHealth = Math.Max(1, oldHealth - Math.Max(1, oldHealth / 20));
                buildingApi.SetCurrentHealth(towerId, (short)newHealth);
            }

            PendingTowers.Remove(towerId);
            PreparedTowers.Add(towerId);
            LogInfo(
                $"TOWER_READY: id={towerId}, type={towerType}, " +
                $"health={oldHealth}->{buildingApi.GetCurrentHealth(towerId)}/{maxHealth}, " +
                $"localHost={localHost}. This tower was damaged once.");
        }

        private static bool IsTower(eStructs structure)
        {
            return structure == eStructs.STRUCT_TOWER ||
                ((int)structure >= (int)eStructs.STRUCT_TOWER1 &&
                 (int)structure <= (int)eStructs.STRUCT_TOWER5);
        }

        private static void OnBuildingRepair(BuildingRepairEventArgs args)
        {
            if (!mapActive || !localHost || args == null || args.PlayerId != HostPlayerId)
            {
                return;
            }

            if (!serializerReady || !controlSent || eventSentCycle >= repairCycle)
            {
                LogError(
                    $"REPAIR_TRIGGER_IGNORED: serializerReady={serializerReady}, " +
                    $"controlSent={controlSent}, repairCycle={repairCycle}, eventSentCycle={eventSentCycle}.");
                return;
            }

            int cycle = ++eventSentCycle;
            LogInfo(
                $"EVENT_TRIGGER: cycle={cycle}, buildingId={args.BuildingId}, " +
                $"preparedTower={PreparedTowers.Contains(args.BuildingId)}, globalId={args.BuildingGlobalId}, " +
                $"cost={args.WoodCost}w/{args.StoneCost}s.");
            SendSeries("EVENT", MethodProbeCount, cycle);
        }

        private static void OnRepairButtonPostfix()
        {
            if (!mapActive || !localHost || !serializerReady || !controlSent || PreparedTowers.Count == 0)
                return;

            int cycle = ++repairCycle;
            LogInfo(
                $"POSTFIX_TRIGGER: cycle={cycle}, Vanilla repair call has returned; " +
                "expected stale size=10, target size=13.");
            SendSeries("POSTFIX", MethodProbeCount, cycle);
        }

        private static void SendSeries(string stage, int count, int cycle)
        {
            for (int sequence = 1; sequence <= count; sequence++)
            {
                if (!SendProbe(stage, sequence, count, cycle))
                    break;
            }
        }

        private static bool SendProbe(string stage, int sequence, int count, int cycle)
        {
            Func<byte[], bool> send = ChoreNetworkTransport.SendRawBlob;
            byte[] body = SerializePacket();
            if (send == null || ToHex(body) != ExpectedBodyHex)
            {
                serializerReady = false;
                LogError($"{stage}_SEND_FAILED: transport or serializer validation failed.");
                return false;
            }

            short packetId = packetHook.GetPacketId();
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            bool queued = send(blob);
            LogInfo(
                $"{stage}_SEND: cycle={cycle}, sequence={sequence}/{count}, queued={queued}, packetId={packetId}, " +
                $"body={ToHex(body)}, blob={ToHex(blob)}, nativeBytes=13.");
            return queued;
        }

        private static void OnPacketReceived(ReceiveCustomPacketEventArgs<ChoreTestPacket> args)
        {
            ChoreTestPacket packet = args?.Packet;
            string bodyHex = ToHex(packet?.ReceivedBody);
            string stage = RecordReceive(out int cycle, out int sequence);
            bool match = packet != null &&
                !args.SenderSteamId.HasValue &&
                packet.ProtocolVersion == 1 &&
                packet.PlayerId == 1 &&
                packet.OperationId == 1 &&
                packet.LordGlobalId == 4254 &&
                bodyHex == ExpectedBodyHex;

            if (!match)
                IncrementCorruption(stage);

            LogInfo(
                $"{(match ? "MATCH" : "CHORE_PAYLOAD_CORRUPTION_REPRODUCED")}: " +
                $"stage={stage}, cycle={cycle}, sequence={sequence}, " +
                $"values=[{packet?.ProtocolVersion},{packet?.PlayerId},{packet?.OperationId},{packet?.LordGlobalId}], " +
                $"body={bodyHex}, expected={ExpectedBodyHex}.");
            FinishCycleIfComplete(stage, cycle, sequence);
        }

        internal static void ReportDecodeFailure(byte[] body, Exception exception)
        {
            bool targetPrefix = body != null && body.Length >= 4 &&
                body[0] == 0x94 && body[1] == 0x01 && body[2] == 0x01 && body[3] == 0x01;
            if (!targetPrefix)
            {
                LogError($"UNCLASSIFIED_DECODE_FAILURE: body={ToHex(body)}, error={exception.Message}");
                return;
            }

            string stage = RecordReceive(out int cycle, out int sequence);
            cycleDecodeFailures++;
            IncrementCorruption(stage);
            LogError(
                $"CHORE_PAYLOAD_CORRUPTION_REPRODUCED: stage={stage}, cycle={cycle}, " +
                $"sequence={sequence}, body={ToHex(body)}, " +
                $"decodeError={exception.GetType().Name}: {exception.Message}");
            FinishCycleIfComplete(stage, cycle, sequence);
        }

        private static string RecordReceive(out int cycle, out int sequence)
        {
            receivedProbes++;
            if (receivedProbes <= ControlProbeCount)
            {
                cycle = 0;
                sequence = receivedProbes;
                return "CONTROL";
            }

            int cycleIndex = receivedProbes - ControlProbeCount - 1;
            cycle = (cycleIndex / ProbesPerRepair) + 1;
            int cycleOffset = cycleIndex % ProbesPerRepair;
            if (cycleOffset == 0)
            {
                eventCorruptions = 0;
                postfixCorruptions = 0;
                cycleDecodeFailures = 0;
            }

            if (cycleOffset < MethodProbeCount)
            {
                sequence = cycleOffset + 1;
                return "POSTFIX";
            }

            sequence = cycleOffset - MethodProbeCount + 1;
            return "EVENT";
        }

        private static void IncrementCorruption(string stage)
        {
            totalCorruptions++;
            if (stage == "CONTROL") controlCorruptions++;
            else if (stage == "EVENT") eventCorruptions++;
            else postfixCorruptions++;
        }

        private static void FinishCycleIfComplete(string stage, int cycle, int sequence)
        {
            if (stage != "EVENT" || sequence != MethodProbeCount)
                return;

            completedReceiveCycles = cycle;
            LogInfo(
                $"CYCLE_SUMMARY: cycle={cycle}, localHost={localHost}, " +
                $"event={MethodProbeCount - eventCorruptions}/{MethodProbeCount}, " +
                $"postfix={MethodProbeCount - postfixCorruptions}/{MethodProbeCount}, " +
                $"postfixReproduced={postfixCorruptions > 0}, " +
                $"corruptions={eventCorruptions + postfixCorruptions}, decodeFailures={cycleDecodeFailures}.");
        }

        private static byte[] SerializePacket()
        {
            return GameNetworkAPI.Serialize(new ChoreTestPacket
            {
                ProtocolVersion = 1,
                PlayerId = 1,
                OperationId = 1,
                LordGlobalId = 4254
            });
        }

        private static string ToHex(byte[] bytes) =>
            bytes == null ? "<null>" : BitConverter.ToString(bytes).Replace("-", string.Empty);

        private static void LogInfo(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private static void LogError(string message) =>
            log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        [HarmonyPatch(typeof(MainViewModel), "ButtonRepairFunction")]
        private static class RepairButtonPatch
        {
            [HarmonyPostfix]
            private static void Postfix() => OnRepairButtonPostfix();
        }

    }
}
