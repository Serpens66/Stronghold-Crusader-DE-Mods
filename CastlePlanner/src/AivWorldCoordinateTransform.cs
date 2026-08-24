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
