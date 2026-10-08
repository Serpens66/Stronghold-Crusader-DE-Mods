using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Shared
{
    /// <summary>
    /// Extracts the editable number from localized value labels such as "50 %",
    /// "3 months" or "1.5x". The setting setter remains responsible for clamping.
    /// </summary>
    public static class NumericTextInput
    {
        private static readonly Regex IntegerPattern =
            new Regex(@"[-+]?\d+", RegexOptions.CultureInvariant);

        private static readonly Regex DecimalPattern =
            new Regex(@"[-+]?(?:\d+(?:[\.,]\d+)?|[\.,]\d+)", RegexOptions.CultureInvariant);

        public static bool TryParseInt(string text, out int value)
        {
            Match match = IntegerPattern.Match(text ?? string.Empty);
            if (!match.Success)
            {
                value = 0;
                return false;
            }

            return int.TryParse(
                match.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        public static bool TryParseDouble(string text, out double value)
        {
            Match match = DecimalPattern.Match(text ?? string.Empty);
            if (!match.Success)
            {
                value = 0.0;
                return false;
            }

            string normalized = match.Value.Replace(',', '.');
            return double.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
