using AIVParser.Core;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
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
        // Sampling stops per mapper once both size and ground alignment are
        // known, so missing offsets can calibrate themselves during normal play.
        internal static bool EnablePreviewMeasurement = true;

        private const float SampleIntervalSeconds = 0.12f;
        private const float StableWidthTolerance = 0.01f;
        private const int StableSamplesRequired = 3;
        private const int ScanRadius = 64;
        private const int TilemapSize = 800;
        private const string FileName =
            "SpawnCastle_Serp.BlueprintBuildingSizes.tsv";

        private readonly ManualLogSource log;
        private readonly string filePath;
        private readonly Dictionary<string, Measurement> measurements =
            new Dictionary<string, Measurement>(StringComparer.Ordinal);
        private float nextSampleTime;
        private int candidateMapper = int.MinValue;
        private BlueprintCaptureSkin candidateSkin;
        private float candidateWidth;
        private float candidateHeight;
        private float candidateVisualOffsetX;
        private float candidateVisualOffsetY;
        private int candidateStableSamples;

        public BlueprintBuildingSizeCalibration(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            filePath = Path.Combine(Paths.ConfigPath, FileName);
            Load();
        }

        public bool Tick()
        {
            if (!EnablePreviewMeasurement ||
                Time.unscaledTime < nextSampleTime)
            {
                return false;
            }

            nextSampleTime = Time.unscaledTime + SampleIntervalSeconds;
            if (!CanMeasure(
                    out int mapperValue,
                    out string mapperName))
            {
                ResetCandidate();
                return false;
            }

            int footprintSize = AivMapperCatalog.Resolve(mapperValue)
                .FootprintSize ?? 1;
            BlueprintCaptureSkin skin = ResolveCalibrationSkin(mapperName);
            string measurementKey = BuildMeasurementKey(mapperValue, skin);
            if (measurements.TryGetValue(
                    measurementKey,
                    out Measurement completed) &&
                HasUsableGroundOffset(completed))
            {
                ResetCandidate();
                return false;
            }
            if (!TryMeasureTilePreview(
                    footprintSize,
                    out PreviewBounds preview))
            {
                ResetCandidate();
                return false;
            }

            if (candidateMapper == mapperValue &&
                candidateSkin == skin &&
                Math.Abs(candidateWidth - preview.Width) <=
                    StableWidthTolerance &&
                Math.Abs(candidateHeight - preview.Height) <=
                    StableWidthTolerance &&
                Math.Abs(candidateVisualOffsetX - preview.VisualOffsetX) <=
                    StableWidthTolerance &&
                Math.Abs(candidateVisualOffsetY - preview.VisualOffsetY) <=
                    StableWidthTolerance)
            {
                candidateStableSamples++;
            }
            else
            {
                candidateMapper = mapperValue;
                candidateSkin = skin;
                candidateWidth = preview.Width;
                candidateHeight = preview.Height;
                candidateVisualOffsetX = preview.VisualOffsetX;
                candidateVisualOffsetY = preview.VisualOffsetY;
                candidateStableSamples = 1;
            }

            if (candidateStableSamples < StableSamplesRequired)
                return false;

            candidateStableSamples = 0;
            int rotation = GameMap.instance != null
                ? (int)GameMap.instance.CurrentRotation()
                : -1;
            measurements[measurementKey] = new Measurement(
                mapperValue,
                mapperName,
                skin,
                preview.Width,
                preview.Height,
                preview.VisualOffsetX,
                preview.VisualOffsetY,
                preview.FragmentCount,
                preview.GroundTileCount,
                rotation,
                BlueprintBuildingIconCatalog
                    .CurrentCalibrationRevision);
            Save();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint building alignment calibrated from Vanilla preview: " +
                $"mapper={mapperName} ({mapperValue}), " +
                $"skin={skin}, " +
                $"worldSize={preview.Width:F4}x{preview.Height:F4}, " +
                $"visualOffset=({preview.VisualOffsetX:F4}," +
                $"{preview.VisualOffsetY:F4}), " +
                $"fragments={preview.FragmentCount}, " +
                $"groundTiles={preview.GroundTileCount}, " +
                $"source={preview.Source}, " +
                $"rotation={rotation}, " +
                $"revision={BlueprintBuildingIconCatalog.CurrentCalibrationRevision}, " +
                $"file={filePath}.");
            return true;
        }

        public bool TryGetWorldSize(
            int mapperValue,
            bool islamicChurchSkin,
            out Vector2 worldSize)
        {
            AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
            if (BlueprintBuildingIconCatalog.HasReservedPlacementArea(
                    mapper.Name))
            {
                // Ignore old TSV rows as well as new mouse-preview samples.
                worldSize = Vector2.zero;
                return false;
            }

            string measurementKey = BuildMeasurementKey(
                mapperValue,
                ResolveCalibrationSkin(mapper.Name, islamicChurchSkin));
            if (measurements.TryGetValue(
                    measurementKey,
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

        public bool TryGetVisualCenterOffset(
            int mapperValue,
            bool islamicChurchSkin,
            Vector2 renderedWorldSize,
            out Vector2 visualOffset)
        {
            AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
            string measurementKey = BuildMeasurementKey(
                mapperValue,
                ResolveCalibrationSkin(mapper.Name, islamicChurchSkin));
            if (measurements.TryGetValue(
                    measurementKey,
                    out Measurement measurement) &&
                HasUsableGroundOffset(measurement))
            {
                visualOffset = new Vector2(
                    BlueprintBuildingIconCatalog
                        .ScaleCalibratedVisualOffset(
                            measurement.VisualOffsetX,
                            measurement.WorldWidth,
                            renderedWorldSize.x),
                    BlueprintBuildingIconCatalog
                        .ScaleCalibratedVisualOffset(
                            measurement.VisualOffsetY,
                            measurement.WorldHeight,
                            renderedWorldSize.y));
                return true;
            }

            visualOffset = Vector2.zero;
            return false;
        }

        private static bool CanMeasure(
            out int mapperValue,
            out string mapperName)
        {
            mapperValue = 0;
            mapperName = string.Empty;
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
                BlueprintBuildingIconDefinition definition =
                    BlueprintBuildingIconCatalog.ResolveDefinition(mapperName);
                return !BlueprintBuildingIconCatalog
                        .HasReservedPlacementArea(mapperName) &&
                    mapper.IsKnown &&
                    (mapper.Category == AivItemCategory.Building ||
                        (definition != null &&
                         !string.IsNullOrWhiteSpace(
                             definition.HelpImageFileName)));
            }
            catch
            {
                return false;
            }
        }

        private static bool TryMeasureTilePreview(
            int footprintSize,
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
            float groundCenterX = 0f;
            float groundCenterY = 0f;
            float minimumGroundHeight = float.PositiveInfinity;
            float maximumGroundHeight = float.NegativeInfinity;
            int groundTileCount = 0;

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
                        tile.tileImage == tile.constructionOrigImage)
                    {
                        continue;
                    }

                    Vector3 cellCenter =
                        tile.tilemapRef.GetCellCenterWorld(
                            new Vector3Int(x, y, 0));
                    cellCenter.y += tile.height;
                    // Vanilla changes every footprint cell: extended sprites
                    // occupy the isometric slice anchors and 64x32 sprites fill
                    // the remaining diamonds. Averaging all changed cells
                    // therefore recovers the complete placement centre.
                    groundCenterX += cellCenter.x;
                    groundCenterY += cellCenter.y;
                    minimumGroundHeight = Math.Min(
                        minimumGroundHeight,
                        tile.height);
                    maximumGroundHeight = Math.Max(
                        maximumGroundHeight,
                        tile.height);
                    groundTileCount++;
                    if (BlueprintBuildingIconCatalog
                        .IsExtendedPreviewSprite(
                            tile.tileImage.rect.width,
                            tile.tileImage.rect.height))
                    {
                        // Extended fragments form the actual Vanilla building
                        // visual; the 64x32 placement diamonds are measured
                        // separately as its ground reference.
                        Bounds bounds = tile.tileImage.bounds;
                        left = Math.Min(left, cellCenter.x + bounds.min.x);
                        right = Math.Max(right, cellCenter.x + bounds.max.x);
                        bottom = Math.Min(
                            bottom,
                            cellCenter.y + bounds.min.y);
                        top = Math.Max(top, cellCenter.y + bounds.max.y);
                        fragmentCount++;
                    }
                }
            }

            float width = right - left;
            float height = top - bottom;
            if (!IsPlausibleMeasurement(
                    width,
                    height,
                    fragmentCount,
                    groundTileCount) ||
                maximumGroundHeight - minimumGroundHeight >
                    StableWidthTolerance)
            {
                return false;
            }

            groundCenterX /= groundTileCount;
            groundCenterY /= groundTileCount;
            float visualOffsetX = (left + right) / 2f - groundCenterX;
            float measuredVisualOffsetY =
                (bottom + top) / 2f - groundCenterY;
            float visualOffsetY = BlueprintBuildingIconCatalog
                .ConvertPreviewSliceOffsetY(
                    footprintSize,
                    measuredVisualOffsetY);
            if (float.IsNaN(visualOffsetX) ||
                float.IsInfinity(visualOffsetX) ||
                float.IsNaN(visualOffsetY) ||
                float.IsInfinity(visualOffsetY))
            {
                return false;
            }

            preview = new PreviewBounds(
                width,
                height,
                visualOffsetX,
                visualOffsetY,
                fragmentCount,
                groundTileCount,
                "full-extended-preview-sprites");
            return true;
        }

        private static bool IsPlausibleMeasurement(
            float width,
            float height,
            int fragmentCount,
            int groundTileCount)
        {
            return fragmentCount > 0 &&
                groundTileCount > 0 &&
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

                    float visualOffsetX = 0f;
                    float visualOffsetY = 0f;
                    int groundTiles = 0;
                    bool hasGroundOffset = false;
                    if (parts.Length >= 10)
                    {
                        if (!float.TryParse(
                                parts[7],
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out visualOffsetX) ||
                            !float.TryParse(
                                parts[8],
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out visualOffsetY) ||
                            !int.TryParse(
                                parts[9],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out groundTiles))
                        {
                            continue;
                        }

                        hasGroundOffset = groundTiles > 0 &&
                            BlueprintBuildingIconCatalog
                                .IsUsableGroundOffsetRevision(revision);
                    }

                    BlueprintCaptureSkin skin = BlueprintCaptureSkin.Generic;
                    if (parts.Length >= 11 &&
                        !Enum.TryParse(parts[10], false, out skin))
                    {
                        continue;
                    }

                    measurements[BuildMeasurementKey(mapperValue, skin)] = new Measurement(
                        mapperValue,
                        parts[1],
                        skin,
                        width,
                        height,
                        visualOffsetX,
                        visualOffsetY,
                        fragments,
                        groundTiles,
                        rotation,
                        revision,
                        hasGroundOffset);
                }

                int usableCount = measurements.Values.Count(
                    IsUsableMeasurement);
                int alignedCount = measurements.Values.Count(
                    HasUsableGroundOffset);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Loaded {measurements.Count} Blueprint building-size " +
                    $"calibrations ({usableCount} usable sizes, " +
                    $"{alignedCount} ground-aligned) from {filePath}.");
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
                    "fragments\trotation\tmeasurementRevision\t" +
                    "visualOffsetX\tvisualOffsetY\tgroundTiles\tskin\r\n");
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
                    output.Append('\t');
                    output.Append(measurement.VisualOffsetX.ToString(
                        "F6",
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.VisualOffsetY.ToString(
                        "F6",
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.GroundTileCount.ToString(
                        CultureInfo.InvariantCulture));
                    output.Append('\t');
                    output.Append(measurement.Skin);
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
            candidateSkin = BlueprintCaptureSkin.Generic;
            candidateWidth = 0f;
            candidateHeight = 0f;
            candidateVisualOffsetX = 0f;
            candidateVisualOffsetY = 0f;
            candidateStableSamples = 0;
        }

        private static bool IsUsableMeasurement(Measurement measurement)
        {
            return !BlueprintBuildingIconCatalog.HasReservedPlacementArea(
                    measurement.MapperName) &&
                BlueprintBuildingIconCatalog
                    .IsUsableCalibrationRevision(measurement.Revision);
        }

        private static bool HasUsableGroundOffset(Measurement measurement)
        {
            return measurement.HasGroundOffset &&
                BlueprintBuildingIconCatalog.IsUsableGroundOffsetRevision(
                    measurement.Revision);
        }

        private static string BuildMeasurementKey(
            int mapperValue,
            BlueprintCaptureSkin skin)
        {
            return mapperValue.ToString(CultureInfo.InvariantCulture) + ":" + skin;
        }

        private static BlueprintCaptureSkin ResolveCalibrationSkin(string mapperName)
        {
            bool islamic = false;
            try
            {
                islamic = GameData.Instance != null &&
                    GameData.Instance.lastGameState != null &&
                    BlueprintBuildingIconCatalog.IsIslamicLordType(
                        GameData.Instance.lastGameState.lord_Type);
            }
            catch
            {
                // During scene changes a Church simply waits for the next sample.
            }
            return ResolveCalibrationSkin(mapperName, islamic);
        }

        private static BlueprintCaptureSkin ResolveCalibrationSkin(
            string mapperName,
            bool islamicChurchSkin)
        {
            bool church = mapperName == "MAPPER_CHURCH1" ||
                mapperName == "MAPPER_CHURCH2" ||
                mapperName == "MAPPER_CHURCH3";
            if (!church)
                return BlueprintCaptureSkin.Generic;
            return islamicChurchSkin
                ? BlueprintCaptureSkin.Islamic
                : BlueprintCaptureSkin.European;
        }

        private readonly struct PreviewBounds
        {
            public PreviewBounds(
                float width,
                float height,
                float visualOffsetX,
                float visualOffsetY,
                int fragmentCount,
                int groundTileCount,
                string source)
            {
                Width = width;
                Height = height;
                VisualOffsetX = visualOffsetX;
                VisualOffsetY = visualOffsetY;
                FragmentCount = fragmentCount;
                GroundTileCount = groundTileCount;
                Source = source ?? string.Empty;
            }

            public float Width { get; }

            public float Height { get; }

            public float VisualOffsetX { get; }

            public float VisualOffsetY { get; }

            public int FragmentCount { get; }

            public int GroundTileCount { get; }

            public string Source { get; }
        }

        private sealed class Measurement
        {
            public Measurement(
                int mapperValue,
                string mapperName,
                BlueprintCaptureSkin skin,
                float worldWidth,
                float worldHeight,
                float visualOffsetX,
                float visualOffsetY,
                int fragmentCount,
                int groundTileCount,
                int rotation,
                int revision,
                bool hasGroundOffset = true)
            {
                MapperValue = mapperValue;
                MapperName = mapperName ?? string.Empty;
                Skin = skin;
                WorldWidth = worldWidth;
                WorldHeight = worldHeight;
                VisualOffsetX = visualOffsetX;
                VisualOffsetY = visualOffsetY;
                FragmentCount = fragmentCount;
                GroundTileCount = groundTileCount;
                Rotation = rotation;
                Revision = revision;
                HasGroundOffset = hasGroundOffset;
            }

            public int MapperValue { get; }

            public string MapperName { get; }

            public BlueprintCaptureSkin Skin { get; }

            public float WorldWidth { get; }

            public float WorldHeight { get; }

            public float VisualOffsetX { get; }

            public float VisualOffsetY { get; }

            public int FragmentCount { get; }

            public int GroundTileCount { get; }

            public int Rotation { get; }

            public int Revision { get; }

            public bool HasGroundOffset { get; }
        }
    }
}
