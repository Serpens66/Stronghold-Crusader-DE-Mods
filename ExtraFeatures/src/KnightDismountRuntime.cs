// Feature: Mount swordsmen and dismount mounted knights through local commands.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Zhuqiaomon.Memory;

namespace ExtraFeatures
{
    internal sealed class KnightDismountButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private static readonly Thickness DefaultButtonMargin = new Thickness(80, 40, 0, 3);

        private Visibility buttonVisibility = Visibility.Hidden;
        private Visibility dismountButtonVisibility = Visibility.Hidden;
        private Visibility mountButtonVisibility = Visibility.Hidden;
        private Thickness buttonMargin = DefaultButtonMargin;
        private bool mountButtonEnabled;

        public KnightDismountButtonViewModel(
            Action dismount,
            Action mount,
            Action showDismountTooltip,
            Action showMountTooltip,
            Action hideTooltip)
        {
            DismountCommand = new RelayCommand(dismount ?? throw new ArgumentNullException(nameof(dismount)));
            MountCommand = new RelayCommand(mount ?? throw new ArgumentNullException(nameof(mount)));
            DismountMouseEnterCommand = new RelayCommand(showDismountTooltip ?? throw new ArgumentNullException(nameof(showDismountTooltip)));
            MountMouseEnterCommand = new RelayCommand(showMountTooltip ?? throw new ArgumentNullException(nameof(showMountTooltip)));
            MouseLeaveCommand = new RelayCommand(hideTooltip ?? throw new ArgumentNullException(nameof(hideTooltip)));
        }

        public RelayCommand DismountCommand { get; }
        public RelayCommand MountCommand { get; }
        public RelayCommand DismountMouseEnterCommand { get; }
        public RelayCommand MountMouseEnterCommand { get; }
        public RelayCommand MouseLeaveCommand { get; }

        public Thickness ButtonMargin
        {
            get => buttonMargin;
            private set
            {
                if (buttonMargin.Equals(value))
                    return;

                buttonMargin = value;
                OnPropertyChanged(nameof(ButtonMargin));
            }
        }

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

        public Visibility DismountButtonVisibility
        {
            get => dismountButtonVisibility;
            private set
            {
                if (dismountButtonVisibility == value)
                    return;

                dismountButtonVisibility = value;
                OnPropertyChanged(nameof(DismountButtonVisibility));
            }
        }

        public Visibility MountButtonVisibility
        {
            get => mountButtonVisibility;
            private set
            {
                if (mountButtonVisibility == value)
                    return;

                mountButtonVisibility = value;
                OnPropertyChanged(nameof(MountButtonVisibility));
            }
        }

        public bool MountButtonEnabled
        {
            get => mountButtonEnabled;
            private set
            {
                if (mountButtonEnabled == value)
                    return;

                mountButtonEnabled = value;
                OnPropertyChanged(nameof(MountButtonEnabled));
            }
        }

        public void Hide()
        {
            ButtonVisibility = Visibility.Hidden;
            DismountButtonVisibility = Visibility.Hidden;
            MountButtonVisibility = Visibility.Hidden;
            MountButtonEnabled = false;
        }

        public void ShowDismount(Thickness margin)
        {
            ButtonMargin = margin;
            ButtonVisibility = Visibility.Visible;
            DismountButtonVisibility = Visibility.Visible;
            MountButtonVisibility = Visibility.Hidden;
            MountButtonEnabled = false;
        }

        public void ShowMount(Thickness margin, bool enabled)
        {
            ButtonMargin = margin;
            ButtonVisibility = Visibility.Visible;
            DismountButtonVisibility = Visibility.Hidden;
            MountButtonVisibility = Visibility.Visible;
            MountButtonEnabled = enabled;
        }
    }

    internal sealed unsafe class KnightDismountRuntime : IDisposable
    {
        private delegate void SetuptroopActionsUIDelegate(HUD_Troops self, bool fromInitialOpening);

        private static readonly Thickness BottomRightSlotMargin = new Thickness(80, 40, 0, 3);
        private const int StableHorseSlotCount = 4;
        private const int ChoreProtocolVersion = 1;
        private const int MountAction = 1;
        private const int DismountAction = 2;
        private const string MissingWeaponsSpeechFileName = "Other_Warning6.wav";
        private static readonly string[] MountSpeechFileNames = { "Knight_m1.wav", "Knight_m2.wav", "Knight_m3.wav" };
        private static readonly string[] DismountSpeechFileNames = { "Sword_s4.wav", "Sword_s5.wav", "Sword_s6.wav" };
        private static readonly Random SpeechRandom = new Random();

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly KnightDismountButtonViewModel buttonViewModel;
        private Hook setupTroopActionsHook;
        private SetuptroopActionsUIDelegate setupTroopActionsTrampoline;
        private Button hookedDismountButton;
        private Button hookedMountButton;
        private bool initialized;
        private bool disposed;
        private bool networkInitialized;
        private int nextOperationId;
        private R3PacketEventHook<KnightTransformationPacket> transformationPacketHook;
        private IDisposable transformationPacketSubscription;

        public KnightDismountRuntime(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
            buttonViewModel = new KnightDismountButtonViewModel(
                OnDismountCommand,
                OnMountCommand,
                ShowDismountTooltip,
                ShowMountTooltip,
                HideTooltip);
        }

        public KnightDismountButtonViewModel ButtonViewModel => buttonViewModel;

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            transformationPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<KnightTransformationPacket>();
            transformationPacketSubscription = transformationPacketHook.GetBaseHook().Observable.Subscribe(OnTransformationPacketReceived);
            networkInitialized = true;
            LogInfo($"Chore packet registered eagerly: packetId={transformationPacketHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
        }

        public void Initialize()
        {
            if (initialized)
                return;

            disposed = false;
            setupTroopActionsHook = new Hook(FindSetuptroopActionsUIMethod(), (SetuptroopActionsUIDelegate)SetuptroopActionsUIHook);
            setupTroopActionsTrampoline = setupTroopActionsHook.GenerateTrampoline<SetuptroopActionsUIDelegate>();
            initialized = true;
            buttonViewModel.Hide();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            initialized = false;
            buttonViewModel.Hide();
            UnhookButtonEvents();

            setupTroopActionsHook?.Undo();
            setupTroopActionsHook?.Dispose();
            setupTroopActionsHook = null;
            setupTroopActionsTrampoline = null;
        }

        public void RefreshButtonVisibility()
        {
            RefreshButtonVisibility(null);
        }

        private void RefreshButtonVisibility(HUD_Troops troopPanel)
        {
            try
            {
                if (!IsFeatureActive())
                {
                    buttonViewModel.Hide();
                    return;
                }

                if (troopPanel == null && !TryGetHudTroopPanel(out troopPanel))
                {
                    buttonViewModel.Hide();
                    return;
                }

                if (!IsBottomRightSlotFree(troopPanel))
                {
                    buttonViewModel.Hide();
                    return;
                }

                int localPlayerId = GetControlledPlayerId();
                if (HasSelectedOwnKnight(localPlayerId))
                {
                    buttonViewModel.ShowDismount(BottomRightSlotMargin);
                    return;
                }

                if (HasSelectedOwnSwordsman(localPlayerId))
                {
                    buttonViewModel.ShowMount(BottomRightSlotMargin, CountAvailableHorseSlots(localPlayerId) > 0);
                    return;
                }

                buttonViewModel.Hide();
            }
            catch (Exception ex)
            {
                buttonViewModel.Hide();
                LogError($"Knight mount/dismount visibility refresh failed: {ex}");
            }
        }

        private static MethodInfo FindSetuptroopActionsUIMethod()
        {
            MethodInfo method = typeof(HUD_Troops).GetMethod(
                "SetuptroopActionsUI",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(HUD_Troops).FullName, "SetuptroopActionsUI");

            return method;
        }

        private void SetuptroopActionsUIHook(HUD_Troops self, bool fromInitialOpening)
        {
            setupTroopActionsTrampoline(self, fromInitialOpening);
            HookButtonEvents(self);
            RefreshButtonVisibility(self);
        }

        private void HookButtonEvents(HUD_Troops troopPanel)
        {
            if (troopPanel == null)
                return;

            HookDismountButton(troopPanel.FindName("ExtraFeaturesKnightDismountButton") as Button);
            HookMountButton(troopPanel.FindName("ExtraFeaturesKnightMountButton") as Button);
        }

        private void HookDismountButton(Button button)
        {
            if (button == null || ReferenceEquals(button, hookedDismountButton))
                return;

            if (hookedDismountButton != null)
            {
                hookedDismountButton.MouseEnter -= OnDismountButtonMouseEnter;
                hookedDismountButton.MouseLeave -= OnButtonMouseLeave;
            }

            hookedDismountButton = button;
            hookedDismountButton.MouseEnter += OnDismountButtonMouseEnter;
            hookedDismountButton.MouseLeave += OnButtonMouseLeave;
        }

        private void HookMountButton(Button button)
        {
            if (button == null || ReferenceEquals(button, hookedMountButton))
                return;

            if (hookedMountButton != null)
            {
                hookedMountButton.MouseEnter -= OnMountButtonMouseEnter;
                hookedMountButton.MouseLeave -= OnButtonMouseLeave;
            }

            hookedMountButton = button;
            hookedMountButton.MouseEnter += OnMountButtonMouseEnter;
            hookedMountButton.MouseLeave += OnButtonMouseLeave;
        }

        private void UnhookButtonEvents()
        {
            if (hookedDismountButton != null)
            {
                hookedDismountButton.MouseEnter -= OnDismountButtonMouseEnter;
                hookedDismountButton.MouseLeave -= OnButtonMouseLeave;
                hookedDismountButton = null;
            }

            if (hookedMountButton != null)
            {
                hookedMountButton.MouseEnter -= OnMountButtonMouseEnter;
                hookedMountButton.MouseLeave -= OnButtonMouseLeave;
                hookedMountButton = null;
            }
        }

        private void OnDismountButtonMouseEnter(object sender, MouseEventArgs e)
        {
            ShowDismountTooltip();
        }

        private void OnMountButtonMouseEnter(object sender, MouseEventArgs e)
        {
            ShowMountTooltip();
        }

        private void OnButtonMouseLeave(object sender, MouseEventArgs e)
        {
            HideTooltip();
        }

        private void ShowDismountTooltip()
        {
            ShowTooltip(
                SerpLocalization.Get(SerpLocalization.KnightDismountTooltip),
                SerpLocalization.Get(SerpLocalization.KnightDismountTooltipBody),
                "dismount");
        }

        private void ShowMountTooltip()
        {
            ShowTooltip(
                SerpLocalization.Get(SerpLocalization.KnightMountTooltip),
                SerpLocalization.Get(SerpLocalization.KnightMountTooltipBody),
                "mount");
        }

        private void ShowTooltip(string title, string body, string label)
        {
            try
            {
                MainViewModel mainViewModel = MainViewModel.Instance;
                HUD_Troops troopPanel = mainViewModel == null ? null : mainViewModel.HUDTroopPanel;
                if (mainViewModel == null || troopPanel == null)
                    return;

                mainViewModel.TroopsPanelRollover = title;
                mainViewModel.TroopsPanelRollover_AmountReq1 = string.Empty;
                mainViewModel.TroopsPanelRollover_AmountGot1 = body;
                mainViewModel.TroopsPanelRollover_GoodsImage1 = null;
                SetTroopRolloverVisibility(troopPanel, false, true);
            }
            catch (Exception ex)
            {
                LogError($"Knight {label} tooltip show failed: {ex}");
            }
        }

        private void HideTooltip()
        {
            try
            {
                HUD_Troops troopPanel = MainViewModel.Instance == null ? null : MainViewModel.Instance.HUDTroopPanel;
                if (troopPanel != null)
                    SetTroopRolloverVisibility(troopPanel, false, false);
            }
            catch (Exception ex)
            {
                LogError($"Knight mount/dismount tooltip hide failed: {ex}");
            }
        }

        private static void SetTroopRolloverVisibility(HUD_Troops troopPanel, bool showShortTooltip, bool showLongTooltip)
        {
            UIElement shortTooltip = troopPanel.RefTroopsPanelRollover;
            UIElement longTooltip = troopPanel.RefTroopsPanelRollover2;

            if (shortTooltip != null)
                shortTooltip.Visibility = showShortTooltip ? Visibility.Visible : Visibility.Hidden;

            if (longTooltip != null)
                longTooltip.Visibility = showLongTooltip ? Visibility.Visible : Visibility.Hidden;
        }

        private bool HasSelectedOwnKnight(int localPlayerId)
        {
            return HasSelectedOwnUnit(localPlayerId, eChimps.CHIMP_TYPE_KNIGHT);
        }

        private bool HasSelectedOwnSwordsman(int localPlayerId)
        {
            return HasSelectedOwnUnit(localPlayerId, eChimps.CHIMP_TYPE_SWORDSMAN);
        }

        private bool HasSelectedOwnUnit(int localPlayerId, eChimps unitType)
        {
            int[] selectedUnits = GetSelectedChimpsSafe();
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;

            for (int i = 0; i < selectedUnits.Length; i++)
            {
                int unitId = selectedUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (IsOwnAliveUnit(unit, localPlayerId, unitType))
                    return true;
            }

            int[] aliveUnits = unitApi.GetAllAliveUnits();
            for (int i = 0; i < aliveUnits.Length; i++)
            {
                int unitId = aliveUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (IsSelected(unit) && IsOwnAliveUnit(unit, localPlayerId, unitType))
                    return true;
            }

            return false;
        }

        private static bool TryGetHudTroopPanel(out HUD_Troops troopPanel)
        {
            troopPanel = null;

            if (!MainViewModel.viewModelLoaded)
                return false;

            MainViewModel mainViewModel = MainViewModel.Instance;
            troopPanel = mainViewModel == null ? null : mainViewModel.HUDTroopPanel;
            return troopPanel != null;
        }

        private static bool IsBottomRightSlotFree(HUD_Troops troopPanel)
        {
            return IsSlotFree(troopPanel, "UnitFireCow", "UnitbuildArabBallista");
        }

        private static bool IsSlotFree(FrameworkElement root, params string[] elementNames)
        {
            for (int i = 0; i < elementNames.Length; i++)
            {
                UIElement element = root.FindName(elementNames[i]) as UIElement;
                if (IsVisible(element))
                    return false;
            }

            return true;
        }

        private static bool IsVisible(UIElement element)
        {
            return element != null && element.Visibility == Visibility.Visible;
        }

        private void OnDismountCommand()
        {
            try
            {
                if (!IsFeatureActive())
                    return;

                int localPlayerId = GetControlledPlayerId();
                List<UnitTransformSnapshot> snapshots = CaptureSelectedUnitSnapshots(localPlayerId, eChimps.CHIMP_TYPE_KNIGHT);
                if (snapshots.Count == 0)
                {
                    RefreshButtonVisibility();
                    return;
                }

                if (RequiresChoreTransport())
                {
                    TrySendTransformationChore(localPlayerId, DismountAction, snapshots);
                }
                else
                {
                    List<UnitTransformSnapshot> appliedSnapshots = new List<UnitTransformSnapshot>(snapshots.Count);
                    ApplyDismountBatch(snapshots, "local-click", appliedSnapshots);
                    if (appliedSnapshots.Count > 0)
                        PlayRandomLocalSpeech(DismountSpeechFileNames, "dismount");
                }

                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount click failed: {ex}");
            }
        }

        private void OnMountCommand()
        {
            try
            {
                if (!IsFeatureActive())
                {
                    return;
                }

                int localPlayerId = GetControlledPlayerId();

                List<UnitTransformSnapshot> snapshots = CaptureSelectedUnitSnapshots(localPlayerId, eChimps.CHIMP_TYPE_SWORDSMAN);
                if (snapshots.Count == 0)
                {
                    RefreshButtonVisibility();
                    return;
                }

                if (FindHorseAllocations(localPlayerId, snapshots.Count).Count == 0)
                {
                    PlayMissingWeaponsSpeech();
                    RefreshButtonVisibility();
                    return;
                }

                if (RequiresChoreTransport())
                {
                    TrySendTransformationChore(localPlayerId, MountAction, snapshots);
                }
                else
                {
                    List<HorseAllocation> allocations = FindHorseAllocations(localPlayerId, snapshots.Count);
                    int applyCount = Math.Min(snapshots.Count, allocations.Count);
                    List<AppliedMountSnapshot> appliedSnapshots = new List<AppliedMountSnapshot>(applyCount);
                    ApplyMountBatch(snapshots, allocations, applyCount, "local-click", appliedSnapshots);
                    if (appliedSnapshots.Count > 0)
                        PlayRandomLocalSpeech(MountSpeechFileNames, "mount");
                }

                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight mount click failed: {ex}");
            }
        }

        private bool IsFeatureActive()
        {
            return settings.EnableMod &&
                settings.EnableKnightDismount &&
                (!RequiresChoreTransport() || IsChoreTransportReady());
        }

        private bool RequiresChoreTransport()
        {
            return multiplayerFeatureGate.BlocksLocalStateChanges;
        }

        private bool IsChoreTransportReady()
        {
            return networkInitialized && transformationPacketHook != null && ChoreNetworkTransport.IsAvailable;
        }

        private bool TrySendTransformationChore(int playerId, int action, List<UnitTransformSnapshot> snapshots)
        {
            if (!IsChoreTransportReady())
            {
                LogError("Knight transformation refused in multiplayer because the Chore transport is unavailable.");
                return false;
            }

            var globalIds = new List<int>(snapshots.Count);
            for (int index = 0; index < snapshots.Count; index++)
            {
                if (snapshots[index].GlobalId <= 0)
                {
                    LogError($"Knight transformation refused because selected unit {snapshots[index].UnitId} has no stable global ID.");
                    return false;
                }

                globalIds.Add(snapshots[index].GlobalId);
            }

            int operationId = unchecked(++nextOperationId);
            var packet = new KnightTransformationPacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                PlayerId = playerId,
                OperationId = operationId,
                Action = action,
                UnitGlobalIds = globalIds.ToArray()
            };

            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(transformationPacketHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            bool queued = sendRawBlob != null && sendRawBlob(blob);
            if (!queued)
            {
                LogError($"Knight transformation Chore was not queued; no local action was applied: operationId={operationId}, payloadBytes={blob.Length}.");
                return false;
            }

            LogInfo($"Knight transformation Chore queued: operationId={operationId}, action={action}, unitCount={globalIds.Count}, payloadBytes={blob.Length}.");
            return true;
        }

        private void OnTransformationPacketReceived(ReceiveCustomPacketEventArgs<KnightTransformationPacket> args)
        {
            KnightTransformationPacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion ||
                (packet.Action != MountAction && packet.Action != DismountAction) ||
                packet.PlayerId <= 0 || packet.UnitGlobalIds == null || packet.UnitGlobalIds.Length == 0)
            {
                LogError("Rejected a Knight transformation Chore with an invalid payload.");
                return;
            }

            try
            {
                eChimps expectedType = packet.Action == MountAction
                    ? eChimps.CHIMP_TYPE_SWORDSMAN
                    : eChimps.CHIMP_TYPE_KNIGHT;
                List<UnitTransformSnapshot> snapshots = CaptureSnapshotsByGlobalIds(packet.PlayerId, expectedType, packet.UnitGlobalIds);
                if (snapshots.Count != packet.UnitGlobalIds.Length)
                {
                    LogError($"Knight transformation Chore refused because not every unit resolved identically: operationId={packet.OperationId}, requested={packet.UnitGlobalIds.Length}, resolved={snapshots.Count}.");
                    return;
                }

                bool localAction = packet.PlayerId == GetControlledPlayerId();
                if (packet.Action == DismountAction)
                {
                    var applied = new List<UnitTransformSnapshot>(snapshots.Count);
                    ApplyDismountBatch(snapshots, "multiplayer-chore", applied);
                    if (localAction && applied.Count > 0)
                        PlayRandomLocalSpeech(DismountSpeechFileNames, "dismount");
                }
                else
                {
                    List<HorseAllocation> allocations = FindHorseAllocations(packet.PlayerId, snapshots.Count);
                    int applyCount = Math.Min(snapshots.Count, allocations.Count);
                    var applied = new List<AppliedMountSnapshot>(applyCount);
                    ApplyMountBatch(snapshots, allocations, applyCount, "multiplayer-chore", applied);
                    if (localAction && applied.Count > 0)
                        PlayRandomLocalSpeech(MountSpeechFileNames, "mount");
                    else if (localAction && allocations.Count == 0)
                        PlayMissingWeaponsSpeech();
                }

                LogInfo($"Knight transformation Chore executed: operationId={packet.OperationId}, action={packet.Action}, unitCount={snapshots.Count}.");
            }
            catch (Exception ex)
            {
                LogError($"Knight transformation Chore execution failed: operationId={packet.OperationId}, exception={ex}");
            }
            finally
            {
                RefreshButtonVisibility();
            }
        }

        private static List<UnitTransformSnapshot> CaptureSnapshotsByGlobalIds(int playerId, eChimps expectedType, int[] globalIds)
        {
            var snapshots = new List<UnitTransformSnapshot>(globalIds.Length);
            var seen = new HashSet<int>();
            for (int index = 0; index < globalIds.Length; index++)
            {
                int globalId = globalIds[index];
                if (globalId <= 0 || !seen.Add(globalId))
                    continue;

                int unitId = FindAliveUnitIdByGlobalId(globalId);
                if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    !IsOwnAliveUnit(unit, playerId, expectedType))
                {
                    continue;
                }

                snapshots.Add(CreateSnapshotFromUnit(unitId, unit));
            }

            return snapshots;
        }

        private void PlayMissingWeaponsSpeech()
        {
            try
            {
                SFXManager.instance?.playSpeech(
                    1,
                    MissingWeaponsSpeechFileName,
                    1f);
            }
            catch (Exception ex)
            {
                LogError($"Could not play knight mount missing stable horse speech: {ex}");
            }
        }

        private void PlayRandomLocalSpeech(string[] fileNames, string label)
        {
            try
            {
                string speechFileName = GetRandomSpeechFileName(fileNames);
                SFXManager.instance?.playSpeech(
                    1,
                    speechFileName,
                    1f);
            }
            catch (Exception ex)
            {
                LogError($"Could not play knight {label} speech: {ex}");
            }
        }

        private static string GetRandomSpeechFileName(string[] speechFileNames)
        {
            lock (SpeechRandom)
            {
                return speechFileNames[SpeechRandom.Next(speechFileNames.Length)];
            }
        }

        private List<UnitTransformSnapshot> CaptureSelectedUnitSnapshots(int localPlayerId, eChimps unitType)
        {
            List<UnitTransformSnapshot> snapshots = new List<UnitTransformSnapshot>();
            int[] selectedUnits = GetSelectedChimpsSafe();
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            HashSet<int> seenGlobalIds = new HashSet<int>();

            for (int i = 0; i < selectedUnits.Length; i++)
            {
                int unitId = selectedUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (!IsOwnAliveUnit(unit, localPlayerId, unitType))
                    continue;

                AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
            }

            int[] aliveUnits = unitApi.GetAllAliveUnits();
            for (int i = 0; i < aliveUnits.Length; i++)
            {
                int unitId = aliveUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (!IsSelected(unit) || !IsOwnAliveUnit(unit, localPlayerId, unitType))
                    continue;

                AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
            }

            return snapshots;
        }

        private void AddSnapshot(List<UnitTransformSnapshot> snapshots, HashSet<int> seenGlobalIds, int unitId, GameUnit* unit)
        {
            int globalId = (int)unit->r_GlobalId;
            int snapshotKey = globalId > 0 ? globalId : -unitId;
            if (!seenGlobalIds.Add(snapshotKey))
                return;

            snapshots.Add(CreateSnapshotFromUnit(unitId, unit));
        }

        private static UnitTransformSnapshot CreateSnapshotFromUnit(int unitId, GameUnit* unit)
        {
            return new UnitTransformSnapshot
            {
                UnitId = unitId,
                GlobalId = (int)unit->r_GlobalId,
                OwnerPlayerId = unit->r_ControllableForPlayerId,
                ColorPlayerId = (int)unit->r_SpritePlayerColorId,
                TileX = unit->r_CurrentTilePositionX,
                TileY = unit->r_CurrentTilePositionY,
                Height = unit->r_HeightElevation,
                CurrentHealth = (int)unit->r_CurrentHealth,
                MaxHealth = (int)unit->r_MaxHealth,
                LinkedProductionBuildingId = unit->r_LinkedProductionBuildingId
            };
        }

        private void ApplyDismountBatch(List<UnitTransformSnapshot> snapshots, string reason, List<UnitTransformSnapshot> appliedSnapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return;

            List<ResolvedTransformSnapshot> resolvedSnapshots = new List<ResolvedTransformSnapshot>(snapshots.Count);
            HashSet<int> seenCurrentUnitIds = new HashSet<int>();

            for (int i = 0; i < snapshots.Count; i++)
            {
                UnitTransformSnapshot snapshot = snapshots[i];
                if (!TryResolveAliveUnitByUnitId(snapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentUnitId))
                    continue;

                if (!seenCurrentUnitIds.Add(currentUnitId))
                    continue;

                resolvedSnapshots.Add(new ResolvedTransformSnapshot
                {
                    Snapshot = snapshot,
                    CurrentUnitId = currentUnitId
                });
            }

            resolvedSnapshots.Sort((left, right) => right.CurrentUnitId.CompareTo(left.CurrentUnitId));
            for (int i = 0; i < resolvedSnapshots.Count; i++)
            {
                ResolvedTransformSnapshot resolved = resolvedSnapshots[i];
                if (!TryResolveAliveUnitByUnitId(resolved.Snapshot, eChimps.CHIMP_TYPE_KNIGHT, out int deleteUnitId))
                    continue;

                if (!GameUnitManagerAPI.Instance.TryGetUnitById(deleteUnitId, out GameUnit* deleteUnit))
                    continue;

                UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(deleteUnitId, deleteUnit);
                int swordsmanUnitId = CreateUnitFromSnapshot(currentSnapshot, eChimps.CHIMP_TYPE_SWORDSMAN, "dismount", reason);
                if (swordsmanUnitId <= 0)
                    continue;

                if (!TryResolveAliveUnitByGlobalId(currentSnapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentKnightId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(currentKnightId, out GameUnit* currentKnight))
                {
                    LogError($"Knight dismount could not reacquire the original knight after spawning its replacement: reason={reason}, originalUnitId={deleteUnitId}, globalId={currentSnapshot.GlobalId}.");
                    GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                    continue;
                }

                if (!TryConsumeLinkedStableHorse(currentKnightId, currentKnight, reason, out ConsumedStableHorse consumedHorse))
                {
                    GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                    continue;
                }

                if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId))
                {
                    RollbackConsumedStableHorse(consumedHorse, reason);
                    LogError($"Knight dismount could not mark the original knight for Vanilla deletion: reason={reason}, unitId={currentKnightId}, globalId={currentSnapshot.GlobalId}.");
                    GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                    continue;
                }

                appliedSnapshots?.Add(currentSnapshot);
            }
        }

        private bool ApplyDismount(UnitTransformSnapshot snapshot, string reason)
        {
            if (!TryResolveAliveUnitByGlobalId(snapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentUnitId))
                return false;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* currentUnit))
                return false;

            UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(currentUnitId, currentUnit);
            int swordsmanUnitId = CreateUnitFromSnapshot(currentSnapshot, eChimps.CHIMP_TYPE_SWORDSMAN, "dismount", reason);
            if (swordsmanUnitId <= 0)
                return false;

            if (!TryResolveAliveUnitByGlobalId(currentSnapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentKnightId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(currentKnightId, out GameUnit* currentKnight))
            {
                LogError($"Knight dismount could not reacquire the original knight after spawning its replacement: reason={reason}, originalUnitId={currentUnitId}, globalId={currentSnapshot.GlobalId}.");
                GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                return false;
            }

            if (!TryConsumeLinkedStableHorse(currentKnightId, currentKnight, reason, out ConsumedStableHorse consumedHorse))
            {
                GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                return false;
            }

            if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId))
            {
                RollbackConsumedStableHorse(consumedHorse, reason);
                LogError($"Knight dismount could not mark the original knight for Vanilla deletion: reason={reason}, unitId={currentKnightId}, globalId={currentSnapshot.GlobalId}.");
                GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                return false;
            }

            return true;
        }

        private void ApplyMountBatch(
            List<UnitTransformSnapshot> snapshots,
            List<HorseAllocation> allocations,
            int applyCount,
            string reason,
            List<AppliedMountSnapshot> appliedSnapshots)
        {
            if (snapshots == null || allocations == null || applyCount <= 0)
                return;

            List<ResolvedMountSnapshot> resolvedSnapshots = new List<ResolvedMountSnapshot>(applyCount);
            HashSet<int> seenCurrentUnitIds = new HashSet<int>();

            for (int i = 0; i < applyCount; i++)
            {
                UnitTransformSnapshot snapshot = snapshots[i];
                if (!TryResolveAliveUnitByUnitId(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int currentUnitId))
                    continue;

                if (!seenCurrentUnitIds.Add(currentUnitId))
                    continue;

                resolvedSnapshots.Add(new ResolvedMountSnapshot
                {
                    Snapshot = snapshot,
                    Allocation = allocations[i],
                    CurrentUnitId = currentUnitId
                });
            }

            List<AppliedMountSnapshot> deletedSnapshots = new List<AppliedMountSnapshot>(resolvedSnapshots.Count);
            resolvedSnapshots.Sort((left, right) => right.CurrentUnitId.CompareTo(left.CurrentUnitId));
            for (int i = 0; i < resolvedSnapshots.Count; i++)
            {
                ResolvedMountSnapshot resolved = resolvedSnapshots[i];
                if (!TryResolveAliveUnitByUnitId(resolved.Snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int deleteUnitId))
                    continue;

                if (!GameUnitManagerAPI.Instance.TryGetUnitById(deleteUnitId, out GameUnit* deleteUnit))
                    continue;

                UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(deleteUnitId, deleteUnit);
                GameUnitManagerAPI.Instance.DeleteUnit(deleteUnitId);
                deletedSnapshots.Add(new AppliedMountSnapshot
                {
                    Snapshot = currentSnapshot,
                    Allocation = resolved.Allocation
                });
            }

            for (int i = 0; i < deletedSnapshots.Count; i++)
            {
                AppliedMountSnapshot applied = deletedSnapshots[i];
                if (CreateMountedKnightFromSnapshot(applied.Snapshot, applied.Allocation, reason))
                    appliedSnapshots?.Add(applied);
            }
        }

        private bool ApplyMount(UnitTransformSnapshot snapshot, HorseAllocation allocation, string reason)
        {
            if (!TryResolveAliveUnitByGlobalId(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int currentUnitId))
                return false;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* currentUnit))
                return false;

            UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(currentUnitId, currentUnit);
            GameUnitManagerAPI.Instance.DeleteUnit(currentUnitId);
            return CreateMountedKnightFromSnapshot(currentSnapshot, allocation, reason);
        }

        private bool TryResolveAliveUnitByUnitId(UnitTransformSnapshot snapshot, eChimps expectedType, out int currentUnitId)
        {
            currentUnitId = snapshot.UnitId;
            return ValidateAliveUnit(snapshot, expectedType, currentUnitId);
        }

        private bool TryResolveAliveUnitByGlobalId(UnitTransformSnapshot snapshot, eChimps expectedType, out int currentUnitId)
        {
            currentUnitId = snapshot.GlobalId > 0 ? FindAliveUnitIdByGlobalId(snapshot.GlobalId) : snapshot.UnitId;
            return ValidateAliveUnit(snapshot, expectedType, currentUnitId);
        }

        private bool ValidateAliveUnit(UnitTransformSnapshot snapshot, eChimps expectedType, int currentUnitId)
        {
            if (currentUnitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* unit))
                return false;

            if (unit->r_AliveState != AliveState.IsAlive ||
                unit->r_UnitChimp != expectedType ||
                unit->r_ControllableForPlayerId != snapshot.OwnerPlayerId)
                return false;

            return true;
        }

        private int CreateUnitFromSnapshot(UnitTransformSnapshot snapshot, eChimps unitType, string label, string reason)
        {
            long createdId = GameUnitManagerAPI.Instance.CreateUnitLocal(
                playerOwnerId: snapshot.OwnerPlayerId,
                playerColorId: snapshot.ColorPlayerId,
                localTileX: snapshot.TileX,
                localTileY: snapshot.TileY,
                heightElevation: snapshot.Height,
                chimp: unitType);

            if (createdId <= 0 || createdId > int.MaxValue)
            {
                LogError($"Knight {label} spawned invalid {unitType} id: reason={reason}, originalUnitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, createdId={createdId}.");
                return -1;
            }

            if (snapshot.LinkedProductionBuildingId > 0 &&
                snapshot.LinkedProductionBuildingId <= ushort.MaxValue &&
                GameUnitManagerAPI.Instance.TryGetUnitById((int)createdId, out GameUnit* createdUnit))
            {
                // Preserve the barracks/production link; the horse stable has separate hidden fields.
                createdUnit->r_LinkedProductionBuildingId = (ushort)snapshot.LinkedProductionBuildingId;
            }

            ApplyHealthRatio((int)createdId, snapshot.CurrentHealth, snapshot.MaxHealth, label);
            return (int)createdId;
        }

        private bool CreateMountedKnightFromSnapshot(UnitTransformSnapshot snapshot, HorseAllocation allocation, string reason)
        {
            int knightUnitId = CreateUnitFromSnapshot(snapshot, eChimps.CHIMP_TYPE_KNIGHT, "mount", reason);
            if (knightUnitId <= 0)
            {
                if (CreateUnitFromSnapshot(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, "mount-spawn-rollback", reason) <= 0)
                    LogError($"Knight mount spawn rollback could not restore swordsman: reason={reason}, sourceGlobalId={snapshot.GlobalId}.");
                return false;
            }

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(knightUnitId, out GameUnit* knight))
            {
                LogError($"Knight mount could not resolve spawned knight: reason={reason}, knightUnitId={knightUnitId}, sourceGlobalId={snapshot.GlobalId}.");
                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                if (CreateUnitFromSnapshot(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, "mount-resolve-rollback", reason) <= 0)
                    LogError($"Knight mount resolve rollback could not restore swordsman: reason={reason}, sourceGlobalId={snapshot.GlobalId}.");
                return false;
            }

            if (!TryConsumeStableHorse(allocation, knightUnitId, (int)knight->r_GlobalId, reason))
            {
                LogError($"Knight mount could not link stable horse: reason={reason}, knightUnitId={knightUnitId}, stableId={allocation.StableId}, slot={allocation.Slot}.");
                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                if (CreateUnitFromSnapshot(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, "mount-rollback", reason) <= 0)
                    LogError($"Knight mount rollback could not restore swordsman: reason={reason}, sourceGlobalId={snapshot.GlobalId}.");
                return false;
            }

            return true;
        }

        private int CountAvailableHorseSlots(int playerId)
        {
            return FindHorseAllocations(playerId, int.MaxValue).Count;
        }

        private List<HorseAllocation> FindHorseAllocations(int playerId, int maxCount)
        {
            List<HorseAllocation> allocations = new List<HorseAllocation>();
            if (maxCount <= 0)
                return allocations;

            List<int> stableIds = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(stableIds, AliveState.IsAlive, eStructs.STRUCT_STABLES, PlayerRelationship.Self, playerId);
            stableIds.Sort();

            for (int i = 0; i < stableIds.Count && allocations.Count < maxCount; i++)
            {
                int stableId = stableIds[i];
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable))
                    continue;

                if (!IsUsableStable(stable, playerId))
                    continue;

                int availableAtStable = GetAvailableStableHorseCount(stable);
                for (int slot = 0; slot < StableHorseSlotCount && availableAtStable > 0 && allocations.Count < maxCount; slot++)
                {
                    if (!IsStableHorseSlotFree(stable, slot))
                        continue;

                    HorseAllocation allocation = new HorseAllocation
                    {
                        StableId = stableId,
                        StableGlobalId = (int)stable->r_GlobalId,
                        OwnerPlayerId = playerId,
                        Slot = slot
                    };

                    allocations.Add(allocation);
                    availableAtStable--;
                }
            }

            return allocations;
        }

        private bool TryConsumeStableHorse(HorseAllocation allocation, int unitId, int unitGlobalId, string reason)
        {
            if (unitId <= 0 || unitId > ushort.MaxValue || unitGlobalId <= 0)
                return false;

            if (allocation.StableId <= 0 || allocation.StableId > ushort.MaxValue)
                return false;

            if (allocation.Slot < 0 || allocation.Slot >= StableHorseSlotCount)
                return false;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* mountedUnit))
                return false;

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable))
                return false;

            if (!IsUsableStable(stable, allocation.OwnerPlayerId))
                return false;

            if (allocation.StableGlobalId > 0 && (int)stable->r_GlobalId != allocation.StableGlobalId)
                return false;

            if (GetAvailableStableHorseCount(stable) <= 0 || !IsStableHorseSlotFree(stable, allocation.Slot))
                return false;

            // Only establish the bidirectional ownership link here. Vanilla's regular stable update
            // recounts r_UsedHorses from the four valid slot links; r_TotalHorses remains game-owned.
            // Updating either counter ourselves would duplicate Vanilla accounting.
            GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                allocation.StableId,
                allocation.Slot,
                unitId,
                unitGlobalId,
                bidirectional: true);

            bool slotMatches = GetStableHorseSlotUnitId(stable, allocation.Slot) == unitId &&
                GetStableHorseSlotGlobalId(stable, allocation.Slot) == unitGlobalId;
            bool backlinkMatches = mountedUnit->r_LinkedStableBuildingId == allocation.StableId &&
                mountedUnit->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId;

            if (!slotMatches || !backlinkMatches)
            {
                // Only the just-written link must be rolled back; no accounting transition should
                // survive. The full Vanilla release routine would additionally consume a horse.
                GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(
                    allocation.StableId,
                    allocation.Slot,
                    bidirectional: true);
                LogError(
                    $"Knight mount rolled back an incomplete bidirectional stable link: reason={reason}, " +
                    $"stableId={allocation.StableId}, slot={allocation.Slot}, unitId={unitId}, " +
                    $"slotMatches={slotMatches}, backlinkMatches={backlinkMatches}.");
                return false;
            }

            return true;
        }

        private static bool IsUsableStable(GameBuilding* stable, int ownerPlayerId)
        {
            return stable != null &&
                stable->r_AliveState == AliveState.IsAlive &&
                stable->r_BuildingType == eStructs.STRUCT_STABLES &&
                (ownerPlayerId <= 0 || stable->r_PlayerIdOwner == ownerPlayerId);
        }

        private static int ClampStableHorseCount(byte value)
        {
            return Math.Max(0, Math.Min(StableHorseSlotCount, (int)value));
        }

        private static int GetAvailableStableHorseCount(GameBuilding* stable)
        {
            int total = ClampStableHorseCount(stable->r_TotalHorses);
            int used = ClampStableHorseCount(stable->r_UsedHorses);
            int freeSlots = CountFreeStableHorseSlots(stable);
            return Math.Max(0, Math.Min(freeSlots, total - used));
        }

        private static int CountFreeStableHorseSlots(GameBuilding* stable)
        {
            int count = 0;
            for (int slot = 0; slot < StableHorseSlotCount; slot++)
            {
                if (IsStableHorseSlotFree(stable, slot))
                    count++;
            }

            return count;
        }

        private static bool IsStableHorseSlotFree(GameBuilding* stable, int slot)
        {
            return GetStableHorseSlotUnitId(stable, slot) == 0 &&
                GetStableHorseSlotGlobalId(stable, slot) == 0;
        }

        private static int GetStableHorseSlotUnitId(GameBuilding* stable, int slot)
        {
            switch (slot)
            {
                case 0: return stable->r_UsedHorse1UnitId;
                case 1: return stable->r_UsedHorse2UnitId;
                case 2: return stable->r_UsedHorse3UnitId;
                case 3: return stable->r_UsedHorse4UnitId;
                default: return -1;
            }
        }

        private static int GetStableHorseSlotGlobalId(GameBuilding* stable, int slot)
        {
            switch (slot)
            {
                case 0: return (int)stable->r_UsedHorse1GlobalId;
                case 1: return (int)stable->r_UsedHorse2GlobalId;
                case 2: return (int)stable->r_UsedHorse3GlobalId;
                case 3: return (int)stable->r_UsedHorse4GlobalId;
                default: return -1;
            }
        }

        private static int GetKnightStableBuildingId(GameUnit* unit)
        {
            return unit == null ? 0 : unit->r_LinkedStableBuildingId;
        }

        private static int GetKnightStableBuildingGlobalId(GameUnit* unit)
        {
            return unit == null ? 0 : (int)unit->r_LinkedStableGlobalId;
        }

        private bool TryConsumeLinkedStableHorse(
            int unitId,
            GameUnit* unit,
            string reason,
            out ConsumedStableHorse consumedHorse)
        {
            consumedHorse = default;
            if (unit == null || unitId <= 0 || unit->r_GlobalId == 0)
                return false;

            int unitGlobalId = (int)unit->r_GlobalId;
            int linkedStableId = GetKnightStableBuildingId(unit);
            int linkedStableGlobalId = GetKnightStableBuildingGlobalId(unit);
            bool hasSlotLink = TryFindStableHorseLink(unitId, unitGlobalId, out int slotStableId, out int slot);

            // Imported or scripted knights can legitimately have no stable. Dismounting them must
            // not invent accounting, but any partial or contradictory link is unsafe to consume.
            if (linkedStableId <= 0 && linkedStableGlobalId <= 0 && !hasSlotLink)
                return true;

            if (linkedStableId <= 0 || linkedStableGlobalId <= 0 || !hasSlotLink ||
                slotStableId != linkedStableId || slot < 0 || slot >= StableHorseSlotCount ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(linkedStableId, out GameBuilding* stable) ||
                stable->r_AliveState != AliveState.IsAlive ||
                stable->r_BuildingType != eStructs.STRUCT_STABLES ||
                (int)stable->r_GlobalId != linkedStableGlobalId ||
                GetStableHorseSlotUnitId(stable, slot) != unitId ||
                GetStableHorseSlotGlobalId(stable, slot) != unitGlobalId)
            {
                LogError(
                    $"Knight dismount refused an inconsistent stable link: reason={reason}, unitId={unitId}, " +
                    $"unitGlobalId={unitGlobalId}, linkedStableId={linkedStableId}, " +
                    $"linkedStableGlobalId={linkedStableGlobalId}, slotStableId={slotStableId}, slot={slot}.");
                return false;
            }

            int totalBefore = stable->r_TotalHorses;
            int usedBefore = stable->r_UsedHorses;
            int rechargeBefore = stable->r_HorseRechargeTimer;
            if (totalBefore <= 0 || totalBefore > StableHorseSlotCount ||
                usedBefore <= 0 || usedBefore > StableHorseSlotCount || usedBefore > totalBefore)
            {
                LogError(
                    $"Knight dismount refused invalid stable accounting: reason={reason}, unitId={unitId}, " +
                    $"unitGlobalId={unitGlobalId}, {FormatStableState(linkedStableId, stable, slot)}.");
                return false;
            }

            consumedHorse = new ConsumedStableHorse
            {
                IsActive = true,
                StableId = linkedStableId,
                StableGlobalId = linkedStableGlobalId,
                Slot = slot,
                UnitId = unitId,
                UnitGlobalId = unitGlobalId,
                TotalBefore = totalBefore,
                UsedBefore = usedBefore,
                RechargeBefore = rechargeBefore
            };

            // TODO(SHCDE-SE): Replace this managed Vanilla-equivalent accounting block when the
            // Script Extender exposes a direct "consume linked stable horse" API. Unlink alone
            // returns the horse immediately. Unless Instant Horse is enabled, decrementing
            // TotalHorses makes the existing stable recharge it. Vanilla owns UsedHorses and
            // HorseRechargeTimer, so never write them here.
            int totalAfter = settings.InstantHorse ? totalBefore : totalBefore - 1;
            stable->r_TotalHorses = (byte)totalAfter;
            GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(linkedStableId, slot, bidirectional: true);

            bool transitionMatches = IsStableHorseSlotFree(stable, slot) &&
                unit->r_LinkedStableBuildingId == 0 && unit->r_LinkedStableGlobalId == 0 &&
                stable->r_TotalHorses == totalAfter &&
                stable->r_UsedHorses == usedBefore &&
                stable->r_HorseRechargeTimer == rechargeBefore;
            if (transitionMatches)
                return true;

            LogError(
                $"Knight dismount stable transition was incomplete and will be rolled back: reason={reason}, " +
                $"unitId={unitId}, unitGlobalId={unitGlobalId}, {FormatStableState(linkedStableId, stable, slot)}.");
            RollbackConsumedStableHorse(consumedHorse, reason);
            consumedHorse = default;
            return false;
        }

        private void RollbackConsumedStableHorse(ConsumedStableHorse consumedHorse, string reason)
        {
            if (!consumedHorse.IsActive)
                return;

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(consumedHorse.StableId, out GameBuilding* stable) ||
                stable->r_AliveState != AliveState.IsAlive ||
                stable->r_BuildingType != eStructs.STRUCT_STABLES ||
                (int)stable->r_GlobalId != consumedHorse.StableGlobalId ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(consumedHorse.UnitId, out GameUnit* unit) ||
                (int)unit->r_GlobalId != consumedHorse.UnitGlobalId)
            {
                LogError(
                    $"Knight dismount could not reacquire objects for stable rollback: reason={reason}, " +
                    $"stableId={consumedHorse.StableId}, unitId={consumedHorse.UnitId}.");
                return;
            }

            bool slotIsFree = IsStableHorseSlotFree(stable, consumedHorse.Slot);
            bool slotAlreadyRestored = GetStableHorseSlotUnitId(stable, consumedHorse.Slot) == consumedHorse.UnitId &&
                GetStableHorseSlotGlobalId(stable, consumedHorse.Slot) == consumedHorse.UnitGlobalId;
            bool totalCanRestore = stable->r_TotalHorses == consumedHorse.TotalBefore - 1 ||
                stable->r_TotalHorses == consumedHorse.TotalBefore;
            if ((!slotIsFree && !slotAlreadyRestored) || !totalCanRestore)
            {
                LogError(
                    $"Knight dismount skipped an unsafe stable rollback: reason={reason}, unitId={consumedHorse.UnitId}, " +
                    $"expectedTotal={consumedHorse.TotalBefore - 1}/{consumedHorse.TotalBefore}, " +
                    $"{FormatStableState(consumedHorse.StableId, stable, consumedHorse.Slot)}.");
                return;
            }

            stable->r_TotalHorses = (byte)consumedHorse.TotalBefore;
            if (slotIsFree)
            {
                GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                    consumedHorse.StableId,
                    consumedHorse.Slot,
                    consumedHorse.UnitId,
                    consumedHorse.UnitGlobalId,
                    bidirectional: true);
            }

            bool rollbackMatches =
                GetStableHorseSlotUnitId(stable, consumedHorse.Slot) == consumedHorse.UnitId &&
                GetStableHorseSlotGlobalId(stable, consumedHorse.Slot) == consumedHorse.UnitGlobalId &&
                unit->r_LinkedStableBuildingId == consumedHorse.StableId &&
                unit->r_LinkedStableGlobalId == (uint)consumedHorse.StableGlobalId &&
                stable->r_TotalHorses == consumedHorse.TotalBefore &&
                stable->r_UsedHorses == consumedHorse.UsedBefore &&
                stable->r_HorseRechargeTimer == consumedHorse.RechargeBefore;
            if (!rollbackMatches)
            {
                LogError(
                    $"Knight dismount stable rollback was incomplete: reason={reason}, unitId={consumedHorse.UnitId}, " +
                    $"unitGlobalId={consumedHorse.UnitGlobalId}, {FormatStableState(consumedHorse.StableId, stable, consumedHorse.Slot)}.");
            }
        }

        private static bool TryFindStableHorseLink(int unitId, int unitGlobalId, out int stableId, out int slot)
        {
            stableId = 0;
            slot = -1;
            List<int> stableIds = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(stableIds, AliveState.IsAlive, eStructs.STRUCT_STABLES);

            for (int i = 0; i < stableIds.Count; i++)
            {
                int candidateStableId = stableIds[i];
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(candidateStableId, out GameBuilding* stable))
                    continue;

                for (int candidateSlot = 0; candidateSlot < StableHorseSlotCount; candidateSlot++)
                {
                    int linkedUnitId = GetStableHorseSlotUnitId(stable, candidateSlot);
                    int linkedGlobalId = GetStableHorseSlotGlobalId(stable, candidateSlot);
                    if ((unitId > 0 && linkedUnitId == unitId) || (unitGlobalId > 0 && linkedGlobalId == unitGlobalId))
                    {
                        stableId = candidateStableId;
                        slot = candidateSlot;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string FormatStableState(int stableId, GameBuilding* stable, int slot)
        {
            return $"stableId={stableId}, stableGlobalId={stable->r_GlobalId}, total={stable->r_TotalHorses}, used={stable->r_UsedHorses}, recharge={stable->r_HorseRechargeTimer}, observedSlot={slot}, slots=[{stable->r_UsedHorse1UnitId}/{stable->r_UsedHorse1GlobalId},{stable->r_UsedHorse2UnitId}/{stable->r_UsedHorse2GlobalId},{stable->r_UsedHorse3UnitId}/{stable->r_UsedHorse3GlobalId},{stable->r_UsedHorse4UnitId}/{stable->r_UsedHorse4GlobalId}]";
        }

        private static int FindAliveUnitIdByGlobalId(int globalId)
        {
            if (globalId <= 0)
                return -1;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            int[] aliveUnitIds = unitApi.GetAllAliveUnits();
            for (int i = 0; i < aliveUnitIds.Length; i++)
            {
                int unitId = aliveUnitIds[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if ((int)unit->r_GlobalId == globalId)
                    return unitId;
            }

            return -1;
        }

        private void ApplyHealthRatio(int targetUnitId, int sourceCurrentHealth, int sourceMaxHealth, string label)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(targetUnitId, out GameUnit* unit))
            {
                LogError($"Knight {label} could not set target health, unit not found: targetUnitId={targetUnitId}.");
                return;
            }

            int targetMaxHealth = Math.Max(1, (int)unit->r_MaxHealth);
            double ratio = sourceMaxHealth > 0 ? Math.Max(0.0, Math.Min(1.0, sourceCurrentHealth / (double)sourceMaxHealth)) : 1.0;
            int targetHealth = Math.Max(1, Math.Min(targetMaxHealth, (int)Math.Round(targetMaxHealth * ratio, MidpointRounding.AwayFromZero)));
            ushort targetPercent = (ushort)Math.Max(0, Math.Min(100, (int)Math.Round(100.0 * targetHealth / targetMaxHealth, MidpointRounding.AwayFromZero)));

            unit->r_CurrentHealth = (uint)targetHealth;
            unit->r_CurrentHealthPercentage = targetPercent;
            unit->r_HealthBarBlocks = (uint)(targetPercent / 10);
        }

        private static bool IsOwnAliveUnit(GameUnit* unit, int localPlayerId, eChimps unitType)
        {
            return unit != null &&
                unit->r_AliveState == AliveState.IsAlive &&
                unit->r_UnitChimp == unitType &&
                unit->r_ControllableForPlayerId == localPlayerId;
        }

        private static bool IsSelected(GameUnit* unit)
        {
            return unit != null && (unit->r_UnitSelected != 0 || unit->r_UnitSelected2 != 0);
        }

        private int[] GetSelectedChimpsSafe()
        {
            try
            {
                return GamePlayerManagerAPI.Instance.GetSelectedChimps();
            }
            catch (Exception ex)
            {
                LogError($"Knight mount/dismount could not read selected units: {ex}");
                return Array.Empty<int>();
            }
        }

        private static int GetControlledPlayerId()
        {
            if ((GamePlayerManagerAPI.Instance?.IsInMapEditor() ?? false) ||
                (MainViewModel.Instance?.IsMapEditorMode ?? false))
            {
                // Editor actions belong to the player currently selected in the editor toolbar.
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            }

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : 1;
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{TimestampNow()}] Extra Features {message}");
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] Extra Features {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private struct UnitTransformSnapshot
        {
            public int UnitId;
            public int GlobalId;
            public int OwnerPlayerId;
            public int ColorPlayerId;
            public int TileX;
            public int TileY;
            public int Height;
            public int CurrentHealth;
            public int MaxHealth;
            public int LinkedProductionBuildingId;
        }

        private struct HorseAllocation
        {
            public int StableId;
            public int StableGlobalId;
            public int OwnerPlayerId;
            public int Slot;
        }

        private struct ConsumedStableHorse
        {
            public bool IsActive;
            public int StableId;
            public int StableGlobalId;
            public int Slot;
            public int UnitId;
            public int UnitGlobalId;
            public int TotalBefore;
            public int UsedBefore;
            public int RechargeBefore;
        }

        private struct ResolvedTransformSnapshot
        {
            public UnitTransformSnapshot Snapshot;
            public int CurrentUnitId;
        }

        private struct ResolvedMountSnapshot
        {
            public UnitTransformSnapshot Snapshot;
            public HorseAllocation Allocation;
            public int CurrentUnitId;
        }

        private struct AppliedMountSnapshot
        {
            public UnitTransformSnapshot Snapshot;
            public HorseAllocation Allocation;
        }

    }
}
