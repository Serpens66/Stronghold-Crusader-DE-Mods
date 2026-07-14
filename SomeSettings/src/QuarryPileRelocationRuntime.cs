using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MessagePack.Formatters;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
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
using System.Reflection;

namespace SomeSettings
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(QuarryPileRelocationPacketFormatter))]
    public sealed class QuarryPileRelocationPacket
    {
        [Key(0)] public int SourcePlayerId;
        [Key(1)] public int RequestId;
        [Key(2)] public int QuarryGlobalId;
        [Key(3)] public int OldPileGlobalId;
        [Key(4)] public int TargetTileX;
        [Key(5)] public int TargetTileY;
    }

    public sealed class QuarryPileRelocationPacketFormatter : IMessagePackFormatter<QuarryPileRelocationPacket>
    {
        public void Serialize(
            ref MessagePackWriter writer,
            QuarryPileRelocationPacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(6);
            writer.Write(value.SourcePlayerId);
            writer.Write(value.RequestId);
            writer.Write(value.QuarryGlobalId);
            writer.Write(value.OldPileGlobalId);
            writer.Write(value.TargetTileX);
            writer.Write(value.TargetTileY);
        }

        public QuarryPileRelocationPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            QuarryPileRelocationPacket packet = new QuarryPileRelocationPacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: packet.SourcePlayerId = reader.ReadInt32(); break;
                    case 1: packet.RequestId = reader.ReadInt32(); break;
                    case 2: packet.QuarryGlobalId = reader.ReadInt32(); break;
                    case 3: packet.OldPileGlobalId = reader.ReadInt32(); break;
                    case 4: packet.TargetTileX = reader.ReadInt32(); break;
                    case 5: packet.TargetTileY = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
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
        private delegate void SetUpInbuildingDelegate(MainViewModel self, int overridePanel, int overrideType);

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly QuarryPileRelocationButtonViewModel buttonViewModel;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, HashSet<int>> processedRequestIds = new Dictionary<int, HashSet<int>>();

        private Hook setUpInbuildingHook;
        private SetUpInbuildingDelegate setUpInbuildingTrampoline;
        private R3PacketEventHook<QuarryPileRelocationPacket> packetHook;
        private Button hookedRelocationButton;
        private TextBlock hookedRelocationTooltip;
        private PrefabSpawnCapture activePrefabSpawnCapture;
        private int nextRequestId;
        private int linkedRemovalSuppressionDepth;
        private bool initialized;
        private string lastVisibilityLogState;

        public QuarryPileRelocationRuntime(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            buttonViewModel = new QuarryPileRelocationButtonViewModel(OnRelocateCommand);
        }

        public QuarryPileRelocationButtonViewModel ButtonViewModel => buttonViewModel;

        public void Initialize()
        {
            if (initialized)
                return;

            Hook installedHook = null;
            try
            {
                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<QuarryPileRelocationPacket>();
                subscriptions.Add(packetHook.GetBaseHook().Observable.Subscribe(OnRelocationPacketReceived));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => ClearMapState()));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(args => OnLinkedBuildingRemoval(args.BuildingId, "bulldoze-pre")));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(args => OnLinkedBuildingRemoval(args.BuildingId, "delete-pre")));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Subscribe(OnBuildingSpawn));

                installedHook = new Hook(FindSetUpInbuildingMethod(), (SetUpInbuildingDelegate)SetUpInbuildingHook);
                setUpInbuildingTrampoline = installedHook.GenerateTrampoline<SetUpInbuildingDelegate>();
                setUpInbuildingHook = installedHook;
                initialized = true;
                buttonViewModel.Hide();
                LogInfo($"runtime initialized: mode=clockwise-prefab-spawn, packetId={packetHook.GetPacketId()}, subscriptions={subscriptions.Count}, setUpInbuildingHookInstalled={setUpInbuildingHook != null}.");
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
            LogInfo("runtime dispose started.");
            buttonViewModel.Hide();
            ClearMapState();
            UnhookRelocationButton();
            DisposeSubscriptions();
            setUpInbuildingHook?.Undo();
            setUpInbuildingHook?.Dispose();
            setUpInbuildingHook = null;
            setUpInbuildingTrampoline = null;
            packetHook = null;
            LogInfo("runtime dispose completed.");
        }

        public void ApplySetting()
        {
            LogInfo($"setting applied: EnableMod={settings.EnableMod}, EnableQuarryPileRelocation={settings.EnableQuarryPileRelocation}.");
            if (settings.EnableMod && settings.EnableQuarryPileRelocation)
            {
                RefreshButtonVisibility();
                return;
            }

            buttonViewModel.Hide();
            HideRelocationTooltip();
            processedRequestIds.Clear();
        }

        public void RefreshButtonVisibility()
        {
            try
            {
                if (!settings.EnableMod || !settings.EnableQuarryPileRelocation)
                {
                    buttonViewModel.Hide();
                    HideRelocationTooltip();
                    LogVisibilityState($"hidden: feature-disabled, EnableMod={settings.EnableMod}, EnableQuarryPileRelocation={settings.EnableQuarryPileRelocation}");
                    return;
                }

                int localPlayerId = GetLocalPlayerIdOrOne();
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
                Shared.DebugLogHelper.LogError(log, $"SomeSettings quarry-pile button visibility refresh failed: {ex}");
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
            Button button = mainViewModel?.HUDBuildingPanel?.FindName("SomeSettingsQuarryPileRelocationButton") as Button;
            TextBlock tooltip = mainViewModel?.HUDBuildingPanel?.FindName("SomeSettingsQuarryPileRelocationTooltipHost") as TextBlock;

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
                LogInfo("UI lookup did not find SomeSettingsQuarryPileRelocationButton after setUpInbuilding.");
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
                LogInfo("tooltip hover entered, but SomeSettingsQuarryPileRelocationTooltipHost was not found.");
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

            try
            {
                LogInfo($"rotation command invoked: EnableMod={settings.EnableMod}, EnableQuarryPileRelocation={settings.EnableQuarryPileRelocation}.");
                if (!settings.EnableMod || !settings.EnableQuarryPileRelocation)
                {
                    LogInfo("rotation command stopped: feature is disabled.");
                    return;
                }

                localPlayerId = GetLocalPlayerIdOrOne();
                selectedBuildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                LogInfo($"command context read: localPlayerId={localPlayerId}, selectedBuildingId={selectedBuildingId}, appMode={GameData.Instance?.app_mode.ToString() ?? "unavailable"}, appSubMode={GameData.Instance?.app_sub_mode.ToString() ?? "unavailable"}.");

                if (!TryGetRelocatableQuarry(selectedBuildingId, localPlayerId, out GameBuilding* quarry, out GameBuilding* oldPile, out string failureReason))
                {
                    LogInfo($"rotation command stopped: selected quarry is not eligible, reason={failureReason}.");
                    RefreshButtonVisibility();
                    return;
                }

                int requestId = NextRequestId();
                LogInfo($"selected quarry validated: requestId={requestId}, quarryId={selectedBuildingId}, quarryGlobalId={quarry->r_GlobalId}, owner={quarry->r_PlayerIdOwner}, quarryTiles={quarry->r_TilePositionXBegin},{quarry->r_TilePositionYBegin}-{quarry->r_TilePositionXEnd},{quarry->r_TilePositionYEnd}, oldPileId={quarry->r_StoneQuarry_StockPileBuildingId}, oldPileGlobalId={oldPile->r_GlobalId}, oldPileTiles={oldPile->r_TilePositionXBegin},{oldPile->r_TilePositionYBegin}-{oldPile->r_TilePositionXEnd},{oldPile->r_TilePositionYEnd}, oldPileGridSize={oldPile->r_OccupyTileGridSize}.");

                if (!TryFindNextRotationTarget(localPlayerId, quarry, oldPile, requestId, out RingPosition target))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"SomeSettings quarry-pile rotation found no valid clockwise position: playerId={localPlayerId}, requestId={requestId}, quarryId={selectedBuildingId}, quarryGlobalId={quarry->r_GlobalId}, oldPileGlobalId={oldPile->r_GlobalId}.");
                    return;
                }

                QuarryPileRelocationPacket packet = new QuarryPileRelocationPacket
                {
                    SourcePlayerId = localPlayerId,
                    RequestId = requestId,
                    QuarryGlobalId = (int)quarry->r_GlobalId,
                    OldPileGlobalId = (int)oldPile->r_GlobalId,
                    TargetTileX = target.X,
                    TargetTileY = target.Y
                };

                if (!TryApplyRotation(packet, "local-click", targetAlreadyValidated: true))
                {
                    LogInfo($"rotation command stopped: replacement transaction failed, requestId={requestId}, target={target.X},{target.Y}.");
                    RefreshButtonVisibility();
                    return;
                }

                SendRelocationPacket(packet);
                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings quarry-pile rotation click failed: selectedBuildingId={selectedBuildingId}, playerId={localPlayerId}: {ex}");
                RefreshButtonVisibility();
            }
        }

        private void SendRelocationPacket(QuarryPileRelocationPacket packet)
        {
            bool networked = GameNetworkAPI.IsNetworkedEnvironment();
            if (!networked || packetHook == null)
            {
                LogInfo($"network packet not required: networked={networked}, packetHookAvailable={packetHook != null}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}.");
                return;
            }

            GameNetworkAPI.SendPacketToAll(packet, packetHook.GetPacketId(), true);
            LogInfo($"network packet sent: packetId={packetHook.GetPacketId()}, instant=true, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, quarryGlobalId={packet.QuarryGlobalId}, oldPileGlobalId={packet.OldPileGlobalId}, target={packet.TargetTileX},{packet.TargetTileY}.");
        }

        private void OnRelocationPacketReceived(ReceiveCustomPacketEventArgs<QuarryPileRelocationPacket> args)
        {
            try
            {
                if (args?.Packet == null)
                {
                    LogInfo("network packet callback received without a packet payload.");
                    return;
                }

                QuarryPileRelocationPacket packet = args.Packet;
                LogInfo($"network packet received: phase={args.Phase}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, quarryGlobalId={packet.QuarryGlobalId}, oldPileGlobalId={packet.OldPileGlobalId}, target={packet.TargetTileX},{packet.TargetTileY}, EnableMod={settings.EnableMod}, EnableQuarryPileRelocation={settings.EnableQuarryPileRelocation}.");
                if (!settings.EnableMod || !settings.EnableQuarryPileRelocation)
                {
                    LogInfo("network packet ignored: feature is disabled.");
                    return;
                }

                string packetFailure = GetPacketValidationFailure(packet);
                if (packetFailure != null)
                {
                    LogInfo($"network packet rejected: reason={packetFailure}.");
                    return;
                }

                if (IsDuplicatePacket(packet.SourcePlayerId, packet.RequestId))
                {
                    LogInfo($"network packet ignored as duplicate: playerId={packet.SourcePlayerId}, requestId={packet.RequestId}.");
                    return;
                }

                bool applied = TryApplyRotation(packet, "network-packet");
                LogInfo($"network packet application finished: playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, applied={applied}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings quarry-pile rotation packet handling failed: {ex}");
            }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            PrefabSpawnCapture capture = activePrefabSpawnCapture;
            if (capture == null || args == null)
                return;

            try
            {
                bool matchesExpectedInput = args.PlayerId == capture.PlayerId &&
                    args.Building == eStructs.STRUCT_QUARRYPILE &&
                    args.TileX == capture.TargetX &&
                    args.TileY == capture.TargetY;
                LogInfo(
                    $"prefab spawn event observed: requestId={capture.RequestId}, phase={args.Phase}, playerId={args.PlayerId}, " +
                    $"building={args.Building}, target={args.TileX},{args.TileY}, scale={args.BuildingScale}, returnValue={args.ReturnValue}, matchesExpectedInput={matchesExpectedInput}.");

                if (args.Phase != EventHookPhase.Post || !matchesExpectedInput)
                    return;

                if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                {
                    capture.InvalidPostEventCount++;
                    return;
                }

                capture.RecordBuildingId((int)args.ReturnValue);
                LogInfo($"prefab spawn id captured: requestId={capture.RequestId}, buildingId={args.ReturnValue}, capturedCount={capture.BuildingIds.Count}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings quarry-pile prefab spawn capture failed: requestId={capture.RequestId}, exception={ex}");
            }
        }

        private void OnLinkedBuildingRemoval(int buildingId, string source)
        {
            try
            {
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
                    $"SomeSettings quarry-pile linked demolition handling failed: source={source}, buildingId={buildingId}, exception={ex}");
            }
        }

        private static string GetPacketValidationFailure(QuarryPileRelocationPacket packet)
        {
            if (packet.SourcePlayerId <= 0)
                return "source-player-id-not-positive";
            if (packet.RequestId <= 0)
                return "request-id-not-positive";
            if (packet.QuarryGlobalId <= 0)
                return "quarry-global-id-not-positive";
            if (packet.OldPileGlobalId <= 0)
                return "old-pile-global-id-not-positive";
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(packet.SourcePlayerId))
                return "source-player-id-invalid";
            if (GamePlayerManagerAPI.Instance.IsAIPlayer(packet.SourcePlayerId))
                return "source-player-is-ai";
            if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(packet.TargetTileX, packet.TargetTileY))
                return "target-tile-outside-map";
            return null;
        }

        private bool IsDuplicatePacket(int sourcePlayerId, int requestId)
        {
            if (!processedRequestIds.TryGetValue(sourcePlayerId, out HashSet<int> requestIds))
            {
                requestIds = new HashSet<int>();
                processedRequestIds[sourcePlayerId] = requestIds;
            }

            return !requestIds.Add(requestId);
        }

        private bool TryApplyRotation(
            QuarryPileRelocationPacket packet,
            string reason,
            bool targetAlreadyValidated = false)
        {
            LogInfo($"replacement transaction started: reason={reason}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, quarryGlobalId={packet.QuarryGlobalId}, oldPileGlobalId={packet.OldPileGlobalId}, requestedTarget={packet.TargetTileX},{packet.TargetTileY}.");

            int quarryId = FindAliveBuildingIdByGlobalId(packet.QuarryGlobalId);
            if (quarryId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(quarryId, out GameBuilding* quarry) ||
                !IsAliveBuilding(quarry, eStructs.STRUCT_QUARRY, packet.SourcePlayerId))
            {
                LogInfo($"replacement transaction rejected: reason=quarry-not-found, requestId={packet.RequestId}.");
                return false;
            }

            int oldPileId = quarry->r_StoneQuarry_StockPileBuildingId;
            if (oldPileId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(oldPileId, out GameBuilding* oldPile) ||
                !IsAliveBuilding(oldPile, eStructs.STRUCT_QUARRYPILE, packet.SourcePlayerId) ||
                (int)oldPile->r_GlobalId != packet.OldPileGlobalId)
            {
                LogInfo($"replacement transaction rejected: reason=old-pile-link-changed, quarryId={quarryId}, currentLinkedPileId={oldPileId}, requestId={packet.RequestId}.");
                return false;
            }

            RingPosition expectedTarget;
            if (targetAlreadyValidated)
            {
                expectedTarget = new RingPosition(packet.TargetTileX, packet.TargetTileY);
                LogInfo($"replacement transaction reuses synchronously validated local target: requestId={packet.RequestId}, target={expectedTarget.X},{expectedTarget.Y}.");
            }
            else if (!TryFindNextRotationTarget(packet.SourcePlayerId, quarry, oldPile, packet.RequestId, out expectedTarget))
            {
                LogInfo($"replacement transaction rejected: reason=no-valid-clockwise-target, requestId={packet.RequestId}.");
                return false;
            }
            else if (expectedTarget.X != packet.TargetTileX || expectedTarget.Y != packet.TargetTileY)
            {
                LogInfo($"replacement transaction rejected: reason=target-mismatch, requestId={packet.RequestId}, expected={expectedTarget.X},{expectedTarget.Y}, received={packet.TargetTileX},{packet.TargetTileY}.");
                return false;
            }

            PileContentSnapshot content = PileContentSnapshot.Capture(oldPile);
            short previousCurrentHealth = oldPile->r_CurrentHealth;
            ushort previousMaxHealth = oldPile->r_MaxHealth;
            if (!TrySpawnReplacement(
                packet.SourcePlayerId,
                quarryId,
                quarry,
                oldPileId,
                oldPile,
                expectedTarget,
                packet.RequestId,
                out int newPileId,
                out GameBuilding* newPile))
            {
                return false;
            }

            if (newPileId > ushort.MaxValue)
            {
                DeleteBuildingWithoutLinkedPropagation(newPileId, "rotation-new-id-overflow");
                LogInfo($"replacement transaction rolled back: reason=new-pile-id-exceeds-link-field, requestId={packet.RequestId}, newPileId={newPileId}.");
                return false;
            }

            int newPileGlobalId = (int)newPile->r_GlobalId;
            content.ApplyTo(newPile);
            newPile->r_CurrentHealth = previousCurrentHealth;
            newPile->r_MaxHealth = previousMaxHealth;
            ClearPileContentBeforeDeletion(oldPileId, oldPile, packet.RequestId, "rotation-old-pile");

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
                LogInfo($"replacement transaction rolled back: reason=old-pile-delete-failed, requestId={packet.RequestId}, oldPileId={oldPileId}, newPileId={newPileId}, replacementMarkedForDeletion={replacementMarkedForDeletion}.");
                return false;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"SomeSettings quarry-pile rotation completed: reason={reason}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, quarryId={quarryId}, quarryGlobalId={packet.QuarryGlobalId}, oldPileId={oldPileId}, oldPileGlobalId={packet.OldPileGlobalId}, newPileId={newPileId}, newPileGlobalId={newPileGlobalId}, newPileState={newPile->r_AliveState}, target={expectedTarget.X},{expectedTarget.Y}, stoneBlocks={newPile->r_StoneBlocksAmount}, oldPileMarkedForDeletion={oldPileMarkedForDeletion}, visualLifecycle=prefab-managed.");
            return true;
        }

        private bool TryFindNextRotationTarget(
            int playerId,
            GameBuilding* quarry,
            GameBuilding* oldPile,
            int requestId,
            out RingPosition target)
        {
            target = default;
            int pileWidth = GetBuildingWidth(oldPile);
            int pileHeight = GetBuildingHeight(oldPile);
            int buildingScale = GetBuildingScale(oldPile);
            if (pileWidth <= 0 || pileHeight <= 0)
            {
                LogInfo($"rotation target search failed: requestId={requestId}, reason=invalid-pile-footprint, width={pileWidth}, height={pileHeight}.");
                return false;
            }

            if (buildingScale <= 0)
            {
                LogInfo($"rotation target search failed: requestId={requestId}, reason=invalid-pile-grid-size, gridSize={oldPile->r_OccupyTileGridSize}.");
                return false;
            }

            List<RingPosition> ring = CreateClockwiseRing(quarry, oldPile, pileWidth, pileHeight, out RingGeometry ringGeometry);
            if (ring.Count < 2)
            {
                LogInfo($"rotation target search failed: requestId={requestId}, reason=ring-empty, ringCount={ring.Count}.");
                return false;
            }

            int oldX = Math.Min(oldPile->r_TilePositionXBegin, oldPile->r_TilePositionXEnd);
            int oldY = Math.Min(oldPile->r_TilePositionYBegin, oldPile->r_TilePositionYEnd);
            int currentIndex = FindRingPosition(ring, oldX, oldY);
            bool exactCurrentPosition = currentIndex >= 0;
            if (!exactCurrentPosition)
                currentIndex = FindNearestRingPosition(ring, oldPile, pileWidth, pileHeight);

            LogInfo(
                $"rotation target search started: requestId={requestId}, playerId={playerId}, pileFootprint={pileWidth}x{pileHeight}, " +
                $"buildingScale={buildingScale}, ringCount={ring.Count}, ringCenter2={ringGeometry.CenterX2},{ringGeometry.CenterY2}, " +
                $"ringRadius2={ringGeometry.Radius2}, ringBounds={ringGeometry.Left},{ringGeometry.Top}-{ringGeometry.Right},{ringGeometry.Bottom}, " +
                $"oldAnchor={oldX},{oldY}, startIndex={currentIndex}, exactCurrentPosition={exactCurrentPosition}.");

            int attempts = exactCurrentPosition ? ring.Count - 1 : ring.Count;
            for (int offset = 1; offset <= attempts; offset++)
            {
                int candidateIndex = (currentIndex + offset) % ring.Count;
                RingPosition candidate = ring[candidateIndex];

                if (candidate.X == oldX && candidate.Y == oldY)
                    continue;

                if (RectanglesOverlap(
                    candidate.X,
                    candidate.Y,
                    pileWidth,
                    pileHeight,
                    oldX,
                    oldY,
                    pileWidth,
                    pileHeight))
                {
                    LogInfo($"rotation candidate skipped: requestId={requestId}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}, reason=overlaps-existing-pile.");
                    continue;
                }

                if (!IsFootprintInsideMap(candidate.X, candidate.Y, pileWidth, pileHeight))
                {
                    LogInfo($"rotation candidate skipped: requestId={requestId}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}, reason=footprint-outside-map.");
                    continue;
                }

                if (!ValidateCandidateWithGame(playerId, candidate, buildingScale, requestId, candidateIndex))
                    continue;

                target = candidate;
                LogInfo($"rotation target selected: requestId={requestId}, candidateIndex={candidateIndex}, target={target.X},{target.Y}, attemptsUsed={offset}.");
                return true;
            }

            LogInfo($"rotation target search exhausted: requestId={requestId}, playerId={playerId}, ringCount={ring.Count}, attempts={attempts}.");
            return false;
        }

        private bool ValidateCandidateWithGame(
            int playerId,
            RingPosition candidate,
            int buildingScale,
            int requestId,
            int candidateIndex)
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
                LogInfo($"native placement validation returned: requestId={requestId}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}, buildingScale={buildingScale}, returnValue={validatorResult}, blocked={blocked}.");
                return !blocked;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings quarry-pile native placement validation failed: requestId={requestId}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}: {ex}");
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
            RingPosition target,
            int requestId,
            out int newPileId,
            out GameBuilding* newPile)
        {
            newPileId = 0;
            newPile = null;
            int buildingScale = GetBuildingScale(oldPile);
            if (buildingScale <= 0)
            {
                LogInfo($"prefab replacement spawn rejected: requestId={requestId}, reason=invalid-pile-grid-size, gridSize={oldPile->r_OccupyTileGridSize}.");
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int targetTileId = tileApi.GetTileId(target.X, target.Y);
            ushort targetStructureBefore = tileApi.GetTileBuildingId(targetTileId);
            ushort quarryLinkBefore = quarry->r_StoneQuarry_StockPileBuildingId;
            PrefabSpawnCapture capture = new PrefabSpawnCapture(requestId, playerId, target.X, target.Y);
            if (activePrefabSpawnCapture != null)
            {
                LogInfo($"prefab replacement spawn rejected: requestId={requestId}, reason=another-prefab-capture-active, activeRequestId={activePrefabSpawnCapture.RequestId}.");
                return false;
            }

            long result = 0;
            Exception prefabException = null;
            LogInfo(
                $"prefab replacement spawn started: requestId={requestId}, playerId={playerId}, quarryId={quarryId}, oldPileId={oldPileId}, " +
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
                $"prefab replacement spawn returned: requestId={requestId}, returnValue={result}, capturedIds={capture.DescribeBuildingIds()}, " +
                $"invalidPostEvents={capture.InvalidPostEventCount}, targetStructureAfter={targetStructureAfter}, quarryLinkAfter={quarryLinkAfter}, " +
                $"oldPileStateAfter={oldPile->r_AliveState}, exception={(prefabException == null ? "none" : prefabException.GetType().FullName)}.");

            if (prefabException != null)
            {
                int fallbackPileId = FindFreshPileAtTarget(oldPileId, playerId, target, out _);
                CleanupFailedPrefabSpawns(capture, oldPileId, requestId, "prefab-exception", fallbackPileId);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings quarry-pile prefab replacement spawn failed: requestId={requestId}, fallbackPileId={fallbackPileId}, exception={prefabException}");
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
                LogInfo($"prefab replacement fallback scan completed: requestId={requestId}, fallbackPileId={newPileId}, actual={DescribeBuilding(newPile)}.");
            }

            bool replacementVerified = newPileId > 0 &&
                IsValidFreshSpawn(newPile, eStructs.STRUCT_QUARRYPILE, playerId) &&
                Math.Min(newPile->r_TilePositionXBegin, newPile->r_TilePositionXEnd) == target.X &&
                Math.Min(newPile->r_TilePositionYBegin, newPile->r_TilePositionYEnd) == target.Y &&
                newPile->r_OccupyTileGridSize == oldPile->r_OccupyTileGridSize;
            if (!replacementVerified)
            {
                LogInfo(
                    $"prefab replacement spawn verification failed before cleanup: requestId={requestId}, returnValue={result}, newPileId={newPileId}, " +
                    $"expectedType={eStructs.STRUCT_QUARRYPILE}, expectedOwner={playerId}, expectedAnchor={target.X},{target.Y}, expectedGridSize={oldPile->r_OccupyTileGridSize}, " +
                    $"capturedIds={capture.DescribeBuildingIds()}, actual={DescribeBuilding(newPile)}.");
                CleanupFailedPrefabSpawns(capture, oldPileId, requestId, "verification-failed", newPileId);
                newPile = null;
                newPileId = 0;
                return false;
            }

            LogInfo(
                $"prefab replacement spawn verified: requestId={requestId}, newPileId={newPileId}, newPileGlobalId={newPile->r_GlobalId}, " +
                $"tiles={newPile->r_TilePositionXBegin},{newPile->r_TilePositionYBegin}-{newPile->r_TilePositionXEnd},{newPile->r_TilePositionYEnd}, " +
                $"gridSize={newPile->r_OccupyTileGridSize}, tileRegistration={DescribeTileRegistration(newPileId, newPile)}, " +
                $"quarryLinkAfterPrefab={quarry->r_StoneQuarry_StockPileBuildingId}, oldPileStateAfterPrefab={oldPile->r_AliveState}.");
            return true;
        }

        private bool TryResolveCapturedReplacement(
            PrefabSpawnCapture capture,
            int oldPileId,
            int playerId,
            RingPosition target,
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

                bool matchesTarget = Math.Min(candidate->r_TilePositionXBegin, candidate->r_TilePositionXEnd) == target.X &&
                    Math.Min(candidate->r_TilePositionYBegin, candidate->r_TilePositionYEnd) == target.Y &&
                    candidate->r_OccupyTileGridSize == expectedGridSize;
                LogInfo($"captured prefab candidate inspected: requestId={capture.RequestId}, candidateId={candidateId}, matchesTarget={matchesTarget}, actual={DescribeBuilding(candidate)}.");
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
            RingPosition target,
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
                    Math.Min(building.r_TilePositionXBegin, building.r_TilePositionXEnd) == target.X &&
                    Math.Min(building.r_TilePositionYBegin, building.r_TilePositionYEnd) == target.Y)
                {
                    if (GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out pile))
                        return buildingId;
                }
            }

            return 0;
        }

        private void CleanupFailedPrefabSpawns(
            PrefabSpawnCapture capture,
            int oldPileId,
            int requestId,
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
                LogInfo($"invalid prefab replacement cleanup completed: requestId={requestId}, reason={reason}, buildingId={buildingId}, markedForDeletion={markedForDeletion}.");
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
            int requestId,
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
            LogInfo($"pile content cleared before prefab-managed deletion: stage={stage}, requestId={requestId}, pileId={pileId}, pileGlobalId={pileGlobalId}, aliveState={pile->r_AliveState}, previousStoneBlocks={previousStoneBlocks}, previousGoodStack={previousGoodStack}.");
        }

        private static List<RingPosition> CreateClockwiseRing(
            GameBuilding* quarry,
            GameBuilding* oldPile,
            int pileWidth,
            int pileHeight,
            out RingGeometry geometry)
        {
            int quarryMinX = Math.Min(quarry->r_TilePositionXBegin, quarry->r_TilePositionXEnd);
            int quarryMaxX = Math.Max(quarry->r_TilePositionXBegin, quarry->r_TilePositionXEnd);
            int quarryMinY = Math.Min(quarry->r_TilePositionYBegin, quarry->r_TilePositionYEnd);
            int quarryMaxY = Math.Max(quarry->r_TilePositionYBegin, quarry->r_TilePositionYEnd);
            int quarryWidth = quarryMaxX - quarryMinX + 1;
            int quarryHeight = quarryMaxY - quarryMinY + 1;
            int quarryCenterX2 = quarryMinX + quarryMaxX;
            int quarryCenterY2 = quarryMinY + quarryMaxY;
            int oldCenterX2 = Math.Min(oldPile->r_TilePositionXBegin, oldPile->r_TilePositionXEnd) * 2 + pileWidth - 1;
            int oldCenterY2 = Math.Min(oldPile->r_TilePositionYBegin, oldPile->r_TilePositionYEnd) * 2 + pileHeight - 1;

            // A rectangle in tile coordinates projects to a diamond in the isometric view. The old one-tile
            // X margin sat inside the visible quarry footprint. Keep the ring centered on both building centers
            // and at least as far out as the vanilla right-hand pile position.
            int currentRadius2 = Math.Max(
                Math.Abs(oldCenterX2 - quarryCenterX2),
                Math.Abs(oldCenterY2 - quarryCenterY2));
            int minimumRadius2 = Math.Max(
                3 * quarryWidth + pileWidth - 2,
                quarryHeight + pileHeight);
            int radius2 = Math.Max(currentRadius2, minimumRadius2);

            int left = DivideCeiling(quarryCenterX2 - radius2 - (pileWidth - 1), 2);
            int right = DivideFloor(quarryCenterX2 + radius2 - (pileWidth - 1), 2);
            int top = DivideCeiling(quarryCenterY2 - radius2 - (pileHeight - 1), 2);
            int bottom = DivideFloor(quarryCenterY2 + radius2 - (pileHeight - 1), 2);
            geometry = new RingGeometry(quarryCenterX2, quarryCenterY2, radius2, left, top, right, bottom);

            if (right < left || bottom < top)
                return new List<RingPosition>();

            List<RingPosition> ring = new List<RingPosition>(2 * ((right - left) + (bottom - top)));

            for (int x = left; x <= right; x++)
                ring.Add(new RingPosition(x, top));
            for (int y = top + 1; y <= bottom; y++)
                ring.Add(new RingPosition(right, y));
            for (int x = right - 1; x >= left; x--)
                ring.Add(new RingPosition(x, bottom));
            for (int y = bottom - 1; y > top; y--)
                ring.Add(new RingPosition(left, y));

            return ring;
        }

        private static int DivideCeiling(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder > 0 ? quotient + 1 : quotient;
        }

        private static int DivideFloor(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int FindRingPosition(List<RingPosition> ring, int x, int y)
        {
            for (int index = 0; index < ring.Count; index++)
            {
                if (ring[index].X == x && ring[index].Y == y)
                    return index;
            }

            return -1;
        }

        private static int FindNearestRingPosition(
            List<RingPosition> ring,
            GameBuilding* oldPile,
            int pileWidth,
            int pileHeight)
        {
            int oldCenterX2 = Math.Min(oldPile->r_TilePositionXBegin, oldPile->r_TilePositionXEnd) * 2 + pileWidth - 1;
            int oldCenterY2 = Math.Min(oldPile->r_TilePositionYBegin, oldPile->r_TilePositionYEnd) * 2 + pileHeight - 1;
            int nearestIndex = 0;
            long nearestDistanceSquared = long.MaxValue;

            for (int index = 0; index < ring.Count; index++)
            {
                long dx = ring[index].X * 2L + pileWidth - 1 - oldCenterX2;
                long dy = ring[index].Y * 2L + pileHeight - 1 - oldCenterY2;
                long distanceSquared = dx * dx + dy * dy;
                if (distanceSquared >= nearestDistanceSquared)
                    continue;

                nearestIndex = index;
                nearestDistanceSquared = distanceSquared;
            }

            return nearestIndex;
        }

        private static bool RectanglesOverlap(
            int firstX,
            int firstY,
            int firstWidth,
            int firstHeight,
            int secondX,
            int secondY,
            int secondWidth,
            int secondHeight)
        {
            return firstX < secondX + secondWidth &&
                firstX + firstWidth > secondX &&
                firstY < secondY + secondHeight &&
                firstY + firstHeight > secondY;
        }

        private static bool IsFootprintInsideMap(int x, int y, int width, int height)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            return tileApi.IsTileInsideMapBounds(x, y) &&
                tileApi.IsTileInsideMapBounds(x + width - 1, y) &&
                tileApi.IsTileInsideMapBounds(x, y + height - 1) &&
                tileApi.IsTileInsideMapBounds(x + width - 1, y + height - 1);
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

        private static int GetBuildingWidth(GameBuilding* building)
        {
            return Math.Abs(building->r_TilePositionXEnd - building->r_TilePositionXBegin) + 1;
        }

        private static int GetBuildingHeight(GameBuilding* building)
        {
            return Math.Abs(building->r_TilePositionYEnd - building->r_TilePositionYBegin) + 1;
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

        private void ClearMapState()
        {
            LogInfo($"clearing rotation state: processedRequestPlayerCount={processedRequestIds.Count}, nextRequestId={nextRequestId}, prefabCaptureActive={activePrefabSpawnCapture != null}, linkedRemovalSuppressionDepth={linkedRemovalSuppressionDepth}.");
            processedRequestIds.Clear();
            activePrefabSpawnCapture = null;
            linkedRemovalSuppressionDepth = 0;
            nextRequestId = 0;
            lastVisibilityLogState = null;
            buttonViewModel.Hide();
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
            Shared.DebugLogHelper.LogDebug(log, $"SomeSettings quarry-pile diagnostic: {message}");
        }

        private void DisposeSubscriptions()
        {
            for (int i = 0; i < subscriptions.Count; i++)
                subscriptions[i]?.Dispose();
            subscriptions.Clear();
        }

        private int NextRequestId()
        {
            if (nextRequestId == int.MaxValue)
                nextRequestId = 0;
            return ++nextRequestId;
        }

        private static int GetLocalPlayerIdOrOne()
        {
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : 1;
        }

        private sealed class PrefabSpawnCapture
        {
            public PrefabSpawnCapture(int requestId, int playerId, int targetX, int targetY)
            {
                RequestId = requestId;
                PlayerId = playerId;
                TargetX = targetX;
                TargetY = targetY;
            }

            public int RequestId { get; }
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

        private readonly struct RingPosition
        {
            public RingPosition(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private readonly struct RingGeometry
        {
            public RingGeometry(int centerX2, int centerY2, int radius2, int left, int top, int right, int bottom)
            {
                CenterX2 = centerX2;
                CenterY2 = centerY2;
                Radius2 = radius2;
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int CenterX2 { get; }
            public int CenterY2 { get; }
            public int Radius2 { get; }
            public int Left { get; }
            public int Top { get; }
            public int Right { get; }
            public int Bottom { get; }
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
