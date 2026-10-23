using System.Collections.Generic;

namespace CrenelatedWallTest
{
    internal static class WallAdmission
    {
        // Shared by presentation and execution. Only execution supplies the queued
        // command maximum; presentation uses the maximum Vanilla would queue now.
        internal static WallPlan Evaluate(IWallWorld world, int owner, List<WallCell> raster,
            int availability, IReadOnlyDictionary<int, WallCell> preferred)
        {
            var predicted = WallPolicy.Predict(raster, owner, availability);
            return predicted.Count == 0 ? new WallPlan() : WallPolicy.Plan(world, owner, predicted, true, preferred);
        }
    }
}
