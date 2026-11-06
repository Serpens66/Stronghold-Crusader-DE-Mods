namespace ImprovedHunters
{
    /// <summary>
    /// The optional limit on how far a Hunter searches. Vanilla has none; it only prefers prey beyond
    /// 20 tiles and looks closer afterwards, so a limit below that would hide its first choice.
    /// </summary>
    internal static class HunterSearchRangePolicy
    {
        public const int Unlimited = 0;
        public const int MinimumRange = 21;
        public const int MaximumRange = 200;

        public static int Clamp(int value)
        {
            if (value <= Unlimited)
                return Unlimited;
            if (value < MinimumRange)
                return MinimumRange;
            return value > MaximumRange ? MaximumRange : value;
        }

        public static bool IsWithin(int distance, int range) => range == Unlimited || distance <= range;
    }
}
