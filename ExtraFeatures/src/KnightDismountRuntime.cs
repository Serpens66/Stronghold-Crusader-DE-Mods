using APIShared.GameModes;
// Feature: Mount swordsmen and dismount mounted knights through local commands.
using BepInEx.Logging;
using CrusaderDE;
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

namespace ExtraFeatures
{
    internal sealed unsafe partial class KnightDismountRuntime
    {
        private const int StableHorseSlotCount = 4;
        private const int ChoreProtocolVersion = 2;
        private const int MountAction = KnightTransformationPacket.StartMountAction;
        private const int DismountAction = KnightTransformationPacket.StartDismountAction;
        private const string MissingWeaponsSpeechFileName = "Other_Warning6.wav";
        // First argument of SFXManager.playSpeech that Vanilla uses for unit and warning speech.
        private const int VoiceSpeechChannel = 1;
        private static readonly string[] MountSpeechFileNames = { "Knight_m1.wav", "Knight_m2.wav", "Knight_m3.wav" };
        private static readonly string[] DismountSpeechFileNames = { "Sword_s4.wav", "Sword_s5.wav", "Sword_s6.wav" };
        private static readonly Random SpeechRandom = new Random();

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly UnitLimitBridge unitLimitBridge;
        private readonly KnightDismountButtonViewModel buttonViewModel;
        private Button hookedDismountButton;
        private Button hookedMountButton;
        private HUD_Troops tooltipMetadataPanel;
        private StackPanel tooltipMetadataHost;
        private Image tooltipGoldIcon;
        private TextBlock tooltipGoldText;
        private TextBlock tooltipSeparatorText;
        private TextBlock tooltipDelayText;
        private bool tooltipMetadataElementsMissingLogged;
        private bool tooltipGoldIconMissingLogged;
        private bool initialized;
        private bool deactivated;
        private bool networkInitialized;
        private int nextOperationId;
        private R3PacketEventHook<KnightTransformationPacket> transformationPacketHook;
        private IDisposable transformationPacketSubscription;

        public KnightDismountRuntime(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate,
            UnitLimitBridge unitLimitBridge)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
            this.unitLimitBridge = unitLimitBridge ?? throw new ArgumentNullException(nameof(unitLimitBridge));
            selectionWorld = new KnightSelectionWorld(this);
            buttonViewModel = new KnightDismountButtonViewModel(
                () => OnTransformationCommand(DismountAction),
                () => OnTransformationCommand(MountAction),
                ShowDismountTooltip,
                ShowMountTooltip,
                HideTooltip);
        }

        public KnightDismountButtonViewModel ButtonViewModel => buttonViewModel;

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            R3PacketEventHook<KnightTransformationPacket> packetHookCandidate =
                GameNetworkAPI.Instance.GetPacketEventFor<KnightTransformationPacket>();
            IDisposable packetSubscriptionCandidate = null;
            try
            {
                packetSubscriptionCandidate = packetHookCandidate.GetBaseHook().Observable.Subscribe(OnTransformationPacketReceived);
                InitializePersistentInfrastructure();
            }
            catch
            {
                packetSubscriptionCandidate?.Dispose();
                throw;
            }

            transformationPacketHook = packetHookCandidate;
            transformationPacketSubscription = packetSubscriptionCandidate;
            networkInitialized = true;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogDebug($"Knight transformation synchronization registered: protocolVersion={ChoreProtocolVersion}.");
        }

        public void Initialize()
        {
            if (initialized)
                return;

            deactivated = false;
            initialized = true;
            // A full refresh instead of a plain Hide: when queued from another thread it may run
            // after a newer HUD refresh and must not hide a button that is valid by then.
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(RefreshButtonVisibility);
        }

        // Logical switch-off: cancels pending work; process-rooted hooks and subscriptions stay.
        public void Deactivate()
        {
            if (deactivated)
                return;

            deactivated = true;
            initialized = false;
            // Without the monitor the persistent infrastructure never published, so nothing is pending.
            if (EngineLock.TryGetGate(out object engineGate))
            {
                lock (engineGate)
                {
                    CancelAllPending("feature-disabled", refundGold: true, releaseReservedHorse: true);
                    selectionTransfers.Clear();
                }
            }

            // Settings and mission-end paths are not guaranteed to run on the main thread.
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(HideButtonAndUnhook);
        }

        private void HideButtonAndUnhook()
        {
            // A later Initialize may already have re-enabled the feature before this ran.
            if (!deactivated)
                return;

            buttonViewModel.Hide();
            UnhookButtonEvents();
        }

        public void RefreshButtonVisibility()
        {
            // Any full refresh covers a pending coalesced UnitLimit refresh. Clearing the marker here
            // also self-heals if the dispatcher ever dropped a queued callback.
            System.Threading.Interlocked.Exchange(ref unitLimitRefreshQueued, 0);
            HUD_Troops troopPanel = null;
            TryGetHudTroopPanel(out troopPanel);
            RefreshButtonVisibility(troopPanel);
            APIShared.Presentation.TroopActionHud.Reflow(troopPanel);
        }

        internal void RefreshButtonVisibility(HUD_Troops troopPanel)
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

                HookButtonEvents(troopPanel);

                int localPlayerId = GetSelectionPlayerId();
                TryCaptureSelectedUnits(localPlayerId, out APIShared.LocalSelectionSnapshot selection);
                FindSelectedOwnKnightOrSwordsman(localPlayerId, selection, out bool knight, out bool swordsman);
                if (knight)
                {
                    buttonViewModel.ShowDismount(HasUnitLimitCapacity(localPlayerId, eChimps.CHIMP_TYPE_SWORDSMAN));
                    return;
                }

                if (swordsman)
                {
                    buttonViewModel.ShowMount(HasUnitLimitCapacity(localPlayerId, eChimps.CHIMP_TYPE_KNIGHT));
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

        private void HookButtonEvents(HUD_Troops troopPanel)
        {
            if (troopPanel == null)
                return;

            RebindButton(ref hookedDismountButton,
                troopPanel.FindName("ExtraFeaturesKnightDismountButton") as Button, OnDismountButtonMouseEnter);
            RebindButton(ref hookedMountButton,
                troopPanel.FindName("ExtraFeaturesKnightMountButton") as Button, OnMountButtonMouseEnter);
        }

        // Moves the tooltip handlers to the button instance the HUD currently resolves.
        private void RebindButton(ref Button hooked, Button button, MouseEventHandler onMouseEnter)
        {
            if (button == null || ReferenceEquals(button, hooked))
                return;

            UnbindButton(ref hooked, onMouseEnter);
            hooked = button;
            hooked.MouseEnter += onMouseEnter;
            hooked.MouseLeave += OnButtonMouseLeave;
        }

        private void UnbindButton(ref Button hooked, MouseEventHandler onMouseEnter)
        {
            if (hooked == null)
                return;

            hooked.MouseEnter -= onMouseEnter;
            hooked.MouseLeave -= OnButtonMouseLeave;
            hooked = null;
        }

        private void UnhookButtonEvents()
        {
            UnbindButton(ref hookedDismountButton, OnDismountButtonMouseEnter);
            UnbindButton(ref hookedMountButton, OnMountButtonMouseEnter);
            ResetTooltipMetadata();
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
                "dismount",
                eChimps.CHIMP_TYPE_SWORDSMAN);
        }

        private void ShowMountTooltip()
        {
            ShowTooltip(
                SerpLocalization.Get(SerpLocalization.KnightMountTooltip),
                SerpLocalization.Get(SerpLocalization.KnightMountTooltipBody),
                "mount",
                eChimps.CHIMP_TYPE_KNIGHT);
        }

        private void ShowTooltip(string title, string body, string label, eChimps targetUnitType)
        {
            try
            {
                MainViewModel mainViewModel = MainViewModel.Instance;
                HUD_Troops troopPanel = mainViewModel == null ? null : mainViewModel.HUDTroopPanel;
                if (mainViewModel == null || troopPanel == null)
                    return;

                mainViewModel.TroopsPanelRollover = title;
                mainViewModel.TroopsPanelRollover_AmountReq1 = string.Empty;
                int playerId = ControlledPlayer.GetId();
                if (unitLimitBridge.TryGetCapacity(playerId, targetUnitType, out int count, out int limit))
                {
                    body += Environment.NewLine +
                        SerpLocalization.Get(SerpLocalization.Limit) + ": " +
                        count.ToString(CultureInfo.CurrentCulture) + "/" +
                        limit.ToString(CultureInfo.CurrentCulture);
                }
                mainViewModel.TroopsPanelRollover_AmountGot1 = body;
                mainViewModel.TroopsPanelRollover_GoodsImage1 = null;
                UpdateTooltipMetadata(troopPanel, mainViewModel);
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
                {
                    ResetTooltipMetadata();
                    SetTroopRolloverVisibility(troopPanel, false, false);
                }
            }
            catch (Exception ex)
            {
                LogError($"Knight mount/dismount tooltip hide failed: {ex}");
            }
        }

        private void UpdateTooltipMetadata(HUD_Troops troopPanel, MainViewModel mainViewModel)
        {
            KnightTransformationTooltipMetadata metadata = KnightTransformationTooltipPolicy.Create(
                settings.KnightTransformationGoldCost,
                settings.KnightTransformationDelaySeconds);
            if (!TryResolveTooltipMetadataElements(troopPanel))
                return;

            tooltipGoldText.Text = metadata.ShowGold
                ? metadata.GoldCost.ToString(CultureInfo.CurrentCulture)
                : string.Empty;
            tooltipGoldText.Visibility = metadata.ShowGold ? Visibility.Visible : Visibility.Collapsed;

            ImageSource goldIcon = null;
            if (metadata.ShowGold)
            {
                try
                {
                    goldIcon = mainViewModel.getSmallGoodsIcon((int)eGoods.STORED_GOLD);
                }
                catch (Exception ex)
                {
                    if (!tooltipGoldIconMissingLogged)
                    {
                        tooltipGoldIconMissingLogged = true;
                        LogWarning($"Knight transformation tooltip could not resolve the Vanilla gold icon: {ex.Message}");
                    }
                }

                if (goldIcon == null && !tooltipGoldIconMissingLogged)
                {
                    tooltipGoldIconMissingLogged = true;
                    LogWarning("Knight transformation tooltip could not resolve the Vanilla gold icon.");
                }
            }

            tooltipGoldIcon.Source = goldIcon;
            tooltipGoldIcon.Visibility = metadata.ShowGold && goldIcon != null
                ? Visibility.Visible
                : Visibility.Collapsed;
            tooltipSeparatorText.Visibility = metadata.ShowSeparator ? Visibility.Visible : Visibility.Collapsed;
            tooltipDelayText.Text = metadata.ShowDelay
                ? metadata.DelaySeconds.ToString(CultureInfo.CurrentCulture) + "s"
                : string.Empty;
            tooltipDelayText.Visibility = metadata.ShowDelay ? Visibility.Visible : Visibility.Collapsed;
            tooltipMetadataHost.Visibility = metadata.ShowHost ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool TryResolveTooltipMetadataElements(HUD_Troops troopPanel)
        {
            if (troopPanel != null && ReferenceEquals(tooltipMetadataPanel, troopPanel) &&
                tooltipMetadataHost != null && tooltipGoldIcon != null && tooltipGoldText != null &&
                tooltipSeparatorText != null && tooltipDelayText != null)
            {
                return true;
            }

            ResetTooltipMetadata();
            tooltipMetadataPanel = troopPanel;
            tooltipMetadataHost = troopPanel?.FindName("ExtraFeaturesKnightTransformationTooltipMetadataHost") as StackPanel;
            tooltipGoldIcon = troopPanel?.FindName("ExtraFeaturesKnightTransformationTooltipGoldIcon") as Image;
            tooltipGoldText = troopPanel?.FindName("ExtraFeaturesKnightTransformationTooltipGoldText") as TextBlock;
            tooltipSeparatorText = troopPanel?.FindName("ExtraFeaturesKnightTransformationTooltipSeparatorText") as TextBlock;
            tooltipDelayText = troopPanel?.FindName("ExtraFeaturesKnightTransformationTooltipDelayText") as TextBlock;

            bool resolved = tooltipMetadataHost != null && tooltipGoldIcon != null && tooltipGoldText != null &&
                tooltipSeparatorText != null && tooltipDelayText != null;
            if (!resolved && !tooltipMetadataElementsMissingLogged)
            {
                tooltipMetadataElementsMissingLogged = true;
                LogWarning("Knight transformation tooltip metadata elements were not found; the normal title and body remain available.");
            }
            else if (resolved)
            {
                tooltipMetadataElementsMissingLogged = false;
            }

            return resolved;
        }

        private void ResetTooltipMetadata()
        {
            if (tooltipMetadataHost != null)
                tooltipMetadataHost.Visibility = Visibility.Collapsed;
            if (tooltipGoldIcon != null)
            {
                tooltipGoldIcon.Source = null;
                tooltipGoldIcon.Visibility = Visibility.Collapsed;
            }
            if (tooltipGoldText != null)
            {
                tooltipGoldText.Text = string.Empty;
                tooltipGoldText.Visibility = Visibility.Collapsed;
            }
            if (tooltipSeparatorText != null)
                tooltipSeparatorText.Visibility = Visibility.Collapsed;
            if (tooltipDelayText != null)
            {
                tooltipDelayText.Text = string.Empty;
                tooltipDelayText.Visibility = Visibility.Collapsed;
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

        private bool HasUnitLimitCapacity(int playerId, eChimps targetUnitType)
        {
            return !unitLimitBridge.TryGetCapacity(playerId, targetUnitType, out int count, out int limit) ||
                count < limit;
        }

        // UnitLimit notifies on every local recruit and death (mostly from the simulation thread),
        // which in large battles means many notifications per frame. One queued refresh per Unity
        // dispatch reaches the same final button state; later notifications before it runs are
        // already covered by it. On the main thread the refresh stays inline as before.
        private int unitLimitRefreshQueued;

        internal void OnUnitLimitStateChanged()
        {
            if (Shared.UnityMainThreadDispatch.IsMainThread)
            {
                RefreshButtonVisibility();
                return;
            }
            if (System.Threading.Interlocked.Exchange(ref unitLimitRefreshQueued, 1) != 0)
                return;
            if (!Shared.UnityMainThreadDispatch.TryEnqueue(RunQueuedUnitLimitRefresh))
                System.Threading.Interlocked.Exchange(ref unitLimitRefreshQueued, 0);
        }

        private void RunQueuedUnitLimitRefresh()
        {
            RefreshButtonVisibility();
        }

        // Determines both button types in one pass: first the captured selection, then (only when
        // that did not already decide the knight priority) one allocation-free pass over the
        // native unit array that tests the cheap selection mark before anything else.
        private void FindSelectedOwnKnightOrSwordsman(int localPlayerId,
            APIShared.LocalSelectionSnapshot selection, out bool knight, out bool swordsman)
        {
            knight = swordsman = false;
            if (localPlayerId < 1 || localPlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
                return;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;

            for (int i = 0; selection != null && i < selection.Count; i++)
            {
                int unitId = selection[i].UnitId;
                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                    continue;
                if (IsOwnAliveUnit(unit, localPlayerId, eChimps.CHIMP_TYPE_KNIGHT)) { knight = true; return; }
                if (IsOwnAliveUnit(unit, localPlayerId, eChimps.CHIMP_TYPE_SWORDSMAN)) swordsman = true;
            }

            Span<GameUnit> units = unitApi.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit candidate = ref units[spanIndex];
                if ((ushort)candidate.r_UnitHover == 0 || !APIShared.UnitAccess.IsReallyAlive(in candidate) ||
                    candidate.r_ControllableForPlayerId != localPlayerId)
                    continue;
                if (candidate.r_UnitChimp == eChimps.CHIMP_TYPE_KNIGHT) { knight = true; return; }
                if (candidate.r_UnitChimp == eChimps.CHIMP_TYPE_SWORDSMAN) swordsman = true;
            }
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

        private static bool IsOwnAliveUnit(GameUnit* unit, int localPlayerId, eChimps unitType)
        {
            return unit != null &&
                APIShared.UnitAccess.IsReallyAlive(unit) &&
                unit->r_UnitChimp == unitType &&
                unit->r_ControllableForPlayerId == localPlayerId;
        }

        private static bool IsSelected(GameUnit* unit)
        {
            // Vanilla's local selection output reads the low 16 bits at GameUnit +0x30.
            return unit != null && (ushort)unit->r_UnitHover != 0;
        }

        private bool TryCaptureSelectedUnits(int playerId, out APIShared.LocalSelectionSnapshot selection)
        {
            selection = null;
            try
            {
                return APIShared.LocalSelectionAPI.TryCapture(playerId, out selection);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Knight mount/dismount could not read selected units: {ex}");
                return false;
            }
        }

        private static int GetSelectionPlayerId()
        {
            if (APIShared.GameModes.GameModeHelper.IsMapEditor())
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            return GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
        }

        private void LogDebug(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            // Diagnostics are a startup snapshot; skip the closure and dispatch when they are off.
            if (!Shared.DebugLogHelper.DiagnosticsEnabled) return;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogDebugLimited(log, callerFile + ":" + callerLine, message));
        }

        private void LogError(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogErrorLimited(log, callerFile + ":" + callerLine, message));
        }

        private void LogWarning(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogWarningLimited(log, callerFile + ":" + callerLine, message));
        }
    }
}
