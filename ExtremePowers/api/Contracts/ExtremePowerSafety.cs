using System;

namespace ExtremePowers.API
{
    public static class ExtremePowerSafety
    {
        public const uint VanillaManaCap = 7000;
        public const int FirstSpawnableUnitType = 1;
        public const int UnitTypeEndSentinel = 90;
        public const int MaximumPlayerId = 8;

        public static bool IsSpawnableUnitType(int unitType) => unitType >= FirstSpawnableUnitType && unitType < UnitTypeEndSentinel;
        public static bool IsValidSpawnOwnerPlayerId(int ownerPlayerId) => ownerPlayerId >= 0 && ownerPlayerId <= MaximumPlayerId;

        public static bool TryCompensateMana(uint mana, int desiredCost, int vanillaCost, out uint compensated)
        {
            compensated = mana;
            if (desiredCost < 0 || vanillaCost < 0) return false;
            // Native selection/dispatch compare this UInt32 field as a signed Int32.
            if (mana > int.MaxValue) return false;
            long value = (long)mana + vanillaCost - desiredCost;
            if (value < 0 || value > int.MaxValue) return false;
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
