namespace OxTetherIdleFixTest
{
    internal enum OxEpisodeAction
    {
        None,
        ConfirmAndRepair,
        Verified,
        Unverified
    }

    internal readonly struct OxObservation
    {
        public OxObservation(
            int unitId,
            uint globalId,
            ushort state,
            ushort pathFlags,
            ushort alternateTargetMarker,
            ushort currentX,
            ushort currentY,
            ushort requestedX,
            ushort requestedY)
        {
            UnitId = unitId;
            GlobalId = globalId;
            State = state;
            PathFlags = pathFlags;
            AlternateTargetMarker = alternateTargetMarker;
            CurrentX = currentX;
            CurrentY = currentY;
            RequestedX = requestedX;
            RequestedY = requestedY;
        }

        public int UnitId { get; }
        public uint GlobalId { get; }
        public ushort State { get; }
        public ushort PathFlags { get; }
        public ushort AlternateTargetMarker { get; }
        public ushort CurrentX { get; }
        public ushort CurrentY { get; }
        public ushort RequestedX { get; }
        public ushort RequestedY { get; }

        public bool HasIdleBugSignature =>
            (State == 1 || State == 3) &&
            PathFlags == 0 &&
            AlternateTargetMarker != 0 &&
            (CurrentX != RequestedX || CurrentY != RequestedY);

        public ushort ExpectedStateAfterRepair => State == 1 ? (ushort)2 : (ushort)4;

        public bool IsSameCandidateAs(in OxObservation other) =>
            UnitId == other.UnitId &&
            GlobalId == other.GlobalId &&
            State == other.State &&
            CurrentX == other.CurrentX &&
            CurrentY == other.CurrentY &&
            RequestedX == other.RequestedX &&
            RequestedY == other.RequestedY &&
            AlternateTargetMarker == other.AlternateTargetMarker;
    }

    internal sealed class OxIdleEpisodePolicy
    {
        internal const int RequiredConsecutiveTicks = 50;
        internal const int VerificationTicks = 20;

        private OxObservation candidate;
        private int lastTick;
        private int consecutiveTicks;
        private int repairTick;
        private ushort expectedState;
        private uint episodeGlobalId;
        private Phase phase;

        public bool IsActive => phase != Phase.None;

        public OxEpisodeAction Observe(in OxObservation observation, int tick)
        {
            if (phase == Phase.AwaitingVerification)
                return ObserveVerification(observation, tick);

            if (phase == Phase.Suppressed)
            {
                // A failed repair is attempted only once while the exact same stuck
                // snapshot persists. Movement or a new target starts a new episode.
                if (candidate.IsSameCandidateAs(observation) && observation.HasIdleBugSignature)
                    return OxEpisodeAction.None;

                Reset();
            }

            if (!observation.HasIdleBugSignature)
            {
                Reset();
                return OxEpisodeAction.None;
            }

            if (phase != Phase.Tracking ||
                tick != lastTick + 1 ||
                !candidate.IsSameCandidateAs(observation))
            {
                candidate = observation;
                lastTick = tick;
                consecutiveTicks = 1;
                phase = Phase.Tracking;
                return OxEpisodeAction.None;
            }

            candidate = observation;
            lastTick = tick;
            consecutiveTicks++;
            if (consecutiveTicks < RequiredConsecutiveTicks)
                return OxEpisodeAction.None;

            repairTick = tick;
            expectedState = observation.ExpectedStateAfterRepair;
            episodeGlobalId = observation.GlobalId;
            phase = Phase.AwaitingVerification;
            return OxEpisodeAction.ConfirmAndRepair;
        }

        public void Cancel()
        {
            Reset();
        }

        private OxEpisodeAction ObserveVerification(in OxObservation observation, int tick)
        {
            if (observation.GlobalId != episodeGlobalId)
            {
                Reset();
                return OxEpisodeAction.Unverified;
            }

            if (observation.State == expectedState)
            {
                Reset();
                return OxEpisodeAction.Verified;
            }

            if (tick - repairTick < VerificationTicks)
                return OxEpisodeAction.None;

            phase = Phase.Suppressed;
            return OxEpisodeAction.Unverified;
        }

        private void Reset()
        {
            candidate = default;
            lastTick = 0;
            consecutiveTicks = 0;
            repairTick = 0;
            expectedState = 0;
            episodeGlobalId = 0;
            phase = Phase.None;
        }

        private enum Phase
        {
            None,
            Tracking,
            AwaitingVerification,
            Suppressed
        }
    }
}
