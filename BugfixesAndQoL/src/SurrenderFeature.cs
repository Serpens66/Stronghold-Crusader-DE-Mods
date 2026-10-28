using APIShared.GameModes;
// Confirmed surrender. Spectator presentation is owned by Spectator_Serp.
using APIShared;
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
using System.Threading;
using Steamworks;

using APIShared.Presentation;

namespace BugfixesAndQoL
{
    internal sealed class SurrenderViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility visibility = Visibility.Collapsed;
        private bool enabled;
        public string SurrenderButtonText => SerpLocalization.Get("BugfixesAndQoL.SurrenderButton");
        public Visibility SurrenderButtonVisibility => visibility;
        public bool SurrenderButtonEnabled => enabled;
        internal void SetMenuState(bool visible, bool available)
        {
            var next = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visibility != next) { visibility = next; OnPropertyChanged(nameof(SurrenderButtonVisibility)); }
            bool nextEnabled = visible && available;
            if (enabled != nextEnabled) { enabled = nextEnabled; OnPropertyChanged(nameof(SurrenderButtonEnabled)); }
        }
        internal void RefreshText() { OnPropertyChanged(nameof(SurrenderButtonText)); }
    }
    internal sealed unsafe class SurrenderFeature : IDisposable
    {
        private const int RequestProtocolVersion = 1;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerLobbyReturnFeature lobbyReturnFeature;
        private readonly SurrenderViewModel buttonViewModel;
        private IngameMenuActionRegistration menuRegistration;
        private int lastMenuGameOverState;
        private readonly HashSet<string> acceptedRequests = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private R3PacketEventHook<SurrenderRequestPacket> requestPacketHook;
        private R3PacketEventHook<SurrenderExecutionPacket> executionPacketHook;
        private IDisposable requestPacketSubscription, executionPacketSubscription;
        private int nextRequestId, lastFrame = -1;
        private long confirmationSequence, confirmationOpenedFromMenuSequence, presentationGeneration;
        private bool initialized, disposed;
        internal SurrenderFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            lobbyReturnFeature = new MultiplayerLobbyReturnFeature(log, settings);
            buttonViewModel = new SurrenderViewModel();
        }
        private bool FeatureEnabled => !disposed && settings.EnableMod && settings.EnableSurrenderAndStatistics;
        internal void Initialize()
        {
            if (initialized) return;
            requestPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SurrenderRequestPacket>();
            requestPacketSubscription = requestPacketHook.GetBaseHook().Observable.Subscribe(OnRequestReceived);
            executionPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SurrenderExecutionPacket>();
            executionPacketSubscription = executionPacketHook.GetBaseHook().Observable.Subscribe(OnExecutionReceived);
            if (!IngameMenuActions.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "surrender",
                () => { RefreshButtonState(); return new IngameMenuActionState(buttonViewModel.SurrenderButtonText,
                    buttonViewModel.SurrenderButtonVisibility == Visibility.Visible, buttonViewModel.SurrenderButtonEnabled); },
                OnSurrenderCommand, new IngameMenuActionOptions(IngameMenuRow.QuitMission, IngameMenuSlot.Left,
                    replaceExisting: true), out menuRegistration, out string menuReason)) throw new InvalidOperationException(menuReason);
            settings.PropertyChanged += (sender, args) => {
                if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(settings.EnableMod) ||
                    args.PropertyName == nameof(settings.EnableSurrenderAndStatistics)) menuRegistration.Invalidate();
            };
            subscriptions.Add(PlayerPerspectiveAPI.SubscribeChanges(BugfixesAndQoLPlugin.PluginGuid,
                "surrender-menu", _ => menuRegistration.Invalidate()));
            lobbyReturnFeature.Initialize();
            try { RegisterEndPresentation(); }
            catch (Exception error)
            {
                lobbyReturnFeature.Dispose();
                Shared.DebugLogHelper.LogErrorLimited(log, "lobby-presentation-contract",
                    "Post-game lobby integration unavailable; surrender remains active: " + error);
            }
            subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(log, _ => ResetSession("start")));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(_ => ResetSession("end")));
            UnityEngine.Application.onBeforeRender += OnBeforeRender;
            initialized = true;
            menuRegistration.Invalidate();
        }
        private void RegisterEndPresentation()
        {
            if (!MissionOverEvents.TryRegister(MissionOverOperation.Button, BugfixesAndQoLPlugin.PluginGuid, "lobby-exit",
                args => {
                    if (disposed || args.Button != "Exit" || !lobbyReturnFeature.CanHandleMissionOverExit) return;
                    long generation = presentationGeneration;
                    HUD_MissionOver view = args.View;
                    Func<bool> isCurrent = () => !disposed && generation == presentationGeneration &&
                        MainViewModel.viewModelLoaded && ReferenceEquals(MainViewModel.Instance.HUDMissionOver, view) && view.IsVisible;
                    args.DeferOriginal(continuation => {
                        if (!lobbyReturnFeature.TryHandleMissionOverExit(() => continuation.TryInvoke(), isCurrent)) continuation.TryInvoke();
                    }, isCurrent);
                }, null, out string reason)) throw new InvalidOperationException(reason);
            if (!MissionOverEvents.TryRegister(MissionOverOperation.State, BugfixesAndQoLPlugin.PluginGuid, "lobby-game-over",
                null, null, out reason, accepted: args => {
                    if (!disposed) lobbyReturnFeature.OnGameOverState(args.Result);
                    if (lastMenuGameOverState != args.Result)
                    { lastMenuGameOverState = args.Result; menuRegistration.Invalidate(); }
                }))
                throw new InvalidOperationException(reason);
            if (!MissionOverEvents.TryRegister(MissionOverOperation.Text, BugfixesAndQoLPlugin.PluginGuid, "lobby-game-over-text",
                null, args => { if (!disposed && args.OriginalCompleted) lobbyReturnFeature.OnGameOverPresentation(); }, out reason))
                throw new InvalidOperationException(reason);
        }
        internal void RefreshButtonState()
        {
            if (!MainViewModel.viewModelLoaded) return;
            try
            {
                int player = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                bool visible = SurrenderPolicy.CanShowButton(FeatureEnabled, IsActiveMatch(), IsMapEditor(), IsStartSpectator(), CaptureLord(player));
                buttonViewModel.SetMenuState(visible, SurrenderPolicy.CanEnableButton(visible, GameModeHelper.IsRealMultiplayer(), IsChoreTransportReady()));
                buttonViewModel.RefreshText();
            }
            catch (Exception error) { buttonViewModel.SetMenuState(false, false); Shared.DebugLogHelper.LogErrorLimited(log, "surrender-menu", "Surrender menu unavailable: " + error); }
        }
        private void OnBeforeRender()
        {
            if (disposed || lastFrame == UnityEngine.Time.frameCount) return;
            lastFrame = UnityEngine.Time.frameCount;
            lobbyReturnFeature.OnBeforeRender();
        }
        public void Dispose()
        {
            disposed = true; presentationGeneration++;
            requestPacketSubscription?.Dispose(); executionPacketSubscription?.Dispose();
            lobbyReturnFeature.Dispose(); UnityEngine.Application.onBeforeRender -= OnBeforeRender;
            foreach (IDisposable subscription in subscriptions) subscription.Dispose();
            subscriptions.Clear(); buttonViewModel.SetMenuState(false, false);
            menuRegistration?.Invalidate();
        }
        private void ResetSession(string reason)
        {
            presentationGeneration++; acceptedRequests.Clear(); nextRequestId = 0;
            confirmationSequence++; confirmationOpenedFromMenuSequence = 0;
            menuRegistration?.Invalidate();
            Shared.UnityMainThreadDispatch.TryEnqueue(() => { if (!disposed) buttonViewModel.SetMenuState(false, false); });
        }

        private void OnSurrenderCommand()
        {
            try
            {
                RefreshButtonState();
                if (buttonViewModel.SurrenderButtonVisibility != Visibility.Visible || !buttonViewModel.SurrenderButtonEnabled)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:750", "Bugfixes and QoL surrender click was rejected because the action is unavailable.");
                    return;
                }

                ShowSurrenderConfirmation(openedFromIngameMenu: true);
            }
            catch (Exception ex)
            {
                ReopenIngameMenu();
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:759", $"Bugfixes and QoL could not display the surrender confirmation: {ex}");
            }
        }

        internal bool TryRequestSurrenderFromLordHud()
        {
            try
            {
                RefreshButtonState();
                if (buttonViewModel.SurrenderButtonVisibility != Visibility.Visible ||
                    !buttonViewModel.SurrenderButtonEnabled)
                {
                    return false;
                }

                ShowSurrenderConfirmation(openedFromIngameMenu: false);
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:779", $"Bugfixes and QoL could not display the Lord-disband surrender confirmation: {ex}");
                return false;
            }
        }

        private void ShowSurrenderConfirmation(bool openedFromIngameMenu)
        {
            long sequence = ++confirmationSequence;
            confirmationOpenedFromMenuSequence = openedFromIngameMenu ? sequence : 0;
            if (openedFromIngameMenu)
                MainViewModel.Instance.HUDIngameMenu.Hide();

            HUD_ConfirmationPopup.ShowConfirmationMessage(
                SerpLocalization.Get("BugfixesAndQoL.SurrenderConfirmationTitle"),
                () => ConfirmSurrender(sequence),
                () => CancelSurrender(sequence),
                SerpLocalization.Get("BugfixesAndQoL.SurrenderConfirmationMessage"));
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:798", $"Displayed surrender confirmation: source={(openedFromIngameMenu ? "ingame-menu" : "lord-disband")}, sequence={sequence}.");
        }

        private void ConfirmSurrender(long sequence)
        {
            if (sequence != confirmationSequence)
                return;

            try
            {
                // Only the menu button changes Vanilla's pause state; the Lord HUD stays in live play.
                if (confirmationOpenedFromMenuSequence == sequence)
                    MainViewModel.Instance.HUDIngameMenu.Close();
                confirmationOpenedFromMenuSequence = 0;
                int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                SurrenderLordSnapshot lord = CaptureLord(localPlayerId);
                bool activeMatch = IsActiveMatch();
                bool realMultiplayer = APIShared.GameModes.GameModeHelper.IsRealMultiplayer();
                if (!SurrenderPolicy.CanShowButton(
                    FeatureEnabled,
                    activeMatch,
                    IsMapEditor(),
                    IsStartSpectator(),
                    lord))
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1535", "Confirmed surrender was rejected because the local lord or match state changed.");
                    return;
                }

                if (!realMultiplayer)
                {
                    if (APIShared.UnitAccess.TryGetById(lord.UnitId, out _, out _)) GameUnitManagerAPI.Instance.KillUnit(lord.UnitId);
                    ClearSurrenderLordSelection(lord.PlayerId, lord.UnitId, lord.GlobalId);
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1543", $"Singleplayer surrender executed through lord death: playerId={lord.PlayerId}, unitId={lord.UnitId}, globalId={lord.GlobalId}.");
                    return;
                }

                if (!IsChoreTransportReady())
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1549", "Multiplayer surrender was rejected because the Chore transport is unavailable; no local kill was applied.");
                    return;
                }

                if (GameNetworkAPI.IsLocalHost())
                {
                    if (!TryQueueExecution(lord))
                        Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1556", "Host surrender could not be queued; no local kill was applied.");
                    return;
                }

                int requestId = NextNonZero(ref nextRequestId);
                var request = new SurrenderRequestPacket
                {
                    ProtocolVersion = RequestProtocolVersion,
                    RequestId = requestId
                };
                GameNetworkAPI.SendPacketToPlayerId(1, request, requestPacketHook.GetPacketId());
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1567", $"Sent targetless surrender request to host: requestId={requestId}, localPlayerId={localPlayerId}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1571", $"Bugfixes and QoL confirmed surrender failed closed: {ex}");
            }
        }

        private void CancelSurrender(long sequence)
        {
            if (sequence != confirmationSequence)
                return;

            bool reopenMenu = confirmationOpenedFromMenuSequence == sequence;
            confirmationOpenedFromMenuSequence = 0;
            if (reopenMenu)
                ReopenIngameMenu();
            Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1584", reopenMenu
                    ? "Cancelled surrender and reopened the in-game menu."
                    : "Cancelled surrender from the Lord troop HUD.");
        }

        private void OnRequestReceived(ReceiveCustomPacketEventArgs<SurrenderRequestPacket> args)
        {
            SurrenderRequestPacket source = args?.Packet;
            if (source == null || !args.SenderSteamId.HasValue)
                return;
            var request = new SurrenderRequestPacket
            {
                ProtocolVersion = source.ProtocolVersion,
                RequestId = source.RequestId
            };
            ulong senderSteamId = args.SenderSteamId.Value.m_SteamID;
            long sessionSnapshot = Interlocked.Read(ref presentationGeneration);
            Shared.UnityMainThreadDispatch.TryEnqueue(() => {
                if (!disposed && sessionSnapshot == Interlocked.Read(ref presentationGeneration))
                    ProcessRequest(request, new CSteamID(senderSteamId));
            });
        }

        private void ProcessRequest(SurrenderRequestPacket request, CSteamID senderSteamId)
        {
            try
            {
                if (request == null || request.ProtocolVersion != RequestProtocolVersion || request.RequestId == 0)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1612", "Rejected surrender request with an invalid payload.");
                    return;
                }

                if (!TryResolveHumanSender(senderSteamId, out int playerId))
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1618", "Rejected surrender request without a known authenticated human sender.");
                    return;
                }

                string requestKey = senderSteamId.m_SteamID + ":" + request.RequestId;
                if (acceptedRequests.Contains(requestKey))
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1625", $"Rejected duplicate surrender request: playerId={playerId}, requestId={request.RequestId}.");
                    return;
                }

                Platform_Multiplayer.MPGameMember member = Platform_Multiplayer.Instance?.getPlayer(playerId);
                SurrenderLordSnapshot lord = CaptureLord(playerId);
                bool accepted = SurrenderPolicy.CanAcceptRequest(
                    FeatureEnabled,
                    IsActiveMatch() && APIShared.GameModes.GameModeHelper.IsRealMultiplayer(),
                    GameNetworkAPI.IsLocalHost(),
                    playerId > 0,
                    IsHumanMember(member),
                    lord);
                if (!accepted)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1640", $"Rejected authenticated surrender request after host validation: playerId={playerId}, requestId={request.RequestId}.");
                    return;
                }

                if (TryQueueExecution(lord))
                    acceptedRequests.Add(requestKey);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1649", $"Bugfixes and QoL host surrender-request handling failed closed: {ex}");
            }
        }

        private void OnExecutionReceived(ReceiveCustomPacketEventArgs<SurrenderExecutionPacket> args)
        {
            SurrenderExecutionPacket packet = args?.Packet;
            try
            {
                // Chore delivery has no Steam sender. A sender here identifies an attempted
                // non-lockstep injection and must never execute a simulation mutation.
                if (packet == null || !SurrenderPolicy.IsChoreDelivery(args.SenderSteamId.HasValue))
                {
                    LogPacketWarning("Rejected surrender execution outside the Chore transport or with an empty payload.");
                    return;
                }

                SurrenderLordSnapshot lord = CaptureLord(packet.PlayerId);
                byte[] decodedBody = GameNetworkAPI.Serialize(packet);
                string decodedBodyHex = ToCompactHex(decodedBody);
                int resolvedUnitId = lord.GlobalId > 0
                    ? GameUnitManagerAPI.Instance.GetByGlobalId(lord.GlobalId)
                    : -1;
                if (!IsActiveMatch() ||
                    !APIShared.GameModes.GameModeHelper.IsRealMultiplayer() ||
                    !SurrenderPolicy.CanExecute(
                        packet.PlayerId,
                        lord,
                        resolvedUnitId))
                {
                    LogPacketWarning(
                        $"Rejected stale or mismatched surrender Chore: " +
                        $"playerId={packet.PlayerId}, decodedBodyHex={decodedBodyHex}, " +
                        $"currentLordUnitId={lord.UnitId}, currentLordGlobalId={lord.GlobalId}, " +
                        $"currentLordOwner={lord.OwnerPlayerId}, currentLordAlive={lord.IsAlive}, " +
                        $"resolvedUnitId={resolvedUnitId}.");
                    return;
                }

                if (APIShared.UnitAccess.TryGetById(resolvedUnitId, out _, out _)) GameUnitManagerAPI.Instance.KillUnit(resolvedUnitId);
                ClearSurrenderLordSelection(packet.PlayerId, resolvedUnitId, lord.GlobalId);
                LogPacketInfo(
                    $"Surrender Chore executed: playerId={packet.PlayerId}, unitId={resolvedUnitId}, " +
                    $"locallyResolvedGlobalId={lord.GlobalId}, decodedBodyHex={decodedBodyHex}.");
            }
            catch (Exception ex)
            {
                LogPacketError($"Bugfixes and QoL surrender Chore failed closed: {ex}");
            }
        }

        private void LogPacketInfo(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1702", message));

        private void LogPacketWarning(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1706", message));

        private void LogPacketError(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1710", message));

        private bool TryQueueExecution(SurrenderLordSnapshot lord)
        {
            if (!GameNetworkAPI.IsLocalHost() ||
                !FeatureEnabled ||
                !IsActiveMatch() ||
                !APIShared.GameModes.GameModeHelper.IsRealMultiplayer() ||
                !IsChoreTransportReady() ||
                !SurrenderPolicy.IsValidLord(lord))
            {
                return false;
            }

            // Only the stable player slot crosses the currently unreliable Chore payload.
            // Every peer resolves and validates that player's living Lord in the execution tick.
            var packet = new SurrenderExecutionPacket
            {
                PlayerId = lord.PlayerId
            };
            short packetId = executionPacketHook?.GetPacketId() ?? (short)0;
            if (!APIShared.Networking.ChoreTransport.TrySend(
                    packet,
                    packetId,
                    initialized && executionPacketHook != null,
                    out byte[] body,
                    out string rejectionReason))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1738", $"Surrender Chore was not queued; no local kill was applied: playerId={lord.PlayerId}, reason={rejectionReason}.");
                return false;
            }

            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Surrender Chore queued: playerId={lord.PlayerId}, locallyValidatedLordGlobalId={lord.GlobalId}, " +
                    $"bodyBytes={body.Length}, payloadBytes={blob.Length}, " +
                    $"bodyHex={ToCompactHex(body)}, blobHex={ToCompactHex(blob)}.");
            return true;
        }

        private static string ToCompactHex(byte[] bytes) =>
            bytes == null ? "<null>" : BitConverter.ToString(bytes).Replace("-", string.Empty);

        private void ClearSurrenderLordSelection(int playerId, int unitId, int globalId)
        {
            try
            {
                GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
                if (!APIShared.UnitAccess.TryGetById(units, unitId, out GameUnit* unit, out _) || unit == null)
                {
                    LogPacketWarning($"Surrender selection cleanup could not resolve Lord slot: playerId={playerId}, unitId={unitId}, globalId={globalId}.");
                    return;
                }

                GameUnitManager* manager = units.GetUnitManager().Pointer;
                if (manager == null || playerId < 1 || playerId > 8)
                {
                    LogPacketWarning($"Surrender selection cleanup could not resolve selection count: playerId={playerId}, unitId={unitId}, globalId={globalId}.");
                    return;
                }

                SurrenderSelectionCleanupResult result = SurrenderSelectionCleanup.Clear(
                    unit, &manager->r_SelectedChimpsCount[playerId], globalId, playerId);
                string detail = $"playerId={playerId}, unitId={unitId}, globalId={globalId}, " +
                    $"status={result.Status}, selectedBefore={result.SelectedBefore}, " +
                    $"selectedAfter={unit->r_UnitSelected}, countBefore={result.CountBefore}, countAfter={result.CountAfter}";
                if (result.Status == SurrenderSelectionCleanupStatus.IdentityChanged ||
                    result.Status == SurrenderSelectionCleanupStatus.UnexpectedSelector ||
                    result.Status == SurrenderSelectionCleanupStatus.ClearedWithEmptyCount)
                    LogPacketWarning("Surrender selection cleanup anomaly: " + detail);
                else
                    LogPacketInfo("Surrender selection cleanup: " + detail);

                if (result.Status == SurrenderSelectionCleanupStatus.IdentityChanged ||
                    result.Status == SurrenderSelectionCleanupStatus.UnexpectedSelector ||
                    GamePlayerManagerAPI.Instance.GetLocalPlayerId() != playerId)
                    return;

                // The native record is corrected synchronously above. The extender's
                // normal selection API updates the local HUD on later input phases.
                if (!GamePlayerManagerAPI.Instance.SetSelectedChimps(new List<int>()))
                    LogPacketWarning($"Local Surrender HUD deselection was not queued: playerId={playerId}, unitId={unitId}.");
            }
            catch (Exception ex)
            {
                LogPacketError($"Surrender selection cleanup failed: playerId={playerId}, unitId={unitId}, globalId={globalId}, error={ex}");
            }
        }

        private SurrenderLordSnapshot CaptureLord(int playerId)
        {
            if (playerId < 1 || playerId > 8)
                return default(SurrenderLordSnapshot);

            int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null)
                return new SurrenderLordSnapshot(playerId, unitId, -1, -1, false);

            return new SurrenderLordSnapshot(
                playerId,
                unitId,
                (int)unit->r_GlobalId,
                unit->r_ControllableForPlayerId,
                APIShared.UnitAccess.IsReallyAlive(unit) &&
                    unit->r_UnitChimp == eChimps.CHIMP_TYPE_LORD &&
                    unit->r_CurrentHealth > 0);
        }

        private bool IsActiveMatch()
        {
            return FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
                Director.instance != null &&
                Director.instance.SimRunning &&
                GameData.Instance != null &&
                GameData.Instance.lastGameState != null;
        }

        private static bool IsMapEditor() => APIShared.GameModes.GameModeHelper.IsMapEditor();

        private static bool IsStartSpectator() =>
            GameData.Instance?.lastGameState != null && GameData.Instance.lastGameState.spectatorMode != 0;

        private bool IsChoreTransportReady() =>
            APIShared.Networking.ChoreTransport.IsAvailable(
                initialized && executionPacketHook != null);

        private static bool IsHumanMember(Platform_Multiplayer.MPGameMember member) =>
            member != null &&
            member.playerID >= 1 && member.playerID <= 8 &&
            !member.kicked &&
            !member.skirmishAI &&
            member.steamID > 1000;

        private bool TryResolveHumanSender(CSteamID sender, out int playerId)
        {
            Shared.PlayerIdentityResolution identity =
                Shared.PlayerIdentityHelper.CapturePlayerIdForSteamId(
                    sender.m_SteamID,
                    preferInGameRoster: true);
            playerId = identity.PlayerId;
            if (!identity.IsResolved)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1872", $"Surrender sender identity resolution failed closed: steamId={sender.m_SteamID}, error={identity.Error}");
                return false;
            }
            if (!string.IsNullOrEmpty(identity.Diagnostic))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1879", $"Surrender sender identity source mismatch: {identity.Diagnostic}");
            }

            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPGameMember member = multiplayer?.getPlayer(playerId);
            if (!IsHumanMember(member) || member.steamID != sender.m_SteamID)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SurrenderFeature.cs:1888", $"Surrender sender identity did not resolve back to the authenticated human: " +
                    $"steamId={sender.m_SteamID}, playerId={playerId}.");
                playerId = 0;
                return false;
            }
            return true;
        }

        private static int NextNonZero(ref int value)
        {
            value = unchecked(value + 1);
            if (value == 0)
                value = 1;
            return value;
        }

        private static void ReopenIngameMenu()
        {
            if (MainViewModel.Instance != null)
                MainViewModel.Instance.Show_HUD_IngameMenu = true;
        }    }
}
