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
    /// Where a player's start herd may stand: on squares around the keep, just outside the 100 x 100 tiles an AI
    /// castle layout covers, so that no finished castle stands in the way. The tiles nearest the middle of the map
    /// come first on each square. Whole numbers only, so all peers try the same tiles.
    /// </summary>
    internal static class WildlifeStartPlaces
    {
        // Half of the castle layout's 100 tiles and a margin for the herd itself.
        public const int NearestDistance = 55;
        private const int FarthestDistance = 70;
        private const int DistanceStep = 5;
        private const int TileStep = 5;

        public static IEnumerable<(int X, int Y)> Around(int keepX, int keepY, int middleX, int middleY)
        {
            for (int distance = NearestDistance; distance <= FarthestDistance; distance += DistanceStep)
            {
                var square = new List<(int X, int Y, long ToMiddle)>();
                for (int along = -distance; along < distance; along += TileStep)
                {
                    Add(square, keepX + along, keepY - distance, middleX, middleY);
                    Add(square, keepX + distance, keepY + along, middleX, middleY);
                    Add(square, keepX - along, keepY + distance, middleX, middleY);
                    Add(square, keepX - distance, keepY - along, middleX, middleY);
                }

                square.Sort((left, right) =>
                {
                    int byMiddle = left.ToMiddle.CompareTo(right.ToMiddle);
                    if (byMiddle != 0)
                        return byMiddle;
                    return left.X != right.X ? left.X.CompareTo(right.X) : left.Y.CompareTo(right.Y);
                });
                foreach ((int x, int y, long _) in square)
                    yield return (x, y);
            }
        }

        private static void Add(List<(int X, int Y, long ToMiddle)> square, int x, int y, int middleX, int middleY)
        {
            long deltaX = x - middleX, deltaY = y - middleY;
            square.Add((x, y, deltaX * deltaX + deltaY * deltaY));
        }
    }
}
