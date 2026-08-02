using AIVParser.Core;
using AIVPlacement.Core;
using MapParser.Core;

internal static class Program
{
    private static readonly AivCastleProjector Projector = new();

    private static int Main()
    {
        (string Name, Action Test)[] tests =
        {
            ("Project all four rotations", TestRotations),
            ("Project asymmetric building footprints", TestAsymmetricFootprints),
            ("Retain coordinates beyond the map edge", TestNearMapEdge),
            ("Project gates, drawbridges and stairs", TestSpecialElements),
            ("Keep overlapping elements traceable", TestOverlappingElements),
            ("Preserve build steps, pauses and anchor-only entries", TestBuildOrderAndNonPlacements),
            ("Project associated blocked areas separately", TestBlockedAreas),
            ("Require an exact AIV keep anchor", TestMissingKeep)
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
            Frame(0, 61, false, Point(50, 50)),
            Frame(1, 25, false, Point(47, 54)));

        AssertCoordinate(Element(blueprint, AivRotation.Degrees0, 1).MapCoordinate, 404, 403);
        AssertCoordinate(Element(blueprint, AivRotation.Degrees90, 1).MapCoordinate, 403, 396);
        AssertCoordinate(Element(blueprint, AivRotation.Degrees180, 1).MapCoordinate, 396, 397);
        AssertCoordinate(Element(blueprint, AivRotation.Degrees270, 1).MapCoordinate, 397, 404);
    }

    private static void TestAsymmetricFootprints()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(50, 50)),
            Frame(1, 50, false, Point(50, 52)));

        AivProjectedElement zero = Element(blueprint, AivRotation.Degrees0, 1);
        AssertEqual(16, zero.OccupiedTiles.Count);
        AssertBounds(zero.OccupiedTiles, 402, 405, 400, 403);

        AivProjectedElement ninety = Element(blueprint, AivRotation.Degrees90, 1);
        AssertEqual(16, ninety.OccupiedTiles.Count);
        AssertBounds(ninety.OccupiedTiles, 400, 403, 395, 398);
    }

    private static void TestNearMapEdge()
    {
        AivBlueprint blueprint = Blueprint(
            Frame(0, 61, false, Point(50, 50)),
            Frame(1, 25, false, Point(60, 40)));
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
}
