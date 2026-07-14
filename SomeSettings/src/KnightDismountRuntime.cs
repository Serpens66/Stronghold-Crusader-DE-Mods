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
using System.Runtime.InteropServices;
using Zhuqiaomon.Memory;
using Zhuqiaomon.Memory.Scanners;

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
        [Key(10)] public int LinkedProductionBuildingId { get; set; }
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

            writer.WriteArrayHeader(11);
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
            writer.Write(value.LinkedProductionBuildingId);
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
                    case 0: packet.SourcePlayerId = reader.ReadInt32(); break;
                    case 1: packet.RequestId = reader.ReadInt32(); break;
                    case 2: packet.KnightGlobalId = reader.ReadInt32(); break;
                    case 3: packet.OwnerPlayerId = reader.ReadInt32(); break;
                    case 4: packet.ColorPlayerId = reader.ReadInt32(); break;
                    case 5: packet.TileX = reader.ReadInt32(); break;
                    case 6: packet.TileY = reader.ReadInt32(); break;
                    case 7: packet.Height = reader.ReadInt32(); break;
                    case 8: packet.CurrentHealth = reader.ReadInt32(); break;
                    case 9: packet.MaxHealth = reader.ReadInt32(); break;
                    case 10: packet.LinkedProductionBuildingId = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(KnightMountPacketFormatter))]
    public sealed class KnightMountPacket
    {
        [Key(0)] public int SourcePlayerId { get; set; }
        [Key(1)] public int RequestId { get; set; }
        [Key(2)] public int SwordsmanGlobalId { get; set; }
        [Key(3)] public int OwnerPlayerId { get; set; }
        [Key(4)] public int ColorPlayerId { get; set; }
        [Key(5)] public int TileX { get; set; }
        [Key(6)] public int TileY { get; set; }
        [Key(7)] public int Height { get; set; }
        [Key(8)] public int CurrentHealth { get; set; }
        [Key(9)] public int MaxHealth { get; set; }
        [Key(10)] public int StableId { get; set; }
        [Key(11)] public int StableGlobalId { get; set; }
        [Key(12)] public int StableSlot { get; set; }
        [Key(13)] public int LinkedProductionBuildingId { get; set; }
    }

    public sealed class KnightMountPacketFormatter : IMessagePackFormatter<KnightMountPacket>
    {
        public void Serialize(ref MessagePackWriter writer, KnightMountPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(14);
            writer.Write(value.SourcePlayerId);
            writer.Write(value.RequestId);
            writer.Write(value.SwordsmanGlobalId);
            writer.Write(value.OwnerPlayerId);
            writer.Write(value.ColorPlayerId);
            writer.Write(value.TileX);
            writer.Write(value.TileY);
            writer.Write(value.Height);
            writer.Write(value.CurrentHealth);
            writer.Write(value.MaxHealth);
            writer.Write(value.StableId);
            writer.Write(value.StableGlobalId);
            writer.Write(value.StableSlot);
            writer.Write(value.LinkedProductionBuildingId);
        }

        public KnightMountPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            KnightMountPacket packet = new KnightMountPacket();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0: packet.SourcePlayerId = reader.ReadInt32(); break;
                    case 1: packet.RequestId = reader.ReadInt32(); break;
                    case 2: packet.SwordsmanGlobalId = reader.ReadInt32(); break;
                    case 3: packet.OwnerPlayerId = reader.ReadInt32(); break;
                    case 4: packet.ColorPlayerId = reader.ReadInt32(); break;
                    case 5: packet.TileX = reader.ReadInt32(); break;
                    case 6: packet.TileY = reader.ReadInt32(); break;
                    case 7: packet.Height = reader.ReadInt32(); break;
                    case 8: packet.CurrentHealth = reader.ReadInt32(); break;
                    case 9: packet.MaxHealth = reader.ReadInt32(); break;
                    case 10: packet.StableId = reader.ReadInt32(); break;
                    case 11: packet.StableGlobalId = reader.ReadInt32(); break;
                    case 12: packet.StableSlot = reader.ReadInt32(); break;
                    case 13: packet.LinkedProductionBuildingId = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }

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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ReleaseStableHorseDelegate(NativePointer<GameBuildingManager> buildingManager, int stableId, int unitId);

        private static readonly Thickness BottomRightSlotMargin = new Thickness(80, 40, 0, 3);
        // Vanilla uses this helper for horse cleanup during knight disband/death.
        private const string ReleaseStableHorsePattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 63 DA 48 8D 35 ?? ?? ?? ?? 4C 69 DB 96 01 00 00 33 FF 4C 8B C9 4C 8B D3";
        private const int StableHorseSlotCount = 4;
        private const int KnightStableBuildingIdOffset = 0x3D2;
        private const int KnightStableBuildingGlobalIdOffset = 0x3DC;
        private const string MissingWeaponsSpeechFileName = "Other_Warning6.wav";
        private static readonly string[] MountSpeechFileNames = { "Knight_m1.wav", "Knight_m2.wav", "Knight_m3.wav" };
        private static readonly string[] DismountSpeechFileNames = { "Sword_s4.wav", "Sword_s5.wav", "Sword_s6.wav" };
        private static readonly Random SpeechRandom = new Random();

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Dictionary<int, HashSet<int>> processedRequestIds = new Dictionary<int, HashSet<int>>();
        private readonly KnightDismountButtonViewModel buttonViewModel;
        private Hook setupTroopActionsHook;
        private SetuptroopActionsUIDelegate setupTroopActionsTrampoline;
        private R3PacketEventHook<KnightDismountPacket> dismountPacketHook;
        private R3PacketEventHook<KnightMountPacket> mountPacketHook;
        private IDisposable dismountPacketSubscription;
        private IDisposable mountPacketSubscription;
        private Button hookedDismountButton;
        private Button hookedMountButton;
        private static ReleaseStableHorseDelegate releaseStableHorse;
        private int nextRequestId;
        private bool initialized;
        private bool disposed;

        public KnightDismountRuntime(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            buttonViewModel = new KnightDismountButtonViewModel(
                OnDismountCommand,
                OnMountCommand,
                ShowDismountTooltip,
                ShowMountTooltip,
                HideTooltip);
        }

        public KnightDismountButtonViewModel ButtonViewModel => buttonViewModel;

        public void InstallNativeFunctions(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            DataScanner scanner = DataScanner.Create(memory, unchecked((ulong)libraryHandle.ToInt64()));
            scanner.Scan(ReleaseStableHorsePattern);
            if (scanner.CurrentAddress == 0)
                throw new InvalidOperationException("The Vanilla stable horse release function was not found.");

            releaseStableHorse = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<ReleaseStableHorseDelegate>((IntPtr)scanner.CurrentAddress);
        }

        public void Initialize()
        {
            if (initialized)
                return;

            disposed = false;
            dismountPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<KnightDismountPacket>();
            mountPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<KnightMountPacket>();
            dismountPacketSubscription = dismountPacketHook.GetBaseHook().Observable.Subscribe(OnDismountPacketReceived);
            mountPacketSubscription = mountPacketHook.GetBaseHook().Observable.Subscribe(OnMountPacketReceived);
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

            dismountPacketSubscription?.Dispose();
            mountPacketSubscription?.Dispose();
            dismountPacketSubscription = null;
            mountPacketSubscription = null;
            setupTroopActionsHook?.Undo();
            setupTroopActionsHook?.Dispose();
            setupTroopActionsHook = null;
            setupTroopActionsTrampoline = null;
            dismountPacketHook = null;
            mountPacketHook = null;
            processedRequestIds.Clear();
        }

        public void RefreshButtonVisibility()
        {
            RefreshButtonVisibility(null);
        }

        private void RefreshButtonVisibility(HUD_Troops troopPanel)
        {
            try
            {
                if (!settings.EnableMod || !settings.EnableKnightDismount)
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

                int localPlayerId = GetLocalPlayerIdOrOne();
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

            HookDismountButton(troopPanel.FindName("SomeSettingsKnightDismountButton") as Button);
            HookMountButton(troopPanel.FindName("SomeSettingsKnightMountButton") as Button);
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
                if (!settings.EnableMod || !settings.EnableKnightDismount)
                    return;

                int localPlayerId = GetLocalPlayerIdOrOne();
                List<UnitTransformSnapshot> snapshots = CaptureSelectedUnitSnapshots(localPlayerId, eChimps.CHIMP_TYPE_KNIGHT);
                if (snapshots.Count == 0)
                {
                    RefreshButtonVisibility();
                    return;
                }

                List<UnitTransformSnapshot> appliedSnapshots = new List<UnitTransformSnapshot>(snapshots.Count);
                ApplyDismountBatch(snapshots, "local-click", appliedSnapshots);

                if (appliedSnapshots.Count > 0)
                    PlayRandomLocalSpeech(DismountSpeechFileNames, "dismount");

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

        private void OnMountCommand()
        {
            try
            {
                if (!settings.EnableMod || !settings.EnableKnightDismount)
                {
                    return;
                }

                int localPlayerId = GetLocalPlayerIdOrOne();

                List<UnitTransformSnapshot> snapshots = CaptureSelectedUnitSnapshots(localPlayerId, eChimps.CHIMP_TYPE_SWORDSMAN);
                if (snapshots.Count == 0)
                {
                    RefreshButtonVisibility();
                    return;
                }

                List<HorseAllocation> allocations = FindHorseAllocations(localPlayerId, snapshots.Count);
                if (allocations.Count == 0)
                {
                    PlayMissingWeaponsSpeech();
                    RefreshButtonVisibility();
                    return;
                }

                int applyCount = Math.Min(snapshots.Count, allocations.Count);
                List<AppliedMountSnapshot> appliedSnapshots = new List<AppliedMountSnapshot>(applyCount);
                ApplyMountBatch(snapshots, allocations, applyCount, "local-click", appliedSnapshots);

                if (appliedSnapshots.Count > 0)
                    PlayRandomLocalSpeech(MountSpeechFileNames, "mount");

                for (int i = 0; i < appliedSnapshots.Count; i++)
                {
                    int requestId = ++nextRequestId;
                    SendMountPacket(localPlayerId, requestId, appliedSnapshots[i].Snapshot, appliedSnapshots[i].Allocation);
                }

                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight mount click failed: {ex}");
            }
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

        private void SendDismountPacket(int sourcePlayerId, int requestId, UnitTransformSnapshot snapshot)
        {
            if (!GameNetworkAPI.IsNetworkedEnvironment() || dismountPacketHook == null)
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
                    MaxHealth = snapshot.MaxHealth,
                    LinkedProductionBuildingId = snapshot.LinkedProductionBuildingId
                };

                GameNetworkAPI.SendPacketToAll(packet, dismountPacketHook.GetPacketId());
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount packet send failed: sourcePlayerId={sourcePlayerId}, requestId={requestId}, globalId={snapshot.GlobalId}, owner={snapshot.OwnerPlayerId}: {ex}");
            }
        }

        private void SendMountPacket(int sourcePlayerId, int requestId, UnitTransformSnapshot snapshot, HorseAllocation allocation)
        {
            if (!GameNetworkAPI.IsNetworkedEnvironment() || mountPacketHook == null)
                return;

            try
            {
                KnightMountPacket packet = new KnightMountPacket
                {
                    SourcePlayerId = sourcePlayerId,
                    RequestId = requestId,
                    SwordsmanGlobalId = snapshot.GlobalId,
                    OwnerPlayerId = snapshot.OwnerPlayerId,
                    ColorPlayerId = snapshot.ColorPlayerId,
                    TileX = snapshot.TileX,
                    TileY = snapshot.TileY,
                    Height = snapshot.Height,
                    CurrentHealth = snapshot.CurrentHealth,
                    MaxHealth = snapshot.MaxHealth,
                    StableId = allocation.StableId,
                    StableGlobalId = allocation.StableGlobalId,
                    StableSlot = allocation.Slot,
                    LinkedProductionBuildingId = snapshot.LinkedProductionBuildingId
                };

                GameNetworkAPI.SendPacketToAll(packet, mountPacketHook.GetPacketId());
            }
            catch (Exception ex)
            {
                LogError($"Knight mount packet send failed: sourcePlayerId={sourcePlayerId}, requestId={requestId}, globalId={snapshot.GlobalId}, owner={snapshot.OwnerPlayerId}, stableId={allocation.StableId}, slot={allocation.Slot}: {ex}");
            }
        }

        private void OnDismountPacketReceived(ReceiveCustomPacketEventArgs<KnightDismountPacket> args)
        {
            try
            {
                if (!settings.EnableMod || !settings.EnableKnightDismount || args == null || args.Packet == null)
                    return;

                KnightDismountPacket packet = args.Packet;
                if (!IsValidPacketIdentity(packet.SourcePlayerId, packet.OwnerPlayerId) ||
                    packet.RequestId <= 0 ||
                    packet.KnightGlobalId <= 0)
                {
                    return;
                }

                if (IsDuplicatePacket(packet.SourcePlayerId, packet.RequestId))
                    return;

                int unitId = FindAliveUnitIdByGlobalId(packet.KnightGlobalId);
                if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                    return;

                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_KNIGHT ||
                    unit->r_ControllableForPlayerId != packet.OwnerPlayerId)
                    return;

                UnitTransformSnapshot snapshot = new UnitTransformSnapshot
                {
                    UnitId = unitId,
                    GlobalId = packet.KnightGlobalId,
                    OwnerPlayerId = packet.OwnerPlayerId,
                    ColorPlayerId = packet.ColorPlayerId,
                    TileX = packet.TileX,
                    TileY = packet.TileY,
                    Height = packet.Height,
                    CurrentHealth = packet.CurrentHealth,
                    MaxHealth = packet.MaxHealth,
                    LinkedProductionBuildingId = packet.LinkedProductionBuildingId
                };

                ApplyDismount(snapshot, $"network:{packet.SourcePlayerId}:{packet.RequestId}");
                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight dismount packet handling failed: {ex}");
            }
        }

        private void OnMountPacketReceived(ReceiveCustomPacketEventArgs<KnightMountPacket> args)
        {
            try
            {
                if (!settings.EnableMod || !settings.EnableKnightDismount || args == null || args.Packet == null)
                    return;

                KnightMountPacket packet = args.Packet;
                if (!IsValidPacketIdentity(packet.SourcePlayerId, packet.OwnerPlayerId) ||
                    packet.RequestId <= 0 ||
                    packet.SwordsmanGlobalId <= 0)
                {
                    return;
                }

                if (IsDuplicatePacket(packet.SourcePlayerId, packet.RequestId))
                    return;

                int unitId = FindAliveUnitIdByGlobalId(packet.SwordsmanGlobalId);
                if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                    return;

                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_SWORDSMAN ||
                    unit->r_ControllableForPlayerId != packet.OwnerPlayerId)
                    return;

                if (!TryResolveHorseAllocation(packet.OwnerPlayerId, packet.StableId, packet.StableGlobalId, packet.StableSlot, out HorseAllocation allocation))
                    return;

                UnitTransformSnapshot snapshot = new UnitTransformSnapshot
                {
                    UnitId = unitId,
                    GlobalId = packet.SwordsmanGlobalId,
                    OwnerPlayerId = packet.OwnerPlayerId,
                    ColorPlayerId = packet.ColorPlayerId,
                    TileX = packet.TileX,
                    TileY = packet.TileY,
                    Height = packet.Height,
                    CurrentHealth = packet.CurrentHealth,
                    MaxHealth = packet.MaxHealth,
                    LinkedProductionBuildingId = packet.LinkedProductionBuildingId
                };

                ApplyMount(snapshot, allocation, $"network:{packet.SourcePlayerId}:{packet.RequestId}");
                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight mount packet handling failed: {ex}");
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

        private static bool IsValidPacketIdentity(int sourcePlayerId, int ownerPlayerId)
        {
            return sourcePlayerId > 0 &&
                sourcePlayerId == ownerPlayerId &&
                GamePlayerManagerAPI.Instance.IsPlayerIdValid(ownerPlayerId) &&
                !GamePlayerManagerAPI.Instance.IsAIPlayer(ownerPlayerId);
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

                if (!TryReleaseKnightHorseWithVanilla(currentKnightId, currentKnight, reason))
                {
                    GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                    continue;
                }

                if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId))
                {
                    LogError($"Knight dismount could not mark the original knight for deletion after releasing its horse: reason={reason}, unitId={currentKnightId}, globalId={currentSnapshot.GlobalId}.");
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

            if (!TryReleaseKnightHorseWithVanilla(currentKnightId, currentKnight, reason))
            {
                GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                return false;
            }

            if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId))
            {
                LogError($"Knight dismount could not mark the original knight for deletion after releasing its horse: reason={reason}, unitId={currentKnightId}, globalId={currentSnapshot.GlobalId}.");
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
                snapshot.ColorPlayerId,
                snapshot.OwnerPlayerId,
                snapshot.TileX,
                snapshot.TileY,
                snapshot.Height,
                unitType);

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

            if (!TryConsumeStableHorse(allocation, knightUnitId, (int)knight->r_GlobalId))
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
                int stableId = ConvertQueryBuildingIndexToId(stableIds[i]);
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

        private bool TryResolveHorseAllocation(int ownerPlayerId, int stableId, int stableGlobalId, int slot, out HorseAllocation allocation)
        {
            allocation = default;
            if (stableId <= 0 || slot < 0 || slot >= StableHorseSlotCount)
                return false;

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable))
                return false;

            if (!IsUsableStable(stable, ownerPlayerId))
                return false;

            if (stableGlobalId > 0 && (int)stable->r_GlobalId != stableGlobalId)
                return false;

            if (GetAvailableStableHorseCount(stable) <= 0 || !IsStableHorseSlotFree(stable, slot))
                return false;

            allocation = new HorseAllocation
            {
                StableId = stableId,
                StableGlobalId = (int)stable->r_GlobalId,
                OwnerPlayerId = ownerPlayerId,
                Slot = slot
            };
            return true;
        }

        private bool TryConsumeStableHorse(HorseAllocation allocation, int unitId, int unitGlobalId)
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

            SetKnightStableBuildingLink(mountedUnit, allocation.StableId, allocation.StableGlobalId);
            SetStablesUnitIdLinkFixed(stable, allocation.Slot, unitId, unitGlobalId);
            stable->r_UsedHorses = (byte)Math.Min(StableHorseSlotCount, ClampStableHorseCount(stable->r_UsedHorses) + 1);
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

        private static void SetStablesUnitIdLinkFixed(GameBuilding* stable, int slot, int unitId, int unitGlobalId)
        {
            // Script Extender bug: SetStablesUnitIdLink indexes the UInt16 unit-id fields through int*,
            // which overlaps/skips slots. Replace this helper with the API once it uses ushort* correctly.
            ushort* linksId = &stable->r_UsedHorse1UnitId;
            uint* linksGlobalId = &stable->r_UsedHorse1GlobalId;
            linksId[slot] = (ushort)unitId;
            linksGlobalId[slot] = (uint)unitGlobalId;
        }

        private static int GetKnightStableBuildingId(GameUnit* unit)
        {
            return unit == null ? 0 : *(ushort*)((byte*)unit + KnightStableBuildingIdOffset);
        }

        private static int GetKnightStableBuildingGlobalId(GameUnit* unit)
        {
            return unit == null ? 0 : (int)*(uint*)((byte*)unit + KnightStableBuildingGlobalIdOffset);
        }

        private static void SetKnightStableBuildingLink(GameUnit* unit, int stableId, int stableGlobalId)
        {
            // These currently unnamed GameUnit fields are the stable backlink validated by Vanilla.
            // Replace the raw offsets once the Script Extender exposes named fields or an equivalent API.
            *(ushort*)((byte*)unit + KnightStableBuildingIdOffset) = (ushort)stableId;
            *(uint*)((byte*)unit + KnightStableBuildingGlobalIdOffset) = (uint)stableGlobalId;
        }

        private bool TryReleaseKnightHorseWithVanilla(int unitId, GameUnit* unit, string reason)
        {
            int stableId = GetKnightStableBuildingId(unit);
            int stableGlobalId = GetKnightStableBuildingGlobalId(unit);
            int unitGlobalId = (int)unit->r_GlobalId;
            bool hasSlotLink = TryFindStableHorseLink(unitId, unitGlobalId, out int slotStableId, out int slot);

            if (stableId <= 0 && !hasSlotLink)
                return true;

            if (stableId <= 0 || stableGlobalId <= 0 || !hasSlotLink || slotStableId != stableId)
            {
                LogError($"Knight dismount found an incomplete Vanilla horse link: reason={reason}, unitId={unitId}, unitGlobalId={unit->r_GlobalId}, horseStableId={stableId}, horseStableGlobalId={stableGlobalId}, slotStableId={slotStableId}, slot={slot}.");
                return false;
            }

            if (releaseStableHorse == null)
            {
                LogError($"Knight dismount cannot use Vanilla horse release because the native function is unavailable: reason={reason}, unitId={unitId}, stableId={stableId}.");
                return false;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable) ||
                stable->r_AliveState != AliveState.IsAlive ||
                stable->r_BuildingType != eStructs.STRUCT_STABLES ||
                (int)stable->r_GlobalId != stableGlobalId ||
                stable->r_TotalHorses == 0 ||
                stable->r_UsedHorses == 0 ||
                GetStableHorseSlotUnitId(stable, slot) != unitId ||
                GetStableHorseSlotGlobalId(stable, slot) != unitGlobalId)
            {
                LogError($"Knight dismount rejected an invalid Vanilla stable link: reason={reason}, unitId={unitId}, stableId={stableId}, expectedStableGlobalId={stableGlobalId}.");
                return false;
            }

            // DeleteUnitSafe only removes stale slots. Calling Vanilla's release helper also consumes the
            // returned horse from r_TotalHorses so the stable regenerates it normally.
            releaseStableHorse(GameBuildingManagerAPI.Instance.GetBuildingManager(), stableId, unitId);

            if (TryFindStableHorseLink(unitId, unitGlobalId, out int remainingStableId, out int remainingSlot))
            {
                LogError($"Knight dismount Vanilla horse release left the slot reserved: reason={reason}, unitId={unitId}, stableId={remainingStableId}, slot={remainingSlot}, {FormatStableState(stableId, stable, slot)}.");
                return false;
            }

            return true;
        }

        private static bool TryFindStableHorseLink(int unitId, int unitGlobalId, out int stableId, out int slot)
        {
            stableId = 0;
            slot = -1;
            List<int> stableIndexes = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(stableIndexes, AliveState.IsAlive, eStructs.STRUCT_STABLES);

            for (int i = 0; i < stableIndexes.Count; i++)
            {
                int candidateStableId = ConvertQueryBuildingIndexToId(stableIndexes[i]);
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

        private static int ConvertQueryBuildingIndexToId(int queryIndex)
        {
            // Script Extender bug: GameStructQuery.ToIdList currently returns zero-based array indexes,
            // while building APIs expect one-based IDs. Remove this +1 once ToIdList returns API IDs.
            return queryIndex + 1;
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
