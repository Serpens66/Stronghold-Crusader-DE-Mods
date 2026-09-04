using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MoatFillTargetTest
{
    internal sealed unsafe class MoatFillTargetTestRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FindMoatWorkTargetDelegate(
            IntPtr tileManager, int playerId, int unitId, int relationshipMode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ResolveMoatWorkTileDelegate(
            IntPtr tileManager, int moatId, int mode, uint sourceX, uint sourceY);

        private const int FindMoatWorkTargetRva = 0x69D60;
        private const int ResolveMoatWorkTileRva = 0x6AF60;
        private const int StateDispatcherRva = 0x13F540;
        private const int StateDispatcherSize = 10069;
        private const int MovementPlannerRva = 0x196280;
        private const int MovementPlannerLowFlagGateRva = 0x196464;
        private const int MovementPlannerStructureFlagGateRva = 0x19648D;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int NativeHeightLayerRva = 0x4DDD350;
        private const int PathRegionGridRva = 0x50EC690;
        private const int MapWidth = 800;
        private const int NativeTileCount = 0x4E520;
        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatRecordTileIdOffset = 0x00;
        private const int MoatRecordXOffset = 0x04;
        private const int MoatRecordYOffset = 0x06;
        private const int MoatRecordReservationOffset = 0x0F;
        private const int SelectedMoatApproachXOffset = 0x2038E38;
        private const int SelectedMoatApproachYOffset = 0x2038E3C;
        private const int SelectedMoatTileIdOffset = 0x2038E40;
        private const int MaximumMoatRecordId = 0x31F;
        private const ushort FillWorkState = 0x7D;
        private const ushort IdleCommand = 3;
        private const int VanillaApproachHeightTolerance = 0x10;

        // Exact order used by Vanilla's 0x6AF60 resolver.
        private static readonly int[] NeighbourX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] NeighbourY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private const string FindMoatWorkTargetPattern =
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 " +
            "48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4";

        private const string ResolveMoatWorkTilePattern =
            "44 89 4C 24 20 53 57 41 57 48 83 EC 20 48 63 44 24 60 " +
            "45 8B D0 49 63 D9 4C 63 DA 81 FB 1F 03 00 00 " +
            "0F 87 ?? ?? ?? ?? 3D 1F 03 00 00 0F 87 ?? ?? ?? ??";

        [ThreadStatic]
        private static PendingApproach pendingApproach;

        private readonly ManualLogSource log;
        private readonly ulong libraryBase;
        private readonly byte* movementTargetAvailability;
        private readonly byte* nativeHeightLayer;
        private readonly short* pathRegionGrid;
        private readonly uint* tileFlags;
        private readonly Dictionary<int, int> selectionCountByUnit = new Dictionary<int, int>();
        private readonly Dictionary<int, AssignmentTracker> trackedAssignments =
            new Dictionary<int, AssignmentTracker>();
        private readonly List<int> completedTrackerIds = new List<int>();

        private FindMoatWorkTargetDelegate originalFindMoatWorkTarget;
        private FindMoatWorkTargetDelegate rootedFindMoatWorkTarget;
        private ResolveMoatWorkTileDelegate originalResolveMoatWorkTile;
        private ResolveMoatWorkTileDelegate rootedResolveMoatWorkTile;
        private NativeDetour findMoatWorkTargetDetour;
        private NativeDetour resolveMoatWorkTileDetour;
        private IDisposable mapStartSubscription;
        private IDisposable mapLoadSubscription;
        private IDisposable mapUnloadSubscription;
        private bool tickSubscribed;
        private bool runtimeTickLogged;
        private int observationTick;

        public MoatFillTargetTestRuntime(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.libraryBase = libraryBase;
            ValidateNativeContracts(memory);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            nativeHeightLayer = (byte*)(libraryBase + NativeHeightLayerRva);
            pathRegionGrid = (short*)(libraryBase + PathRegionGridRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
        }

        public void Apply()
        {
            rootedFindMoatWorkTarget = FindMoatWorkTargetWithFreeApproach;
            rootedResolveMoatWorkTile = ResolveMoatWorkTileWithSelectedApproach;
            NativeDetour pendingFind = null;
            NativeDetour pendingResolve = null;
            bool findApplied = false;
            bool resolveApplied = false;
            try
            {
                pendingFind = CreateDetour(
                    libraryBase + FindMoatWorkTargetRva,
                    rootedFindMoatWorkTarget);
                originalFindMoatWorkTarget =
                    pendingFind.GenerateTrampoline<FindMoatWorkTargetDelegate>();
                pendingResolve = CreateDetour(
                    libraryBase + ResolveMoatWorkTileRva,
                    rootedResolveMoatWorkTile);
                originalResolveMoatWorkTile =
                    pendingResolve.GenerateTrampoline<ResolveMoatWorkTileDelegate>();

                pendingFind.Apply();
                findApplied = true;
                pendingResolve.Apply();
                resolveApplied = true;
                findMoatWorkTargetDetour = pendingFind;
                resolveMoatWorkTileDetour = pendingResolve;

                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ResetMapState("start-map"));
                mapLoadSubscription = MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ResetMapState("load-save"));
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => ResetMapState("unload-map"));
                GameTimeManagerAPI.Instance.OnTick += ObserveAssignments;
                tickSubscribed = true;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "MOAT_FILL_TARGET_READY: selector=0x69D60, resolver=0x6AF60, " +
                    "relationshipMode=2, initialAndAutomaticFollowUp=true, occupancy=physicalLivingUnitsOnly, " +
                    "regionRule=vanillaEqualityIncludingZero, completedMoatApproaches=blocked, " +
                    "movementFlagGates=0x30+0x10000100, digMoatUnchanged=true, " +
                    "reservationStep=20, temporaryExclusion=100.");
            }
            catch
            {
                if (tickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= ObserveAssignments;
                    tickSubscribed = false;
                }
                mapStartSubscription?.Dispose();
                mapLoadSubscription?.Dispose();
                mapUnloadSubscription?.Dispose();
                UndoAndDispose(pendingResolve, resolveApplied);
                UndoAndDispose(pendingFind, findApplied);
                throw;
            }
        }

        private int FindMoatWorkTargetWithFreeApproach(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode)
        {
            pendingApproach = null;
            if (!MoatFillApproachPolicy.ShouldInspectSelection(relationshipMode) ||
                !TryCaptureUnit(unitId, playerId, out GameUnit* unit, out int sourceX, out int sourceY))
            {
                return originalFindMoatWorkTarget(tileManager, playerId, unitId, relationshipMode);
            }

            if (tileManager == IntPtr.Zero ||
                tileManager != GameTileManagerAPI.Instance.GetTileManager())
            {
                return originalFindMoatWorkTarget(tileManager, playerId, unitId, relationshipMode);
            }

            var exclusions = new List<ExcludedReservation>();
            int firstVanillaMoatId = -1;
            int currentMoatId = -1;
            int skippedMoats = 0;
            bool currentReservationRetained = false;
            var skippedMoatIds = new List<int>();
            ApproachDecisionSummary aggregate = default;
            try
            {
                int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
                int maximumAttempts = Math.Min(Math.Max(moatCount, 1), MaximumMoatRecordId + 1);
                for (int attempt = 0; attempt < maximumAttempts; attempt++)
                {
                    currentReservationRetained = false;
                    currentMoatId = originalFindMoatWorkTarget(
                        tileManager, playerId, unitId, relationshipMode);
                    if (firstVanillaMoatId < 0)
                        firstVanillaMoatId = currentMoatId;
                    if (currentMoatId <= 0)
                        break;
                    currentReservationRetained = true;

                    if (!TryReadMoatRecord(
                            tileManager,
                            currentMoatId,
                            out byte* record,
                            out int moatTileId,
                            out int moatX,
                            out int moatY))
                    {
                        // The original reservation is still intact; fail closed to Vanilla.
                        return currentMoatId;
                    }

                    ApproachCandidate[] candidates = BuildCandidates(
                        unitId, sourceX, sourceY, moatX, moatY);
                    if (MoatFillApproachPolicy.TryChoose(
                            candidates,
                            sourceX,
                            sourceY,
                            out ApproachCandidate selected,
                            out ApproachDecisionSummary summary))
                    {
                        Merge(ref aggregate, summary);
                        int invocation = IncrementSelectionCount(unitId);
                        string phase = invocation == 1 ? "initial" : "automatic-follow-up";
                        pendingApproach = new PendingApproach(
                            tileManager,
                            unitId,
                            playerId,
                            currentMoatId,
                            moatTileId,
                            sourceX,
                            sourceY,
                            selected,
                            phase);
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"MOAT_FILL_TARGET_SELECTED: phase={phase}, unit={unitId}, player={playerId}, " +
                            $"firstVanillaMoat={firstVanillaMoatId}, selectedMoat={currentMoatId}, " +
                            $"moat=({moatX},{moatY})/{moatTileId}, approach=({selected.X},{selected.Y})/" +
                            $"{selected.TileId}, nativeOrder={selected.Order}, skippedMoats={skippedMoats}, " +
                            $"skippedMoatIds={FormatIds(skippedMoatIds)}, " +
                            $"occupiedNeighbours={FormatOccupiedNeighbours(candidates)}, " +
                            $"checked={aggregate.Checked}, occupied={aggregate.Occupied}, " +
                            $"completedMoatRejected={aggregate.CompletedMoatRejected}, " +
                            $"terrainBlocked={aggregate.BlockedTerrain}, geometryRejected={aggregate.NativeGeometryRejected}.");
                        return currentMoatId;
                    }

                    Merge(ref aggregate, summary);
                    byte reservationAfterSelection = record[MoatRecordReservationOffset];
                    if (!MoatFillApproachPolicy.TryUndoVanillaReservation(
                            reservationAfterSelection,
                            out byte reservationBeforeSelection))
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"MOAT_FILL_RESERVATION_UNSAFE: unit={unitId}, moat={currentMoatId}, " +
                            $"reservationAfterSelection={reservationAfterSelection}; preserving Vanilla selection.");
                        return currentMoatId;
                    }

                    exclusions.Add(new ExcludedReservation(record, reservationBeforeSelection));
                    currentReservationRetained = false;
                    record[MoatRecordReservationOffset] =
                        MoatFillApproachPolicy.TemporarilyExcludedReservation;
                    skippedMoatIds.Add(currentMoatId);
                    skippedMoats++;
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MOAT_FILL_TARGET_NONE: unit={unitId}, player={playerId}, " +
                    $"firstVanillaMoat={firstVanillaMoatId}, skippedMoats={skippedMoats}, " +
                    $"skippedMoatIds={FormatIds(skippedMoatIds)}, " +
                    $"checked={aggregate.Checked}, occupied={aggregate.Occupied}, " +
                    $"completedMoatRejected={aggregate.CompletedMoatRejected}, " +
                    $"terrainBlocked={aggregate.BlockedTerrain}, geometryRejected={aggregate.NativeGeometryRejected}; " +
                    "no physically free Vanilla-radius approach remains.");
                return currentMoatId;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MOAT_FILL_TARGET_ERROR: unit={unitId}, selectedMoat={currentMoatId}; " +
                    $"preserving the current Vanilla result when safely possible. {ex}");
                // Restore exclusions before either returning or invoking Vanilla again.
                RestoreExclusions(exclusions);
                exclusions.Clear();
                if (currentMoatId > 0 && currentReservationRetained)
                    return currentMoatId;
                return originalFindMoatWorkTarget(tileManager, playerId, unitId, relationshipMode);
            }
            finally
            {
                RestoreExclusions(exclusions);
            }
        }

        private int ResolveMoatWorkTileWithSelectedApproach(
            IntPtr tileManager,
            int moatId,
            int mode,
            uint sourceX,
            uint sourceY)
        {
            PendingApproach pending = pendingApproach;
            bool matches = pending != null && pending.Matches(
                tileManager, moatId, sourceX, sourceY);
            int vanillaResult = originalResolveMoatWorkTile(
                tileManager, moatId, mode, sourceX, sourceY);

            if (mode == MoatFillApproachPolicy.PublishMoatTileMode)
            {
                if (!matches || vanillaResult <= 0)
                    pendingApproach = null;
                return vanillaResult;
            }

            pendingApproach = null;
            if (!MoatFillApproachPolicy.ShouldReplaceResolverResult(mode, matches))
                return vanillaResult;

            byte* manager = null;
            int vanillaMoatTileId = 0;
            int vanillaApproachX = 0;
            int vanillaApproachY = 0;
            bool vanillaFieldsCaptured = false;
            try
            {
                string rejectionReason = "context-or-record";
                if (tileManager == IntPtr.Zero ||
                    tileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                    !TryCaptureUnit(
                        pending.UnitId,
                        pending.PlayerId,
                        out _,
                        out int currentX,
                        out int currentY) ||
                    currentX != pending.SourceX || currentY != pending.SourceY ||
                    !TryReadMoatRecord(
                        tileManager,
                        moatId,
                        out _,
                        out int moatTileId,
                        out _,
                        out _) ||
                    moatTileId != pending.MoatTileId ||
                    !IsApproachStillEligible(pending, currentX, currentY, out rejectionReason))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"MOAT_FILL_APPROACH_REVALIDATION_REJECTED: phase={pending.Phase}, " +
                        $"unit={pending.UnitId}, moat={pending.MoatId}, " +
                        $"approach=({pending.Approach.X},{pending.Approach.Y})/{pending.Approach.TileId}, " +
                        $"reason={rejectionReason}; Vanilla result retained.");
                    return vanillaResult;
                }

                manager = (byte*)tileManager.ToPointer();
                vanillaMoatTileId = *(int*)(manager + SelectedMoatTileIdOffset);
                vanillaApproachX = *(int*)(manager + SelectedMoatApproachXOffset);
                vanillaApproachY = *(int*)(manager + SelectedMoatApproachYOffset);
                vanillaFieldsCaptured = true;
                *(int*)(manager + SelectedMoatTileIdOffset) = pending.MoatTileId;
                *(int*)(manager + SelectedMoatApproachXOffset) = pending.Approach.X;
                *(int*)(manager + SelectedMoatApproachYOffset) = pending.Approach.Y;
                trackedAssignments[pending.UnitId] = new AssignmentTracker(
                    pending.UnitId,
                    pending.MoatId,
                    pending.Approach.X,
                    pending.Approach.Y,
                    pending.Approach.TileId,
                    pending.Phase,
                    observationTick);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MOAT_FILL_APPROACH_PUBLISHED: phase={pending.Phase}, unit={pending.UnitId}, " +
                    $"moat={pending.MoatId}/{pending.MoatTileId}, approach=({pending.Approach.X}," +
                    $"{pending.Approach.Y})/{pending.Approach.TileId}, vanillaResolver={vanillaResult}, " +
                    $"replacement={pending.Approach.TileId}.");
                return pending.Approach.TileId;
            }
            catch (Exception ex)
            {
                if (vanillaFieldsCaptured)
                {
                    *(int*)(manager + SelectedMoatTileIdOffset) = vanillaMoatTileId;
                    *(int*)(manager + SelectedMoatApproachXOffset) = vanillaApproachX;
                    *(int*)(manager + SelectedMoatApproachYOffset) = vanillaApproachY;
                }
                trackedAssignments.Remove(pending.UnitId);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MOAT_FILL_RESOLVER_ERROR: unit={pending.UnitId}, moat={moatId}; Vanilla result retained. {ex}");
                return vanillaResult;
            }
        }

        private ApproachCandidate[] BuildCandidates(
            int unitId,
            int sourceX,
            int sourceY,
            int moatX,
            int moatY)
        {
            var candidates = new ApproachCandidate[NeighbourX.Length];
            int sourceTileId = GameTileManagerAPI.Instance.GetTileId(sourceX, sourceY);
            if (!IsValidTileId(sourceTileId))
                return candidates;
            short sourceRegion = pathRegionGrid[sourceTileId];
            byte sourceHeight = nativeHeightLayer[sourceTileId];

            for (int order = 0; order < candidates.Length; order++)
            {
                int x = moatX + NeighbourX[order];
                int y = moatY + NeighbourY[order];
                bool inBounds = (uint)x < MapWidth && (uint)y < MapWidth;
                int tileId = inBounds ? GameTileManagerAPI.Instance.GetTileId(x, y) : -1;
                bool valid = inBounds && IsValidTileId(tileId) &&
                    movementTargetAvailability[y * MapWidth + x] != 0;
                bool heightAllowed = valid &&
                    nativeHeightLayer[tileId] <= sourceHeight + VanillaApproachHeightTolerance;
                bool sameRegion = valid && MoatFillApproachPolicy.IsSameNativeRegion(
                    sourceRegion, pathRegionGrid[tileId]);
                uint flags = valid ? tileFlags[tileId] : 0;
                bool completedMoat = valid && MoatFillApproachPolicy.IsCompletedMoat(flags);
                // Mirror the ordinary-unit gates in 0x196280 so the published tile is not
                // rejected immediately by Vanilla's downstream movement planner.
                bool walkable = valid &&
                    !MoatFillApproachPolicy.HasDownstreamMovementBlockingFlags(flags);
                int occupantUnitId = 0;
                bool occupied = valid &&
                    IsOccupiedByOtherLivingUnit(tileId, unitId, out occupantUnitId);
                candidates[order] = new ApproachCandidate(
                    order, x, y, tileId, valid, heightAllowed, sameRegion,
                    completedMoat, walkable, occupied, occupantUnitId);
            }
            return candidates;
        }

        private bool IsApproachStillEligible(
            PendingApproach pending,
            int currentX,
            int currentY,
            out string rejectionReason)
        {
            int tileId = pending.Approach.TileId;
            int x = pending.Approach.X;
            int y = pending.Approach.Y;
            if ((uint)x >= MapWidth || (uint)y >= MapWidth || !IsValidTileId(tileId) ||
                GameTileManagerAPI.Instance.GetTileId(x, y) != tileId ||
                movementTargetAvailability[y * MapWidth + x] == 0)
            {
                rejectionReason = "invalid-or-unavailable";
                return false;
            }

            int sourceTileId = GameTileManagerAPI.Instance.GetTileId(currentX, currentY);
            if (!IsValidTileId(sourceTileId) ||
                nativeHeightLayer[tileId] >
                    nativeHeightLayer[sourceTileId] + VanillaApproachHeightTolerance)
            {
                rejectionReason = "height";
                return false;
            }
            if (pathRegionGrid[tileId] != pathRegionGrid[sourceTileId])
            {
                rejectionReason = "region";
                return false;
            }

            uint flags = tileFlags[tileId];
            if (MoatFillApproachPolicy.IsCompletedMoat(flags))
            {
                rejectionReason = "completed-moat";
                return false;
            }
            if (MoatFillApproachPolicy.HasDownstreamMovementBlockingFlags(flags))
            {
                rejectionReason = "movement-flags";
                return false;
            }
            if (IsOccupiedByOtherLivingUnit(tileId, pending.UnitId, out int occupantUnitId))
            {
                rejectionReason = $"occupied-by-unit-{occupantUnitId}";
                return false;
            }

            rejectionReason = "none";
            return true;
        }

        private bool IsOccupiedByOtherLivingUnit(
            int tileId,
            int currentUnitId,
            out int occupantUnitId)
        {
            occupantUnitId = GameTileManagerAPI.Instance.GetTileUnitId(tileId);
            if (occupantUnitId == 0 || occupantUnitId == currentUnitId)
                return false;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                    occupantUnitId, out GameUnit* occupant) || occupant == null)
            {
                // The selected policy counts only a currently resolvable living unit.
                return false;
            }
            return occupant->r_AliveState == AliveState.IsAlive;
        }

        private static bool TryCaptureUnit(
            int unitId,
            int playerId,
            out GameUnit* unit,
            out int sourceX,
            out int sourceY)
        {
            unit = null;
            sourceX = -1;
            sourceY = -1;
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_ControllableForPlayerId != playerId || !CanDigMoat(unit->r_UnitChimp))
            {
                return false;
            }
            sourceX = unit->r_CurrentTilePositionX;
            sourceY = unit->r_CurrentTilePositionY;
            return (uint)sourceX < MapWidth && (uint)sourceY < MapWidth;
        }

        private static bool CanDigMoat(eChimps type)
        {
            // Mirrors Vanilla's per-unit switch in the moat command handler.
            switch (type)
            {
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadMoatRecord(
            IntPtr tileManager,
            int moatId,
            out byte* record,
            out int tileId,
            out int x,
            out int y)
        {
            record = null;
            tileId = -1;
            x = -1;
            y = -1;
            if (tileManager == IntPtr.Zero)
                return false;
            byte* manager = (byte*)tileManager.ToPointer();
            int count = *(int*)(manager + MoatRecordCountOffset);
            if (moatId <= 0 || moatId > MaximumMoatRecordId || moatId >= count)
                return false;
            record = manager + MoatRecordArrayOffset + moatId * MoatRecordSize;
            tileId = *(int*)(record + MoatRecordTileIdOffset);
            x = *(short*)(record + MoatRecordXOffset);
            y = *(short*)(record + MoatRecordYOffset);
            return IsValidTileId(tileId) && (uint)x < MapWidth && (uint)y < MapWidth &&
                GameTileManagerAPI.Instance.GetTileId(x, y) == tileId;
        }

        private void ObserveAssignments(int gameTick)
        {
            try
            {
                ObserveAssignmentsCore(gameTick);
            }
            catch (Exception ex)
            {
                trackedAssignments.Clear();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MOAT_FILL_OBSERVER_ERROR: assignment diagnostics reset without affecting gameplay. {ex}");
            }
        }

        private void ObserveAssignmentsCore(int gameTick)
        {
            observationTick++;
            if (!runtimeTickLogged)
            {
                runtimeTickLogged = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MOAT_FILL_RUNTIME_TICK: observationTick={observationTick}, gameTick={gameTick}, " +
                    "runtimeSurvivedStartupCleanup=true.");
            }
            completedTrackerIds.Clear();
            foreach (KeyValuePair<int, AssignmentTracker> pair in trackedAssignments)
            {
                AssignmentTracker tracker = pair.Value;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        tracker.UnitId, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"MOAT_FILL_ASSIGNMENT_ENDED: phase={tracker.Phase}, unit={tracker.UnitId}, " +
                        $"reason=unit-missing-or-dead, moat={tracker.MoatId}.");
                    completedTrackerIds.Add(tracker.UnitId);
                    continue;
                }

                if (unit->r_AIState == FillWorkState)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MOAT_FILL_ASSIGNMENT_ACCEPTED: phase={tracker.Phase}, unit={tracker.UnitId}, " +
                        $"moat={tracker.MoatId}, approach=({tracker.X},{tracker.Y})/{tracker.TileId}, " +
                        $"aiState={unit->r_AIState}, ticks={observationTick - tracker.StartTick}.");
                    completedTrackerIds.Add(tracker.UnitId);
                }
                else if (unit->r_AI_LastIssuedTribeCommand == IdleCommand)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"MOAT_FILL_ASSIGNMENT_IDLE: phase={tracker.Phase}, unit={tracker.UnitId}, " +
                        $"moat={tracker.MoatId}, approach=({tracker.X},{tracker.Y})/{tracker.TileId}, " +
                        $"aiState={unit->r_AIState}, ticks={observationTick - tracker.StartTick}.");
                    completedTrackerIds.Add(tracker.UnitId);
                }
                else if (observationTick - tracker.StartTick > 25)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"MOAT_FILL_ASSIGNMENT_TIMEOUT: phase={tracker.Phase}, unit={tracker.UnitId}, " +
                        $"moat={tracker.MoatId}, approach=({tracker.X},{tracker.Y})/{tracker.TileId}, " +
                        $"aiState={unit->r_AIState}, command={unit->r_AI_LastIssuedTribeCommand}.");
                    completedTrackerIds.Add(tracker.UnitId);
                }
            }
            for (int index = 0; index < completedTrackerIds.Count; index++)
                trackedAssignments.Remove(completedTrackerIds[index]);
        }

        private void ResetMapState(string reason)
        {
            pendingApproach = null;
            selectionCountByUnit.Clear();
            trackedAssignments.Clear();
            observationTick = 0;
            runtimeTickLogged = false;
            Shared.DebugLogHelper.LogInfo(log, $"MOAT_FILL_MAP_RESET: reason={reason}.");
        }

        private int IncrementSelectionCount(int unitId)
        {
            selectionCountByUnit.TryGetValue(unitId, out int count);
            count++;
            selectionCountByUnit[unitId] = count;
            return count;
        }

        private static void Merge(
            ref ApproachDecisionSummary aggregate,
            ApproachDecisionSummary value)
        {
            aggregate.Checked += value.Checked;
            aggregate.Invalid += value.Invalid;
            aggregate.NativeGeometryRejected += value.NativeGeometryRejected;
            aggregate.CompletedMoatRejected += value.CompletedMoatRejected;
            aggregate.BlockedTerrain += value.BlockedTerrain;
            aggregate.Occupied += value.Occupied;
            aggregate.Free += value.Free;
        }

        private static string FormatOccupiedNeighbours(ApproachCandidate[] candidates)
        {
            var entries = new List<string>();
            for (int index = 0; index < candidates.Length; index++)
            {
                ApproachCandidate candidate = candidates[index];
                if (candidate.Occupied)
                {
                    entries.Add(
                        $"{candidate.Order}:({candidate.X},{candidate.Y})/{candidate.TileId}" +
                        $"=unit{candidate.OccupantUnitId}");
                }
            }
            return entries.Count == 0 ? "none" : string.Join(",", entries);
        }

        private static string FormatIds(List<int> ids) =>
            ids.Count == 0 ? "none" : string.Join(",", ids);

        private static void RestoreExclusions(List<ExcludedReservation> exclusions)
        {
            for (int index = 0; index < exclusions.Count; index++)
                exclusions[index].Restore();
        }

        private static bool IsValidTileId(int tileId) =>
            tileId >= 0 && tileId < NativeTileCount;

        private static NativeDetour CreateDetour<TDelegate>(ulong address, TDelegate callback)
            where TDelegate : Delegate =>
            new NativeDetour(
                (IntPtr)unchecked((long)address),
                Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });

        private static void UndoAndDispose(NativeDetour detour, bool applied)
        {
            if (applied)
                detour?.Undo();
            detour?.Dispose();
        }

        private static void ValidateNativeContracts(ReadOnlySpan<byte> memory)
        {
            ResolveExact(memory, FindMoatWorkTargetPattern, FindMoatWorkTargetRva, "moat work-target selector");
            ResolveExact(memory, ResolveMoatWorkTilePattern, ResolveMoatWorkTileRva, "moat work-tile resolver");
            ValidateExactBytes(
                memory,
                FindMoatWorkTargetRva,
                new byte[]
                {
                    0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                    0x10, 0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55,
                    0x41, 0x56, 0x48, 0x83, 0xEC, 0x68, 0x48, 0x8B,
                    0xE9
                },
                "moat work-target selector entry");
            ValidateExactBytes(
                memory,
                ResolveMoatWorkTileRva,
                new byte[]
                {
                    0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x57, 0x41,
                    0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63, 0x44,
                    0x24, 0x60, 0x45, 0x8B, 0xD0, 0x49, 0x63, 0xD9,
                    0x4C, 0x63, 0xDA
                },
                "moat work-tile resolver entry");
            ValidateExactBytes(
                memory,
                MovementPlannerRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x20, 0x55, 0x56, 0x57,
                    0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
                    0x48, 0x83, 0xEC, 0x30, 0x48, 0x63, 0xF2
                },
                "downstream movement planner entry");
            ValidateExactBytes(
                memory,
                MovementPlannerLowFlagGateRva,
                new byte[] { 0xF6, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04, 0x30 },
                "downstream movement low-flag gate");
            ValidateExactBytes(
                memory,
                MovementPlannerStructureFlagGateRva,
                new byte[]
                {
                    0xF7, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04,
                    0x00, 0x01, 0x00, 0x10
                },
                "downstream movement structure-flag gate");
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490)
                throw new InvalidOperationException("GameUnit no longer matches the audited 0x490-byte layout.");
            ValidateField(nameof(GameUnit.r_AliveState), 0x88);
            ValidateField(nameof(GameUnit.r_ControllableForPlayerId), 0x92);
            ValidateField(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
            ValidateField(nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
            ValidateField(nameof(GameUnit.r_AIState), 0x2BC);
            ValidateField(nameof(GameUnit.r_AI_LastIssuedTribeCommand), 0x398);

            int selectorCalls = CountNearCalls(memory, StateDispatcherRva, StateDispatcherSize, FindMoatWorkTargetRva);
            int resolverCalls = CountNearCalls(memory, StateDispatcherRva, StateDispatcherSize, ResolveMoatWorkTileRva);
            int movementPlannerCalls = CountNearCalls(
                memory, StateDispatcherRva, StateDispatcherSize, MovementPlannerRva);
            if (selectorCalls < 2 || resolverCalls < 3 || movementPlannerCalls < 1)
            {
                throw new InvalidOperationException(
                    $"The state-dispatcher moat callgraph changed: selectorCalls={selectorCalls}, " +
                    $"resolverCalls={resolverCalls}, movementPlannerCalls={movementPlannerCalls}.");
            }
        }

        private static void ResolveExact(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string name)
        {
            Shared.NativeResolution result = Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, expectedRva, true, name, null);
            if (result.Rva != expectedRva)
                throw new InvalidOperationException($"{name} resolved to 0x{result.Rva:X}, expected 0x{expectedRva:X}.");
        }

        private static void ValidateExactBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string name)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException($"Native bytes changed for {name}.");
            }
        }

        private static void ValidateField(string fieldName, int expectedOffset)
        {
            int actual = Marshal.OffsetOf(typeof(GameUnit), fieldName).ToInt32();
            if (actual != expectedOffset)
            {
                throw new InvalidOperationException(
                    $"GameUnit.{fieldName} offset is 0x{actual:X}, expected 0x{expectedOffset:X}.");
            }
        }

        private static int CountNearCalls(
            ReadOnlySpan<byte> memory,
            int startRva,
            int length,
            int targetRva)
        {
            int count = 0;
            int end = Math.Min(memory.Length - 5, checked(startRva + length));
            for (int rva = startRva; rva <= end; rva++)
            {
                if (memory[rva] != 0xE8)
                    continue;
                if (Shared.NativePatternResolver.ResolveRelativeTarget(memory, rva + 1, rva + 5) == targetRva)
                    count++;
            }
            return count;
        }

        private sealed class PendingApproach
        {
            public PendingApproach(
                IntPtr tileManager,
                int unitId,
                int playerId,
                int moatId,
                int moatTileId,
                int sourceX,
                int sourceY,
                ApproachCandidate approach,
                string phase)
            {
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                MoatId = moatId;
                MoatTileId = moatTileId;
                SourceX = sourceX;
                SourceY = sourceY;
                Approach = approach;
                Phase = phase;
            }

            public IntPtr TileManager { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int MoatId { get; }
            public int MoatTileId { get; }
            public int SourceX { get; }
            public int SourceY { get; }
            public ApproachCandidate Approach { get; }
            public string Phase { get; }

            public bool Matches(IntPtr tileManager, int moatId, uint sourceX, uint sourceY) =>
                TileManager == tileManager && MoatId == moatId &&
                sourceX == unchecked((uint)SourceX) && sourceY == unchecked((uint)SourceY);
        }

        private readonly struct ExcludedReservation
        {
            public ExcludedReservation(byte* record, byte originalReservation)
            {
                Record = record;
                OriginalReservation = originalReservation;
            }

            public byte* Record { get; }
            public byte OriginalReservation { get; }

            public void Restore() => Record[MoatRecordReservationOffset] = OriginalReservation;
        }

        private readonly struct AssignmentTracker
        {
            public AssignmentTracker(
                int unitId,
                int moatId,
                int x,
                int y,
                int tileId,
                string phase,
                int startTick)
            {
                UnitId = unitId;
                MoatId = moatId;
                X = x;
                Y = y;
                TileId = tileId;
                Phase = phase;
                StartTick = startTick;
            }

            public int UnitId { get; }
            public int MoatId { get; }
            public int X { get; }
            public int Y { get; }
            public int TileId { get; }
            public string Phase { get; }
            public int StartTick { get; }
        }
    }
}
