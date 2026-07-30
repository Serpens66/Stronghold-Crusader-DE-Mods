using System.Text.Json;
using System.Xml.Linq;
using AIVParser.Cli;
using AIVParser.Core;

namespace AIVParser.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Body)[]
        {
            ("Decode offsets", TestDecodeOffsets),
            ("Rotate all four directions", TestRotations),
            ("Resolve DE footprint sizes", TestFootprintCatalog),
            ("Rotate footprints", TestFootprintRotations),
            ("Resolve associated blocked areas", TestBlockedAreas),
            ("Compute rotated keep deltas", TestAnchorDelta),
            ("Project AIV coordinates into world tiles", TestWorldProjection),
            ("Parse build order and multi-tile paths", TestValidParse),
            ("Normalize DE misc types", TestMiscNormalization),
            ("Preserve unknown positive types", TestUnknownTypes),
            ("Accept empty misc array", TestEmptyMisc),
            ("Reject missing required lists", TestMissingLists),
            ("Reject empty and off-grid offsets", TestBadOffsets),
            ("Reject invalid misc slots", TestInvalidMiscSlot),
            ("Require exactly one keep", TestKeepCardinality),
            ("Report malformed and extended JSON", TestJsonLoader),
            ("Write semantic JSON and self-contained SVG", TestExporters)
        };

        int failures = 0;
        foreach ((string name, Action body) in tests)
        {
            try
            {
                body();
                Log("PASS", name);
            }
            catch (Exception ex)
            {
                failures++;
                Log("FAIL", $"{name}: {ex.Message}");
            }
        }

        Log(
            failures == 0 ? "PASS" : "FAIL",
            $"Summary: total={tests.Length}, passed={tests.Length - failures}, failed={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static void TestDecodeOffsets()
    {
        AssertPoint(new AivGridPoint(0), 0, 0, 0);
        AssertPoint(new AivGridPoint(5044), 50, 44, 5044);
        AssertPoint(new AivGridPoint(9890), 98, 90, 9890);
    }

    private static void TestRotations()
    {
        var source = new AivGridPoint(10, 20);
        AssertPoint(AivGridTransform.Rotate(source, AivRotation.Degrees0), 10, 20, 1020);
        AssertPoint(AivGridTransform.Rotate(source, AivRotation.Degrees90), 20, 89, 2089);
        AssertPoint(AivGridTransform.Rotate(source, AivRotation.Degrees180), 89, 79, 8979);
        AssertPoint(AivGridTransform.Rotate(source, AivRotation.Degrees270), 79, 10, 7910);
    }

    private static void TestFootprintCatalog()
    {
        AssertEqual(4, AivMapperCatalog.Resolve(50).FootprintSize);
        AssertEqual(3, AivMapperCatalog.Resolve(51).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(52).FootprintSize);
        AssertEqual(4, AivMapperCatalog.Resolve(54).FootprintSize);
        AssertEqual(2, AivMapperCatalog.Resolve(55).FootprintSize);
        AssertEqual(6, AivMapperCatalog.Resolve(56).FootprintSize);
        AssertEqual(7, AivMapperCatalog.Resolve(61).FootprintSize);
        AssertEqual(11, AivMapperCatalog.Resolve(62).FootprintSize);
        AssertEqual(9, AivMapperCatalog.Resolve(70).FootprintSize);
        AssertEqual(10, AivMapperCatalog.Resolve(72).FootprintSize);
        AssertEqual(3, AivMapperCatalog.Resolve(78).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(87).FootprintSize);
        AssertEqual(1, AivMapperCatalog.Resolve(99).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(311).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(325).FootprintSize);
        AssertEqual(6, AivMapperCatalog.Resolve(327).FootprintSize);
        AssertEqual(4, AivMapperCatalog.Resolve(342).FootprintSize);
        AssertEqual<int?>(null, AivMapperCatalog.Resolve(63).FootprintSize);
    }

    private static void TestFootprintRotations()
    {
        var anchor = new AivGridPoint(10, 20);
        AssertFootprint(
            AivGridTransform.GetFootprint(anchor, 4, AivRotation.Degrees0),
            7,
            20,
            10,
            23);
        AssertFootprint(
            AivGridTransform.GetFootprint(anchor, 4, AivRotation.Degrees90),
            20,
            89,
            23,
            92);
        AssertFootprint(
            AivGridTransform.GetFootprint(anchor, 4, AivRotation.Degrees180),
            89,
            76,
            92,
            79);
        AssertFootprint(
            AivGridTransform.GetFootprint(anchor, 4, AivRotation.Degrees270),
            76,
            7,
            79,
            10);
    }

    private static void TestAnchorDelta()
    {
        var keep = new AivGridPoint(50, 44);
        var point = new AivGridPoint(57, 41);

        AssertEqual(
            new AivGridDelta(7, -3),
            AivGridTransform.GetAnchorDelta(point, keep, AivRotation.Degrees0));
        AssertEqual(
            new AivGridDelta(-3, -7),
            AivGridTransform.GetAnchorDelta(point, keep, AivRotation.Degrees90));
        AssertEqual(
            new AivGridDelta(-7, 3),
            AivGridTransform.GetAnchorDelta(point, keep, AivRotation.Degrees180));
        AssertEqual(
            new AivGridDelta(3, 7),
            AivGridTransform.GetAnchorDelta(point, keep, AivRotation.Degrees270));
    }

    private static void TestWorldProjection()
    {
        var keep = new AivGridPoint(50, 44);
        AivWorldTile same = AivWorldTransform.Project(
            keep,
            keep,
            320,
            410,
            AivRotation.Degrees0);
        AssertEqual(320, same.X);
        AssertEqual(410, same.Y);

        AivWorldTile projected = AivWorldTransform.Project(
            new AivGridPoint(57, 41),
            keep,
            320,
            410,
            AivRotation.Degrees0);
        AssertEqual(317, projected.X);
        AssertEqual(403, projected.Y);
    }

    private static void TestBlockedAreas()
    {
        IReadOnlyList<AivBlockedArea> keepAreas =
            AivBlockedAreaCatalog.Resolve(
                AivMapperCatalog.Resolve(61),
                new AivGridPoint(50, 44),
                AivRotation.Degrees0);
        AssertEqual(1, keepAreas.Count);
        AssertEqual(AivBlockedAreaKind.Campfire, keepAreas[0].Kind);
        AssertPoint(keepAreas[0].Footprint.RawAnchor, 43, 45, 4345);
        AssertFootprint(
            keepAreas[0].Footprint,
            39,
            45,
            43,
            49);

        IReadOnlyList<AivBlockedArea> rotatedKeepAreas =
            AivBlockedAreaCatalog.Resolve(
                AivMapperCatalog.Resolve(61),
                new AivGridPoint(50, 44),
                AivRotation.Degrees90);
        AssertFootprint(
            rotatedKeepAreas[0].Footprint,
            45,
            56,
            49,
            60);

        IReadOnlyList<AivBlockedArea> barracksAreas =
            AivBlockedAreaCatalog.Resolve(
                AivMapperCatalog.Resolve(87),
                new AivGridPoint(43, 51),
                AivRotation.Degrees0);
        AssertEqual(3, barracksAreas.Count);
        AssertPoint(barracksAreas[0].Footprint.RawAnchor, 38, 51, 3851);
        AssertPoint(barracksAreas[1].Footprint.RawAnchor, 43, 56, 4356);
        AssertPoint(barracksAreas[2].Footprint.RawAnchor, 38, 56, 3856);

        AssertEqual(
            1,
            AivBlockedAreaCatalog.Resolve(
                AivMapperCatalog.Resolve(88),
                new AivGridPoint(46, 15),
                AivRotation.Degrees0).Count);
        AssertEqual(
            1,
            AivBlockedAreaCatalog.Resolve(
                AivMapperCatalog.Resolve(180),
                new AivGridPoint(54, 75),
                AivRotation.Degrees0).Count);
    }

    private static void TestValidParse()
    {
        AivJsonDocument document = CreateValidDocument();
        AivParseResult parsed = new AivBlueprintParser().Parse(document, "valid.aivjson");

        Assert(parsed.IsValid, "Expected the fixture to be valid.");
        AssertEqual(3, parsed.Blueprint.Frames.Count);
        AssertEqual(61, parsed.Blueprint.Frames[0].RawItemType);
        Assert(parsed.Blueprint.Frames[1].ShouldPause, "Pause flag was not preserved.");
        AssertEqual(2, parsed.Blueprint.Frames[1].Positions.Count);
        AssertEqual(AivItemCategory.HighWallPath, parsed.Blueprint.Frames[1].Mapper.Category);
        AssertEqual(new AivGridPoint(5044), parsed.Blueprint.KeepAnchor!.Value);
    }

    private static void TestMiscNormalization()
    {
        AivJsonDocument document = CreateValidDocument();
        document.miscItems.Add(new AivJsonMiscItem
        {
            positionOfset = 5144,
            itemType = 9023,
            number = 0
        });

        AivParseResult parsed = new AivBlueprintParser().Parse(document);
        Assert(parsed.IsValid, "Expected 9023 to be a known DE misc type.");
        AivMiscPlacement item = parsed.Blueprint.MiscItems.Single();
        AssertEqual(9023, item.RawItemType);
        AssertEqual(23, item.ItemType.EngineValue);
        AssertEqual("BEDOUIN_CAMEL_LANCER", item.ItemType.Name);
    }

    private static void TestUnknownTypes()
    {
        AivJsonDocument document = CreateValidDocument();
        document.frames.Add(new AivJsonFrame
        {
            itemType = 7777,
            tilePositionOfsets = new List<int> { 5144 },
            shouldPause = false
        });
        document.miscItems.Add(new AivJsonMiscItem
        {
            positionOfset = 5244,
            itemType = 7778,
            number = 0
        });

        AivParseResult parsed = new AivBlueprintParser().Parse(document);
        Assert(parsed.IsValid, "Unknown positive values should only warn.");
        AssertEqual(2, parsed.WarningCount);
        AssertEqual(7777, parsed.Blueprint.Frames.Last().RawItemType);
        AssertEqual(7778, parsed.Blueprint.MiscItems.Last().RawItemType);
    }

    private static void TestEmptyMisc()
    {
        AivJsonDocument document = CreateValidDocument();
        document.miscItems.Clear();
        AivParseResult parsed = new AivBlueprintParser().Parse(document);
        Assert(parsed.IsValid, "An empty misc array is valid.");
    }

    private static void TestMissingLists()
    {
        var missingFrames = new AivJsonDocument
        {
            pauseDelayAmount = 100,
            frames = null,
            miscItems = new List<AivJsonMiscItem>()
        };
        AivParseResult parsedFrames = new AivBlueprintParser().Parse(missingFrames);
        AssertHasError(parsedFrames, "AIV003");
        AssertHasError(parsedFrames, "AIV020");

        var missingMisc = CreateValidDocument();
        missingMisc.miscItems = null;
        AivParseResult parsedMisc = new AivBlueprintParser().Parse(missingMisc);
        AssertHasError(parsedMisc, "AIV030");
    }

    private static void TestBadOffsets()
    {
        AivJsonDocument empty = CreateValidDocument();
        empty.frames[1].tilePositionOfsets.Clear();
        AssertHasError(new AivBlueprintParser().Parse(empty), "AIV012");

        AivJsonDocument offGrid = CreateValidDocument();
        offGrid.frames[1].tilePositionOfsets[0] = 10000;
        AssertHasError(new AivBlueprintParser().Parse(offGrid), "AIV014");

        AivJsonDocument offGridFootprint = CreateValidDocument();
        offGridFootprint.frames.Add(new AivJsonFrame
        {
            itemType = 97,
            tilePositionOfsets = new List<int> { 9090 },
            shouldPause = false
        });
        AssertHasError(
            new AivBlueprintParser().Parse(offGridFootprint),
            "AIV016");

        AivJsonDocument offGridAssociatedArea = CreateValidDocument();
        offGridAssociatedArea.frames[0].tilePositionOfsets[0] = 744;
        AssertHasError(
            new AivBlueprintParser().Parse(offGridAssociatedArea),
            "AIV017");
    }

    private static void TestInvalidMiscSlot()
    {
        AivJsonDocument document = CreateValidDocument();
        document.miscItems.Add(new AivJsonMiscItem
        {
            positionOfset = 5144,
            itemType = 6,
            number = 10
        });
        AssertHasError(new AivBlueprintParser().Parse(document), "AIV034");
    }

    private static void TestKeepCardinality()
    {
        AivJsonDocument missing = CreateValidDocument();
        missing.frames.RemoveAt(0);
        AssertHasError(new AivBlueprintParser().Parse(missing), "AIV020");

        AivJsonDocument duplicate = CreateValidDocument();
        duplicate.frames.Add(new AivJsonFrame
        {
            itemType = 62,
            tilePositionOfsets = new List<int> { 5144 },
            shouldPause = false
        });
        AssertHasError(new AivBlueprintParser().Parse(duplicate), "AIV021");
    }

    private static void TestJsonLoader()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string malformedPath = Path.Combine(directory, "malformed.aivjson");
            File.WriteAllText(malformedPath, "{\"frames\":[");
            AivJsonLoadResult malformed = AivJsonFileLoader.Load(malformedPath);
            Assert(malformed.Document == null, "Malformed JSON unexpectedly produced a document.");
            Assert(
                malformed.Diagnostics.Any(d => d.Code == "JSON002"),
                "Malformed JSON diagnostic was not reported.");

            string extendedPath = Path.Combine(directory, "extended.aivjson");
            File.WriteAllText(
                extendedPath,
                """
                {
                  "pauseDelayAmount": 100,
                  "futureRoot": true,
                  "frames": [
                    {
                      "itemType": 61,
                      "tilePositionOfsets": [5044],
                      "shouldPause": false,
                      "futureFrame": 1
                    }
                  ],
                  "miscItems": []
                }
                """);
            AivJsonLoadResult extended = AivJsonFileLoader.Load(extendedPath);
            AivParseResult parsed = new AivBlueprintParser().Parse(
                extended.Document,
                extendedPath,
                extended.Diagnostics);
            Assert(parsed.IsValid, "Unknown JSON fields should only warn.");
            AssertEqual(2, parsed.WarningCount);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void TestExporters()
    {
        AivParseResult parsed = new AivBlueprintParser().Parse(
            CreateValidDocument(),
            "A&B Castle.aivjson");
        string directory = CreateTemporaryDirectory();
        try
        {
            string jsonPath = Path.Combine(directory, "castle.parsed.json");
            string svgPath = Path.Combine(directory, "castle.svg");
            ParsedJsonExporter.Write(jsonPath, parsed, AivRotation.Degrees90);
            SvgExporter.Write(svgPath, parsed, AivRotation.Degrees90);

            AssertCrLfOnly(jsonPath);
            AssertCrLfOnly(svgPath);

            using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(jsonPath));
            AssertEqual(
                90,
                json.RootElement.GetProperty("rotation").GetInt32());
            JsonElement firstPosition = json.RootElement
                .GetProperty("frames")[0]
                .GetProperty("positions")[0];
            Assert(firstPosition.TryGetProperty("anchorDelta", out _), "Anchor delta is missing.");
            AssertEqual(
                7,
                firstPosition.GetProperty("footprint").GetProperty("size").GetInt32());
            JsonElement exportedFootprint =
                firstPosition.GetProperty("footprint");
            Assert(
                !exportedFootprint.TryGetProperty("center", out _),
                "Footprint center should not be exported.");
            Assert(
                !exportedFootprint.TryGetProperty(
                    "centerDeltaFromKeepCenter",
                    out _),
                "Center delta should not be exported.");
            AssertEqual(
                1,
                firstPosition.GetProperty("additionalBlockedAreas")
                    .GetArrayLength());

            XDocument svg = XDocument.Load(svgPath);
            XNamespace ns = "http://www.w3.org/2000/svg";
            int buildCells = svg.Descendants(ns + "rect")
                .Count(element => element.Attribute("data-frame") != null);
            AssertEqual(4, buildCells);
            XElement keepRect = svg.Descendants(ns + "rect")
                .Single(element => (string?)element.Attribute("data-offset") == "5044");
            AssertEqual("56", (string?)keepRect.Attribute("width"));
            AssertEqual("56", (string?)keepRect.Attribute("height"));
            AssertEqual("7", (string?)keepRect.Attribute("data-footprint-size"));
            AssertEqual(
                1,
                svg.Descendants(ns + "g")
                    .Single(element =>
                        (string?)element.Attribute("id") ==
                        "additional-blocked-areas")
                    .Elements(ns + "rect")
                    .Count());
            Assert(
                svg.Descendants(ns + "text")
                    .Any(element => element.Attribute("data-label-for") != null),
                "Building labels are missing.");
            Assert(
                svg.Descendants(ns + "pattern")
                    .Any(element => (string?)element.Attribute("id") == "stair-pattern"),
                "Three-step stair pattern is missing.");
            Assert(
                !svg.Descendants(ns + "pattern")
                    .Any(element => (string?)element.Attribute("id") == "pitch-pattern"),
                "Pitch ditch should use a solid black fill.");
            string svgStyles = string.Concat(
                svg.Descendants(ns + "style").Select(element => element.Value));
            Assert(
                svgStyles.Contains(
                    ".pitch { fill: #050505;",
                    StringComparison.Ordinal),
                "Pitch ditch solid-black style is missing.");
            Assert(
                svgStyles.Contains(
                    ".anchor-marker { fill: #000000;",
                    StringComparison.Ordinal),
                "Stored AIV anchor black-circle style is missing.");
            XElement stairPattern = svg.Descendants(ns + "pattern")
                .Single(element =>
                    (string?)element.Attribute("id") == "stair-pattern");
            Assert(
                stairPattern.Descendants(ns + "path").Any(element =>
                    (string?)element.Attribute("d") ==
                    "M1,7 H3 V5 H5 V3 H7 V1"),
                "Stair pattern must contain three visible steps.");
            Assert(
                svg.Descendants(ns + "pattern")
                    .Any(element =>
                        (string?)element.Attribute("id") ==
                        "blocked-area-pattern"),
                "Blocked-area pattern is missing.");
            Assert(
                svg.Descendants(ns + "title").Any(),
                "SVG placement tooltips are missing.");
            Assert(
                !svg.Root!.DescendantsAndSelf()
                    .Attributes()
                    .Any(attribute =>
                        attribute.Name.LocalName.StartsWith(
                            "data-center",
                            StringComparison.Ordinal)),
                "SVG should not expose building centers.");
            Assert(
                !svg.Descendants()
                    .Any(element =>
                        ((string?)element.Attribute("class"))?
                            .Contains("center-marker", StringComparison.Ordinal) ==
                        true),
                "SVG should not render building center markers.");
            Assert(
                svg.Descendants(ns + "circle")
                    .Any(element =>
                        (string?)element.Attribute("class") == "anchor-marker"),
                "Stored AIV anchors should be rendered as circles.");
            Assert(
                !svg.Root!.DescendantsAndSelf()
                    .Attributes()
                    .Any(attribute => attribute.Name.LocalName == "href"),
                "SVG must not reference external resources.");
            Assert(
                svg.Descendants(ns + "text").Any(element =>
                    element.Value.Contains("A&B Castle.aivjson", StringComparison.Ordinal)),
                "SVG source name was not preserved as XML text.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void AssertCrLfOnly(string path)
    {
        string withoutCrLf = File.ReadAllText(path).Replace("\r\n", string.Empty);
        Assert(
            !withoutCrLf.Contains('\r') && !withoutCrLf.Contains('\n'),
            $"{Path.GetFileName(path)} contains non-CRLF line endings.");
    }

    private static AivJsonDocument CreateValidDocument()
    {
        return new AivJsonDocument
        {
            pauseDelayAmount = 100,
            frames = new List<AivJsonFrame>
            {
                new()
                {
                    itemType = 61,
                    tilePositionOfsets = new List<int> { 5044 },
                    shouldPause = false
                },
                new()
                {
                    itemType = 25,
                    tilePositionOfsets = new List<int> { 5045, 5046 },
                    shouldPause = true
                },
                new()
                {
                    itemType = 93,
                    tilePositionOfsets = new List<int> { 5144 },
                    shouldPause = false
                }
            },
            miscItems = new List<AivJsonMiscItem>()
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "AIVParserTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void AssertPoint(
        AivGridPoint point,
        int row,
        int column,
        int offset)
    {
        AssertEqual(row, point.Row);
        AssertEqual(column, point.Column);
        AssertEqual(offset, point.EncodedOffset);
    }

    private static void AssertFootprint(
        AivFootprint footprint,
        int minRow,
        int minColumn,
        int maxRow,
        int maxColumn)
    {
        AssertPoint(
            footprint.Minimum,
            minRow,
            minColumn,
            minRow * AivGridPoint.GridSize + minColumn);
        AssertPoint(
            footprint.Maximum,
            maxRow,
            maxColumn,
            maxRow * AivGridPoint.GridSize + maxColumn);
    }

    private static void AssertHasError(AivParseResult result, string code)
    {
        Assert(
            result.Diagnostics.Any(d =>
                d.Severity == AivDiagnosticSeverity.Error &&
                d.Code == code),
            $"Expected error {code}, got: " +
            string.Join(", ", result.Diagnostics.Select(d => d.Code)));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Expected '{expected}', but got '{actual}'.");
        }
    }

    private static void Log(string level, string message)
    {
        Console.WriteLine(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");
    }
}
