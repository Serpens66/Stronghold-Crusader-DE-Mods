namespace MoatMove
{
    // Capture the config once at startup; command snapshots cannot change mid-order.
    internal sealed class MoatMoveOptions
    {
        private readonly MoatMove.FriendlyMoatMovementMode mode;
        public MoatMoveOptions() : this("precise") { }
        internal MoatMoveOptions(string value)
        {
            NativeFast = string.Equals(value, "FastNative", System.StringComparison.OrdinalIgnoreCase);
            if (NativeFast || string.Equals(value, "fast", System.StringComparison.OrdinalIgnoreCase))
                mode = MoatMove.FriendlyMoatMovementMode.RequiredOnly;
            else if (string.Equals(value, "precise", System.StringComparison.OrdinalIgnoreCase))
                mode = MoatMove.FriendlyMoatMovementMode.Exact;
            else throw new System.ArgumentException("Movement mode must be precise, fast or FastNative.", nameof(value));
        }
        internal bool NativeFast { get; }
        internal string ModeName => NativeFast ? "FastNative" : mode == MoatMove.FriendlyMoatMovementMode.RequiredOnly ? "fast" : "precise";
        public bool EnableMod => true;
        public bool EnableImprovedMoatFilling => false;
        public bool EnableLadderAttackPathfindingFix => false;
        public bool EnableMoveFormationEnhancements => false;
        public int MoveFormationSpacing => MoveFormationSpacingPolicy.Default;
        public int FriendlyMoatMovementMode => (int)mode;
        internal FriendlyMoatMovementMode GetFriendlyMoatMovementMode() =>
            mode;
    }
}
