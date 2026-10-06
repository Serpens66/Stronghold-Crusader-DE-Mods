using APIShared;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace AIBuildDiagnoseTest
{
    // Mutates only one of three named, byte-verified disposable Canari copies.
    // The original save and ordinary sessions are always observation-only.
    internal sealed unsafe class CanariFarmSwapProbe
    {
        private const string OriginalHash =
            "17BAB0CA7C73F1254765CEC0DD6BCFEDBA73343D74DE1D7F35DD6715C3EAC31A";
        private const int OrchardX = 344, OrchardY = 480, OrchardSize = 10;
        private const uint FarmBit = 0x4, AppleBit = 0x04000000, TreeBit = 0x1000;
        private const int TimeoutTicks = 1200;
        private static readonly int[] TreeX = { 5, 9, 1, 5, 9, 1, 5, 9 };
        private static readonly int[] TreeY = { 1, 1, 5, 5, 5, 9, 9, 9 };
        private readonly bool configured;
        private readonly Action<string> log;
        private string farmName;
        private eMappers mapper;
        private eStructs structure;
        private int scale;
        private int state, owner, oldId, oldGlobal, replacementId, replacementSpawns;
        private int stateTick, removalGeneration, buildGeneration, attempts;
        private bool creating;
        private bool armed;
        private int deferredWoodCalls, reportedDeferredWoodCalls;

        internal CanariFarmSwapProbe(bool enabled, Action<string> writeLog)
        { configured = enabled; log = writeLog ?? throw new ArgumentNullException(nameof(writeLog)); }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            state = owner = oldId = oldGlobal = replacementId = replacementSpawns = 0;
            stateTick = removalGeneration = buildGeneration = attempts = 0;
            creating = false;
            armed = false;
            deferredWoodCalls = reportedDeferredWoodCalls = 0;
            farmName = null;
            if (!configured || !session.IsLoadedSave || session.IsEditor || session.IsReplay) return;
            string name = Path.GetFileName(session.SaveFileName ?? "");
            if (name.Equals("test_canari_farm_swap_cattle.sav", StringComparison.OrdinalIgnoreCase))
            { farmName = "cattle"; mapper = eMappers.MAPPER_CATTLEFARM;
                structure = eStructs.STRUCT_CATTLEFARM; scale = 10; }
            else if (name.Equals("test_canari_farm_swap_wheat.sav", StringComparison.OrdinalIgnoreCase))
            { farmName = "wheat"; mapper = eMappers.MAPPER_WHEATFARM;
                structure = eStructs.STRUCT_WHEATFARM; scale = 9; }
            else if (name.Equals("test_canari_farm_swap_hops.sav", StringComparison.OrdinalIgnoreCase))
            { farmName = "hops"; mapper = eMappers.MAPPER_HOPSFARM;
                structure = eStructs.STRUCT_HOPSFARM; scale = 9; }
            else return;
            try
            {
                if (!File.Exists(session.SaveFileName) ||
                    !string.Equals(Hash(session.SaveFileName), OriginalHash,
                        StringComparison.OrdinalIgnoreCase))
                { Fail("save-hash-mismatch"); return; }
                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(6))
                { Fail("player-6-not-ai"); return; }
                state = 1;
                armed = true;
                log("AI_BUILD_CANARI_SWAP: armed=true; farm=" + farmName +
                    "; sourceSha256=" + OriginalHash + "; origin=(344,480); " +
                    "copyOnly=true; placementBypass=false; noSave=true");
            }
            catch (Exception ex) { Fail("startup-exception:" + ex.GetType().Name); }
        }

        internal void OnSessionEnded()
        {
            if (farmName != null)
                log("AI_BUILD_CANARI_SWAP: end; farm=" + farmName + "; state=" + state +
                    "; replacementId=" + replacementId + "; woodAttempts=" + attempts +
                    "; woodDeferred=" + deferredWoodCalls);
            state = 0;
            armed = false;
        }

        internal bool ShouldDeferWoodBuild(int playerId)
        {
            if (!armed || playerId != 6 || Volatile.Read(ref state) == 5 ||
                Volatile.Read(ref state) == 0) return false;
            Interlocked.Increment(ref deferredWoodCalls);
            return true;
        }

        internal void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (creating && args.Phase == EventHookPhase.Post &&
                args.Building == structure && args.PlayerId == owner &&
                args.TileX == OrchardX && args.TileY == OrchardY &&
                args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue)
            { replacementSpawns++; replacementId = (int)args.ReturnValue; }
        }

        internal void OnNativeRecord(AiBuildDiagnosticRecord record)
        {
            if (!armed || record.PlayerId != 6) return;
            if (record.Stage == "wood-build-deferred") return;
            if (state == 0 || state == -1) return;
            if (record.Stage == "wood-build-before")
            {
                attempts++;
                if (state != 5)
                { Fail("player-6-wood-before-swap-complete"); return; }
            }
            if (attempts > 2) return;
            if (record.Stage == "wood-search-after" || record.Stage == "wood-nearby-after" ||
                record.Stage == "route-result" || record.Stage == "wood-build-after")
                log("AI_BUILD_CANARI_WOOD: farm=" + farmName + "; attempt=" + attempts +
                    "; id=" + record.AttemptId + "; stage=" + record.Stage +
                    "; a=" + record.A + "; b=" + record.B + "; c=" + record.C +
                    "; d=" + record.D + "; originalCell=(69,97)" +
                    (record.Stage == "wood-nearby-after"
                        ? "; sameOriginalCell=" + (record.A == 69 && record.B == 97)
                        : ""));
        }

        internal void OnTick(int tick, int generation)
        {
            int deferred = Volatile.Read(ref deferredWoodCalls);
            if (armed && deferred != reportedDeferredWoodCalls)
            {
                log("AI_BUILD_CANARI_WOOD_GATE: farm=" + farmName +
                    "; deferredTotal=" + deferred + "; state=" + state +
                    "; tick=" + tick + "; generation=" + generation);
                reportedDeferredWoodCalls = deferred;
            }
            if (state <= 0) return;
            try
            {
                if (state == 1)
                {
                    int matches = 0;
                    Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                    for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                    {
                        ref GameBuilding b = ref buildings[spanIndex];
                        if (b.r_BuildingType != eStructs.STRUCT_APPLEFARM ||
                            b.r_TilePositionXBegin != OrchardX ||
                            b.r_TilePositionYBegin != OrchardY ||
                            b.r_AliveState != AliveState.IsAlive) continue;
                        matches++; owner = b.r_PlayerIdOwner; oldId = spanIndex + 1;
                        oldGlobal = checked((int)b.r_GlobalId);
                        if (b.r_OccupyTileGridSize == 0 ||
                            b.r_OccupyTileGridSize > OrchardSize)
                            Fail("orchard-building-footprint-invalid:" +
                                b.r_OccupyTileGridSize);
                    }
                    if (state < 0) return;
                    if (matches != 1 || owner < 1 || owner > 8)
                    { Fail("orchard-identity-not-unique:" + matches); return; }
                    if (!OriginalOrchardPresent())
                    { Fail("orchard-parcel-or-tree-contract-mismatch"); return; }
                    Snapshot("before-delete", tick, generation);
                    if (!GameBuildingManagerAPI.Instance.DeleteBuildingSafe(oldId))
                    { Fail("delete-safe-refused"); return; }
                    removalGeneration = generation;
                    stateTick = tick;
                    state = 2;
                    log("AI_BUILD_CANARI_SWAP: delete-marked; oldId=" + oldId +
                        "; global=" + oldGlobal + "; owner=" + owner +
                        "; tick=" + tick + "; generation=" + generation);
                    return;
                }
                if (state == 2)
                {
                    if (tick - stateTick > TimeoutTicks)
                    { Snapshot("delete-timeout", tick, generation);
                        Fail("delete-cleanup-timeout:" + DescribeDeleteBarrier(generation)); return; }
                    if (!OldBuildingGone() || !ParcelClean()) return;
                    if (generation < 0 || generation == removalGeneration) return;
                    Snapshot("after-native-delete", tick, generation);
                    state = 3; stateTick = tick;
                    return; // Build on a subsequent tick only after verified cleanup.
                }
                if (state == 3 && tick > stateTick)
                {
                    replacementSpawns = replacementId = 0;
                    creating = true;
                    long result;
                    try
                    {
                        result = GameBuildingManagerAPI.Instance.CreatePrefab(owner,
                            OrchardX, OrchardY, mapper, scale, 15, true, false);
                    }
                    finally { creating = false; }
                    if (result <= 0 || replacementSpawns != 1 || !ReplacementPresent())
                    {
                        Snapshot("replacement-failed", tick, generation);
                        string reason = result <= 0 ? "vanilla-placement-rejected" :
                            replacementSpawns != 1 ? "spawn-event-not-unique" :
                            "identity-or-parcel-mismatch";
                        Fail("replacement-not-confirmed:" + reason +
                             ":result=" + result + ":spawns=" + replacementSpawns);
                        return;
                    }
                    buildGeneration = generation;
                    stateTick = tick;
                    state = 4;
                    Snapshot("after-replacement", tick, generation);
                    log("AI_BUILD_CANARI_SWAP: replacement-confirmed; farm=" + farmName +
                        "; owner=" + owner + "; id=" + replacementId +
                        "; scale=" + scale + "; tick=" + tick +
                        "; result=" + result + "; freeCost=true");
                    return;
                }
                if (state == 4 && generation >= 0 && generation != buildGeneration &&
                    ReplacementAlive())
                {
                    if (!ReplacementPresent() || !OldAppleTilesGone())
                    { Snapshot("replacement-lost-after-path-rebuild", tick, generation);
                        Fail("replacement-or-orchard-cleanup-lost"); return; }
                    Snapshot("after-path-rebuild", tick, generation);
                    state = 5;
                    log("AI_BUILD_CANARI_WOOD_GATE: released=true; farm=" + farmName +
                        "; tick=" + tick + "; generation=" + generation +
                        "; deferredTotal=" + deferredWoodCalls);
                }
                else if (state == 4 && tick - stateTick > TimeoutTicks)
                { Snapshot("path-rebuild-not-observed", tick, generation);
                    Fail("replacement-path-rebuild-timeout"); }
            }
            catch (Exception ex) { Fail("exception:" + ex.GetType().Name + ":" + ex.Message); }
        }

        private bool OldBuildingGone()
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(oldId, out GameBuilding* b) ||
                b == null) return true;
            return b->r_GlobalId != oldGlobal || b->r_AliveState == AliveState.MarkedForDeletion;
        }

        private string DescribeDeleteBarrier(int generation)
        {
            string building = "unavailable";
            if (GameBuildingManagerAPI.Instance.TryGetBuildingById(oldId,
                out GameBuilding* b) && b != null)
                building = b->r_GlobalId == oldGlobal ? b->r_AliveState.ToString() :
                    "identity-changed";
            IReadOnlyList<AiPathTileSample> tiles =
                CaptureRectangle(OrchardX, OrchardY, OrchardSize, OrchardSize);
            int parcel = 0, apple = 0, tree = 0, occupied = 0;
            foreach (AiPathTileSample t in tiles)
            {
                if (t.Status != "ok") continue;
                if ((t.PropertyFlags & FarmBit) != 0) parcel++;
                if ((t.PropertyFlags & AppleBit) != 0) apple++;
                if ((t.PropertyFlags & TreeBit) != 0) tree++;
                if (t.BuildingId == oldId) occupied++;
            }
            return "building=" + building + ",plotBit4=" + parcel +
                ",apple=" + apple + ",tree=" + tree + ",occupied=" + occupied +
                ",generation=" + generation + ",markedAt=" + removalGeneration;
        }

        private bool ParcelClean()
        {
            IReadOnlyList<AiPathTileSample> tiles =
                CaptureRectangle(OrchardX, OrchardY, OrchardSize, OrchardSize);
            if (tiles.Count != 100) return false;
            foreach (AiPathTileSample t in tiles)
                if (t.Status != "ok" || t.BuildingId == oldId ||
                    (t.PropertyFlags & (FarmBit | AppleBit)) != 0) return false;
            for (int i = 0; i < TreeX.Length; i++)
            {
                AiPathTileSample t = tiles[TreeY[i] * OrchardSize + TreeX[i]];
                if ((t.PropertyFlags & TreeBit) != 0 || t.Organism > 0) return false;
            }
            return true;
        }

        private bool OriginalOrchardPresent()
        {
            IReadOnlyList<AiPathTileSample> tiles =
                CaptureRectangle(OrchardX, OrchardY, OrchardSize, OrchardSize);
            if (tiles.Count != OrchardSize * OrchardSize) return false;
            int farm = 0, apple = 0, occupied = 0;
            foreach (AiPathTileSample t in tiles)
            {
                if (t.Status != "ok") return false;
                if ((t.PropertyFlags & FarmBit) != 0) farm++;
                if ((t.PropertyFlags & AppleBit) != 0) apple++;
                if (t.BuildingId == oldId) occupied++;
            }
            log("AI_BUILD_CANARI_SWAP: original-parcel; farm=" + farmName +
                "; oldId=" + oldId + "; buildingOccupiedTiles=" + occupied +
                "; parcelBit4=" + farm + "; appleTreeFlags=" + apple +
                "; separateBuildingAndParcelSizes=true");
            return farm == 100 && apple == TreeX.Length && occupied == 9;
        }

        private bool ReplacementPresent()
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(replacementId,
                    out GameBuilding* b) || b == null ||
                b->r_BuildingType != structure || b->r_PlayerIdOwner != owner ||
                b->r_TilePositionXBegin != OrchardX ||
                b->r_TilePositionYBegin != OrchardY ||
                b->r_OccupyTileGridSize == 0 ||
                b->r_OccupyTileGridSize > scale) return false;
            IReadOnlyList<AiPathTileSample> tiles =
                CaptureRectangle(OrchardX, OrchardY, scale, scale);
            if (tiles.Count != scale * scale) return false;
            foreach (AiPathTileSample t in tiles)
                if (t.Status != "ok" || (t.PropertyFlags & FarmBit) == 0) return false;
            log("AI_BUILD_CANARI_SWAP: replacement-parcel; farm=" + farmName +
                "; buildingFootprintSize=" + b->r_OccupyTileGridSize +
                "; parcelSize=" + scale + "; parcelBit4=" + tiles.Count +
                "; identityAndOwnerConfirmed=true");
            return true;
        }

        private bool OldAppleTilesGone()
        {
            IReadOnlyList<AiPathTileSample> tiles =
                CaptureRectangle(OrchardX, OrchardY, OrchardSize, OrchardSize);
            if (tiles.Count != OrchardSize * OrchardSize) return false;
            foreach (AiPathTileSample tile in tiles)
                if (tile.Status != "ok" || (tile.PropertyFlags & AppleBit) != 0)
                    return false;
            return true;
        }

        private bool ReplacementAlive()
        {
            return replacementId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(replacementId,
                    out GameBuilding* building) && building != null &&
                building->r_AliveState == AliveState.IsAlive;
        }

        private void Snapshot(string stage, int tick, int generation)
        {
            IReadOnlyList<AiPathTileSample> tiles =
                CaptureRectangle(OrchardX, OrchardY, OrchardSize, OrchardSize);
            int bit4 = 0, apple = 0, tree = 0, zero = 0, building = 0, invalid = 0;
            foreach (AiPathTileSample t in tiles)
            {
                if (t.Status != "ok") { invalid++; continue; }
                if ((t.PropertyFlags & FarmBit) != 0) bit4++;
                if ((t.PropertyFlags & AppleBit) != 0) apple++;
                if ((t.PropertyFlags & TreeBit) != 0) tree++;
                if (t.NativeComponent == 0) zero++;
                if (t.BuildingId != 0) building++;
            }
            Span<AivCoarseCell> grid = GameAIVManagerAPI.Instance.GetCoarseGrid();
            int index = 69 * 160 + 97;
            string coarse = (uint)index < (uint)grid.Length
                ? "foreign=" + grid[index].ForeignPathComponentTileCount +
                  ",reservation=" + grid[index].StructureOrReservationCount +
                  ",treeWeight=" + grid[index].TreeObstructionWeight
                : "unavailable";
            var anchors = new List<string>();
            foreach (int x in new[] { 345, 350, 355 })
            {
                IReadOnlyList<AiPathTileSample> f = AiBuildDiagnostic.CaptureTiles(x, 485, 3, 3);
                int parcel = 0, pcl = -1;
                if (f.Count == 9) { pcl = f[0].NativeComponent;
                    foreach (AiPathTileSample t in f)
                        if ((t.PropertyFlags & FarmBit) != 0) parcel++; }
                anchors.Add("(" + x + ",485):pcl=" + pcl + ",parcel=" + parcel);
            }
            log("AI_BUILD_CANARI_SWAP_SNAPSHOT: farm=" + farmName + "; stage=" + stage +
                "; tick=" + tick + "; generation=" + generation +
                "; plotBit4=" + bit4 + "; apple=" + apple +
                "; tree=" + tree + "; pclZero=" + zero +
                "; buildingTiles=" + building + "; invalid=" + invalid +
                "; coarse(69,97)=" + coarse + "; anchors=" + string.Join("|", anchors));
        }

        private void Fail(string reason)
        { log("AI_BUILD_CANARI_SWAP: status=inconclusive; farm=" + farmName +
              "; reason=" + reason + "; state=" + state); state = -1; }

        // APIShared bounds each native tile-copy call to 8 by 8. Assemble larger
        // farm rectangles from four bounded calls without widening that contract.
        private static IReadOnlyList<AiPathTileSample> CaptureRectangle(int x, int y,
            int width, int height)
        {
            var result = new AiPathTileSample[width * height];
            for (int oy = 0; oy < height; oy += 8)
                for (int ox = 0; ox < width; ox += 8)
                {
                    int w = Math.Min(8, width - ox), h = Math.Min(8, height - oy);
                    IReadOnlyList<AiPathTileSample> part =
                        AiBuildDiagnostic.CaptureTiles(x + ox, y + oy, w, h);
                    if (part.Count != w * h) return Array.AsReadOnly(new AiPathTileSample[0]);
                    for (int dy = 0; dy < h; dy++)
                        for (int dx = 0; dx < w; dx++)
                            result[(oy + dy) * width + ox + dx] = part[dy * w + dx];
                }
            return Array.AsReadOnly(result);
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
