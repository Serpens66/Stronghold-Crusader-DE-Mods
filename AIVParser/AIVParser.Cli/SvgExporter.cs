using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AIVParser.Core;

namespace AIVParser.Cli;

public static class SvgExporter
{
    private const int CellSize = 8;
    private const int GridOriginX = 60;
    private const int GridOriginY = 80;
    private const int GridPixelSize = AivGridPoint.GridSize * CellSize;
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    public static void Write(
        string path,
        AivParseResult result,
        AivRotation rotation)
    {
        AivBlueprint blueprint = result.Blueprint;
        var root = new XElement(
            Svg + "svg",
            new XAttribute("viewBox", "0 0 1160 940"),
            new XAttribute("width", "1160"),
            new XAttribute("height", "940"),
            new XAttribute("role", "img"),
            new XAttribute("aria-label", "Stronghold Crusader DE AIV castle blueprint"));

        root.Add(new XElement(
            Svg + "style",
            """
            text { font-family: Consolas, "Segoe UI", sans-serif; fill: #dce7f3; }
            .background { fill: #111820; }
            .grid-bg { fill: #1c2731; stroke: #8aa0b4; stroke-width: 1; }
            .grid-minor { stroke: #344553; stroke-width: 0.35; }
            .grid-major { stroke: #6b7e8d; stroke-width: 0.8; }
            .building { fill: #4f83cc; stroke: #b9d6ff; }
            .keep { fill: #f4c542; stroke: #fff0a3; }
            .high-wall { fill: #a9adb2; stroke: #eff2f5; }
            .low-wall { fill: #9b6f4a; stroke: #e0ba91; }
            .crenel { fill: #d5d8dc; stroke: #ffffff; }
            .stair { fill: #e28b32; stroke: #ffd09a; }
            .pitch { fill: #a53939; stroke: #ff8b72; }
            .moat { fill: #2f9fbf; stroke: #9cecff; }
            .trap { fill: #7f4c9e; stroke: #dab4ef; }
            .unknown { fill: #ff38d1; stroke: #ffffff; }
            .unit { fill: #bd74e8; stroke: #ffffff; stroke-width: 0.8; }
            .brazier { fill: #ff7b25; stroke: #ffe0a8; stroke-width: 0.8; }
            .flag { fill: #58cf68; stroke: #d4ffda; stroke-width: 0.8; }
            .cell { opacity: 0.84; stroke-width: 0.45; }
            .heading { font-size: 20px; font-weight: 700; }
            .summary { font-size: 12px; fill: #b8c8d6; }
            .axis { font-size: 9px; fill: #91a5b6; }
            .legend { font-size: 12px; }
            """));

        root.Add(new XElement(
            Svg + "rect",
            new XAttribute("class", "background"),
            new XAttribute("x", 0),
            new XAttribute("y", 0),
            new XAttribute("width", 1160),
            new XAttribute("height", 940)));

        root.Add(Text(24, 32, "heading", Path.GetFileName(blueprint.SourceName)));
        root.Add(Text(
            24,
            54,
            "summary",
            $"Rotation {(int)rotation}° | Frames {blueprint.Frames.Count} | " +
            $"Misc {blueprint.MiscItems.Count} | Pause delay {blueprint.PauseDelayAmount} | " +
            $"Warnings {result.WarningCount}"));

        XElement grid = new(
            Svg + "g",
            new XAttribute("id", "aiv-grid"));
        grid.Add(new XElement(
            Svg + "rect",
            new XAttribute("class", "grid-bg"),
            new XAttribute("x", GridOriginX),
            new XAttribute("y", GridOriginY),
            new XAttribute("width", GridPixelSize),
            new XAttribute("height", GridPixelSize)));
        AddGridLines(grid);
        root.Add(grid);

        XElement placements = new(
            Svg + "g",
            new XAttribute("id", "build-placements"));
        foreach (AivBuildFrame frame in blueprint.Frames
                     .OrderBy(frame => GetLayer(frame.Mapper.Category))
                     .ThenBy(frame => frame.BuildIndex))
        {
            foreach (AivGridPoint rawPoint in frame.Positions)
            {
                AivGridPoint point = AivGridTransform.Rotate(rawPoint, rotation);
                var rect = new XElement(
                    Svg + "rect",
                    new XAttribute("class", "cell " + GetCssClass(frame.Mapper.Category)),
                    new XAttribute("data-frame", frame.BuildIndex),
                    new XAttribute("data-offset", rawPoint.EncodedOffset),
                    new XAttribute("x", GridOriginX + point.Column * CellSize),
                    new XAttribute("y", GridOriginY + point.Row * CellSize),
                    new XAttribute("width", CellSize),
                    new XAttribute("height", CellSize));
                rect.Add(new XElement(
                    Svg + "title",
                    $"Frame {frame.BuildIndex}; {frame.Mapper.Name} ({frame.RawItemType}); " +
                    $"offset {rawPoint.EncodedOffset}; row {rawPoint.Row}; column {rawPoint.Column}; " +
                    $"pause {frame.ShouldPause}"));
                placements.Add(rect);
            }
        }

        root.Add(placements);

        XElement miscLayer = new(
            Svg + "g",
            new XAttribute("id", "misc-placements"));
        foreach (AivMiscPlacement item in blueprint.MiscItems)
        {
            AivGridPoint point = AivGridTransform.Rotate(item.Position, rotation);
            string cssClass = item.ItemType.EngineValue == 20
                ? "brazier"
                : item.ItemType.EngineValue == 21
                    ? "flag"
                    : "unit";
            var circle = new XElement(
                Svg + "circle",
                new XAttribute("class", cssClass),
                new XAttribute("data-misc-index", item.SourceIndex),
                new XAttribute("data-offset", item.Position.EncodedOffset),
                new XAttribute("cx", GridOriginX + point.Column * CellSize + CellSize / 2.0),
                new XAttribute("cy", GridOriginY + point.Row * CellSize + CellSize / 2.0),
                new XAttribute("r", 2.7));
            circle.Add(new XElement(
                Svg + "title",
                $"Misc {item.SourceIndex}; {item.ItemType.Name}; raw {item.RawItemType}; " +
                $"engine {item.ItemType.EngineValue}; slot {item.SlotIndex}; " +
                $"offset {item.Position.EncodedOffset}; row {item.Position.Row}; column {item.Position.Column}"));
            miscLayer.Add(circle);
        }

        root.Add(miscLayer);
        AddAxes(root);
        AddLegend(root);

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            root);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace
        };
        using XmlWriter writer = XmlWriter.Create(path, settings);
        document.Save(writer);
    }

    private static void AddGridLines(XElement grid)
    {
        for (int index = 0; index <= AivGridPoint.GridSize; index++)
        {
            int position = index * CellSize;
            string cssClass = index % 10 == 0 ? "grid-major" : "grid-minor";
            grid.Add(new XElement(
                Svg + "line",
                new XAttribute("class", cssClass),
                new XAttribute("x1", GridOriginX + position),
                new XAttribute("y1", GridOriginY),
                new XAttribute("x2", GridOriginX + position),
                new XAttribute("y2", GridOriginY + GridPixelSize)));
            grid.Add(new XElement(
                Svg + "line",
                new XAttribute("class", cssClass),
                new XAttribute("x1", GridOriginX),
                new XAttribute("y1", GridOriginY + position),
                new XAttribute("x2", GridOriginX + GridPixelSize),
                new XAttribute("y2", GridOriginY + position)));
        }
    }

    private static void AddAxes(XElement root)
    {
        for (int index = 0; index < AivGridPoint.GridSize; index += 10)
        {
            root.Add(Text(
                GridOriginX + index * CellSize + 2,
                GridOriginY - 8,
                "axis",
                index.ToString(CultureInfo.InvariantCulture)));
            root.Add(Text(
                GridOriginX - 28,
                GridOriginY + index * CellSize + 9,
                "axis",
                index.ToString(CultureInfo.InvariantCulture)));
        }

        root.Add(Text(
            GridOriginX + GridPixelSize / 2 - 24,
            GridOriginY + GridPixelSize + 28,
            "summary",
            "Column"));
        root.Add(Text(12, GridOriginY + GridPixelSize / 2, "summary", "Row"));
    }

    private static void AddLegend(XElement root)
    {
        int x = 900;
        int y = 100;
        root.Add(Text(x, y - 24, "heading", "Legend"));

        var entries = new[]
        {
            ("keep", "Keep"),
            ("building", "Building"),
            ("high-wall", "High wall"),
            ("low-wall", "Low wall"),
            ("crenel", "Crenel"),
            ("stair", "Stair"),
            ("pitch", "Pitch ditch"),
            ("moat", "Moat"),
            ("trap", "Killing pit"),
            ("unit", "Unit slot"),
            ("brazier", "Brazier"),
            ("flag", "Flag"),
            ("unknown", "Unknown")
        };

        for (int index = 0; index < entries.Length; index++)
        {
            int entryY = y + index * 28;
            root.Add(new XElement(
                Svg + "rect",
                new XAttribute("class", entries[index].Item1),
                new XAttribute("x", x),
                new XAttribute("y", entryY),
                new XAttribute("width", 16),
                new XAttribute("height", 16)));
            root.Add(Text(x + 26, entryY + 13, "legend", entries[index].Item2));
        }
    }

    private static XElement Text(double x, double y, string cssClass, string value)
    {
        return new XElement(
            Svg + "text",
            new XAttribute("class", cssClass),
            new XAttribute("x", x),
            new XAttribute("y", y),
            value ?? string.Empty);
    }

    private static int GetLayer(AivItemCategory category)
    {
        return category switch
        {
            AivItemCategory.MoatPath => 1,
            AivItemCategory.PitchDitchPath => 2,
            AivItemCategory.HighWallPath => 3,
            AivItemCategory.LowWallPath => 4,
            AivItemCategory.CrenelPath => 5,
            AivItemCategory.Building => 6,
            AivItemCategory.Stair => 7,
            AivItemCategory.Trap => 8,
            AivItemCategory.Keep => 9,
            _ => 10
        };
    }

    private static string GetCssClass(AivItemCategory category)
    {
        return category switch
        {
            AivItemCategory.Building => "building",
            AivItemCategory.Keep => "keep",
            AivItemCategory.HighWallPath => "high-wall",
            AivItemCategory.LowWallPath => "low-wall",
            AivItemCategory.CrenelPath => "crenel",
            AivItemCategory.Stair => "stair",
            AivItemCategory.PitchDitchPath => "pitch",
            AivItemCategory.MoatPath => "moat",
            AivItemCategory.Trap => "trap",
            _ => "unknown"
        };
    }
}
