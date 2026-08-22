using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal static class AivCandidateSelectionPolicy
    {
        public static int AppendDistinct<T>(
            IList<T> selected,
            IList<T> available,
            IEnumerable<int> visibleIndexes,
            Func<T, ulong> checksum,
            int maximum)
            where T : class
        {
            if (selected == null || available == null || visibleIndexes == null || checksum == null)
                return 0;
            var known = new HashSet<ulong>();
            foreach (T existing in selected)
            {
                if (existing != null)
                    known.Add(checksum(existing));
            }
            var orderedIndexes = new List<int>(visibleIndexes);
            orderedIndexes.Sort();
            int added = 0;
            foreach (int index in orderedIndexes)
            {
                if (selected.Count >= maximum)
                    break;
                if (index < 0 || index >= available.Count)
                    continue;
                T candidate = available[index];
                if (candidate != null && known.Add(checksum(candidate)))
                {
                    selected.Add(candidate);
                    added++;
                }
            }
            return added;
        }

        public static int TrimToMaximum<T>(List<T> values, int maximum)
        {
            if (values == null || values.Count <= maximum)
                return 0;
            int safeMaximum = Math.Max(0, maximum);
            int removed = values.Count - safeMaximum;
            values.RemoveRange(safeMaximum, removed);
            return removed;
        }
    }
}
