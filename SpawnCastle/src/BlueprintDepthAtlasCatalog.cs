using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SpawnCastle
{
    internal sealed class BlueprintDepthAtlasCaptureDefinition
    {
        public string Key { get; set; }
        public int MapperValue { get; set; }
        public string MapperName { get; set; }
        public BlueprintCaptureSkin Skin { get; set; }
        public BlueprintCaptureView View { get; set; }
        public int CaptureRotation { get; set; }
        public bool NormalizedHorizontalFlip { get; set; }
        public int MinimumRow { get; set; }
        public int MaximumRow { get; set; }
        public int FragmentCount { get; set; }
        public int PageCount { get; set; }
        public string FragmentSignature { get; set; }
        public string CaptureSource { get; set; }
        public string PlacedVisualVersion { get; set; }
        public string Directory { get; set; }
        public IReadOnlyList<BlueprintDepthAtlasPageDefinition> Pages { get; set; }
        public IReadOnlyList<BlueprintDepthAtlasFragmentDefinition> Fragments { get; set; }
    }

    internal sealed class BlueprintDepthAtlasPageDefinition
    {
        public string CaptureKey { get; set; }
        public int PageIndex { get; set; }
        public string PngFile { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Sha256 { get; set; }
    }

    internal sealed class BlueprintDepthAtlasFragmentDefinition
    {
        public string CaptureKey { get; set; }
        public int Index { get; set; }
        public int PageIndex { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RowOffset { get; set; }
        public int SortingOffset { get; set; }
        public float PositionOffsetX { get; set; }
        public float PositionOffsetY { get; set; }
        public float PositionOffsetZ { get; set; }
    }

    internal static class BlueprintDepthAtlasCatalog
    {
        public const int CurrentFormatVersion = 1;
        public const string ManifestFileName = "BlueprintDepthAtlases.tsv";
        private const string ManifestHeader =
            "# SpawnCastle Blueprint depth atlas format 1 (UTF-8, CRLF)";

        public static IReadOnlyList<BlueprintDepthAtlasCaptureDefinition> Parse(
            string directory,
            IEnumerable<string> lines,
            out IReadOnlyList<string> errors)
        {
            var problems = new List<string>();
            var captures = new Dictionary<string, BlueprintDepthAtlasCaptureDefinition>(
                StringComparer.Ordinal);
            var pages = new List<BlueprintDepthAtlasPageDefinition>();
            var fragments = new List<BlueprintDepthAtlasFragmentDefinition>();
            int lineNumber = 0;
            bool versionHeaderSeen = false;
            foreach (string line in lines)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line[0] == '#')
                {
                    if (string.Equals(line, ManifestHeader, StringComparison.Ordinal))
                        versionHeaderSeen = true;
                    continue;
                }

                string[] fields = line.Split('\t');
                try
                {
                    switch (fields[0])
                    {
                        case "C":
                            if (fields.Length != 15)
                                throw new InvalidDataException("capture record must have 15 columns.");
                            var capture = new BlueprintDepthAtlasCaptureDefinition
                            {
                                Key = fields[1],
                                MapperValue = ParseInt(fields[2], "mapperValue"),
                                MapperName = fields[3],
                                Skin = ParseEnum<BlueprintCaptureSkin>(fields[4], "skin"),
                                View = ParseEnum<BlueprintCaptureView>(fields[5], "view"),
                                CaptureRotation = ParseInt(fields[6], "captureRotation"),
                                NormalizedHorizontalFlip = ParseBool(fields[7], "normalizedHorizontalFlip"),
                                MinimumRow = ParseInt(fields[8], "minimumRow"),
                                MaximumRow = ParseInt(fields[9], "maximumRow"),
                                FragmentCount = ParseInt(fields[10], "fragmentCount"),
                                PageCount = ParseInt(fields[11], "pageCount"),
                                FragmentSignature = fields[12],
                                CaptureSource = fields[13],
                                PlacedVisualVersion = fields[14],
                                Directory = directory
                            };
                            if (captures.ContainsKey(capture.Key))
                                throw new InvalidDataException("duplicate capture key.");
                            captures.Add(capture.Key, capture);
                            break;
                        case "P":
                            if (fields.Length != 7)
                                throw new InvalidDataException("page record must have 7 columns.");
                            pages.Add(new BlueprintDepthAtlasPageDefinition
                            {
                                CaptureKey = fields[1],
                                PageIndex = ParseInt(fields[2], "pageIndex"),
                                PngFile = fields[3],
                                Width = ParseInt(fields[4], "width"),
                                Height = ParseInt(fields[5], "height"),
                                Sha256 = fields[6]
                            });
                            break;
                        case "F":
                            if (fields.Length != 13)
                                throw new InvalidDataException("fragment record must have 13 columns.");
                            fragments.Add(new BlueprintDepthAtlasFragmentDefinition
                            {
                                CaptureKey = fields[1],
                                Index = ParseInt(fields[2], "index"),
                                PageIndex = ParseInt(fields[3], "pageIndex"),
                                X = ParseInt(fields[4], "x"),
                                Y = ParseInt(fields[5], "y"),
                                Width = ParseInt(fields[6], "width"),
                                Height = ParseInt(fields[7], "height"),
                                RowOffset = ParseInt(fields[8], "rowOffset"),
                                SortingOffset = ParseInt(fields[9], "sortingOffset"),
                                PositionOffsetX = ParseFloat(fields[10], "positionOffsetX"),
                                PositionOffsetY = ParseFloat(fields[11], "positionOffsetY"),
                                PositionOffsetZ = ParseFloat(fields[12], "positionOffsetZ")
                            });
                            break;
                        default:
                            throw new InvalidDataException("unknown record type.");
                    }
                }
                catch (Exception ex)
                {
                    problems.Add($"Line {lineNumber}: {ex.Message}");
                }
            }

            if (!versionHeaderSeen)
            {
                problems.Add($"The depth-atlas format-{CurrentFormatVersion} header is missing.");
                errors = problems;
                return Array.Empty<BlueprintDepthAtlasCaptureDefinition>();
            }
            foreach (BlueprintDepthAtlasPageDefinition page in pages)
            {
                if (!captures.ContainsKey(page.CaptureKey))
                    problems.Add($"Atlas page references unknown capture '{page.CaptureKey}'.");
            }
            foreach (BlueprintDepthAtlasFragmentDefinition fragment in fragments)
            {
                if (!captures.ContainsKey(fragment.CaptureKey))
                    problems.Add($"Atlas fragment references unknown capture '{fragment.CaptureKey}'.");
            }

            var valid = new List<BlueprintDepthAtlasCaptureDefinition>();
            foreach (BlueprintDepthAtlasCaptureDefinition capture in captures.Values)
            {
                List<BlueprintDepthAtlasPageDefinition> capturePages = pages
                    .Where(value => value.CaptureKey == capture.Key)
                    .OrderBy(value => value.PageIndex)
                    .ToList();
                List<BlueprintDepthAtlasFragmentDefinition> captureFragments = fragments
                    .Where(value => value.CaptureKey == capture.Key)
                    .OrderBy(value => value.Index)
                    .ToList();
                string error = Validate(capture, capturePages, captureFragments);
                if (error != null)
                {
                    problems.Add($"Capture '{capture.Key}': {error}");
                    continue;
                }
                capture.Pages = capturePages;
                capture.Fragments = captureFragments;
                valid.Add(capture);
            }

            errors = problems;
            return valid;
        }

        public static string Serialize(
            IEnumerable<BlueprintDepthAtlasCaptureDefinition> captures)
        {
            var output = new StringBuilder();
            output.Append(ManifestHeader).Append("\r\n");
            foreach (BlueprintDepthAtlasCaptureDefinition capture in captures
                .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                output.Append("C\t").Append(capture.Key).Append('\t')
                    .Append(capture.MapperValue).Append('\t')
                    .Append(capture.MapperName).Append('\t')
                    .Append(capture.Skin).Append('\t')
                    .Append(capture.View).Append('\t')
                    .Append(capture.CaptureRotation).Append('\t')
                    .Append(capture.NormalizedHorizontalFlip ? "true" : "false").Append('\t')
                    .Append(capture.MinimumRow).Append('\t')
                    .Append(capture.MaximumRow).Append('\t')
                    .Append(capture.FragmentCount).Append('\t')
                    .Append(capture.PageCount).Append('\t')
                    .Append(capture.FragmentSignature ?? string.Empty).Append('\t')
                    .Append(capture.CaptureSource ?? string.Empty).Append('\t')
                    .Append(capture.PlacedVisualVersion ?? string.Empty).Append("\r\n");
                foreach (BlueprintDepthAtlasPageDefinition page in capture.Pages
                    .OrderBy(value => value.PageIndex))
                {
                    output.Append("P\t").Append(capture.Key).Append('\t')
                        .Append(page.PageIndex).Append('\t')
                        .Append(page.PngFile).Append('\t')
                        .Append(page.Width).Append('\t')
                        .Append(page.Height).Append('\t')
                        .Append(page.Sha256).Append("\r\n");
                }
                foreach (BlueprintDepthAtlasFragmentDefinition fragment in capture.Fragments
                    .OrderBy(value => value.Index))
                {
                    output.Append("F\t").Append(capture.Key).Append('\t')
                        .Append(fragment.Index).Append('\t')
                        .Append(fragment.PageIndex).Append('\t')
                        .Append(fragment.X).Append('\t')
                        .Append(fragment.Y).Append('\t')
                        .Append(fragment.Width).Append('\t')
                        .Append(fragment.Height).Append('\t')
                        .Append(fragment.RowOffset).Append('\t')
                        .Append(fragment.SortingOffset).Append('\t')
                        .Append(fragment.PositionOffsetX.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(fragment.PositionOffsetY.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(fragment.PositionOffsetZ.ToString("R", CultureInfo.InvariantCulture)).Append("\r\n");
                }
            }
            return output.ToString();
        }

        private static string Validate(
            BlueprintDepthAtlasCaptureDefinition capture,
            IReadOnlyList<BlueprintDepthAtlasPageDefinition> pages,
            IReadOnlyList<BlueprintDepthAtlasFragmentDefinition> fragments)
        {
            if (string.IsNullOrWhiteSpace(capture.Key) ||
                string.IsNullOrWhiteSpace(capture.MapperName) ||
                capture.MaximumRow < capture.MinimumRow ||
                capture.FragmentCount <= 0 || capture.PageCount <= 0)
            {
                return "capture metadata is invalid.";
            }
            if (pages.Count != capture.PageCount || fragments.Count != capture.FragmentCount)
                return "page or fragment count differs from the capture record.";
            for (int index = 0; index < pages.Count; index++)
            {
                BlueprintDepthAtlasPageDefinition page = pages[index];
                if (page.PageIndex != index || page.Width <= 0 || page.Height <= 0 ||
                    page.Width > 2048 || page.Height > 2048 ||
                    !IsSafeRelativePath(page.PngFile) ||
                    page.Sha256.Length != 64 || page.Sha256.Any(value => !Uri.IsHexDigit(value)))
                {
                    return "an atlas page is invalid.";
                }
            }
            for (int index = 0; index < fragments.Count; index++)
            {
                BlueprintDepthAtlasFragmentDefinition fragment = fragments[index];
                if (fragment.Index != index || fragment.PageIndex < 0 ||
                    fragment.PageIndex >= pages.Count || fragment.Width <= 0 ||
                    fragment.Height <= 0 || fragment.RowOffset < 0 ||
                    fragment.RowOffset > capture.MaximumRow - capture.MinimumRow)
                {
                    return "a fragment index or depth is invalid.";
                }
                BlueprintDepthAtlasPageDefinition page = pages[fragment.PageIndex];
                if (fragment.X < 0 || fragment.Y < 0 ||
                    fragment.X + fragment.Width > page.Width ||
                    fragment.Y + fragment.Height > page.Height ||
                    !IsFinite(fragment.PositionOffsetX) ||
                    !IsFinite(fragment.PositionOffsetY) ||
                    !IsFinite(fragment.PositionOffsetZ))
                {
                    return "a fragment rectangle or position is invalid.";
                }
            }
            return null;
        }

        private static bool IsSafeRelativePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
                path.IndexOf("..", StringComparison.Ordinal) < 0 && path.IndexOf(':') < 0;
        }

        private static int ParseInt(string value, string name)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new InvalidDataException($"'{name}' is not an integer.");
            return result;
        }

        private static float ParseFloat(string value, string name)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ||
                !IsFinite(result))
                throw new InvalidDataException($"'{name}' is not finite.");
            return result;
        }

        private static bool ParseBool(string value, string name)
        {
            if (!bool.TryParse(value, out bool result))
                throw new InvalidDataException($"'{name}' is not a Boolean.");
            return result;
        }

        private static T ParseEnum<T>(string value, string name) where T : struct
        {
            if (!Enum.TryParse(value, false, out T result) || !Enum.IsDefined(typeof(T), result))
                throw new InvalidDataException($"'{name}' is invalid.");
            return result;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
