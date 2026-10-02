namespace EnemyGatePathfindingTest
{
    // Pure interpretation of values captured during one existing DA020 invocation.
    // Role compatibility never identifies a native caller and never rewrites its argument.
    internal readonly struct BuildingSearchPlayerContext
    {
        internal BuildingSearchPlayerContext(int rawSearchPlayer, int tribeId,
            int snapshotOwner, uint snapshotGlobal, int liveOwner, uint liveGlobal,
            int leaderId, uint leaderGlobal, int nativeLeaderControl,
            int planningPlayer, bool editor)
        {
            RawSearchPlayer = rawSearchPlayer; TribeId = tribeId;
            SnapshotOwner = snapshotOwner; SnapshotGlobal = snapshotGlobal;
            LiveOwner = liveOwner; LiveGlobal = liveGlobal;
            LeaderId = leaderId; LeaderGlobal = leaderGlobal;
            NativeLeaderControl = nativeLeaderControl; PlanningPlayer = planningPlayer;
            EffectivePlanningPlayer = planningPlayer == 0 && !editor ? 1 : planningPlayer;
            Editor = editor;
            bool planner = ValidPlayer(EffectivePlanningPlayer) && rawSearchPlayer == EffectivePlanningPlayer;
            bool control = ValidPlayer(nativeLeaderControl) && rawSearchPlayer == nativeLeaderControl;
            ArgumentRole = rawSearchPlayer == 0 ? "explicit-zero" :
                planner && control ? "compatible-planner-or-leader" :
                planner ? "compatible-planner" : control ? "compatible-leader-control" : "unexplained";
            Failure = tribeId <= 0 ? "invalid-tribe" :
                !ValidPlayer(snapshotOwner) || snapshotGlobal == 0 ? "snapshot-untracked" :
                liveGlobal == 0 || liveGlobal != snapshotGlobal ? "tribe-identity-mismatch" :
                liveOwner != snapshotOwner ? "tribe-owner-mismatch" :
                leaderId <= 0 || leaderGlobal == 0 ? "leader-identity-unverified" :
                !ValidPlayer(nativeLeaderControl) ? "invalid-leader-control-word" :
                nativeLeaderControl != liveOwner ? "leader-control-mismatch" :
                ArgumentRole == "unexplained" ? "unexplained-search-player" : null;
            MovementPlayer = Failure == null ? nativeLeaderControl : -1;
        }

        internal int RawSearchPlayer { get; }
        internal int TribeId { get; }
        internal int SnapshotOwner { get; }
        internal uint SnapshotGlobal { get; }
        internal int LiveOwner { get; }
        internal uint LiveGlobal { get; }
        internal int LeaderId { get; }
        internal uint LeaderGlobal { get; }
        internal int NativeLeaderControl { get; }
        internal int PlanningPlayer { get; }
        internal int EffectivePlanningPlayer { get; }
        internal bool Editor { get; }
        internal int MovementPlayer { get; }
        internal string ArgumentRole { get; }
        internal string Failure { get; }
        internal bool RoleDifference => MovementPlayer > 0 && RawSearchPlayer != MovementPlayer;

        internal static int NativeControlWord(byte low, byte high) => unchecked((short)(low | (high << 8)));
        private static bool ValidPlayer(int value) => value > 0 && value <= 8;
    }
}
