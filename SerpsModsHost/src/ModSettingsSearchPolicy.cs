using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SerpsModsHost
{
    internal static class ModSettingsSearchPolicy
    {
        public static bool ShouldIncludeCandidate(
            bool isSection,
            string key,
            string sectionKey,
            ISet<string> matchedSectionKeys)
        {
            if (matchedSectionKeys == null)
                return true;
            return isSection
                ? matchedSectionKeys.Contains(key ?? string.Empty)
                : !matchedSectionKeys.Contains(sectionKey ?? string.Empty);
        }

        public static int Rank(string title, string toolTip, string query, bool includeToolTips)
        {
            return Rank(title, toolTip, string.Empty, query, includeToolTips);
        }

        public static int Rank(
            string title,
            string toolTip,
            string sectionTitle,
            string query,
            bool includeToolTips)
        {
            title = title ?? string.Empty;
            toolTip = toolTip ?? string.Empty;
            sectionTitle = sectionTitle ?? string.Empty;
            query = (query ?? string.Empty).Trim();
            if (query.Length == 0)
                return int.MaxValue;

            string[] terms = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            CompareInfo compare = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            bool allTermsMatch = terms.All(term =>
                compare.IndexOf(title, term, options) >= 0 ||
                compare.IndexOf(sectionTitle, term, options) >= 0 ||
                (includeToolTips && compare.IndexOf(toolTip, term, options) >= 0));
            if (!allTermsMatch)
                return int.MaxValue;
            if (compare.Compare(title, query, options) == 0)
                return 0;
            if (compare.IsPrefix(title, query, options))
                return 1;
            if (compare.IndexOf(title, query, options) >= 0)
                return 2;
            if (compare.IndexOf(sectionTitle, query, options) >= 0)
                return 3;
            return includeToolTips && compare.IndexOf(toolTip, query, options) >= 0 ? 4 : 3;
        }
    }
}
