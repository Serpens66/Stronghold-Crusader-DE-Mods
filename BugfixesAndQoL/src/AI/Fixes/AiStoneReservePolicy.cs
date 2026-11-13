// Feature: Pure validation and calculation policy for the AI stone-building reserve.
using System;

namespace BugfixesAndQoL
{
    internal static class AiStoneReservePolicy
    {
        public const int MaximumPlayerId = 8;
        public const int LiveAivSlotCount = 8;
        public const int PlayerResourceStrideElements = 0x160F;

        private const byte SkippedStateZero = 0;
        private const byte InitialFirstBuildState = 1;
        private const byte PreviouslyBuiltState = 3;
        private const byte SkippedStateFour = 4;
        private const byte PlacementRetryState = 5;

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

        public static bool TryFindUniquePlayerSlot(
            ReadOnlySpan<int> ownerPlayerIds,
            int playerId,
            out int liveSlotIndex)
        {
            liveSlotIndex = -1;
            if (playerId < 1 || playerId > MaximumPlayerId ||
                ownerPlayerIds.Length != LiveAivSlotCount)
            {
                return false;
            }

            for (int index = 0; index < ownerPlayerIds.Length; index++)
            {
                if (ownerPlayerIds[index] != playerId)
                    continue;
                if (liveSlotIndex >= 0)
                {
                    liveSlotIndex = -1;
                    return false;
                }
                liveSlotIndex = index;
            }

            return liveSlotIndex >= 0;
        }

        public static bool IsValidMaximumBuildStep(int maximumBuildStep, int stepCapacity) =>
            maximumBuildStep >= 0 && maximumBuildStep < stepCapacity;

        public static bool TryAccumulateReserve(
            byte state,
            short commandBuildingType,
            Func<short, int?> stoneCostResolver,
            ref int reserve)
        {
            if (stoneCostResolver == null || reserve < 0)
                return false;

            bool needsFirstBuildReserve;
            switch (state)
            {
                // Vanilla initializes every generated AIV step to state 1. A failed
                // resource check returns without changing it, so this is the one state
                // that reliably means the first successful build is still outstanding.
                case InitialFirstBuildState:
                    needsFirstBuildReserve = true;
                    break;
                case SkippedStateZero:
                case PreviouslyBuiltState:
                case SkippedStateFour:
                case PlacementRetryState:
                    needsFirstBuildReserve = false;
                    break;
                default:
                    return false;
            }

            if (!needsFirstBuildReserve)
                return true;

            int? cost = stoneCostResolver(commandBuildingType);
            if (!cost.HasValue)
                return true;
            if (cost.Value < 0)
                return false;
            if (cost.Value > reserve)
                reserve = cost.Value;
            return true;
        }

        public static bool TryValidateThreshold(int maximumStone, int variance, int reserve)
        {
            if (reserve < 0)
                return false;

            long threshold = (long)maximumStone + variance + reserve;
            return threshold >= int.MinValue && threshold <= int.MaxValue;
        }

    }
}
