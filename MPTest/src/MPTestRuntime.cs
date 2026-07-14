using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MPTest
{
    internal sealed unsafe class MPTestRuntime : IDisposable
    {
        private delegate void SetUpInbuildingDelegate(MainViewModel self, int overridePanel, int overrideType);

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly HashSet<long> processedRequests = new HashSet<long>();

        private Hook setUpInbuildingHook;
        private SetUpInbuildingDelegate setUpInbuildingTrampoline;
        private R3PacketEventHook<WoodcutterSwordsmanSpawnPacket> packetHook;
        private int nextRequestId;
        private bool initialized;
        private string lastVisibilityState;

        public MPTestRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            ButtonViewModel = new WoodcutterSpawnButtonViewModel(OnSpawnCommand);
        }

        public WoodcutterSpawnButtonViewModel ButtonViewModel { get; }

        public void Initialize()
        {
            if (initialized)
                return;

            Hook installedHook = null;
            try
            {
                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<WoodcutterSwordsmanSpawnPacket>();
                subscriptions.Add(packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => ClearMapState()));

                installedHook = new Hook(FindSetUpInbuildingMethod(), (SetUpInbuildingDelegate)SetUpInbuildingHook);
                setUpInbuildingTrampoline = installedHook.GenerateTrampoline<SetUpInbuildingDelegate>();
                setUpInbuildingHook = installedHook;
                initialized = true;
                ButtonViewModel.SetVisible(false);

                LogInfo($"Runtime initialized: packetId={packetHook.GetPacketId()}, synchronization=local-first-broadcast.");
            }
            catch
            {
                installedHook?.Dispose();
                DisposeSubscriptions();
                packetHook = null;
                throw;
            }
        }

        public void Dispose()
        {
            if (!initialized)
                return;

            initialized = false;
            ClearMapState();
            DisposeSubscriptions();
            setUpInbuildingHook?.Undo();
            setUpInbuildingHook?.Dispose();
            setUpInbuildingHook = null;
            setUpInbuildingTrampoline = null;
            packetHook = null;
            LogInfo("Runtime disposed during application shutdown.");
        }

        private static MethodInfo FindSetUpInbuildingMethod()
        {
            MethodInfo method = typeof(MainViewModel).GetMethod(
                "setUpInbuilding",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(int) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, "setUpInbuilding");

            return method;
        }

        private void SetUpInbuildingHook(MainViewModel self, int overridePanel, int overrideType)
        {
            setUpInbuildingTrampoline(self, overridePanel, overrideType);
            RefreshButtonVisibility();
        }

        private void RefreshButtonVisibility()
        {
            try
            {
                if (!TryGetLocalHumanPlayerId(out int localPlayerId))
                {
                    SetButtonVisibility(false, "hidden: local human player id unavailable");
                    return;
                }

                int selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (!TryGetOwnedWoodcutter(selectedBuildingId, localPlayerId, out _, out string failureReason))
                {
                    SetButtonVisibility(
                        false,
                        $"hidden: playerId={localPlayerId}, selectedBuildingId={selectedBuildingId}, reason={failureReason}");
                    return;
                }

                SetButtonVisibility(
                    true,
                    $"visible: playerId={localPlayerId}, selectedBuildingId={selectedBuildingId}");
            }
            catch (Exception ex)
            {
                ButtonViewModel.SetVisible(false);
                Shared.DebugLogHelper.LogError(log, $"MPTest button visibility refresh failed: {ex}");
            }
        }

        private void SetButtonVisibility(bool visible, string logState)
        {
            ButtonViewModel.SetVisible(visible);
            LogVisibility(logState);
        }

        private void OnSpawnCommand()
        {
            int sourcePlayerId = 0;
            int selectedBuildingId = 0;

            try
            {
                if (!TryGetLocalHumanPlayerId(out sourcePlayerId))
                {
                    LogInfo("Spawn click rejected: local human player id unavailable.");
                    return;
                }

                selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (!TryGetOwnedWoodcutter(
                    selectedBuildingId,
                    sourcePlayerId,
                    out GameBuilding* woodcutter,
                    out string failureReason))
                {
                    LogInfo($"Spawn click rejected: selectedBuildingId={selectedBuildingId}, reason={failureReason}.");
                    RefreshButtonVisibility();
                    return;
                }

                if (!TryFindAdjacentSpawnTile(woodcutter, out int targetTileX, out int targetTileY))
                {
                    LogInfo($"Spawn click rejected: no valid directly adjacent tile exists for woodcutterId={selectedBuildingId}, globalId={woodcutter->r_GlobalId}.");
                    return;
                }

                int requestId = NextRequestId();
                bool networked = GameNetworkAPI.IsNetworkedEnvironment();

                if (networked && packetHook == null)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        "MPTest rejected a multiplayer spawn because the packet hook is unavailable.");
                    return;
                }

                WoodcutterSwordsmanSpawnPacket packet = new WoodcutterSwordsmanSpawnPacket
                {
                    SourcePlayerId = sourcePlayerId,
                    RequestId = requestId,
                    WoodcutterGlobalId = (int)woodcutter->r_GlobalId,
                    TargetTileX = targetTileX,
                    TargetTileY = targetTileY
                };

                // Detect serialization problems before changing the local game state.
                if (networked)
                    GameNetworkAPI.Serialize(packet);

                if (!TryApplySpawn(packet, networked ? "local-multiplayer-click" : "singleplayer-click"))
                    return;

                MarkRequestProcessed(sourcePlayerId, requestId);
                if (networked)
                {
                    GameNetworkAPI.SendPacketToAll(packet, packetHook.GetPacketId(), true);
                    LogInfo($"Spawn packet broadcast after local spawn: playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, woodcutterGlobalId={packet.WoodcutterGlobalId}, target={packet.TargetTileX},{packet.TargetTileY}.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MPTest spawn click failed: selectedBuildingId={selectedBuildingId}, playerId={sourcePlayerId}: {ex}");
            }
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<WoodcutterSwordsmanSpawnPacket> args)
        {
            try
            {
                WoodcutterSwordsmanSpawnPacket packet = args?.Packet;
                if (packet == null)
                {
                    LogInfo("Network packet rejected: payload is null.");
                    return;
                }

                string packetFailure = GetPacketValidationFailure(packet);
                if (packetFailure != null)
                {
                    LogInfo($"Network packet rejected: playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, reason={packetFailure}.");
                    return;
                }

                if (!GameNetworkAPI.IsNetworkedEnvironment())
                {
                    LogInfo($"Network packet ignored outside multiplayer: playerId={packet.SourcePlayerId}, requestId={packet.RequestId}.");
                    return;
                }

                if (IsRequestProcessed(packet.SourcePlayerId, packet.RequestId))
                {
                    LogInfo($"Duplicate spawn packet ignored: playerId={packet.SourcePlayerId}, requestId={packet.RequestId}.");
                    return;
                }

                if (!TryApplySpawn(packet, "remote-multiplayer-packet"))
                    return;

                MarkRequestProcessed(packet.SourcePlayerId, packet.RequestId);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"MPTest network packet handling failed: {ex}");
            }
        }

        private bool TryApplySpawn(WoodcutterSwordsmanSpawnPacket packet, string reason)
        {
            if (!TryFindOwnedWoodcutter(
                packet.WoodcutterGlobalId,
                packet.SourcePlayerId,
                out int woodcutterId,
                out GameBuilding* woodcutter,
                out string failureReason))
            {
                LogInfo($"Spawn rejected: source={reason}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, woodcutterGlobalId={packet.WoodcutterGlobalId}, reason={failureReason}.");
                return false;
            }

            if (!IsDirectlyAdjacentToBuilding(woodcutter, packet.TargetTileX, packet.TargetTileY))
            {
                LogInfo($"Spawn rejected: source={reason}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, target={packet.TargetTileX},{packet.TargetTileY}, reason=target-not-directly-adjacent.");
                return false;
            }

            if (!TryValidateSpawnTile(packet.TargetTileX, packet.TargetTileY, out int height, out string tileFailure))
            {
                LogInfo($"Spawn rejected: source={reason}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, target={packet.TargetTileX},{packet.TargetTileY}, reason={tileFailure}.");
                return false;
            }

            long createdId = GameUnitManagerAPI.Instance.CreateUnitLocal(
                packet.SourcePlayerId,
                packet.SourcePlayerId,
                packet.TargetTileX,
                packet.TargetTileY,
                height,
                eChimps.CHIMP_TYPE_SWORDSMAN);

            if (createdId <= 0 || createdId > int.MaxValue || !GameUnitManagerAPI.Instance.IsValid((int)createdId))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MPTest swordsman spawn returned an invalid unit id: source={reason}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, createdId={createdId}.");
                return false;
            }

            UnitSpawnDiagnostics.Log(log, (int)createdId, reason, packet, height);
            LogInfo($"Swordsman spawned: source={reason}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, unitId={createdId}, woodcutterId={woodcutterId}, woodcutterGlobalId={packet.WoodcutterGlobalId}, tile={packet.TargetTileX},{packet.TargetTileY}, height={height}.");
            return true;
        }

        private static bool TryFindOwnedWoodcutter(
            int globalId,
            int ownerPlayerId,
            out int buildingId,
            out GameBuilding* building,
            out string failureReason)
        {
            buildingId = FindOwnedWoodcutterIdByGlobalId(globalId, ownerPlayerId);
            if (buildingId <= 0)
            {
                building = null;
                failureReason = "not-found";
                return false;
            }

            return TryGetOwnedWoodcutter(buildingId, ownerPlayerId, out building, out failureReason);
        }

        private static string GetPacketValidationFailure(WoodcutterSwordsmanSpawnPacket packet)
        {
            if (packet.SourcePlayerId <= 0)
                return "source-player-id-not-positive";
            if (packet.RequestId <= 0)
                return "request-id-not-positive";
            if (packet.WoodcutterGlobalId <= 0)
                return "woodcutter-global-id-not-positive";
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(packet.SourcePlayerId))
                return "source-player-id-invalid";
            if (GamePlayerManagerAPI.Instance.IsAIPlayer(packet.SourcePlayerId))
                return "source-player-is-ai";
            if (GameNetworkAPI.IsNetworkedEnvironment() && GameNetworkAPI.GetPlayerById(packet.SourcePlayerId) == null)
                return "source-player-is-not-a-network-member";
            if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(packet.TargetTileX, packet.TargetTileY))
                return "target-outside-map";
            return null;
        }

        private static bool TryGetOwnedWoodcutter(
            int buildingId,
            int ownerPlayerId,
            out GameBuilding* building,
            out string failureReason)
        {
            building = null;
            failureReason = null;

            if (buildingId <= 0)
            {
                failureReason = "building-id-not-positive";
                return false;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out building))
            {
                failureReason = "building-id-not-resolvable";
                return false;
            }

            if (building->r_AliveState != AliveState.IsAlive)
            {
                failureReason = $"building-not-alive(state={building->r_AliveState})";
                return false;
            }

            if (building->r_BuildingType != eStructs.STRUCT_WOODCUTTERS_HUT)
            {
                failureReason = $"building-is-not-woodcutter(type={building->r_BuildingType})";
                return false;
            }

            if (building->r_PlayerIdOwner != ownerPlayerId)
            {
                failureReason = $"building-owner-mismatch(actual={building->r_PlayerIdOwner},expected={ownerPlayerId})";
                return false;
            }

            if (building->r_GlobalId == 0 || building->r_GlobalId > int.MaxValue)
            {
                failureReason = $"building-global-id-unsupported({building->r_GlobalId})";
                return false;
            }

            return true;
        }

        private static int FindOwnedWoodcutterIdByGlobalId(int globalId, int ownerPlayerId)
        {
            if (globalId <= 0 || ownerPlayerId <= 0)
                return 0;

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_AliveState == AliveState.IsAlive &&
                    building.r_BuildingType == eStructs.STRUCT_WOODCUTTERS_HUT &&
                    building.r_PlayerIdOwner == ownerPlayerId &&
                    (int)building.r_GlobalId == globalId)
                {
                    return index + 1;
                }
            }

            return 0;
        }

        private static bool TryFindAdjacentSpawnTile(
            GameBuilding* building,
            out int targetTileX,
            out int targetTileY)
        {
            targetTileX = 0;
            targetTileY = 0;

            GetBuildingBounds(building, out int minX, out int minY, out int maxX, out int maxY);

            int topY = minY - 1;
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                if (TryUseSpawnCandidate(x, topY, out targetTileX, out targetTileY))
                    return true;
            }

            int rightX = maxX + 1;
            for (int y = minY; y <= maxY + 1; y++)
            {
                if (TryUseSpawnCandidate(rightX, y, out targetTileX, out targetTileY))
                    return true;
            }

            int bottomY = maxY + 1;
            for (int x = maxX; x >= minX - 1; x--)
            {
                if (TryUseSpawnCandidate(x, bottomY, out targetTileX, out targetTileY))
                    return true;
            }

            int leftX = minX - 1;
            for (int y = maxY; y >= minY; y--)
            {
                if (TryUseSpawnCandidate(leftX, y, out targetTileX, out targetTileY))
                    return true;
            }

            return false;
        }

        private static bool TryUseSpawnCandidate(
            int x,
            int y,
            out int targetTileX,
            out int targetTileY)
        {
            targetTileX = 0;
            targetTileY = 0;
            if (!TryValidateSpawnTile(x, y, out _, out _))
                return false;

            targetTileX = x;
            targetTileY = y;
            return true;
        }

        private static bool TryValidateSpawnTile(int x, int y, out int height, out string failureReason)
        {
            height = 0;
            failureReason = null;

            if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y))
            {
                failureReason = "target-outside-map";
                return false;
            }

            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!GameTileManagerAPI.Instance.IsValidTileId(tileId))
            {
                failureReason = $"invalid-tile-id({tileId})";
                return false;
            }

            if (!GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(tileId))
            {
                failureReason = $"tile-not-walkable-or-building-occupied({tileId})";
                return false;
            }

            height = GameTileManagerAPI.Instance.GetTileHeight(tileId);
            return true;
        }

        private static bool IsDirectlyAdjacentToBuilding(GameBuilding* building, int x, int y)
        {
            if (building == null)
                return false;

            GetBuildingBounds(building, out int minX, out int minY, out int maxX, out int maxY);
            bool insideExpandedBounds =
                x >= minX - 1 && x <= maxX + 1 &&
                y >= minY - 1 && y <= maxY + 1;
            bool insideBuildingBounds =
                x >= minX && x <= maxX &&
                y >= minY && y <= maxY;
            return insideExpandedBounds && !insideBuildingBounds;
        }

        private static void GetBuildingBounds(
            GameBuilding* building,
            out int minX,
            out int minY,
            out int maxX,
            out int maxY)
        {
            minX = Math.Min(building->r_TilePositionXBegin, building->r_TilePositionXEnd);
            minY = Math.Min(building->r_TilePositionYBegin, building->r_TilePositionYEnd);
            maxX = Math.Max(building->r_TilePositionXBegin, building->r_TilePositionXEnd);
            maxY = Math.Max(building->r_TilePositionYBegin, building->r_TilePositionYEnd);
        }

        private static bool TryGetLocalHumanPlayerId(out int playerId)
        {
            playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (playerId <= 0 && !GameNetworkAPI.IsNetworkedEnvironment())
                playerId = 1;

            return playerId > 0 &&
                GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
        }

        private bool IsRequestProcessed(int sourcePlayerId, int requestId)
        {
            return processedRequests.Contains(GetRequestKey(sourcePlayerId, requestId));
        }

        private void MarkRequestProcessed(int sourcePlayerId, int requestId)
        {
            processedRequests.Add(GetRequestKey(sourcePlayerId, requestId));
        }

        private static long GetRequestKey(int sourcePlayerId, int requestId)
        {
            return ((long)sourcePlayerId << 32) | (uint)requestId;
        }

        private int NextRequestId()
        {
            if (nextRequestId == int.MaxValue)
                nextRequestId = 0;
            return ++nextRequestId;
        }

        private void ClearMapState()
        {
            processedRequests.Clear();
            nextRequestId = 0;
            lastVisibilityState = null;
            ButtonViewModel.SetVisible(false);
            LogInfo("Map state cleared.");
        }

        private void LogVisibility(string state)
        {
            if (string.Equals(lastVisibilityState, state, StringComparison.Ordinal))
                return;

            lastVisibilityState = state;
            Shared.DebugLogHelper.LogDebug(log, $"MPTest button visibility: {state}.");
        }

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, $"MPTest: {message}");
        }

        private void DisposeSubscriptions()
        {
            for (int index = 0; index < subscriptions.Count; index++)
                subscriptions[index]?.Dispose();
            subscriptions.Clear();
        }
    }
}
