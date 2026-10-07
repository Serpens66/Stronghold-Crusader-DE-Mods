// Feature: Reachability-aware and per-building manual gatehouse automation.
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MonoMod.RuntimeDetour;
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
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
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

        public void Show(bool automaticEnabled, bool drawbridge)
        {
            ButtonVisibility = Visibility.Visible;
            ManualIndicatorVisibility = automaticEnabled ? Visibility.Hidden : Visibility.Visible;
            IconOpacity = automaticEnabled ? 1.0 : 0.48;
            ToolTipText = SerpLocalization.Get(drawbridge
                ? (automaticEnabled ? "SomeSettings.DrawbridgeAutomaticEnabledTooltip" : "SomeSettings.DrawbridgeManualOnlyTooltip")
                : (automaticEnabled ? "SomeSettings.GatehouseAutomaticEnabledTooltip" : "SomeSettings.GatehouseManualOnlyTooltip"));
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
        private delegate void BuildingHudActionDelegate(MainViewModel self);
        private delegate void EditorPlayerActionDelegate(EditorDirector self, int playerId);
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
        private IDisposable gatehouseQuerySubscription;
        private R3PacketEventHook<GatehouseAutomationPacket> packetHook;
        private IDisposable packetSubscription;
        private IGatehouseTimingCapability timingCapability;
        private bool automationReady;
        private bool timingReadinessRegistered;
        private string lastTimingFailure;
        private bool initialized;
        private bool networkInitialized;
        private bool saveHandlerRegistered;
        private bool mapActive;
        private bool editorSessionActive;
        private bool loadedMapStatePending;
        private bool disposed;
        private bool firstQueryLogged;
        private bool iconLoadAttempted;
        private int failureLogs;
        private int nextOperationId;
        private Hook buildingHudHook;
        private BuildingHudActionDelegate buildingHudTrampoline;
        private Hook editorPlayerHook;
        private EditorPlayerActionDelegate editorPlayerTrampoline;
        private MainViewModel observedHudViewModel;
        private IDisposable buildingDeleteSubscription;
        private IDisposable buildingSpawnSubscription;
        private string lastVisibilityState;

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

            // Script Extender 2.4.0 supplies one-based game IDs here. Keep the
            // boundary validation because index/ID confusion usually resolves a neighbour.
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
            try
            {
                buildingDeleteSubscription = BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(OnBuildingDeleted);
                buildingSpawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawned);
                InstallHudChangeHooks();
            }
            catch
            {
                buildingDeleteSubscription?.Dispose();
                buildingDeleteSubscription = null;
                buildingSpawnSubscription?.Dispose();
                buildingSpawnSubscription = null;
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
                gatehouseQuerySubscription?.Dispose();
                gatehouseQuerySubscription = null;
                throw;
            }
            initialized = true;
            if (!timingReadinessRegistered)
            {
                timingReadinessRegistered = true;
                ApiShared.WhenReady(OnApiSharedReady);
            }
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

        public void ApplySettings()
        {
            ApplyTimingSettings();

            // Save data can arrive before the native map has finished loading.
            // Only the completed session start may resolve saved building IDs.
            if (!loadedMapStatePending && automationReady && Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                if (mapActive)
                    ReconcileManualGateTimers(removeMissing: false);
            }
            else if (!loadedMapStatePending)
                ReleaseManualGateTimers();

            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(RefreshButtonVisibility);
        }

        public void BeginMap()
        {
            bool wasActive = mapActive;
            loadedMapStatePending = false;
            mapActive = true;
            editorSessionActive = false;
            ResolvePendingMapLocators(removeUnresolved: true);
            ReconcileManualGateTimers(removeMissing: true);
            RefreshButtonVisibility();
            LogInfo($"gatehouse map state {(wasActive ? "resumed" : "started")}: manualOnly={manualOnlyGateGlobalIds.Count}.");
        }

        public void EndMap()
        {
            ResetMapState();
        }

        public void RefreshButtonVisibility()
        {
            TryLoadButtonIcon();
            if (loadedMapStatePending)
            {
                buttonViewModel.Hide();
                LogVisibilityState("hidden: saved-map-pending");
                return;
            }
            RefreshEditorReadiness();
            if (!automationReady)
            {
                buttonViewModel.Hide();
                LogVisibilityState("hidden: native-automation-unavailable");
                return;
            }
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
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
            buttonViewModel.Show(automaticEnabled, building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE);
            LogVisibilityState($"visible: editor={IsMapEditor()}, playerId={localPlayerId}, selectedBuildingId={selectedBuildingId}, globalId={building->r_GlobalId}, automaticEnabled={automaticEnabled}");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (observedHudViewModel != null)
            {
                observedHudViewModel.PropertyChanged -= OnHudPropertyChanged;
                observedHudViewModel = null;
            }
            buildingDeleteSubscription?.Dispose();
            buildingDeleteSubscription = null;
            buildingSpawnSubscription?.Dispose();
            buildingSpawnSubscription = null;
            gatehouseQuerySubscription?.Dispose();
            gatehouseQuerySubscription = null;
            packetSubscription?.Dispose();
            packetSubscription = null;
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
            ReleaseManualGateTimers();
            ResetMapState();
        }

        private void OnApiSharedReady(IApiShared api)
        {
            try
            {
                NativeCapabilityDiagnostic diagnostic = null;
                if (api == null || !api.TryGetGatehouseTiming(
                        ExtraFeaturesPlugin.PluginGuid,
                        out IGatehouseTimingCapability capability,
                        out diagnostic))
                {
                    ReportTimingFailure(diagnostic, "APIShared did not publish gatehouse timing");
                    return;
                }

                timingCapability = capability;
                lastTimingFailure = null;
                ApplyTimingSettings();
                if (!(capability is IGatehouseAutomationCapability automation) ||
                    !automation.TrySetManualOnlyResolver(IsManualOnlyBuilding, out diagnostic))
                {
                    automationReady = false;
                    ReleaseManualGateTimers();
                    LogError($"gate/bridge manual control unavailable: {diagnostic?.Reason ?? "APIShared automation capability missing"}.");
                    return;
                }
                automationReady = true;
                ApplySettings();
                LogInfo("gatehouse timing is owned by APIShared; no local native timing patch is installed.");
            }
            catch (Exception ex)
            {
                ReportTimingFailure(null, $"APIShared readiness callback failed: {ex}");
            }
        }

        private void ApplyTimingSettings()
        {
            IGatehouseTimingCapability capability = timingCapability;
            if (capability == null)
                return;

            var desired = new GatehouseTimingSettings(
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod),
                settings.HumanGateReopenDelaySeconds,
                settings.AIGateReopenDelaySeconds,
                settings.HumanGateClosingDistanceTiles,
                settings.AIGateClosingDistanceTiles);
            if (!capability.TryApply(desired, out NativeCapabilityDiagnostic diagnostic))
                ReportTimingFailure(diagnostic, "APIShared rejected the requested gatehouse timing");
            else
                lastTimingFailure = null;
        }

        private void ReportTimingFailure(NativeCapabilityDiagnostic diagnostic, string fallback)
        {
            string failure = diagnostic == null
                ? fallback
                : $"{fallback}: state={diagnostic.State}, reason={diagnostic.Reason}, conflictOwner={diagnostic.ConflictOwnerGuid ?? "none"}";
            if (string.Equals(lastTimingFailure, failure, StringComparison.Ordinal))
                return;
            lastTimingFailure = failure;
            LogError(failure + ". Gatehouse timing customization remains disabled; other gatehouse automation stays active.");
        }

        private void InstallHudChangeHooks()
        {
            Hook pendingHud = null;
            Hook pendingEditor = null;
            try
            {
                MethodInfo hudMethod = typeof(MainViewModel).GetMethod(
                    nameof(MainViewModel.InBuildingGameAction), BindingFlags.Public | BindingFlags.Instance);
                MethodInfo editorMethod = typeof(EditorDirector).GetMethod(
                    nameof(EditorDirector.SetEditorPlayerID), BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);
                if (hudMethod == null || editorMethod == null)
                    throw new MissingMethodException("Gatehouse HUD change publisher is unavailable.");
                pendingHud = new Hook(hudMethod, (BuildingHudActionDelegate)OnBuildingHudAction);
                buildingHudTrampoline = pendingHud.GenerateTrampoline<BuildingHudActionDelegate>();
                pendingEditor = new Hook(editorMethod, (EditorPlayerActionDelegate)OnEditorPlayerAction);
                editorPlayerTrampoline = pendingEditor.GenerateTrampoline<EditorPlayerActionDelegate>();
                buildingHudHook = pendingHud;
                editorPlayerHook = pendingEditor;
            }
            catch
            {
                try { pendingEditor?.Undo(); } catch { }
                try { pendingEditor?.Dispose(); } catch { }
                try { pendingHud?.Undo(); } catch { }
                try { pendingHud?.Dispose(); } catch { }
                throw;
            }
        }

        private void OnBuildingHudAction(MainViewModel viewModel)
        {
            buildingHudTrampoline(viewModel);
            if (!ReferenceEquals(observedHudViewModel, viewModel))
            {
                if (observedHudViewModel != null)
                    observedHudViewModel.PropertyChanged -= OnHudPropertyChanged;
                observedHudViewModel = viewModel;
                observedHudViewModel.PropertyChanged += OnHudPropertyChanged;
            }
            RefreshButtonVisibilitySafely();
        }

        private void OnEditorPlayerAction(EditorDirector editor, int playerId)
        {
            editorPlayerTrampoline(editor, playerId);
            if (editorSessionActive)
                RefreshButtonVisibilitySafely();
        }

        private void OnHudPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args?.PropertyName == nameof(MainViewModel.Show_HUD_Building) &&
                sender is MainViewModel viewModel && !viewModel.Show_HUD_Building)
                buttonViewModel.Hide();
        }

        private void OnBuildingDeleted(BuildingDeleteEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post)
                return;
            int buildingId = args.BuildingId;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                if (mapActive && GamePlayerManagerAPI.Instance.GetSelectedBuildingId() == buildingId)
                    RefreshButtonVisibilitySafely();
            });
        }

        private void OnBuildingSpawned(BuildingSpawnEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post)
                return;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                if (editorSessionActive && pendingMapLocators.Count != 0)
                    RefreshButtonVisibilitySafely();
            });
        }

        private void RefreshButtonVisibilitySafely()
        {
            try { RefreshButtonVisibility(); }
            catch (Exception ex) { LogFailure($"gatehouse button refresh failed: {ex}"); }
        }

        private void TryLoadButtonIcon()
        {
            if (iconLoadAttempted || !MainViewModel.viewModelLoaded ||
                MainViewModel.Instance?.HUDBuildingPanel == null)
                return;

            // The plugin initializes before the game HUD; decode once its panel is available.
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
                if (!automationReady || loadedMapStatePending || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) || !mapActive)
                    return;
                RefreshEditorReadiness();

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
            short packetId = packetHook?.GetPacketId() ?? (short)0;
            if (!ExtraFeaturesChoreSender.TrySend(
                    packet,
                    packetId,
                    networkInitialized && packetHook != null,
                    value => GameNetworkAPI.Serialize(value),
                    () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA,
                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                    out byte[] body,
                    out string rejectionReason))
            {
                LogError($"gatehouse Chore was not queued; no local change was applied: operationId={operationId}, globalId={globalId}, reason={rejectionReason}.");
                return false;
            }

            LogInfo($"gatehouse Chore queued: operationId={operationId}, globalId={globalId}, automaticEnabled={automaticEnabled}, payloadBytes={sizeof(short) + body.Length}.");
            return true;
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<GatehouseAutomationPacket> args)
        {
            if (!Shared.GameplayModActivationGate.IsAllowed)
                return;

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
            if (!automationReady || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;
            if (!TryFindGatehouseByGlobalId(globalId, out GameBuilding* building, out int buildingId) ||
                building->r_PlayerIdOwner != playerId)
            {
                LogError($"gatehouse state change rejected because the owned gatehouse could not be resolved: source={source}, globalId={globalId}, playerId={playerId}.");
                return;
            }

            if (automaticEnabled)
            {
                manualOnlyGateGlobalIds.Remove(globalId);
                SetManualGateTimer(building, false);
            }
            else
            {
                manualOnlyGateGlobalIds.Add(globalId);
                SetManualGateTimer(building, true);
            }

            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(RefreshButtonVisibility);
            LogInfo($"gatehouse automatic state applied: source={source}, buildingId={buildingId}, globalId={globalId}, owner={playerId}, automaticEnabled={automaticEnabled}, gateState={building->r_GateState}.");
        }

        private void OnGatehouseQuery(GatehouseQueryEventArgs args)
        {
            if (loadedMapStatePending || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) || args == null)
                return;

            try
            {
                if (!TryGetLiveGatehouse(args.BuildingId, out GameBuilding* building, out _))
                    return;

                // The Script Extender supplies the normal one-based game ID.
                // Validate that boundary directly; applying the old +1 correction
                // would silently select the adjacent unit for most valid values.
                int eventUnitId = args.UnitId;
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                        eventUnitId,
                        units.Length,
                        out int unitId) ||
                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null)
                {
                    return;
                }

                // The Script Extender replaces these Vanilla comparisons at the
                // native hook. Re-evaluate them for the corrected candidate slot.
                bool vanillaCandidateCanClose =
                    APIShared.UnitAccess.IsReallyAlive(unit) &&
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_LION &&
                    unit->r_ControllableForPlayerId != 0;
                // Preserve an intentional decision made by an earlier event
                // subscriber; only repair the Script Extender's broken default.
                args.ShouldClose = Shared.GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(
                    args.ShouldClose,
                    vanillaCandidateCanClose);
                if (args.ShouldClose != true)
                    return;

                int globalId = (int)building->r_GlobalId;
                if (!firstQueryLogged)
                {
                    firstQueryLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Extra Features gatehouse query hook confirmed: buildingId={args.BuildingId}, eventUnitId={eventUnitId}, validatedUnitId={unitId}, globalId={globalId}, owner={building->r_PlayerIdOwner}, tileX={building->r_TilePositionXBegin}, tileY={building->r_TilePositionYBegin}.");
                }

                // A manual gate still supplies Vanilla's enemy decision to automatic bridges.
                // APIShared prevents the local gate command and filters each linked recipient.

            }
            catch (Exception ex)
            {
                LogFailure($"gatehouse automation query failed: buildingId={args.BuildingId}, rawUnitSpanIndex={args.UnitId}, error={ex}");
            }
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!Shared.GameplayModActivationGate.IsAllowed)
                return null;

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
            loadedMapStatePending = true;
            if (context.IsSaveFile)
            {
                manualOnlyGateGlobalIds.UnionWith(loadedIds);
            }
            else
            {
                pendingMapLocators.AddRange(savedLocators);
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
                    // Editor locators can resolve later than ApplySettings (HUD/spawn event).
                    // Release a saved sentinel when disabled; never reintroduce it from a late load.
                    SetManualGateTimer(building,
                        automationReady && Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod));
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

        public void BeginEditorMap()
        {
            loadedMapStatePending = false;
            mapActive = true;
            editorSessionActive = true;
            RefreshButtonVisibility();
        }

        private void RefreshEditorReadiness()
        {
            if (!editorSessionActive || !mapActive) return;

            int activePlayerId = EditorDirector.instance?.ActivePlayerID ?? -1;
            if (activePlayerId < 1 || activePlayerId > 8 ||
                GameData.Instance?.lastGameState == null || !MainViewModel.viewModelLoaded ||
                MainViewModel.Instance?.HUDBuildingPanel == null)
            {
                return;
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
                AccessTileX = building->r_TileAccessPositionX,
                AccessTileY = building->r_TileAccessPositionY
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
                    candidate.r_TileAccessPositionX != locator.AccessTileX ||
                    candidate.r_TileAccessPositionY != locator.AccessTileY)
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
            result = left.AccessTileX.CompareTo(right.AccessTileX);
            return result != 0 ? result : left.AccessTileY.CompareTo(right.AccessTileY);
        }

        private static string FormatLocator(GatehouseMapLocator locator) =>
            locator.IdentityKey;

        private void ReconcileManualGateTimers(bool removeMissing)
        {
            if (!automationReady || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) || manualOnlyGateGlobalIds.Count == 0)
                return;

            List<int> missing = removeMissing ? new List<int>() : null;
            foreach (int globalId in manualOnlyGateGlobalIds)
            {
                if (TryFindGatehouseByGlobalId(globalId, out GameBuilding* building, out _))
                    SetManualGateTimer(building, true);
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
                    SetManualGateTimer(building, false);
            }
        }

        private void ResetMapState()
        {
            // The mode gate can close while the current native map is still alive.
            // Release our permanent-manual sentinel before discarding its identities.
            if (!loadedMapStatePending)
                ReleaseManualGateTimers();
            if (mapActive)
            {
                LogInfo($"gatehouse map state cleared: manualOnly={manualOnlyGateGlobalIds.Count}.");
            }
            mapActive = false;
            editorSessionActive = false;
            loadedMapStatePending = false;
            manualOnlyGateGlobalIds.Clear();
            pendingMapLocators.Clear();
            buttonViewModel.Hide();
            lastVisibilityState = null;
            firstQueryLogged = false;
            failureLogs = 0;
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

        private static bool TryGetLiveGatehouse(int buildingId, out GameBuilding* building, out PathConnectionRecord* gatehouse)
        {
            building = null;
            gatehouse = null;
            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            if (buildingId <= 0 || !api.TryGetBuildingById(buildingId, out building) || building == null ||
                building->r_AliveState != AliveState.IsAlive || building->r_GlobalId == 0 ||
                building->r_GlobalId > int.MaxValue)
                return false;
            if (building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE)
                return true;
            if (!IsGatehouseType(building->r_BuildingType))
                return false;
            return GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(buildingId, out gatehouse) &&
                gatehouse != null && gatehouse->r_BuildingId == buildingId &&
                gatehouse->r_SubjectGlobalId == building->r_GlobalId;
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
                if (TryGetLiveGatehouse(candidateId, out building, out _))
                {
                    buildingId = candidateId;
                    return true;
                }
            }
            return false;
        }

        private bool IsManualOnlyBuilding(int buildingId) =>
            automationReady && mapActive && !loadedMapStatePending &&
            Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
            TryGetLiveGatehouse(buildingId, out GameBuilding* building, out _) &&
            manualOnlyGateGlobalIds.Contains((int)building->r_GlobalId);

        private static bool IsGatehouseType(eStructs type) =>
            type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER ||
            type == eStructs.STRUCT_GATE_WOOD || type == eStructs.STRUCT_GATE_POSTERN ||
            type == eStructs.STRUCT_GATEHOUSE;

        private static void SetManualGateTimer(GameBuilding* building, bool manual)
        {
            // The bridge command/state lives in different bytes; never write a gate timer there.
            if (building != null && IsGatehouseType(building->r_BuildingType))
                building->r_GateDoNotCloseForTicks = manual ? (short)-1 : (short)0;
        }

        private bool IsChoreTransportReady() =>
            ExtraFeaturesChoreSender.IsAvailable(
                networkInitialized && packetHook != null,
                () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA);

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
            return localPlayerId > 0 ? localPlayerId : -1;
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

        private void LogInfo(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
            () => log.LogInfo($"[{TimestampNow()}] Extra Features {message}"));
        private void LogWarning(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
            () => log.LogWarning($"[{TimestampNow()}] Extra Features {message}"));
        private void LogError(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
            () => log.LogError($"[{TimestampNow()}] Extra Features {message}"));
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    }
}
