using System;
using System.Collections.Generic;

namespace CrenelatedWallTest
{
    internal sealed class WallVerdict
    {
        internal string Rejection;
        internal int Added;
        internal bool Accepted => Rejection == null;
    }

    internal static class WallAdmission
    {
        // Shared by presentation and execution: the same raster, prediction and rules.
        // heightsManaged=false: height adjustment is off, so heights are predicted as pure Vanilla.
        internal static WallVerdict Evaluate(IWallWorld world, int owner, List<WallCell> raster, Func<int, bool> managed, bool heightsManaged = true)
        {
            List<WallCell> predicted = WallPolicy.Predict(raster, owner);
            return new WallVerdict { Added = predicted.Count, Rejection = WallPolicy.Violation(world, predicted, managed, heightsManaged) };
        }
    }
}
