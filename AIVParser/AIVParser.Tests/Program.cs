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
            ("Resolve all special Blueprint icons", TestSpecialBlueprintIcons),
            ("Rotate Gatehouse Blueprint images with the map", TestGateVisualRotation),
            ("Resolve four Drawbridge image slots", TestDrawbridgeImageSlots),
            ("Associate Drawbridges with directional gates", TestDrawbridgeGateAssociations),
            ("Associate all six stair pieces with common endpoints", TestStaircaseEndpoints),
            ("Resolve selected Blueprint help images", TestBlueprintHelpImages),
            ("Resolve lord-dependent Church skins", TestChurchBlueprintSkins),
            ("Scale Blueprint sources independently", TestBlueprintSourceScale),
            ("Validate Blueprint calibration rules", TestBlueprintCalibrationRules),
            ("Expand reserved Blueprint grounds without moving icons", TestReservedBlueprintGrounds),
            ("Exclude Blueprint placement-ground sprites", TestBlueprintPreviewSpriteFilter),
            ("Align Blueprint visuals to footprint centres", TestBuildingIconOffsets),
            ("Compute exact Blueprint capture pivots", TestBlueprintCapturePivots),
            ("Filter Vanilla Blueprint preview fragments", TestBlueprintCaptureFragmentFilter),
            ("Exclude ground-only Blueprint capture variants", TestBlueprintCaptureRequirements),
            ("Resolve Blueprint capture skins and views", TestBlueprintCaptureRouting),
            ("Round-trip and validate Blueprint capture manifests", TestBlueprintCaptureManifest),
            ("Round-trip Blueprint depth fragment manifests", TestBlueprintFragmentManifest),
            ("Remap Blueprint depth rows", TestBlueprintDepthRows),
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
        AssertEqual(5, AivMapperCatalog.Resolve(53).FootprintSize);
        AssertEqual(4, AivMapperCatalog.Resolve(54).FootprintSize);
        AssertEqual(2, AivMapperCatalog.Resolve(55).FootprintSize);
        AssertEqual(6, AivMapperCatalog.Resolve(56).FootprintSize);
        AssertEqual(7, AivMapperCatalog.Resolve(61).FootprintSize);
        AssertEqual(11, AivMapperCatalog.Resolve(62).FootprintSize);
        AssertEqual(9, AivMapperCatalog.Resolve(70).FootprintSize);
        AssertEqual(10, AivMapperCatalog.Resolve(72).FootprintSize);
        AssertEqual(3, AivMapperCatalog.Resolve(78).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(87).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(178).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(179).FootprintSize);
        AssertEqual(1, AivMapperCatalog.Resolve(99).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(311).FootprintSize);
        AssertEqual(5, AivMapperCatalog.Resolve(325).FootprintSize);
        AssertEqual(6, AivMapperCatalog.Resolve(327).FootprintSize);
        AssertEqual(4, AivMapperCatalog.Resolve(342).FootprintSize);
        AssertEqual<int?>(null, AivMapperCatalog.Resolve(63).FootprintSize);
        AssertEqual("Bedouin Outpost", AivMapperCatalog.Resolve(53).DisplayName);
        AssertEqual("Crusader Outpost", AivMapperCatalog.Resolve(178).DisplayName);
        AssertEqual("Arabian Outpost", AivMapperCatalog.Resolve(179).DisplayName);
        AssertEqual("Mercenary Post", AivMapperCatalog.Resolve(86).DisplayName);
        AssertEqual("Low Wall", AivMapperCatalog.Resolve(46).DisplayName);
    }

    private static void TestSpecialBlueprintIcons()
    {
        AssertEqual(
            "UI-Buildings C005",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_WALL"));
        AssertEqual(
            "UI-Buildings C007",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_CRENAL"));
        AssertEqual(
            "UI-Buildings C007",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_CRENAL2"));
        AssertEqual(
            "UI-Buildings C003",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_WOODWALL"));
        AssertEqual(
            "UI-Buildings L013",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_KILLING_PIT"));
        AssertEqual(
            "UI-Buildings L011",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_PITCH_DITCH"));
        AssertEqual(
            "UI-Buildings A013",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_MOAT"));
        AssertEqual(
            "UI-Buildings L003",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_GATE_STONE1A"));
        AssertEqual(
            "UI-Buildings L025",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_GATE_STONE1B"));
        AssertEqual(
            "UI-Buildings L005",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_GATE_STONE2A"));
        AssertEqual(
            "UI-Buildings L023",
            SpawnCastle.BlueprintBuildingIconCatalog.Resolve("MAPPER_GATE_STONE2B"));
        for (int stair = 1; stair <= 6; stair++)
        {
            AssertEqual(
                "UI-Buildings C001",
                SpawnCastle.BlueprintBuildingIconCatalog.Resolve(
                    $"MAPPER_STAIR{stair}"));
        }
    }

    private static void TestBuildingIconOffsets()
    {
        // The fallback places the visual centre above the footprint centre by
        // half the visual height minus half the projected footprint depth.
        AssertEqual(
            0.5f,
            SpawnCastle.BlueprintBuildingIconCatalog
                .CalculateFootprintVisualCenterOffsetY(2, 2f));
        AssertEqual(
            2f,
            SpawnCastle.BlueprintBuildingIconCatalog
                .CalculateFootprintVisualCenterOffsetY(4, 6f));
        AssertEqual(
            -0.5f,
            SpawnCastle.BlueprintBuildingIconCatalog
                .CalculateFootprintVisualCenterOffsetY(5, 1.5f));
        AssertEqual(
            1.4453125f,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ConvertPreviewSliceOffsetY(9, 3.4453125f));
        AssertEqual(
            -0.0234375f,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ConvertPreviewSliceOffsetY(5, 0.9765625f));
        AssertEqual(
            3f,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ScaleCalibratedVisualOffset(1f, 2f, 6f));
        AssertEqual(
            -3f,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ScaleCalibratedVisualOffset(-1f, 2f, 6f));
    }

    private static void TestBlueprintCapturePivots()
    {
        AssertEqual(
            0.5f,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .CalculateNormalizedPivot(1.25f, 0f, 160, 64f));
        AssertEqual(
            0.25f,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .CalculateNormalizedPivot(-0.5f, -1f, 128, 64f));
    }

    private static void TestBlueprintCaptureFragmentFilter()
    {
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .IsBuildingPreviewFragment(
                    "tile_castle 123",
                    "AllTileSprites",
                    64f,
                    32f));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .IsBuildingPreviewFragment(
                    "tile_buildings2 45",
                    "AllTileSprites",
                    192f,
                    160f));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .IsBuildingPreviewFragment(
                    "SpawnCastle_PlacementDiamond",
                    "ConstructionOverlay",
                    64f,
                    32f));
    }

    private static void TestBlueprintCaptureRequirements()
    {
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresCapturedImage("MAPPER_WHEATFARM"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresCapturedImage("MAPPER_CHURCH2"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresCapturedImage("MAPPER_WALL"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresCapturedImage("MAPPER_STAIR4"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresCapturedImage("MAPPER_POND1"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresCapturedImage("MAPPER_MOAT"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresCapturedImage("MAPPER_PITCH_DITCH"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresPlacedCapture("MAPPER_WALL"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresPlacedCapture("MAPPER_WOODWALL"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresPlacedCapture("MAPPER_CRENAL2"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingCaptureCatalog
                .RequiresPlacedCapture("MAPPER_ARMOURY"));
    }

    private static void TestBlueprintCaptureRouting()
    {
        SpawnCastle.BlueprintCaptureRequest european =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_CHURCH2",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest islamic =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_CHURCH2",
                true,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        AssertEqual(SpawnCastle.BlueprintCaptureSkin.European, european.Skin);
        AssertEqual(SpawnCastle.BlueprintCaptureSkin.Islamic, islamic.Skin);

        SpawnCastle.BlueprintCaptureRequest gateA =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_GATE_STONE1A",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest gateB =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_GATE_STONE1B",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        AssertEqual(gateA.Key, gateB.Key);
        AssertEqual(false, gateA.FlipHorizontally);
        AssertEqual(true, gateB.FlipHorizontally);

        SpawnCastle.BlueprintCaptureRequest crenel =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_CRENAL",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest crenel2 =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_CRENAL2",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        AssertEqual(SpawnCastle.BlueprintCaptureView.PlacedDefault, crenel.View);
        AssertEqual(SpawnCastle.BlueprintCaptureView.PlacedDefault, crenel2.View);
        AssertEqual("MAPPER_CRENAL", crenel.MapperName);
        AssertEqual("MAPPER_CRENAL2", crenel2.MapperName);
        Assert(crenel.Key != crenel2.Key,
            "Normal and small crenals need different wall-body captures.");

        SpawnCastle.BlueprintCaptureRequest stairNorth =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_STAIR4",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable,
                SpawnCastle.BlueprintStairDirection.North);
        SpawnCastle.BlueprintCaptureRequest stairSouth =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_STAIR4",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable,
                SpawnCastle.BlueprintStairDirection.South);
        AssertEqual(SpawnCastle.BlueprintCaptureView.StairNorth, stairNorth.View);
        AssertEqual(SpawnCastle.BlueprintCaptureView.StairSouth, stairSouth.View);
        Assert(stairNorth.Key != stairSouth.Key, "Both stair directions need separate visuals.");
        AssertEqual("MAPPER_STAIR", stairNorth.MapperName);
        Assert(SpawnCastle.BlueprintBuildingCaptureCatalog.IsStairMapper("MAPPER_STAIR7"),
            "Numbered stair cells must not be restricted to the former 1..6 range.");
        SpawnCastle.BlueprintCaptureRequest stairNorthMirrored =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_STAIR4",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable,
                SpawnCastle.BlueprintStairDirection.North,
                true);
        AssertEqual(stairNorth.Key, stairNorthMirrored.Key);
        AssertEqual(true, stairNorthMirrored.FlipHorizontally);

        SpawnCastle.BlueprintCaptureRequest oilNorth =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_OIL_SMELTER",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest oilSouth =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_OIL_SMELTER",
                false,
                2,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest oilEast =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_OIL_SMELTER",
                false,
                1,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        Assert(oilNorth.Key != oilSouth.Key, "Front and rear reservations need separate visuals.");
        AssertEqual(false, oilSouth.FlipHorizontally);
        AssertEqual(SpawnCastle.BlueprintCaptureView.ReservationFront, oilNorth.View);
        AssertEqual(SpawnCastle.BlueprintCaptureView.ReservationRear, oilSouth.View);
        AssertEqual(
            SpawnCastle.BlueprintCaptureView.ReservationFront,
            oilEast.View);
        AssertEqual(true, oilEast.FlipHorizontally);

        SpawnCastle.BlueprintCaptureRequest tunnelerNorth =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_TUNNELERS_GUILD",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest tunnelerEast =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_TUNNELERS_GUILD",
                false,
                1,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest tunnelerSouth =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_TUNNELERS_GUILD",
                false,
                2,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        SpawnCastle.BlueprintCaptureRequest tunnelerWest =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_TUNNELERS_GUILD",
                false,
                3,
                SpawnCastle.BlueprintDrawbridgePosition.NotApplicable);
        AssertEqual(SpawnCastle.BlueprintCaptureView.ReservationRear, tunnelerNorth.View);
        AssertEqual(true, tunnelerNorth.FlipHorizontally);
        AssertEqual(SpawnCastle.BlueprintCaptureView.ReservationRear, tunnelerEast.View);
        AssertEqual(false, tunnelerEast.FlipHorizontally);
        AssertEqual(SpawnCastle.BlueprintCaptureView.ReservationFront, tunnelerSouth.View);
        AssertEqual(true, tunnelerSouth.FlipHorizontally);
        AssertEqual(SpawnCastle.BlueprintCaptureView.ReservationFront, tunnelerWest.View);
        AssertEqual(false, tunnelerWest.FlipHorizontally);

        SpawnCastle.BlueprintCaptureRequest bridgeFront =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_DRAWBRIDGE",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.BottomLeft);
        SpawnCastle.BlueprintCaptureRequest bridgeFrontMirrored =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_DRAWBRIDGE",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.BottomRight);
        SpawnCastle.BlueprintCaptureRequest bridgeFrontAfterMapRotation =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_DRAWBRIDGE",
                false,
                2,
                SpawnCastle.BlueprintDrawbridgePosition.BottomLeft);
        AssertEqual(SpawnCastle.BlueprintCaptureView.DrawbridgeFront, bridgeFront.View);
        AssertEqual(false, bridgeFront.FlipHorizontally);
        AssertEqual(bridgeFront.Key, bridgeFrontMirrored.Key);
        AssertEqual(true, bridgeFrontMirrored.FlipHorizontally);
        AssertEqual(bridgeFront.Key, bridgeFrontAfterMapRotation.Key);
        AssertEqual(
            bridgeFront.FlipHorizontally,
            bridgeFrontAfterMapRotation.FlipHorizontally);

        SpawnCastle.BlueprintCaptureRequest bridgeRear =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_DRAWBRIDGE",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.TopRight);
        AssertEqual(SpawnCastle.BlueprintCaptureView.DrawbridgeRear, bridgeRear.View);
        AssertEqual(false, bridgeRear.FlipHorizontally);
        SpawnCastle.BlueprintCaptureRequest bridgeRearMirrored =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ResolveRequest(
                "MAPPER_DRAWBRIDGE",
                false,
                0,
                SpawnCastle.BlueprintDrawbridgePosition.TopLeft);
        AssertEqual(true, bridgeRearMirrored.FlipHorizontally);
    }

    private static void TestBlueprintCaptureManifest()
    {
        var entry = new SpawnCastle.BlueprintCaptureManifestEntry
        {
            FormatVersion = SpawnCastle.BlueprintBuildingCaptureCatalog.CurrentFormatVersion,
            MapperValue = 96,
            MapperName = "MAPPER_CHURCH2",
            Skin = SpawnCastle.BlueprintCaptureSkin.Islamic,
            View = SpawnCastle.BlueprintCaptureView.Default,
            PngFile = "Church2_Islamic.png",
            PivotX = 0.45f,
            PivotY = 0.2f,
            PixelsPerUnit = 64f,
            FragmentSignature = "0123456789ABCDEF"
        };
        string serialized = SpawnCastle.BlueprintBuildingCaptureCatalog
            .SerializeManifest([entry]);
        IReadOnlyList<SpawnCastle.BlueprintCaptureManifestEntry> parsed =
            SpawnCastle.BlueprintBuildingCaptureCatalog.ParseManifest(
                serialized.Split(["\r\n", "\n"], StringSplitOptions.None),
                out IReadOnlyList<string> errors);
        AssertEqual(0, errors.Count);
        AssertEqual(1, parsed.Count);
        AssertEqual(entry.Key, parsed[0].Key);
        AssertEqual(entry.PivotX, parsed[0].PivotX);

        entry.PivotY = 1.1f;
        AssertEqual(
            "pivot is outside the sprite rectangle.",
            SpawnCastle.BlueprintBuildingCaptureCatalog.ValidateEntry(entry));
    }

    private static void TestBlueprintFragmentManifest()
    {
        var capture = new SpawnCastle.BlueprintFragmentCaptureEntry
        {
            FormatVersion = SpawnCastle.BlueprintFragmentCaptureCatalog.CurrentFormatVersion,
            MapperValue = 84,
            MapperName = "MAPPER_ARMOURER",
            Skin = SpawnCastle.BlueprintCaptureSkin.Generic,
            View = SpawnCastle.BlueprintCaptureView.Default,
            CaptureRotation = 1,
            NormalizedHorizontalFlip = false,
            FragmentCount = 1,
            TileCount = 1,
            MinimumRow = 100,
            MaximumRow = 106,
            FragmentSignature = "0123456789ABCDEF"
        };
        capture.Metadata["futureField"] = "tabs\tnewlines\r\nand=equals%";
        var tile = new SpawnCastle.BlueprintFragmentTileEntry
        {
            FormatVersion = SpawnCastle.BlueprintFragmentCaptureCatalog.CurrentFormatVersion,
            CaptureKey = capture.Key,
            Index = 0
        };
        tile.Metadata["row"] = "100";
        var fragment = new SpawnCastle.BlueprintFragmentImageEntry
        {
            FormatVersion = SpawnCastle.BlueprintFragmentCaptureCatalog.CurrentFormatVersion,
            CaptureKey = capture.Key,
            Index = 0,
            PngFile = "Fragments/Armourer/fragment_000.png",
            Sha256 = new string('A', 64),
            Width = 256,
            Height = 128,
            PivotX = 0.5f,
            PivotY = 0.25f,
            PixelsPerUnit = 64f,
            RowOffset = 3,
            PositionOffsetX = -1.25f,
            PositionOffsetY = 0.5f,
            PositionOffsetZ = 0f
        };

        string captureText = SpawnCastle.BlueprintFragmentCaptureCatalog
            .SerializeCaptures([capture]);
        string tileText = SpawnCastle.BlueprintFragmentCaptureCatalog
            .SerializeTiles([tile]);
        string fragmentText = SpawnCastle.BlueprintFragmentCaptureCatalog
            .SerializeFragments([fragment]);
        AssertCrLfString(captureText);
        AssertCrLfString(tileText);
        AssertCrLfString(fragmentText);

        IReadOnlyList<SpawnCastle.BlueprintFragmentCaptureEntry> captures =
            SpawnCastle.BlueprintFragmentCaptureCatalog.ParseCaptures(
                captureText.Split(["\r\n"], StringSplitOptions.None),
                out IReadOnlyList<string> captureErrors);
        IReadOnlyList<SpawnCastle.BlueprintFragmentTileEntry> tiles =
            SpawnCastle.BlueprintFragmentCaptureCatalog.ParseTiles(
                tileText.Split(["\r\n"], StringSplitOptions.None),
                out IReadOnlyList<string> tileErrors);
        IReadOnlyList<SpawnCastle.BlueprintFragmentImageEntry> fragments =
            SpawnCastle.BlueprintFragmentCaptureCatalog.ParseFragments(
                fragmentText.Split(["\r\n"], StringSplitOptions.None),
                out IReadOnlyList<string> fragmentErrors);
        AssertEqual(0, captureErrors.Count + tileErrors.Count + fragmentErrors.Count);
        AssertEqual(1, captures.Count);
        AssertEqual(1, tiles.Count);
        AssertEqual(1, fragments.Count);
        AssertEqual(capture.Metadata["futureField"],
            captures[0].Metadata["futureField"]);
        AssertEqual(fragment.PositionOffsetX, fragments[0].PositionOffsetX);

        fragment.PngFile = "../unsafe.png";
        AssertEqual(
            "fragment PNG path is unsafe or missing.",
            SpawnCastle.BlueprintFragmentCaptureCatalog.ValidateFragment(fragment));
        fragment.PngFile = "Fragments/Armourer/fragment_000.png";
        fragment.RowOffset = 7;
        Assert(
            !SpawnCastle.BlueprintFragmentCaptureCatalog.IsValidRowOffset(
                capture,
                fragment),
            "An out-of-range fragment row offset was accepted.");
    }

    private static void TestBlueprintDepthRows()
    {
        AssertEqual(
            203,
            SpawnCastle.BlueprintFragmentCaptureCatalog.RemapDepthRow(
                100, 106, 200, 206, 3));
        AssertEqual(
            204,
            SpawnCastle.BlueprintFragmentCaptureCatalog.GetMiddleDepthRow(
                200, 208));

        int rearWall = SpawnCastle.BlueprintFragmentCaptureCatalog
            .RemapDepthRow(100, 106, 200, 206, 0);
        int buildingMiddle = SpawnCastle.BlueprintFragmentCaptureCatalog
            .RemapDepthRow(100, 106, 200, 206, 3);
        int frontWall = SpawnCastle.BlueprintFragmentCaptureCatalog
            .RemapDepthRow(100, 106, 200, 206, 6);
        Assert(rearWall < buildingMiddle && buildingMiddle < frontWall,
            "Depth fragments do not sort between rear and front walls.");

        int secondBuildingInSameRow = SpawnCastle.BlueprintFragmentCaptureCatalog
            .RemapDepthRow(100, 106, 200, 206, 3);
        AssertEqual(buildingMiddle, secondBuildingInSameRow);
        AssertEqual(
            -20000 + buildingMiddle * 49 + 4,
            -20000 + secondBuildingInSameRow * 49 + 4);
    }

    private static void TestGateVisualRotation()
    {
        string[] gates =
        [
            "MAPPER_GATE_STONE1A",
            "MAPPER_GATE_STONE1B",
            "MAPPER_GATE_STONE2A",
            "MAPPER_GATE_STONE2B"
        ];
        string[] swapped =
        [
            "MAPPER_GATE_STONE1B",
            "MAPPER_GATE_STONE1A",
            "MAPPER_GATE_STONE2B",
            "MAPPER_GATE_STONE2A"
        ];

        for (int index = 0; index < gates.Length; index++)
        {
            AssertEqual(
                gates[index],
                SpawnCastle.BlueprintBuildingIconCatalog
                    .ResolveGateVisualMapper(gates[index], false));
            AssertEqual(
                swapped[index],
                SpawnCastle.BlueprintBuildingIconCatalog
                    .ResolveGateVisualMapper(gates[index], true));
        }

        AssertEqual(
            "MAPPER_DRAWBRIDGE",
            SpawnCastle.BlueprintBuildingIconCatalog.ResolveGateVisualMapper(
                "MAPPER_DRAWBRIDGE",
                true));
    }

    private static void TestDrawbridgeImageSlots()
    {
        AssertDrawbridgeImage(
            SpawnCastle.BlueprintDrawbridgePosition.BottomLeft,
            "ST49_Drawbridge.png",
            flipHorizontally: false,
            usesPlaceholderImage: false,
            usesBundledImage: false,
            expectedPivotPixelsFromBottom: 0f);
        AssertDrawbridgeImage(
            SpawnCastle.BlueprintDrawbridgePosition.BottomRight,
            "ST49_Drawbridge.png",
            flipHorizontally: true,
            usesPlaceholderImage: false,
            usesBundledImage: false,
            expectedPivotPixelsFromBottom: 0f);
        AssertDrawbridgeImage(
            SpawnCastle.BlueprintDrawbridgePosition.TopLeft,
            "MAPPER_DRAWBRIDGE_Generic_DrawbridgeRear.png",
            flipHorizontally: true,
            usesPlaceholderImage: false,
            usesBundledImage: true,
            expectedPivotPixelsFromBottom: 80.5f);
        AssertDrawbridgeImage(
            SpawnCastle.BlueprintDrawbridgePosition.TopRight,
            "MAPPER_DRAWBRIDGE_Generic_DrawbridgeRear.png",
            flipHorizontally: false,
            usesPlaceholderImage: false,
            usesBundledImage: true,
            expectedPivotPixelsFromBottom: 80.5f);

        AssertEqual(
            SpawnCastle.BlueprintDrawbridgePosition.BottomLeft,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDrawbridgePosition(-1f, -1f));
        AssertEqual(
            SpawnCastle.BlueprintDrawbridgePosition.BottomRight,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDrawbridgePosition(1f, -1f));
        AssertEqual(
            SpawnCastle.BlueprintDrawbridgePosition.TopLeft,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDrawbridgePosition(-1f, 1f));
        AssertEqual(
            SpawnCastle.BlueprintDrawbridgePosition.TopRight,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDrawbridgePosition(1f, 1f));
    }

    private static void TestDrawbridgeGateAssociations()
    {
        // Small A gates accept the centered five-tile edge above and below.
        AssertDrawbridgeGateAssociation(144, 4040, 3540);
        AssertDrawbridgeGateAssociation(144, 4040, 4540);
        // Small B gates accept the matching edge left and right.
        AssertDrawbridgeGateAssociation(145, 4040, 4035);
        AssertDrawbridgeGateAssociation(145, 4040, 4045);
        // A seven-tile gate centers the same five-tile Drawbridge edge.
        AssertDrawbridgeGateAssociation(146, 4040, 3341);
        AssertDrawbridgeGateAssociation(146, 4040, 4541);
        AssertDrawbridgeGateAssociation(147, 4040, 3935);
        AssertDrawbridgeGateAssociation(147, 4040, 3947);

        SpawnCastle.BlueprintIconPlacement isolated =
            BuildDrawbridgeLayout(144, 4040, 2020)
                .Icons.Single(icon => icon.MapperValue == 105);
        Assert(
            !isolated.AdjacentGateCenter.HasValue,
            "A non-adjacent Drawbridge must retain an unresolved image slot.");

        var twoGateDocument = new SpawnCastle.AivJsonDocument
        {
            frames =
            [
                new SpawnCastle.AivJsonFrame
                {
                    itemType = 60,
                    tilePositionOfsets = [5050]
                },
                new SpawnCastle.AivJsonFrame
                {
                    itemType = 144,
                    tilePositionOfsets = [3540]
                },
                new SpawnCastle.AivJsonFrame
                {
                    itemType = 105,
                    tilePositionOfsets = [4040]
                },
                new SpawnCastle.AivJsonFrame
                {
                    itemType = 54,
                    tilePositionOfsets = [6060]
                },
                new SpawnCastle.AivJsonFrame
                {
                    itemType = 144,
                    tilePositionOfsets = [4540]
                }
            ],
            miscItems = []
        };
        SpawnCastle.BlueprintIconPlacement betweenTwoGates =
            SpawnCastle.BlueprintLayoutBuilder.Build(
                    twoGateDocument,
                    400,
                    400)
                .Icons.Single(icon => icon.MapperValue == 105);
        AssertEqual(
            new SpawnCastle.BlueprintWorldTile(392, 417),
            betweenTwoGates.AdjacentGateCenter!.Value);
    }

    private static void AssertDrawbridgeImage(
        SpawnCastle.BlueprintDrawbridgePosition position,
        string expectedFileName,
        bool flipHorizontally,
        bool usesPlaceholderImage,
        bool usesBundledImage,
        float expectedPivotPixelsFromBottom)
    {
        SpawnCastle.BlueprintDrawbridgeImageDefinition definition =
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDrawbridgeImage(position);
        AssertEqual(expectedFileName, definition.HelpImageFileName);
        AssertEqual(flipHorizontally, definition.FlipHorizontally);
        AssertEqual(usesPlaceholderImage, definition.UsesPlaceholderImage);
        AssertEqual(usesBundledImage, definition.UsesBundledImage);
        AssertEqual(
            expectedPivotPixelsFromBottom,
            definition.BundledPivotPixelsFromBottom);
    }

    private static void AssertDrawbridgeGateAssociation(
        int gateMapper,
        int gateOffset,
        int drawbridgeOffset)
    {
        SpawnCastle.BlueprintIconPlacement drawbridge =
            BuildDrawbridgeLayout(gateMapper, gateOffset, drawbridgeOffset)
                .Icons.Single(icon => icon.MapperValue == 105);
        Assert(
            drawbridge.AdjacentGateCenter.HasValue,
            $"Drawbridge {drawbridgeOffset} was not associated with " +
            $"gate {gateMapper} at {gateOffset}.");
    }

    private static SpawnCastle.BlueprintLayout BuildDrawbridgeLayout(
        int gateMapper,
        int gateOffset,
        int drawbridgeOffset)
    {
        var document = new SpawnCastle.AivJsonDocument
        {
            frames =
            [
                new SpawnCastle.AivJsonFrame
                {
                    itemType = 60,
                    tilePositionOfsets = [5050]
                },
                new SpawnCastle.AivJsonFrame
                {
                    itemType = gateMapper,
                    tilePositionOfsets = [gateOffset]
                },
                new SpawnCastle.AivJsonFrame
                {
                    itemType = 105,
                    tilePositionOfsets = [drawbridgeOffset]
                }
            ],
            miscItems = []
        };

        return SpawnCastle.BlueprintLayoutBuilder.Build(
            document,
            400,
            400);
    }

    private static void TestStaircaseEndpoints()
    {
        var frames = new List<SpawnCastle.AivJsonFrame>
        {
            new SpawnCastle.AivJsonFrame
            {
                itemType = 60,
                tilePositionOfsets = [5050]
            }
        };
        for (int index = 0; index < 6; index++)
        {
            frames.Add(new SpawnCastle.AivJsonFrame
            {
                itemType = 181 + index,
                tilePositionOfsets = [4040 + index * 101]
            });
        }

        var document = new SpawnCastle.AivJsonDocument
        {
            frames = frames,
            miscItems = []
        };
        List<SpawnCastle.BlueprintIconPlacement> stairs =
            SpawnCastle.BlueprintLayoutBuilder.Build(document, 400, 400)
                .Icons
                .Where(value => value.MapperValue >= 181 && value.MapperValue <= 186)
                .OrderBy(value => value.MapperValue)
                .ToList();
        AssertEqual(6, stairs.Count);
        Assert(stairs[0].StairLowEnd.HasValue, "The staircase needs a low endpoint.");
        Assert(stairs[0].StairHighEnd.HasValue, "The staircase needs a high endpoint.");
        Assert(
            !stairs[0].StairLowEnd.GetValueOrDefault().Equals(
                stairs[0].StairHighEnd.GetValueOrDefault()),
            "The low and high staircase endpoints must differ.");
        foreach (SpawnCastle.BlueprintIconPlacement stair in stairs)
        {
            AssertEqual(stairs[0].StairLowEnd, stair.StairLowEnd);
            AssertEqual(stairs[0].StairHighEnd, stair.StairHighEnd);
        }
    }

    private static void TestBlueprintHelpImages()
    {
        var expected = new Dictionary<string, string>
        {
            ["MAPPER_STAIR1"] = "stairs_help.png",
            ["MAPPER_STAIR2"] = "stairs_help.png",
            ["MAPPER_STAIR3"] = "stairs_help.png",
            ["MAPPER_STAIR4"] = "stairs_help.png",
            ["MAPPER_STAIR5"] = "stairs_help.png",
            ["MAPPER_STAIR6"] = "stairs_help.png",
            ["MAPPER_HOVEL"] = "ST02_House.png",
            ["MAPPER_WOODSMAN"] = "ST03_Woodcutters_Hut.png",
            ["MAPPER_OXENBASE"] = "ST04_Oxen_Base.png",
            ["MAPPER_IRON_MINE"] = "ST05_Iron_Mine.png",
            ["MAPPER_PITCH_WORKINGS"] = "ST06_Pitch_Digger.png",
            ["MAPPER_HUNTER"] = "ST07_Hunters_Hut.png",
            ["MAPPER_BARRACKS_WOOD"] = "ST08_Mercenary_Post.png",
            ["MAPPER_BARRACKS_STONE"] = "ST08_Barracks.png",
            ["MAPPER_BEDOUIN_STOCKADE"] = "ST08_Bedouin_Stockade.png",
            ["MAPPER_FLETCHER"] = "ST12_Fletchers_Workshop.png",
            ["MAPPER_BLACKSMITH"] = "ST13_Blacksmiths_Workshop.png",
            ["MAPPER_POLETURNER"] = "ST14_Poleturners_Workshop.png",
            ["MAPPER_ARMOURER"] = "ST15_Armourers_Workshop.png",
            ["MAPPER_TANNER"] = "ST16_Tanners_Workshop.png",
            ["MAPPER_BAKER"] = "ST17_Bakers_Workshop.png",
            ["MAPPER_BREWER"] = "ST18_Brewers_Workshop.png",
            ["MAPPER_GRANARY"] = "ST19_Granary.png",
            ["MAPPER_QUARRY"] = "ST20_Quarry.png",
            ["MAPPER_INN"] = "ST22_Inn.png",
            ["MAPPER_HEALER"] = "ST23_Healer.png",
            ["MAPPER_ENGINEERS_GUILD"] = "ST24_Engineers_Guild.png",
            ["MAPPER_TUNNELERS_GUILD"] = "ST25_Tunnellers_Guild.png",
            ["MAPPER_TRADEPOST"] = "ST26_Tradepost.png",
            ["MAPPER_WELL"] = "ST27_well.png",
            ["MAPPER_OIL_SMELTER"] = "ST28_Oil_Smelter.png",
            ["MAPPER_WHEATFARM"] = "ST30_Wheatfarm.png",
            ["MAPPER_HOPSFARM"] = "ST31_Hopsfarm.png",
            ["MAPPER_APPLEFARM"] = "ST32_Applefarm.png",
            ["MAPPER_CATTLEFARM"] = "ST33_Cattlefarm.png",
            ["MAPPER_MILL"] = "ST34_Mill.png",
            ["MAPPER_STABLES"] = "ST35_Stables.png",
            ["MAPPER_CHURCH3"] = "ST36_Church.png",
            ["MAPPER_GATE_STONE2A"] = "ST45_Gate_Main.png",
            ["MAPPER_DRAWBRIDGE"] = "ST49_Drawbridge.png",
            ["MAPPER_GALLOWS"] = "ST62_Gallows.png",
            ["MAPPER_MAYPOLE"] = "ST65_Maypole.png",
            ["MAPPER_TOWER1"] = "ST74_Tower1.png",
            ["MAPPER_TOWER2"] = "ST74_Tower2.png",
            ["MAPPER_TOWER3"] = "ST74_Tower3.png",
            ["MAPPER_TOWER4"] = "ST74_Tower4.png",
            ["MAPPER_TOWER5"] = "ST74_Tower5.png",
            ["MAPPER_DOG_CAGE"] = "st99_dog_cage.png",
            ["MAPPER_WATERPOT"] = "st70_Water_Pot.png"
        };
        foreach ((string mapper, string fileName) in expected)
        {
            AssertEqual(
                fileName,
                SpawnCastle.BlueprintBuildingIconCatalog
                    .ResolveDefinition(mapper)!
                    .HelpImageFileName);
        }

        foreach (string mapper in new[]
        {
            "MAPPER_STORES",
            "MAPPER_ARMOURY",
            "MAPPER_CHURCH1",
            "MAPPER_CHURCH2",
            "MAPPER_KILLING_PIT",
            "MAPPER_PITCH_DITCH",
            "MAPPER_GATE_STONE1A",
            "MAPPER_GATE_STONE1B",
            "MAPPER_GATE_STONE2B"
        })
        {
            AssertEqual<string?>(
                null,
                SpawnCastle.BlueprintBuildingIconCatalog
                    .ResolveDefinition(mapper)!
                    .HelpImageFileName);
        }

        AssertEqual(
            SpawnCastle.BlueprintHelpImageCleanup.RemoveTannerArtifacts,
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDefinition("MAPPER_TANNER")!
                .Cleanup);

        // HUD_Main.xaml exposes these three build buttons only in the editor.
        AssertEqual(
            "UI-Buildings N071",
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDefinition("MAPPER_OUTPOST_ARAB")!
                .BuildMenuResourceKey);
        AssertEqual(
            "UI-Buildings N073",
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDefinition("MAPPER_OUTPOST")!
                .BuildMenuResourceKey);
        AssertEqual(
            "UI-Buildings N075",
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDefinition("MAPPER_OUTPOST_BEDOUIN")!
                .BuildMenuResourceKey);
    }

    private static void TestChurchBlueprintSkins()
    {
        SpawnCastle.BlueprintBuildingIconDefinition church1 =
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDefinition("MAPPER_CHURCH1")!;
        SpawnCastle.BlueprintBuildingIconDefinition church2 =
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDefinition("MAPPER_CHURCH2")!;
        SpawnCastle.BlueprintBuildingIconDefinition church3 =
            SpawnCastle.BlueprintBuildingIconCatalog
                .ResolveDefinition("MAPPER_CHURCH3")!;
        AssertEqual("UI-Buildings F003", church1.ResolveBuildMenuResource(false));
        AssertEqual("UI-Buildings F003a", church1.ResolveBuildMenuResource(true));
        AssertEqual("UI-Buildings F005", church2.ResolveBuildMenuResource(false));
        AssertEqual("UI-Buildings F005a", church2.ResolveBuildMenuResource(true));
        AssertEqual("UI-Buildings F007", church3.ResolveBuildMenuResource(false));
        AssertEqual("UI-Buildings F007a", church3.ResolveBuildMenuResource(true));
        AssertEqual("ST36_Church.png", church3.ResolveHelpImage(false));
        AssertEqual("ST100_Mosque.png", church3.ResolveHelpImage(true));

        for (int lordType = 0; lordType <= 8; lordType++)
        {
            bool expected =
                lordType == 1 ||
                lordType == 2 ||
                lordType == 6 ||
                lordType == 7;
            AssertEqual(
                expected,
                SpawnCastle.BlueprintBuildingIconCatalog
                    .IsIslamicLordType(lordType));
        }
    }

    private static void TestBlueprintSourceScale()
    {
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateNormalWorldScale(
                "MAPPER_GRANARY",
                4f,
                3f,
                4f,
                3f,
                true,
                out float helpScale));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateNormalWorldScale(
                "MAPPER_GRANARY",
                4f,
                3f,
                4f,
                3f,
                false,
                out float buildMenuScale));
        AssertEqual(1f, helpScale);
        AssertEqual(1.1f, buildMenuScale);

        var reservedSources = new[]
        {
            ("MAPPER_BARRACKS_STONE", 5f, 1f),
            ("MAPPER_BARRACKS_WOOD", 4f, 1.25f),
            ("MAPPER_BEDOUIN_STOCKADE", 4f, 1.25f),
            ("MAPPER_ENGINEERS_GUILD", 4f, 1.25f),
            ("MAPPER_TUNNELERS_GUILD", 4f, 1.25f),
            ("MAPPER_OIL_SMELTER", 253f / 64f, 1f)
        };
        foreach ((
            string mapperName,
            float sourceWidth,
            float expectedScale) in reservedSources)
        {
            AssertEqual(
                true,
                SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateNormalWorldScale(
                    mapperName,
                    sourceWidth,
                    4f,
                    10f,
                    8f,
                    true,
                    out float reservedScale));
            AssertEqual(expectedScale, reservedScale);
        }

        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateNormalWorldScale(
                "MAPPER_OUTPOST",
                4f,
                3f,
                0f,
                0f,
                false,
                out float missingScale));
        AssertEqual(0f, missingScale);

        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateFootprintEstimatedScale(
                    5,
                    4f,
                    out float estimatedScale));
        AssertEqual(1.25f, estimatedScale);
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateFootprintEstimatedScale(
                    0,
                    4f,
                    out estimatedScale));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateFootprintEstimatedScale(
                    5,
                    float.NaN,
                    out estimatedScale));

        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .TryCalculateNormalWorldScale(
                    "MAPPER_OUTPOST",
                    140f / 64f,
                    143f / 64f,
                    5f,
                    4.078125f,
                    false,
                    out float outpostScale));
        Assert(outpostScale > 0f, "A measured Outpost scale must be usable.");
    }

    private static void TestBlueprintCalibrationRules()
    {
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_BARRACKS_STONE"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_ENGINEERS_GUILD"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_BEDOUIN_STOCKADE"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_TUNNELERS_GUILD"));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_OIL_SMELTER"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_GRANARY"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_OUTPOST"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_OUTPOST_ARAB"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .HasReservedPlacementArea("MAPPER_OUTPOST_BEDOUIN"));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsUsableCalibrationRevision(1));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsUsableCalibrationRevision(2));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsUsableCalibrationRevision(3));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsUsableCalibrationRevision(4));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsUsableGroundOffsetRevision(4));
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsUsableGroundOffsetRevision(5));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsUsableGroundOffsetRevision(6));
    }

    private static void TestReservedBlueprintGrounds()
    {
        var cases = new[]
        {
            (Mapper: 87, Tiles: 100, CoreSize: 5, MaximumX: 399, MaximumY: 419),
            (Mapper: 86, Tiles: 100, CoreSize: 5, MaximumX: 399, MaximumY: 419),
            (Mapper: 79, Tiles: 100, CoreSize: 5, MaximumX: 399, MaximumY: 419),
            (Mapper: 88, Tiles: 50, CoreSize: 5, MaximumX: 394, MaximumY: 419),
            (Mapper: 89, Tiles: 50, CoreSize: 5, MaximumX: 394, MaximumY: 419),
            (Mapper: 180, Tiles: 32, CoreSize: 4, MaximumX: 393, MaximumY: 417)
        };

        foreach ((
            int mapper,
            int tiles,
            int coreSize,
            int maximumX,
            int maximumY) in cases)
        {
            var document = new SpawnCastle.AivJsonDocument
            {
                frames =
                [
                    new SpawnCastle.AivJsonFrame
                    {
                        itemType = 60,
                        tilePositionOfsets = [5050]
                    },
                    new SpawnCastle.AivJsonFrame
                    {
                        itemType = mapper,
                        tilePositionOfsets = [4040]
                    }
                ],
                miscItems = []
            };
            SpawnCastle.BlueprintLayout layout =
                SpawnCastle.BlueprintLayoutBuilder.Build(document, 400, 400);
            SpawnCastle.BlueprintIconPlacement icon = layout.Icons.Single();

            AssertEqual(tiles, layout.Tiles.Count);
            AssertEqual(coreSize, icon.Size);
            AssertEqual(390, icon.MinimumWorldX);
            AssertEqual(390 + coreSize - 1, icon.MaximumWorldX);
            AssertEqual(410, icon.MinimumWorldY);
            AssertEqual(410 + coreSize - 1, icon.MaximumWorldY);
            AssertEqual(maximumX, layout.Tiles.Max(tile => tile.Tile.X));
            AssertEqual(maximumY, layout.Tiles.Max(tile => tile.Tile.Y));
            AssertEqual(
                layout.Tiles.Min(tile => tile.Tile.X),
                icon.MarkerMinimumWorldX);
            AssertEqual(
                layout.Tiles.Max(tile => tile.Tile.X),
                icon.MarkerMaximumWorldX);
            AssertEqual(
                layout.Tiles.Min(tile => tile.Tile.Y),
                icon.MarkerMinimumWorldY);
            AssertEqual(
                layout.Tiles.Max(tile => tile.Tile.Y),
                icon.MarkerMaximumWorldY);
        }
    }

    private static void TestBlueprintPreviewSpriteFilter()
    {
        AssertEqual(
            false,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsExtendedPreviewSprite(64f, 32f));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsExtendedPreviewSprite(65f, 32f));
        AssertEqual(
            true,
            SpawnCastle.BlueprintBuildingIconCatalog
                .IsExtendedPreviewSprite(64f, 33f));
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

    private static void AssertCrLfString(string value)
    {
        string withoutCrLf = value.Replace("\r\n", string.Empty);
        Assert(
            !withoutCrLf.Contains('\r') && !withoutCrLf.Contains('\n'),
            "Serialized manifest contains non-CRLF line endings.");
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
