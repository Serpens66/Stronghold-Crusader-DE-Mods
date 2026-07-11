using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MessagePack.Formatters;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace SomeSettings
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(KnightDismountPacketFormatter))]
    public sealed class KnightDismountPacket
    {
        [Key(0)] public int SourcePlayerId { get; set; }
        [Key(1)] public int RequestId { get; set; }
        [Key(2)] public int KnightGlobalId { get; set; }
        [Key(3)] public int OwnerPlayerId { get; set; }
        [Key(4)] public int ColorPlayerId { get; set; }
        [Key(5)] public int TileX { get; set; }
        [Key(6)] public int TileY { get; set; }
        [Key(7)] public int Height { get; set; }
        [Key(8)] public int CurrentHealth { get; set; }
        [Key(9)] public int MaxHealth { get; set; }
    }

    public sealed class KnightDismountPacketFormatter : IMessagePackFormatter<KnightDismountPacket>
    {
        public void Serialize(ref MessagePackWriter writer, KnightDismountPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(10);
            writer.Write(value.SourcePlayerId);
            writer.Write(value.RequestId);
            writer.Write(value.KnightGlobalId);
            writer.Write(value.OwnerPlayerId);
            writer.Write(value.ColorPlayerId);
            writer.Write(value.TileX);
            writer.Write(value.TileY);
            writer.Write(value.Height);
            writer.Write(value.CurrentHealth);
            writer.Write(value.MaxHealth);
        }

        public KnightDismountPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            KnightDismountPacket packet = new KnightDismountPacket();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        packet.SourcePlayerId = reader.ReadInt32();
                        break;
                    case 1:
                        packet.RequestId = reader.ReadInt32();
                        break;
                    case 2:
                        packet.KnightGlobalId = reader.ReadInt32();
                        break;
                    case 3:
                        packet.OwnerPlayerId = reader.ReadInt32();
                        break;
                    case 4:
                        packet.ColorPlayerId = reader.ReadInt32();
                        break;
                    case 5:
                        packet.TileX = reader.ReadInt32();
                        break;
                    case 6:
                        packet.TileY = reader.ReadInt32();
                        break;
                    case 7:
                        packet.Height = reader.ReadInt32();
                        break;
                    case 8:
                        packet.CurrentHealth = reader.ReadInt32();
                        break;
                    case 9:
                        packet.MaxHealth = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return packet;
        }
    }

    internal sealed class KnightDismountButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private static readonly Thickness DefaultButtonMargin = new Thickness(81, 40, 0, 3);

        private Visibility buttonVisibility = Visibility.Hidden;
        private Thickness buttonMargin = DefaultButtonMargin;

        public KnightDismountButtonViewModel(Action dismount, Action showTooltip, Action hideTooltip)
        {
            DismountCommand = new RelayCommand(dismount ?? throw new ArgumentNullException(nameof(dismount)));
            MouseEnterCommand = new RelayCommand(showTooltip ?? throw new ArgumentNullException(nameof(showTooltip)));
            MouseLeaveCommand = new RelayCommand(hideTooltip ?? throw new ArgumentNullException(nameof(hideTooltip)));
        }

        public RelayCommand DismountCommand { get; }
        public RelayCommand MouseEnterCommand { get; }
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

        public void SetVisible(bool visible)
        {
            ButtonVisibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        public void SetPlacement(bool visible, Thickness margin)
        {
            ButtonMargin = margin;
            SetVisible(visible);
        }
    }

    internal sealed unsafe class KnightDismountRuntime : IDisposable
    {
        private delegate void SetuptroopActionsUIDelegate(HUD_Troops self, bool fromInitialOpening);

        private static readonly Thickness BottomRightSlotMargin = new Thickness(81, 40, 0, 3);

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Dictionary<int, HashSet<int>> processedRequestIds = new Dictionary<int, HashSet<int>>();
        private readonly KnightDismountButtonViewModel buttonViewModel;
        private Hook setupTroopActionsHook;
        private SetuptroopActionsUIDelegate setupTroopActionsTrampoline;
        private R3PacketEventHook<KnightDismountPacket> packetHook;
        private IDisposable packetSubscription;
        private Button hookedDismountButton;
        private FieldInfo engineSelectedChimpsField;
        private FieldInfo editorSelectedChimpListField;
        private FieldInfo editorLastSelectedChimpListField;
        private FieldInfo editorGotNewSelectionInfoField;
        private List<int> pendingSelectionIds;
        private int pendingSelectionLocalPlayerId;
        private int pendingSelectionAttempts;
        private long pendingSelectionDueTimestamp;
        private int nextRequestId;
        private string lastPlacementName;
        private bool initialized;
        private bool disposed;

        public KnightDismountRuntime(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            buttonViewModel = new KnightDismountButtonViewModel(OnDismountCommand, ShowDismountTooltip, HideDismountTooltip);
        }

        public KnightDismountButtonViewModel ButtonViewModel => buttonViewModel;

        public void Initialize()
        {
            if (initialized)
                return;

            disposed = false;
            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<KnightDismountPacket>();
            packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            setupTroopActionsHook = new Hook(FindSetuptroopActionsUIMethod(), (SetuptroopActionsUIDelegate)SetuptroopActionsUIHook);
            setupTroopActionsTrampoline = setupTroopActionsHook.GenerateTrampoline<SetuptroopActionsUIDelegate>();
            engineSelectedChimpsField = typeof(EngineInterface).GetField("selectedChimps", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            editorSelectedChimpListField = typeof(EditorDirector).GetField("selectedChimpList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            editorLastSelectedChimpListField = typeof(EditorDirector).GetField("lastSelectedChimpList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            editorGotNewSelectionInfoField = typeof(EditorDirector).GetField("gotNewSelectionInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            initialized = true;
            RefreshButtonVisibility();
            LogDebug("Knight dismount runtime initialized.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            initialized = false;
            buttonViewModel.SetVisible(false);
            if (hookedDismountButton != null)
            {
                hookedDismountButton.MouseEnter -= OnDismountButtonMouseEnter;
                hookedDismountButton.MouseLeave -= OnDismountButtonMouseLeave;
                hookedDismountButton = null;
            }

            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            packetSubscription?.Dispose();
            packetSubscription = null;
            setupTroopActionsHook?.Undo();
            setupTroopActionsHook?.Dispose();
            setupTroopActionsHook = null;
            setupTroopActionsTrampoline = null;
            engineSelectedChimpsField = null;
            editorSelectedChimpListField = null;
            editorLastSelectedChimpListField = null;
            editorGotNewSelectionInfoField = null;
            packetHook = null;
            processedRequestIds.Clear();
            LogDebug("Knight dismount runtime disposed.");
        }

        public void RefreshButtonVisibility()
        {
            try
            {
                bool visible = settings.EnableMod && settings.EnableKnightDismount && HasSelectedOwnKnight();
                if (!visible)
                {
                    buttonViewModel.SetVisible(false);
                    lastPlacementName = null;
                    return;
                }

                if (!IsBottomRightSlotFree())
                {
                    buttonViewModel.SetVisible(false);
                    if (!string.Equals(lastPlacementName, "hidden-bottom-right-occupied", StringComparison.Ordinal))
                    {
                        lastPlacementName = "hidden-bottom-right-occupied";
                        LogDebug("Knight dismount button hidden: bottom-right command slot is occupied.");
                    }

                    return;
                }

                buttonViewModel.SetPlacement(true, BottomRightSlotMargin);
                if (!string.Equals(lastPlacementName, "slot-bottom-right", StringComparison.Ordinal))
                {
                    lastPlacementName = "slot-bottom-right";
                    LogDebug("Knight dismount button placement: slot-bottom-right.");
                }
            }
            catch (Exception ex)
            {
                buttonViewModel.SetVisible(false);
                lastPlacementName = null;
                LogError($"Knight dismount visibility refresh failed: {ex}");
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
            HookDismountButtonEvents(self);
            RefreshButtonVisibility();
        }

        private void HookDismountButtonEvents(HUD_Troops troopPanel)
        {
            if (troopPanel == null)
                return;

            Button button = troopPanel.FindName("SomeSettingsKnightDismountButton") as Button;
            if (button == null || ReferenceEquals(button, hookedDismountButton))
                return;

            if (hookedDismountButton != null)
            {
                hookedDismountButton.MouseEnter -= OnDismountButtonMouseEnter;
                hookedDismountButton.MouseLeave -= OnDismountButtonMouseLeave;
            }

            hookedDismountButton = button;
            hookedDismountButton.MouseEnter += OnDismountButtonMouseEnter;
            hookedDismountButton.MouseLeave += OnDismountButtonMouseLeave;
            LogInfo("Knight dismount button mouse events hooked.");
        }

        private void OnDismountButtonMouseEnter(object sender, MouseEventArgs e)
        {
            ShowDismountTooltip();
        }

        private void OnDismountButtonMouseLeave(object sender, MouseEventArgs e)
        {
            HideDismountTooltip();
        }

        private void ShowDismountTooltip()
        {
            try
            {
                MainViewModel mainViewModel = MainViewModel.Instance;
                HUD_Troops troopPanel = mainViewModel == null ? null : mainViewModel.HUDTroopPanel;
                if (mainViewModel == null || troopPanel == null)
                    return;

                mainViewModel.TroopsPanelRollover = SerpLocalization.Get(SerpLocalization.KnightDismountTooltip);
                mainViewModel.TroopsPanelRollover_AmountReq1 = string.Empty;
                mainViewModel.TroopsPanelRollover_AmountGot1 = SerpLocalization.Get(SerpLocalization.KnightDismountTooltipBody);
                mainViewModel.TroopsPanelRollover_GoodsImage1 = null;
                SetTroopRolloverVisibility(troopPanel, false, true);
                LogInfo("Knight dismount tooltip shown.");
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount tooltip show failed: {ex}");
            }
        }

        private void HideDismountTooltip()
        {
            try
            {
                HUD_Troops troopPanel = MainViewModel.Instance == null ? null : MainViewModel.Instance.HUDTroopPanel;
                if (troopPanel != null)
                    SetTroopRolloverVisibility(troopPanel, false, false);
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount tooltip hide failed: {ex}");
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

        private bool HasSelectedOwnKnight()
        {
            int localPlayerId = GetLocalPlayerIdOrOne();
            int[] selectedUnits = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;

            for (int i = 0; i < selectedUnits.Length; i++)
            {
                int unitId = selectedUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (IsOwnAliveKnight(unit, localPlayerId))
                    return true;
            }

            int[] aliveUnits = unitApi.GetAllAliveUnits();
            for (int i = 0; i < aliveUnits.Length; i++)
            {
                int unitId = aliveUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (IsSelected(unit) && IsOwnAliveKnight(unit, localPlayerId))
                    return true;
            }

            return false;
        }

        private static bool IsBottomRightSlotFree()
        {
            HUD_Troops troopPanel = MainViewModel.Instance == null ? null : MainViewModel.Instance.HUDTroopPanel;
            if (troopPanel == null)
                return true;

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
                if (!settings.EnableMod || !settings.EnableKnightDismount)
                    return;

                int localPlayerId = GetLocalPlayerIdOrOne();
                int[] originalSelectedUnits = GetSelectedChimpsSafe();
                List<KnightDismountSnapshot> snapshots = CaptureSelectedKnightSnapshots(localPlayerId);
                if (snapshots.Count == 0)
                {
                    LogInfo("Knight dismount clicked, but no selected own mounted knights were found.");
                    RefreshButtonVisibility();
                    return;
                }

                LogInfo($"Knight dismount clicked: sourcePlayerId={localPlayerId}, count={snapshots.Count}.");
                List<KnightDismountSnapshot> appliedSnapshots = new List<KnightDismountSnapshot>(snapshots.Count);
                List<int> createdSwordsmanIds = ApplyDismountBatch(snapshots, "local-click", appliedSnapshots);
                for (int i = 0; i < appliedSnapshots.Count; i++)
                {
                    int requestId = ++nextRequestId;
                    SendDismountPacket(localPlayerId, requestId, appliedSnapshots[i]);
                }

                List<int> postDismountSelectionIds = BuildPostDismountSelection(originalSelectedUnits, snapshots, createdSwordsmanIds, localPlayerId);
                ScheduleDeferredSelection(postDismountSelectionIds, localPlayerId);
                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount click failed: {ex}");
            }
        }

        private List<KnightDismountSnapshot> CaptureSelectedKnightSnapshots(int localPlayerId)
        {
            List<KnightDismountSnapshot> snapshots = new List<KnightDismountSnapshot>();
            int[] selectedUnits = GetSelectedChimpsSafe();
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            HashSet<int> seenGlobalIds = new HashSet<int>();

            LogInfo($"Knight dismount capture started: selectedUnitCount={selectedUnits.Length}.");

            for (int i = 0; i < selectedUnits.Length; i++)
            {
                int unitId = selectedUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (!IsOwnAliveKnight(unit, localPlayerId))
                    continue;

                AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
            }

            int[] aliveUnits = unitApi.GetAllAliveUnits();
            int selectedFlagMatches = 0;
            for (int i = 0; i < aliveUnits.Length; i++)
            {
                int unitId = aliveUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (!IsSelected(unit) || !IsOwnAliveKnight(unit, localPlayerId))
                    continue;

                selectedFlagMatches++;
                AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
            }

            LogInfo($"Knight dismount capture finished: apiSelected={selectedUnits.Length}, selectedFlagMatches={selectedFlagMatches}, snapshots={snapshots.Count}.");
            return snapshots;
        }

        private void AddSnapshot(List<KnightDismountSnapshot> snapshots, HashSet<int> seenGlobalIds, int unitId, GameUnit* unit)
        {
            int globalId = (int)unit->r_GlobalId;
            int snapshotKey = globalId > 0 ? globalId : -unitId;
            if (!seenGlobalIds.Add(snapshotKey))
                return;

            KnightDismountSnapshot snapshot = new KnightDismountSnapshot
            {
                UnitId = unitId,
                GlobalId = globalId,
                OwnerPlayerId = unit->r_ControllableForPlayerId,
                ColorPlayerId = (int)unit->r_SpritePlayerColorId,
                TileX = unit->r_CurrentTilePositionX,
                TileY = unit->r_CurrentTilePositionY,
                Height = unit->r_HeightElevation,
                CurrentHealth = (int)unit->r_CurrentHealth,
                MaxHealth = (int)unit->r_MaxHealth
            };

            snapshots.Add(snapshot);
            LogInfo($"Knight dismount snapshot: unitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, owner={snapshot.OwnerPlayerId}, color={snapshot.ColorPlayerId}, selected=({unit->r_UnitSelected},{unit->r_UnitSelected2}), tile=({snapshot.TileX},{snapshot.TileY}), height={snapshot.Height}, hp={snapshot.CurrentHealth}/{snapshot.MaxHealth}.");
        }

        private void SendDismountPacket(int sourcePlayerId, int requestId, KnightDismountSnapshot snapshot)
        {
            if (!GameNetworkAPI.IsNetworkedEnvironment() || packetHook == null)
                return;

            try
            {
                KnightDismountPacket packet = new KnightDismountPacket
                {
                    SourcePlayerId = sourcePlayerId,
                    RequestId = requestId,
                    KnightGlobalId = snapshot.GlobalId,
                    OwnerPlayerId = snapshot.OwnerPlayerId,
                    ColorPlayerId = snapshot.ColorPlayerId,
                    TileX = snapshot.TileX,
                    TileY = snapshot.TileY,
                    Height = snapshot.Height,
                    CurrentHealth = snapshot.CurrentHealth,
                    MaxHealth = snapshot.MaxHealth
                };

                GameNetworkAPI.SendPacketToAll(packet, packetHook.GetPacketId());
                LogDebug($"Knight dismount packet sent: sourcePlayerId={sourcePlayerId}, requestId={requestId}, globalId={snapshot.GlobalId}, owner={snapshot.OwnerPlayerId}.");
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount packet send failed: sourcePlayerId={sourcePlayerId}, requestId={requestId}, globalId={snapshot.GlobalId}, owner={snapshot.OwnerPlayerId}: {ex}");
            }
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<KnightDismountPacket> args)
        {
            try
            {
                if (!settings.EnableMod || !settings.EnableKnightDismount || args == null || args.Packet == null)
                    return;

                KnightDismountPacket packet = args.Packet;
                if (IsDuplicatePacket(packet.SourcePlayerId, packet.RequestId))
                {
                    LogDebug($"Knight dismount packet ignored as duplicate: sourcePlayerId={packet.SourcePlayerId}, requestId={packet.RequestId}.");
                    return;
                }

                int unitId = FindAliveUnitIdByGlobalId(packet.KnightGlobalId);
                if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                {
                    LogDebug($"Knight dismount packet ignored, unit not found: sourcePlayerId={packet.SourcePlayerId}, requestId={packet.RequestId}, globalId={packet.KnightGlobalId}.");
                    return;
                }

                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_KNIGHT ||
                    unit->r_ControllableForPlayerId != packet.OwnerPlayerId)
                {
                    LogDebug($"Knight dismount packet ignored, validation failed: sourcePlayerId={packet.SourcePlayerId}, requestId={packet.RequestId}, unitId={unitId}, type={unit->r_UnitChimp}, owner={unit->r_ControllableForPlayerId}, expectedOwner={packet.OwnerPlayerId}, alive={unit->r_AliveState}.");
                    return;
                }

                KnightDismountSnapshot snapshot = new KnightDismountSnapshot
                {
                    UnitId = unitId,
                    GlobalId = packet.KnightGlobalId,
                    OwnerPlayerId = packet.OwnerPlayerId,
                    ColorPlayerId = packet.ColorPlayerId,
                    TileX = packet.TileX,
                    TileY = packet.TileY,
                    Height = packet.Height,
                    CurrentHealth = packet.CurrentHealth,
                    MaxHealth = packet.MaxHealth
                };

                ApplyDismount(snapshot, $"network:{packet.SourcePlayerId}:{packet.RequestId}", out _);
                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount packet handling failed: {ex}");
            }
        }

        private bool IsDuplicatePacket(int sourcePlayerId, int requestId)
        {
            if (sourcePlayerId <= 0 || requestId <= 0)
                return false;

            if (!processedRequestIds.TryGetValue(sourcePlayerId, out HashSet<int> requestIds))
            {
                requestIds = new HashSet<int>();
                processedRequestIds[sourcePlayerId] = requestIds;
            }

            return !requestIds.Add(requestId);
        }

        private List<int> ApplyDismountBatch(List<KnightDismountSnapshot> snapshots, string reason, List<KnightDismountSnapshot> appliedSnapshots)
        {
            List<int> createdSwordsmanIds = new List<int>();
            if (snapshots == null || snapshots.Count == 0)
                return createdSwordsmanIds;

            List<ResolvedDismountSnapshot> resolvedSnapshots = new List<ResolvedDismountSnapshot>(snapshots.Count);
            HashSet<int> seenCurrentUnitIds = new HashSet<int>();

            for (int i = 0; i < snapshots.Count; i++)
            {
                KnightDismountSnapshot snapshot = snapshots[i];
                if (!TryResolveAliveKnightByUnitId(snapshot, reason, out int currentUnitId))
                    continue;

                if (!seenCurrentUnitIds.Add(currentUnitId))
                {
                    LogInfo($"Knight dismount skipped duplicate resolved unit: reason={reason}, unitId={currentUnitId}, originalUnitId={snapshot.UnitId}, globalId={snapshot.GlobalId}.");
                    continue;
                }

                resolvedSnapshots.Add(new ResolvedDismountSnapshot
                {
                    Snapshot = snapshot,
                    CurrentUnitId = currentUnitId
                });
            }

            List<KnightDismountSnapshot> deletedSnapshots = new List<KnightDismountSnapshot>(resolvedSnapshots.Count);
            resolvedSnapshots.Sort((left, right) => right.CurrentUnitId.CompareTo(left.CurrentUnitId));
            for (int i = 0; i < resolvedSnapshots.Count; i++)
            {
                ResolvedDismountSnapshot resolved = resolvedSnapshots[i];
                if (!TryResolveAliveKnightByUnitId(resolved.Snapshot, $"{reason}:delete", out int deleteUnitId))
                    continue;

                GameUnitManagerAPI.Instance.DeleteUnit(deleteUnitId);
                deletedSnapshots.Add(resolved.Snapshot);
                LogInfo($"Knight dismount deleted knight: reason={reason}, unitId={deleteUnitId}, originalUnitId={resolved.Snapshot.UnitId}, globalId={resolved.Snapshot.GlobalId}.");
            }

            for (int i = 0; i < deletedSnapshots.Count; i++)
            {
                KnightDismountSnapshot snapshot = deletedSnapshots[i];
                if (CreateSwordsmanFromSnapshot(snapshot, reason, out int swordsmanUnitId))
                {
                    createdSwordsmanIds.Add(swordsmanUnitId);
                    appliedSnapshots?.Add(snapshot);
                }
            }

            LogInfo($"Knight dismount batch applied: reason={reason}, requested={snapshots.Count}, resolved={resolvedSnapshots.Count}, deleted={deletedSnapshots.Count}, created={createdSwordsmanIds.Count}, ids={string.Join(",", createdSwordsmanIds)}.");
            return createdSwordsmanIds;
        }

        private bool ApplyDismount(KnightDismountSnapshot snapshot, string reason, out int swordsmanUnitId)
        {
            swordsmanUnitId = 0;
            if (!TryResolveAliveKnightByGlobalId(snapshot, reason, out int currentUnitId))
                return false;

            GameUnitManagerAPI.Instance.DeleteUnit(currentUnitId);
            LogInfo($"Knight dismount deleted knight: reason={reason}, unitId={currentUnitId}, originalUnitId={snapshot.UnitId}, globalId={snapshot.GlobalId}.");
            return CreateSwordsmanFromSnapshot(snapshot, reason, out swordsmanUnitId);
        }

        private bool TryResolveAliveKnightByUnitId(KnightDismountSnapshot snapshot, string reason, out int currentUnitId)
        {
            currentUnitId = snapshot.UnitId;
            return ValidateAliveKnight(snapshot, reason, currentUnitId);
        }

        private bool TryResolveAliveKnightByGlobalId(KnightDismountSnapshot snapshot, string reason, out int currentUnitId)
        {
            currentUnitId = snapshot.GlobalId > 0 ? FindAliveUnitIdByGlobalId(snapshot.GlobalId) : snapshot.UnitId;
            return ValidateAliveKnight(snapshot, reason, currentUnitId);
        }

        private bool ValidateAliveKnight(KnightDismountSnapshot snapshot, string reason, int currentUnitId)
        {
            if (currentUnitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* unit))
            {
                LogInfo($"Knight dismount skipped, unit missing: reason={reason}, unitId={snapshot.UnitId}, globalId={snapshot.GlobalId}.");
                return false;
            }

            if (unit->r_AliveState != AliveState.IsAlive ||
                unit->r_UnitChimp != eChimps.CHIMP_TYPE_KNIGHT ||
                unit->r_ControllableForPlayerId != snapshot.OwnerPlayerId)
            {
                LogInfo($"Knight dismount skipped, validation failed: reason={reason}, unitId={currentUnitId}, originalUnitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, type={unit->r_UnitChimp}, owner={unit->r_ControllableForPlayerId}, expectedOwner={snapshot.OwnerPlayerId}, alive={unit->r_AliveState}.");
                return false;
            }

            return true;
        }

        private bool CreateSwordsmanFromSnapshot(KnightDismountSnapshot snapshot, string reason, out int swordsmanUnitId)
        {
            swordsmanUnitId = 0;
            long createdId = GameUnitManagerAPI.Instance.CreateUnitLocal(
                snapshot.ColorPlayerId,
                snapshot.OwnerPlayerId,
                snapshot.TileX,
                snapshot.TileY,
                snapshot.Height,
                eChimps.CHIMP_TYPE_SWORDSMAN);

            if (createdId <= 0 || createdId > int.MaxValue)
            {
                LogError($"Knight dismount spawned invalid swordsman id: reason={reason}, originalUnitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, createdId={createdId}.");
                return false;
            }

            swordsmanUnitId = (int)createdId;
            ApplyHealthRatio(swordsmanUnitId, snapshot.CurrentHealth, snapshot.MaxHealth);
            LogInfo($"Knight dismount created swordsman: reason={reason}, originalUnitId={snapshot.UnitId}, knightGlobalId={snapshot.GlobalId}, swordsmanUnitId={createdId}, owner={snapshot.OwnerPlayerId}, color={snapshot.ColorPlayerId}, tile=({snapshot.TileX},{snapshot.TileY}), sourceHp={snapshot.CurrentHealth}/{snapshot.MaxHealth}.");
            return true;
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

        private List<int> BuildPostDismountSelection(int[] originalSelectedUnits, List<KnightDismountSnapshot> snapshots, List<int> createdSwordsmanIds, int localPlayerId)
        {
            List<int> selectionIds = new List<int>();
            HashSet<int> seenIds = new HashSet<int>();
            HashSet<int> removedKnightUnitIds = new HashSet<int>();

            if (snapshots != null)
            {
                for (int i = 0; i < snapshots.Count; i++)
                    removedKnightUnitIds.Add(snapshots[i].UnitId);
            }

            if (originalSelectedUnits != null)
            {
                for (int i = 0; i < originalSelectedUnits.Length; i++)
                {
                    int unitId = originalSelectedUnits[i];
                    if (unitId <= 0 || removedKnightUnitIds.Contains(unitId) || !seenIds.Add(unitId))
                        continue;

                    if (IsSelectableOwnAliveUnit(unitId, localPlayerId))
                        selectionIds.Add(unitId);
                }
            }

            if (createdSwordsmanIds != null)
            {
                for (int i = 0; i < createdSwordsmanIds.Count; i++)
                {
                    int unitId = createdSwordsmanIds[i];
                    if (unitId <= 0 || !seenIds.Add(unitId))
                        continue;

                    selectionIds.Add(unitId);
                }
            }

            LogInfo($"Knight dismount post selection built: original={originalSelectedUnits?.Length ?? 0}, removedKnights={removedKnightUnitIds.Count}, created={createdSwordsmanIds?.Count ?? 0}, final={selectionIds.Count}, ids={string.Join(",", selectionIds)}.");
            return selectionIds;
        }

        private void SelectUnits(List<int> requestedUnitIds, int localPlayerId)
        {
            if (requestedUnitIds == null || requestedUnitIds.Count == 0)
                return;

            List<int> selectableIds = new List<int>(requestedUnitIds.Count);
            List<int> selectableTypes = new List<int>(requestedUnitIds.Count);
            HashSet<int> seenIds = new HashSet<int>();
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;

            for (int i = 0; i < requestedUnitIds.Count; i++)
            {
                int unitId = requestedUnitIds[i];
                if (unitId <= 0 || !seenIds.Add(unitId))
                    continue;

                if (!unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_ControllableForPlayerId != localPlayerId)
                    continue;

                unit->r_UnitSelected = 1;
                unit->r_UnitSelected2 = 1;
                unit->r_SelectionRelevant3 = (ushort)UnitSelectionType.SelectionRect;
                selectableIds.Add(unitId);
                selectableTypes.Add((int)unit->r_UnitChimp);
            }

            if (selectableIds.Count == 0)
            {
                LogDebug("Knight dismount selection skipped, no requested units were selectable.");
                return;
            }

            try
            {
                int[] idsArray = selectableIds.ToArray();
                ApplyEngineSelection(selectableIds, selectableTypes);
                ApplyEditorDirectorSelectionCache(idsArray);
                ApplyNativeTroopSelection(idsArray);
                EngineInterface.TroopSelectionChanged(selectableIds.ToArray());
                UpdateEditorDirectorSelection(selectableIds, selectableTypes);
                ResetCommandInputState();
                MainViewModel.Instance?.TroopsSelectedGameAction(false);
                LogSelectionState("Knight dismount selected units", selectableIds);
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount selection failed: count={selectableIds.Count}, ids={string.Join(",", selectableIds)}: {ex}");
            }
        }

        private void ApplyEngineSelection(List<int> selectableIds, List<int> selectableTypes)
        {
            int[] selectedChimps = engineSelectedChimpsField?.GetValue(null) as int[];
            if (selectedChimps == null)
            {
                LogInfo("Knight dismount engine selection skipped, selectedChimps field was not found.");
                return;
            }

            int maxCount = Math.Min(selectableIds.Count, selectedChimps.Length / 2);
            Array.Clear(selectedChimps, 0, selectedChimps.Length);
            ClearSelectedUnitFlags();

            for (int i = 0; i < maxCount; i++)
            {
                int unitId = selectableIds[i];
                selectedChimps[i * 2] = unitId;
                selectedChimps[i * 2 + 1] = selectableTypes[i];

                if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                {
                    unit->r_UnitSelected = 1;
                    unit->r_UnitSelected2 = 1;
                    unit->r_SelectionRelevant3 = (ushort)UnitSelectionType.SelectionRect;
                }
            }

            GameUnitManagerAPI.Instance.GetUnitManager().Pointer->r_SelectedChimpsCount = (uint)maxCount;
            LogInfo($"Knight dismount engine selection applied: count={maxCount}, ids={string.Join(",", selectableIds)}.");
        }

        private void ApplyEditorDirectorSelectionCache(int[] selectableIds)
        {
            EditorDirector editorDirector = EditorDirector.instance;
            if (editorDirector == null)
                return;

            editorSelectedChimpListField?.SetValue(editorDirector, selectableIds);
            editorLastSelectedChimpListField?.SetValue(editorDirector, selectableIds);
            editorGotNewSelectionInfoField?.SetValue(editorDirector, true);
        }

        private static void ApplyNativeTroopSelection(int[] selectableIds)
        {
            int[] empty = Array.Empty<int>();
            EngineInterface.TroopSelection(1, false, false, selectableIds, false, false, empty, -1, -1, false, empty);
            EngineInterface.TroopSelection(2, false, false, selectableIds, true, false, empty, -1, -1, false, empty);
            EngineInterface.TroopSelection(3, false, false, selectableIds, true, true, empty, -1, -1, false, empty);
        }

        private void ResetCommandInputState()
        {
            try
            {
                if (MainControls.instance != null)
                    MainControls.instance.CurrentAction = 0;

                EditorDirector.instance?.clearMouseStateForEngine();
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount command input reset failed: {ex}");
            }
        }

        private void LogSelectionState(string prefix, List<int> selectableIds)
        {
            try
            {
                int selectedCount = GamePlayerManagerAPI.Instance.GetSelectedChimpsCount();
                int[] selectedUnits = GetSelectedChimpsSafe();
                int currentAction = MainControls.instance == null ? -1 : MainControls.instance.CurrentAction;
                LogInfo($"{prefix}: requestedCount={selectableIds.Count}, engineCount={selectedCount}, engineIds={string.Join(",", selectedUnits)}, currentAction={currentAction}, ids={string.Join(",", selectableIds)}.");
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount selection state log failed: {ex}");
            }
        }

        private static void ClearSelectedUnitFlags()
        {
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            int[] aliveUnitIds = unitApi.GetAllAliveUnits();
            for (int i = 0; i < aliveUnitIds.Length; i++)
            {
                int unitId = aliveUnitIds[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                unit->r_UnitSelected = 0;
                unit->r_UnitSelected2 = 0;
            }
        }

        private void ScheduleDeferredSelection(List<int> createdSwordsmanIds, int localPlayerId)
        {
            if (createdSwordsmanIds == null || createdSwordsmanIds.Count == 0)
                return;

            pendingSelectionIds = new List<int>(createdSwordsmanIds);
            pendingSelectionLocalPlayerId = localPlayerId;
            pendingSelectionAttempts = 8;
            pendingSelectionDueTimestamp = Stopwatch.GetTimestamp() + (Stopwatch.Frequency / 2);
            LogInfo($"Knight dismount deferred selection scheduled: delayMs=500, count={pendingSelectionIds.Count}, ids={string.Join(",", pendingSelectionIds)}.");
        }

        private void OnGameTick(int tick)
        {
            if (pendingSelectionIds == null || pendingSelectionIds.Count == 0 || pendingSelectionAttempts <= 0)
                return;

            if (Stopwatch.GetTimestamp() < pendingSelectionDueTimestamp)
                return;

            pendingSelectionAttempts--;
            SelectUnits(pendingSelectionIds, pendingSelectionLocalPlayerId);
            if (pendingSelectionAttempts <= 0)
            {
                pendingSelectionIds = null;
                pendingSelectionLocalPlayerId = 0;
                pendingSelectionDueTimestamp = 0;
            }
        }

        private void UpdateEditorDirectorSelection(List<int> selectableIds, List<int> selectableTypes)
        {
            EngineInterface.PlayState playState = new EngineInterface.PlayState();
            playState.numSelectedChimps = selectableIds.Count;

            for (int i = 0; i < selectableIds.Count; i++)
            {
                playState.selectedChimps[i] = selectableIds[i];
                playState.selectedChimpTypes[i] = selectableTypes[i];
            }

            EditorDirector.instance.updateDLLSelectedTroops(playState, true);
        }

        private static bool IsSelectableOwnAliveUnit(int unitId, int localPlayerId)
        {
            return unitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                unit->r_AliveState == AliveState.IsAlive &&
                unit->r_ControllableForPlayerId == localPlayerId;
        }

        private void ApplyHealthRatio(int swordsmanUnitId, int sourceCurrentHealth, int sourceMaxHealth)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(swordsmanUnitId, out GameUnit* unit))
            {
                LogError($"Knight dismount could not set swordsman health, unit not found: swordsmanUnitId={swordsmanUnitId}.");
                return;
            }

            int targetMaxHealth = Math.Max(1, (int)unit->r_MaxHealth);
            double ratio = sourceMaxHealth > 0 ? Math.Max(0.0, Math.Min(1.0, sourceCurrentHealth / (double)sourceMaxHealth)) : 1.0;
            int targetHealth = Math.Max(1, Math.Min(targetMaxHealth, (int)Math.Round(targetMaxHealth * ratio, MidpointRounding.AwayFromZero)));
            ushort targetPercent = (ushort)Math.Max(0, Math.Min(100, (int)Math.Round(100.0 * targetHealth / targetMaxHealth, MidpointRounding.AwayFromZero)));

            unit->r_CurrentHealth = (uint)targetHealth;
            unit->r_CurrentHealthPercentage = targetPercent;
            unit->r_HealthBarBlocks = (uint)(targetPercent / 10);
            LogDebug($"Knight dismount health set: swordsmanUnitId={swordsmanUnitId}, ratio={ratio.ToString("0.###", CultureInfo.InvariantCulture)}, hp={targetHealth}/{targetMaxHealth}, percent={targetPercent}, blocks={unit->r_HealthBarBlocks}.");
        }

        private static bool IsOwnAliveKnight(GameUnit* unit, int localPlayerId)
        {
            return unit != null &&
                unit->r_AliveState == AliveState.IsAlive &&
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_KNIGHT &&
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
                LogError($"Knight dismount could not read selected units: {ex}");
                return Array.Empty<int>();
            }
        }

        private static int GetLocalPlayerIdOrOne()
        {
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : 1;
        }

        private void LogDebug(string message)
        {
            log.LogDebug($"[{TimestampNow()}] SomeSettings {message}");
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] SomeSettings {message}");
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{TimestampNow()}] SomeSettings {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private struct KnightDismountSnapshot
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
        }

        private struct ResolvedDismountSnapshot
        {
            public KnightDismountSnapshot Snapshot;
            public int CurrentUnitId;
        }
    }
}
