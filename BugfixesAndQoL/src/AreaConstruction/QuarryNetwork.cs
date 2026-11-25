using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using APIShared;
using APIShared.Networking;
using BepInEx.Logging;
using SHCDESE.API;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed class QuarryNetwork
    {
        internal const int Protocol = 1;
        private const int MaximumPeers = 32;
        private const int MaximumSites = 4;
        private const int PeerIdentityBytes = sizeof(ulong) + sizeof(byte);
        private const int NonceBytes = 16;
        internal const int MaximumWorldVectorBytes = 1 + MaximumPeers * (PeerIdentityBytes + NonceBytes);

        // All native bindings are hash-gated. The module ID also distinguishes builds sharing a version.
        private static readonly string Fingerprint = Digest(Encoding.ASCII.GetBytes(
            "area-construction/quarry/" + typeof(QuarryNetwork).Module.ModuleVersionId.ToString("N")));

        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly Func<bool> nativeAvailable;
        private readonly Action<int, Tile[], Tile[]> execute;
        private readonly Action<bool> setFrameDemand;
        private readonly Action requestFrame;
        private readonly IMissionPlayStateReadinessCapability readiness;
        private readonly MessageChannel<QuarryControlPacket> control;
        private readonly AuthenticatedChoreChannel<QuarrySimulationPacket> simulation;
        private readonly Dictionary<ulong, string> nonces = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, string> advertised = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, string> challenges = new Dictionary<ulong, string>();
        private readonly NativeReadinessProof acknowledgements = new NativeReadinessProof();
        private readonly Dictionary<string, Peer[]> proven = new Dictionary<string, Peer[]>(StringComparer.Ordinal);
        private readonly Dictionary<(ulong Steam, string Nonce), long> lastOperations =
            new Dictionary<(ulong, string), long>();
        private readonly Queue<(ulong Peer, QuarryControlPacket Packet)> outbox = new Queue<(ulong, QuarryControlPacket)>();
        private readonly Queue<string> diagnostics = new Queue<string>();
        private MissionContext mission;
        private Peer[] roster = Array.Empty<Peer>();
        private ulong self;
        private int localPlayer;
        private string nonce = "", rosterDigest = "", sendingToken = "";
        private long nextScan, nextHello, nextProof, proofAttempt, operation;
        private bool ready;

        private readonly struct Peer
        {
            internal Peer(ulong steam, int player, string nonce = "")
            { Steam = steam; Player = player; Nonce = nonce; }
            internal readonly ulong Steam;
            internal readonly int Player;
            internal readonly string Nonce;
        }

        internal QuarryNetwork(ModApiClient api, ManualLogSource log, Func<bool> nativeAvailable,
            Action<int, Tile[], Tile[]> execute, Action<bool> setFrameDemand, Action requestFrame)
        {
            this.log = log;
            this.nativeAvailable = nativeAvailable;
            this.execute = execute;
            this.setFrameDemand = setFrameDemand;
            this.requestFrame = requestFrame;
            if (!api.TryGetMissionPlayStateReadiness(out readiness, out var diagnostic) ||
                !api.TryGetMissionLifecycle(out var lifecycle, out diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            simulation = api.Network.RegisterAuthenticatedChore<QuarrySimulationPacket>(
                "area-construction.quarry.simulation", ReceiveSimulation,
                new AuthenticatedChoreChannelOptions<QuarrySimulationPacket>
                {
                    MaximumPayloadBytes = 1200,
                    ValidatePayload = ValidateSimulation,
                    Diagnostic = diagnostic => Report(diagnostic.Reason)
                });
            control = api.Network.RegisterMessage<QuarryControlPacket>(
                "area-construction.quarry.control", ReceiveControl,
                new MessageChannelOptions<QuarryControlPacket>
                {
                    MaximumBodyBytes = 512,
                    Validate = message => ValidateControl(message.Packet),
                    Diagnostic = diagnostic => Report(diagnostic.Reason)
                });
            if (!lifecycle.TryRegisterObserver("area-construction.quarry.mission", null, End, null, out diagnostic) ||
                !readiness.TryRegisterObserver("area-construction.quarry.ready", note => Start(note.Context), out diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
        }

        internal bool IsReady
        {
            get { lock (sync) return ready && IsCurrent() && localPlayer != 0; }
        }

        private bool IsCurrent() => mission != null && readiness.Current?.SessionId == mission.SessionId;

        private void Start(MissionContext context)
        {
            if (!context.Mode.IsRealMultiplayer)
                return;
            lock (sync)
            {
                if (mission?.SessionId == context.SessionId)
                    return;
                ResetWorld();
                mission = context;
                nonce = NewNonce();
                setFrameDemand(true);
            }
        }

        private void End(MissionLifecycleNotification notification)
        {
            lock (sync)
            {
                if (mission?.SessionId != notification.Context.SessionId)
                    return;
                mission = null;
                ResetWorld();
                setFrameDemand(false);
            }
        }

        private void ResetWorld()
        {
            proven.Clear();
            lastOperations.Clear();
            operation = proofAttempt = 0;
            nonce = "";
            ResetRoster();
            nextScan = 0;
            roster = Array.Empty<Peer>();
            rosterDigest = "";
            self = 0;
            localPlayer = 0;
        }

        private void InvalidateSending()
        {
            ready = false;
            sendingToken = "";
            nextProof = 0;
            acknowledgements.Clear();
        }

        internal void SuspendSending()
        {
            lock (sync) InvalidateSending();
        }

        private void ResetRoster()
        {
            InvalidateSending();
            nextHello = 0;
            outbox.Clear();
            nonces.Clear();
            advertised.Clear();
            challenges.Clear();
        }

        // Main thread only. The APIShared send calls are deliberately outside sync and the engine monitor.
        internal void Pump()
        {
            DrainDiagnostics();
            long now = Stopwatch.GetTimestamp();
            lock (sync)
                if (!IsCurrent())
                    return;
            if (now >= nextScan)
            {
                nextScan = now + Stopwatch.Frequency;
                RefreshRoster();
            }

            QuarrySimulationPacket proof = null;
            lock (sync)
            {
                if (!IsCurrent() || roster.Length == 0)
                    return;
                if (!ready && now >= nextHello)
                {
                    nextHello = now + 3 * Stopwatch.Frequency;
                    foreach (Peer peer in roster)
                        if (!nonces.ContainsKey(peer.Steam))
                            QueueHello(peer.Steam);
                }
                if (!ready && localPlayer != 0 && now >= nextProof && roster.All(peer => nonces.ContainsKey(peer.Steam)))
                {
                    var peers = roster.Select(peer => new Peer(peer.Steam, peer.Player, nonces[peer.Steam])).ToArray();
                    byte[] vector = PackPeers(peers, true);
                    sendingToken = WorldToken(vector);
                    acknowledgements.Begin(self, sendingToken, ++proofAttempt,
                        peers.Select(peer => new KeyValuePair<ulong, string>(peer.Steam, peer.Nonce)));
                    nextProof = now + 3 * Stopwatch.Frequency;
                    proof = new QuarrySimulationPacket
                    {
                        Kind = (byte)QuarrySimulationKind.ReadyProof,
                        Sequence = proofAttempt,
                        Token = sendingToken,
                        Body = vector
                    };
                }
            }

            while (true)
            {
                (ulong Peer, QuarryControlPacket Packet) outbound;
                lock (sync)
                {
                    if (!IsCurrent() || outbox.Count == 0)
                        break;
                    outbound = outbox.Dequeue();
                }
                NetworkSendResult result = control.SendToSteamPeer(outbound.Packet, outbound.Peer);
                if (!result.Submitted)
                    Report(result.Reason);
            }
            if (proof != null)
            {
                NetworkSendResult result = simulation.Send(proof);
                if (!result.Submitted)
                    Report(result.Reason);
            }
        }

        internal bool TrySubmit(long sessionId, int actor, Tile[] reservedSites, Tile[] buildSites, out string reason)
        {
            if (actor <= 0 || PlayerPerspectiveAPI.GetControlledPlayerId() != actor)
            {
                reason = "Quarry drag requires the captured local player.";
                return false;
            }
            RefreshRoster();
            QuarrySimulationPacket packet;
            lock (sync)
            {
                if (!ready || !IsCurrent() || mission.SessionId != sessionId || localPlayer != actor)
                {
                    reason = "Quarry drag is waiting for multiplayer readiness.";
                    return false;
                }
                if (!ValidateSites(reservedSites, buildSites))
                {
                    reason = "Invalid quarry block.";
                    return false;
                }
                packet = new QuarrySimulationPacket
                {
                    Kind = (byte)QuarrySimulationKind.Build,
                    Token = sendingToken,
                    Sequence = ++operation,
                    Body = PackSites(reservedSites, buildSites)
                };
            }
            NetworkSendResult result = simulation.Send(packet);
            reason = result.Reason;
            return result.Submitted;
        }

        private void RefreshRoster()
        {
            MissionContext current = readiness.Current;
            if (current == null || !current.Mode.IsRealMultiplayer)
                return;
            Peer[] next = Array.Empty<Peer>();
            ulong localSteam = 0;
            var members = Platform_Multiplayer.Instance?.gameMembers;
            if (members != null && nativeAvailable())
            {
                var humans = members.Where(member => member != null && !member.skirmishAI && !member.kicked && member.steamID != 0)
                    .OrderBy(member => member.steamID).ToArray();
                var own = humans.Where(member => member.isSelf).ToArray();
                var active = humans.Where(member => member.playerID >= 1 && member.playerID <= 8).ToArray();
                if (own.Length == 1 && humans.Length >= 2 && humans.Length <= MaximumPeers &&
                    humans.Select(member => member.steamID).Distinct().Count() == humans.Length &&
                    active.Select(member => member.playerID).Distinct().Count() == active.Length)
                {
                    localSteam = own[0].steamID;
                    next = humans.Select(member => new Peer(member.steamID,
                        member.playerID >= 1 && member.playerID <= 8 ? member.playerID : 0)).ToArray();
                }
            }
            string digest = next.Length == 0 ? "" : Digest(PackPeers(next, false));
            lock (sync)
            {
                if (mission?.SessionId != current.SessionId || !IsCurrent() || self == localSteam && rosterDigest == digest)
                    return;
                // Roster or peer readiness changes stop new sends. Already-proven native work
                // stays executable until the simulation world ends, independently of Steam timing.
                ResetRoster();
                roster = next;
                rosterDigest = digest;
                self = localSteam;
                localPlayer = 0;
                if (self == 0)
                    return;
                nonces[self] = nonce;
                foreach (Peer peer in roster)
                {
                    if (peer.Steam == self)
                        localPlayer = peer.Player;
                }
            }
        }

        private void QueueHello(ulong peer)
        {
            string challenge = NewNonce();
            challenges[peer] = challenge;
            QueueControl(peer, new QuarryControlPacket
            {
                Kind = (byte)QuarryControlKind.Hello,
                Fingerprint = Fingerprint,
                Roster = rosterDigest,
                Nonce = nonce,
                Token = challenge
            });
        }

        private void QueueControl(ulong peer, QuarryControlPacket packet)
        {
            if (outbox.Count < MaximumPeers * 4)
                outbox.Enqueue((peer, packet));
        }

        private void ReceiveControl(NetworkMessage<QuarryControlPacket> message)
        {
            RefreshRoster();
            QuarryControlPacket packet = message.Packet;
            lock (sync)
            {
                if (!IsCurrent() || packet.Fingerprint != Fingerprint || packet.Roster != rosterDigest ||
                    message.SenderSteamId == self || !roster.Any(peer => peer.Steam == message.SenderSteamId))
                    return;
                if ((QuarryControlKind)packet.Kind == QuarryControlKind.Hello)
                {
                    ulong sender = message.SenderSteamId;
                    QueueControl(sender, new QuarryControlPacket
                    {
                        Kind = (byte)QuarryControlKind.HelloEcho,
                        Fingerprint = Fingerprint,
                        Roster = rosterDigest,
                        Nonce = nonce,
                        Token = packet.Token
                    });
                    if ((!advertised.TryGetValue(sender, out string prior) || prior != packet.Nonce) &&
                        (!nonces.TryGetValue(sender, out string confirmed) || confirmed != packet.Nonce))
                    {
                        if (nonces.Remove(sender))
                            InvalidateSending();
                        QueueHello(sender);
                    }
                    advertised[sender] = packet.Nonce;
                    return;
                }
                if ((QuarryControlKind)packet.Kind == QuarryControlKind.HelloEcho)
                {
                    ulong sender = message.SenderSteamId;
                    if (challenges.TryGetValue(sender, out string challenge) && challenge == packet.Token)
                    {
                        challenges.Remove(sender);
                        if (nonces.TryGetValue(sender, out string confirmed) && confirmed != packet.Nonce)
                            InvalidateSending();
                        nonces[sender] = packet.Nonce;
                    }
                    return;
                }
                AcceptReceipt(message.SenderSteamId, packet.Nonce, packet.Token, packet.Attempt, packet.Command, packet.Tick);
            }
        }

        private void AcceptReceipt(ulong sender, string senderNonce, string token, long attempt, int commandId, int tick)
        {
            if (acknowledgements.TryAccept(sender, senderNonce, token, attempt, commandId, tick))
                ready = acknowledgements.Evaluate() == NativeReadinessState.Ready;
        }

        private void ReceiveSimulation(AuthenticatedChoreMessage<QuarrySimulationPacket> message)
        {
            QuarrySimulationPacket packet = message.Packet;
            if ((QuarrySimulationKind)packet.Kind == QuarrySimulationKind.ReadyProof)
            {
                ReceiveProof(message);
                return;
            }
            if (!TryUnpackSites(packet.Body, out Tile[] reserved, out Tile[] candidates))
                return;
            int actor = message.SenderPlayerId;
            lock (sync)
            {
                if (!IsCurrent() || actor <= 0 || !proven.TryGetValue(packet.Token, out Peer[] peers))
                    return;
                Peer sender = Array.Find(peers, peer => peer.Player == actor);
                var issuer = (sender.Steam, sender.Nonce);
                if (sender.Steam == 0 || lastOperations.TryGetValue(issuer, out long last) && packet.Sequence <= last)
                    return;
                // Reserve the sequence before native entry, also when the call fails partway.
                lastOperations[issuer] = packet.Sequence;
            }
            execute(actor, reserved, candidates);
        }

        private void ReceiveProof(AuthenticatedChoreMessage<QuarrySimulationPacket> message)
        {
            QuarrySimulationPacket packet = message.Packet;
            Peer[] vector = UnpackPeers(packet.Body);
            if (vector == null || WorldToken(packet.Body) != packet.Token)
                return;
            lock (sync)
            {
                if (!IsCurrent() || !nativeAvailable() || roster.Length == 0 ||
                    Digest(PackPeers(vector, false)) != rosterDigest || !vector.Any(peer => peer.Steam == self && peer.Nonce == nonce))
                    return;
                Peer sender = Array.Find(vector, peer => peer.Player == message.SenderPlayerId);
                if (sender.Steam == 0)
                    return;
                if (!proven.ContainsKey(packet.Token))
                {
                    if (proven.Count >= MaximumPeers)
                        return;
                    proven.Add(packet.Token, vector);
                }
                if (sender.Steam == self)
                {
                    AcceptReceipt(self, nonce, packet.Token, packet.Sequence, message.CommandId, message.ScheduledTick);
                    return;
                }
                QueueControl(sender.Steam, new QuarryControlPacket
                {
                    Kind = (byte)QuarryControlKind.ProofAck,
                    Fingerprint = Fingerprint,
                    Roster = rosterDigest,
                    Nonce = nonce,
                    Token = packet.Token,
                    Attempt = packet.Sequence,
                    Command = message.CommandId,
                    Tick = message.ScheduledTick
                });
            }
        }

        private void Report(string message)
        {
            lock (sync)
                if (diagnostics.Count < MaximumPeers)
                    diagnostics.Enqueue(message);
            requestFrame();
        }

        private void DrainDiagnostics()
        {
            string[] messages;
            lock (sync)
            {
                if (diagnostics.Count == 0)
                    return;
                messages = diagnostics.ToArray();
                diagnostics.Clear();
            }
            foreach (string message in messages)
                log.LogWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Area construction quarry: {message}");
        }

        private static bool ValidateControl(QuarryControlPacket packet) =>
            packet != null && packet.Protocol == Protocol && IsHex(packet.Fingerprint, 64) &&
            IsHex(packet.Roster, 64) && IsHex(packet.Nonce, 2 * NonceBytes) &&
            ((packet.Kind == (byte)QuarryControlKind.Hello || packet.Kind == (byte)QuarryControlKind.HelloEcho) &&
             IsHex(packet.Token, 2 * NonceBytes) && packet.Attempt == 0 ||
             packet.Kind == (byte)QuarryControlKind.ProofAck && IsHex(packet.Token, 64) && packet.Attempt > 0);

        private static bool ValidateSimulation(QuarrySimulationPacket packet)
        {
            if (packet == null || packet.Protocol != Protocol || !IsHex(packet.Token, 64) || packet.Sequence <= 0 || packet.Body == null)
                return false;
            if (packet.Kind == (byte)QuarrySimulationKind.ReadyProof)
                return UnpackPeers(packet.Body) != null;
            return packet.Kind == (byte)QuarrySimulationKind.Build && TryUnpackSites(packet.Body, out _, out _);
        }

        private static bool IsHex(string value, int length) =>
            value != null && value.Length == length && value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        private static string Hex(byte[] data) => BitConverter.ToString(data).Replace("-", "");
        private static string NewNonce()
        {
            var bytes = new byte[NonceBytes];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Hex(bytes);
        }

        private static string Digest(byte[] data)
        {
            using (var sha = SHA256.Create())
                return Hex(sha.ComputeHash(data));
        }

        private static string WorldToken(byte[] vector) => Digest(Encoding.ASCII.GetBytes(Fingerprint + ":" + Hex(vector)));

        private static byte[] PackPeers(Peer[] peers, bool includeNonce)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write((byte)peers.Length);
                foreach (Peer peer in peers)
                {
                    writer.Write(peer.Steam);
                    writer.Write((byte)peer.Player);
                    if (includeNonce)
                        for (int i = 0; i < NonceBytes; i++)
                            writer.Write(Convert.ToByte(peer.Nonce.Substring(i * 2, 2), 16));
                }
                return buffer.ToArray();
            }
        }

        private static Peer[] UnpackPeers(byte[] body)
        {
            if (body.Length == 0 || body[0] < 2 || body[0] > MaximumPeers ||
                body.Length != 1 + body[0] * (PeerIdentityBytes + NonceBytes))
                return null;
            var peers = new Peer[body[0]];
            using (var reader = new BinaryReader(new MemoryStream(body, 1, body.Length - 1, false)))
            {
                ulong prior = 0;
                int players = 0;
                for (int i = 0; i < peers.Length; i++)
                {
                    ulong steam = reader.ReadUInt64();
                    int player = reader.ReadByte();
                    if (steam <= prior || player > 8 || player > 0 && (players & (1 << player)) != 0)
                        return null;
                    prior = steam;
                    players |= 1 << player;
                    peers[i] = new Peer(steam, player, Hex(reader.ReadBytes(NonceBytes)));
                }
            }
            return peers;
        }

        private static byte[] PackSites(Tile[] reserved, Tile[] candidates)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write((byte)reserved.Length);
                writer.Write((byte)candidates.Length);
                foreach (Tile site in reserved.Concat(candidates))
                {
                    writer.Write((ushort)site.X);
                    writer.Write((ushort)site.Y);
                }
                return buffer.ToArray();
            }
        }

        private static bool TryUnpackSites(byte[] body, out Tile[] reserved, out Tile[] candidates)
        {
            reserved = candidates = null;
            if (body.Length < 2 || body[0] < 2 || body[0] > MaximumSites || body[1] < 1 ||
                body[1] > body[0] || body.Length != 2 + (body[0] + body[1]) * 2 * sizeof(ushort))
                return false;
            using (var reader = new BinaryReader(new MemoryStream(body, 2, body.Length - 2, false)))
            {
                reserved = ReadTiles(reader, body[0]);
                candidates = ReadTiles(reader, body[1]);
            }
            return ValidateSites(reserved, candidates);
        }

        private static Tile[] ReadTiles(BinaryReader reader, int count)
        {
            var tiles = new Tile[count];
            for (int i = 0; i < count; i++)
                tiles[i] = new Tile(reader.ReadUInt16(), reader.ReadUInt16());
            return tiles;
        }

        private static bool ValidateSites(Tile[] reserved, Tile[] candidates)
        {
            if (reserved == null || reserved.Length < 2 || reserved.Length > MaximumSites ||
                candidates == null || candidates.Length == 0 || candidates.Length > reserved.Length ||
                !QuarryLayout.IsBlock(reserved) ||
                !reserved.All(tile => (uint)tile.X < GameTileManagerAPI.MAX_WIDTH && (uint)tile.Y < GameTileManagerAPI.MAX_HEIGHT))
                return false;
            int index = 0;
            foreach (Tile site in reserved)
                if (site.Equals(candidates[index]) && ++index == candidates.Length)
                    return true;
            return false;
        }
    }
}
