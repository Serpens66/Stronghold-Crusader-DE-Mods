using System;
using System.Collections.Generic;
using System.Linq;

namespace AIVParser.Core
{
    public enum AivDiagnosticSeverity
    {
        Warning,
        Error
    }

    public enum AivRotation
    {
        Degrees0 = 0,
        Degrees90 = 90,
        Degrees180 = 180,
        Degrees270 = 270
    }

    public enum AivItemCategory
    {
        Unknown,
        Building,
        Keep,
        HighWallPath,
        LowWallPath,
        CrenelPath,
        Stair,
        PitchDitchPath,
        MoatPath,
        Trap
    }

    public enum AivVisualGroup
    {
        Unknown,
        GeneralBuilding,
        Housing,
        Food,
        Industry,
        Storage,
        Military,
        Defense,
        Civic,
        PositiveFear,
        NegativeFear,
        Water
    }

    public enum AivBlockedAreaKind
    {
        Campfire,
        PlacementReserve
    }

    public enum AivBlockedAreaSource
    {
        DefinitiveEditionNativeTable,
        EditorDerivedKeepCampfire
    }

    public sealed class AivDiagnostic
    {
        public AivDiagnostic(
            AivDiagnosticSeverity severity,
            string code,
            string message,
            string location = null)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Location = location ?? string.Empty;
        }

        public AivDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string Location { get; }
    }

    public readonly struct AivGridPoint : IEquatable<AivGridPoint>
    {
        public const int GridSize = 100;

        public AivGridPoint(int encodedOffset)
        {
            EncodedOffset = encodedOffset;
            Row = encodedOffset / GridSize;
            Column = encodedOffset % GridSize;
        }

        public AivGridPoint(int row, int column)
        {
            Row = row;
            Column = column;
            EncodedOffset = row * GridSize + column;
        }

        public int EncodedOffset { get; }
        public int Row { get; }
        public int Column { get; }

        public bool Equals(AivGridPoint other)
        {
            return EncodedOffset == other.EncodedOffset &&
                   Row == other.Row &&
                   Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is AivGridPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((EncodedOffset * 397) ^ Row) * 397 ^ Column;
            }
        }

        public override string ToString()
        {
            return $"({Row}, {Column})";
        }
    }

    public readonly struct AivGridDelta : IEquatable<AivGridDelta>
    {
        public AivGridDelta(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }
        public int Column { get; }

        public bool Equals(AivGridDelta other)
        {
            return Row == other.Row && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is AivGridDelta other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Row * 397) ^ Column;
            }
        }

        public override string ToString()
        {
            return $"({Row:+#;-#;0}, {Column:+#;-#;0})";
        }
    }

    public readonly struct AivFootprint
    {
        public AivFootprint(
            AivGridPoint rawAnchor,
            AivGridPoint rotatedAnchor,
            AivGridPoint minimum,
            AivGridPoint maximum,
            int size,
            AivRotation rotation)
        {
            RawAnchor = rawAnchor;
            RotatedAnchor = rotatedAnchor;
            Minimum = minimum;
            Maximum = maximum;
            Size = size;
            Rotation = rotation;
        }

        // Editor rows grow upwards, so buildings extend towards smaller rows.
        public AivGridPoint RawAnchor { get; }
        public AivGridPoint RotatedAnchor { get; }
        public AivGridPoint Minimum { get; }
        public AivGridPoint Maximum { get; }
        public AivGridPoint EditorTopLeft =>
            new AivGridPoint(Maximum.Row, Minimum.Column);
        public AivGridPoint EditorBottomRight =>
            new AivGridPoint(Minimum.Row, Maximum.Column);
        public int Size { get; }
        public AivRotation Rotation { get; }
    }

    public readonly struct AivBlockedArea
    {
        public AivBlockedArea(
            string name,
            AivBlockedAreaKind kind,
            AivBlockedAreaSource source,
            AivFootprint footprint)
        {
            Name = name ?? string.Empty;
            Kind = kind;
            Source = source;
            Footprint = footprint;
        }

        public string Name { get; }
        public AivBlockedAreaKind Kind { get; }
        public AivBlockedAreaSource Source { get; }
        public AivFootprint Footprint { get; }
    }

    public sealed class AivMapperInfo
    {
        public AivMapperInfo(
            int value,
            string name,
            AivItemCategory category,
            bool isKnown,
            int? footprintSize = null,
            AivVisualGroup visualGroup = AivVisualGroup.Unknown,
            string displayName = null)
        {
            Value = value;
            Name = name ?? $"UNKNOWN_MAPPER_{value}";
            Category = category;
            IsKnown = isKnown;
            FootprintSize = footprintSize;
            VisualGroup = visualGroup;
            DisplayName = displayName ?? Name;
        }

        public int Value { get; }
        public string Name { get; }
        public AivItemCategory Category { get; }
        public bool IsKnown { get; }
        public int? FootprintSize { get; }
        public AivVisualGroup VisualGroup { get; }
        public string DisplayName { get; }
    }

    public sealed class AivMiscTypeInfo
    {
        public AivMiscTypeInfo(
            int jsonValue,
            int engineValue,
            string name,
            bool isKnown)
        {
            JsonValue = jsonValue;
            EngineValue = engineValue;
            Name = name ?? $"UNKNOWN_MISC_{engineValue}";
            IsKnown = isKnown;
        }

        public int JsonValue { get; }
        public int EngineValue { get; }
        public string Name { get; }
        public bool IsKnown { get; }
    }

    public sealed class AivBuildFrame
    {
        public AivBuildFrame(
            int buildIndex,
            int rawItemType,
            AivMapperInfo mapper,
            bool shouldPause,
            IReadOnlyList<AivGridPoint> positions)
        {
            BuildIndex = buildIndex;
            RawItemType = rawItemType;
            Mapper = mapper;
            ShouldPause = shouldPause;
            Positions = positions ?? Array.Empty<AivGridPoint>();
        }

        public int BuildIndex { get; }
        public int RawItemType { get; }
        public AivMapperInfo Mapper { get; }
        public bool ShouldPause { get; }
        public IReadOnlyList<AivGridPoint> Positions { get; }
    }

    public sealed class AivMiscPlacement
    {
        public AivMiscPlacement(
            int sourceIndex,
            int rawItemType,
            AivMiscTypeInfo itemType,
            int slotIndex,
            AivGridPoint position)
        {
            SourceIndex = sourceIndex;
            RawItemType = rawItemType;
            ItemType = itemType;
            SlotIndex = slotIndex;
            Position = position;
        }

        public int SourceIndex { get; }
        public int RawItemType { get; }
        public AivMiscTypeInfo ItemType { get; }
        public int SlotIndex { get; }
        public AivGridPoint Position { get; }
    }

    public sealed class AivBlueprint
    {
        public AivBlueprint(
            string sourceName,
            int pauseDelayAmount,
            IReadOnlyList<AivBuildFrame> frames,
            IReadOnlyList<AivMiscPlacement> miscItems,
            AivGridPoint? keepAnchor)
        {
            SourceName = sourceName ?? string.Empty;
            PauseDelayAmount = pauseDelayAmount;
            Frames = frames ?? Array.Empty<AivBuildFrame>();
            MiscItems = miscItems ?? Array.Empty<AivMiscPlacement>();
            KeepAnchor = keepAnchor;
        }

        public string SourceName { get; }
        public int PauseDelayAmount { get; }
        public IReadOnlyList<AivBuildFrame> Frames { get; }
        public IReadOnlyList<AivMiscPlacement> MiscItems { get; }
        public AivGridPoint? KeepAnchor { get; }
    }

    public sealed class AivParseResult
    {
        public AivParseResult(AivBlueprint blueprint, IReadOnlyList<AivDiagnostic> diagnostics)
        {
            Blueprint = blueprint;
            Diagnostics = diagnostics ?? Array.Empty<AivDiagnostic>();
        }

        public AivBlueprint Blueprint { get; }
        public IReadOnlyList<AivDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.All(d => d.Severity != AivDiagnosticSeverity.Error);
        public int ErrorCount => Diagnostics.Count(d => d.Severity == AivDiagnosticSeverity.Error);
        public int WarningCount => Diagnostics.Count(d => d.Severity == AivDiagnosticSeverity.Warning);
    }
}
