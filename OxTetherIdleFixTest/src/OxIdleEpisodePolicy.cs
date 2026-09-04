namespace OxTetherIdleFixTest
{
    internal static class OxTargetBlockadePolicy
    {
        internal static bool IsEligible(in OxObservation observation, bool episodeActive) =>
            !episodeActive &&
            (observation.State == 1 || observation.State == 3) &&
            observation.PathFlags == 2 &&
            observation.PathSize > 0 &&
            observation.PathSize <= ushort.MaxValue &&
            observation.PathCursor < observation.PathSize &&
            (observation.CurrentX != observation.RequestedX ||
             observation.CurrentY != observation.RequestedY);

        internal static bool IsEligibleMovingBlocker(in OxObservation observation) =>
            IsEligible(observation, episodeActive: false);

        internal static bool HasIndependentTarget(
            in OxObservation observation,
            ushort blockedTargetX,
            ushort blockedTargetY) =>
            observation.RequestedX != blockedTargetX ||
            observation.RequestedY != blockedTargetY;

        internal static bool ShouldReissueOriginalBlockerRoute(
            ushort originalState,
            ushort currentState,
            ushort originalX,
            ushort originalY,
            ushort requestedX,
            ushort requestedY) =>
            currentState == originalState &&
            (originalX != requestedX || originalY != requestedY);
    }

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
            ushort requestedY,
            ushort pathCursor = 0,
            uint pathSize = 0,
            ushort movingRelevant = 0,
            ushort pathRelated1 = 0,
            ushort primaryX = 0,
            ushort primaryY = 0,
            ushort nextX = 0,
            ushort nextY = 0,
            uint animationTimer = 0,
            uint carryGoods = 0,
            uint workerTargetGlobalId = 0,
            ushort linkedBuildingId = 0)
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
            PathCursor = pathCursor;
            PathSize = pathSize;
            MovingRelevant = movingRelevant;
            PathRelated1 = pathRelated1;
            PrimaryX = primaryX;
            PrimaryY = primaryY;
            NextX = nextX;
            NextY = nextY;
            AnimationTimer = animationTimer;
            CarryGoods = carryGoods;
            WorkerTargetGlobalId = workerTargetGlobalId;
            LinkedBuildingId = linkedBuildingId;
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
        public ushort PathCursor { get; }
        public uint PathSize { get; }
        public ushort MovingRelevant { get; }
        public ushort PathRelated1 { get; }
        public ushort PrimaryX { get; }
        public ushort PrimaryY { get; }
        public ushort NextX { get; }
        public ushort NextY { get; }
        public uint AnimationTimer { get; }
        public uint CarryGoods { get; }
        public uint WorkerTargetGlobalId { get; }
        public ushort LinkedBuildingId { get; }

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

        public bool IsSameGeneralStallAs(in OxObservation other) =>
            UnitId == other.UnitId &&
            GlobalId == other.GlobalId &&
            State == other.State &&
            CurrentX == other.CurrentX &&
            CurrentY == other.CurrentY &&
            RequestedX == other.RequestedX &&
            RequestedY == other.RequestedY;

        public bool HasDiagnosticTransitionFrom(in OxObservation previous) =>
            State != previous.State ||
            PathFlags != previous.PathFlags ||
            AlternateTargetMarker != previous.AlternateTargetMarker ||
            CurrentX != previous.CurrentX ||
            CurrentY != previous.CurrentY ||
            PrimaryX != previous.PrimaryX ||
            PrimaryY != previous.PrimaryY ||
            NextX != previous.NextX ||
            NextY != previous.NextY ||
            RequestedX != previous.RequestedX ||
            RequestedY != previous.RequestedY ||
            PathCursor != previous.PathCursor ||
            PathSize != previous.PathSize ||
            MovingRelevant != previous.MovingRelevant ||
            PathRelated1 != previous.PathRelated1 ||
            CarryGoods != previous.CarryGoods ||
            WorkerTargetGlobalId != previous.WorkerTargetGlobalId ||
            LinkedBuildingId != previous.LinkedBuildingId;
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
