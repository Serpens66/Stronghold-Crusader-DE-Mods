namespace AIVParser.Core
{
    public readonly struct AivWorldTile
    {
        public AivWorldTile(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }

    public static class AivWorldTransform
    {
        private const int NativeKeepReferenceRow = 56;
        private const int NativeKeepReferenceColumn = 43;

        public static AivWorldTile Project(
            AivGridPoint point,
            AivGridPoint keepAnchor,
            int keepWorldX,
            int keepWorldY,
            AivRotation rotation)
        {
            AivGridDelta delta =
                AivGridTransform.GetAnchorDelta(point, keepAnchor, rotation);

            // AIV columns follow world X, while editor rows run opposite to world Y.
            return new AivWorldTile(
                keepWorldX + delta.Column,
                keepWorldY - delta.Row);
        }

        /// <summary>
        /// Projects the absolute, unrotated AIV point exactly like Vanilla's
        /// native fit grid. Unlike <see cref="Project"/>, rotations retain the
        /// origin of the complete 100x100 grid instead of pivoting around the
        /// AIV's stored Keep marker.
        /// </summary>
        public static AivWorldTile ProjectNativeFit(
            AivGridPoint point,
            int keepWorldX,
            int keepWorldY,
            AivRotation rotation)
        {
            AivGridPoint rotatedPoint = AivGridTransform.Rotate(point, rotation);
            return new AivWorldTile(
                keepWorldX + rotatedPoint.Column - NativeKeepReferenceColumn,
                keepWorldY - rotatedPoint.Row + NativeKeepReferenceRow);
        }
    }
}
