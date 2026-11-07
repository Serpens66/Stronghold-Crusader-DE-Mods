using System;
using System.Collections.Generic;

namespace ImprovedHunters
{
    /// <summary>Values of the herd settings. A setting that can be off keeps that in its one value, as 0.</summary>
    internal static class HerdSettingsPolicy
    {
        public const int Off = 0;
        public const int MinimumReturnSeconds = 30;
        public const int MaximumReturnSeconds = 1800;
        public const int MinimumRangeSize = 40;
        public const int MaximumRangeSize = 400;
        public const int StartHerdsOff = 0;
        public const int StartHerdsOnEmptyMaps = 1;
        public const int StartHerdsAlways = 2;

        public static int ClampReturnSeconds(int value) => Clamp(value, MinimumReturnSeconds, MaximumReturnSeconds);

        public static int ClampRangeSize(int value) => Clamp(value, MinimumRangeSize, MaximumRangeSize);

        public static int ClampStartHerdMode(int value) => Math.Max(StartHerdsOff, Math.Min(StartHerdsAlways, value));

        public static bool WantsStartHerds(int mode, bool mapHasHerds) =>
            mode == StartHerdsAlways || (mode == StartHerdsOnEmptyMaps && !mapHasHerds);

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value <= Off)
                return Off;
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }

    /// <summary>
    /// Where a player's start herd may stand: rings around the keep door, each starting towards the middle of the
    /// map so that no player gets the map's edge first. Whole numbers only, so all peers try the same tiles.
    /// </summary>
    internal static class WildlifeStartPlaces
    {
        public const int NearestDistance = 30;
        private const int FarthestDistance = 70;
        private const int DistanceStep = 8;
        private const int DirectionScale = 256;

        // Sixteen directions around a circle, scaled by DirectionScale.
        private static readonly (int X, int Y)[] Directions =
        {
            (256, 0), (237, 98), (181, 181), (98, 237), (0, 256), (-98, 237), (-181, 181), (-237, 98),
            (-256, 0), (-237, -98), (-181, -181), (-98, -237), (0, -256), (98, -237), (181, -181), (237, -98)
        };

        public static IEnumerable<(int X, int Y)> Around(int doorX, int doorY, int centerX, int centerY)
        {
            int first = 0;
            long best = long.MinValue;
            for (int index = 0; index < Directions.Length; index++)
            {
                long toCenter = (long)Directions[index].X * (centerX - doorX) + (long)Directions[index].Y * (centerY - doorY);
                if (toCenter > best)
                {
                    best = toCenter;
                    first = index;
                }
            }

            for (int distance = NearestDistance; distance <= FarthestDistance; distance += DistanceStep)
            {
                // Away from the first direction to both sides in turn.
                for (int step = 0; step < Directions.Length; step++)
                {
                    int offset = (step + 1) / 2 * (step % 2 == 0 ? -1 : 1);
                    (int x, int y) = Directions[(first + offset + Directions.Length) % Directions.Length];
                    yield return (doorX + x * distance / DirectionScale, doorY + y * distance / DirectionScale);
                }
            }
        }
    }
}
