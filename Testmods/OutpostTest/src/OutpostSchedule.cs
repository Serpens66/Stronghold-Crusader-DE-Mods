using System;

namespace OutpostTest
{
    // Macemen profile 2; arithmetic mirrors ABB90, random draws are mod-local.
    internal static class OutpostSchedule
    {
        internal static bool SameIdentity(uint global, int owner, int type, uint currentGlobal, int currentOwner, int currentType) =>
            global == currentGlobal && owner == currentOwner && type == currentType;
        internal static bool FinishRetired(bool linked, bool sameBuilding, int type) => linked || (!sameBuilding && type == 2);
        internal static bool IsOutpost(int type) => type == 2 || type == 106 || type == 107;
        internal static bool HasCapacity(int mode, int count, int extraThisTick, int limit) =>
            mode == 0 || mode == 99 || (long)count + extraThisTick < limit;
        internal static int GroupWait(int acceleration, int mode, bool acceleratedWorld)
        {
            int value = 2000 - acceleration;
            if (mode != 0 && mode != 99) value = Math.Max(400, value);
            return Math.Max(acceleratedWorld ? 100 : 600, value);
        }
        internal static int SpawnWait(int acceleration, int size)
        {
            ValidateSize(size);
            return Math.Max(100, 250 - acceleration) * (100 - 15 * size) / 100;
        }
        internal static int Target(int size, int roll)
        {
            ValidateSize(size);
            if (roll < 0 || roll >= 10) throw new ArgumentOutOfRangeException(nameof(roll));
            return (10 + roll) * (size + 1);
        }
        private static void ValidateSize(int size)
        {
            if (size < 0 || size > 6) throw new InvalidOperationException("Unsupported outpost size setting.");
        }
        internal static int Batch(int members, int target, int delay, bool ai) =>
            delay > 0 && members == 0 && ai ? target / 2 : 1;
        internal static bool DelayBlocks(int members, int target, int delay) => delay > 0 && members >= target - 1;
        internal static bool Complete(int members, int target, int delay) => members >= target && delay <= 0;
        internal static int Roll(uint global, int tick, uint salt, int exclusiveMax)
        {
            unchecked {
                uint x = global ^ (uint)tick * 0x9E3779B9u ^ salt;
                x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; x ^= x >> 16;
                return (int)(x % (uint)exclusiveMax);
            }
        }
    }
}
