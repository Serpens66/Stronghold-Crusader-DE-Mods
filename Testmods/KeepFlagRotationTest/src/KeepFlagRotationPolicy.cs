using System;

namespace KeepFlagRotationTest
{
    internal readonly struct FlagPosition
    {
        public FlagPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public override string ToString() => $"({X},{Y})";
    }

    internal static class KeepFlagRotationPolicy
    {
        internal static bool TryGetPositions(
            int originX,
            int originY,
            int scale,
            int orientation,
            out FlagPosition vanillaPosition,
            out FlagPosition correctedPosition)
        {
            vanillaPosition = default;
            correctedPosition = default;
            if (originX < 0 || originY < 0 || scale < 1)
                return false;

            if (!TryNormalizeOrientation(orientation, out int normalizedOrientation))
                return false;

            int microX;
            int microY;
            switch (normalizedOrientation)
            {
                case 0:
                    microX = 7;
                    microY = 0;
                    break;
                case 2:
                    microX = 0;
                    microY = 0;
                    break;
                case 4:
                    microX = 0;
                    microY = 7;
                    break;
                case 6:
                    microX = 7;
                    microY = 7;
                    break;
                default:
                    throw new InvalidOperationException("Normalized keep orientation is invalid.");
            }

            int farX = checked(originX + scale - 1);
            int farY = checked(originY + scale - 1);
            vanillaPosition = new FlagPosition(
                checked(farX * 8 + 7),
                checked(originY * 8));
            correctedPosition = new FlagPosition(
                checked((microX == 7 ? farX : originX) * 8 + microX),
                checked((microY == 7 ? farY : originY) * 8 + microY));
            return true;
        }

        internal static bool TryNormalizeOrientation(int orientation, out int normalizedOrientation)
        {
            normalizedOrientation = orientation == 15 ? 0 : orientation;
            return normalizedOrientation == 0 || normalizedOrientation == 2 ||
                normalizedOrientation == 4 || normalizedOrientation == 6;
        }

        internal static string DescribeDirection(int normalizedOrientation)
        {
            switch (normalizedOrientation)
            {
                case 0: return "South";
                case 2: return "East";
                case 4: return "North";
                case 6: return "West";
                default: return "Unknown";
            }
        }

        internal static string DescribeCorner(int normalizedOrientation)
        {
            switch (normalizedOrientation)
            {
                case 0: return "NorthEast";
                case 2: return "NorthWest";
                case 4: return "SouthWest";
                case 6: return "SouthEast";
                default: return "Unknown";
            }
        }

        internal static FlagPosition ToTilePosition(FlagPosition projectilePosition) =>
            new FlagPosition(projectilePosition.X / 8, projectilePosition.Y / 8);

        internal static bool TryGetProjectileIdFromSpanIndex(
            int spanIndex,
            out int projectileId)
        {
            projectileId = spanIndex;
            return spanIndex > 0;
        }

        internal static bool TryResolveUniqueProjectileId(
            int matchCount,
            int lastMatchedProjectileId,
            out int projectileId)
        {
            projectileId = matchCount == 1 ? lastMatchedProjectileId : 0;
            return projectileId > 0;
        }

        internal static bool IsStationaryMainFlag(
            int sourceUnitId,
            int playerSourceId,
            int unitPlayerSourceId,
            int sourceX,
            int sourceY,
            int sourceElevation,
            int targetX,
            int targetY,
            int targetElevation,
            int attackedUnitId)
        {
            return sourceUnitId == 0 &&
                unitPlayerSourceId >= 1 && unitPlayerSourceId <= 8 &&
                playerSourceId == unitPlayerSourceId &&
                attackedUnitId == 0 &&
                targetX == sourceX &&
                targetY == sourceY &&
                targetElevation == sourceElevation;
        }

        internal static bool MatchesVanillaPosition(
            int sourceX,
            int sourceY,
            int targetX,
            int targetY,
            FlagPosition vanillaPosition)
        {
            return sourceX == vanillaPosition.X &&
                sourceY == vanillaPosition.Y &&
                targetX == vanillaPosition.X &&
                targetY == vanillaPosition.Y;
        }

        internal static int ExpectedScale(int mapperOrStructKind)
        {
            // Callers normalize Keep1/Keep2 to 1/2 and Keep3 to 3.
            return mapperOrStructKind == 3 ? 11 :
                mapperOrStructKind == 1 || mapperOrStructKind == 2 ? 7 : 0;
        }
    }
}
