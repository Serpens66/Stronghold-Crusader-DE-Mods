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
                !KeepFlagRotationPolicy.TryGetPositions(
                    args.TileX, args.TileY, args.BuildingScaleUnknown, args.Unknown1,
                    out FlagPosition vanilla, out FlagPosition corrected))
            {
                pendingKeeps.Remove(args.PlayerId);
                log.LogWarning(
                    $"KFR_TEST_KEEP_REJECTED: session={sessionNumber}; player={args.PlayerId}; " +
                    $"mapper={args.Mappers}; origin=({args.TileX},{args.TileY}); " +
                    $"scale={args.BuildingScaleUnknown}; expectedScale={expectedScale}; " +
                    $"orientation={args.Unknown1}; isFree={args.IsFree}.");
                return;
            }

            pendingKeeps[args.PlayerId] = new KeepSpawn(
                args.PlayerId, args.Mappers, args.TileX, args.TileY,
                args.BuildingScaleUnknown, args.Unknown1, vanilla, corrected);
            log.LogInfo(
                $"KFR_TEST_KEEP_CAPTURED: session={sessionNumber}; player={args.PlayerId}; " +
                $"mapper={args.Mappers}; origin=({args.TileX},{args.TileY}); " +
                $"scale={args.BuildingScaleUnknown}; orientation={args.Unknown1}; " +
                $"vanillaFlag={vanilla}; correctedFlag={corrected}; isFree={args.IsFree}.");
        }

        private void OnProjectileSpawnPre(ProjectileSpawnEventArgs args)
        {
            if (args.ProjectileType != ProjectileType.Flag3)
                return;

            flagSpawnOrdinal++;
            int playerId = args.UnitPlayerSourceId;
            bool mainFlagShape =
                args.SourceUnitId == 0 &&
                playerId >= 1 && playerId <= 8 &&
                args.PlayerSourceId == playerId &&
                args.AttackedUnitId == 0 &&
                args.TargetWorldTileX == 0 &&
                args.TargetWorldTileY == 0 &&
                args.TargetElevation == 0;

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

            FlagPosition actual = new FlagPosition(args.SourceWorldTileX, args.SourceWorldTileY);
            if (actual.X != keep.Vanilla.X || actual.Y != keep.Vanilla.Y)
            {
                log.LogWarning(
                    $"KFR_TEST_FLAG3_DIVERGED: session={sessionNumber}; ordinal={flagSpawnOrdinal}; " +
                    $"player={playerId}; actual={actual}; expectedVanilla={keep.Vanilla}; " +
                    $"expectedCorrected={keep.Corrected}; keep={keep}. No mutation performed.");
                return;
            }

            pendingKeeps.Remove(playerId);
            args.SourceWorldTileX = keep.Corrected.X;
            args.SourceWorldTileY = keep.Corrected.Y;
            string outcome = actual.X == keep.Corrected.X && actual.Y == keep.Corrected.Y
                ? "already-correct"
                : "corrected-before-native-spawn";
            log.LogInfo(
                $"KFR_TEST_FLAG3_SPAWN_RESULT: session={sessionNumber}; ordinal={flagSpawnOrdinal}; " +
                $"player={playerId}; outcome={outcome}; old={actual}; new={keep.Corrected}; " +
                $"elevationPreserved={args.SourceElevation}; keep={keep}.");
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                sessionNumber++;
                pendingKeeps.Clear();
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
                $"delayedScanInTicks={MissionScanDelayTicks}.");
        }

        private void OnUnloadMapPre(MapUnloadEventArgs args)
        {
            log.LogInfo(
                $"KFR_TEST_MAP_UNLOAD: session={sessionNumber}; pendingSpawnKeeps={pendingKeeps.Count}; " +
                $"remainingScanTicks={remainingScanTicks}.");
            pendingKeeps.Clear();
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
                int orientation = keep->r_SpriteVariationIndex;
                keeps++;
                if (keepKind == 0 || scale != expectedScale || keep->r_PlayerIdOwner != playerId ||
                    !KeepFlagRotationPolicy.TryGetPositions(
                        keep->r_TilePositionXBegin, keep->r_TilePositionYBegin, scale, orientation,
                        out FlagPosition vanilla, out FlagPosition desired))
                {
                    rejected++;
                    log.LogWarning(
                        $"KFR_TEST_LOADED_KEEP_REJECTED: session={sessionNumber}; player={playerId}; keepId={keepId}; " +
                        $"type={keep->r_BuildingType}; owner={keep->r_PlayerIdOwner}; " +
                        $"origin=({keep->r_TilePositionXBegin},{keep->r_TilePositionYBegin}); " +
                        $"scale={scale}; expectedScale={expectedScale}; orientation={orientation}.");
                    continue;
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
                        $"scale={scale}; orientation={orientation}; vanilla={vanilla}; desired={desired}; " +
                        $"matches={match.MatchCount}; ambiguous={match.Ambiguous}.");
                    continue;
                }

                FlagPosition oldSource = new FlagPosition(flag->r_SourceWorldTileX, flag->r_SourceWorldTileY);
                FlagPosition oldCurrent = new FlagPosition(flag->r_CurrentTileX, flag->r_CurrentTileY);
                FlagPosition oldUnknown = new FlagPosition(flag->r_UnkWorldTileX, flag->r_UnkWorldTileY);
                uint oldTileId = flag->r_CurrentTileId;
                if (oldSource.X == desired.X && oldSource.Y == desired.Y &&
                    oldCurrent.X == desired.X && oldCurrent.Y == desired.Y)
                {
                    verified++;
                    log.LogInfo(
                        $"KFR_TEST_LOADED_FLAG_RESULT: session={sessionNumber}; player={playerId}; projectileId={match.ProjectileId}; " +
                        $"globalId={flag->r_GlobalId}; outcome=already-correct; source={oldSource}; current={oldCurrent}; " +
                        $"unknown={oldUnknown}; tileId={oldTileId}; keepType={keep->r_BuildingType}; scale={scale}; orientation={orientation}.");
                    continue;
                }

                if (oldSource.X != vanilla.X || oldSource.Y != vanilla.Y ||
                    oldCurrent.X != vanilla.X || oldCurrent.Y != vanilla.Y)
                {
                    rejected++;
                    log.LogWarning(
                        $"KFR_TEST_LOADED_FLAG_STATE_DIVERGED: session={sessionNumber}; player={playerId}; projectileId={match.ProjectileId}; " +
                        $"source={oldSource}; current={oldCurrent}; unknown={oldUnknown}; expectedVanilla={vanilla}; desired={desired}. " +
                        "No mutation performed.");
                    continue;
                }

                flag->r_SourceWorldTileX = checked((ushort)desired.X);
                flag->r_SourceWorldTileY = checked((ushort)desired.Y);
                flag->r_CurrentTileX = checked((ushort)desired.X);
                flag->r_CurrentTileY = checked((ushort)desired.Y);
                if (oldUnknown.X == vanilla.X && oldUnknown.Y == vanilla.Y)
                {
                    flag->r_UnkWorldTileX = checked((ushort)desired.X);
                    flag->r_UnkWorldTileY = checked((ushort)desired.Y);
                }
                flag->r_CurrentTileId = checked((uint)GameTileManagerAPI.Instance.GetTileId(desired.X / 8, desired.Y / 8));
                corrected++;
                log.LogInfo(
                    $"KFR_TEST_LOADED_FLAG_RESULT: session={sessionNumber}; player={playerId}; projectileId={match.ProjectileId}; " +
                    $"globalId={flag->r_GlobalId}; outcome=corrected-after-load; source={oldSource}->{desired}; " +
                    $"current={oldCurrent}->{desired}; unknown={oldUnknown}->({flag->r_UnkWorldTileX},{flag->r_UnkWorldTileY}); " +
                    $"tileId={oldTileId}->{flag->r_CurrentTileId}; keepType={keep->r_BuildingType}; scale={scale}; orientation={orientation}.");
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
            for (int projectileId = 1; projectileId < projectiles.Length; projectileId++)
            {
                ref GameProjectile projectile = ref projectiles[projectileId];
                if ((projectile.r_AliveState != AliveState.NeedsInit &&
                     projectile.r_AliveState != AliveState.IsAlive) ||
                    projectile.r_ProjectileType != ProjectileType.Flag3 ||
                    projectile.r_SourceUnitId != 0 ||
                    projectile.r_TargetUnidId != 0 ||
                    projectile.r_PlayerSourceId != playerId ||
                    projectile.r_UnitPlayerSourceId != playerId ||
                    projectile.r_TargetWorldTileX != 0 ||
                    projectile.r_TargetWorldTileY != 0 ||
                    projectile.r_TargetElevation != 0)
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
                resultId = projectileId;
            }

            return new LoadedFlagMatch(matches == 1 ? resultId : 0, matches);
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
                int scale, int orientation, FlagPosition vanilla, FlagPosition corrected)
            {
                PlayerId = playerId;
                Mapper = mapper;
                OriginX = originX;
                OriginY = originY;
                Scale = scale;
                Orientation = orientation;
                Vanilla = vanilla;
                Corrected = corrected;
            }

            internal int PlayerId { get; }
            internal eMappers Mapper { get; }
            internal int OriginX { get; }
            internal int OriginY { get; }
            internal int Scale { get; }
            internal int Orientation { get; }
            internal FlagPosition Vanilla { get; }
            internal FlagPosition Corrected { get; }

            public override string ToString() =>
                $"P{PlayerId}/{Mapper}/origin=({OriginX},{OriginY})/scale={Scale}/orientation={Orientation}";
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
