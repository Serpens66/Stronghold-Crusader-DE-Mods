#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SpawnCastle
{
    internal sealed class BlueprintFragmentCaptureEntry
    {
        public int FormatVersion { get; set; }
        public int MapperValue { get; set; }
        public string MapperName { get; set; } = string.Empty;
        public BlueprintCaptureSkin Skin { get; set; }
        public BlueprintCaptureView View { get; set; }
        public int CaptureRotation { get; set; }
        public bool NormalizedHorizontalFlip { get; set; }
        public int FragmentCount { get; set; }
        public int TileCount { get; set; }
        public int MinimumRow { get; set; }
        public int MaximumRow { get; set; }
        public string FragmentSignature { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public string Key => BlueprintBuildingCaptureCatalog.BuildKey(
            MapperName,
            Skin,
            View);
    }

    internal sealed class BlueprintFragmentTileEntry
    {
        public int FormatVersion { get; set; }
        public string CaptureKey { get; set; } = string.Empty;
        public int Index { get; set; }
        public Dictionary<string, string> Metadata { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    internal sealed class BlueprintFragmentImageEntry
    {
        public int FormatVersion { get; set; }
        public string CaptureKey { get; set; } = string.Empty;
        public int Index { get; set; }
        public string PngFile { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public float PivotX { get; set; }
        public float PivotY { get; set; }
        public float PixelsPerUnit { get; set; }
        public int RowOffset { get; set; }
        public float PositionOffsetX { get; set; }
        public float PositionOffsetY { get; set; }
        public float PositionOffsetZ { get; set; }
        public Dictionary<string, string> Metadata { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    internal static class BlueprintFragmentCaptureCatalog
    {
        public const int CurrentFormatVersion = 2;
        public const string CaptureManifestFileName =
            "BlueprintFragmentCaptures.tsv";
        public const string TileManifestFileName =
            "BlueprintCaptureTiles.tsv";
        public const string FragmentManifestFileName =
            "BlueprintFragments.tsv";

        private const string Header =
            "# formatVersion\tcaptureKey\tname=value fields (UTF-8, CRLF)\r\n";

        public static IReadOnlyList<BlueprintFragmentCaptureEntry>
            ParseCaptures(IEnumerable<string> lines, out IReadOnlyList<string> errors)
        {
            var result = new List<BlueprintFragmentCaptureEntry>();
            var problems = new List<string>();
            foreach (ParsedRecord record in ParseRecords(lines, problems))
            {
                try
                {
                    var entry = new BlueprintFragmentCaptureEntry
                    {
                        FormatVersion = record.Version,
                        MapperValue = ReadInt(record, "mapperValue"),
                        MapperName = Read(record, "mapperName"),
                        Skin = ReadEnum<BlueprintCaptureSkin>(record, "skin"),
                        View = ReadEnum<BlueprintCaptureView>(record, "view"),
                        CaptureRotation = ReadInt(record, "captureRotation"),
                        NormalizedHorizontalFlip =
                            ReadBool(record, "normalizedHorizontalFlip"),
                        FragmentCount = ReadInt(record, "fragmentCount"),
                        TileCount = ReadInt(record, "tileCount"),
                        MinimumRow = ReadInt(record, "minimumRow"),
                        MaximumRow = ReadInt(record, "maximumRow"),
                        FragmentSignature = Read(record, "fragmentSignature")
                    };
                    CopyMetadata(record, entry.Metadata);
                    string? error = ValidateCapture(entry, record.Key);
                    if (error == null)
                        result.Add(entry);
                    else
                        problems.Add($"Line {record.LineNumber}: {error}");
                }
                catch (Exception ex)
                {
                    problems.Add($"Line {record.LineNumber}: {ex.Message}");
                }
            }
            errors = problems;
            return result;
        }

        public static IReadOnlyList<BlueprintFragmentTileEntry>
            ParseTiles(IEnumerable<string> lines, out IReadOnlyList<string> errors)
        {
            var result = new List<BlueprintFragmentTileEntry>();
            var problems = new List<string>();
            foreach (ParsedRecord record in ParseRecords(lines, problems))
            {
                try
                {
                    var entry = new BlueprintFragmentTileEntry
                    {
                        FormatVersion = record.Version,
                        CaptureKey = record.Key,
                        Index = ReadInt(record, "index")
                    };
                    CopyMetadata(record, entry.Metadata);
                    string? error = ValidateTile(entry);
                    if (error == null)
                        result.Add(entry);
                    else
                        problems.Add($"Line {record.LineNumber}: {error}");
                }
                catch (Exception ex)
                {
                    problems.Add($"Line {record.LineNumber}: {ex.Message}");
                }
            }
            errors = problems;
            return result;
        }

        public static IReadOnlyList<BlueprintFragmentImageEntry>
            ParseFragments(IEnumerable<string> lines, out IReadOnlyList<string> errors)
        {
            var result = new List<BlueprintFragmentImageEntry>();
            var problems = new List<string>();
            foreach (ParsedRecord record in ParseRecords(lines, problems))
            {
                try
                {
                    var entry = new BlueprintFragmentImageEntry
                    {
                        FormatVersion = record.Version,
                        CaptureKey = record.Key,
                        Index = ReadInt(record, "index"),
                        PngFile = Read(record, "pngFile"),
                        Sha256 = Read(record, "sha256"),
                        Width = ReadInt(record, "width"),
                        Height = ReadInt(record, "height"),
                        PivotX = ReadFloat(record, "pivotX"),
                        PivotY = ReadFloat(record, "pivotY"),
                        PixelsPerUnit = ReadFloat(record, "ppu"),
                        RowOffset = ReadInt(record, "rowOffset"),
                        PositionOffsetX = ReadFloat(record, "positionOffsetX"),
                        PositionOffsetY = ReadFloat(record, "positionOffsetY"),
                        PositionOffsetZ = ReadFloat(record, "positionOffsetZ")
                    };
                    CopyMetadata(record, entry.Metadata);
                    string? error = ValidateFragment(entry);
                    if (error == null)
                        result.Add(entry);
                    else
                        problems.Add($"Line {record.LineNumber}: {error}");
                }
                catch (Exception ex)
                {
                    problems.Add($"Line {record.LineNumber}: {ex.Message}");
                }
            }
            errors = problems;
            return result;
        }

        public static string SerializeCaptures(
            IEnumerable<BlueprintFragmentCaptureEntry> entries)
        {
            var output = new StringBuilder(Header);
            foreach (BlueprintFragmentCaptureEntry entry in entries
                .OrderBy(value => value.MapperValue)
                .ThenBy(value => value.Skin)
                .ThenBy(value => value.View))
            {
                var fields = new Dictionary<string, string>(entry.Metadata,
                    StringComparer.Ordinal)
                {
                    ["mapperValue"] = Invariant(entry.MapperValue),
                    ["mapperName"] = entry.MapperName,
                    ["skin"] = entry.Skin.ToString(),
                    ["view"] = entry.View.ToString(),
                    ["captureRotation"] = Invariant(entry.CaptureRotation),
                    ["normalizedHorizontalFlip"] =
                        entry.NormalizedHorizontalFlip ? "true" : "false",
                    ["fragmentCount"] = Invariant(entry.FragmentCount),
                    ["tileCount"] = Invariant(entry.TileCount),
                    ["minimumRow"] = Invariant(entry.MinimumRow),
                    ["maximumRow"] = Invariant(entry.MaximumRow),
                    ["fragmentSignature"] = entry.FragmentSignature
                };
                AppendRecord(output, entry.FormatVersion, entry.Key, fields);
            }
            return output.ToString();
        }

        public static string SerializeTiles(
            IEnumerable<BlueprintFragmentTileEntry> entries)
        {
            var output = new StringBuilder(Header);
            foreach (BlueprintFragmentTileEntry entry in entries
                .OrderBy(value => value.CaptureKey, StringComparer.Ordinal)
                .ThenBy(value => value.Index))
            {
                var fields = new Dictionary<string, string>(entry.Metadata,
                    StringComparer.Ordinal)
                {
                    ["index"] = Invariant(entry.Index)
                };
                AppendRecord(output, entry.FormatVersion, entry.CaptureKey, fields);
            }
            return output.ToString();
        }

        public static string SerializeFragments(
            IEnumerable<BlueprintFragmentImageEntry> entries)
        {
            var output = new StringBuilder(Header);
            foreach (BlueprintFragmentImageEntry entry in entries
                .OrderBy(value => value.CaptureKey, StringComparer.Ordinal)
                .ThenBy(value => value.Index))
            {
                var fields = new Dictionary<string, string>(entry.Metadata,
                    StringComparer.Ordinal)
                {
                    ["index"] = Invariant(entry.Index),
                    ["pngFile"] = entry.PngFile,
                    ["sha256"] = entry.Sha256,
                    ["width"] = Invariant(entry.Width),
                    ["height"] = Invariant(entry.Height),
                    ["pivotX"] = Invariant(entry.PivotX),
                    ["pivotY"] = Invariant(entry.PivotY),
                    ["ppu"] = Invariant(entry.PixelsPerUnit),
                    ["rowOffset"] = Invariant(entry.RowOffset),
                    ["positionOffsetX"] = Invariant(entry.PositionOffsetX),
                    ["positionOffsetY"] = Invariant(entry.PositionOffsetY),
                    ["positionOffsetZ"] = Invariant(entry.PositionOffsetZ)
                };
                AppendRecord(output, entry.FormatVersion, entry.CaptureKey, fields);
            }
            return output.ToString();
        }

        public static string? ValidateCapture(
            BlueprintFragmentCaptureEntry entry,
            string? serializedKey = null)
        {
            if (entry.FormatVersion != CurrentFormatVersion)
                return $"unsupported fragment format {entry.FormatVersion}.";
            if (entry.MapperValue < 0 || string.IsNullOrWhiteSpace(entry.MapperName))
                return "mapper is missing.";
            if (!Enum.IsDefined(typeof(BlueprintCaptureSkin), entry.Skin) ||
                !Enum.IsDefined(typeof(BlueprintCaptureView), entry.View))
            {
                return "capture skin or view is invalid.";
            }
            if (entry.CaptureRotation < 0 || entry.CaptureRotation > 3)
                return "capture rotation is invalid.";
            if (!string.IsNullOrEmpty(serializedKey) &&
                !string.Equals(entry.Key, serializedKey, StringComparison.Ordinal))
            {
                return "capture key does not match mapper, skin and view.";
            }
            if (entry.FragmentCount <= 0 || entry.TileCount <= 0)
                return "capture has no fragments or ground tiles.";
            if (entry.MaximumRow < entry.MinimumRow)
                return "capture row range is invalid.";
            if (string.IsNullOrWhiteSpace(entry.FragmentSignature))
                return "fragment signature is missing.";
            return null;
        }

        public static string? ValidateTile(BlueprintFragmentTileEntry entry)
        {
            if (entry.FormatVersion != CurrentFormatVersion)
                return $"unsupported tile format {entry.FormatVersion}.";
            if (string.IsNullOrWhiteSpace(entry.CaptureKey) || entry.Index < 0)
                return "tile key or index is invalid.";
            return null;
        }

        public static string? ValidateFragment(BlueprintFragmentImageEntry entry)
        {
            if (entry.FormatVersion != CurrentFormatVersion)
                return $"unsupported fragment format {entry.FormatVersion}.";
            if (string.IsNullOrWhiteSpace(entry.CaptureKey) || entry.Index < 0)
                return "fragment key or index is invalid.";
            if (!IsSafeRelativePath(entry.PngFile))
                return "fragment PNG path is unsafe or missing.";
            if (entry.Width <= 0 || entry.Height <= 0)
                return "fragment dimensions are invalid.";
            if (!IsFinite(entry.PivotX) || !IsFinite(entry.PivotY) ||
                !IsFinite(entry.PositionOffsetX) ||
                !IsFinite(entry.PositionOffsetY) ||
                !IsFinite(entry.PositionOffsetZ))
            {
                return "fragment coordinates are not finite.";
            }
            if (entry.PivotX < 0f || entry.PivotX > 1f ||
                entry.PivotY < 0f || entry.PivotY > 1f)
            {
                return "fragment pivot is outside its image.";
            }
            if (Math.Abs(entry.PixelsPerUnit -
                    BlueprintBuildingCaptureCatalog.VanillaPixelsPerUnit) >
                0.001f)
            {
                return "fragment PPU must be 64.";
            }
            if (entry.Sha256.Length != 64 ||
                entry.Sha256.Any(character => !Uri.IsHexDigit(character)))
            {
                return "fragment SHA-256 is invalid.";
            }
            return null;
        }

        public static bool IsValidRowOffset(
            BlueprintFragmentCaptureEntry capture,
            BlueprintFragmentImageEntry fragment)
        {
            return fragment.RowOffset >= 0 &&
                fragment.RowOffset <= capture.MaximumRow - capture.MinimumRow;
        }

        public static bool IsSafeRelativePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                !Path.IsPathRooted(path) &&
                path.IndexOf("..", StringComparison.Ordinal) < 0 &&
                path.IndexOf(':') < 0;
        }

        public static int RemapDepthRow(
            int captureMinimumRow,
            int captureMaximumRow,
            int currentMinimumRow,
            int currentMaximumRow,
            int capturedRowOffset)
        {
            int capturedSpan = Math.Max(
                0,
                captureMaximumRow - captureMinimumRow);
            int currentSpan = Math.Max(
                0,
                currentMaximumRow - currentMinimumRow);
            int remappedOffset = capturedSpan == 0
                ? 0
                : (int)Math.Round(
                    capturedRowOffset * currentSpan / (double)capturedSpan,
                    MidpointRounding.AwayFromZero);
            return currentMinimumRow + remappedOffset;
        }

        public static int GetMiddleDepthRow(int minimumRow, int maximumRow)
        {
            if (maximumRow < minimumRow)
                throw new ArgumentOutOfRangeException(nameof(maximumRow));
            return minimumRow + (maximumRow - minimumRow) / 2;
        }

        private static IEnumerable<ParsedRecord> ParseRecords(
            IEnumerable<string> lines,
            ICollection<string> problems)
        {
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
                if (parts.Length < 3 ||
                    !int.TryParse(parts[0], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int version))
                {
                    problems.Add($"Line {lineNumber}: invalid record header.");
                    continue;
                }

                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                bool valid = true;
                for (int index = 2; index < parts.Length; index++)
                {
                    int equals = parts[index].IndexOf('=');
                    if (equals <= 0)
                    {
                        valid = false;
                        break;
                    }
                    string name = parts[index].Substring(0, equals);
                    string value = Unescape(parts[index].Substring(equals + 1));
                    if (fields.ContainsKey(name))
                    {
                        valid = false;
                        break;
                    }
                    fields.Add(name, value);
                }
                if (!valid)
                {
                    problems.Add($"Line {lineNumber}: invalid or duplicate field.");
                    continue;
                }
                yield return new ParsedRecord(
                    version,
                    Unescape(parts[1]),
                    fields,
                    lineNumber);
            }
        }

        private static void AppendRecord(
            StringBuilder output,
            int version,
            string key,
            IReadOnlyDictionary<string, string> fields)
        {
            output.Append(version.ToString(CultureInfo.InvariantCulture));
            output.Append('\t').Append(Escape(key));
            foreach (KeyValuePair<string, string> field in fields
                .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                output.Append('\t').Append(field.Key).Append('=')
                    .Append(Escape(field.Value ?? string.Empty));
            }
            output.Append("\r\n");
        }

        private static void CopyMetadata(
            ParsedRecord record,
            IDictionary<string, string> destination)
        {
            foreach (KeyValuePair<string, string> field in record.Fields)
                destination[field.Key] = field.Value;
        }

        private static string Read(ParsedRecord record, string name)
        {
            if (!record.Fields.TryGetValue(name, out string? value))
                throw new FormatException($"required field '{name}' is missing.");
            return value;
        }

        private static int ReadInt(ParsedRecord record, string name)
        {
            if (!int.TryParse(Read(record, name), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int value))
            {
                throw new FormatException($"field '{name}' is not an integer.");
            }
            return value;
        }

        private static float ReadFloat(ParsedRecord record, string name)
        {
            if (!float.TryParse(Read(record, name), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new FormatException($"field '{name}' is not a finite number.");
            }
            return value;
        }

        private static bool ReadBool(ParsedRecord record, string name)
        {
            if (!bool.TryParse(Read(record, name), out bool value))
                throw new FormatException($"field '{name}' is not a boolean.");
            return value;
        }

        private static T ReadEnum<T>(ParsedRecord record, string name)
            where T : struct
        {
            if (!Enum.TryParse(Read(record, name), false, out T value) ||
                !Enum.IsDefined(typeof(T), value))
                throw new FormatException($"field '{name}' is not a valid {typeof(T).Name}.");
            return value;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static string Invariant(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string Invariant(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);

        private static string Escape(string value)
        {
            return value.Replace("%", "%25")
                .Replace("\t", "%09")
                .Replace("\r", "%0D")
                .Replace("\n", "%0A")
                .Replace("=", "%3D");
        }

        private static string Unescape(string value)
        {
            return value.Replace("%3D", "=")
                .Replace("%0A", "\n")
                .Replace("%0D", "\r")
                .Replace("%09", "\t")
                .Replace("%25", "%");
        }

        private readonly struct ParsedRecord
        {
            public ParsedRecord(
                int version,
                string key,
                Dictionary<string, string> fields,
                int lineNumber)
            {
                Version = version;
                Key = key;
                Fields = fields;
                LineNumber = lineNumber;
            }

            public int Version { get; }
            public string Key { get; }
            public Dictionary<string, string> Fields { get; }
            public int LineNumber { get; }
        }
    }
}
