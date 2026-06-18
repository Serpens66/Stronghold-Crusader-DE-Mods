namespace UnitCosts
{
    public sealed class UnitCostValues
    {
        public UnitCostValues(int gold)
        {
            Gold = ClampCost(gold);
        }

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
