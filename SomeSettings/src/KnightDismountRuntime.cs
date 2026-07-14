using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MessagePack.Formatters;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Zhuqiaomon.Memory;
using Zhuqiaomon.Memory.Scanners;

namespace SomeSettings
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(KnightDismountCommandFormatter))]
    public sealed class KnightDismountCommand
    {
        [Key(0)] public int[] KnightGlobalIds { get; set; }
    }

    public sealed class KnightDismountCommandFormatter : IMessagePackFormatter<KnightDismountCommand>
    {
        public void Serialize(ref MessagePackWriter writer, KnightDismountCommand value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(1);
            WriteIds(ref writer, value.KnightGlobalIds);
        }

        public KnightDismountCommand Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            KnightDismountCommand command = new KnightDismountCommand();
            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                    command.KnightGlobalIds = ReadIds(ref reader);
                else
                    reader.Skip();
            }

            return command;
        }

        internal static void WriteIds(ref MessagePackWriter writer, int[] ids)
        {
            if (ids == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(ids.Length);
            for (int i = 0; i < ids.Length; i++)
                writer.Write(ids[i]);
        }

        internal static int[] ReadIds(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            int[] ids = new int[count];
            for (int i = 0; i < count; i++)
                ids[i] = reader.ReadInt32();

            return ids;
        }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(KnightMountCommandFormatter))]
    public sealed class KnightMountCommand
    {
        [Key(0)] public int[] SwordsmanGlobalIds { get; set; }
    }

    public sealed class KnightMountCommandFormatter : IMessagePackFormatter<KnightMountCommand>
    {
        public void Serialize(ref MessagePackWriter writer, KnightMountCommand value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(1);
            KnightDismountCommandFormatter.WriteIds(ref writer, value.SwordsmanGlobalIds);
        }

        public KnightMountCommand Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            KnightMountCommand command = new KnightMountCommand();
            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                    command.SwordsmanGlobalIds = KnightDismountCommandFormatter.ReadIds(ref reader);
                else
                    reader.Skip();
            }

            return command;
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
        private const string DismountCommandChannel = "knight-dismount";
        private const string MountCommandChannel = "knight-mount";
        private const int MaximumUnitsPerCommand = 512;
        private const string MissingWeaponsSpeechFileName = "Other_Warning6.wav";
        private static readonly string[] MountSpeechFileNames = { "Knight_m1.wav", "Knight_m2.wav", "Knight_m3.wav" };
        private static readonly string[] DismountSpeechFileNames = { "Sword_s4.wav", "Sword_s5.wav", "Sword_s6.wav" };
        private static readonly Random SpeechRandom = new Random();

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Shared.DeterministicMultiplayerCommandBus commandBus;
        private readonly KnightDismountButtonViewModel buttonViewModel;
        private Hook setupTroopActionsHook;
        private SetuptroopActionsUIDelegate setupTroopActionsTrampoline;
        private Button hookedDismountButton;
        private Button hookedMountButton;
        private static ReleaseStableHorseDelegate releaseStableHorse;
        private bool initialized;
        private bool disposed;

        public KnightDismountRuntime(
            ManualLogSource log,
            SomeSettingsViewModel settings,
            Shared.DeterministicMultiplayerCommandBus commandBus)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
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
            try
            {
                commandBus.RegisterChannel<KnightDismountCommand>(DismountCommandChannel, ValidateDismountCommand, ExecuteDismountCommand);
                commandBus.RegisterChannel<KnightMountCommand>(MountCommandChannel, ValidateMountCommand, ExecuteMountCommand);
                setupTroopActionsHook = new Hook(FindSetuptroopActionsUIMethod(), (SetuptroopActionsUIDelegate)SetuptroopActionsUIHook);
                setupTroopActionsTrampoline = setupTroopActionsHook.GenerateTrampoline<SetuptroopActionsUIDelegate>();
                initialized = true;
                buttonViewModel.Hide();
                LogInfo($"Knight mount/dismount runtime initialized: dismountChannel={DismountCommandChannel}, mountChannel={MountCommandChannel}, nativeHorseReleaseAvailable={releaseStableHorse != null}, troopActionsHookInstalled={setupTroopActionsHook != null}.");
            }
            catch
            {
                commandBus.UnregisterChannel(DismountCommandChannel);
                commandBus.UnregisterChannel(MountCommandChannel);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            initialized = false;
            buttonViewModel.Hide();
            UnhookButtonEvents();

            commandBus.UnregisterChannel(DismountCommandChannel);
            commandBus.UnregisterChannel(MountCommandChannel);
            setupTroopActionsHook?.Undo();
            setupTroopActionsHook?.Dispose();
            setupTroopActionsHook = null;
            setupTroopActionsTrampoline = null;
            LogInfo("Knight mount/dismount runtime disposed.");
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
                LogInfo($"Knight dismount button command invoked: EnableMod={settings.EnableMod}, EnableKnightDismount={settings.EnableKnightDismount}.");
                if (!settings.EnableMod || !settings.EnableKnightDismount)
                {
                    LogInfo("Knight dismount button command stopped: reason=feature-disabled.");
                    return;
                }

                int localPlayerId = GetLocalPlayerIdOrOne();
                List<UnitTransformSnapshot> snapshots = CaptureSelectedUnitSnapshots(localPlayerId, eChimps.CHIMP_TYPE_KNIGHT);
                LogInfo($"Knight dismount selection captured: localPlayer={localPlayerId}, snapshotCount={snapshots.Count}, globalIds={FormatSnapshotGlobalIds(snapshots)}.");
                if (snapshots.Count == 0)
                {
                    LogInfo("Knight dismount button command stopped: reason=no-selected-owned-alive-knights.");
                    RefreshButtonVisibility();
                    return;
                }

                int requestId = commandBus.ReserveRequestId();
                KnightDismountCommand command = new KnightDismountCommand
                {
                    KnightGlobalIds = GetSortedGlobalIds(snapshots)
                };
                bool submitted = commandBus.Submit(DismountCommandChannel, requestId, command);
                LogInfo($"Knight dismount deterministic command submission finished: player={localPlayerId}, request={requestId}, units={command.KnightGlobalIds.Length}, globalIds={FormatGlobalIds(command.KnightGlobalIds)}, submitted={submitted}.");
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
                LogInfo($"Knight mount button command invoked: EnableMod={settings.EnableMod}, EnableKnightDismount={settings.EnableKnightDismount}.");
                if (!settings.EnableMod || !settings.EnableKnightDismount)
                {
                    LogInfo("Knight mount button command stopped: reason=feature-disabled.");
                    return;
                }

                int localPlayerId = GetLocalPlayerIdOrOne();

                List<UnitTransformSnapshot> snapshots = CaptureSelectedUnitSnapshots(localPlayerId, eChimps.CHIMP_TYPE_SWORDSMAN);
                LogInfo($"Knight mount selection captured: localPlayer={localPlayerId}, snapshotCount={snapshots.Count}, globalIds={FormatSnapshotGlobalIds(snapshots)}.");
                if (snapshots.Count == 0)
                {
                    LogInfo("Knight mount button command stopped: reason=no-selected-owned-alive-swordsmen.");
                    RefreshButtonVisibility();
                    return;
                }

                List<HorseAllocation> allocations = FindHorseAllocations(localPlayerId, snapshots.Count);
                LogInfo($"Knight mount pre-submit horse search finished: localPlayer={localPlayerId}, requested={snapshots.Count}, allocations={FormatHorseAllocations(allocations)}.");
                if (allocations.Count == 0)
                {
                    LogInfo("Knight mount button command stopped: reason=no-available-stable-horse.");
                    PlayMissingWeaponsSpeech();
                    RefreshButtonVisibility();
                    return;
                }

                int requestId = commandBus.ReserveRequestId();
                KnightMountCommand command = new KnightMountCommand
                {
                    SwordsmanGlobalIds = GetSortedGlobalIds(snapshots)
                };
                bool submitted = commandBus.Submit(MountCommandChannel, requestId, command);
                LogInfo($"Knight mount deterministic command submission finished: player={localPlayerId}, request={requestId}, units={command.SwordsmanGlobalIds.Length}, globalIds={FormatGlobalIds(command.SwordsmanGlobalIds)}, submitted={submitted}.");
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

        private static string ValidateDismountCommand(KnightDismountCommand command)
        {
            return ValidateGlobalIds(command?.KnightGlobalIds);
        }

        private static string ValidateMountCommand(KnightMountCommand command)
        {
            return ValidateGlobalIds(command?.SwordsmanGlobalIds);
        }

        private static string ValidateGlobalIds(int[] globalIds)
        {
            if (globalIds == null || globalIds.Length == 0)
                return "unit-global-ids-empty";
            if (globalIds.Length > MaximumUnitsPerCommand)
                return "too-many-unit-global-ids";

            HashSet<int> seenIds = new HashSet<int>();
            for (int i = 0; i < globalIds.Length; i++)
            {
                if (globalIds[i] <= 0)
                    return "unit-global-id-not-positive";
                if (!seenIds.Add(globalIds[i]))
                    return "duplicate-unit-global-id";
            }

            return null;
        }

        private bool ExecuteDismountCommand(
            KnightDismountCommand command,
            Shared.DeterministicCommandContext context)
        {
            LogInfo($"Knight dismount deterministic feature handler entered: channel={context.ChannelId}, source={context.SourcePlayerId}, request={context.RequestId}, sequence={context.Sequence}, executionTick={context.ExecutionTick}, localSource={context.IsLocalSource}, payloadGlobalIds={FormatGlobalIds(command?.KnightGlobalIds)}, EnableMod={settings.EnableMod}, EnableKnightDismount={settings.EnableKnightDismount}.");
            if (!settings.EnableMod || !settings.EnableKnightDismount)
            {
                LogInfo($"Knight dismount deterministic feature handler stopped: reason=feature-disabled, request={context.RequestId}, sequence={context.Sequence}.");
                return false;
            }

            ReportKnightStateFingerprint(command.KnightGlobalIds, context, "dismount", "before");
            List<UnitTransformSnapshot> snapshots = CaptureUnitSnapshotsByGlobalIds(
                context.SourcePlayerId,
                eChimps.CHIMP_TYPE_KNIGHT,
                command.KnightGlobalIds);
            LogInfo($"Knight dismount deterministic unit resolution finished: request={context.RequestId}, sequence={context.Sequence}, requested={command.KnightGlobalIds.Length}, resolved={snapshots.Count}, resolvedGlobalIds={FormatSnapshotGlobalIds(snapshots)}.");
            List<UnitTransformSnapshot> appliedSnapshots = new List<UnitTransformSnapshot>(snapshots.Count);
            string reason = $"deterministic:{context.SourcePlayerId}:{context.RequestId}:{context.Sequence}:{context.ExecutionTick}";
            ApplyDismountBatch(snapshots, reason, appliedSnapshots);
            ReportKnightStateFingerprint(command.KnightGlobalIds, context, "dismount", "after-0");
            LogInfo($"Knight dismount deterministic feature handler finished: request={context.RequestId}, sequence={context.Sequence}, requested={command.KnightGlobalIds.Length}, resolved={snapshots.Count}, applied={appliedSnapshots.Count}, appliedGlobalIds={FormatSnapshotGlobalIds(appliedSnapshots)}.");
            QueuePostCommandUi(context.IsLocalSource, appliedSnapshots.Count > 0, DismountSpeechFileNames, false);
            return appliedSnapshots.Count > 0;
        }

        private bool ExecuteMountCommand(
            KnightMountCommand command,
            Shared.DeterministicCommandContext context)
        {
            LogInfo($"Knight mount deterministic feature handler entered: channel={context.ChannelId}, source={context.SourcePlayerId}, request={context.RequestId}, sequence={context.Sequence}, executionTick={context.ExecutionTick}, localSource={context.IsLocalSource}, payloadGlobalIds={FormatGlobalIds(command?.SwordsmanGlobalIds)}, EnableMod={settings.EnableMod}, EnableKnightDismount={settings.EnableKnightDismount}.");
            if (!settings.EnableMod || !settings.EnableKnightDismount)
            {
                LogInfo($"Knight mount deterministic feature handler stopped: reason=feature-disabled, request={context.RequestId}, sequence={context.Sequence}.");
                return false;
            }

            ReportKnightStateFingerprint(command.SwordsmanGlobalIds, context, "mount", "before");
            List<UnitTransformSnapshot> snapshots = CaptureUnitSnapshotsByGlobalIds(
                context.SourcePlayerId,
                eChimps.CHIMP_TYPE_SWORDSMAN,
                command.SwordsmanGlobalIds);
            List<HorseAllocation> allocations = FindHorseAllocations(context.SourcePlayerId, snapshots.Count);
            LogInfo($"Knight mount deterministic resolution finished: request={context.RequestId}, sequence={context.Sequence}, requested={command.SwordsmanGlobalIds.Length}, resolved={snapshots.Count}, resolvedGlobalIds={FormatSnapshotGlobalIds(snapshots)}, allocations={FormatHorseAllocations(allocations)}.");
            int applyCount = Math.Min(snapshots.Count, allocations.Count);
            if (applyCount == 0)
            {
                ReportKnightStateFingerprint(command.SwordsmanGlobalIds, context, "mount", "after-0");
                LogInfo($"Knight mount deterministic feature handler stopped: reason=no-resolved-unit-or-horse, request={context.RequestId}, sequence={context.Sequence}, resolved={snapshots.Count}, horseAllocations={allocations.Count}.");
                QueuePostCommandUi(context.IsLocalSource, false, null, true);
                return false;
            }

            List<AppliedMountSnapshot> appliedSnapshots = new List<AppliedMountSnapshot>(applyCount);
            string reason = $"deterministic:{context.SourcePlayerId}:{context.RequestId}:{context.Sequence}:{context.ExecutionTick}";
            ApplyMountBatch(snapshots, allocations, applyCount, reason, appliedSnapshots);
            ReportKnightStateFingerprint(command.SwordsmanGlobalIds, context, "mount", "after-0");
            LogInfo($"Knight mount deterministic feature handler finished: request={context.RequestId}, sequence={context.Sequence}, requested={command.SwordsmanGlobalIds.Length}, resolved={snapshots.Count}, applyCount={applyCount}, applied={appliedSnapshots.Count}, appliedSourceGlobalIds={FormatAppliedMountGlobalIds(appliedSnapshots)}.");
            QueuePostCommandUi(context.IsLocalSource, appliedSnapshots.Count > 0, MountSpeechFileNames, false);
            return appliedSnapshots.Count > 0;
        }

        private List<UnitTransformSnapshot> CaptureUnitSnapshotsByGlobalIds(
            int ownerPlayerId,
            eChimps unitType,
            int[] globalIds)
        {
            List<UnitTransformSnapshot> snapshots = new List<UnitTransformSnapshot>(globalIds.Length);
            for (int i = 0; i < globalIds.Length; i++)
            {
                int unitId = FindAliveUnitIdByGlobalId(globalIds[i]);
                if (unitId <= 0)
                {
                    LogInfo($"Knight deterministic unit resolution skipped global id: reason=alive-unit-not-found, owner={ownerPlayerId}, expectedType={unitType}, globalId={globalIds[i]}.");
                    continue;
                }
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                {
                    LogInfo($"Knight deterministic unit resolution skipped global id: reason=unit-id-lookup-failed, owner={ownerPlayerId}, expectedType={unitType}, globalId={globalIds[i]}, unitId={unitId}.");
                    continue;
                }
                if (!IsOwnAliveUnit(unit, ownerPlayerId, unitType))
                {
                    LogInfo($"Knight deterministic unit resolution skipped global id: reason=unit-state-mismatch, owner={ownerPlayerId}, expectedType={unitType}, globalId={globalIds[i]}, unitId={unitId}, actualAliveState={unit->r_AliveState}, actualType={unit->r_UnitChimp}, actualOwner={unit->r_ControllableForPlayerId}.");
                    continue;
                }

                snapshots.Add(CreateSnapshotFromUnit(unitId, unit));
            }

            return snapshots;
        }

        private static int[] GetSortedGlobalIds(List<UnitTransformSnapshot> snapshots)
        {
            List<int> globalIds = new List<int>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].GlobalId > 0)
                    globalIds.Add(snapshots[i].GlobalId);
            }

            globalIds.Sort();
            return globalIds.ToArray();
        }

        private void QueuePostCommandUi(bool localSource, bool applied, string[] speechFiles, bool missingHorse)
        {
            UnityMainThreadDispatcher.EnqueueStatic(() =>
            {
                if (disposed)
                    return;

                if (localSource)
                {
                    if (missingHorse)
                        PlayMissingWeaponsSpeech();
                    else if (applied && speechFiles != null)
                        PlayRandomLocalSpeech(speechFiles, speechFiles == MountSpeechFileNames ? "mount" : "dismount");
                }

                RefreshButtonVisibility();
            });
        }

        private void ApplyDismountBatch(List<UnitTransformSnapshot> snapshots, string reason, List<UnitTransformSnapshot> appliedSnapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                LogInfo($"Knight dismount batch stopped: reason={reason}, cause=no-snapshots.");
                return;
            }

            LogInfo($"Knight dismount batch started: reason={reason}, snapshotCount={snapshots.Count}, globalIds={FormatSnapshotGlobalIds(snapshots)}.");

            List<ResolvedTransformSnapshot> resolvedSnapshots = new List<ResolvedTransformSnapshot>(snapshots.Count);
            HashSet<int> seenCurrentUnitIds = new HashSet<int>();

            for (int i = 0; i < snapshots.Count; i++)
            {
                UnitTransformSnapshot snapshot = snapshots[i];
                if (!TryResolveAliveUnitByUnitId(snapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentUnitId))
                {
                    LogInfo($"Knight dismount batch pre-resolution skipped unit: reason={reason}, cause=unit-state-invalid, unitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, owner={snapshot.OwnerPlayerId}.");
                    continue;
                }

                if (!seenCurrentUnitIds.Add(currentUnitId))
                {
                    LogInfo($"Knight dismount batch pre-resolution skipped unit: reason={reason}, cause=duplicate-current-unit-id, unitId={currentUnitId}, globalId={snapshot.GlobalId}.");
                    continue;
                }

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
                {
                    LogInfo($"Knight dismount batch mutation skipped unit: reason={reason}, cause=unit-changed-before-mutation, unitId={resolved.CurrentUnitId}, globalId={resolved.Snapshot.GlobalId}.");
                    continue;
                }

                if (!GameUnitManagerAPI.Instance.TryGetUnitById(deleteUnitId, out GameUnit* deleteUnit))
                {
                    LogInfo($"Knight dismount batch mutation skipped unit: reason={reason}, cause=unit-lookup-failed, unitId={deleteUnitId}, globalId={resolved.Snapshot.GlobalId}.");
                    continue;
                }

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
                    LogInfo($"Knight dismount batch rolling back replacement: reason={reason}, cause=vanilla-horse-release-failed, knightUnitId={currentKnightId}, knightGlobalId={currentSnapshot.GlobalId}, replacementSwordsmanId={swordsmanUnitId}.");
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
                LogInfo($"Knight dismount batch applied unit: reason={reason}, originalKnightId={currentKnightId}, originalGlobalId={currentSnapshot.GlobalId}, replacementSwordsmanId={swordsmanUnitId}.");
            }

            LogInfo($"Knight dismount batch finished: reason={reason}, resolvedCount={resolvedSnapshots.Count}, appliedCount={appliedSnapshots?.Count ?? 0}.");
        }

        private void ApplyMountBatch(
            List<UnitTransformSnapshot> snapshots,
            List<HorseAllocation> allocations,
            int applyCount,
            string reason,
            List<AppliedMountSnapshot> appliedSnapshots)
        {
            if (snapshots == null || allocations == null || applyCount <= 0)
            {
                LogInfo($"Knight mount batch stopped: reason={reason}, snapshotCount={snapshots?.Count ?? 0}, allocationCount={allocations?.Count ?? 0}, applyCount={applyCount}.");
                return;
            }

            LogInfo($"Knight mount batch started: reason={reason}, snapshotCount={snapshots.Count}, allocationCount={allocations.Count}, applyCount={applyCount}, globalIds={FormatSnapshotGlobalIds(snapshots)}, allocations={FormatHorseAllocations(allocations)}.");

            List<ResolvedMountSnapshot> resolvedSnapshots = new List<ResolvedMountSnapshot>(applyCount);
            HashSet<int> seenCurrentUnitIds = new HashSet<int>();

            for (int i = 0; i < applyCount; i++)
            {
                UnitTransformSnapshot snapshot = snapshots[i];
                if (!TryResolveAliveUnitByUnitId(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int currentUnitId))
                {
                    LogInfo($"Knight mount batch pre-resolution skipped unit: reason={reason}, cause=unit-state-invalid, unitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, owner={snapshot.OwnerPlayerId}.");
                    continue;
                }

                if (!seenCurrentUnitIds.Add(currentUnitId))
                {
                    LogInfo($"Knight mount batch pre-resolution skipped unit: reason={reason}, cause=duplicate-current-unit-id, unitId={currentUnitId}, globalId={snapshot.GlobalId}.");
                    continue;
                }

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
                {
                    LogInfo($"Knight mount batch mutation skipped unit: reason={reason}, cause=unit-changed-before-delete, unitId={resolved.CurrentUnitId}, globalId={resolved.Snapshot.GlobalId}.");
                    continue;
                }

                if (!GameUnitManagerAPI.Instance.TryGetUnitById(deleteUnitId, out GameUnit* deleteUnit))
                {
                    LogInfo($"Knight mount batch mutation skipped unit: reason={reason}, cause=unit-lookup-failed, unitId={deleteUnitId}, globalId={resolved.Snapshot.GlobalId}.");
                    continue;
                }

                UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(deleteUnitId, deleteUnit);
                GameUnitManagerAPI.Instance.DeleteUnit(deleteUnitId);
                deletedSnapshots.Add(new AppliedMountSnapshot
                {
                    Snapshot = currentSnapshot,
                    Allocation = resolved.Allocation
                });
                LogInfo($"Knight mount batch deleted source swordsman: reason={reason}, sourceUnitId={deleteUnitId}, sourceGlobalId={currentSnapshot.GlobalId}, stableId={resolved.Allocation.StableId}, stableGlobalId={resolved.Allocation.StableGlobalId}, stableSlot={resolved.Allocation.Slot}.");
            }

            for (int i = 0; i < deletedSnapshots.Count; i++)
            {
                AppliedMountSnapshot applied = deletedSnapshots[i];
                if (CreateMountedKnightFromSnapshot(applied.Snapshot, applied.Allocation, reason))
                {
                    appliedSnapshots?.Add(applied);
                    LogInfo($"Knight mount batch applied unit: reason={reason}, sourceGlobalId={applied.Snapshot.GlobalId}, stableId={applied.Allocation.StableId}, stableSlot={applied.Allocation.Slot}.");
                }
            }

            LogInfo($"Knight mount batch finished: reason={reason}, resolvedCount={resolvedSnapshots.Count}, deletedCount={deletedSnapshots.Count}, appliedCount={appliedSnapshots?.Count ?? 0}.");
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
            int createdGlobalId = 0;
            if (GameUnitManagerAPI.Instance.TryGetUnitById((int)createdId, out GameUnit* loggedCreatedUnit))
                createdGlobalId = (int)loggedCreatedUnit->r_GlobalId;
            LogInfo($"Knight {label} unit spawned: reason={reason}, sourceUnitId={snapshot.UnitId}, sourceGlobalId={snapshot.GlobalId}, createdUnitId={createdId}, createdGlobalId={createdGlobalId}, unitType={unitType}, owner={snapshot.OwnerPlayerId}, tile={snapshot.TileX},{snapshot.TileY}, sourceHealth={snapshot.CurrentHealth}/{snapshot.MaxHealth}.");
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

            LogInfo($"Knight mount linked spawned knight to stable horse: reason={reason}, sourceGlobalId={snapshot.GlobalId}, knightUnitId={knightUnitId}, knightGlobalId={knight->r_GlobalId}, stableId={allocation.StableId}, stableGlobalId={allocation.StableGlobalId}, stableSlot={allocation.Slot}.");
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

        private bool TryConsumeStableHorse(HorseAllocation allocation, int unitId, int unitGlobalId)
        {
            if (unitId <= 0 || unitId > ushort.MaxValue || unitGlobalId <= 0)
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=invalid-unit-identity, unitId={unitId}, unitGlobalId={unitGlobalId}, stableId={allocation.StableId}, slot={allocation.Slot}.");
                return false;
            }

            if (allocation.StableId <= 0 || allocation.StableId > ushort.MaxValue)
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=invalid-stable-id, unitId={unitId}, stableId={allocation.StableId}, stableGlobalId={allocation.StableGlobalId}, slot={allocation.Slot}.");
                return false;
            }

            if (allocation.Slot < 0 || allocation.Slot >= StableHorseSlotCount)
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=invalid-stable-slot, unitId={unitId}, stableId={allocation.StableId}, slot={allocation.Slot}.");
                return false;
            }

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* mountedUnit))
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=mounted-unit-not-found, unitId={unitId}, stableId={allocation.StableId}, slot={allocation.Slot}.");
                return false;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable))
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=stable-not-found, unitId={unitId}, stableId={allocation.StableId}, slot={allocation.Slot}.");
                return false;
            }

            if (!IsUsableStable(stable, allocation.OwnerPlayerId))
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=stable-not-usable, unitId={unitId}, stableId={allocation.StableId}, expectedOwner={allocation.OwnerPlayerId}, actualOwner={stable->r_PlayerIdOwner}, aliveState={stable->r_AliveState}, type={stable->r_BuildingType}.");
                return false;
            }

            if (allocation.StableGlobalId > 0 && (int)stable->r_GlobalId != allocation.StableGlobalId)
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=stable-global-id-changed, unitId={unitId}, stableId={allocation.StableId}, expectedGlobalId={allocation.StableGlobalId}, actualGlobalId={stable->r_GlobalId}.");
                return false;
            }

            if (GetAvailableStableHorseCount(stable) <= 0 || !IsStableHorseSlotFree(stable, allocation.Slot))
            {
                LogInfo($"Knight mount stable-horse consumption rejected: reason=horse-or-slot-unavailable, unitId={unitId}, stableId={allocation.StableId}, slot={allocation.Slot}, {FormatStableState(allocation.StableId, stable, allocation.Slot)}.");
                return false;
            }

            SetKnightStableBuildingLink(mountedUnit, allocation.StableId, allocation.StableGlobalId);
            SetStablesUnitIdLinkFixed(stable, allocation.Slot, unitId, unitGlobalId);
            stable->r_UsedHorses = (byte)Math.Min(StableHorseSlotCount, ClampStableHorseCount(stable->r_UsedHorses) + 1);
            ushort previousRechargeTimer = stable->r_HorseRechargeTimer;
            stable->r_HorseRechargeTimer = 0;
            LogInfo($"Knight mount normalized stable recharge timer: unitId={unitId}, stableId={allocation.StableId}, previousRecharge={previousRechargeTimer}, normalizedRecharge={stable->r_HorseRechargeTimer}.");
            LogInfo($"Knight mount stable-horse consumption applied: unitId={unitId}, unitGlobalId={unitGlobalId}, stableId={allocation.StableId}, slot={allocation.Slot}, {FormatStableState(allocation.StableId, stable, allocation.Slot)}.");
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
            {
                LogInfo($"Knight dismount horse release not required: reason={reason}, unitId={unitId}, unitGlobalId={unitGlobalId}, stableBacklink=none, slotLink=none.");
                return true;
            }

            LogInfo($"Knight dismount horse release started: reason={reason}, unitId={unitId}, unitGlobalId={unitGlobalId}, stableId={stableId}, stableGlobalId={stableGlobalId}, slotStableId={slotStableId}, slot={slot}, hasSlotLink={hasSlotLink}.");

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

            ushort previousRechargeTimer = stable->r_HorseRechargeTimer;
            stable->r_HorseRechargeTimer = 0;
            LogInfo($"Knight dismount normalized stable recharge timer: reason={reason}, unitId={unitId}, stableId={stableId}, previousRecharge={previousRechargeTimer}, normalizedRecharge={stable->r_HorseRechargeTimer}.");
            LogInfo($"Knight dismount horse release finished: reason={reason}, unitId={unitId}, unitGlobalId={unitGlobalId}, stableId={stableId}, releasedSlot={slot}, {FormatStableState(stableId, stable, slot)}.");
            return true;
        }

        private void ReportKnightStateFingerprint(
            int[] sourceGlobalIds,
            Shared.DeterministicCommandContext context,
            string operation,
            string checkpoint)
        {
            try
            {
                string fingerprint = BuildKnightStateFingerprint(
                    context.SourcePlayerId,
                    sourceGlobalIds,
                    operation);
                bool reported = commandBus.ReportStateFingerprint(context, checkpoint, fingerprint);
                LogInfo(
                    $"Knight state fingerprint report completed: operation={operation}, checkpoint={checkpoint}, " +
                    $"request={context.RequestId}, sequence={context.Sequence}, executionTick={context.ExecutionTick}, reported={reported}, fingerprint={fingerprint}.");
            }
            catch (Exception ex)
            {
                LogError(
                    $"Knight state fingerprint failed: operation={operation}, checkpoint={checkpoint}, " +
                    $"request={context.RequestId}, sequence={context.Sequence}, exception={ex}");
            }
        }

        private static string BuildKnightStateFingerprint(
            int ownerPlayerId,
            int[] sourceGlobalIds,
            string operation)
        {
            StringBuilder fingerprint = new StringBuilder();
            fingerprint.Append("operation=").Append(operation)
                .Append(";owner=").Append(ownerPlayerId)
                .Append(";sources=[");

            int[] sortedGlobalIds = sourceGlobalIds == null
                ? Array.Empty<int>()
                : (int[])sourceGlobalIds.Clone();
            Array.Sort(sortedGlobalIds);
            for (int index = 0; index < sortedGlobalIds.Length; index++)
            {
                if (index > 0)
                    fingerprint.Append('|');

                int globalId = sortedGlobalIds[index];
                int unitId = FindAliveUnitIdByGlobalId(globalId);
                if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                {
                    fingerprint.Append("global=").Append(globalId).Append(":missing");
                    continue;
                }

                UnitTransformSnapshot snapshot = CreateSnapshotFromUnit(unitId, unit);
                fingerprint.Append("id=").Append(snapshot.UnitId)
                    .Append(":global=").Append(snapshot.GlobalId)
                    .Append(":alive=").Append((int)unit->r_AliveState)
                    .Append(":type=").Append((int)unit->r_UnitChimp)
                    .Append(":owner=").Append(snapshot.OwnerPlayerId)
                    .Append(":tile=").Append(snapshot.TileX).Append(',').Append(snapshot.TileY)
                    .Append(":height=").Append(snapshot.Height)
                    .Append(":health=").Append(snapshot.CurrentHealth).Append('/').Append(snapshot.MaxHealth)
                    .Append(":production=").Append(snapshot.LinkedProductionBuildingId)
                    .Append(":stable=").Append(GetKnightStableBuildingId(unit)).Append('/').Append(GetKnightStableBuildingGlobalId(unit));
            }

            fingerprint.Append("];stables=[");
            List<int> stableIndexes = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(stableIndexes, AliveState.IsAlive, eStructs.STRUCT_STABLES);
            stableIndexes.Sort();
            bool firstStable = true;
            for (int index = 0; index < stableIndexes.Count; index++)
            {
                int stableId = ConvertQueryBuildingIndexToId(stableIndexes[index]);
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable) ||
                    stable->r_PlayerIdOwner != ownerPlayerId)
                {
                    continue;
                }

                if (!firstStable)
                    fingerprint.Append('|');
                firstStable = false;
                fingerprint.Append("id=").Append(stableId)
                    .Append(":global=").Append(stable->r_GlobalId)
                    .Append(":total=").Append(stable->r_TotalHorses)
                    .Append(":used=").Append(stable->r_UsedHorses)
                    .Append(":recharge=").Append(stable->r_HorseRechargeTimer)
                    .Append(":slots=")
                    .Append(stable->r_UsedHorse1UnitId).Append('/').Append(stable->r_UsedHorse1GlobalId).Append(',')
                    .Append(stable->r_UsedHorse2UnitId).Append('/').Append(stable->r_UsedHorse2GlobalId).Append(',')
                    .Append(stable->r_UsedHorse3UnitId).Append('/').Append(stable->r_UsedHorse3GlobalId).Append(',')
                    .Append(stable->r_UsedHorse4UnitId).Append('/').Append(stable->r_UsedHorse4GlobalId);
            }

            fingerprint.Append(']');
            return fingerprint.ToString();
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
            LogInfo($"Knight {label} health ratio applied: targetUnitId={targetUnitId}, sourceHealth={sourceCurrentHealth}/{sourceMaxHealth}, targetHealth={targetHealth}/{targetMaxHealth}, targetPercent={targetPercent}.");
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

        private static string FormatGlobalIds(int[] globalIds)
        {
            return globalIds == null ? "null" : globalIds.Length == 0 ? "empty" : string.Join(",", globalIds);
        }

        private static string FormatSnapshotGlobalIds(List<UnitTransformSnapshot> snapshots)
        {
            if (snapshots == null)
                return "null";
            if (snapshots.Count == 0)
                return "empty";

            List<int> globalIds = new List<int>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
                globalIds.Add(snapshots[i].GlobalId);
            return string.Join(",", globalIds);
        }

        private static string FormatHorseAllocations(List<HorseAllocation> allocations)
        {
            if (allocations == null)
                return "null";
            if (allocations.Count == 0)
                return "empty";

            List<string> parts = new List<string>(allocations.Count);
            for (int i = 0; i < allocations.Count; i++)
            {
                HorseAllocation allocation = allocations[i];
                parts.Add($"stable={allocation.StableId}/{allocation.StableGlobalId}:slot={allocation.Slot}:owner={allocation.OwnerPlayerId}");
            }

            return string.Join("|", parts);
        }

        private static string FormatAppliedMountGlobalIds(List<AppliedMountSnapshot> snapshots)
        {
            if (snapshots == null)
                return "null";
            if (snapshots.Count == 0)
                return "empty";

            List<int> globalIds = new List<int>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
                globalIds.Add(snapshots[i].Snapshot.GlobalId);
            return string.Join(",", globalIds);
        }

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, $"SomeSettings knight mount/dismount diagnostic: {message}");
        }

        private void LogError(string message)
        {
            Shared.DebugLogHelper.LogError(log, $"SomeSettings knight mount/dismount diagnostic: {message}");
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
