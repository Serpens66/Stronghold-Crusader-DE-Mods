using System.Collections.Generic;

namespace MoatMove
{
    internal interface IFastRouteField
    {
        int Expanded { get; }
        long Work { get; }
        long BufferBytes { get; }
        bool Exhausted { get; }
        bool HasDiscovered(int node);
        int Distance(int node);
        void Reset(int target);
        void ResetRoots(IEnumerable<int> roots);
        void Cancel();
        FastRouteStatus Status(int start, int maximumEdges = 2000);
        FastRouteStatus Advance(int start, int maximumExpanded, int maximumEdges = 2000);
        FastRouteStatus WritePacked(int start, byte[] buffer, out int count, int maximumEdges = 2000);
        FastRouteStatus GetPath(int start, out int[] path, int maximumEdges = 2000);
    }
}
