using System;
using System.Linq;

namespace AIVParser.Core
{
    public static class AivGridTransform
    {
        public static AivGridPoint Rotate(AivGridPoint point, AivRotation rotation)
        {
            int last = AivGridPoint.GridSize - 1;

            switch (rotation)
            {
                case AivRotation.Degrees0:
                    return point;
                case AivRotation.Degrees90:
                    return new AivGridPoint(point.Column, last - point.Row);
                case AivRotation.Degrees180:
                    return new AivGridPoint(last - point.Row, last - point.Column);
                case AivRotation.Degrees270:
                    return new AivGridPoint(last - point.Column, point.Row);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Unsupported AIV rotation.");
            }
        }

        /// <summary>
        /// Produces a relative placement delta without deciding how AIV rows/columns
        /// correspond to the game's world tile X/Y axes.
        /// </summary>
        public static AivGridDelta GetAnchorDelta(
            AivGridPoint point,
            AivGridPoint keepAnchor,
            AivRotation rotation)
        {
            AivGridPoint rotatedPoint = Rotate(point, rotation);
            AivGridPoint rotatedKeep = Rotate(keepAnchor, rotation);
            return new AivGridDelta(
                rotatedPoint.Row - rotatedKeep.Row,
                rotatedPoint.Column - rotatedKeep.Column);
        }

        public static AivFootprint GetFootprint(
            AivGridPoint rawAnchor,
            int size,
            AivRotation rotation)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            int firstRow = rawAnchor.Row - size + 1;
            int lastColumn = rawAnchor.Column + size - 1;
            if (firstRow < 0 ||
                lastColumn >= AivGridPoint.GridSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rawAnchor),
                    "The footprint extends outside the 100x100 AIV grid.");
            }

            AivGridPoint[] corners =
            {
                Rotate(rawAnchor, rotation),
                Rotate(new AivGridPoint(rawAnchor.Row, lastColumn), rotation),
                Rotate(new AivGridPoint(firstRow, rawAnchor.Column), rotation),
                Rotate(new AivGridPoint(firstRow, lastColumn), rotation)
            };

            int minRow = corners.Min(point => point.Row);
            int maxRow = corners.Max(point => point.Row);
            int minColumn = corners.Min(point => point.Column);
            int maxColumn = corners.Max(point => point.Column);

            return new AivFootprint(
                rawAnchor,
                Rotate(rawAnchor, rotation),
                new AivGridPoint(minRow, minColumn),
                new AivGridPoint(maxRow, maxColumn),
                size,
                rotation);
        }
    }
}
