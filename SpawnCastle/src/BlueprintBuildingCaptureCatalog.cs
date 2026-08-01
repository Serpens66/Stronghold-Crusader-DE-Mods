#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SpawnCastle
{
    internal enum BlueprintCaptureSkin
    {
        Generic,
        European,
        Islamic
    }

    internal enum BlueprintCaptureView
    {
        Default,
        ReservationDefault,
        ReservationFront,
        ReservationRear,
        PlacedDefault,
        StairNorth,
        StairSouth,
        DrawbridgeFront,
        DrawbridgeRear
    }

    internal enum BlueprintStairDirection
    {
        NotApplicable,
        Unknown,
        North,
        South
    }

    internal readonly struct BlueprintCaptureRequest
    {
        public BlueprintCaptureRequest(
            string mapperName,
            BlueprintCaptureSkin skin,
            BlueprintCaptureView view,
            bool flipHorizontally)
        {
            MapperName = mapperName ?? throw new ArgumentNullException(nameof(mapperName));
            Skin = skin;
            View = view;
            FlipHorizontally = flipHorizontally;
        }

        public string MapperName { get; }

        public BlueprintCaptureSkin Skin { get; }

        public BlueprintCaptureView View { get; }

        public bool FlipHorizontally { get; }

        public string Key => BlueprintBuildingCaptureCatalog.BuildKey(
            MapperName,
            Skin,
            View);
    }

    internal sealed class BlueprintCaptureManifestEntry
    {
        public int FormatVersion { get; set; }

        public int MapperValue { get; set; }

        public string MapperName { get; set; } = string.Empty;

        public BlueprintCaptureSkin Skin { get; set; }

        public BlueprintCaptureView View { get; set; }

        public string PngFile { get; set; } = string.Empty;

        public float PivotX { get; set; }

        public float PivotY { get; set; }

        public float PixelsPerUnit { get; set; }

        public int AlphaX { get; set; }

        public int AlphaY { get; set; }

        public int AlphaWidth { get; set; }

        public int AlphaHeight { get; set; }

        public string FragmentSignature { get; set; } = string.Empty;

        public string Key => BlueprintBuildingCaptureCatalog.BuildKey(
            MapperName,
            Skin,
            View);
    }

    internal static class BlueprintBuildingCaptureCatalog
    {
        public const int CurrentFormatVersion = 2;

        public const float VanillaPixelsPerUnit = 64f;

        public const string ManifestFileName = "BlueprintImages.tsv";

        private static readonly HashSet<string> Churches =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAPPER_CHURCH1",
                "MAPPER_CHURCH2",
                "MAPPER_CHURCH3"
            };

        private static readonly HashSet<string> AxisViews =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAPPER_ENGINEERS_GUILD",
                "MAPPER_TUNNELERS_GUILD",
                "MAPPER_OIL_SMELTER"
            };

        private static readonly HashSet<string> GroundOnlyMappers =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAPPER_KILLING_PIT",
                "MAPPER_PITCH_DITCH",
                "MAPPER_MOAT"
            };

        public static bool RequiresCapturedImage(string mapperName)
        {
            return !string.IsNullOrWhiteSpace(mapperName) &&
                !GroundOnlyMappers.Contains(mapperName);
        }

        public static bool RequiresPlacedCapture(string mapperName)
        {
            return mapperName == "MAPPER_WALL" ||
                mapperName == "MAPPER_WOODWALL" ||
                mapperName == "MAPPER_CRENAL" ||
                mapperName == "MAPPER_CRENAL2" ||
                mapperName == "MAPPER_STAIR" ||
                IsStairMapper(mapperName);
        }

        public static BlueprintCaptureRequest ResolveRequest(
            string mapperName,
            bool islamicChurchSkin,
            int cameraQuarter,
            BlueprintDrawbridgePosition drawbridgePosition,
            BlueprintStairDirection stairDirection =
                BlueprintStairDirection.NotApplicable,
            bool stairFlipHorizontally = false)
        {
            if (string.IsNullOrWhiteSpace(mapperName))
                throw new ArgumentException("A mapper name is required.", nameof(mapperName));

            int quarter = ((cameraQuarter % 4) + 4) % 4;
            BlueprintCaptureSkin skin = Churches.Contains(mapperName)
                ? (islamicChurchSkin
                    ? BlueprintCaptureSkin.Islamic
                    : BlueprintCaptureSkin.European)
                : BlueprintCaptureSkin.Generic;

            if (string.Equals(mapperName, "MAPPER_CRENAL", StringComparison.Ordinal) ||
                string.Equals(mapperName, "MAPPER_CRENAL2", StringComparison.Ordinal))
            {
                // Each placed capture includes the cap plus its fixed wall body:
                // normal crenals use Wall, Crenal2 uses the small Woodwall.
                return new BlueprintCaptureRequest(
                    mapperName,
                    skin,
                    BlueprintCaptureView.PlacedDefault,
                    false);
            }

            if (IsStairMapper(mapperName))
            {
                // A staircase may contain any number of cells. Its individual
                // pieces share one symbol; the changing wall below them carries
                // the actual height, so only the visible rise direction matters.
                return new BlueprintCaptureRequest(
                    "MAPPER_STAIR",
                    skin,
                    stairDirection == BlueprintStairDirection.South
                        ? BlueprintCaptureView.StairSouth
                        : BlueprintCaptureView.StairNorth,
                    stairFlipHorizontally);
            }

            if (string.Equals(mapperName, "MAPPER_DRAWBRIDGE", StringComparison.Ordinal))
            {
                bool rear = drawbridgePosition == BlueprintDrawbridgePosition.TopLeft ||
                    drawbridgePosition == BlueprintDrawbridgePosition.TopRight;
                // The bundled rear canonical image is the former TopRight
                // asset; only TopLeft is derived by mirroring it.
                bool flip = drawbridgePosition == BlueprintDrawbridgePosition.BottomRight ||
                    drawbridgePosition == BlueprintDrawbridgePosition.TopLeft;
                return new BlueprintCaptureRequest(
                    mapperName,
                    skin,
                    rear
                        ? BlueprintCaptureView.DrawbridgeRear
                        : BlueprintCaptureView.DrawbridgeFront,
                    flip);
            }

            if (TryResolveGateCanonical(mapperName, out string canonicalGate, out bool gateFlip))
            {
                return new BlueprintCaptureRequest(
                    canonicalGate,
                    skin,
                    BlueprintCaptureView.Default,
                    gateFlip);
            }

            if (AxisViews.Contains(mapperName))
            {
                if (string.Equals(
                        mapperName,
                        "MAPPER_TUNNELERS_GUILD",
                        StringComparison.Ordinal))
                {
                    // The retained Tunneler captures use the opposite pair of
                    // canonical faces. Remap them to the proven Engineers'
                    // Guild screen directions without resampling the artwork.
                    return new BlueprintCaptureRequest(
                        mapperName,
                        skin,
                        quarter < 2
                            ? BlueprintCaptureView.ReservationRear
                            : BlueprintCaptureView.ReservationFront,
                        quarter == 0 || quarter == 2);
                }

                // Consecutive camera quarters are mirrored sides of the same
                // face. The opposite half-turn exposes the rear/north yard.
                bool rear = quarter >= 2;
                return new BlueprintCaptureRequest(
                    mapperName,
                    skin,
                    rear
                        ? BlueprintCaptureView.ReservationRear
                        : BlueprintCaptureView.ReservationFront,
                    quarter == 1 || quarter == 3);
            }

            if (BlueprintBuildingIconCatalog.HasReservedPlacementArea(mapperName))
            {
                return new BlueprintCaptureRequest(
                    mapperName,
                    skin,
                    BlueprintCaptureView.ReservationDefault,
                    false);
            }

            return new BlueprintCaptureRequest(
                mapperName,
                skin,
                BlueprintCaptureView.Default,
                false);
        }

        public static string BuildKey(
            string mapperName,
            BlueprintCaptureSkin skin,
            BlueprintCaptureView view)
        {
            return mapperName + "|" + skin + "|" + view;
        }

        public static bool IsStairMapper(string mapperName)
        {
            const string prefix = "MAPPER_STAIR";
            if (string.IsNullOrEmpty(mapperName) ||
                !mapperName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            // Do not impose the game's formerly observed 1..6 range here: all
            // numbered stair cells intentionally resolve to one shared symbol.
            return int.TryParse(
                    mapperName.Substring(prefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int stairNumber) &&
                stairNumber > 0;
        }

        public static bool IsBuildingPreviewFragment(
            string? spriteName,
            string? textureName,
            float pixelWidth,
            float pixelHeight)
        {
            if (string.IsNullOrWhiteSpace(spriteName) ||
                string.IsNullOrWhiteSpace(textureName))
            {
                return false;
            }

            // The colored 64x32 placement diamonds do not come from Vanilla's
            // tile atlas. Keep even 64x32 tile-atlas slices because bridges and
            // other low buildings use them as real visual fragments.
            return spriteName!.StartsWith("tile_", StringComparison.OrdinalIgnoreCase) &&
                textureName!.IndexOf("Tile", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static float CalculateNormalizedPivot(
            float groundCoordinate,
            float minimumCoordinate,
            int pixelCount,
            float pixelsPerUnit)
        {
            if (pixelCount <= 0 || pixelsPerUnit <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pixelCount));

            return (groundCoordinate - minimumCoordinate) *
                pixelsPerUnit / pixelCount;
        }

        public static IReadOnlyList<BlueprintCaptureManifestEntry> ParseManifest(
            IEnumerable<string> lines,
            out IReadOnlyList<string> errors)
        {
            var entries = new List<BlueprintCaptureManifestEntry>();
            var problems = new List<string>();
            int lineNumber = 0;
            foreach (string line in lines)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split('\t');
                if (parts.Length != 14 ||
                    !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapperValue) ||
                    !Enum.TryParse(parts[3], false, out BlueprintCaptureSkin skin) ||
                    !Enum.TryParse(parts[4], false, out BlueprintCaptureView view) ||
                    !float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float pivotX) ||
                    !float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float pivotY) ||
                    !float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float ppu) ||
                    !int.TryParse(parts[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out int alphaX) ||
                    !int.TryParse(parts[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out int alphaY) ||
                    !int.TryParse(parts[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out int alphaWidth) ||
                    !int.TryParse(parts[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out int alphaHeight))
                {
                    problems.Add($"Line {lineNumber}: invalid column data.");
                    continue;
                }

                var entry = new BlueprintCaptureManifestEntry
                {
                    FormatVersion = version,
                    MapperValue = mapperValue,
                    MapperName = parts[2],
                    Skin = skin,
                    View = view,
                    PngFile = parts[5],
                    PivotX = pivotX,
                    PivotY = pivotY,
                    PixelsPerUnit = ppu,
                    AlphaX = alphaX,
                    AlphaY = alphaY,
                    AlphaWidth = alphaWidth,
                    AlphaHeight = alphaHeight,
                    FragmentSignature = parts[13]
                };
                string? validationError = ValidateEntry(entry);
                if (validationError != null)
                    problems.Add($"Line {lineNumber}: {validationError}");
                else
                    entries.Add(entry);
            }

            errors = problems;
            return entries;
        }

        public static string SerializeManifest(
            IEnumerable<BlueprintCaptureManifestEntry> entries)
        {
            var output = new StringBuilder();
            output.Append("# formatVersion\tmapperValue\tmapperName\tskin\tview\tpngFile\tpivotX\tpivotY\tppu\talphaX\talphaY\talphaWidth\talphaHeight\tfragmentSignature\r\n");
            foreach (BlueprintCaptureManifestEntry entry in entries
                .OrderBy(value => value.MapperValue)
                .ThenBy(value => value.Skin)
                .ThenBy(value => value.View))
            {
                output.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6:R}\t{7:R}\t{8:R}\t{9}\t{10}\t{11}\t{12}\t{13}\r\n",
                    entry.FormatVersion,
                    entry.MapperValue,
                    entry.MapperName,
                    entry.Skin,
                    entry.View,
                    entry.PngFile,
                    entry.PivotX,
                    entry.PivotY,
                    entry.PixelsPerUnit,
                    entry.AlphaX,
                    entry.AlphaY,
                    entry.AlphaWidth,
                    entry.AlphaHeight,
                    entry.FragmentSignature);
            }

            return output.ToString();
        }

        public static string? ValidateEntry(BlueprintCaptureManifestEntry entry)
        {
            if (entry.FormatVersion != CurrentFormatVersion)
                return $"unsupported format version {entry.FormatVersion}.";
            if (entry.MapperValue < 0 || string.IsNullOrWhiteSpace(entry.MapperName))
                return "mapper is missing.";
            if (string.IsNullOrWhiteSpace(entry.PngFile) ||
                Path.IsPathRooted(entry.PngFile) ||
                entry.PngFile.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                return "PNG path is unsafe or missing.";
            }
            if (entry.PivotX < 0f || entry.PivotX > 1f ||
                entry.PivotY < 0f || entry.PivotY > 1f)
            {
                return "pivot is outside the sprite rectangle.";
            }
            if (Math.Abs(entry.PixelsPerUnit - VanillaPixelsPerUnit) > 0.001f)
                return "PPU must be 64.";
            if (entry.AlphaX < 0 || entry.AlphaY < 0 ||
                entry.AlphaWidth <= 0 || entry.AlphaHeight <= 0)
            {
                return "alpha bounds are invalid.";
            }
            if (string.IsNullOrWhiteSpace(entry.FragmentSignature))
                return "fragment signature is missing.";
            return null;
        }

        private static bool TryResolveGateCanonical(
            string mapperName,
            out string canonicalMapper,
            out bool flip)
        {
            switch (mapperName)
            {
                case "MAPPER_GATE_STONE1A":
                    canonicalMapper = mapperName;
                    flip = false;
                    return true;
                case "MAPPER_GATE_STONE1B":
                    canonicalMapper = "MAPPER_GATE_STONE1A";
                    flip = true;
                    return true;
                case "MAPPER_GATE_STONE2A":
                    canonicalMapper = mapperName;
                    flip = false;
                    return true;
                case "MAPPER_GATE_STONE2B":
                    canonicalMapper = "MAPPER_GATE_STONE2A";
                    flip = true;
                    return true;
                default:
                    canonicalMapper = mapperName;
                    flip = false;
                    return false;
            }
        }
    }
}
