// Feature: Relocate a quarry's linked stone pile to the next valid position.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Zhuqiaomon.Memory;

namespace ExtraFeatures
{
    internal sealed class QuarryPileRelocationOperation
    {
        public int PlayerId;
        public int OperationId;
        public int QuarryGlobalId;
        public int OldPileGlobalId;
        public int TargetTileX;
        public int TargetTileY;
    }

    internal sealed class QuarryPileRelocationButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility buttonVisibility = Visibility.Hidden;

        public QuarryPileRelocationButtonViewModel(Action relocate)
        {
            RelocateCommand = new RelayCommand(relocate ?? throw new ArgumentNullException(nameof(relocate)));
        }

        public RelayCommand RelocateCommand { get; }

        public Visibility ButtonVisibility
        {
            get => buttonVisibility;
            private set
            {
                if (buttonVisibility == value)
                    return;

                buttonVisibility = value;
                OnPropertyChanged(nameof(ButtonVisibility));
            }
        }

        public void Show()
        {
            ButtonVisibility = Visibility.Visible;
        }

        public void Hide()
        {
            ButtonVisibility = Visibility.Hidden;
        }
    }

    internal sealed unsafe class QuarryPileRelocationRuntime : IDisposable
    {
        // Vanilla placeQuarry uses size 6 for the quarry, size 2 for its pile and tries 1..9.
        // setupBuildingEntrancesOffset exposes 4 * buildingSize clockwise perimeter candidates.
        private const int VanillaQuarryScale = 6;
        private const int VanillaPileScale = 2;
        private const int VanillaCandidateCount = VanillaQuarryScale * 4;
        private const int VanillaMinimumPlacementTry = 1;
        private const int VanillaMaximumPlacementTry = 9;
        private const int VanillaCandidateOffsetX = 0x31B7D0;
        private const int VanillaCandidateOffsetY = 0x31B7D4;
        private const int ChoreProtocolVersion = 1;
        private const double AIQuarryReadinessTimeoutSeconds = 10.0;

        // CrusaderDE setupBuildingEntrancesOffset. This is the native helper used by
        // findQuarryPileLocation to turn (buildingSize, pileSize, perimeterIndex, try)
        // into the exact relative candidate coordinates used by Vanilla.
        private const string SetupBuildingEntrancesOffsetPattern =
            "48 89 5C 24 08 8D 42 FF 41 8B D8 44 8B DA 4C 8B D1 83 F8 0C 0F 87 ?? ?? ?? ?? " +
            "48 98 48 8D 15 ?? ?? ?? ?? 8B 84 82 ?? ?? ?? ?? 48 03 C2 FF E0 49 63 C1 " +
            "8B 8C C2 ?? ?? ?? ?? 41 89 8A D0 B7 31 00";
        private const int SetupBuildingEntrancesOffsetRva = 0xC0270;

        private delegate void SetUpInbuildingDelegate(MainViewModel self, int overridePanel, int overrideType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetupBuildingEntrancesOffsetDelegate(
            NativePointer<GameBuildingManager> buildingManager,
            int buildingSize,
            int pileSize,
            int perimeterIndex,
            int placementTry);

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly QuarryPileRelocationButtonViewModel buttonViewModel;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, FailedRotationTargets> failedRotationTargetsByQuarry = new Dictionary<int, FailedRotationTargets>();
        private readonly Dictionary<int, PendingAIQuarry> pendingAIQuarriesByGlobalId = new Dictionary<int, PendingAIQuarry>();

        private Hook setUpInbuildingHook;
        private SetUpInbuildingDelegate setUpInbuildingTrampoline;
        private Button hookedRelocationButton;
        private TextBlock hookedRelocationTooltip;
        private PrefabSpawnCapture activePrefabSpawnCapture;
        private SetupBuildingEntrancesOffsetDelegate setupBuildingEntrancesOffset;
        private int nextOperationId;
        private int linkedRemovalSuppressionDepth;
        private bool initialized;
        private bool networkInitialized;
        private bool tickSubscribed;
        private bool mapActive;
        private bool aiSpawnObservationArmed;
        private string lastVisibilityLogState;
        private R3PacketEventHook<QuarryPileRelocationPacket> relocationPacketHook;
        private IDisposable relocationPacketSubscription;

        public QuarryPileRelocationRuntime(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
            buttonViewModel = new QuarryPileRelocationButtonViewModel(OnRelocateCommand);
        }

        public QuarryPileRelocationButtonViewModel ButtonViewModel => buttonViewModel;

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            relocationPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<QuarryPileRelocationPacket>();
            relocationPacketSubscription = relocationPacketHook.GetBaseHook().Observable.Subscribe(OnRelocationPacketReceived);
            networkInitialized = true;
            LogInfo($"Chore packet registered eagerly: packetId={relocationPacketHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
        }

        public void InstallNativeFunctions(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            try
            {
                int rva = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    SetupBuildingEntrancesOffsetPattern,
                    SetupBuildingEntrancesOffsetRva,
                    referenceHashMatches,
                    "quarry-pile Vanilla candidate helper",
                    log).Rva;
                setupBuildingEntrancesOffset = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<SetupBuildingEntrancesOffsetDelegate>(
                    IntPtr.Add(libraryHandle, rva));
                LogInfo($"Vanilla candidate helper installed: rva=0x{rva:X}, candidatesPerTry={VanillaCandidateCount}, placementTries={VanillaMinimumPlacementTry}-{VanillaMaximumPlacementTry}.");
            }
            catch (Exception ex)
            {
                setupBuildingEntrancesOffset = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile Vanilla candidate helper was not resolved; relocation remains disabled: {ex}");
            }
        }

        public void Initialize()
        {
            if (initialized)
                return;

            Hook installedHook = null;
            try
            {
                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => BeginMapState()));
                subscriptions.Add(MapLoaderR3EventHooks.OnLoadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ResetAISpawnObservationAfterLoad("map")));
                subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ResetAISpawnObservationAfterLoad("save")));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => EndMapState()));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(args => OnLinkedBuildingRemoval(args.BuildingId, "bulldoze-pre")));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(args => OnLinkedBuildingRemoval(args.BuildingId, "delete-pre")));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Subscribe(OnBuildingSpawn));
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;

                installedHook = new Hook(FindSetUpInbuildingMethod(), (SetUpInbuildingDelegate)SetUpInbuildingHook);
                setUpInbuildingTrampoline = installedHook.GenerateTrampoline<SetUpInbuildingDelegate>();
                setUpInbuildingHook = installedHook;
                initialized = true;
                buttonViewModel.Hide();
                LogInfo($"runtime initialized: mode=Vanilla-clockwise-prefab-spawn, nativeCandidateHelperAvailable={setupBuildingEntrancesOffset != null}, subscriptions={subscriptions.Count}, setUpInbuildingHookInstalled={setUpInbuildingHook != null}.");
            }
            catch
            {
                installedHook?.Dispose();
                UnsubscribeTick();
                DisposeSubscriptions();
                throw;
            }
        }

        public void Dispose()
        {
            if (!initialized)
                return;

            initialized = false;
            LogInfo("runtime dispose started.");
            buttonViewModel.Hide();
            EndMapState();
            UnhookRelocationButton();
            UnsubscribeTick();
            DisposeSubscriptions();
            setUpInbuildingHook?.Undo();
            setUpInbuildingHook?.Dispose();
            setUpInbuildingHook = null;
            setUpInbuildingTrampoline = null;
            LogInfo("runtime dispose completed.");
        }

        public void ApplySetting()
        {
            LogInfo($"setting applied: EnableMod={settings.EnableMod}, EnableQuarryPileRelocation={settings.EnableQuarryPileRelocation}, EnableAIQuarryPileTowardsKeep={settings.EnableAIQuarryPileTowardsKeep}.");
            if (!IsAIAutomationActive())
                pendingAIQuarriesByGlobalId.Clear();

            if (IsManualFeatureActive())
            {
                RefreshButtonVisibility();
                return;
            }

            buttonViewModel.Hide();
            HideRelocationTooltip();
        }

        public void RefreshButtonVisibility()
        {
            try
            {
                if (!IsManualFeatureActive())
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    LogVisibilityState($"hidden: feature-disabled, EnableMod={settings.EnableMod}, EnableQuarryPileRelocation={settings.EnableQuarryPileRelocation}");
                    return;
                }

                if (setupBuildingEntrancesOffset == null)
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    LogVisibilityState("hidden: Vanilla candidate helper unavailable");
                    return;
                }

                if (RequiresChoreTransport() && !IsChoreTransportReady())
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    LogVisibilityState("hidden: real multiplayer requires an available Chore transport");
                    return;
                }

                int localPlayerId = GetControlledPlayerId();
                int selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (!TryGetOwnedQuarry(selectedBuildingId, localPlayerId, out _, out string failureReason))
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    LogVisibilityState($"hidden: playerId={localPlayerId}, selectedBuildingId={selectedBuildingId}, reason={failureReason}");
                    return;
                }

                buttonViewModel.Show();
                LogVisibilityState($"visible: playerId={localPlayerId}, selectedBuildingId={selectedBuildingId}");
            }
            catch (Exception ex)
            {
                buttonViewModel.Hide();
                HideRelocationTooltip();
                Shared.DebugLogHelper.LogError(log, $"Extra Features quarry-pile button visibility refresh failed: {ex}");
            }
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
            HookRelocationButton(self);
            RefreshButtonVisibility();
        }

        private void HookRelocationButton(MainViewModel mainViewModel)
        {
            Button button = mainViewModel?.HUDBuildingPanel?.FindName("ExtraFeaturesQuarryPileRelocationButton") as Button;
            TextBlock tooltip = mainViewModel?.HUDBuildingPanel?.FindName("ExtraFeaturesQuarryPileRelocationTooltipHost") as TextBlock;

            if (tooltip != null)
                tooltip.Text = SerpLocalization.Get(SerpLocalization.QuarryPileRelocationTooltip);

            if (ReferenceEquals(button, hookedRelocationButton) && ReferenceEquals(tooltip, hookedRelocationTooltip))
                return;

            UnhookRelocationButton();
            hookedRelocationButton = button;
            hookedRelocationTooltip = tooltip;
            HideRelocationTooltip();
            if (hookedRelocationButton == null)
            {
                LogInfo("UI lookup did not find ExtraFeaturesQuarryPileRelocationButton after setUpInbuilding.");
                return;
            }

            hookedRelocationButton.Click += OnRelocationButtonClicked;
            hookedRelocationButton.MouseEnter += OnRelocationButtonMouseEnter;
            hookedRelocationButton.MouseLeave += OnRelocationButtonMouseLeave;
            LogInfo($"UI button hooked: visibility={hookedRelocationButton.Visibility}, isEnabled={hookedRelocationButton.IsEnabled}, dataContext={hookedRelocationButton.DataContext?.GetType().FullName ?? "null"}, tooltipFound={hookedRelocationTooltip != null}.");
        }

        private void UnhookRelocationButton()
        {
            if (hookedRelocationButton != null)
            {
                hookedRelocationButton.Click -= OnRelocationButtonClicked;
                hookedRelocationButton.MouseEnter -= OnRelocationButtonMouseEnter;
                hookedRelocationButton.MouseLeave -= OnRelocationButtonMouseLeave;
            }

            HideRelocationTooltip();
            hookedRelocationButton = null;
            hookedRelocationTooltip = null;
        }

        private void OnRelocationButtonMouseEnter(object sender, MouseEventArgs args)
        {
            if (hookedRelocationTooltip == null)
            {
                LogInfo("tooltip hover entered, but ExtraFeaturesQuarryPileRelocationTooltipHost was not found.");
                return;
            }

            hookedRelocationTooltip.Text = SerpLocalization.Get(SerpLocalization.QuarryPileRelocationTooltip);
            hookedRelocationTooltip.Visibility = Visibility.Visible;
            LogInfo("tooltip shown from physical MouseEnter event.");
        }

        private void OnRelocationButtonMouseLeave(object sender, MouseEventArgs args)
        {
            HideRelocationTooltip();
            LogInfo("tooltip hidden from physical MouseLeave event.");
        }

        private void HideRelocationTooltip()
        {
            if (hookedRelocationTooltip != null)
                hookedRelocationTooltip.Visibility = Visibility.Hidden;
        }

        private void OnRelocationButtonClicked(object sender, RoutedEventArgs args)
        {
            Button button = sender as Button;
            LogInfo($"physical UI button click received: senderType={sender?.GetType().FullName ?? "null"}, visibility={button?.Visibility.ToString() ?? "unknown"}, isEnabled={button?.IsEnabled.ToString() ?? "unknown"}, dataContext={button?.DataContext?.GetType().FullName ?? "null"}.");
        }

        private void OnRelocateCommand()
        {
            int localPlayerId = 0;
            int selectedBuildingId = 0;
            QuarryPileRelocationOperation attemptedOperation = null;

            try
            {
                LogInfo($"rotation command invoked: EnableMod={settings.EnableMod}, EnableQuarryPileRelocation={settings.EnableQuarryPileRelocation}.");
                if (!IsManualFeatureActive())
                {
                    LogInfo("rotation command stopped: feature is disabled.");
                    return;
                }

                localPlayerId = GetControlledPlayerId();
                selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                LogInfo($"command context read: localPlayerId={localPlayerId}, selectedBuildingId={selectedBuildingId}, appMode={GameData.Instance?.app_mode.ToString() ?? "unavailable"}, appSubMode={GameData.Instance?.app_sub_mode.ToString() ?? "unavailable"}.");

                if (!TryGetRelocatableQuarry(selectedBuildingId, localPlayerId, out GameBuilding* quarry, out GameBuilding* oldPile, out string failureReason))
                {
                    LogInfo($"rotation command stopped: selected quarry is not eligible, reason={failureReason}.");
                    RefreshButtonVisibility();
                    return;
                }

                int operationId = NextOperationId();
                LogInfo($"selected quarry validated: operationId={operationId}, quarryId={selectedBuildingId}, quarryGlobalId={quarry->r_GlobalId}, owner={quarry->r_PlayerIdOwner}, quarryTiles={quarry->r_TilePositionXBegin},{quarry->r_TilePositionYBegin}-{quarry->r_TilePositionXEnd},{quarry->r_TilePositionYEnd}, oldPileId={quarry->r_StoneQuarry_StockPileBuildingId}, oldPileGlobalId={oldPile->r_GlobalId}, oldPileTiles={oldPile->r_TilePositionXBegin},{oldPile->r_TilePositionYBegin}-{oldPile->r_TilePositionXEnd},{oldPile->r_TilePositionYEnd}, oldPileGridSize={oldPile->r_OccupyTileGridSize}.");

                if (!TryFindNextRotationTarget(localPlayerId, quarry, oldPile, operationId, out PlacementPosition target))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Extra Features quarry-pile rotation found no valid clockwise position: playerId={localPlayerId}, operationId={operationId}, quarryId={selectedBuildingId}, quarryGlobalId={quarry->r_GlobalId}, oldPileGlobalId={oldPile->r_GlobalId}.");
                    return;
                }

                attemptedOperation = new QuarryPileRelocationOperation
                {
                    PlayerId = localPlayerId,
                    OperationId = operationId,
                    QuarryGlobalId = (int)quarry->r_GlobalId,
                    OldPileGlobalId = (int)oldPile->r_GlobalId,
                    TargetTileX = target.X,
                    TargetTileY = target.Y
                };

                if (RequiresChoreTransport())
                {
                    if (!TrySendRotationChore(attemptedOperation))
                    {
                        LogInfo($"rotation command stopped: Chore send failed, operationId={operationId}, target={target.X},{target.Y}.");
                        RefreshButtonVisibility();
                    }
                    return;
                }

                if (!TryApplyRotation(attemptedOperation, "singleplayer-local-click", targetAlreadyValidated: true))
                {
                    RememberFailedRotationTarget(attemptedOperation, "replacement-transaction-failed");
                    LogInfo($"rotation command stopped: replacement transaction failed, operationId={operationId}, target={target.X},{target.Y}.");
                    RefreshButtonVisibility();
                    return;
                }

                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                if (attemptedOperation != null)
                    RememberFailedRotationTarget(attemptedOperation, "rotation-command-exception");

                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile rotation click failed: selectedBuildingId={selectedBuildingId}, playerId={localPlayerId}: {ex}");
                RefreshButtonVisibility();
            }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args == null)
                return;

            try
            {
                PrefabSpawnCapture capture = activePrefabSpawnCapture;
                if (capture != null)
                {
                    bool matchesExpectedInput = args.PlayerId == capture.PlayerId &&
                        args.Building == eStructs.STRUCT_QUARRYPILE &&
                        args.TileX == capture.TargetX &&
                        args.TileY == capture.TargetY;
                    LogInfo(
                        $"prefab spawn event observed: operationId={capture.OperationId}, phase={args.Phase}, playerId={args.PlayerId}, " +
                        $"building={args.Building}, target={args.TileX},{args.TileY}, scale={args.BuildingScale}, returnValue={args.ReturnValue}, matchesExpectedInput={matchesExpectedInput}.");

                    if (args.Phase == EventHookPhase.Post && matchesExpectedInput)
                    {
                        if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                        {
                            capture.InvalidPostEventCount++;
                        }
                        else
                        {
                            capture.RecordBuildingId((int)args.ReturnValue);
                            LogInfo($"prefab spawn id captured: operationId={capture.OperationId}, buildingId={args.ReturnValue}, capturedCount={capture.BuildingIds.Count}.");
                        }
                    }
                }

                TryQueueAIQuarry(args);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile building-spawn handling failed: exception={ex}");
            }
        }

        private void TryQueueAIQuarry(BuildingSpawnEventArgs args)
        {
            if (!IsAIAutomationActive() ||
                !mapActive ||
                !aiSpawnObservationArmed ||
                Shared.GameModeHelper.IsMapEditor() ||
                args.Phase != EventHookPhase.Post ||
                args.Building != eStructs.STRUCT_QUARRY ||
                args.ReturnValue <= 0 ||
                args.ReturnValue > int.MaxValue ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId))
            {
                return;
            }

            // In multiplayer only the host chooses and broadcasts the explicit target.
            if (RequiresChoreTransport() && !GameNetworkAPI.IsLocalHost())
                return;

            int quarryId = (int)args.ReturnValue;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out GameBuilding* quarry) ||
                quarry == null ||
                quarry->r_BuildingType != eStructs.STRUCT_QUARRY ||
                quarry->r_PlayerIdOwner != args.PlayerId ||
                quarry->r_GlobalId <= 0 ||
                quarry->r_GlobalId > int.MaxValue)
            {
                LogInfo($"AI quarry was not queued because its Post-spawn building could not be verified: playerId={args.PlayerId}, buildingId={quarryId}.");
                return;
            }

            int quarryGlobalId = (int)quarry->r_GlobalId;
            if (pendingAIQuarriesByGlobalId.ContainsKey(quarryGlobalId))
                return;

            long timeoutTicks = checked((long)Math.Ceiling(
                AIQuarryReadinessTimeoutSeconds * Stopwatch.Frequency));
            pendingAIQuarriesByGlobalId.Add(
                quarryGlobalId,
                new PendingAIQuarry(
                    quarryId,
                    quarryGlobalId,
                    args.PlayerId,
                    Stopwatch.GetTimestamp() + timeoutTicks));
            LogInfo($"AI quarry queued for Keep-facing pile placement: playerId={args.PlayerId}, quarryId={quarryId}, quarryGlobalId={quarryGlobalId}, timeoutSeconds={AIQuarryReadinessTimeoutSeconds:0.#}.");
        }

        private void OnGameTick(int tick)
        {
            if (mapActive && !aiSpawnObservationArmed)
            {
                aiSpawnObservationArmed = true;
                LogInfo($"AI quarry spawn observation armed after map/load initialization: tick={tick}.");
            }

            if (pendingAIQuarriesByGlobalId.Count == 0)
                return;

            try
            {
                if (!IsAIAutomationActive())
                {
                    pendingAIQuarriesByGlobalId.Clear();
                    return;
                }

                var quarryGlobalIds = new List<int>(pendingAIQuarriesByGlobalId.Keys);
                for (int index = 0; index < quarryGlobalIds.Count; index++)
                    TryProcessPendingAIQuarry(quarryGlobalIds[index], tick);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features AI quarry-pile tick processing failed: tick={tick}, exception={ex}");
            }
        }

        private void TryProcessPendingAIQuarry(int quarryGlobalId, int tick)
        {
            if (!pendingAIQuarriesByGlobalId.TryGetValue(quarryGlobalId, out PendingAIQuarry pending))
                return;

            if (Stopwatch.GetTimestamp() >= pending.DeadlineTimestamp)
            {
                pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extra Features AI quarry-pile placement timed out; the Vanilla pile remains unchanged: playerId={pending.PlayerId}, quarryId={pending.QuarryId}, quarryGlobalId={quarryGlobalId}, tick={tick}.");
                return;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(pending.QuarryId, out GameBuilding* quarry) ||
                quarry == null ||
                (int)quarry->r_GlobalId != quarryGlobalId ||
                quarry->r_BuildingType != eStructs.STRUCT_QUARRY ||
                quarry->r_PlayerIdOwner != pending.PlayerId ||
                quarry->r_AliveState == AliveState.MarkedForDeletion)
            {
                pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                LogInfo($"pending AI quarry discarded because the quarry no longer exists: playerId={pending.PlayerId}, quarryId={pending.QuarryId}, quarryGlobalId={quarryGlobalId}, tick={tick}.");
                return;
            }

            if (quarry->r_AliveState != AliveState.IsAlive ||
                !TryGetRelocatableQuarry(pending.QuarryId, pending.PlayerId, out quarry, out GameBuilding* oldPile, out _))
            {
                return;
            }

            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(pending.PlayerId);
            if (keepId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                keep == null ||
                keep->r_PlayerIdOwner != pending.PlayerId ||
                (keep->r_AliveState != AliveState.NeedsInit && keep->r_AliveState != AliveState.IsAlive))
            {
                return;
            }

            int operationId = NextOperationId();
            if (!TryFindNearestKeepTarget(
                pending.PlayerId,
                quarry,
                oldPile,
                keep,
                operationId,
                out PlacementPosition target,
                out bool currentPositionIsBest))
            {
                pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extra Features found no safe Keep-facing pile position for a new AI quarry; the Vanilla pile remains unchanged: playerId={pending.PlayerId}, quarryGlobalId={quarryGlobalId}, operationId={operationId}.");
                return;
            }

            pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
            if (currentPositionIsBest)
            {
                LogInfo($"AI quarry pile already occupies the valid position nearest to its Keep: playerId={pending.PlayerId}, quarryGlobalId={quarryGlobalId}, operationId={operationId}, target={target.X},{target.Y}.");
                return;
            }

            var operation = new QuarryPileRelocationOperation
            {
                PlayerId = pending.PlayerId,
                OperationId = operationId,
                QuarryGlobalId = quarryGlobalId,
                OldPileGlobalId = (int)oldPile->r_GlobalId,
                TargetTileX = target.X,
                TargetTileY = target.Y
            };

            bool applied = RequiresChoreTransport()
                ? TrySendRotationChore(operation)
                : TryApplyRotation(operation, "singleplayer-ai-keep-facing", targetAlreadyValidated: true);
            if (!applied)
            {
                RememberFailedRotationTarget(operation, "ai-keep-facing-transaction-failed");
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extra Features could not move a new AI quarry pile towards its Keep; the existing pile was retained: playerId={pending.PlayerId}, quarryGlobalId={quarryGlobalId}, operationId={operationId}.");
            }
        }

        private void OnLinkedBuildingRemoval(int buildingId, string source)
        {
            try
            {
                if (!IsLinkedRemovalActive())
                    return;

                if (linkedRemovalSuppressionDepth > 0)
                {
                    LogInfo($"linked demolition propagation suppressed for internal operation: source={source}, buildingId={buildingId}, suppressionDepth={linkedRemovalSuppressionDepth}.");
                    return;
                }

                if (buildingId <= 0 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                {
                    return;
                }

                if (building->r_BuildingType == eStructs.STRUCT_QUARRY)
                {
                    int pileId = building->r_StoneQuarry_StockPileBuildingId;
                    if (pileId <= 0 ||
                        !GameBuildingManagerAPI.Instance.TryGetBuildingById(pileId, out GameBuilding* pile) ||
                        pile->r_BuildingType != eStructs.STRUCT_QUARRYPILE)
                    {
                        return;
                    }

                    int pileGlobalId = (int)pile->r_GlobalId;
                    int quarryGlobalId = (int)building->r_GlobalId;
                    if (pile->r_PlayerIdOwner != building->r_PlayerIdOwner)
                        return;

                    ClearPileContentBeforeDeletion(pileId, pile, 0, source + "-linked-pile");
                    bool pileMarkedForDeletion = pile->r_AliveState == AliveState.MarkedForDeletion ||
                        DeleteBuildingWithoutLinkedPropagation(pileId, source + "-paired-pile");
                    LogInfo($"linked demolition propagated from quarry to pile: source={source}, quarryId={buildingId}, quarryGlobalId={quarryGlobalId}, pileId={pileId}, pileGlobalId={pileGlobalId}, pileMarkedForDeletion={pileMarkedForDeletion}.");
                    return;
                }

                if (building->r_BuildingType != eStructs.STRUCT_QUARRYPILE)
                    return;

                int removedPileGlobalId = (int)building->r_GlobalId;
                int linkedQuarryId = FindAliveQuarryIdByPileId(buildingId, building->r_PlayerIdOwner);
                if (linkedQuarryId <= 0 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(linkedQuarryId, out GameBuilding* linkedQuarry))
                {
                    return;
                }

                int linkedQuarryGlobalId = (int)linkedQuarry->r_GlobalId;
                ClearPileContentBeforeDeletion(buildingId, building, 0, source + "-linked-pile");

                bool quarryMarkedForDeletion = DeleteBuildingWithoutLinkedPropagation(linkedQuarryId, source + "-paired-quarry");

                LogInfo($"linked demolition propagated from pile to quarry: source={source}, pileId={buildingId}, pileGlobalId={removedPileGlobalId}, quarryId={linkedQuarryId}, quarryGlobalId={linkedQuarryGlobalId}, quarryMarkedForDeletion={quarryMarkedForDeletion}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile linked demolition handling failed: source={source}, buildingId={buildingId}, exception={ex}");
            }
        }

        private bool IsRuntimeActive()
        {
            return settings.EnableMod &&
                (settings.EnableQuarryPileRelocation || settings.EnableAIQuarryPileTowardsKeep);
        }

        private bool IsManualFeatureActive()
        {
            return settings.EnableMod && settings.EnableQuarryPileRelocation;
        }

        private bool IsAIAutomationActive()
        {
            return settings.EnableMod && settings.EnableAIQuarryPileTowardsKeep;
        }

        private bool IsLinkedRemovalActive()
        {
            // Linked quarry/pile demolition is a separate state-changing path and remains
            // multiplayer-gated until it is independently carried or proven deterministic.
            return IsRuntimeActive() && !multiplayerFeatureGate.BlocksLocalStateChanges;
        }

        private bool RequiresChoreTransport()
        {
            // Detection failures deliberately take the multiplayer path and therefore fail closed.
            return multiplayerFeatureGate.BlocksLocalStateChanges;
        }

        private bool IsChoreTransportReady()
        {
            return networkInitialized &&
                relocationPacketHook != null &&
                ChoreNetworkTransport.IsAvailable;
        }

        private bool TrySendRotationChore(QuarryPileRelocationOperation operation)
        {
            if (!IsChoreTransportReady())
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile rotation refused in multiplayer because the Chore transport is unavailable: operationId={operation.OperationId}.");
                return false;
            }

            var packet = new QuarryPileRelocationPacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                PlayerId = operation.PlayerId,
                OperationId = operation.OperationId,
                QuarryGlobalId = operation.QuarryGlobalId,
                OldPileGlobalId = operation.OldPileGlobalId,
                TargetTileX = operation.TargetTileX,
                TargetTileY = operation.TargetTileY
            };

            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(relocationPacketHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);

            // Call the Chore transport directly so failure cannot silently fall back to the
            // non-tick-aligned Steam transport used by SendPacketToAllEx2.
            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            bool queued = sendRawBlob != null && sendRawBlob(blob);
            if (!queued)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile Chore was not queued; no local action was applied: operationId={operation.OperationId}, payloadBytes={blob.Length}.");
                return false;
            }

            LogInfo($"rotation Chore queued: operationId={operation.OperationId}, packetId={relocationPacketHook.GetPacketId()}, payloadBytes={blob.Length}, quarryGlobalId={operation.QuarryGlobalId}, oldPileGlobalId={operation.OldPileGlobalId}, target={operation.TargetTileX},{operation.TargetTileY}.");
            return true;
        }

        private void OnRelocationPacketReceived(ReceiveCustomPacketEventArgs<QuarryPileRelocationPacket> args)
        {
            QuarryPileRelocationPacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features rejected a quarry-pile Chore with an unsupported payload: protocolVersion={packet?.ProtocolVersion.ToString() ?? "null"}.");
                return;
            }

            var operation = new QuarryPileRelocationOperation
            {
                PlayerId = packet.PlayerId,
                OperationId = packet.OperationId,
                QuarryGlobalId = packet.QuarryGlobalId,
                OldPileGlobalId = packet.OldPileGlobalId,
                TargetTileX = packet.TargetTileX,
                TargetTileY = packet.TargetTileY
            };

            try
            {
                LogInfo($"rotation Chore received: operationId={operation.OperationId}, playerId={operation.PlayerId}, quarryGlobalId={operation.QuarryGlobalId}, oldPileGlobalId={operation.OldPileGlobalId}, target={operation.TargetTileX},{operation.TargetTileY}.");
                if (!initialized || setupBuildingEntrancesOffset == null)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Extra Features cannot execute quarry-pile Chore because the relocation runtime is unavailable: operationId={operation.OperationId}, initialized={initialized}, nativeCandidateHelperAvailable={setupBuildingEntrancesOffset != null}.");
                    return;
                }

                if (!TryApplyRotation(operation, "multiplayer-chore", targetAlreadyValidated: false))
                {
                    RememberFailedRotationTarget(operation, "multiplayer-chore-transaction-failed");
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Extra Features quarry-pile Chore completed without relocation: operationId={operation.OperationId}, target={operation.TargetTileX},{operation.TargetTileY}.");
                    return;
                }

                LogInfo($"rotation Chore executed successfully: operationId={operation.OperationId}.");
            }
            catch (Exception ex)
            {
                RememberFailedRotationTarget(operation, "multiplayer-chore-exception");
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile Chore execution failed: operationId={operation.OperationId}, exception={ex}");
            }
            finally
            {
                RefreshButtonVisibility();
            }
        }

        private bool TryApplyRotation(
            QuarryPileRelocationOperation operation,
            string reason,
            bool targetAlreadyValidated = false)
        {
            LogInfo($"replacement transaction started: reason={reason}, playerId={operation.PlayerId}, operationId={operation.OperationId}, quarryGlobalId={operation.QuarryGlobalId}, oldPileGlobalId={operation.OldPileGlobalId}, requestedTarget={operation.TargetTileX},{operation.TargetTileY}.");

            int quarryId = FindAliveBuildingIdByGlobalId(operation.QuarryGlobalId);
            if (quarryId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out GameBuilding* quarry) ||
                !IsAliveBuilding(quarry, eStructs.STRUCT_QUARRY, operation.PlayerId))
            {
                LogInfo($"replacement transaction rejected: reason=quarry-not-found, operationId={operation.OperationId}.");
                return false;
            }

            int oldPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            if (oldPileId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(oldPileId, out GameBuilding* oldPile) ||
                !IsAliveBuilding(oldPile, eStructs.STRUCT_QUARRYPILE, operation.PlayerId) ||
                (int)oldPile->r_GlobalId != operation.OldPileGlobalId)
            {
                LogInfo($"replacement transaction rejected: reason=old-pile-link-changed, quarryId={quarryId}, currentLinkedPileId={oldPileId}, operationId={operation.OperationId}.");
                return false;
            }

            PlacementPosition expectedTarget;
            if (targetAlreadyValidated)
            {
                expectedTarget = new PlacementPosition(operation.TargetTileX, operation.TargetTileY);
                LogInfo($"replacement transaction reuses synchronously validated local target: operationId={operation.OperationId}, target={expectedTarget.X},{expectedTarget.Y}.");
            }
            else
            {
                expectedTarget = new PlacementPosition(operation.TargetTileX, operation.TargetTileY);
                if (!ValidateRequestedRotationTarget(
                    operation.PlayerId,
                    quarry,
                    oldPile,
                    expectedTarget,
                    operation.OperationId))
                {
                    LogInfo($"replacement transaction rejected: reason=requested-target-invalid, operationId={operation.OperationId}, target={expectedTarget.X},{expectedTarget.Y}.");
                    return false;
                }
            }

            PileContentSnapshot content = PileContentSnapshot.Capture(oldPile);
            short previousCurrentHealth = oldPile->r_CurrentHealth;
            ushort previousMaxHealth = oldPile->r_MaxHealth;
            if (!TrySpawnReplacement(
                operation.PlayerId,
                quarryId,
                quarry,
                oldPileId,
                oldPile,
                expectedTarget,
                operation.OperationId,
                out int newPileId,
                out GameBuilding* newPile))
            {
                return false;
            }

            if (newPileId > ushort.MaxValue)
            {
                DeleteBuildingWithoutLinkedPropagation(newPileId, "rotation-new-id-overflow");
                LogInfo($"replacement transaction rolled back: reason=new-pile-id-exceeds-link-field, operationId={operation.OperationId}, newPileId={newPileId}.");
                return false;
            }

            int newPileGlobalId = (int)newPile->r_GlobalId;
            content.ApplyTo(newPile);
            newPile->r_CurrentHealth = previousCurrentHealth;
            newPile->r_MaxHealth = previousMaxHealth;
            ClearPileContentBeforeDeletion(oldPileId, oldPile, operation.OperationId, "rotation-old-pile");

            quarry->r_StoneQuarry_StockPileBuildingId = checked((ushort)newPileId);
            bool oldPileMarkedForDeletion = oldPile->r_AliveState == AliveState.MarkedForDeletion ||
                DeleteBuildingWithoutLinkedPropagation(oldPileId, "rotation-old-pile");
            if (!oldPileMarkedForDeletion)
            {
                quarry->r_StoneQuarry_StockPileBuildingId = checked((ushort)oldPileId);
                content.ApplyTo(oldPile);
                newPile->r_StoneBlocksAmount = 0;
                newPile->r_CurrentGoodStackAmount = 0;
                bool replacementMarkedForDeletion = DeleteBuildingWithoutLinkedPropagation(newPileId, "rotation-rollback-new-pile");
                LogInfo($"replacement transaction rolled back: reason=old-pile-delete-failed, operationId={operation.OperationId}, oldPileId={oldPileId}, newPileId={newPileId}, replacementMarkedForDeletion={replacementMarkedForDeletion}.");
                return false;
            }

            ClearFailedRotationTargets(operation.QuarryGlobalId, "rotation-completed");
            LogInfo(
                $"rotation completed: reason={reason}, playerId={operation.PlayerId}, operationId={operation.OperationId}, quarryId={quarryId}, " +
                $"quarryGlobalId={operation.QuarryGlobalId}, oldPileId={oldPileId}, oldPileGlobalId={operation.OldPileGlobalId}, newPileId={newPileId}, " +
                $"newPileGlobalId={newPileGlobalId}, newPileState={newPile->r_AliveState}, target={expectedTarget.X},{expectedTarget.Y}, " +
                $"stoneBlocks={newPile->r_StoneBlocksAmount}, oldPileMarkedForDeletion={oldPileMarkedForDeletion}, visualLifecycle=prefab-managed.");
            return true;
        }

        private bool TryFindNextRotationTarget(
            int playerId,
            GameBuilding* quarry,
            GameBuilding* oldPile,
            int operationId,
            out PlacementPosition target)
        {
            target = default;
            if (setupBuildingEntrancesOffset == null)
            {
                LogInfo($"rotation target search failed: operationId={operationId}, reason=Vanilla-candidate-helper-unavailable.");
                return false;
            }

            int quarryScale = GetBuildingScale(quarry);
            int buildingScale = GetBuildingScale(oldPile);
            if (quarryScale != VanillaQuarryScale || buildingScale != VanillaPileScale)
            {
                LogInfo(
                    $"rotation target search failed: operationId={operationId}, reason=unexpected-Vanilla-scales, " +
                    $"quarryScale={quarryScale}, expectedQuarryScale={VanillaQuarryScale}, pileScale={buildingScale}, expectedPileScale={VanillaPileScale}.");
                return false;
            }

            int quarryGlobalId = (int)quarry->r_GlobalId;
            int oldPileGlobalId = (int)oldPile->r_GlobalId;
            int oldX = oldPile->r_TilePositionXBegin;
            int oldY = oldPile->r_TilePositionYBegin;
            if (!TryResolveVanillaCursor(
                quarry,
                oldX,
                oldY,
                out int currentIndex,
                out int currentPlacementTry,
                out bool exactCurrentPosition))
            {
                LogInfo($"rotation target search failed: operationId={operationId}, reason=Vanilla-cursor-resolution-failed.");
                return false;
            }

            HashSet<long> failedTargets = GetFailedRotationTargets(quarryGlobalId, oldPileGlobalId);
            int failedTargetSkipCount = 0;
            int candidateAttemptCount = 0;

            LogInfo(
                $"rotation target search started: operationId={operationId}, playerId={playerId}, quarryGlobalId={quarryGlobalId}, " +
                $"oldPileGlobalId={oldPileGlobalId}, quarryAnchor={quarry->r_TilePositionXBegin},{quarry->r_TilePositionYBegin}, " +
                $"oldAnchor={oldX},{oldY}, currentVanillaIndex={currentIndex}, currentVanillaTry={currentPlacementTry}, " +
                $"exactCurrentPosition={exactCurrentPosition}, candidatesPerTry={VanillaCandidateCount}, " +
                $"placementTries={VanillaMinimumPlacementTry}-{VanillaMaximumPlacementTry}, failedTargetCount={failedTargets?.Count ?? 0}.");

            // Vanilla's perimeter indexes increase clockwise. Always exhaust the closest Vanilla distance first,
            // but begin immediately after the current angular index so a vacated position is not selected again.
            for (int placementTry = VanillaMinimumPlacementTry;
                placementTry <= VanillaMaximumPlacementTry;
                placementTry++)
            {
                for (int clockwiseOffset = 1; clockwiseOffset <= VanillaCandidateCount; clockwiseOffset++)
                {
                    int candidateIndex = (currentIndex + clockwiseOffset) % VanillaCandidateCount;
                    if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                    {
                        LogInfo(
                            $"rotation target search failed: operationId={operationId}, reason=Vanilla-candidate-generation-failed, " +
                            $"vanillaTry={placementTry}, candidateIndex={candidateIndex}.");
                        return false;
                    }

                    candidateAttemptCount++;

                    if (candidate.X == oldX && candidate.Y == oldY)
                    {
                        LogInfo(
                            $"rotation candidate skipped: operationId={operationId}, vanillaTry={placementTry}, candidateIndex={candidateIndex}, " +
                            $"target={candidate.X},{candidate.Y}, reason=current-pile-position.");
                        continue;
                    }

                    if (failedTargets != null && failedTargets.Contains(GetPositionKey(candidate)))
                    {
                        failedTargetSkipCount++;
                        LogInfo(
                            $"rotation candidate skipped: operationId={operationId}, vanillaTry={placementTry}, candidateIndex={candidateIndex}, " +
                            $"target={candidate.X},{candidate.Y}, reason=previous-spawn-failure.");
                        continue;
                    }

                    if (!ValidateCandidateWithGame(
                        playerId,
                        candidate,
                        buildingScale,
                        operationId,
                        candidateIndex,
                        placementTry))
                    {
                        continue;
                    }

                    target = candidate;
                    LogInfo(
                        $"rotation target selected: operationId={operationId}, vanillaTry={placementTry}, candidateIndex={candidateIndex}, " +
                        $"target={target.X},{target.Y}, clockwiseOffset={clockwiseOffset}, totalCandidateAttempts={candidateAttemptCount}, " +
                        $"previousFailedTargetsSkipped={failedTargetSkipCount}.");
                    return true;
                }
            }

            LogInfo(
                $"rotation target search exhausted: operationId={operationId}, playerId={playerId}, totalCandidateAttempts={candidateAttemptCount}, " +
                $"previousFailedTargetsSkipped={failedTargetSkipCount}, maximumVanillaTry={VanillaMaximumPlacementTry}.");

            return false;
        }

        private bool TryFindNearestKeepTarget(
            int playerId,
            GameBuilding* quarry,
            GameBuilding* oldPile,
            GameBuilding* keep,
            int operationId,
            out PlacementPosition target,
            out bool currentPositionIsBest)
        {
            target = default;
            currentPositionIsBest = false;
            if (setupBuildingEntrancesOffset == null || quarry == null || oldPile == null || keep == null)
                return false;

            int quarryScale = GetBuildingScale(quarry);
            int pileScale = GetBuildingScale(oldPile);
            if (quarryScale != VanillaQuarryScale || pileScale != VanillaPileScale)
            {
                LogInfo(
                    $"AI Keep-facing target search failed: operationId={operationId}, reason=unexpected-Vanilla-scales, " +
                    $"quarryScale={quarryScale}, pileScale={pileScale}.");
                return false;
            }

            int oldX = oldPile->r_TilePositionXBegin;
            int oldY = oldPile->r_TilePositionYBegin;
            if (!TryResolveVanillaCursor(
                quarry,
                oldX,
                oldY,
                out int currentIndex,
                out int currentPlacementTry,
                out bool exactCurrentPosition))
            {
                return false;
            }

            var candidates = new List<QuarryPileTargetCandidate>();
            var seenPositions = new HashSet<long>();
            var currentPosition = new PlacementPosition(oldX, oldY);
            candidates.Add(new QuarryPileTargetCandidate(
                oldX,
                oldY,
                exactCurrentPosition ? currentPlacementTry : int.MaxValue,
                exactCurrentPosition ? currentIndex : int.MaxValue,
                isCurrentPosition: true));
            seenPositions.Add(GetPositionKey(currentPosition));

            HashSet<long> failedTargets = GetFailedRotationTargets(
                (int)quarry->r_GlobalId,
                (int)oldPile->r_GlobalId);
            int generatedCount = 0;
            int validReplacementCount = 0;
            for (int placementTry = VanillaMinimumPlacementTry;
                placementTry <= VanillaMaximumPlacementTry;
                placementTry++)
            {
                for (int candidateIndex = 0; candidateIndex < VanillaCandidateCount; candidateIndex++)
                {
                    if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                        return false;

                    generatedCount++;
                    long positionKey = GetPositionKey(candidate);
                    if (!seenPositions.Add(positionKey) ||
                        (failedTargets != null && failedTargets.Contains(positionKey)) ||
                        !ValidateCandidateWithGame(
                            playerId,
                            candidate,
                            pileScale,
                            operationId,
                            candidateIndex,
                            placementTry,
                            logResult: false))
                    {
                        continue;
                    }

                    candidates.Add(new QuarryPileTargetCandidate(
                        candidate.X,
                        candidate.Y,
                        placementTry,
                        candidateIndex,
                        isCurrentPosition: false));
                    validReplacementCount++;
                }
            }

            int keepCenterXTimesTwo = keep->r_TilePositionXBegin + keep->r_TilePositionXEnd;
            int keepCenterYTimesTwo = keep->r_TilePositionYBegin + keep->r_TilePositionYEnd;
            if (!QuarryPileTargetSelectionPolicy.TrySelectNearest(
                candidates,
                keepCenterXTimesTwo,
                keepCenterYTimesTwo,
                out QuarryPileTargetCandidate selected))
            {
                return false;
            }

            target = new PlacementPosition(selected.X, selected.Y);
            currentPositionIsBest = selected.IsCurrentPosition;
            LogInfo(
                $"AI Keep-facing target selected: operationId={operationId}, playerId={playerId}, quarryGlobalId={quarry->r_GlobalId}, " +
                $"keepGlobalId={keep->r_GlobalId}, keepCenterTimesTwo={keepCenterXTimesTwo},{keepCenterYTimesTwo}, " +
                $"oldTarget={oldX},{oldY}, selectedTarget={selected.X},{selected.Y}, currentPositionIsBest={currentPositionIsBest}, " +
                $"vanillaTry={selected.PlacementTry}, candidateIndex={selected.CandidateIndex}, generatedCandidates={generatedCount}, " +
                $"validReplacementCandidates={validReplacementCount}.");
            return true;
        }

        private bool ValidateRequestedRotationTarget(
            int playerId,
            GameBuilding* quarry,
            GameBuilding* oldPile,
            PlacementPosition target,
            int operationId)
        {
            if (setupBuildingEntrancesOffset == null)
                return false;

            int quarryScale = GetBuildingScale(quarry);
            int buildingScale = GetBuildingScale(oldPile);
            if (quarryScale != VanillaQuarryScale || buildingScale != VanillaPileScale)
                return false;

            int targetCandidateIndex = -1;
            int targetPlacementTry = -1;
            for (int placementTry = VanillaMinimumPlacementTry;
                placementTry <= VanillaMaximumPlacementTry && targetPlacementTry < 0;
                placementTry++)
            {
                for (int candidateIndex = 0; candidateIndex < VanillaCandidateCount; candidateIndex++)
                {
                    if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                        return false;

                    if (candidate.X != target.X || candidate.Y != target.Y)
                        continue;

                    targetCandidateIndex = candidateIndex;
                    targetPlacementTry = placementTry;
                    break;
                }
            }

            if (targetPlacementTry < 0)
            {
                LogInfo(
                    $"requested rotation target rejected: operationId={operationId}, target={target.X},{target.Y}, " +
                    $"reason=not-a-Vanilla-quarry-pile-candidate.");
                return false;
            }

            LogInfo(
                $"requested rotation target matched Vanilla candidate: operationId={operationId}, target={target.X},{target.Y}, " +
                $"vanillaTry={targetPlacementTry}, candidateIndex={targetCandidateIndex}.");
            return ValidateCandidateWithGame(
                playerId,
                target,
                buildingScale,
                operationId,
                targetCandidateIndex,
                targetPlacementTry);
        }

        private bool TryResolveVanillaCursor(
            GameBuilding* quarry,
            int oldX,
            int oldY,
            out int currentIndex,
            out int currentPlacementTry,
            out bool exactCurrentPosition)
        {
            currentIndex = VanillaCandidateCount / 4;
            currentPlacementTry = 0;
            exactCurrentPosition = false;
            long nearestDistanceSquared = long.MaxValue;

            for (int placementTry = VanillaMinimumPlacementTry;
                placementTry <= VanillaMaximumPlacementTry;
                placementTry++)
            {
                for (int candidateIndex = 0; candidateIndex < VanillaCandidateCount; candidateIndex++)
                {
                    if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                        return false;

                    if (candidate.X == oldX && candidate.Y == oldY)
                    {
                        currentIndex = candidateIndex;
                        currentPlacementTry = placementTry;
                        exactCurrentPosition = true;
                        return true;
                    }

                    if (placementTry != VanillaMinimumPlacementTry)
                        continue;

                    long dx = candidate.X - (long)oldX;
                    long dy = candidate.Y - (long)oldY;
                    long distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= nearestDistanceSquared)
                        continue;

                    currentIndex = candidateIndex;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return true;
        }

        private bool TryGetVanillaCandidate(
            GameBuilding* quarry,
            int candidateIndex,
            int placementTry,
            out PlacementPosition candidate)
        {
            candidate = default;
            if (quarry == null ||
                setupBuildingEntrancesOffset == null ||
                candidateIndex < 0 ||
                candidateIndex >= VanillaCandidateCount ||
                placementTry < VanillaMinimumPlacementTry ||
                placementTry > VanillaMaximumPlacementTry)
            {
                return false;
            }

            NativePointer<GameBuildingManager> buildingManager = GameBuildingManagerAPI.Instance.GetBuildingManager();
            GameBuildingManager* buildingManagerPointer = buildingManager;
            if (buildingManagerPointer == null)
                return false;

            int* relativeXPointer = (int*)((byte*)buildingManagerPointer + VanillaCandidateOffsetX);
            int* relativeYPointer = (int*)((byte*)buildingManagerPointer + VanillaCandidateOffsetY);
            int previousRelativeX = *relativeXPointer;
            int previousRelativeY = *relativeYPointer;
            try
            {
                setupBuildingEntrancesOffset(
                    buildingManager,
                    VanillaQuarryScale,
                    VanillaPileScale,
                    candidateIndex,
                    placementTry);

                int relativeX = *relativeXPointer;
                int relativeY = *relativeYPointer;
                candidate = new PlacementPosition(
                    quarry->r_TilePositionXBegin + relativeX,
                    quarry->r_TilePositionYBegin + relativeY);
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile Vanilla candidate generation failed: candidateIndex={candidateIndex}, " +
                    $"placementTry={placementTry}: {ex}");
                return false;
            }
            finally
            {
                *relativeXPointer = previousRelativeX;
                *relativeYPointer = previousRelativeY;
            }
        }

        private bool ValidateCandidateWithGame(
            int playerId,
            PlacementPosition candidate,
            int buildingScale,
            int operationId,
            int candidateIndex,
            int placementTry,
            bool logResult = true)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            bool previousBlockedState = tileApi.TileManager.IsPlacementBlocked;
            try
            {
                tileApi.TileManager.IsPlacementBlocked = false;
                long validatorResult = BulkBuildingDetours.c_game_player_build_placement_validator_hook_impl(
                    tileApi.GetTileManager(),
                    playerId,
                    candidate.X,
                    candidate.Y,
                    eMappers.MAPPER_QUARRYPILE,
                    buildingScale,
                    0);
                bool blocked = tileApi.TileManager.IsPlacementBlocked;
                if (logResult)
                {
                    LogInfo($"native placement validation returned: operationId={operationId}, vanillaTry={placementTry}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}, buildingScale={buildingScale}, returnValue={validatorResult}, blocked={blocked}.");
                }
                return !blocked;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile native placement validation failed: operationId={operationId}, vanillaTry={placementTry}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}: {ex}");
                return false;
            }
            finally
            {
                tileApi.TileManager.IsPlacementBlocked = previousBlockedState;
            }
        }

        private bool TrySpawnReplacement(
            int playerId,
            int quarryId,
            GameBuilding* quarry,
            int oldPileId,
            GameBuilding* oldPile,
            PlacementPosition target,
            int operationId,
            out int newPileId,
            out GameBuilding* newPile)
        {
            newPileId = 0;
            newPile = null;
            int buildingScale = GetBuildingScale(oldPile);
            if (buildingScale <= 0)
            {
                LogInfo($"prefab replacement spawn rejected: operationId={operationId}, reason=invalid-pile-grid-size, gridSize={oldPile->r_OccupyTileGridSize}.");
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int targetTileId = tileApi.GetTileId(target.X, target.Y);
            ushort targetStructureBefore = tileApi.GetTileBuildingId(targetTileId);
            ushort quarryLinkBefore = quarry->r_StoneQuarry_StockPileBuildingId;
            PrefabSpawnCapture capture = new PrefabSpawnCapture(operationId, playerId, target.X, target.Y);
            if (activePrefabSpawnCapture != null)
            {
                LogInfo($"prefab replacement spawn rejected: operationId={operationId}, reason=another-prefab-capture-active, activeOperationId={activePrefabSpawnCapture.OperationId}.");
                return false;
            }

            long result = 0;
            Exception prefabException = null;
            LogInfo(
                $"prefab replacement spawn started: operationId={operationId}, playerId={playerId}, quarryId={quarryId}, oldPileId={oldPileId}, " +
                $"target={target.X},{target.Y}, targetTileId={targetTileId}, targetStructureBefore={targetStructureBefore}, buildingScale={buildingScale}, " +
                $"mapper={eMappers.MAPPER_QUARRYPILE}, isFree=True, bypassPlacementRules=True, quarryLinkBefore={quarryLinkBefore}.");
            activePrefabSpawnCapture = capture;
            try
            {
                linkedRemovalSuppressionDepth++;
                try
                {
                    result = GameBuildingManagerAPI.Instance.CreatePrefab(
                        playerId,
                        target.X,
                        target.Y,
                        eMappers.MAPPER_QUARRYPILE,
                        buildingScale,
                        0,
                        true,
                        true);
                }
                finally
                {
                    linkedRemovalSuppressionDepth--;
                }
            }
            catch (Exception ex)
            {
                prefabException = ex;
            }
            finally
            {
                activePrefabSpawnCapture = null;
            }

            ushort targetStructureAfter = tileApi.GetTileBuildingId(targetTileId);
            ushort quarryLinkAfter = quarry->r_StoneQuarry_StockPileBuildingId;
            LogInfo(
                $"prefab replacement spawn returned: operationId={operationId}, returnValue={result}, capturedIds={capture.DescribeBuildingIds()}, " +
                $"invalidPostEvents={capture.InvalidPostEventCount}, targetStructureAfter={targetStructureAfter}, quarryLinkAfter={quarryLinkAfter}, " +
                $"oldPileStateAfter={oldPile->r_AliveState}, exception={(prefabException == null ? "none" : prefabException.GetType().FullName)}.");

            if (prefabException != null)
            {
                int fallbackPileId = FindFreshPileAtTarget(oldPileId, playerId, target, out _);
                CleanupFailedPrefabSpawns(capture, oldPileId, operationId, "prefab-exception", fallbackPileId);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features quarry-pile prefab replacement spawn failed: operationId={operationId}, fallbackPileId={fallbackPileId}, exception={prefabException}");
                return false;
            }

            bool capturedReplacementResolved = TryResolveCapturedReplacement(
                capture,
                oldPileId,
                playerId,
                target,
                oldPile->r_OccupyTileGridSize,
                out newPileId,
                out newPile);
            if (!capturedReplacementResolved)
            {
                newPileId = FindFreshPileAtTarget(oldPileId, playerId, target, out newPile);
                LogInfo($"prefab replacement fallback scan completed: operationId={operationId}, fallbackPileId={newPileId}, actual={DescribeBuilding(newPile)}.");
            }

            bool replacementVerified = newPileId > 0 &&
                IsValidFreshSpawn(newPile, eStructs.STRUCT_QUARRYPILE, playerId) &&
                MatchesSpawnAnchor(newPile->r_TilePositionXBegin, newPile->r_TilePositionYBegin, target) &&
                newPile->r_OccupyTileGridSize == oldPile->r_OccupyTileGridSize;
            if (!replacementVerified)
            {
                LogInfo(
                    $"prefab replacement spawn verification failed before cleanup: operationId={operationId}, returnValue={result}, newPileId={newPileId}, " +
                    $"expectedType={eStructs.STRUCT_QUARRYPILE}, expectedOwner={playerId}, expectedAnchor={target.X},{target.Y}, expectedGridSize={oldPile->r_OccupyTileGridSize}, " +
                    $"capturedIds={capture.DescribeBuildingIds()}, actual={DescribeBuilding(newPile)}.");
                CleanupFailedPrefabSpawns(capture, oldPileId, operationId, "verification-failed", newPileId);
                newPile = null;
                newPileId = 0;
                return false;
            }

            LogInfo(
                $"prefab replacement spawn verified: operationId={operationId}, newPileId={newPileId}, newPileGlobalId={newPile->r_GlobalId}, " +
                $"tiles={newPile->r_TilePositionXBegin},{newPile->r_TilePositionYBegin}-{newPile->r_TilePositionXEnd},{newPile->r_TilePositionYEnd}, " +
                $"gridSize={newPile->r_OccupyTileGridSize}, tileRegistration={DescribeTileRegistration(newPileId, newPile)}, " +
                $"quarryLinkAfterPrefab={quarry->r_StoneQuarry_StockPileBuildingId}, oldPileStateAfterPrefab={oldPile->r_AliveState}.");
            return true;
        }

        private bool TryResolveCapturedReplacement(
            PrefabSpawnCapture capture,
            int oldPileId,
            int playerId,
            PlacementPosition target,
            uint expectedGridSize,
            out int newPileId,
            out GameBuilding* newPile)
        {
            newPileId = 0;
            newPile = null;
            for (int index = 0; index < capture.BuildingIds.Count; index++)
            {
                int candidateId = capture.BuildingIds[index];
                if (candidateId == oldPileId ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(candidateId, out GameBuilding* candidate) ||
                    !IsValidFreshSpawn(candidate, eStructs.STRUCT_QUARRYPILE, playerId))
                {
                    continue;
                }

                bool matchesTarget = MatchesSpawnAnchor(
                        candidate->r_TilePositionXBegin,
                        candidate->r_TilePositionYBegin,
                        target) &&
                    candidate->r_OccupyTileGridSize == expectedGridSize;
                LogInfo($"captured prefab candidate inspected: operationId={capture.OperationId}, candidateId={candidateId}, matchesTarget={matchesTarget}, actual={DescribeBuilding(candidate)}.");
                if (!matchesTarget)
                    continue;

                newPileId = candidateId;
                newPile = candidate;
                return true;
            }

            return false;
        }

        private static int FindFreshPileAtTarget(
            int oldPileId,
            int playerId,
            PlacementPosition target,
            out GameBuilding* pile)
        {
            pile = null;
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                int buildingId = index + 1;
                if (buildingId == oldPileId)
                    continue;

                ref GameBuilding building = ref buildings[index];
                if ((building.r_AliveState == AliveState.NeedsInit || building.r_AliveState == AliveState.IsAlive) &&
                    building.r_BuildingType == eStructs.STRUCT_QUARRYPILE &&
                    building.r_PlayerIdOwner == playerId &&
                    MatchesSpawnAnchor(
                        building.r_TilePositionXBegin,
                        building.r_TilePositionYBegin,
                        target))
                {
                    if (GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out pile))
                        return buildingId;
                }
            }

            return 0;
        }

        private static bool MatchesSpawnAnchor(int tileXBegin, int tileYBegin, PlacementPosition target)
        {
            // CreatePrefab stores its requested placement anchor in Begin. End describes the footprint
            // direction and can therefore be smaller than Begin for candidates above or left of a quarry.
            return tileXBegin == target.X && tileYBegin == target.Y;
        }

        private void CleanupFailedPrefabSpawns(
            PrefabSpawnCapture capture,
            int oldPileId,
            int operationId,
            string reason,
            int additionalBuildingId = 0)
        {
            HashSet<int> cleanupIds = new HashSet<int>(capture.BuildingIds);
            if (additionalBuildingId > 0)
                cleanupIds.Add(additionalBuildingId);

            foreach (int buildingId in cleanupIds)
            {
                if (buildingId <= 0 || buildingId == oldPileId)
                    continue;

                bool markedForDeletion = DeleteBuildingWithoutLinkedPropagation(buildingId, "prefab-cleanup-" + reason);
                LogInfo($"invalid prefab replacement cleanup completed: operationId={operationId}, reason={reason}, buildingId={buildingId}, markedForDeletion={markedForDeletion}.");
            }
        }

        private static string DescribeTileRegistration(int buildingId, GameBuilding* building)
        {
            if (buildingId <= 0 || building == null)
                return "unavailable";

            int minX = Math.Min(building->r_TilePositionXBegin, building->r_TilePositionXEnd);
            int maxX = Math.Max(building->r_TilePositionXBegin, building->r_TilePositionXEnd);
            int minY = Math.Min(building->r_TilePositionYBegin, building->r_TilePositionYEnd);
            int maxY = Math.Max(building->r_TilePositionYBegin, building->r_TilePositionYEnd);
            int registered = 0;
            int occupiedByOther = 0;
            int empty = 0;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    ushort tileBuildingId = tileApi.GetTileBuildingId(tileApi.GetTileId(x, y));
                    if (tileBuildingId == buildingId)
                        registered++;
                    else if (tileBuildingId == 0)
                        empty++;
                    else
                        occupiedByOther++;
                }
            }

            return $"registered={registered},empty={empty},other={occupiedByOther},total={(maxX - minX + 1) * (maxY - minY + 1)}";
        }

        private void ClearPileContentBeforeDeletion(
            int pileId,
            GameBuilding* pile,
            int operationId,
            string stage)
        {
            if (pile == null)
                return;

            int pileGlobalId = (int)pile->r_GlobalId;
            uint previousStoneBlocks = pile->r_StoneBlocksAmount;
            uint previousGoodStack = pile->r_CurrentGoodStackAmount;
            pile->r_StoneBlocksAmount = 0;
            pile->r_CurrentGoodStackAmount = 0;

            // CreatePrefab/DeleteBuildingSafe own the tile and visual lifecycle. Only clear the transferred
            // resource state here; forcing the raw visual APIs is ineffective for these prefab instances.
            LogInfo($"pile content cleared before prefab-managed deletion: stage={stage}, operationId={operationId}, pileId={pileId}, pileGlobalId={pileGlobalId}, aliveState={pile->r_AliveState}, previousStoneBlocks={previousStoneBlocks}, previousGoodStack={previousGoodStack}.");
        }

        private bool TryGetRelocatableQuarry(
            int quarryId,
            int ownerPlayerId,
            out GameBuilding* quarry,
            out GameBuilding* oldPile,
            out string failureReason)
        {
            oldPile = null;
            if (!TryGetOwnedQuarry(quarryId, ownerPlayerId, out quarry, out failureReason))
                return false;

            int oldPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            if (oldPileId <= 0)
            {
                failureReason = "quarry-linked-pile-id-not-positive";
                return false;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(oldPileId, out oldPile))
            {
                failureReason = $"linked-pile-id-not-resolvable(id={oldPileId})";
                return false;
            }

            if (!IsAliveBuilding(oldPile, eStructs.STRUCT_QUARRYPILE, ownerPlayerId))
            {
                failureReason = $"linked-building-not-alive-owned-quarry-pile(id={oldPileId},type={oldPile->r_BuildingType},aliveState={oldPile->r_AliveState},owner={oldPile->r_PlayerIdOwner},expectedOwner={ownerPlayerId},globalId={oldPile->r_GlobalId})";
                return false;
            }

            return true;
        }

        private static bool TryGetOwnedQuarry(
            int quarryId,
            int ownerPlayerId,
            out GameBuilding* quarry,
            out string failureReason)
        {
            quarry = null;
            failureReason = null;

            if (quarryId <= 0)
            {
                failureReason = "selected-building-id-not-positive";
                return false;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out quarry))
            {
                failureReason = "selected-building-id-not-resolvable";
                return false;
            }

            if (!IsAliveBuilding(quarry, eStructs.STRUCT_QUARRY, ownerPlayerId))
            {
                failureReason = $"selected-building-not-alive-owned-quarry(type={quarry->r_BuildingType},aliveState={quarry->r_AliveState},owner={quarry->r_PlayerIdOwner},expectedOwner={ownerPlayerId},globalId={quarry->r_GlobalId})";
                return false;
            }

            return true;
        }

        private static bool IsAliveBuilding(GameBuilding* building, eStructs type, int ownerPlayerId)
        {
            return building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                building->r_BuildingType == type &&
                building->r_PlayerIdOwner == ownerPlayerId;
        }

        private static bool IsValidFreshSpawn(GameBuilding* building, eStructs type, int ownerPlayerId)
        {
            return building != null &&
                (building->r_AliveState == AliveState.NeedsInit || building->r_AliveState == AliveState.IsAlive) &&
                building->r_BuildingType == type &&
                building->r_PlayerIdOwner == ownerPlayerId;
        }

        private static int GetBuildingScale(GameBuilding* building)
        {
            uint gridSize = building->r_OccupyTileGridSize;
            return gridSize > 0 && gridSize <= int.MaxValue ? (int)gridSize : 0;
        }

        private static string DescribeBuilding(GameBuilding* building)
        {
            if (building == null)
                return "null";

            return $"aliveState={building->r_AliveState}, type={building->r_BuildingType}, owner={building->r_PlayerIdOwner}, globalId={building->r_GlobalId}, " +
                $"tiles={building->r_TilePositionXBegin},{building->r_TilePositionYBegin}-{building->r_TilePositionXEnd},{building->r_TilePositionYEnd}, gridSize={building->r_OccupyTileGridSize}";
        }

        private static int FindAliveBuildingIdByGlobalId(int globalId)
        {
            if (globalId <= 0)
                return 0;

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_AliveState == AliveState.IsAlive && (int)building.r_GlobalId == globalId)
                    return index + 1;
            }

            return 0;
        }

        private static int FindAliveQuarryIdByPileId(int pileId, int ownerPlayerId)
        {
            if (pileId <= 0)
                return 0;

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_AliveState == AliveState.IsAlive &&
                    building.r_BuildingType == eStructs.STRUCT_QUARRY &&
                    building.r_PlayerIdOwner == ownerPlayerId &&
                    building.r_StoneQuarry_StockPileBuildingId == pileId)
                {
                    return index + 1;
                }
            }

            return 0;
        }

        private bool DeleteBuildingWithoutLinkedPropagation(int buildingId, string reason)
        {
            if (buildingId <= 0)
                return false;

            linkedRemovalSuppressionDepth++;
            try
            {
                bool result = GameBuildingManagerAPI.Instance.DeleteBuildingSafe(buildingId);
                LogInfo($"internal building deletion requested: reason={reason}, buildingId={buildingId}, result={result}, suppressionDepth={linkedRemovalSuppressionDepth}.");
                return result;
            }
            finally
            {
                linkedRemovalSuppressionDepth--;
            }
        }

        private HashSet<long> GetFailedRotationTargets(int quarryGlobalId, int oldPileGlobalId)
        {
            if (!failedRotationTargetsByQuarry.TryGetValue(quarryGlobalId, out FailedRotationTargets state))
                return null;

            if (state.OldPileGlobalId == oldPileGlobalId)
                return state.Targets;

            failedRotationTargetsByQuarry.Remove(quarryGlobalId);
            LogInfo(
                $"discarded stale failed-target state: quarryGlobalId={quarryGlobalId}, " +
                $"storedOldPileGlobalId={state.OldPileGlobalId}, currentOldPileGlobalId={oldPileGlobalId}.");
            return null;
        }

        private void RememberFailedRotationTarget(QuarryPileRelocationOperation operation, string reason)
        {
            if (operation == null || operation.QuarryGlobalId <= 0 || operation.OldPileGlobalId <= 0)
                return;

            if (!failedRotationTargetsByQuarry.TryGetValue(operation.QuarryGlobalId, out FailedRotationTargets state) ||
                state.OldPileGlobalId != operation.OldPileGlobalId)
            {
                state = new FailedRotationTargets(operation.OldPileGlobalId);
                failedRotationTargetsByQuarry[operation.QuarryGlobalId] = state;
            }

            PlacementPosition target = new PlacementPosition(operation.TargetTileX, operation.TargetTileY);
            bool added = state.Targets.Add(GetPositionKey(target));
            LogInfo(
                $"failed rotation target remembered: reason={reason}, quarryGlobalId={operation.QuarryGlobalId}, " +
                $"oldPileGlobalId={operation.OldPileGlobalId}, target={target.X},{target.Y}, added={added}, failedTargetCount={state.Targets.Count}.");
        }

        private void ClearFailedRotationTargets(int quarryGlobalId, string reason)
        {
            if (quarryGlobalId <= 0 || !failedRotationTargetsByQuarry.Remove(quarryGlobalId))
                return;

            LogInfo($"failed rotation targets cleared: reason={reason}, quarryGlobalId={quarryGlobalId}.");
        }

        private static long GetPositionKey(PlacementPosition position)
        {
            return ((long)position.X << 32) | (uint)position.Y;
        }

        private void ClearMapState()
        {
            LogInfo(
                $"clearing rotation state: failedTargetQuarryCount={failedRotationTargetsByQuarry.Count}, nextOperationId={nextOperationId}, " +
                $"pendingAIQuarryCount={pendingAIQuarriesByGlobalId.Count}, prefabCaptureActive={activePrefabSpawnCapture != null}, " +
                $"linkedRemovalSuppressionDepth={linkedRemovalSuppressionDepth}.");
            failedRotationTargetsByQuarry.Clear();
            pendingAIQuarriesByGlobalId.Clear();
            activePrefabSpawnCapture = null;
            linkedRemovalSuppressionDepth = 0;
            nextOperationId = 0;
            lastVisibilityLogState = null;
            buttonViewModel.Hide();
        }

        private void BeginMapState()
        {
            ClearMapState();
            mapActive = true;
            aiSpawnObservationArmed = false;
            LogInfo("map state started; AI quarry spawn observation awaits the first simulation tick.");
        }

        private void ResetAISpawnObservationAfterLoad(string source)
        {
            pendingAIQuarriesByGlobalId.Clear();
            aiSpawnObservationArmed = false;
            LogInfo($"AI quarry spawn observation reset after {source} load; existing quarry spawns are ignored until the next simulation tick.");
        }

        private void EndMapState()
        {
            mapActive = false;
            aiSpawnObservationArmed = false;
            ClearMapState();
        }

        private void LogVisibilityState(string state)
        {
            if (string.Equals(lastVisibilityLogState, state, StringComparison.Ordinal))
                return;

            lastVisibilityLogState = state;
            LogInfo($"button visibility state: {state}.");
        }

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogDebug(log, $"Extra Features quarry-pile diagnostic: {message}");
        }

        private void DisposeSubscriptions()
        {
            for (int i = 0; i < subscriptions.Count; i++)
                subscriptions[i]?.Dispose();
            subscriptions.Clear();
        }

        private void UnsubscribeTick()
        {
            if (!tickSubscribed)
                return;

            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            tickSubscribed = false;
        }

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
                // Never expose an editor mutation for an object owned by another editor player.
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            }

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : 1;
        }

        private sealed class FailedRotationTargets
        {
            public FailedRotationTargets(int oldPileGlobalId)
            {
                OldPileGlobalId = oldPileGlobalId;
            }

            public int OldPileGlobalId { get; }
            public HashSet<long> Targets { get; } = new HashSet<long>();
        }

        private sealed class PendingAIQuarry
        {
            public PendingAIQuarry(
                int quarryId,
                int quarryGlobalId,
                int playerId,
                long deadlineTimestamp)
            {
                QuarryId = quarryId;
                QuarryGlobalId = quarryGlobalId;
                PlayerId = playerId;
                DeadlineTimestamp = deadlineTimestamp;
            }

            public int QuarryId { get; }
            public int QuarryGlobalId { get; }
            public int PlayerId { get; }
            public long DeadlineTimestamp { get; }
        }

        private sealed class PrefabSpawnCapture
        {
            public PrefabSpawnCapture(int operationId, int playerId, int targetX, int targetY)
            {
                OperationId = operationId;
                PlayerId = playerId;
                TargetX = targetX;
                TargetY = targetY;
            }

            public int OperationId { get; }
            public int PlayerId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int InvalidPostEventCount { get; set; }
            public List<int> BuildingIds { get; } = new List<int>();

            public void RecordBuildingId(int buildingId)
            {
                if (buildingId > 0 && !BuildingIds.Contains(buildingId))
                    BuildingIds.Add(buildingId);
            }

            public string DescribeBuildingIds()
            {
                return BuildingIds.Count == 0 ? "none" : string.Join(",", BuildingIds);
            }
        }

        private readonly struct PlacementPosition
        {
            public PlacementPosition(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private readonly struct PileContentSnapshot
        {
            private PileContentSnapshot(
                uint stoneBlocks,
                uint currentGoodStack,
                uint maxGoodStack,
                eGoods localStorageGoodType)
            {
                StoneBlocks = stoneBlocks;
                CurrentGoodStack = currentGoodStack;
                MaxGoodStack = maxGoodStack;
                LocalStorageGoodType = localStorageGoodType;
            }

            public uint StoneBlocks { get; }
            public uint CurrentGoodStack { get; }
            public uint MaxGoodStack { get; }
            public eGoods LocalStorageGoodType { get; }

            public static PileContentSnapshot Capture(GameBuilding* pile)
            {
                return new PileContentSnapshot(
                    pile->r_StoneBlocksAmount,
                    pile->r_CurrentGoodStackAmount,
                    pile->r_MaxGoodStackAmount,
                    pile->r_LocalStorageGoodType);
            }

            public void ApplyTo(GameBuilding* pile)
            {
                pile->r_StoneBlocksAmount = StoneBlocks;
                pile->r_CurrentGoodStackAmount = CurrentGoodStack;
                pile->r_MaxGoodStackAmount = MaxGoodStack;
                pile->r_LocalStorageGoodType = LocalStorageGoodType;
            }
        }
    }
}
