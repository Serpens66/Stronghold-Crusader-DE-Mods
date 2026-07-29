using System;
using System.Collections.Generic;
using System.Linq;

namespace AIVParser.Core
{
    public sealed class AivBlueprintParser
    {
        public AivParseResult Parse(AivJsonDocument document, string sourceName = null)
        {
            return Parse(document, sourceName, null);
        }

        public AivParseResult Parse(
            AivJsonDocument document,
            string sourceName,
            IEnumerable<AivDiagnostic> initialDiagnostics)
        {
            var diagnostics = initialDiagnostics == null
                ? new List<AivDiagnostic>()
                : new List<AivDiagnostic>(initialDiagnostics);
            var frames = new List<AivBuildFrame>();
            var miscItems = new List<AivMiscPlacement>();
            var keepPlacements = new List<AivGridPoint>();

            if (document == null)
            {
                diagnostics.Add(Error("AIV001", "The AIV document is null.", "$"));
                return new AivParseResult(
                    new AivBlueprint(sourceName, 0, frames, miscItems, null),
                    diagnostics);
            }

            if (document.pauseDelayAmount < short.MinValue ||
                document.pauseDelayAmount > short.MaxValue)
            {
                diagnostics.Add(Error(
                    "AIV002",
                    $"pauseDelayAmount={document.pauseDelayAmount} does not fit the game's Int16 raw format.",
                    "$.pauseDelayAmount"));
            }

            ParseFrames(document.frames, frames, keepPlacements, diagnostics);
            ParseMiscItems(document.miscItems, miscItems, diagnostics);

            AivGridPoint? keepAnchor = null;
            if (keepPlacements.Count == 0)
            {
                diagnostics.Add(Error(
                    "AIV020",
                    "No keep placement (MAPPER_KEEP1 through MAPPER_KEEP5) was found.",
                    "$.frames"));
            }
            else if (keepPlacements.Count > 1)
            {
                diagnostics.Add(Error(
                    "AIV021",
                    $"Expected exactly one keep placement, but found {keepPlacements.Count}.",
                    "$.frames"));
            }
            else
            {
                keepAnchor = keepPlacements[0];
            }

            return new AivParseResult(
                new AivBlueprint(
                    sourceName,
                    document.pauseDelayAmount,
                    frames,
                    miscItems,
                    keepAnchor),
                diagnostics);
        }

        private static void ParseFrames(
            IList<AivJsonFrame> sourceFrames,
            ICollection<AivBuildFrame> targetFrames,
            ICollection<AivGridPoint> keepPlacements,
            ICollection<AivDiagnostic> diagnostics)
        {
            if (sourceFrames == null)
            {
                diagnostics.Add(Error("AIV003", "Required array 'frames' is missing.", "$.frames"));
                return;
            }

            if (sourceFrames.Count == 0)
            {
                diagnostics.Add(Error("AIV004", "The 'frames' array must not be empty.", "$.frames"));
            }

            if (sourceFrames.Count > 1000)
            {
                diagnostics.Add(Warning(
                    "AIV005",
                    $"The file has {sourceFrames.Count} frames; the documented native AIV queue has 1000 slots.",
                    "$.frames"));
            }

            if (sourceFrames.Count > short.MaxValue)
            {
                diagnostics.Add(Error(
                    "AIV006",
                    $"The frame count {sourceFrames.Count} does not fit the game's Int16 raw format.",
                    "$.frames"));
            }

            int pausedCount = sourceFrames.Count(f => f != null && f.shouldPause);
            if (pausedCount > 49)
            {
                diagnostics.Add(Warning(
                    "AIV007",
                    $"The file has {pausedCount} paused frames; the game stores at most 49 plus one sentinel.",
                    "$.frames"));
            }

            for (int frameIndex = 0; frameIndex < sourceFrames.Count; frameIndex++)
            {
                AivJsonFrame source = sourceFrames[frameIndex];
                string frameLocation = $"$.frames[{frameIndex}]";
                if (source == null)
                {
                    diagnostics.Add(Error("AIV008", "Frame entry is null.", frameLocation));
                    continue;
                }

                AivMapperInfo mapper = AivMapperCatalog.Resolve(source.itemType);
                if (source.itemType <= 0)
                {
                    diagnostics.Add(Error(
                        "AIV009",
                        $"itemType={source.itemType} must be positive in DE JSON.",
                        frameLocation + ".itemType"));
                }
                else if (!mapper.IsKnown)
                {
                    diagnostics.Add(Warning(
                        "AIV010",
                        $"Unknown mapper itemType={source.itemType}; the raw value was preserved.",
                        frameLocation + ".itemType"));
                }

                var positions = new List<AivGridPoint>();
                if (source.tilePositionOfsets == null)
                {
                    diagnostics.Add(Error(
                        "AIV011",
                        "Required array 'tilePositionOfsets' is missing.",
                        frameLocation + ".tilePositionOfsets"));
                }
                else if (source.tilePositionOfsets.Count == 0)
                {
                    diagnostics.Add(Error(
                        "AIV012",
                        "A frame must contain at least one tile offset.",
                        frameLocation + ".tilePositionOfsets"));
                }
                else
                {
                    if (source.tilePositionOfsets.Count > short.MaxValue)
                    {
                        diagnostics.Add(Error(
                            "AIV013",
                            $"Tile count {source.tilePositionOfsets.Count} does not fit the game's Int16 raw format.",
                            frameLocation + ".tilePositionOfsets"));
                    }

                    for (int offsetIndex = 0;
                         offsetIndex < source.tilePositionOfsets.Count;
                         offsetIndex++)
                    {
                        int offset = source.tilePositionOfsets[offsetIndex];
                        if (offset < 0 || offset >= AivGridPoint.GridSize * AivGridPoint.GridSize)
                        {
                            diagnostics.Add(Error(
                                "AIV014",
                                $"Tile offset {offset} is outside the 100x100 AIV grid.",
                                $"{frameLocation}.tilePositionOfsets[{offsetIndex}]"));
                            continue;
                        }

                        positions.Add(new AivGridPoint(offset));
                    }
                }

                var frame = new AivBuildFrame(
                    frameIndex,
                    source.itemType,
                    mapper,
                    source.shouldPause,
                    positions);
                targetFrames.Add(frame);

                if (AivMapperCatalog.IsKeep(source.itemType))
                {
                    foreach (AivGridPoint position in positions)
                    {
                        keepPlacements.Add(position);
                    }
                }
            }
        }

        private static void ParseMiscItems(
            IList<AivJsonMiscItem> sourceItems,
            ICollection<AivMiscPlacement> targetItems,
            ICollection<AivDiagnostic> diagnostics)
        {
            if (sourceItems == null)
            {
                diagnostics.Add(Error(
                    "AIV030",
                    "Required array 'miscItems' is missing. An empty array is valid.",
                    "$.miscItems"));
                return;
            }

            var usedSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < sourceItems.Count; index++)
            {
                AivJsonMiscItem source = sourceItems[index];
                string itemLocation = $"$.miscItems[{index}]";
                if (source == null)
                {
                    diagnostics.Add(Error("AIV031", "Misc entry is null.", itemLocation));
                    continue;
                }

                AivMiscTypeInfo itemType = AivMiscTypeCatalog.Resolve(source.itemType);
                if (source.itemType <= 0)
                {
                    diagnostics.Add(Error(
                        "AIV032",
                        $"itemType={source.itemType} must be positive.",
                        itemLocation + ".itemType"));
                }
                else if (!itemType.IsKnown)
                {
                    diagnostics.Add(Warning(
                        "AIV033",
                        $"Unknown misc itemType={source.itemType} (engine value {itemType.EngineValue}); the raw value was preserved.",
                        itemLocation + ".itemType"));
                }

                if (source.number < 0 || source.number > 9)
                {
                    diagnostics.Add(Error(
                        "AIV034",
                        $"Misc slot number {source.number} is outside the native range 0..9.",
                        itemLocation + ".number"));
                }

                string slotKey = itemType.EngineValue + ":" + source.number;
                if (!usedSlots.Add(slotKey))
                {
                    diagnostics.Add(Error(
                        "AIV035",
                        $"Misc slot {source.number} for engine type {itemType.EngineValue} is duplicated.",
                        itemLocation + ".number"));
                }

                if (source.positionOfset < 0 ||
                    source.positionOfset >= AivGridPoint.GridSize * AivGridPoint.GridSize)
                {
                    diagnostics.Add(Error(
                        "AIV036",
                        $"Misc position offset {source.positionOfset} is outside the 100x100 AIV grid.",
                        itemLocation + ".positionOfset"));
                    continue;
                }

                targetItems.Add(new AivMiscPlacement(
                    index,
                    source.itemType,
                    itemType,
                    source.number,
                    new AivGridPoint(source.positionOfset)));
            }
        }

        private static AivDiagnostic Error(string code, string message, string location)
        {
            return new AivDiagnostic(AivDiagnosticSeverity.Error, code, message, location);
        }

        private static AivDiagnostic Warning(string code, string message, string location)
        {
            return new AivDiagnostic(AivDiagnosticSeverity.Warning, code, message, location);
        }
    }
}
