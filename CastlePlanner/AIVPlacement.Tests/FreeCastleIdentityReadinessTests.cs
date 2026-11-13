using CastlePlanner;

internal static class FreeCastleIdentityReadinessTests
{
    internal static void Run()
    {
        var gate = new FreeCastleIdentityReadiness();
        var readiness = new APIShared.PlayStateReadinessBoundary();
        int notifications = 0;
        if (!gate.TryBindPlayStateReadiness(readiness, _ => notifications++, out _))
            throw new Exception("Shared PlayState readiness registration failed.");
        void Expect(FreeCastleIdentityStatus expected, bool resolved = false,
            bool conflict = false, bool spectator = false, bool roster = true)
        {
            var actual = gate.Evaluate(resolved, conflict, spectator, roster);
            if (actual != expected) throw new Exception($"Identity readiness: expected {expected}, got {actual}.");
        }
        gate.Begin(7);
        Expect(FreeCastleIdentityStatus.Waiting, resolved: true);
        if (gate.IsReady) throw new Exception("An early native start authorized selection.");
        gate.MarkStarted(6, 10000);
        if (gate.HasStarted) throw new Exception("An old mission marked the current mission started.");
        gate.MarkStarted(7, 10000);
        readiness.Publish(6);
        Expect(FreeCastleIdentityStatus.Waiting, resolved: true, spectator: true);
        if (gate.IsReady || gate.IsCurrentPlayStateReady)
            throw new Exception("A previous spectator PlayState was accepted for the new human game.");
        if (gate.ShouldReportDelay(10029) || !gate.ShouldReportDelay(10030) || gate.ShouldReportDelay(10100))
            throw new Exception("The support delay notice is not once per waiting session.");
        Expect(FreeCastleIdentityStatus.Waiting, resolved: true);
        readiness.Publish(7); // A slow MP load delivers the first current-world state after the notice.
        Expect(FreeCastleIdentityStatus.Waiting);
        Expect(FreeCastleIdentityStatus.Waiting, resolved: true, roster: false);
        Expect(FreeCastleIdentityStatus.Ready, resolved: true);
        if (!gate.IsReady) throw new Exception("Final identity did not authorize selection.");
        if (notifications != 2) throw new Exception("The consumer did not observe shared readiness notifications.");

        gate.Begin(8); // Map replacement/unload must discard the old ready identity.
        if (gate.IsReady || gate.HasStarted) throw new Exception("Old identity survived replacement.");
        gate.MarkStarted(8, 20);
        gate.MarkStarted(8, 49); // Repeated notifications cannot postpone the support notice.
        if (!gate.ShouldReportDelay(50)) throw new Exception("Repeated starts postponed the support notice.");
        Expect(FreeCastleIdentityStatus.Waiting, resolved: true);
        readiness.Publish(8);
        Expect(FreeCastleIdentityStatus.Ready, resolved: true);
        readiness.Clear();
        Expect(FreeCastleIdentityStatus.Waiting, resolved: true);
        if (gate.IsCurrentPlayStateReady) throw new Exception("A retired world retained readiness.");
        gate.Begin(9);
        gate.MarkStarted(9, 0);
        readiness.Publish(9);
        Expect(FreeCastleIdentityStatus.Conflict, resolved: true, conflict: true);
        Expect(FreeCastleIdentityStatus.Spectator, resolved: true, spectator: true);
        if (gate.IsReady) throw new Exception("A spectator's viewed AI authorized a personal selection.");
        gate.Begin(10);
        gate.MarkStarted(10, 10);
        readiness.Publish(10);
        Expect(FreeCastleIdentityStatus.Ready, resolved: true);
        if (gate.ShouldReportDelay(1000)) throw new Exception("A ready identity emitted a waiting notice.");
        var replay = new FreeCastleIdentityReadiness();
        var lateReadiness = new APIShared.PlayStateReadinessBoundary();
        lateReadiness.Publish(10);
        int replays = 0;
        replay.Begin(10);
        replay.MarkStarted(10, 0);
        if (!replay.TryBindPlayStateReadiness(lateReadiness, notification => { if (notification.IsReplay) replays++; }, out _) ||
            replays != 1 || replay.Evaluate(true, false, false, true) != FreeCastleIdentityStatus.Ready)
            throw new Exception("A late consumer could not use the shared ready snapshot/replay.");
        if (!FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.AbortRequest, 3, 0) ||
            !FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.AbortRequest, 3, 4))
            throw new Exception("A verified participant could not report a missing/conflicting local identity.");
        if (FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.AbortRequest, 0, 3) ||
            FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.SelectionBegin, 3, 4) ||
            FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.PreviewReady, 3, 0))
            throw new Exception("The abort exception weakened sender authentication or gameplay slot matching.");
    }
}

// Public API boundary doubles: the unmodified production gate consumes the capability,
// including session invalidation and late replay. Native publication is tested in APIShared.
namespace APIShared
{
    internal sealed class MissionContext
    {
        internal long SessionId { get; set; }
    }
    internal sealed class MissionPlayStateReadyNotification
    {
        internal MissionContext Context { get; set; }
        internal bool IsReplay { get; set; }
    }
    internal interface IMissionPlayStateReadinessCapability
    {
        MissionContext Current { get; }
        bool TryRegisterObserver(string id, Action<MissionPlayStateReadyNotification> onReady,
            out NativeCapabilityDiagnostic diagnostic);
    }
    internal sealed class PlayStateReadinessBoundary : IMissionPlayStateReadinessCapability
    {
        private readonly List<Action<MissionPlayStateReadyNotification>> observers = new();
        public MissionContext Current { get; private set; }
        public bool TryRegisterObserver(string id, Action<MissionPlayStateReadyNotification> onReady,
            out NativeCapabilityDiagnostic diagnostic)
        {
            diagnostic = null;
            observers.Add(onReady);
            if (Current != null) onReady(new MissionPlayStateReadyNotification { Context = Current, IsReplay = true });
            return true;
        }
        internal void Publish(long session)
        {
            Current = new MissionContext { SessionId = session };
            foreach (var observer in observers)
                observer(new MissionPlayStateReadyNotification { Context = Current });
        }
        internal void Clear() => Current = null;
    }
}
