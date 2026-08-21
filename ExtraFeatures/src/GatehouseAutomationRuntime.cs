// Feature: Reachability-aware and per-building manual gatehouse automation.
using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ExtraFeatures
{
    internal sealed class GatehouseAutomationButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private ImageSource iconImageSource;
        private Visibility buttonVisibility = Visibility.Hidden;
        private Visibility manualIndicatorVisibility = Visibility.Hidden;
        private double iconOpacity = 1.0;
        private string toolTipText = string.Empty;

        public GatehouseAutomationButtonViewModel(Action toggle)
        {
            ToggleCommand = new RelayCommand(toggle ?? throw new ArgumentNullException(nameof(toggle)));
        }

        public RelayCommand ToggleCommand { get; }
        public ImageSource IconImageSource { get => iconImageSource; private set => Set(ref iconImageSource, value, nameof(IconImageSource)); }
        public Visibility ButtonVisibility { get => buttonVisibility; private set => Set(ref buttonVisibility, value, nameof(ButtonVisibility)); }
        public Visibility ManualIndicatorVisibility { get => manualIndicatorVisibility; private set => Set(ref manualIndicatorVisibility, value, nameof(ManualIndicatorVisibility)); }
        public double IconOpacity { get => iconOpacity; private set => Set(ref iconOpacity, value, nameof(IconOpacity)); }
        public string ToolTipText { get => toolTipText; private set => Set(ref toolTipText, value, nameof(ToolTipText)); }

        public void SetIcon(ImageSource icon)
        {
            IconImageSource = icon;
        }

        public void Show(bool automaticEnabled)
        {
            ButtonVisibility = Visibility.Visible;
            ManualIndicatorVisibility = automaticEnabled ? Visibility.Hidden : Visibility.Visible;
            IconOpacity = automaticEnabled ? 1.0 : 0.48;
            ToolTipText = SerpLocalization.Get(automaticEnabled
                ? "SomeSettings.GatehouseAutomaticEnabledTooltip"
                : "SomeSettings.GatehouseManualOnlyTooltip");
        }

        public void Hide()
        {
            ButtonVisibility = Visibility.Hidden;
        }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            OnPropertyChanged(propertyName);
        }
    }

    internal sealed unsafe class GatehouseAutomationRuntime : IDisposable
    {
        private const string SaveDataIdentifier = "serp-extrafeatures-gatehouse-automation-v1";
        private const string AutomationIconAssetPath = "Assets/GUI/Sprites/ExtraFeatures_GatehouseAutomation.png";
        private const int ChoreProtocolVersion = 1;
        private const int MaximumSavedGatehouses = 10000;
        private const int MaximumFailureLogs = 20;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly GatehouseAutomationButtonViewModel buttonViewModel;
        private readonly HashSet<int> manualOnlyGateGlobalIds = new HashSet<int>();
        private readonly Dictionary<ReachabilityKey, bool> reachabilityCache = new Dictionary<ReachabilityKey, bool>();
        private IDisposable gatehouseQuerySubscription;
        private R3PacketEventHook<GatehouseAutomationPacket> packetHook;
        private IDisposable packetSubscription;
        private GatehouseTimingPatch timingPatch;
        private bool initialized;
        private bool networkInitialized;
        private bool saveHandlerRegistered;
        private bool reachabilityAvailable;
        private bool mapActive;
        private bool disposed;
        private bool firstQueryLogged;
        private bool iconLoadAttempted;
        private int lastCacheTick = int.MinValue;
        private int failureLogs;
        private int nextOperationId;
        private int lastUiFrame = -1;
        private long nativeQueries;
        private long cacheHits;
        private long reachableResults;
        private long unreachableResults;

        public GatehouseAutomationRuntime(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
            buttonViewModel = new GatehouseAutomationButtonViewModel(ToggleSelectedGatehouse);
        }

        public object ButtonViewModel => buttonViewModel;

        public void Initialize()
        {
            if (initialized)
                return;

            gatehouseQuerySubscription = BuildingR3EventHooks.OnGatehouseQuery.Observable.Subscribe(OnGatehouseQuery);
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                    SaveDataIdentifier,
                    SaveState,
                    LoadState,
                    ResetMapState))
            {
                gatehouseQuerySubscription.Dispose();
                gatehouseQuerySubscription = null;
                throw new InvalidOperationException("Gatehouse automation save-data registration failed.");
            }

            saveHandlerRegistered = true;
            UnityEngine.Application.onBeforeRender += OnBeforeRender;
            initialized = true;
            LogInfo("gatehouse automation initialized; per-gate state uses global building IDs and save schema v1.");
        }

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<GatehouseAutomationPacket>();
            packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
            networkInitialized = true;
            LogInfo($"gatehouse Chore packet registered eagerly: packetId={packetHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
        }

        public void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            reachabilityAvailable = referenceHashMatches;
            if (!referenceHashMatches)
            {
                LogWarning("gatehouse PCL reachability filtering is unavailable because the installed DLL differs from the audited build; Vanilla candidate handling remains active.");
            }

            try
            {
                timingPatch = new GatehouseTimingPatch(log, libraryHandle, memory, referenceHashMatches);
                ApplySettings();
            }
            catch (Exception ex)
            {
                try
                {
                    timingPatch?.Dispose();
                }
                catch (Exception restoreException)
                {
                    LogError($"gatehouse Vanilla-value restoration also failed: {restoreException}");
                }
                timingPatch = null;
                LogError($"gatehouse distance/delay customization is disabled for this process: {ex}");
            }
        }

        public void ApplySettings()
        {
            timingPatch?.Apply(
                settings.HumanGateReopenDelaySeconds,
                settings.AIGateReopenDelaySeconds,
                settings.HumanGateClosingDistanceTiles,
                settings.AIGateClosingDistanceTiles,
                settings.EnableMod);

            if (settings.EnableMod)
                ReconcileManualGateTimers(removeMissing: false);
            else
                ReleaseManualGateTimers();

            RefreshButtonVisibility();
        }

        public void BeginMap()
        {
            mapActive = true;
            ClearReachabilityCache();
            ReconcileManualGateTimers(removeMissing: true);
            RefreshButtonVisibility();
            LogInfo($"gatehouse map state started: manualOnly={manualOnlyGateGlobalIds.Count}, reachabilityAvailable={reachabilityAvailable}.");
        }

        public void EndMap()
        {
            ResetMapState();
        }

        public void RefreshButtonVisibility()
        {
            if (!settings.EnableMod || !mapActive ||
                (multiplayerFeatureGate.BlocksLocalStateChanges && !IsChoreTransportReady()))
            {
                buttonViewModel.Hide();
                return;
            }

            int localPlayerId = GetControlledPlayerId();
            int selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
            if (!TryGetOwnedGatehouse(selectedBuildingId, localPlayerId, out GameBuilding* building, out _))
            {
                buttonViewModel.Hide();
                return;
            }

            buttonViewModel.Show(!manualOnlyGateGlobalIds.Contains((int)building->r_GlobalId));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            UnityEngine.Application.onBeforeRender -= OnBeforeRender;
            gatehouseQuerySubscription?.Dispose();
            gatehouseQuerySubscription = null;
            packetSubscription?.Dispose();
            packetSubscription = null;
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
            try
            {
                timingPatch?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"gatehouse timing patch disposal failed: {ex}");
            }
            finally
            {
                timingPatch = null;
                ReleaseManualGateTimers();
                ResetMapState();
            }
        }

        private void OnBeforeRender()
        {
            if (lastUiFrame == Time.frameCount)
                return;
            lastUiFrame = Time.frameCount;

            try
            {
                TryLoadButtonIcon();
                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogFailure($"gatehouse button refresh failed: {ex}");
            }
        }

        private void TryLoadButtonIcon()
        {
            if (iconLoadAttempted || MainViewModel.Instance == null)
                return;

            // The plugin initializes before the game HUD; defer the one-time decode until rendering begins.
            iconLoadAttempted = true;
            if (!GameAssetManagerAPI.Instance.GetFileBinaryContent(AutomationIconAssetPath, out byte[] imageBytes) ||
                imageBytes == null || imageBytes.Length == 0)
            {
                LogError($"gatehouse automation icon could not be loaded from '{AutomationIconAssetPath}'.");
                return;
            }

            TextureSource icon = MainViewModel.Instance.LoadImageFile(imageBytes);
            if (icon == null)
            {
                LogError($"gatehouse automation icon decoding failed for '{AutomationIconAssetPath}'.");
                return;
            }

            buttonViewModel.SetIcon(icon);
            LogInfo($"gatehouse automation icon loaded: asset='{AutomationIconAssetPath}', bytes={imageBytes.Length}.");
        }

        private void ToggleSelectedGatehouse()
        {
            try
            {
                if (!settings.EnableMod || !mapActive)
                    return;

                int playerId = GetControlledPlayerId();
                int buildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (!TryGetOwnedGatehouse(buildingId, playerId, out GameBuilding* building, out string failure))
                {
                    LogError($"gatehouse automation toggle rejected: buildingId={buildingId}, playerId={playerId}, reason={failure}.");
                    return;
                }

                int globalId = (int)building->r_GlobalId;
                bool automaticEnabled = manualOnlyGateGlobalIds.Contains(globalId);
                if (multiplayerFeatureGate.BlocksLocalStateChanges)
                {
                    if (!TrySendChore(playerId, globalId, automaticEnabled))
                        return;
                }
                else
                {
                    ApplyManualState(playerId, globalId, automaticEnabled, "local");
                }
            }
            catch (Exception ex)
            {
                LogError($"gatehouse automation toggle failed: {ex}");
            }
        }

        private bool TrySendChore(int playerId, int globalId, bool automaticEnabled)
        {
            if (!IsChoreTransportReady())
            {
                LogError("gatehouse automation toggle refused in multiplayer because the Chore transport is unavailable.");
                return false;
            }

            int operationId = NextOperationId();
            var packet = new GatehouseAutomationPacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                PlayerId = playerId,
                OperationId = operationId,
                BuildingGlobalId = globalId,
                AutomaticEnabled = automaticEnabled
            };
            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            if (sendRawBlob == null || !sendRawBlob(blob))
            {
                LogError($"gatehouse Chore was not queued; no local change was applied: operationId={operationId}, globalId={globalId}.");
                return false;
            }

            LogInfo($"gatehouse Chore queued: operationId={operationId}, globalId={globalId}, automaticEnabled={automaticEnabled}, payloadBytes={blob.Length}.");
            return true;
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<GatehouseAutomationPacket> args)
        {
            GatehouseAutomationPacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion ||
                packet.PlayerId <= 0 || packet.BuildingGlobalId <= 0)
            {
                LogError("rejected a gatehouse Chore with an invalid payload.");
                return;
            }

            try
            {
                ApplyManualState(packet.PlayerId, packet.BuildingGlobalId, packet.AutomaticEnabled,
                    $"Chore operationId={packet.OperationId}");
            }
            catch (Exception ex)
            {
                LogError($"gatehouse Chore execution failed: operationId={packet.OperationId}, exception={ex}");
            }
        }

        private void ApplyManualState(int playerId, int globalId, bool automaticEnabled, string source)
        {
            if (!TryFindGatehouseByGlobalId(globalId, out GameBuilding* building, out int buildingId) ||
                building->r_PlayerIdOwner != playerId)
            {
                LogError($"gatehouse state change rejected because the owned gatehouse could not be resolved: source={source}, globalId={globalId}, playerId={playerId}.");
                return;
            }

            if (automaticEnabled)
            {
                manualOnlyGateGlobalIds.Remove(globalId);
                building->r_GateDoNotCloseForTicks = 0;
            }
            else
            {
                manualOnlyGateGlobalIds.Add(globalId);
                // Negative is Vanilla's own permanent manual-close sentinel and skips automatic updates.
                building->r_GateDoNotCloseForTicks = -1;
            }

            RefreshButtonVisibility();
            LogInfo($"gatehouse automatic state applied: source={source}, buildingId={buildingId}, globalId={globalId}, owner={playerId}, automaticEnabled={automaticEnabled}, gateState={building->r_GateState}.");
        }

        private void OnGatehouseQuery(GatehouseQueryEventArgs args)
        {
            if (!settings.EnableMod || args == null)
                return;

            try
            {
                if (!TryGetLiveGatehouse(args.BuildingId, out GameBuilding* building, out GameGatehouseEntry* gatehouse))
                    return;

                int globalId = (int)building->r_GlobalId;
                if (!firstQueryLogged)
                {
                    firstQueryLogged = true;
                    LogInfo($"gatehouse query hook confirmed: buildingId={args.BuildingId}, globalId={globalId}, owner={building->r_PlayerIdOwner}, tileX={building->r_TilePositionXBegin}, tileY={building->r_TilePositionYBegin}.");
                }

                if (manualOnlyGateGlobalIds.Contains(globalId))
                {
                    args.ShouldClose = false;
                    return;
                }

                if (!settings.RequireReachableEnemyForAutomaticGateClosing || !reachabilityAvailable)
                    return;

                if (TryIsUnitReachableToGate(args.UnitId, gatehouse, out bool reachable) && !reachable)
                    args.ShouldClose = false;
            }
            catch (Exception ex)
            {
                // Fail open: a diagnostic or PCL failure must never suppress Vanilla closure.
                LogFailure($"gatehouse reachability query failed: buildingId={args.BuildingId}, unitId={args.UnitId}, error={ex}");
            }
        }

        private bool TryIsUnitReachableToGate(int unitId, GameGatehouseEntry* gatehouse, out bool reachable)
        {
            reachable = true;
            if (unitId <= 0 || gatehouse == null ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0 ||
                unit->r_ControllableForPlayerId <= 0)
            {
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int sourceTileId = (int)unit->r_CurrentPositionTileId;
            int entryTileId = (int)gatehouse->r_EntryDoorTileId;
            int exitTileId = (int)gatehouse->r_ExitDoorTileId;
            if (!tileApi.IsValidTileId(sourceTileId) || !tileApi.IsValidTileId(entryTileId) || !tileApi.IsValidTileId(exitTileId))
                return false;

            Span<ushort> pathConnections = tileApi.TileManager.PathConnectionGrid;
            if ((uint)sourceTileId >= (uint)pathConnections.Length ||
                (uint)entryTileId >= (uint)pathConnections.Length ||
                (uint)exitTileId >= (uint)pathConnections.Length)
            {
                return false;
            }

            int tick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
            if (tick != lastCacheTick)
            {
                reachabilityCache.Clear();
                lastCacheTick = tick;
            }

            int playerId = unit->r_ControllableForPlayerId;
            int sourcePcl = pathConnections[sourceTileId];
            int entryPcl = pathConnections[entryTileId];
            int exitPcl = pathConnections[exitTileId];
            int mode = unit->N000001CA;
            var key = new ReachabilityKey(playerId, sourcePcl, entryPcl, exitPcl, mode);
            if (reachabilityCache.TryGetValue(key, out reachable))
            {
                cacheHits++;
                return true;
            }

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            int entryResult = playerApi.GetNextReachablePCLToDestinationForPlayer(playerId, entryPcl, sourcePcl, mode);
            int exitResult = entryResult != 0
                ? entryResult
                : playerApi.GetNextReachablePCLToDestinationForPlayer(playerId, exitPcl, sourcePcl, mode);
            reachable = entryResult != 0 || exitResult != 0;
            nativeQueries += entryResult != 0 ? 1 : 2;
            if (reachable)
                reachableResults++;
            else
                unreachableResults++;
            reachabilityCache[key] = reachable;
            return true;
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !mapActive || manualOnlyGateGlobalIds.Count == 0)
                return null;

            int[] ids = new int[manualOnlyGateGlobalIds.Count];
            manualOnlyGateGlobalIds.CopyTo(ids);
            Array.Sort(ids);
            byte[] bytes = MessagePackSerializer.Serialize(new GatehouseAutomationSaveState
            {
                Version = GatehouseAutomationSaveState.CurrentVersion,
                ManualOnlyGateGlobalIds = ids
            });
            LogInfo($"gatehouse state saved: manualOnly={ids.Length}, payloadBytes={bytes.Length}.");
            return bytes;
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile)
                return;

            GatehouseAutomationSaveState state = MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(bytes);
            if (state == null || state.Version != GatehouseAutomationSaveState.CurrentVersion ||
                state.ManualOnlyGateGlobalIds == null || state.ManualOnlyGateGlobalIds.Length > MaximumSavedGatehouses)
            {
                throw new InvalidOperationException("The gatehouse save state has an unsupported version or invalid entry count.");
            }

            var loadedIds = new HashSet<int>();
            for (int index = 0; index < state.ManualOnlyGateGlobalIds.Length; index++)
            {
                int globalId = state.ManualOnlyGateGlobalIds[index];
                if (globalId <= 0 || !loadedIds.Add(globalId))
                    throw new InvalidOperationException($"The gatehouse save state contains an invalid or duplicate global ID: {globalId}.");
            }

            manualOnlyGateGlobalIds.Clear();
            manualOnlyGateGlobalIds.UnionWith(loadedIds);
            mapActive = true;
            ReconcileManualGateTimers(removeMissing: false);
            LogInfo($"gatehouse state loaded: manualOnly={manualOnlyGateGlobalIds.Count}, payloadBytes={bytes.Length}.");
        }

        private void ReconcileManualGateTimers(bool removeMissing)
        {
            if (!settings.EnableMod || manualOnlyGateGlobalIds.Count == 0)
                return;

            List<int> missing = removeMissing ? new List<int>() : null;
            foreach (int globalId in manualOnlyGateGlobalIds)
            {
                if (TryFindGatehouseByGlobalId(globalId, out GameBuilding* building, out _))
                    building->r_GateDoNotCloseForTicks = -1;
                else
                    missing?.Add(globalId);
            }

            if (missing != null)
            {
                for (int index = 0; index < missing.Count; index++)
                    manualOnlyGateGlobalIds.Remove(missing[index]);
                if (missing.Count > 0)
                    LogWarning($"ignored {missing.Count} saved gatehouse IDs that do not resolve on the loaded map.");
            }
        }

        private void ReleaseManualGateTimers()
        {
            foreach (int globalId in manualOnlyGateGlobalIds)
            {
                if (TryFindGatehouseByGlobalId(globalId, out GameBuilding* building, out _))
                    building->r_GateDoNotCloseForTicks = 0;
            }
        }

        private void ResetMapState()
        {
            if (mapActive)
            {
                LogInfo($"gatehouse map state cleared: manualOnly={manualOnlyGateGlobalIds.Count}, nativeQueries={nativeQueries}, cacheHits={cacheHits}, reachable={reachableResults}, unreachable={unreachableResults}.");
            }
            mapActive = false;
            manualOnlyGateGlobalIds.Clear();
            ClearReachabilityCache();
            buttonViewModel.Hide();
            firstQueryLogged = false;
            failureLogs = 0;
            nativeQueries = 0;
            cacheHits = 0;
            reachableResults = 0;
            unreachableResults = 0;
        }

        private void ClearReachabilityCache()
        {
            reachabilityCache.Clear();
            lastCacheTick = int.MinValue;
        }

        private static bool TryGetOwnedGatehouse(int buildingId, int playerId, out GameBuilding* building, out string failure)
        {
            building = null;
            failure = string.Empty;
            if (!TryGetLiveGatehouse(buildingId, out building, out _))
            {
                failure = "not-a-live-gatehouse";
                return false;
            }
            if (building->r_PlayerIdOwner != playerId)
            {
                failure = $"owner-{building->r_PlayerIdOwner}-does-not-match-{playerId}";
                building = null;
                return false;
            }
            if (building->r_GlobalId == 0 || building->r_GlobalId > int.MaxValue)
            {
                failure = "invalid-global-id";
                building = null;
                return false;
            }
            return true;
        }

        private static bool TryGetLiveGatehouse(int buildingId, out GameBuilding* building, out GameGatehouseEntry* gatehouse)
        {
            building = null;
            gatehouse = null;
            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            return buildingId > 0 && api.TryGetBuildingById(buildingId, out building) && building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                api.TryGetGatehouseEntryById(buildingId, out gatehouse) && gatehouse != null &&
                gatehouse->r_BuildingId == (uint)buildingId && gatehouse->r_GlobalId == building->r_GlobalId;
        }

        private static bool TryFindGatehouseByGlobalId(int globalId, out GameBuilding* building, out int buildingId)
        {
            building = null;
            buildingId = 0;
            if (globalId <= 0)
                return false;

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding candidate = ref buildings[index];
                if (candidate.r_AliveState != AliveState.IsAlive || candidate.r_GlobalId != (uint)globalId)
                    continue;

                int candidateId = index + 1;
                if (GameBuildingManagerAPI.Instance.TryGetGatehouseEntryById(candidateId, out GameGatehouseEntry* gatehouse) &&
                    gatehouse != null && gatehouse->r_BuildingId == (uint)candidateId && gatehouse->r_GlobalId == candidate.r_GlobalId)
                {
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(candidateId, out building) || building == null)
                        continue;
                    buildingId = candidateId;
                    return true;
                }
            }
            return false;
        }

        private bool IsChoreTransportReady() =>
            networkInitialized && packetHook != null && ChoreNetworkTransport.IsAvailable;

        private int NextOperationId()
        {
            if (nextOperationId == int.MaxValue)
                nextOperationId = 0;
            return ++nextOperationId;
        }

        private static int GetControlledPlayerId()
        {
            if ((GamePlayerManagerAPI.Instance?.IsInMapEditor() ?? false) ||
                (MainViewModel.Instance?.IsMapEditorMode ?? false))
            {
                // The gate button is available only for the active editor player's buildings.
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            }

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : 1;
        }

        private void LogFailure(string message)
        {
            if (failureLogs >= MaximumFailureLogs)
                return;
            failureLogs++;
            LogWarning($"{message}. Vanilla remains authoritative ({failureLogs}/{MaximumFailureLogs}).");
        }

        private void LogInfo(string message) => log.LogInfo($"[{TimestampNow()}] Extra Features {message}");
        private void LogWarning(string message) => log.LogWarning($"[{TimestampNow()}] Extra Features {message}");
        private void LogError(string message) => log.LogError($"[{TimestampNow()}] Extra Features {message}");
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private readonly struct ReachabilityKey : IEquatable<ReachabilityKey>
        {
            public ReachabilityKey(int playerId, int sourcePcl, int entryPcl, int exitPcl, int mode)
            {
                PlayerId = playerId;
                SourcePcl = sourcePcl;
                EntryPcl = entryPcl;
                ExitPcl = exitPcl;
                Mode = mode;
            }

            private int PlayerId { get; }
            private int SourcePcl { get; }
            private int EntryPcl { get; }
            private int ExitPcl { get; }
            private int Mode { get; }

            public bool Equals(ReachabilityKey other) =>
                PlayerId == other.PlayerId && SourcePcl == other.SourcePcl &&
                EntryPcl == other.EntryPcl && ExitPcl == other.ExitPcl && Mode == other.Mode;
            public override bool Equals(object obj) => obj is ReachabilityKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = hash * 397 ^ SourcePcl;
                    hash = hash * 397 ^ EntryPcl;
                    hash = hash * 397 ^ ExitPcl;
                    return hash * 397 ^ Mode;
                }
            }
        }
    }
}
