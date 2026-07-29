using System;

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
    }
}
