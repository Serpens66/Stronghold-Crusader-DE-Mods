namespace AIVPlacement.Core
{
    // Audited native terrain contract, shared by offline evaluation and live supplemental spawning.
    internal static class AivTerrainPlacementRules
    {
        internal const int NativeMaximumHeight = 200;

        private const int Sea = 0x00000001;
        private const int IsFarm = 0x00000004;
        private const int PitchTrap = 0x00000008;
        private const int MapEdges = 0x00000030;
        private const int ImpassableEdge = 0x00000080;
        private const int IsWall = 0x00000100;
        private const int IsBuildingOrElevated = 0x10000400;
        private const int River = 0x00100000;
        private const int Ford = 0x00200000;
        private const int FarmTypeFlags = 0x0F000000;
        private const int IsSwamp = 0x20000000;
        private const int IsMoat = 0x40000000;

        internal static bool IsTerrainBlocked(int mapperValue, int flags)
        {
            bool waterMapper = mapperValue >= 195 && mapperValue <= 198;
            if ((flags & Sea) != 0 && !waterMapper)
                return true;
            if ((flags & IsFarm) != 0)
                return true;
            if ((flags & PitchTrap) != 0 && mapperValue == 99)
                return true;
            if ((flags & MapEdges) != 0)
                return true;
            if ((flags & River) != 0 && !waterMapper)
                return true;
            if ((flags & IsBuildingOrElevated) != 0)
                return true;
            if ((flags & FarmTypeFlags) != 0)
                return true;
            if ((flags & Ford) != 0)
                return true;

            bool bareImpassableEdge =
                (flags & (ImpassableEdge | IsWall)) == ImpassableEdge;
            // Skirmish/player-state initialization clears the general mapper
            // profile exception before the AIV validator checks this bit.
            if (bareImpassableEdge)
                return true;
            if ((flags & IsSwamp) != 0 && mapperValue != 91)
                return true;
            if ((flags & IsMoat) != 0 && mapperValue != 105)
                return true;

            return false;
        }

    }
}
