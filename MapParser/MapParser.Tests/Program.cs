using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MapParser.Core;

namespace MapParser.Tests;

internal static class Program
{
    private const int SnapshotSampleTileId = 160400;

    private static int Main(string[] args)
    {
        var tests = new (string Name, Action Body)[]
        {
            ("Parse byte array, stream and path", TestParseOverloads),
            ("Parse 100-slot directory", () => TestDirectoryCapacity(100)),
            ("Parse 150-slot directory", () => TestDirectoryCapacity(150)),
            ("Parse 200-slot directory", () => TestDirectoryCapacity(200)),
            ("Normalize old and new tile IDs", TestLogicalSectionIds),
            ("Read typed placement layers", TestPlacementLayers),
            ("Create immutable placement snapshot", TestPlacementSnapshot),
            ("Create snapshot from old and new tile IDs", TestPlacementSnapshotSectionIds),
            ("Reject snapshot without map sections", TestPlacementSnapshotSectionsUnavailable),
            ("Reject snapshot with missing layer", TestPlacementSnapshotMissingLayer),
            ("Reject snapshot with inconsistent layer lengths", TestPlacementSnapshotLayerLengths),
            ("Reject snapshot with unsupported geometry", TestPlacementSnapshotGeometry),
            ("Decode only placement layers for snapshot", TestPlacementSnapshotLazyDecoding),
            ("Match native map-tile geometry vectors", TestMapTileGeometryVectors),
            ("Roundtrip every map tile", TestMapTileGeometryRoundTrips),
            ("Separate fixed geometry from world-size bounds", TestMapTileWorldBounds),
            ("Reject invalid map-tile geometry inputs", TestInvalidMapTileGeometry),
            ("Decode PKWARE-DCL and verify CRC32", TestCompressedSection),
            ("Reject truncated preamble", TestTruncatedPreamble),
            ("Reject invalid section offset", TestInvalidSectionOffset),
            ("Reject raw size mismatch", TestRawSizeMismatch),
            ("Reject compressed header size mismatch", TestCompressedHeaderSizeMismatch),
            ("Reject unknown compression flag", TestUnknownCompressionFlag),
            ("Reject duplicate section ID", TestDuplicateSectionId),
            ("Reject duplicate logical section ID", TestDuplicateLogicalSectionId),
            ("Reject invalid PKWARE-DCL data", TestInvalidCompressedData),
            ("Reject wrong CRC32", TestWrongCrc),
            ("Reject unknown directory tag", TestUnknownDirectoryTag),
            ("Recognize special map as opaque", TestSpecialMap),
            ("Preserve regular trailing bytes", TestRegularTail),
            ("Recognize unavailable zero-filled section 1190", TestUnavailableSection1190),
            ("Do not expose incomplete placement layers", TestIncompletePlacementLayers)
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

        bool parityMode = args.Length >= 3 && args[0] == "--parity";
        if (args.Length > 0)
        {
            try
            {
                if (parityMode)
                {
                    TestPythonParity(args[1], args[2], args.Skip(3));
                    Log("PASS", "Python/C# result parity");
                }
                else
                {
                    TestLocalCorpus(args);
                    Log("PASS", "Local integration corpus");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Log("FAIL", $"{(parityMode ? "Python/C# result parity" : "Local integration corpus")}: {ex.Message}");
            }
        }

        Log(
            failures == 0 ? "PASS" : "FAIL",
            $"Summary: total={tests.Length + (args.Length > 0 ? 1 : 0)}, failed={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static void TestParseOverloads()
    {
        Fixture fixture = FixtureBuilder.Build(200, PlacementSections());
        byte[] callerBytes = (byte[])fixture.Bytes.Clone();
        MapDocument fromBytes = MapFileReader.Parse(callerBytes);
        callerBytes[fixture.PayloadOffset] ^= 0xff;
        AssertSequence(PlacementSections()[0].Content, fromBytes.Sections[0].ReadContent());

        using var stream = new MemoryStream(fixture.Bytes);
        MapDocument fromStream = MapFileReader.Parse(stream);
        AssertEqual(fixture.Bytes.Length, stream.Position);
        AssertEqual(8, fromStream.Sections.Count);

        string path = Path.Combine(Path.GetTempPath(), $"MapParser-{Guid.NewGuid():N}.map");
        try
        {
            File.WriteAllBytes(path, fixture.Bytes);
            MapDocument fromPath = MapFileReader.Parse(path);
            AssertEqual(Path.GetFullPath(path), fromPath.SourceName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void TestDirectoryCapacity(int capacity)
    {
        Fixture fixture = FixtureBuilder.Build(capacity, new[] { new SectionSpec(1018, Int32Bytes(42)) });
        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        AssertEqual(capacity, map.Directory!.Capacity);
        AssertEqual(capacity * 20 + 36, (int)map.Directory.DirectoryTag);
        AssertEqual(42, BinaryPrimitives.ReadInt32LittleEndian(map.Sections[0].ReadContent()));
    }

    private static void TestLogicalSectionIds()
    {
        MapDocument oldMap = MapFileReader.Parse(
            FixtureBuilder.Build(100, new[] { new SectionSpec(1003, Int32Bytes(7)) }).Bytes);
        MapDocument newMap = MapFileReader.Parse(
            FixtureBuilder.Build(200, new[] { new SectionSpec(3003, Int32Bytes(8)) }).Bytes);
        AssertEqual(1003, oldMap.Sections[0].LogicalSectionId);
        AssertEqual(1003, newMap.Sections[0].LogicalSectionId);
        AssertEqual(3003, newMap.GetLogicalSection(1003).SectionId);
    }

    private static void TestPlacementLayers()
    {
        MapDocument map = MapFileReader.Parse(FixtureBuilder.Build(200, PlacementSections()).Bytes);
        AssertTrue(map.HasPlacementLayers);
        MapTileLayers layers = map.ReadPlacementLayers();
        AssertEqual(3, layers.TileCount);
        AssertEqual(0x11223344, layers.TerrainFlags[1]);
        AssertEqual((byte)12, layers.SecondaryLogic[2]);
        AssertEqual((byte)22, layers.Heights[2]);
        AssertEqual((byte)32, layers.DefaultHeights[2]);
        AssertEqual((ushort)102, layers.Organisms[2]);
        AssertEqual((ushort)202, layers.BuildingOccupancy[2]);
        AssertEqual((ushort)302, layers.EntityOccupancy[2]);
        AssertEqual((byte)42, layers.OwnerOccupancy[2]);
    }

    private static void TestPlacementSnapshot()
    {
        MapDocument map = MapFileReader.Parse(
            FixtureBuilder.Build(200, PlacementSnapshotSections(useNewIds: true)).Bytes);
        AssertTrue(map.HasPlacementSnapshot);

        MapPlacementSnapshot snapshot = map.ReadPlacementSnapshot();
        AssertEqual(MapTileGeometry.FixedTileCount, snapshot.TileCount);
        AssertEqual(map.Metadata.WorldSize, snapshot.Geometry.WorldSize);
        AssertTrue(snapshot.Geometry.TryGetCoordinate(SnapshotSampleTileId, out MapCoordinate coordinate));

        MapPlacementTile byId = snapshot.GetTile(SnapshotSampleTileId);
        MapPlacementTile byCoordinate = snapshot.GetTile(coordinate.X, coordinate.Y);
        AssertPlacementTile(byId);
        AssertPlacementTile(byCoordinate);
        AssertTrue(snapshot.TryGetTile(coordinate.X, coordinate.Y, out MapPlacementTile fromTry));
        AssertPlacementTile(fromTry);

        AssertTrue(!snapshot.TryGetTile(-1, 0, out _));
        AssertThrows<ArgumentOutOfRangeException>(() => snapshot.GetTile(-1));
        AssertThrows<ArgumentOutOfRangeException>(() => snapshot.GetTile(snapshot.TileCount));
        AssertThrows<ArgumentOutOfRangeException>(() => snapshot.GetTile(-1, 0));
    }

    private static void TestPlacementSnapshotSectionIds()
    {
        foreach (bool useNewIds in new[] { false, true })
        {
            MapDocument map = MapFileReader.Parse(
                FixtureBuilder.Build(200, PlacementSnapshotSections(useNewIds)).Bytes);
            MapPlacementSnapshot snapshot = MapPlacementSnapshot.Create(map);
            AssertPlacementTile(snapshot.GetTile(SnapshotSampleTileId));
        }
    }

    private static void TestPlacementSnapshotSectionsUnavailable()
    {
        Fixture fixture = FixtureBuilder.Build(100, Array.Empty<SectionSpec>());
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.Bytes.AsSpan(fixture.DirectoryTagOffset), 1076);
        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        AssertTrue(!map.HasPlacementSnapshot);

        MapPlacementSnapshotException exception = AssertThrowsAndGet<MapPlacementSnapshotException>(
            () => map.ReadPlacementSnapshot());
        AssertEqual(MapPlacementSnapshotFailureKind.SectionsUnavailable, exception.FailureKind);
    }

    private static void TestPlacementSnapshotMissingLayer()
    {
        SectionSpec[] sections = PlacementSections()
            .Where(section => section.Id != 3026)
            .ToArray();
        MapDocument map = MapFileReader.Parse(FixtureBuilder.Build(200, sections).Bytes);
        AssertTrue(!map.HasPlacementSnapshot);

        MapPlacementSnapshotException exception = AssertThrowsAndGet<MapPlacementSnapshotException>(
            () => map.ReadPlacementSnapshot());
        AssertEqual(MapPlacementSnapshotFailureKind.MissingLayer, exception.FailureKind);
        AssertEqual((int?)MapSectionCatalog.Entity, exception.LogicalSectionId);
    }

    private static void TestPlacementSnapshotLayerLengths()
    {
        SectionSpec[] sections = PlacementSections();
        sections[1] = new SectionSpec(3037, [10, 11]);
        MapDocument map = MapFileReader.Parse(FixtureBuilder.Build(200, sections).Bytes);
        AssertTrue(!map.HasPlacementSnapshot);

        MapPlacementSnapshotException exception = AssertThrowsAndGet<MapPlacementSnapshotException>(
            () => map.ReadPlacementSnapshot());
        AssertEqual(MapPlacementSnapshotFailureKind.InconsistentLayerLength, exception.FailureKind);
        AssertEqual((int?)MapSectionCatalog.Logic2, exception.LogicalSectionId);
    }

    private static void TestPlacementSnapshotGeometry()
    {
        MapDocument map = MapFileReader.Parse(FixtureBuilder.Build(200, PlacementSections()).Bytes);
        AssertTrue(!map.HasPlacementSnapshot);

        MapPlacementSnapshotException exception = AssertThrowsAndGet<MapPlacementSnapshotException>(
            () => map.ReadPlacementSnapshot());
        AssertEqual(MapPlacementSnapshotFailureKind.UnsupportedGeometry, exception.FailureKind);
        AssertTrue(exception.InnerException is MapUnsupportedGeometryException);
    }

    private static void TestPlacementSnapshotLazyDecoding()
    {
        SectionSpec[] placement = PlacementSnapshotSections(useNewIds: true);
        SectionSpec[] sections = placement
            .Append(new SectionSpec(1050, Encoding.ASCII.GetBytes("unused invalid DCL"), Compressed: true))
            .ToArray();
        Fixture fixture = FixtureBuilder.Build(200, sections);
        int unrelatedPayloadOffset = fixture.PayloadOffset + placement.Sum(section => section.Content.Length);
        fixture.Bytes[unrelatedPayloadOffset + 12] = 9;

        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        MapPlacementSnapshot snapshot = map.ReadPlacementSnapshot();
        AssertPlacementTile(snapshot.GetTile(SnapshotSampleTileId));

        // An invalid unrelated section would fail immediately if snapshot creation decoded it.
        AssertThrows<MapCorruptDataException>(() => map.GetLogicalSection(1050).ReadContent());
    }

    private static void TestMapTileGeometryVectors()
    {
        using JsonDocument vectors = ReadMapTileGeometryVectors();
        JsonElement root = vectors.RootElement;
        JsonElement geometryData = root.GetProperty("geometry");
        int tileCount = geometryData.GetProperty("tileCount").GetInt32();
        var geometry = new MapTileGeometry(tileCount, 400);

        AssertEqual(MapTileGeometry.FixedRowCount, geometryData.GetProperty("rowCount").GetInt32());
        AssertEqual(MapTileGeometry.FixedTileCount, tileCount);
        AssertEqual(0, geometryData.GetProperty("minimumTileId").GetInt32());
        AssertEqual(tileCount - 1, geometryData.GetProperty("maximumTileId").GetInt32());

        foreach (JsonElement vector in root.GetProperty("nativeCoordinateVectors").EnumerateArray())
        {
            int x = vector.GetProperty("x").GetInt32();
            int y = vector.GetProperty("y").GetInt32();
            int expectedTileId = vector.GetProperty("tileId").GetInt32();
            AssertTrue(geometry.IsValidCoordinate(x, y));
            AssertTrue(geometry.TryGetTileId(x, y, out int tileId));
            AssertEqual(expectedTileId, tileId);
            AssertEqual(expectedTileId, geometry.GetTileId(x, y));
            AssertTrue(geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate));
            AssertEqual(new MapCoordinate(x, y), coordinate);
        }

        foreach (JsonElement vector in root.GetProperty("invalidCoordinateVectors").EnumerateArray())
        {
            int x = vector.GetProperty("x").GetInt32();
            int y = vector.GetProperty("y").GetInt32();
            AssertTrue(!geometry.IsValidCoordinate(x, y));
            AssertTrue(!geometry.TryGetTileId(x, y, out _));
            AssertThrows<ArgumentOutOfRangeException>(() => geometry.GetTileId(x, y));
        }

        foreach (JsonElement tileIdData in root.GetProperty("invalidTileIds").EnumerateArray())
            AssertTrue(!geometry.TryGetCoordinate(tileIdData.GetInt32(), out _));

        // U4 entries are deliberately radar observations and have no tile IDs to validate here.
        foreach (JsonElement observation in root.GetProperty("u4RadarObservations").EnumerateArray())
            AssertEqual(JsonValueKind.Null, observation.GetProperty("tileIds").ValueKind);
    }

    private static void TestMapTileGeometryRoundTrips()
    {
        var geometry = new MapTileGeometry(MapTileGeometry.FixedTileCount, 400);
        AssertTrue(geometry.TryGetCoordinate(0, out MapCoordinate first));
        AssertEqual(new MapCoordinate(399, 0), first);
        AssertTrue(geometry.TryGetCoordinate(geometry.TileCount - 1, out MapCoordinate last));
        AssertEqual(new MapCoordinate(400, 799), last);

        for (int tileId = 0; tileId < geometry.TileCount; tileId++)
        {
            AssertTrue(geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate));
            AssertEqual(tileId, geometry.GetTileId(coordinate.X, coordinate.Y));
        }
    }

    private static void TestMapTileWorldBounds()
    {
        using JsonDocument vectors = ReadMapTileGeometryVectors();
        JsonElement root = vectors.RootElement;
        int tileCount = root.GetProperty("geometry").GetProperty("tileCount").GetInt32();

        foreach (JsonElement vector in root.GetProperty("worldSizeBoundaryVectors").EnumerateArray())
        {
            int worldSize = vector.GetProperty("worldSize").GetInt32();
            var geometry = new MapTileGeometry(tileCount, worldSize);
            int nativeX = vector.GetProperty("nativeX").GetInt32();
            int nativeY = vector.GetProperty("nativeY").GetInt32();
            AssertEqual(vector.GetProperty("border").GetInt32(), geometry.WorldBorder);
            AssertEqual(vector.GetProperty("localX").GetInt32() + geometry.WorldBorder, nativeX);
            AssertEqual(vector.GetProperty("localY").GetInt32() + geometry.WorldBorder, nativeY);
            AssertTrue(geometry.IsWithinWorldBounds(nativeX, nativeY));
            AssertEqual(vector.GetProperty("tileId").GetInt32(), geometry.GetTileId(nativeX, nativeY));
        }

        foreach (int worldSize in MapTileGeometry.SupportedWorldSizes)
        {
            var geometry = new MapTileGeometry(tileCount, worldSize);
            int countedWorldTiles = 0;
            for (int y = 0; y < MapTileGeometry.FixedRowCount; y++)
            {
                for (int x = 0; x < MapTileGeometry.FixedRowCount; x++)
                {
                    if (geometry.IsWithinWorldBounds(x, y))
                    {
                        countedWorldTiles++;
                        AssertTrue(geometry.IsValidCoordinate(x, y));
                    }
                }
            }
            AssertEqual(geometry.WorldTileCount, countedWorldTiles);

            // A fixed-geometry tile remains addressable even when outside this map's playable world.
            AssertTrue(geometry.IsValidCoordinate(399, 0));
            AssertTrue(!geometry.IsWithinWorldBounds(399, 0));
            AssertEqual(0, geometry.GetTileId(399, 0));
        }
    }

    private static void TestInvalidMapTileGeometry()
    {
        AssertThrows<MapUnsupportedGeometryException>(() => new MapTileGeometry(0, 400));
        AssertThrows<MapUnsupportedGeometryException>(() =>
            new MapTileGeometry(MapTileGeometry.FixedTileCount - 1, 400));
        AssertThrows<MapUnsupportedGeometryException>(() =>
            new MapTileGeometry(MapTileGeometry.FixedTileCount + 1, 400));
        AssertThrows<MapUnsupportedGeometryException>(() =>
            new MapTileGeometry(MapTileGeometry.FixedTileCount, 0));
        AssertThrows<MapUnsupportedGeometryException>(() =>
            new MapTileGeometry(MapTileGeometry.FixedTileCount, 800));
    }

    private static JsonDocument ReadMapTileGeometryVectors()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MapTileGeometryVectors.json");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    private static void TestCompressedSection()
    {
        byte[] expected = Encoding.ASCII.GetBytes("PKWARE-DCL synthetic fixture");
        Fixture fixture = FixtureBuilder.Build(150, new[] { new SectionSpec(1050, expected, Compressed: true) });
        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        AssertEqual(MapSectionStorageKind.PkwareDcl, map.Sections[0].StorageKind);
        byte[] actual = map.Sections[0].ReadContent();
        AssertSequence(expected, actual);
        AssertEqual(Sha256(expected), Sha256(actual));
    }

    private static void TestTruncatedPreamble()
    {
        byte[] bytes = FixtureBuilder.Build(100, Array.Empty<SectionSpec>()).Bytes[..17];
        AssertThrows<MapCorruptDataException>(() => MapFileReader.Parse(bytes));
    }

    private static void TestInvalidSectionOffset()
    {
        Fixture fixture = FixtureBuilder.Build(100, new[] { new SectionSpec(1018, Int32Bytes(1)) });
        fixture.WriteDirectoryValue(4, 0, int.MaxValue);
        AssertThrows<MapCorruptDataException>(() => MapFileReader.Parse(fixture.Bytes));
    }

    private static void TestRawSizeMismatch()
    {
        Fixture fixture = FixtureBuilder.Build(100, new[] { new SectionSpec(1018, Int32Bytes(1)) });
        fixture.WriteDirectoryValue(1, 0, 3);
        AssertThrows<MapCorruptDataException>(() => MapFileReader.Parse(fixture.Bytes));
    }

    private static void TestUnknownCompressionFlag()
    {
        Fixture fixture = FixtureBuilder.Build(100, new[] { new SectionSpec(1018, Int32Bytes(1)) });
        fixture.WriteDirectoryValue(3, 0, 7);
        AssertThrows<MapCorruptDataException>(() => MapFileReader.Parse(fixture.Bytes));
    }

    private static void TestCompressedHeaderSizeMismatch()
    {
        Fixture fixture = FixtureBuilder.Build(
            100,
            new[] { new SectionSpec(1018, Int32Bytes(1), Compressed: true) });
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.Bytes.AsSpan(fixture.PayloadOffset), 99);
        AssertThrows<MapCorruptDataException>(() => MapFileReader.Parse(fixture.Bytes));
    }

    private static void TestDuplicateSectionId()
    {
        var sections = new[]
        {
            new SectionSpec(1018, Int32Bytes(1)),
            new SectionSpec(1018, Int32Bytes(2))
        };
        AssertThrows<MapCorruptDataException>(() =>
            MapFileReader.Parse(FixtureBuilder.Build(100, sections).Bytes));
    }

    private static void TestDuplicateLogicalSectionId()
    {
        var sections = new[]
        {
            new SectionSpec(1003, Int32Bytes(1)),
            new SectionSpec(3003, Int32Bytes(2))
        };
        AssertThrows<MapCorruptDataException>(() =>
            MapFileReader.Parse(FixtureBuilder.Build(100, sections).Bytes));
    }

    private static void TestInvalidCompressedData()
    {
        Fixture fixture = FixtureBuilder.Build(
            100,
            new[] { new SectionSpec(1018, Int32Bytes(1), Compressed: true) });
        fixture.Bytes[fixture.PayloadOffset + 12] = 9;
        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        AssertThrows<MapCorruptDataException>(() => map.Sections[0].ReadContent());
    }

    private static void TestWrongCrc()
    {
        Fixture fixture = FixtureBuilder.Build(
            100,
            new[] { new SectionSpec(1018, Int32Bytes(1), Compressed: true) });
        fixture.Bytes[fixture.PayloadOffset + 8] ^= 0x01;
        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        AssertThrows<MapSectionCrcException>(() => map.Sections[0].ReadContent());
    }

    private static void TestUnknownDirectoryTag()
    {
        Fixture fixture = FixtureBuilder.Build(100, Array.Empty<SectionSpec>());
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.Bytes.AsSpan(fixture.DirectoryTagOffset), 9999);
        AssertThrows<MapUnsupportedFormatException>(() => MapFileReader.Parse(fixture.Bytes));
    }

    private static void TestSpecialMap()
    {
        foreach (uint tag in new uint[] { 1076, 2100, 2108 })
        {
            Fixture fixture = FixtureBuilder.Build(100, Array.Empty<SectionSpec>());
            BinaryPrimitives.WriteUInt32LittleEndian(fixture.Bytes.AsSpan(fixture.DirectoryTagOffset), tag);
            MapDocument map = MapFileReader.Parse(fixture.Bytes);
            AssertEqual(MapFormatKind.CrusaderDefinitiveEditionSpecial, map.FormatKind);
            AssertTrue(!map.SectionsAvailable);
            AssertTrue(!map.HasPlacementLayers);
            AssertEqual(fixture.Bytes.Length - fixture.DirectoryTagOffset, map.ReadOpaqueTail().Length);
            AssertEqual(4, map.Metadata.MaxPlayers);
        }
    }

    private static void TestRegularTail()
    {
        byte[] tail = { 0x50, 0x4b, 0x05, 0x06 };
        Fixture fixture = FixtureBuilder.Build(100, Array.Empty<SectionSpec>(), tail);
        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        AssertSequence(tail, map.ReadOpaqueTail());
    }

    private static void TestIncompletePlacementLayers()
    {
        MapDocument map = MapFileReader.Parse(
            FixtureBuilder.Build(100, new[] { new SectionSpec(1003, Int32Bytes(1)) }).Bytes);
        AssertTrue(!map.HasPlacementLayers);
        AssertThrows<MapCorruptDataException>(() => map.ReadPlacementLayers());
    }

    private static void TestUnavailableSection1190()
    {
        Fixture fixture = FixtureBuilder.Build(
            100,
            new[] { new SectionSpec(1190, new byte[18], Compressed: true) });
        int storedSize = BinaryPrimitives.ReadInt32LittleEndian(
            fixture.Bytes.AsSpan(fixture.DirectoryBodyOffset + 28 + fixture.Capacity * 4));
        fixture.Bytes.AsSpan(fixture.PayloadOffset + 12, storedSize - 12).Clear();
        MapDocument map = MapFileReader.Parse(fixture.Bytes);
        AssertEqual(MapSectionStorageKind.UnavailableZeroFilledDcl, map.Sections[0].StorageKind);
        AssertTrue(!map.Sections[0].IsContentAvailable);
        AssertThrows<MapUnsupportedFormatException>(() => map.Sections[0].ReadContent());
    }

    private static void TestLocalCorpus(string[] roots)
    {
        string[] files = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.map", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
            throw new InvalidOperationException("No .map files found in integration roots.");

        int regular = 0;
        int special = 0;
        int unavailableSections = 0;
        var watch = Stopwatch.StartNew();
        for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            string file = files[fileIndex];
            MapDocument map;
            try
            {
                map = MapFileReader.Parse(file);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{file}: {ex.Message}", ex);
            }
            if (!map.SectionsAvailable)
            {
                special++;
            }
            else
            {
                foreach (MapSectionInfo section in map.Sections)
                {
                    if (section.IsContentAvailable)
                        section.ReadContent();
                    else
                        unavailableSections++;
                }
                regular++;
            }

            int completed = fileIndex + 1;
            if (completed == 1 || completed == files.Length || completed % 25 == 0)
            {
                double elapsed = watch.Elapsed.TotalSeconds;
                double eta = elapsed / completed * (files.Length - completed);
                Console.WriteLine(
                    $"Integration progress: {completed}/{files.Length}, " +
                    $"elapsed={elapsed:F1}s, eta={eta:F1}s");
            }
        }
        Console.WriteLine(
            $"Integration: files={files.Length}, regular={regular}, special={special}, " +
            $"unavailable-sections={unavailableSections}, elapsed={watch.Elapsed.TotalSeconds:F1}s");
    }

    private static void TestPythonParity(string python, string script, IEnumerable<string> mapPaths)
    {
        var paths = mapPaths.Select(Path.GetFullPath).ToList();
        string synthetic = Path.Combine(Path.GetTempPath(), $"MapParser-parity-{Guid.NewGuid():N}.map");
        string special = Path.Combine(Path.GetTempPath(), $"MapParser-special-{Guid.NewGuid():N}.map");
        try
        {
            File.WriteAllBytes(
                synthetic,
                FixtureBuilder.Build(100, new[]
                {
                    new SectionSpec(3003, Int32Bytes(11, 12)),
                    new SectionSpec(1050, Encoding.ASCII.GetBytes("parity"), Compressed: true)
                }).Bytes);
            Fixture specialFixture = FixtureBuilder.Build(100, Array.Empty<SectionSpec>());
            BinaryPrimitives.WriteUInt32LittleEndian(
                specialFixture.Bytes.AsSpan(specialFixture.DirectoryTagOffset), 2100);
            File.WriteAllBytes(special, specialFixture.Bytes);
            paths.Insert(0, synthetic);
            paths.Insert(1, special);

            foreach (string path in paths)
                ComparePythonManifest(python, script, path);
            Console.WriteLine($"Parity: files={paths.Count}");
        }
        finally
        {
            File.Delete(synthetic);
            File.Delete(special);
        }
    }

    private static void ComparePythonManifest(string python, string script, string mapPath)
    {
        var start = new ProcessStartInfo
        {
            FileName = python,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("manifest");
        start.ArgumentList.Add(mapPath);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Python.");
        string json = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Python failed for {mapPath}: {error}");

        using JsonDocument manifest = JsonDocument.Parse(json);
        JsonElement root = manifest.RootElement;
        MapDocument map = MapFileReader.Parse(mapPath);
        AssertEqual(
            map.SectionsAvailable ? "scde" : "scde-special",
            root.GetProperty("format").GetString());
        AssertEqual(map.HasPlacementLayers, root.GetProperty("has_placement_layers").GetBoolean());
        AssertEqual(map.OpaqueTailLength, root.GetProperty("opaque_tail_size").GetInt32());

        JsonElement metadata = root.GetProperty("metadata");
        AssertEqual(map.Metadata.MapType, metadata.GetProperty("map_type").GetInt32());
        AssertEqual(map.Metadata.MaxPlayers, metadata.GetProperty("max_players").GetInt32());
        AssertEqual(map.Metadata.ScenarioMissionType, metadata.GetProperty("mission_type").GetInt32());
        AssertEqual(map.Metadata.MissionLockType, metadata.GetProperty("mission_lock_type").GetInt32());
        AssertEqual(map.Metadata.StandaloneFileName, metadata.GetProperty("standalone_filename").GetString());
        AssertEqual(map.Metadata.IsSkirmishMap, metadata.GetProperty("is_skirmish").GetBoolean());
        AssertEqual(map.Metadata.IsBalancedMap, metadata.GetProperty("is_balanced").GetBoolean());
        AssertEqual(map.Metadata.WorldSize, metadata.GetProperty("world_size").GetInt32());
        JsonElement keeps = metadata.GetProperty("keep_locations");
        AssertEqual(map.Metadata.KeepLocations.Count, keeps.GetArrayLength());
        for (int index = 0; index < keeps.GetArrayLength(); index++)
        {
            AssertEqual(map.Metadata.KeepLocations[index].X, keeps[index][0].GetInt32());
            AssertEqual(map.Metadata.KeepLocations[index].Y, keeps[index][1].GetInt32());
        }

        JsonElement directory = root.GetProperty("directory");
        if (map.Directory == null)
        {
            AssertEqual(JsonValueKind.Null, directory.ValueKind);
        }
        else
        {
            AssertEqual((int)map.Directory.DirectoryTag, directory.GetProperty("tag").GetInt32());
            AssertEqual(map.Directory.Capacity, directory.GetProperty("capacity").GetInt32());
            AssertEqual((int)map.Directory.FormatVersion, directory.GetProperty("version").GetInt32());
            AssertEqual(map.Directory.SectionCount, directory.GetProperty("section_count").GetInt32());
            AssertEqual((int)map.Directory.PayloadSize, directory.GetProperty("payload_size").GetInt32());
            AssertEqual(map.Directory.PayloadOffset, directory.GetProperty("payload_offset").GetInt32());
        }

        JsonElement sections = root.GetProperty("sections");
        AssertEqual(map.Sections.Count, sections.GetArrayLength());
        for (int index = 0; index < map.Sections.Count; index++)
        {
            MapSectionInfo actual = map.Sections[index];
            JsonElement expected = sections[index];
            AssertEqual(actual.SectionId, expected.GetProperty("id").GetInt32());
            AssertEqual(actual.LogicalSectionId, expected.GetProperty("logical_id").GetInt32());
            AssertEqual(actual.StoredSize, expected.GetProperty("stored_size").GetInt32());
            AssertEqual(actual.UncompressedSize, expected.GetProperty("uncompressed_size").GetInt32());
            string storage = actual.StorageKind switch
            {
                MapSectionStorageKind.Raw => "raw",
                MapSectionStorageKind.PkwareDcl => "pkware-dcl",
                _ => "unavailable-zero-filled-dcl"
            };
            AssertEqual(storage, expected.GetProperty("storage").GetString());
            if (actual.IsContentAvailable)
                AssertEqual(Sha256(actual.ReadContent()).ToLowerInvariant(), expected.GetProperty("sha256").GetString());
            else
                AssertEqual(JsonValueKind.Null, expected.GetProperty("sha256").ValueKind);
        }
    }

    private static SectionSpec[] PlacementSections() =>
    [
        new SectionSpec(3003, Int32Bytes(1, 0x11223344, 3)),
        new SectionSpec(3037, [10, 11, 12]),
        new SectionSpec(3005, [20, 21, 22]),
        new SectionSpec(3045, [30, 31, 32]),
        new SectionSpec(3004, UInt16Bytes(100, 101, 102)),
        new SectionSpec(3012, UInt16Bytes(200, 201, 202)),
        new SectionSpec(3026, UInt16Bytes(300, 301, 302)),
        new SectionSpec(3043, [40, 41, 42])
    ];

    private static SectionSpec[] PlacementSnapshotSections(bool useNewIds)
    {
        int SectionId(int logicalId) => useNewIds ? logicalId + 2000 : logicalId;

        return
        [
            new SectionSpec(SectionId(MapSectionCatalog.Logic),
                Int32Layer(SnapshotSampleTileId, 0x11223344)),
            new SectionSpec(SectionId(MapSectionCatalog.Logic2),
                ByteLayer(SnapshotSampleTileId, 12)),
            new SectionSpec(SectionId(MapSectionCatalog.Height),
                ByteLayer(SnapshotSampleTileId, 22)),
            new SectionSpec(SectionId(MapSectionCatalog.DefaultHeight),
                ByteLayer(SnapshotSampleTileId, 32)),
            new SectionSpec(SectionId(MapSectionCatalog.Organism),
                UInt16Layer(SnapshotSampleTileId, 102)),
            new SectionSpec(SectionId(MapSectionCatalog.Building),
                UInt16Layer(SnapshotSampleTileId, 202)),
            new SectionSpec(SectionId(MapSectionCatalog.Entity),
                UInt16Layer(SnapshotSampleTileId, 302)),
            new SectionSpec(SectionId(MapSectionCatalog.WallOwner),
                ByteLayer(SnapshotSampleTileId, 42))
        ];
    }

    private static byte[] ByteLayer(int tileId, byte value)
    {
        var bytes = new byte[MapTileGeometry.FixedTileCount];
        bytes[tileId] = value;
        return bytes;
    }

    private static byte[] UInt16Layer(int tileId, ushort value)
    {
        var bytes = new byte[MapTileGeometry.FixedTileCount * 2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(tileId * 2), value);
        return bytes;
    }

    private static byte[] Int32Layer(int tileId, int value)
    {
        var bytes = new byte[MapTileGeometry.FixedTileCount * 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(tileId * 4), value);
        return bytes;
    }

    private static byte[] Int32Bytes(params int[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 4), values[index]);
        return bytes;
    }

    private static byte[] UInt16Bytes(params ushort[] values)
    {
        var bytes = new byte[values.Length * 2];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), values[index]);
        return bytes;
    }

    private static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value));

    private static void AssertPlacementTile(MapPlacementTile tile)
    {
        AssertEqual(0x11223344, tile.TerrainFlags);
        AssertEqual((byte)12, tile.SecondaryLogic);
        AssertEqual((byte)22, tile.Height);
        AssertEqual((byte)32, tile.DefaultHeight);
        AssertEqual((ushort)102, tile.OrganismId);
        AssertEqual((ushort)202, tile.BuildingId);
        AssertEqual((ushort)302, tile.EntityId);
        AssertEqual((byte)42, tile.OwnerId);
    }

    private static void AssertThrows<TException>(Action action) where TException : Exception
    {
        AssertThrowsAndGet<TException>(action);
    }

    private static TException AssertThrowsAndGet<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static void AssertTrue(bool value)
    {
        if (!value)
            throw new InvalidOperationException("Expected true.");
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }

    private static void AssertSequence(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
            throw new InvalidOperationException("Byte sequences differ.");
    }

    private static void Log(string state, string message) =>
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {state} {message}");

    private sealed record SectionSpec(int Id, byte[] Content, bool Compressed = false);

    private sealed class Fixture
    {
        public required byte[] Bytes { get; init; }
        public required int Capacity { get; init; }
        public required int DirectoryTagOffset { get; init; }
        public required int DirectoryBodyOffset { get; init; }
        public required int PayloadOffset { get; init; }

        public void WriteDirectoryValue(int arrayIndex, int sectionIndex, int value)
        {
            int offset = DirectoryBodyOffset + 28 + ((arrayIndex * Capacity + sectionIndex) * 4);
            BinaryPrimitives.WriteInt32LittleEndian(Bytes.AsSpan(offset), value);
        }
    }

    private static class FixtureBuilder
    {
        public static Fixture Build(int capacity, IReadOnlyList<SectionSpec> sections, byte[]? tail = null)
        {
            if (capacity is not (100 or 150 or 200))
                throw new ArgumentOutOfRangeException(nameof(capacity));
            using var preamble = new MemoryStream();
            using (var writer = new BinaryWriter(preamble, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(0xfffffffeu);
                writer.Write(0u); // radar
                writer.Write(0u); // description
                writer.Write(0u); // U1
                writer.Write(28u);
                writer.Write(7);
                for (int index = 0; index < 5; index++) writer.Write(0);
                writer.Write(4);

                byte[] name = Encoding.UTF8.GetBytes("Synthetic.map");
                writer.Write((uint)(16 + name.Length));
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write((uint)name.Length);
                writer.Write(name);

                writer.Write(84u);
                writer.Write(0);
                writer.Write(99);
                writer.Write(10);
                writer.Write(0);
                for (int index = 0; index < 8; index++)
                {
                    writer.Write(index * 10 + 1);
                    writer.Write(index * 10 + 2);
                }
                writer.Write(400);
                writer.Write(0u); // restart-info size
            }

            int tag = capacity * 20 + 36;
            var payloads = sections.Select(section =>
                section.Compressed ? WrapCompressed(section.Content) : section.Content).ToArray();
            int payloadSize = payloads.Sum(value => value.Length);
            int directoryTagOffset = checked((int)preamble.Length);
            int directoryBodyOffset = directoryTagOffset + 4;
            int payloadOffset = directoryTagOffset + tag;
            using var output = new MemoryStream();
            preamble.Position = 0;
            preamble.CopyTo(output);
            using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((uint)tag);
                writer.Write((uint)payloadSize);
                writer.Write((uint)sections.Count);
                writer.Write(236u);
                for (int index = 0; index < 4; index++) writer.Write(0u);

                WriteArray(writer, capacity, sections.Select(value => value.Content.Length));
                WriteArray(writer, capacity, payloads.Select(value => value.Length));
                WriteArray(writer, capacity, sections.Select(value => value.Id));
                WriteArray(writer, capacity, sections.Select(value => value.Compressed ? 1 : 0));
                int runningOffset = 0;
                WriteArray(writer, capacity, payloads.Select(value =>
                {
                    int current = runningOffset;
                    runningOffset += value.Length;
                    return current;
                }));
                writer.Write(0u);
                foreach (byte[] payload in payloads) writer.Write(payload);
                if (tail != null) writer.Write(tail);
            }

            return new Fixture
            {
                Bytes = output.ToArray(),
                Capacity = capacity,
                DirectoryTagOffset = directoryTagOffset,
                DirectoryBodyOffset = directoryBodyOffset,
                PayloadOffset = payloadOffset
            };
        }

        private static void WriteArray(BinaryWriter writer, int capacity, IEnumerable<int> values)
        {
            int count = 0;
            foreach (int value in values)
            {
                writer.Write((uint)value);
                count++;
            }
            while (count++ < capacity) writer.Write(0u);
        }

        private static byte[] WrapCompressed(byte[] content)
        {
            byte[] stream = DclLiteralEncoder.Encode(content);
            var result = new byte[12 + stream.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)content.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)stream.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), ComputeCrc32(content));
            stream.CopyTo(result.AsSpan(12));
            return result;
        }

        private static uint ComputeCrc32(byte[] data)
        {
            uint crc = 0xffffffffu;
            foreach (byte item in data)
            {
                crc ^= item;
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
            }
            return crc ^ 0xffffffffu;
        }
    }

    private static class DclLiteralEncoder
    {
        private static readonly byte[] LengthLengths = { 2, 35, 36, 53, 38, 23 };

        public static byte[] Encode(byte[] data)
        {
            var bits = new BitWriter();
            bits.Write(0, 8); // uncoded literals
            bits.Write(4, 8); // smallest valid dictionary
            foreach (byte value in data)
            {
                bits.Write(0, 1);
                bits.Write(value, 8);
            }
            bits.Write(1, 1);
            WriteHuffmanSymbol(bits, LengthLengths, 15);
            bits.Write(255, 8); // 264 + 255 = 519 end marker
            return bits.ToArray();
        }

        private static void WriteHuffmanSymbol(BitWriter writer, byte[] repeats, int targetSymbol)
        {
            var lengths = new List<int>();
            foreach (byte value in repeats)
            {
                int repeat = (value >> 4) + 1;
                for (int index = 0; index < repeat; index++) lengths.Add(value & 15);
            }

            int maxBits = lengths.Max();
            var counts = new int[maxBits + 1];
            foreach (int length in lengths) counts[length]++;
            int first = 0;
            for (int length = 1; length <= maxBits; length++)
            {
                int position = 0;
                for (int symbol = 0; symbol < lengths.Count; symbol++)
                {
                    if (lengths[symbol] != length) continue;
                    if (symbol == targetSymbol)
                    {
                        int code = first + position;
                        for (int bit = length - 1; bit >= 0; bit--)
                            writer.Write(((code >> bit) & 1) ^ 1, 1);
                        return;
                    }
                    position++;
                }
                first = (first + counts[length]) << 1;
            }
            throw new InvalidOperationException("Huffman symbol was not found.");
        }

        private sealed class BitWriter
        {
            private readonly List<byte> bytes = new();
            private int bitIndex;

            public void Write(int value, int count)
            {
                for (int index = 0; index < count; index++)
                {
                    if (bitIndex % 8 == 0) bytes.Add(0);
                    if (((value >> index) & 1) != 0)
                        bytes[^1] |= (byte)(1 << (bitIndex % 8));
                    bitIndex++;
                }
            }

            public byte[] ToArray() => bytes.ToArray();
        }
    }
}
