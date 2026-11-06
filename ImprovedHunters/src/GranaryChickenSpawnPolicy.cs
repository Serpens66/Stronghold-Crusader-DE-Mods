using System;

namespace ImprovedHunters
{
    internal static class GranaryChickenSpawnPolicy
    {
        public const int DefaultTargetPerPlayer = 10;
        public const int MinimumTargetPerPlayer = 0;
        public const int MaximumTargetPerPlayer = 100;

        public static int ClampTarget(int value) =>
            Math.Max(MinimumTargetPerPlayer, Math.Min(MaximumTargetPerPlayer, value));
    }
}
