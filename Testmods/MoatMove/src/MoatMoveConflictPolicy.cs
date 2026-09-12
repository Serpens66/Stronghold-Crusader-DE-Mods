using System;
using System.Collections.Generic;

namespace MoatMove
{
    internal static class MoatMoveConflictPolicy
    {
        internal static string FindConflict(IEnumerable<string> loadedGuids)
        {
            foreach (string guid in loadedGuids)
            {
                if (string.Equals(guid, "BugfixesAndQoL_Serp", StringComparison.Ordinal) ||
                    string.Equals(guid, "EnemyGatePathfindingTest_Serp", StringComparison.Ordinal))
                    return guid;
            }
            return null;
        }
    }
}
