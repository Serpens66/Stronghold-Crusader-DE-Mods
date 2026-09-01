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
        private const int MaximumFailureLogs = 20;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly GatehouseAutomationButtonViewModel buttonViewModel;
        private readonly HashSet<int> manualOnlyGateGlobalIds = new HashSet<int>();
        private readonly List<GatehouseMapLocator> pendingMapLocators = new List<GatehouseMapLocator>();
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
        private bool editorSessionActive;
        private bool disposed;
        private bool firstQueryLogged;
        private bool iconLoadAttempted;
        private int lastCacheTick = int.MinValue;
        private int failureLogs;
        private int nextOperationId;
        private int lastUiFrame = -1;
        private int lastLocatorResolveFrame = -1;
        private string lastVisibilityState;
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

            // SE-GATEHOUSE-UNIT-ID-COMPAT: Re-audit this subscription after every
            // Script Extender update. The handler compensates for the 0-based
            // UnitId emitted by the audited 1.42.0 implementation.
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
            LogInfo("gatehouse automation initialized; savegames use global building IDs and editor maps use stable locators in save schema v2.");
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
            bool wasActive = mapActive;
            mapActive = true;
            editorSessionActive = false;
            ClearReachabilityCache();
            ResolvePendingMapLocators(removeUnresolved: true);
            ReconcileManualGateTimers(removeMissing: true);
            RefreshButtonVisibility();
            LogInfo($"gatehouse map state {(wasActive ? "resumed" : "started")}: manualOnly={manualOnlyGateGlobalIds.Count}, reachabilityAvailable={reachabilityAvailable}.");
        }

        public void EndMap()
        {
            ResetMapState();
        }

        public void RefreshButtonVisibility()
        {
            EnsureEditorMapState();
            if (!settings.EnableMod)
            {
                buttonViewModel.Hide();
                LogVisibilityState("hidden: mod-disabled");
                return;
            }
            if (!mapActive)
            {
                buttonViewModel.Hide();
                LogVisibilityState($"hidden: map-inactive, editor={IsMapEditor()}");
                return;
            }
            if (multiplayerFeatureGate.BlocksLocalStateChanges && !IsChoreTransportReady())
            {
                buttonViewModel.Hide();
                LogVisibilityState("hidden: multiplayer-chore-unavailable");
                return;
            }

            int localPlayerId = GetControlledPlayerId();
            int selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
            if (!TryGetOwnedGatehouse(selectedBuildingId, localPlayerId, out GameBuilding* building, out string failure))
            {
                buttonViewModel.Hide();
                LogVisibilityState($"hidden: editor={IsMapEditor()}, playerId={localPlayerId}, selectedBuildingId={selectedBuildingId}, reason={failure}, selection={DescribeBuilding(selectedBuildingId)}");
                return;
            }

            bool automaticEnabled = !manualOnlyGateGlobalIds.Contains((int)building->r_GlobalId);
            buttonViewModel.Show(automaticEnabled);
            LogVisibilityState($"visible: editor={IsMapEditor()}, playerId={localPlayerId}, selectedBuildingId={selectedBuildingId}, globalId={building->r_GlobalId}, automaticEnabled={automaticEnabled}");
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
            if (!settings.EnableMod)
                return;

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
                EnsureEditorMapState();
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

                // SE-GATEHOUSE-UNIT-ID-COMPAT: Script Extender 1.42.0
                // creates this event field with GetIndexByOffset(...), so it is
                // a zero-based span index despite being named UnitId. Re-audit
                // after an SE update; remove this conversion once upstream emits
                // the documented one-based ID, or it would become an off-by-one.
                int unitSpanIndex = args.UnitId;
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!GatehouseQueryUnitIdPolicy.TryConvertSpanIndexToGameId(
                        unitSpanIndex,
                        units.Length,
                        out int unitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null)
                {
                    return;
                }

                // The Script Extender replaces these Vanilla comparisons at the
                // native hook. Re-evaluate them for the corrected candidate slot.
                bool vanillaCandidateCanClose =
                    unit->r_AliveState == AliveState.IsAlive &&
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_LION &&
                    unit->r_ControllableForPlayerId != 0;
                // Preserve an intentional decision made by an earlier event
                // subscriber; only repair the Script Extender's broken default.
                args.ShouldClose = GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(
                    args.ShouldClose,
                    vanillaCandidateCanClose);
                if (args.ShouldClose != true)
                    return;

                int globalId = (int)building->r_GlobalId;
                if (!firstQueryLogged)
                {
                    firstQueryLogged = true;
                    LogInfo($"gatehouse query hook confirmed: buildingId={args.BuildingId}, rawUnitSpanIndex={unitSpanIndex}, unitId={unitId}, globalId={globalId}, owner={building->r_PlayerIdOwner}, tileX={building->r_TilePositionXBegin}, tileY={building->r_TilePositionYBegin}.");
                }

                if (manualOnlyGateGlobalIds.Contains(globalId))
                {
                    args.ShouldClose = false;
                    return;
                }

                if (!settings.RequireReachableEnemyForAutomaticGateClosing || !reachabilityAvailable)
                    return;

                if (TryIsUnitReachableToGate(unitId, unit, gatehouse, out bool reachable) && !reachable)
                    args.ShouldClose = false;
            }
            catch (Exception ex)
            {
                // Fail open: a diagnostic or PCL failure must never suppress Vanilla closure.
                LogFailure($"gatehouse reachability query failed: buildingId={args.BuildingId}, rawUnitSpanIndex={args.UnitId}, error={ex}");
            }
        }

        private bool TryIsUnitReachableToGate(
            int unitId,
            GameUnit* unit,
            GameGatehouseEntry* gatehouse,
            out bool reachable)
        {
            reachable = true;
            if (unitId <= 0 || gatehouse == null || unit == null ||
                unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0 ||
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
            bool editorMapSave = context.IsMapEditorSave;
            if ((!context.IsSaveFile && !editorMapSave) || (!mapActive && !editorMapSave))
                return null;

            int[] ids = new int[manualOnlyGateGlobalIds.Count];
            manualOnlyGateGlobalIds.CopyTo(ids);
            Array.Sort(ids);
            GatehouseMapLocator[] locators = editorMapSave
                ? BuildMapLocators()
                : Array.Empty<GatehouseMapLocator>();
            byte[] bytes = MessagePackSerializer.Serialize(new GatehouseAutomationSaveState
            {
                Version = GatehouseAutomationSaveState.CurrentVersion,
                ManualOnlyGateGlobalIds = editorMapSave ? Array.Empty<int>() : ids,
                ManualOnlyGateLocators = locators
            });
            LogInfo($"gatehouse state saved: context={(editorMapSave ? "editor-map" : "save-file")}, globalIds={(editorMapSave ? 0 : ids.Length)}, locators={locators.Length}, payloadBytes={bytes.Length}.");
            return bytes;
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            GatehouseAutomationSaveState state = MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(bytes);
            bool supportedVersion = state != null && (state.Version == 1 || state.Version == GatehouseAutomationSaveState.CurrentVersion);
            int[] savedIds = state?.ManualOnlyGateGlobalIds ?? Array.Empty<int>();
            GatehouseMapLocator[] savedLocators = state?.ManualOnlyGateLocators ?? Array.Empty<GatehouseMapLocator>();
            if (!supportedVersion)
            {
                throw new InvalidOperationException("The gatehouse save state has an unsupported version or invalid entry count.");
            }

            var loadedIds = new HashSet<int>();
            for (int index = 0; index < savedIds.Length; index++)
            {
                int globalId = savedIds[index];
                if (globalId <= 0 || !loadedIds.Add(globalId))
                    throw new InvalidOperationException($"The gatehouse save state contains an invalid or duplicate global ID: {globalId}.");
            }

            ValidateLocators(savedLocators);

            manualOnlyGateGlobalIds.Clear();
            pendingMapLocators.Clear();
            if (context.IsSaveFile)
            {
                manualOnlyGateGlobalIds.UnionWith(loadedIds);
                mapActive = true;
                ReconcileManualGateTimers(removeMissing: false);
            }
            else
            {
                pendingMapLocators.AddRange(savedLocators);
                ResolvePendingMapLocators(removeUnresolved: false);
            }
            LogInfo($"gatehouse state loaded: context={(context.IsSaveFile ? "save-file" : "map")}, version={state.Version}, manualOnly={manualOnlyGateGlobalIds.Count}, pendingLocators={pendingMapLocators.Count}, payloadBytes={bytes.Length}.");
        }

        private GatehouseMapLocator[] BuildMapLocators()
        {
            var locators = new List<GatehouseMapLocator>(manualOnlyGateGlobalIds.Count + pendingMapLocators.Count);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (int globalId in manualOnlyGateGlobalIds)
            {
                if (TryFindGatehouseByGlobalId(globalId, out GameBuilding* building, out _))
                    AddUniqueLocator(locators, identities, CreateLocator(building));
                else
                    LogFailure($"gatehouse editor-map save could not resolve manual-only globalId={globalId}");
            }
            for (int index = 0; index < pendingMapLocators.Count; index++)
                AddUniqueLocator(locators, identities, pendingMapLocators[index]);

            locators.Sort(CompareLocators);
            return locators.ToArray();
        }

        private void ResolvePendingMapLocators(bool removeUnresolved)
        {
            if (pendingMapLocators.Count == 0)
                return;
            if (!removeUnresolved && lastLocatorResolveFrame >= 0 && Time.frameCount - lastLocatorResolveFrame < 30)
                return;
            lastLocatorResolveFrame = Time.frameCount;

            int resolved = 0;
            int ambiguous = 0;
            var remaining = new List<GatehouseMapLocator>();
            for (int index = 0; index < pendingMapLocators.Count; index++)
            {
                GatehouseMapLocator locator = pendingMapLocators[index];
                int matches = FindGatehouseByLocator(locator, out GameBuilding* building);
                if (matches == 1)
                {
                    manualOnlyGateGlobalIds.Add((int)building->r_GlobalId);
                    building->r_GateDoNotCloseForTicks = -1;
                    resolved++;
                }
                else
                {
                    if (matches > 1)
                        ambiguous++;
                    if (!removeUnresolved)
                        remaining.Add(locator);
                    else
                        LogFailure($"gatehouse map locator could not be resolved uniquely: {FormatLocator(locator)}, matches={matches}");
                }
            }

            pendingMapLocators.Clear();
            pendingMapLocators.AddRange(remaining);
            LogInfo($"gatehouse map locators resolved: resolved={resolved}, pending={pendingMapLocators.Count}, ambiguous={ambiguous}, finalPass={removeUnresolved}.");
        }

        private void EnsureEditorMapState()
        {
            bool editor = IsMapEditor();
            if (!editor)
            {
                if (editorSessionActive)
                {
                    editorSessionActive = false;
                    ResetMapState();
                    LogInfo("gatehouse editor map state ended after leaving the editor.");
                }
                return;
            }

            int activePlayerId = EditorDirector.instance?.ActivePlayerID ?? -1;
            if (activePlayerId < 1 || activePlayerId > 8 ||
                GameData.Instance?.lastGameState == null || MainViewModel.Instance?.HUDBuildingPanel == null)
            {
                return;
            }

            if (!mapActive)
            {
                mapActive = true;
                editorSessionActive = true;
                ClearReachabilityCache();
                LogInfo($"gatehouse editor map state started: activePlayerId={activePlayerId}, pendingLocators={pendingMapLocators.Count}.");
            }
            else
            {
                editorSessionActive = true;
            }

            ResolvePendingMapLocators(removeUnresolved: false);
            ReconcileManualGateTimers(removeMissing: false);
        }

        private static GatehouseMapLocator CreateLocator(GameBuilding* building)
        {
            return new GatehouseMapLocator
            {
                OwnerPlayerId = building->r_PlayerIdOwner,
                BuildingType = (int)building->r_BuildingType,
                TileXBegin = building->r_TilePositionXBegin,
                TileYBegin = building->r_TilePositionYBegin,
                TileXEnd = building->r_TilePositionXEnd,
                TileYEnd = building->r_TilePositionYEnd
            };
        }

        private static int FindGatehouseByLocator(GatehouseMapLocator locator, out GameBuilding* building)
        {
            building = null;
            int matches = 0;
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding candidate = ref buildings[spanIndex];
                if (candidate.r_AliveState != AliveState.IsAlive ||
                    candidate.r_PlayerIdOwner != locator.OwnerPlayerId ||
                    (int)candidate.r_BuildingType != locator.BuildingType ||
                    candidate.r_TilePositionXBegin != locator.TileXBegin ||
                    candidate.r_TilePositionYBegin != locator.TileYBegin ||
                    candidate.r_TilePositionXEnd != locator.TileXEnd ||
                    candidate.r_TilePositionYEnd != locator.TileYEnd)
                {
                    continue;
                }

                int candidateId = spanIndex + 1;
                if (!TryGetLiveGatehouse(candidateId, out GameBuilding* candidateBuilding, out _))
                    continue;
                matches++;
                building = candidateBuilding;
            }
            return matches;
        }

        private static void ValidateLocators(GatehouseMapLocator[] locators)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < locators.Length; index++)
            {
                GatehouseMapLocator locator = locators[index];
                if (locator == null || !locator.HasValidShape ||
                    !seen.Add(FormatLocator(locator)))
                {
                    throw new InvalidOperationException($"The gatehouse save state contains an invalid or duplicate map locator at index {index}.");
                }
            }
        }

        private static void AddUniqueLocator(
            List<GatehouseMapLocator> locators,
            HashSet<string> identities,
            GatehouseMapLocator candidate)
        {
            if (candidate == null || !identities.Add(FormatLocator(candidate)))
                return;
            locators.Add(candidate);
        }

        private static int CompareLocators(GatehouseMapLocator left, GatehouseMapLocator right)
        {
            int result = left.OwnerPlayerId.CompareTo(right.OwnerPlayerId);
            if (result != 0) return result;
            result = left.BuildingType.CompareTo(right.BuildingType);
            if (result != 0) return result;
            result = left.TileXBegin.CompareTo(right.TileXBegin);
            if (result != 0) return result;
            result = left.TileYBegin.CompareTo(right.TileYBegin);
            if (result != 0) return result;
            result = left.TileXEnd.CompareTo(right.TileXEnd);
            return result != 0 ? result : left.TileYEnd.CompareTo(right.TileYEnd);
        }

        private static string FormatLocator(GatehouseMapLocator locator) =>
            locator.IdentityKey;

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
            editorSessionActive = false;
            manualOnlyGateGlobalIds.Clear();
            pendingMapLocators.Clear();
            ClearReachabilityCache();
            buttonViewModel.Hide();
            lastVisibilityState = null;
            lastLocatorResolveFrame = -1;
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
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding candidate = ref buildings[spanIndex];
                if (candidate.r_AliveState != AliveState.IsAlive || candidate.r_GlobalId != (uint)globalId)
                    continue;

                int candidateId = spanIndex + 1;
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
            if (Shared.GameModeHelper.IsMapEditor())
            {
                // The gate button is available only for the active editor player's buildings.
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            }

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : 1;
        }

        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();

        private static string DescribeBuilding(int buildingId)
        {
            if (buildingId <= 0)
                return "none";
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null)
                return "unresolvable";
            return $"type={building->r_BuildingType},alive={building->r_AliveState},owner={building->r_PlayerIdOwner},globalId={building->r_GlobalId}";
        }

        private void LogVisibilityState(string state)
        {
            if (string.Equals(lastVisibilityState, state, StringComparison.Ordinal))
                return;
            lastVisibilityState = state;
            log.LogDebug($"[{TimestampNow()}] Extra Features gatehouse diagnostic: button visibility state: {state}.");
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
