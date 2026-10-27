#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using BetterPathfindingTest.Core;

namespace BetterPathfindingTest.Service
{
    // Private seam: swapping the search kernel does not change the experimental client API.
    internal interface IResidentBackend : IDisposable
    {
        WorkBudget Budget { get; }
        long ChangedNodes { get; }
        double MaximumSliceMilliseconds { get; }
        void BeginOperation(long maximumBytes);
        void Update(GridSnapshot map);
        RouteResult[] FindPaths(IList<RouteQuery> queries);
        long TransferReturnedBuffers(RouteResult[] routes);
    }
    internal sealed class AreaResidentBackend : IResidentBackend
    {
        private readonly AreaNavigator navigator;
        internal AreaResidentBackend(GridSnapshot map, WorkBudget budget) { navigator = new AreaNavigator(map, budget); }
        public WorkBudget Budget => navigator.Budget;
        public long ChangedNodes => navigator.ChangedNodes;
        public double MaximumSliceMilliseconds => navigator.MaximumSliceMs;
        public void BeginOperation(long maximumBytes) => navigator.BeginOperation(maximumBytes);
        public void Update(GridSnapshot map) => navigator.Update(map);
        public RouteResult[] FindPaths(IList<RouteQuery> queries) => navigator.FindPaths(queries);
        public long TransferReturnedBuffers(RouteResult[] routes) => navigator.TransferReturnedBuffers(routes);
        public void Dispose() => navigator.Dispose();
    }
}
#endif
