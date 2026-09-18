using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop.Enums;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using UnityEngine;

namespace CastlePlanner
{
    internal sealed class FreeCastlePreviewRuntime : INotifyPropertyChanged
    {
        private delegate int GameActionDelegate(
            Enums.GameActionCommand command,
            int structureId,
            int state,
            int value2);
        private delegate void LeaveLobbyDelegate(
            Platform_Multiplayer self,
            bool preserveGameMembers);
        private delegate void StartGameDelegate(
            Platform_Multiplayer self,
            EngineInterface.MultiplayerSetupData setup,
            FileHeader map,
            int coopTrailId,
            int coopMissionId);

        private enum PreviewState
        {
            Inactive,
            AwaitingGameplay,
            Loading,
            Selecting,
            Distributing,
            Aborting,
            RestartCommitted,
            SpawnMap
        }

        private sealed class QueuedPacket
        {
            public QueuedPacket(FreeCastlePacket packet, ulong senderSteamId)
            {
                Packet = packet;
                SenderSteamId = senderSteamId;
            }

            public FreeCastlePacket Packet { get; }
            public ulong SenderSteamId { get; }
        }

        private const int TimeoutSeconds = 120;
        private readonly ManualLogSource log;
        private readonly CastlePlannerSettingsViewModel settings;
        private readonly HashSet<int> roster = new HashSet<int>();
        private readonly HashSet<int> readyPlayers = new HashSet<int>();
        private readonly Dictionary<int, FreeCastleSelection> decisions =
            new Dictionary<int, FreeCastleSelection>();
        private readonly HashSet<int> noneDecisions = new HashSet<int>();
        private readonly Dictionary<int, IncomingTransfer> incoming =
            new Dictionary<int, IncomingTransfer>();
        private readonly HashSet<ulong> manifestAcks = new HashSet<ulong>();
        private readonly BulkObservableCollection<string> castleChoices =
            new BulkObservableCollection<string>();
        private readonly CastleRotationOptions rotationOptions;
        private readonly ObservableCollection<string> rotations;

        private R3PacketEventHook<FreeCastlePacket> packetHook;
        private CoalescedSynchronizationContextQueue<QueuedPacket> packetDispatchQueue;
        private SynchronizationContext unitySynchronizationContext;
        private IDisposable packetSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private Hook gameActionHook;
        private Hook leaveLobbyHook;
        private Hook startGameHook;
        private MethodInfo initFastMethod;
        private GameActionDelegate gameActionTrampoline;
        private LeaveLobbyDelegate leaveLobbyTrampoline;
        private StartGameDelegate startGameTrampoline;
        private EngineInterface.MultiplayerSetupData capturedSetup;
        private FileHeader capturedMap;
        private int capturedCoopTrailId;
        private int capturedCoopMissionId;
        private PreviewState state;
        private List<FreeCastleSelection> committedSelections =
            new List<FreeCastleSelection>();
        private int operationId;
        private int unityMainThreadId;
        private int localPlayerId;
        private bool realMultiplayer;
        private bool localConfirmed;
        private bool bypassPauseHook;
        private bool bypassLeaveLobbyHook;
        private bool bypassStartCapture;
        private bool multiplayerLivenessGuardArmed;
        private bool previousResyncingOrSaving;
        private bool briefingObserved;
        private bool localCatalogReady;
        private bool catalogWaitLogged;
        private long countdownStarted;
        private long lastReadySent;
        private long lastAbortSent;
        private string pendingAbortReason;
        private int lastFrame = -1;
        private string selectedChoice = string.Empty;
        private string selectedRotation;
        private string statusText = string.Empty;

        public FreeCastlePreviewRuntime(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            rotationOptions = CastleRotationOptions.Create(GetVanillaGameText);
            rotations = new ObservableCollection<string>(rotationOptions.DisplayTexts);
            selectedRotation = rotationOptions.DefaultDisplayText;
            ConfirmCommand = new RelayCommand(ConfirmLocalSelection, () => CanConfirm);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ConfirmCommand { get; }
        public ObservableCollection<string> CastleChoices => castleChoices;
        public ObservableCollection<string> RotationChoices => rotations;
        public bool IsPreviewActive =>
            state == PreviewState.Loading || state == PreviewState.Selecting ||
            state == PreviewState.Distributing || state == PreviewState.Aborting;
        private bool IsPreviewPendingOrActive =>
            state == PreviewState.AwaitingGameplay || IsPreviewActive;
        public bool IsSpawnMapPass => state == PreviewState.SpawnMap;
        public bool IsLocalConfirmed => localConfirmed;
        public bool CanConfirm => state == PreviewState.Selecting && !localConfirmed;
        public bool HasSelectedCastle =>
            IsPreviewActive &&
            !string.Equals(selectedChoice, NothingText, StringComparison.Ordinal) &&
            !string.Equals(selectedChoice, KeepOnlyText, StringComparison.Ordinal);
        public bool HasRotatableSelection =>
            IsPreviewActive &&
            !string.Equals(selectedChoice, NothingText, StringComparison.Ordinal);
        public int SelectedNativeRotation =>
            rotationOptions.GetNativeRotation(selectedRotation);
        public string TitleText => SerpLocalization.Get("CastlePlanner.Preview.Title");
        public string TimerText
        {
            get
            {
                int remaining = countdownStarted == 0
                    ? TimeoutSeconds
                    : Math.Max(0, TimeoutSeconds - (int)((Stopwatch.GetTimestamp() - countdownStarted) / Stopwatch.Frequency));
                return $"{remaining / 60:00}:{remaining % 60:00}";
            }
        }
        public string StatusText => localConfirmed
            ? SerpLocalization.Get("CastlePlanner.Preview.Waiting")
            : statusText;
        public string ConfirmText => SerpLocalization.Get("CastlePlanner.Preview.Confirm");
        public string RotationText => SerpLocalization.Get("CastlePlanner.Preview.Rotation");

        public string SelectedChoice
        {
            get => selectedChoice;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(selectedChoice, normalized, StringComparison.Ordinal))
                    return;
                selectedChoice = normalized;
                bool updatesPersistedSelection =
                    !string.Equals(normalized, NothingText, StringComparison.Ordinal) &&
                    !string.Equals(normalized, KeepOnlyText, StringComparison.Ordinal) &&
                    !string.Equals(settings.SelectedCastle, normalized, StringComparison.Ordinal);
                if (updatesPersistedSelection)
                    settings.SelectedCastle = normalized;
                Notify(nameof(SelectedChoice));
                Notify(nameof(HasSelectedCastle));
                Notify(nameof(HasRotatableSelection));
                // SettingsChanged already rebuilds a newly persisted castle. None
                // and reselecting the persisted castle need this preview-only path.
                if (!updatesPersistedSelection)
                    SelectionVisualChanged?.Invoke();
            }
        }

        public string SelectedRotation
        {
            get => selectedRotation;
            set
            {
                string normalized = rotations.Contains(value)
                    ? value
                    : rotationOptions.DefaultDisplayText;
                if (selectedRotation == normalized)
                    return;
                selectedRotation = normalized;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Free-castle rotation changed: ui={selectedRotation}, native={SelectedNativeRotation}.");
                Notify(nameof(SelectedRotation));
                Notify(nameof(SelectedNativeRotation));
                SelectionVisualChanged?.Invoke();
            }
        }

        public string NothingText => SerpLocalization.Get("CastlePlanner.Preview.Nothing");
        public string KeepOnlyText => SerpLocalization.Get("CastlePlanner.Preview.RotateKeepOnly");
        public event Action SelectionVisualChanged;
        internal Func<IReadOnlyCollection<FreeCastleSelection>, string>
            RestartCompatibilityValidator { get; set; }

        private bool IsOnUnityMainThread =>
            unityMainThreadId != 0 &&
            Thread.CurrentThread.ManagedThreadId == unityMainThreadId;

        public void Initialize()
        {
            unitySynchronizationContext = SynchronizationContext.Current;
            unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (unitySynchronizationContext == null ||
                !string.Equals(
                    unitySynchronizationContext.GetType().FullName,
                    "UnityEngine.UnitySynchronizationContext",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Free-castle networking must initialize on Unity's main-thread SynchronizationContext.");
            }
            packetDispatchQueue =
                new CoalescedSynchronizationContextQueue<QueuedPacket>(
                    unitySynchronizationContext,
                    DispatchQueuedPacket);
            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<FreeCastlePacket>();
            packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacket);
            // SaveLifecycle: NewMapOnly - preview pause must surround a newly committed launch.
            mapStartSubscription = Shared.MissionEvents.NativeStart.Subscribe(OnStartMap);
            mapUnloadSubscription = Shared.MissionEvents.Ended
                .Subscribe(OnUnloadMap);

            MethodInfo action = typeof(EngineInterface).GetMethod(
                nameof(EngineInterface.GameAction),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                null) ?? throw new MissingMethodException("EngineInterface.GameAction");
            MethodInfo leave = typeof(Platform_Multiplayer).GetMethod(
                "LeaveLobby",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(bool) },
                null) ?? throw new MissingMethodException("Platform_Multiplayer.LeaveLobby");
            MethodInfo start = typeof(Platform_Multiplayer).GetMethod(
                "StartGame",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(EngineInterface.MultiplayerSetupData), typeof(FileHeader),
                    typeof(int), typeof(int)
                },
                null) ?? throw new MissingMethodException("Platform_Multiplayer.StartGame");
            initFastMethod = typeof(Platform_Multiplayer).GetMethod(
                "initFast",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null) ?? throw new MissingMethodException("Platform_Multiplayer.initFast");

            gameActionHook = new Hook(action, (GameActionDelegate)GameActionHook);
            gameActionTrampoline = gameActionHook.GenerateTrampoline<GameActionDelegate>();
            leaveLobbyHook = new Hook(leave, (LeaveLobbyDelegate)LeaveLobbyHook);
            leaveLobbyTrampoline = leaveLobbyHook.GenerateTrampoline<LeaveLobbyDelegate>();
            startGameHook = new Hook(start, (StartGameDelegate)StartGameHook);
            startGameTrampoline = startGameHook.GenerateTrampoline<StartGameDelegate>();
            Application.onBeforeRender += OnBeforeRender;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Free-castle preview initialized: packetId={packetHook.GetPacketId()}, " +
                $"timeout={TimeoutSeconds}s, mainThread={unityMainThreadId}, " +
                $"synchronizationContext={unitySynchronizationContext.GetType().FullName}.");
        }

        public bool TryGetCommittedSelections(out List<FreeCastleSelection> selections)
        {
            selections = null;
            if (!IsFeatureModeAllowed() || state != PreviewState.SpawnMap)
                return false;
            selections = committedSelections
                .Where(item => item.Mode != FreeCastleSelectionMode.Nothing)
                .Select(item => item.Clone())
                .ToList();
            return selections.Count > 0;
        }

        public bool TryGetCommittedRotation(int playerId, out int rotation)
        {
            rotation = 0;
            if (!IsFeatureModeAllowed() || state != PreviewState.SpawnMap)
                return false;
            return FreeCastleSelectionLookup.TryGetRotation(
                committedSelections,
                playerId,
                out rotation);
        }

        public bool TryGetCommittedSelection(
            int playerId,
            out FreeCastleSelection selection)
        {
            selection = null;
            if (!IsFeatureModeAllowed() || state != PreviewState.SpawnMap)
                return false;
            FreeCastleSelection committed = committedSelections.FirstOrDefault(
                item => item != null && item.PlayerId == playerId);
            if (committed == null)
                return false;
            selection = committed.Clone();
            return true;
        }

        private void StartGameHook(
            Platform_Multiplayer self,
            EngineInterface.MultiplayerSetupData setup,
            FileHeader map,
            int coopTrailId,
            int coopMissionId)
        {
            if (!bypassStartCapture && settings.IsSpawnMode)
            {
                capturedSetup = setup;
                capturedMap = map;
                capturedCoopTrailId = coopTrailId;
                capturedCoopMissionId = coopMissionId;
            }
            startGameTrampoline(self, setup, map, coopTrailId, coopMissionId);
        }

        private int GameActionHook(
            Enums.GameActionCommand command,
            int structureId,
            int actionState,
            int value2)
        {
            if (IsFeatureModeAllowed() &&
                !bypassPauseHook && IsPreviewPendingOrActive &&
                command == Enums.GameActionCommand.Game_Paused && actionState == 0)
            {
                Shared.DebugLogHelper.LogInfo(log, "Unpause command suppressed during castle selection.");
                return 0;
            }
            return gameActionTrampoline(command, structureId, actionState, value2);
        }

        private void LeaveLobbyHook(Platform_Multiplayer self, bool preserveGameMembers)
        {
            if (IsFeatureModeAllowed() &&
                !bypassLeaveLobbyHook && IsPreviewPendingOrActive && realMultiplayer)
            {
                Shared.DebugLogHelper.LogInfo(log, "Vanilla lobby departure deferred during castle selection.");
                return;
            }
            leaveLobbyTrampoline(self, preserveGameMembers);
        }

        private void OnStartMap(APIShared.MissionLifecycleNotification args)
        {
            if (args.IsBeforeInitialization)
            {
                if (state == PreviewState.RestartCommitted)
                {
                    if (!IsFeatureModeAllowed())
                    {
                        ResetPreview();
                        return;
                    }
                    state = PreviewState.SpawnMap;
                    NotifyAll();
                    return;
                }
                if (!ShouldStartPreview(args))
                    return;

                ResetPreview();
                state = PreviewState.AwaitingGameplay;
                operationId = unchecked((int)DateTime.UtcNow.Ticks) & int.MaxValue;
                realMultiplayer = Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer;
                localPlayerId = ResolveLocalPlayerId(out string identityError);
                BuildRoster(out string rosterError);
                bool livenessReady = TryArmMultiplayerLivenessGuard(out string livenessError);
                if (livenessReady)
                    ApplyPause(true);
                NotifyAll();
                if (livenessReady)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Castle preview pause armed in OnStartMap(Pre): operation={operationId}, localPlayer={localPlayerId}, multiplayer={realMultiplayer}, roster=[{string.Join(",", roster.OrderBy(id => id))}].");
                }
                if (!string.IsNullOrEmpty(identityError) ||
                    !string.IsNullOrEmpty(rosterError) ||
                    !string.IsNullOrEmpty(livenessError))
                {
                    FailBeforeCommit(!string.IsNullOrEmpty(identityError)
                        ? identityError
                        : !string.IsNullOrEmpty(rosterError)
                            ? rosterError
                            : livenessError);
                }
                return;
            }

            if (!args.IsBeforeInitialization &&
                state == PreviewState.AwaitingGameplay)
            {
                ApplyPause(true);
                settings.PumpCastleCatalogLoad();
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Castle selection is waiting for Vanilla's start-situation screen to close.");
            }
        }

        private bool ShouldStartPreview(APIShared.MissionLifecycleNotification args)
        {
            if (!IsFeatureModeAllowed() ||
                !settings.IsSpawnMode || args.Context.IsSave || args.Context.Mode.CampaignMapId != 0)
                return false;
            Shared.GameModeSnapshot mode = Shared.GameplayModActivationGate.Snapshot;
            return mode.IsRealMultiplayer || mode.IsSingleplayerSkirmishMode;
        }

        private void OnUnloadMap(APIShared.MissionLifecycleNotification args)
        {
            if (state == PreviewState.RestartCommitted)
                return;
            if (state == PreviewState.SpawnMap)
            {
                state = PreviewState.Inactive;
                committedSelections.Clear();
                NotifyAll();
                return;
            }
            if (IsPreviewPendingOrActive)
            {
                Shared.DebugLogHelper.LogWarning(log, "Castle preview map unloaded before a decision; state discarded.");
                ResetPreview();
            }
        }

        private void MarkLocalReady()
        {
            if (!realMultiplayer)
            {
                readyPlayers.Add(localPlayerId);
                BeginSelection();
                return;
            }
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            if (platform?.activeLobby?.isHost == true)
            {
                readyPlayers.Add(localPlayerId);
                TryBeginSelectionAsHost();
            }
            else
            {
                TrySendPreviewReady();
            }
        }

        private void TryBeginSelectionAsHost()
        {
            if (!roster.All(readyPlayers.Contains))
                return;
            countdownStarted = Stopwatch.GetTimestamp();
            BeginSelection();
            Broadcast(NewPacket(FreeCastlePacketKind.PreviewBegin, 0));
        }

        private void BeginSelection()
        {
            state = PreviewState.Selecting;
            if (countdownStarted == 0)
                countdownStarted = Stopwatch.GetTimestamp();
            statusText = SerpLocalization.Get("CastlePlanner.Preview.ChooseHint");
            NotifyAll();
            SelectionVisualChanged?.Invoke();
        }

        private void ConfirmLocalSelection()
        {
            if (!CanConfirm)
                return;
            try
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Free-castle confirmation requested: playerId={localPlayerId}, " +
                    $"choice='{SelectedChoice}', uiRotation={SelectedRotation}, " +
                    $"nativeRotation={SelectedNativeRotation}.");
                FreeCastleSelection selection;
                if (string.Equals(SelectedChoice, NothingText, StringComparison.Ordinal))
                {
                    selection = new FreeCastleSelection
                    {
                        Mode = FreeCastleSelectionMode.Nothing,
                        PlayerId = localPlayerId,
                        Rotation = 0
                    };
                    FreeCastleProtocol.ValidateSelection(selection);
                }
                else if (string.Equals(SelectedChoice, KeepOnlyText, StringComparison.Ordinal))
                {
                    selection = new FreeCastleSelection
                    {
                        Mode = FreeCastleSelectionMode.RotateKeepOnly,
                        PlayerId = localPlayerId,
                        Rotation = SelectedNativeRotation
                    };
                    FreeCastleProtocol.ValidateSelection(selection);
                }
                else
                {
                    if (!settings.TryPrepareSelectedCastle(
                            localPlayerId,
                            SelectedNativeRotation,
                            out selection,
                            out string error))
                        throw new InvalidOperationException(error);
                }
                localConfirmed = true;
                NotifyAll();
                if (!realMultiplayer)
                {
                    AcceptHostDecision(localPlayerId, selection);
                    TryFinalizeAsHost();
                    return;
                }
                Platform_Multiplayer platform = Platform_Multiplayer.Instance;
                if (platform?.activeLobby?.isHost == true)
                {
                    AcceptHostDecision(localPlayerId, selection);
                    TryFinalizeAsHost();
                }
                else
                {
                    UploadSelectionToHost(selection);
                }
            }
            catch (Exception ex)
            {
                statusText = ex.GetBaseException().Message;
                NotifyAll();
                Shared.DebugLogHelper.LogError(log, $"Castle selection could not be confirmed: {ex}");
            }
        }

        private void UploadSelectionToHost(FreeCastleSelection selection)
        {
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            byte[] encoded = FreeCastleProtocol.EncodeSelections(new[] { selection });
            SendTransferToHost(selection.PlayerId, selection.Rotation, selection.DisplayName, encoded);
        }

        private void AcceptHostDecision(int playerId, FreeCastleSelection selection)
        {
            if (selection?.HasCastle == true)
                selection.SpawnBraziersAndFlags = settings.SpawnBraziersAndFlags;
            if (!roster.Contains(playerId) || decisions.ContainsKey(playerId) || noneDecisions.Contains(playerId))
                throw new InvalidOperationException("Duplicate or foreign castle decision.");
            if (selection == null || selection.Mode == FreeCastleSelectionMode.Nothing)
            {
                if (selection != null)
                {
                    FreeCastleProtocol.ValidateSelection(selection);
                    if (selection.PlayerId != playerId)
                        throw new InvalidOperationException("Castle decision player mismatch.");
                }
                noneDecisions.Add(playerId);
            }
            else
            {
                FreeCastleProtocol.ValidateSelection(selection);
                if (selection.PlayerId != playerId)
                    throw new InvalidOperationException("Castle decision player mismatch.");
                decisions.Add(playerId, selection.Clone());
            }
            if (realMultiplayer && Platform_Multiplayer.Instance?.activeLobby?.isHost == true)
                BroadcastParticipantStatus();
        }

        private void TryFinalizeAsHost()
        {
            if (decisions.Count + noneDecisions.Count != roster.Count)
                return;
            if (decisions.Count == 0)
            {
                Broadcast(NewPacket(FreeCastlePacketKind.ContinueWithoutCastles, 0));
                ContinueCurrentGame();
                return;
            }

            byte[] encoded;
            try
            {
                IEnumerable<FreeCastleSelection> manifestSelections =
                    decisions.Values.Concat(noneDecisions.Select(playerId =>
                        new FreeCastleSelection
                        {
                            Mode = FreeCastleSelectionMode.Nothing,
                            PlayerId = playerId,
                            Rotation = 0
                        }));
                encoded = FreeCastleProtocol.EncodeSelections(manifestSelections);
                committedSelections = FreeCastleProtocol.DecodeSelections(encoded);
                ValidateRestartCompatibility(committedSelections);
            }
            catch (Exception ex)
            {
                committedSelections.Clear();
                FailBeforeCommit(ex.GetBaseException().Message);
                return;
            }

            state = PreviewState.Distributing;
            if (!realMultiplayer)
            {
                CommitRestart();
                return;
            }

            manifestAcks.Clear();
            manifestAcks.Add(SteamUser.GetSteamID().m_SteamID);
            SendManifestToPeers(encoded);
            Shared.DebugLogHelper.LogInfo(
                log,
                "Castle manifest distributed; waiting for every participant without a connection-speed deadline.");
            NotifyAll();
            TryCommitRestartWhenManifestReady();
        }

        private void TryCommitRestartWhenManifestReady()
        {
            // Network delay must not turn into a peer-local outcome. Completion is
            // readiness-driven and waits until every authenticated participant acks.
            if (state != PreviewState.Distributing ||
                !FreeCastleParticipantReadiness.AreAllReady(HumanPeerSteamIds(), manifestAcks))
                return;

            // A network game may legitimately have no remote human peers. In that
            // case the host's local readiness completes the same protocol immediately.
            Broadcast(NewPacket(FreeCastlePacketKind.Commit, 0));
            CommitRestart();
        }

        private void OnPacket(ReceiveCustomPacketEventArgs<FreeCastlePacket> args)
        {
            FreeCastlePacket packet = args?.Packet;
            if (packet == null || !args.SenderSteamId.HasValue)
                return;
            ulong sender = args.SenderSteamId.Value.m_SteamID;
            var queued = new QueuedPacket(ClonePacket(packet), sender);
            try
            {
                packetDispatchQueue.Enqueue(queued);
            }
            catch
            {
                // Raw packet callbacks may run on a Timer worker. Logging here would
                // reintroduce an off-main BepInEx call after dispatch itself failed.
            }
        }

        private void DispatchQueuedPacket(QueuedPacket queued)
        {
            if (!IsOnUnityMainThread)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Free-castle packet dispatch was rejected outside Unity's main thread.");
                return;
            }

            try
            {
                ProcessQueuedPacket(queued.Packet, queued.SenderSteamId);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Free-castle main-thread packet dispatch failed: {ex}");
                if (IsPreviewPendingOrActive)
                    FailBeforeCommit(ex.GetBaseException().Message);
            }
        }

        private void ProcessQueuedPacket(FreeCastlePacket packet, ulong sender)
        {
            if (!IsFeatureModeAllowed() || !IsPreviewPendingOrActive)
                return;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            if (platform?.activeLobby == null)
                return;
            bool host = platform.activeLobby.isHost;
            bool senderIsHost = sender == SteamMatchmaking.GetLobbyOwner(platform.activeLobby.id).m_SteamID;
            if (!host && !senderIsHost)
                return;
            if (host && PlayerIdForSteam(sender) < 1)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Ignored free-castle packet from unauthenticated transport sender {sender}.");
                return;
            }
            if (packet.ProtocolVersion != FreeCastleProtocol.ProtocolVersion)
            {
                FailBeforeCommit(
                    $"Castle protocol version mismatch from sender {sender}: " +
                    $"received={packet.ProtocolVersion}, expected={FreeCastleProtocol.ProtocolVersion}.");
                return;
            }
            if (!Enum.IsDefined(typeof(FreeCastlePacketKind), packet.Kind))
            {
                FailBeforeCommit($"Unknown castle packet kind {packet.Kind} from sender {sender}.");
                return;
            }
            FreeCastlePacketKind kind = (FreeCastlePacketKind)packet.Kind;
            if (state == PreviewState.Aborting &&
                (host || kind != FreeCastlePacketKind.Reject))
                return;
            bool operationBootstrap = FreeCastlePacketRouting.IsOperationBootstrap(
                kind,
                host,
                senderIsHost);
            if (!operationBootstrap && packet.OperationId != operationId)
            {
                FailBeforeCommit(
                    $"Castle operation mismatch from sender {sender}: " +
                    $"received={packet.OperationId}, expected={operationId}, kind={kind}.");
                return;
            }

            try
            {
                if (host)
                    HandleHostPacket(packet, sender, kind);
                else if (senderIsHost)
                    HandleClientPacket(packet, kind);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Rejected free-castle packet: kind={kind}, sender={sender}, error={ex}");
                FailBeforeCommit(ex.GetBaseException().Message);
            }
        }

        private static FreeCastlePacket ClonePacket(FreeCastlePacket packet) =>
            new FreeCastlePacket
            {
                ProtocolVersion = packet.ProtocolVersion,
                Kind = packet.Kind,
                OperationId = packet.OperationId,
                PlayerId = packet.PlayerId,
                Rotation = packet.Rotation,
                DisplayName = packet.DisplayName,
                ContentHash = packet.ContentHash,
                UncompressedLength = packet.UncompressedLength,
                CompressedLength = packet.CompressedLength,
                ChunkIndex = packet.ChunkIndex,
                ChunkCount = packet.ChunkCount,
                DataBase64 = packet.DataBase64,
                TimeoutSeconds = packet.TimeoutSeconds,
                Roster = packet.Roster,
                Message = packet.Message
            };

        private void HandleHostPacket(FreeCastlePacket packet, ulong sender, FreeCastlePacketKind kind)
        {
            int senderPlayer = PlayerIdForSteam(sender);
            if (senderPlayer < 1 || packet.PlayerId != senderPlayer)
                throw new InvalidOperationException("Packet player is not authenticated by its transport sender.");
            if (kind == FreeCastlePacketKind.AbortRequest)
            {
                FailBeforeCommit(
                    $"Player {senderPlayer} requested a synchronized castle-preview abort: " +
                    (packet.Message ?? string.Empty));
            }
            else if (kind == FreeCastlePacketKind.PreviewReady &&
                FreeCastlePacketRouting.CanHostAcceptPreviewReady(
                    state == PreviewState.AwaitingGameplay,
                    state == PreviewState.Loading,
                    state == PreviewState.Selecting))
            {
                // Peers reach the gameplay HUD independently. Retain an authenticated
                // early readiness signal until the host's own HUD/catalog is ready.
                readyPlayers.Add(senderPlayer);
                TryBeginSelectionAsHost();
            }
            else if (kind == FreeCastlePacketKind.SelectionBegin && state == PreviewState.Selecting)
                AcceptTransferBegin(packet, senderPlayer, false);
            else if (kind == FreeCastlePacketKind.SelectionChunk && state == PreviewState.Selecting)
            {
                AcceptTransferChunk(packet, senderPlayer, false);
                TryFinalizeAsHost();
            }
            else if (kind == FreeCastlePacketKind.SelectionReady && state == PreviewState.Distributing)
            {
                manifestAcks.Add(sender);
                TryCommitRestartWhenManifestReady();
            }
            else
                throw new InvalidOperationException($"Unexpected castle packet {kind} in host state {state}.");
        }

        private void HandleClientPacket(FreeCastlePacket packet, FreeCastlePacketKind kind)
        {
            if (kind == FreeCastlePacketKind.PreviewBegin)
            {
                operationId = packet.OperationId;
                countdownStarted = Stopwatch.GetTimestamp();
                BeginSelection();
            }
            else if (kind == FreeCastlePacketKind.SelectionBegin)
                AcceptTransferBegin(packet, 0, true);
            else if (kind == FreeCastlePacketKind.SelectionChunk)
                AcceptTransferChunk(packet, 0, true);
            else if (kind == FreeCastlePacketKind.Commit)
                CommitRestart();
            else if (kind == FreeCastlePacketKind.ContinueWithoutCastles)
                ContinueCurrentGame();
            else if (kind == FreeCastlePacketKind.Reject)
                CompleteSynchronizedAbort(packet.Message);
            else if (kind == FreeCastlePacketKind.ParticipantStatus)
            {
                statusText = packet.Message ?? string.Empty;
                NotifyAll();
            }
            else
                throw new InvalidOperationException($"Unexpected castle packet {kind} in client state {state}.");
        }

        private void AcceptTransferBegin(FreeCastlePacket packet, int transferKey, bool manifest)
        {
            if (packet.UncompressedLength < 12 || packet.UncompressedLength > FreeCastleProtocol.MaximumUncompressedBytes ||
                packet.CompressedLength < 1 || packet.CompressedLength > FreeCastleProtocol.MaximumCompressedBytes ||
                packet.ChunkCount < 1 || packet.ChunkCount !=
                    (packet.CompressedLength + FreeCastleProtocol.MaximumChunkBytes - 1) /
                    FreeCastleProtocol.MaximumChunkBytes || string.IsNullOrEmpty(packet.ContentHash) ||
                packet.ContentHash.Length != 64)
                throw new InvalidOperationException("Invalid castle transfer header.");
            if (incoming.ContainsKey(transferKey))
                throw new InvalidOperationException("Duplicate castle transfer header.");
            incoming.Add(transferKey, new IncomingTransfer(packet, manifest));
        }

        private void AcceptTransferChunk(FreeCastlePacket packet, int transferKey, bool manifest)
        {
            if (!incoming.TryGetValue(transferKey, out IncomingTransfer transfer) || transfer.Manifest != manifest)
                throw new InvalidOperationException("Castle chunk has no matching transfer.");
            transfer.Add(packet);
            if (!transfer.IsComplete)
                return;
            byte[] encoded = transfer.Finish();
            incoming.Remove(transferKey);
            List<FreeCastleSelection> decoded = FreeCastleProtocol.DecodeSelections(encoded);
            if (manifest)
            {
                if (decoded.Count != roster.Count ||
                    decoded.Any(item => !roster.Contains(item.PlayerId)) ||
                    roster.Any(playerId => !decoded.Any(item => item.PlayerId == playerId)))
                {
                    throw new InvalidOperationException(
                        "Manifest does not contain exactly one decision for every player.");
                }
                committedSelections = decoded;
                ValidateRestartCompatibility(committedSelections);
                SendToHost(NewPacket(FreeCastlePacketKind.SelectionReady, localPlayerId));
            }
            else
            {
                if (decoded.Count != 1 || decoded[0].PlayerId != transferKey)
                    throw new InvalidOperationException("Client selection transfer is not singular.");
                AcceptHostDecision(transferKey, decoded[0]);
            }
        }

        private void SendTransferToHost(int playerId, int rotation, string name, byte[] encoded)
        {
            SendTransfer(SendToHost, playerId, rotation, name, encoded);
        }

        private void SendManifestToPeers(byte[] encoded)
        {
            foreach (CSteamID peer in HumanPeers())
                SendTransfer(packet => SendReliable(peer, packet), 0, 0, string.Empty, encoded);
        }

        private void SendTransfer(Action<FreeCastlePacket> send, int playerId, int rotation, string name, byte[] encoded)
        {
            byte[] compressed = FreeCastleProtocol.Compress(encoded);
            List<byte[]> chunks = FreeCastleProtocol.Split(compressed);
            FreeCastlePacket begin = NewPacket(FreeCastlePacketKind.SelectionBegin, playerId);
            begin.Rotation = rotation;
            begin.DisplayName = name ?? string.Empty;
            begin.ContentHash = FreeCastleProtocol.HashBytes(encoded);
            begin.UncompressedLength = encoded.Length;
            begin.CompressedLength = compressed.Length;
            begin.ChunkCount = chunks.Count;
            send(begin);
            for (int index = 0; index < chunks.Count; index++)
            {
                FreeCastlePacket chunk = NewPacket(FreeCastlePacketKind.SelectionChunk, playerId);
                chunk.ContentHash = begin.ContentHash;
                chunk.ChunkIndex = index;
                chunk.ChunkCount = chunks.Count;
                chunk.DataBase64 = Convert.ToBase64String(chunks[index]);
                send(chunk);
            }
        }

        private void OnBeforeRender()
        {
            if (!IsPreviewPendingOrActive || Time.frameCount == lastFrame)
                return;
            lastFrame = Time.frameCount;
            if (state == PreviewState.Aborting)
            {
                if (lastAbortSent == 0 ||
                    Stopwatch.GetTimestamp() - lastAbortSent >= Stopwatch.Frequency)
                {
                    TryPropagateSynchronizedAbort();
                }
                return;
            }
            if (state == PreviewState.AwaitingGameplay)
            {
                TryBeginSelectionAfterVanillaStartScreen();
                return;
            }
            if (state == PreviewState.Loading && !localCatalogReady)
            {
                TryFinishCatalogLoading();
                if (!localCatalogReady)
                    return;
            }
            Notify(nameof(TimerText));
            if (realMultiplayer && state == PreviewState.Loading &&
                Platform_Multiplayer.Instance?.activeLobby?.isHost != true &&
                (lastReadySent == 0 || Stopwatch.GetTimestamp() - lastReadySent >= Stopwatch.Frequency))
            {
                TrySendPreviewReady();
            }
            if (countdownStarted == 0 ||
                Stopwatch.GetTimestamp() - countdownStarted < TimeoutSeconds * Stopwatch.Frequency)
                return;
            if (Platform_Multiplayer.Instance?.activeLobby?.isHost == true || !realMultiplayer)
            {
                foreach (int playerId in roster.Where(id => !decisions.ContainsKey(id) && !noneDecisions.Contains(id)).ToArray())
                    noneDecisions.Add(playerId);
                TryFinalizeAsHost();
            }
        }

        private void TryBeginSelectionAfterVanillaStartScreen()
        {
            if (!MainViewModel.viewModelLoaded)
                return;

            MainViewModel viewModel = MainViewModel.Instance;
            if (viewModel == null || viewModel.Show_BlackOut)
                return;
            if (viewModel.Show_HUD_Briefing)
            {
                briefingObserved = true;
                return;
            }
            if (!viewModel.Show_HUD_Main)
                return;

            ApplyPause(true);
            state = PreviewState.Loading;
            NotifyAll();
            Shared.DebugLogHelper.LogInfo(
                log,
                briefingObserved
                    ? "Vanilla start-situation screen closed; castle selection opened."
                    : "Gameplay HUD became active without a start-situation screen; castle selection opened.");
            TryFinishCatalogLoading();
        }

        private void TryFinishCatalogLoading()
        {
            if (localCatalogReady)
                return;

            if (!settings.EnsureCastleCatalogLoaded())
            {
                if (!catalogWaitLogged)
                {
                    catalogWaitLogged = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Castle selection is waiting for asynchronous AIVJSON catalog loading without blocking rendered frames.");
                }
                return;
            }

            RebuildChoices();
            localCatalogReady = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Castle selection catalog is ready; choices={castleChoices.Count}.");
            MarkLocalReady();
        }

        private void CommitRestart()
        {
            try
            {
                state = PreviewState.RestartCommitted;
                NotifyAll();
                if (realMultiplayer)
                {
                    if (capturedSetup == null || capturedMap == null)
                        throw new InvalidOperationException("The multiplayer restart context is unavailable.");
                    Platform_Multiplayer platform = Platform_Multiplayer.Instance ??
                        throw new InvalidOperationException("The multiplayer platform is unavailable.");

                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Free-castle multiplayer restart reset beginning: " +
                        $"operation={operationId}, mpGameActive={Platform_Multiplayer.MPGameActive}, " +
                        $"gameMembers={platform.gameMembers?.Count ?? 0}.");
                    Director.instance.stopSimThread();
                    // This is Vanilla's complete follow-on reset. initFast clears
                    // seed/queue state; initFastFollowOn closes the old sessions,
                    // clears the old roster and resets MPGameActive before StartGame
                    // reconstructs both for the new map.
                    initFastMethod.Invoke(platform, null);
                    platform.initFastFollowOn();
                    RelinquishMultiplayerLivenessGuardAfterVanillaReset();
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Free-castle multiplayer restart reset completed: " +
                        $"operation={operationId}, mpGameActive={Platform_Multiplayer.MPGameActive}, " +
                        $"gameMembers={platform.gameMembers?.Count ?? 0}.");
                    try
                    {
                        bypassStartCapture = true;
                        startGameTrampoline(
                            platform,
                            capturedSetup,
                            capturedMap,
                            capturedCoopTrailId,
                            capturedCoopMissionId);
                    }
                    finally
                    {
                        bypassStartCapture = false;
                    }
                    // Platform.StartGame only loads the map and arms its host/client
                    // seed handshake. Vanilla always follows it with this Director
                    // activation so messages and host acknowledgements are processed.
                    Director.instance.StartMultiplayerGame();
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Free-castle multiplayer restart handshake activated: " +
                        $"operation={operationId}, mpGameActive={Platform_Multiplayer.MPGameActive}, " +
                        $"gameMembers={platform.gameMembers?.Count ?? 0}.");
                }
                else
                {
                    RestartSingleplayer();
                }
            }
            catch (Exception ex)
            {
                FailAfterCommit(ex);
            }
        }

        private void RestartSingleplayer()
        {
            if (!MainViewModel.viewModelLoaded || MainViewModel.Instance?.HUDIngameMenu == null)
                throw new InvalidOperationException("The singleplayer restart context is unavailable.");
            HUD_IngameMenu.RestartSkirmishMapInfo info =
                MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo;
            EditorDirector.instance.stopGameSim();
            if (GameData.Instance.SkirmishTrailType >= 0)
                MainViewModel.Instance.FrontEndMenu.StartTrailMission(
                    GameData.Instance.SkirmishTrailLevel,
                    GameData.Instance.SkirmishTrailType);
            else if (info != null && info.customTrail)
                MainViewModel.Instance.StartCustomTrailMission(
                    info.customTrailName, info.customTrailLevel, info.customTrailDifficulty);
            else if (info != null)
                MainViewModel.Instance.FRONTMultiplayer.RestartSkirmishGame(info);
            else
                throw new InvalidOperationException("No restartable skirmish descriptor exists.");
        }

        private void ContinueCurrentGame()
        {
            try
            {
                try
                {
                    bypassLeaveLobbyHook = true;
                    if (realMultiplayer && Platform_Multiplayer.Instance?.activeLobby != null)
                        leaveLobbyTrampoline(Platform_Multiplayer.Instance, true);
                }
                finally
                {
                    bypassLeaveLobbyHook = false;
                }
            }
            finally
            {
                ReleaseMultiplayerLivenessGuard(
                    refreshRemoteHumanTimestamps: true,
                    reason: "continue-current-game");
            }

            try
            {
                bypassPauseHook = true;
                gameActionTrampoline(Enums.GameActionCommand.Game_Paused, 0, 0, 0);
            }
            finally
            {
                bypassPauseHook = false;
            }
            state = PreviewState.Inactive;
            lastReadySent = 0;
            lastAbortSent = 0;
            pendingAbortReason = null;
            committedSelections.Clear();
            NotifyAll();
            // Returning from a no-castle decision restores the independently
            // persisted local Blueprint choice, but never makes it visible.
            SelectionVisualChanged?.Invoke();
        }

        private void FailBeforeCommit(string reason)
        {
            string normalizedReason = string.IsNullOrWhiteSpace(reason)
                ? "Unknown castle-preview protocol error."
                : reason.Trim();
            Shared.DebugLogHelper.LogError(
                log,
                $"Free-castle preview requires a synchronized abort before commit: {normalizedReason}");
            if (!realMultiplayer)
            {
                ContinueCurrentGame();
                return;
            }

            pendingAbortReason = normalizedReason;
            lastAbortSent = 0;
            state = PreviewState.Aborting;
            statusText = normalizedReason;
            NotifyAll();
            TryPropagateSynchronizedAbort();
        }

        private void TryPropagateSynchronizedAbort()
        {
            if (state != PreviewState.Aborting || string.IsNullOrEmpty(pendingAbortReason))
                return;

            lastAbortSent = Stopwatch.GetTimestamp();
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            try
            {
                if (platform?.activeLobby?.isHost == true)
                {
                    FreeCastlePacket reject = NewPacket(FreeCastlePacketKind.Reject, 0);
                    reject.Message = pendingAbortReason;
                    Broadcast(reject);
                    CompleteSynchronizedAbort(pendingAbortReason);
                    return;
                }

                FreeCastlePacket request = NewPacket(
                    FreeCastlePacketKind.AbortRequest,
                    localPlayerId);
                request.Message = pendingAbortReason;
                SendToHost(request);
                Shared.DebugLogHelper.LogError(
                    log,
                    "Castle-preview abort request sent to the host; this client remains paused until the host broadcasts the shared outcome.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Castle-preview synchronized abort transmission failed and will be retried: {ex}");
            }
        }

        private void CompleteSynchronizedAbort(string reason)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"Free-castle preview aborted consistently for this session; continuing without castles: {reason}");
            ContinueCurrentGame();
        }

        private void FailAfterCommit(Exception error)
        {
            Shared.DebugLogHelper.LogError(log, $"Free-castle restart failed after commit; returning to frontend fail-closed: {error}");
            state = PreviewState.Inactive;
            if (realMultiplayer)
            {
                try
                {
                    ReleaseMultiplayerLivenessGuard(
                        refreshRemoteHumanTimestamps: false,
                        reason: "restart-failure");
                }
                finally
                {
                    Platform_Multiplayer.Instance?.exitMP();
                }
            }
            if (MainViewModel.viewModelLoaded)
                MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            NotifyAll();
        }

        private void ApplyPause(bool paused)
        {
            try
            {
                bypassPauseHook = true;
                EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, paused ? 1 : 0, paused ? 1 : 0);
            }
            finally
            {
                bypassPauseHook = false;
            }
        }

        private bool TryArmMultiplayerLivenessGuard(out string error)
        {
            error = string.Empty;
            if (!realMultiplayer || multiplayerLivenessGuardArmed)
                return true;

            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            if (platform == null)
            {
                error = "The multiplayer platform is unavailable for the castle-preview liveness guard.";
                return false;
            }

            previousResyncingOrSaving = platform.resyncingOrSaving;
            platform.resyncingOrSaving = true;
            multiplayerLivenessGuardArmed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Castle-preview multiplayer liveness guard armed: operation={operationId}, " +
                $"previousResyncingOrSaving={previousResyncingOrSaving}, " +
                $"mpGameActive={Platform_Multiplayer.MPGameActive}.");
            return true;
        }

        private void ReleaseMultiplayerLivenessGuard(
            bool refreshRemoteHumanTimestamps,
            string reason)
        {
            if (!multiplayerLivenessGuardArmed)
                return;

            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            bool restoreValue = previousResyncingOrSaving;
            int refreshedPlayers = 0;
            try
            {
                if (refreshRemoteHumanTimestamps && !restoreValue && platform?.gameMembers != null)
                {
                    DateTime now = DateTime.UtcNow;
                    foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
                    {
                        if (member == null || member.isSelf || member.kicked ||
                            member.skirmishAI || member.steamID <= 1000)
                        {
                            continue;
                        }

                        member.lastTimePacketRecieved = now;
                        refreshedPlayers++;
                    }
                }
            }
            finally
            {
                try
                {
                    if (platform != null)
                        platform.resyncingOrSaving = restoreValue;
                }
                finally
                {
                    multiplayerLivenessGuardArmed = false;
                    previousResyncingOrSaving = false;
                }
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Castle-preview multiplayer liveness guard released: operation={operationId}, " +
                $"reason={reason}, restoredResyncingOrSaving={restoreValue}, " +
                $"refreshedRemoteHumans={refreshedPlayers}.");
        }

        private void RelinquishMultiplayerLivenessGuardAfterVanillaReset()
        {
            if (!multiplayerLivenessGuardArmed)
                return;

            multiplayerLivenessGuardArmed = false;
            previousResyncingOrSaving = false;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Castle-preview multiplayer liveness guard ownership relinquished after Vanilla reset: operation={operationId}.");
        }

        private void BuildRoster(out string error)
        {
            roster.Clear();
            error = string.Empty;
            if (realMultiplayer)
            {
                if (!Shared.PlayerIdentityHelper.TryCaptureHumanRoster(
                    preferInGameRoster: true,
                    out Dictionary<int, ulong> players,
                    out error))
                    return;
                foreach (int playerId in players.Keys)
                    roster.Add(playerId);
            }
            if (roster.Count == 0 && localPlayerId >= 1)
                roster.Add(localPlayerId);
        }

        private int ResolveLocalPlayerId(out string identityError)
        {
            Shared.PlayerIdentityResolution identity =
                Shared.PlayerIdentityHelper.CaptureLocalPlayerId(
                    preferInGameRoster: true);
            identityError = identity.IsResolved ? null : identity.Error;
            if (!string.IsNullOrEmpty(identity.Diagnostic))
                Shared.DebugLogHelper.LogError(log, identity.Diagnostic);
            return identity.PlayerId;
        }

        private int PlayerIdForSteam(ulong steamId)
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPGameMember member = platform?.gameMembers?
                .FirstOrDefault(item => item != null && item.steamID == steamId && !item.kicked && !item.skirmishAI);
            return member?.playerID ?? -1;
        }

        private IEnumerable<CSteamID> HumanPeers()
        {
            ulong local = SteamUser.GetSteamID().m_SteamID;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            if (platform?.gameMembers == null)
                yield break;
            foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
                if (member != null && !member.kicked && !member.skirmishAI && member.steamID > 1000 && member.steamID != local)
                    yield return new CSteamID(member.steamID);
        }

        private IEnumerable<ulong> HumanPeerSteamIds() =>
            HumanPeers().Select(peer => peer.m_SteamID).Concat(new[] { SteamUser.GetSteamID().m_SteamID });

        private void RebuildChoices()
        {
            settings.EnsureCastleCatalogLoaded();
            castleChoices.ReplaceWith(
                new[] { NothingText, KeepOnlyText }.Concat(settings.CastleOptions));
            selectedChoice = settings.CastleOptions.Contains(settings.SelectedCastle)
                ? settings.SelectedCastle
                : NothingText;
            Notify(nameof(CastleChoices));
            Notify(nameof(SelectedChoice));
        }

        private FreeCastlePacket NewPacket(FreeCastlePacketKind kind, int playerId) =>
            new FreeCastlePacket
            {
                ProtocolVersion = FreeCastleProtocol.ProtocolVersion,
                Kind = (int)kind,
                OperationId = operationId,
                PlayerId = playerId,
                TimeoutSeconds = TimeoutSeconds
            };

        private void SendToHost(FreeCastlePacket packet)
        {
            Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
            if (lobby == null)
                throw new InvalidOperationException("No active lobby for castle selection.");
            SendReliable(SteamMatchmaking.GetLobbyOwner(lobby.id), packet);
        }

        private void TrySendPreviewReady()
        {
            try
            {
                SendToHost(NewPacket(FreeCastlePacketKind.PreviewReady, localPlayerId));
                lastReadySent = Stopwatch.GetTimestamp();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Preview-ready transmission will be retried: {ex.GetBaseException().Message}");
            }
        }

        private void Broadcast(FreeCastlePacket packet)
        {
            foreach (CSteamID peer in HumanPeers())
                SendReliable(peer, packet);
        }

        private void SendReliable(CSteamID target, FreeCastlePacket packet)
        {
            byte[] body = GameNetworkAPI.Serialize(packet);
            var envelope = new Platform_Multiplayer.MPData
            {
                packetType = packetHook.GetPacketId(), dataLength = body.Length,
                data = body, dataOffset = 0
            };
            byte[] raw = envelope.ToBytes();
            SteamNetworkingIdentity identity = default(SteamNetworkingIdentity);
            identity.SetSteamID(target);
            GCHandle handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            try
            {
                EResult result = SteamNetworkingMessages.SendMessageToUser(
                    ref identity, handle.AddrOfPinnedObject(), (uint)raw.Length, 40, 2);
                if (result != EResult.k_EResultOK)
                    throw new InvalidOperationException($"Reliable castle packet failed: target={target.m_SteamID}, result={result}.");
            }
            finally
            {
                handle.Free();
            }
        }

        private static bool IsFeatureModeAllowed() =>
            Shared.GameplayFeatureModePolicy.IsAllowed(
                CastlePlannerPlugin.PluginGuid,
                Shared.GameplayFeatureId.FreeCastlePreview,
                Shared.GameplayModActivationGate.Snapshot);

        private void ResetPreview()
        {
            ReleaseMultiplayerLivenessGuard(
                refreshRemoteHumanTimestamps: false,
                reason: "preview-reset");
            state = PreviewState.Inactive;
            roster.Clear();
            readyPlayers.Clear();
            decisions.Clear();
            noneDecisions.Clear();
            incoming.Clear();
            manifestAcks.Clear();
            committedSelections.Clear();
            localConfirmed = false;
            briefingObserved = false;
            localCatalogReady = false;
            catalogWaitLogged = false;
            countdownStarted = 0;
            lastReadySent = 0;
            lastAbortSent = 0;
            pendingAbortReason = null;
            statusText = string.Empty;
            ResetRotationToDefault();
            NotifyAll();
        }

        private void ResetRotationToDefault()
        {
            // The ComboBox can retain its previous SelectedItem while the preview
            // panel is hidden. Force the source notifications on every new map so
            // its visible value cannot drift from the native zero rotation.
            selectedRotation = rotationOptions.DefaultDisplayText;
            Notify(nameof(SelectedRotation));
            Notify(nameof(SelectedNativeRotation));
        }

        private static string GetVanillaGameText(string key)
        {
            return Translate.Instance.GameTexts.TryGetValue(key, out string text)
                ? text
                : null;
        }

        private void BroadcastParticipantStatus()
        {
            string decided = string.Join(", ", decisions.Keys
                .Concat(noneDecisions)
                .Distinct()
                .OrderBy(id => id)
                .Select(id => id.ToString()));
            statusText = $"Confirmed {decisions.Count + noneDecisions.Count}/{roster.Count}" +
                (decided.Length == 0 ? string.Empty : $" (players {decided})");
            FreeCastlePacket packet = NewPacket(FreeCastlePacketKind.ParticipantStatus, 0);
            packet.Message = statusText;
            Broadcast(packet);
            NotifyAll();
        }

        private void NotifyAll()
        {
            Notify(nameof(IsPreviewActive));
            Notify(nameof(IsSpawnMapPass));
            Notify(nameof(IsLocalConfirmed));
            Notify(nameof(CanConfirm));
            Notify(nameof(HasSelectedCastle));
            Notify(nameof(HasRotatableSelection));
            Notify(nameof(StatusText));
            Notify(nameof(TimerText));
            (ConfirmCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void Notify(string property) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        private void ValidateRestartCompatibility(
            IReadOnlyCollection<FreeCastleSelection> selections)
        {
            string error = RestartCompatibilityValidator?.Invoke(selections);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }

        private sealed class IncomingTransfer
        {
            private readonly FreeCastlePacket begin;
            private readonly Dictionary<int, byte[]> chunks = new Dictionary<int, byte[]>();
            private int nextChunkIndex;

            public IncomingTransfer(FreeCastlePacket begin, bool manifest)
            {
                this.begin = begin;
                Manifest = manifest;
            }

            public bool Manifest { get; }
            public bool IsComplete => chunks.Count == begin.ChunkCount;

            public void Add(FreeCastlePacket packet)
            {
                if (packet.ChunkCount != begin.ChunkCount ||
                    !string.Equals(packet.ContentHash, begin.ContentHash, StringComparison.OrdinalIgnoreCase) ||
                    packet.ChunkIndex < 0 || packet.ChunkIndex >= begin.ChunkCount ||
                    packet.ChunkIndex != nextChunkIndex ||
                    chunks.ContainsKey(packet.ChunkIndex))
                    throw new InvalidOperationException("Invalid or duplicate castle chunk.");
                string base64 = packet.DataBase64 ?? string.Empty;
                if (base64.Length > ((FreeCastleProtocol.MaximumChunkBytes + 2) / 3) * 4)
                    throw new InvalidOperationException("Castle chunk encoding is oversized.");
                byte[] data = Convert.FromBase64String(base64);
                if (data.Length > FreeCastleProtocol.MaximumChunkBytes)
                    throw new InvalidOperationException("Castle chunk is oversized.");
                chunks.Add(packet.ChunkIndex, data);
                nextChunkIndex++;
            }

            public byte[] Finish()
            {
                var compressed = new byte[begin.CompressedLength];
                int offset = 0;
                for (int index = 0; index < begin.ChunkCount; index++)
                {
                    if (!chunks.TryGetValue(index, out byte[] chunk) || offset + chunk.Length > compressed.Length)
                        throw new InvalidOperationException("Castle transfer is incomplete.");
                    Buffer.BlockCopy(chunk, 0, compressed, offset, chunk.Length);
                    offset += chunk.Length;
                }
                if (offset != compressed.Length)
                    throw new InvalidOperationException("Castle transfer length mismatch.");
                byte[] encoded = FreeCastleProtocol.Decompress(compressed, begin.UncompressedLength);
                if (!string.Equals(FreeCastleProtocol.HashBytes(encoded), begin.ContentHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Castle transfer hash mismatch.");
                return encoded;
            }
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action execute;
            private readonly Func<bool> canExecute;
            public RelayCommand(Action execute, Func<bool> canExecute)
            {
                this.execute = execute;
                this.canExecute = canExecute;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter) => canExecute();
            public void Execute(object parameter) => execute();
            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
