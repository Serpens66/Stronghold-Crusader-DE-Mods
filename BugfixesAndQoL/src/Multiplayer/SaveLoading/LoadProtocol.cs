using System;
using System.Collections.Generic;
using System.Linq;

namespace BugfixesAndQoL.Multiplayer.SaveLoading
{
    // This coordinates only the irreversible transition to an ordinary shared lobby.
    // Save selection, native save eligibility and loading remain with Vanilla/APIShared.
    internal enum LoadMessageKind
    {
        Prepare = 1, Prepared, Stop, Stopped, Drained, Lobby, Joined,
        ReleaseLobby, ReleaseAcknowledged, Abort, CommittedAbort
    }
    internal enum LoadPhase
    {
        Idle, Preparing, Stopping, Draining, WaitingLobby, Joining, Releasing,
        Completed, Aborted, Failed
    }
    [Flags]
    internal enum LoadEffect
    {
        None = 0, PrepareLocal = 1, StopLocal = 2, CreateLobby = 4, JoinLocal = 8,
        ReleaseLocal = 16, OpenLoadMenu = 32, Cancelled = 64
    }
    internal sealed class LoadPacket
    {
        public LoadMessageKind Kind { get; set; }
        public ulong Session { get; set; }
        public string Operation { get; set; }
        public ulong LobbyId { get; set; }
    }
    internal sealed class LoadStep
    {
        internal LoadEffect Effects { get; set; }
        internal List<LoadPacket> Outbound { get; } = new List<LoadPacket>();
        internal string Rejection { get; set; }
        internal bool Accepted => Rejection == null;
        internal static LoadStep Reject(string reason) => new LoadStep { Rejection = reason };
    }

    internal sealed class LoadProtocol
    {
        private readonly ulong self, host, session;
        private readonly HashSet<ulong> peers;
        private readonly IReadOnlyList<ulong> remotePeers, hostOnly;
        private readonly HashSet<ulong> prepared = new HashSet<ulong>();
        private readonly HashSet<ulong> stopped = new HashSet<ulong>();
        private readonly HashSet<ulong> drained = new HashSet<ulong>();
        private readonly HashSet<ulong> joined = new HashSet<ulong>();
        private readonly HashSet<ulong> releaseAcknowledged = new HashSet<ulong>();
        private readonly long timeout;
        private long phaseStarted, lastClock;
        private bool localPrepared, localStopped, localDrained, localJoined;

        internal LoadProtocol(ulong selfSteamId, ulong hostSteamId, IEnumerable<ulong> humanPeers,
            ulong sourceSession, long timeoutTicks)
        {
            if (humanPeers == null) throw new ArgumentNullException(nameof(humanPeers));
            ulong[] members = humanPeers.ToArray();
            peers = new HashSet<ulong>(members);
            if (peers.Count < 1 || peers.Count > 8 || peers.Count != members.Length || peers.Contains(0) ||
                !peers.Contains(selfSteamId) || !peers.Contains(hostSteamId))
                throw new ArgumentException("Expected unique human peers containing self and host.", nameof(humanPeers));
            if (sourceSession == 0) throw new ArgumentOutOfRangeException(nameof(sourceSession));
            if (timeoutTicks <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutTicks));
            self = selfSteamId; host = hostSteamId; session = sourceSession; timeout = timeoutTicks;
            remotePeers = Array.AsReadOnly(members.Where(id => id != self).ToArray());
            hostOnly = Array.AsReadOnly(new[] { host });
        }
        internal LoadPhase Phase { get; private set; }
        internal ulong Session => session;
        internal string Operation { get; private set; }
        internal ulong LobbyId { get; private set; }
        internal string FailureReason { get; private set; }
        internal bool IsHost => self == host;
        internal bool IsCommitted { get; private set; }
        internal bool IsCompleted => Phase == LoadPhase.Completed;
        internal bool IsTerminal => IsCompleted || Phase == LoadPhase.Aborted || Phase == LoadPhase.Failed;

        // A client Stopped fence must reach every old sender stream, not only the host.
        internal IReadOnlyList<ulong> Recipients(LoadPacket packet) =>
            IsHost || packet.Kind == LoadMessageKind.Stopped ? remotePeers : hostOnly;
        internal bool RosterMatches(IEnumerable<ulong> humans)
        {
            if (humans == null) return false;
            ulong[] actual = humans.ToArray();
            return actual.Length == peers.Count && peers.SetEquals(actual);
        }
        internal LoadStep Begin(string operation, long now)
        {
            if (!IsHost || Phase != LoadPhase.Idle || !ValidOperation(operation) || now < 0)
                return LoadStep.Reject("Only the current host may begin an idle transition.");
            Operation = operation; Enter(LoadPhase.Preparing, now);
            var result = Send(LoadMessageKind.Prepare); result.Effects = LoadEffect.PrepareLocal;
            return result;
        }
        internal LoadStep Receive(ulong sender, LoadPacket packet, long now)
        {
            if (packet == null || sender == self || !peers.Contains(sender) || Phase == LoadPhase.Failed)
                return LoadStep.Reject("Missing packet, unknown sender or retired transition.");
            if (packet.Session != session || !ValidOperation(packet.Operation) ||
                !Enum.IsDefined(typeof(LoadMessageKind), packet.Kind) ||
                (packet.Kind == LoadMessageKind.Lobby ? packet.LobbyId == 0 : packet.LobbyId != 0))
                return LoadStep.Reject("Source session, message or lobby framing does not match.");
            if (Phase == LoadPhase.Idle)
            {
                if (IsHost || sender != host || packet.Kind != LoadMessageKind.Prepare || now < 0)
                    return LoadStep.Reject("Only a host Prepare can admit an idle client.");
                Operation = packet.Operation; Enter(LoadPhase.Preparing, now);
                return new LoadStep { Effects = LoadEffect.PrepareLocal };
            }
            if (packet.Operation != Operation) return LoadStep.Reject("Different or stale operation.");
            if (Phase == LoadPhase.Aborted)
            {
                // A local preparation timeout can cross the host's Stop publication.
                // Keep this bounded receipt: only its host can prove that somebody
                // committed, requiring the resumed old world to be retired as well.
                if (!IsHost && sender == host && packet.Kind == LoadMessageKind.CommittedAbort)
                { IsCommitted = true; return AbortCore("The host committed before this transition failed.", false); }
                return LoadStep.Reject("The transition was cancelled before commitment.");
            }
            if (IsCompleted)
            {
                // The transition no longer owns the ordinary lobby or a subsequent save.
                // A late Abort therefore must never tear either down.
                if (!IsHost && sender == host && packet.Kind == LoadMessageKind.ReleaseLobby)
                    return Send(LoadMessageKind.ReleaseAcknowledged);
                if (IsHost && packet.Kind == LoadMessageKind.ReleaseAcknowledged) return new LoadStep();
                return LoadStep.Reject("The lobby handoff is complete.");
            }
            // Positive commitment evidence wins over a crossed local timeout.
            // Otherwise this peer could resume the source after learning that
            // another participant has already irreversibly retired it.
            if (packet.Kind == LoadMessageKind.CommittedAbort)
            {
                if (!IsHost && sender != host) return LoadStep.Reject("Only the host may report group commitment to a client.");
                IsCommitted = true;
                return AbortCore("A participant failed after commitment.", IsHost);
            }
            if (!IsHost && sender == host && packet.Kind == LoadMessageKind.Stop &&
                Phase == LoadPhase.Preparing && localPrepared) IsCommitted = true;
            var clock = CheckClock(now); if (clock != null) return clock;
            switch (packet.Kind)
            {
                case LoadMessageKind.Prepared:
                    if (!IsHost || Phase != LoadPhase.Preparing || !prepared.Add(sender))
                        return LoadStep.Reject("Unexpected or duplicate preparation acknowledgement.");
                    return AdvancePrepared(now);
                case LoadMessageKind.Stop:
                    if (IsHost || sender != host || Phase != LoadPhase.Preparing || !localPrepared)
                        return LoadStep.Reject("Stop requires the prepared client and current host.");
                    IsCommitted = true; Enter(LoadPhase.Stopping, now);
                    return new LoadStep { Effects = LoadEffect.StopLocal };
                case LoadMessageKind.Stopped:
                    // Different senders have independent reliable streams. Another client's
                    // fence can precede the host's Stop without granting stop authority.
                    if ((Phase != LoadPhase.Stopping && Phase != LoadPhase.Draining &&
                        !(Phase == LoadPhase.Preparing && !IsHost && localPrepared)) || !stopped.Add(sender))
                        return LoadStep.Reject("Unexpected or duplicate stopped fence.");
                    return AdvanceStopped(now);
                case LoadMessageKind.Drained:
                    if (!IsHost || (Phase != LoadPhase.Stopping && Phase != LoadPhase.Draining) || !drained.Add(sender))
                        return LoadStep.Reject("Unexpected or duplicate stream-drain acknowledgement.");
                    return AdvanceDrained(now);
                case LoadMessageKind.Lobby:
                    if (IsHost || sender != host || Phase != LoadPhase.WaitingLobby || !localDrained)
                        return LoadStep.Reject("Replacement lobby requires the host and all old stream fences.");
                    LobbyId = packet.LobbyId; Enter(LoadPhase.Joining, now);
                    return new LoadStep { Effects = LoadEffect.JoinLocal };
                case LoadMessageKind.Joined:
                    if (!IsHost || Phase != LoadPhase.Joining || !joined.Add(sender))
                        return LoadStep.Reject("Unexpected or duplicate lobby join acknowledgement.");
                    return AdvanceJoined(now);
                case LoadMessageKind.ReleaseLobby:
                    if (IsHost || sender != host || Phase != LoadPhase.Joining || !localJoined)
                        return LoadStep.Reject("Lobby release requires an acknowledged local join and host authority.");
                    Phase = LoadPhase.Completed;
                    var released = Send(LoadMessageKind.ReleaseAcknowledged);
                    // Runtime applies this BEFORE sending the acknowledgement. The host may
                    // open the ordinary requester immediately after receiving the final ack.
                    released.Effects = LoadEffect.ReleaseLocal;
                    return released;
                case LoadMessageKind.ReleaseAcknowledged:
                    if (!IsHost || Phase != LoadPhase.Releasing || !releaseAcknowledged.Add(sender))
                        return LoadStep.Reject("Unexpected or duplicate lobby release acknowledgement.");
                    if (releaseAcknowledged.Count != peers.Count) return new LoadStep();
                    Phase = LoadPhase.Completed;
                    return new LoadStep { Effects = LoadEffect.ReleaseLocal | LoadEffect.OpenLoadMenu };
                case LoadMessageKind.Abort:
                    if (!IsHost && sender != host) return LoadStep.Reject("Only the host may cancel this client transition.");
                    return AbortCore("A participant cancelled the transition.", IsHost);
                default:
                    return LoadStep.Reject("Unexpected transition message.");
            }
        }
        internal LoadStep Prepared(long now)
        {
            if (Phase != LoadPhase.Preparing || localPrepared) return LoadStep.Reject("Local preparation is not pending.");
            var clock = CheckClock(now); if (clock != null) return clock;
            localPrepared = true; prepared.Add(self);
            return IsHost ? AdvancePrepared(now) : Send(LoadMessageKind.Prepared);
        }
        internal LoadStep Stopped(long now)
        {
            if (Phase != LoadPhase.Stopping || localStopped) return LoadStep.Reject("Local stopping is not pending.");
            var clock = CheckClock(now); if (clock != null) return clock;
            localStopped = true; stopped.Add(self); Enter(LoadPhase.Draining, now);
            var result = Send(LoadMessageKind.Stopped);
            var progress = AdvanceStopped(now);
            result.Effects |= progress.Effects; result.Outbound.AddRange(progress.Outbound);
            return result;
        }
        internal LoadStep PublishLobby(ulong id, long now)
        {
            if (!IsHost || Phase != LoadPhase.WaitingLobby || !localDrained || drained.Count != peers.Count || id == 0)
                return LoadStep.Reject("Only an all-streams-drained host may publish the lobby.");
            var clock = CheckClock(now); if (clock != null) return clock;
            LobbyId = id; Enter(LoadPhase.Joining, now);
            var result = Send(LoadMessageKind.Lobby, id); result.Effects = LoadEffect.JoinLocal;
            return result;
        }
        internal LoadStep Joined(long now)
        {
            if (Phase != LoadPhase.Joining || localJoined) return LoadStep.Reject("Local joining is not pending.");
            var clock = CheckClock(now); if (clock != null) return clock;
            localJoined = true; joined.Add(self);
            return IsHost ? AdvanceJoined(now) : Send(LoadMessageKind.Joined);
        }
        internal LoadStep Tick(long now) => Phase == LoadPhase.Idle || IsTerminal ? new LoadStep() : CheckClock(now) ?? new LoadStep();
        internal LoadStep Abort(string reason) => Phase == LoadPhase.Idle || IsTerminal
            ? LoadStep.Reject("No live transition to cancel.")
            : AbortCore(string.IsNullOrWhiteSpace(reason) ? "Local transition failed." : reason, true);

        private LoadStep AdvancePrepared(long now)
        {
            if (prepared.Count != peers.Count) return new LoadStep();
            IsCommitted = true; Enter(LoadPhase.Stopping, now);
            var result = Send(LoadMessageKind.Stop); result.Effects = LoadEffect.StopLocal;
            return result;
        }
        private LoadStep AdvanceStopped(long now)
        {
            if (!localStopped || localDrained || stopped.Count != peers.Count) return new LoadStep();
            localDrained = true; drained.Add(self);
            if (IsHost) return AdvanceDrained(now);
            Enter(LoadPhase.WaitingLobby, now); return Send(LoadMessageKind.Drained);
        }
        private LoadStep AdvanceDrained(long now)
        {
            if (!localDrained || drained.Count != peers.Count) return new LoadStep();
            Enter(LoadPhase.WaitingLobby, now); return new LoadStep { Effects = LoadEffect.CreateLobby };
        }
        private LoadStep AdvanceJoined(long now)
        {
            if (joined.Count != peers.Count) return new LoadStep();
            Enter(LoadPhase.Releasing, now); releaseAcknowledged.Add(self);
            if (releaseAcknowledged.Count == peers.Count)
            {
                Phase = LoadPhase.Completed;
                return new LoadStep { Effects = LoadEffect.ReleaseLocal | LoadEffect.OpenLoadMenu };
            }
            return Send(LoadMessageKind.ReleaseLobby);
        }
        private LoadStep CheckClock(long now)
        {
            if (now < lastClock) return AbortCore("Transition clock regressed.", true);
            lastClock = now;
            return now - phaseStarted >= timeout ? AbortCore("Transition phase timed out.", true) : null;
        }
        private void Enter(LoadPhase phase, long now) { Phase = phase; phaseStarted = lastClock = now; }
        private LoadStep AbortCore(string reason, bool broadcast)
        {
            FailureReason = reason; Phase = IsCommitted ? LoadPhase.Failed : LoadPhase.Aborted;
            var result = new LoadStep { Effects = LoadEffect.Cancelled };
            if (broadcast) result.Outbound.Add(Packet(IsCommitted ? LoadMessageKind.CommittedAbort : LoadMessageKind.Abort));
            return result;
        }
        private LoadPacket Packet(LoadMessageKind kind, ulong lobby = 0) =>
            new LoadPacket { Kind = kind, Session = session, Operation = Operation, LobbyId = lobby };
        private LoadStep Send(LoadMessageKind kind, ulong lobby = 0)
        { var result = new LoadStep(); result.Outbound.Add(Packet(kind, lobby)); return result; }
        private static bool ValidOperation(string operation) => operation?.Length == 32 && Guid.TryParseExact(operation, "N", out _);
    }
}
