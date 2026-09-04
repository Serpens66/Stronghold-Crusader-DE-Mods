// Feature: Per-player Assassin climbing mode and its troop-HUD command.
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
using System.Globalization;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class AssassinClimbButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private bool wantsVisibility;
        private bool layoutAvailable = true;
        private Visibility forbiddenMarkVisibility = Visibility.Hidden;

        public AssassinClimbButtonViewModel(Action toggle, Action showTooltip, Action hideTooltip)
        {
            ToggleCommand = new RelayCommand(toggle ?? throw new ArgumentNullException(nameof(toggle)));
            MouseEnterCommand = new RelayCommand(showTooltip ?? throw new ArgumentNullException(nameof(showTooltip)));
            MouseLeaveCommand = new RelayCommand(hideTooltip ?? throw new ArgumentNullException(nameof(hideTooltip)));
        }

        public RelayCommand ToggleCommand { get; }
        public RelayCommand MouseEnterCommand { get; }
        public RelayCommand MouseLeaveCommand { get; }

        public int TroopActionLayoutVersion => 1;
        public string TroopActionId => "BugfixesAndQoL_Serp.AssassinClimb";
        public int TroopActionPriority => 200;
        public bool WantsVisibility => wantsVisibility;

        public bool LayoutAvailable
        {
            get => layoutAvailable;
            set
            {
                if (layoutAvailable == value)
                    return;
                layoutAvailable = value;
                OnPropertyChanged(nameof(LayoutAvailable));
                OnPropertyChanged(nameof(ButtonVisibility));
            }
        }

        public Visibility ButtonVisibility => wantsVisibility && layoutAvailable
            ? Visibility.Visible
            : Visibility.Hidden;

        public Visibility ForbiddenMarkVisibility
        {
            get => forbiddenMarkVisibility;
            private set
            {
                if (forbiddenMarkVisibility == value)
                    return;
                forbiddenMarkVisibility = value;
                OnPropertyChanged(nameof(ForbiddenMarkVisibility));
            }
        }

        public void Show(bool climbingAllowed)
        {
            SetWantsVisibility(true);
            ForbiddenMarkVisibility = climbingAllowed ? Visibility.Hidden : Visibility.Visible;
        }

        public void Hide()
        {
            SetWantsVisibility(false);
            ForbiddenMarkVisibility = Visibility.Hidden;
        }

        private void SetWantsVisibility(bool value)
        {
            if (wantsVisibility == value)
                return;
            wantsVisibility = value;
            OnPropertyChanged(nameof(WantsVisibility));
            OnPropertyChanged(nameof(ButtonVisibility));
        }
    }

    internal sealed unsafe class AssassinClimbRuntime : IDisposable
    {
        private const int ChoreProtocolVersion = 1;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly AssassinClimbButtonViewModel buttonViewModel;
        private readonly bool[] climbingAllowed = new bool[9];
        private readonly int[] lastOperationIds = new int[9];
        private R3PacketEventHook<AssassinClimbStatePacket> packetHook;
        private IDisposable packetSubscription;
        private bool initialized;
        private bool networkInitialized;
        private bool renderStateKnown;
        private bool renderRefreshSubscribed;
        private HUD_Troops lastRenderPanel;
        private bool lastRenderFeatureActive;
        private int lastRenderPlayerId = -1;
        private int lastRenderSelectionSignature;
        private bool lastRenderSelectedOwnAssassin;
        private bool lastRenderClimbingAllowed;
        private bool renderFailureLogged;
        private int lastRenderFrame = -1;
        private int nextOperationId;
        private Button hookedButton;
        private bool tooltipVisible;

        public AssassinClimbRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings, MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
            buttonViewModel = new AssassinClimbButtonViewModel(OnToggleCommand, ShowTooltip, HideTooltip);
            ResetPlayerStates();
        }

        public AssassinClimbButtonViewModel ButtonViewModel => buttonViewModel;

        public bool IsClimbingAllowed(int playerId)
        {
            return playerId <= 0 || playerId >= climbingAllowed.Length || climbingAllowed[playerId];
        }

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<AssassinClimbStatePacket>();
            packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
            networkInitialized = true;
            LogDebug($"Assassin climb-state synchronization registered: protocolVersion={ChoreProtocolVersion}.");
        }

        public void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            if (!renderRefreshSubscribed)
            {
                Application.onBeforeRender += OnBeforeRender;
                renderRefreshSubscribed = true;
            }
            InvalidateRenderState();
            RefreshButtonVisibility();
        }

        public void Dispose()
        {
            initialized = false;
            if (renderRefreshSubscribed)
            {
                Application.onBeforeRender -= OnBeforeRender;
                renderRefreshSubscribed = false;
            }
            InvalidateRenderState();
            HideTooltip();
            UnhookButtonEvents();
            buttonViewModel.Hide();
        }

        public void BeginMap()
        {
            ResetPlayerStates();
            InvalidateRenderState();
            RefreshButtonVisibility();
        }

        public void EndMap()
        {
            ResetPlayerStates();
            InvalidateRenderState();
            HideTooltip();
            buttonViewModel.Hide();
        }

        public void RefreshButtonVisibility()
        {
            HUD_Troops troopPanel = null;
            TryGetHudTroopPanel(out troopPanel);
            RefreshButtonVisibilityCore(troopPanel, force: true);
            Shared.TroopActionButtonLayout.Reflow(troopPanel, log);
        }

        internal void RefreshButtonVisibility(HUD_Troops troopPanel)
        {
            RefreshButtonVisibilityCore(troopPanel, force: true);
        }

        private bool RefreshButtonVisibilityCore(HUD_Troops troopPanel, bool force)
        {
            try
            {
                if (troopPanel == null)
                    TryGetHudTroopPanel(out troopPanel);

                HookButtonEvents(troopPanel);

                MainViewModel viewModel = MainViewModel.viewModelLoaded ? MainViewModel.Instance : null;
                bool featureActive = initialized && troopPanel != null &&
                    viewModel?.Show_HUD_Troops == true && IsFeatureActive();
                int playerId = featureActive ? GetControlledPlayerId() : -1;
                int selectionSignature = 0;
                bool selectedOwnAssassin = false;
                if (featureActive)
                    selectionSignature = CaptureSelectionState(playerId, out selectedOwnAssassin);
                bool climbingIsAllowed = selectedOwnAssassin && IsClimbingAllowed(playerId);
                bool changed = !renderStateKnown ||
                    !ReferenceEquals(lastRenderPanel, troopPanel) ||
                    lastRenderFeatureActive != featureActive ||
                    lastRenderPlayerId != playerId ||
                    lastRenderSelectionSignature != selectionSignature ||
                    lastRenderSelectedOwnAssassin != selectedOwnAssassin ||
                    lastRenderClimbingAllowed != climbingIsAllowed;
                renderFailureLogged = false;
                if (!force && !changed)
                    return false;

                renderStateKnown = true;
                lastRenderPanel = troopPanel;
                lastRenderFeatureActive = featureActive;
                lastRenderPlayerId = playerId;
                lastRenderSelectionSignature = selectionSignature;
                lastRenderSelectedOwnAssassin = selectedOwnAssassin;
                lastRenderClimbingAllowed = climbingIsAllowed;

                if (!featureActive || !selectedOwnAssassin)
                {
                    HideTooltip();
                    buttonViewModel.Hide();
                    return changed;
                }

                buttonViewModel.Show(climbingIsAllowed);
                if (tooltipVisible)
                    ShowTooltip();
                return changed;
            }
            catch (Exception ex)
            {
                InvalidateRenderState();
                HideTooltip();
                buttonViewModel.Hide();
                if (!renderFailureLogged)
                {
                    renderFailureLogged = true;
                    LogError($"Assassin climb button visibility refresh failed; repeated errors are suppressed until a successful refresh: {ex}");
                }
                return false;
            }
        }

        private void OnBeforeRender()
        {
            if (!initialized || lastRenderFrame == Time.frameCount)
                return;
            lastRenderFrame = Time.frameCount;

            HUD_Troops troopPanel = null;
            TryGetHudTroopPanel(out troopPanel);
            if (RefreshButtonVisibilityCore(troopPanel, force: false))
                Shared.TroopActionButtonLayout.Reflow(troopPanel, log);
        }

        private void OnToggleCommand()
        {
            try
            {
                if (!IsFeatureActive())
                    return;

                int playerId = GetControlledPlayerId();
                if (playerId <= 0 || playerId >= climbingAllowed.Length || !HasSelectedOwnAssassin(playerId))
                    return;

                bool targetState = !climbingAllowed[playerId];
                int operationId = unchecked(++nextOperationId);
                if (multiplayerFeatureGate.BlocksLocalStateChanges)
                {
                    if (!TrySendChore(playerId, operationId, targetState))
                        return;
                }
                else
                {
                    ApplyState(playerId, operationId, targetState, "local-click");
                }

                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Assassin climb-mode click failed: {ex}");
            }
        }

        private bool TrySendChore(int playerId, int operationId, bool targetState)
        {
            if (!IsChoreTransportReady())
            {
                LogError("Assassin climb-mode change refused in multiplayer because Chore transport is unavailable.");
                return false;
            }

            var packet = new AssassinClimbStatePacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                PlayerId = playerId,
                OperationId = operationId,
                AllowClimbing = targetState
            };
            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            Func<byte[], bool> send = ChoreNetworkTransport.SendRawBlob;
            bool queued = send != null && send(blob);
            if (!queued)
            {
                LogError($"Assassin climb-state Chore was not queued; no local change was applied: operationId={operationId}.");
                return false;
            }

            return true;
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<AssassinClimbStatePacket> args)
        {
            AssassinClimbStatePacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion ||
                packet.PlayerId <= 0 || packet.PlayerId >= climbingAllowed.Length || packet.OperationId <= 0)
            {
                LogError("Rejected an Assassin climb-state Chore with an invalid payload.");
                return;
            }

            ApplyState(packet.PlayerId, packet.OperationId, packet.AllowClimbing, "multiplayer-chore");
            RefreshButtonVisibility();
        }

        private void ApplyState(int playerId, int operationId, bool allowClimbing, string source)
        {
            if (operationId <= lastOperationIds[playerId])
                return;

            lastOperationIds[playerId] = operationId;
            climbingAllowed[playerId] = allowClimbing;
            LogDebug($"Assassin climbing {(allowClimbing ? "enabled" : "disabled")}: source={source}, playerId={playerId}.");
        }

        private bool IsFeatureActive()
        {
            return settings.EnableMod && settings.EnableImprovedAssassinPathfinding &&
                (!multiplayerFeatureGate.BlocksLocalStateChanges || IsChoreTransportReady());
        }

        private bool IsChoreTransportReady()
        {
            return networkInitialized && packetHook != null && ChoreNetworkTransport.IsAvailable;
        }

        private bool HasSelectedOwnAssassin(int playerId)
        {
            CaptureSelectionState(playerId, out bool selectedOwnAssassin);
            return selectedOwnAssassin;
        }

        private int CaptureSelectionState(int playerId, out bool selectedOwnAssassin)
        {
            selectedOwnAssassin = false;
            int[] selected = GamePlayerManagerAPI.Instance.GetSelectedChimps() ?? Array.Empty<int>();
            GameUnitManagerAPI api = GameUnitManagerAPI.Instance;
            int signature = 17;
            for (int index = 0; index < selected.Length; index++)
            {
                int unitId = selected[index];
                signature = unchecked((signature * 31) + unitId);
                if (unitId > 0 && api.TryGetUnitById(unitId, out GameUnit* unit) && IsOwnAssassin(unit, playerId))
                    selectedOwnAssassin = true;
            }

            int expectedSelectedCount = GameData.Instance?.lastGameState.numSelectedChimps ?? selected.Length;
            signature = unchecked((signature * 31) + expectedSelectedCount);
            // During the first editor click the managed ID list can trail the native selection
            // flags for one frame. Scan only while the game reports a non-empty selection.
            if (!selectedOwnAssassin && expectedSelectedCount > 0)
            {
                Span<GameUnit> units = api.GetUnitsAsSpan();
                for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                {
                    ref GameUnit unit = ref units[spanIndex];
                    if ((unit.r_UnitSelected != 0 || unit.r_UnitSelected2 != 0) &&
                        unit.r_AliveState == AliveState.IsAlive &&
                        unit.r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN &&
                        unit.r_ControllableForPlayerId == playerId)
                    {
                        selectedOwnAssassin = true;
                        signature = unchecked((signature * 31) + spanIndex + 1);
                        break;
                    }
                }
            }
            return signature;
        }

        private static bool IsOwnAssassin(GameUnit* unit, int playerId)
        {
            return unit != null && unit->r_AliveState == AliveState.IsAlive &&
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN && unit->r_ControllableForPlayerId == playerId;
        }

        private void HookButtonEvents(HUD_Troops troopPanel)
        {
            Button button = troopPanel?.FindName("BugfixesAndQoLAssassinClimbButton") as Button;
            if (button == null || ReferenceEquals(button, hookedButton))
                return;

            UnhookButtonEvents();
            hookedButton = button;
            hookedButton.MouseEnter += OnButtonMouseEnter;
            hookedButton.MouseLeave += OnButtonMouseLeave;
        }

        private void UnhookButtonEvents()
        {
            if (hookedButton == null)
                return;

            hookedButton.MouseEnter -= OnButtonMouseEnter;
            hookedButton.MouseLeave -= OnButtonMouseLeave;
            hookedButton = null;
        }

        private void OnButtonMouseEnter(object sender, Noesis.MouseEventArgs e)
        {
            ShowTooltip();
        }

        private void OnButtonMouseLeave(object sender, Noesis.MouseEventArgs e)
        {
            HideTooltip();
        }

        private void ShowTooltip()
        {
            try
            {
                int playerId = GetControlledPlayerId();
                MainViewModel viewModel = MainViewModel.Instance;
                HUD_Troops troopPanel = viewModel?.HUDTroopPanel;
                if (viewModel == null || troopPanel == null)
                    return;

                bool allowed = IsClimbingAllowed(playerId);
                viewModel.TroopsPanelRollover = SerpLocalization.Get(SerpLocalization.AssassinClimbingToggleTooltip);
                viewModel.TroopsPanelRollover_AmountReq1 = string.Empty;
                viewModel.TroopsPanelRollover_AmountGot1 = SerpLocalization.Get(
                    allowed ? SerpLocalization.AssassinClimbingActiveTooltipBody : SerpLocalization.AssassinClimbingForbiddenTooltipBody);
                viewModel.TroopsPanelRollover_GoodsImage1 = null;
                SetTooltipVisibility(troopPanel, true);
                tooltipVisible = true;
            }
            catch (Exception ex)
            {
                LogError($"Assassin climb tooltip show failed: {ex}");
            }
        }

        private void HideTooltip()
        {
            if (!tooltipVisible)
                return;

            tooltipVisible = false;
            try
            {
                HUD_Troops troopPanel = MainViewModel.viewModelLoaded ? MainViewModel.Instance?.HUDTroopPanel : null;
                if (troopPanel != null)
                    SetTooltipVisibility(troopPanel, false);
            }
            catch (Exception ex)
            {
                LogError($"Assassin climb tooltip hide failed: {ex}");
            }
        }

        private static void SetTooltipVisibility(HUD_Troops troopPanel, bool visible)
        {
            if (troopPanel.RefTroopsPanelRollover != null)
                troopPanel.RefTroopsPanelRollover.Visibility = Visibility.Hidden;
            if (troopPanel.RefTroopsPanelRollover2 != null)
                troopPanel.RefTroopsPanelRollover2.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        private static bool TryGetHudTroopPanel(out HUD_Troops troopPanel)
        {
            troopPanel = null;
            if (!MainViewModel.viewModelLoaded)
                return false;
            troopPanel = MainViewModel.Instance?.HUDTroopPanel;
            return troopPanel != null;
        }

        private static int GetControlledPlayerId()
        {
            if (Shared.GameModeHelper.IsMapEditor())
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return playerId > 0 ? playerId : 1;
        }

        private void ResetPlayerStates()
        {
            for (int playerId = 0; playerId < climbingAllowed.Length; playerId++)
            {
                climbingAllowed[playerId] = true;
                lastOperationIds[playerId] = 0;
            }
            nextOperationId = 0;
        }

        private void InvalidateRenderState()
        {
            renderStateKnown = false;
            lastRenderPanel = null;
            lastRenderFeatureActive = false;
            lastRenderPlayerId = -1;
            lastRenderSelectionSignature = 0;
            lastRenderSelectedOwnAssassin = false;
            lastRenderClimbingAllowed = false;
            lastRenderFrame = -1;
        }

        private void LogDebug(string message) => log.LogDebug($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogError(string message) => log.LogError($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
