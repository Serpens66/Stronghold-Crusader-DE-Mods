using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace KeepFlagRotationTest
{
    internal sealed unsafe class KeepFlagRotationRuntime
    {
        private const int MissionScanDelayTicks = 3;
        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, KeepSpawn> pendingKeeps = new Dictionary<int, KeepSpawn>();
        private readonly Dictionary<int, KeepSpawn> capturedKeeps = new Dictionary<int, KeepSpawn>();
        private int remainingScanTicks = -1;
        private int sessionNumber;
        private int flagSpawnOrdinal;

        internal KeepFlagRotationRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Install()
        {
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildStructurePre));
            subscriptions.Add(ProjectileR3EventHooks.OnProjectileSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnProjectileSpawnPre));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Subscribe(OnStartMap));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnUnloadMapPre));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            log.LogInfo("KFR_TEST_INSTALL: alwaysActive=true; modeFilter=none; " +
                "spawnPath=BuildStructure(Pre)+ProjectileSpawn(Pre); loadedMapPath=delayed scan; " +
                $"scanDelayTicks={MissionScanDelayTicks}.");
        }

        private void OnBuildStructurePre(BuildStructureEventArgs args)
        {
            int keepKind = MapperKeepKind(args.Mappers);
            if (keepKind == 0)
                return;

            int expectedScale = KeepFlagRotationPolicy.ExpectedScale(keepKind);
            if (args.PlayerId < 1 || args.PlayerId > 8 ||
                args.BuildingScaleUnknown != expectedScale ||
                !KeepFlagRotationPolicy.TryNormalizeOrientation(args.Unknown1, out int normalizedOrientation) ||
                !KeepFlagRotationPolicy.TryGetPositions(
                    args.TileX, args.TileY, args.BuildingScaleUnknown, args.Unknown1,
                    out FlagPosition vanilla, out FlagPosition corrected))
            {
                pendingKeeps.Remove(args.PlayerId);
                capturedKeeps.Remove(args.PlayerId);
                log.LogWarning(
                    $"KFR_TEST_KEEP_REJECTED: session={sessionNumber}; player={args.PlayerId}; " +
                    $"mapper={args.Mappers}; origin=({args.TileX},{args.TileY}); " +
                    $"scale={args.BuildingScaleUnknown}; expectedScale={expectedScale}; " +
                    $"orientation={args.Unknown1}; isFree={args.IsFree}.");
                return;
            }

            pendingKeeps[args.PlayerId] = new KeepSpawn(
                args.PlayerId, args.Mappers, args.TileX, args.TileY,
                args.BuildingScaleUnknown, args.Unknown1, normalizedOrientation,
                "build-event", vanilla, corrected);
            capturedKeeps[args.PlayerId] = pendingKeeps[args.PlayerId];
            log.LogInfo(
                $"KFR_TEST_KEEP_CAPTURED: session={sessionNumber}; player={args.PlayerId}; " +
                $"mapper={args.Mappers}; origin=({args.TileX},{args.TileY}); " +
                $"scale={args.BuildingScaleUnknown}; orientationRaw={args.Unknown1}; " +
                $"orientationNormalized={normalizedOrientation}; orientationSource=build-event; " +
                $"direction={KeepFlagRotationPolicy.DescribeDirection(normalizedOrientation)}; " +
                $"targetCorner={KeepFlagRotationPolicy.DescribeCorner(normalizedOrientation)}; " +
                $"vanillaFlag={vanilla}; correctedFlag={corrected}; isFree={args.IsFree}.");
        }

        private void OnProjectileSpawnPre(ProjectileSpawnEventArgs args)
        {
            if (args.ProjectileType != ProjectileType.Flag3)
                return;

            flagSpawnOrdinal++;
            int playerId = args.UnitPlayerSourceId;
            bool mainFlagShape = KeepFlagRotationPolicy.IsStationaryMainFlag(
                args.SourceUnitId, args.PlayerSourceId, args.UnitPlayerSourceId,
                args.SourceWorldTileX, args.SourceWorldTileY, args.SourceElevation,
                args.TargetWorldTileX, args.TargetWorldTileY, args.TargetElevation,
                args.AttackedUnitId);

            if (!mainFlagShape || !pendingKeeps.TryGetValue(playerId, out KeepSpawn keep))
            {
                log.LogInfo(
                    $"KFR_TEST_FLAG3_IGNORED: session={sessionNumber}; ordinal={flagSpawnOrdinal}; " +
                    $"sourceUnit={args.SourceUnitId}; playerSource={args.PlayerSourceId}; " +
                    $"unitPlayerSource={args.UnitPlayerSourceId}; source=({args.SourceWorldTileX},{args.SourceWorldTileY},{args.SourceElevation}); " +
                    $"target=({args.TargetWorldTileX},{args.TargetWorldTileY},{args.TargetElevation}); " +
                    $"attackedUnit={args.AttackedUnitId}; mainFlagShape={mainFlagShape}; pendingKeep={pendingKeeps.ContainsKey(playerId)}.");
                return;
            }

            FlagPosition oldSource = new FlagPosition(args.SourceWorldTileX, args.SourceWorldTileY);
            FlagPosition oldTarget = new FlagPosition(args.TargetWorldTileX, args.TargetWorldTileY);
            if (!KeepFlagRotationPolicy.MatchesVanillaPosition(
                oldSource.X, oldSource.Y, oldTarget.X, oldTarget.Y, keep.Vanilla))
            {
                log.LogWarning(
                    $"KFR_TEST_FLAG3_DIVERGED: session={sessionNumber}; ordinal={flagSpawnOrdinal}; " +
                    $"player={playerId}; oldSource={oldSource}; oldTarget={oldTarget}; " +
                    $"sourceElevation={args.SourceElevation}; targetElevation={args.TargetElevation}; " +
                    $"expectedVanilla={keep.Vanilla}; " +
                    $"expectedCorrected={keep.Corrected}; keep={keep}. No mutation performed.");
                return;
            }

            pendingKeeps.Remove(playerId);
            args.SourceWorldTileX = keep.Corrected.X;
            args.SourceWorldTileY = keep.Corrected.Y;
            args.TargetWorldTileX = keep.Corrected.X;
            args.TargetWorldTileY = keep.Corrected.Y;
            string outcome = oldSource.X == keep.Corrected.X && oldSource.Y == keep.Corrected.Y
                ? "already-correct"
                : "corrected-before-native-spawn";
            log.LogInfo(
                $"KFR_TEST_FLAG3_SPAWN_RESULT: session={sessionNumber}; ordinal={flagSpawnOrdinal}; " +
                $"player={playerId}; outcome={outcome}; oldSource={oldSource}; oldTarget={oldTarget}; " +
                $"newSource={keep.Corrected}; newTarget={keep.Corrected}; " +
                $"sourceElevationPreserved={args.SourceElevation}; targetElevationPreserved={args.TargetElevation}; " +
                $"orientationRaw={keep.RawOrientation}; orientationNormalized={keep.NormalizedOrientation}; " +
                $"orientationSource={keep.OrientationSource}; direction={keep.Direction}; " +
                $"targetCorner={keep.TargetCorner}; keep={keep}.");
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                sessionNumber++;
                pendingKeeps.Clear();
                capturedKeeps.Clear();
                remainingScanTicks = -1;
                flagSpawnOrdinal = 0;
                log.LogInfo(
                    $"KFR_TEST_MAP_START_PRE: session={sessionNumber}; campaignMapId={args.CampaignMapId}; " +
                    $"multiplayerSave={args.bMultiplayerSave}.");
                return;
            }

            remainingScanTicks = MissionScanDelayTicks;
            log.LogInfo(
                $"KFR_TEST_MAP_START: session={sessionNumber}; campaignMapId={args.CampaignMapId}; " +
                $"multiplayerSave={args.bMultiplayerSave}; pendingSpawnKeeps={pendingKeeps.Count}; " +
                $"capturedKeeps={capturedKeeps.Count}; " +
                $"delayedScanInTicks={MissionScanDelayTicks}.");
        }

        private void OnUnloadMapPre(MapUnloadEventArgs args)
        {
            log.LogInfo(
                $"KFR_TEST_MAP_UNLOAD: session={sessionNumber}; pendingSpawnKeeps={pendingKeeps.Count}; " +
                $"capturedKeeps={capturedKeeps.Count}; " +
                $"remainingScanTicks={remainingScanTicks}.");
            pendingKeeps.Clear();
            capturedKeeps.Clear();
            remainingScanTicks = -1;
            flagSpawnOrdinal = 0;
        }

        private void OnGameTick(int tick)
        {
            if (remainingScanTicks < 0)
                return;
            if (remainingScanTicks-- > 0)
                return;

            remainingScanTicks = -1;
            try
            {
                ScanAndCorrectLoadedFlags(tick);
            }
            catch (Exception ex)
            {
                log.LogError($"KFR_TEST_DELAYED_SCAN_FAILED: session={sessionNumber}; tick={tick}; error={ex}");
            }
        }

        private void ScanAndCorrectLoadedFlags(int tick)
        {
            int keeps = 0;
            int corrected = 0;
            int verified = 0;
            int rejected = 0;
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(playerId);
                if (keepId <= 0 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                    keep == null || keep->r_AliveState == AliveState.None)
                {
                    continue;
                }

                int keepKind = StructKeepKind(keep->r_BuildingType);
                int expectedScale = KeepFlagRotationPolicy.ExpectedScale(keepKind);
                int scale = checked((int)keep->r_OccupyTileGridSize);
                keeps++;
                if (keepKind == 0 || scale != expectedScale || keep->r_PlayerIdOwner != playerId)
                {
                    rejected++;
                    log.LogWarning(
                        $"KFR_TEST_LOADED_KEEP_REJECTED: session={sessionNumber}; player={playerId}; keepId={keepId}; " +
                        $"type={keep->r_BuildingType}; owner={keep->r_PlayerIdOwner}; " +
                        $"origin=({keep->r_TilePositionXBegin},{keep->r_TilePositionYBegin}); " +
                        $"scale={scale}; expectedScale={expectedScale}; spriteVariation={keep->r_SpriteVariationIndex}.");
                    continue;
                }

                int originX = keep->r_TilePositionXBegin;
                int originY = keep->r_TilePositionYBegin;
                int rawOrientation;
                int normalizedOrientation;
                string orientationSource;
                FlagPosition vanilla;
                FlagPosition desired;
                if (capturedKeeps.TryGetValue(playerId, out KeepSpawn captured))
                {
                    if (captured.OriginX != originX || captured.OriginY != originY ||
                        captured.Scale != scale ||
                        !KeepFlagRotationPolicy.TryGetPositions(
                            originX, originY, scale, captured.RawOrientation,
                            out vanilla, out desired))
                    {
                        rejected++;
                        log.LogWarning(
                            $"KFR_TEST_LOADED_ORIENTATION_REJECTED: session={sessionNumber}; player={playerId}; keepId={keepId}; " +
                            $"reason=capture-mismatch; origin=({originX},{originY}); scale={scale}; " +
                            $"captured={captured}; spriteVariation={keep->r_SpriteVariationIndex}.");
                        continue;
                    }

                    rawOrientation = captured.RawOrientation;
                    normalizedOrientation = captured.NormalizedOrientation;
                    orientationSource = captured.OrientationSource;
                }
                else
                {
                    rawOrientation = keep->r_SpriteVariationIndex;
                    if (rawOrientation != 15 ||
                        !KeepFlagRotationPolicy.TryNormalizeOrientation(rawOrientation, out normalizedOrientation) ||
                        !KeepFlagRotationPolicy.TryGetPositions(
                            originX, originY, scale, rawOrientation,
                            out vanilla, out desired))
                    {
                        rejected++;
                        log.LogWarning(
                            $"KFR_TEST_LOADED_ORIENTATION_REJECTED: session={sessionNumber}; player={playerId}; keepId={keepId}; " +
                            $"reason=no-build-capture-and-no-default-sentinel; origin=({originX},{originY}); scale={scale}; " +
                            $"spriteVariation={rawOrientation}; keepType={keep->r_BuildingType}. No rotation guessed.");
                        continue;
                    }

                    orientationSource = "loaded-sentinel-default";
                }

                LoadedFlagMatch match = FindLoadedMainFlag(playerId, vanilla, desired);
                if (match.Ambiguous || match.ProjectileId == 0 ||
                    !GameProjectileManagerAPI.Instance.TryGetProjectileById(
                        match.ProjectileId, out GameProjectile* flag) || flag == null)
                {
                    rejected++;
                    log.LogWarning(
                        $"KFR_TEST_LOADED_FLAG_REJECTED: session={sessionNumber}; player={playerId}; keepId={keepId}; " +
                        $"type={keep->r_BuildingType}; origin=({keep->r_TilePositionXBegin},{keep->r_TilePositionYBegin}); " +
                        $"scale={scale}; orientationRaw={rawOrientation}; orientationNormalized={normalizedOrientation}; " +
                        $"orientationSource={orientationSource}; " +
                        $"direction={KeepFlagRotationPolicy.DescribeDirection(normalizedOrientation)}; " +
                        $"targetCorner={KeepFlagRotationPolicy.DescribeCorner(normalizedOrientation)}; " +
                        $"vanilla={vanilla}; desired={desired}; " +
                        $"matches={match.MatchCount}; ambiguous={match.Ambiguous}.");
                    continue;
                }

                FlagPosition oldSource = new FlagPosition(flag->r_SourceWorldTileX, flag->r_SourceWorldTileY);
                FlagPosition oldTarget = new FlagPosition(flag->r_TargetWorldTileX, flag->r_TargetWorldTileY);
                FlagPosition oldCurrent = new FlagPosition(flag->r_CurrentTileX, flag->r_CurrentTileY);
                FlagPosition oldUnknown = new FlagPosition(flag->r_UnkWorldTileX, flag->r_UnkWorldTileY);
                FlagPosition vanillaTile = KeepFlagRotationPolicy.ToTilePosition(vanilla);
                FlagPosition desiredTile = KeepFlagRotationPolicy.ToTilePosition(desired);
                uint oldTileId = flag->r_CurrentTileId;
                if (oldSource.X == desired.X && oldSource.Y == desired.Y &&
                    oldTarget.X == desired.X && oldTarget.Y == desired.Y &&
                    oldCurrent.X == desiredTile.X && oldCurrent.Y == desiredTile.Y)
                {
                    verified++;
                    log.LogInfo(
                        $"KFR_TEST_LOADED_FLAG_RESULT: session={sessionNumber}; player={playerId}; projectileId={match.ProjectileId}; " +
                        $"globalId={flag->r_GlobalId}; outcome=already-correct; source={oldSource}; target={oldTarget}; current={oldCurrent}; " +
                        $"unknown={oldUnknown}; tileId={oldTileId}; keepType={keep->r_BuildingType}; scale={scale}; " +
                        $"orientationRaw={rawOrientation}; orientationNormalized={normalizedOrientation}; orientationSource={orientationSource}; " +
                        $"direction={KeepFlagRotationPolicy.DescribeDirection(normalizedOrientation)}; " +
                        $"targetCorner={KeepFlagRotationPolicy.DescribeCorner(normalizedOrientation)}; " +
                        $"sourceElevationPreserved={flag->r_SourceElevation}; targetElevationPreserved={flag->r_TargetElevation}.");
                    continue;
                }

                if (oldSource.X != vanilla.X || oldSource.Y != vanilla.Y ||
                    oldTarget.X != vanilla.X || oldTarget.Y != vanilla.Y ||
                    oldCurrent.X != vanillaTile.X || oldCurrent.Y != vanillaTile.Y)
                {
                    rejected++;
                    log.LogWarning(
                        $"KFR_TEST_LOADED_FLAG_STATE_DIVERGED: session={sessionNumber}; player={playerId}; projectileId={match.ProjectileId}; " +
                        $"source={oldSource}; target={oldTarget}; current={oldCurrent}; unknown={oldUnknown}; " +
                        $"expectedVanilla={vanilla}; desired={desired}; orientationRaw={rawOrientation}; " +
                        $"orientationNormalized={normalizedOrientation}; orientationSource={orientationSource}; " +
                        $"direction={KeepFlagRotationPolicy.DescribeDirection(normalizedOrientation)}; " +
                        $"targetCorner={KeepFlagRotationPolicy.DescribeCorner(normalizedOrientation)}. " +
                        "No mutation performed.");
                    continue;
                }

                flag->r_SourceWorldTileX = checked((ushort)desired.X);
                flag->r_SourceWorldTileY = checked((ushort)desired.Y);
                flag->r_TargetWorldTileX = checked((ushort)desired.X);
                flag->r_TargetWorldTileY = checked((ushort)desired.Y);
                flag->r_CurrentTileX = checked((ushort)desiredTile.X);
                flag->r_CurrentTileY = checked((ushort)desiredTile.Y);
                if (oldUnknown.X == vanilla.X && oldUnknown.Y == vanilla.Y)
                {
                    flag->r_UnkWorldTileX = checked((ushort)desired.X);
                    flag->r_UnkWorldTileY = checked((ushort)desired.Y);
                }
                flag->r_CurrentTileId = checked((uint)GameTileManagerAPI.Instance.GetTileId(desiredTile.X, desiredTile.Y));
                corrected++;
                log.LogInfo(
                    $"KFR_TEST_LOADED_FLAG_RESULT: session={sessionNumber}; player={playerId}; projectileId={match.ProjectileId}; " +
                    $"globalId={flag->r_GlobalId}; outcome=corrected-after-load; source={oldSource}->{desired}; " +
                    $"target={oldTarget}->{desired}; " +
                    $"current={oldCurrent}->{desiredTile}; unknown={oldUnknown}->({flag->r_UnkWorldTileX},{flag->r_UnkWorldTileY}); " +
                    $"tileId={oldTileId}->{flag->r_CurrentTileId}; keepType={keep->r_BuildingType}; scale={scale}; " +
                    $"orientationRaw={rawOrientation}; orientationNormalized={normalizedOrientation}; orientationSource={orientationSource}; " +
                    $"direction={KeepFlagRotationPolicy.DescribeDirection(normalizedOrientation)}; " +
                    $"targetCorner={KeepFlagRotationPolicy.DescribeCorner(normalizedOrientation)}; " +
                    $"sourceElevationPreserved={flag->r_SourceElevation}; targetElevationPreserved={flag->r_TargetElevation}.");
            }

            log.LogInfo(
                $"KFR_TEST_DELAYED_SCAN_SUMMARY: session={sessionNumber}; tick={tick}; keeps={keeps}; " +
                $"corrected={corrected}; verified={verified}; rejected={rejected}; modeFilter=none.");
        }

        private static LoadedFlagMatch FindLoadedMainFlag(
            int playerId,
            FlagPosition vanilla,
            FlagPosition desired)
        {
            Span<GameProjectile> projectiles = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            int resultId = 0;
            int matches = 0;
            for (int spanIndex = 1; spanIndex < projectiles.Length; spanIndex++)
            {
                ref GameProjectile projectile = ref projectiles[spanIndex];
                if ((projectile.r_AliveState != AliveState.NeedsInit &&
                     projectile.r_AliveState != AliveState.IsAlive) ||
                    projectile.r_ProjectileType != ProjectileType.Flag3 ||
                    projectile.r_SourceUnitId != 0 ||
                    projectile.r_TargetUnidId != 0 ||
                    projectile.r_PlayerSourceId != playerId ||
                    projectile.r_UnitPlayerSourceId != playerId ||
                    projectile.r_TargetWorldTileX != projectile.r_SourceWorldTileX ||
                    projectile.r_TargetWorldTileY != projectile.r_SourceWorldTileY ||
                    projectile.r_TargetElevation != projectile.r_SourceElevation)
                {
                    continue;
                }

                bool atVanilla = projectile.r_SourceWorldTileX == vanilla.X &&
                    projectile.r_SourceWorldTileY == vanilla.Y;
                bool atDesired = projectile.r_SourceWorldTileX == desired.X &&
                    projectile.r_SourceWorldTileY == desired.Y;
                if (!atVanilla && !atDesired)
                    continue;

                matches++;
                if (!KeepFlagRotationPolicy.TryGetProjectileIdFromSpanIndex(spanIndex, out resultId))
                    throw new InvalidOperationException("A live projectile occupied reserved slot zero.");
            }

            KeepFlagRotationPolicy.TryResolveUniqueProjectileId(
                matches, resultId, out int uniqueProjectileId);
            return new LoadedFlagMatch(uniqueProjectileId, matches);
        }

        private static int MapperKeepKind(eMappers mapper) =>
            mapper == eMappers.MAPPER_KEEP1 ? 1 :
            mapper == eMappers.MAPPER_KEEP2 ? 2 :
            mapper == eMappers.MAPPER_KEEP3 ? 3 : 0;

        private static int StructKeepKind(eStructs type) =>
            type == eStructs.STRUCT_KEEP_ONE ? 1 :
            type == eStructs.STRUCT_KEEP_TWO ? 2 :
            type == eStructs.STRUCT_KEEP_THREE ? 3 : 0;

        private readonly struct KeepSpawn
        {
            internal KeepSpawn(
                int playerId, eMappers mapper, int originX, int originY,
                int scale, int rawOrientation, int normalizedOrientation,
                string orientationSource, FlagPosition vanilla, FlagPosition corrected)
            {
                PlayerId = playerId;
                Mapper = mapper;
                OriginX = originX;
                OriginY = originY;
                Scale = scale;
                RawOrientation = rawOrientation;
                NormalizedOrientation = normalizedOrientation;
                OrientationSource = orientationSource;
                Direction = KeepFlagRotationPolicy.DescribeDirection(normalizedOrientation);
                TargetCorner = KeepFlagRotationPolicy.DescribeCorner(normalizedOrientation);
                Vanilla = vanilla;
                Corrected = corrected;
            }

            internal int PlayerId { get; }
            internal eMappers Mapper { get; }
            internal int OriginX { get; }
            internal int OriginY { get; }
            internal int Scale { get; }
            internal int RawOrientation { get; }
            internal int NormalizedOrientation { get; }
            internal string OrientationSource { get; }
            internal string Direction { get; }
            internal string TargetCorner { get; }
            internal FlagPosition Vanilla { get; }
            internal FlagPosition Corrected { get; }

            public override string ToString() =>
                $"P{PlayerId}/{Mapper}/origin=({OriginX},{OriginY})/scale={Scale}/" +
                $"orientationRaw={RawOrientation}/orientationNormalized={NormalizedOrientation}/" +
                $"orientationSource={OrientationSource}/direction={Direction}/targetCorner={TargetCorner}";
        }

        private readonly struct LoadedFlagMatch
        {
            internal LoadedFlagMatch(int projectileId, int matchCount)
            {
                ProjectileId = projectileId;
                MatchCount = matchCount;
            }

            internal int ProjectileId { get; }
            internal int MatchCount { get; }
            internal bool Ambiguous => MatchCount > 1;
        }
    }
}
