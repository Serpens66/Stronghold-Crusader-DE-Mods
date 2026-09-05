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
using RedBird.Core.Memory;

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
        // Vanilla placeQuarry uses size 6 for the quarry, size 2 for its pile and tries 1..9.
        // setupBuildingEntrancesOffset exposes 4 * buildingSize clockwise perimeter candidates.
        private const int VanillaQuarryScale = 6;
        private const int VanillaPileScale = 2;
        private const int VanillaCandidateCount = VanillaQuarryScale * 4;
        private const int VanillaMinimumPlacementTry = 1;
        private const int VanillaMaximumPlacementTry = 9;
        private const int VanillaCandidateOffsetX = 0x31B7D0;
        private const int VanillaCandidateOffsetY = 0x31B7D4;
        private const int VanillaGameBuildingSize = 0x32C;
        private const int VanillaQuarryPileIdOffset = 0x192;
        private const int VanillaStructureGroupIdOffset = 0x2A8;
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
        private readonly BugfixesAndQoLViewModel settings;
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
            try
            {
                if (!ValidateVanillaBuildingLayout())
                {
                    setupBuildingEntrancesOffset = null;
                    return;
                }

                int rva = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    SetupBuildingEntrancesOffsetPattern,
                    SetupBuildingEntrancesOffsetRva,
                    referenceHashMatches,
                    "quarry-pile Vanilla candidate helper",
                    log).Rva;
                setupBuildingEntrancesOffset = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<SetupBuildingEntrancesOffsetDelegate>(
                    IntPtr.Add(libraryHandle, rva));
            }
            catch (Exception ex)
            {
                setupBuildingEntrancesOffset = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile Vanilla candidate helper was not resolved; relocation remains disabled: {ex}");
            }
        }

        private bool ValidateVanillaBuildingLayout()
        {
            int buildingSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GameBuilding));
            int pileIdOffset = System.Runtime.InteropServices.Marshal.OffsetOf(
                typeof(GameBuilding),
                nameof(GameBuilding.r_StoneQuarry_StockPileBuildingId)).ToInt32();
            int structureGroupOffset = System.Runtime.InteropServices.Marshal.OffsetOf(
                typeof(GameBuilding),
                nameof(GameBuilding.r_UsedInSiegeAttemptId)).ToInt32();
            bool compatible = buildingSize == VanillaGameBuildingSize &&
                pileIdOffset == VanillaQuarryPileIdOffset &&
                structureGroupOffset == VanillaStructureGroupIdOffset;
            if (!compatible)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile relocation was disabled because the GameBuilding layout is incompatible with Vanilla's structure-group deletion: size=0x{buildingSize:X}, pileIdOffset=0x{pileIdOffset:X}, structureGroupOffset=0x{structureGroupOffset:X}.");
                return false;
            }

            LogInfo($"validated Vanilla GameBuilding structure-group layout: size=0x{buildingSize:X}, pileIdOffset=0x{pileIdOffset:X}, structureGroupOffset=0x{structureGroupOffset:X}.");
            return true;
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
                    .Subscribe(_ => OnMapContentLoaded()));
                subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => OnMapContentLoaded()));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => EndMapState()));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Subscribe(OnBuildingSpawn));
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;

                installedHook = new Hook(FindSetUpInbuildingMethod(), (SetUpInbuildingDelegate)SetUpInbuildingHook);
                setUpInbuildingTrampoline = installedHook.GenerateTrampoline<SetUpInbuildingDelegate>();
                setUpInbuildingHook = installedHook;
                initialized = true;
                buttonViewModel.Hide();
                LogInfo($"runtime initialized: mode=Vanilla-clockwise-prefab-spawn-and-structure-group, nativeCandidateHelperAvailable={setupBuildingEntrancesOffset != null}, subscriptions={subscriptions.Count}, setUpInbuildingHookInstalled={setUpInbuildingHook != null}.");
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
            buttonViewModel.Hide();
            EndMapState();
            UnhookRelocationButton();
            UnsubscribeTick();
            DisposeSubscriptions();
            setUpInbuildingHook?.Undo();
            setUpInbuildingHook?.Dispose();
            setUpInbuildingHook = null;
            setUpInbuildingTrampoline = null;
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
                    return;
                }

                if (setupBuildingEntrancesOffset == null)
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
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL quarry-pile button visibility refresh failed: {ex}");
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

                localPlayerId = GetControlledPlayerId();
                selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();

                if (!TryGetRelocatableQuarry(selectedBuildingId, localPlayerId, out GameBuilding* quarry, out GameBuilding* oldPile))
                {
                    RefreshButtonVisibility();
                    return;
                }

                int operationId = NextOperationId();

                if (!TryFindNextRotationTarget(localPlayerId, quarry, oldPile, operationId, out PlacementPosition target))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL quarry-pile rotation found no valid clockwise position: playerId={localPlayerId}, operationId={operationId}, quarryId={selectedBuildingId}, quarryGlobalId={quarry->r_GlobalId}, oldPileGlobalId={oldPile->r_GlobalId}.");
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
                    RememberFailedRotationTarget(attemptedOperation);
                    RefreshButtonVisibility();
                    return;
                }

                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                if (attemptedOperation != null)
                    RememberFailedRotationTarget(attemptedOperation);

                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile rotation click failed: selectedBuildingId={selectedBuildingId}, playerId={localPlayerId}: {ex}");
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
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile building-spawn handling failed: exception={ex}");
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
                {
                    int quarryGlobalId = quarryGlobalIds[index];
                    try
                    {
                        TryProcessPendingAIQuarry(quarryGlobalId, tick);
                    }
                    catch (Exception ex)
                    {
                        // One malformed or externally removed quarry must not postpone every other queued quarry.
                        pendingAIQuarriesByGlobalId.Remove(quarryGlobalId);
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Bugfixes and QoL AI quarry-pile queue entry failed and was discarded: tick={tick}, quarryGlobalId={quarryGlobalId}, exception={ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI quarry-pile tick processing failed: tick={tick}, exception={ex}");
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
                    $"Bugfixes and QoL AI quarry-pile placement timed out; the Vanilla pile remains unchanged: playerId={pending.PlayerId}, quarryId={pending.QuarryId}, quarryGlobalId={quarryGlobalId}, tick={tick}.");
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
                    $"Bugfixes and QoL found no safe Keep-facing pile position for a new AI quarry; the Vanilla pile remains unchanged: playerId={pending.PlayerId}, quarryGlobalId={quarryGlobalId}, operationId={operationId}.");
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
                RememberFailedRotationTarget(operation);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL could not move a new AI quarry pile towards its Keep; the existing pile was retained: playerId={pending.PlayerId}, quarryGlobalId={quarryGlobalId}, operationId={operationId}.");
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
            return BugfixesAndQoLChoreSender.IsAvailable(
                networkInitialized && relocationPacketHook != null,
                () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA);
        }

        private bool TrySendRotationChore(QuarryPileRelocationOperation operation)
        {
            if (!IsChoreTransportReady())
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile rotation refused in multiplayer because the Chore transport is unavailable: operationId={operation.OperationId}.");
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
            if (!BugfixesAndQoLChoreSender.TrySend(
                    packet,
                    packetId,
                    networkInitialized && relocationPacketHook != null,
                    value => GameNetworkAPI.Serialize(value),
                    () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA,
                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                    out byte[] body,
                    out string rejectionReason))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile Chore was not queued; no local action was applied: operationId={operation.OperationId}, reason={rejectionReason}.");
                return false;
            }

            LogInfo($"rotation Chore queued: operationId={operation.OperationId}, packetId={packetId}, payloadBytes={sizeof(short) + body.Length}, quarryGlobalId={operation.QuarryGlobalId}, oldPileGlobalId={operation.OldPileGlobalId}, target={operation.TargetTileX},{operation.TargetTileY}.");
            return true;
        }

        private void OnRelocationPacketReceived(ReceiveCustomPacketEventArgs<QuarryPileRelocationPacket> args)
        {
            QuarryPileRelocationPacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion)
            {
                Shared.DebugLogHelper.LogError(
                    log,
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
                if (!initialized || setupBuildingEntrancesOffset == null)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL cannot execute quarry-pile Chore because the relocation runtime is unavailable: operationId={operation.OperationId}, initialized={initialized}, nativeCandidateHelperAvailable={setupBuildingEntrancesOffset != null}.");
                    return;
                }

                if (!TryApplyRotation(operation, "multiplayer-chore", targetAlreadyValidated: false))
                {
                    RememberFailedRotationTarget(operation);
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL quarry-pile Chore completed without relocation: operationId={operation.OperationId}, target={operation.TargetTileX},{operation.TargetTileY}.");
                    return;
                }

                LogInfo($"rotation Chore executed successfully: operationId={operation.OperationId}.");
            }
            catch (Exception ex)
            {
                RememberFailedRotationTarget(operation);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile Chore execution failed: operationId={operation.OperationId}, exception={ex}");
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
            int quarryId = FindAliveBuildingIdByGlobalId(operation.QuarryGlobalId);
            if (quarryId <= 0 ||
                quarryId > ushort.MaxValue ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out GameBuilding* quarry) ||
                !IsAliveBuilding(quarry, eStructs.STRUCT_QUARRY, operation.PlayerId))
                return false;

            int oldPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            if (oldPileId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(oldPileId, out GameBuilding* oldPile) ||
                !IsAliveBuilding(oldPile, eStructs.STRUCT_QUARRYPILE, operation.PlayerId) ||
                (int)oldPile->r_GlobalId != operation.OldPileGlobalId)
                return false;

            PlacementPosition expectedTarget;
            if (targetAlreadyValidated)
            {
                expectedTarget = new PlacementPosition(operation.TargetTileX, operation.TargetTileY);
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
                    return false;
            }

            QuarryPileVanillaGroupResolution groupResolution = QuarryPileVanillaGroupPolicy.Resolve(
                quarry->r_UsedInSiegeAttemptId,
                oldPile->r_UsedInSiegeAttemptId);
            if (!groupResolution.CanUse)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL rejected quarry-pile relocation because Vanilla structure groups are inconsistent: operationId={operation.OperationId}, quarryId={quarryId}, oldPileId={oldPileId}, quarryGroupId={quarry->r_UsedInSiegeAttemptId}, oldPileGroupId={oldPile->r_UsedInSiegeAttemptId}, status={groupResolution.Status}.");
                return false;
            }

            if (groupResolution.RepairsPileGroup)
            {
                oldPile->r_UsedInSiegeAttemptId = groupResolution.GroupId;
                LogInfo($"repaired missing Vanilla structure group before relocation: operationId={operation.OperationId}, quarryId={quarryId}, pileId={oldPileId}, groupId={groupResolution.GroupId}.");
            }

            PileContentSnapshot content = PileContentSnapshot.Capture(oldPile);
            short previousCurrentHealth = oldPile->r_CurrentHealth;
            ushort previousMaxHealth = oldPile->r_MaxHealth;
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
                DeleteBuildingSafely(newPileId);
                return false;
            }

            int newPileGlobalId = (int)newPile->r_GlobalId;
            ushort previousQuarryPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            ushort previousOldPileQuarryId = oldPile->r_StoneQuarry_StockPileBuildingId;
            uint previousOldPileGroupId = oldPile->r_UsedInSiegeAttemptId;
            content.ApplyTo(newPile);
            newPile->r_CurrentHealth = previousCurrentHealth;
            newPile->r_MaxHealth = previousMaxHealth;

            // Vanilla deletes every building sharing this non-zero structure group. A directly
            // spawned quarry pile does not receive the quarry's group automatically.
            newPile->r_UsedInSiegeAttemptId = groupResolution.GroupId;
            newPile->r_StoneQuarry_StockPileBuildingId = 0;
            quarry->r_StoneQuarry_StockPileBuildingId = checked((ushort)newPileId);

            // Detach the replaced pile before its asynchronous deletion so only the replacement
            // remains in Vanilla's multi-building structure group.
            oldPile->r_UsedInSiegeAttemptId = 0;
            if (QuarryPileVanillaGroupPolicy.IsLegacyReverseLink(quarryId, previousOldPileQuarryId))
                oldPile->r_StoneQuarry_StockPileBuildingId = 0;
            ClearPileContentBeforeDeletion(oldPile);

            bool oldPileMarkedForDeletion = oldPile->r_AliveState == AliveState.MarkedForDeletion ||
                DeleteBuildingSafely(oldPileId);
            if (!oldPileMarkedForDeletion)
            {
                quarry->r_StoneQuarry_StockPileBuildingId = previousQuarryPileId;
                oldPile->r_UsedInSiegeAttemptId = previousOldPileGroupId;
                oldPile->r_StoneQuarry_StockPileBuildingId = previousOldPileQuarryId;
                content.ApplyTo(oldPile);
                newPile->r_UsedInSiegeAttemptId = 0;
                newPile->r_StoneQuarry_StockPileBuildingId = 0;
                newPile->r_StoneBlocksAmount = 0;
                newPile->r_CurrentGoodStackAmount = 0;
                DeleteBuildingSafely(newPileId);
                return false;
            }

            ClearFailedRotationTargets(operation.QuarryGlobalId);
            LogInfo(
                $"rotation completed: reason={reason}, playerId={operation.PlayerId}, quarryGlobalId={operation.QuarryGlobalId}, " +
                $"newPileGlobalId={newPileGlobalId}, vanillaGroupId={groupResolution.GroupId}, target={expectedTarget.X},{expectedTarget.Y}.");
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
                return false;

            int quarryScale = GetBuildingScale(quarry);
            int buildingScale = GetBuildingScale(oldPile);
            if (quarryScale != VanillaQuarryScale || buildingScale != VanillaPileScale)
                return false;

            int quarryGlobalId = (int)quarry->r_GlobalId;
            int oldPileGlobalId = (int)oldPile->r_GlobalId;
            int oldX = oldPile->r_TilePositionXBegin;
            int oldY = oldPile->r_TilePositionYBegin;
            if (!TryResolveVanillaCursor(
                quarry,
                oldX,
                oldY,
                out int currentIndex))
                return false;

            HashSet<long> failedTargets = GetFailedRotationTargets(quarryGlobalId, oldPileGlobalId);

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
                        return false;

                    if (candidate.X == oldX && candidate.Y == oldY)
                        continue;

                    if (failedTargets != null && failedTargets.Contains(GetPositionKey(candidate)))
                        continue;

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
                    return true;
                }
            }

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
                return false;

            int oldX = oldPile->r_TilePositionXBegin;
            int oldY = oldPile->r_TilePositionYBegin;
            var candidates = new List<QuarryPileTargetCandidate>();
            var seenPositions = new HashSet<long>();
            var currentPosition = new PlacementPosition(oldX, oldY);
            seenPositions.Add(GetPositionKey(currentPosition));

            HashSet<long> failedTargets = GetFailedRotationTargets(
                (int)quarry->r_GlobalId,
                (int)oldPile->r_GlobalId);
            int placementTry = VanillaMinimumPlacementTry;
            for (int candidateIndex = 0; candidateIndex < VanillaCandidateCount; candidateIndex++)
            {
                if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                    return false;

                long positionKey = GetPositionKey(candidate);
                if (candidate.X == oldX && candidate.Y == oldY)
                {
                    candidates.Add(new QuarryPileTargetCandidate(
                        oldX,
                        oldY,
                        placementTry,
                        candidateIndex,
                        isCurrentPosition: true));
                    continue;
                }

                if (!seenPositions.Add(positionKey) ||
                    (failedTargets != null && failedTargets.Contains(positionKey)) ||
                    !ValidateCandidateWithGame(
                        playerId,
                        candidate,
                        pileScale,
                        operationId,
                        candidateIndex,
                        placementTry))
                {
                    continue;
                }

                candidates.Add(new QuarryPileTargetCandidate(
                    candidate.X,
                    candidate.Y,
                    placementTry,
                    candidateIndex,
                    isCurrentPosition: false));
            }

            int keepCenterXTimesTwo = keep->r_TilePositionXBegin + keep->r_TilePositionXEnd;
            int keepCenterYTimesTwo = keep->r_TilePositionYBegin + keep->r_TilePositionYEnd;
            if (!QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(
                candidates,
                VanillaMinimumPlacementTry,
                keepCenterXTimesTwo,
                keepCenterYTimesTwo,
                out QuarryPileTargetCandidate selected))
            {
                target = currentPosition;
                currentPositionIsBest = true;
                LogInfo(
                    $"AI Keep-facing pile remains unchanged: playerId={playerId}, quarryGlobalId={quarry->r_GlobalId}, " +
                    $"reason=no-valid-placementTry-{VanillaMinimumPlacementTry}-position.");
                return true;
            }

            target = new PlacementPosition(selected.X, selected.Y);
            currentPositionIsBest = selected.IsCurrentPosition;
            LogInfo(
                $"AI Keep-facing target selected: playerId={playerId}, quarryGlobalId={quarry->r_GlobalId}, " +
                $"target={selected.X},{selected.Y}, vanillaTry={selected.PlacementTry}, candidateIndex={selected.CandidateIndex}, currentPositionIsBest={currentPositionIsBest}.");
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
                return false;
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
            out int currentIndex)
        {
            currentIndex = VanillaCandidateCount / 4;
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
                    $"Bugfixes and QoL quarry-pile Vanilla candidate generation failed: candidateIndex={candidateIndex}, " +
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
            int placementTry)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!TryGetSafeTileId(tileApi, candidate.X, candidate.Y, out _))
                return false;

            bool previousBlockedState = tileApi.TileManager.IsPlacementBlocked;
            try
            {
                tileApi.TileManager.IsPlacementBlocked = false;
                BulkBuildingDetours.c_game_player_build_placement_validator_hook_impl(
                    tileApi.GetTileManager(),
                    playerId,
                    candidate.X,
                    candidate.Y,
                    eMappers.MAPPER_QUARRYPILE,
                    buildingScale,
                    0);
                bool blocked = tileApi.TileManager.IsPlacementBlocked;
                return !blocked;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL quarry-pile native placement validation failed: operationId={operationId}, vanillaTry={placementTry}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}: {ex}");
                return false;
            }
            finally
            {
                tileApi.TileManager.IsPlacementBlocked = previousBlockedState;
            }
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
            int buildingScale = GetBuildingScale(oldPile);
            if (buildingScale <= 0)
                return false;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!TryGetSafeTileId(tileApi, target.X, target.Y, out _))
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
                Shared.DebugLogHelper.LogError(
                    log,
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
                Shared.DebugLogHelper.LogWarning(
                    log,
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
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL could not clean up an invalid quarry-pile prefab: operationId={operationId}, reason={reason}, buildingId={buildingId}.");
                }
            }
        }

        private static bool TryGetSafeTileId(
            GameTileManagerAPI tileApi,
            int tileX,
            int tileY,
            out int tileId)
        {
            tileId = -1;
            if (tileApi == null || !tileApi.IsTileInsideMapBounds(tileX, tileY))
                return false;

            int resolvedTileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(resolvedTileId))
                return false;

            tileId = resolvedTileId;
            return true;
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

        private static int GetBuildingScale(GameBuilding* building)
        {
            uint gridSize = building->r_OccupyTileGridSize;
            return gridSize > 0 && gridSize <= int.MaxValue ? (int)gridSize : 0;
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

        private HashSet<long> GetFailedRotationTargets(int quarryGlobalId, int oldPileGlobalId)
        {
            if (!failedRotationTargetsByQuarry.TryGetValue(quarryGlobalId, out FailedRotationTargets state))
                return null;

            if (state.OldPileGlobalId == oldPileGlobalId)
                return state.Targets;

            failedRotationTargetsByQuarry.Remove(quarryGlobalId);
            return null;
        }

        private void RememberFailedRotationTarget(QuarryPileRelocationOperation operation)
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
            state.Targets.Add(GetPositionKey(target));
        }

        private void ClearFailedRotationTargets(int quarryGlobalId)
        {
            if (quarryGlobalId > 0)
                failedRotationTargetsByQuarry.Remove(quarryGlobalId);
        }

        private static long GetPositionKey(PlacementPosition position)
        {
            return ((long)position.X << 32) | (uint)position.Y;
        }

        private void ClearMapState()
        {
            failedRotationTargetsByQuarry.Clear();
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
            if (!IsRuntimeActive() || setupBuildingEntrancesOffset == null)
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
                uint pileGroupId = valid ? pile->r_UsedInSiegeAttemptId : 0;
                ushort pileLegacyReverseLink = valid ? pile->r_StoneQuarry_StockPileBuildingId : (ushort)0;
                candidates.Add(new QuarryPileVanillaGroupCandidate(
                    quarryId,
                    pileId,
                    quarry.r_UsedInSiegeAttemptId,
                    pileGroupId,
                    pileLegacyReverseLink,
                    valid));

                if (valid)
                {
                    QuarryPileVanillaGroupResolution resolution = QuarryPileVanillaGroupPolicy.Resolve(
                        quarry.r_UsedInSiegeAttemptId,
                        pileGroupId);
                    if (!resolution.CanUse)
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Bugfixes and QoL did not alter an inconsistent loaded quarry-pile Vanilla group: quarryId={quarryId}, pileId={pileId}, quarryGroupId={quarry.r_UsedInSiegeAttemptId}, pileGroupId={pileGroupId}, status={resolution.Status}.");
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
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL did not repair an ambiguous loaded quarry-pile group claimed by multiple quarries: pileId={ambiguousPileIds[index]}.");
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
                    quarry->r_UsedInSiegeAttemptId,
                    pile->r_UsedInSiegeAttemptId);
                if (!currentResolution.CanUse || currentResolution.GroupId != repair.GroupId)
                    continue;

                if (repair.AssignPileGroup)
                {
                    pile->r_UsedInSiegeAttemptId = repair.GroupId;
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

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogDebug(log, $"Bugfixes and QoL quarry-pile runtime: {message}");
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
