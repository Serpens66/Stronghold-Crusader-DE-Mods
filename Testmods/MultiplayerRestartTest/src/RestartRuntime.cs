using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using APIShared;
using APIShared.Economy;
using APIShared.Lobby;
using APIShared.ModSettings;
using APIShared.Networking;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Steamworks;

namespace MultiplayerRestartTest
{
    internal sealed class RestartRuntime
    {
        private readonly ManualLogSource log;
        private readonly int unityThread = Thread.CurrentThread.ManagedThreadId;
        private ModApiClient api;
        private IMissionLifecycleCapability lifecycle;
        private MessageChannel<RestartWire> messages;
        private IUiFrameWorkRegistration frame;
        private IngameMenuActionRegistration menu;
        private Harmony harmony;
        private LaunchSnapshot snapshot;
        private RestartProtocol protocol;
        // New launch evidence never replaces the control identity of an unfinished attempt.
        private sealed class LaunchCapture
        {
            internal LaunchSnapshot Snapshot;
            internal string Session, Digest, SettingsHash;
            internal ulong LobbyId;
            internal Action<string, string, int> Chat;
        }
        private LaunchCapture pendingLaunch;
        private RestartTargetState targetState;
        private ulong[] attemptPeers;
        private long sourceSessionId, adoptedSessionId, nextCompletionRetry;
        private bool expectedSourceEnd, targetLoadEntered, simulationReported, completionPromoted;
        private bool failureParkPending;
        private bool productionReady;
        private long nextReadyAttempt;
        private readonly HashSet<ulong> compatible = new HashSet<ulong>();
        private readonly HashSet<string> retired = new HashSet<string>(StringComparer.Ordinal);
        private string session, digest, settingsHash, notice = "Restart.NoMission";
        private ulong self, host, capturedLobby, targetLobby;
        private bool initialized, ready, stopped, localPublished, restored, verified, allowStart, cancellationHandled, recoveryPending;
        private bool lobbyCreated;
        private long helloUntil, nextHello;
        private int gameOverSignal;
        private Action<string, string, int> chat;
        private long Now => Stopwatch.GetTimestamp();
        private bool Active => protocol != null && !protocol.IsTerminal;
        private bool HasCompletionReceipt => protocol?.Phase == RestartPhase.Completed &&
            targetState != null && lifecycle?.Current?.SessionId == targetState.TargetSessionId;

        internal RestartRuntime(ManualLogSource log) { this.log = log; }
        internal void Report(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] MP_RESTART_TEST " + text);
        private void Main() { if (Thread.CurrentThread.ManagedThreadId != unityThread) throw new InvalidOperationException("Unity thread required."); }

        internal void Initialize()
        {
            Main(); if (initialized) return;
            api = ApiShared.ForMod(RestartPlugin.Guid);
            messages = api.Network.RegisterMessage<RestartWire>("restart-v2", Receive,
                new MessageChannelOptions<RestartWire> { Snapshot = p => p?.Copy(), MaximumBodyBytes = 512,
                    Diagnostic = d => Report("TRANSPORT " + d.Reason) });
            if (!api.Ui.TryRegisterBeforeRender("restart-transition", Tick, out frame, out string reason)) throw new InvalidOperationException(reason);
            if (!api.TryGetMissionLifecycle(out lifecycle, out var diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            if (!lifecycle.TryRegisterObserver("restart-session", Started, Ended, Initializing, out diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            if (!IngameMenuActions.TryRegister(RestartPlugin.Guid, "restart", MenuState, Confirm,
                new IngameMenuActionOptions(IngameMenuRow.RestartMission, IngameMenuSlot.Right, replaceVanilla: true), out menu, out reason))
                throw new InvalidOperationException(reason);
            foreach (var operation in new[] { MissionOverOperation.State, MissionOverOperation.Text })
                if (!MissionOverEvents.TryRegister(operation, RestartPlugin.Guid, "transition-" + operation,
                    args => {
                        // Suppress only the stopped SOURCE presentation. Target mission outcomes
                        // retain Vanilla behavior, including a legitimate first-tick defeat.
                        if (Volatile.Read(ref suppressSourcePresentation) != 0) args.SkipOriginalFunction = true;
                        else if (args.Operation == MissionOverOperation.Text || args.Result > 0)
                            Volatile.Write(ref gameOverSignal, 1);
                    }, null, out reason, order: -1000)) throw new InvalidOperationException(reason);
            if (!LobbyLaunchGuards.TryRegister(RestartPlugin.Guid, "restart-admission", LaunchGuard, out reason))
                throw new InvalidOperationException(reason);
            VanillaBridge.Validate();
            harmony = new Harmony(RestartPlugin.Guid + ".launch-capture");
            harmony.Patch(AccessTools.Method(typeof(Platform_Multiplayer), "StartGame"),
                new HarmonyMethod(typeof(RestartRuntime), nameof(BeforeLaunch)),
                new HarmonyMethod(typeof(RestartRuntime), nameof(AfterLaunch)));
            harmony.Patch(AccessTools.Method(typeof(Platform_Multiplayer), "StartSave"),
                new HarmonyMethod(typeof(RestartRuntime), nameof(BeforeSave)));
            if (!api.TryGetMissionPlayStateReadiness(out var playState, out diagnostic) ||
                !playState.TryRegisterObserver("restart-first-state", PlayStateReady, out diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            if (!WorkerProductionModifiers.TryRegisterObserver(RestartPlugin.Guid, "restart-production", ProductionChanged, out reason))
                throw new InvalidOperationException(reason);
            initialized = true;
            SmokeAutomation.TryStart(api, log, Report);
            Report("INITIALIZED version=0.1.3 barrierInstalled=" + StopBarrier.Installed + "; new games only; install identical package on every peer.");
        }
        private int transitionCommitted;
        private int suppressSourcePresentation;
        private static void BeforeLaunch(EngineInterface.MultiplayerSetupData MPSetupData, FileHeader map, int coopTrailID, int coopMissionID)
        {
            var runtime = RestartPlugin.Runtime;
            if (runtime == null || !runtime.initialized) return;
            runtime.Main();
            try
            {
                if (StopBarrier.Requested && (!runtime.Active || runtime.protocol.Phase != RestartPhase.Starting || !runtime.allowStart))
                    throw new InvalidOperationException("A failed or incomplete restart must be cleaned up before starting a mission.");
                if (runtime.Active)
                {
                    if (runtime.protocol.Phase != RestartPhase.Starting || !runtime.allowStart || runtime.targetLoadEntered ||
                        runtime.snapshot.ValidateSource() != null ||
                        runtime.snapshot.ValidateLobby(Platform_Multiplayer.Instance.activeLobby, runtime.protocol.IsHost) != null ||
                        SettingsFingerprint() != runtime.settingsHash)
                        throw new InvalidOperationException("Restart source, launch permission or host Modsettings changed before loading.");
                    // MPGameActive was reset during the transport-preserving detach. Record
                    // this before loading, never infer a new start from a stale true flag.
                    if (Platform_Multiplayer.MPGameActive) throw new InvalidOperationException("Missing target multiplayer-start reset.");
                    runtime.targetState.AuthorizeLaunch(); runtime.targetState.NoteInactive();
                    runtime.targetLoadEntered = true;
                    Volatile.Write(ref runtime.suppressSourcePresentation, 0);
                }
                runtime.Capture(MPSetupData, map, coopTrailID, coopMissionID);
            }
            catch (Exception error) { if (runtime.Active) runtime.Fail("Target launch rejected: " + error.Message); throw; }
        }
        private static void AfterLaunch(EngineInterface.MultiplayerSetupData MPSetupData, FileHeader map, int coopTrailID, int coopMissionID)
        {
            var runtime = RestartPlugin.Runtime;
            if (runtime == null || !runtime.initialized) return;
            runtime.Capture(MPSetupData, map, coopTrailID, coopMissionID);
            runtime.frame.Request();
        }
        private static void BeforeSave()
        {
            var runtime = RestartPlugin.Runtime;
            if (runtime == null) return;
            if (StopBarrier.Requested || runtime.Active) throw new InvalidOperationException("Cannot load a save during the restart transition.");
            runtime.snapshot = null; runtime.ready = false;
            runtime.notice = "Restart.SavedGame";
            runtime.menu?.Invalidate();
        }
        private void Capture(EngineInterface.MultiplayerSetupData setup, FileHeader map, int trail, int mission)
        {
            try
            {
                var capture = new LaunchCapture { Snapshot = LaunchSnapshot.Capture(setup, map, trail, mission),
                    SettingsHash = SettingsFingerprint(), LobbyId = Platform_Multiplayer.Instance.activeLobby?.identifier ?? 0,
                    Chat = VanillaBridge.Chat() };
                capture.Digest = Hash(capture.Snapshot.Digest + ":" + capture.SettingsHash);
                capture.Session = capture.LobbyId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + capture.Digest;
                if (Active)
                {
                    if (capture.LobbyId != targetLobby || capture.Digest != protocol.Digest)
                        throw new InvalidOperationException("Target launch configuration differs from the agreed original.");
                    pendingLaunch = capture;
                }
                else AdoptCapture(capture);
                Report("CAPTURE session=" + capture.Session + " humans=" + string.Join(",", capture.Snapshot.HumanIds) +
                    " launchDigest=" + capture.Snapshot.Digest + " hostSettingsDigest=" + capture.SettingsHash);
            }
            catch (Exception error)
            {
                if (Active) { Fail("Target capture failed: " + error.Message); throw; }
                snapshot = null; notice = "Restart.CaptureFailed"; Report("CAPTURE_REJECTED " + error);
            }
        }
        private void AdoptCapture(LaunchCapture capture)
        {
            snapshot = capture.Snapshot; session = capture.Session; digest = capture.Digest;
            settingsHash = capture.SettingsHash; capturedLobby = capture.LobbyId; chat = capture.Chat;
        }
        private void Started(MissionLifecycleNotification n)
        {
            if (!api.Ui.TryPost(() => Adopt(n))) Report("Lifecycle dispatch unavailable.");
        }
        private void Adopt(MissionLifecycleNotification n)
        {
            Main();
            if (lifecycle.Current?.SessionId != n.Context.SessionId) return;
            if (Active)
            {
                if (protocol.Phase != RestartPhase.Starting || pendingLaunch == null || targetState == null ||
                    !targetState.MarkManagedReady(n.Context.SessionId)) { Fail("Unexpected target mission start."); return; }
                Report("MANAGED_START_COMPLETE session=" + n.Context.SessionId);
                frame.SetContinuous(true); return;
            }
            // Ordinary starts retire any earlier completion receipt, never an active attempt.
            protocol = null; targetState = null; pendingLaunch = null; attemptPeers = null;
            adoptedSessionId = n.Context.SessionId;
            Volatile.Write(ref transitionCommitted, 0); Volatile.Write(ref suppressSourcePresentation, 0); StopBarrier.Release();
            stopped = localPublished = restored = verified = lobbyCreated = allowStart = cancellationHandled = false;
            targetLobby = 0; compatible.Clear(); self = SteamUser.GetSteamID().m_SteamID;
            ready = !n.Context.IsSave && !n.Context.IsEditor && n.Context.Mode.IsRealMultiplayer && snapshot != null && capturedLobby != 0;
            Volatile.Write(ref gameOverSignal, 0);
            if (ready)
            {
                host = CurrentHost(); compatible.Add(self); notice = "Restart.WaitPeers";
                helloUntil = Now + Stopwatch.Frequency * 90L; nextHello = 0; frame.SetContinuous(true);
            }
            else { frame.SetContinuous(false); if (n.Context.IsSave) notice = "Restart.SavedGame"; }
            menu.Invalidate();
        }
        private void Ended(MissionLifecycleNotification n)
        {
            if (!api.Ui.TryPost(() => {
                if (Active || HasCompletionReceipt)
                {
                    if (targetState != null && targetState.CanIgnoreSourceEnd(n.Context.SessionId, protocol.IsCommitted, expectedSourceEnd)) return;
                    if (targetState?.MatchesTarget(n.Context.SessionId) == true || n.Context.SessionId == sourceSessionId)
                    { Fail("Mission ended: " + n.EndReason); return; }
                }
                if (n.Context.SessionId != adoptedSessionId) return; // A late old End cannot invalidate a newer world.
                ready = false; protocol = null; targetState = null; pendingLaunch = null; menu.Invalidate();
            })) Report("End dispatch unavailable.");
        }
        private void Initializing(MissionLifecycleNotification n)
        {
            Main();
            if (!Active || !n.IsBeforeInitialization) return;
            if (!targetLoadEntered || targetState == null || protocol.Phase != RestartPhase.Starting)
            { Fail("Unexpected mission initialization during restart."); return; }
            try { targetState.BindTarget(n.Context.SessionId); }
            catch (Exception error) { Fail(error.Message); }
        }
        private void PlayStateReady(MissionPlayStateReadyNotification n)
        {
            Main(); Report("FIRST_PLAYSTATE session=" + n.Context.SessionId + " realMP=" + n.Context.Mode.IsRealMultiplayer);
            if (Active && targetState != null && targetState.MarkPlayStateReady(n.Context.SessionId)) frame.Request();
        }
        private void ProductionChanged(WorkerProductionStatus status)
        {
            // This public observer may run on the preparation thread. Copy the immutable
            // admission result and marshal it; never touch lobby/UI state on that thread.
            bool canReady = status != null && status.CanStart && string.IsNullOrEmpty(status.PeerReadinessCode);
            if (!api.Ui.TryPost(() => {
                if (!ReferenceEquals(status, WorkerProductionModifiers.Status)) return;
                productionReady = canReady; if (Active) frame.Request();
            }))
                Report("Production readiness dispatch unavailable.");
        }
        private IngameMenuActionState MenuState()
        {
            bool visible = initialized && lifecycle?.Current?.Mode.IsRealMultiplayer == true && CurrentHost() == self;
            string reason = Eligible();
            return new IngameMenuActionState(RestartText.Get("Restart.Label"), visible, visible && reason == null,
                reason ?? RestartText.Get("Restart.Help"));
        }
        private string Eligible()
        {
            if (!initialized) return RestartText.Get("Restart.InitializationFailed");
            if (!ready || snapshot == null || lifecycle.Current == null) return RestartText.Get(notice);
            if (Active) return RestartText.Get("Restart.InProgress");
            if (!StopBarrier.Installed || !StopBarrier.Observed) return RestartText.Get("Restart.CheckpointMissing");
            if (CurrentHost() != self) return RestartText.Get("Restart.HostOnly");
            if (!snapshot.HumanIds.All(compatible.Contains)) return RestartText.Get("Restart.PeersMismatch");
            return ValidateRunning();
        }
        private string ValidateRunning()
        {
            var mp = Platform_Multiplayer.Instance;
            if (!Director.instance.MultiplayerGame || !Director.instance.SimRunning) return RestartText.Get("Restart.NotRunning");
            if (!Roster().OrderBy(x => x).SequenceEqual(snapshot.HumanIds.OrderBy(x => x))) return RestartText.Get("Restart.RosterChanged");
            if (Volatile.Read(ref gameOverSignal) != 0 || GameData.scenario.InGameoverSituation || mp.CoopContinuationLobbyID != 0 || mp.activeLobby != null)
                return RestartText.Get("Restart.TransitionActive");
            if (VanillaBridge.Busy()) return RestartText.Get("Restart.SaveBusy");
            if (SettingsFingerprint() != settingsHash) return RestartText.Get("Restart.SettingsChanged");
            return null;
        }
        private void Confirm()
        {
            Main(); if (Eligible() != null) return;
            string source = session;
            HUD_ConfirmationPopup.ShowConfirmation(RestartText.Get("Restart.ConfirmTitle"), () => {
                if (session != source || Eligible() != null) return;
                try {
                    host = CurrentHost(); BeginAttempt();
                    frame.SetContinuous(true); Apply(protocol.Begin(Guid.NewGuid().ToString("N"), Now));
                } catch (Exception error) { Fail(error.ToString()); }
            }, () => { });
        }
        private RestartProtocol NewProtocol() => new RestartProtocol(self, host, snapshot.HumanIds, session, digest, Stopwatch.Frequency * 90L);
        private void BeginAttempt()
        {
            sourceSessionId = lifecycle.Current?.SessionId ?? 0;
            if (sourceSessionId <= 0) throw new InvalidOperationException("A current source mission is required.");
            protocol = NewProtocol(); targetState = new RestartTargetState(sourceSessionId);
            attemptPeers = snapshot.HumanIds; pendingLaunch = null;
            expectedSourceEnd = targetLoadEntered = simulationReported = completionPromoted = false;
            stopped = localPublished = restored = verified = lobbyCreated = allowStart = cancellationHandled = recoveryPending = failureParkPending = false;
            targetLobby = 0; nextCompletionRetry = 0;
            nextReadyAttempt = 0;
        }
        private void Receive(NetworkMessage<RestartWire> message)
        { try { ReceiveCore(message); } catch (Exception error) { Fail(error.ToString()); } }
        private void ReceiveCore(NetworkMessage<RestartWire> message)
        {
            Main(); var wire = message.Packet; var p = wire?.Packet;
            if (p == null || snapshot == null || wire.PacketId != messages.PacketId) return;
            bool attemptDelivery = wire.Control == 0 && protocol != null && (Active || HasCompletionReceipt) &&
                p.Session == protocol.Session && p.Digest == protocol.Digest && p.Operation == protocol.Operation &&
                (attemptPeers ?? snapshot.HumanIds).Contains(message.SenderSteamId);
            if (!attemptDelivery && (p.Session != session || p.Digest != digest || !snapshot.HumanIds.Contains(message.SenderSteamId))) return;
            if (wire.Control != 0)
            {
                if (!ready || Active) return;
                if (compatible.Add(message.SenderSteamId)) menu.Invalidate();
                if (wire.Control == 1) Send(message.SenderSteamId, new RestartWire { Control = 2, Packet = HelloPacket() });
                return;
            }
            if (!Active && !attemptDelivery)
            {
                if (!ready || retired.Contains(p.Operation) || p.Kind != RestartMessageKind.Prepare || message.SenderSteamId != CurrentHost()) return;
                host = CurrentHost(); BeginAttempt(); frame.SetContinuous(true);
            }
            if (!protocol.IsCommitted && (CurrentHost() != host || ValidateRunning() != null)) { Fail("Host, roster or running-state precondition changed."); return; }
            var received = protocol.Receive(message.SenderSteamId, p, Now);
            if (!received.Accepted && protocol.Phase == RestartPhase.Idle)
            { protocol = null; frame.SetContinuous(false); Report("INITIAL_PREPARE_REJECTED " + received.Rejection); return; }
            Apply(received);
        }
        private RestartPacket HelloPacket() => new RestartPacket { Session = session, Digest = digest, Operation = "", Kind = RestartMessageKind.Prepare };
        private void Send(ulong peer, RestartWire wire)
        {
            wire.PacketId = messages.PacketId;
            var result = messages.SendToSteamPeer(wire, peer);
            if (!result.Submitted) throw new InvalidOperationException("Send failed: " + result.Reason);
        }
        private void Apply(RestartStep step)
        {
            if (!step.Accepted) { Report("IGNORED " + step.Rejection); return; }
            if (protocol.IsCommitted)
            {
                Volatile.Write(ref transitionCommitted, 1);
                if (!targetLoadEntered) Volatile.Write(ref suppressSourcePresentation, 1);
            }
            // The client's grant must happen before StartAcknowledged is submitted. Lobby
            // metadata and channel-2 messages are not ordered relative to each other.
            if ((step.Effects & RestartEffect.StartLocal) != 0 && !protocol.IsHost)
            {
                string invalid = snapshot.ValidateLobby(Platform_Multiplayer.Instance.activeLobby, false);
                if (invalid != null || SettingsFingerprint() != settingsHash) { Fail(invalid ?? "Settings changed before start grant."); return; }
                allowStart = true; StopBarrier.AllowLaunch();
            }
            if ((step.Effects & RestartEffect.Cancelled) != 0)
            {
                // Failure cleanup must run even when one disconnected peer rejects Abort.
                try {
                    foreach (var packet in step.Outbound)
                        foreach (ulong peer in attemptPeers ?? snapshot.HumanIds) if (peer != self)
                            try { Send(peer, new RestartWire { Packet = packet }); }
                            catch (Exception error) { Report("ABORT_SEND_FAILED " + error.Message); }
                } finally { Cancelled(); }
                return;
            }
            // Client completion is cached before its acknowledgement is sent. A send error
            // may still Abort this exact target via the retained old control context.
            if ((step.Effects & RestartEffect.CompletedLocal) != 0) PromoteCompletion();
            foreach (var p in step.Outbound)
                foreach (ulong peer in attemptPeers ?? snapshot.HumanIds) if (peer != self) Send(peer, new RestartWire { Packet = p });
            if ((step.Effects & RestartEffect.PrepareLocal) != 0)
            {
                string invalid = ValidateRunning() ?? snapshot.ValidateSource();
                if (invalid != null || !StopBarrier.Installed || !StopBarrier.Observed) { Fail(invalid ?? "Worker checkpoint unavailable."); return; }
                Report("PREPARED operation=" + protocol.Operation); Apply(protocol.Prepared(Now));
            }
            if ((step.Effects & RestartEffect.StopLocal) != 0)
            {
                // No Unity wait. Tick observes a positive between-iterations worker receipt and drained callbacks.
                StopBarrier.Request(); MainViewModel.Instance.Show_HUD_IngameMenu = false;
                Report("STOP_REQUESTED operation=" + protocol.Operation);
            }
            if ((step.Effects & RestartEffect.CreateLobby) != 0) CreateLobby();
            if ((step.Effects & RestartEffect.JoinLobby) != 0) OpenLobby();
            if ((step.Effects & RestartEffect.StartLocal) != 0)
            {
                allowStart = true; StopBarrier.AllowLaunch();
                if (protocol.IsHost)
                {
                    string invalid = snapshot.ValidateLobby(Platform_Multiplayer.Instance.activeLobby, true);
                    if (invalid != null) { Fail(invalid); return; }
                    Report("AUTOSTART operation=" + protocol.Operation);
                    MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("Play");
                }
            }
            menu.Invalidate();
        }
        private void PromoteCompletion()
        {
            if (completionPromoted) return;
            if (pendingLaunch == null || targetState?.IsReady != true || lifecycle.Current?.SessionId != targetState.TargetSessionId)
                throw new InvalidOperationException("Cannot complete without this target's full start evidence.");
            if (CurrentHost() != host || !protocol.RosterMatches(Roster()) ||
                !Platform_Multiplayer.MPGameActive || !Director.instance.SimRunning || Volatile.Read(ref gameOverSignal) != 0)
                throw new InvalidOperationException("Target host, roster or simulation changed before completion.");
            AdoptCapture(pendingLaunch); completionPromoted = true;
            adoptedSessionId = targetState.TargetSessionId; retired.Add(protocol.Operation);
            ready = true; compatible.Clear(); foreach (ulong peer in snapshot.HumanIds) compatible.Add(peer);
            // Keep protocol/targetState as one bounded receipt; never keep the UI pump alive for a completed operation.
            Volatile.Write(ref transitionCommitted, 0); Volatile.Write(ref suppressSourcePresentation, 0);
            frame.SetContinuous(false); Report("RESTART_COMPLETE operation=" + protocol.Operation + " target=" + adoptedSessionId);
        }
        private void Tick()
        {
            Main();
            try
            {
                if (failureParkPending)
                {
                    if (!StopBarrier.Quiescent) return;
                    failureParkPending = false;
                    ShowFailureRecovery();
                    return;
                }
                if (recoveryPending)
                {
                    if (!StopBarrier.Quiescent) return;
                    Director.instance.EndMultiplayer(); EditorDirector.instance.stopGameSim();
                    if (Platform_Multiplayer.Instance.activeLobby != null) Platform_Multiplayer.Instance.LeaveLobby();
                    VanillaBridge.CleanupRetainedPeers((attemptPeers ?? snapshot.HumanIds).Where(p => p != self).ToArray());
                    Platform_Multiplayer.Instance.exitMP(); FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
                    recoveryPending = false; Volatile.Write(ref transitionCommitted, 0); Volatile.Write(ref suppressSourcePresentation, 0);
                    StopBarrier.Release(); frame.SetContinuous(false);
                    return;
                }
                if (!Active)
                {
                    if (!ready || snapshot.HumanIds.All(compatible.Contains) || Now > helloUntil) { frame.SetContinuous(false); return; }
                    if (Now >= nextHello)
                    {
                        nextHello = Now + Stopwatch.Frequency;
                        foreach (ulong peer in snapshot.HumanIds) if (peer != self) Send(peer, new RestartWire { Control = 1, Packet = HelloPacket() });
                    }
                    return;
                }
                Apply(protocol.Tick(Now)); if (!Active) return;
                if (!protocol.IsCommitted && (CurrentHost() != host || ValidateRunning() != null)) { Fail("Preparation invalidated by session/host/state change."); return; }
                if (protocol.Phase == RestartPhase.Stopping && StopBarrier.Quiescent && !stopped)
                {
                    // EndMultiplayer is local flag cleanup only. stopGameSim then takes its non-MP
                    // branch and cannot start the delayed ExitMP coroutine or send LeaveGame.
                    expectedSourceEnd = true;
                    Director.instance.EndMultiplayer(); EditorDirector.instance.stopGameSim();
                    VanillaBridge.ClearPendingExit(); stopped = true;
                    Report("STOPPED workerParked=true callbacksDrained=true"); Apply(protocol.Stopped(Now));
                }
                if (stopped && StopBarrier.Quiescent && Platform_Multiplayer.Instance.activeLobby == null) VanillaBridge.DrainRestart(messages.PacketId);
                if (lobbyCreated && protocol.Phase == RestartPhase.WaitingLobby)
                {
                    lobbyCreated = false;
                    Apply(protocol.PublishLobby(targetLobby, Now)); // CreateLobbyResult's flush already returned.
                }
                if (protocol.Phase == RestartPhase.Joining) VerifyLobby();
                if (targetLoadEntered && Volatile.Read(ref gameOverSignal) != 0)
                { Fail("Target mission entered game-over during restart."); return; }
                if (simulationReported && (CurrentHost() != host || !protocol.RosterMatches(Roster())))
                { Fail("Target host or human roster changed before completion."); return; }
                if (protocol.Phase == RestartPhase.Starting && targetState.TargetSessionId != 0)
                {
                    long current = lifecycle.Current?.SessionId ?? 0;
                    targetState.ObserveNativeStarted(current, Platform_Multiplayer.MPGameActive, Director.instance.SimRunning);
                    if (!simulationReported && targetState.IsReady)
                    {
                        if (CurrentHost() != host || !protocol.RosterMatches(Roster()))
                        { Fail("Target host or human roster changed during start."); return; }
                        simulationReported = true;
                        Report("SIMULATION_READY target=" + current);
                        Apply(protocol.LocalSimulationReady(Now));
                    }
                }
                if (protocol.Phase == RestartPhase.Completing && Now >= nextCompletionRetry)
                {
                    nextCompletionRetry = Now + Stopwatch.Frequency;
                    Apply(protocol.RetryCompletion(Now));
                }
            }
            catch (Exception error) { Fail(error.ToString()); }
        }
        private void CreateLobby()
        {
            // Every old peer stream has crossed its reliable stop marker before this effect.
            VanillaBridge.ClearOldManagedMessages();
            Report("CREATE_LOBBY"); var mp = Platform_Multiplayer.Instance;
            string operation = protocol.Operation;
            mp.CreateLobby(snapshot.GameName, snapshot.MapName, snapshot.MapFileName, snapshot.MaxPlayers,
                snapshot.GameTypeCoop, 4, snapshot.LobbySettings, snapshot.Crc, () => {
                    if (!Active || protocol.Operation != operation || protocol.Phase != RestartPhase.WaitingLobby) return;
                    var created = mp.activeLobby;
                    if (created == null || !created.isHost || created.identifier == 0 || SteamMatchmaking.GetLobbyOwner(created.id).m_SteamID != self)
                    { Fail("Replacement lobby creation failed."); return; }
                    targetLobby = created.identifier; lobbyCreated = true;
                    Report("LOBBY_CREATED id=" + targetLobby);
                }, chat, snapshot.LobbyCoopTrailId, clearGameMembers: false);
        }
        private void OpenLobby()
        {
            VanillaBridge.ClearOldManagedMessages();
            targetLobby = protocol.LobbyId;
            var mp = Platform_Multiplayer.Instance;
            Report("OPEN_LOBBY id=" + targetLobby);
            if (protocol.IsHost) VanillaBridge.OpenHost(snapshot, mp.activeLobby);
            else { VanillaBridge.DetachTransport(); mp.SetInviteLobbyID(targetLobby); mp.ResumeInvite(); }
            StopBarrier.AllowNetwork();
        }
        private void VerifyLobby()
        {
            if (verified) return; // Fresh final validation is performed again at the actual start grant.
            var mp = Platform_Multiplayer.Instance; var lobby = mp.activeLobby;
            if (lobby == null || lobby.identifier != targetLobby || lobby.members == null) return;
            if (SteamMatchmaking.GetLobbyOwner(lobby.id).m_SteamID != host) { Fail("Replacement lobby host changed."); return; }
            if (!localPublished) { snapshot.RestoreLocalMember(lobby); VanillaBridge.RestoreMode(snapshot); localPublished = true; }
            if (protocol.IsHost && !restored && snapshot.ValidateHumanRoster(lobby) == null)
            {
                snapshot.RestoreHost(lobby); VanillaBridge.RefreshHost(); restored = true; Report("LOBBY_RESTORED");
                return; // Normal Steam refresh must make the published state observable first.
            }
            string invalid = snapshot.ValidateLobby(lobby, protocol.IsHost);
            if (invalid != null) { notice = "Restart.LobbyInvalid"; return; }
            if (SettingsFingerprint() != settingsHash) { Fail("Host Modsettings differ after lobby return; automatic start blocked."); return; }
            if (!verified)
            {
                // Ready through Vanilla and its public admission guards; never set the Ready field directly.
                if (!VanillaBridge.IsReady())
                {
                    if (!TryIssueReady()) return;
                }
                if (!VanillaBridge.IsReady()) return;
                verified = true; Report("LOBBY_VERIFIED digest=" + digest); Apply(protocol.Verified(Now));
            }
        }
        private bool TryIssueReady()
        {
            if (!productionReady || Now < nextReadyAttempt) return false;
            // A different guard can still reject a ready attempt. Yield before retrying
            // so asynchronous admission cannot flood lobby chat once per rendered frame.
            nextReadyAttempt = Now + Stopwatch.Frequency;
            ClickReady(); return true;
        }
        private static void ClickReady() => MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("Ready");
        private string LaunchGuard(LobbyLaunchGuardContext context)
        {
            if (StopBarrier.Requested && !Active) return RestartText.Get("Restart.FailedGuard");
            if (!Active) return null;
            if (protocol.Phase != RestartPhase.Joining && protocol.Phase != RestartPhase.Starting) return RestartText.Get("Restart.TransitionPending");
            if (context.Operation == LobbyLaunchOperation.Start && (!allowStart || protocol.Phase != RestartPhase.Starting))
                return RestartText.Get("Restart.WaitStart");
            return snapshot.ValidateLobby(Platform_Multiplayer.Instance.activeLobby, protocol.IsHost) != null
                ? RestartText.Get("Restart.LobbyInvalid")
                : (SettingsFingerprint() == settingsHash ? null : RestartText.Get("Restart.SettingsChanged"));
        }
        private void Fail(string reason)
        {
            Report("FAILED " + reason);
            if (Active || HasCompletionReceipt)
            {
                try { Apply(protocol.Abort(reason)); }
                catch (Exception error) { Report("ABORT_SEND_FAILED " + error.Message); Cancelled(); }
            }
            else if (protocol != null && protocol.IsTerminal && !cancellationHandled) Cancelled();
        }
        private void Cancelled()
        {
            if (cancellationHandled) return;
            cancellationHandled = true;
            bool committed = protocol.IsCommitted;
            notice = "Restart.Failed";
            if (protocol.Operation != null) retired.Add(protocol.Operation);
            frame.SetContinuous(false); allowStart = false; ready = !committed && ready;
            if (!committed) { StopBarrier.Release(); Volatile.Write(ref transitionCommitted, 0); }
            else
            {
                // A late Abort can arrive after the target started: park again before any
                // cleanup or recovery UI. The old parked receipt is no longer sufficient.
                StopBarrier.Request(); failureParkPending = true; frame.SetContinuous(true);
            }
            menu.Invalidate(); Report("CANCELLED committed=" + committed + " " + protocol.FailureReason);
        }
        private void ShowFailureRecovery()
        {
            frame.SetContinuous(false);
            HUD_ConfirmationPopup.ShowConfirmation(RestartText.Get("Restart.RecoveryTitle"),
                () => { StopBarrier.Request(); recoveryPending = true; frame.SetContinuous(true); }, () => { });
        }
        private ulong CurrentHost() => Platform_Multiplayer.Instance?.gameMembers?
            .Where(p => !p.skirmishAI && !p.kicked && p.isHost).Select(p => p.steamID).FirstOrDefault() ?? 0;
        private ulong[] Roster() => Platform_Multiplayer.Instance?.gameMembers?
            .Where(p => !p.skirmishAI && !p.kicked).Select(p => p.steamID).ToArray() ?? Array.Empty<ulong>();
        private static string SettingsFingerprint() => RestartSettingsFingerprint.Compute(
            ModSettingsApplication.Endpoints.Select(endpoint => new KeyValuePair<string, object>(endpoint.Key, endpoint.Value)));
        private static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
    }
}
