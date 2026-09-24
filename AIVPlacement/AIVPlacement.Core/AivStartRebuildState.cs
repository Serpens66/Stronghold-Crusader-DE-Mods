using System;
using AIVParser.Core;

namespace AIVPlacement.Core
{
    public readonly struct AivStartRebuildState : IEquatable<AivStartRebuildState>
    {
        public static readonly AivGridPoint CanonicalMarker = new AivGridPoint(56, 43);

        public AivStartRebuildState(AivRotation rotation, AivGridPoint marker)
        {
            if (rotation != AivRotation.Degrees0 &&
                rotation != AivRotation.Degrees90 &&
                rotation != AivRotation.Degrees180 &&
                rotation != AivRotation.Degrees270)
                throw new ArgumentOutOfRangeException(nameof(rotation));
            if (marker.Row < 0 || marker.Row >= AivGridPoint.GridSize ||
                marker.Column < 0 || marker.Column >= AivGridPoint.GridSize)
                throw new ArgumentOutOfRangeException(nameof(marker));

            Rotation = rotation;
            Marker = marker;
            AivGridPoint rotated = AivGridTransform.Rotate(marker, rotation);
            AivGridPoint canonical = AivGridTransform.Rotate(CanonicalMarker, rotation);
            MarkerDeltaX = rotated.Column - canonical.Column;
            MarkerDeltaY = canonical.Row - rotated.Row;
        }

        public AivRotation Rotation { get; }
        public AivGridPoint Marker { get; }
        public int MarkerDeltaX { get; }
        public int MarkerDeltaY { get; }

        public bool Equals(AivStartRebuildState other) =>
            Rotation == other.Rotation && Marker.Equals(other.Marker);

        public override bool Equals(object obj) =>
            obj is AivStartRebuildState other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((int)Rotation * 397) ^ Marker.GetHashCode());
    }
}
