// Feature: Deterministic ordering, labels, and filtering for the AIC quick selector.
using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal enum AicDropdownOrigin
    {
        Default,
        Local,
        Workshop
    }

    internal static class AicDropdownPolicy
    {
        public static AicDropdownOrigin GetOrigin(bool isDefault, bool workshop)
        {
            if (isDefault)
                return AicDropdownOrigin.Default;
            return workshop ? AicDropdownOrigin.Workshop : AicDropdownOrigin.Local;
        }

        public static string FormatDisplayName(
            string name,
            int power,
            bool isDefault,
            string defaultText)
        {
            if (isDefault)
                return defaultText ?? string.Empty;
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} ({1})",
                name ?? string.Empty,
                power);
        }

        public static bool Matches(
            string name,
            int power,
            bool isDefault,
            string defaultText,
            string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            string query = searchText.Trim();
            string candidateName = isDefault ? defaultText : name;
            return Contains(candidateName, query) ||
                (!isDefault && Contains(
                    power.ToString(CultureInfo.CurrentCulture),
                    query));
        }

        public static int Compare(
            bool leftDefault,
            string leftName,
            bool leftWorkshop,
            int leftPower,
            bool rightDefault,
            string rightName,
            bool rightWorkshop,
            int rightPower)
        {
            if (leftDefault != rightDefault)
                return leftDefault ? -1 : 1;

            int comparison = StringComparer.CurrentCultureIgnoreCase.Compare(
                leftName ?? string.Empty,
                rightName ?? string.Empty);
            if (comparison != 0)
                return comparison;

            comparison = leftWorkshop.CompareTo(rightWorkshop);
            if (comparison != 0)
                return comparison;
            return leftPower.CompareTo(rightPower);
        }

        private static bool Contains(string value, string query) =>
            !string.IsNullOrEmpty(value) &&
            value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}
