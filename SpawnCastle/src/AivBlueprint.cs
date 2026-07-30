using System;
using System.Collections.Generic;

namespace SpawnCastle
{
    // Unity JsonUtility assigns these public fields through reflection.
#pragma warning disable 0649
    [Serializable]
    internal sealed class AivJsonDocument
    {
        public int pauseDelayAmount;
        public List<AivJsonFrame> frames;
        public List<AivJsonMiscItem> miscItems;
    }

    [Serializable]
    internal sealed class AivJsonFrame
    {
        public int itemType;
        public List<int> tilePositionOfsets;
        public bool shouldPause;
    }

    [Serializable]
    internal sealed class AivJsonMiscItem
    {
        public int positionOfset;
        public int itemType;
        public int number;
    }
#pragma warning restore 0649

    internal enum AivDiagnosticSeverity
    {
        Warning,
        Error
    }

    internal enum AivRotation
    {
        Degrees0 = 0
    }

    internal enum AivItemCategory
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

    internal sealed class AivDiagnostic
    {
        public AivDiagnostic(
            AivDiagnosticSeverity severity,
            string code,
            string message,
            string location)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Location = location;
        }

        public AivDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string Location { get; }
    }

    internal readonly struct AivGridPoint
    {
        public const int GridSize = 100;

        public AivGridPoint(int encodedOffset)
        {
            EncodedOffset = encodedOffset;
            Row = encodedOffset / GridSize;
            Column = encodedOffset % GridSize;
        }

        public int EncodedOffset { get; }
        public int Row { get; }
        public int Column { get; }
    }

    internal readonly struct AivGridDelta
    {
        public AivGridDelta(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }
        public int Column { get; }
    }

    internal sealed class AivMapperInfo
    {
        public AivMapperInfo(
            int value,
            string name,
            AivItemCategory category,
            bool isKnown,
            int? footprintSize)
        {
            Value = value;
            Name = name;
            Category = category;
            IsKnown = isKnown;
            FootprintSize = footprintSize;
        }

        public int Value { get; }
        public string Name { get; }
        public AivItemCategory Category { get; }
        public bool IsKnown { get; }
        public int? FootprintSize { get; }
    }

    internal sealed class AivBuildFrame
    {
        public AivBuildFrame(
            int buildIndex,
            AivMapperInfo mapper,
            IReadOnlyList<AivGridPoint> positions)
        {
            BuildIndex = buildIndex;
            Mapper = mapper;
            Positions = positions;
        }

        public int BuildIndex { get; }
        public AivMapperInfo Mapper { get; }
        public IReadOnlyList<AivGridPoint> Positions { get; }
    }

    internal sealed class AivBlueprint
    {
        public AivBlueprint(
            IReadOnlyList<AivBuildFrame> frames,
            AivGridPoint? keepAnchor)
        {
            Frames = frames;
            KeepAnchor = keepAnchor;
        }

        public IReadOnlyList<AivBuildFrame> Frames { get; }
        public AivGridPoint? KeepAnchor { get; }
    }

    internal sealed class AivParseResult
    {
        public AivParseResult(
            AivBlueprint blueprint,
            IReadOnlyList<AivDiagnostic> diagnostics,
            int errorCount,
            int warningCount)
        {
            Blueprint = blueprint;
            Diagnostics = diagnostics;
            ErrorCount = errorCount;
            WarningCount = warningCount;
        }

        public AivBlueprint Blueprint { get; }
        public IReadOnlyList<AivDiagnostic> Diagnostics { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public bool IsValid => ErrorCount == 0;
    }

    internal static class AivGridTransform
    {
        public static AivGridDelta GetAnchorDelta(
            AivGridPoint point,
            AivGridPoint keepAnchor,
            AivRotation rotation)
        {
            if (rotation != AivRotation.Degrees0)
                throw new ArgumentOutOfRangeException(nameof(rotation));

            return new AivGridDelta(
                point.Row - keepAnchor.Row,
                point.Column - keepAnchor.Column);
        }
    }

    internal static class AivMapperCatalog
    {
        private static readonly IReadOnlyDictionary<int, AivMapperInfo> Mappers =
            CreateMappers();

        public static AivMapperInfo Resolve(int value)
        {
            if (Mappers.TryGetValue(value, out AivMapperInfo mapper))
                return mapper;

            return new AivMapperInfo(
                value,
                $"UNKNOWN_MAPPER_{value}",
                AivItemCategory.Unknown,
                false,
                null);
        }

        private static IReadOnlyDictionary<int, AivMapperInfo> CreateMappers()
        {
            var result = new Dictionary<int, AivMapperInfo>();

            Add(result, 25, "MAPPER_WALL", AivItemCategory.HighWallPath, 1);
            Add(result, 26, "MAPPER_CRENAL", AivItemCategory.CrenelPath, 1);
            Add(result, 35, "MAPPER_CRENAL2", AivItemCategory.CrenelPath, 1);
            Add(result, 46, "MAPPER_WOODWALL", AivItemCategory.LowWallPath, 1);
            Add(result, 50, "MAPPER_FLETCHER", AivItemCategory.Building, 4);
            Add(result, 52, "MAPPER_STORES", AivItemCategory.Building, 5);
            Add(result, 54, "MAPPER_HOVEL", AivItemCategory.Building, 4);
            Add(result, 60, "MAPPER_KEEP1", AivItemCategory.Keep, 7);
            Add(result, 61, "MAPPER_KEEP2", AivItemCategory.Keep, 7);
            Add(result, 62, "MAPPER_KEEP3", AivItemCategory.Keep, 11);
            Add(result, 63, "MAPPER_KEEP4", AivItemCategory.Keep, null);
            Add(result, 64, "MAPPER_KEEP5", AivItemCategory.Keep, null);
            Add(result, 65, "MAPPER_STABLES", AivItemCategory.Building, 6);
            Add(result, 74, "MAPPER_MILL", AivItemCategory.Building, 3);
            Add(result, 75, "MAPPER_BAKER", AivItemCategory.Building, 4);
            Add(result, 76, "MAPPER_BREWER", AivItemCategory.Building, 4);
            Add(result, 77, "MAPPER_TRADEPOST", AivItemCategory.Building, 5);
            Add(result, 79, "MAPPER_BEDOUIN_STOCKADE", AivItemCategory.Building, 5);
            Add(result, 80, "MAPPER_GRANARY", AivItemCategory.Building, 4);
            Add(result, 81, "MAPPER_ARMOURY", AivItemCategory.Building, 4);
            Add(result, 82, "MAPPER_POLETURNER", AivItemCategory.Building, 4);
            Add(result, 83, "MAPPER_BLACKSMITH", AivItemCategory.Building, 4);
            Add(result, 84, "MAPPER_ARMOURER", AivItemCategory.Building, 4);
            Add(result, 85, "MAPPER_TANNER", AivItemCategory.Building, 4);
            Add(result, 86, "MAPPER_BARRACKS_WOOD", AivItemCategory.Building, 5);
            Add(result, 87, "MAPPER_BARRACKS_STONE", AivItemCategory.Building, 5);
            Add(result, 88, "MAPPER_ENGINEERS_GUILD", AivItemCategory.Building, 5);
            Add(result, 89, "MAPPER_TUNNELERS_GUILD", AivItemCategory.Building, 5);
            Add(result, 92, "MAPPER_INN", AivItemCategory.Building, 5);
            Add(result, 93, "MAPPER_HEALER", AivItemCategory.Building, 6);
            Add(result, 95, "MAPPER_CHURCH1", AivItemCategory.Building, 6);
            Add(result, 96, "MAPPER_CHURCH2", AivItemCategory.Building, 9);
            Add(result, 97, "MAPPER_CHURCH3", AivItemCategory.Building, 13);
            Add(result, 98, "MAPPER_KILLING_PIT", AivItemCategory.Trap, 1);
            Add(result, 99, "MAPPER_PITCH_DITCH", AivItemCategory.PitchDitchPath, 1);
            Add(result, 105, "MAPPER_DRAWBRIDGE", AivItemCategory.Building, 5);
            Add(result, 106, "MAPPER_MOAT", AivItemCategory.MoatPath, 1);
            Add(result, 110, "MAPPER_TOWER1", AivItemCategory.Building, 3);
            Add(result, 111, "MAPPER_TOWER2", AivItemCategory.Building, 4);
            Add(result, 112, "MAPPER_TOWER3", AivItemCategory.Building, 5);
            Add(result, 113, "MAPPER_TOWER4", AivItemCategory.Building, 6);
            Add(result, 114, "MAPPER_TOWER5", AivItemCategory.Building, 6);
            Add(result, 144, "MAPPER_GATE_STONE1A", AivItemCategory.Building, 5);
            Add(result, 145, "MAPPER_GATE_STONE1B", AivItemCategory.Building, 5);
            Add(result, 146, "MAPPER_GATE_STONE2A", AivItemCategory.Building, 7);
            Add(result, 147, "MAPPER_GATE_STONE2B", AivItemCategory.Building, 7);
            Add(result, 160, "MAPPER_GARDEN1", AivItemCategory.Building, 2);
            Add(result, 166, "MAPPER_GARDEN7", AivItemCategory.Building, 3);
            Add(result, 169, "MAPPER_GARDEN10", AivItemCategory.Building, 4);
            Add(result, 175, "MAPPER_MAYPOLE", AivItemCategory.Building, 3);
            Add(result, 176, "MAPPER_GALLOWS", AivItemCategory.Building, 2);
            Add(result, 177, "MAPPER_STOCKS", AivItemCategory.Building, 3);
            Add(result, 180, "MAPPER_OIL_SMELTER", AivItemCategory.Building, 4);
            Add(result, 181, "MAPPER_STAIR1", AivItemCategory.Stair, 1);
            Add(result, 182, "MAPPER_STAIR2", AivItemCategory.Stair, 1);
            Add(result, 183, "MAPPER_STAIR3", AivItemCategory.Stair, 1);
            Add(result, 184, "MAPPER_STAIR4", AivItemCategory.Stair, 1);
            Add(result, 185, "MAPPER_STAIR5", AivItemCategory.Stair, 1);
            Add(result, 186, "MAPPER_STAIR6", AivItemCategory.Stair, 1);
            Add(result, 301, "MAPPER_CESS_PIT1", AivItemCategory.Building, 5);
            Add(result, 305, "MAPPER_BURNING_STAKE", AivItemCategory.Building, 3);
            Add(result, 306, "MAPPER_GIBBET", AivItemCategory.Building, 2);
            Add(result, 307, "MAPPER_DUNGEON", AivItemCategory.Building, 5);
            Add(result, 308, "MAPPER_RACK_STRETCHING", AivItemCategory.Building, 3);
            Add(result, 310, "MAPPER_CHOPPING_BLOCK", AivItemCategory.Building, 3);
            Add(result, 312, "MAPPER_DOG_CAGE", AivItemCategory.Building, 3);
            Add(result, 313, "MAPPER_STATUE1", AivItemCategory.Building, 2);
            Add(result, 318, "MAPPER_SHRINE1", AivItemCategory.Building, 2);
            Add(result, 324, "MAPPER_DANCING_BEAR", AivItemCategory.Building, 5);
            Add(result, 330, "MAPPER_WELL", AivItemCategory.Building, 3);
            Add(result, 342, "MAPPER_WATERPOT", AivItemCategory.Building, 4);

            return result;
        }

        private static void Add(
            IDictionary<int, AivMapperInfo> result,
            int value,
            string name,
            AivItemCategory category,
            int? footprintSize)
        {
            result.Add(
                value,
                new AivMapperInfo(
                    value,
                    name,
                    category,
                    true,
                    footprintSize));
        }
    }

    internal sealed class AivBlueprintParser
    {
        public AivParseResult Parse(AivJsonDocument document, string sourceName)
        {
            var diagnostics = new List<AivDiagnostic>();
            var frames = new List<AivBuildFrame>();
            var keepPositions = new List<AivGridPoint>();

            if (document == null)
            {
                AddError(diagnostics, "AIV001", "The AIV document is null.", "$");
                return CreateResult(frames, null, diagnostics);
            }

            if (document.frames == null || document.frames.Count == 0)
            {
                AddError(
                    diagnostics,
                    "AIV003",
                    "The required frames array is missing or empty.",
                    "$.frames");
                return CreateResult(frames, null, diagnostics);
            }

            for (int frameIndex = 0; frameIndex < document.frames.Count; frameIndex++)
            {
                AivJsonFrame source = document.frames[frameIndex];
                string location = $"$.frames[{frameIndex}]";
                if (source == null)
                {
                    AddError(diagnostics, "AIV008", "Frame entry is null.", location);
                    continue;
                }

                AivMapperInfo mapper = AivMapperCatalog.Resolve(source.itemType);
                if (!mapper.IsKnown)
                {
                    AddWarning(
                        diagnostics,
                        "AIV010",
                        $"Unknown mapper itemType={source.itemType}; it will be skipped.",
                        location + ".itemType");
                }

                var positions = new List<AivGridPoint>();
                if (source.tilePositionOfsets == null ||
                    source.tilePositionOfsets.Count == 0)
                {
                    AddError(
                        diagnostics,
                        "AIV012",
                        "A frame must contain at least one tile offset.",
                        location + ".tilePositionOfsets");
                }
                else
                {
                    for (int index = 0; index < source.tilePositionOfsets.Count; index++)
                    {
                        int offset = source.tilePositionOfsets[index];
                        if (offset < 0 ||
                            offset >= AivGridPoint.GridSize * AivGridPoint.GridSize)
                        {
                            AddError(
                                diagnostics,
                                "AIV014",
                                $"Tile offset {offset} is outside the 100x100 AIV grid.",
                                $"{location}.tilePositionOfsets[{index}]");
                            continue;
                        }

                        AivGridPoint point = new AivGridPoint(offset);
                        positions.Add(point);
                        if (mapper.Category == AivItemCategory.Keep)
                            keepPositions.Add(point);
                    }
                }

                frames.Add(new AivBuildFrame(frameIndex, mapper, positions));
            }

            AivGridPoint? keepAnchor = null;
            if (keepPositions.Count != 1)
            {
                AddError(
                    diagnostics,
                    "AIV020",
                    $"Expected exactly one keep placement, but found {keepPositions.Count}.",
                    "$.frames");
            }
            else
            {
                keepAnchor = keepPositions[0];
            }

            return CreateResult(frames, keepAnchor, diagnostics);
        }

        private static AivParseResult CreateResult(
            IReadOnlyList<AivBuildFrame> frames,
            AivGridPoint? keepAnchor,
            IReadOnlyList<AivDiagnostic> diagnostics)
        {
            int errors = 0;
            int warnings = 0;
            foreach (AivDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Severity == AivDiagnosticSeverity.Error)
                    errors++;
                else
                    warnings++;
            }

            return new AivParseResult(
                new AivBlueprint(frames, keepAnchor),
                diagnostics,
                errors,
                warnings);
        }

        private static void AddError(
            ICollection<AivDiagnostic> diagnostics,
            string code,
            string message,
            string location)
        {
            diagnostics.Add(
                new AivDiagnostic(
                    AivDiagnosticSeverity.Error,
                    code,
                    message,
                    location));
        }

        private static void AddWarning(
            ICollection<AivDiagnostic> diagnostics,
            string code,
            string message,
            string location)
        {
            diagnostics.Add(
                new AivDiagnostic(
                    AivDiagnosticSeverity.Warning,
                    code,
                    message,
                    location));
        }
    }
}
