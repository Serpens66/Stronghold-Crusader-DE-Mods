using AIVParser.Core;
using CastlePlanner;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using AivRotation = AIVParser.Core.AivRotation;

internal static class SupplementalSpawnTests
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }

    internal static void Run()
    {
        CheckMiscPlacement();
        CheckSpawnReports();
        int mapper = (int)eMappers.MAPPER_STORES;
        int size = AivMapperCatalog.Resolve(mapper).FootprintSize.Value;
        foreach (AivRotation rotation in Enum.GetValues<AivRotation>())
        {
            var visited = new HashSet<AivWorldTile>();
            Check(SupplementalPlacementPolicy.CanPlace(mapper, 5050, 200, 200, rotation,
                tile => { visited.Add(tile); return ""; }, out _), "valid footprint");
            Check(visited.Count == size * size, "entire rotated footprint is checked");
            AivWorldTile origin = AivNativeBuildingPlacement.ResolveBuildStructureOrigin(new AivGridPoint(5050), size, 200, 200, rotation);
            Check(visited.All(tile => tile.X >= origin.X && tile.X < origin.X + size &&
                tile.Y >= origin.Y && tile.Y < origin.Y + size), "checked footprint matches native build origin");
            AivWorldTile corner = visited.Last();
            foreach (TilePropertyFlag flag in new[] { TilePropertyFlag.Sea, TilePropertyFlag.River,
                TilePropertyFlag.Ford, TilePropertyFlag.IsMoat, TilePropertyFlag.MapBorder,
                TilePropertyFlag.RealityEdge, TilePropertyFlag.IsWall, TilePropertyFlag.IsBuilding,
                TilePropertyFlag.IsElevated, TilePropertyFlag.ImpassableEdge })
                Check(!SupplementalPlacementPolicy.CanPlace(mapper, 5050, 200, 200, rotation,
                    tile => SupplementalPlacementPolicy.CheckPhysicalTile(mapper, 0, 0,
                        tile.Equals(corner) ? flag : TilePropertyFlag.IsLand), out _), "partially obstructed footprint: " + flag);
            foreach (string obstacle in new[] { "invalid-map-tile", "footprint-out-of-bounds" })
                Check(!SupplementalPlacementPolicy.CanPlace(mapper, 5050, 200, 200, rotation,
                    tile => tile.Equals(corner) ? obstacle : "", out _), "partly invalid footprint");
            Check(!SupplementalPlacementPolicy.CanPlace(mapper, 0, 200, 200, rotation, _ => "", out _), "source edge rejected");
        }
        Check(SupplementalPlacementPolicy.CheckPhysicalTile(mapper, 7, 0, TilePropertyFlag.IsLand) != "", "occupied tile rejected");
        Check(SupplementalPlacementPolicy.CheckPhysicalTile(mapper, 0, 201, TilePropertyFlag.IsLand) != "", "native height bound");
        foreach (int anchor in new[] { -1, 10000 })
            Check(!SupplementalPlacementPolicy.CanPlace(mapper, anchor, 200, 200, AivRotation.Degrees0,
                _ => throw new Exception("invalid anchor reached map lookup"), out _), "invalid source anchor rejected before lookup");

        var spawner = new SupplementalPrefabSpawner();
        eMappers prefabMapper = eMappers.MAPPER_GARDEN1;
        eStructs type = prefabMapper.ConvertToEStructs();
        var pool = new Dictionary<int, SupplementalBuildingIdentity>();
        SupplementalBuildingIdentity? Lookup(int id) => pool.TryGetValue(id, out var value) ? value : null;
        SupplementalBuildingIdentity Identity(uint global = 100, int owner = 1, int x = 20, int y = 30,
            AliveState state = AliveState.NeedsInit, eStructs? kind = null) =>
            new SupplementalBuildingIdentity(global, owner, kind ?? type, x, y, state);
        pool[10] = Identity();
        int calls = 0;
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => { calls++; return 10; }, Lookup, out _) < 0 && calls == 1,
            "raw return 10 never adopts an existing building or retries");
        pool[11] = Identity();
        long Spawn(int variation = 15) { Check(variation == 15, "automatic sprite selection reaches native delegate"); spawner.Observe(11, 1, type, 20, 30, Lookup); return 10; }
        Check(spawner.Create(1, prefabMapper, 20, 30, Spawn, Lookup, out _) == 11, "captured ID replaces raw status");
        pool[11] = Identity(state: AliveState.IsAlive);
        Check(spawner.Create(1, prefabMapper, 20, 30, Spawn, Lookup, out _) == 11, "alive identity accepted");
        foreach (var bad in new[] { Identity(global: 0), Identity(owner: 2), Identity(x: 21), Identity(y: 31),
            Identity(state: AliveState.MarkedForDeletion), Identity(kind: eStructs.STRUCT_NULL) })
        {
            pool[11] = bad;
            Check(spawner.Create(1, prefabMapper, 20, 30, Spawn, Lookup, out _) < 0, "incorrect native identity rejected");
        }
        pool[11] = Identity();
        foreach (long invalidId in new[] { 0L, -1L, (long)int.MaxValue + 1 })
            Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => {
                spawner.Observe(invalidId, 1, type, 20, 30, ignoredId => throw new Exception("invalid ID reached native lookup")); return 10;
            }, Lookup, out _) < 0, "invalid event ID rejected before lookup");
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => {
            spawner.Observe(11, 2, type, 20, 30, Lookup); return 10;
        }, Lookup, out _) < 0, "foreign event cannot fulfill request");
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => { Spawn(); pool[11] = Identity(global: 101); return 10; }, Lookup, out _) < 0,
            "reused slot after Post rejected");
        pool[11] = Identity();
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => { Spawn(); Spawn(); return 10; }, Lookup, out _) < 0, "ambiguous multiple events rejected");
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => {
            Check(spawner.Create(1, prefabMapper, 20, 30, Spawn, Lookup, out _) == 11, "inner capture");
            return 10;
        }, Lookup, out _) < 0, "inner event cannot fulfill identical outer request");
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => {
            Check(spawner.Create(1, prefabMapper, 20, 30, Spawn, Lookup, out _) == 11, "nested successful capture");
            return Spawn();
        }, Lookup, out _) == 11, "outer capture restored after nested success");
        try { spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => throw new InvalidOperationException("native failure"), Lookup, out _); }
        catch (InvalidOperationException) { }
        Check(spawner.Create(1, prefabMapper, 20, 30, Spawn, Lookup, out _) == 11, "capture restored after exception");
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => {
            try { spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => throw new InvalidOperationException(), Lookup, out _); }
            catch (InvalidOperationException) { }
            return Spawn();
        }, Lookup, out _) == 11, "outer capture restored after nested exception");
        Check(spawner.Create(1, prefabMapper, 20, 30, ignoredVariation => {
            Task.Run(() => Spawn()).GetAwaiter().GetResult(); return 10;
        }, Lookup, out _) < 0, "other thread cannot fulfill synchronous capture");
        // Enumerate the actual catalog-driven routes rather than assuming future
        // FearFactor additions automatically accept the same native argument.
        var expected = Enum.GetValues<eMappers>().Distinct().Where(value =>
            AivSpawnPlan.ClassifyFrame((int)value) == AivFrameSpawnCategory.FearFactor).ToHashSet();
        foreach (eMappers value in Enum.GetValues<eMappers>().Distinct())
        {
            Check(SupplementalPrefabVisualPolicy.TryGetVariation(value, out int variation) == expected.Contains(value),
                "audited policy matches every supported supplemental route: " + value);
            if (!expected.Contains(value)) continue;
            Check(variation == 15, "Vanilla automatic variation: " + value);
            eStructs expectedType = value.ConvertToEStructs();
            foreach (AivRotation rotation in Enum.GetValues<AivRotation>())
            {
                int footprint = AivMapperCatalog.Resolve((int)value).FootprintSize.Value;
                AivWorldTile target = AivNativeBuildingPlacement.ResolveBuildStructureOrigin(
                    new AivGridPoint(5050), footprint, 200, 200, rotation);
                pool[12] = Identity(kind: expectedType, x: target.X, y: target.Y);
                int count = 0;
                Check(spawner.Create(1, value, target.X, target.Y, actualVariation => {
                    Check(actualVariation == 15, "native argument for " + value + " / " + rotation);
                    count++;
                    spawner.Observe(12, 1, expectedType, target.X, target.Y, Lookup);
                    return 10;
                }, Lookup, out _) == 12 && count == 1, "all supplemental types use one verified spawn");
            }
        }
        Check(spawner.Create(1, unchecked((eMappers)(-1)), 20, 30,
            _ => throw new Exception("unsupported mapper reached native creation"),
            _ => throw new Exception("unsupported mapper reached lookup"), out string unsupported) < 0 &&
            unsupported.Contains("unsupported-supplemental-visual"), "unknown visual contract rejected without side effects");
    }

    private static void CheckSpawnReports()
    {
        var report = new SupplementalSpawnReport();
        Check(report.Count == 0 && !report.Summaries.Any(), "successful spawn batch has no omission summary");
        string[] expected = { "out-of-bounds", "footprint-out-of-bounds", "water-terrain",
            "impassable-terrain", "physical-terrain-obstacle", "occupied-or-already-created",
            "duplicate-prefab-request", "decoration-already-present-or-queued" };
        string[] technical = { "source-anchor-out-of-bounds", "source-footprint-out-of-bounds",
            "invalid-map-tile", "unknown-misc", "unsupported-supplemental-visual(mapper=-1)",
            "creation-threw", "unit-creation-threw", "decoration-creation-threw",
            "invalid-decoration-coordinate", "invalid-projectile-id" };
        string[] unexplained = { "unit-creation-failed", "decoration-creation-failed",
            "decoration-verification-failed", "unconfirmed-spawn(events=0, capturedId=0)",
            "changed-spawn-identity(id=12, globalId=100)", "new-native-rejection",
            "water-terrain(unrecognized-extension)", "", null };
        foreach (string reason in expected)
        {
            Check(SupplementalSpawnReport.CategoryFor(reason) == "expected", "known normal omission: " + reason);
            report.Add(reason, "expected object");
        }
        foreach (string reason in technical)
        {
            Check(SupplementalSpawnReport.CategoryFor(reason) == "technical", "technical failure: " + reason);
            report.Add(reason, "technical object");
        }
        foreach (string reason in unexplained)
        {
            Check(SupplementalSpawnReport.CategoryFor(reason) == "unexplained", "no inferred space problem: " + reason);
            report.Add(reason, "state=MarkedForDeletion, flyGrid=0, buildingId=7, tileFlags=IsWall");
        }
        Check(report.Count == expected.Length + technical.Length + unexplained.Length && report.Summaries.Count() == 3,
            "mixed batch keeps separate category totals");
        foreach (SupplementalSpawnSummary summary in report.Summaries)
        {
            int count = summary.IsExpected ? expected.Length : summary.Category == "technical" ? technical.Length : unexplained.Length;
            Check(summary.Count == count && summary.Reasons.Values.Sum() == count, "category conserves omission count");
            Check(summary.IsExpected == (summary.Category == "expected"), "only expected category routes to Info");
            Check(summary.Examples.Count == 5, "examples bounded separately for every category");
            Check(summary.ToString().Contains("category=" + summary.Category), "summary carries its classification");
        }

        var repeated = new SupplementalSpawnReport();
        for (int index = 0; index < 50; index++)
            repeated.Add($"unconfirmed-spawn(events={index}, capturedId=0)", "object " + index);
        SupplementalSpawnSummary grouped = repeated.Summaries.Single();
        Check(grouped.Category == "unexplained" && grouped.Count == 50 && grouped.Reasons.Count == 1 &&
            grouped.Reasons["unconfirmed-spawn"] == 50 && grouped.Examples.Count == 5 &&
            !grouped.ToString().Contains("object 5:"), "variable capture observations preserve bounded aggregation");
        for (int index = 0; index < 50; index++) repeated.Add("unknown-reason-" + index, "unknown object");
        Check(grouped.Count == 100 && grouped.Reasons.Count <= 17 && grouped.Reasons.Values.Sum() == 100 &&
            grouped.Examples.Count == 5 && grouped.Reasons.ContainsKey("other-reasons"),
            "new reasons stay warning-classified and bounded without dropping counts");
    }

    private static void CheckMiscPlacement()
    {
        var blocked = new[] { TilePropertyFlag.Sea, TilePropertyFlag.River,
            TilePropertyFlag.Ford, TilePropertyFlag.IsMoat, TilePropertyFlag.ImpassableEdge,
            TilePropertyFlag.IsLand | TilePropertyFlag.ImpassableEdge,
            TilePropertyFlag.HasStone | TilePropertyFlag.ImpassableEdge };
        var allowed = new[] { TilePropertyFlag.IsLand, TilePropertyFlag.StoneResource,
            TilePropertyFlag.NormalWall, TilePropertyFlag.CrenelatedWall,
            TilePropertyFlag.Stairs, TilePropertyFlag.ElevatedPosition,
            TilePropertyFlag.IsWall | TilePropertyFlag.ImpassableEdge,
            TilePropertyFlag.IsElevated | TilePropertyFlag.ImpassableEdge };
        foreach (TilePropertyFlag flags in allowed)
            Check(SupplementalPlacementPolicy.CheckMiscTerrain(flags) == "", "valid misc terrain: " + flags);
        foreach (TilePropertyFlag water in new[] { TilePropertyFlag.Sea, TilePropertyFlag.River,
            TilePropertyFlag.Ford, TilePropertyFlag.IsMoat })
            foreach (TilePropertyFlag surface in allowed)
                Check(SupplementalPlacementPolicy.CheckMiscTerrain(water | surface) == "water-terrain",
                    "fortification/resource flags cannot override water: " + (water | surface));

        foreach (AivMiscItemType type in new[] { AivMiscItemType.Flag, AivMiscItemType.Brazier,
            AivMiscItemType.Mangonel, AivMiscItemType.TowerMountedBallista,
            AivMiscItemType.Trebuchet, AivMiscItemType.FireBallista })
        foreach (int encoding in new[] { (int)type, (int)type + 9000 })
        foreach (AivRotation rotation in Enum.GetValues<AivRotation>())
        {
            AivMiscSpawnCategory expected = type == AivMiscItemType.Flag || type == AivMiscItemType.Brazier
                ? AivMiscSpawnCategory.Decoration : AivMiscSpawnCategory.SiegeEngine;
            Check(AivSpawnPlan.ClassifyMisc(encoding) == expected, "supplemental category: " + encoding);
            AivWorldTile projected = AivWorldTransform.ProjectNativeFit(new AivGridPoint(3057), 200, 200, rotation);
            foreach (TilePropertyFlag flags in blocked.Concat(allowed))
            {
                int lookups = 0;
                bool accepted = SupplementalPlacementPolicy.TryResolveMiscTile(3057, 200, 200, rotation,
                    tile => {
                        lookups++;
                        Check(tile.Equals(projected), "terrain checked at rotated spawn tile");
                        return SupplementalPlacementPolicy.CheckMiscTerrain(flags);
                    }, out AivWorldTile resolved, out string reason);
                Check(accepted == allowed.Contains(flags) && accepted == string.IsNullOrEmpty(reason),
                    "misc terrain admission: " + type + "/" + rotation + "/" + flags);
                Check(lookups == 1 && resolved.Equals(projected), "single target lookup without relocation");
            }
            foreach (int anchor in new[] { -1, AivGridPoint.GridSize * AivGridPoint.GridSize })
                Check(!SupplementalPlacementPolicy.TryResolveMiscTile(anchor, 200, 200, rotation,
                    _ => throw new Exception("invalid misc source reached map lookup"), out _, out string reason) &&
                    reason == "source-anchor-out-of-bounds", "misc source rejected before lookup");
            foreach (string failure in new[] { "out-of-bounds", "invalid-map-tile" })
                Check(!SupplementalPlacementPolicy.TryResolveMiscTile(3057, 200, 200, rotation,
                    _ => failure, out _, out string reason) && reason == failure, "map rejection prevents misc admission");
        }
    }
}
