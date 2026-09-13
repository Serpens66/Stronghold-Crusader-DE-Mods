using System;
using System.Collections.Generic;

namespace MoatMove
{
    public static class FastRouteFieldTests
    {
        private static int assertions;
        private static void Check(bool ok, string message)
        { assertions++; if (!ok) throw new Exception("Fast field: " + message); }

        public static void Run()
        {
            const int width = 17, height = 13, count = width * height;
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
            var random = new Random(940613);
            var edges = new bool[count, 8];
            bool Edge(int f, int t, int d, out bool moat, out bool structure)
            { moat = f % 4 == 0; structure = f % 11 == 0; return edges[f, d]; }
            var field = new FastRouteField(width, height, Edge);
            for (int revision = 0; revision < 100; revision++)
            {
                for (int f = 0; f < count; f++) for (int d = 0; d < 8; d++) edges[f, d] = random.Next(4) != 0;
                int target = random.Next(count);
                field.Reset(target);
                for (int start = 0; start < count; start++)
                {
                    // Independent forward BFS for each start; production searches in reverse.
                    var distance = new int[count]; Array.Fill(distance, -1);
                    var queue = new Queue<int>(); queue.Enqueue(start); distance[start] = 0;
                    while (queue.Count > 0 && distance[target] < 0)
                    {
                        int f = queue.Dequeue();
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = f % width + dx[d], ny = f / width + dy[d];
                            if ((uint)nx >= width || (uint)ny >= height || !edges[f, d]) continue;
                            int to = ny * width + nx;
                            if (distance[to] >= 0) continue;
                            distance[to] = distance[f] + 1; queue.Enqueue(to);
                        }
                    }
                    FastRouteStatus before = field.Status(start);
                    Check(field.Advance(start, 0) == before, "zero slice changes no answer");
                    FastRouteStatus result;
                    do
                    {
                        int expanded = field.Expanded;
                        result = field.Advance(start, 1 + start % 7);
                        Check(field.Expanded - expanded <= 1 + start % 7, "slice bound");
                    } while (result == FastRouteStatus.Pending);
                    Check((result == FastRouteStatus.Found) == (distance[target] >= 0), "directed reachability");
                    Check(field.GetPath(start, out var path) == result, "stable result");
                    if (path != null)
                    {
                        Check(path.Length == distance[target] + 1 && path[0] == start && path[path.Length - 1] == target, "unweighted optimum and exact endpoints");
                        for (int i = 1; i < path.Length; i++)
                        {
                            int direction = -1;
                            for (int d = 0; d < 8; d++) if (path[i] % width - path[i - 1] % width == dx[d] && path[i] / width - path[i - 1] / width == dy[d]) direction = d;
                            Check(direction >= 0 && edges[path[i - 1], direction], "original directed edge, not its reverse");
                        }
                    }
                }
                Check(field.Expanded <= count, "whole group shares at most one expansion per tile");
                field.Cancel();
                Check(field.GetPath(target, out _) == FastRouteStatus.Cancelled, "cancel revokes even a ready path");
            }
            bool Open(int f, int t, int d, out bool m, out bool s) { m = s = false; return true; }
            var longField = new FastRouteField(2502, 1, Open); longField.Reset(2501);
            Check(longField.Advance(0, 10) == FastRouteStatus.Pending, "budget exhaustion is not NoRoute");
            Check(longField.Advance(0, 3000) == FastRouteStatus.TooLong, "native buffer boundary is distinct from NoRoute");
            Check(longField.GetPath(0, out var longPath, 3000) == FastRouteStatus.Found && longPath.Length == 2502, "field retains long route evidence");
            longField.Reset(0);
            Check(longField.GetPath(0, out var zero) == FastRouteStatus.Found && zero.Length == 1, "reset and zero-length route");
            Console.WriteLine($"PASS: {assertions} new Fast field assertions; directed BFS oracle, shared groups, deterministic slices, cancellation and route limits.");
        }
    }
}
