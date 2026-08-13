using System;

namespace ImprovedHunters
{
    internal static class GranaryChickenSpawnPolicy
    {
        public const int DefaultMaximumPerPlayer = 10;
        public const int MinimumMaximumPerPlayer = 0;
        public const int MaximumMaximumPerPlayer = 100;

        public static int ClampMaximum(int value)
        {
            if (value < MinimumMaximumPerPlayer)
                return MinimumMaximumPerPlayer;
            if (value > MaximumMaximumPerPlayer)
                return MaximumMaximumPerPlayer;
            return value;
        }

        public static bool TryGetNormalizedVanillaTarget(
            bool managementEnabled,
            int liveChickenCount,
            int configuredMaximum,
            out int normalizedTarget)
        {
            if (!managementEnabled)
            {
                normalizedTarget = 0;
                return false;
            }

            int safeCount = Math.Max(0, liveChickenCount);
            normalizedTarget = safeCount < ClampMaximum(configuredMaximum)
                ? int.MaxValue
                : 0;
            return true;
        }

        public static bool IsMatchingGranaryUnitCreate(
            bool managementEnabled,
            bool alreadyMatched,
            int sourcePlayerId,
            int expectedUnitType,
            int granaryTileX,
            int granaryTileY,
            int expectedHeightElevation,
            int actualOwnerId,
            int actualUnitType,
            int actualWorldTileX,
            int actualWorldTileY,
            int actualHeightElevation) =>
            managementEnabled &&
            !alreadyMatched &&
            sourcePlayerId == actualOwnerId &&
            expectedUnitType == actualUnitType &&
            actualWorldTileX == (long)granaryTileY * 8 &&
            actualWorldTileY == (long)granaryTileX * 8 &&
            expectedHeightElevation == actualHeightElevation;

        public static bool IsTrackedIdentityValid(
            uint expectedGlobalId,
            uint actualGlobalId,
            bool isChicken,
            bool isLive) =>
            expectedGlobalId != 0 &&
            expectedGlobalId == actualGlobalId &&
            isChicken &&
            isLive;

        public static bool CanAssignCompletedSpawn(
            bool managementEnabled,
            long returnValue,
            bool unitResolved,
            bool isChicken,
            uint globalId,
            bool ownerIsNeutral,
            bool colorIsNeutral,
            bool isLive) =>
            managementEnabled &&
            returnValue > 0 &&
            returnValue <= int.MaxValue &&
            unitResolved &&
            isChicken &&
            globalId != 0 &&
            ownerIsNeutral &&
            colorIsNeutral &&
            isLive;

        public static int ChebyshevDistance(int ax, int ay, int bx, int by) =>
            Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        public static bool IsBetterGranaryCandidate(
            int distance,
            int buildingId,
            int playerId,
            int bestDistance,
            int bestBuildingId,
            int bestPlayerId)
        {
            if (distance != bestDistance)
                return distance < bestDistance;
            if (buildingId != bestBuildingId)
                return buildingId < bestBuildingId;
            return playerId < bestPlayerId;
        }
    }
}
