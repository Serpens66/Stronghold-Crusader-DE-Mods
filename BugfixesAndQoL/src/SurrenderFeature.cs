// Feature: Confirmed surrender and reversible spectator statistics.
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
using Steamworks;

namespace BugfixesAndQoL
{
    internal sealed class SurrenderAndStatisticsViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility surrenderButtonVisibility = Visibility.Collapsed;
        private Visibility statisticsButtonVisibility = Visibility.Collapsed;
        private Visibility statisticsPreviewControlsVisibility = Visibility.Collapsed;
        private bool surrenderButtonEnabled;
        private bool statisticsButtonEnabled;

        internal SurrenderAndStatisticsViewModel(
            Action surrender,
            Action showStatistics,
            Action refreshStatistics,
            Action quitMission)
        {
            SurrenderCommand = new RelayCommand(surrender ?? throw new ArgumentNullException(nameof(surrender)));
            StatisticsCommand = new RelayCommand(showStatistics ?? throw new ArgumentNullException(nameof(showStatistics)));
            RefreshStatisticsCommand = new RelayCommand(refreshStatistics ?? throw new ArgumentNullException(nameof(refreshStatistics)));
            QuitMissionCommand = new RelayCommand(quitMission ?? throw new ArgumentNullException(nameof(quitMission)));
        }

        public RelayCommand SurrenderCommand { get; }
        public RelayCommand StatisticsCommand { get; }
        public RelayCommand RefreshStatisticsCommand { get; }
        public RelayCommand QuitMissionCommand { get; }
        public string SurrenderButtonText => SerpLocalization.Get("BugfixesAndQoL.SurrenderButton");
        public string StatisticsButtonText => SerpLocalization.Get("BugfixesAndQoL.StatisticsButton");
        public string RefreshStatisticsButtonText => SerpLocalization.Get("BugfixesAndQoL.RefreshStatisticsButton");
        public string RefreshStatisticsButtonHelpText => SerpLocalization.Get("BugfixesAndQoL.RefreshStatisticsButtonHelp");
        public string QuitButtonText => MainViewModel.Instance?.IngameMessageQuitButtonText ?? string.Empty;

        public Visibility SurrenderButtonVisibility
        {
            get => surrenderButtonVisibility;
            private set
            {
                if (surrenderButtonVisibility == value)
                    return;

                surrenderButtonVisibility = value;
                OnPropertyChanged(nameof(SurrenderButtonVisibility));
                OnPropertyChanged(nameof(QuitButtonWidth));
                OnPropertyChanged(nameof(QuitButtonHorizontalAlignment));
            }
        }

        public Visibility StatisticsButtonVisibility
        {
            get => statisticsButtonVisibility;
            private set
            {
                if (statisticsButtonVisibility == value)
                    return;

                statisticsButtonVisibility = value;
                OnPropertyChanged(nameof(StatisticsButtonVisibility));
                OnPropertyChanged(nameof(QuitButtonWidth));
                OnPropertyChanged(nameof(QuitButtonHorizontalAlignment));
            }
        }

        public Visibility StatisticsPreviewControlsVisibility
        {
            get => statisticsPreviewControlsVisibility;
            private set
            {
                if (statisticsPreviewControlsVisibility == value)
                    return;

                statisticsPreviewControlsVisibility = value;
                OnPropertyChanged(nameof(StatisticsPreviewControlsVisibility));
            }
        }

        public bool SurrenderButtonEnabled
        {
            get => surrenderButtonEnabled;
            private set
            {
                if (surrenderButtonEnabled == value)
                    return;

                surrenderButtonEnabled = value;
                OnPropertyChanged(nameof(SurrenderButtonEnabled));
            }
        }

        public bool StatisticsButtonEnabled
        {
            get => statisticsButtonEnabled;
            private set
            {
                if (statisticsButtonEnabled == value)
                    return;

                statisticsButtonEnabled = value;
                OnPropertyChanged(nameof(StatisticsButtonEnabled));
            }
        }

        public double QuitButtonWidth =>
            SurrenderButtonVisibility == Visibility.Visible || StatisticsButtonVisibility == Visibility.Visible
                ? 181.25
                : 300.0;

        public HorizontalAlignment QuitButtonHorizontalAlignment =>
            SurrenderButtonVisibility == Visibility.Visible || StatisticsButtonVisibility == Visibility.Visible
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Center;

        internal void SetMenuState(
            bool surrenderVisible,
            bool surrenderEnabled,
            bool statisticsVisible,
            bool statisticsEnabled)
        {
            SurrenderButtonVisibility = surrenderVisible ? Visibility.Visible : Visibility.Collapsed;
            SurrenderButtonEnabled = surrenderVisible && surrenderEnabled;
            StatisticsButtonVisibility = statisticsVisible ? Visibility.Visible : Visibility.Collapsed;
            StatisticsButtonEnabled = statisticsVisible && statisticsEnabled;
        }

        internal void SetStatisticsPreviewActive(bool active)
        {
            StatisticsPreviewControlsVisibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void RefreshText()
        {
            OnPropertyChanged(nameof(SurrenderButtonText));
            OnPropertyChanged(nameof(StatisticsButtonText));
            OnPropertyChanged(nameof(RefreshStatisticsButtonText));
            OnPropertyChanged(nameof(RefreshStatisticsButtonHelpText));
            OnPropertyChanged(nameof(QuitButtonText));
        }
    }

    internal sealed unsafe class SurrenderFeature : IDisposable
    {
        private const int RequestProtocolVersion = 1;
        private delegate void IngameMenuInitDelegate(HUD_IngameMenu self);
        private delegate void AddOnScreenTextEntryDelegate(
            OnScreenText self,
            Enums.eOnScreenText ostID,
            int data1,
            int data2,
            int data3,
            int data4,
            int data5);
        private delegate void MissionOverButtonClickedDelegate(HUD_MissionOver self, string parameter);
        private delegate void SetGameOverStateDelegate(GameData.Scenarios self, int state, int screen, int skirmishDate);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerLobbyReturnFeature lobbyReturnFeature;
        private readonly SurrenderAndStatisticsViewModel buttonViewModel;
        private readonly HashSet<string> acceptedRequests = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        private Hook ingameMenuInitHook;
        private IngameMenuInitDelegate ingameMenuInitOriginal;
        private Hook missionOverButtonHook;
        private MissionOverButtonClickedDelegate missionOverButtonOriginal;
        private Hook addOnScreenTextEntryHook;
        private AddOnScreenTextEntryDelegate addOnScreenTextEntryOriginal;
        private Hook setGameOverStateHook;
        private SetGameOverStateDelegate setGameOverStateOriginal;
        private MethodInfo prepMpScoresMethod;
        private MethodInfo showMpScoreMethod;
        private MethodInfo updateHelpTextMethod;
        private MethodInfo clearWeaselTextMethod;
        private FieldInfo lastStatsField;
        private FieldInfo mpSortTypeField;
        private FieldInfo sortReversedField;
        private FieldInfo rankingField;
        private FieldInfo individualRankingField;
        private FieldInfo missionOverInstance1Field;
        private FieldInfo missionOverInstance2Field;
        private R3PacketEventHook<SurrenderRequestPacket> requestPacketHook;
        private R3PacketEventHook<SurrenderExecutionPacket> executionPacketHook;
        private R3PacketEventHook<EliminatedPlayerSpectatorPacket> spectatorPacketHook;
        private IDisposable requestPacketSubscription;
        private IDisposable executionPacketSubscription;
        private IDisposable spectatorPacketSubscription;
        private int nextRequestId;
        private long confirmationSequence;
        private long confirmationOpenedFromMenuSequence;
        private HUD_MissionOver statisticsPreviewView;
        private bool statisticsReady;
        private bool statisticsPreviewActive;
        private bool statisticsTeamBadgesReady;
        private bool statisticsTeamBadgeErrorLogged;
        private HUD_MissionOver statisticsTeamBadgeView;
        private EngineInterface.MPScoreData statisticsTeamBadgeSnapshot;
        private Grid[,] statisticsTeamBadgeHosts;
        private Image[,] statisticsTeamBadgeVanillaIcons;
        private Grid[,] statisticsTeamBadgeEasyReadIcons;
        private Path[,] statisticsTeamBadgeShields;
        private TextBlock[,] statisticsTeamBadgeNumbers;
        private readonly SolidColorBrush[] statisticsTeamBadgeFillBrushes = new SolidColorBrush[5];
        private SolidColorBrush statisticsTeamBadgeLightTextBrush;
        private SolidColorBrush statisticsTeamBadgeDarkTextBrush;
        private int statisticsTeamBadgeSortType = int.MinValue;
        private bool statisticsTeamBadgeSortReversed;
        private int statisticsTeamBadgeMode = int.MinValue;
        private readonly int[] statisticsTeamBadgeRowPlayerIds = new int[8];
        private readonly long[] lordDeathSessionIds = new long[9];
        private readonly int[] lordDeathSimulationTicks = new int[9];
        private static string lastSurrenderChoreDiagnostic = "none";
        private static string lastSpectatorChoreDiagnostic = "none";
        private readonly bool[] spectatorChoreQueuedPlayers = new bool[9];
        private readonly bool[] spectatorChoreExecutedPlayers = new bool[9];
        private long activeSessionId;
        private bool localPlayerLordDeathObserved;
        private int localPlayerLordDeathPlayerId = -1;
        private bool spectatorPromotionChoreExpected;
        private bool spectatorPromotionActivated;
        private bool spectatorPromotionConfirmed;
        private bool spectatorPromotionErrorLogged;
        private bool spectatorPromotionRejectionLogged;
        private bool gameOverStateCorrectionLogged;
        private int spectatorPromotionPlayerId = -1;
        private string spectatorPromotionGameMode = string.Empty;
        private int lastSpectatorPromotionFrame = -1;
        private bool initialized;
        private bool disposed;

        internal SurrenderFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            lobbyReturnFeature = new MultiplayerLobbyReturnFeature(log, settings);
            buttonViewModel = new SurrenderAndStatisticsViewModel(
                OnSurrenderCommand,
                OnStatisticsCommand,
                OnRefreshStatisticsCommand,
                OnQuitMissionCommand);
        }

        internal SurrenderAndStatisticsViewModel ButtonViewModel => buttonViewModel;

        internal void Initialize()
        {
            if (initialized)
                return;

            // Packet types are registered unconditionally and in a stable order on every peer.
            requestPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SurrenderRequestPacket>();
            requestPacketSubscription = requestPacketHook.GetBaseHook().Observable.Subscribe(OnRequestReceived);
            executionPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SurrenderExecutionPacket>();
            executionPacketSubscription = executionPacketHook.GetBaseHook().Observable.Subscribe(OnExecutionReceived);
            spectatorPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<EliminatedPlayerSpectatorPacket>();
            spectatorPacketSubscription = spectatorPacketHook.GetBaseHook().Observable.Subscribe(OnSpectatorPacketReceived);

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

            // The post-game lobby feature shares these Vanilla hooks so hook order cannot
            // change the surrender/statistics behavior.
            InitializeMissionOverHooks();
            lobbyReturnFeature.Initialize();

            try
            {
                InitializeStatisticsPreview();
                statisticsReady = true;
            }
            catch (Exception ex)
            {
                statisticsReady = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL spectator statistics initialization failed closed; surrender remains available: {ex}");
            }
            try
            {
                InitializeStatisticsTeamBadges();
                statisticsTeamBadgesReady = true;
            }
            catch (Exception ex)
            {
                statisticsTeamBadgesReady = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL statistics team-badge initialization failed closed; Vanilla statistics remain available: {ex}");
            }
            subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(
                log,
                context =>
                {
                    ResetSession("session-start:" + context.Kind);
                    activeSessionId = context.SessionId;
                }));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(_ => ResetSession("mission-end")));
            RegisterPlayerDefeatObserver();
            UnityEngine.Application.onBeforeRender += OnBeforeRender;

            initialized = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bugfixes and QoL surrender/statistics initialized: requestPacketId={requestPacketHook.GetPacketId()}, executionPacketId={executionPacketHook.GetPacketId()}, spectatorPacketId={spectatorPacketHook.GetPacketId()}, requestProtocolVersion={RequestProtocolVersion}, statisticsReady={statisticsReady}, statisticsTeamBadgesReady={statisticsTeamBadgesReady}.");
        }

        internal void RefreshButtonState()
        {
            try
            {
                int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                SurrenderLordSnapshot lord = CaptureLord(localPlayerId);
                bool mapEditor = IsMapEditor();
                bool startSpectator = IsStartSpectator();
                bool statisticsViewer = SurrenderPolicy.IsStatisticsViewer(
                    startSpectator,
                    localPlayerId,
                    lord);
                bool activeMatch = IsActiveMatch();
                bool realMultiplayer = Shared.GameModeHelper.IsRealMultiplayer();
                bool surrenderVisible = SurrenderPolicy.CanShowButton(
                    FeatureEnabled,
                    activeMatch,
                    mapEditor,
                    startSpectator,
                    lord);
                bool surrenderEnabled = SurrenderPolicy.CanEnableButton(
                    surrenderVisible,
                    realMultiplayer,
                    IsChoreTransportReady());
                bool statisticsVisible = SurrenderPolicy.CanShowStatisticsButton(
                    FeatureEnabled,
                    activeMatch,
                    mapEditor,
                    statisticsViewer,
                    IsStatisticsGameMode(),
                    statisticsReady);

                if (statisticsPreviewActive && !statisticsVisible)
                    CloseStatisticsPreview("availability-changed");

                buttonViewModel.SetMenuState(
                    surrenderVisible,
                    surrenderEnabled,
                    statisticsVisible,
                    statisticsVisible);
                buttonViewModel.RefreshText();
            }
            catch (Exception ex)
            {
                if (statisticsPreviewActive)
                    CloseStatisticsPreview("button-refresh-error");
                buttonViewModel.SetMenuState(false, false, false, false);
                buttonViewModel.RefreshText();
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL surrender/statistics button refresh failed closed: {ex}");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            CloseStatisticsPreview("dispose");
            ClearStatisticsTeamBadges();
            ingameMenuInitHook?.Undo();
            ingameMenuInitHook?.Dispose();
            missionOverButtonHook?.Undo();
            missionOverButtonHook?.Dispose();
            addOnScreenTextEntryHook?.Undo();
            addOnScreenTextEntryHook?.Dispose();
            setGameOverStateHook?.Undo();
            setGameOverStateHook?.Dispose();
            requestPacketSubscription?.Dispose();
            executionPacketSubscription?.Dispose();
            spectatorPacketSubscription?.Dispose();
            lobbyReturnFeature.Dispose();
            UnityEngine.Application.onBeforeRender -= OnBeforeRender;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            buttonViewModel.SetMenuState(false, false, false, false);
            buttonViewModel.SetStatisticsPreviewActive(false);
        }

        private bool FeatureEnabled => settings.EnableMod && settings.EnableSurrenderAndStatistics;

        private bool StatisticsTeamBadgesEnabled => settings.EnableMod && settings.EnableClientFeatures;

        private bool EliminatedPlayerSpectatorEnabled =>
            settings.EnableMod && settings.EnableEliminatedPlayersBecomeSpectators;

        private void OnBeforeRender()
        {
            if (disposed || lastSpectatorPromotionFrame == UnityEngine.Time.frameCount)
                return;

            lastSpectatorPromotionFrame = UnityEngine.Time.frameCount;
            try
            {
                lobbyReturnFeature.OnBeforeRender();
                ConfirmSpectatorPromotion();
            }
            catch (Exception ex)
            {
                if (!spectatorPromotionErrorLogged)
                {
                    spectatorPromotionErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL could not activate local spectator features for the eliminated player; Vanilla behavior remains active: {ex}");
                }
            }

            try
            {
                UpdateStatisticsTeamBadges();
            }
            catch (Exception ex)
            {
                ClearStatisticsTeamBadges();
                statisticsTeamBadgesReady = false;
                if (!statisticsTeamBadgeErrorLogged)
                {
                    statisticsTeamBadgeErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL statistics team badges failed closed; Vanilla statistics remain available: {ex}");
                }
            }
        }

        private void ConfirmSpectatorPromotion()
        {
            if (!spectatorPromotionActivated || spectatorPromotionConfirmed || !IsStartSpectator())
                return;

            int currentLocalPlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
            int managedLocalPlayerId = EditorDirector.instance?.ActivePlayerID ?? -1;
            if (currentLocalPlayerId != spectatorPromotionPlayerId ||
                managedLocalPlayerId != spectatorPromotionPlayerId)
            {
                if (!spectatorPromotionErrorLogged)
                {
                    spectatorPromotionErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Vanilla spectator mode changed the local player identity unexpectedly: expected={spectatorPromotionPlayerId}, native={currentLocalPlayerId}, managed={managedLocalPlayerId}, mode={spectatorPromotionGameMode}.");
                }
                return;
            }

            spectatorPromotionConfirmed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Vanilla spectator mode confirmed for eliminated local player {spectatorPromotionPlayerId}; local identity remained unchanged and omniscient visibility/AI information are active: mode={spectatorPromotionGameMode}.");
        }

        private void RegisterPlayerDefeatObserver()
        {
            if (!ApiShared.Current.TryGetPlayerDefeat(
                    BugfixesAndQoLPlugin.PluginGuid,
                    out IPlayerDefeatCapability capability,
                    out NativeCapabilityDiagnostic diagnostic))
            {
                LogPlayerDefeatRegistrationFailure(diagnostic);
                return;
            }

            if (!capability.TryRegisterObserver(
                    "eliminated-player-spectator",
                    OnPlayerLordDied,
                    null,
                    out diagnostic))
                LogPlayerDefeatRegistrationFailure(diagnostic);
        }

        private void LogPlayerDefeatRegistrationFailure(NativeCapabilityDiagnostic diagnostic) =>
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Eliminated-player spectator promotion is unavailable because the APIShared player-defeat observer could not be registered: state={diagnostic?.State}, reason={diagnostic?.Reason}");

        private void OnPlayerLordDied(PlayerLordDeathNotification notification)
        {
            if (disposed || notification == null)
                return;

            int playerId = notification.PlayerId;
            if (playerId < 1 || playerId > 8 ||
                activeSessionId <= 0 || notification.SessionId != activeSessionId)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Ignored stale or invalid APIShared lord-death notification: activeSessionId={activeSessionId}, notificationSessionId={notification.SessionId}, playerId={playerId}, simulationTick={notification.SimulationTick}.");
                return;
            }

            if (lordDeathSessionIds[playerId] == notification.SessionId)
                return;

            lordDeathSessionIds[playerId] = notification.SessionId;
            lordDeathSimulationTicks[playerId] = notification.SimulationTick;

            GamePlayerManagerAPI playerManager = GamePlayerManagerAPI.Instance;
            int localPlayerId = playerManager?.GetLocalPlayerId() ?? -1;
            bool localLordDied = SurrenderPolicy.ShouldLatchPlayerLordDeath(playerId, localPlayerId);
            if (localLordDied)
            {
                localPlayerLordDeathObserved = true;
                localPlayerLordDeathPlayerId = playerId;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Observed lord death through APIShared: sessionId={notification.SessionId}, playerId={playerId}, lordUnitId={notification.LordUnitId}, lordGlobalId={notification.LordGlobalId}, simulationTick={notification.SimulationTick}, localPlayerId={localPlayerId}.");

            Shared.GameModeSnapshot gameMode = Shared.GameModeHelper.Capture();
            if (!gameMode.IsRealMultiplayer)
            {
                if (localLordDied)
                    TryActivateLocalSpectator(playerId, gameMode, "APIShared singleplayer lord-death event");
                return;
            }

            if (localLordDied && EliminatedPlayerSpectatorEnabled)
                spectatorPromotionChoreExpected = true;

            if (GameNetworkAPI.IsLocalHost())
                TryQueueSpectatorChore(notification, gameMode);
        }

        private void TryQueueSpectatorChore(
            PlayerLordDeathNotification notification,
            Shared.GameModeSnapshot gameMode)
        {
            int playerId = notification.PlayerId;
            Platform_Multiplayer.MPGameMember member = Platform_Multiplayer.Instance?.getPlayer(playerId);
            if (!EliminatedPlayerSpectatorEnabled ||
                !IsActiveMatch() || !gameMode.IsRealMultiplayer || IsMapEditor() ||
                notification.SessionId != activeSessionId ||
                !IsHumanMember(member) || spectatorChoreQueuedPlayers[playerId])
            {
                return;
            }

            var packet = new EliminatedPlayerSpectatorPacket { PlayerId = playerId };
            short packetId = spectatorPacketHook?.GetPacketId() ?? (short)0;
            if (!BugfixesAndQoLChoreSender.TrySend(
                    packet,
                    packetId,
                    initialized && spectatorPacketHook != null,
                    value => GameNetworkAPI.Serialize(value),
                    () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA,
                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                    out byte[] body,
                    out string rejectionReason))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Eliminated-player spectator Chore was not queued; no unsynchronized fallback was applied: sessionId={notification.SessionId}, playerId={playerId}, lordDeathTick={notification.SimulationTick}, reason={rejectionReason}.");
                return;
            }

            spectatorChoreQueuedPlayers[playerId] = true;
            int queueTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Eliminated-player spectator Chore queued: sessionId={notification.SessionId}, playerId={playerId}, lordDeathTick={notification.SimulationTick}, queueTick={queueTick}, packetId={packetId}, bodyBytes={body.Length}, bodyHex={ToCompactHex(body)}.");
        }

        private void OnSpectatorPacketReceived(
            ReceiveCustomPacketEventArgs<EliminatedPlayerSpectatorPacket> args)
        {
            EliminatedPlayerSpectatorPacket packet = args?.Packet;
            try
            {
                int playerId = packet?.PlayerId ?? 0;
                Platform_Multiplayer.MPGameMember member = Platform_Multiplayer.Instance?.getPlayer(playerId);
                if (packet == null || args.SenderSteamId.HasValue ||
                    playerId < 1 || playerId > 8 ||
                    !EliminatedPlayerSpectatorEnabled || !IsActiveMatch() ||
                    !Shared.GameModeHelper.IsRealMultiplayer() || IsMapEditor() ||
                    activeSessionId <= 0 || lordDeathSessionIds[playerId] != activeSessionId ||
                    !IsHumanMember(member))
                {
                    LogPacketWarning(
                        $"Rejected stale or invalid eliminated-player spectator Chore: playerId={playerId}, hasSteamSender={args?.SenderSteamId.HasValue}, activeSessionId={activeSessionId}, observedLordDeathSessionId={(playerId >= 1 && playerId <= 8 ? lordDeathSessionIds[playerId] : 0)}.");
                    return;
                }

                if (spectatorChoreExecutedPlayers[playerId])
                    return;

                spectatorChoreExecutedPlayers[playerId] = true;
                int localPlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
                int executionTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
                lastSpectatorChoreDiagnostic =
                    $"session={activeSessionId},player={playerId},lordDeathTick={lordDeathSimulationTicks[playerId]},executionTick={executionTick},localPlayer={localPlayerId}";
                LogPacketInfo(
                    $"Eliminated-player spectator Chore executed: sessionId={activeSessionId}, playerId={playerId}, lordDeathTick={lordDeathSimulationTicks[playerId]}, executionTick={executionTick}, localPlayerId={localPlayerId}.");

                if (localPlayerId != playerId)
                    return;

                spectatorPromotionChoreExpected = false;
                long sessionSnapshot = activeSessionId;
                Shared.GameModeSnapshot gameModeSnapshot = Shared.GameModeHelper.Capture();
                Shared.UnityMainThreadDispatch.TryEnqueue(() =>
                {
                    if (activeSessionId != sessionSnapshot ||
                        lordDeathSessionIds[playerId] != sessionSnapshot)
                        return;
                    TryActivateLocalSpectator(
                        playerId,
                        gameModeSnapshot,
                        "synchronized eliminated-player spectator Chore");
                });
            }
            catch (Exception ex)
            {
                LogPacketError($"Eliminated-player spectator Chore failed closed: {ex}");
            }
        }

        private void TryActivateLocalSpectator(
            int localPlayerId,
            Shared.GameModeSnapshot gameMode,
            string source)
        {
            bool supportedGameMode =
                gameMode.IsRealMultiplayer || gameMode.IsSingleplayerSkirmishMode;
            bool validLocalParticipant =
                IsValidLocalSpectatorPromotionParticipant(localPlayerId, gameMode.IsRealMultiplayer);
            bool lordDeathObserved =
                localPlayerLordDeathObserved && localPlayerLordDeathPlayerId == localPlayerId;
            if (spectatorPromotionActivated ||
                !SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                    EliminatedPlayerSpectatorEnabled,
                    IsActiveMatch(),
                    IsMapEditor(),
                    IsStartSpectator(),
                    supportedGameMode,
                    validLocalParticipant,
                    lordDeathObserved,
                    localPlayerId))
            {
                if (EliminatedPlayerSpectatorEnabled && lordDeathObserved &&
                    (!supportedGameMode || !validLocalParticipant) &&
                    !spectatorPromotionRejectionLogged)
                {
                    spectatorPromotionRejectionLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Eliminated-player spectator activation was rejected fail-closed: playerId={localPlayerId}, source={source}, supportedGameMode={supportedGameMode}, validLocalParticipant={validLocalParticipant}, mode={gameMode.ToDiagnosticString()}.");
                }
                return;
            }

            EngineInterface.GameAction(Enums.GameActionCommand.SpectatorMode, 0, 0);
            spectatorPromotionActivated = true;
            spectatorPromotionPlayerId = localPlayerId;
            spectatorPromotionGameMode = gameMode.ToDiagnosticString();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Activated Vanilla spectator mode for eliminated local player {localPlayerId}: source={source}, mode={spectatorPromotionGameMode}.");
        }

        private static bool IsValidLocalSpectatorPromotionParticipant(
            int localPlayerId,
            bool realMultiplayer)
        {
            if (localPlayerId < 1 || localPlayerId > 8 ||
                EditorDirector.instance == null ||
                EditorDirector.instance.ActivePlayerID != localPlayerId)
            {
                return false;
            }

            if (!realMultiplayer)
                return true;

            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPGameMember localMember = multiplayer?.getPlayer(localPlayerId);
            CSteamID localSteamId = SteamUser.GetSteamID();
            return IsHumanMember(localMember) &&
                localSteamId.m_SteamID > 1000 &&
                localMember.steamID == localSteamId.m_SteamID;
        }

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
                if (buttonViewModel.SurrenderButtonVisibility != Visibility.Visible || !buttonViewModel.SurrenderButtonEnabled)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Bugfixes and QoL surrender click was rejected because the action is unavailable.");
                    return;
                }

                ShowSurrenderConfirmation(openedFromIngameMenu: true);
            }
            catch (Exception ex)
            {
                ReopenIngameMenu();
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not display the surrender confirmation: {ex}");
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
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not display the Lord-disband surrender confirmation: {ex}");
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
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Displayed surrender confirmation: source={(openedFromIngameMenu ? "ingame-menu" : "lord-disband")}, sequence={sequence}.");
        }

        private static void OnQuitMissionCommand()
        {
            // Keep Vanilla's existing mission-leave path and confirmation unchanged.
            MainViewModel.Instance?.HUDIngameMenu?.ButtonIngameMenuFunction(6);
        }

        private void OnStatisticsCommand()
        {
            try
            {
                RefreshButtonState();
                if (buttonViewModel.StatisticsButtonVisibility != Visibility.Visible ||
                    !buttonViewModel.StatisticsButtonEnabled)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Bugfixes and QoL spectator-statistics click was rejected because the action is unavailable.");
                    return;
                }

                // Close restores Vanilla's pre-menu pause state so the observed match keeps running.
                MainViewModel.Instance?.HUDIngameMenu?.Close();
                if (!TryOpenStatisticsPreview())
                    Shared.DebugLogHelper.LogError(log, "Bugfixes and QoL spectator statistics could not be opened; the match remains unchanged.");
            }
            catch (Exception ex)
            {
                CloseStatisticsPreview("open-error");
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL spectator-statistics opening failed closed: {ex}");
            }
        }

        private void OnRefreshStatisticsCommand()
        {
            if (!statisticsPreviewActive || statisticsPreviewView == null)
                return;

            try
            {
                if (!CanUseStatisticsPreview())
                {
                    CloseStatisticsPreview("refresh-unavailable");
                    return;
                }

                EngineInterface.MPScoreData snapshot = EngineInterface.GetMPScoreData();
                if (!ValidateStatisticsSnapshot(snapshot))
                {
                    Shared.DebugLogHelper.LogError(log, "Bugfixes and QoL rejected an invalid spectator-statistics refresh snapshot; the previous snapshot remains visible.");
                    return;
                }

                if (!TryApplyStatisticsSnapshot(statisticsPreviewView, snapshot, initializeView: false))
                    return;

                Shared.DebugLogHelper.LogInfo(log, "Spectator statistics refreshed from the current local simulation snapshot.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL spectator-statistics refresh failed; the previous snapshot remains selected: {ex}");
            }
        }

        private void InitializeStatisticsPreview()
        {
            prepMpScoresMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "prepMPScores",
                BindingFlags.Instance | BindingFlags.NonPublic,
                typeof(EngineInterface.MPScoreData));
            showMpScoreMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "ShowMPScore",
                BindingFlags.Instance | BindingFlags.NonPublic,
                typeof(EngineInterface.MPScoreData),
                typeof(bool),
                typeof(bool));
            updateHelpTextMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "UpdateHelpText",
                BindingFlags.Instance | BindingFlags.NonPublic,
                typeof(int));
            clearWeaselTextMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "ClearWeaselText",
                BindingFlags.Instance | BindingFlags.NonPublic);
            lastStatsField = FindRequiredField(typeof(HUD_MissionOver), "last_stats", BindingFlags.Instance | BindingFlags.NonPublic);
            mpSortTypeField = FindRequiredField(typeof(HUD_MissionOver), "mp_sortType", BindingFlags.Instance | BindingFlags.NonPublic);
            sortReversedField = FindRequiredField(typeof(HUD_MissionOver), "sortReversed", BindingFlags.Instance | BindingFlags.NonPublic);
            missionOverInstance1Field = FindRequiredField(typeof(HUD_MissionOver), "instance1", BindingFlags.Static | BindingFlags.NonPublic);
            missionOverInstance2Field = FindRequiredField(typeof(HUD_MissionOver), "instance2", BindingFlags.Static | BindingFlags.NonPublic);

        }

        private void InitializeStatisticsTeamBadges()
        {
            lastStatsField = FindRequiredField(typeof(HUD_MissionOver), "last_stats", BindingFlags.Instance | BindingFlags.NonPublic);
            mpSortTypeField = FindRequiredField(typeof(HUD_MissionOver), "mp_sortType", BindingFlags.Instance | BindingFlags.NonPublic);
            sortReversedField = FindRequiredField(typeof(HUD_MissionOver), "sortReversed", BindingFlags.Instance | BindingFlags.NonPublic);
            rankingField = FindRequiredField(typeof(HUD_MissionOver), "ranking", BindingFlags.Instance | BindingFlags.NonPublic);
            individualRankingField = FindRequiredField(typeof(HUD_MissionOver), "individual_ranking", BindingFlags.Instance | BindingFlags.NonPublic);
            missionOverInstance1Field = FindRequiredField(typeof(HUD_MissionOver), "instance1", BindingFlags.Static | BindingFlags.NonPublic);
            missionOverInstance2Field = FindRequiredField(typeof(HUD_MissionOver), "instance2", BindingFlags.Static | BindingFlags.NonPublic);

            for (int teamId = 1; teamId <= 4; teamId++)
            {
                if (!SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(
                        teamId,
                        out StatisticsTeamBadgeStyle style))
                {
                    throw new InvalidOperationException($"Statistics team-badge style {teamId} is unavailable.");
                }

                statisticsTeamBadgeFillBrushes[teamId] = new SolidColorBrush(
                    Noesis.Color.FromArgb(byte.MaxValue, style.Red, style.Green, style.Blue));
            }

            statisticsTeamBadgeLightTextBrush = new SolidColorBrush(
                Noesis.Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
            statisticsTeamBadgeDarkTextBrush = new SolidColorBrush(
                Noesis.Color.FromArgb(byte.MaxValue, 20, 16, 10));
        }

        private void InitializeMissionOverHooks()
        {
            MethodInfo buttonClickedMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                nameof(HUD_MissionOver.ButtonClicked),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(string));
            MethodInfo gameOverStateMethod = FindRequiredMethod(
                typeof(GameData.Scenarios),
                "setGameOverState",
                BindingFlags.Instance | BindingFlags.Public,
                typeof(int),
                typeof(int),
                typeof(int));
            MethodInfo addOnScreenTextEntryMethod = FindRequiredMethod(
                typeof(OnScreenText),
                nameof(OnScreenText.addOSTEntry),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(Enums.eOnScreenText),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int));

            Hook newButtonHook = null;
            Hook newGameOverHook = null;
            Hook newOnScreenTextHook = null;
            try
            {
                newButtonHook = new Hook(buttonClickedMethod, (MissionOverButtonClickedDelegate)MissionOverButtonClickedHook);
                MissionOverButtonClickedDelegate newButtonOriginal =
                    newButtonHook.GenerateTrampoline<MissionOverButtonClickedDelegate>();
                newGameOverHook = new Hook(gameOverStateMethod, (SetGameOverStateDelegate)SetGameOverStateHook);
                SetGameOverStateDelegate newGameOverOriginal =
                    newGameOverHook.GenerateTrampoline<SetGameOverStateDelegate>();
                newOnScreenTextHook = new Hook(
                    addOnScreenTextEntryMethod,
                    (AddOnScreenTextEntryDelegate)AddOnScreenTextEntryHook);
                AddOnScreenTextEntryDelegate newOnScreenTextOriginal =
                    newOnScreenTextHook.GenerateTrampoline<AddOnScreenTextEntryDelegate>();

                missionOverButtonHook = newButtonHook;
                missionOverButtonOriginal = newButtonOriginal;
                setGameOverStateHook = newGameOverHook;
                setGameOverStateOriginal = newGameOverOriginal;
                addOnScreenTextEntryHook = newOnScreenTextHook;
                addOnScreenTextEntryOriginal = newOnScreenTextOriginal;
            }
            catch
            {
                newOnScreenTextHook?.Undo();
                newOnScreenTextHook?.Dispose();
                newGameOverHook?.Undo();
                newGameOverHook?.Dispose();
                newButtonHook?.Undo();
                newButtonHook?.Dispose();
                throw;
            }
        }

        private void AddOnScreenTextEntryHook(
            OnScreenText self,
            Enums.eOnScreenText ostID,
            int data1,
            int data2,
            int data3,
            int data4,
            int data5)
        {
            int presentedData1 = ostID == Enums.eOnScreenText.OST_MP_GAME_OVER
                ? SurrenderPolicy.ResolvePresentedGameOverState(data1, SpectatorPromotionPendingOrActive)
                : data1;

            // Preserve Vanilla's record and all auxiliary data. Only the result consumed by
            // the game-over text, video and sound is corrected for our eliminated spectator.
            addOnScreenTextEntryOriginal(self, ostID, presentedData1, data2, data3, data4, data5);
            if (ostID != Enums.eOnScreenText.OST_MP_GAME_OVER)
                return;

            try
            {
                LogGameOverStateCorrectionOnce(data1, presentedData1, "OST_MP_GAME_OVER");
                lobbyReturnFeature.OnGameOverPresentation();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL early post-game lobby preparation failed; the later Game-over fallback remains available: {ex}");
            }
        }

        private void LogGameOverStateCorrectionOnce(
            int originalState,
            int presentedState,
            string source)
        {
            if (presentedState == originalState || gameOverStateCorrectionLogged)
                return;

            gameOverStateCorrectionLogged = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Corrected Vanilla spectator game-over result for eliminated local player {spectatorPromotionPlayerId}: source={source}, originalState={originalState}, presentedState={presentedState}.");
        }

        private void MissionOverButtonClickedHook(HUD_MissionOver self, string parameter)
        {
            if (statisticsPreviewActive && string.Equals(parameter, "Exit", StringComparison.Ordinal))
            {
                CloseStatisticsPreview("exit");
                return;
            }

            if (string.Equals(parameter, "Exit", StringComparison.Ordinal) &&
                lobbyReturnFeature.TryHandleMissionOverExit(
                    () => missionOverButtonOriginal(self, parameter)))
            {
                return;
            }

            missionOverButtonOriginal(self, parameter);
        }

        private void SetGameOverStateHook(GameData.Scenarios self, int state, int screen, int skirmishDate)
        {
            int presentedState = SurrenderPolicy.ResolvePresentedGameOverState(
                state,
                SpectatorPromotionPendingOrActive);
            try
            {
                LogGameOverStateCorrectionOnce(state, presentedState, "setGameOverState");
                lobbyReturnFeature.OnGameOverState(presentedState);
                if (presentedState > 0 && statisticsPreviewActive)
                    CloseStatisticsPreview("vanilla-game-over");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL pre-game-over handling failed; Vanilla game over still runs: {ex}");
            }

            // Vanilla must run exactly once even if preview cleanup failed.
            setGameOverStateOriginal(self, presentedState, screen, skirmishDate);
        }

        private bool TryOpenStatisticsPreview()
        {
            if (!CanUseStatisticsPreview())
                return false;

            EngineInterface.MPScoreData snapshot = EngineInterface.GetMPScoreData();
            if (!ValidateStatisticsSnapshot(snapshot))
            {
                Shared.DebugLogHelper.LogError(log, "Bugfixes and QoL rejected an invalid spectator-statistics snapshot.");
                return false;
            }

            MainViewModel viewModel = MainViewModel.Instance;
            viewModel.Show_HUD_MissionOver_Video = false;
            viewModel.Show_HUD_MissionOver_SandsBackground = false;
            viewModel.Show_HUD_MissionOver = true;

            HUD_MissionOver view = ResolveVisibleMissionOverView();
            if (view == null)
            {
                viewModel.Show_HUD_MissionOver = false;
                Shared.DebugLogHelper.LogError(log, "Bugfixes and QoL could not resolve exactly one visible in-game statistics view.");
                return false;
            }

            viewModel.HUDMissionOver = view;
            if (!TryApplyStatisticsSnapshot(view, snapshot, initializeView: true))
            {
                viewModel.Show_HUD_MissionOver = false;
                return false;
            }

            statisticsPreviewView = view;
            statisticsPreviewActive = true;
            buttonViewModel.SetStatisticsPreviewActive(true);
            Shared.DebugLogHelper.LogInfo(log, "Opened spectator statistics from the current local simulation snapshot without entering Vanilla game over.");
            return true;
        }

        private bool TryApplyStatisticsSnapshot(
            HUD_MissionOver view,
            EngineInterface.MPScoreData snapshot,
            bool initializeView)
        {
            EngineInterface.MPScoreData previousSnapshot =
                lastStatsField.GetValue(view) as EngineInterface.MPScoreData;

            try
            {
                ApplyStatisticsSnapshot(view, snapshot, initializeView);
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not apply the spectator-statistics snapshot: {ex}");
                if (!initializeView && ValidateStatisticsSnapshot(previousSnapshot))
                {
                    try
                    {
                        ApplyStatisticsSnapshot(view, previousSnapshot, initializeView: false);
                    }
                    catch (Exception rollbackEx)
                    {
                        Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not restore the previous spectator-statistics snapshot: {rollbackEx}");
                    }
                }
                return false;
            }
        }

        private void ApplyStatisticsSnapshot(
            HUD_MissionOver view,
            EngineInterface.MPScoreData snapshot,
            bool initializeView)
        {
            prepMpScoresMethod.Invoke(view, new object[] { snapshot });
            lastStatsField.SetValue(view, snapshot);

            MainViewModel viewModel = MainViewModel.Instance;
            if (initializeView)
            {
                // Start spectators never pass through Vanilla's victory/defeat loaders, so
                // dismiss the default Coop "Congratulations" overlay before showing scores.
                clearWeaselTextMethod.Invoke(view, null);
                mpSortTypeField.SetValue(view, 0);
                sortReversedField.SetValue(view, false);
                viewModel.MO_ShowPage1 = Visibility.Visible;
                viewModel.MO_ShowPage2 = Visibility.Collapsed;
                if (view.refRankButton1 == null || view.refRankButton2 == null)
                    throw new InvalidOperationException("Vanilla statistics page selectors are unavailable.");
                view.refRankButton1.IsChecked = true;
                view.refRankButton2.IsChecked = false;
            }

            showMpScoreMethod.Invoke(view, new object[] { snapshot, false, false });
            updateHelpTextMethod.Invoke(view, new object[] { (int)mpSortTypeField.GetValue(view) });

            viewModel.MO_SP_Score = false;
            viewModel.MO_SandsOutro1 = Visibility.Hidden;
            viewModel.MO_MP_Score = Visibility.Visible;
            viewModel.MO_MP_Victory = Visibility.Collapsed;
            viewModel.MO_MP_Defeat = Visibility.Collapsed;
            viewModel.Show_HUD_MissionOver_Video = false;
            viewModel.Show_HUD_MissionOver_SandsBackground = false;
        }

        private HUD_MissionOver ResolveVisibleMissionOverView()
        {
            HUD_MissionOver first = missionOverInstance1Field.GetValue(null) as HUD_MissionOver;
            HUD_MissionOver second = missionOverInstance2Field.GetValue(null) as HUD_MissionOver;
            HUD_MissionOver resolved = null;
            int visibleCount = 0;

            if (first != null && first.IsVisible)
            {
                resolved = first;
                visibleCount++;
            }
            if (second != null && !ReferenceEquals(second, first) && second.IsVisible)
            {
                resolved = second;
                visibleCount++;
            }

            return visibleCount == 1 ? resolved : null;
        }

        private void UpdateStatisticsTeamBadges()
        {
            MainViewModel viewModel = MainViewModel.Instance;
            int badgeMode = SurrenderPolicy.NormalizeStatisticsTeamBadgeMode(
                settings.StatisticsTeamBadgeMode);
            if (!statisticsTeamBadgesReady ||
                !StatisticsTeamBadgesEnabled ||
                badgeMode == SurrenderPolicy.StatisticsTeamBadgesOff ||
                viewModel == null ||
                !viewModel.Show_HUD_MissionOver ||
                viewModel.MO_MP_Score != Visibility.Visible)
            {
                ClearStatisticsTeamBadges();
                return;
            }

            HUD_MissionOver view = ResolveVisibleMissionOverView();
            EngineInterface.MPScoreData snapshot =
                view == null ? null : lastStatsField.GetValue(view) as EngineInterface.MPScoreData;
            if (view == null || !ValidateStatisticsSnapshot(snapshot))
            {
                ClearStatisticsTeamBadges();
                return;
            }

            int sortType = (int)mpSortTypeField.GetValue(view);
            bool sortReversed = (bool)sortReversedField.GetValue(view);
            if (ReferenceEquals(view, statisticsTeamBadgeView) &&
                ReferenceEquals(snapshot, statisticsTeamBadgeSnapshot) &&
                sortType == statisticsTeamBadgeSortType &&
                sortReversed == statisticsTeamBadgeSortReversed &&
                badgeMode == statisticsTeamBadgeMode)
            {
                return;
            }

            EnsureStatisticsTeamBadgeElements(view);
            int[] ranking = rankingField.GetValue(view) as int[];
            int[][] individualRanking = individualRankingField.GetValue(view) as int[][];
            if (!SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                    snapshot.valid,
                    ranking,
                    individualRanking,
                    sortType,
                    sortReversed,
                    statisticsTeamBadgeRowPlayerIds))
            {
                ClearStatisticsTeamBadgeElements();
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    int teamId = SurrenderPolicy.ResolveStatisticsTeamShield(
                        statisticsTeamBadgeRowPlayerIds[row],
                        snapshot.team_shield);
                    ApplyStatisticsTeamBadge(0, row, teamId, badgeMode, viewModel);
                    ApplyStatisticsTeamBadge(1, row, teamId, badgeMode, viewModel);
                }
            }

            statisticsTeamBadgeSnapshot = snapshot;
            statisticsTeamBadgeSortType = sortType;
            statisticsTeamBadgeSortReversed = sortReversed;
            statisticsTeamBadgeMode = badgeMode;
            statisticsTeamBadgeErrorLogged = false;
        }

        private void EnsureStatisticsTeamBadgeElements(HUD_MissionOver view)
        {
            if (ReferenceEquals(view, statisticsTeamBadgeView) &&
                statisticsTeamBadgeHosts != null)
            {
                return;
            }

            ClearStatisticsTeamBadges();
            var hosts = new Grid[2, 8];
            var vanillaIcons = new Image[2, 8];
            var easyReadIcons = new Grid[2, 8];
            var shields = new Path[2, 8];
            var numbers = new TextBlock[2, 8];
            for (int page = 0; page < 2; page++)
            {
                for (int row = 0; row < 8; row++)
                {
                    string prefix = $"BugfixesAndQoLTeamBadgePage{page + 1}Row{row + 1}";
                    hosts[page, row] = view.FindName(prefix) as Grid;
                    vanillaIcons[page, row] = view.FindName(prefix + "VanillaIcon") as Image;
                    easyReadIcons[page, row] = view.FindName(prefix + "EasyReadIcon") as Grid;
                    shields[page, row] = view.FindName(prefix + "Shield") as Path;
                    numbers[page, row] = view.FindName(prefix + "Number") as TextBlock;
                    if (hosts[page, row] == null ||
                        vanillaIcons[page, row] == null ||
                        easyReadIcons[page, row] == null ||
                        shields[page, row] == null ||
                        numbers[page, row] == null)
                    {
                        throw new InvalidOperationException(
                            $"HUD_MissionOver team-badge elements for '{prefix}' were not found.");
                    }
                }
            }

            statisticsTeamBadgeView = view;
            statisticsTeamBadgeHosts = hosts;
            statisticsTeamBadgeVanillaIcons = vanillaIcons;
            statisticsTeamBadgeEasyReadIcons = easyReadIcons;
            statisticsTeamBadgeShields = shields;
            statisticsTeamBadgeNumbers = numbers;
        }

        private void ApplyStatisticsTeamBadge(
            int page,
            int row,
            int teamId,
            int badgeMode,
            MainViewModel viewModel)
        {
            Grid host = statisticsTeamBadgeHosts[page, row];
            if (!SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(
                    teamId,
                    out StatisticsTeamBadgeStyle style))
            {
                statisticsTeamBadgeVanillaIcons[page, row].Source = null;
                statisticsTeamBadgeVanillaIcons[page, row].Visibility = Visibility.Collapsed;
                statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Collapsed;
                host.Visibility = Visibility.Collapsed;
                return;
            }

            if (badgeMode == SurrenderPolicy.StatisticsTeamBadgesVanillaIcons)
            {
                Image vanillaIcon = statisticsTeamBadgeVanillaIcons[page, row];
                vanillaIcon.Source = viewModel.getTeamAlliesShield(teamId, large: false);
                vanillaIcon.Visibility = vanillaIcon.Source == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Collapsed;
                host.Visibility = vanillaIcon.Source == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                return;
            }

            statisticsTeamBadgeVanillaIcons[page, row].Source = null;
            statisticsTeamBadgeVanillaIcons[page, row].Visibility = Visibility.Collapsed;
            statisticsTeamBadgeShields[page, row].Fill = statisticsTeamBadgeFillBrushes[teamId];
            TextBlock number = statisticsTeamBadgeNumbers[page, row];
            number.Text = teamId.ToString();
            number.Foreground = style.UseDarkText
                ? statisticsTeamBadgeDarkTextBrush
                : statisticsTeamBadgeLightTextBrush;
            statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Visible;
            host.Visibility = Visibility.Visible;
        }

        private void ClearStatisticsTeamBadgeElements()
        {
            if (statisticsTeamBadgeHosts == null)
                return;

            for (int page = 0; page < 2; page++)
            {
                for (int row = 0; row < 8; row++)
                {
                    try
                    {
                        statisticsTeamBadgeHosts[page, row].Visibility = Visibility.Collapsed;
                        statisticsTeamBadgeVanillaIcons[page, row].Source = null;
                        statisticsTeamBadgeVanillaIcons[page, row].Visibility = Visibility.Collapsed;
                        statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Collapsed;
                        statisticsTeamBadgeShields[page, row].Fill = null;
                        statisticsTeamBadgeNumbers[page, row].Text = string.Empty;
                    }
                    catch
                    {
                        // Badge cleanup must never interfere with the Vanilla statistics view.
                    }
                }
            }
        }

        private void ClearStatisticsTeamBadges()
        {
            ClearStatisticsTeamBadgeElements();
            statisticsTeamBadgeView = null;
            statisticsTeamBadgeHosts = null;
            statisticsTeamBadgeVanillaIcons = null;
            statisticsTeamBadgeEasyReadIcons = null;
            statisticsTeamBadgeShields = null;
            statisticsTeamBadgeNumbers = null;
            statisticsTeamBadgeSnapshot = null;
            statisticsTeamBadgeSortType = int.MinValue;
            statisticsTeamBadgeSortReversed = false;
            statisticsTeamBadgeMode = int.MinValue;
        }

        private void CloseStatisticsPreview(string reason)
        {
            HUD_MissionOver view = statisticsPreviewView;
            if (!statisticsPreviewActive && view == null)
            {
                buttonViewModel.SetStatisticsPreviewActive(false);
                return;
            }

            statisticsPreviewActive = false;
            statisticsPreviewView = null;
            buttonViewModel.SetStatisticsPreviewActive(false);

            try
            {
                view?.PlayBackgroundVideo(false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL spectator-statistics video cleanup failed: {ex}");
            }

            MainViewModel viewModel = MainViewModel.Instance;
            if (viewModel != null)
            {
                viewModel.Show_HUD_MissionOver_Video = false;
                viewModel.Show_HUD_MissionOver_SandsBackground = false;
                viewModel.Show_HUD_MissionOver = false;
            }

            Shared.DebugLogHelper.LogDebug(log, $"Closed spectator statistics preview: reason={reason}.");
        }

        private bool CanUseStatisticsPreview() =>
            SurrenderPolicy.CanShowStatisticsButton(
                FeatureEnabled,
                IsActiveMatch(),
                IsMapEditor(),
                IsStatisticsViewer(),
                IsStatisticsGameMode(),
                statisticsReady);

        private static bool IsStatisticsGameMode() =>
            Shared.GameModeHelper.IsRealMultiplayer() ||
            Shared.GameModeHelper.IsSingleplayerSkirmishMode();

        private static bool ValidateStatisticsSnapshot(EngineInterface.MPScoreData snapshot)
        {
            if (snapshot == null ||
                !HasLength(snapshot.valid, 9) ||
                !HasLength(snapshot.gold_acquired, 9) ||
                !HasLength(snapshot.max_population, 9) ||
                !HasLength(snapshot.fearfactor, 9) ||
                !HasLength(snapshot.time_deceased, 9) ||
                !HasLength(snapshot.who_killed_who, 81) ||
                !HasLength(snapshot.enemy_buildings_destroyed, 9) ||
                !HasLength(snapshot.food_produced, 9) ||
                !HasLength(snapshot.iron_produced, 9) ||
                !HasLength(snapshot.stone_produced, 9) ||
                !HasLength(snapshot.wood_produced, 9) ||
                !HasLength(snapshot.pitch_produced, 9) ||
                !HasLength(snapshot.minfearfactor, 9) ||
                !HasLength(snapshot.winners, 9) ||
                !HasLength(snapshot.troop_points_killed, 9) ||
                !HasLength(snapshot.enemy_buildings_razed_points, 9) ||
                !HasLength(snapshot.troops_produced, 9) ||
                !HasLength(snapshot.goods_received, 9) ||
                !HasLength(snapshot.goods_sent, 9) ||
                !HasLength(snapshot.notable_victories, 9) ||
                !HasLength(snapshot.notable_defeats, 9) ||
                !HasLength(snapshot.time_lord_killed, 9) ||
                !HasLength(snapshot.blank2, 9) ||
                !HasLength(snapshot.blank3, 9) ||
                !HasLength(snapshot.blank4, 9) ||
                !HasLength(snapshot.weapons_produced, 9) ||
                !HasLength(snapshot.buildings_lost, 9) ||
                !HasLength(snapshot.lords_killed, 9) ||
                !HasLength(snapshot.team_shield, 9) ||
                !HasLength(snapshot.teams, 9) ||
                !HasLength(snapshot.computer_register, 9) ||
                !HasLength(snapshot.playerName, 9) ||
                !HasLength(snapshot.colourMap1, 9) ||
                !HasLength(snapshot.colourMap2, 9))
            {
                return false;
            }

            return true;
        }

        private static bool HasLength(Array array, int minimumLength) =>
            array != null && array.Length >= minimumLength;

        private static MethodInfo FindRequiredMethod(
            Type type,
            string methodName,
            BindingFlags bindingFlags,
            params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                bindingFlags,
                null,
                parameterTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, methodName);
            return method;
        }

        private static FieldInfo FindRequiredField(Type type, string fieldName, BindingFlags bindingFlags)
        {
            FieldInfo field = type.GetField(fieldName, bindingFlags);
            if (field == null)
                throw new MissingFieldException(type.FullName, fieldName);
            return field;
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
                bool realMultiplayer = Shared.GameModeHelper.IsRealMultiplayer();
                if (!SurrenderPolicy.CanShowButton(
                    FeatureEnabled,
                    activeMatch,
                    IsMapEditor(),
                    IsStartSpectator(),
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
                    ProtocolVersion = RequestProtocolVersion,
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

            bool reopenMenu = confirmationOpenedFromMenuSequence == sequence;
            confirmationOpenedFromMenuSequence = 0;
            if (reopenMenu)
                ReopenIngameMenu();
            Shared.DebugLogHelper.LogDebug(
                log,
                reopenMenu
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
            Shared.UnityMainThreadDispatch.TryEnqueue(
                () => ProcessRequest(request, new CSteamID(senderSteamId)));
        }

        private void ProcessRequest(SurrenderRequestPacket request, CSteamID senderSteamId)
        {
            try
            {
                if (request == null || request.ProtocolVersion != RequestProtocolVersion || request.RequestId == 0)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Rejected surrender request with an invalid payload.");
                    return;
                }

                if (!TryResolveHumanSender(senderSteamId, out int playerId))
                {
                    Shared.DebugLogHelper.LogWarning(log, "Rejected surrender request without a known authenticated human sender.");
                    return;
                }

                string requestKey = senderSteamId.m_SteamID + ":" + request.RequestId;
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
                    !Shared.GameModeHelper.IsRealMultiplayer() ||
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

                GameUnitManagerAPI.Instance.KillUnit(resolvedUnitId);
                int executionTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
                lastSurrenderChoreDiagnostic =
                    $"player={packet.PlayerId},unit={resolvedUnitId},global={lord.GlobalId},executionTick={executionTick}";
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
                () => Shared.DebugLogHelper.LogInfo(log, message));

        private void LogPacketWarning(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogWarning(log, message));

        private void LogPacketError(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogError(log, message));

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

            // Only the stable player slot crosses the currently unreliable Chore payload.
            // Every peer resolves and validates that player's living Lord in the execution tick.
            var packet = new SurrenderExecutionPacket
            {
                PlayerId = lord.PlayerId
            };
            short packetId = executionPacketHook?.GetPacketId() ?? (short)0;
            if (!BugfixesAndQoLChoreSender.TrySend(
                    packet,
                    packetId,
                    initialized && executionPacketHook != null,
                    value => GameNetworkAPI.Serialize(value),
                    () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA,
                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                    out byte[] body,
                    out string rejectionReason))
            {
                Shared.DebugLogHelper.LogError(log, $"Surrender Chore was not queued; no local kill was applied: playerId={lord.PlayerId}, reason={rejectionReason}.");
                return false;
            }

            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Surrender Chore queued: playerId={lord.PlayerId}, locallyValidatedLordGlobalId={lord.GlobalId}, " +
                $"bodyBytes={body.Length}, payloadBytes={blob.Length}, " +
                $"bodyHex={ToCompactHex(body)}, blobHex={ToCompactHex(blob)}.");
            return true;
        }

        private static string ToCompactHex(byte[] bytes) =>
            bytes == null ? "<null>" : BitConverter.ToString(bytes).Replace("-", string.Empty);

        internal static string CaptureResyncDiagnostic() =>
            $"lastSurrender=[{lastSurrenderChoreDiagnostic}],lastSpectator=[{lastSpectatorChoreDiagnostic}]";

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
                unit->r_AliveState == AliveState.IsAlive &&
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

        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();

        private bool SpectatorPromotionPendingOrActive =>
            spectatorPromotionChoreExpected || spectatorPromotionActivated;

        private bool IsStatisticsViewer()
        {
            GamePlayerManagerAPI playerManager = GamePlayerManagerAPI.Instance;
            if (playerManager == null)
                return IsStartSpectator();

            int localPlayerId = playerManager.GetLocalPlayerId();
            return SurrenderPolicy.IsStatisticsViewer(
                IsStartSpectator(),
                localPlayerId,
                CaptureLord(localPlayerId));
        }

        private static bool IsStartSpectator() =>
            GameData.Instance?.lastGameState != null && GameData.Instance.lastGameState.spectatorMode != 0;

        private bool IsChoreTransportReady() =>
            BugfixesAndQoLChoreSender.IsAvailable(
                initialized && executionPacketHook != null,
                () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA);

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
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Surrender sender identity resolution failed closed: steamId={sender.m_SteamID}, error={identity.Error}");
                return false;
            }
            if (!string.IsNullOrEmpty(identity.Diagnostic))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Surrender sender identity source mismatch: {identity.Diagnostic}");
            }

            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPGameMember member = multiplayer?.getPlayer(playerId);
            if (!IsHumanMember(member) || member.steamID != sender.m_SteamID)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Surrender sender identity did not resolve back to the authenticated human: " +
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

        private void ResetSession(string reason)
        {
            CloseStatisticsPreview(reason);
            ClearStatisticsTeamBadges();
            acceptedRequests.Clear();
            nextRequestId = 0;
            activeSessionId = 0;
            lastSurrenderChoreDiagnostic = "none";
            lastSpectatorChoreDiagnostic = "none";
            Array.Clear(lordDeathSessionIds, 0, lordDeathSessionIds.Length);
            Array.Clear(lordDeathSimulationTicks, 0, lordDeathSimulationTicks.Length);
            Array.Clear(spectatorChoreQueuedPlayers, 0, spectatorChoreQueuedPlayers.Length);
            Array.Clear(spectatorChoreExecutedPlayers, 0, spectatorChoreExecutedPlayers.Length);
            localPlayerLordDeathObserved = false;
            localPlayerLordDeathPlayerId = -1;
            spectatorPromotionChoreExpected = false;
            spectatorPromotionActivated = false;
            spectatorPromotionConfirmed = false;
            spectatorPromotionErrorLogged = false;
            spectatorPromotionRejectionLogged = false;
            gameOverStateCorrectionLogged = false;
            spectatorPromotionPlayerId = -1;
            spectatorPromotionGameMode = string.Empty;
            lastSpectatorPromotionFrame = -1;
            confirmationSequence++;
            buttonViewModel.SetMenuState(false, false, false, false);
            Shared.DebugLogHelper.LogDebug(log, $"Reset surrender/statistics session state: reason={reason}.");
        }

        private static void ReopenIngameMenu()
        {
            if (MainViewModel.Instance != null)
                MainViewModel.Instance.Show_HUD_IngameMenu = true;
        }
    }
}
