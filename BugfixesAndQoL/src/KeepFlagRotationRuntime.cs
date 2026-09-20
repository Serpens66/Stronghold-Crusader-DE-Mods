using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed unsafe class KeepFlagRotationRuntime
    {
        private const int NewMapScanDelayTicks = 3;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<int, KeepSpawn> pendingKeeps = new Dictionary<int, KeepSpawn>();
        private readonly Dictionary<int, KeepSpawn> capturedKeeps = new Dictionary<int, KeepSpawn>();
        private List<IDisposable> subscriptions;
        private int remainingScanTicks = -1;
        private int sessionNumber;
        private int capturedCount;
        private int correctedBeforeSpawnCount;
        private int alreadyCorrectBeforeSpawnCount;
        private int rejectedCount;
        private bool warningLogged;
        private bool newMapActive;

        internal KeepFlagRotationRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void Install()
        {
            if (subscriptions != null)
                return;

            var candidates = new List<IDisposable>();
            bool tickSubscribed = false;
            try
            {
                candidates.Add(BuildingR3EventHooks.OnBuildStructure.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnBuildStructurePre));
                candidates.Add(ProjectileR3EventHooks.OnProjectileSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnProjectileSpawnPre));
                // SaveLifecycle: NewMapOnly - saved and editor sessions already contain placed flags.
                candidates.Add(Shared.MissionEvents.Loading
                    .Where(args => args.Phase == MissionInitializationPhase.BeforeLoad)
                    .Subscribe(OnBeforeMapLoad));
                candidates.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log,
                    OnSessionStarted));
                candidates.Add(Shared.MissionEvents.Ended
                    .Subscribe(_ => ResetMapState()));
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;
                subscriptions = candidates;
            }
            catch
            {
                if (tickSubscribed)
                    GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
                foreach (IDisposable candidate in candidates)
                    candidate.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Keep-flag rotation fix installed; scanDelayTicks={NewMapScanDelayTicks}, modeFilter=new-map-only.");
        }

        private bool Enabled => newMapActive && settings.EnableMod && settings.EnableKeepFlagRotationFix;

        private void OnBuildStructurePre(BuildStructureEventArgs args)
        {
            int keepKind = MapperKeepKind(args.Mappers);
            if (keepKind == 0)
                return;
            if (!Enabled)
            {
                pendingKeeps.Remove(args.PlayerId);
                capturedKeeps.Remove(args.PlayerId);
                return;
            }

            int expectedScale = KeepFlagRotationPolicy.ExpectedScale(keepKind);
            if (args.PlayerId < 1 || args.PlayerId > 8 ||
                args.BuildingScaleUnknown != expectedScale ||
                !KeepFlagRotationPolicy.TryNormalizeOrientation(args.Unknown1, out int normalizedOrientation) ||
                !KeepFlagRotationPolicy.TryGetPositions(
                    args.TileX,
                    args.TileY,
                    args.BuildingScaleUnknown,
                    args.Unknown1,
                    out KeepFlagPosition vanilla,
                    out KeepFlagPosition corrected))
            {
                pendingKeeps.Remove(args.PlayerId);
                capturedKeeps.Remove(args.PlayerId);
                rejectedCount++;
                WarnOnce(
                    $"Keep-flag rotation rejected a Keep capture: player={args.PlayerId}, mapper={args.Mappers}, " +
                    $"origin=({args.TileX},{args.TileY}), scale={args.BuildingScaleUnknown}, " +
                    $"expectedScale={expectedScale}, orientation={args.Unknown1}. No mutation was performed.");
                return;
            }

            var keep = new KeepSpawn(
                args.PlayerId,
                args.Mappers,
                args.TileX,
                args.TileY,
                args.BuildingScaleUnknown,
                args.Unknown1,
                normalizedOrientation,
                vanilla,
                corrected);
            pendingKeeps[args.PlayerId] = keep;
            capturedKeeps[args.PlayerId] = keep;
            capturedCount++;
        }

        private void OnProjectileSpawnPre(ProjectileSpawnEventArgs args)
        {
            if (!Enabled || args.ProjectileType != ProjectileType.Flag3)
                return;

            int playerId = args.UnitPlayerSourceId;
            if (!KeepFlagRotationPolicy.IsStationaryMainFlag(
                    args.SourceUnitId,
                    args.PlayerSourceId,
                    args.UnitPlayerSourceId,
                    args.SourceWorldTileX,
                    args.SourceWorldTileY,
                    args.SourceElevation,
                    args.TargetWorldTileX,
                    args.TargetWorldTileY,
                    args.TargetElevation,
                    args.AttackedUnitId) ||
                !pendingKeeps.TryGetValue(playerId, out KeepSpawn keep))
            {
                return;
            }

            var oldSource = new KeepFlagPosition(args.SourceWorldTileX, args.SourceWorldTileY);
            var oldTarget = new KeepFlagPosition(args.TargetWorldTileX, args.TargetWorldTileY);
            if (!KeepFlagRotationPolicy.TryGetCorrectedPosition(
                    keep.OriginX,
                    keep.OriginY,
                    keep.Scale,
                    keep.RawOrientation,
                    oldSource,
                    out KeepFlagPosition actualVanilla,
                    out KeepFlagPosition actualCorrected,
                    out _,
                    out _) ||
                oldTarget.X != actualVanilla.X || oldTarget.Y != actualVanilla.Y)
            {
                rejectedCount++;
                WarnOnce(
                    $"Keep-flag spawn diverged: player={playerId}, source={oldSource}, target={oldTarget}, " +
                    $"expectedVanilla={keep.Vanilla}, expectedCorrected={keep.Corrected}, keep={keep}. " +
                    "No mutation was performed.");
                return;
            }

            pendingKeeps.Remove(playerId);
            capturedKeeps[playerId] = keep.WithResolvedPositions(actualVanilla, actualCorrected);
            args.SourceWorldTileX = actualCorrected.X;
            args.SourceWorldTileY = actualCorrected.Y;
            args.TargetWorldTileX = actualCorrected.X;
            args.TargetWorldTileY = actualCorrected.Y;
            if (oldSource.X == actualCorrected.X && oldSource.Y == actualCorrected.Y)
                alreadyCorrectBeforeSpawnCount++;
            else
                correctedBeforeSpawnCount++;
        }

        private void OnBeforeMapLoad(MissionLifecycleNotification args)
        {
            ResetMapState();
            newMapActive = args.Context.StartKind == MissionStartKind.NewGame;
        }

        private void OnSessionStarted(Shared.GameplaySessionStartedContext context)
        {
            if (context.Kind != Shared.GameplaySessionStartKind.NewMap || context.IsReplay)
            {
                ResetMapState();
                return;
            }

            newMapActive = true;
            sessionNumber++;
            remainingScanTicks = Enabled ? NewMapScanDelayTicks : -1;
        }

        private void ResetMapState()
        {
            newMapActive = false;
            pendingKeeps.Clear();
            capturedKeeps.Clear();
            remainingScanTicks = -1;
            capturedCount = 0;
            correctedBeforeSpawnCount = 0;
            alreadyCorrectBeforeSpawnCount = 0;
            rejectedCount = 0;
            warningLogged = false;
        }

        private void OnGameTick(int tick)
        {
            if (remainingScanTicks < 0)
                return;
            if (remainingScanTicks-- > 0)
                return;

            remainingScanTicks = -1;
            if (!Enabled)
                return;
            try
            {
                ScanAndCorrectStartedFlags(tick);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Keep-flag post-start scan failed: session={sessionNumber}, tick={tick}, error={ex}");
            }
        }

        private void ScanAndCorrectStartedFlags(int tick)
        {
            int keeps = 0;
            int correctedAfterStart = 0;
            int verified = 0;
            int delayedRejected = 0;
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(playerId);
                if (keepId <= 0 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                    keep == null || keep->r_AliveState == AliveState.None)
                {
                    continue;
                }

                keeps++;
                int keepKind = StructKeepKind(keep->r_BuildingType);
                int expectedScale = KeepFlagRotationPolicy.ExpectedScale(keepKind);
                int scale = checked((int)keep->r_OccupyTileGridSize);
                if (keepKind == 0 || scale != expectedScale || keep->r_PlayerIdOwner != playerId)
                {
                    delayedRejected++;
                    continue;
                }

                int originX = keep->r_TilePositionXBegin;
                int originY = keep->r_TilePositionYBegin;
                int rawOrientation;
                int normalizedOrientation;
                KeepFlagPosition vanilla;
                KeepFlagPosition desired;
                if (capturedKeeps.TryGetValue(playerId, out KeepSpawn captured))
                {
                    if (captured.OriginX != originX || captured.OriginY != originY ||
                        captured.Scale != scale ||
                        !KeepFlagRotationPolicy.TryGetPositions(
                            originX,
                            originY,
                            scale,
                            captured.RawOrientation,
                            out vanilla,
                            out desired))
                    {
                        delayedRejected++;
                        WarnOnce(
                            $"Keep-flag delayed scan found a capture mismatch: player={playerId}, keepId={keepId}, " +
                            $"origin=({originX},{originY}), scale={scale}, captured={captured}. " +
                            "No mutation was performed.");
                        continue;
                    }

                    rawOrientation = captured.RawOrientation;
                    normalizedOrientation = captured.NormalizedOrientation;
                }
                else
                {
                    rawOrientation = keep->r_SpriteVariationIndex;
                    if (rawOrientation != 15 ||
                        !KeepFlagRotationPolicy.TryNormalizeOrientation(rawOrientation, out normalizedOrientation) ||
                        !KeepFlagRotationPolicy.TryGetPositions(
                            originX,
                            originY,
                            scale,
                            rawOrientation,
                            out vanilla,
                            out desired))
                    {
                        delayedRejected++;
                        continue;
                    }
                }

                LoadedFlagMatch match = FindLoadedMainFlag(
                    playerId,
                    originX,
                    originY,
                    scale,
                    rawOrientation);
                if (match.Ambiguous || match.ProjectileId == 0 ||
                    !GameProjectileManagerAPI.Instance.TryGetProjectileById(
                        match.ProjectileId,
                        out GameProjectile* flag) || flag == null)
                {
                    delayedRejected++;
                    if (match.Ambiguous)
                    {
                        WarnOnce(
                            $"Keep-flag delayed scan found ambiguous Flag3 candidates: player={playerId}, " +
                            $"keepId={keepId}, matches={match.MatchCount}. No mutation was performed.");
                    }
                    continue;
                }

                vanilla = match.Vanilla;
                desired = match.Desired;
                var oldSource = new KeepFlagPosition(flag->r_SourceWorldTileX, flag->r_SourceWorldTileY);
                var oldTarget = new KeepFlagPosition(flag->r_TargetWorldTileX, flag->r_TargetWorldTileY);
                var oldCurrent = new KeepFlagPosition(flag->r_CurrentTileX, flag->r_CurrentTileY);
                var oldUnknown = new KeepFlagPosition(flag->r_UnkWorldTileX, flag->r_UnkWorldTileY);
                KeepFlagPosition vanillaTile = KeepFlagRotationPolicy.ToTilePosition(vanilla);
                KeepFlagPosition desiredTile = KeepFlagRotationPolicy.ToTilePosition(desired);
                if (oldSource.X == desired.X && oldSource.Y == desired.Y &&
                    oldTarget.X == desired.X && oldTarget.Y == desired.Y &&
                    oldCurrent.X == desiredTile.X && oldCurrent.Y == desiredTile.Y)
                {
                    verified++;
                    continue;
                }

                if (oldSource.X != vanilla.X || oldSource.Y != vanilla.Y ||
                    oldTarget.X != vanilla.X || oldTarget.Y != vanilla.Y ||
                    oldCurrent.X != vanillaTile.X || oldCurrent.Y != vanillaTile.Y)
                {
                    delayedRejected++;
                    WarnOnce(
                        $"Keep-flag delayed state diverged: player={playerId}, projectileId={match.ProjectileId}, " +
                        $"source={oldSource}, target={oldTarget}, current={oldCurrent}, " +
                        $"expectedVanilla={vanilla}, desired={desired}. No mutation was performed.");
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
                flag->r_CurrentTileId = checked((uint)GameTileManagerAPI.Instance.GetTileId(
                    desiredTile.X,
                    desiredTile.Y));
                correctedAfterStart++;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Keep-flag rotation summary: session={sessionNumber}, tick={tick}, enabled={Enabled}, " +
                $"captured={capturedCount}, correctedBeforeSpawn={correctedBeforeSpawnCount}, " +
                $"alreadyCorrectBeforeSpawn={alreadyCorrectBeforeSpawnCount}, keeps={keeps}, " +
                $"correctedAfterStart={correctedAfterStart}, verified={verified}, " +
                $"rejected={rejectedCount + delayedRejected}.");
        }

        private LoadedFlagMatch FindLoadedMainFlag(
            int playerId,
            int originX,
            int originY,
            int scale,
            int orientation)
        {
            Span<GameProjectile> projectiles = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            int resultId = 0;
            int matches = 0;
            KeepFlagPosition matchedVanilla = default;
            KeepFlagPosition matchedDesired = default;
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

                var actual = new KeepFlagPosition(
                    projectile.r_SourceWorldTileX,
                    projectile.r_SourceWorldTileY);
                if (!KeepFlagRotationPolicy.TryResolveObservedPosition(
                    originX,
                    originY,
                    scale,
                    orientation,
                    actual,
                    out KeepFlagPosition candidateVanilla,
                    out KeepFlagPosition candidateDesired))
                {
                    continue;
                }

                matches++;
                matchedVanilla = candidateVanilla;
                matchedDesired = candidateDesired;
                if (!KeepFlagRotationPolicy.TryGetProjectileIdFromSpanIndex(spanIndex, out resultId))
                    throw new InvalidOperationException("A live projectile occupied reserved slot zero.");
            }

            KeepFlagRotationPolicy.TryResolveUniqueProjectileId(matches, resultId, out int uniqueProjectileId);
            return new LoadedFlagMatch(uniqueProjectileId, matches, matchedVanilla, matchedDesired);
        }

        private void WarnOnce(string message)
        {
            if (warningLogged)
                return;
            warningLogged = true;
            Shared.DebugLogHelper.LogWarning(log, message);
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
                int playerId,
                eMappers mapper,
                int originX,
                int originY,
                int scale,
                int rawOrientation,
                int normalizedOrientation,
                KeepFlagPosition vanilla,
                KeepFlagPosition corrected)
            {
                PlayerId = playerId;
                Mapper = mapper;
                OriginX = originX;
                OriginY = originY;
                Scale = scale;
                RawOrientation = rawOrientation;
                NormalizedOrientation = normalizedOrientation;
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
            internal KeepFlagPosition Vanilla { get; }
            internal KeepFlagPosition Corrected { get; }

            internal KeepSpawn WithResolvedPositions(
                KeepFlagPosition vanilla,
                KeepFlagPosition corrected) =>
                new KeepSpawn(
                    PlayerId,
                    Mapper,
                    OriginX,
                    OriginY,
                    Scale,
                    RawOrientation,
                    NormalizedOrientation,
                    vanilla,
                    corrected);

            public override string ToString() =>
                $"P{PlayerId}/{Mapper}/origin=({OriginX},{OriginY})/scale={Scale}/" +
                $"orientationRaw={RawOrientation}/orientationNormalized={NormalizedOrientation}/" +
                $"direction={KeepFlagRotationPolicy.DescribeDirection(NormalizedOrientation)}/" +
                $"targetCorner={KeepFlagRotationPolicy.DescribeCorner(NormalizedOrientation)}";
        }

        private readonly struct LoadedFlagMatch
        {
            internal LoadedFlagMatch(
                int projectileId,
                int matchCount,
                KeepFlagPosition vanilla,
                KeepFlagPosition desired)
            {
                ProjectileId = projectileId;
                MatchCount = matchCount;
                Vanilla = vanilla;
                Desired = desired;
            }

            internal int ProjectileId { get; }
            internal int MatchCount { get; }
            internal KeepFlagPosition Vanilla { get; }
            internal KeepFlagPosition Desired { get; }
            internal bool Ambiguous => MatchCount > 1;
        }
    }
}
