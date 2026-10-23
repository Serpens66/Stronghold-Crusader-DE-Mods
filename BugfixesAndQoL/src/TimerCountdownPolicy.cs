using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal static class TimerCountdownPolicy
    {
        internal const int TicksPerSecond = 40;

        // The countdown is refreshed every frame but changes once per second; reuse the last text.
        private sealed class FormattedSeconds
        {
            internal FormattedSeconds(long seconds, string text) { Seconds = seconds; Text = text; }
            internal readonly long Seconds;
            internal readonly string Text;
        }
        private static FormattedSeconds lastFormatted = new FormattedSeconds(-1, string.Empty);

        internal static string FormatTicks(long remainingTicks)
        {
            long seconds = Math.Max(0, remainingTicks);
            seconds = seconds / TicksPerSecond + (seconds % TicksPerSecond == 0 ? 0 : 1);
            FormattedSeconds cached = lastFormatted;
            if (cached.Seconds == seconds) return cached.Text;
            long minutes = seconds / 60;
            string text = minutes.ToString(CultureInfo.InvariantCulture) + ":" +
                (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
            lastFormatted = new FormattedSeconds(seconds, text);
            return text;
        }

        internal static long? SelectOstRemaining(long? defeat, long? victory, long? peace)
        {
            return defeat ?? victory ?? peace;
        }
    }
}
