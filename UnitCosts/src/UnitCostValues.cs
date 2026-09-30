namespace UnitCosts
{
    public sealed class UnitCostValues
    {
        public UnitCostValues(int gold, bool noWeapons = false)
        {
            Gold = ClampCost(gold);
            NoWeapons = noWeapons;
        }

        public int Gold { get; }
        public bool NoWeapons { get; }

        public static int ClampCost(int value)
        {
            if (value < -1)
                return -1;
            if (value > 10000)
                return 10000;
            return value;
        }
    }
}
