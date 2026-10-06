using APIShared;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AIBuildDiagnoseTest
{
    // Active evidence collection only on the named, byte-verified disposable save.
    // No AI search hook, coarse-grid writes, or placement-rule bypass is used here.
    internal sealed class FarmSiteContractProbe
    {
        private const string SaveName = "rat_farm_site_contract_probe.sav";
        private const string SourceHash = "9CE228735067734138E450CD91902C6F8F28FE75634F0952AC5F0643E20D378E";
        private const int PlayerId = 2;
        private const uint FarmFlag = 0x4;
        private const uint ExcludedSetupFlags = 0x20000000u | 0x1000u | 0x100u | FarmFlag;
        private static readonly FarmCase[] Definitions =
        {
            new FarmCase("apple", eMappers.MAPPER_APPLEFARM, 10),
            new FarmCase("wheat", eMappers.MAPPER_WHEATFARM, 9),
            new FarmCase("hops", eMappers.MAPPER_HOPSFARM, 9),
            new FarmCase("cattle", eMappers.MAPPER_CATTLEFARM, 10)
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long NativeRoute(IntPtr manager, int playerId, int mode, int x, int y);

        private readonly bool configured;
        private readonly ulong moduleBase;
        private readonly Action<string> log;
        private readonly Func<int, string> resources;
        private readonly List<FarmCase> placed = new List<FarmCase>();
        private NativeRoute route;
        private bool armed, finished, setupDone, generationObserved;
        private int setupIndex, observedTicks, activeSpawnCount;
        private string activeBuild;

        internal FarmSiteContractProbe(bool enabled, ulong nativeModuleBase,
            Action<string> writeLog, Func<int, string> readResources)
        {
            configured = enabled;
            moduleBase = nativeModuleBase;
            log = writeLog ?? throw new ArgumentNullException(nameof(writeLog));
            resources = readResources ?? throw new ArgumentNullException(nameof(readResources));
            if (nativeModuleBase != 0)
                route = (NativeRoute)Marshal.GetDelegateForFunctionPointer(
                    new IntPtr(checked((long)nativeModuleBase + 0xC3BF0)), typeof(NativeRoute));
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            armed = false;
            finished = setupDone = generationObserved = false;
            setupIndex = observedTicks = activeSpawnCount = 0;
            placed.Clear();
            activeBuild = null;
            if (!configured) return;
            string path = session.SaveFileName;
            if (!session.IsLoadedSave || session.IsEditor || session.IsReplay ||
                !string.Equals(Path.GetFileName(path ?? ""), SaveName, StringComparison.OrdinalIgnoreCase) ||
                moduleBase == 0 || route == null)
            {
                log("AI_BUILD_FARM_CONTRACT: armed=false; reason=session-name-mode-or-native-contract; expected=" + SaveName);
                return;
            }
            try
            {
                if (!File.Exists(path) || !string.Equals(Hash(path), SourceHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    log("AI_BUILD_FARM_CONTRACT: armed=false; reason=save-copy-sha256-mismatch; expected=" + SourceHash);
                    return;
                }
                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(PlayerId))
                {
                    log("AI_BUILD_FARM_CONTRACT: armed=false; reason=player-2-not-AI");
                    return;
                }
                armed = true;
                log("AI_BUILD_FARM_CONTRACT: armed=true; save=" + SaveName +
                    "; sha256=" + SourceHash + "; player=2; farms=apple,wheat,hops,cattle; " +
                    "farmSetupFreeCost=true; woodProbeFreeCost=false; " +
                    "bypassPlacementRules=false; no-save=true");
            }
            catch (Exception ex)
            {
                log("AI_BUILD_FARM_CONTRACT: armed=false; reason=preflight-exception:" + ex.GetType().Name);
            }
        }

        internal void OnSessionEnded()
        {
            if (armed)
                log("AI_BUILD_FARM_CONTRACT: ended; finished=" + finished +
                    "; placed=" + placed.Count + "; generationObserved=" + generationObserved);
            armed = false;
            activeBuild = null;
        }

        internal void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (armed && activeBuild != null && args.Phase == EventHookPhase.Post &&
                args.PlayerId == PlayerId && args.ReturnValue > 0)
                activeSpawnCount++;
        }

        internal void OnTick(int tick, int generation)
        {
            if (!armed || finished) return;
            observedTicks++;
            try
            {
                foreach (FarmCase farm in placed)
                    if (!farm.GenerationSnapshotDone && farm.SpawnGeneration >= 0 &&
                        generation >= 0 && generation != farm.SpawnGeneration)
                    {
                        farm.GenerationSnapshotDone = true;
                        Snapshot(farm, "first-path-generation-after-farm", tick, generation);
                    }
                if (!setupDone)
                {
                    if (setupIndex < Definitions.Length)
                    {
                        FarmCase farm = Definitions[setupIndex++];
                        PlaceFarm(farm, tick, generation);
                        return;
                    }
                    setupDone = true;
                    foreach (FarmCase farm in placed) Snapshot(farm, "after-all-farms", tick, generation);
                    log("AI_BUILD_FARM_CONTRACT: setup-complete; placed=" + placed.Count +
                        "; requested=4; pathGeneration=" + generation);
                    return;
                }
                bool allObserved = placed.Count != 0;
                foreach (FarmCase farm in placed)
                    if (!farm.GenerationSnapshotDone) allObserved = false;
                if (allObserved)
                {
                    generationObserved = true;
                    Complete(tick, generation, "first-path-generation-after-each-farm");
                }
                else if (observedTicks >= 250)
                    Complete(tick, generation, "generation-timeout-250-observed-ticks");
            }
            catch (Exception ex)
            {
                finished = true;
                log("AI_BUILD_FARM_CONTRACT: failed-closed; error=" + ex);
            }
        }

        private void PlaceFarm(FarmCase farm, int tick, int generation)
        {
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(PlayerId);
            int attempts = 0;
            for (int dy = -65; dy <= 65; dy += 5)
                for (int dx = -65; dx <= 65; dx += 5)
                {
                    // Offset four leaves the orchard's native +6..+8 gap aligned
                    // with a 5-grid wood anchor, avoiding its +5/+9 apple trees.
                    int x = FloorToFive(keep.X + dx) + 4;
                    int y = FloorToFive(keep.Y + dy) + 4;
                    if (!SetupRectangleClear(x, y, farm.Scale)) continue;
                    bool separated = true;
                    foreach (FarmCase prior in placed)
                        if (Math.Abs(prior.X - x) < 20 && Math.Abs(prior.Y - y) < 20)
                        { separated = false; break; }
                    if (!separated) continue;
                    if (++attempts > 96)
                    {
                        log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name +
                            "; status=inconclusive; reason=96-vanilla-placement-attempt-limit");
                        return;
                    }
                    int before = CountFarmTiles(x, y, farm.Scale);
                    long result = Build(farm.Name, x, y, farm.Mapper, farm.Scale, true);
                    int after = CountFarmTiles(x, y, farm.Scale);
                    if (result == 0 && after == before) continue;
                    if (result <= 0 || after <= before || activeSpawnCount <= 0)
                    {
                        log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name +
                            "; status=inconclusive; reason=construction-return-spawn-or-parcel-disagree; " +
                            "tile=(" + x + "," + y + "); result=" + result +
                            "; spawnEvents=" + activeSpawnCount + "; bit4Before=" + before +
                            "; bit4After=" + after);
                        return;
                    }
                    farm.X = x; farm.Y = y;
                    farm.SpawnGeneration = generation;
                    placed.Add(farm);
                    log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name + "; status=placed; " +
                        "tile=(" + x + "," + y + "); scale=" + farm.Scale +
                        "; attempts=" + attempts + "; spawnEvents=" + activeSpawnCount +
                        "; bit4After=" + after + "; tick=" + tick);
                    Snapshot(farm, "immediately-after-spawn", tick, generation);
                    return;
                }
            log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name +
                "; status=inconclusive; reason=no-valid-separated-site; attempts=" + attempts);
        }

        private void Complete(int tick, int generation, string stage)
        {
            finished = true; // Never repeat a native placement call after any failure.
            foreach (FarmCase farm in placed)
                Snapshot(farm, stage, tick, generation);
            int woodBefore = GamePlayerManagerAPI.Instance.GetGoodAmount(PlayerId,
                eGoods.STORED_WOOD_PLANKS);
            bool provisioned = woodBefore >= 20 ||
                GamePlayerManagerAPI.Instance.TryAddGood(PlayerId,
                    eGoods.STORED_WOOD_PLANKS, 50);
            int woodAfter = GamePlayerManagerAPI.Instance.GetGoodAmount(PlayerId,
                eGoods.STORED_WOOD_PLANKS);
            log("AI_BUILD_FARM_CONTRACT: resource-control; woodPlanksBefore=" + woodBefore +
                "; woodPlanksAfter=" + woodAfter + "; provisioned=" + provisioned +
                "; copy-only=true");
            if (woodAfter < 20)
            {
                log("AI_BUILD_FARM_CONTRACT: complete; status=inconclusive; " +
                    "reason=insufficient-wood-for-exact-AI-placement-argument");
                return;
            }
            foreach (FarmCase farm in placed)
            {
                ProbeSite(farm, "farm-parcel", true, tick);
                ProbeZeroAnchor(farm, tick);
                ProbeSite(farm, "outside-parcel", false, tick);
            }
            log("AI_BUILD_FARM_CONTRACT: complete; stage=" + stage + "; tick=" + tick +
                "; pathGeneration=" + generation + "; placed=" + placed.Count +
                "; resultScope=controlled-candidate-not-repeated-AI-loop");
        }

        private void ProbeSite(FarmCase farm, string kind, bool overlapFarm, int tick)
        {
            int bestX = -1, bestY = -1, bestBits = -1;
            for (int y = FloorToFive(farm.Y - 15); y <= farm.Y + farm.Scale + 15; y += 5)
                for (int x = FloorToFive(farm.X - 15); x <= farm.X + farm.Scale + 15; x += 5)
                {
                    if (!TryWoodFootprint(x, y, out int bits, out int component)) continue;
                    if ((overlapFarm && bits == 0) || (!overlapFarm && bits != 0)) continue;
                    if (overlapFarm && !OverlapsParcel(farm, x, y)) continue;
                    if (!CoarseEligible(x / 5, y / 5, out string coarse)) continue;
                    if (!overlapFarm && component == 0) continue;
                    if (bestX >= 0 && (overlapFarm ? bits <= bestBits : true)) continue;
                    bestX = x; bestY = y; bestBits = bits;
                }
            if (bestX < 0)
            {
                log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name + "; site=" + kind +
                    "; status=inconclusive; reason=no-coarse-eligible-clear-3x3-candidate");
                return;
            }
            TryWoodFootprint(bestX, bestY, out int parcelBits, out int anchorPcl);
            RunWoodProbe(farm, kind, bestX, bestY, parcelBits, anchorPcl, tick);
        }

        private void ProbeZeroAnchor(FarmCase farm, int tick)
        {
            for (int y = FloorToFive(farm.Y - 15); y <= farm.Y + farm.Scale + 15; y += 5)
                for (int x = FloorToFive(farm.X - 15); x <= farm.X + farm.Scale + 15; x += 5)
                {
                    if (!OverlapsParcel(farm, x, y) ||
                        !TryWoodFootprint(x, y, out int bits, out int component) || component != 0 ||
                        !CoarseEligible(x / 5, y / 5, out string raw)) continue;
                    if (x == farm.OverlapX && y == farm.OverlapY)
                    {
                        log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name +
                            "; site=zero-anchor; anchor=(" + x + "," + y +
                            "); alreadyProbedAs=farm-parcel; no-repeat=true");
                        return;
                    }
                    RunWoodProbe(farm, "zero-anchor", x, y, bits, component, tick);
                    return;
                }
            log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name +
                "; site=zero-anchor; status=none-observed-in-coarse-eligible-parcel-area");
        }

        private void RunWoodProbe(FarmCase farm, string kind, int bestX, int bestY,
            int parcelBits, int anchorPcl, int tick)
        {
            if (kind == "farm-parcel") { farm.OverlapX = bestX; farm.OverlapY = bestY; }
            CoarseEligible(bestX / 5, bestY / 5, out string coarseRaw);
            long routeResult = -1;
            try
            {
                routeResult = route(new IntPtr(checked((long)moduleBase + 0x64CCBB0)),
                    PlayerId, 3, bestX, bestY);
            }
            catch (Exception ex)
            {
                log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name + "; site=" + kind +
                    "; route=unavailable:" + ex.GetType().Name);
            }
            int before = CountBuildingTiles(bestX, bestY, 3);
            string resourcesBefore = resources(PlayerId);
            long buildResult = Build(farm.Name + "-" + kind, bestX, bestY,
                eMappers.MAPPER_WOODSMAN, 3, false);
            int after = CountBuildingTiles(bestX, bestY, 3);
            log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name + "; site=" + kind +
                "; anchor=(" + bestX + "," + bestY + "); tick=" + tick +
                "; coarseEligible=true; coarseRaw=" + coarseRaw +
                "; parcelBit4Tiles=" + parcelBits + "; anchorPcl=" + anchorPcl +
                "; routeResult=" + routeResult + "; placementResult=" + buildResult +
                "; spawnEvents=" + activeSpawnCount + "; buildingTilesBefore=" + before +
                "; buildingTilesAfter=" + after +
                "; resourcesBefore=" + resourcesBefore +
                "; resourcesAfter=" + resources(PlayerId) +
                "; verdict=" + (activeSpawnCount > 0 && after > before ? "spawned" : "no-spawn") +
                "; routeAndPlacementAreSeparate=true");
        }

        private static bool OverlapsParcel(FarmCase farm, int x, int y) =>
            x < farm.X + farm.Scale && x + 2 >= farm.X &&
            y < farm.Y + farm.Scale && y + 2 >= farm.Y;

        private long Build(string label, int x, int y, eMappers mapper, int scale,
            bool freeCost)
        {
            activeBuild = label;
            activeSpawnCount = 0;
            try
            {
                return GameBuildingManagerAPI.Instance.CreatePrefab(PlayerId, x, y,
                    mapper, scale, 15, freeCost, false);
            }
            finally { activeBuild = null; }
        }

        private bool SetupRectangleClear(int x, int y, int scale)
        {
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            for (int yy = y; yy < y + scale; yy++)
                for (int xx = x; xx < x + scale; xx++)
                {
                    if (!api.IsTileInsideMapBounds(xx, yy)) return false;
                    int id = api.GetTileId(xx, yy);
                    if (!api.IsValidTileId(id) || api.GetTileBuildingId(id) != 0 ||
                        (((uint)api.GetTilePropertyFlag(id)) & ExcludedSetupFlags) != 0)
                        return false;
                }
            return true;
        }

        private static bool TryWoodFootprint(int x, int y, out int farmBits, out int anchorPcl)
        {
            farmBits = 0; anchorPcl = -1;
            IReadOnlyList<AiPathTileSample> tiles = AiBuildDiagnostic.CaptureTiles(x, y, 3, 3);
            if (tiles.Count != 9 || tiles[0].Status != "ok" ||
                tiles[0].NativeComponent != tiles[0].ApiComponent) return false;
            anchorPcl = tiles[0].NativeComponent;
            foreach (AiPathTileSample tile in tiles)
            {
                if (tile.Status != "ok" || tile.NativeComponent != tile.ApiComponent ||
                    tile.BuildingId != 0 || tile.WallOwner != 0 ||
                    (tile.PropertyFlags & (0x1000u | 0x100u | 0x20000000u)) != 0)
                    return false;
                if ((tile.PropertyFlags & FarmFlag) != 0) farmBits++;
            }
            return true;
        }

        private static bool CoarseEligible(int cx, int cy, out string raw)
        {
            raw = "unavailable";
            if ((uint)cx >= 160 || (uint)cy >= 160) return false;
            Span<AivCoarseCell> grid = GameAIVManagerAPI.Instance.GetCoarseGrid();
            int index = cx * 160 + cy;
            if ((uint)index >= (uint)grid.Length) return false;
            AivCoarseCell c = grid[index];
            raw = "foreign=" + c.ForeignPathComponentTileCount +
                ",treeWeight=" + c.TreeObstructionWeight +
                ",stone=" + c.StoneTileCount +
                ",height=" + c.HeightRangeExceeds12 +
                ",reservation=" + c.StructureOrReservationCount +
                ",outside=" + c.OutsideUsableMap +
                ",edge=" + c.ImpassableEdgeTileCount;
            return c.ForeignPathComponentTileCount == 0 && c.TreeObstructionWeight == 0 &&
                c.StoneTileCount == 0 && c.HeightRangeExceeds12 == 0 &&
                c.StructureOrReservationCount == 0 && c.OutsideUsableMap == 0 &&
                c.ImpassableEdgeTileCount == 0;
        }

        private void Snapshot(FarmCase farm, string stage, int tick, int generation)
        {
            int bit4 = CountFarmTiles(farm.X, farm.Y, farm.Scale);
            int zero = 0, positive = 0, tree = 0;
            for (int y = farm.Y; y < farm.Y + farm.Scale; y++)
                for (int x = farm.X; x < farm.X + farm.Scale; x++)
                {
                    IReadOnlyList<AiPathTileSample> samples = AiBuildDiagnostic.CaptureTiles(x, y, 1, 1);
                    if (samples.Count == 0 || samples[0].Status != "ok") continue;
                    if (samples[0].NativeComponent == 0) zero++;
                    else positive++;
                    if ((samples[0].PropertyFlags & 0x1000u) != 0) tree++;
                }
            var cells = new List<string>();
            for (int cx = farm.X / 5 - 1; cx <= (farm.X + farm.Scale - 1) / 5 + 1; cx++)
                for (int cy = farm.Y / 5 - 1; cy <= (farm.Y + farm.Scale - 1) / 5 + 1; cy++)
                    if (CoarseEligible(cx, cy, out string raw))
                        cells.Add("(" + cx + "," + cy + "):eligible:" + raw);
                    else if (raw != "unavailable")
                        cells.Add("(" + cx + "," + cy + "):excluded:" + raw);
            log("AI_BUILD_FARM_CONTRACT: farm=" + farm.Name + "; stage=" + stage +
                "; tick=" + tick + "; generation=" + generation +
                "; origin=(" + farm.X + "," + farm.Y + "); scale=" + farm.Scale +
                "; parcelBit4=" + bit4 + "; pclZero=" + zero +
                "; pclPositive=" + positive + "; treeTiles=" + tree +
                "; coarseCells=" + string.Join("|", cells));
        }

        private static int CountFarmTiles(int x, int y, int scale)
        {
            int count = 0;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            for (int yy = y; yy < y + scale; yy++)
                for (int xx = x; xx < x + scale; xx++)
                    if (api.IsTileInsideMapBounds(xx, yy) &&
                        (((uint)api.GetTilePropertyFlag(api.GetTileId(xx, yy))) & FarmFlag) != 0)
                        count++;
            return count;
        }

        private static int CountBuildingTiles(int x, int y, int scale)
        {
            int count = 0;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            for (int yy = y; yy < y + scale; yy++)
                for (int xx = x; xx < x + scale; xx++)
                    if (api.IsTileInsideMapBounds(xx, yy) &&
                        api.GetTileBuildingId(api.GetTileId(xx, yy)) != 0)
                        count++;
            return count;
        }

        private static int FloorToFive(int value) => (int)Math.Floor(value / 5.0) * 5;

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private sealed class FarmCase
        {
            internal FarmCase(string name, eMappers mapper, int scale)
            { Name = name; Mapper = mapper; Scale = scale; }
            internal readonly string Name;
            internal readonly eMappers Mapper;
            internal readonly int Scale;
            internal int X, Y;
            internal int OverlapX = -1, OverlapY = -1;
            internal int SpawnGeneration = -1;
            internal bool GenerationSnapshotDone;
        }
    }
}
