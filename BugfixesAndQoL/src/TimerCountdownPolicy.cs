using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal static class TimerCountdownPolicy
    {
        internal const int TicksPerSecond = 40;

        internal static string FormatTicks(long remainingTicks)
        {
            long seconds = Math.Max(0, remainingTicks);
            seconds = seconds / TicksPerSecond + (seconds % TicksPerSecond == 0 ? 0 : 1);
            long minutes = seconds / 60;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" +
                (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        internal static long? SelectOstRemaining(long? defeat, long? victory, long? peace)
        {
            return defeat ?? victory ?? peace;
        }
    }
}
