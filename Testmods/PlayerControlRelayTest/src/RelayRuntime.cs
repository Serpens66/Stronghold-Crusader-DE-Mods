using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using APIShared;
using APIShared.Networking;
using APIShared.Presentation;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using Steamworks;
using UnityEngine;

namespace PlayerControlRelayTest
{
    internal sealed class RelayRuntime
    {
        private const string Contract = "relay-v1";
        private readonly ManualLogSource log;
        private readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        private readonly ModApiClient api = ApiShared.ForMod(PlayerControlRelayPlugin.Guid);
        private readonly RelayLedger admission = new RelayLedger(), execution = new RelayLedger();
        private readonly RelayWorldReadiness readiness = new RelayWorldReadiness();
        private readonly List<RelayUnit> selected = new List<RelayUnit>();
        private readonly Dictionary<ulong, string> challenges = new Dictionary<ulong, string>();
        private readonly HashSet<ulong> acknowledged = new HashSet<ulong>();
        private readonly RelayPendingRequests pending = new RelayPendingRequests();
        private MessageChannel<RelayEnvelope> messages;
        private AuthenticatedChoreChannel<RelayEnvelope> chores;
        private IUiFrameWorkRegistration frame;
        private IMissionLifecycleCapability lifecycle;
        private RelayPanel panel;
        private volatile Session session, offer;
        private volatile RelayWorldReadiness.Token worldToken;
        private MissionContext starting;
        private long world, requestId;
        private string localChallenge, hostEpoch, status = "Waiting for a mission.";
        private bool initialized, readyQueued, moveArmed;
        private int selectedPlayer, lastMarkers;
        private double nextHello;

        private sealed class Peer
        {
            internal int Player; internal ulong Steam; internal bool Host;
        }
        // Immutable after publication. No Unity objects are read by Chore receivers.
        private sealed class Session
        {
            internal long World; internal string Epoch, RosterHash; internal RelayWorldReadiness.Token Token;
            internal int LocalPlayer, HostPlayer; internal ulong LocalSteam, HostSteam;
            internal Peer[] Humans; internal bool Multiplayer;
            internal int PlayerOf(ulong steam) { foreach (var p in Humans) if (p.Steam == steam) return p.Player; return 0; }
            internal ulong SteamOf(int player) { foreach (var p in Humans) if (p.Player == player) return p.Steam; return 0; }
        }

        internal RelayRuntime(ManualLogSource log) { this.log = log; }
        internal void Report(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] Relay: " + text);
        private void State(string text) { status = text; Report(text); frame?.Request(); }
        private void Post(Action action) { if (!api.Ui.TryPost(action) && Thread.CurrentThread.ManagedThreadId == mainThread)
                Report("Main-thread presentation unavailable; operation not posted."); }
        private void AssertMain() { if (Thread.CurrentThread.ManagedThreadId != mainThread) throw new InvalidOperationException("Unity-thread operation required."); }

        internal void Initialize(CrusaderLibraryLoadContext context)
        {
            AssertMain(); if (initialized) return;
            if (!RelayWorld.Initialize(context, out string unavailable)) throw new InvalidOperationException(unavailable);
            RelayInputGuard.Initialize();
            // Both types are always registered in this order, before any mission or setting.
            messages = api.Network.RegisterMessage<RelayEnvelope>("control", ReceiveMessage,
                new MessageChannelOptions<RelayEnvelope> { Snapshot = p => p?.Clone(), MaximumBodyBytes = 8192,
                    Diagnostic = d => Report(d.Reason) });
            chores = api.Network.RegisterAuthenticatedChore<RelayEnvelope>("execution", ReceiveChore,
                new AuthenticatedChoreChannelOptions<RelayEnvelope> { ValidatePayload = p => p != null &&
                    (p.Kind == RelayEnvelope.Ready || p.Kind == RelayEnvelope.Execute), Diagnostic = d => Report(d.Reason) });
            panel = new RelayPanel(PlayerControlRelayPlugin.Guid,
                () => Application.systemLanguage == SystemLanguage.German,
                player => { selectedPlayer = player; frame.Request(); },
                () => { moveArmed = true; State("Select a move destination on the map."); },
                () => Submit(RelayAction.Stop, 0, 0), ClearSelection);
            panel.SelectionGesture += SelectRectangle;
            panel.WorldTarget += CaptureTarget;
            panel.CancelRequested += () => { moveArmed = false; frame?.Request(); };
            panel.OpenChanged += open => { RelayInputGuard.SetActive(open); frame?.SetContinuous(open); frame?.Request(); };
            panel.OpenAllowed = CanOpenProxy;
            panel.Initialize();
            if (!api.Ui.TryRegisterBeforeRender("relay-panel", Tick, out frame, out string reason)) throw new InvalidOperationException(reason);
            if (!api.TryGetMissionLifecycle(out lifecycle, out var diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            initialized = true;
            if (!lifecycle.TryRegisterObserver("relay-world", Started, Ended, n => {
                if (n.Phase == MissionInitializationPhase.BeforeLoad) Retire();
            }, out diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            State("Ready; private selection and full-existing-group Move/Stop. Subsets are rejected.");
        }
        private void Started(MissionLifecycleNotification notification)
        {
            Retire();
            RelayWorldReadiness.Token next;
            lock (RelayWorld.EngineGate) {
                next = readiness.Begin(notification.Context.SessionId); worldToken = next;
                Interlocked.Exchange(ref world, notification.Context.SessionId);
            }
            Post(() => {
                if (!ReferenceEquals(worldToken, next)) return;
                panel.Reset();
                lock (RelayWorld.EngineGate) {
                    if (!ReferenceEquals(worldToken, next) || Volatile.Read(ref world) != next.World) return;
                    session = null; offer = null;
                    starting = notification.Context;
                    localChallenge = Guid.NewGuid().ToString("N"); hostEpoch = Guid.NewGuid().ToString("N");
                    challenges.Clear(); acknowledged.Clear(); pending.Clear(); selected.Clear();
                    readyQueued = false; nextHello = 0;
                    if (!readiness.Prepare(next)) return;
                }
                frame.Request();
                State("Mission ready; negotiating relay session.");
            });
        }
        private void Ended(MissionLifecycleNotification notification) => Retire();
        private void Retire()
        {
            lock (RelayWorld.EngineGate) { readiness.Reset(); worldToken = null;
                Interlocked.Exchange(ref world, 0); session = null; offer = null; }
            RelayInputGuard.SetActive(false);
            Post(() => { if (Volatile.Read(ref world) != 0) return;
                starting = null; moveArmed = false; selected.Clear(); pending.Clear();
                panel?.Reset(); frame?.SetContinuous(false); frame?.Request(); });
        }
        private bool Current(Session s) => s != null && Volatile.Read(ref world) == s.World &&
            readiness.IsPrepared(s.Token) && lifecycle.Current?.SessionId == s.World;
        private RelayEnvelope Envelope(int kind, string epoch = null) => new RelayEnvelope {
            Kind = kind, Epoch = epoch ?? "", Nonce = "", Text = Contract,
            MessagePacketId = messages.PacketId, ChorePacketId = chores.PacketId };
        private bool Compatible(RelayEnvelope p) => p != null && p.MessagePacketId == messages.PacketId && p.ChorePacketId == chores.PacketId;

        private Session CaptureSession(string epoch)
        {
            AssertMain();
            long currentWorld = Volatile.Read(ref world);
            var token = worldToken;
            if (starting == null || currentWorld == 0 || starting.SessionId != currentWorld ||
                token == null || token.World != currentWorld || !readiness.IsPrepared(token) ||
                lifecycle.Current?.SessionId != currentWorld) return null;
            var peers = new List<Peer>();
            var platform = Platform_Multiplayer.Instance;
            bool multiplayer = starting.Mode.IsRealMultiplayer;
            if (platform?.gameMembers != null)
                foreach (var member in platform.gameMembers)
                {
                    if (member == null || member.kicked || member.skirmishAI || member.steamID == 0) continue;
                    if (member.playerID < 1 || member.playerID > 8 || peers.Any(p => p.Player == member.playerID || p.Steam == member.steamID)) return null;
                    peers.Add(new Peer { Player = member.playerID, Steam = member.steamID, Host = member.isHost });
                }
            ulong self = SteamUser.GetSteamID().m_SteamID;
            int local = peers.FirstOrDefault(p => p.Steam == self)?.Player ?? starting.LocalPlayerId ?? 0;
            if (local < 1 || local > 8) return null;
            if (!multiplayer && peers.Count <= 1)
            {
                // Offline loopback has no transport identity; use only within this session.
                if (self == 0) self = 1;
                peers.Clear(); peers.Add(new Peer { Player = local, Steam = self, Host = true });
            }
            if (peers.Count == 0 || multiplayer && peers.Count < 2 || peers.Count(p => p.Host) != 1) return null;
            var me = peers.SingleOrDefault(p => p.Steam == self);
            if (me == null) return null;
            peers.Sort((a, b) => a.Player.CompareTo(b.Player));
            string roster = string.Join(";", peers.Select(p => p.Player + ":" + p.Steam + ":" + p.Host));
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(roster))).Replace("-", "");
            var host = peers.Single(p => p.Host);
            return new Session { World = currentWorld, Token = token, Epoch = epoch, RosterHash = hash, Humans = peers.ToArray(),
                LocalPlayer = me.Player, LocalSteam = self, HostPlayer = host.Player, HostSteam = host.Steam, Multiplayer = multiplayer };
        }
        private bool RosterCurrent(Session s)
        {
            var captured = CaptureSession(s.Epoch);
            if (captured == null || captured.RosterHash != s.RosterHash) { State("Roster changed; relay requires a new mission session."); return false; }
            return true;
        }
        private void SendTo(ulong steam, RelayEnvelope packet)
        {
            var s = session ?? offer;
            if (s != null && steam == s.LocalSteam) ReceiveLocal(packet, steam);
            else { var result = messages.SendToSteamPeer(packet, steam); if (!result.Submitted) State("Control send failed: " + result.Reason); }
        }
        private void ReceiveMessage(NetworkMessage<RelayEnvelope> message) => ReceiveLocal(message.Packet, message.SenderSteamId);
        private void ReceiveLocal(RelayEnvelope p, ulong sender)
        {
            AssertMain(); if (!Compatible(p) || Volatile.Read(ref world) == 0 || starting == null) return;
            var s = session ?? offer ?? CaptureSession(hostEpoch);
            if (s == null || !Current(s) || s.PlayerOf(sender) == 0 || !RosterCurrent(s)) return;
            switch (p.Kind)
            {
                case RelayEnvelope.Hello:
                    if (s.LocalPlayer != s.HostPlayer || p.Text != Contract || string.IsNullOrEmpty(p.Nonce)) return;
                    challenges[sender] = p.Nonce;
                    var proposal = Envelope(RelayEnvelope.Offer, hostEpoch);
                    proposal.Nonce = p.Nonce; proposal.Player = s.PlayerOf(sender); proposal.Text = s.RosterHash;
                    SendTo(sender, proposal); return;
                case RelayEnvelope.Offer:
                    if (sender != s.HostSteam || p.Nonce != localChallenge || p.Player != s.LocalPlayer ||
                        string.IsNullOrEmpty(p.Epoch) || p.Text != s.RosterHash) return;
                    lock (RelayWorld.EngineGate) {
                        if (session != null && session.Epoch != p.Epoch) return;
                        offer = CaptureSession(p.Epoch);
                    }
                    var ack = Envelope(RelayEnvelope.Ack, p.Epoch); ack.Nonce = localChallenge; ack.Text = s.RosterHash;
                    SendTo(s.HostSteam, ack); return;
                case RelayEnvelope.Ack:
                    if (s.LocalPlayer != s.HostPlayer || p.Epoch != hostEpoch || p.Text != s.RosterHash ||
                        !challenges.TryGetValue(sender, out var challenge) || challenge != p.Nonce) return;
                    acknowledged.Add(sender);
                    if (s.Humans.All(peer => acknowledged.Contains(peer.Steam))) QueueReady(s);
                    return;
                case RelayEnvelope.Request:
                    HandleRequest(s, p, sender); return;
                case RelayEnvelope.Reply:
                    if (session == null || p.Command == null || p.Epoch != session.Epoch ||
                        p.Command.Requester != session.LocalSteam || !pending.TryGet(p.Command.Owner, out var sent) ||
                        !sent.ComputeDigest().SequenceEqual(p.Command.ComputeDigest())) return;
                    int expected = Responsible(session, p.Command.Owner);
                    if (expected == 0 || session.SteamOf(expected) != sender) return;
                    State("Request " + sent.RequestId + ": " + (RelayOutcome)p.Outcome + " — " + p.Text);
                    if ((RelayOutcome)p.Outcome != RelayOutcome.Submitted && (RelayOutcome)p.Outcome != RelayOutcome.Accepted)
                        pending.RemoveIfMatches(sent);
                    return;
            }
        }
        private void QueueReady(Session s)
        {
            if (readyQueued || session != null) return;
            var packet = Envelope(RelayEnvelope.Ready, hostEpoch); packet.Text = s.RosterHash;
            var result = chores.Send(packet); readyQueued = result.Submitted;
            if (!result.Submitted) State("Cannot synchronize relay readiness: " + result.Reason);
        }
        private int Responsible(Session s, int owner)
        {
            if (s.SteamOf(owner) != 0) return owner;
            lock (RelayWorld.EngineGate) return GamePlayerManagerAPI.Instance.IsAIPlayer(owner) ? s.HostPlayer : 0;
        }
        private void HandleRequest(Session s, RelayEnvelope p, ulong sender)
        {
            if (!ReferenceEquals(session, s) || p.Command == null || p.Epoch != s.Epoch || p.Command.Requester != sender ||
                Responsible(s, p.Command.Owner) != s.LocalPlayer) return;
            var command = p.Command;
            string reason = "Relay session changed."; bool duplicate; RelayOutcome outcome;
            lock (RelayWorld.EngineGate)
            {
                if (!Current(s) || !admission.Reserve(command, out duplicate, out reason)) { Reply(s, command, RelayOutcome.Rejected, reason); return; }
                if (duplicate) { admission.TryGet(command, out outcome, out reason); Reply(s, command, outcome, reason); return; }
                if (!RelayWorld.CanControl(s.PlayerOf(sender), command.Owner) || !RelayWorld.TryPlan(command, out _, out reason))
                {
                    reason = reason ?? "Consumer team policy rejected this request.";
                    admission.Record(command, RelayOutcome.Rejected, reason); Reply(s, command, RelayOutcome.Rejected, reason); return;
                }
            }
            var authorized = Envelope(RelayEnvelope.Execute, s.Epoch); authorized.Player = command.Owner; authorized.Command = command;
            var result = chores.Send(authorized);
            outcome = result.Submitted ? RelayOutcome.Submitted : RelayOutcome.Uncertain;
            reason = result.Submitted ? "Queued for synchronized execution; not an arrival acknowledgement." : result.Reason;
            lock (RelayWorld.EngineGate) admission.Record(command, outcome, reason);
            // A zero-delay native send can execute reentrantly before Send returns.
            // Keep that terminal result instead of publishing a late Submitted reply.
            lock (RelayWorld.EngineGate) admission.TryGet(command, out outcome, out reason);
            Reply(s, command, outcome, reason);
        }
        private void Reply(Session s, RelayCommand command, RelayOutcome outcome, string reason)
        {
            var reply = Envelope(RelayEnvelope.Reply, s.Epoch); reply.Command = command; reply.Player = command.Owner;
            reply.Outcome = (int)outcome; reply.Text = (reason ?? "").Substring(0, Math.Min(480, (reason ?? "").Length));
            if (Thread.CurrentThread.ManagedThreadId == mainThread) SendTo(command.Requester, reply);
            else Post(() => { if (Current(s) && ReferenceEquals(session, s)) SendTo(command.Requester, reply); });
        }
        private void ReceiveChore(AuthenticatedChoreMessage<RelayEnvelope> message)
        {
            var packet = message.Packet;
            if (!Compatible(packet)) return;
            lock (RelayWorld.EngineGate)
            {
                if (packet.Kind == RelayEnvelope.Ready)
                {
                    var candidate = offer;
                    if (!Current(candidate) || packet.Epoch != candidate.Epoch || packet.Text != candidate.RosterHash ||
                        message.SenderPlayerId != candidate.HostPlayer || session != null) return;
                    admission.Clear(candidate.Epoch); execution.Clear(candidate.Epoch); session = candidate;
                    Post(() => { if (ReferenceEquals(session, candidate)) State("Relay ready. Select units; Move/Stop executes only complete existing groups."); });
                    return;
                }
                var s = session; var command = packet.Command;
                if (!Current(s) || command == null || command.Epoch != s.Epoch || s.PlayerOf(command.Requester) == 0) return;
                int responsible = s.SteamOf(command.Owner) != 0 ? command.Owner :
                    GamePlayerManagerAPI.Instance.IsAIPlayer(command.Owner) ? s.HostPlayer : 0;
                if (message.SenderPlayerId != responsible || responsible == 0) return;
                if (!execution.Reserve(command, out bool duplicate, out string reason))
                {
                    if (s.LocalPlayer == responsible) {
                        admission.Record(command, RelayOutcome.Rejected, reason);
                        Reply(s, command, RelayOutcome.Rejected, reason);
                    }
                    return;
                }
                if (duplicate)
                {
                    if (s.LocalPlayer == responsible && execution.TryGet(command, out var previousOutcome, out var previousReason))
                        Reply(s, command, previousOutcome, previousReason);
                    return;
                }
                // Authorization was accepted by the real owner (or the host for AI).
                // Later revocation does not retract an already submitted Vanilla-like order.
                bool success = false;
                try { success = RelayWorld.Execute(command, () => Current(s) && ReferenceEquals(session, s), out reason); }
                catch (Exception error) { reason = "Native execution failed; no retry: " + error.Message; }
                var outcome = success ? RelayOutcome.Executed : RelayOutcome.Uncertain;
                execution.Record(command, outcome, reason);
                if (s.LocalPlayer == responsible) admission.Record(command, outcome, reason);
                string captured = "tick=" + message.ScheduledTick + " request=" + command.RequestId + " owner=" + command.Owner + " " + outcome + ": " + reason;
                Post(() => { Report(captured); if (ReferenceEquals(session, s) && command.Requester == s.LocalSteam) {
                    if (pending.RemoveIfMatches(command)) State(captured); } });
                if (s.LocalPlayer == responsible) Reply(s, command, outcome, reason);
            }
        }
        private void Submit(RelayAction action, int x, int y)
        {
            AssertMain(); var s = session;
            if (!Current(s) || !RosterCurrent(s)) { State("Relay session is not ready."); return; }
            if (selected.Count == 0) { State("Select units first."); return; }
            moveArmed = false;
            foreach (var group in selected.GroupBy(unit => unit.Owner))
            {
                if (pending.ContainsOwner(group.Key)) { State("An earlier request for player " + group.Key + " is still pending; no automatic retry."); continue; }
                int executor = Responsible(s, group.Key);
                if (executor == 0) { State("No active owner or AI host for player " + group.Key); continue; }
                var command = new RelayCommand(s.Epoch, group.Key, s.LocalSteam, ++requestId, action, x, y, group.ToArray());
                if (!pending.TryAdd(command)) continue;
                var packet = Envelope(RelayEnvelope.Request, s.Epoch); packet.Command = command; packet.Player = group.Key;
                State("Request " + command.RequestId + " sent to player " + executor + ".");
                SendTo(s.SteamOf(executor), packet);
            }
        }
        private void ClearSelection() { selected.Clear(); moveArmed = false; frame?.Request(); }
        private void CaptureTarget(float x, float y)
        {
            AssertMain();
            if (!moveArmed || !panel.IsOpen || !Current(session) || GameMap.instance == null) return;
            // Convert the captured screen point, never a later cached mouse position.
            var position = Vector3.zero; var cell = Vector3Int.zero; int depth = 0;
            GameMap.instance.CalcMapTileFromMousePos(new Vector3(x, Screen.height - y, 0), ref position, ref cell, ref depth);
            var tile = GameMap.instance.getMapTile(cell.x, cell.y);
            if (tile == null) { State("Destination is outside the map."); return; }
            Submit(RelayAction.Move, tile.gameMapX, tile.gameMapY);
        }
        private bool CanOpenProxy()
        {
            AssertMain();
            bool idle = Current(session) && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2) &&
                MainControls.instance != null && MainControls.instance.CurrentAction == 0 &&
                EditorDirector.instance != null && EditorDirector.instance.peekleftMouseStateForEngine() == 0 &&
                (TroopSelector.instance == null || !TroopSelector.instance.selection_on && !TroopSelector.instance.selection_established);
            if (!idle) State("Finish the current native mouse gesture/placement before opening the relay.");
            return idle;
        }
        private void SelectRectangle(float startX, float startY, float endX, float endY, bool add)
        {
            AssertMain(); var s = session;
            if (!Current(s) || !panel.IsOpen) return;
            if (!Monitor.TryEnter(RelayWorld.EngineGate)) { State("Simulation busy; repeat selection."); return; }
            try
            {
                var units = RelayWorld.SnapshotLiveUnits();
                bool click = Math.Abs(endX - startX) < 5 && Math.Abs(endY - startY) < 5;
                var found = new List<RelayUnit>(); float nearest = 22 * 22;
                foreach (var unit in units)
                {
                    if (selectedPlayer != 0 && unit.Owner != selectedPlayer || !RelayWorld.CanControl(s.LocalPlayer, unit.Owner)) continue;
                    if (!Project(unit, out var point)) continue;
                    if (click)
                    {
                        float distance = (point.X - endX) * (point.X - endX) + (point.Y - endY) * (point.Y - endY);
                        if (distance < nearest) { nearest = distance; found.Clear(); found.Add(unit); }
                    }
                    else if (point.X >= Math.Min(startX, endX) && point.X <= Math.Max(startX, endX) &&
                        point.Y >= Math.Min(startY, endY) && point.Y <= Math.Max(startY, endY)) found.Add(unit);
                }
                var next = add ? new List<RelayUnit>(selected) : new List<RelayUnit>();
                foreach (var unit in found) if (!next.Contains(unit)) next.Add(unit);
                if (next.Count > RelayCommand.MaximumUnits) { State("Selection exceeds 64 units; selection was not shortened."); return; }
                selected.Clear(); selected.AddRange(next);
                State(selected.Count + " private unit selections. Partial native groups will be rejected.");
            }
            finally { Monitor.Exit(RelayWorld.EngineGate); }
        }
        private static unsafe bool Project(RelayUnit unit, out RelayMarker marker)
        {
            marker = default;
            if (!RelayWorld.ResolveUnit(unit, out _, out _) || Camera.main == null) return false;
            var position = APIShared.UnitAccess.TryGetById(unit.Slot, out _, out _)
                ? GameUnitManagerAPI.Instance.GetCurrentUnityPosition(unit.Slot) : default;
            if (position == default(System.Numerics.Vector3)) return false; // No Unity sprite snapshot available.
            var point = Camera.main.WorldToScreenPoint(new Vector3(position.X, position.Y, position.Z));
            if (point.z < 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) return false;
            marker = new RelayMarker(point.x, Screen.height - point.y); return true;
        }
        private void Tick()
        {
            AssertMain();
            if (starting == null || Volatile.Read(ref world) == 0) { frame.SetContinuous(false); return; }
            if (!readiness.IsPrepared(worldToken) || starting.SessionId != Volatile.Read(ref world)) {
                frame.RequestAfter(TimeSpan.FromMilliseconds(100)); return;
            }
            try
            {
                var s = session;
                if (s == null && Time.realtimeSinceStartup >= nextHello)
                {
                    nextHello = Time.realtimeSinceStartup + 1;
                    var candidate = CaptureSession(hostEpoch);
                    if (candidate != null)
                    {
                        lock (RelayWorld.EngineGate) {
                            if (Current(candidate) && candidate.LocalPlayer == candidate.HostPlayer && offer == null) offer = candidate;
                        }
                        var hello = Envelope(RelayEnvelope.Hello); hello.Nonce = localChallenge;
                        if (candidate.LocalPlayer == candidate.HostPlayer) ReceiveLocal(hello, candidate.LocalSteam);
                        else SendTo(candidate.HostSteam, hello);
                    }
                }
                var choices = new List<RelayPlayerChoice> { new RelayPlayerChoice(0, "All authorized owners / Alle freigegebenen Spieler") };
                if (s != null && Monitor.TryEnter(RelayWorld.EngineGate))
                {
                    try
                    {
                        for (int player = 1; player <= 8; player++)
                            if (RelayWorld.CanControl(s.LocalPlayer, player) && (s.SteamOf(player) != 0 || GamePlayerManagerAPI.Instance.IsAIPlayer(player)))
                                choices.Add(new RelayPlayerChoice(player, "Player " + player + (s.SteamOf(player) != 0 ? " (human)" : " (AI / host)")));
                        if (panel.IsOpen && lastMarkers != Time.frameCount)
                        {
                            lastMarkers = Time.frameCount;
                            var markers = new List<RelayMarker>();
                            foreach (var unit in selected) if (Project(unit, out var marker)) markers.Add(marker);
                            panel.SetMarkers(markers.ToArray());
                        }
                    }
                    finally { Monitor.Exit(RelayWorld.EngineGate); }
                }
                panel.SetViewport(Screen.width, Screen.height);
                panel.Refresh(new RelayPanelState(Current(s), status, choices.ToArray(), selectedPlayer, selected.Count,
                    string.Join(", ", selected.Select(u => u.Slot + "@" + u.Owner)), moveArmed));
                frame.SetContinuous(panel.IsOpen);
                if (s == null) frame.RequestAfter(TimeSpan.FromMilliseconds(500));
                else if (!panel.IsOpen) frame.RequestAfter(TimeSpan.FromMilliseconds(250));
            }
            catch (Exception error) { State("Relay presentation unavailable: " + error.Message); frame.RequestAfter(TimeSpan.FromSeconds(1)); }
        }
    }
}
