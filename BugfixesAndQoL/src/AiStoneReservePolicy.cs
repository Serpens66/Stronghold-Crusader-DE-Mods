// Feature: Pure validation and calculation policy for the AI stone-building reserve.
using System;

namespace BugfixesAndQoL
{
    internal static class AiStoneReservePolicy
    {
        public const int MaximumPlayerId = 8;
        public const int AivSlotCount = 9;
        public const int AivSlotSize = 0x6D98;
        public const int FirstPlayerSlot = 1;
        public const int PlayerIdOffset = 0x04;
        public const int HighestFrameOffset = 0x24;
        public const int StepsOffset = 0x38;
        public const int StepSize = 12;
        public const int MaximumSteps = 1000;
        public const int PlayerResourceStrideElements = 0x160F;

        private const byte DisabledStatus = 0;
        private const byte UnbuiltStatus = 1;
        private const byte BuiltStatus = 3;
        private const byte InsufficientRoomStatus = 4;
        private const byte InsufficientResourcesStatus = 5;

        public static bool TryGetPlayerId(ulong playerResourceOffset, out int playerId)
        {
            playerId = 0;
            if (playerResourceOffset % PlayerResourceStrideElements != 0)
                return false;

            ulong candidate = playerResourceOffset / PlayerResourceStrideElements;
            if (candidate < 1 || candidate > MaximumPlayerId)
                return false;

            playerId = checked((int)candidate);
            return true;
        }

        public static bool TryFindPlayerSlot(
            ReadOnlySpan<byte> aivTable,
            int playerId,
            out int slotOffset)
        {
            slotOffset = 0;
            if (playerId < 1 || playerId > MaximumPlayerId ||
                aivTable.Length < AivSlotCount * AivSlotSize)
            {
                return false;
            }

            int matchingOffset = -1;
            // Slot zero is reserved. Vanilla's allocator only searches slots 1 through 8.
            for (int slot = FirstPlayerSlot; slot < AivSlotCount; slot++)
            {
                int candidateOffset = slot * AivSlotSize;
                if (ReadInt32(aivTable, candidateOffset + PlayerIdOffset) != playerId)
                    continue;
                if (matchingOffset >= 0)
                    return false;
                matchingOffset = candidateOffset;
            }

            if (matchingOffset < 0)
                return false;

            slotOffset = matchingOffset;
            return true;
        }

        public static bool TryCalculateReserve(
            ReadOnlySpan<byte> aivSlot,
            Func<short, int?> stoneCostResolver,
            out int reserve)
        {
            reserve = 0;
            if (stoneCostResolver == null || aivSlot.Length < StepsOffset + StepSize)
                return false;

            int highestFrame = ReadInt32(aivSlot, HighestFrameOffset);
            if (highestFrame < 0 || highestFrame >= MaximumSteps)
            {
                return false;
            }

            int maximum = 0;
            // Vanilla stores the highest valid index, not a step count.
            for (int index = 0; index <= highestFrame; index++)
            {
                int stepOffset = StepsOffset + index * StepSize;
                byte status = aivSlot[stepOffset];
                bool pending;
                switch (status)
                {
                    case UnbuiltStatus:
                    case InsufficientResourcesStatus:
                        pending = true;
                        break;
                    case DisabledStatus:
                    case BuiltStatus:
                    case InsufficientRoomStatus:
                        pending = false;
                        break;
                    default:
                        return false;
                }

                if (!pending)
                    continue;

                short commandBuildingType = ReadInt16(aivSlot, stepOffset + 2);
                int? cost = stoneCostResolver(commandBuildingType);
                if (!cost.HasValue)
                    continue;
                if (cost.Value < 0)
                    return false;
                if (cost.Value > maximum)
                    maximum = cost.Value;
            }

            reserve = maximum;
            return true;
        }

        public static bool TryValidateThreshold(int maximumStone, int variance, int reserve)
        {
            if (reserve < 0)
                return false;

            long threshold = (long)maximumStone + variance + reserve;
            return threshold >= int.MinValue && threshold <= int.MaxValue;
        }

        private static short ReadInt16(ReadOnlySpan<byte> data, int offset) =>
            unchecked((short)(data[offset] | data[offset + 1] << 8));

        private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
            data[offset] |
            data[offset + 1] << 8 |
            data[offset + 2] << 16 |
            data[offset + 3] << 24;
    }
}
