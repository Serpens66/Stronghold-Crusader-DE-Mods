// Feature: Confirmed surrender through Vanilla's natural lord-death path.
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
using System.Reflection;
using Steamworks;

namespace BugfixesAndQoL
{
    internal sealed class SurrenderButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility buttonVisibility = Visibility.Collapsed;
        private bool buttonEnabled;

        internal SurrenderButtonViewModel(Action surrender)
        {
            SurrenderCommand = new RelayCommand(surrender ?? throw new ArgumentNullException(nameof(surrender)));
        }

        public RelayCommand SurrenderCommand { get; }
        public string ButtonText => SerpLocalization.Get("BugfixesAndQoL.SurrenderButton");
        public string HelpText => SerpLocalization.Get("BugfixesAndQoL.SurrenderButtonHelp");

        public Visibility ButtonVisibility
        {
            get => buttonVisibility;
            private set
            {
                if (buttonVisibility == value)
                    return;

                buttonVisibility = value;
                OnPropertyChanged(nameof(ButtonVisibility));
                OnPropertyChanged(nameof(QuitButtonWidth));
            }
        }

        public bool ButtonEnabled
        {
            get => buttonEnabled;
            private set
            {
                if (buttonEnabled == value)
                    return;

                buttonEnabled = value;
                OnPropertyChanged(nameof(ButtonEnabled));
            }
        }

        public double QuitButtonWidth => ButtonVisibility == Visibility.Visible ? 145.0 : 300.0;

        internal void SetState(bool visible, bool enabled)
        {
            ButtonVisibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ButtonEnabled = visible && enabled;
        }
    }

    internal sealed unsafe class SurrenderFeature : IDisposable
    {
        private const int ProtocolVersion = 1;
        private delegate void IngameMenuInitDelegate(HUD_IngameMenu self);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly SurrenderButtonViewModel buttonViewModel;
        private readonly HashSet<string> acceptedRequests = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<long> executedOperations = new HashSet<long>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        private Hook ingameMenuInitHook;
        private IngameMenuInitDelegate ingameMenuInitOriginal;
        private R3PacketEventHook<SurrenderRequestPacket> requestPacketHook;
        private R3PacketEventHook<SurrenderExecutionPacket> executionPacketHook;
        private IDisposable requestPacketSubscription;
        private IDisposable executionPacketSubscription;
        private int nextRequestId;
        private int nextOperationId;
        private long confirmationSequence;
        private bool initialized;
        private bool disposed;

        internal SurrenderFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            buttonViewModel = new SurrenderButtonViewModel(OnSurrenderCommand);
        }

        internal SurrenderButtonViewModel ButtonViewModel => buttonViewModel;

        internal void Initialize()
        {
            if (initialized)
                return;

            // Packet types are registered unconditionally and in a stable order on every peer.
            requestPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SurrenderRequestPacket>();
            requestPacketSubscription = requestPacketHook.GetBaseHook().Observable.Subscribe(OnRequestReceived);
            executionPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SurrenderExecutionPacket>();
            executionPacketSubscription = executionPacketHook.GetBaseHook().Observable.Subscribe(OnExecutionReceived);

            MethodInfo initMethod = typeof(HUD_IngameMenu).GetMethod(
                nameof(HUD_IngameMenu.Init),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            if (initMethod == null || initMethod.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(HUD_IngameMenu).FullName, nameof(HUD_IngameMenu.Init));

            ingameMenuInitHook = new Hook(initMethod, (IngameMenuInitDelegate)IngameMenuInitHook);
            ingameMenuInitOriginal = ingameMenuInitHook.GenerateTrampoline<IngameMenuInitDelegate>();
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(args =>
            {
                if (args.Phase == EventHookPhase.Post)
                    ResetSession("map-start");
            }));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(args =>
            {
                if (args.Phase == EventHookPhase.Post)
                    ResetSession("map-unload");
            }));

            initialized = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bugfixes and QoL surrender initialized: requestPacketId={requestPacketHook.GetPacketId()}, executionPacketId={executionPacketHook.GetPacketId()}, protocolVersion={ProtocolVersion}.");
        }

        internal void RefreshButtonState()
        {
            try
            {
                int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                SurrenderLordSnapshot lord = CaptureLord(localPlayerId);
                bool mapEditor = IsMapEditor();
                bool spectator = IsSpectator();
                bool activeMatch = IsActiveMatch();
                bool realMultiplayer = Shared.GameModeHelper.IsRealMultiplayer();
                bool visible = SurrenderPolicy.CanShowButton(
                    FeatureEnabled,
                    activeMatch,
                    mapEditor,
                    spectator,
                    lord);
                bool enabled = SurrenderPolicy.CanEnableButton(
                    visible,
                    realMultiplayer,
                    IsChoreTransportReady());
                buttonViewModel.SetState(visible, enabled);
            }
            catch (Exception ex)
            {
                buttonViewModel.SetState(false, false);
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL surrender button refresh failed closed: {ex}");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ingameMenuInitHook?.Undo();
            ingameMenuInitHook?.Dispose();
            requestPacketSubscription?.Dispose();
            executionPacketSubscription?.Dispose();
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            buttonViewModel.SetState(false, false);
        }

        private bool FeatureEnabled => settings.EnableMod && settings.EnableSurrender;

        private void IngameMenuInitHook(HUD_IngameMenu self)
        {
            ingameMenuInitOriginal(self);
            RefreshButtonState();
        }

        private void OnSurrenderCommand()
        {
            try
            {
                RefreshButtonState();
                if (buttonViewModel.ButtonVisibility != Visibility.Visible || !buttonViewModel.ButtonEnabled)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Bugfixes and QoL surrender click was rejected because the action is unavailable.");
                    return;
                }

                long sequence = ++confirmationSequence;
                MainViewModel.Instance.HUDIngameMenu.Hide();
                HUD_ConfirmationPopup.ShowConfirmationMessage(
                    SerpLocalization.Get("BugfixesAndQoL.SurrenderConfirmationTitle"),
                    () => ConfirmSurrender(sequence),
                    () => CancelSurrender(sequence),
                    SerpLocalization.Get("BugfixesAndQoL.SurrenderConfirmationMessage"));
                Shared.DebugLogHelper.LogDebug(log, "Displayed surrender confirmation.");
            }
            catch (Exception ex)
            {
                ReopenIngameMenu();
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not display the surrender confirmation: {ex}");
            }
        }

        private void ConfirmSurrender(long sequence)
        {
            if (sequence != confirmationSequence)
                return;

            try
            {
                // Close restores Vanilla's pre-menu pause state before the lord death is processed.
                MainViewModel.Instance.HUDIngameMenu.Close();
                int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                SurrenderLordSnapshot lord = CaptureLord(localPlayerId);
                bool activeMatch = IsActiveMatch();
                bool realMultiplayer = Shared.GameModeHelper.IsRealMultiplayer();
                if (!SurrenderPolicy.CanShowButton(
                    FeatureEnabled,
                    activeMatch,
                    IsMapEditor(),
                    IsSpectator(),
                    lord))
                {
                    Shared.DebugLogHelper.LogWarning(log, "Confirmed surrender was rejected because the local lord or match state changed.");
                    return;
                }

                if (!realMultiplayer)
                {
                    GameUnitManagerAPI.Instance.KillUnit(lord.UnitId);
                    Shared.DebugLogHelper.LogInfo(log, $"Singleplayer surrender executed through lord death: playerId={lord.PlayerId}, unitId={lord.UnitId}, globalId={lord.GlobalId}.");
                    return;
                }

                if (!IsChoreTransportReady())
                {
                    Shared.DebugLogHelper.LogError(log, "Multiplayer surrender was rejected because the Chore transport is unavailable; no local kill was applied.");
                    return;
                }

                if (GameNetworkAPI.IsLocalHost())
                {
                    if (!TryQueueExecution(lord))
                        Shared.DebugLogHelper.LogError(log, "Host surrender could not be queued; no local kill was applied.");
                    return;
                }

                int requestId = NextNonZero(ref nextRequestId);
                var request = new SurrenderRequestPacket
                {
                    ProtocolVersion = ProtocolVersion,
                    RequestId = requestId
                };
                GameNetworkAPI.SendPacketToPlayerId(1, request, requestPacketHook.GetPacketId());
                Shared.DebugLogHelper.LogInfo(log, $"Sent targetless surrender request to host: requestId={requestId}, localPlayerId={localPlayerId}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL confirmed surrender failed closed: {ex}");
            }
        }

        private void CancelSurrender(long sequence)
        {
            if (sequence != confirmationSequence)
                return;

            ReopenIngameMenu();
            Shared.DebugLogHelper.LogDebug(log, "Cancelled surrender and reopened the in-game menu.");
        }

        private void OnRequestReceived(ReceiveCustomPacketEventArgs<SurrenderRequestPacket> args)
        {
            SurrenderRequestPacket request = args?.Packet;
            try
            {
                if (request == null || request.ProtocolVersion != ProtocolVersion || request.RequestId == 0)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Rejected surrender request with an invalid payload.");
                    return;
                }

                if (!args.SenderSteamId.HasValue || !TryResolveHumanSender(args.SenderSteamId.Value, out int playerId))
                {
                    Shared.DebugLogHelper.LogWarning(log, "Rejected surrender request without a known authenticated human sender.");
                    return;
                }

                string requestKey = args.SenderSteamId.Value.m_SteamID + ":" + request.RequestId;
                if (acceptedRequests.Contains(requestKey))
                {
                    Shared.DebugLogHelper.LogWarning(log, $"Rejected duplicate surrender request: playerId={playerId}, requestId={request.RequestId}.");
                    return;
                }

                Platform_Multiplayer.MPGameMember member = Platform_Multiplayer.Instance?.getPlayer(playerId);
                SurrenderLordSnapshot lord = CaptureLord(playerId);
                bool accepted = SurrenderPolicy.CanAcceptRequest(
                    FeatureEnabled,
                    IsActiveMatch() && Shared.GameModeHelper.IsRealMultiplayer(),
                    GameNetworkAPI.IsLocalHost(),
                    playerId > 0,
                    IsHumanMember(member),
                    lord);
                if (!accepted)
                {
                    Shared.DebugLogHelper.LogWarning(log, $"Rejected authenticated surrender request after host validation: playerId={playerId}, requestId={request.RequestId}.");
                    return;
                }

                if (TryQueueExecution(lord))
                    acceptedRequests.Add(requestKey);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL host surrender-request handling failed closed: {ex}");
            }
        }

        private void OnExecutionReceived(ReceiveCustomPacketEventArgs<SurrenderExecutionPacket> args)
        {
            SurrenderExecutionPacket packet = args?.Packet;
            try
            {
                // Chore delivery has no Steam sender. A sender here identifies an attempted
                // non-lockstep injection and must never execute a simulation mutation.
                if (packet == null || args.SenderSteamId.HasValue)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Rejected surrender execution outside the Chore transport or with an empty payload.");
                    return;
                }

                long operationKey = ((long)packet.PlayerId << 32) | (uint)packet.OperationId;
                bool duplicate = executedOperations.Contains(operationKey);
                SurrenderLordSnapshot lord = CaptureLord(packet.PlayerId);
                if (!IsActiveMatch() ||
                    !Shared.GameModeHelper.IsRealMultiplayer() ||
                    !SurrenderPolicy.CanExecute(
                        packet.ProtocolVersion,
                        ProtocolVersion,
                        packet.PlayerId,
                        packet.OperationId,
                        packet.LordGlobalId,
                        duplicate,
                        lord))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Rejected stale, duplicate, or mismatched surrender Chore: playerId={packet.PlayerId}, operationId={packet.OperationId}, lordGlobalId={packet.LordGlobalId}.");
                    return;
                }

                int resolvedUnitId = GameUnitManagerAPI.Instance.GetByGlobalId(packet.LordGlobalId);
                if (resolvedUnitId != lord.UnitId)
                {
                    Shared.DebugLogHelper.LogWarning(log, $"Rejected surrender Chore because global-ID resolution no longer matches the current lord slot: playerId={packet.PlayerId}, operationId={packet.OperationId}, expectedUnitId={lord.UnitId}, resolvedUnitId={resolvedUnitId}.");
                    return;
                }

                executedOperations.Add(operationKey);
                GameUnitManagerAPI.Instance.KillUnit(resolvedUnitId);
                Shared.DebugLogHelper.LogInfo(log, $"Surrender Chore executed: playerId={packet.PlayerId}, operationId={packet.OperationId}, unitId={resolvedUnitId}, globalId={packet.LordGlobalId}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL surrender Chore failed closed: {ex}");
            }
        }

        private bool TryQueueExecution(SurrenderLordSnapshot lord)
        {
            if (!GameNetworkAPI.IsLocalHost() ||
                !FeatureEnabled ||
                !IsActiveMatch() ||
                !Shared.GameModeHelper.IsRealMultiplayer() ||
                !IsChoreTransportReady() ||
                !SurrenderPolicy.IsValidLord(lord))
            {
                return false;
            }

            int operationId = NextNonZero(ref nextOperationId);
            var packet = new SurrenderExecutionPacket
            {
                ProtocolVersion = ProtocolVersion,
                PlayerId = lord.PlayerId,
                OperationId = operationId,
                LordGlobalId = lord.GlobalId
            };
            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(executionPacketHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);

            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            bool queued = sendRawBlob != null && sendRawBlob(blob);
            if (!queued)
            {
                Shared.DebugLogHelper.LogError(log, $"Surrender Chore was not queued; no local kill was applied: playerId={lord.PlayerId}, operationId={operationId}, payloadBytes={blob.Length}.");
                return false;
            }

            Shared.DebugLogHelper.LogInfo(log, $"Surrender Chore queued: playerId={lord.PlayerId}, operationId={operationId}, lordGlobalId={lord.GlobalId}, payloadBytes={blob.Length}.");
            return true;
        }

        private SurrenderLordSnapshot CaptureLord(int playerId)
        {
            if (playerId < 1 || playerId > 8)
                return default(SurrenderLordSnapshot);

            int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
                return new SurrenderLordSnapshot(playerId, unitId, -1, -1, false);

            return new SurrenderLordSnapshot(
                playerId,
                unitId,
                (int)unit->r_GlobalId,
                unit->r_ControllableForPlayerId,
                unit->r_AliveState == AliveState.IsAlive && unit->r_CurrentHealth > 0);
        }

        private bool IsActiveMatch()
        {
            return FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
                Director.instance != null &&
                Director.instance.SimRunning &&
                GameData.Instance != null &&
                GameData.Instance.lastGameState != null;
        }

        private static bool IsMapEditor()
        {
            return GamePlayerManagerAPI.Instance.IsInMapEditor() ||
                (MainViewModel.Instance != null && MainViewModel.Instance.IsMapEditorMode);
        }

        private static bool IsSpectator() =>
            GameData.Instance?.lastGameState != null && GameData.Instance.lastGameState.spectatorMode != 0;

        private bool IsChoreTransportReady() =>
            initialized && executionPacketHook != null && ChoreNetworkTransport.IsAvailable;

        private static bool IsHumanMember(Platform_Multiplayer.MPGameMember member) =>
            member != null &&
            member.playerID >= 1 && member.playerID <= 8 &&
            !member.kicked &&
            !member.skirmishAI &&
            member.steamID > 1000;

        private static bool TryResolveHumanSender(CSteamID sender, out int playerId)
        {
            playerId = GameNetworkAPI.GetPlayerIdForSteamId(sender);
            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            if (playerId <= 0 && multiplayer?.gameMembers != null)
            {
                foreach (Platform_Multiplayer.MPGameMember member in multiplayer.gameMembers)
                {
                    if (member != null && member.steamID == sender.m_SteamID)
                    {
                        playerId = member.playerID;
                        break;
                    }
                }
            }

            return IsHumanMember(multiplayer?.getPlayer(playerId));
        }

        private static int NextNonZero(ref int value)
        {
            value = unchecked(value + 1);
            if (value == 0)
                value = 1;
            return value;
        }

        private void ResetSession(string reason)
        {
            acceptedRequests.Clear();
            executedOperations.Clear();
            nextRequestId = 0;
            nextOperationId = 0;
            confirmationSequence++;
            buttonViewModel.SetState(false, false);
            Shared.DebugLogHelper.LogDebug(log, $"Reset surrender session state: reason={reason}.");
        }

        private static void ReopenIngameMenu()
        {
            if (MainViewModel.Instance != null)
                MainViewModel.Instance.Show_HUD_IngameMenu = true;
        }
    }
}
