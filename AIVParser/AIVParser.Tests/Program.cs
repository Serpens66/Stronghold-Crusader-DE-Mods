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
            ("Compute rotated keep deltas", TestAnchorDelta),
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

            XDocument svg = XDocument.Load(svgPath);
            XNamespace ns = "http://www.w3.org/2000/svg";
            int buildCells = svg.Descendants(ns + "rect")
                .Count(element => element.Attribute("data-frame") != null);
            AssertEqual(4, buildCells);
            Assert(
                svg.Descendants(ns + "title").Any(),
                "SVG placement tooltips are missing.");
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
