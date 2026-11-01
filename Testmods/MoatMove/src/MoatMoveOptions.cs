extern alias BugfixesRuntime;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
namespace MoatMove
{
    // Capture the config once at startup; command snapshots cannot change mid-order.
    internal sealed class MoatMoveOptions
    {
        // Single-unit ground moves through the core's resumable fields as well.
        internal bool AnchorSingleMoves { get; set; }
        // Burning pitch as APIShared route tile rules (FireRouteRules); moving units re-plan around
        // new fire only with AnchorSingleMoves.
        internal bool AvoidFire { get; set; }
        // With AvoidFire: wait in front of fire before the move is aborted, and the window in which a
        // soldier that left a fire returns (seconds of game time, 40 ticks each).
        internal int FireWaitSeconds { get; set; } = 10;
        // With AvoidFire: a fire-free detour may add this percentage of the remaining route, or at least
        // FireDetourMinTiles steps; a longer one counts as no route.
        internal int FireDetourPercent { get; set; } = 50;
        internal int FireDetourMinTiles { get; set; } = 10;
        // With AvoidFire: soldiers standing on a burning tile leave it (FireEscape).
        internal bool EscapeFire { get; set; } = true;
        // DEVDIAG in-game check of Forbidden tile rules (FastMovementScheduler.ProbeRouteRules); 0 = off.
        internal int RuleProbeTicks { get; set; }
        // DEVDIAG in-game check of route cost rules (FastMovementScheduler.ProbeMoatCost); 0 = off.
        internal int MoatCostProbeTicks { get; set; }
        // DEVDIAG in-game timing of full cursor topology builds (FastMovementScheduler.ProbeCursorTopology); 0 = off.
        internal int TopologyProbeTicks { get; set; }
        // Plugin folder; the anchor core DLL ships next to the mod assembly.
        internal string PluginDirectory { get; set; }
        public bool EnableMod => true;
        public bool EnableImprovedMoatFilling => false;
        public bool EnableLadderAttackPathfindingFix => false;
        public bool EnableMoveFormationEnhancements => false;
    }
}
