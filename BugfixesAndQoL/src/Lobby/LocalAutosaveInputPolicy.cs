using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal static class LocalAutosaveInputPolicy
    {
        internal const int MaximumMinutes = 1440;

        internal static bool TryParse(string text, string offText, out int minutes)
        {
            text = (text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(offText) && string.Equals(text, offText.Trim(), StringComparison.CurrentCultureIgnoreCase))
            { minutes = 0; return true; }
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out minutes) &&
                minutes >= 0 && minutes <= MaximumMinutes;
        }

        internal static string Format(int minutes, string offText) =>
            minutes == 0 ? offText : minutes.ToString(CultureInfo.InvariantCulture);

        internal static int Step(string draft, string offText, int committedMinutes, int direction)
        {
            int value = TryParse(draft, offText, out int parsed) ? parsed : committedMinutes;
            if (direction > 0) return Math.Min(MaximumMinutes, value + 1);
            if (direction < 0) return Math.Max(0, value - 1);
            return value;
        }
    }
}
