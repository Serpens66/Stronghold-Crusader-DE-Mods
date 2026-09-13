using System;

namespace MoatMove
{
    internal enum FastRouteStatus
    {
        Pending,
        Found,
        NoRoute,
        TooLong,
        Cancelled,
        InvalidQuery
    }

    // One reverse, unweighted directed BFS serves every start for this destination.
    // The caller owns an immutable traversal revision until Reset/Cancel. No native
    // state, unit pointers, timers or publication side effects belong in this class.
    internal sealed class FastRouteField
    {
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private readonly int width, height;
        private readonly MoatSearchEdge edge;
        private readonly int[] distances, queue;
        private readonly byte[] nextDirections;
        private int head, tail, destination = -1;
        private bool cancelled;

        internal FastRouteField(int width, int height, MoatSearchEdge edge)
        {
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width));
            this.width = width; this.height = height;
            this.edge = edge ?? throw new ArgumentNullException(nameof(edge));
            distances = new int[width * height];
            queue = new int[distances.Length];
            nextDirections = new byte[distances.Length];
        }

        internal int Expanded => head;
        internal int Discovered => tail;
        internal bool Exhausted => destination >= 0 && head == tail && !cancelled;

        internal void Reset(int target)
        {
            if ((uint)target >= distances.Length) throw new ArgumentOutOfRangeException(nameof(target));
            // Clear only cells touched by the preceding command, including its frontier.
            for (int i = 0; i < tail; i++) distances[queue[i]] = 0;
            head = tail = 0; cancelled = false; destination = target;
            distances[target] = 1;
            queue[tail++] = target;
        }

        internal void Cancel() { cancelled = true; }

        internal FastRouteStatus Status(int start, int maximumEdges = 2000)
        {
            if ((uint)start >= distances.Length || maximumEdges < 0 || destination < 0)
                return FastRouteStatus.InvalidQuery;
            if (cancelled) return FastRouteStatus.Cancelled;
            if (distances[start] != 0)
                return distances[start] - 1 > maximumEdges ? FastRouteStatus.TooLong : FastRouteStatus.Found;
            return head == tail ? FastRouteStatus.NoRoute : FastRouteStatus.Pending;
        }

        // Budget limits this slice, not the lifetime of a command. A zero slice never
        // claims that a still-unexplored start is unreachable. Ordering is tick-neutral.
        internal FastRouteStatus Advance(int start, int maximumExpanded, int maximumEdges = 2000)
        {
            if (maximumExpanded < 0) throw new ArgumentOutOfRangeException(nameof(maximumExpanded));
            FastRouteStatus status = Status(start, maximumEdges);
            if (status != FastRouteStatus.Pending) return status;
            int expanded = 0;
            while (head < tail && expanded < maximumExpanded)
            {
                // Finish all neighbors before consuming this queue entry. A throwing
                // predicate can be retried without losing the unexplored directions.
                int node = queue[head], x = node % width, y = node / width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + Dx[d], ny = y + Dy[d];
                    if ((uint)nx >= width || (uint)ny >= height) continue;
                    int predecessor = ny * width + nx;
                    if (distances[predecessor] != 0) continue;
                    int originalDirection = (d + 4) & 7;
                    if (!edge(predecessor, node, originalDirection, out _, out _)) continue;
                    distances[predecessor] = distances[node] + 1;
                    nextDirections[predecessor] = (byte)originalDirection;
                    queue[tail++] = predecessor;
                }
                head++; expanded++;
                status = Status(start, maximumEdges);
                if (status != FastRouteStatus.Pending) return status;
            }
            return Status(start, maximumEdges);
        }

        internal FastRouteStatus GetPath(int start, out int[] path, int maximumEdges = 2000)
        {
            path = null;
            FastRouteStatus status = Status(start, maximumEdges);
            if (status != FastRouteStatus.Found) return status;
            int length = distances[start];
            var result = new int[length];
            int node = start;
            for (int i = 0; i < length; i++)
            {
                result[i] = node;
                if (i + 1 < length)
                {
                    int d = nextDirections[node];
                    node += Dy[d] * width + Dx[d];
                }
            }
            if (node != destination) throw new InvalidOperationException("Fast field parent chain changed.");
            path = result;
            return FastRouteStatus.Found;
        }
    }
}
