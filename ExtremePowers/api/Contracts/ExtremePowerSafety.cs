using System;

namespace ExtremePowers.API
{
    public static class ExtremePowerSafety
    {
        public const uint VanillaManaCap = 7000;
        public const int FirstSpawnableUnitType = 1;
        public const int UnitTypeEndSentinel = 90;

        public static bool IsSpawnableUnitType(int unitType) => unitType >= FirstSpawnableUnitType && unitType < UnitTypeEndSentinel;

        public static bool TryCompensateMana(uint mana, int desiredCost, int vanillaCost, out uint compensated)
        {
            compensated = mana;
            if (desiredCost < 0 || vanillaCost < 0) return false;
            long value = (long)mana + vanillaCost - desiredCost;
            if (value < 0 || value > uint.MaxValue) return false;
            compensated = (uint)value;
            return true;
        }

        public static uint SaturatingAdd(uint value, uint addition)
        {
            ulong result = (ulong)value + addition;
            return result > uint.MaxValue ? uint.MaxValue : (uint)result;
        }
    }
}
