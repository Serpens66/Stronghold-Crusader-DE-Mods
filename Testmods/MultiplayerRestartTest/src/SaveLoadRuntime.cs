using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Lobby;
using APIShared.Networking;
using APIShared.Presentation;
using APIShared.Savegames;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Steamworks;

namespace MultiplayerSaveLoadTest
{
    // Only the joint return is custom. Once every peer joined, the native save
    // requester and its normal SDK contributions own eligibility and loading.
    internal sealed class SaveLoadRuntime
    {
        private readonly ManualLogSource log;
        private readonly int unityThread = Thread.CurrentThread.ManagedThreadId;
        private readonly HashSet<ulong> compatible = new HashSet<ulong>();
        private readonly HashSet<string> retired = new HashSet<string>(StringComparer.Ordinal);
        private ModApiClient api;
        private IMissionLifecycleCapability lifecycle;
        private MessageChannel<LoadWire> channel;
        private IUiFrameWorkRegistration frame;
        private IngameMenuActionRegistration menu;
        private Harmony hooks;
        private LoadProtocol protocol;
        private ulong nextLobby, sourceLobby, self, host;
        private ulong[] peers;
        private long sourceMission, helloUntil, nextHello;
        private bool initialized, stopped, joined, released, failurePending, recovering;
        private bool creating, created;
        private string creationError;
        private LoadPacket releaseRetry, acknowledgementRetry;
        private long nextReleaseRetry, nextAcknowledgementRetry, acknowledgementDeadline;
        private ulong readyLobby;
        private long readyDeadline, nextReady;
        private int readyAttempts;
        private bool readyPending, loadMenuPending, ownReadyInvocation;
        private Action<string, string, int> chat;
        private long Now => Stopwatch.GetTimestamp();
        private bool Active => protocol != null && !protocol.IsTerminal;

        internal SaveLoadRuntime(ManualLogSource logger) { log = logger; }
        internal void Report(string message) => log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] MP_SAVE_LOAD " + message);
        private void Main() { if (Thread.CurrentThread.ManagedThreadId != unityThread) throw new InvalidOperationException("Unity thread required."); }

        internal void Initialize()
        {
            Main(); if (initialized) return;
            api = ApiShared.ForMod(SaveLoadPlugin.Guid);
            channel = api.Network.RegisterMessage<LoadWire>("save-load-v1", Receive,
                new MessageChannelOptions<LoadWire> { Snapshot = value => value?.Copy(), MaximumBodyBytes = 512,
                    Diagnostic = value => Report("TRANSPORT " + value.Reason) });
            if (!api.Ui.TryRegisterBeforeRender("save-load-return", Tick, out frame, out string reason)) throw new InvalidOperationException(reason);
            if (!api.TryGetMissionLifecycle(out lifecycle, out var diagnostic) ||
                !lifecycle.TryRegisterObserver("save-load-session", Started, Ended, null, out diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            if (!IngameMenuActions.TryOverrideVanillaAction(SaveLoadPlugin.Guid, "load", IngameMenuRow.Load,
                LoadEnabled, Confirm, out menu, out reason)) throw new InvalidOperationException(reason);
            if (!LobbyLaunchGuards.TryRegister(SaveLoadPlugin.Guid, "return-pending",
                _ => Active || failurePending || recovering ? LoadText.Get("Load.Transition") : null, out reason))
                throw new InvalidOperationException(reason);
            if (!SaveLoadDialogEvents.IsAvailable) throw new InvalidOperationException("Native save requester observations are unavailable.");
            SaveLoadDialogEvents.Register(SaveLoadPlugin.Guid, "load-intent",
                new SaveLoadDialogCallbacks { Opened = SaveRequesterOpened });
            if (!LobbyActionEvents.TryRegisterObserver(SaveLoadPlugin.Guid, "ready-intent", LobbyActionObserved, out reason))
                throw new InvalidOperationException(reason);
            LobbyBridge.Validate();
            hooks = new Harmony(SaveLoadPlugin.Guid + ".save-load");
            foreach (string name in new[] { "StartGame", "StartSave" })
                hooks.Patch(AccessTools.Method(typeof(Platform_Multiplayer), name),
                    prefix: new HarmonyMethod(typeof(SaveLoadRuntime), nameof(BeforeStart)));
            var creation = typeof(Platform_Multiplayer).GetMethod("CreateLobbyResult",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(LobbyCreated_t), typeof(bool) }, null);
            if (creation == null) throw new MissingMethodException("Platform_Multiplayer.CreateLobbyResult");
            hooks.Patch(creation, finalizer: new HarmonyMethod(typeof(SaveLoadRuntime), nameof(CreationReturned)));
            initialized = true;
            Report("INITIALIZED; replaces restart experiment; no save compatibility rules added.");
        }

        private static void BeforeStart(Platform_Multiplayer __instance)
        {
            var runtime = SaveLoadPlugin.Runtime;
            if (runtime == null || !runtime.initialized) return;
            runtime.Main();
            if (runtime.Active || runtime.failurePending || runtime.recovering ||
                LoadBarrier.Requested && !LoadBarrier.LaunchAllowed)
                throw new InvalidOperationException("The group lobby transition has not finished.");
            // Managed Started can precede the native MP handshake. Adopt resets the
            // old protocol flags then, but only startSimThread releases the parked
            // worker. A later Vanilla retry/start must retain that launch permission.
            runtime.nextLobby = __instance.activeLobby?.identifier ?? 0;
            runtime.acknowledgementRetry = null;
            runtime.readyPending = false;
            runtime.loadMenuPending = false;
        }
        private static Exception CreationReturned(Exception __exception)
        {
            var runtime = SaveLoadPlugin.Runtime;
            if (runtime != null && runtime.creating)
            {
                runtime.creating = false;
                if (__exception != null)
                {
                    runtime.creationError = __exception.Message;
                    runtime.Report("LOBBY_CALLBACK_FAILED " + __exception.Message);
                }
                runtime.frame?.Request();
            }
            return __exception;
        }
        private void Started(MissionLifecycleNotification notification)
        {
            if (!api.Ui.TryPost(() => Adopt(notification))) Report("START_DISPATCH_UNAVAILABLE");
        }
        private void Adopt(MissionLifecycleNotification notification)
        {
            Main();
            if (lifecycle.Current?.SessionId != notification.Context.SessionId) return;
            if (Active) { Fail("Unexpected mission start during group return."); return; }
            protocol = null; peers = null; compatible.Clear();
            readyPending = false;
            loadMenuPending = false;
            stopped = joined = released = failurePending = recovering = false;
            sourceMission = notification.Context.SessionId;
            sourceLobby = notification.Context.Mode.IsRealMultiplayer && !notification.Context.IsEditor ? nextLobby : 0;
            self = SteamUser.GetSteamID().m_SteamID;
            compatible.Add(self);
            helloUntil = Now + Stopwatch.Frequency * 90L; nextHello = 0;
            frame.SetContinuous(sourceLobby != 0);
            menu.Invalidate();
            Report("SESSION session=" + sourceLobby + " saved=" + notification.Context.IsSave);
        }
        private void Ended(MissionLifecycleNotification notification)
        {
            if (!api.Ui.TryPost(() => {
                if (notification.Context.SessionId != sourceMission) return;
                // Explicit retirement is part of our committed group return.
                if (protocol?.IsCommitted == true) return;
                sourceLobby = 0; compatible.Clear(); menu.Invalidate();
                if (Active) Fail("The source mission ended before the group committed.");
                else frame.SetContinuous(false);
            })) Report("END_DISPATCH_UNAVAILABLE");
        }

        private static ulong[] Humans() => Platform_Multiplayer.Instance?.gameMembers?
            .Where(member => member != null && !member.kicked && !member.skirmishAI && member.steamID != 0)
            .Select(member => member.steamID).OrderBy(value => value).ToArray() ?? Array.Empty<ulong>();
        private static ulong Host() => Platform_Multiplayer.Instance?.gameMembers?
            .Where(member => member != null && !member.kicked && !member.skirmishAI && member.isHost)
            .Select(member => member.steamID).FirstOrDefault() ?? 0;
        private bool Running() => sourceLobby != 0 && lifecycle.Current?.SessionId == sourceMission &&
            lifecycle.Current.Mode.IsRealMultiplayer && Director.instance.MultiplayerGame && Director.instance.SimRunning &&
            !GameData.scenario.InGameoverSituation && Platform_Multiplayer.Instance.CoopContinuationLobbyID == 0;
        private bool? LoadEnabled()
        {
            if (lifecycle?.Current?.Mode.IsRealMultiplayer != true) return null;
            var members = Humans();
            return initialized && !Active && !failurePending && !recovering && !creating && !LoadBarrier.Requested &&
                LoadBarrier.Installed && LoadBarrier.Observed && Running() && Host() == self &&
                members.Length >= 1 && members.All(compatible.Contains);
        }
        private void Confirm()
        {
            Main(); if (LoadEnabled() != true) return;
            ulong session = sourceLobby;
            HUD_ConfirmationPopup.ShowConfirmation(LoadText.Get("Load.ConfirmTitle"), () => {
                if (sourceLobby != session || LoadEnabled() != true) return;
                try {
                    Begin();
                    Apply(protocol.Begin(System.Guid.NewGuid().ToString("N"), Now));
                } catch (Exception error) { Fail(error.Message); }
            }, () => { });
        }
        private void Begin()
        {
            // Capture fallible local prerequisites before publishing an idle operation.
            var sourceChat = LobbyBridge.CaptureChat(Platform_Multiplayer.Instance);
            host = Host(); peers = Humans();
            protocol = new LoadProtocol(self, host, peers, sourceLobby, Stopwatch.Frequency * 90L);
            chat = sourceChat;
            stopped = joined = released = created = failurePending = recovering = false;
            creationError = null; releaseRetry = acknowledgementRetry = null;
            frame.SetContinuous(true); menu.Invalidate();
        }
        private LoadWire Hello(int control) => new LoadWire { Control = control, PacketId = channel.PacketId,
            Packet = new LoadPacket { Kind = LoadMessageKind.Prepare, Session = sourceLobby, Operation = string.Empty } };
        private void Send(ulong peer, LoadWire wire)
        {
            wire.PacketId = channel.PacketId;
            var sent = channel.SendToSteamPeer(wire, peer);
            if (!sent.Submitted) throw new InvalidOperationException("Control send failed: " + sent.Reason);
        }
        private void Receive(NetworkMessage<LoadWire> message)
        {
            try
            {
                Main();
                var wire = message.Packet; var packet = wire?.Packet;
                if (!initialized || packet == null || wire.PacketId != channel.PacketId || packet.Session != sourceLobby) return;
                if (wire.Control != 0)
                {
                    if (Active || !Running() || !Humans().Contains(message.SenderSteamId)) return;
                    if (compatible.Add(message.SenderSteamId)) menu.Invalidate();
                    if (wire.Control == 1) Send(message.SenderSteamId, Hello(2));
                    return;
                }
                // A matching timed-out receipt must still receive the host's committed
                // cancellation. It cannot authorize a new operation or a later world.
                if (protocol != null && packet.Operation == protocol.Operation &&
                    protocol.IsTerminal && peers != null && peers.Contains(message.SenderSteamId))
                {
                    Apply(protocol.Receive(message.SenderSteamId, packet, Now));
                    return;
                }
                if (protocol == null || protocol.IsTerminal)
                {
                    if (!Running() || message.SenderSteamId != Host() || packet.Kind != LoadMessageKind.Prepare ||
                        retired.Contains(packet.Operation)) return;
                    Begin();
                }
                Apply(protocol.Receive(message.SenderSteamId, packet, Now));
            }
            catch (Exception error) { Fail(error.Message); }
        }
        private void Apply(LoadStep step)
        {
            if (!step.Accepted) { Report("IGNORED " + step.Rejection); return; }
            if ((step.Effects & LoadEffect.StopLocal) != 0)
            {
                LoadBarrier.Request();
                MainViewModel.Instance.Show_HUD_IngameMenu = false;
                Report("STOP_REQUESTED");
            }
            if ((step.Effects & LoadEffect.ReleaseLocal) != 0)
            {
                // Clients must permit the ordinary native loader before acknowledging.
                released = true; LoadBarrier.AllowLaunch();
                Report("LOBBY_RELEASED id=" + protocol.LobbyId);
                ScheduleAutoReady();
            }
            bool cancelling = (step.Effects & LoadEffect.Cancelled) != 0;
            string sendFailure = null;
            try
            {
                foreach (var packet in step.Outbound)
                {
                    if (packet.Kind == LoadMessageKind.ReleaseLobby)
                    {
                        if (!LobbyBridge.IsLobbyReady(protocol.LobbyId, host, peers, self))
                            throw new InvalidOperationException("The loading-lobby group changed before release.");
                        releaseRetry = packet; nextReleaseRetry = Now + Stopwatch.Frequency;
                    }
                    foreach (ulong peer in protocol.Recipients(packet))
                        try
                        {
                            Send(peer, new LoadWire { Packet = packet });
                            if (packet.Kind == LoadMessageKind.ReleaseAcknowledged) acknowledgementRetry = null;
                        }
                        catch (Exception error)
                        {
                            Report("SEND_FAILED " + error.Message);
                            if (packet.Kind == LoadMessageKind.ReleaseAcknowledged && protocol.IsCompleted)
                            {
                                if (acknowledgementRetry == null)
                                    acknowledgementDeadline = Now + Stopwatch.Frequency * 90L;
                                acknowledgementRetry = packet;
                                nextAcknowledgementRetry = Now + Stopwatch.Frequency;
                            }
                            else sendFailure = error.Message;
                        }
                }
            }
            finally { if (cancelling) Cancelled(); }
            if (cancelling) return;
            if (sendFailure != null) { Fail(sendFailure); return; }
            if ((step.Effects & LoadEffect.PrepareLocal) != 0)
            {
                if (!Running() || Host() != host || !protocol.RosterMatches(Humans()) || !LoadBarrier.Installed || !LoadBarrier.Observed)
                { Fail("Current host, participants or stop checkpoint changed."); return; }
                Report("PREPARED operation=" + protocol.Operation);
                Apply(protocol.Prepared(Now));
            }
            if ((step.Effects & LoadEffect.CreateLobby) != 0)
            {
                LobbyBridge.ClearOldMessages(Platform_Multiplayer.Instance);
                creating = true;
                try { LobbyBridge.CreatePrivateLobby("Multiplayer load", peers.Length, () => { created = true; }, chat); }
                catch { creating = false; throw; }
                Report("LOBBY_REQUESTED");
            }
            if ((step.Effects & LoadEffect.JoinLocal) != 0)
            {
                LobbyBridge.ClearOldMessages(Platform_Multiplayer.Instance);
                if (protocol.IsHost) LobbyBridge.OpenHost(Platform_Multiplayer.Instance.activeLobby);
                else LobbyBridge.OpenClient(protocol.LobbyId);
                LoadBarrier.AllowNetwork();
                Report("LOBBY_OPEN id=" + protocol.LobbyId);
            }
            if ((step.Effects & LoadEffect.OpenLoadMenu) != 0)
            {
                retired.Add(protocol.Operation); frame.SetContinuous(false); menu.Invalidate();
                Report("LOAD_MENU_READY id=" + protocol.LobbyId);
                // Ready guards can open a native error popup. Finish this one-shot local
                // action before installing the category popup's shared native callbacks.
                loadMenuPending = true;
                frame.RequestAfter(TimeSpan.FromSeconds(1));
            }
            if (protocol.IsCompleted)
            {
                retired.Add(protocol.Operation); frame.SetContinuous(acknowledgementRetry != null); menu.Invalidate();
            }
        }
        private void OpenNativeLoad(bool coop)
        {
            try {
                if (protocol?.IsCompleted != true || Platform_Multiplayer.Instance.activeLobby?.identifier != protocol.LobbyId) return;
                LobbyBridge.OpenLoadDialog(coop);
                Report("VANILLA_SAVE_DIALOG coop=" + coop);
            } catch (Exception error) { Report("SAVE_DIALOG_FAILED " + error); }
        }
        private void Tick()
        {
            Main();
            try
            {
                PumpReceipts();
                PumpAutoReady();
                PumpLoadMenu();
                if (failurePending || recovering)
                {
                    // CreateLobbyResult writes through its pending placeholder before invoking
                    // our callback. Do not destroy it while the Steam call is outstanding.
                    if (creating || !LoadBarrier.Quiescent) return;
                    if (recovering)
                    {
                        LobbyBridge.RecoverAfterCommitted(peers);
                        LoadBarrier.Release(); recovering = failurePending = false;
                        sourceLobby = 0; frame.SetContinuous(false); return;
                    }
                    // Retain the launch guard while the recovery choice is displayed.
                    frame.SetContinuous(false);
                    HUD_ConfirmationPopup.ShowConfirmationOKMessage(LoadText.Get("Load.RecoveryTitle"),
                        () => { recovering = true; frame.SetContinuous(true); }, LoadText.Get("Load.Failed"));
                    return;
                }
                if (!Active)
                {
                    if (acknowledgementRetry != null) return;
                    var members = Humans();
                    if (sourceLobby == 0 || members.All(compatible.Contains) || Now > helloUntil) { frame.SetContinuous(false); return; }
                    if (Now >= nextHello)
                    {
                        nextHello = Now + Stopwatch.Frequency;
                        foreach (ulong peer in members) if (peer != self) Send(peer, Hello(1));
                    }
                    return;
                }
                Apply(protocol.Tick(Now)); if (!Active) return;
                if (!creating && creationError != null)
                { string error = creationError; creationError = null; Fail("Lobby creation callback failed: " + error); return; }
                if (!protocol.IsCommitted && (!Running() || Host() != host || !protocol.RosterMatches(Humans())))
                { Fail("Source party changed before joint return."); return; }
                if (protocol.Phase == LoadPhase.Stopping && !stopped && LoadBarrier.Quiescent)
                {
                    // This order selects stopGameSim's non-MP cleanup: no delayed
                    // ExitMP coroutine may later dismantle our new lobby.
                    Director.instance.EndMultiplayer(); EditorDirector.instance.stopGameSim();
                    stopped = true;
                    Report("SOURCE_STOPPED workerParked=true callbacksDrained=true");
                    Apply(protocol.Stopped(Now));
                }
                if (stopped && LoadBarrier.Quiescent && Platform_Multiplayer.Instance.activeLobby == null)
                    LobbyBridge.DrainControl(channel.PacketId);
                if (created && !creating && protocol.Phase == LoadPhase.WaitingLobby)
                {
                    created = false;
                    var lobby = Platform_Multiplayer.Instance.activeLobby;
                    if (lobby == null || !lobby.isHost || lobby.identifier == 0)
                    { Fail("The private load lobby was not created."); return; }
                    Apply(protocol.PublishLobby(lobby.identifier, Now));
                }
                if (protocol.Phase == LoadPhase.Joining && !joined &&
                    LobbyBridge.IsLobbyReady(protocol.LobbyId, host, peers, self))
                {
                    if (protocol.IsHost)
                    {
                        var lobby = Platform_Multiplayer.Instance.activeLobby;
                        if (!SteamMatchmaking.SetLobbyJoinable(lobby.id, false) ||
                            !LobbyBridge.IsLobbyReady(protocol.LobbyId, host, peers, self))
                        { Fail("Could not seal the exact loading-lobby group."); return; }
                    }
                    joined = true;
                    Report("PARTY_JOINED id=" + protocol.LobbyId);
                    Apply(protocol.Joined(Now));
                }
            }
            catch (Exception error) { Fail(error.Message); }
        }
        private void ScheduleAutoReady()
        {
            readyLobby = protocol.LobbyId; readyAttempts = 0;
            readyDeadline = Now + Stopwatch.Frequency * 90L;
            nextReady = Now + Stopwatch.Frequency;
            readyPending = true;
            // Let the enclosing frontend update and initial settings/map refresh finish.
            frame.RequestAfter(TimeSpan.FromSeconds(1));
        }
        private void SaveRequesterOpened(SaveLoadDialogContext context)
        {
            Main();
            if (!loadMenuPending || protocol?.IsCompleted != true || !protocol.IsHost ||
                Platform_Multiplayer.Instance.activeLobby?.identifier != protocol.LobbyId ||
                (context.RequesterType != Enums.RequesterTypes.LoadMultiplayerGame &&
                 context.RequesterType != Enums.RequesterTypes.LoadMultiplayerCoopGame)) return;
            // Remember the opening event even if the user closes it between frame callbacks.
            loadMenuPending = false;
            Report("SAVE_CATEGORY_CANCELLED native requester opened.");
        }
        private void LobbyActionObserved(LobbyActionNotification notification)
        {
            Main();
            if (!readyPending || ownReadyInvocation || notification.Command != "Ready" ||
                !notification.OriginalInvoked || !notification.ReadyBefore || notification.ReadyAfter ||
                protocol?.IsCompleted != true || notification.LobbyId != readyLobby ||
                protocol.LobbyId != readyLobby || Platform_Multiplayer.Instance.activeLobby?.identifier != readyLobby) return;
            // Only an executed Ready toggle is user intent; settings/map resets are not.
            readyPending = false;
            Report("AUTO_READY_STOP manual unready.");
        }
        private void PumpAutoReady()
        {
            if (!readyPending) return;
            if (Active || failurePending || recovering || !released || protocol?.IsCompleted != true ||
                protocol.LobbyId != readyLobby)
            { readyPending = false; return; }
            if (Now < nextReady) { frame.RequestAfter(TimeSpan.FromSeconds(1)); return; }
            if (Now >= readyDeadline)
            { readyPending = false; Report("AUTO_READY_STOP timeout; manual Ready remains available."); return; }
            try
            {
                LocalReadyResult result;
                ownReadyInvocation = true;
                try { result = LobbyBridge.TryReady(readyLobby, host, peers, self); }
                finally { ownReadyInvocation = false; }
                if (result == LocalReadyResult.Ready || result == LocalReadyResult.NotCurrent)
                {
                    readyPending = false;
                    Report(result == LocalReadyResult.Ready ? "AUTO_READY_CONFIRMED" : "AUTO_READY_STOP lobby changed or launch pending.");
                    return;
                }
                // The real command retains all guards. Bound rejected attempts and back off;
                // never keep reasserting Ready after success or a later Vanilla reset.
                if (result == LocalReadyResult.Rejected && ++readyAttempts >= 5)
                { readyPending = false; Report("AUTO_READY_STOP rejected; manual Ready remains available."); return; }
                int seconds = result == LocalReadyResult.Rejected ? 1 << readyAttempts : 1;
                if (result == LocalReadyResult.Rejected) Report("AUTO_READY_RETRY attempt=" + readyAttempts);
                nextReady = Now + Stopwatch.Frequency * seconds;
                frame.RequestAfter(TimeSpan.FromSeconds(seconds));
            }
            catch (Exception error)
            { readyPending = false; Report("AUTO_READY_STOP " + error.Message); }
        }
        private void PumpLoadMenu()
        {
            if (!loadMenuPending) return;
            if (Active || failurePending || recovering || protocol?.IsCompleted != true || !protocol.IsHost ||
                Platform_Multiplayer.Instance.activeLobby?.identifier != protocol.LobbyId ||
                Director.instance.MultiplayerGame || Director.instance.SimRunning)
            { loadMenuPending = false; return; }
            try
            {
                // A manually opened native requester already fulfils this pending action.
                // Do not reopen a second dialog after its confirmation or cancellation.
                if (LobbyBridge.HasSaveRequester()) { loadMenuPending = false; return; }
                if (readyPending || LobbyBridge.HasModal())
                { frame.RequestAfter(TimeSpan.FromSeconds(1)); return; }
                loadMenuPending = false;
                Report("SAVE_CATEGORY_OPEN");
                HUD_ConfirmationPopup.ShowConfirmation(LoadText.Get("Load.CategoryTitle"),
                    () => OpenNativeLoad(true), () => OpenNativeLoad(false));
            }
            catch (Exception error)
            { loadMenuPending = false; Report("SAVE_DIALOG_FAILED " + error); }
        }
        private void PumpReceipts()
        {
            if (protocol == null) return;
            if (protocol.Phase == LoadPhase.Releasing && releaseRetry != null && Now >= nextReleaseRetry)
            {
                if (!LobbyBridge.IsLobbyReady(protocol.LobbyId, host, peers, self))
                { Fail("The loading-lobby group changed before release acknowledgement."); return; }
                nextReleaseRetry = Now + Stopwatch.Frequency;
                foreach (ulong peer in protocol.Recipients(releaseRetry))
                    Send(peer, new LoadWire { Packet = releaseRetry });
            }
            if (acknowledgementRetry == null) return;
            // A retired receipt can never terminate a subsequently started save/world.
            bool targetLobby = Platform_Multiplayer.Instance.activeLobby?.identifier == protocol.LobbyId &&
                !Director.instance.MultiplayerGame && !Director.instance.SimRunning &&
                (lifecycle.Current == null || lifecycle.Current.SessionId == sourceMission);
            if (!targetLobby) { acknowledgementRetry = null; return; }
            if (Now >= acknowledgementDeadline)
            {
                acknowledgementRetry = null;
                LoadBarrier.Request(); failurePending = true; frame.SetContinuous(true);
                Report("RELEASE_ACK_TIMEOUT; old mission remains retired.");
                return;
            }
            if (Now < nextAcknowledgementRetry) return;
            nextAcknowledgementRetry = Now + Stopwatch.Frequency;
            try
            {
                Send(host, new LoadWire { Packet = acknowledgementRetry });
                acknowledgementRetry = null;
            }
            catch (Exception error) { Report("RELEASE_ACK_RETRY " + error.Message); }
        }
        private void Fail(string reason)
        {
            Report("FAILED " + reason);
            if (!Active) return;
            // An exception before Begin/Prepare was admitted owns no source-world state.
            if (protocol.Phase == LoadPhase.Idle)
            {
                protocol = null; peers = null;
                frame.SetContinuous(false); menu.Invalidate(); return;
            }
            Apply(protocol.Abort(reason));
        }
        private void Cancelled()
        {
            retired.Add(protocol.Operation);
            if (protocol.IsCommitted)
            {
                LoadBarrier.Request(); failurePending = true; frame.SetContinuous(true);
            }
            else { LoadBarrier.Release(); frame.SetContinuous(false); }
            menu.Invalidate();
            Report("CANCELLED committed=" + protocol.IsCommitted);
        }
    }
}
