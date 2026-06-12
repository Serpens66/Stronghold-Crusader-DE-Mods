namespace BuildingCosts
{
    public sealed class BuildingCostValues
    {
        public BuildingCostValues(int wood, int stone, int iron, int pitch, int gold)
        {
            Wood = ClampCost(wood);
            Stone = ClampCost(stone);
            Iron = ClampCost(iron);
            Pitch = ClampCost(pitch);
            Gold = ClampCost(gold);
        }

        public int Wood { get; }
        public int Stone { get; }
        public int Iron { get; }
        public int Pitch { get; }
        public int Gold { get; }

        public static int ClampCost(int value)
        {
            if (value < -1)
                return -1;
            if (value > 1000)
                return 1000;
            return value;
        }
    }
}
