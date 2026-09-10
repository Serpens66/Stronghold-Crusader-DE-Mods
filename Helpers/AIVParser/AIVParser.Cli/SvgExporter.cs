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

        AddPatterns(root);
        root.Add(new XElement(
            Svg + "style",
            """
            text { font-family: Consolas, "Segoe UI", sans-serif; fill: #dce7f3; }
            .background { fill: #111820; }
            .grid-bg { fill: #435735; stroke: #9daf91; stroke-width: 1; }
            .grid-minor { stroke: #637556; stroke-width: 0.25; opacity: 0.5; }
            .grid-major { stroke: #a5b398; stroke-width: 0.7; opacity: 0.72; }
            .building { fill: #78a8d8; stroke: #eef7ff; }
            .housing, .water { fill: #fff72f; stroke: #171717; }
            .food { fill: #fffcc1; stroke: #171717; }
            .industry { fill: #c3c3c3; stroke: #171717; }
            .storage { fill: #f5f5f5; stroke: #171717; }
            .military { fill: #999999; stroke: #171717; }
            .defense, .keep { fill: #414141; stroke: #111111; }
            .civic { fill: #d6d2b8; stroke: #171717; }
            .fear-positive { fill: #8fc38d; stroke: #244b2a; }
            .fear-negative { fill: #bb765e; stroke: #512a20; }
            .blocked-area { fill: url(#blocked-area-pattern); stroke: #4b6648; stroke-width: 0.55; }
            .high-wall { fill: #464646; stroke: #111111; }
            .low-wall { fill: #806142; stroke: #251b12; }
            .crenel { fill: #6a6a6a; stroke: #111111; }
            .stair { fill: url(#stair-pattern); stroke: #555555; }
            .pitch { fill: #050505; stroke: #292929; }
            .moat { fill: url(#moat-pattern); stroke: #b8efff; }
            .trap { fill: #9d4fc4; stroke: #f4cbff; }
            .unknown { fill: #ff38d1; stroke: #ffffff; }
            .unit { fill: #c76cff; stroke: #ffffff; stroke-width: 0.8; }
            .brazier { fill: #ff7b25; stroke: #ffe0a8; stroke-width: 0.8; }
            .flag { fill: #58cf68; stroke: #d4ffda; stroke-width: 0.8; }
            .cell { opacity: 0.94; stroke-width: 0.55; }
            .placement-label { font-size: 5.3px; font-weight: 700; fill: #171717; text-anchor: middle; pointer-events: none; }
            .placement-label-light { font-size: 5.3px; font-weight: 700; fill: #f6f6f6; text-anchor: middle; pointer-events: none; }
            .anchor-marker { fill: #000000; stroke: #ffffff; stroke-width: 0.45; pointer-events: none; }
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

        XElement blockedAreas = new(
            Svg + "g",
            new XAttribute("id", "additional-blocked-areas"));
        foreach (AivBuildFrame frame in blueprint.Frames)
        {
            foreach (AivGridPoint rawPoint in frame.Positions)
            {
                foreach (AivBlockedArea area in
                         AivBlockedAreaCatalog.Resolve(frame.Mapper, rawPoint, rotation))
                {
                    AivFootprint footprint = area.Footprint;
                    var rect = new XElement(
                        Svg + "rect",
                        new XAttribute("class", "blocked-area"),
                        new XAttribute("data-blocked-area", area.Name),
                        new XAttribute("data-owner-frame", frame.BuildIndex),
                        new XAttribute("data-owner-item-type", frame.RawItemType),
                        new XAttribute("data-source", area.Source),
                        new XAttribute(
                            "x",
                            GridOriginX + footprint.Minimum.Column * CellSize),
                        new XAttribute(
                            "y",
                            ToSvgCellY(footprint.Maximum.Row)),
                        new XAttribute("width", footprint.Size * CellSize),
                        new XAttribute("height", footprint.Size * CellSize));
                    rect.Add(new XElement(
                        Svg + "title",
                        $"{area.Name}; owner frame {frame.BuildIndex}; {frame.Mapper.Name}; " +
                        $"raw anchor {footprint.RawAnchor.Row},{footprint.RawAnchor.Column}; " +
                        $"size {footprint.Size}x{footprint.Size}; source {area.Source}"));
                    blockedAreas.Add(rect);
                }
            }
        }

        root.Add(blockedAreas);

        XElement placements = new(
            Svg + "g",
            new XAttribute("id", "build-placements"));
        foreach (AivBuildFrame frame in blueprint.Frames
                     .OrderBy(frame => GetLayer(frame.Mapper.Category))
                     .ThenBy(frame => frame.BuildIndex))
        {
            foreach (AivGridPoint rawPoint in frame.Positions)
            {
                int size = frame.Mapper.FootprintSize ?? 1;
                AivFootprint footprint =
                    AivGridTransform.GetFootprint(rawPoint, size, rotation);
                AivGridPoint point = footprint.RotatedAnchor;
                string cssClass = GetCssClass(frame.Mapper);
                var rect = new XElement(
                    Svg + "rect",
                    new XAttribute("class", "cell " + cssClass),
                    new XAttribute("data-frame", frame.BuildIndex),
                    new XAttribute("data-offset", rawPoint.EncodedOffset),
                    new XAttribute("data-item-type", frame.RawItemType),
                    new XAttribute("data-mapper-name", frame.Mapper.Name),
                    new XAttribute("data-footprint-size", size),
                    new XAttribute(
                        "x",
                        GridOriginX + footprint.Minimum.Column * CellSize),
                    new XAttribute(
                        "y",
                        ToSvgCellY(footprint.Maximum.Row)),
                    new XAttribute("width", size * CellSize),
                    new XAttribute("height", size * CellSize));
                rect.Add(new XElement(
                    Svg + "title",
                    $"Frame {frame.BuildIndex}; {frame.Mapper.Name} ({frame.RawItemType}); " +
                    $"offset {rawPoint.EncodedOffset}; row {rawPoint.Row}; column {rawPoint.Column}; " +
                    $"footprint {size}x{size}; rotated anchor {point.Row},{point.Column}; " +
                    $"pause {frame.ShouldPause}"));
                placements.Add(rect);

                if (size > 1)
                {
                    // This is only visual text alignment, not a parsed placement value.
                    double labelX = GridOriginX +
                        (footprint.Minimum.Column + footprint.Maximum.Column + 1) *
                        CellSize / 2.0;
                    double labelY = ToSvgCellY(footprint.Maximum.Row) +
                        size * CellSize / 2.0;
                    placements.Add(new XElement(
                        Svg + "circle",
                        new XAttribute("class", "anchor-marker"),
                        new XAttribute("cx", ToSvgCenterX(point.Column)),
                        new XAttribute("cy", ToSvgCenterY(point.Row)),
                        new XAttribute("r", 1.45)));

                    if (frame.Mapper.Category == AivItemCategory.Building ||
                        frame.Mapper.Category == AivItemCategory.Keep)
                    {
                        placements.Add(PlacementLabel(
                            labelX,
                            labelY + 1.8,
                            frame.Mapper,
                            size));
                    }
                }
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
                new XAttribute("cx", ToSvgCenterX(point.Column)),
                new XAttribute("cy", ToSvgCenterY(point.Row)),
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

    private static void AddPatterns(XElement root)
    {
        var definitions = new XElement(Svg + "defs");
        definitions.Add(new XElement(
            Svg + "pattern",
            new XAttribute("id", "blocked-area-pattern"),
            new XAttribute("width", 12),
            new XAttribute("height", 12),
            new XAttribute("patternUnits", "userSpaceOnUse"),
            new XElement(
                Svg + "rect",
                new XAttribute("width", 12),
                new XAttribute("height", 12),
                new XAttribute("fill", "#9abd91")),
            new XElement(
                Svg + "path",
                new XAttribute("d", "M-3,3 L3,-3 M0,12 L12,0 M9,15 L15,9 M-3,9 L3,15 M0,0 L12,12 M9,-3 L15,3"),
                new XAttribute("stroke", "#38583d"),
                new XAttribute("stroke-width", 1.4))));
        definitions.Add(new XElement(
            Svg + "pattern",
            new XAttribute("id", "stair-pattern"),
            new XAttribute("width", 8),
            new XAttribute("height", 8),
            new XAttribute("patternUnits", "userSpaceOnUse"),
            new XElement(
                Svg + "rect",
                new XAttribute("width", 8),
                new XAttribute("height", 8),
                new XAttribute("fill", "#d8d8d8")),
            new XElement(
                Svg + "path",
                new XAttribute("d", "M1,7 H3 V5 H5 V3 H7 V1"),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", "#5f5f5f"),
                new XAttribute("stroke-width", 1.1))));
        definitions.Add(new XElement(
            Svg + "pattern",
            new XAttribute("id", "moat-pattern"),
            new XAttribute("width", 8),
            new XAttribute("height", 8),
            new XAttribute("patternUnits", "userSpaceOnUse"),
            new XElement(
                Svg + "rect",
                new XAttribute("width", 8),
                new XAttribute("height", 8),
                new XAttribute("fill", "#2f9fbf")),
            new XElement(
                Svg + "path",
                new XAttribute("d", "M0,2 C2,0 6,4 8,2 M0,6 C2,4 6,8 8,6"),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", "#8be5f7"),
                new XAttribute("stroke-width", 0.8))));
        root.Add(definitions);
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
                ToSvgCenterY(index) + 3,
                "axis",
                index.ToString(CultureInfo.InvariantCulture)));
        }

        root.Add(Text(
            GridOriginX + GridPixelSize / 2 - 24,
            GridOriginY + GridPixelSize + 28,
            "summary",
            "Column"));
        root.Add(Text(12, GridOriginY + GridPixelSize / 2, "summary", "Row ↑"));
    }

    private static void AddLegend(XElement root)
    {
        int x = 900;
        int y = 100;
        root.Add(Text(x, y - 24, "heading", "Legend"));

        var entries = new[]
        {
            ("keep", "Keep"),
            ("housing", "Housing / water"),
            ("food", "Food / inn"),
            ("industry", "Industry"),
            ("storage", "Storage / market"),
            ("military", "Military"),
            ("defense", "Defense"),
            ("civic", "Civic"),
            ("fear-positive", "Positive fear"),
            ("fear-negative", "Negative fear"),
            ("blocked-area", "Blocked / associated area"),
            ("building", "Other building"),
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
            ("unknown", "Unknown"),
            ("anchor-marker", "Stored AIV anchor")
        };

        for (int index = 0; index < entries.Length; index++)
        {
            int entryY = y + index * 24;
            if (entries[index].Item1 == "anchor-marker")
            {
                root.Add(new XElement(
                    Svg + "circle",
                    new XAttribute("class", "anchor-marker"),
                    new XAttribute("cx", x + 8),
                    new XAttribute("cy", entryY + 8),
                    new XAttribute("r", 5)));
            }
            else
            {
                root.Add(new XElement(
                    Svg + "rect",
                    new XAttribute("class", entries[index].Item1),
                    new XAttribute("x", x),
                    new XAttribute("y", entryY),
                    new XAttribute("width", 16),
                    new XAttribute("height", 16)));
            }
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

    private static string GetCssClass(AivMapperInfo mapper)
    {
        return mapper.Category switch
        {
            AivItemCategory.Building => mapper.VisualGroup switch
            {
                AivVisualGroup.Housing => "housing",
                AivVisualGroup.Food => "food",
                AivVisualGroup.Industry => "industry",
                AivVisualGroup.Storage => "storage",
                AivVisualGroup.Military => "military",
                AivVisualGroup.Defense => "defense",
                AivVisualGroup.Civic => "civic",
                AivVisualGroup.PositiveFear => "fear-positive",
                AivVisualGroup.NegativeFear => "fear-negative",
                AivVisualGroup.Water => "water",
                _ => "building"
            },
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

    private static bool UsesLightLabel(AivMapperInfo mapper)
    {
        return mapper.Category == AivItemCategory.Keep ||
               mapper.VisualGroup == AivVisualGroup.Defense;
    }

    private static XElement PlacementLabel(
        double x,
        double y,
        AivMapperInfo mapper,
        int size)
    {
        string value = mapper.DisplayName;
        int maximumCharacters = Math.Max(5, size * 3);
        if (value.Length > maximumCharacters)
        {
            value = value.Substring(0, maximumCharacters - 1) + "…";
        }

        var label = Text(
            x,
            y,
            UsesLightLabel(mapper)
                ? "placement-label-light"
                : "placement-label",
            value);
        label.Add(new XAttribute("data-label-for", mapper.Value));
        return label;
    }

    private static double ToSvgCenterX(double column)
    {
        return GridOriginX + column * CellSize + CellSize / 2.0;
    }

    private static double ToSvgCenterY(double row)
    {
        // The official editor treats the first coordinate as upward-growing Y.
        return GridOriginY +
               (AivGridPoint.GridSize - 1 - row) * CellSize +
               CellSize / 2.0;
    }

    private static double ToSvgCellY(int row)
    {
        return GridOriginY +
               (AivGridPoint.GridSize - 1 - row) * CellSize;
    }

}
