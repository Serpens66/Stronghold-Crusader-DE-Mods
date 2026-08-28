using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SerpsModsHost
{
    internal static class ModSettingsSearchPolicy
    {
        public static int Rank(string title, string toolTip, string query, bool includeToolTips)
        {
            title = title ?? string.Empty;
            toolTip = toolTip ?? string.Empty;
            query = (query ?? string.Empty).Trim();
            if (query.Length == 0)
                return int.MaxValue;

            string[] terms = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            CompareInfo compare = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            bool allTermsMatch = terms.All(term =>
                compare.IndexOf(title, term, options) >= 0 ||
                (includeToolTips && compare.IndexOf(toolTip, term, options) >= 0));
            if (!allTermsMatch)
                return int.MaxValue;
            if (compare.Compare(title, query, options) == 0)
                return 0;
            if (compare.IsPrefix(title, query, options))
                return 1;
            if (compare.IndexOf(title, query, options) >= 0)
                return 2;
            return 3;
        }
    }
}
