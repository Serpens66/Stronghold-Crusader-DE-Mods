using AIVParser.Core;
using System;

namespace CastlePlanner
{
    internal static class AivProjectileTransform
    {
        private const int MapSizeInTiles = 800;
        private const int ProjectileUnitsPerTile = 8;

        public static int ToProjectileCoordinate(int tileCoordinate)
        {
            if (tileCoordinate < 0 || tileCoordinate >= MapSizeInTiles)
                throw new ArgumentOutOfRangeException(nameof(tileCoordinate));

            return checked(tileCoordinate * ProjectileUnitsPerTile);
        }
    }

    internal static class AivStarterUnitTransform
    {
        private const int NativeGridLastTile = AivGridPoint.GridSize - 1;
        private const int NativeKeepReferenceRow = 56;
        private const int NativeKeepReferenceColumn = 43;
        private const int WorldUnitsPerTile = 8;

        public static bool TryProjectReservedWorldPosition(
            int sourceWorldX,
            int sourceWorldY,
            int requestedKeepX,
            int requestedKeepY,
            AivRotation rotation,
            out int targetWorldX,
            out int targetWorldY)
        {
            targetWorldX = sourceWorldX;
            targetWorldY = sourceWorldY;
            if (sourceWorldX < 0 || sourceWorldY < 0)
                return false;

            int sourceTileX = sourceWorldX / WorldUnitsPerTile;
            int sourceTileY = sourceWorldY / WorldUnitsPerTile;
            int offsetX = sourceTileX - requestedKeepX;
            int offsetY = sourceTileY - requestedKeepY;
            if (!IsInsideUnrotatedKeepReserve(offsetX, offsetY))
                return false;

            long keepWorldX = (long)requestedKeepX * WorldUnitsPerTile;
            long keepWorldY = (long)requestedKeepY * WorldUnitsPerTile;
            long row = keepWorldY - sourceWorldY +
                (NativeKeepReferenceRow * WorldUnitsPerTile);
            long column = sourceWorldX - keepWorldX +
                (NativeKeepReferenceColumn * WorldUnitsPerTile);
            long last = NativeGridLastTile * WorldUnitsPerTile;

            long rotatedRow;
            long rotatedColumn;
            switch (rotation)
            {
                case AivRotation.Degrees0:
                    rotatedRow = row;
                    rotatedColumn = column;
                    break;
                case AivRotation.Degrees90:
                    rotatedRow = column;
                    rotatedColumn = last - row;
                    break;
                case AivRotation.Degrees180:
                    rotatedRow = last - row;
                    rotatedColumn = last - column;
                    break;
                case AivRotation.Degrees270:
                    rotatedRow = last - column;
                    rotatedColumn = row;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Unsupported AIV rotation.");
            }

            long projectedX = keepWorldX + rotatedColumn -
                (NativeKeepReferenceColumn * WorldUnitsPerTile);
            long projectedY = keepWorldY - rotatedRow +
                (NativeKeepReferenceRow * WorldUnitsPerTile);
            if (projectedX < int.MinValue || projectedX > int.MaxValue ||
                projectedY < int.MinValue || projectedY > int.MaxValue)
            {
                return false;
            }

            targetWorldX = (int)projectedX;
            targetWorldY = (int)projectedY;
            return true;
        }

        private static bool IsInsideUnrotatedKeepReserve(int offsetX, int offsetY)
        {
            // These are the exact native Keep reservations represented in
            // AivBlockedAreaCatalog: campfire, 7x7 staging area and connectors.
            bool campfire = offsetX >= 7 && offsetX <= 11 &&
                offsetY >= 2 && offsetY <= 6;
            bool stagingArea = offsetX >= 0 && offsetX <= 6 &&
                offsetY >= 8 && offsetY <= 14;
            bool connector = offsetX >= 2 && offsetX <= 4 && offsetY == 7;
            return campfire || stagingArea || connector;
        }
    }

    internal static class AivNativeKeepAlignment
    {
        public static AivWorldTile ResolveNativeReference(
            AivGridPoint keepAnchor,
            int footprintSize,
            int liveKeepX,
            int liveKeepY,
            AivRotation rotation)
        {
            if (footprintSize < 1)
                throw new ArgumentOutOfRangeException(nameof(footprintSize));

            AivFootprint footprint = AivGridTransform.GetFootprint(
                keepAnchor,
                footprintSize,
                AivRotation.Degrees0);
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int row = footprint.Minimum.Row; row <= footprint.Maximum.Row; row++)
            {
                for (int column = footprint.Minimum.Column;
                     column <= footprint.Maximum.Column;
                     column++)
                {
                    AivWorldTile projected = AivWorldTransform.ProjectNativeFit(
                        new AivGridPoint(row, column),
                        0,
                        0,
                        rotation);
                    minimumX = Math.Min(minimumX, projected.X);
                    minimumY = Math.Min(minimumY, projected.Y);
                }
            }

            // Native rotates the complete 100x100 AIV grid. Undo the resulting
            // Keep-footprint offset so the projected footprint starts at the live Keep.
            return new AivWorldTile(liveKeepX - minimumX, liveKeepY - minimumY);
        }
    }
}
