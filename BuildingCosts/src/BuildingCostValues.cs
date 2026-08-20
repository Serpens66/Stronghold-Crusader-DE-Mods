namespace BuildingCosts
{
    public sealed class BuildingCostValues
    {
        public BuildingCostValues(int wood, int stone, int iron, int pitch, int gold)
        {
            Wood = ClampStandardCost(wood);
            Stone = ClampStandardCost(stone);
            Iron = ClampStandardCost(iron);
            Pitch = ClampStandardCost(pitch);
            Gold = ClampGoldCost(gold);
        }

        public int Wood { get; }
        public int Stone { get; }
        public int Iron { get; }
        public int Pitch { get; }
        public int Gold { get; }

        public static int ClampStandardCost(int value)
        {
            if (value < -1)
                return -1;
            if (value > 100)
                return 100;
            return value;
        }

        public static int ClampGoldCost(int value)
        {
            if (value < -1)
                return -1;
            if (value > 10000)
                return 10000;
            return value;
        }
    }
}
