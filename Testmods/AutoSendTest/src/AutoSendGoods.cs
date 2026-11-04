using SHCDESE.Interop;

namespace AutoSendTest
{
    internal static class AutoSendGoods
    {
        // Deliberately matches the selectable goods of Vanilla's send dialog.
        internal static readonly eGoods[] Values = { eGoods.STORED_WOOD_PLANKS, eGoods.STORED_RAW_HOPS,
            eGoods.STORED_STONE_BLOCKS, eGoods.STORED_IRON_INGOTS, eGoods.STORED_PITCH_RAW,
            eGoods.STORED_RAW_WHEAT, eGoods.STORED_FOOD_BREAD, eGoods.STORED_FOOD_CHEESE,
            eGoods.STORED_FOOD_MEAT, eGoods.STORED_FOOD_FRUIT, eGoods.STORED_FOOD_ALE,
            eGoods.STORED_GOLD, eGoods.STORED_FLOUR, eGoods.STORED_BOWS, eGoods.STORED_CROSSBOWS,
            eGoods.STORED_SPEARS, eGoods.STORED_PIKES, eGoods.STORED_MACES, eGoods.STORED_SWORDS,
            eGoods.STORED_LEATHER_ARMOUR, eGoods.STORED_METAL_ARMOUR };
        internal static int[] Ids()
        { var result = new int[Values.Length]; for (int i = 0; i < result.Length; i++) result[i] = (int)Values[i]; return result; }
        internal static int[] Limits()
        { var result = new int[Values.Length]; for (int i = 0; i < result.Length; i++) result[i] = Values[i] == eGoods.STORED_GOLD ? 1000 : 100; return result; }
        internal static bool Contains(int good)
        { for (int i = 0; i < Values.Length; i++) if ((int)Values[i] == good) return true; return false; }
        internal static AutoSendState NewState() => new AutoSendState(Ids(), Limits());
    }
}
