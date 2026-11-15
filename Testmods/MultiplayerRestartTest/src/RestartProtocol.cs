using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiplayerRestartTest
{
    // The caller owns Unity dispatch, actual transport, native stopping and lobby validation.
    // Each instance owns exactly one attempt. Session IDs and retired operation IDs must also
    // be checked by the caller before it admits a new instance from a received Prepare.
    internal enum RestartMessageKind { Prepare = 1, Ready, Stop, Stopped, Lobby, Joined, Start, Abort, StartAcknowledged }
    internal enum RestartPhase { Idle, Preparing, Stopping, WaitingLobby, Joining, Starting, Completed, Aborted, Failed }
    [Flags]
    internal enum RestartEffect { None = 0, PrepareLocal = 1, StopLocal = 2, CreateLobby = 4, JoinLobby = 8, StartLocal = 16, Cancelled = 32 }

    internal sealed class RestartPacket
    {
        public RestartMessageKind Kind { get; set; }
        public string Session { get; set; }
        public string Operation { get; set; }
        public string Digest { get; set; }
        public ulong LobbyId { get; set; }
    }

    internal sealed class RestartStep
    {
        internal RestartEffect Effects { get; set; }
        internal List<RestartPacket> Outbound { get; } = new List<RestartPacket>();
        internal string Rejection { get; set; }
        internal bool Accepted => Rejection == null;
        internal static RestartStep Reject(string reason) => new RestartStep { Rejection = reason };
    }

    internal sealed class RestartProtocol
    {
        private readonly ulong self, host;
        private readonly HashSet<ulong> peers, ready = new HashSet<ulong>(), stopped = new HashSet<ulong>(), verified = new HashSet<ulong>();
        private readonly HashSet<ulong> startAcknowledged = new HashSet<ulong>();
        private readonly string session, digest;
        private readonly long timeout;
        private long phaseStarted, lastClock;
        private bool localPrepared, localStopped, localVerified;
        private bool hostLaunchRequested;

        internal RestartProtocol(ulong selfSteamId, ulong hostSteamId, IEnumerable<ulong> humanPeers,
            string sessionId, string snapshotDigest, long timeoutTicks)
        {
            if (humanPeers == null) throw new ArgumentNullException(nameof(humanPeers));
            ulong[] members = humanPeers.ToArray();
            peers = new HashSet<ulong>(members);
            if (peers.Count < 2 || peers.Count > 8 || peers.Count != members.Length || peers.Contains(0) ||
                !peers.Contains(selfSteamId) || !peers.Contains(hostSteamId))
                throw new ArgumentException("Expected a unique human roster containing self and host.", nameof(humanPeers));
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(snapshotDigest))
                throw new ArgumentException("Session and snapshot digest are required.");
            if (timeoutTicks <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutTicks));
            self = selfSteamId; host = hostSteamId; session = sessionId; digest = snapshotDigest; timeout = timeoutTicks;
        }

        internal RestartPhase Phase { get; private set; }
        internal string Operation { get; private set; }
        internal ulong LobbyId { get; private set; }
        internal bool Committed { get; private set; }
        internal bool IsCommitted => Committed;
        internal string FailureReason { get; private set; }
        internal bool IsHost => self == host;
        internal bool IsTerminal => Phase == RestartPhase.Completed || Phase == RestartPhase.Aborted || Phase == RestartPhase.Failed;
        internal bool RosterMatches(IEnumerable<ulong> currentPeers)
        {
            if (currentPeers == null) return false;
            ulong[] members = currentPeers.ToArray();
            return members.Length == peers.Count && peers.SetEquals(members);
        }

        internal RestartStep Begin(string operation, long now)
        {
            if (!IsHost || Phase != RestartPhase.Idle || !ValidOperation(operation) || now < 0)
                return RestartStep.Reject("Only the current host can begin an idle attempt with a valid operation.");
            Operation = operation;
            Enter(RestartPhase.Preparing, now);
            var result = new RestartStep { Effects = RestartEffect.PrepareLocal };
            result.Outbound.Add(Packet(RestartMessageKind.Prepare));
            return result;
        }

        internal RestartStep Receive(ulong senderSteamId, RestartPacket packet, long now)
        {
            if (packet == null || !peers.Contains(senderSteamId) || senderSteamId == self || IsTerminal)
                return RestartStep.Reject("Unknown sender, self-delivery, missing packet or retired attempt.");
            if (packet.Session != session || packet.Digest != digest || !ValidOperation(packet.Operation))
                return RestartStep.Reject("Session, operation or start configuration does not match.");
            if (Phase == RestartPhase.Idle)
            {
                if (IsHost || senderSteamId != host || packet.Kind != RestartMessageKind.Prepare || packet.LobbyId != 0 || now < 0)
                    return RestartStep.Reject("Only a host Prepare can admit an idle client attempt.");
                Operation = packet.Operation;
                Enter(RestartPhase.Preparing, now);
                return new RestartStep { Effects = RestartEffect.PrepareLocal };
            }
            if (packet.Operation != Operation) return RestartStep.Reject("Stale or different operation.");
            RestartStep clockFailure = CheckClock(now);
            if (clockFailure != null) return clockFailure;
            // Lobby is the only packet carrying a target lobby. Reject accidental reuse of IDs.
            if (packet.Kind != RestartMessageKind.Lobby && packet.LobbyId != 0)
                return RestartStep.Reject("Unexpected target lobby.");
            switch (packet.Kind)
            {
                case RestartMessageKind.Prepare:
                    return RestartStep.Reject("Duplicate Prepare.");
                case RestartMessageKind.Ready:
                    if (!IsHost || Phase != RestartPhase.Preparing || !ready.Add(senderSteamId))
                        return RestartStep.Reject("Unexpected or duplicate Ready.");
                    return AdvancePrepared(now);
                case RestartMessageKind.Stop:
                    if (IsHost || senderSteamId != host || Phase != RestartPhase.Preparing || !localPrepared)
                        return RestartStep.Reject("Unexpected Stop or preparation is incomplete.");
                    Committed = true;
                    Enter(RestartPhase.Stopping, now);
                    return new RestartStep { Effects = RestartEffect.StopLocal };
                case RestartMessageKind.Stopped:
                    if (!IsHost || Phase != RestartPhase.Stopping || !stopped.Add(senderSteamId))
                        return RestartStep.Reject("Unexpected or duplicate Stopped.");
                    return AdvanceStopped(now);
                case RestartMessageKind.Lobby:
                    if (IsHost || senderSteamId != host || Phase != RestartPhase.WaitingLobby || !localStopped || packet.LobbyId == 0)
                        return RestartStep.Reject("Unexpected lobby handoff or simulation is not stopped.");
                    LobbyId = packet.LobbyId;
                    Enter(RestartPhase.Joining, now);
                    return new RestartStep { Effects = RestartEffect.JoinLobby };
                case RestartMessageKind.Joined:
                    if (!IsHost || Phase != RestartPhase.Joining || !verified.Add(senderSteamId))
                        return RestartStep.Reject("Unexpected or duplicate verified lobby member.");
                    return AdvanceVerified(now);
                case RestartMessageKind.Start:
                    if (IsHost || senderSteamId != host || Phase != RestartPhase.Joining || !localVerified)
                        return RestartStep.Reject("Unexpected Start or local lobby verification is incomplete.");
                    Enter(RestartPhase.Starting, now);
                    var start = Send(RestartMessageKind.StartAcknowledged);
                    // Apply StartLocal (grant local launch permission) before sending this
                    // acknowledgement. Steam lobby start and channel 2 have no shared ordering.
                    start.Effects = RestartEffect.StartLocal;
                    return start;
                case RestartMessageKind.StartAcknowledged:
                    if (!IsHost || Phase != RestartPhase.Starting || !startAcknowledged.Add(senderSteamId))
                        return RestartStep.Reject("Unexpected or duplicate start acknowledgement.");
                    if (startAcknowledged.Count != peers.Count) return new RestartStep();
                    hostLaunchRequested = true;
                    return new RestartStep { Effects = RestartEffect.StartLocal };
                case RestartMessageKind.Abort:
                    // Any expected participant can veto its own unsafe transition. Clients only
                    // follow the host's broadcast; an unrelated client's message is not authority.
                    if (!IsHost && senderSteamId != host)
                        return RestartStep.Reject("Only the host can abort a client transition.");
                    return AbortCore("A participant cancelled the restart.", IsHost);
                default:
                    return RestartStep.Reject("Unknown message kind.");
            }
        }

        internal RestartStep Prepared(long now)
        {
            if (Phase != RestartPhase.Preparing || localPrepared) return RestartStep.Reject("Local preparation is not pending.");
            RestartStep failure = CheckClock(now); if (failure != null) return failure;
            localPrepared = true; ready.Add(self);
            if (IsHost) return AdvancePrepared(now);
            return Send(RestartMessageKind.Ready);
        }

        internal RestartStep Stopped(long now)
        {
            if (Phase != RestartPhase.Stopping || localStopped) return RestartStep.Reject("Local stop is not pending.");
            RestartStep failure = CheckClock(now); if (failure != null) return failure;
            localStopped = true; stopped.Add(self);
            if (IsHost) return AdvanceStopped(now);
            Enter(RestartPhase.WaitingLobby, now);
            return Send(RestartMessageKind.Stopped);
        }

        internal RestartStep PublishLobby(ulong lobbyId, long now)
        {
            if (!IsHost || Phase != RestartPhase.WaitingLobby || stopped.Count != peers.Count || lobbyId == 0)
                return RestartStep.Reject("Host lobby publication requires all stopped receipts.");
            RestartStep failure = CheckClock(now); if (failure != null) return failure;
            LobbyId = lobbyId;
            Enter(RestartPhase.Joining, now);
            var result = new RestartStep { Effects = RestartEffect.JoinLobby };
            result.Outbound.Add(Packet(RestartMessageKind.Lobby, lobbyId));
            return result;
        }

        // Call only after the exact expected roster, slots, content and settings have been
        // checked in the actual target lobby. A Steam join callback alone is insufficient.
        internal RestartStep Verified(long now)
        {
            if (Phase != RestartPhase.Joining || localVerified) return RestartStep.Reject("Local verification is not pending.");
            RestartStep failure = CheckClock(now); if (failure != null) return failure;
            localVerified = true; verified.Add(self);
            if (IsHost) return AdvanceVerified(now);
            return Send(RestartMessageKind.Joined);
        }

        internal RestartStep Complete()
        {
            if (Phase != RestartPhase.Starting || (IsHost && !hostLaunchRequested))
                return RestartStep.Reject("Normal game startup is not pending or the start barrier is incomplete.");
            Phase = RestartPhase.Completed;
            return new RestartStep();
        }

        internal RestartStep Tick(long now)
        {
            if (Phase == RestartPhase.Idle || IsTerminal) return new RestartStep();
            return CheckClock(now) ?? new RestartStep();
        }

        internal RestartStep Abort(string reason)
        {
            if (Phase == RestartPhase.Idle || IsTerminal) return RestartStep.Reject("No live attempt to abort.");
            return AbortCore(string.IsNullOrWhiteSpace(reason) ? "Local transition failed." : reason, true);
        }

        private RestartStep AdvancePrepared(long now)
        {
            if (ready.Count != peers.Count) return new RestartStep();
            Committed = true;
            Enter(RestartPhase.Stopping, now);
            var result = new RestartStep { Effects = RestartEffect.StopLocal };
            result.Outbound.Add(Packet(RestartMessageKind.Stop));
            return result;
        }

        private RestartStep AdvanceStopped(long now)
        {
            if (stopped.Count != peers.Count) return new RestartStep();
            Enter(RestartPhase.WaitingLobby, now);
            return new RestartStep { Effects = RestartEffect.CreateLobby };
        }

        private RestartStep AdvanceVerified(long now)
        {
            if (verified.Count != peers.Count) return new RestartStep();
            Enter(RestartPhase.Starting, now);
            // The host must not set Steam's lobby start state until every client has
            // installed its launch permission and acknowledged this final barrier.
            startAcknowledged.Add(self);
            var result = new RestartStep();
            result.Outbound.Add(Packet(RestartMessageKind.Start));
            return result;
        }

        private RestartStep CheckClock(long now)
        {
            if (now < lastClock) return AbortCore("Monotonic clock regressed.", true);
            lastClock = now;
            if (now - phaseStarted >= timeout) return AbortCore("Restart phase timed out.", true);
            return null;
        }

        private RestartStep AbortCore(string reason, bool broadcast)
        {
            FailureReason = reason;
            Phase = Committed ? RestartPhase.Failed : RestartPhase.Aborted;
            var result = new RestartStep { Effects = RestartEffect.Cancelled };
            if (broadcast) result.Outbound.Add(Packet(RestartMessageKind.Abort));
            return result;
        }

        private void Enter(RestartPhase phase, long now) { Phase = phase; phaseStarted = lastClock = now; }
        private RestartPacket Packet(RestartMessageKind kind, ulong lobby = 0) => new RestartPacket
        { Kind = kind, Session = session, Operation = Operation, Digest = digest, LobbyId = lobby };
        private RestartStep Send(RestartMessageKind kind)
        { var result = new RestartStep(); result.Outbound.Add(Packet(kind)); return result; }
        private static bool ValidOperation(string value) => Guid.TryParseExact(value, "N", out _);
    }
}
