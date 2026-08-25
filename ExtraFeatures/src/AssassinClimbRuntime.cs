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

namespace ExtraFeatures
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
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly AssassinClimbButtonViewModel buttonViewModel;
        private readonly bool[] climbingAllowed = new bool[9];
        private readonly int[] lastOperationIds = new int[9];
        private R3PacketEventHook<AssassinClimbStatePacket> packetHook;
        private IDisposable packetSubscription;
        private bool initialized;
        private bool networkInitialized;
        private int nextOperationId;

        public AssassinClimbRuntime(ManualLogSource log, ExtraFeaturesViewModel settings, MultiplayerFeatureGate multiplayerFeatureGate)
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
            LogInfo($"Assassin climb-state Chore registered eagerly: packetId={packetHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
        }

        public void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            buttonViewModel.Hide();
        }

        public void Dispose()
        {
            initialized = false;
            buttonViewModel.Hide();
        }

        public void BeginMap()
        {
            ResetPlayerStates();
            RefreshButtonVisibility();
        }

        public void EndMap()
        {
            ResetPlayerStates();
            buttonViewModel.Hide();
        }

        public void RefreshButtonVisibility()
        {
            HUD_Troops troopPanel = null;
            TryGetHudTroopPanel(out troopPanel);
            RefreshButtonVisibility(troopPanel);
            Shared.TroopActionButtonLayout.Reflow(troopPanel, log);
        }

        internal void RefreshButtonVisibility(HUD_Troops troopPanel)
        {
            try
            {
                if (!IsFeatureActive() || (troopPanel == null && !TryGetHudTroopPanel(out troopPanel)))
                {
                    buttonViewModel.Hide();
                    return;
                }

                int playerId = GetControlledPlayerId();
                if (!HasSelectedOwnAssassin(playerId))
                {
                    buttonViewModel.Hide();
                    return;
                }

                buttonViewModel.Show(IsClimbingAllowed(playerId));
            }
            catch (Exception ex)
            {
                buttonViewModel.Hide();
                LogError($"Assassin climb button visibility refresh failed: {ex}");
            }
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

            LogInfo($"Assassin climb-state Chore queued: playerId={playerId}, operationId={operationId}, allowClimbing={targetState}.");
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
            LogInfo($"Assassin climb state applied: source={source}, playerId={playerId}, operationId={operationId}, allowClimbing={allowClimbing}.");
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
            int[] selected = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            GameUnitManagerAPI api = GameUnitManagerAPI.Instance;
            for (int index = 0; index < selected.Length; index++)
            {
                if (selected[index] > 0 && api.TryGetUnitById(selected[index], out GameUnit* unit) && IsOwnAssassin(unit, playerId))
                    return true;
            }

            Span<GameUnit> units = api.GetUnitsAsSpan();
            for (int index = 0; index < units.Length; index++)
            {
                ref GameUnit unit = ref units[index];
                if ((unit.r_UnitSelected != 0 || unit.r_UnitSelected2 != 0) &&
                    unit.r_AliveState == AliveState.IsAlive &&
                    unit.r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN &&
                    unit.r_ControllableForPlayerId == playerId)
                    return true;
            }
            return false;
        }

        private static bool IsOwnAssassin(GameUnit* unit, int playerId)
        {
            return unit != null && unit->r_AliveState == AliveState.IsAlive &&
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN && unit->r_ControllableForPlayerId == playerId;
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
                viewModel.TroopsPanelRollover = SerpLocalization.Get(
                    allowed ? SerpLocalization.AssassinClimbingDisableTooltip : SerpLocalization.AssassinClimbingEnableTooltip);
                viewModel.TroopsPanelRollover_AmountReq1 = string.Empty;
                viewModel.TroopsPanelRollover_AmountGot1 = SerpLocalization.Get(
                    allowed ? SerpLocalization.AssassinClimbingDisableTooltipBody : SerpLocalization.AssassinClimbingEnableTooltipBody);
                viewModel.TroopsPanelRollover_GoodsImage1 = null;
                SetTooltipVisibility(troopPanel, true);
            }
            catch (Exception ex)
            {
                LogError($"Assassin climb tooltip show failed: {ex}");
            }
        }

        private void HideTooltip()
        {
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

        private void LogInfo(string message) => log.LogInfo($"[{TimestampNow()}] Extra Features {message}");
        private void LogError(string message) => log.LogError($"[{TimestampNow()}] Extra Features {message}");
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
