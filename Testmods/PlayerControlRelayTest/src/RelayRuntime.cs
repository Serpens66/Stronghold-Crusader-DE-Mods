using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string Contract = "relay-v4";
        private readonly ManualLogSource log;
        private readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        private readonly ModApiClient api = ApiShared.ForMod(PlayerControlRelayPlugin.Guid);
        private readonly RelayLedger admission = new RelayLedger(), execution = new RelayLedger();
        private readonly RelayWorldReadiness readiness = new RelayWorldReadiness();
        private readonly List<RelayUnit> selected = new List<RelayUnit>();
        private readonly Dictionary<ulong, string> challenges = new Dictionary<ulong, string>();
        private readonly HashSet<ulong> acknowledged = new HashSet<ulong>();
        private readonly RelayMarkers markers;
        private MessageChannel<RelayEnvelope> messages;
        private AuthenticatedChoreChannel<RelayChoreEnvelope> chores;
        private IUiFrameWorkRegistration frame;
        private IMissionLifecycleCapability lifecycle;
        private volatile Session session, offer;
        private volatile RelayWorldReadiness.Token worldToken;
        private MissionContext starting;
        private long world, requestId;
        private string localChallenge, hostEpoch;
        private bool initialized, readyQueued, inactiveReported;
        private int hasSelection, lastInputFailureHash;
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

        internal RelayRuntime(ManualLogSource log) { this.log = log; markers = new RelayMarkers(Report); }
        internal void Report(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] Relay: " + text);
        private void Post(Action action) { if (!api.Ui.TryPost(action) && Thread.CurrentThread.ManagedThreadId == mainThread)
                Report("Main-thread presentation unavailable; operation not posted."); }
        private void AssertMain() { if (Thread.CurrentThread.ManagedThreadId != mainThread) throw new InvalidOperationException("Unity-thread operation required."); }

        internal void Initialize(CrusaderLibraryLoadContext context)
        {
            AssertMain(); if (initialized) return;
            if (!RelayWorld.Initialize(context, out string unavailable)) throw new InvalidOperationException(unavailable);
            // Both types are always registered in this order, before any mission or setting.
            messages = api.Network.RegisterMessage<RelayEnvelope>("control", ReceiveMessage,
                new MessageChannelOptions<RelayEnvelope> { Snapshot = p => p?.Clone(), MaximumBodyBytes = 8192,
                    Diagnostic = d => Report(d.Reason) });
            chores = api.Network.RegisterAuthenticatedChore<RelayChoreEnvelope>("execution", ReceiveChore,
                new AuthenticatedChoreChannelOptions<RelayChoreEnvelope> { ValidatePayload = p => p?.Inner != null &&
                    (p.Inner.Kind == RelayEnvelope.Ready || p.Inner.Kind == RelayEnvelope.Execute), Diagnostic = d => Report(d.Reason) });
            if (!api.Ui.TryRegisterBeforeRender("relay-frame", Tick, out frame, out string reason)) throw new InvalidOperationException(reason);
            if (!api.TryGetMissionLifecycle(out lifecycle, out var diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            RelayInput.Initialize(OnTroopInput, OnInputFailure);
            initialized = true;
            if (!lifecycle.TryRegisterObserver("relay-world", Started, Ended, n => {
                if (n.Phase == MissionInitializationPhase.BeforeLoad) Retire();
            }, out diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            Report("Ready; allied units can be selected and ordered with the normal mouse input.");
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
                ResetSelection();
                lock (RelayWorld.EngineGate) {
                    if (!ReferenceEquals(worldToken, next) || Volatile.Read(ref world) != next.World) return;
                    session = null; offer = null;
                    starting = notification.Context;
                    localChallenge = Guid.NewGuid().ToString("N"); hostEpoch = Guid.NewGuid().ToString("N");
                    challenges.Clear(); acknowledged.Clear();
                    readyQueued = false; inactiveReported = false; nextHello = 0;
                    if (!readiness.Prepare(next)) return;
                }
                frame.Request();
                Report("Mission ready; negotiating relay session.");
            });
        }
        private void Ended(MissionLifecycleNotification notification) => Retire();
        private void Retire()
        {
            lock (RelayWorld.EngineGate) { readiness.Reset(); worldToken = null;
                Interlocked.Exchange(ref world, 0); session = null; offer = null; }
            Post(() => { if (Volatile.Read(ref world) != 0) return;
                starting = null; ResetSelection(); frame?.Request(); });
        }
        private void ResetSelection()
        {
            selected.Clear(); Volatile.Write(ref hasSelection, 0);
            markers.Reset();
            frame?.SetContinuous(false);
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
            if (captured == null || captured.RosterHash != s.RosterHash) { Report("Roster changed; relay requires a new mission session."); return false; }
            return true;
        }
        private void SendTo(ulong steam, RelayEnvelope packet)
        {
            var s = session ?? offer;
            if (s != null && steam == s.LocalSteam) ReceiveLocal(packet, steam);
            else { var result = messages.SendToSteamPeer(packet, steam); if (!result.Submitted) Report("Control send failed: " + result.Reason); }
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
                        p.Command.Requester != session.LocalSteam) return;
                    int expected = Responsible(session, p.Command.Owner);
                    if (expected == 0 || session.SteamOf(expected) != sender) return;
                    Report("Request " + p.Command.RequestId + ": " + (RelayOutcome)p.Outcome + " - " + p.Text);
                    return;
            }
        }
        private void QueueReady(Session s)
        {
            if (readyQueued || session != null) return;
            var packet = Envelope(RelayEnvelope.Ready, hostEpoch); packet.Text = s.RosterHash;
            var result = chores.Send(new RelayChoreEnvelope(packet)); readyQueued = result.Submitted;
            if (!result.Submitted) Report("Cannot synchronize relay readiness: " + result.Reason);
        }
        private int Responsible(Session s, int owner)
        {
            if (s.SteamOf(owner) != 0) return owner;
            lock (RelayWorld.EngineGate) return GamePlayerManagerAPI.Instance.IsAIPlayer(owner) ? s.HostPlayer : 0;
        }
        private void HandleRequest(Session s, RelayEnvelope p, ulong sender)
        {
            if (p.Command == null || p.Epoch != s.Epoch || p.Command.Requester != sender ||
                Responsible(s, p.Command.Owner) != s.LocalPlayer) return;
            if (!ReferenceEquals(session, s))
            {
                // Nothing was reserved or queued, so the requester may send a new request.
                Reply(s, p.Command, RelayOutcome.Rejected, "Owner relay session is not ready yet.");
                return;
            }
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
            var result = chores.Send(new RelayChoreEnvelope(authorized));
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
        private void ReceiveChore(AuthenticatedChoreMessage<RelayChoreEnvelope> message)
        {
            var packet = message.Packet.Inner;
            if (!Compatible(packet)) return;
            lock (RelayWorld.EngineGate)
            {
                if (packet.Kind == RelayEnvelope.Ready)
                {
                    var candidate = offer;
                    if (!Current(candidate) || packet.Epoch != candidate.Epoch || packet.Text != candidate.RosterHash ||
                        message.SenderPlayerId != candidate.HostPlayer || session != null) return;
                    admission.Clear(candidate.Epoch); execution.Clear(candidate.Epoch); session = candidate;
                    Post(() => { if (ReferenceEquals(session, candidate)) Report("Relay ready; allied units can be selected with the normal mouse input."); });
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
                bool success = false, nativeCalled = false;
                try { success = RelayWorld.Execute(command, () => Current(s) && ReferenceEquals(session, s), out reason, out nativeCalled); }
                catch (Exception error) { nativeCalled = true; reason = "Native execution failed; no retry: " + error.Message; }
                var outcome = success ? RelayOutcome.Executed : nativeCalled ? RelayOutcome.Uncertain : RelayOutcome.Rejected;
                execution.Record(command, outcome, reason);
                if (s.LocalPlayer == responsible) admission.Record(command, outcome, reason);
                string captured = "tick=" + message.ScheduledTick + " request=" + command.RequestId + " owner=" + command.Owner + " " + outcome + ": " + reason;
                Post(() => Report(captured));
                if (s.LocalPlayer == responsible) Reply(s, command, outcome, reason);
            }
        }
        private void Submit(RelayAction action, int x, int y)
        {
            AssertMain(); var s = session;
            if (!Current(s) || !RosterCurrent(s)) { Report("Relay session is not ready."); return; }
            if (selected.Count == 0) return;
            foreach (var group in selected.GroupBy(entry => entry.Owner))
            {
                int executor = Responsible(s, group.Key);
                if (executor == 0) { Report("No active owner or AI host for player " + group.Key); continue; }
                uint maxGlobalId = group.Max(unit => unit.GlobalId);
                var slots = group.Select(unit => unit.Slot).OrderBy(slot => slot).ToArray();
                // A very large selection needs several Chores; each chunk becomes its own native group.
                foreach (int[] chunk in RelaySlotCodec.Split(slots))
                {
                    var command = new RelayCommand(s.Epoch, group.Key, s.LocalSteam, ++requestId, action, x, y, maxGlobalId, chunk);
                    var packet = Envelope(RelayEnvelope.Request, s.Epoch); packet.Command = command; packet.Player = group.Key;
                    Report("Request " + command.RequestId + " (" + action + ", " + chunk.Length + " units) sent to player " + executor + ".");
                    SendTo(s.SteamOf(executor), packet);
                }
            }
        }

        // Runs on the simulation thread inside the engine pass; only reads simulation state
        // and hands an immutable result to Unity. Gestures stay local until they become a request.
        private void OnTroopInput(bool right, int[] boxed, bool box, int[] underCursor, int tileX, int tileY)
        {
            var s = session;
            if (s == null) return;
            bool hasGroups = Volatile.Read(ref hasSelection) != 0;
            if (right) { if (hasGroups) Post(ClearSelection); return; }
            // Placement and other tools own the mouse; a finished drag is back at idle here.
            if (MainControls.instance == null || MainControls.instance.CurrentAction != 0) return;
            bool add = EditorDirector.instance != null && EditorDirector.instance.shiftPressed;
            int[] ids = box ? boxed : underCursor;
            var chosen = new List<RelayUnit>();
            bool hitOther = false, hitEnemy = false;
            lock (RelayWorld.EngineGate)
            {
                if (!Current(s) || !ReferenceEquals(session, s)) return;
                if (ids != null)
                    foreach (int id in ids)
                    {
                        if (!RelayWorld.TryGetSelectableUnit(id, out var unit)) continue;
                        if (unit.Owner == s.LocalPlayer) hitOther = true;
                        else if (!RelayWorld.CanControl(s.LocalPlayer, unit.Owner)) hitEnemy = true;
                        else chosen.Add(unit);
                        // A click belongs to the topmost selectable unit only.
                        if (!box) break;
                    }
            }
            if (chosen.Count > 0) { var units = chosen.ToArray(); Post(() => ApplySelection(units, add)); return; }
            if (box || hitOther) { if (hasGroups && !add) Post(ClearSelection); return; }
            // Clicking an enemy is reserved for a later attack order.
            if (!hitEnemy && hasGroups && tileX >= 0 && tileY >= 0) Post(() => Submit(RelayAction.Move, tileX, tileY));
        }
        private void OnInputFailure(Exception error)
        {
            // Bounded: repeated identical failures in this per-pass callback are not logged again.
            int hash = error.GetType().GetHashCode() ^ error.Message.GetHashCode();
            if (Interlocked.Exchange(ref lastInputFailureHash, hash) == hash) return;
            string text = error.ToString();
            Post(() => Report("Troop input observer failed: " + text));
        }
        private void ApplySelection(RelayUnit[] units, bool add)
        {
            AssertMain();
            if (session == null) return;
            if (!add) selected.Clear();
            var known = new HashSet<RelayUnit>(selected);
            foreach (var unit in units) if (known.Add(unit)) selected.Add(unit);
            Volatile.Write(ref hasSelection, selected.Count > 0 ? 1 : 0);
            Report("Selected allied units: " + selected.Count + " (" + string.Join(", ", selected.Take(12).Select(u => u.Slot + "@" + u.Owner)) + (selected.Count > 12 ? ", ..." : "") + ")");
            frame.SetContinuous(selected.Count > 0);
            frame.Request();
        }
        private void ClearSelection()
        {
            AssertMain();
            if (selected.Count == 0) return;
            selected.Clear(); Volatile.Write(ref hasSelection, 0);
            markers.Hide();
            frame.SetContinuous(false);
            Report("Allied selection cleared.");
        }
        // Engine gate held. Drops dead units from the selection.
        private RelayMarker[] RefreshSelection()
        {
            var result = new List<RelayMarker>();
            for (int i = selected.Count - 1; i >= 0; i--)
            {
                if (!RelayWorld.IsSameLiveUnit(selected[i])) { selected.RemoveAt(i); continue; }
                if (result.Count < RelayMarkers.MaximumMarkers && Project(selected[i], out var marker)) result.Add(marker);
            }
            return result.ToArray();
        }
        private static unsafe bool Project(RelayUnit unit, out RelayMarker marker)
        {
            marker = default;
            if (Camera.main == null) return false;
            var position = APIShared.UnitAccess.TryGetById(unit.Slot, out _, out _)
                ? GameUnitManagerAPI.Instance.GetCurrentUnityPosition(unit.Slot) : default;
            if (position == default(System.Numerics.Vector3)) return false; // No Unity sprite snapshot available.
            var point = Camera.main.WorldToScreenPoint(new Vector3(position.X, position.Y, position.Z));
            if (point.z < 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) return false;
            marker = new RelayMarker(point.x, point.y); return true;
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
                if (s == null) { Negotiate(); frame.RequestAfter(TimeSpan.FromMilliseconds(500)); return; }
                if (selected.Count == 0) return;
                // A busy engine monitor only delays this frame's marker update.
                if (Monitor.TryEnter(RelayWorld.EngineGate))
                {
                    try { markers.Show(RefreshSelection()); }
                    finally { Monitor.Exit(RelayWorld.EngineGate); }
                    if (selected.Count == 0) { ClearSelection(); return; }
                }
                // KeyManager's key edges already respect GUI keyboard ownership and bindings.
                if (KeyManager.instance != null && KeyManager.instance.IsActionPressed(Enums.KeyFunctions.Stop))
                    Submit(RelayAction.Stop, 0, 0);
            }
            catch (Exception error) { Report("Relay frame failed: " + error.Message); frame.RequestAfter(TimeSpan.FromSeconds(1)); }
        }
        private void Negotiate()
        {
            if (Time.realtimeSinceStartup < nextHello) return;
            nextHello = Time.realtimeSinceStartup + 1;
            var candidate = CaptureSession(hostEpoch);
            if (candidate == null)
            {
                if (!inactiveReported)
                {
                    inactiveReported = true;
                    Report("Inactive for now: this client has no participating player (spectator) or the roster is unresolved.");
                }
                return;
            }
            lock (RelayWorld.EngineGate) {
                if (Current(candidate) && candidate.LocalPlayer == candidate.HostPlayer && offer == null) offer = candidate;
            }
            var hello = Envelope(RelayEnvelope.Hello); hello.Nonce = localChallenge;
            if (candidate.LocalPlayer == candidate.HostPlayer) ReceiveLocal(hello, candidate.LocalSteam);
            else SendTo(candidate.HostSteam, hello);
        }
    }
}
