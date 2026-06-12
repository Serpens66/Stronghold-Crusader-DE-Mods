using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace UnitCosts
{
    public sealed class UnitCostValues
    {
        public const string UnchangedKey = "UNCHANGED";

        public UnitCostValues(string slot1, string slot2, string slot3, string slot4, int gold)
        {
            string[] slots =
            {
                NormalizeSlotKey(slot1),
                NormalizeSlotKey(slot2),
                NormalizeSlotKey(slot3),
                NormalizeSlotKey(slot4)
            };

            NormalizeDuplicateSlots(slots);
            Slot1 = slots[0];
            Slot2 = slots[1];
            Slot3 = slots[2];
            Slot4 = slots[3];
            Gold = ClampCost(gold);
        }

        public string Slot1 { get; }
        public string Slot2 { get; }
        public string Slot3 { get; }
        public string Slot4 { get; }
        public int Gold { get; }

        public static string NormalizeSlotKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UnchangedKey;

            return value.Trim().ToUpperInvariant();
        }

        private static void NormalizeDuplicateSlots(string[] slots)
        {
            HashSet<string> usedGoods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string horseKey = eGoods._SE_REQUIRE_HORSE.ToString();
            string nullKey = eGoods.STORED_NULL.ToString();

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == UnchangedKey || slots[i] == nullKey)
                    continue;

                if (i < 3 && string.Equals(slots[i], horseKey, StringComparison.OrdinalIgnoreCase))
                {
                    slots[i] = UnchangedKey;
                    continue;
                }

                if (!usedGoods.Add(slots[i]))
                    slots[i] = UnchangedKey;
            }
        }

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
