extern alias BugfixesRuntime;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
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
            // Anchor uses Fast's deferred flow (RequiredOnly) with the native anchor core.
            Anchor = string.Equals(value, "anchor", System.StringComparison.OrdinalIgnoreCase);
            if (NativeFast || Anchor || string.Equals(value, "fast", System.StringComparison.OrdinalIgnoreCase))
                mode = MoatMove.FriendlyMoatMovementMode.RequiredOnly;
            else if (string.Equals(value, "precise", System.StringComparison.OrdinalIgnoreCase))
                mode = MoatMove.FriendlyMoatMovementMode.Exact;
            else throw new System.ArgumentException("Movement mode must be precise, fast, FastNative or anchor.", nameof(value));
        }
        internal bool NativeFast { get; }
        internal bool Anchor { get; }
        // Anchor mode only: single-unit ground moves through the core's resumable fields as well.
        internal bool AnchorSingleMoves { get; set; }
        // Anchor mode only: burning pitch as APIShared route tile rules (FireRouteRules); moving units
        // re-plan around new fire only with AnchorSingleMoves.
        internal bool AvoidFire { get; set; }
        // DEVDIAG in-game check of Forbidden tile rules (FastMovementScheduler.ProbeRouteRules); 0 = off.
        internal int RuleProbeTicks { get; set; }
        // DEVDIAG in-game check of route cost rules (FastMovementScheduler.ProbeMoatCost); 0 = off.
        internal int MoatCostProbeTicks { get; set; }
        // DEVDIAG in-game timing of full cursor topology builds (FastMovementScheduler.ProbeCursorTopology); 0 = off.
        internal int TopologyProbeTicks { get; set; }
        // Plugin folder; the anchor core DLL ships next to the mod assembly.
        internal string PluginDirectory { get; set; }
        internal string ModeName => NativeFast ? "FastNative" : Anchor ? "anchor" : mode == MoatMove.FriendlyMoatMovementMode.RequiredOnly ? "fast" : "precise";
        public bool EnableMod => true;
        public bool EnableImprovedMoatFilling => false;
        public bool EnableLadderAttackPathfindingFix => false;
        public bool EnableMoveFormationEnhancements => false;
        public int FriendlyMoatMovementMode => (int)mode;
        internal FriendlyMoatMovementMode GetFriendlyMoatMovementMode() =>
            mode;
    }
}
