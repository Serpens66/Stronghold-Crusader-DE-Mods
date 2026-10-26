using System;
using System.Collections.Generic;
using BetterPathfindingTest.Core;
using BugfixesAndQoL.UnitCommands;

internal sealed class ProductionOracle
{
    private readonly GridSnapshot map;
    private readonly WeightedGridSearchKernel kernel;
    private RouteQuery active, edgeQuery;
    internal readonly List<RouteQuery> History = new List<RouteQuery>();
    internal ProductionOracle(GridSnapshot map)
    { this.map = map; kernel = new WeightedGridSearchKernel(map.Width, map.Height, Edge); }
    private bool Edge(int from, int to, int d, out bool wet, out bool structure) => map.Edge(from, d, edgeQuery, out _, out wet, out structure);
    internal RouteResult Find(RouteQuery query, bool shared = true)
    {
        bool changedContext = active != null && (active.Terminals.Length != 0 || query.Terminals.Length != 0);
        active = query; History.Add(query.Copy());
        // Production caches traversal independently from its request-level structure filter.
        edgeQuery = query.Copy(); edgeQuery.ExcludeStructures = false;
        var bounds = new MoatSearchLimit[query.Bounds.Length];
        for (int i = 0; i < bounds.Length; i++) bounds[i] = new MoatSearchLimit(query.Bounds[i].Ground, query.Bounds[i].Moat, query.Bounds[i].Maximum);
        // Different permission contexts cannot reuse the old production edge cache.
        if (changedContext || query.Terminals.Length != 0) kernel.Invalidate();
        bool found = kernel.Search(query.Start, query.Target, query.GroundCost, query.MoatCost, query.MaximumEdges, query.RequireMoat, query.ExcludeStructures, bounds, shared, out int[] nodes);
        if (!found) return RouteResult.Failed(kernel.LastSearchBudgetExceeded ? SearchStatus.Inconclusive : SearchStatus.Unreachable, "production-kernel-none");
        var directions = new byte[nodes.Length - 1]; for (int i = 0; i < directions.Length; i++) directions[i] = (byte)kernel.Direction(nodes[i], nodes[i + 1]);
        return map.Describe(query, directions);
    }
}
