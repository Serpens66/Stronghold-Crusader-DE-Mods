namespace UnitCosts
{
    public sealed class UnitCostValues
    {
        public UnitCostValues(int bows, int crossbows, int spears, int pikes, int maces, int swords, int leatherArmour, int metalArmour, int gold)
        {
            Bows = ClampCost(bows);
            Crossbows = ClampCost(crossbows);
            Spears = ClampCost(spears);
            Pikes = ClampCost(pikes);
            Maces = ClampCost(maces);
            Swords = ClampCost(swords);
            LeatherArmour = ClampCost(leatherArmour);
            MetalArmour = ClampCost(metalArmour);
            Gold = ClampCost(gold);
        }

        public int Bows { get; }
        public int Crossbows { get; }
        public int Spears { get; }
        public int Pikes { get; }
        public int Maces { get; }
        public int Swords { get; }
        public int LeatherArmour { get; }
        public int MetalArmour { get; }
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
