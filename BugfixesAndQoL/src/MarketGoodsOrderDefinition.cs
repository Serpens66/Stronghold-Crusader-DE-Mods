// Feature: Defines and validates the configurable circular market-goods order.
using SHCDESE.Interop;
using System;

namespace BugfixesAndQoL
{
    internal static class MarketGoodsOrderDefinition
    {
        private static readonly int[] HdOrder =
        {
            (int)eGoods.STORED_FOOD_MEAT,
            (int)eGoods.STORED_FOOD_CHEESE,
            (int)eGoods.STORED_FOOD_FRUIT,
            (int)eGoods.STORED_FOOD_BREAD,
            (int)eGoods.STORED_RAW_WHEAT,
            (int)eGoods.STORED_FLOUR,
            (int)eGoods.STORED_WOOD_PLANKS,
            (int)eGoods.STORED_STONE_BLOCKS,
            (int)eGoods.STORED_IRON_INGOTS,
            (int)eGoods.STORED_PITCH_REFINED,
            (int)eGoods.STORED_SPEARS,
            (int)eGoods.STORED_BOWS,
            (int)eGoods.STORED_MACES,
            (int)eGoods.STORED_CROSSBOWS,
            (int)eGoods.STORED_PIKES,
            (int)eGoods.STORED_SWORDS,
            (int)eGoods.STORED_LEATHER_ARMOUR,
            (int)eGoods.STORED_METAL_ARMOUR,
            (int)eGoods.STORED_FOOD_ALE,
            (int)eGoods.STORED_RAW_HOPS
        };

        public static int Count => HdOrder.Length;

        public static int[] CreateHdOrder() => (int[])HdOrder.Clone();

        public static int[] CloneOrDefault(int[] order) =>
            IsValid(order) ? (int[])order.Clone() : CreateHdOrder();

        public static bool IsValid(int[] order)
        {
            if (order == null || order.Length != HdOrder.Length)
                return false;

            for (int index = 0; index < order.Length; index++)
            {
                int good = order[index];
                if (Array.IndexOf(HdOrder, good) < 0)
                    return false;

                for (int previous = 0; previous < index; previous++)
                {
                    if (order[previous] == good)
                        return false;
                }
            }

            return true;
        }

        public static bool AreEqual(int[] left, int[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }

            return true;
        }

        public static int[] SwapGoodWithNeighbor(int[] order, int good, int direction)
        {
            int[] result = CloneOrDefault(order);
            if (direction != -1 && direction != 1)
                return result;

            int currentIndex = Array.IndexOf(result, good);
            if (currentIndex < 0)
                return result;

            int neighborIndex = (currentIndex + direction + result.Length) % result.Length;
            int neighbor = result[neighborIndex];
            result[neighborIndex] = result[currentIndex];
            result[currentIndex] = neighbor;
            return result;
        }

        public static bool TryGetTradeableNeighbor(
            int[] order,
            int currentGood,
            int direction,
            Func<int, bool> isTradeable,
            out int neighborGood)
        {
            neighborGood = currentGood;
            if (!IsValid(order) || direction == 0 || isTradeable == null)
                return false;

            int currentIndex = Array.IndexOf(order, currentGood);
            if (currentIndex < 0)
                return false;

            for (int offset = 1; offset <= order.Length; offset++)
            {
                int index = (currentIndex + direction * offset) % order.Length;
                if (index < 0)
                    index += order.Length;

                int candidate = order[index];
                if (isTradeable(candidate))
                {
                    neighborGood = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
