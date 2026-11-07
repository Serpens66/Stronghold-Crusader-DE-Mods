using Pathfinding.UnitCommands;
namespace Pathfinding.Engine
{
    // Capture the config once at startup; command snapshots cannot change mid-order.
    internal sealed class EngineOptions
    {
        // Single-unit ground moves through the core's resumable fields as well.
        internal bool AnchorSingleMoves { get; set; }
        // The shared route core at all; without it every command keeps the exact command runtime.
        internal bool EnablePathfindingCore { get; set; } = true;
        // Units that can enter moats may cross finished friendly moats (only with the core).
        internal bool EnableFriendlyMoatTraversal { get; set; }
        // Burning pitch as APIShared route tile rules (FireRouteRules); moving units re-plan around
        // new fire only with AnchorSingleMoves.
        internal bool AvoidFire { get; set; }
        // With AvoidFire: wait in front of fire before the move is aborted, and the window in which a
        // soldier that left a fire continues to its old move target (seconds of game time, 40 ticks each).
        internal int FireWaitSeconds { get; set; } = 60;
        // With AvoidFire: the window in which a soldier that stood in a fire returns to its old position
        // (seconds of game time from reaching a fire-free tile; a pitch fire alone burns 22-27 s).
        internal int FireReturnSeconds { get; set; } = 120;
        // With AvoidFire: a fire-free detour may add this percentage of the remaining route, or at least
        // FireDetourMinTiles steps; a longer one counts as no route.
        internal int FireDetourPercent { get; set; } = 50;
        internal int FireDetourMinTiles { get; set; } = 10;
        // With AvoidFire: soldiers standing on a burning tile leave it (FireEscape).
        internal bool EscapeFire { get; set; } = true;
        // Tiles where a plague cloud damages units as route tile rules (PlagueRouteRules), with the same
        // re-planning, waiting and aborting as fire; only where units would really take damage (APIShared's
        // plague height policy for walls, gates, towers and keeps).
        internal bool AvoidPlague { get; set; }
        // With AvoidPlague: wait in front of plague before the move is aborted, and the window in which a
        // soldier that left plague continues to its old move target (seconds of game time).
        internal int PlagueWaitSeconds { get; set; } = 90;
        // With AvoidPlague: the window in which a soldier that stood in plague returns to its old position.
        internal int PlagueReturnSeconds { get; set; } = 180;
        // With AvoidPlague: soldiers leave tiles where plague damages them; on walls, gates, towers and keeps
        // only with EscapePlagueRaised (the garrison would otherwise leave its post).
        internal bool EscapePlague { get; set; } = true;
        internal bool EscapePlagueRaised { get; set; }
        // DEVDIAG in-game check of Forbidden tile rules (FastMovementScheduler.ProbeRouteRules); 0 = off.
        internal int RuleProbeTicks { get; set; }
        // DEVDIAG in-game check of route cost rules (FastMovementScheduler.ProbeMoatCost); 0 = off.
        internal int MoatCostProbeTicks { get; set; }
        // DEVDIAG in-game timing of full cursor topology builds (FastMovementScheduler.ProbeCursorTopology); 0 = off.
        internal int TopologyProbeTicks { get; set; }
        // Plugin folder; the anchor core DLL ships next to the mod assembly.
        internal string PluginDirectory { get; set; }
        // Value set of one mission, read from the host-synchronized settings.
        internal static EngineOptions Capture(PathfindingViewModel settings, string pluginDirectory) => new EngineOptions
        {
            PluginDirectory = pluginDirectory, EnablePathfindingCore = settings.EnableMod && settings.EnablePathfindingCore,
            EnableFriendlyMoatTraversal = settings.EnableFriendlyMoatTraversal, AnchorSingleMoves = settings.AnchorSingleMoves, AvoidFire = settings.AvoidFire,
            FireWaitSeconds = settings.FireWaitSeconds, FireReturnSeconds = settings.FireReturnSeconds,
            FireDetourPercent = settings.FireDetourPercent, FireDetourMinTiles = settings.FireDetourMinTiles, EscapeFire = settings.EscapeFire,
            AvoidPlague = settings.AvoidPlague, PlagueWaitSeconds = settings.PlagueWaitSeconds, PlagueReturnSeconds = settings.PlagueReturnSeconds,
            EscapePlague = settings.EscapePlague, EscapePlagueRaised = settings.EscapePlagueRaised,
        };
    }
}
