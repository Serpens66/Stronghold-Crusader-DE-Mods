namespace BugfixesAndQoL
{
    internal static class AssassinClimbCostPolicy
    {
        public const int MovementSubstepsPerTile = 8;
        public const int MinimumClimbTicks = 80;
        public const int LowWallClimbTicks = 240;
        public const int NormalWallClimbTicks = 400;
        public const int NormalWallHeight = 90;

        public static int GetCardinalMovementTicks(int speedDelay)
        {
            int normalizedDelay = speedDelay < 0 ? 0 : speedDelay;
            long ticks = (long)MovementSubstepsPerTile * (normalizedDelay + 1L);
            return ticks > int.MaxValue ? int.MaxValue : (int)ticks;
        }

        public static int GetDiagonalMovementTicks(int speedDelay)
        {
            int cardinalTicks = GetCardinalMovementTicks(speedDelay);
            long scaled = (long)cardinalTicks * 181L + 64L;
            int diagonalTicks = scaled / 128L > int.MaxValue
                ? int.MaxValue
                : (int)(scaled / 128L);
            if (cardinalTicks == int.MaxValue)
                return int.MaxValue;
            return diagonalTicks <= cardinalTicks ? cardinalTicks + 1 : diagonalTicks;
        }

        public static int GetAdditionalTicks(
            bool isClimbEdge,
            int heightDifference,
            bool targetIsLowWall,
            bool targetIsNormalWall,
            bool targetIsStairs)
        {
            if (!isClimbEdge || heightDifference == 0)
                return 0;
            if (heightDifference < 0)
                return MinimumClimbTicks;
            if (targetIsLowWall)
                return LowWallClimbTicks;
            if (targetIsNormalWall && !targetIsStairs)
                return NormalWallClimbTicks;

            int scaled = (heightDifference * NormalWallClimbTicks + NormalWallHeight / 2) / NormalWallHeight;
            return scaled < MinimumClimbTicks ? MinimumClimbTicks : scaled;
        }
    }
}
