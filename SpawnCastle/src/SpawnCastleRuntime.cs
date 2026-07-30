using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;

namespace SpawnCastle
{
    internal sealed class SpawnCastleRuntime
    {
        private readonly ManualLogSource log;
        private readonly SpawnCastleSettingsViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private bool installed;
        private bool handledCurrentMap;

        public SpawnCastleRuntime(
            ManualLogSource log,
            SpawnCastleSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Install()
        {
            if (installed)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));

            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave));

            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));

            installed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "SpawnCastle map lifecycle subscriptions installed.");
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            // A saved game already contains earlier SpawnCastle output.
            handledCurrentMap = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Savegame load detected; SpawnCastle will not duplicate the castle.");
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            handledCurrentMap = false;
            Shared.DebugLogHelper.LogInfo(
                log,
                "OnUnloadMap(Post) received; the next new map may spawn a castle.");
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            Shared.DebugLogHelper.LogInfo(
                log,
                $"OnStartMap(Post) received: handledCurrentMap={handledCurrentMap}, " +
                $"selection='{settings.SelectedCastle}', " +
                $"networkedEnvironment={GameNetworkAPI.IsNetworkedEnvironment()}.");

            if (handledCurrentMap)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "OnStartMap(Post) ignored because this map was already handled or is a loaded savegame.");
                return;
            }

            handledCurrentMap = true;
            try
            {
                SpawnSelectedCastle();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Castle spawn failed: {ex}");
            }
        }

        private void SpawnSelectedCastle()
        {
            if (settings.IsDisabled)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "SpawnCastle is disabled for this map.");
                return;
            }

            bool networkedEnvironment = GameNetworkAPI.IsNetworkedEnvironment();
            if (networkedEnvironment)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "GameNetworkAPI reports a networked environment; continuing because regular skirmish starts can also report this state.");
            }

            if (!settings.TryResolveSelectedFile(out string filePath))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Selected AIVJSON file is unavailable: '{settings.SelectedCastle}'.");
                return;
            }

            long fileLength = new FileInfo(filePath).Length;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Loading selected AIVJSON: path={filePath}, bytes={fileLength}.");

            AivParseResult parsed = LoadBlueprint(filePath);
            LogDiagnostics(parsed);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AIVJSON parsed: valid={parsed.IsValid}, frames={parsed.Blueprint.Frames.Count}, " +
                $"hasKeepAnchor={parsed.Blueprint.KeepAnchor.HasValue}, " +
                $"errors={parsed.ErrorCount}, warnings={parsed.WarningCount}.");

            if (!parsed.IsValid || !parsed.Blueprint.KeepAnchor.HasValue)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"AIVJSON rejected: path={filePath}, errors={parsed.ErrorCount}, warnings={parsed.WarningCount}.");
                return;
            }

            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"No valid local human player was found; playerId={playerId}.");
                return;
            }

            int reportedKeepId =
                GamePlayerManagerAPI.Instance.GetPlayerKeepId(playerId);
            if (!TryGetActualKeep(playerId, reportedKeepId, out KeepPlacement keep))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"No real local-player keep was found in the building array; " +
                    $"playerId={playerId}, reportedKeepId={reportedKeepId}.");
                return;
            }

            UnmanagedVector2<int> reportedKeepPosition =
                GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(playerId);
            UnmanagedVector2<int> keepDoorPosition =
                GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(playerId);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Keep anchor resolved: playerId={playerId}, " +
                $"reportedKeepId={reportedKeepId}, actualKeepId={keep.BuildingId}, " +
                $"resourceTile=({reportedKeepPosition.X},{reportedKeepPosition.Y}), " +
                $"doorTile=({keepDoorPosition.X},{keepDoorPosition.Y}), " +
                $"buildingBegin=({keep.BeginX},{keep.BeginY}), " +
                $"buildingEnd=({keep.EndX},{keep.EndY}), " +
                $"gridSize={keep.GridSize}, type={keep.Type}, aliveState={keep.AliveState}.");
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Castle spawn planned: playerId={playerId}, " +
                $"keepAnchor=({keep.BeginX},{keep.BeginY}), " +
                $"anchorSource=actual-building-begin, file={filePath}, " +
                $"frames={parsed.Blueprint.Frames.Count}.");

            SpawnStatistics statistics = SpawnBlueprint(
                parsed.Blueprint,
                playerId,
                keep.BeginX,
                keep.BeginY,
                keep.GridSize);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Castle spawn executed: playerId={playerId}, attempted={statistics.Attempted}, " +
                $"succeeded={statistics.Succeeded}, failed={statistics.Failed}, " +
                $"skipped={statistics.Skipped}, outOfBounds={statistics.OutOfBounds}, " +
                $"buildings={statistics.Buildings}, wallSegments={statistics.WallSegments}, " +
                $"wallTilesRequested={statistics.WallTilesRequested}, " +
                $"wallTilesVerified={statistics.WallTilesVerified}, " +
                $"wallTilesDeferred={statistics.WallTilesDeferred}, " +
                $"pitchTiles={statistics.PitchTiles}, " +
                $"preflightRejected={statistics.PreflightRejected}.");
        }

        private bool TryGetActualKeep(
            int playerId,
            int reportedKeepId,
            out KeepPlacement keep)
        {
            keep = default;
            int fallbackBuildingId = -1;
            Span<GameBuilding> buildings =
                GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();

            for (int buildingId = 0; buildingId < buildings.Length; buildingId++)
            {
                GameBuilding building = buildings[buildingId];
                if (building.r_PlayerIdOwner != playerId ||
                    !IsKeepType(building.r_BuildingType) ||
                    (building.r_AliveState != AliveState.NeedsInit &&
                     building.r_AliveState != AliveState.IsAlive))
                {
                    continue;
                }

                if (buildingId == reportedKeepId)
                {
                    keep = KeepPlacement.FromBuilding(buildingId, building);
                    return true;
                }

                if (fallbackBuildingId < 0)
                    fallbackBuildingId = buildingId;
            }

            if (fallbackBuildingId < 0)
                return false;

            // StartConditions also trusts the real building array over the player resource.
            keep = KeepPlacement.FromBuilding(
                fallbackBuildingId,
                buildings[fallbackBuildingId]);
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Reported keep id did not identify the real player keep; " +
                $"reportedKeepId={reportedKeepId}, usingBuildingId={fallbackBuildingId}.");
            return true;
        }

        private AivParseResult LoadBlueprint(string filePath)
        {
            string json = File.ReadAllText(filePath);
            AivJsonDocument document = AivJsonReader.Parse(json);
            return new AivBlueprintParser().Parse(document, filePath);
        }

        private void LogDiagnostics(AivParseResult parsed)
        {
            foreach (AivDiagnostic diagnostic in parsed.Diagnostics)
            {
                string message =
                    $"AIV diagnostic {diagnostic.Code} at {diagnostic.Location}: {diagnostic.Message}";
                if (diagnostic.Severity == AivDiagnosticSeverity.Error)
                    Shared.DebugLogHelper.LogError(log, message);
                else
                    Shared.DebugLogHelper.LogWarning(log, message);
            }
        }

        private SpawnStatistics SpawnBlueprint(
            AivBlueprint blueprint,
            int playerId,
            int keepTileX,
            int keepTileY,
            int keepGridSize)
        {
            var statistics = new SpawnStatistics();
            AivGridPoint keepAnchor = blueprint.KeepAnchor.Value;
            if (!ValidateBuildingFootprints(
                    blueprint,
                    keepAnchor,
                    keepTileX,
                    keepTileY,
                    keepGridSize))
            {
                statistics.PreflightRejected = true;
                return statistics;
            }

            foreach (AivBuildFrame frame in blueprint.Frames)
            {
                if (!frame.Mapper.IsKnown)
                {
                    statistics.Skipped += frame.Positions.Count;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Skipping unknown frame={frame.BuildIndex}, mapper={frame.Mapper.Name}, " +
                        $"positions={frame.Positions.Count}.");
                    continue;
                }

                // The map already owns and initializes the human keep.
                if (frame.Mapper.Category == AivItemCategory.Keep)
                {
                    statistics.Skipped += frame.Positions.Count;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Skipping AIV keep frame={frame.BuildIndex}, mapper={frame.Mapper.Name}, " +
                        "because the real human keep is used as the world anchor.");
                    continue;
                }

                if (IsWallCategory(frame.Mapper.Category))
                {
                    // Wall cost bypassing will be solved separately without changing costs or goods.
                    statistics.Skipped += frame.Positions.Count;
                    statistics.WallTilesDeferred += frame.Positions.Count;
                    continue;
                }

                foreach (AivGridPoint point in frame.Positions)
                {
                    WorldTile tile = AivWorldPlacement.ToWorld(
                        point,
                        keepAnchor,
                        keepTileX,
                        keepTileY);
                    if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(tile.X, tile.Y))
                    {
                        statistics.OutOfBounds++;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Skipping out-of-bounds spawn: frame={frame.BuildIndex}, " +
                            $"mapper={frame.Mapper.Name}, tile=({tile.X},{tile.Y}).");
                        continue;
                    }

                    SpawnFramePosition(
                        frame,
                        playerId,
                        tile.X,
                        tile.Y,
                        statistics);
                }
            }

            if (statistics.WallTilesDeferred > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Wall spawning deferred for the building-position test: " +
                    $"sourceTiles={statistics.WallTilesDeferred}. " +
                    "No wall cost multiplier or player stone was changed.");
            }

            return statistics;
        }

        private bool ValidateBuildingFootprints(
            AivBlueprint blueprint,
            AivGridPoint keepAnchor,
            int keepTileX,
            int keepTileY,
            int keepGridSize)
        {
            var footprints = new List<WorldFootprint>();
            foreach (AivBuildFrame frame in blueprint.Frames)
            {
                if (!frame.Mapper.IsKnown ||
                    (frame.Mapper.Category != AivItemCategory.Keep &&
                     frame.Mapper.Category != AivItemCategory.Building))
                {
                    continue;
                }

                int scale = frame.Mapper.Category == AivItemCategory.Keep
                    ? keepGridSize
                    : GetPlacementScale(frame.Mapper);
                foreach (AivGridPoint point in frame.Positions)
                {
                    WorldTile tile = frame.Mapper.Category == AivItemCategory.Keep
                        ? new WorldTile(keepTileX, keepTileY)
                        : AivWorldPlacement.ToWorld(
                            point,
                            keepAnchor,
                            keepTileX,
                            keepTileY);
                    var footprint = new WorldFootprint(
                        frame.BuildIndex,
                        frame.Mapper.Name,
                        tile.X,
                        tile.Y,
                        scale);

                    if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(
                            footprint.MinX,
                            footprint.MinY) ||
                        !GameTileManagerAPI.Instance.IsTileInsideMapBounds(
                            footprint.MaxX,
                            footprint.MaxY))
                    {
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"World footprint is outside the map: frame={frame.BuildIndex}, " +
                            $"mapper={frame.Mapper.Name}, bounds=({footprint.MinX},{footprint.MinY})-" +
                            $"({footprint.MaxX},{footprint.MaxY}).");
                        return false;
                    }

                    footprints.Add(footprint);
                }
            }

            int overlapCount = 0;
            for (int leftIndex = 0; leftIndex < footprints.Count; leftIndex++)
            {
                WorldFootprint left = footprints[leftIndex];
                for (int rightIndex = leftIndex + 1;
                     rightIndex < footprints.Count;
                     rightIndex++)
                {
                    WorldFootprint right = footprints[rightIndex];
                    if (!left.Overlaps(right))
                        continue;

                    overlapCount++;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"World footprint overlap: frame={left.FrameIndex} {left.MapperName} " +
                        $"bounds=({left.MinX},{left.MinY})-({left.MaxX},{left.MaxY}) with " +
                        $"frame={right.FrameIndex} {right.MapperName} " +
                        $"bounds=({right.MinX},{right.MinY})-({right.MaxX},{right.MaxY}).");
                }
            }

            if (overlapCount != 0)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"World footprint preflight rejected the castle: " +
                    $"buildings={footprints.Count}, overlaps={overlapCount}, " +
                    "anchorSource=actual-building-begin, rowMapping=inverted.");
                return false;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"World footprint preflight passed: buildings={footprints.Count}, " +
                "overlaps=0, outOfBounds=0, " +
                "anchorSource=actual-building-begin, rowMapping=inverted.");
            return true;
        }

        private void SpawnWallMapper(
            AivBlueprint blueprint,
            AivBuildFrame firstFrame,
            int playerId,
            AivGridPoint keepAnchor,
            int keepTileX,
            int keepTileY,
            SpawnStatistics statistics)
        {
            var worldTiles = new List<WorldTile>();
            int sourceFrameCount = 0;
            foreach (AivBuildFrame frame in blueprint.Frames)
            {
                if (frame.Mapper.Value != firstFrame.Mapper.Value)
                    continue;

                sourceFrameCount++;
                foreach (AivGridPoint point in frame.Positions)
                {
                    WorldTile tile = AivWorldPlacement.ToWorld(
                        point,
                        keepAnchor,
                        keepTileX,
                        keepTileY);
                    if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(tile.X, tile.Y))
                    {
                        statistics.OutOfBounds++;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Skipping out-of-bounds wall tile: frame={frame.BuildIndex}, " +
                            $"mapper={frame.Mapper.Name}, tile=({tile.X},{tile.Y}).");
                        continue;
                    }

                    worldTiles.Add(tile);
                }
            }

            IReadOnlyList<WallSegment> segments =
                AivWorldPlacement.CreateWallSegments(
                    worldTiles,
                    out IReadOnlyList<WorldTile> isolatedTiles,
                    out int duplicateTileCount);

            statistics.Skipped += isolatedTiles.Count;
            foreach (WorldTile tile in isolatedTiles)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Skipping isolated wall tile because CreateWall requires a line: " +
                    $"mapper={firstFrame.Mapper.Name}, tile=({tile.X},{tile.Y}).");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Wall segmentation prepared: mapper={firstFrame.Mapper.Name}, " +
                $"sourceFrames={sourceFrameCount}, sourceTiles={worldTiles.Count}, " +
                $"segments={segments.Count}, isolatedTiles={isolatedTiles.Count}, " +
                $"duplicateTiles={duplicateTileCount}.");

            foreach (WallSegment segment in segments)
            {
                SpawnWallSegment(
                    firstFrame,
                    playerId,
                    segment,
                    statistics);
            }
        }

        private void SpawnWallSegment(
            AivBuildFrame frame,
            int playerId,
            WallSegment segment,
            SpawnStatistics statistics)
        {
            statistics.Attempted++;
            statistics.WallSegments++;
            RecordSegmentTiles(segment, statistics.RequestedWallTiles);
            eMappers mapper = (eMappers)frame.Mapper.Value;

            try
            {
                GameBuildingManagerAPI.Instance.CreateWall(
                    playerId,
                    segment.Start.X,
                    segment.Start.Y,
                    segment.End.X,
                    segment.End.Y,
                    mapper,
                    segment.TileCount);

                int verifiedTiles = CountVerifiedWallTiles(
                    frame.Mapper.Category,
                    segment,
                    statistics.VerifiedWallTiles);
                if (verifiedTiles == segment.TileCount)
                    statistics.Succeeded++;
                else
                    statistics.Failed++;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Wall segment mapper={frame.Mapper.Name}, " +
                    $"start=({segment.Start.X},{segment.Start.Y}), " +
                    $"end=({segment.End.X},{segment.End.Y}), " +
                    $"requestedTiles={segment.TileCount}, " +
                    $"verifiedTiles={verifiedTiles}/{segment.TileCount}.");
            }
            catch (Exception ex)
            {
                statistics.Failed++;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Wall segment failed: mapper={frame.Mapper.Name}, " +
                    $"start=({segment.Start.X},{segment.Start.Y}), " +
                    $"end=({segment.End.X},{segment.End.Y}): {ex}");
            }
        }

        private static int CountVerifiedWallTiles(
            AivItemCategory category,
            WallSegment segment,
            ISet<WorldTile> verifiedWallTiles)
        {
            int verified = 0;
            int stepX = Math.Sign(segment.End.X - segment.Start.X);
            int stepY = Math.Sign(segment.End.Y - segment.Start.Y);
            for (int offset = 0; offset < segment.TileCount; offset++)
            {
                int tileX = segment.Start.X + (stepX * offset);
                int tileY = segment.Start.Y + (stepY * offset);
                int tileId = GameTileManagerAPI.Instance.GetTileId(tileX, tileY);
                TilePropertyFlag flags =
                    GameTileManagerAPI.Instance.GetTilePropertyFlag(tileId);
                bool matches;
                switch (category)
                {
                    case AivItemCategory.LowWallPath:
                        matches = (flags & TilePropertyFlag.IsWall) != 0 &&
                                  (flags & TilePropertyFlag.IsLowWall) != 0;
                        break;

                    case AivItemCategory.CrenelPath:
                        matches = (flags & TilePropertyFlag.IsWall) != 0 &&
                                  (flags & (TilePropertyFlag.CrenelationComponent |
                                            TilePropertyFlag.CrenelationModifier)) != 0;
                        break;

                    default:
                        matches = (flags & TilePropertyFlag.IsWall) != 0;
                        break;
                }

                if (matches)
                {
                    verified++;
                    verifiedWallTiles.Add(new WorldTile(tileX, tileY));
                }
            }

            return verified;
        }

        private static void RecordSegmentTiles(
            WallSegment segment,
            ISet<WorldTile> target)
        {
            int stepX = Math.Sign(segment.End.X - segment.Start.X);
            int stepY = Math.Sign(segment.End.Y - segment.Start.Y);
            for (int offset = 0; offset < segment.TileCount; offset++)
            {
                target.Add(
                    new WorldTile(
                        segment.Start.X + (stepX * offset),
                        segment.Start.Y + (stepY * offset)));
            }
        }

        private void SpawnFramePosition(
            AivBuildFrame frame,
            int playerId,
            int tileX,
            int tileY,
            SpawnStatistics statistics)
        {
            statistics.Attempted++;
            eMappers mapper = (eMappers)frame.Mapper.Value;
            string resultText = "<not-called>";
            try
            {
                switch (frame.Mapper.Category)
                {
                    case AivItemCategory.PitchDitchPath:
                        int pitchId = GamePitchManagerAPI.Instance.CreatePitch(
                            tileX,
                            tileY,
                            playerId);
                        statistics.PitchTiles++;
                        RecordResult(pitchId, statistics);
                        resultText = pitchId.ToString();
                        break;

                    case AivItemCategory.Building:
                    case AivItemCategory.Stair:
                    case AivItemCategory.MoatPath:
                    case AivItemCategory.Trap:
                        int scale = GetPlacementScale(frame.Mapper);

                        long result = GameBuildingManagerAPI.Instance.CreatePrefab(
                            playerId,
                            tileX,
                            tileY,
                            mapper,
                            scale,
                            0,
                            true,
                            true);
                        statistics.Buildings++;
                        RecordResult(result, statistics);
                        resultText = result.ToString();
                        break;

                    default:
                        statistics.Attempted--;
                        statistics.Skipped++;
                        resultText = "skipped-category";
                        break;
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Spawn frame={frame.BuildIndex}, mapper={frame.Mapper.Name}, " +
                    $"tile=({tileX},{tileY}), category={frame.Mapper.Category}, " +
                    $"result={resultText}.");
            }
            catch (Exception ex)
            {
                statistics.Failed++;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Spawn failed for frame={frame.BuildIndex}, mapper={frame.Mapper.Name}, " +
                    $"tile=({tileX},{tileY}): {ex}");
            }
        }

        private static void RecordResult(long result, SpawnStatistics statistics)
        {
            if (result != 0)
                statistics.Succeeded++;
            else
                statistics.Failed++;
        }

        private static int GetPlacementScale(AivMapperInfo mapper)
        {
            int scale = mapper.FootprintSize ??
                        BuildingScales.GetScale((eMappers)mapper.Value);
            return scale > 0 ? scale : 1;
        }

        private static bool IsWallCategory(AivItemCategory category)
        {
            return category == AivItemCategory.HighWallPath ||
                   category == AivItemCategory.LowWallPath ||
                   category == AivItemCategory.CrenelPath;
        }

        private static bool IsKeepType(eStructs buildingType)
        {
            return buildingType == eStructs.STRUCT_KEEP_ONE ||
                   buildingType == eStructs.STRUCT_KEEP_TWO ||
                   buildingType == eStructs.STRUCT_KEEP_THREE ||
                   buildingType == eStructs.STRUCT_KEEP_FOUR ||
                   buildingType == eStructs.STRUCT_KEEP_FIVE;
        }

        private readonly struct KeepPlacement
        {
            public KeepPlacement(
                int buildingId,
                int beginX,
                int beginY,
                int endX,
                int endY,
                int gridSize,
                eStructs type,
                AliveState aliveState)
            {
                BuildingId = buildingId;
                BeginX = beginX;
                BeginY = beginY;
                EndX = endX;
                EndY = endY;
                GridSize = gridSize;
                Type = type;
                AliveState = aliveState;
            }

            public int BuildingId { get; }
            public int BeginX { get; }
            public int BeginY { get; }
            public int EndX { get; }
            public int EndY { get; }
            public int GridSize { get; }
            public eStructs Type { get; }
            public AliveState AliveState { get; }

            public static KeepPlacement FromBuilding(
                int buildingId,
                GameBuilding building)
            {
                int gridSize = (int)building.r_OccupyTileGridSize;
                if (gridSize <= 0)
                {
                    gridSize = Math.Max(
                        Math.Abs(
                            building.r_TilePositionXEnd -
                            building.r_TilePositionXBegin) + 1,
                        Math.Abs(
                            building.r_TilePositionYEnd -
                            building.r_TilePositionYBegin) + 1);
                }

                return new KeepPlacement(
                    buildingId,
                    building.r_TilePositionXBegin,
                    building.r_TilePositionYBegin,
                    building.r_TilePositionXEnd,
                    building.r_TilePositionYEnd,
                    gridSize,
                    building.r_BuildingType,
                    building.r_AliveState);
            }
        }

        private sealed class WorldFootprint
        {
            public WorldFootprint(
                int frameIndex,
                string mapperName,
                int minX,
                int minY,
                int scale)
            {
                FrameIndex = frameIndex;
                MapperName = mapperName;
                MinX = minX;
                MinY = minY;
                MaxX = minX + scale - 1;
                MaxY = minY + scale - 1;
            }

            public int FrameIndex { get; }
            public string MapperName { get; }
            public int MinX { get; }
            public int MinY { get; }
            public int MaxX { get; }
            public int MaxY { get; }

            public bool Overlaps(WorldFootprint other)
            {
                return MinX <= other.MaxX &&
                       other.MinX <= MaxX &&
                       MinY <= other.MaxY &&
                       other.MinY <= MaxY;
            }
        }

        private sealed class SpawnStatistics
        {
            public int Attempted;
            public int Succeeded;
            public int Failed;
            public int Skipped;
            public int OutOfBounds;
            public int Buildings;
            public int WallSegments;
            public int WallTilesDeferred;
            public readonly HashSet<WorldTile> RequestedWallTiles =
                new HashSet<WorldTile>();
            public readonly HashSet<WorldTile> VerifiedWallTiles =
                new HashSet<WorldTile>();
            public int WallTilesRequested => RequestedWallTiles.Count;
            public int WallTilesVerified => VerifiedWallTiles.Count;
            public int PitchTiles;
            public bool PreflightRejected;
        }
    }
}
