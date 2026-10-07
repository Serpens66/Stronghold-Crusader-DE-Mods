using APIShared.UnitCommands;
namespace MoatMove
{
    // Precise traversal uses the same directed weighted kernel as the independent
    // fill optimizer. Access policy is supplied by the shared traversal-edge service.
    internal sealed class MoatSearchKernel : WeightedGridSearchKernel
    {
        internal MoatSearchKernel(int width, int height, MoatSearchEdge edge)
            : base(width, height, edge) { }
    }
}
