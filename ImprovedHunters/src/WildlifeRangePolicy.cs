using System;

namespace ImprovedHunters
{
    /// <summary>Which waypoints of a herd's route stay in its area: a square around its start place.</summary>
    internal static class WildlifeRangePolicy
    {
        public const int DefaultSize = 100;
        public const int MinimumSize = 40;
        public const int MaximumSize = 400;

        public static bool IsInside(int x, int y, int centerX, int centerY, int size)
        {
            int reach = size / 2;
            return Math.Abs(x - centerX) <= reach && Math.Abs(y - centerY) <= reach;
        }

        /// <summary>How many waypoints stay when the route ends before its first one outside the area.</summary>
        public static int CountBeforeLeaving(ReadOnlySpan<short> waypoints, int count, int centerX, int centerY, int size)
        {
            for (int index = 0; index < count; index++)
            {
                if (!IsInside(waypoints[index * 2], waypoints[index * 2 + 1], centerX, centerY, size))
                    return index;
            }

            return count;
        }
    }
}
