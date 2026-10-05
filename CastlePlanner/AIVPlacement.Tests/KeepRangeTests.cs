using AIVParser.Core;
using AIVPlacement.Core;
using CastlePlanner.AIVPlacement.Core;
using MapParser.Core;
using SHCDESE.Interop;

internal static class KeepRangeTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Keep range: " + message);
    }

    private static KeepRangeSnapshot Settings(int? range, bool? bypass = false) => new(bypass,
        range.HasValue ? new Dictionary<int, int> { [400] = range.Value } : null);

    private static AivProjectedCastle Project(AivRotation rotation, params AivBuildFrame[] frames)
    {
        var keep = new AivGridPoint(20, 10);
        var all = new[] { Frame(0, eMappers.MAPPER_KEEP2, keep) }.Concat(frames).ToArray();
        var blueprint = new AivBlueprint("range test", 5, all, Array.Empty<AivMiscPlacement>(), keep);
        return new AivCastleProjector().Project(blueprint, new MapCoordinate(400, 400), rotation);
    }

    private static AivBuildFrame Frame(int index, eMappers mapper, params AivGridPoint[] positions) =>
        new(index, (int)mapper, AivMapperCatalog.Resolve((int)mapper), false, positions);

    private static readonly MapTileGeometry Geometry = new(MapTileGeometry.FixedTileCount, 400);
    private static HashSet<int> Tiles(AivProjectedCastle castle) => castle.OccupiedTiles
        .Where(t => Geometry.TryGetTileId(t.MapCoordinate.X, t.MapCoordinate.Y, out _))
        .Select(t => { Geometry.TryGetTileId(t.MapCoordinate.X, t.MapCoordinate.Y, out int id); return id; }).ToHashSet();

    public static void Run()
    {
        int[] expected = { 45, 50, 60, 70, 80, 90, 100, 100 };
        Check(MapTileGeometry.SupportedWorldSizes.Select(KeepRangeSnapshot.VanillaRange).SequenceEqual(expected), "map defaults");
        Check(KeepRangeSnapshot.VanillaRange(123) == 70, "unknown size fallback");
        Check(KeepRangePolicy.Allowance(eMappers.MAPPER_TOWER4) == 0, "tower no allowance");
        Check(KeepRangePolicy.Allowance(eMappers.MAPPER_DRAWBRIDGE) == 5, "drawbridge allowance");
        Check(KeepRangePolicy.Allowance(eMappers.MAPPER_MOAT) == null, "moat not ordinary building rule");

        foreach (AivRotation rotation in Enum.GetValues<AivRotation>())
        {
            // Keep reference x=13, remote 6x6 tower x=81..86 -> required range 73.
            AivProjectedCastle castle = Project(rotation, Frame(1, eMappers.MAPPER_TOWER4, new AivGridPoint(20, 81)));
            KeepRangeLayout layout = KeepRangePolicy.Prepare(castle);
            Check(layout.Known, "prepared Keep known");
            foreach (int range in new[] { 1, 70, 72 })
            {
                KeepRangeResult blocked = KeepRangePolicy.Evaluate(layout, Settings(range), Geometry, Tiles(castle));
                Check(blocked.CertainBuildings == 1 && blocked.CertainTiles.Count == 36, "whole tower rejected: " + rotation + "/" + range);
            }
            foreach (int range in new[] { 73, 80, 99, 100, 500 })
                Check(!KeepRangePolicy.Evaluate(layout, Settings(range), Geometry, Tiles(castle)).HasNotice,
                    "inclusive threshold/unrestricted: " + rotation + "/" + range);
            Check(!KeepRangePolicy.Evaluate(layout, Settings(1, true), Geometry, Tiles(castle)).HasNotice, "bypass beats small slider");
            Check(!KeepRangePolicy.Evaluate(layout, Settings(null, true), Geometry, Tiles(castle)).HasNotice, "bypass beats unknown range");
            Check(!KeepRangePolicy.Evaluate(layout, Settings(100, null), Geometry, Tiles(castle)).HasNotice, "100 beats unknown bypass");
            Check(KeepRangePolicy.Evaluate(layout, Settings(70, null), Geometry, Tiles(castle)).Unknown, "unknown optional provider");

            var towerCells = castle.Elements[1].OccupiedTiles.Select(t => t.MapCoordinate).ToArray();
            var rescue = towerCells[0];
            var allies = new[] { new KeepRangeAlly(new[] { rescue }) };
            Check(!KeepRangePolicy.Evaluate(layout, Settings(70), Geometry, Tiles(castle), allies).HasNotice, "certain ally rescues tower");
            var possibleAlly = new[] { new KeepRangeAlly(new[] { rescue }, true) };
            var possible = KeepRangePolicy.Evaluate(layout, Settings(70), Geometry, Tiles(castle), possibleAlly);
            Check(possible.CertainBuildings == 0 && possible.PossibleBuildings == 1, "uncertain ally is a score interval");
            var choices = new[] { new KeepRangeAlly(new[] { rescue, new MapCoordinate(10, 10) }) };
            Check(KeepRangePolicy.Evaluate(layout, Settings(70), Geometry, Tiles(castle), choices).CertainBuildings == 0,
                "different possible ally rotations do not falsely prove rejection");

            var native = new AivPlacementEvaluator().Evaluate(new EmptyMap(), castle);
            int score = native.Score.SequentialBuildScore;
            foreach (int range in new[] { 1, 70, 100 }) KeepRangePolicy.Evaluate(layout, Settings(range), Geometry, Tiles(castle));
            Check(native.Score.SequentialBuildScore == score, "practice does not mutate native result");
        }

        Check(KeepRangePolicy.Within(new MapCoordinate(35, 35), new MapCoordinate(0, 0), 71 / 2), "odd ally range floor inclusive");
        Check(!KeepRangePolicy.Within(new MapCoordinate(36, 0), new MapCoordinate(0, 0), 71 / 2), "odd ally range outside");
        var doubled = Project(AivRotation.Degrees0,
            Frame(1, eMappers.MAPPER_TOWER4, new AivGridPoint(20, 81)),
            Frame(2, eMappers.MAPPER_TOWER4, new AivGridPoint(20, 81)));
        Check(KeepRangePolicy.Evaluate(KeepRangePolicy.Prepare(doubled), Settings(70), Geometry, Tiles(doubled)).CertainBuildings == 1,
            "overwritten frame has no prepared building");

        var shifted = Project(AivRotation.Degrees0,
            Frame(1, eMappers.MAPPER_TOWER4, new AivGridPoint(20, 81)),
            Frame(2, eMappers.MAPPER_PITCH_DITCH, new AivGridPoint(20, 81)));
        // First surviving tower cell moves +1 in X; its 6x6 footprint now needs range 74.
        Check(KeepRangePolicy.Evaluate(KeepRangePolicy.Prepare(shifted), Settings(73), Geometry, Tiles(shifted)).CertainBuildings == 1,
            "partial overwrite moves prepared anchor");
        Check(!KeepRangePolicy.Evaluate(KeepRangePolicy.Prepare(shifted), Settings(74), Geometry, Tiles(shifted)).HasNotice,
            "shifted anchor boundary");

        AivPracticeRotation union = AivGeometricPractice.Score(AivRotation.Degrees0, 90, 10, 1,
            new[] { 1, 2, 3, 4 }, Array.Empty<int>(), new[] { 1 },
            new[] { new[] { 1, 2 }.AsEnumerable() }, new[] { new[] { 1, 2, 4 }.AsEnumerable() },
            Array.Empty<int>(), false, new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
        Check(union.MinimumDeduction == 2 && union.MaximumDeduction == 3 && union.MinimumPercentage == 60 && union.MaximumPercentage == 70,
            "native/overlap/range counted once");

        var teams = new Dictionary<int, int> { [1] = 1, [2] = 1 };
        var snapshot = new KeepRangeSnapshot(false, new Dictionary<int, int> { [400] = 70 }, teams);
        string fingerprint = snapshot.Fingerprint;
        teams[2] = 2;
        Check(snapshot.Teams[2] == 1, "immutable team capture");
        Check(new KeepRangeSnapshot(false, snapshot.Ranges, teams).Fingerprint != fingerprint, "team change invalidates");
        Check(new KeepRangeSnapshot(true, snapshot.Ranges, snapshot.Teams).Fingerprint != fingerprint, "bypass change invalidates");
        Check(new KeepRangeSnapshot(false, new Dictionary<int, int> { [400] = 100 }, snapshot.Teams).Fingerprint != fingerprint, "range change invalidates");
    }

    public static void Baibars(string directory)
    {
        foreach (string name in new[] { "Nimrod1.aivjson", "Nimrodwest.aivjson", "Nimrodwest2.aivjson" })
        {
            string path = Path.Combine(directory, name);
            AivJsonLoadResult loaded = AivJsonFileLoader.Load(path);
            AivParseResult parsed = new AivBlueprintParser().Parse(loaded.Document, path, loaded.Diagnostics);
            foreach (AivRotation rotation in Enum.GetValues<AivRotation>())
            {
                var castle = new AivCastleProjector().Project(parsed.Blueprint, new MapCoordinate(400, 400), rotation);
                var layout = KeepRangePolicy.Prepare(castle);
                var result = KeepRangePolicy.Evaluate(layout, Settings(70), Geometry, Tiles(castle));
                Check(result.CertainBuildings >= 3, name + " remote gate and towers at 70");
                Check(!KeepRangePolicy.Evaluate(layout, Settings(80), Geometry, Tiles(castle)).HasNotice, name + " passes at 80");
            }
        }
        Console.WriteLine("PASS: Baibars three castles, four rotations, ranges 70/80.");
    }

    private sealed class EmptyMap : IAivPlacementTileSource
    {
        public MapTileGeometry Geometry => KeepRangeTests.Geometry;
        public AivPlacementTileEvidence GetTileEvidence(int tileId) => new(0, 0, 0, 0, 0, 0, 0, 0);
    }
}
