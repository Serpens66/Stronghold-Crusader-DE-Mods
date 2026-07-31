using AIVParser.Core;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpawnCastle
{
    internal sealed class BlueprintBuildingSizeCalibration
    {
        private const float SampleIntervalSeconds = 0.12f;
        private const float StableWidthTolerance = 0.01f;
        private const int StableSamplesRequired = 3;
        private const int ScanRadius = 64;
        private const int TilemapSize = 800;
        private const string FileName =
            "SpawnCastle_Serp.BlueprintBuildingSizes.tsv";

        private readonly ManualLogSource log;
        private readonly string filePath;
        private readonly Dictionary<int, Measurement> measurements =
            new Dictionary<int, Measurement>();
        private float nextSampleTime;
        private int candidateMapper = int.MinValue;
        private float candidateWidth;
        private float candidateHeight;
        private int candidateStableSamples;

        public BlueprintBuildingSizeCalibration(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            filePath = Path.Combine(Paths.ConfigPath, FileName);
            Load();
        }

        public bool Tick()
        {
            if (Time.unscaledTime < nextSampleTime)
                return false;

            nextSampleTime = Time.unscaledTime + SampleIntervalSeconds;
            if (!CanMeasure(
                    out int mapperValue,
                    out string mapperName,
                    out int footprintSize) ||
                !TryMeasurePreview(
                    mapperName,
                    footprintSize,
                    out PreviewBounds preview))
            {
                ResetCandidate();
                return false;
            }

            if (candidateMapper == mapperValue &&
                Math.Abs(candidateWidth - preview.Width) <=
                    StableWidthTolerance &&
                Math.Abs(candidateHeight - preview.Height) <=
                    StableWidthTolerance)
            {
                candidateStableSamples++;
            }
            else
            {
                candidateMapper = mapperValue;
                candidateWidth = preview.Width;
                candidateHeight = preview.Height;
                candidateStableSamples = 1;
            }

            if (candidateStableSamples < StableSamplesRequired)
                return false;

            candidateStableSamples = 0;
            if (measurements.TryGetValue(
                    mapperValue,
                    out Measurement previous) &&
                previous.Revision >= BlueprintBuildingIconCatalog
                    .CurrentCalibrationRevision &&
                Math.Abs(previous.WorldWidth - preview.Width) <=
                    StableWidthTolerance &&
                Math.Abs(previous.WorldHeight - preview.Height) <=
                    StableWidthTolerance)
            {
                return false;
            }

            int rotation = GameMap.instance != null
                ? (int)GameMap.instance.CurrentRotation()
                : -1;
            measurements[mapperValue] = new Measurement(
                mapperValue,
                mapperName,
                preview.Width,
                preview.Height,
                preview.FragmentCount,
                rotation,
                BlueprintBuildingIconCatalog
                    .CurrentCalibrationRevision);
            Save();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint building size calibrated from Vanilla preview: " +
                $"mapper={mapperName} ({mapperValue}), " +
                $"worldSize={preview.Width:F4}x{preview.Height:F4}, " +
                $"fragments={preview.FragmentCount}, source={preview.Source}, " +
                $"rotation={rotation}, " +
                $"revision={BlueprintBuildingIconCatalog.CurrentCalibrationRevision}, " +
                $"file={filePath}.");
            return true;
        }

        public bool TryGetWorldSize(
            int mapperValue,
            out Vector2 worldSize)
        {
            int calibrationMapperValue =
                BlueprintBuildingIconCatalog
                    .ResolveCalibrationMapperValue(mapperValue);
            if (measurements.TryGetValue(
                    calibrationMapperValue,
                    out Measurement measurement) &&
                IsUsableMeasurement(measurement))
            {
                worldSize = new Vector2(
                    measurement.WorldWidth,
                    measurement.WorldHeight);
                return true;
            }

            worldSize = Vector2.zero;
            return false;
        }

        private static bool CanMeasure(
            out int mapperValue,
            out string mapperName,
            out int footprintSize)
        {
            mapperValue = 0;
            mapperName = string.Empty;
            footprintSize = 0;
            if (EngineInterface.FlattenedLandscape ||
                MainControls.instance == null ||
                MainControls.instance.CurrentAction != 5 ||
                GameMap.instance == null)
            {
                return false;
            }

            mapperValue = MainControls.instance.CurrentSubAction;
            try
            {
                AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
                mapperName = mapper.Name;
                footprintSize = mapper.FootprintSize ?? 1;
                BlueprintBuildingIconDefinition definition =
                    BlueprintBuildingIconCatalog.ResolveDefinition(mapperName);
                return definition != null &&
                    (mapper.Category == AivItemCategory.Building ||
                        !string.IsNullOrWhiteSpace(
                            definition.HelpImageFileName));
            }
            catch
            {
                return false;
            }
        }

        private static bool TryMeasurePreview(
            string mapperName,
            int footprintSize,
            out PreviewBounds preview)
        {
            if (!TryMeasureTilePreview(out preview))
                return false;

            // Reject the characteristic full reservation-yard bounds while
            // still allowing the same proven preview scan for every building.
            return BlueprintBuildingIconCatalog
                .IsPlausibleCalibrationMeasurement(
                    mapperName,
                    footprintSize,
                    preview.Width,
                    preview.FragmentCount);
        }

        private static bool TryMeasureTilePreview(
            out PreviewBounds preview)
        {
            preview = default;
            float mouseTileX = 0f;
            float mouseTileY = 0f;
            MainControls.instance.getMouseMapTilePosition(
                ref mouseTileX,
                ref mouseTileY);
            int minimumX =
                Math.Max(1, (int)mouseTileX - ScanRadius);
            int maximumX =
                Math.Min(TilemapSize - 1, (int)mouseTileX + ScanRadius);
            int minimumY =
                Math.Max(1, (int)mouseTileY - ScanRadius);
            int maximumY =
                Math.Min(TilemapSize - 1, (int)mouseTileY + ScanRadius);

            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            int fragmentCount = 0;

            for (int y = minimumY; y <= maximumY; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    GameMapTile tile =
                        GameMap.instance.getMapTile(x, y);
                    if (tile == null ||
                        tile.tilemapRef == null ||
                        tile.tileImage == null ||
                        tile.constructionOrigImage == null ||
                        tile.tileImage == tile.constructionOrigImage ||
                        !BlueprintBuildingIconCatalog
                            .IsExtendedPreviewSprite(
                                tile.tileImage.rect.width,
                                tile.tileImage.rect.height))
                    {
                        continue;
                    }

                    // Only changed, non-ground sprites form the proven
                    // building-preview bounds used by every mapper.
                    Vector3 cellCenter =
                        tile.tilemapRef.GetCellCenterWorld(
                            new Vector3Int(x, y, 0));
                    cellCenter.y += tile.height;
                    Bounds bounds = tile.tileImage.bounds;
                    left = Math.Min(left, cellCenter.x + bounds.min.x);
                    right = Math.Max(right, cellCenter.x + bounds.max.x);
                    bottom = Math.Min(bottom, cellCenter.y + bounds.min.y);
                    top = Math.Max(top, cellCenter.y + bounds.max.y);
                    fragmentCount++;
                }
            }

            float width = right - left;
            float height = top - bottom;
            if (!IsPlausibleMeasurement(width, height, fragmentCount))
                return false;

            preview = new PreviewBounds(
                width,
                height,
                fragmentCount,
                "full-extended-preview-sprites");
            return true;
        }

        private static bool IsPlausibleMeasurement(
            float width,
            float height,
            int fragmentCount)
        {
            return fragmentCount > 0 &&
                width >= 0.25f &&
                height >= 0.1f &&
                width <= 64f &&
                height <= 64f;
        }

        private void Load()
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                foreach (string line in File.ReadAllLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(line) ||
                        line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] parts = line.Split('\t');
                    if (parts.Length < 6 ||
                        !int.TryParse(
                            parts[0],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int mapperValue) ||
                        !float.TryParse(
                            parts[2],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float width) ||
                        !float.TryParse(
                            parts[3],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float height) ||
                        !int.TryParse(
                            parts[4],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int fragments) ||
                        !int.TryParse(
                            parts[5],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int rotation) ||
                        width <= 0f)
                    {
                        continue;
                    }

                    int revision = 1;
                    if (parts.Length >= 7 &&
                        (!int.TryParse(
                            parts[6],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out revision) ||
                         revision <= 0))
                    {
                        continue;
                    }

                    measurements[mapperValue] = new Measurement(
                        mapperValue,
                        parts[1],
                        width,
                        height,
                        fragments,
                        rotation,
                        revision);
                }

                int usableCount = measurements.Values.Count(
                    IsUsableMeasurement);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Loaded {measurements.Count} Blueprint building-size " +
                    $"calibrations ({usableCount} usable) from {filePath}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint building-size calibrations could not be " +
                    $"loaded from {filePath}: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                var output = new StringBuilder();
                output.Append(
                    "# mapperValue\tmapperName\tworldWidth\tworldHeight\t" +
                    "fragments\trotation\tmeasurementRevision\r\n");
                foreach (Measurement measurement in measurements.Values
                    .OrderBy(value => value.MapperValue))
                {
                    output.Append(measurement.MapperValue.ToString(
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.MapperName);
                    output.Append('\t');
                    output.Append(measurement.WorldWidth.ToString(
                        "F6",
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.WorldHeight.ToString(
                        "F6",
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.FragmentCount.ToString(
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.Rotation.ToString(
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.Revision.ToString(
                        CultureInfo.InvariantCulture));
                    output.Append("\r\n");
                }

                File.WriteAllText(
                    filePath,
                    output.ToString(),
                    new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint building-size calibration could not be " +
                    $"saved to {filePath}: {ex.Message}");
            }
        }

        private void ResetCandidate()
        {
            candidateMapper = int.MinValue;
            candidateWidth = 0f;
            candidateHeight = 0f;
            candidateStableSamples = 0;
        }

        private static bool IsUsableMeasurement(Measurement measurement)
        {
            return BlueprintBuildingIconCatalog
                .IsUsableCalibrationRevision(measurement.Revision);
        }

        private readonly struct PreviewBounds
        {
            public PreviewBounds(
                float width,
                float height,
                int fragmentCount,
                string source)
            {
                Width = width;
                Height = height;
                FragmentCount = fragmentCount;
                Source = source ?? string.Empty;
            }

            public float Width { get; }

            public float Height { get; }

            public int FragmentCount { get; }

            public string Source { get; }
        }

        private sealed class Measurement
        {
            public Measurement(
                int mapperValue,
                string mapperName,
                float worldWidth,
                float worldHeight,
                int fragmentCount,
                int rotation,
                int revision)
            {
                MapperValue = mapperValue;
                MapperName = mapperName ?? string.Empty;
                WorldWidth = worldWidth;
                WorldHeight = worldHeight;
                FragmentCount = fragmentCount;
                Rotation = rotation;
                Revision = revision;
            }

            public int MapperValue { get; }

            public string MapperName { get; }

            public float WorldWidth { get; }

            public float WorldHeight { get; }

            public int FragmentCount { get; }

            public int Rotation { get; }

            public int Revision { get; }
        }
    }
}
