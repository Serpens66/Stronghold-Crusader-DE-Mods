using CastlePlanner;

internal static class FreeCastleIdentityReadinessTests
{
    internal static void Run()
    {
        var gate = new FreeCastleIdentityReadiness();
        void Expect(FreeCastleIdentityStatus expected, double now, bool resolved = false,
            bool conflict = false, bool spectator = false, bool roster = true)
        {
            var actual = gate.Evaluate(now, resolved, conflict, spectator, roster);
            if (actual != expected) throw new Exception($"Identity readiness: expected {expected}, got {actual}.");
        }
        gate.Begin(7);
        Expect(FreeCastleIdentityStatus.Waiting, 10000, resolved: true);
        if (gate.IsReady) throw new Exception("An early native start authorized selection.");
        gate.MarkStarted(6, 10000);
        if (gate.HasStarted) throw new Exception("An old mission started the current deadline.");
        gate.MarkStarted(7, 10000);
        var staleSpectator = gate.Evaluate(10001, true, false, true, true, currentPlayState: false);
        if (staleSpectator != FreeCastleIdentityStatus.Waiting || gate.IsReady)
            throw new Exception("A previous spectator PlayState was accepted for the new human game.");
        Expect(FreeCastleIdentityStatus.Waiting, 10001);
        Expect(FreeCastleIdentityStatus.Waiting, 10002, resolved: true, roster: false);
        Expect(FreeCastleIdentityStatus.Ready, 10029, resolved: true);
        if (!gate.IsReady) throw new Exception("Final identity did not authorize selection.");

        gate.Begin(8); // Map replacement/unload must discard the old ready identity.
        if (gate.IsReady || gate.HasStarted) throw new Exception("Old identity survived replacement.");
        gate.MarkStarted(8, 20);
        gate.MarkStarted(8, 49); // Repeated notifications cannot extend the deadline.
        Expect(FreeCastleIdentityStatus.TimedOut, 50);
        Expect(FreeCastleIdentityStatus.TimedOut, 51, resolved: true);
        gate.Begin(9);
        gate.MarkStarted(9, 0);
        Expect(FreeCastleIdentityStatus.Conflict, 1, resolved: true, conflict: true);
        Expect(FreeCastleIdentityStatus.Spectator, 1, resolved: true, spectator: true);
        if (gate.IsReady) throw new Exception("A spectator's viewed AI authorized a personal selection.");
        gate.Begin(10);
        gate.MarkStarted(10, 10);
        Expect(FreeCastleIdentityStatus.Ready, 11, resolved: true);
        if (!FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.AbortRequest, 3, 0) ||
            !FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.AbortRequest, 3, 4))
            throw new Exception("A verified participant could not report a missing/conflicting local identity.");
        if (FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.AbortRequest, 0, 3) ||
            FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.SelectionBegin, 3, 4) ||
            FreeCastlePacketRouting.IsAuthenticatedParticipantPacket(FreeCastlePacketKind.PreviewReady, 3, 0))
            throw new Exception("The abort exception weakened sender authentication or gameplay slot matching.");
    }
}
