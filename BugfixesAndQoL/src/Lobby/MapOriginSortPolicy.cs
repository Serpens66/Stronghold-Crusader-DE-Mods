// Feature: Deterministic origin ordering for Vanilla map-selection rows.
using System;

namespace BugfixesAndQoL
{
    internal readonly struct MapOriginSortKey
    {
        internal MapOriginSortKey(
            bool builtIn,
            bool user,
            bool workshop,
            string displayName)
        {
            BuiltIn = builtIn;
            User = user;
            Workshop = workshop;
            DisplayName = displayName ?? string.Empty;
        }

        internal bool BuiltIn { get; }
        internal bool User { get; }
        internal bool Workshop { get; }
        internal string DisplayName { get; }
    }

    internal static class MapOriginSortPolicy
    {
        private const int BuiltInRank = 0;
        private const int UserRank = 1;
        private const int WorkshopRank = 2;
        private const int UnknownRank = 3;

        internal static int Compare(
            MapOriginSortKey left,
            MapOriginSortKey right,
            bool originAscending)
        {
            int leftRank = GetOriginRank(left);
            int rightRank = GetOriginRank(right);

            // Unknown or malformed rows remain at the end in either direction.
            if (leftRank == UnknownRank || rightRank == UnknownRank)
            {
                if (leftRank != rightRank)
                    return leftRank == UnknownRank ? 1 : -1;
            }
            else
            {
                int originComparison = leftRank.CompareTo(rightRank);
                if (originComparison != 0)
                    return originAscending ? originComparison : -originComparison;
            }

            int nameComparison = StringComparer.OrdinalIgnoreCase.Compare(
                left.DisplayName,
                right.DisplayName);
            if (nameComparison != 0)
                return nameComparison;

            return StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
        }

        internal static int GetOriginRank(MapOriginSortKey key)
        {
            // Match Vanilla's displayed icon priority when flags are contradictory.
            if (key.BuiltIn)
                return BuiltInRank;
            if (key.Workshop)
                return WorkshopRank;
            if (key.User)
                return UserRank;
            return UnknownRank;
        }
    }
}
