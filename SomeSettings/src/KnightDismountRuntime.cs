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
        private int nextRequestId;
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
            setupTroopActionsHook = new Hook(FindSetuptroopActionsUIMethod(), (SetuptroopActionsUIDelegate)SetuptroopActionsUIHook);
            setupTroopActionsTrampoline = setupTroopActionsHook.GenerateTrampoline<SetuptroopActionsUIDelegate>();
            initialized = true;
            RefreshButtonVisibility();
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

            packetSubscription?.Dispose();
            packetSubscription = null;
            setupTroopActionsHook?.Undo();
            setupTroopActionsHook?.Dispose();
            setupTroopActionsHook = null;
            setupTroopActionsTrampoline = null;
            packetHook = null;
            processedRequestIds.Clear();
        }

        public void RefreshButtonVisibility()
        {
            try
            {
                bool visible = settings.EnableMod && settings.EnableKnightDismount && HasSelectedOwnKnight();
                if (!visible)
                {
                    buttonViewModel.SetVisible(false);
                    return;
                }

                if (!IsBottomRightSlotFree())
                {
                    buttonViewModel.SetVisible(false);
                    return;
                }

                buttonViewModel.SetPlacement(true, BottomRightSlotMargin);
            }
            catch (Exception ex)
            {
                buttonViewModel.SetVisible(false);
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
                List<KnightDismountSnapshot> snapshots = CaptureSelectedKnightSnapshots(localPlayerId);
                if (snapshots.Count == 0)
                {
                    RefreshButtonVisibility();
                    return;
                }

                List<KnightDismountSnapshot> appliedSnapshots = new List<KnightDismountSnapshot>(snapshots.Count);
                ApplyDismountBatch(snapshots, "local-click", appliedSnapshots);
                for (int i = 0; i < appliedSnapshots.Count; i++)
                {
                    int requestId = ++nextRequestId;
                    SendDismountPacket(localPlayerId, requestId, appliedSnapshots[i]);
                }

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
            for (int i = 0; i < aliveUnits.Length; i++)
            {
                int unitId = aliveUnits[i];
                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    continue;

                if (!IsSelected(unit) || !IsOwnAliveKnight(unit, localPlayerId))
                    continue;

                AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
            }

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
                    return;

                int unitId = FindAliveUnitIdByGlobalId(packet.KnightGlobalId);
                if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                    return;

                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_KNIGHT ||
                    unit->r_ControllableForPlayerId != packet.OwnerPlayerId)
                    return;

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

                ApplyDismount(snapshot, $"network:{packet.SourcePlayerId}:{packet.RequestId}");
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

        private void ApplyDismountBatch(List<KnightDismountSnapshot> snapshots, string reason, List<KnightDismountSnapshot> appliedSnapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return;

            List<ResolvedDismountSnapshot> resolvedSnapshots = new List<ResolvedDismountSnapshot>(snapshots.Count);
            HashSet<int> seenCurrentUnitIds = new HashSet<int>();

            for (int i = 0; i < snapshots.Count; i++)
            {
                KnightDismountSnapshot snapshot = snapshots[i];
                if (!TryResolveAliveKnightByUnitId(snapshot, out int currentUnitId))
                    continue;

                if (!seenCurrentUnitIds.Add(currentUnitId))
                    continue;

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
                if (!TryResolveAliveKnightByUnitId(resolved.Snapshot, out int deleteUnitId))
                    continue;

                GameUnitManagerAPI.Instance.DeleteUnit(deleteUnitId);
                deletedSnapshots.Add(resolved.Snapshot);
            }

            for (int i = 0; i < deletedSnapshots.Count; i++)
            {
                KnightDismountSnapshot snapshot = deletedSnapshots[i];
                if (CreateSwordsmanFromSnapshot(snapshot, reason))
                {
                    appliedSnapshots?.Add(snapshot);
                }
            }
        }

        private bool ApplyDismount(KnightDismountSnapshot snapshot, string reason)
        {
            if (!TryResolveAliveKnightByGlobalId(snapshot, out int currentUnitId))
                return false;

            GameUnitManagerAPI.Instance.DeleteUnit(currentUnitId);
            return CreateSwordsmanFromSnapshot(snapshot, reason);
        }

        private bool TryResolveAliveKnightByUnitId(KnightDismountSnapshot snapshot, out int currentUnitId)
        {
            currentUnitId = snapshot.UnitId;
            return ValidateAliveKnight(snapshot, currentUnitId);
        }

        private bool TryResolveAliveKnightByGlobalId(KnightDismountSnapshot snapshot, out int currentUnitId)
        {
            currentUnitId = snapshot.GlobalId > 0 ? FindAliveUnitIdByGlobalId(snapshot.GlobalId) : snapshot.UnitId;
            return ValidateAliveKnight(snapshot, currentUnitId);
        }

        private bool ValidateAliveKnight(KnightDismountSnapshot snapshot, int currentUnitId)
        {
            if (currentUnitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* unit))
                return false;

            if (unit->r_AliveState != AliveState.IsAlive ||
                unit->r_UnitChimp != eChimps.CHIMP_TYPE_KNIGHT ||
                unit->r_ControllableForPlayerId != snapshot.OwnerPlayerId)
                return false;

            return true;
        }

        private bool CreateSwordsmanFromSnapshot(KnightDismountSnapshot snapshot, string reason)
        {
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

            ApplyHealthRatio((int)createdId, snapshot.CurrentHealth, snapshot.MaxHealth);
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

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] SomeSettings {message}");
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
