#nullable enable

using System;
using System.Collections.Generic;

namespace SpawnCastle
{
    internal enum BlueprintHelpImageCleanup
    {
        None,
        RemoveWorkshopBottomWedge,
        RemoveTannerArtifacts
    }

    internal enum BlueprintDrawbridgePosition
    {
        NotApplicable,
        Unknown,
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight
    }

    internal sealed class BlueprintDrawbridgeImageDefinition
    {
        public BlueprintDrawbridgeImageDefinition(
            string bundledImageFileName,
            bool flipHorizontally,
            bool usesPlaceholderImage,
            bool usesBundledImage,
            float bundledPivotPixelsFromBottom = 0f)
        {
            if (usesBundledImage && bundledPivotPixelsFromBottom <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bundledPivotPixelsFromBottom));
            }

            HelpImageFileName = bundledImageFileName ??
                throw new ArgumentNullException(nameof(bundledImageFileName));
            FlipHorizontally = flipHorizontally;
            UsesPlaceholderImage = usesPlaceholderImage;
            UsesBundledImage = usesBundledImage;
            BundledPivotPixelsFromBottom = bundledPivotPixelsFromBottom;
        }

        public string HelpImageFileName { get; }

        public bool FlipHorizontally { get; }

        public bool UsesPlaceholderImage { get; }

        public bool UsesBundledImage { get; }

        public float BundledPivotPixelsFromBottom { get; }
    }

    internal sealed class BlueprintBuildingIconDefinition
    {
        public BlueprintBuildingIconDefinition(
            string buildMenuResourceKey,
            string? helpImageFileName = null,
            BlueprintHelpImageCleanup cleanup =
                BlueprintHelpImageCleanup.None,
            string? islamicBuildMenuResourceKey = null,
            string? islamicHelpImageFileName = null)
        {
            BuildMenuResourceKey = buildMenuResourceKey ??
                throw new ArgumentNullException(nameof(buildMenuResourceKey));
            HelpImageFileName = helpImageFileName;
            Cleanup = cleanup;
            IslamicBuildMenuResourceKey = islamicBuildMenuResourceKey;
            IslamicHelpImageFileName = islamicHelpImageFileName;
        }

        public string BuildMenuResourceKey { get; }

        public string? HelpImageFileName { get; }

        public BlueprintHelpImageCleanup Cleanup { get; }

        public string? IslamicBuildMenuResourceKey { get; }

        public string? IslamicHelpImageFileName { get; }

        public string ResolveBuildMenuResource(bool islamicSkin)
        {
            return islamicSkin &&
                !string.IsNullOrWhiteSpace(IslamicBuildMenuResourceKey)
                    ? IslamicBuildMenuResourceKey!
                    : BuildMenuResourceKey;
        }

        public string? ResolveHelpImage(bool islamicSkin)
        {
            return islamicSkin &&
                !string.IsNullOrWhiteSpace(IslamicHelpImageFileName)
                    ? IslamicHelpImageFileName
                    : HelpImageFileName;
        }
    }

    internal static class BlueprintBuildingIconCatalog
    {
        public const int CurrentCalibrationRevision = 4;

        // Vanilla crops these world-style images to different heights, but
        // their ground contact stays at one baseline above the lower crop edge.
        private const float GroundContactPixelsFromBottom = 21f;
        private const float BuildMenuCalibratedVisualCorrection = 1.10f;

        private static readonly IReadOnlyDictionary<string, float>
            ReservedAreaVisibleWorldWidths =
            new Dictionary<string, float>(StringComparer.Ordinal)
            {
                // The Vanilla preview reserves much more ground than the
                // visible building. The fixed widths preserve the screenshot
                // ratios against the clean Help images at 64 PPU:
                // 5/10, 5/10, 5/10, 5/7.5, 5/7.5, and 253/384.
                ["MAPPER_BARRACKS_STONE"] = 5f,
                ["MAPPER_BARRACKS_WOOD"] = 5f,
                ["MAPPER_BEDOUIN_STOCKADE"] = 5f,
                ["MAPPER_ENGINEERS_GUILD"] = 5f,
                ["MAPPER_TUNNELERS_GUILD"] = 5f,
                ["MAPPER_OIL_SMELTER"] = 253f / 64f
            };

        private static readonly IReadOnlyDictionary<string, float>
            NormalScaleOverrides =
                new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    // Farm placement footprints include large crop clearances
                    // that are not part of the farmhouse image. Feature matches
                    // against tile_buildings2 put all four crops near 0.61x.
                    ["MAPPER_WHEATFARM"] = 1.64f,
                    ["MAPPER_HOPSFARM"] = 1.64f,
                    ["MAPPER_APPLEFARM"] = 1.64f,
                    ["MAPPER_CATTLEFARM"] = 1.64f
                };

        private static readonly IReadOnlyDictionary<
            BlueprintDrawbridgePosition,
            BlueprintDrawbridgeImageDefinition> DrawbridgeImages =
                new Dictionary<
                    BlueprintDrawbridgePosition,
                    BlueprintDrawbridgeImageDefinition>
                {
                    [BlueprintDrawbridgePosition.BottomLeft] =
                        new BlueprintDrawbridgeImageDefinition(
                            "ST49_Drawbridge.png",
                            false,
                            false,
                            false),
                    [BlueprintDrawbridgePosition.BottomRight] =
                        new BlueprintDrawbridgeImageDefinition(
                            "ST49_Drawbridge.png",
                            true,
                            false,
                            false),
                    [BlueprintDrawbridgePosition.TopLeft] =
                        new BlueprintDrawbridgeImageDefinition(
                            "Drawbridge_TopRight.png",
                            false,
                            false,
                            true,
                            80.5f),
                    [BlueprintDrawbridgePosition.TopRight] =
                        new BlueprintDrawbridgeImageDefinition(
                            "Drawbridge_TopLeft.png",
                            false,
                            false,
                            true,
                            80.5f)
                };

        private static readonly BlueprintDrawbridgeImageDefinition
            UnknownDrawbridgeImage =
                new BlueprintDrawbridgeImageDefinition(
                    "ST49_Drawbridge.png",
                    false,
                    true,
                    false);

        private static readonly IReadOnlyDictionary<string, string>
            HelpImageFiles =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MAPPER_STAIR1"] = "stairs_help.png",
                    ["MAPPER_STAIR2"] = "stairs_help.png",
                    ["MAPPER_STAIR3"] = "stairs_help.png",
                    ["MAPPER_STAIR4"] = "stairs_help.png",
                    ["MAPPER_STAIR5"] = "stairs_help.png",
                    ["MAPPER_STAIR6"] = "stairs_help.png",
                    ["MAPPER_FLETCHER"] = "ST12_Fletchers_Workshop.png",
                    ["MAPPER_WOODSMAN"] = "ST03_Woodcutters_Hut.png",
                    ["MAPPER_HOVEL"] = "ST02_House.png",
                    ["MAPPER_OXENBASE"] = "ST04_Oxen_Base.png",
                    ["MAPPER_QUARRY"] = "ST20_Quarry.png",
                    ["MAPPER_STABLES"] = "ST35_Stables.png",
                    ["MAPPER_WHEATFARM"] = "ST30_Wheatfarm.png",
                    ["MAPPER_HOPSFARM"] = "ST31_Hopsfarm.png",
                    ["MAPPER_APPLEFARM"] = "ST32_Applefarm.png",
                    ["MAPPER_CATTLEFARM"] = "ST33_Cattlefarm.png",
                    ["MAPPER_MILL"] = "ST34_Mill.png",
                    ["MAPPER_BAKER"] = "ST17_Bakers_Workshop.png",
                    ["MAPPER_BREWER"] = "ST18_Brewers_Workshop.png",
                    ["MAPPER_TRADEPOST"] = "ST26_Tradepost.png",
                    ["MAPPER_HUNTER"] = "ST07_Hunters_Hut.png",
                    ["MAPPER_BEDOUIN_STOCKADE"] =
                        "ST08_Bedouin_Stockade.png",
                    ["MAPPER_GRANARY"] = "ST19_Granary.png",
                    ["MAPPER_POLETURNER"] =
                        "ST14_Poleturners_Workshop.png",
                    ["MAPPER_BLACKSMITH"] =
                        "ST13_Blacksmiths_Workshop.png",
                    ["MAPPER_ARMOURER"] =
                        "ST15_Armourers_Workshop.png",
                    ["MAPPER_TANNER"] = "ST16_Tanners_Workshop.png",
                    ["MAPPER_BARRACKS_WOOD"] =
                        "ST08_Mercenary_Post.png",
                    ["MAPPER_BARRACKS_STONE"] = "ST08_Barracks.png",
                    ["MAPPER_ENGINEERS_GUILD"] =
                        "ST24_Engineers_Guild.png",
                    ["MAPPER_TUNNELERS_GUILD"] =
                        "ST25_Tunnellers_Guild.png",
                    ["MAPPER_IRON_MINE"] = "ST05_Iron_Mine.png",
                    ["MAPPER_PITCH_WORKINGS"] = "ST06_Pitch_Digger.png",
                    ["MAPPER_INN"] = "ST22_Inn.png",
                    ["MAPPER_HEALER"] = "ST23_Healer.png",
                    ["MAPPER_CHURCH3"] = "ST36_Church.png",
                    ["MAPPER_DRAWBRIDGE"] = "ST49_Drawbridge.png",
                    ["MAPPER_TOWER1"] = "ST74_Tower1.png",
                    ["MAPPER_TOWER2"] = "ST74_Tower2.png",
                    ["MAPPER_TOWER3"] = "ST74_Tower3.png",
                    ["MAPPER_TOWER4"] = "ST74_Tower4.png",
                    ["MAPPER_TOWER5"] = "ST74_Tower5.png",
                    // The Help image shows only Vanilla's A/NS orientation.
                    ["MAPPER_GATE_STONE2A"] = "ST45_Gate_Main.png",
                    ["MAPPER_MAYPOLE"] = "ST65_Maypole.png",
                    ["MAPPER_GALLOWS"] = "ST62_Gallows.png",
                    ["MAPPER_OIL_SMELTER"] = "ST28_Oil_Smelter.png",
                    ["MAPPER_DOG_CAGE"] = "st99_dog_cage.png",
                    ["MAPPER_WELL"] = "ST27_well.png",
                    ["MAPPER_WATERPOT"] = "st70_Water_Pot.png"
                };

        private static readonly IReadOnlyDictionary<string, string> ResourceKeys =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Odd-numbered resources are Vanilla's normal variants; the
                // following even numbers are the highlighted build-menu images.
                ["MAPPER_STAIR1"] = "UI-Buildings C001",
                ["MAPPER_STAIR2"] = "UI-Buildings C001",
                ["MAPPER_STAIR3"] = "UI-Buildings C001",
                ["MAPPER_STAIR4"] = "UI-Buildings C001",
                ["MAPPER_STAIR5"] = "UI-Buildings C001",
                ["MAPPER_STAIR6"] = "UI-Buildings C001",
                ["MAPPER_WOODWALL"] = "UI-Buildings C003",
                ["MAPPER_WALL"] = "UI-Buildings C005",
                ["MAPPER_CRENAL"] = "UI-Buildings C007",
                ["MAPPER_CRENAL2"] = "UI-Buildings C007",
                ["MAPPER_PITCH_DITCH"] = "UI-Buildings L011",
                ["MAPPER_KILLING_PIT"] = "UI-Buildings L013",
                ["MAPPER_MOAT"] = "UI-Buildings A013",
                ["MAPPER_FLETCHER"] = "UI-Buildings I001",
                ["MAPPER_WOODSMAN"] = "UI-Buildings D003",
                ["MAPPER_STORES"] = "UI-Buildings D001",
                ["MAPPER_HOVEL"] = "UI-Buildings F001",
                ["MAPPER_OXENBASE"] = "UI-Buildings D007",
                ["MAPPER_QUARRY"] = "UI-Buildings D005",
                ["MAPPER_STABLES"] = "UI-Buildings M007",
                ["MAPPER_WHEATFARM"] = "UI-Buildings E007",
                ["MAPPER_HOPSFARM"] = "UI-Buildings E009",
                ["MAPPER_APPLEFARM"] = "UI-Buildings E005",
                ["MAPPER_CATTLEFARM"] = "UI-Buildings E003",
                ["MAPPER_MILL"] = "UI-Buildings J005",
                ["MAPPER_BAKER"] = "UI-Buildings J003",
                ["MAPPER_BREWER"] = "UI-Buildings J007",
                ["MAPPER_TRADEPOST"] = "UI-Buildings D013",
                ["MAPPER_HUNTER"] = "UI-Buildings E001",
                ["MAPPER_BEDOUIN_STOCKADE"] = "UI-Buildings C015",
                ["MAPPER_GRANARY"] = "UI-Buildings J001",
                ["MAPPER_ARMOURY"] = "UI-Buildings C013",
                ["MAPPER_POLETURNER"] = "UI-Buildings I003",
                ["MAPPER_BLACKSMITH"] = "UI-Buildings I005",
                ["MAPPER_ARMOURER"] = "UI-Buildings I009",
                ["MAPPER_TANNER"] = "UI-Buildings I007",
                ["MAPPER_BARRACKS_WOOD"] = "UI-Buildings C011",
                ["MAPPER_BARRACKS_STONE"] = "UI-Buildings C009",
                ["MAPPER_ENGINEERS_GUILD"] = "UI-Buildings M001",
                ["MAPPER_TUNNELERS_GUILD"] = "UI-Buildings M009",
                ["MAPPER_IRON_MINE"] = "UI-Buildings D009",
                ["MAPPER_PITCH_WORKINGS"] = "UI-Buildings D011",
                ["MAPPER_INN"] = "UI-Buildings J009",
                ["MAPPER_HEALER"] = "UI-Buildings F009",
                ["MAPPER_CHURCH1"] = "UI-Buildings F003",
                ["MAPPER_CHURCH2"] = "UI-Buildings F005",
                ["MAPPER_CHURCH3"] = "UI-Buildings F007",
                ["MAPPER_DRAWBRIDGE"] = "UI-Buildings L007",
                ["MAPPER_TOWER1"] = "UI-Buildings K001",
                ["MAPPER_TOWER2"] = "UI-Buildings K003",
                ["MAPPER_TOWER3"] = "UI-Buildings K005",
                ["MAPPER_TOWER4"] = "UI-Buildings K007",
                ["MAPPER_TOWER5"] = "UI-Buildings K009",
                // Vanilla provides separate normal sprites for both axes.
                ["MAPPER_GATE_STONE1A"] = "UI-Buildings L003",
                ["MAPPER_GATE_STONE1B"] = "UI-Buildings L025",
                ["MAPPER_GATE_STONE2A"] = "UI-Buildings L005",
                ["MAPPER_GATE_STONE2B"] = "UI-Buildings L023",
                ["MAPPER_GARDEN1"] = "UI-Buildings H005",
                ["MAPPER_GARDEN7"] = "UI-Buildings H005",
                ["MAPPER_GARDEN10"] = "UI-Buildings H005",
                ["MAPPER_MAYPOLE"] = "UI-Buildings H001",
                ["MAPPER_GALLOWS"] = "UI-Buildings G001",
                ["MAPPER_STOCKS"] = "UI-Buildings G005",
                ["MAPPER_OIL_SMELTER"] = "UI-Buildings M011",
                ["MAPPER_CESS_PIT1"] = "UI-Buildings G003",
                ["MAPPER_BURNING_STAKE"] = "UI-Buildings G009",
                ["MAPPER_GIBBET"] = "UI-Buildings G015",
                ["MAPPER_DUNGEON"] = "UI-Buildings G011",
                ["MAPPER_RACK_STRETCHING"] = "UI-Buildings G013",
                ["MAPPER_CHOPPING_BLOCK"] = "UI-Buildings G017",
                ["MAPPER_DUNKING_STOOL"] = "UI-Buildings G019",
                ["MAPPER_DOG_CAGE"] = "UI-Buildings L009",
                ["MAPPER_STATUE1"] = "UI-Buildings H007",
                ["MAPPER_SHRINE1"] = "UI-Buildings H009",
                ["MAPPER_DANCING_BEAR"] = "UI-Buildings H003",
                ["MAPPER_POND1"] = "UI-Buildings H011",
                ["MAPPER_POND3"] = "UI-Buildings H011",
                ["MAPPER_WELL"] = "UI-Buildings F011",
                ["MAPPER_WATERPOT"] = "UI-Buildings F013",
                // These normal variants are exposed only by the map editor.
                // HUD_Main.xaml maps them directly to the three outpost types.
                ["MAPPER_OUTPOST_ARAB"] = "UI-Buildings N071",
                ["MAPPER_OUTPOST"] = "UI-Buildings N073",
                ["MAPPER_OUTPOST_BEDOUIN"] = "UI-Buildings N075"
            };

        private static readonly IReadOnlyDictionary<string, string>
            IslamicResourceKeys =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MAPPER_CHURCH1"] = "UI-Buildings F003a",
                    ["MAPPER_CHURCH2"] = "UI-Buildings F005a",
                    ["MAPPER_CHURCH3"] = "UI-Buildings F007a"
                };

        private static readonly IReadOnlyDictionary<
            string,
            BlueprintBuildingIconDefinition> Definitions =
                CreateDefinitions();

        public static string? Resolve(string? mapperName)
        {
            return mapperName != null &&
                Definitions.TryGetValue(
                    mapperName,
                    out BlueprintBuildingIconDefinition? definition)
                    ? definition.BuildMenuResourceKey
                    : null;
        }

        public static BlueprintBuildingIconDefinition? ResolveDefinition(
            string? mapperName)
        {
            return mapperName != null &&
                Definitions.TryGetValue(
                    mapperName,
                    out BlueprintBuildingIconDefinition? definition)
                    ? definition
                    : null;
        }

        public static string? ResolveGateVisualMapper(
            string? mapperName,
            bool mapRotationSwapsAxes)
        {
            if (!mapRotationSwapsAxes || mapperName == null)
                return mapperName;

            // East/West camera views exchange the two screen diagonals. Keep
            // the AIV mapper unchanged, but use the opposite Vanilla picture.
            return mapperName switch
            {
                "MAPPER_GATE_STONE1A" => "MAPPER_GATE_STONE1B",
                "MAPPER_GATE_STONE1B" => "MAPPER_GATE_STONE1A",
                "MAPPER_GATE_STONE2A" => "MAPPER_GATE_STONE2B",
                "MAPPER_GATE_STONE2B" => "MAPPER_GATE_STONE2A",
                _ => mapperName
            };
        }

        public static BlueprintDrawbridgeImageDefinition
            ResolveDrawbridgeImage(BlueprintDrawbridgePosition position)
        {
            return DrawbridgeImages.TryGetValue(
                position,
                out BlueprintDrawbridgeImageDefinition? definition)
                    ? definition
                    : UnknownDrawbridgeImage;
        }

        public static BlueprintDrawbridgePosition ResolveDrawbridgePosition(
            float horizontalDelta,
            float verticalDelta)
        {
            const float epsilon = 0.001f;
            if (Math.Abs(horizontalDelta) <= epsilon ||
                Math.Abs(verticalDelta) <= epsilon)
            {
                return BlueprintDrawbridgePosition.Unknown;
            }

            if (verticalDelta < 0f)
            {
                return horizontalDelta < 0f
                    ? BlueprintDrawbridgePosition.BottomLeft
                    : BlueprintDrawbridgePosition.BottomRight;
            }

            return horizontalDelta < 0f
                ? BlueprintDrawbridgePosition.TopLeft
                : BlueprintDrawbridgePosition.TopRight;
        }

        public static bool IsIslamicLordType(int lordType)
        {
            // This is the exact condition Vanilla uses for Churches, Mosques,
            // their help text, and the corresponding Monk unit name.
            return lordType == 1 ||
                lordType == 2 ||
                lordType == 6 ||
                lordType == 7;
        }

        public static bool HasReservedPlacementArea(
            string mapperName)
        {
            return !string.IsNullOrWhiteSpace(mapperName) &&
                ReservedAreaVisibleWorldWidths.ContainsKey(mapperName);
        }

        public static bool IsUsableCalibrationRevision(int revision)
        {
            return revision >= CurrentCalibrationRevision;
        }

        public static bool IsExtendedPreviewSprite(
            float pixelWidth,
            float pixelHeight)
        {
            // Vanilla's colored placement ground uses exactly one 64x32 tile.
            // Building-preview slices extend beyond at least one of those axes.
            return pixelWidth > 64.5f || pixelHeight > 32.5f;
        }

        public static float CalculateGroundPivotY(int iconPixelHeight)
        {
            if (iconPixelHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(iconPixelHeight));

            return Math.Min(
                0.5f,
                GroundContactPixelsFromBottom / iconPixelHeight);
        }

        public static bool TryCalculateNormalWorldScale(
            string mapperName,
            float iconWorldWidth,
            float iconWorldHeight,
            float calibratedWorldWidth,
            float calibratedWorldHeight,
            bool usesHelpImage,
            out float scale)
        {
            if (string.IsNullOrWhiteSpace(mapperName))
                throw new ArgumentException(
                    "A mapper name is required.",
                    nameof(mapperName));
            if (iconWorldWidth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(iconWorldWidth));
            if (iconWorldHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(iconWorldHeight));

            if (ReservedAreaVisibleWorldWidths.TryGetValue(
                    mapperName,
                    out float visibleWorldWidth))
            {
                // Never let a reservation-yard preview determine the scale.
                // This also gives a correctly sized build-menu fallback.
                scale = visibleWorldWidth / iconWorldWidth;
                return true;
            }

            if (calibratedWorldWidth > 0f &&
                calibratedWorldHeight > 0f)
            {
                // Tile slices can constrain one axis to the footprint while
                // the other retains the full overhanging building graphic.
                // Covering both measured axes selects the useful dimension.
                float correction = usesHelpImage
                    ? 1f
                    : BuildMenuCalibratedVisualCorrection;
                scale = correction * Math.Max(
                    calibratedWorldWidth / iconWorldWidth,
                    calibratedWorldHeight / iconWorldHeight);
                return true;
            }

            if (!usesHelpImage &&
                NormalScaleOverrides.TryGetValue(
                    mapperName,
                    out float measuredScale))
            {
                scale = measuredScale;
                return true;
            }

            // Missing calibration is recoverable: the renderer skips only
            // this icon and keeps the remaining Blueprint operational.
            scale = 0f;
            return false;
        }

        public static bool TryCalculateFootprintEstimatedScale(
            int footprintSize,
            float iconWorldWidth,
            out float scale)
        {
            scale = 0f;
            if (footprintSize <= 0 ||
                iconWorldWidth <= 0f ||
                float.IsNaN(iconWorldWidth) ||
                float.IsInfinity(iconWorldWidth))
            {
                return false;
            }

            // Only the ground width is known. Match the complete sprite width
            // to it and preserve aspect ratio; building height cannot be
            // inferred sensibly from an isometric ground footprint.
            scale = footprintSize / iconWorldWidth;
            return scale > 0f &&
                !float.IsNaN(scale) &&
                !float.IsInfinity(scale);
        }

        private static IReadOnlyDictionary<
            string,
            BlueprintBuildingIconDefinition> CreateDefinitions()
        {
            var definitions = new Dictionary<
                string,
                BlueprintBuildingIconDefinition>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> resource in ResourceKeys)
            {
                HelpImageFiles.TryGetValue(
                    resource.Key,
                    out string? helpImageFile);
                IslamicResourceKeys.TryGetValue(
                    resource.Key,
                    out string? islamicBuildMenuResource);
                string? islamicHelpImage = string.Equals(
                    resource.Key,
                    "MAPPER_CHURCH3",
                    StringComparison.Ordinal)
                        ? "ST100_Mosque.png"
                        : null;
                definitions.Add(
                    resource.Key,
                    new BlueprintBuildingIconDefinition(
                        resource.Value,
                        helpImageFile,
                        ResolveCleanup(helpImageFile),
                        islamicBuildMenuResource,
                        islamicHelpImage));
            }

            return definitions;
        }

        private static BlueprintHelpImageCleanup ResolveCleanup(
            string? helpImageFile)
        {
            if (string.Equals(
                    helpImageFile,
                    "ST16_Tanners_Workshop.png",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintHelpImageCleanup.RemoveTannerArtifacts;
            }

            if (string.Equals(
                    helpImageFile,
                    "ST12_Fletchers_Workshop.png",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    helpImageFile,
                    "ST14_Poleturners_Workshop.png",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    helpImageFile,
                    "ST15_Armourers_Workshop.png",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintHelpImageCleanup.RemoveWorkshopBottomWedge;
            }

            return BlueprintHelpImageCleanup.None;
        }
    }
}
