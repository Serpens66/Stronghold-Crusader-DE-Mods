namespace MoatMove
{
    // Fixed comparison profile: no settings registration or persistence.
    internal sealed class MoatMoveOptions
    {
        public bool EnableMod => true;
        public bool EnableImprovedMoatFilling => false;
        public bool EnableLadderAttackPathfindingFix => false;
        public bool EnableMoveFormationEnhancements => false;
        public int MoveFormationSpacing => MoveFormationSpacingPolicy.Default;
        public int FriendlyMoatMovementMode => (int)MoatMove.FriendlyMoatMovementMode.Exact;
        internal FriendlyMoatMovementMode GetFriendlyMoatMovementMode() =>
            MoatMove.FriendlyMoatMovementMode.Exact;
    }
}
