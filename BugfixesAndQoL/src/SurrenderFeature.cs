// Feature: Confirmed surrender and reversible spectator statistics.
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
        private const int ProtocolVersion = 1;
        private delegate void IngameMenuInitDelegate(HUD_IngameMenu self);
        private delegate void MissionOverButtonClickedDelegate(HUD_MissionOver self, string parameter);
        private delegate void SetGameOverStateDelegate(GameData.Scenarios self, int state, int screen, int skirmishDate);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly SurrenderAndStatisticsViewModel buttonViewModel;
        private readonly HashSet<string> acceptedRequests = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<long> executedOperations = new HashSet<long>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        private Hook ingameMenuInitHook;
        private IngameMenuInitDelegate ingameMenuInitOriginal;
        private Hook missionOverButtonHook;
        private MissionOverButtonClickedDelegate missionOverButtonOriginal;
        private Hook setGameOverStateHook;
        private SetGameOverStateDelegate setGameOverStateOriginal;
        private MethodInfo prepMpScoresMethod;
        private MethodInfo showMpScoreMethod;
        private MethodInfo updateHelpTextMethod;
        private FieldInfo lastStatsField;
        private FieldInfo mpSortTypeField;
        private FieldInfo sortReversedField;
        private FieldInfo missionOverInstance1Field;
        private FieldInfo missionOverInstance2Field;
        private R3PacketEventHook<SurrenderRequestPacket> requestPacketHook;
        private R3PacketEventHook<SurrenderExecutionPacket> executionPacketHook;
        private IDisposable requestPacketSubscription;
        private IDisposable executionPacketSubscription;
        private int nextRequestId;
        private int nextOperationId;
        private long confirmationSequence;
        private HUD_MissionOver statisticsPreviewView;
        private bool statisticsReady;
        private bool statisticsPreviewActive;
        private bool localPlayerHadLivingLord;
        private bool spectatorPromotionRequested;
        private bool spectatorPromotionConfirmed;
        private bool spectatorPromotionErrorLogged;
        private int lastSpectatorPromotionFrame = -1;
        private bool initialized;
        private bool disposed;

        internal SurrenderFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

            try
            {
                InitializeStatisticsPreview();
            }
            catch (Exception ex)
            {
                statisticsReady = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL spectator statistics initialization failed closed; surrender remains available: {ex}");
            }
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
            UnityEngine.Application.onBeforeRender += OnBeforeRender;

            initialized = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bugfixes and QoL surrender/statistics initialized: requestPacketId={requestPacketHook.GetPacketId()}, executionPacketId={executionPacketHook.GetPacketId()}, protocolVersion={ProtocolVersion}, statisticsReady={statisticsReady}.");
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
            ingameMenuInitHook?.Undo();
            ingameMenuInitHook?.Dispose();
            missionOverButtonHook?.Undo();
            missionOverButtonHook?.Dispose();
            setGameOverStateHook?.Undo();
            setGameOverStateHook?.Dispose();
            requestPacketSubscription?.Dispose();
            executionPacketSubscription?.Dispose();
            UnityEngine.Application.onBeforeRender -= OnBeforeRender;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            buttonViewModel.SetMenuState(false, false, false, false);
            buttonViewModel.SetStatisticsPreviewActive(false);
        }

        private bool FeatureEnabled => settings.EnableMod && settings.EnableSurrenderAndStatistics;

        private bool EliminatedPlayerSpectatorEnabled =>
            settings.EnableMod && settings.EnableEliminatedPlayersBecomeSpectators;

        private void OnBeforeRender()
        {
            if (disposed || lastSpectatorPromotionFrame == UnityEngine.Time.frameCount)
                return;

            lastSpectatorPromotionFrame = UnityEngine.Time.frameCount;
            try
            {
                TryPromoteEliminatedPlayerToSpectator();
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
        }

        private void TryPromoteEliminatedPlayerToSpectator()
        {
            bool activeMatch = IsActiveMatch();
            if (!activeMatch || IsMapEditor())
                return;

            bool alreadySpectator = IsStartSpectator();
            if (alreadySpectator)
            {
                if (spectatorPromotionRequested && !spectatorPromotionConfirmed)
                {
                    spectatorPromotionConfirmed = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Vanilla spectator mode confirmed for the eliminated local player; omniscient visibility and spectator AI information are now active.");
                }
                return;
            }

            GamePlayerManagerAPI playerManager = GamePlayerManagerAPI.Instance;
            if (playerManager == null)
                return;

            int localPlayerId = playerManager.GetLocalPlayerId();
            SurrenderLordSnapshot lord = CaptureLord(localPlayerId);
            if (SurrenderPolicy.IsValidLord(lord))
            {
                localPlayerHadLivingLord = true;
                return;
            }

            if (spectatorPromotionRequested ||
                !SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                    EliminatedPlayerSpectatorEnabled,
                    activeMatch,
                    false,
                    alreadySpectator,
                    localPlayerHadLivingLord,
                    localPlayerId,
                    lord))
            {
                return;
            }

            // Vanilla evaluates this local flag during every display tick. Keeping the
            // player's slot intact preserves team membership and final statistics.
            EngineInterface.GameAction(Enums.GameActionCommand.SpectatorMode, 0, 0);
            spectatorPromotionRequested = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Requested Vanilla spectator features for eliminated local player {localPlayerId}; the player slot and synchronized game state were left unchanged.");
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
            lastStatsField = FindRequiredField(typeof(HUD_MissionOver), "last_stats", BindingFlags.Instance | BindingFlags.NonPublic);
            mpSortTypeField = FindRequiredField(typeof(HUD_MissionOver), "mp_sortType", BindingFlags.Instance | BindingFlags.NonPublic);
            sortReversedField = FindRequiredField(typeof(HUD_MissionOver), "sortReversed", BindingFlags.Instance | BindingFlags.NonPublic);
            missionOverInstance1Field = FindRequiredField(typeof(HUD_MissionOver), "instance1", BindingFlags.Static | BindingFlags.NonPublic);
            missionOverInstance2Field = FindRequiredField(typeof(HUD_MissionOver), "instance2", BindingFlags.Static | BindingFlags.NonPublic);

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

            Hook newButtonHook = null;
            Hook newGameOverHook = null;
            try
            {
                newButtonHook = new Hook(buttonClickedMethod, (MissionOverButtonClickedDelegate)MissionOverButtonClickedHook);
                MissionOverButtonClickedDelegate newButtonOriginal =
                    newButtonHook.GenerateTrampoline<MissionOverButtonClickedDelegate>();
                newGameOverHook = new Hook(gameOverStateMethod, (SetGameOverStateDelegate)SetGameOverStateHook);
                SetGameOverStateDelegate newGameOverOriginal =
                    newGameOverHook.GenerateTrampoline<SetGameOverStateDelegate>();

                missionOverButtonHook = newButtonHook;
                missionOverButtonOriginal = newButtonOriginal;
                setGameOverStateHook = newGameOverHook;
                setGameOverStateOriginal = newGameOverOriginal;
                statisticsReady = true;
            }
            catch
            {
                newGameOverHook?.Undo();
                newGameOverHook?.Dispose();
                newButtonHook?.Undo();
                newButtonHook?.Dispose();
                throw;
            }
        }

        private void MissionOverButtonClickedHook(HUD_MissionOver self, string parameter)
        {
            if (statisticsPreviewActive && string.Equals(parameter, "Exit", StringComparison.Ordinal))
            {
                CloseStatisticsPreview("exit");
                return;
            }

            missionOverButtonOriginal(self, parameter);
        }

        private void SetGameOverStateHook(GameData.Scenarios self, int state, int screen, int skirmishDate)
        {
            try
            {
                if (state > 0 && statisticsPreviewActive)
                    CloseStatisticsPreview("vanilla-game-over");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not close the statistics preview before Vanilla game over: {ex}");
            }

            // Vanilla must run exactly once even if preview cleanup failed.
            setGameOverStateOriginal(self, state, screen, skirmishDate);
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
            GamePlayerManagerAPI playerManager = GamePlayerManagerAPI.Instance;
            return (playerManager != null && playerManager.IsInMapEditor()) ||
                (MainViewModel.Instance != null && MainViewModel.Instance.IsMapEditorMode);
        }

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
            CloseStatisticsPreview(reason);
            acceptedRequests.Clear();
            executedOperations.Clear();
            nextRequestId = 0;
            nextOperationId = 0;
            localPlayerHadLivingLord = false;
            spectatorPromotionRequested = false;
            spectatorPromotionConfirmed = false;
            spectatorPromotionErrorLogged = false;
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
