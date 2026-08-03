using AIVParser.Core;
using AIVPlacement.Core;
using AIVPlacement.OracleComparison;
using MapParser.Core;

internal static class Program
{
    private static readonly AivCastleProjector Projector = new();
    private static readonly AivPlacementRuleEvaluator RuleEvaluator = new();
    private static readonly AivPlacementEvaluator PlacementEvaluator = new();

    private static int Main()
    {
        (string Name, Action Test)[] tests =
        {
            ("Project all four rotations", TestRotations),
            ("Use the native fixed keep reference", TestFixedNativeKeepReference),
            ("Project asymmetric building footprints", TestAsymmetricFootprints),
            ("Retain coordinates beyond the map edge", TestNearMapEdge),
            ("Project gates, drawbridges and stairs", TestSpecialElements),
            ("Keep overlapping elements traceable", TestOverlappingElements),
            ("Preserve build steps, pauses and anchor-only entries", TestBuildOrderAndNonPlacements),
            ("Project associated blocked areas separately", TestBlockedAreas),
            ("Require an exact AIV keep anchor", TestMissingKeep),
            ("Retain placement issue evidence", TestPlacementIssueEvidence),
            ("Normalize serialized player start occupancy", TestPreplacementMapState),
            ("Rotate rebuilt player start occupancy", TestRebuiltStartRotations),
            ("Reconstruct native rock footprints", TestRockFootprintReconstruction),
            ("Propagate only placed prior AIV elements", TestPriorCastleMapState),
            ("Reject reasonless placement issues", TestReasonlessPlacementIssue),
            ("Distinguish native-domain and diamond failures", TestGeometryRules),
            ("Apply the native mapper height limit", TestHeightRule),
            ("Report the exact blocked tile in a multi-tile building", TestBuildingRule),
            ("Apply proven logic masks and mapper profiles", TestTerrainRules),
            ("Allow organisms in native Skirmish mode", TestSkirmishOrganismRule),
            ("Reject existing walls as owner conflicts", TestOwnerRule),
            ("Detect internal overlaps in build order", TestInternalOverlapRule),
            ("Evaluate associated areas separately from core footprints", TestAssociatedAreaRule),
            ("Produce deterministic element results", TestDeterministicEvaluation),
            ("Build opt-in per-cell Oracle diagnostics", TestOfflineCaseDiagnostic),
            ("Aggregate complete, partial and impossible candidates", TestCandidateStatuses),
            ("Keep unresolved candidates not evaluable", TestCandidateNotEvaluable),
            ("Select the first complete rotation in native order", TestCompleteRotationSelection),
            ("Apply the native alternative-rotation threshold", TestPartialRotationSelection)
        };

        int failures = 0;
        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name + ": " + ex.Message);
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void TestRotations()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(56, 43)),
            Frame(1, 25, false, Point(53, 47)));

        AssertCoordinate(Element(blueprint, AivRotation.Degrees0, 1).MapCoordinate, 404, 403);
        AssertCoordinate(Element(blueprint, AivRotation.Degrees90, 1).MapCoordinate, 403, 409);
        AssertCoordinate(Element(blueprint, AivRotation.Degrees180, 1).MapCoordinate, 409, 410);
        AssertCoordinate(Element(blueprint, AivRotation.Degrees270, 1).MapCoordinate, 410, 404);
    }

    private static void TestFixedNativeKeepReference()
    {
        var frames = new[]
        {
            Frame(0, 61, false, Point(55, 44)),
            Frame(15, 110, false, Point(94, 6))
        };
        var blueprint = new AivBlueprint(
            "synthetic-shifted-keep",
            5,
            frames,
            Array.Empty<AivMiscPlacement>(),
            Point(55, 44));

        AivProjectedElement pitchRig = Projector.Project(
            blueprint,
            new MapCoordinate(369, 528),
            AivRotation.Degrees180).Elements[1];

        AssertCoordinate(pitchRig.MapCoordinate, 419, 579);
        Assert(pitchRig.OccupiedTiles.Any(tile =>
            tile.MapCoordinate.Equals(new MapCoordinate(419, 579))),
            "The native first-blocking Testlord cell was not projected.");
    }

    private static void TestAsymmetricFootprints()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(56, 43)),
            Frame(1, 50, false, Point(56, 45)));

        AivProjectedElement zero = Element(blueprint, AivRotation.Degrees0, 1);
        AssertEqual(16, zero.OccupiedTiles.Count);
        AssertBounds(zero.OccupiedTiles, 402, 405, 400, 403);

        AivProjectedElement ninety = Element(blueprint, AivRotation.Degrees90, 1);
        AssertEqual(16, ninety.OccupiedTiles.Count);
        AssertBounds(ninety.OccupiedTiles, 400, 403, 408, 411);
    }

    private static void TestNearMapEdge()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(56, 43)),
            Frame(1, 25, false, Point(66, 33)));
        AivProjectedCastle projected = Projector.Project(
            blueprint,
            new MapCoordinate(1, 1),
            AivRotation.Degrees0);

        AivProjectedElement wall = projected.Elements[1];
        AssertCoordinate(wall.MapCoordinate, -9, -9);
        AssertEqual(1, wall.OccupiedTiles.Count);
        AssertCoordinate(wall.OccupiedTiles[0].MapCoordinate, -9, -9);
    }

    private static void TestSpecialElements()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(50, 50)),
            Frame(1, 146, false, Point(54, 55)),
            Frame(2, 105, false, Point(45, 40)),
            Frame(3, 181, false, Point(48, 53), Point(47, 53), Point(46, 53)));
        AivProjectedCastle projected = Projector.Project(
            blueprint,
            new MapCoordinate(400, 400),
            AivRotation.Degrees270);

        AssertEqual(49, projected.BuildSteps[1].Elements[0].OccupiedTiles.Count);
        AssertEqual(25, projected.BuildSteps[2].Elements[0].OccupiedTiles.Count);
        AssertEqual(3, projected.BuildSteps[3].Elements.Count);
        foreach (AivProjectedElement stair in projected.BuildSteps[3].Elements)
        {
            AssertEqual(AivItemCategory.Stair, stair.Mapper.Category);
            AssertEqual(1, stair.OccupiedTiles.Count);
        }
    }

    private static void TestOverlappingElements()
    {
        AivGridPoint shared = Point(52, 48);
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(50, 50)),
            Frame(1, 25, false, shared),
            Frame(2, 46, false, shared));
        AivProjectedCastle projected = Projector.Project(
            blueprint,
            new MapCoordinate(400, 400),
            AivRotation.Degrees0);

        AivProjectedTile first = projected.Elements[1].OccupiedTiles[0];
        AivProjectedTile second = projected.Elements[2].OccupiedTiles[0];
        AssertEqual(first.MapCoordinate, second.MapCoordinate);
        AssertEqual(1, first.ElementIndex);
        AssertEqual(2, second.ElementIndex);
        AssertEqual(2, projected.OccupiedTiles.Count(tile =>
            tile.MapCoordinate.Equals(first.MapCoordinate)));
    }

    private static void TestBuildOrderAndNonPlacements()
    {
        var frames = new[]
        {
            Frame(12, 61, false, Point(50, 50)),
            Frame(3, 25, true),
            Frame(99, 999, false, Point(51, 51))
        };
        var blueprint = new AivBlueprint(
            "synthetic-order",
            17,
            frames,
            Array.Empty<AivMiscPlacement>(),
            Point(50, 50));
        AivProjectedCastle projected = Projector.Project(
            blueprint,
            new MapCoordinate(400, 400),
            AivRotation.Degrees0);

        AssertEqual(12, projected.BuildSteps[0].BuildIndex);
        AssertEqual(3, projected.BuildSteps[1].BuildIndex);
        Assert(projected.BuildSteps[1].ShouldPause, "Pause metadata was lost.");
        AssertEqual(17, projected.BuildSteps[1].PauseDelayAmount);
        Assert(!projected.BuildSteps[1].HasPlacements, "An empty frame must not occupy tiles.");
        AssertEqual(99, projected.BuildSteps[2].BuildIndex);
        AssertEqual(AivProjectedElementKind.AnchorOnly, projected.BuildSteps[2].Elements[0].Kind);
        AssertEqual(0, projected.BuildSteps[2].Elements[0].OccupiedTiles.Count);
    }

    private static void TestBlockedAreas()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(50, 50)),
            Frame(1, 87, false, Point(48, 52)));
        AivProjectedElement barracks = Element(blueprint, AivRotation.Degrees90, 1);
        AivProjectedElement keep = Element(blueprint, AivRotation.Degrees0, 0);

        AssertEqual(126, keep.OccupiedTiles.Count);
        AssertEqual(77, keep.OccupiedTiles.Count(tile =>
            tile.Kind == AivProjectedTileKind.AssociatedBlockedArea));
        AssertEqual(100, barracks.OccupiedTiles.Count);
        AssertEqual(25, barracks.OccupiedTiles.Count(tile =>
            tile.Kind == AivProjectedTileKind.CoreFootprint));
        AssertEqual(75, barracks.OccupiedTiles.Count(tile =>
            tile.Kind == AivProjectedTileKind.AssociatedBlockedArea));
        AssertEqual(3, barracks.OccupiedTiles
            .Where(tile => tile.Kind == AivProjectedTileKind.AssociatedBlockedArea)
            .Select(tile => tile.AssociatedAreaName)
            .Distinct()
            .Count());
        Assert(barracks.OccupiedTiles.All(tile => tile.ElementIndex == barracks.OriginalIndex),
            "A blocked tile lost its source element index.");
    }

    private static void TestMissingKeep()
    {
        var blueprint = new AivBlueprint(
            "no-keep",
            0,
            new[] { Frame(0, 25, false, Point(50, 50)) },
            Array.Empty<AivMiscPlacement>(),
            null);
        AssertThrows<ArgumentException>(() => Projector.Project(
            blueprint,
            new MapCoordinate(400, 400),
            AivRotation.Degrees0));
    }

    private static void TestPlacementIssueEvidence()
    {
        var evidence = new AivPlacementTileEvidence(
            unchecked((int)0xA0018400),
            12,
            22,
            32,
            102,
            202,
            302,
            4);
        var issue = new AivPlacementIssue(
            AivPlacementIssueKind.BuildingOccupied | AivPlacementIssueKind.TerrainBlocked,
            7,
            11,
            87,
            AivProjectedTileKind.AssociatedBlockedArea,
            new MapCoordinate(410, 390),
            12345,
            evidence,
            3);

        AssertEqual(7, issue.ElementIndex);
        AssertEqual(11, issue.BuildIndex);
        AssertEqual(87, issue.MapperValue);
        AssertEqual((int?)12345, issue.TileId);
        AssertEqual((int?)3, issue.ConflictingElementIndex);
        Assert(issue.Kind.HasFlag(AivPlacementIssueKind.BuildingOccupied),
            "The building reason was lost.");
        Assert(issue.Kind.HasFlag(AivPlacementIssueKind.TerrainBlocked),
            "The terrain reason was lost.");
        AssertEqual(unchecked((int)0xA0018400), issue.TileEvidence!.Value.TerrainFlags);
        AssertEqual((ushort)302, issue.TileEvidence.Value.EntityId);
        AssertEqual((byte)4, issue.TileEvidence.Value.OwnerId);
    }

    private static void TestPreplacementMapState()
    {
        MapCoordinate keepTile = new(400, 400);
        MapCoordinate adjacentWallTile = new(401, 400);
        MapCoordinate diagonalWallTile = new(401, 401);
        MapCoordinate otherTile = new(405, 400);
        var raw = new SparsePlacementMap();
        raw.Set(keepTile, Evidence(
            terrainFlags: unchecked((int)0x10008500),
            secondaryLogic: 2,
            height: 8,
            defaultHeight: 7,
            organismId: 3,
            buildingId: 28,
            entityId: 4,
            ownerId: 5));
        raw.Set(adjacentWallTile, Evidence(
            terrainFlags: 0x00008100,
            ownerId: 5));
        raw.Set(diagonalWallTile, Evidence(
            terrainFlags: 0x00008100,
            ownerId: 5));
        raw.Set(otherTile, Evidence(
            terrainFlags: unchecked((int)0x10000400),
            buildingId: 29));

        var normalized = new AivPreplacementMapState(
            raw,
            new ushort[] { 28 },
            Array.Empty<ushort>(),
            Array.Empty<MapRockRecord>());
        AivPlacementTileEvidence keep = normalized.GetTileEvidence(
            normalized.Geometry.GetTileId(keepTile.X, keepTile.Y));
        AivPlacementTileEvidence other = normalized.GetTileEvidence(
            normalized.Geometry.GetTileId(otherTile.X, otherTile.Y));
        AivPlacementTileEvidence adjacentWall = normalized.GetTileEvidence(
            normalized.Geometry.GetTileId(adjacentWallTile.X, adjacentWallTile.Y));
        AivPlacementTileEvidence diagonalWall = normalized.GetTileEvidence(
            normalized.Geometry.GetTileId(diagonalWallTile.X, diagonalWallTile.Y));

        AssertEqual(0x00008000, keep.TerrainFlags);
        AssertEqual((ushort)0, keep.BuildingId);
        AssertEqual((byte)2, keep.SecondaryLogic);
        AssertEqual((byte)8, keep.Height);
        AssertEqual((byte)7, keep.DefaultHeight);
        AssertEqual((ushort)3, keep.OrganismId);
        AssertEqual((ushort)4, keep.EntityId);
        AssertEqual((byte)0, keep.OwnerId);
        AssertEqual(0x00008000, adjacentWall.TerrainFlags);
        AssertEqual((byte)0, adjacentWall.OwnerId);
        AssertEqual(0x00008000, diagonalWall.TerrainFlags);
        AssertEqual((byte)0, diagonalWall.OwnerId);
        AssertEqual(unchecked((int)0x10000400), other.TerrainFlags);
        AssertEqual((ushort)29, other.BuildingId);
        AssertEqual(1, normalized.NormalizedStartBuildingIds.Count);
        AssertEqual((ushort)28, normalized.NormalizedStartBuildingIds[0]);
        AssertEqual(0, normalized.RetainedStartBuildingIds.Count);
        AivPlacementTileEvidence originalKeep = normalized.GetOriginalTileEvidence(
            normalized.Geometry.GetTileId(keepTile.X, keepTile.Y));
        AssertEqual((ushort)28, originalKeep.BuildingId);
        AivStartBuildingAdjacency adjacency = normalized.GetStartBuildingAdjacency(
            normalized.Geometry.GetTileId(adjacentWallTile.X, adjacentWallTile.Y));
        AssertEqual(1, adjacency.OrthogonalNeighborCount);
        AssertEqual(0, adjacency.DiagonalNeighborCount);

        var retained = new AivPreplacementMapState(
            raw,
            new ushort[] { 28 },
            new ushort[] { 28 },
            Array.Empty<MapRockRecord>(),
            new Dictionary<ushort, AivTileOccupancyKind>
            {
                [28] = AivTileOccupancyKind.PlayerStartKeep
            });
        AivPlacementTileEvidence retainedKeep = retained.GetTileEvidence(
            retained.Geometry.GetTileId(keepTile.X, keepTile.Y));
        AssertEqual((ushort)28, retainedKeep.BuildingId);
        AssertEqual(AivTileOccupancyKind.PlayerStartKeep, retainedKeep.Occupancies[0].Kind);
        AssertEqual(0x00008100, retained.GetTileEvidence(
            retained.Geometry.GetTileId(adjacentWallTile.X, adjacentWallTile.Y)).TerrainFlags);
        AssertEqual((byte)5, retained.GetTileEvidence(
            retained.Geometry.GetTileId(adjacentWallTile.X, adjacentWallTile.Y)).OwnerId);
        AssertEqual(0, retained.NormalizedStartBuildingIds.Count);
        AssertEqual((ushort)28, retained.RetainedStartBuildingIds[0]);
    }

    private static void TestRockFootprintReconstruction()
    {
        MapCoordinate coordinate = new(399, 403);
        var raw = new SparsePlacementMap();
        raw.Set(coordinate, Evidence(
            terrainFlags: 0x00001000,
            height: 78,
            defaultHeight: 78,
            organismId: 208));
        var rock = new MapRockRecord(
            27, 257, 161598, 2750, 1, 56, 16, 399, 401, 4, 2);
        var normalized = new AivPreplacementMapState(
            raw,
            Array.Empty<ushort>(),
            Array.Empty<ushort>(),
            new[] { rock });
        int tileId = normalized.Geometry.GetTileId(coordinate.X, coordinate.Y);

        AivPlacementTileEvidence evidence = normalized.GetTileEvidence(tileId);
        AssertEqual(0x00001080, evidence.TerrainFlags);
        AssertEqual((ushort)4027, evidence.OrganismId);
        AssertEqual((byte)78, evidence.Height);
        AssertEqual((ushort)208, normalized.GetOriginalTileEvidence(tileId).OrganismId);

        AivElementPlacementResult result = RuleEvaluator.EvaluateElement(
            normalized,
            ElementAt(106, coordinate));
        Assert(result.Issues.Any(issue =>
                issue.MapCoordinate.Equals(coordinate) &&
                issue.Kind.HasFlag(AivPlacementIssueKind.TerrainBlocked)),
            "The reconstructed rock cell was not rejected as native impassable terrain.");
    }

    private static void TestPriorCastleMapState()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(56, 43)),
            Frame(1, 50, false, Point(56, 60)),
            Frame(2, 105, false, Point(56, 70)),
            Frame(3, 50, false, Point(56, 80)));
        var evaluationMap = new SparsePlacementMap();
        evaluationMap.Set(new MapCoordinate(417, 400), Evidence(buildingId: 77));
        AivPlacementResult prior = PlacementEvaluator.Evaluate(
            evaluationMap,
            blueprint,
            new MapCoordinate(400, 400),
            AivRotation.Degrees0);
        var map = new AivProjectedPrebuildMapState(
            new SparsePlacementMap(),
            new[] { new AivProjectedPrebuildPlacement("test-session", 2, prior) });

        int blockedElementTileId = map.Geometry.GetTileId(420, 403);
        int drawbridgeTileId = map.Geometry.GetTileId(427, 400);
        int occupiedTileId = map.Geometry.GetTileId(437, 400);
        AssertEqual((ushort)0, map.GetTileEvidence(blockedElementTileId).BuildingId);
        AivPlacementTileEvidence drawbridge = map.GetTileEvidence(drawbridgeTileId);
        AssertEqual((ushort)0, drawbridge.BuildingId);
        AssertEqual(AivTileOccupancyKind.ProjectedPrebuiltAivBuilding, drawbridge.Occupancies[0].Kind);
        AivPlacementTileEvidence prebuilt = map.GetTileEvidence(occupiedTileId);
        AssertEqual((ushort)0, prebuilt.BuildingId);
        AssertEqual(1, prebuilt.Occupancies.Count);
        AssertEqual("test-session", prebuilt.Occupancies[0].SessionId);
        AssertEqual(2, prebuilt.Occupancies[0].PlayerId);
        AssertEqual(50, prebuilt.Occupancies[0].MapperValue);
        AssertEqual(AivTileOccupancyKind.ProjectedPrebuiltAivBuilding, prebuilt.Occupancies[0].Kind);

        AivElementPlacementResult blockedByPlan = RuleEvaluator.EvaluateElement(
            map,
            ElementAt(50, new MapCoordinate(437, 400)));
        Assert(blockedByPlan.Issues.Any(issue =>
                issue.Kind.HasFlag(AivPlacementIssueKind.ProjectedPriorAivPrebuildOccupied) &&
                !issue.Kind.HasFlag(AivPlacementIssueKind.BuildingOccupied)),
            "A prior prebuilt AIV cell was reported as a serialized map building.");

        AssertThrows<ArgumentException>(() => new AivProjectedPrebuildMapState(
            new SparsePlacementMap(),
            new[]
            {
                new AivProjectedPrebuildPlacement("session-a", 2, prior),
                new AivProjectedPrebuildPlacement("session-b", 3, prior)
            }));
    }

    private static void TestReasonlessPlacementIssue()
    {
        AssertThrows<ArgumentOutOfRangeException>(() => new AivPlacementIssue(
            AivPlacementIssueKind.None,
            0,
            0,
            25,
            AivProjectedTileKind.CoreFootprint,
            new MapCoordinate(400, 400),
            160400,
            null));
    }

    private static void TestGeometryRules()
    {
        var map = new SparsePlacementMap();

        AivElementPlacementResult outside = RuleEvaluator.EvaluateElement(
            map,
            ElementAt(25, new MapCoordinate(-1, 399)));
        AssertOnlyIssue(outside, AivElementPlacementStatus.Blocked,
            AivPlacementIssueKind.OutsideMap);
        AssertEqual((int?)null, outside.Issues[0].TileId);

        AivElementPlacementResult invalid = RuleEvaluator.EvaluateElement(
            map,
            ElementAt(25, new MapCoordinate(0, 0)));
        AssertOnlyIssue(invalid, AivElementPlacementStatus.Blocked,
            AivPlacementIssueKind.InvalidMapTile);
        AssertEqual((int?)null, invalid.Issues[0].TileId);

        AivElementPlacementResult valid = RuleEvaluator.EvaluateElement(
            map,
            ElementAt(25, new MapCoordinate(400, 400)));
        AssertEqual(AivElementPlacementStatus.Placeable, valid.Status);
        AssertEqual(0, valid.Issues.Count);

    }

    private static void TestHeightRule()
    {
        MapCoordinate coordinate = new(400, 400);
        var acceptedMap = new SparsePlacementMap();
        acceptedMap.Set(coordinate, Evidence(height: 200));
        AivElementPlacementResult accepted = RuleEvaluator.EvaluateElement(
            acceptedMap,
            ElementAt(25, coordinate));
        AssertEqual(AivElementPlacementStatus.Placeable, accepted.Status);

        var rejectedMap = new SparsePlacementMap();
        rejectedMap.Set(coordinate, Evidence(height: 201));
        AivElementPlacementResult rejected = RuleEvaluator.EvaluateElement(
            rejectedMap,
            ElementAt(25, coordinate));
        AssertOnlyIssue(rejected, AivElementPlacementStatus.Blocked,
            AivPlacementIssueKind.HeightMismatch);
        AssertEqual((byte)201, rejected.Issues[0].TileEvidence!.Value.Height);
    }

    private static void TestBuildingRule()
    {
        AivProjectedElement element = ElementAt(50, new MapCoordinate(400, 400));
        AivProjectedTile blockedTile = element.OccupiedTiles[5];
        var map = new SparsePlacementMap();
        map.Set(blockedTile.MapCoordinate, Evidence(buildingId: 77));

        AivElementPlacementResult result = RuleEvaluator.EvaluateElement(map, element);
        AssertOnlyIssue(result, AivElementPlacementStatus.Blocked,
            AivPlacementIssueKind.BuildingOccupied);
        AssertEqual(blockedTile.MapCoordinate, result.Issues[0].MapCoordinate);
        AssertEqual((ushort)77, result.Issues[0].TileEvidence!.Value.BuildingId);

        AivElementPlacementResult free = RuleEvaluator.EvaluateElement(
            new SparsePlacementMap(),
            element);
        AssertEqual(AivElementPlacementStatus.Placeable, free.Status);
    }

    private static void TestTerrainRules()
    {
        AssertTerrainBlocked(50, 0x00000001); // Sea
        AssertTerrainBlocked(50, 0x00000004); // IsFarm
        AssertTerrainBlocked(99, 0x00000008); // PitchTrap only blocks its mapper
        AssertTerrainBlocked(50, 0x00000010); // RealityEdge
        AssertTerrainBlocked(50, 0x00000020); // MapBorder
        AssertTerrainBlocked(50, 0x00100000); // River
        AssertTerrainBlocked(50, 0x00000400); // IsBuilding
        AssertTerrainBlocked(50, 0x10000000); // IsElevated
        AssertTerrainBlocked(50, 0x01000000); // Farm type
        AssertTerrainBlocked(50, 0x00200000); // Ford
        AssertTerrainBlocked(50, 0x00000080); // Bare ImpassableEdge
        AssertTerrainBlocked(50, 0x20000000); // Swamp
        AssertTerrainBlocked(50, 0x40000000); // Moat

        AssertTerrainAccepted(50, 0x00000008);
        AssertTerrainAccepted(195, 0x00000001 | 0x00100000);
        AssertTerrainBlocked(51, 0x00000080);
        AssertTerrainBlocked(113, 0x00000080);
        AssertTerrainAccepted(91, 0x20000000);
        AssertTerrainAccepted(105, 0x40000000);
    }

    private static void TestSkirmishOrganismRule()
    {
        MapCoordinate coordinate = new(400, 400);
        var organismMap = new SparsePlacementMap();
        organismMap.Set(coordinate, Evidence(
            terrainFlags: 0x00001000,
            organismId: 12));
        AivElementPlacementResult organism = RuleEvaluator.EvaluateElement(
            organismMap,
            ElementAt(25, coordinate));
        AssertEqual(AivElementPlacementStatus.Placeable, organism.Status);

        var noRecordMap = new SparsePlacementMap();
        noRecordMap.Set(coordinate, Evidence(terrainFlags: 0x00001000));
        AivElementPlacementResult noRecord = RuleEvaluator.EvaluateElement(
            noRecordMap,
            ElementAt(25, coordinate));
        AssertEqual(AivElementPlacementStatus.Placeable, noRecord.Status);

        var outOfRecordRangeMap = new SparsePlacementMap();
        outOfRecordRangeMap.Set(coordinate, Evidence(
            terrainFlags: 0x00001000,
            organismId: 4000));
        AivElementPlacementResult outOfRecordRange = RuleEvaluator.EvaluateElement(
            outOfRecordRangeMap,
            ElementAt(25, coordinate));
        AssertEqual(AivElementPlacementStatus.Placeable, outOfRecordRange.Status);

        var entityOnlyMap = new SparsePlacementMap();
        entityOnlyMap.Set(coordinate, Evidence(entityId: 41));
        AivElementPlacementResult entityOnly = RuleEvaluator.EvaluateElement(
            entityOnlyMap,
            ElementAt(25, coordinate));
        AssertEqual(AivElementPlacementStatus.Placeable, entityOnly.Status);

        var deterministicallyBlockedMap = new SparsePlacementMap();
        deterministicallyBlockedMap.Set(coordinate, Evidence(
            terrainFlags: 0x00001000,
            organismId: 12,
            buildingId: 2));
        AivElementPlacementResult blocked = RuleEvaluator.EvaluateElement(
            deterministicallyBlockedMap,
            ElementAt(25, coordinate));
        AssertEqual(AivElementPlacementStatus.Blocked, blocked.Status);
        Assert(blocked.Issues[0].Kind.HasFlag(AivPlacementIssueKind.BuildingOccupied),
            "The proven building block was lost.");
    }

    private static void TestOwnerRule()
    {
        MapCoordinate coordinate = new(400, 400);
        var map = new SparsePlacementMap();
        map.Set(coordinate, Evidence(terrainFlags: 0x00000100, ownerId: 5));

        AivElementPlacementResult result = RuleEvaluator.EvaluateElement(
            map,
            ElementAt(25, coordinate));
        AssertOnlyIssue(result, AivElementPlacementStatus.Blocked,
            AivPlacementIssueKind.OwnerConflict);
        AssertEqual((byte)5, result.Issues[0].TileEvidence!.Value.OwnerId);
    }

    private static void TestInternalOverlapRule()
    {
        AivGridPoint shared = Point(50, 50);
        AivBlueprint blueprint = Blueprint(
            Frame(0, 25, false, shared),
            Frame(1, 46, false, shared));
        AivProjectedCastle castle = Projector.Project(
            blueprint,
            new MapCoordinate(400, 400),
            AivRotation.Degrees0);

        IReadOnlyList<AivElementPlacementResult> results =
            RuleEvaluator.EvaluateElements(new SparsePlacementMap(), castle);
        AssertEqual(AivElementPlacementStatus.Placeable, results[0].Status);
        AssertOnlyIssue(results[1], AivElementPlacementStatus.Placeable,
            AivPlacementIssueKind.InternalOverlap);
        AssertEqual((int?)0, results[1].Issues[0].ConflictingElementIndex);
        AssertEqual(1, results[1].Issues[0].ElementIndex);

        AivPlacementResult placement = PlacementEvaluator.Evaluate(
            new SparsePlacementMap(),
            blueprint,
            new MapCoordinate(400, 400),
            AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Complete, placement.Status);
        AssertEqual(1, placement.Score.EvaluatedTileCount);
        AssertEqual(0, placement.Score.BlockedTileCount);
        OfflineCaseDiagnostic diagnostic = OfflineCaseDiagnosticBuilder.Build(
            new SparsePlacementMap(),
            placement);
        AssertEqual(1, diagnostic.EvaluatedCellCount);
        AssertEqual(0, diagnostic.BlockedCellCount);
        AssertEqual(1, diagnostic.Elements.Sum(item => item.Cells.Count));
        AssertEqual(
            1,
            diagnostic.Elements.Single(item => item.Cells.Count == 1).ElementIndex);
    }

    private static void TestAssociatedAreaRule()
    {
        AivProjectedElement barracks = ElementAt(87, new MapCoordinate(400, 400));
        AivProjectedTile associated = barracks.OccupiedTiles.First(candidate =>
            candidate.Kind == AivProjectedTileKind.AssociatedBlockedArea &&
            barracks.OccupiedTiles.Count(tile =>
                tile.MapCoordinate.Equals(candidate.MapCoordinate)) == 1);
        var map = new SparsePlacementMap();
        map.Set(associated.MapCoordinate, Evidence(buildingId: 4));

        AivElementPlacementResult result = RuleEvaluator.EvaluateElement(map, barracks);
        AssertOnlyIssue(result, AivElementPlacementStatus.Blocked,
            AivPlacementIssueKind.BuildingOccupied);
        AssertEqual(AivProjectedTileKind.AssociatedBlockedArea, result.Issues[0].TileKind);
        AssertEqual(associated.MapCoordinate, result.Issues[0].MapCoordinate);
    }

    private static void TestDeterministicEvaluation()
    {
        AivProjectedElement element = ElementAt(50, new MapCoordinate(400, 400));
        var map = new SparsePlacementMap();
        map.Set(element.OccupiedTiles[2].MapCoordinate,
            Evidence(terrainFlags: 0x00000020));
        map.Set(element.OccupiedTiles[9].MapCoordinate,
            Evidence(buildingId: 9));

        AivElementPlacementResult first = RuleEvaluator.EvaluateElement(map, element);
        AivElementPlacementResult second = RuleEvaluator.EvaluateElement(map, element);
        AssertEqual(first.Status, second.Status);
        AssertEqual(first.Issues.Count, second.Issues.Count);
        for (int index = 0; index < first.Issues.Count; index++)
        {
            AssertEqual(first.Issues[index].Kind, second.Issues[index].Kind);
            AssertEqual(first.Issues[index].MapCoordinate, second.Issues[index].MapCoordinate);
            AssertEqual(first.Issues[index].TileId, second.Issues[index].TileId);
        }
    }

    private static void TestOfflineCaseDiagnostic()
    {
        MapCoordinate keep = new(400, 400);
        AivBlueprint blueprint = Blueprint(Frame(7, 50, false, Point(50, 50)));
        AivProjectedElement projected = Projector.Project(
            blueprint,
            keep,
            AivRotation.Degrees90).Elements[0];
        AivProjectedTile blockedTile = projected.OccupiedTiles[0];
        var map = new SparsePlacementMap();
        map.Set(blockedTile.MapCoordinate, Evidence(
            terrainFlags: 0x00000010,
            secondaryLogic: 3,
            height: 9,
            defaultHeight: 8,
            buildingId: 42));

        AivPlacementResult result = PlacementEvaluator.Evaluate(
            map,
            blueprint,
            keep,
            AivRotation.Degrees90);
        OfflineCaseDiagnostic diagnostic = OfflineCaseDiagnosticBuilder.Build(map, result);

        AssertEqual(result.Score.EvaluatedTileCount, diagnostic.EvaluatedCellCount);
        AssertEqual(result.Score.BlockedTileCount, diagnostic.BlockedCellCount);
        AssertEqual(1, diagnostic.Elements.Count);
        OfflineElementDiagnostic element = diagnostic.Elements[0];
        AssertEqual(7, element.BuildIndex);
        AssertEqual(50, element.MapperValue);
        AssertEqual(AivRotation.Degrees90, element.Rotation);
        AssertEqual(result.Score.BlockedTileCount, element.BlockedCellCount);
        OfflineCellDiagnostic cell = element.Cells.Single(item => item.Blocked);
        AssertEqual(blockedTile.SourceAivCoordinate.Row, cell.SourceAivRow);
        AssertEqual(blockedTile.SourceAivCoordinate.Column, cell.SourceAivColumn);
        AssertEqual(blockedTile.MapCoordinate.X, cell.MapX);
        AssertEqual(blockedTile.MapCoordinate.Y, cell.MapY);
        AssertEqual((ushort?)42, cell.BuildingId);
        Assert(cell.IssueKind.HasFlag(AivPlacementIssueKind.BuildingOccupied),
            "The per-cell diagnostic lost the blocking reason.");
        Assert(!cell.WasPreplacementNormalized,
            "An unchanged synthetic cell was reported as normalized.");
    }

    private static void TestRebuiltStartRotations()
    {
        var keep = new MapCoordinate(400, 400);
        var source = new MapCoordinate(402, 414);

        AssertCoordinate(
            AivPreplacementMapState.TransformRebuiltStartCoordinate(
                source,
                keep,
                AivRotation.Degrees0),
            403,
            415);
        AssertCoordinate(
            AivPreplacementMapState.TransformRebuiltStartCoordinate(
                source,
                keep,
                AivRotation.Degrees90),
            415,
            410);
        AssertCoordinate(
            AivPreplacementMapState.TransformRebuiltStartCoordinate(
                source,
                keep,
                AivRotation.Degrees180),
            410,
            398);
        AssertCoordinate(
            AivPreplacementMapState.TransformRebuiltStartCoordinate(
                source,
                keep,
                AivRotation.Degrees270),
            398,
            403);
    }

    private static void TestCandidateStatuses()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 25, false, Point(45, 55)),
            Frame(1, 25, false, Point(44, 56)),
            Frame(2, 25, false, Point(43, 57)));
        MapCoordinate keep = new(400, 400);

        AivPlacementResult complete = PlacementEvaluator.Evaluate(
            new SparsePlacementMap(),
            blueprint,
            keep,
            AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Complete, complete.Status);
        AssertEqual(3, complete.TotalElementCount);
        AssertEqual(3, complete.PlaceableElementCount);
        AssertEqual(AivPlacementEvaluator.CompleteSequentialScore,
            complete.Score.SequentialBuildScore);
        AssertEqual(100, complete.Score.FitPercentage);
        AssertEqual((int?)null, complete.FirstBlockingBuildStep);

        AivProjectedCastle projected = Projector.Project(
            blueprint,
            keep,
            AivRotation.Degrees0);
        var partialMap = new SparsePlacementMap();
        partialMap.Set(projected.Elements[2].MapCoordinate, Evidence(buildingId: 12));
        AivPlacementResult partial = PlacementEvaluator.Evaluate(
            partialMap,
            blueprint,
            keep,
            AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Partial, partial.Status);
        AssertEqual(2, partial.PlaceableElementCount);
        AssertEqual(1, partial.BlockedElementCount);
        AssertEqual((int?)2, partial.FirstBlockingBuildStep);
        AssertEqual(2, partial.Score.SequentialBuildScore);
        AssertEqual(66, partial.Score.FitPercentage);
        AssertEqual(1, partial.Issues.Count);
        AssertEqual(2, partial.Issues[0].ElementIndex);

        var impossibleMap = new SparsePlacementMap();
        impossibleMap.Set(projected.Elements[0].MapCoordinate, Evidence(buildingId: 13));
        AivPlacementResult impossible = PlacementEvaluator.Evaluate(
            impossibleMap,
            blueprint,
            keep,
            AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Impossible, impossible.Status);
        AssertEqual((int?)0, impossible.FirstBlockingBuildStep);
        AssertEqual(0, impossible.Score.SequentialBuildScore);
    }

    private static void TestCandidateNotEvaluable()
    {
        MapCoordinate keep = new(400, 400);
        AivBlueprint organismBlueprint = Blueprint(
            Frame(0, 25, false, Point(45, 55)));
        AivProjectedElement element = Projector.Project(
            organismBlueprint,
            keep,
            AivRotation.Degrees0).Elements[0];
        var organismMap = new SparsePlacementMap();
        organismMap.Set(element.MapCoordinate, Evidence(
            terrainFlags: 0x00001000,
            organismId: 9));

        AivPlacementResult organism = PlacementEvaluator.Evaluate(
            organismMap,
            organismBlueprint,
            keep,
            AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Complete, organism.Status);
        AssertEqual(0, organism.NotEvaluableElementCount);

        AivBlueprint unknownFootprint = Blueprint(
            Frame(0, 999, false, Point(45, 55)));
        AivPlacementResult unknown = PlacementEvaluator.Evaluate(
            new SparsePlacementMap(),
            unknownFootprint,
            keep,
            AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.NotEvaluable, unknown.Status);
        AssertEqual(1, unknown.Issues.Count);
        AssertEqual(AivProjectedTileKind.ElementAnchor, unknown.Issues[0].TileKind);
        AssertEqual(AivPlacementIssueKind.UnresolvedNativeRule, unknown.Issues[0].Kind);
    }

    private static void TestCompleteRotationSelection()
    {
        MapCoordinate keep = new(400, 400);
        AivBlueprint blueprint = Blueprint(
            Frame(0, 25, false, Point(40, 60)));
        var map = new SparsePlacementMap();
        foreach (AivRotation rotation in new[]
        {
            AivRotation.Degrees0,
            AivRotation.Degrees180,
            AivRotation.Degrees270
        })
        {
            map.Set(Projector.Project(blueprint, keep, rotation).Elements[0].MapCoordinate,
                Evidence(buildingId: 21));
        }

        AivPlacementRotationSelection selection =
            PlacementEvaluator.EvaluateAllRotations(
                map,
                blueprint,
                keep,
                AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Complete, selection.Status);
        AivPlacementResult complete = RequireBestVariant(selection);
        AssertEqual(AivRotation.Degrees90, complete.Rotation);
        AssertEqual(4, selection.Variants.Count);
        AssertEqual(1, selection.CompleteVariants.Count);
        AssertEqual(0, selection.PartialVariants.Count);
    }

    private static void TestPartialRotationSelection()
    {
        MapCoordinate keep = new(400, 400);
        var frames = new List<AivBuildFrame>();
        for (int index = 0; index < 10; index++)
            frames.Add(Frame(index, 25, false, Point(40, 60 + index)));
        AivBlueprint blueprint = Blueprint(frames.ToArray());

        var map = new SparsePlacementMap();
        AivProjectedCastle initial = Projector.Project(
            blueprint,
            keep,
            AivRotation.Degrees0);
        map.Set(initial.Elements[0].MapCoordinate, Evidence(buildingId: 31));
        foreach (AivRotation rotation in new[]
        {
            AivRotation.Degrees90,
            AivRotation.Degrees180,
            AivRotation.Degrees270
        })
        {
            AivProjectedCastle alternative = Projector.Project(blueprint, keep, rotation);
            map.Set(alternative.Elements[9].MapCoordinate, Evidence(buildingId: 32));
        }

        AivPlacementRotationSelection accepted =
            PlacementEvaluator.EvaluateAllRotations(
                map,
                blueprint,
                keep,
                AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Partial, accepted.Status);
        AivPlacementResult acceptedPartial = RequireBestVariant(accepted);
        AssertEqual(AivRotation.Degrees90, acceptedPartial.Rotation);
        AssertEqual(90, acceptedPartial.Score.FitPercentage);
        AssertEqual(3, accepted.PartialVariants.Count);

        var initialPartialMap = new SparsePlacementMap();
        initialPartialMap.Set(initial.Elements[1].MapCoordinate, Evidence(buildingId: 35));
        foreach (AivRotation rotation in new[]
        {
            AivRotation.Degrees90,
            AivRotation.Degrees180,
            AivRotation.Degrees270
        })
        {
            AivProjectedCastle alternative = Projector.Project(blueprint, keep, rotation);
            initialPartialMap.Set(
                alternative.Elements[9].MapCoordinate,
                Evidence(buildingId: 36));
        }

        AivPlacementRotationSelection retainedInitial =
            PlacementEvaluator.EvaluateAllRotations(
                initialPartialMap,
                blueprint,
                keep,
                AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Partial, retainedInitial.Status);
        AivPlacementResult retainedPartial = RequireBestVariant(retainedInitial);
        AssertEqual(AivRotation.Degrees0, retainedPartial.Rotation);
        AssertEqual(1, retainedPartial.Score.SequentialBuildScore);
        AssertEqual(9,
            retainedInitial.PartialVariants[0].Score.SequentialBuildScore);

        var belowThresholdMap = new SparsePlacementMap();
        foreach (AivRotation rotation in new[]
        {
            AivRotation.Degrees0,
            AivRotation.Degrees90,
            AivRotation.Degrees180,
            AivRotation.Degrees270
        })
        {
            AivProjectedCastle variant = Projector.Project(blueprint, keep, rotation);
            int firstBlocked = rotation == AivRotation.Degrees0 ? 0 : 1;
            belowThresholdMap.Set(
                variant.Elements[firstBlocked].MapCoordinate,
                Evidence(buildingId: 41));
            belowThresholdMap.Set(
                variant.Elements[9].MapCoordinate,
                Evidence(buildingId: 42));
        }

        AivPlacementRotationSelection rejected =
            PlacementEvaluator.EvaluateAllRotations(
                belowThresholdMap,
                blueprint,
                keep,
                AivRotation.Degrees0);
        AssertEqual(AivPlacementStatus.Impossible, rejected.Status);
        Assert(rejected.BestVariant == null,
            "A below-threshold alternative must not be accepted.");

        AivPlacementRotationSelection repeated =
            PlacementEvaluator.EvaluateAllRotations(
                belowThresholdMap,
                blueprint,
                keep,
                AivRotation.Degrees0);
        AssertEqual(rejected.Status, repeated.Status);
        for (int index = 0; index < rejected.Variants.Count; index++)
        {
            AssertEqual(rejected.Variants[index].Rotation,
                repeated.Variants[index].Rotation);
            AssertEqual(rejected.Variants[index].Score.SequentialBuildScore,
                repeated.Variants[index].Score.SequentialBuildScore);
            AssertEqual(rejected.Variants[index].Score.FitPercentage,
                repeated.Variants[index].Score.FitPercentage);
        }
    }

    private static AivProjectedElement Element(
        AivBlueprint blueprint,
        AivRotation rotation,
        int index)
    {
        return Projector.Project(
            blueprint,
            new MapCoordinate(400, 400),
            rotation).Elements[index];
    }

    private static AivProjectedElement ElementAt(
        int mapperValue,
        MapCoordinate coordinate)
    {
        AivGridPoint anchor = Point(56, 43);
        AivBuildFrame frame;
        AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
        if (!mapper.FootprintSize.HasValue)
        {
            mapper = new AivMapperInfo(
                mapperValue,
                $"SYNTHETIC_MAPPER_{mapperValue}",
                AivItemCategory.Building,
                true,
                1);
            frame = new AivBuildFrame(
                0,
                mapperValue,
                mapper,
                false,
                new[] { anchor });
        }
        else
        {
            frame = Frame(0, mapperValue, false, anchor);
        }

        return Projector.Project(
            Blueprint(frame),
            coordinate,
            AivRotation.Degrees0).Elements[0];
    }

    private static AivPlacementTileEvidence Evidence(
        int terrainFlags = 0,
        byte secondaryLogic = 0,
        byte height = 0,
        byte defaultHeight = 0,
        ushort organismId = 0,
        ushort buildingId = 0,
        ushort entityId = 0,
        byte ownerId = 0)
    {
        return new AivPlacementTileEvidence(
            terrainFlags,
            secondaryLogic,
            height,
            defaultHeight,
            organismId,
            buildingId,
            entityId,
            ownerId);
    }

    private static void AssertTerrainBlocked(int mapperValue, int terrainFlags)
    {
        MapCoordinate coordinate = new(400, 400);
        var map = new SparsePlacementMap();
        map.Set(coordinate, Evidence(terrainFlags: terrainFlags));
        AivElementPlacementResult result = RuleEvaluator.EvaluateElement(
            map,
            ElementAt(mapperValue, coordinate));
        AssertEqual(AivElementPlacementStatus.Blocked, result.Status);
        Assert(result.Issues.Any(issue =>
            issue.Kind.HasFlag(AivPlacementIssueKind.TerrainBlocked)),
            $"Mapper {mapperValue} did not reject flags 0x{terrainFlags:X8}.");
    }

    private static void AssertTerrainAccepted(int mapperValue, int terrainFlags)
    {
        MapCoordinate coordinate = new(400, 400);
        var map = new SparsePlacementMap();
        map.Set(coordinate, Evidence(terrainFlags: terrainFlags));
        AivElementPlacementResult result = RuleEvaluator.EvaluateElement(
            map,
            ElementAt(mapperValue, coordinate));
        AssertEqual(AivElementPlacementStatus.Placeable, result.Status);
        AssertEqual(0, result.Issues.Count);
    }

    private static void AssertOnlyIssue(
        AivElementPlacementResult result,
        AivElementPlacementStatus expectedStatus,
        AivPlacementIssueKind expectedKind)
    {
        AssertEqual(expectedStatus, result.Status);
        AssertEqual(1, result.Issues.Count);
        AssertEqual(expectedKind, result.Issues[0].Kind);
    }

    private static AivBlueprint Blueprint(params AivBuildFrame[] frames)
    {
        return new AivBlueprint(
            "synthetic",
            5,
            frames,
            Array.Empty<AivMiscPlacement>(),
            Point(50, 50));
    }

    private static AivBuildFrame Frame(
        int buildIndex,
        int mapperValue,
        bool shouldPause,
        params AivGridPoint[] positions)
    {
        return new AivBuildFrame(
            buildIndex,
            mapperValue,
            AivMapperCatalog.Resolve(mapperValue),
            shouldPause,
            positions);
    }

    private static AivGridPoint Point(int row, int column) => new(row, column);

    private static void AssertBounds(
        IReadOnlyList<AivProjectedTile> tiles,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY)
    {
        AssertEqual(minimumX, tiles.Min(tile => tile.MapCoordinate.X));
        AssertEqual(maximumX, tiles.Max(tile => tile.MapCoordinate.X));
        AssertEqual(minimumY, tiles.Min(tile => tile.MapCoordinate.Y));
        AssertEqual(maximumY, tiles.Max(tile => tile.MapCoordinate.Y));
    }

    private static void AssertCoordinate(MapCoordinate actual, int x, int y)
    {
        AssertEqual(new MapCoordinate(x, y), actual);
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static AivPlacementResult RequireBestVariant(
        AivPlacementRotationSelection selection) =>
        selection.BestVariant ??
        throw new InvalidOperationException("The selection has no best variant.");

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SparsePlacementMap : IAivPlacementTileSource
    {
        private readonly Dictionary<int, AivPlacementTileEvidence> evidenceByTileId = new();

        public MapTileGeometry Geometry { get; } =
            new(MapTileGeometry.FixedTileCount, 400);

        public AivPlacementTileEvidence GetTileEvidence(int tileId)
        {
            return evidenceByTileId.TryGetValue(tileId, out AivPlacementTileEvidence evidence)
                ? evidence
                : default;
        }

        public void Set(MapCoordinate coordinate, AivPlacementTileEvidence evidence)
        {
            evidenceByTileId[Geometry.GetTileId(coordinate.X, coordinate.Y)] = evidence;
        }
    }
}
