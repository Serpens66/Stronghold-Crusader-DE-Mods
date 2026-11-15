using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using APIShared;
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
        private readonly HashSet<ulong> compatible = new HashSet<ulong>();
        private readonly HashSet<string> retired = new HashSet<string>(StringComparer.Ordinal);
        private string session, digest, settingsHash, notice = "Noch keine unterstützte Partie erfasst.";
        private ulong self, host, capturedLobby, targetLobby;
        private bool initialized, ready, stopped, localPublished, restored, verified, allowStart, cancellationHandled, recoveryPending;
        private bool lobbyCreated;
        private long helloUntil, nextHello;
        private int gameOverSignal;
        private Action<string, string, int> chat;
        private long Now => Stopwatch.GetTimestamp();
        private bool Active => protocol != null && !protocol.IsTerminal;

        internal RestartRuntime(ManualLogSource log) { this.log = log; }
        internal void Report(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] MP_RESTART_TEST " + text);
        private void Main() { if (Thread.CurrentThread.ManagedThreadId != unityThread) throw new InvalidOperationException("Unity thread required."); }

        internal void Initialize()
        {
            Main(); if (initialized) return;
            api = ApiShared.ForMod(RestartPlugin.Guid);
            messages = api.Network.RegisterMessage<RestartWire>("restart-v1", Receive,
                new MessageChannelOptions<RestartWire> { Snapshot = p => p?.Copy(), MaximumBodyBytes = 512,
                    Diagnostic = d => Report("TRANSPORT " + d.Reason) });
            if (!api.Ui.TryRegisterBeforeRender("restart-transition", Tick, out frame, out string reason)) throw new InvalidOperationException(reason);
            if (!api.TryGetMissionLifecycle(out lifecycle, out var diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            if (!lifecycle.TryRegisterObserver("restart-session", Started, Ended, null, out diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            if (!IngameMenuActions.TryRegister(RestartPlugin.Guid, "restart", MenuState, Confirm,
                new IngameMenuActionOptions(IngameMenuRow.RestartMission, IngameMenuSlot.Left), out menu, out reason))
                throw new InvalidOperationException(reason);
            foreach (var operation in new[] { MissionOverOperation.State, MissionOverOperation.Text })
                if (!MissionOverEvents.TryRegister(operation, RestartPlugin.Guid, "transition-" + operation,
                    args => {
                        if (Volatile.Read(ref transitionCommitted) != 0) args.SkipOriginalFunction = true;
                        else Volatile.Write(ref gameOverSignal, 1);
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
            initialized = true;
            if (api.TryGetMissionPlayStateReadiness(out var playState, out diagnostic))
                playState.TryRegisterObserver("restart-first-state", n => Report("FIRST_PLAYSTATE session=" + n.Context.SessionId + " realMP=" + n.Context.Mode.IsRealMultiplayer), out diagnostic);
            SmokeAutomation.TryStart(api, log, Report);
            Report("INITIALIZED version=0.1.0 barrierInstalled=" + StopBarrier.Installed + "; new games only; install identical package on every peer.");
        }
        private int transitionCommitted;
        private static void BeforeLaunch(EngineInterface.MultiplayerSetupData MPSetupData, FileHeader map, int coopTrailID, int coopMissionID)
        {
            var runtime = RestartPlugin.Runtime;
            if (runtime == null || !runtime.initialized) return;
            runtime.Main();
            if (StopBarrier.Requested && (!runtime.Active || runtime.protocol.Phase != RestartPhase.Starting || !runtime.allowStart))
                throw new InvalidOperationException("A failed or incomplete restart must be cleaned up before starting a mission.");
            if (runtime.Active && runtime.protocol.Phase == RestartPhase.Starting)
            {
                if (!runtime.allowStart || runtime.snapshot.ValidateSource() != null ||
                    runtime.snapshot.ValidateLobby(Platform_Multiplayer.Instance.activeLobby, runtime.protocol.IsHost) != null ||
                    SettingsFingerprint() != runtime.settingsHash)
                    throw new InvalidOperationException("Restart source or host Modsettings changed before native loading.");
            }
            runtime.capturedLobby = Platform_Multiplayer.Instance.activeLobby?.identifier ?? 0;
            // Capture inputs, not a second mission detector. Lifecycle owns all readiness/end decisions.
            runtime.snapshot = null;
            runtime.Capture(MPSetupData, map, coopTrailID, coopMissionID);
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
            runtime.notice = "Aus Spielständen geladene Partien werden im ersten Prototyp nicht unterstützt.";
            runtime.menu?.Invalidate();
        }
        private void Capture(EngineInterface.MultiplayerSetupData setup, FileHeader map, int trail, int mission)
        {
            try
            {
                snapshot = LaunchSnapshot.Capture(setup, map, trail, mission);
                settingsHash = SettingsFingerprint();
                digest = Hash(snapshot.Digest + ":" + settingsHash);
                session = capturedLobby.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + digest;
                chat = VanillaBridge.Chat();
                Report("CAPTURE session=" + session + " humans=" + string.Join(",", snapshot.HumanIds));
            }
            catch (Exception error) { snapshot = null; notice = "Startdaten konnten nicht vollständig erfasst werden: " + error.Message; Report("CAPTURE_REJECTED " + error); }
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
                if (protocol.Phase != RestartPhase.Starting) { Fail("Unexpected mission replacement."); return; }
                var completion = protocol.Complete();
                if (!completion.Accepted) { Fail(completion.Rejection); return; }
                Report("MANAGED_START_COMPLETE session=" + n.Context.SessionId);
                if (protocol.Operation != null) retired.Add(protocol.Operation);
            }
            protocol = null; Volatile.Write(ref transitionCommitted, 0); StopBarrier.Release();
            stopped = localPublished = restored = verified = lobbyCreated = allowStart = cancellationHandled = false;
            targetLobby = 0; compatible.Clear(); self = SteamUser.GetSteamID().m_SteamID;
            ready = !n.Context.IsSave && !n.Context.IsEditor && n.Context.Mode.IsRealMultiplayer && snapshot != null && capturedLobby != 0;
            Volatile.Write(ref gameOverSignal, 0);
            if (ready)
            {
                host = CurrentHost(); compatible.Add(self); notice = "Warte auf Testmod-Bestätigung aller Teilnehmer.";
                helloUntil = Now + Stopwatch.Frequency * 90L; nextHello = 0; frame.SetContinuous(true);
            }
            else { frame.SetContinuous(false); if (n.Context.IsSave) notice = "Geladene Spielstände sind im ersten Prototyp gesperrt."; }
            menu.Invalidate();
        }
        private void Ended(MissionLifecycleNotification n)
        {
            if (!api.Ui.TryPost(() => {
                if (Active && protocol.IsCommitted) return; // Our own frontend transition deliberately retires the old world.
                if (Active) Fail("Mission ended during preparation.");
                ready = false; menu.Invalidate();
            })) Report("End dispatch unavailable.");
        }
        private IngameMenuActionState MenuState()
        {
            bool visible = lifecycle?.Current?.Mode.IsRealMultiplayer == true && CurrentHost() == self;
            string reason = Eligible();
            return new IngameMenuActionState("Mission neustarten (Test)", visible, visible && reason == null,
                reason ?? "Alle Teilnehmer wechseln automatisch in eine neue Lobby. Dieselbe Mission startet mit denselben Spielern und Einstellungen erneut. Experimenteller Testmod.");
        }
        private string Eligible()
        {
            if (!ready || snapshot == null || lifecycle.Current == null) return notice;
            if (Active) return "Ein Neustart läuft bereits.";
            if (!StopBarrier.Installed || !StopBarrier.Observed) return "Der frühe Simulations-Checkpoint wurde nicht bestätigt. Spiel vollständig neu starten.";
            if (CurrentHost() != self) return "Nur der aktuelle Host darf neustarten.";
            if (!snapshot.HumanIds.All(compatible.Contains)) return "Noch nicht alle Teilnehmer haben denselben Testmod und dieselben Startdaten bestätigt.";
            return ValidateRunning();
        }
        private string ValidateRunning()
        {
            var mp = Platform_Multiplayer.Instance;
            if (!Director.instance.MultiplayerGame || !Director.instance.SimRunning) return "Die Multiplayer-Simulation läuft noch nicht.";
            if (!Roster().OrderBy(x => x).SequenceEqual(snapshot.HumanIds.OrderBy(x => x))) return "Der Teilnehmerbestand hat sich geändert.";
            if (Volatile.Read(ref gameOverSignal) != 0 || GameData.scenario.InGameoverSituation || mp.CoopContinuationLobbyID != 0 || mp.activeLobby != null)
                return "Ein Missionsende oder anderer Lobbyübergang läuft bereits.";
            if (VanillaBridge.Busy()) return "Speichern oder Resynchronisieren läuft.";
            if (SettingsFingerprint() != settingsHash) return "Missionsbezogene Modsettings wurden seit dem Start verändert.";
            return null;
        }
        private void Confirm()
        {
            Main(); if (Eligible() != null) return;
            string source = session;
            HUD_ConfirmationPopup.ShowConfirmation("Mission für alle Teilnehmer neu starten? (Test)", () => {
                if (session != source || Eligible() != null) return;
                try {
                    host = CurrentHost(); protocol = NewProtocol(); cancellationHandled = false;
                    frame.SetContinuous(true); Apply(protocol.Begin(Guid.NewGuid().ToString("N"), Now));
                } catch (Exception error) { Fail(error.ToString()); }
            }, () => { });
        }
        private RestartProtocol NewProtocol() => new RestartProtocol(self, host, snapshot.HumanIds, session, digest, Stopwatch.Frequency * 90L);
        private void Receive(NetworkMessage<RestartWire> message)
        { try { ReceiveCore(message); } catch (Exception error) { Fail(error.ToString()); } }
        private void ReceiveCore(NetworkMessage<RestartWire> message)
        {
            Main(); var wire = message.Packet; var p = wire?.Packet;
            if (p == null || snapshot == null || p.Session != session || p.Digest != digest ||
                wire.PacketId != messages.PacketId || !snapshot.HumanIds.Contains(message.SenderSteamId)) return;
            if (wire.Control != 0)
            {
                if (!ready || Active) return;
                if (compatible.Add(message.SenderSteamId)) menu.Invalidate();
                if (wire.Control == 1) Send(message.SenderSteamId, new RestartWire { Control = 2, Packet = HelloPacket() });
                return;
            }
            if (!Active)
            {
                if (!ready || retired.Contains(p.Operation) || p.Kind != RestartMessageKind.Prepare || message.SenderSteamId != CurrentHost()) return;
                host = CurrentHost(); protocol = NewProtocol(); cancellationHandled = false; frame.SetContinuous(true);
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
            if (protocol.IsCommitted) Volatile.Write(ref transitionCommitted, 1);
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
                        foreach (ulong peer in snapshot.HumanIds) if (peer != self)
                            try { Send(peer, new RestartWire { Packet = packet }); }
                            catch (Exception error) { Report("ABORT_SEND_FAILED " + error.Message); }
                } finally { Cancelled(); }
                return;
            }
            foreach (var p in step.Outbound)
                foreach (ulong peer in snapshot.HumanIds) if (peer != self) Send(peer, new RestartWire { Packet = p });
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
        private void Tick()
        {
            Main();
            try
            {
                if (recoveryPending)
                {
                    if (!StopBarrier.Quiescent) return;
                    Director.instance.EndMultiplayer(); EditorDirector.instance.stopGameSim();
                    Platform_Multiplayer.Instance.exitMP(); FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
                    recoveryPending = false; Volatile.Write(ref transitionCommitted, 0); StopBarrier.Release(); frame.SetContinuous(false);
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
                    Director.instance.EndMultiplayer(); EditorDirector.instance.stopGameSim();
                    VanillaBridge.ClearPendingExit(); stopped = true;
                    Report("STOPPED workerParked=true callbacksDrained=true"); Apply(protocol.Stopped(Now));
                }
                if (stopped && Platform_Multiplayer.Instance.activeLobby == null) VanillaBridge.DrainRestart(messages.PacketId);
                if (lobbyCreated && protocol.Phase == RestartPhase.WaitingLobby)
                {
                    lobbyCreated = false;
                    Apply(protocol.PublishLobby(targetLobby, Now)); // CreateLobbyResult's flush already returned.
                }
                if (protocol.Phase == RestartPhase.Joining) VerifyLobby();
            }
            catch (Exception error) { Fail(error.ToString()); }
        }
        private void CreateLobby()
        {
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
            targetLobby = protocol.LobbyId;
            var mp = Platform_Multiplayer.Instance;
            Report("OPEN_LOBBY id=" + targetLobby);
            if (protocol.IsHost) VanillaBridge.OpenHost(snapshot, mp.activeLobby);
            else { mp.SetInviteLobbyID(targetLobby); mp.ResumeInvite(); }
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
            if (invalid != null) { notice = invalid; return; }
            if (SettingsFingerprint() != settingsHash) { Fail("Host Modsettings differ after lobby return; automatic start blocked."); return; }
            if (!verified)
            {
                // Ready through Vanilla and its public admission guards; never set the Ready field directly.
                if (!VanillaBridge.IsReady()) MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("Ready");
                if (!VanillaBridge.IsReady()) return;
                verified = true; Report("LOBBY_VERIFIED digest=" + digest); Apply(protocol.Verified(Now));
            }
        }
        private string LaunchGuard(LobbyLaunchGuardContext context)
        {
            if (StopBarrier.Requested && !Active) return "Test-Neustart fehlgeschlagen. Zuerst zum Hauptmenü zurückkehren oder das Spiel beenden.";
            if (!Active) return null;
            if (protocol.Phase != RestartPhase.Joining && protocol.Phase != RestartPhase.Starting) return "Test-Neustart: Lobbyübergang läuft.";
            if (context.Operation == LobbyLaunchOperation.Start && (!allowStart || protocol.Phase != RestartPhase.Starting))
                return "Test-Neustart: Warte auf die geprüften Bestätigungen aller Teilnehmer.";
            return snapshot.ValidateLobby(Platform_Multiplayer.Instance.activeLobby, protocol.IsHost) ??
                (SettingsFingerprint() == settingsHash ? null : "Test-Neustart: Modsettings stimmen nicht überein.");
        }
        private void Fail(string reason)
        {
            Report("FAILED " + reason);
            if (Active)
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
            notice = "Neustart abgebrochen: " + protocol.FailureReason;
            if (protocol.Operation != null) retired.Add(protocol.Operation);
            frame.SetContinuous(false); allowStart = false; ready = !committed && ready;
            if (!committed) { StopBarrier.Release(); Volatile.Write(ref transitionCommitted, 0); }
            else
            {
                // No rollback claim: keep a possibly partial old world parked. Application quit
                // still releases the worker in Director.OnDestroy. A regular new game may resume it.
                StopBarrier.AllowNetwork();
                HUD_ConfirmationPopup.ShowConfirmation(notice + " Die alte Partie kann nicht fortgesetzt werden. Zum Hauptmenü?",
                    () => { StopBarrier.Request(); recoveryPending = true; frame.SetContinuous(true); }, () => { });
            }
            menu.Invalidate(); Report("CANCELLED committed=" + committed + " " + notice);
        }
        private ulong CurrentHost() => Platform_Multiplayer.Instance?.gameMembers?
            .Where(p => !p.skirmishAI && !p.kicked && p.isHost).Select(p => p.steamID).FirstOrDefault() ?? 0;
        private ulong[] Roster() => Platform_Multiplayer.Instance?.gameMembers?
            .Where(p => !p.skirmishAI && !p.kicked).Select(p => p.steamID).ToArray() ?? Array.Empty<ulong>();
        private static string SettingsFingerprint()
        {
            using (var bytes = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(bytes))
            {
                foreach (var endpoint in ModSettingsApplication.Endpoints.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.Write(endpoint.Key);
                    var snapshot = endpoint.Value.System_CreateCurrentWorkingSnapshot();
                    foreach (var property in ModSettingsApplication.GetHostProperties(endpoint.Value).OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        writer.Write(property.Name);
                        if (!snapshot.TryGetValue(property.Name, out byte[] value) || value == null) writer.Write(-1);
                        else { writer.Write(value.Length); writer.Write(value); }
                    }
                }
                writer.Flush(); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "");
            }
        }
        private static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
    }
}
