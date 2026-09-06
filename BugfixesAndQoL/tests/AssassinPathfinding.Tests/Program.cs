using BugfixesAndQoL;
using System.Diagnostics;

internal static class Program
{
    private static int assertions;
    private static readonly (int X, int Y)[] Directions =
    {
        (0, -1), (1, -1), (1, 0), (1, 1),
        (0, 1), (-1, 1), (-1, 0), (-1, -1)
    };

    private static int Main(string[] args)
    {
        try
        {
            TestHeuristicContract();
            TestKnownWallChoices();
            TestRandomOracleAgreement();
            TestPartialSuffixOracleAgreement();
            TestNodeLimit();
            TestRuntimeIntegration(args);
            BenchmarkWallGroup();
            Console.WriteLine($"PASS: {assertions} Assassin A*/Dijkstra assertions.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void TestHeuristicContract()
    {
        int cardinal = AssassinClimbCostPolicy.GetCardinalMovementTicks(4);
        int diagonal = AssassinClimbCostPolicy.GetDiagonalMovementTicks(4);
        Check(AssassinAStarPolicy.EstimateOctileTicks(0, 0, 0, 0, cardinal, diagonal) == 0,
            "goal heuristic is zero");
        Check(AssassinAStarPolicy.EstimateOctileTicks(0, 0, 3, 0, cardinal, diagonal) == 3 * cardinal,
            "cardinal heuristic");
        Check(AssassinAStarPolicy.EstimateOctileTicks(0, 0, 3, 2, cardinal, diagonal) ==
              2 * diagonal + cardinal,
            "octile heuristic");
        Check(AssassinAStarPolicy.SaturatingAdd(int.MaxValue - 2, 3) == int.MaxValue,
            "cost addition saturates");
        Check(AssassinAStarPolicy.EstimateOctileTicks(0, 0, 2, 2, 5, 50) == 20,
            "heuristic caps an expensive diagonal at two cardinal steps");
        Check(AssassinAStarPolicy.ComesBefore(10, 8, 4, 11, 1, 0),
            "priority compares estimated total cost first");
        Check(AssassinAStarPolicy.ComesBefore(10, 7, 9, 10, 8, 0),
            "equal estimates compare actual cost second");
        Check(AssassinAStarPolicy.ComesBefore(10, 7, 2, 10, 7, 3),
            "equal estimates and costs preserve insertion order");

        for (int y = 0; y < 20; y++)
        for (int x = 0; x < 20; x++)
        for (int direction = 0; direction < Directions.Length; direction++)
        {
            int nx = x + Directions[direction].X;
            int ny = y + Directions[direction].Y;
            if ((uint)nx >= 20 || (uint)ny >= 20)
                continue;
            int here = AssassinAStarPolicy.EstimateOctileTicks(x, y, 19, 19, cardinal, diagonal);
            int next = AssassinAStarPolicy.EstimateOctileTicks(nx, ny, 19, 19, cardinal, diagonal);
            int edge = (direction & 1) == 0 ? cardinal : diagonal;
            Check(here <= edge + next, "octile heuristic is consistent on every ground edge");
            Check(here <= edge + AssassinClimbCostPolicy.NormalWallClimbTicks + next,
                "octile heuristic remains consistent on climb edges");
        }
    }

    private static void TestKnownWallChoices()
    {
        var graph = new TestGraph(15, 9, speedDelay: 2);
        graph.AddVerticalWall(7, 1, 7, climbable: true, climbTicks: 400);
        SearchResult dijkstra = Search(graph, 2, 4, 12, 4, useHeuristic: false, int.MaxValue);
        SearchResult astar = Search(graph, 2, 4, 12, 4, useHeuristic: true, int.MaxValue);
        Check(dijkstra.Found && astar.Found && dijkstra.Cost == astar.Cost,
            "normal-wall detour has identical optimal cost");
        Check(astar.ClimbEdges == 0, "expensive normal wall selects the faster detour");

        graph = new TestGraph(15, 9, speedDelay: 20);
        graph.AddVerticalWall(7, 1, 7, climbable: true, climbTicks: 80);
        dijkstra = Search(graph, 2, 4, 12, 4, useHeuristic: false, int.MaxValue);
        astar = Search(graph, 2, 4, 12, 4, useHeuristic: true, int.MaxValue);
        Check(dijkstra.Found && astar.Found && dijkstra.Cost == astar.Cost,
            "cheap climb has identical optimal cost");
        Check(astar.ClimbEdges > 0, "cheap climb is retained when faster");

        graph.ClimbingAllowed = false;
        astar = Search(graph, 2, 4, 12, 4, useHeuristic: true, int.MaxValue);
        Check(astar.Found && astar.ClimbEdges == 0,
            "disabled climbing retains a ground-only detour");
    }

    private static void TestRandomOracleAgreement()
    {
        var random = new Random(0x5A551);
        for (int graphIndex = 0; graphIndex < 80; graphIndex++)
        {
            var graph = new TestGraph(24, 24, random.Next(0, 18));
            for (int y = 0; y < graph.Height; y++)
            for (int x = 0; x < graph.Width; x++)
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                if (random.NextDouble() >= 0.13)
                    continue;
                bool cardinal = (direction & 1) == 0;
                graph.SetEdge(x, y, direction,
                    cardinal && random.NextDouble() < 0.62
                        ? Edge.Climb(random.Next(80, 481))
                        : Edge.Blocked);
            }

            for (int request = 0; request < 45; request++)
            {
                int sx = random.Next(graph.Width);
                int sy = random.Next(graph.Height);
                int tx = random.Next(graph.Width);
                int ty = random.Next(graph.Height);
                graph.ClimbingAllowed = random.Next(4) != 0;
                SearchResult expected = Search(graph, sx, sy, tx, ty, false, int.MaxValue);
                SearchResult actual = Search(graph, sx, sy, tx, ty, true, int.MaxValue);
                Check(expected.Found == actual.Found, "A* and Dijkstra agree on reachability");
                if (expected.Found)
                    Check(expected.Cost == actual.Cost, "A* and Dijkstra agree on optimal travel time");
            }
        }
    }

    private static void TestNodeLimit()
    {
        var graph = new TestGraph(30, 30, 3);
        SearchResult limited = Search(graph, 0, 0, 29, 29, true, 1);
        Check(!limited.Found && limited.Expanded == 1, "A* honors the native node limit");
    }

    private static void TestPartialSuffixOracleAgreement()
    {
        var random = new Random(0x5AFF1);
        for (int graphIndex = 0; graphIndex < 12; graphIndex++)
        {
            var graph = new TestGraph(18, 18, random.Next(0, 18));
            for (int y = 0; y < graph.Height; y++)
            for (int x = 0; x < graph.Width; x++)
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                if (random.NextDouble() < 0.16)
                {
                    bool cardinal = (direction & 1) == 0;
                    graph.SetEdge(x, y, direction,
                        cardinal && random.NextDouble() < 0.55
                            ? Edge.Climb(random.Next(80, 481))
                            : Edge.Blocked);
                }
            }

            int targetX = random.Next(graph.Width);
            int targetY = random.Next(graph.Height);
            var suffixCosts = new Dictionary<int, int>();
            for (int node = graphIndex % 5; node < graph.Width * graph.Height; node += 11)
            {
                SearchResult suffix = Search(
                    graph, node % graph.Width, node / graph.Width,
                    targetX, targetY, useHeuristic: false, int.MaxValue);
                if (suffix.Found)
                    suffixCosts[node] = suffix.Cost;
            }

            for (int request = 0; request < 30; request++)
            {
                int startX = random.Next(graph.Width);
                int startY = random.Next(graph.Height);
                SearchResult expected = Search(
                    graph, startX, startY, targetX, targetY,
                    useHeuristic: false, int.MaxValue);
                SearchResult actual = Search(
                    graph, startX, startY, targetX, targetY,
                    useHeuristic: true, int.MaxValue, suffixCosts);
                Check(expected.Found == actual.Found,
                    "partial exact suffix heuristic preserves reachability");
                if (expected.Found)
                    Check(expected.Cost == actual.Cost,
                        "partial exact suffix heuristic preserves optimal travel time");
            }
        }
    }

    private static void TestRuntimeIntegration(string[] args)
    {
        string root = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string runtimePath = Path.Combine(root, "BugfixesAndQoL", "src", "AssassinPathfindingRuntime.cs");
        string runtime = File.ReadAllText(runtimePath);
        Check(runtime.Contains("detour.Original(context, startX, startY, targetX, targetY, maximumNodes, continuation)"),
            "Vanilla builder remains in the Assassin request path");
        Check(runtime.IndexOf("detour.Original(context", StringComparison.Ordinal) <
              runtime.IndexOf("TryBuildWeightedRoute(", StringComparison.Ordinal),
            "Vanilla builder still executes before the managed replacement search");
        Check(runtime.Contains("DetailedDiagnosticsEnabled = false"),
            "detailed Assassin diagnostics default to off");
        Check(runtime.Contains("Assassin route publication contract failed"),
            "route publication contract failures remain immediately visible");
        Check(runtime.Contains("BuildRequestIndex()") && runtime.Contains("command.RequestIndex"),
            "Assassin identity resolution uses a command-bound index");
        Check(runtime.Contains("CloseCommandScope(MoveCommandKind, args.TribeId)") &&
              runtime.Contains("CloseCommandScope(TargetCommandKind, args.TribeId)"),
            "command-bound caches fail closed on mismatched event scopes");
        Check(runtime.Contains("if (totalCost == int.MaxValue)") &&
              runtime.Contains("sourceCost > totalCost"),
            "overflowed route costs cannot seed a suffix heuristic");
        Check(runtime.Contains("AssassinAStarPolicy.EstimateOctileTicks") &&
              runtime.Contains("estimatedTotalCosts"),
            "runtime heap uses the exact A* estimate");
        Check(!runtime.Contains("SamePcl", StringComparison.OrdinalIgnoreCase) &&
              !runtime.Contains("RequiredOnly", StringComparison.OrdinalIgnoreCase),
            "Assassin optimization contains no semantic fast-path shortcut");
    }

    private static void BenchmarkWallGroup()
    {
        var graph = new TestGraph(80, 80, speedDelay: 5);
        graph.AddVerticalWall(40, 2, 77, climbable: true, climbTicks: 400);
        long dijkstraExpanded = 0;
        long astarExpanded = 0;
        var stopwatch = Stopwatch.StartNew();
        for (int unit = 0; unit < 900; unit++)
        {
            int sy = 3 + unit % 74;
            SearchResult result = Search(graph, 4 + unit % 8, sy, 70, sy, false, int.MaxValue);
            dijkstraExpanded += result.Expanded;
        }
        double dijkstraMs = stopwatch.Elapsed.TotalMilliseconds;
        stopwatch.Restart();
        for (int unit = 0; unit < 900; unit++)
        {
            int sy = 3 + unit % 74;
            SearchResult result = Search(graph, 4 + unit % 8, sy, 70, sy, true, int.MaxValue);
            astarExpanded += result.Expanded;
        }
        double astarMs = stopwatch.Elapsed.TotalMilliseconds;
        Check(astarExpanded < dijkstraExpanded,
            "A* expands fewer nodes in the 900-unit wall scenario");
        Console.WriteLine(
            $"ASSASSIN SEARCH MODEL units=900 dijkstraExpanded={dijkstraExpanded} " +
            $"astarExpanded={astarExpanded} dijkstraMs={dijkstraMs:F2} astarMs={astarMs:F2}");
    }

    private static SearchResult Search(
        TestGraph graph,
        int startX,
        int startY,
        int targetX,
        int targetY,
        bool useHeuristic,
        int maximumNodes,
        IReadOnlyDictionary<int, int> suffixCosts = null)
    {
        int count = graph.Width * graph.Height;
        int start = startY * graph.Width + startX;
        int target = targetY * graph.Width + targetX;
        var costs = Enumerable.Repeat(int.MaxValue, count).ToArray();
        var parents = Enumerable.Repeat(-1, count).ToArray();
        var incomingClimb = new bool[count];
        var queue = new PriorityQueue<int, Priority>();
        int insertion = 0;
        costs[start] = 0;
        queue.Enqueue(start, Priority.Create(
            graph, startX, startY, targetX, targetY,
            0, insertion++, useHeuristic, suffixCosts));
        int expanded = 0;

        while (queue.Count > 0 && expanded < maximumNodes)
        {
            queue.TryDequeue(out int current, out Priority priority);
            if (priority.Cost != costs[current])
                continue;
            expanded++;
            if (current == target)
            {
                int climbs = 0;
                for (int node = target; parents[node] >= 0; node = parents[node])
                    if (incomingClimb[node]) climbs++;
                return new SearchResult(true, costs[target], expanded, climbs);
            }

            int x = current % graph.Width;
            int y = current / graph.Width;
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                int nx = x + Directions[direction].X;
                int ny = y + Directions[direction].Y;
                if ((uint)nx >= graph.Width || (uint)ny >= graph.Height)
                    continue;
                Edge edge = graph.GetEdge(x, y, direction);
                if (edge.Kind == EdgeKind.Blocked ||
                    (edge.Kind == EdgeKind.Climb && !graph.ClimbingAllowed))
                    continue;
                int movement = (direction & 1) == 0 ? graph.CardinalTicks : graph.DiagonalTicks;
                int edgeCost = AssassinAStarPolicy.SaturatingAdd(movement, edge.AdditionalTicks);
                int nextCost = AssassinAStarPolicy.SaturatingAdd(costs[current], edgeCost);
                int next = ny * graph.Width + nx;
                if (nextCost >= costs[next])
                    continue;
                costs[next] = nextCost;
                parents[next] = current;
                incomingClimb[next] = edge.Kind == EdgeKind.Climb;
                queue.Enqueue(next, Priority.Create(
                    graph, nx, ny, targetX, targetY,
                    nextCost, insertion++, useHeuristic, suffixCosts));
            }
        }
        return new SearchResult(false, int.MaxValue, expanded, 0);
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("FAIL: " + message);
    }

    private readonly record struct SearchResult(bool Found, int Cost, int Expanded, int ClimbEdges);

    private readonly record struct Priority(int EstimatedTotal, int Cost, int Insertion) : IComparable<Priority>
    {
        public static Priority Create(
            TestGraph graph, int x, int y, int tx, int ty,
            int cost, int insertion, bool useHeuristic,
            IReadOnlyDictionary<int, int> suffixCosts)
        {
            int heuristic = useHeuristic
                ? AssassinAStarPolicy.EstimateOctileTicks(
                    x, y, tx, ty, graph.CardinalTicks, graph.DiagonalTicks)
                : 0;
            int node = y * graph.Width + x;
            if (useHeuristic && suffixCosts != null &&
                suffixCosts.TryGetValue(node, out int suffixCost))
            {
                heuristic = Math.Max(heuristic, suffixCost);
            }
            return new Priority(AssassinAStarPolicy.SaturatingAdd(cost, heuristic), cost, insertion);
        }

        public int CompareTo(Priority other)
        {
            if (AssassinAStarPolicy.ComesBefore(
                EstimatedTotal, Cost, Insertion,
                other.EstimatedTotal, other.Cost, other.Insertion)) return -1;
            if (AssassinAStarPolicy.ComesBefore(
                other.EstimatedTotal, other.Cost, other.Insertion,
                EstimatedTotal, Cost, Insertion)) return 1;
            return 0;
        }
    }

    private sealed class TestGraph
    {
        private readonly Edge[,,] edges;

        public TestGraph(int width, int height, int speedDelay)
        {
            Width = width;
            Height = height;
            CardinalTicks = AssassinClimbCostPolicy.GetCardinalMovementTicks(speedDelay);
            DiagonalTicks = AssassinClimbCostPolicy.GetDiagonalMovementTicks(speedDelay);
            edges = new Edge[width, height, Directions.Length];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            for (int direction = 0; direction < Directions.Length; direction++)
                edges[x, y, direction] = Edge.Ground;
        }

        public int Width { get; }
        public int Height { get; }
        public int CardinalTicks { get; }
        public int DiagonalTicks { get; }
        public bool ClimbingAllowed { get; set; } = true;

        public Edge GetEdge(int x, int y, int direction) => edges[x, y, direction];
        public void SetEdge(int x, int y, int direction, Edge edge) => edges[x, y, direction] = edge;

        public void AddVerticalWall(int wallX, int fromY, int toY, bool climbable, int climbTicks)
        {
            for (int y = fromY; y <= toY; y++)
            {
                SetEdge(wallX - 1, y, 2, climbable ? Edge.Climb(climbTicks) : Edge.Blocked);
                SetEdge(wallX, y, 6, climbable ? Edge.Climb(climbTicks) : Edge.Blocked);
                for (int diagonal = 1; diagonal < Directions.Length; diagonal += 2)
                {
                    if ((diagonal == 1 || diagonal == 3) && wallX > 0)
                        SetEdge(wallX - 1, y, diagonal, Edge.Blocked);
                    if ((diagonal == 5 || diagonal == 7) && wallX < Width)
                        SetEdge(wallX, y, diagonal, Edge.Blocked);
                }
            }
        }
    }

    private enum EdgeKind { Ground, Climb, Blocked }
    private readonly record struct Edge(EdgeKind Kind, int AdditionalTicks)
    {
        public static Edge Ground => new(EdgeKind.Ground, 0);
        public static Edge Blocked => new(EdgeKind.Blocked, 0);
        public static Edge Climb(int ticks) => new(EdgeKind.Climb, ticks);
    }
}
