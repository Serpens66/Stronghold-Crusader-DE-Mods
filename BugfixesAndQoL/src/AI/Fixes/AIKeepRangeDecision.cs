using System;
using System.Runtime.InteropServices;
using SHCDESE.API;

namespace BugfixesAndQoL
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate long AIKeepDistanceCheck(IntPtr manager, int playerId, uint x, uint y, int range);

    internal static class AIKeepRangeDecision
    {
        internal static bool Bypass(bool enabled, int playerId, Func<int, bool> isAI)
        {
            return enabled && playerId > 0 && playerId <= GamePlayerManagerAPI.MAX_PLAYERS && isAI(playerId);
        }

        internal static long Execute(bool bypass, AIKeepDistanceCheck original, IntPtr manager,
            int playerId, uint x, uint y, int range)
        {
            // Zero is the distance predicate's success result. All other validation stays in callers.
            return bypass ? 0L : original(manager, playerId, x, y, range);
        }
    }
}
