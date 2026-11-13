using System;

namespace BugfixesAndQoL
{
    internal readonly struct KeepFlagPosition
    {
        internal KeepFlagPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal int X { get; }
        internal int Y { get; }

        public override string ToString() => $"({X},{Y})";
    }

    internal static class KeepFlagRotationPolicy
    {
        internal static bool TryGetPositions(
            int originX,
            int originY,
            int scale,
            int orientation,
            out KeepFlagPosition vanillaPosition,
            out KeepFlagPosition correctedPosition)
        {
            vanillaPosition = default;
            correctedPosition = default;
            if (originX < 0 || originY < 0 || scale < 1)
                return false;

            vanillaPosition = new KeepFlagPosition(
                checked((originX + scale - 1) * 8 + 7),
                checked(originY * 8));
            return TryGetCorrectedPosition(
                originX,
                originY,
                scale,
                orientation,
                vanillaPosition,
                out vanillaPosition,
                out correctedPosition,
                out _,
                out _);
        }

        internal static bool TryGetCorrectedPosition(
            int originX,
            int originY,
            int scale,
            int orientation,
            KeepFlagPosition actualVanillaPosition,
            out KeepFlagPosition vanillaPosition,
            out KeepFlagPosition correctedPosition,
            out int vanillaMicroX,
            out int vanillaMicroY)
        {
            vanillaPosition = default;
            correctedPosition = default;
            vanillaMicroX = 0;
            vanillaMicroY = 0;
            if (originX < 0 || originY < 0 || scale < 1 ||
                !TryNormalizeOrientation(orientation, out int normalizedOrientation))
            {
                return false;
            }

            int originWorldX = checked(originX * 8);
            int originWorldY = checked(originY * 8);
            int farTileX = checked(originX + scale - 1);
            if (actualVanillaPosition.X / 8 != farTileX ||
                actualVanillaPosition.Y / 8 != originY)
            {
                return false;
            }

            vanillaMicroX = actualVanillaPosition.X % 8;
            vanillaMicroY = actualVanillaPosition.Y % 8;
            if ((vanillaMicroX != 0 && vanillaMicroX != 7) ||
                (vanillaMicroY != 0 && vanillaMicroY != 7))
            {
                return false;
            }

            int localX = checked(actualVanillaPosition.X - originWorldX);
            int localY = checked(actualVanillaPosition.Y - originWorldY);
            int last = checked(scale * 8 - 1);
            int rotatedX;
            int rotatedY;
            switch (normalizedOrientation)
            {
                case 0:
                    rotatedX = localX;
                    rotatedY = localY;
                    break;
                case 2:
                    rotatedX = localY;
                    rotatedY = last - localX;
                    break;
                case 4:
                    rotatedX = last - localX;
                    rotatedY = last - localY;
                    break;
                case 6:
                    rotatedX = last - localY;
                    rotatedY = localX;
                    break;
                default:
                    throw new InvalidOperationException("Normalized keep orientation is invalid.");
            }

            vanillaPosition = actualVanillaPosition;
            correctedPosition = new KeepFlagPosition(
                checked(originWorldX + rotatedX),
                checked(originWorldY + rotatedY));
            return true;
        }

        internal static bool TryResolveObservedPosition(
            int originX,
            int originY,
            int scale,
            int orientation,
            KeepFlagPosition observedPosition,
            out KeepFlagPosition vanillaPosition,
            out KeepFlagPosition correctedPosition)
        {
            vanillaPosition = default;
            correctedPosition = default;
            if (originX < 0 || originY < 0 || scale < 1)
                return false;

            int farTileX = checked(originX + scale - 1);
            for (int variant = 0; variant < 4; variant++)
            {
                int microX = variant < 2 ? 7 : 0;
                int microY = variant == 0 || variant == 3 ? 0 : 7;
                var candidate = new KeepFlagPosition(
                    checked(farTileX * 8 + microX),
                    checked(originY * 8 + microY));
                if (!TryGetCorrectedPosition(
                    originX,
                    originY,
                    scale,
                    orientation,
                    candidate,
                    out KeepFlagPosition candidateVanilla,
                    out KeepFlagPosition candidateCorrected,
                    out _,
                    out _))
                {
                    return false;
                }

                bool atVanilla = observedPosition.X == candidateVanilla.X &&
                    observedPosition.Y == candidateVanilla.Y;
                bool atCorrected = observedPosition.X == candidateCorrected.X &&
                    observedPosition.Y == candidateCorrected.Y;
                if (!atVanilla && !atCorrected)
                    continue;

                vanillaPosition = candidateVanilla;
                correctedPosition = candidateCorrected;
                return true;
            }

            return false;
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

        internal static KeepFlagPosition ToTilePosition(KeepFlagPosition projectilePosition) =>
            new KeepFlagPosition(projectilePosition.X / 8, projectilePosition.Y / 8);

        internal static bool TryGetProjectileIdFromSpanIndex(int spanIndex, out int projectileId)
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
            KeepFlagPosition vanillaPosition)
        {
            return sourceX == vanillaPosition.X &&
                sourceY == vanillaPosition.Y &&
                targetX == vanillaPosition.X &&
                targetY == vanillaPosition.Y;
        }

        internal static int ExpectedScale(int mapperOrStructKind)
        {
            return mapperOrStructKind == 3 ? 11 :
                mapperOrStructKind == 1 || mapperOrStructKind == 2 ? 7 : 0;
        }
    }
}
