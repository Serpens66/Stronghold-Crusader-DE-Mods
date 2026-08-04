using AIVParser.Core;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public static class AivInitialRotationResolver
    {
        private const int NativeMapCenter = 400;

        public static AivRotation ResolveMapFacing(MapCoordinate keep)
        {
            int horizontal = System.Math.Abs(keep.X - NativeMapCenter);
            int vertical = System.Math.Abs(keep.Y - NativeMapCenter);

            // Vanilla first resolves one of eight compass sectors, then folds diagonals
            // onto the four even AIV orientations and swaps its east/west codes.
            int orientation;
            if (vertical * 2 < horizontal)
            {
                orientation = keep.X <= NativeMapCenter ? 6 : 2;
            }
            else if (horizontal * 2 < vertical)
            {
                orientation = keep.Y <= NativeMapCenter ? 4 : 0;
            }
            else if (keep.Y > NativeMapCenter)
            {
                orientation = keep.X <= NativeMapCenter ? 0 : 2;
            }
            else
            {
                orientation = keep.X <= NativeMapCenter ? 6 : 4;
            }

            return (AivRotation)(orientation * 45);
        }
    }
}
