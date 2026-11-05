using APIShared.GameModes;
// Feature: Relocate a quarry's linked stone pile to the next valid position.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
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
using PlacementPosition = BugfixesAndQoL.QuarryPileTargetSearch.PlacementPosition;

namespace BugfixesAndQoL
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
        private bool simulationTickRegistered;
        private const int ChoreProtocolVersion = 1;
        private const double AIQuarryReadinessTimeoutSeconds = 10.0;
        // Simulation ticks (40 per game second): pausing or slow game speed must not let a
        // wall-clock deadline expire before the quarry had game time to become ready.
        private const int AIQuarryReadinessTimeoutTicks = 400;

        private delegate void SetUpInbuildingDelegate(MainViewModel self, int overridePanel, int overrideType);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly QuarryPileRelocationButtonViewModel buttonViewModel;
        private readonly QuarryPileTargetSearch targetSearch;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, PendingAIQuarry> pendingAIQuarriesByGlobalId = new Dictionary<int, PendingAIQuarry>();
        private readonly Stack<int[]> pendingAIQuarryKeyBuffers = new Stack<int[]>();

        private Hook setUpInbuildingHook;
        private SetUpInbuildingDelegate setUpInbuildingTrampoline;
        private Button hookedRelocationButton;
        private TextBlock hookedRelocationTooltip;
        private PrefabSpawnCapture activePrefabSpawnCapture;
        private int nextOperationId;
        private bool initialized;
        private bool networkInitialized;
        private bool tickSubscribed;
        private bool mapActive;
        private bool aiSpawnObservationArmed;
        private R3PacketEventHook<QuarryPileRelocationPacket> relocationPacketHook;
        private IDisposable relocationPacketSubscription;

        public QuarryPileRelocationRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
            buttonViewModel = new QuarryPileRelocationButtonViewModel(OnRelocateCommand);
            targetSearch = new QuarryPileTargetSearch(log);
        }

        public QuarryPileRelocationButtonViewModel ButtonViewModel => buttonViewModel;

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            relocationPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<QuarryPileRelocationPacket>();
            relocationPacketSubscription = relocationPacketHook.GetBaseHook().Observable.Subscribe(OnRelocationPacketReceived);
            networkInitialized = true;
        }

        public void InstallNativeFunctions(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            targetSearch.InstallNativeFunctions(libraryHandle, memory, referenceHashMatches);
        }

        public void Initialize()
        {
            if (initialized)
                return;

            Hook installedHook = null;
            try
            {
                subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log,
                    context =>
                    {
                        BeginMapState();
                        OnMapContentLoaded();
                    }));
                subscriptions.Add(Shared.MissionEvents.Ended
                    .Subscribe(_ => EndMapState()));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Subscribe(OnBuildingSpawn));
                if (!simulationTickRegistered)
                {
                    if (!APIShared.Simulation.SimulationTickEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "quarry-pile", OnGameTick, out string tickReason, 200))
                        throw new InvalidOperationException("Native simulation ticks unavailable: " + tickReason);
                    simulationTickRegistered = true;
                }
                tickSubscribed = true;

                installedHook = new Hook(FindSetUpInbuildingMethod(), (SetUpInbuildingDelegate)SetUpInbuildingHook);
                setUpInbuildingTrampoline = installedHook.GenerateTrampoline<SetUpInbuildingDelegate>();
                setUpInbuildingHook = installedHook;
                initialized = true;
                buttonViewModel.Hide();
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogInfo($"runtime initialized: mode=Vanilla-clockwise-prefab-spawn-and-structure-group, nativeCandidateHelperAvailable={targetSearch.NativeHelperAvailable}, subscriptions={subscriptions.Count}, setUpInbuildingHookInstalled={setUpInbuildingHook != null}.");
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

            // This runtime is toggled by live settings. Keep the published
            // MonoMod hook and its event publishers rooted for the process
            // lifetime; every behavior path is guarded by the current settings.
            buttonViewModel.Hide();
            EndMapState();
            UnhookRelocationButton();
        }

        public void ApplySetting()
        {
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
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
                    return;
                }

                if (!targetSearch.NativeHelperAvailable)
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    return;
                }

                if (RequiresChoreTransport() && !IsChoreTransportReady())
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    return;
                }

                int localPlayerId = GetControlledPlayerId();
                int selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (!TryGetOwnedQuarry(selectedBuildingId, localPlayerId, out _))
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    return;
                }

                buttonViewModel.Show();
            }
            catch (Exception ex)
            {
                buttonViewModel.Hide();
                HideRelocationTooltip();
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/RefreshButtonVisibility", $"Bugfixes and QoL quarry-pile button visibility refresh failed: {ex}");
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
            Button button = mainViewModel?.HUDBuildingPanel?.FindName("BugfixesAndQoLQuarryPileRelocationButton") as Button;
            TextBlock tooltip = mainViewModel?.HUDBuildingPanel?.FindName("BugfixesAndQoLQuarryPileRelocationTooltipHost") as TextBlock;

            if (tooltip != null)
                tooltip.Text = SerpLocalization.Get(SerpLocalization.QuarryPileRelocationTooltip);

            if (ReferenceEquals(button, hookedRelocationButton) && ReferenceEquals(tooltip, hookedRelocationTooltip))
                return;

            UnhookRelocationButton();
            hookedRelocationButton = button;
            hookedRelocationTooltip = tooltip;
            HideRelocationTooltip();
            if (hookedRelocationButton == null)
                return;

            hookedRelocationButton.MouseEnter += OnRelocationButtonMouseEnter;
            hookedRelocationButton.MouseLeave += OnRelocationButtonMouseLeave;
        }

        private void UnhookRelocationButton()
        {
            if (hookedRelocationButton != null)
            {
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
                return;

            hookedRelocationTooltip.Text = SerpLocalization.Get(SerpLocalization.QuarryPileRelocationTooltip);
            hookedRelocationTooltip.Visibility = Visibility.Visible;
        }

        private void OnRelocationButtonMouseLeave(object sender, MouseEventArgs args)
        {
            HideRelocationTooltip();
        }

        private void HideRelocationTooltip()
        {
            if (hookedRelocationTooltip != null)
                hookedRelocationTooltip.Visibility = Visibility.Hidden;
        }

        private void OnRelocateCommand()
        {
            int localPlayerId = 0;
            int selectedBuildingId = 0;
            QuarryPileRelocationOperation attemptedOperation = null;

            try
            {
                if (!IsManualFeatureActive())
                    return;

                // Target validation writes TileManager.IsPlacementBlocked, and the validated target
                // is not checked again before the spawn. A tick must not run in between.
                lock (EngineThreadLock.Gate)
                {
                    localPlayerId = GetControlledPlayerId();
                    selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();

                    if (!TryGetRelocatableQuarry(selectedBuildingId, localPlayerId, out GameBuilding* quarry, out GameBuilding* oldPile))
                    {
                        RefreshButtonVisibility();
                        return;
                    }

                    int operationId = NextOperationId();

                    if (!targetSearch.TryFindNextRotationTarget(localPlayerId, quarry, oldPile, operationId, out PlacementPosition target))
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/OnRelocateCommand.found-no-valid-clockwise", $"Bugfixes and QoL quarry-pile rotation found no valid clockwise position: playerId={localPlayerId}, operationId={operationId}, quarryId={selectedBuildingId}, quarryGlobalId={quarry->r_GlobalId}, oldPileGlobalId={oldPile->r_GlobalId}.");
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
                            RefreshButtonVisibility();
                        }
                        return;
                    }

                    if (!TryApplyRotation(attemptedOperation, "singleplayer-local-click", targetAlreadyValidated: true))
                    {
                        targetSearch.RememberFailedRotationTarget(attemptedOperation);
                        RefreshButtonVisibility();
                        return;
                    }

                    RefreshButtonVisibility();
                }
            }
            catch (Exception ex)
            {
                if (attemptedOperation != null)
                    targetSearch.RememberFailedRotationTarget(attemptedOperation);

                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/OnRelocateCommand.click-failed-selected-building", $"Bugfixes and QoL quarry-pile rotation click failed: selectedBuildingId={selectedBuildingId}, playerId={localPlayerId}: {ex}");
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

                    if (args.Phase == EventHookPhase.Post &&
                        matchesExpectedInput &&
                        args.ReturnValue > 0 &&
                        args.ReturnValue <= int.MaxValue)
                    {
                        capture.RecordBuildingId((int)args.ReturnValue);
                    }
                }

                TryQueueAIQuarry(args);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/OnBuildingSpawn", $"Bugfixes and QoL quarry-pile building-spawn handling failed: exception={ex}");
            }
        }

        private void TryQueueAIQuarry(BuildingSpawnEventArgs args)
        {
            if (!IsAIAutomationActive() ||
                !mapActive ||
                !aiSpawnObservationArmed ||
                args.Phase != EventHookPhase.Post ||
                args.Building != eStructs.STRUCT_QUARRY ||
                args.ReturnValue <= 0 ||
                args.ReturnValue > int.MaxValue ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId))
            {
                return;
            }

            // In multiplayer only the host chooses and broadcasts the explicit target.
            if (RequiresChoreTransport() && !MultiplayerRoster.IsLocalHost())
                return;

            int quarryId = (int)args.ReturnValue;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out GameBuilding* quarry) ||
                quarry == null ||
                quarry->r_BuildingType != eStructs.STRUCT_QUARRY ||
                quarry->r_PlayerIdOwner != args.PlayerId ||
                quarry->r_GlobalId <= 0 ||
                quarry->r_GlobalId > int.MaxValue)
            {
                return;
            }

            int quarryGlobalId = (int)quarry->r_GlobalId;
            if (pendingAIQuarriesByGlobalId.ContainsKey(quarryGlobalId))
                return;

            // The deadline starts at the first queue tick that processes this entry.
            pendingAIQuarriesByGlobalId.Add(
                quarryGlobalId,
                new PendingAIQuarry(
                    quarryId,
                    quarryGlobalId,
                    args.PlayerId));
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogInfo($"AI quarry queued for Keep-facing pile placement: playerId={args.PlayerId}, quarryId={quarryId}, quarryGlobalId={quarryGlobalId}, timeoutSeconds={AIQuarryReadinessTimeoutSeconds:0.#}.");
        }

        private void OnGameTick(int tick)
        {
            if (mapActive && !aiSpawnObservationArmed)
            {
                aiSpawnObservationArmed = true;
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
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

                int keyCount = pendingAIQuarriesByGlobalId.Count;
                int[] quarryGlobalIds = pendingAIQuarryKeyBuffers.Count > 0
                    ? pendingAIQuarryKeyBuffers.Pop()
                    : Array.Empty<int>();
                if (quarryGlobalIds.Length < keyCount)
                    quarryGlobalIds = new int[keyCount];
                // Keep the entry snapshot private even if a worker invokes a nested tick.
                try
                {
                    pendingAIQuarriesByGlobalId.Keys.CopyTo(quarryGlobalIds, 0);
                    for (int index = 0; index < keyCount; index++)
                    {
                        int quarryGlobalId = quarryGlobalIds[index];
                        try
                        {
                            TryProcessPendingAIQuarry(quarryGlobalId, tick);
                        }
                        catch (Exception ex)
                        {
                            // One malformed entry must not postpone the remaining quarries.
                            pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                            Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/OnGameTick.queue-entry-failed-discarded", $"Bugfixes and QoL AI quarry-pile queue entry failed and was discarded: tick={tick}, quarryGlobalId={quarryGlobalId}, exception={ex}");
                        }
                    }
                }
                finally
                {
                    Array.Clear(quarryGlobalIds, 0, keyCount);
                    pendingAIQuarryKeyBuffers.Push(quarryGlobalIds);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/OnGameTick.tick-processing-failed-tick", $"Bugfixes and QoL AI quarry-pile tick processing failed: tick={tick}, exception={ex}");
            }
        }

        private void TryProcessPendingAIQuarry(int quarryGlobalId, int tick)
        {
            if (!pendingAIQuarriesByGlobalId.TryGetValue(quarryGlobalId, out PendingAIQuarry pending))
                return;

            if (pending.FirstProcessedTick == null)
                pending.FirstProcessedTick = tick;
            if (!APIShared.Simulation.SimulationClock.TryGetElapsedTicks(pending.FirstProcessedTick.Value, tick, out int elapsed) ||
                elapsed >= AIQuarryReadinessTimeoutTicks)
            {
                pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/TryProcessPendingAIQuarry.ai-quarry-pile-placement", $"Bugfixes and QoL AI quarry-pile placement timed out; the Vanilla pile remains unchanged: playerId={pending.PlayerId}, quarryId={pending.QuarryId}, quarryGlobalId={quarryGlobalId}, tick={tick}.");
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
                return;
            }

            if (quarry->r_AliveState != AliveState.IsAlive ||
                !TryGetRelocatableQuarry(pending.QuarryId, pending.PlayerId, out quarry, out GameBuilding* oldPile))
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

            QuarryKeepCenterResolution keepCenterResolution = QuarryKeepCenterPolicy.Resolve(
                keep->r_BuildingType,
                keep->r_OccupyTileGridSize,
                keep->r_TilePositionXBegin,
                keep->r_TilePositionYBegin,
                out int keepCenterXTimesTwo,
                out int keepCenterYTimesTwo);
            if (keepCenterResolution == QuarryKeepCenterResolution.NotReady)
                return;
            if (keepCenterResolution != QuarryKeepCenterResolution.Ready)
            {
                pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/TryProcessPendingAIQuarry.discarded-ai-quarry-pile", $"Bugfixes and QoL discarded an AI quarry-pile queue entry because the Keep geometry is structurally invalid; " +
                    $"the Vanilla pile remains unchanged: playerId={pending.PlayerId}, keepId={keepId}, " +
                    $"keepType={keep->r_BuildingType}, keepAliveState={keep->r_AliveState}, " +
                    $"keepGridSize={keep->r_OccupyTileGridSize}, " +
                    $"keepBegin=({keep->r_TilePositionXBegin},{keep->r_TilePositionYBegin}), " +
                    $"quarryGlobalId={quarryGlobalId}, tick={tick}.");
                return;
            }

            int operationId = NextOperationId();
            if (!targetSearch.TryFindNearestKeepTarget(
                pending.PlayerId,
                quarry,
                oldPile,
                keepCenterXTimesTwo,
                keepCenterYTimesTwo,
                operationId,
                out PlacementPosition target,
                out bool currentPositionIsBest))
            {
                pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/TryProcessPendingAIQuarry.found-no-safe-keep", $"Bugfixes and QoL found no safe Keep-facing pile position for a new AI quarry; the Vanilla pile remains unchanged: playerId={pending.PlayerId}, quarryGlobalId={quarryGlobalId}, operationId={operationId}.");
                return;
            }

            pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
            if (currentPositionIsBest)
            {
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
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
                targetSearch.RememberFailedRotationTarget(operation);
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/TryProcessPendingAIQuarry.could-not-move-new", $"Bugfixes and QoL could not move a new AI quarry pile towards its Keep; the existing pile was retained: playerId={pending.PlayerId}, quarryGlobalId={quarryGlobalId}, operationId={operationId}.");
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

        private bool RequiresChoreTransport()
        {
            // Detection failures deliberately take the multiplayer path and therefore fail closed.
            return multiplayerFeatureGate.BlocksLocalStateChanges;
        }

        private bool IsChoreTransportReady()
        {
            return APIShared.Networking.ChoreTransport.IsAvailable(
                networkInitialized && relocationPacketHook != null);
        }

        private bool TrySendRotationChore(QuarryPileRelocationOperation operation)
        {
            if (!IsChoreTransportReady())
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/TrySendRotationChore.rotation-refused-multiplayer-because", $"Bugfixes and QoL quarry-pile rotation refused in multiplayer because the Chore transport is unavailable: operationId={operation.OperationId}.");
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

            short packetId = relocationPacketHook?.GetPacketId() ?? (short)0;
            if (!APIShared.Networking.ChoreTransport.TrySend(
                    packet,
                    packetId,
                    networkInitialized && relocationPacketHook != null,
                    out byte[] body,
                    out string rejectionReason))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/TrySendRotationChore.chore-not-queued-no", $"Bugfixes and QoL quarry-pile Chore was not queued; no local action was applied: operationId={operation.OperationId}, reason={rejectionReason}.");
                return false;
            }

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogInfo($"rotation Chore queued: operationId={operation.OperationId}, packetId={packetId}, payloadBytes={sizeof(short) + body.Length}, quarryGlobalId={operation.QuarryGlobalId}, oldPileGlobalId={operation.OldPileGlobalId}, target={operation.TargetTileX},{operation.TargetTileY}.");
            return true;
        }

        private void OnRelocationPacketReceived(ReceiveCustomPacketEventArgs<QuarryPileRelocationPacket> args)
        {
            if (!APIShared.Networking.ChoreTransport.IsChoreDelivery(args))
                return;

            QuarryPileRelocationPacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion)
            {
                LogErrorThreadSafe(
                    $"Bugfixes and QoL rejected a quarry-pile Chore with an unsupported payload: protocolVersion={packet?.ProtocolVersion.ToString() ?? "null"}.");
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
                if (!initialized || !targetSearch.NativeHelperAvailable)
                {
                    LogErrorThreadSafe(
                        $"Bugfixes and QoL cannot execute quarry-pile Chore because the relocation runtime is unavailable: operationId={operation.OperationId}, initialized={initialized}, nativeCandidateHelperAvailable={targetSearch.NativeHelperAvailable}.");
                    return;
                }

                if (!TryApplyRotation(operation, "multiplayer-chore", targetAlreadyValidated: false))
                {
                    targetSearch.RememberFailedRotationTarget(operation);
                    return;
                }

                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogInfo($"rotation Chore executed successfully: operationId={operation.OperationId}.");
            }
            catch (Exception ex)
            {
                targetSearch.RememberFailedRotationTarget(operation);
                LogErrorThreadSafe(
                    $"Bugfixes and QoL quarry-pile Chore execution failed: operationId={operation.OperationId}, exception={ex}");
            }
            finally
            {
                Shared.UnityMainThreadDispatch.TryEnqueue(RefreshButtonVisibility);
            }
        }

        private bool TryApplyRotation(
            QuarryPileRelocationOperation operation,
            string reason,
            bool targetAlreadyValidated = false)
        {
            bool applied = TryApplyRotation(operation, reason, out string failure, targetAlreadyValidated);
            if (!applied)
                LogWarningThreadSafe($"Bugfixes and QoL quarry-pile relocation rejected: source={reason}, " +
                    $"reason={failure}, operationId={operation.OperationId}, player={operation.PlayerId}, " +
                    $"quarryGlobalId={operation.QuarryGlobalId}, oldPileGlobalId={operation.OldPileGlobalId}, " +
                    $"target={operation.TargetTileX},{operation.TargetTileY}, targetPrevalidated={targetAlreadyValidated}; existing pile retained.");
            return applied;
        }

        private bool TryApplyRotation(
            QuarryPileRelocationOperation operation,
            string reason,
            out string failure,
            bool targetAlreadyValidated)
        {
            failure = "quarry missing, stale, wrong owner/type or outside link capacity";
            int quarryId = FindAliveBuildingIdByGlobalId(operation.QuarryGlobalId);
            if (quarryId <= 0 ||
                quarryId > ushort.MaxValue ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out GameBuilding* quarry) ||
                !IsAliveBuilding(quarry, eStructs.STRUCT_QUARRY, operation.PlayerId))
                return false;

            failure = "old pile missing, stale or no longer linked to requested quarry identity; quarryId=" + quarryId;
            int oldPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            if (oldPileId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(oldPileId, out GameBuilding* oldPile) ||
                !IsAliveBuilding(oldPile, eStructs.STRUCT_QUARRYPILE, operation.PlayerId) ||
                (int)oldPile->r_GlobalId != operation.OldPileGlobalId)
                return false;

            failure = "requested target no longer valid; quarryId=" + quarryId + ",oldPileId=" + oldPileId;
            PlacementPosition expectedTarget;
            if (targetAlreadyValidated)
            {
                expectedTarget = new PlacementPosition(operation.TargetTileX, operation.TargetTileY);
            }
            else
            {
                expectedTarget = new PlacementPosition(operation.TargetTileX, operation.TargetTileY);
                if (!targetSearch.ValidateRequestedRotationTarget(
                    operation.PlayerId,
                    quarry,
                    oldPile,
                    expectedTarget,
                    operation.OperationId))
                    return false;
            }

            QuarryPileVanillaGroupResolution groupResolution = QuarryPileVanillaGroupPolicy.Resolve(
                Shared.NativeBuildingCompoundGroup.Read(quarry),
                Shared.NativeBuildingCompoundGroup.Read(oldPile));
            if (!groupResolution.CanUse)
            {
                failure = "inconsistent Vanilla structure groups: " + groupResolution.Status;
                LogErrorThreadSafe(
                    $"Bugfixes and QoL rejected quarry-pile relocation because Vanilla structure groups are inconsistent: operationId={operation.OperationId}, quarryId={quarryId}, oldPileId={oldPileId}, quarryGroupId={Shared.NativeBuildingCompoundGroup.Read(quarry)}, oldPileGroupId={Shared.NativeBuildingCompoundGroup.Read(oldPile)}, status={groupResolution.Status}.");
                return false;
            }

            if (groupResolution.RepairsPileGroup)
            {
                Shared.NativeBuildingCompoundGroup.Write(oldPile, groupResolution.GroupId);
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogInfo($"repaired missing Vanilla structure group before relocation: operationId={operation.OperationId}, quarryId={quarryId}, pileId={oldPileId}, groupId={groupResolution.GroupId}.");
            }

            PileContentSnapshot content = PileContentSnapshot.Capture(oldPile);
            short previousCurrentHealth = oldPile->r_CurrentHealth;
            ushort previousMaxHealth = oldPile->r_MaxHealth;
            failure = "replacement spawn failed; quarryId=" + quarryId + ",oldPileId=" + oldPileId;
            if (!TrySpawnReplacement(
                operation.PlayerId,
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
                failure = "replacement exceeds ushort link capacity; newPileId=" + newPileId;
                DeleteBuildingSafely(newPileId);
                return false;
            }

            int newPileGlobalId = (int)newPile->r_GlobalId;
            ushort previousQuarryPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            ushort previousOldPileQuarryId = oldPile->r_StoneQuarry_StockPileBuildingId;
            uint previousOldPileGroupId = Shared.NativeBuildingCompoundGroup.Read(oldPile);
            content.ApplyTo(newPile);
            newPile->r_CurrentHealth = previousCurrentHealth;
            newPile->r_MaxHealth = previousMaxHealth;

            // Vanilla deletes every building sharing this non-zero structure group. A directly
            // spawned quarry pile does not receive the quarry's group automatically.
            Shared.NativeBuildingCompoundGroup.Write(newPile, groupResolution.GroupId);
            newPile->r_StoneQuarry_StockPileBuildingId = 0;
            quarry->r_StoneQuarry_StockPileBuildingId = checked((ushort)newPileId);

            // Detach the replaced pile before its asynchronous deletion so only the replacement
            // remains in Vanilla's multi-building structure group.
            Shared.NativeBuildingCompoundGroup.Write(oldPile, 0);
            if (QuarryPileVanillaGroupPolicy.IsLegacyReverseLink(quarryId, previousOldPileQuarryId))
                oldPile->r_StoneQuarry_StockPileBuildingId = 0;
            ClearPileContentBeforeDeletion(oldPile);

            bool oldPileMarkedForDeletion = oldPile->r_AliveState == AliveState.MarkedForDeletion ||
                DeleteBuildingSafely(oldPileId);
            if (!oldPileMarkedForDeletion)
            {
                failure = "old pile deletion failed; links/content restored and replacement deletion requested; oldPileId=" +
                    oldPileId + ",newPileId=" + newPileId;
                quarry->r_StoneQuarry_StockPileBuildingId = previousQuarryPileId;
                Shared.NativeBuildingCompoundGroup.Write(oldPile, previousOldPileGroupId);
                oldPile->r_StoneQuarry_StockPileBuildingId = previousOldPileQuarryId;
                content.ApplyTo(oldPile);
                Shared.NativeBuildingCompoundGroup.Write(newPile, 0);
                newPile->r_StoneQuarry_StockPileBuildingId = 0;
                newPile->r_StoneBlocksAmount = 0;
                newPile->r_CurrentGoodStackAmount = 0;
                DeleteBuildingSafely(newPileId);
                return false;
            }

            targetSearch.ClearFailedRotationTargets(operation.QuarryGlobalId);
            failure = null;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogInfo(
                    $"rotation completed: reason={reason}, playerId={operation.PlayerId}, quarryGlobalId={operation.QuarryGlobalId}, " +
                    $"newPileGlobalId={newPileGlobalId}, vanillaGroupId={groupResolution.GroupId}, target={expectedTarget.X},{expectedTarget.Y}.");
            return true;
        }

        private bool TrySpawnReplacement(
            int playerId,
            int oldPileId,
            GameBuilding* oldPile,
            PlacementPosition target,
            int operationId,
            out int newPileId,
            out GameBuilding* newPile)
        {
            newPileId = 0;
            newPile = null;
            int buildingScale = QuarryPileTargetSearch.GetBuildingScale(oldPile);
            if (buildingScale <= 0)
                return false;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!QuarryPileTargetSearch.TryGetSafeTileId(tileApi, target.X, target.Y, out _))
                return false;

            PrefabSpawnCapture capture = new PrefabSpawnCapture(playerId, target.X, target.Y);
            if (activePrefabSpawnCapture != null)
                return false;

            Exception prefabException = null;
            activePrefabSpawnCapture = capture;
            try
            {
                GameBuildingManagerAPI.Instance.CreatePrefab(
                    playerId,
                    target.X,
                    target.Y,
                    eMappers.MAPPER_QUARRYPILE,
                    buildingScale,
                    0,
                    true,
                    true);
            }
            catch (Exception ex)
            {
                prefabException = ex;
            }
            finally
            {
                activePrefabSpawnCapture = null;
            }

            if (prefabException != null)
            {
                int fallbackPileId = FindFreshPileAtTarget(oldPileId, playerId, target, out _);
                CleanupFailedPrefabSpawns(capture, oldPileId, operationId, "prefab-exception", fallbackPileId);
                LogErrorThreadSafe(
                    $"Bugfixes and QoL quarry-pile prefab replacement spawn failed: operationId={operationId}, fallbackPileId={fallbackPileId}, exception={prefabException}");
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
                newPileId = FindFreshPileAtTarget(oldPileId, playerId, target, out newPile);

            bool replacementVerified = newPileId > 0 &&
                IsValidFreshSpawn(newPile, eStructs.STRUCT_QUARRYPILE, playerId) &&
                MatchesSpawnAnchor(newPile->r_TilePositionXBegin, newPile->r_TilePositionYBegin, target) &&
                newPile->r_OccupyTileGridSize == oldPile->r_OccupyTileGridSize;
            if (!replacementVerified)
            {
                LogWarningThreadSafe(
                    $"Bugfixes and QoL quarry-pile replacement verification failed; spawned candidates are being cleaned up: operationId={operationId}, playerId={playerId}, target={target.X},{target.Y}.");
                CleanupFailedPrefabSpawns(capture, oldPileId, operationId, "verification-failed", newPileId);
                newPile = null;
                newPileId = 0;
                return false;
            }

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
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                int buildingId = spanIndex + 1;
                if (buildingId == oldPileId)
                    continue;

                ref GameBuilding building = ref buildings[spanIndex];
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

                bool markedForDeletion = DeleteBuildingSafely(buildingId);
                if (!markedForDeletion)
                {
                    LogWarningThreadSafe(
                        $"Bugfixes and QoL could not clean up an invalid quarry-pile prefab: operationId={operationId}, reason={reason}, buildingId={buildingId}.");
                }
            }
        }

        private static void ClearPileContentBeforeDeletion(GameBuilding* pile)
        {
            if (pile == null)
                return;

            // The resource snapshot has already been transferred; prevent deletion from refunding it again.
            pile->r_StoneBlocksAmount = 0;
            pile->r_CurrentGoodStackAmount = 0;
        }

        private bool TryGetRelocatableQuarry(
            int quarryId,
            int ownerPlayerId,
            out GameBuilding* quarry,
            out GameBuilding* oldPile)
        {
            oldPile = null;
            if (!TryGetOwnedQuarry(quarryId, ownerPlayerId, out quarry))
                return false;

            int oldPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            if (oldPileId <= 0)
                return false;

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(oldPileId, out oldPile))
                return false;

            if (!IsAliveBuilding(oldPile, eStructs.STRUCT_QUARRYPILE, ownerPlayerId))
                return false;

            return true;
        }

        private static bool TryGetOwnedQuarry(
            int quarryId,
            int ownerPlayerId,
            out GameBuilding* quarry)
        {
            quarry = null;

            if (quarryId <= 0)
                return false;

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out quarry))
                return false;

            if (!IsAliveBuilding(quarry, eStructs.STRUCT_QUARRY, ownerPlayerId))
                return false;

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

        private static int FindAliveBuildingIdByGlobalId(int globalId)
        {
            if (globalId <= 0)
                return 0;

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_AliveState == AliveState.IsAlive && (int)building.r_GlobalId == globalId)
                    return spanIndex + 1;
            }

            return 0;
        }

        private static bool DeleteBuildingSafely(int buildingId)
        {
            if (buildingId <= 0)
                return false;

            return GameBuildingManagerAPI.Instance.DeleteBuildingSafe(buildingId);
        }

        private void ClearMapState()
        {
            targetSearch.ClearAllFailedRotationTargets();
            pendingAIQuarriesByGlobalId.Clear();
            activePrefabSpawnCapture = null;
            nextOperationId = 0;
            buttonViewModel.Hide();
        }

        private void BeginMapState()
        {
            ClearMapState();
            mapActive = true;
            aiSpawnObservationArmed = false;
        }

        private void OnMapContentLoaded()
        {
            pendingAIQuarriesByGlobalId.Clear();
            aiSpawnObservationArmed = false;
            RepairLoadedQuarryPileVanillaGroups();
        }

        private void RepairLoadedQuarryPileVanillaGroups()
        {
            if (!IsRuntimeActive() || !targetSearch.NativeHelperAvailable)
                return;

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            var candidates = new List<QuarryPileVanillaGroupCandidate>();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding quarry = ref buildings[spanIndex];
                if (quarry.r_AliveState != AliveState.IsAlive ||
                    quarry.r_BuildingType != eStructs.STRUCT_QUARRY)
                {
                    continue;
                }

                int quarryId = spanIndex + 1;
                int pileId = quarry.r_StoneQuarry_StockPileBuildingId;
                GameBuilding* pile = null;
                bool valid = quarryId <= ushort.MaxValue &&
                    pileId > 0 &&
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(pileId, out pile) &&
                    IsAliveBuilding(pile, eStructs.STRUCT_QUARRYPILE, quarry.r_PlayerIdOwner);
                uint pileGroupId = valid ? Shared.NativeBuildingCompoundGroup.Read(pile) : 0;
                ushort pileLegacyReverseLink = valid ? pile->r_StoneQuarry_StockPileBuildingId : (ushort)0;
                candidates.Add(new QuarryPileVanillaGroupCandidate(
                    quarryId,
                    pileId,
                    Shared.NativeBuildingCompoundGroup.Read(ref quarry),
                    pileGroupId,
                    pileLegacyReverseLink,
                    valid));

                if (valid)
                {
                    QuarryPileVanillaGroupResolution resolution = QuarryPileVanillaGroupPolicy.Resolve(
                        Shared.NativeBuildingCompoundGroup.Read(ref quarry),
                        pileGroupId);
                    if (!resolution.CanUse)
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/RepairLoadedQuarryPileVanillaGroups.alter-inconsistent-loaded-quarry", $"Bugfixes and QoL did not alter an inconsistent loaded quarry-pile Vanilla group: quarryId={quarryId}, pileId={pileId}, quarryGroupId={Shared.NativeBuildingCompoundGroup.Read(ref quarry)}, pileGroupId={pileGroupId}, status={resolution.Status}.");
                    }
                }
            }

            var repairs = new List<QuarryPileVanillaGroupRepair>();
            var ambiguousPileIds = new List<int>();
            QuarryPileVanillaGroupRepairSummary summary = QuarryPileVanillaGroupPolicy.PlanRepairs(
                candidates,
                repairs,
                ambiguousPileIds);

            for (int index = 0; index < ambiguousPileIds.Count; index++)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/RepairLoadedQuarryPileVanillaGroups.repair-ambiguous-loaded-quarry", $"Bugfixes and QoL did not repair an ambiguous loaded quarry-pile group claimed by multiple quarries: pileId={ambiguousPileIds[index]}.");
            }

            int groupIdsCorrected = 0;
            int legacyReverseLinksCleared = 0;
            for (int index = 0; index < repairs.Count; index++)
            {
                QuarryPileVanillaGroupRepair repair = repairs[index];
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(repair.QuarryId, out GameBuilding* quarry) ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(repair.PileId, out GameBuilding* pile) ||
                    !IsAliveBuilding(quarry, eStructs.STRUCT_QUARRY, pile->r_PlayerIdOwner) ||
                    !IsAliveBuilding(pile, eStructs.STRUCT_QUARRYPILE, quarry->r_PlayerIdOwner) ||
                    quarry->r_StoneQuarry_StockPileBuildingId != repair.PileId)
                {
                    continue;
                }

                QuarryPileVanillaGroupResolution currentResolution = QuarryPileVanillaGroupPolicy.Resolve(
                    Shared.NativeBuildingCompoundGroup.Read(quarry),
                    Shared.NativeBuildingCompoundGroup.Read(pile));
                if (!currentResolution.CanUse || currentResolution.GroupId != repair.GroupId)
                    continue;

                if (repair.AssignPileGroup)
                {
                    Shared.NativeBuildingCompoundGroup.Write(pile, repair.GroupId);
                    groupIdsCorrected++;
                }

                if (repair.ClearLegacyReverseLink &&
                    QuarryPileVanillaGroupPolicy.IsLegacyReverseLink(
                        repair.QuarryId,
                        pile->r_StoneQuarry_StockPileBuildingId))
                {
                    pile->r_StoneQuarry_StockPileBuildingId = 0;
                    legacyReverseLinksCleared++;
                }
            }

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogInfo(
                    $"loaded Vanilla structure-group repair completed: candidates={candidates.Count}, validPairs={summary.ValidPairs}, " +
                    $"alreadyValid={summary.AlreadyValid}, plannedRepairs={summary.PlannedRepairs}, groupIdsCorrected={groupIdsCorrected}, " +
                    $"legacyReverseLinksCleared={legacyReverseLinksCleared}, invalid={summary.InvalidCandidates}, " +
                    $"ambiguous={summary.AmbiguousPiles}, rejectedGroups={summary.RejectedGroups}.");
        }

        private void EndMapState()
        {
            mapActive = false;
            aiSpawnObservationArmed = false;
            ClearMapState();
        }

        private void LogInfo(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
                Shared.DebugLogHelper.LogDebugLimited(log, callerFile + ":" + callerLine, message));
        }

        private void LogWarningThreadSafe(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/LogWarningThreadSafe", message));

        private void LogErrorThreadSafe(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/LogErrorThreadSafe", message));

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

            // Process-owned tick registration remains rooted; feature state gates execution.
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
            if (APIShared.GameModes.GameModeHelper.IsMapEditor())
            {
                // Never expose an editor mutation for an object owned by another editor player.
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            }

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : -1;
        }

        private sealed class PendingAIQuarry
        {
            public PendingAIQuarry(
                int quarryId,
                int quarryGlobalId,
                int playerId)
            {
                QuarryId = quarryId;
                QuarryGlobalId = quarryGlobalId;
                PlayerId = playerId;
            }

            public int QuarryId { get; }
            public int QuarryGlobalId { get; }
            public int PlayerId { get; }
            public int? FirstProcessedTick { get; set; }
        }

        private sealed class PrefabSpawnCapture
        {
            public PrefabSpawnCapture(int playerId, int targetX, int targetY)
            {
                PlayerId = playerId;
                TargetX = targetX;
                TargetY = targetY;
            }

            public int PlayerId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public List<int> BuildingIds { get; } = new List<int>();

            public void RecordBuildingId(int buildingId)
            {
                if (buildingId > 0 && !BuildingIds.Contains(buildingId))
                    BuildingIds.Add(buildingId);
            }
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
