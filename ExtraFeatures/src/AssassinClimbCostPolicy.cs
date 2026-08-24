namespace ExtraFeatures
{
    internal static class AssassinClimbCostPolicy
    {
        public const int MinimumClimbTicks = 80;
        public const int LowWallClimbTicks = 240;
        public const int NormalWallClimbTicks = 400;
        public const int NormalWallHeight = 90;

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
