using System;
using System.Collections.Generic;
using APIShared.Pathfinding;

namespace MoatMove
{
    // The production planner is used unchanged. This double replaces only the
    // search boundary so dispatch, costs and live publication checks are observable.
    public static unsafe class RouteSearchEventIntegrationTests
    {
        private sealed class SearchBoundary : IMoatSearchKernel
        {
            internal int Calls;
            internal long Ground, Moat;
            internal bool Throw, ReturnInvalidEdge;
            public long Expanded => Calls;
            public long Searches => Calls;
            public long FieldHits => 0;
            public int CachedFields => 0;
            public bool LastSearchBudgetExceeded => false;
            public void Invalidate() { }
            public int Direction(int from, int to) => 2;
            public bool Search(int start, int destination, long ground, long moat, int maximumEdges,
                bool requireMoat, bool excludeStructures, MoatSearchLimit[] limits,
                bool shareField, out int[] path, int maximumExpanded = int.MaxValue)
            {
                Calls++; Ground = ground; Moat = moat;
                if (Throw) throw new InvalidOperationException("search boundary");
                path = ReturnInvalidEdge ? new[] { start, start + 1 } : new[] { start };
                return true;
            }
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Route events: " + message); }

        public static void Run()
        {
            int* rows = stackalloc int[2400];
            uint* flags = stackalloc uint[20];
            ushort* buildings = stackalloc ushort[20];
            byte* heights = stackalloc byte[20];
            byte* occupancy = stackalloc byte[20];
            byte* directions = stackalloc byte[8];
            for (int i = 0; i < 2400; i++) rows[i] = 0;
            for (int i = 0; i < 20; i++) { flags[i] = 0; buildings[i] = 0; heights[i] = occupancy[i] = 0; }
            for (int i = 0; i < 8; i++) directions[i] = 0;
            var planner = new WeightedMoatRoutePlanner(rows, flags, buildings, heights, occupancy, directions, null,
                (player, tile) => CompletedMoatRelationship.Friendly, tile => false);
            var search = new SearchBoundary();
            planner.KernelFactory = (width, height, edge) => search;
            Check(WeightedMovementCostProfile.TryCreate(1, 1, 0, 0, 0, 0, false,
                out var profile, out _), "valid baseline profile");
            bool Build(int target = 10, bool reachability = false) => planner.TryBuildCore(1, 10, 10,
                target, 10, profile, false, true, MoatTraversalPolicy.FriendlyOnly,
                out _, out _, reachability: reachability);
            Check(Build() && search.Ground == profile.GetEdgeFixedCost(false), "unowned search unchanged");
            planner.PublisherGuid = "community.publisher";
            bool skip = false;
            int pre = 0;
            var results = new List<RouteSearchPostEventArgs>();
            Check(RouteSearchEvents.TryRegister("community.observer", "integration", args => {
                pre++; args.GroundEdgeCost = 31; args.MoatEdgeCost = 47;
                args.SkipOriginalFunction = skip;
            }, args => results.Add(args), out _), "public registration");
            Check(Build() && search.Ground == 31 && search.Moat == 47, "effective costs reach search");
            Check(results.Count == 1 && results[0].Success && results[0].Context.SourceGuid == planner.PublisherGuid,
                "successful calculation reports publisher and result");
            int calls = search.Calls;
            skip = true;
            Check(!Build() && search.Calls == calls && results.Count == 1, "skip prevents search and Post");
            skip = false;
            Check(!Build(-1) && search.Calls == calls && results.Count == 2 &&
                !results[1].Success && results[1].Reason == "invalid-coordinate", "ordinary failure receives Post");
            int notifications = pre;
            Check(Build(reachability: true) && search.Ground == 1 && search.Moat == 1 &&
                pre == notifications && results.Count == 2, "topological query bypasses preference events");
            search.Throw = true;
            bool threw = false;
            try { Build(); } catch (InvalidOperationException) { threw = true; }
            Check(threw && results.Count == 2, "publisher exception propagates without Post");
            search.Throw = false;
            search.ReturnInvalidEdge = true;
            Check(!Build(11) && results.Count == 3 && results[2].Reason == "live-edge-changed",
                "changed preference cannot bypass live edge checks");
            Check(RouteSearchEvents.CallbackFailures == 0, "no swallowed fixture callback failures");
            Console.WriteLine("PASS: production route Pre/Post integration, effective costs, skip, failure, reachability and live-edge safety.");
        }
    }
}
