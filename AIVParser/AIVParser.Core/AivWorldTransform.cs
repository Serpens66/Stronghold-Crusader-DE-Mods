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
    }
}
