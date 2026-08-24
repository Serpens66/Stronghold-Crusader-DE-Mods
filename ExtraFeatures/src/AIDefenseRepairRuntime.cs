// Feature: Configure AI repair proximity and deterministic damaged/destroyed defense delays.
using BepInEx.Logging;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace ExtraFeatures
{
    internal sealed unsafe class AIDefenseRepairRuntime : IDisposable
    {
        private const string SaveDataIdentifier = "ExtraFeatures.AIDefenseRepair.v1";
        private const string ExecuteBuildStepPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
        private const string ActiveLayoutIndexReferencePattern =
            "48 63 F2 48 8D 05 ?? ?? ?? ?? 4C 69 CE 3C 58 00 00";
        private const int ExecuteBuildStepRva = 0x51790;
        private const int ActiveLayoutIndexReferenceRva = 0x55F64;
        private const int PlayerRuntimeStateStride = 0x583C;
        private const int PreparedLayoutFrameCount = 0x922;
        private const int PreparedEntryBaseOffset = 0x38;
        private const int PreparedEntrySize = 0x0C;
        private const int PreparedPositionBaseOffset = 0x2F18;
        private const int PreparedPositionCountPerLayout = 0x1B66;
        private const int TicksPerSecond = 40;
        private const int MaximumDamageDelayTicks = 300 * TicksPerSecond;
        private const int DiagnosticRepeatTicks = 10 * TicksPerSecond;
        private const int MaximumTrackedRecords = 20000;
        private const int KindWallTile = 1;
        private const int KindBuilding = 2;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ExecuteBuildStepDelegate(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced);

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<string, TrackedDefense> damaged = new Dictionary<string, TrackedDefense>();
        private readonly Dictionary<string, TrackedDefense> destroyed = new Dictionary<string, TrackedDefense>();
        private readonly Dictionary<string, DiagnosticState> rebuildDiagnostics = new Dictionary<string, DiagnosticState>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>> executeBuildStepHook =
            new HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>>();
        private ulong activeLayoutIndexBaseAddress;
        private PendingDamage pendingDamage;
        private bool initialized;
        private bool nativeInitialized;
        private bool mapActive;
        private bool callbackFailureLogged;
        private bool capacityFailureLogged;
        private bool disposed;
        private int nextDamagedPruneTick;

        public AIDefenseRepairRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized)
                return;

            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnBuildingTileTakeDamage));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingAllowRepairInProximity.Observable.Subscribe(OnAllowRepairInProximity));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => BeginMap()));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ResetMapState()));

            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                    SaveDataIdentifier,
                    SaveState,
                    LoadState,
                    ResetMapState))
            {
                throw new InvalidOperationException("AI defense repair save-data registration failed.");
            }

            initialized = true;
            Shared.DebugLogHelper.LogInfo(log, "Extra Features AI defense repair event and save-state handling initialized.");
        }

        public void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (nativeInitialized)
                return;

            Shared.NativeResolution executeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ExecuteBuildStepPattern,
                ExecuteBuildStepRva,
                referenceHashMatches,
                "AI AIV execute-build-step",
                log);
            Shared.NativeResolution activeLayoutResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ActiveLayoutIndexReferencePattern,
                ActiveLayoutIndexReferenceRva,
                referenceHashMatches,
                "active AIV layout-index table reference",
                log);

            int leaRva = checked(activeLayoutResolution.Rva + 3);
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                checked(leaRva + 3),
                checked(leaRva + 7));
            if (targetRva < 0 || targetRva > memory.Length - 9 * PlayerRuntimeStateStride)
                throw new InvalidOperationException("The active AIV layout-index table resolved outside the loaded image.");

            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            activeLayoutIndexBaseAddress = libraryBase + unchecked((ulong)targetRva);
            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref executeBuildStepHook,
                    libraryBase + unchecked((ulong)executeResolution.Rva),
                    ExecuteBuildStep);
                transaction.Commit();
                if (!executeBuildStepHook.Success)
                    throw new InvalidOperationException("The AI execute-build-step hook was not installed.");
                nativeInitialized = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Extra Features AI defense rebuild hook installed: executeRva=0x{executeResolution.Rva:X}, activeLayoutTable=0x{activeLayoutIndexBaseAddress:X}.");
            }
            catch
            {
                transaction?.Unload();
                transaction?.Dispose();
                transaction = null;
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            damaged.Clear();
            destroyed.Clear();
            rebuildDiagnostics.Clear();
            pendingDamage = null;
        }

        private void BeginMap()
        {
            mapActive = true;
            pendingDamage = null;
            capacityFailureLogged = false;
            nextDamagedPruneTick = CurrentTick();
        }

        private void ResetMapState()
        {
            mapActive = false;
            pendingDamage = null;
            damaged.Clear();
            destroyed.Clear();
            rebuildDiagnostics.Clear();
            capacityFailureLogged = false;
        }

        private void OnBuildingTileTakeDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (!mapActive || !settings.EnableMod ||
                (settings.AIDamagedDefenseRepairDelaySeconds < 0 &&
                 settings.AIDestroyedDefenseRebuildDelaySeconds < 0 &&
                 settings.AIRepairEnemyProximity < 0))
                return;

            try
            {
                if (args.Phase == EventHookPhase.Pre)
                    pendingDamage = CapturePendingDamage(args);
                else if (args.Phase == EventHookPhase.Post)
                    CompletePendingDamage(args);
            }
            catch (Exception ex)
            {
                LogCallbackFailure("damage tracking", ex);
                pendingDamage = null;
            }
        }

        private PendingDamage CapturePendingDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (args.Damage <= 0)
                return null;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int mapTileId = tileApi.GetTileId(args.TileX, args.TileY);
            if (!tileApi.IsValidTileId(mapTileId))
                return null;

            ushort buildingId = tileApi.GetTileBuildingId(mapTileId);
            if (buildingId != 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
            {
                int owner = building->r_PlayerIdOwner;
                if (!IsAI(owner) || !IsDefenseBuilding(building->r_BuildingType) || IsTowerRuin(building->r_BuildingType))
                    return null;
                return PendingDamage.ForBuilding(args, buildingId, building);
            }

            TilePropertyFlag flags = tileApi.GetTilePropertyFlag(mapTileId);
            int wallOwner = tileApi.GetTilePlayerOwnerId(mapTileId);
            if ((flags & TilePropertyFlag.IsWall) == 0 || !IsAI(wallOwner))
                return null;
            return PendingDamage.ForWall(args, mapTileId, wallOwner, tileApi.TileManager.DamageGrid[mapTileId]);
        }

        private void CompletePendingDamage(BuildingTileTakeDamageEventArgs args)
        {
            PendingDamage pending = pendingDamage;
            pendingDamage = null;
            if (pending == null || !pending.Matches(args))
                return;

            int now = CurrentTick();
            if (pending.Kind == KindBuilding)
            {
                GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
                bool sameStandingBuilding = buildingApi.TryGetBuildingById(pending.BuildingId, out GameBuilding* building) &&
                    building->r_GlobalId == pending.GlobalId &&
                    building->r_PlayerIdOwner == pending.PlayerId &&
                    building->r_BuildingType == (eStructs)pending.BuildingType &&
                    building->r_AliveState == AliveState.IsAlive &&
                    building->r_CurrentHealth < pending.Health;
                if (sameStandingBuilding)
                {
                    TrackedDefense record = pending.ToTracked(now);
                    if (TrySetTracked(damaged, record.Identity, record))
                        LogTimer("damaged-defense timer reset", record, now);
                    return;
                }

                bool noLongerStanding = !buildingApi.TryGetBuildingById(pending.BuildingId, out building) ||
                    building->r_GlobalId != pending.GlobalId ||
                    building->r_AliveState != AliveState.IsAlive ||
                    IsTowerRuin(building->r_BuildingType);
                if (noLongerStanding)
                    MoveToDestroyed(pending.ToTracked(now), now);
                return;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            TilePropertyFlag flags = tileApi.GetTilePropertyFlag(pending.TileId);
            byte currentDamage = tileApi.TileManager.DamageGrid[pending.TileId];
            bool wallStillStanding = (flags & TilePropertyFlag.IsWall) != 0 &&
                tileApi.GetTilePlayerOwnerId(pending.TileId) == pending.PlayerId;
            TrackedDefense wallRecord = pending.ToTracked(now);
            if (wallStillStanding && currentDamage != pending.WallDamage)
            {
                if (TrySetTracked(damaged, wallRecord.Identity, wallRecord))
                    LogTimer("damaged-wall timer reset", wallRecord, now);
            }
            else if (!wallStillStanding)
            {
                MoveToDestroyed(wallRecord, now);
            }
        }

        private void MoveToDestroyed(TrackedDefense record, int now)
        {
            damaged.Remove(record.Identity);
            record.StartTick = now;
            if (TrySetTracked(destroyed, record.RebuildIdentity, record))
                LogTimer("destroyed-defense timer started", record, now);
        }

        private void OnAllowRepairInProximity(BuildingAllowRepairInProximityEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || !mapActive || !settings.EnableMod || !IsAI(args.PlayerId))
                return;

            if (settings.AIDamagedDefenseRepairDelaySeconds < 0 && settings.AIRepairEnemyProximity < 0)
                return;

            try
            {
                PruneExpiredDamagedRecords();
                if (!IsStandingDefenseAt(args.PlayerId, args.TileX, args.TileY))
                    return;

                TrackedDefense record = FindStandingDefense(args.PlayerId, args.TileX, args.TileY);
                if (record != null && IsDelayActive(record.StartTick, settings.AIDamagedDefenseRepairDelaySeconds))
                {
                    args.SkipOriginalFunction = true;
                    args.ReturnValue = 1;
                    return;
                }

                if (settings.AIRepairEnemyProximity >= 0)
                    args.Proximity = settings.AIRepairEnemyProximity;
            }
            catch (Exception ex)
            {
                LogCallbackFailure("repair permission", ex);
            }
        }

        private int ExecuteBuildStep(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced)
        {
            try
            {
                if (!settings.EnableMod ||
                    (settings.AIDestroyedDefenseRebuildDelaySeconds < 0 && settings.AIRepairEnemyProximity < 0))
                {
                    return executeBuildStepHook.Value.Hook.Trampoline(
                        aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
                }

                if (mapActive && IsAI(playerId) &&
                    TryReadPreparedFrame(aivStateAddress, playerId, frameIndex, out PreparedFrame frame) &&
                    IsDefenseMapper(frame.Mapper) && IsRebuildStatus(frame.Status) &&
                    TryResolveTargetTiles(aivStateAddress, frame, out int[] targetTileIds))
                {
                    List<TrackedDefense> records = FindDestroyedDefenses(playerId, frame.Mapper, targetTileIds);
                    RemoveRestoredRecords(records);
                    for (int index = 0; index < targetTileIds.Length; index++)
                    {
                        int targetTileId = targetTileIds[index];
                        if (HasMatchingRecord(records, targetTileId) ||
                            IsExpectedDefenseAt(playerId, frame.Mapper, targetTileId, allowNeedsInit: true))
                        {
                            continue;
                        }

                        TrackedDefense fallback = CreateFallbackDestroyedRecord(playerId, frame.Mapper, targetTileId);
                        if (fallback == null)
                            continue;
                        if (!TrySetTracked(destroyed, fallback.RebuildIdentity, fallback))
                            continue;
                        records.Add(fallback);
                        LogTimer("fallback rebuild timer started", fallback, fallback.StartTick);
                    }

                    if (records.Count != 0)
                    {
                        TrackedDefense delayingRecord = FindYoungestDelayedRecord(
                            records,
                            settings.AIDestroyedDefenseRebuildDelaySeconds);
                        if (delayingRecord != null)
                        {
                            LogRebuildBlock("delay", delayingRecord);
                            return 0;
                        }

                        for (int index = 0; index < records.Count; index++)
                        {
                            if (!IsEnemyNear(playerId, records[index]))
                                continue;
                            LogRebuildBlock("enemy", records[index]);
                            return 0;
                        }

                        int result = executeBuildStepHook.Value.Hook.Trampoline(
                            aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
                        for (int index = records.Count - 1; index >= 0; index--)
                        {
                            TrackedDefense record = records[index];
                            if (!IsDefenseRestored(record))
                                continue;
                            destroyed.Remove(record.RebuildIdentity);
                            rebuildDiagnostics.Remove(record.RebuildIdentity);
                            Shared.DebugLogHelper.LogInfo(log, $"AI defense rebuild released and observed: {record.Describe()}.");
                        }
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                LogCallbackFailure("execute-build-step", ex);
            }

            return executeBuildStepHook.Value.Hook.Trampoline(
                aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
        }

        private bool TryReadPreparedFrame(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            out PreparedFrame frame)
        {
            frame = default;
            if (aivStateAddress == 0 || activeLayoutIndexBaseAddress == 0 ||
                playerId < 1 || playerId > 8 || frameIndex < 0 || frameIndex >= PreparedLayoutFrameCount)
            {
                return false;
            }

            int activeLayout = *(int*)(activeLayoutIndexBaseAddress + unchecked((ulong)(playerId * PlayerRuntimeStateStride)));
            if (activeLayout < 0 || activeLayout >= 8)
                return false;
            long entryIndex = checked((long)activeLayout * PreparedLayoutFrameCount + frameIndex);
            byte* entry = (byte*)aivStateAddress + PreparedEntryBaseOffset + checked(entryIndex * PreparedEntrySize);
            frame = new PreparedFrame(activeLayout, entry[0], *(short*)(entry + 2), *(short*)(entry + 4), *(int*)(entry + 8));
            return frame.PositionCount >= 0 && frame.PositionCount <= PreparedPositionCountPerLayout;
        }

        private static bool IsRebuildStatus(byte status) => status == 3 || status == 5;

        private static bool TryResolveTargetTiles(ulong aivStateAddress, PreparedFrame frame, out int[] tileIds)
        {
            tileIds = null;
            DefenseFamily family = GetMapperFamily(frame.Mapper);
            if (family == DefenseFamily.Tower || family == DefenseFamily.Gate)
            {
                if (!GameTileManagerAPI.Instance.IsValidTileId(frame.FirstPositionIndex))
                    return false;
                tileIds = new[] { frame.FirstPositionIndex };
                return true;
            }

            if (family != DefenseFamily.Wall || frame.PositionCount <= 0 ||
                frame.ActiveLayout < 0 || frame.ActiveLayout >= 8 || frame.FirstPositionIndex < 0 ||
                frame.FirstPositionIndex > PreparedPositionCountPerLayout - frame.PositionCount)
            {
                return false;
            }

            int firstIndex = checked(frame.ActiveLayout * PreparedPositionCountPerLayout + frame.FirstPositionIndex);
            int* positions = (int*)((byte*)aivStateAddress + PreparedPositionBaseOffset) + firstIndex;
            tileIds = new int[frame.PositionCount];
            var seen = new HashSet<int>();
            for (int index = 0; index < tileIds.Length; index++)
            {
                int tileId = positions[index];
                if (!GameTileManagerAPI.Instance.IsValidTileId(tileId) || !seen.Add(tileId))
                {
                    tileIds = null;
                    return false;
                }
                tileIds[index] = tileId;
            }
            return true;
        }

        private bool IsEnemyNear(int playerId, TrackedDefense record)
        {
            if (settings.AIRepairEnemyProximity < 0)
                return false;

            int tileX = record.TileXBegin;
            int tileY = record.TileYBegin;
            if (record.TileId >= 0 && GameTileManagerAPI.Instance.IsValidTileId(record.TileId))
            {
                UnmanagedVector2<ushort> position = GameTileManagerAPI.Instance.GetTileVectorFromId(record.TileId);
                tileX = position.X;
                tileY = position.Y;
            }

            long result = BulkBuildingDetours.c_game_allow_repair_for_building_proximity_hook_impl(
                IntPtr.Zero,
                playerId,
                tileX,
                tileY,
                settings.AIRepairEnemyProximity,
                0);
            return result != 0;
        }

        private TrackedDefense FindStandingDefense(int playerId, int tileX, int tileY)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId))
                return null;
            ushort buildingId = tileApi.GetTileBuildingId(tileId);
            if (buildingId != 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
            {
                string identity = TrackedDefense.BuildBuildingIdentity(
                    building->r_PlayerIdOwner,
                    (int)building->r_BuildingType,
                    (int)building->r_GlobalId,
                    building->r_TilePositionXBegin,
                    building->r_TilePositionYBegin);
                damaged.TryGetValue(identity, out TrackedDefense value);
                return value;
            }

            damaged.TryGetValue(TrackedDefense.BuildWallIdentity(playerId, tileId), out TrackedDefense wall);
            return wall;
        }

        private static bool IsStandingDefenseAt(int playerId, int tileX, int tileY)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId))
                return false;

            ushort buildingId = tileApi.GetTileBuildingId(tileId);
            if (buildingId != 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
            {
                return building->r_PlayerIdOwner == playerId &&
                    building->r_AliveState == AliveState.IsAlive &&
                    IsDefenseBuilding(building->r_BuildingType) &&
                    !IsTowerRuin(building->r_BuildingType);
            }

            return (tileApi.GetTilePropertyFlag(tileId) & TilePropertyFlag.IsWall) != 0 &&
                tileApi.GetTilePlayerOwnerId(tileId) == playerId;
        }

        private List<TrackedDefense> FindDestroyedDefenses(int playerId, short mapper, int[] targetTileIds)
        {
            DefenseFamily family = GetMapperFamily(mapper);
            var result = new List<TrackedDefense>();
            var identities = new HashSet<string>();
            foreach (TrackedDefense record in destroyed.Values)
            {
                if (record.PlayerId != playerId || GetRecordFamily(record) != family)
                    continue;
                for (int index = 0; index < targetTileIds.Length; index++)
                {
                    if (!RecordMatchesTile(record, targetTileIds[index]) || !identities.Add(record.RebuildIdentity))
                        continue;
                    result.Add(record);
                    break;
                }
            }
            return result;
        }

        private void RemoveRestoredRecords(List<TrackedDefense> records)
        {
            for (int index = records.Count - 1; index >= 0; index--)
            {
                TrackedDefense record = records[index];
                if (!IsDefenseRestored(record))
                    continue;
                destroyed.Remove(record.RebuildIdentity);
                rebuildDiagnostics.Remove(record.RebuildIdentity);
                records.RemoveAt(index);
            }
        }

        private static bool HasMatchingRecord(List<TrackedDefense> records, int tileId)
        {
            for (int index = 0; index < records.Count; index++)
            {
                if (RecordMatchesTile(records[index], tileId))
                    return true;
            }
            return false;
        }

        private static bool RecordMatchesTile(TrackedDefense record, int tileId)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsValidTileId(tileId))
                return false;
            if (record.Kind == KindWallTile)
                return record.TileId == tileId;
            UnmanagedVector2<ushort> position = tileApi.GetTileVectorFromId(tileId);
            return record.Contains(position.X, position.Y);
        }

        private TrackedDefense CreateFallbackDestroyedRecord(int playerId, short mapper, int tileId)
        {
            if (!GameTileManagerAPI.Instance.IsValidTileId(tileId))
                return null;
            DefenseFamily family = GetMapperFamily(mapper);
            if (family == DefenseFamily.None)
                return null;
            UnmanagedVector2<ushort> position = GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
            return new TrackedDefense
            {
                PlayerId = playerId,
                Kind = family == DefenseFamily.Wall ? KindWallTile : KindBuilding,
                BuildingType = MapperToExpectedStructure(mapper),
                GlobalId = 0,
                TileId = tileId,
                TileXBegin = position.X,
                TileYBegin = position.Y,
                TileXEnd = position.X,
                TileYEnd = position.Y,
                StartTick = CurrentTick()
            };
        }

        private static TrackedDefense FindYoungestDelayedRecord(List<TrackedDefense> records, int delaySeconds)
        {
            if (delaySeconds <= 0)
                return null;
            int now = CurrentTick();
            int limit = checked(delaySeconds * TicksPerSecond);
            TrackedDefense youngest = null;
            int youngestElapsed = int.MaxValue;
            for (int index = 0; index < records.Count; index++)
            {
                int elapsed = ElapsedTicks(now, records[index].StartTick);
                if (elapsed >= limit || elapsed >= youngestElapsed)
                    continue;
                youngest = records[index];
                youngestElapsed = elapsed;
            }
            return youngest;
        }

        private static bool IsExpectedDefenseAt(int playerId, short mapper, int tileId, bool allowNeedsInit)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsValidTileId(tileId))
                return false;
            if (GetMapperFamily(mapper) == DefenseFamily.Wall)
            {
                return (tileApi.GetTilePropertyFlag(tileId) & TilePropertyFlag.IsWall) != 0 &&
                    tileApi.GetTilePlayerOwnerId(tileId) == playerId;
            }

            int expectedType = MapperToExpectedStructure(mapper);
            ushort buildingId = tileApi.GetTileBuildingId(tileId);
            return expectedType != 0 && buildingId != 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) &&
                building->r_PlayerIdOwner == playerId && building->r_BuildingType == (eStructs)expectedType &&
                IsStandingState(building->r_AliveState, allowNeedsInit);
        }

        private bool IsDefenseRestored(TrackedDefense record)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (record.Kind == KindWallTile)
            {
                if (!tileApi.IsValidTileId(record.TileId))
                    return false;
                return (tileApi.GetTilePropertyFlag(record.TileId) & TilePropertyFlag.IsWall) != 0 &&
                    tileApi.GetTilePlayerOwnerId(record.TileId) == record.PlayerId;
            }

            for (int y = record.TileYBegin; y <= record.TileYEnd; y++)
            {
                for (int x = record.TileXBegin; x <= record.TileXEnd; x++)
                {
                    int tileId = tileApi.GetTileId(x, y);
                    if (!tileApi.IsValidTileId(tileId))
                        continue;
                    ushort buildingId = tileApi.GetTileBuildingId(tileId);
                    if (buildingId != 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) &&
                        building->r_PlayerIdOwner == record.PlayerId &&
                        IsStandingState(building->r_AliveState, allowNeedsInit: true) &&
                        building->r_BuildingType == (eStructs)record.BuildingType)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsStandingState(AliveState state, bool allowNeedsInit) =>
            state == AliveState.IsAlive || (allowNeedsInit && state == AliveState.NeedsInit);

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !mapActive || (damaged.Count == 0 && destroyed.Count == 0))
                return null;

            int now = CurrentTick();
            return MessagePackSerializer.Serialize(new AIDefenseRepairSaveState
            {
                Damaged = ToSaveRecords(damaged.Values, now),
                Destroyed = ToSaveRecords(destroyed.Values, now)
            });
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile)
                return;
            AIDefenseRepairSaveState state = MessagePackSerializer.Deserialize<AIDefenseRepairSaveState>(bytes);
            if (state == null || state.Version != AIDefenseRepairSaveState.CurrentVersion)
                throw new InvalidOperationException("AI defense repair save state has an unsupported version.");
            state.Damaged = state.Damaged ?? Array.Empty<AIDefenseRepairSaveRecord>();
            state.Destroyed = state.Destroyed ?? Array.Empty<AIDefenseRepairSaveRecord>();
            if ((long)state.Damaged.Length + state.Destroyed.Length > MaximumTrackedRecords)
                throw new InvalidOperationException("AI defense repair save state contains too many records.");

            damaged.Clear();
            destroyed.Clear();
            int now = CurrentTick();
            RestoreRecords(state.Damaged, damaged, now, requireStanding: true);
            RestoreRecords(state.Destroyed, destroyed, now, requireStanding: false);
            mapActive = true;
            Shared.DebugLogHelper.LogInfo(log, $"AI defense repair state loaded: damaged={damaged.Count}, destroyed={destroyed.Count}.");
        }

        private static AIDefenseRepairSaveRecord[] ToSaveRecords(IEnumerable<TrackedDefense> source, int now)
        {
            var records = new List<AIDefenseRepairSaveRecord>();
            foreach (TrackedDefense value in source)
                records.Add(value.ToSaveRecord(ElapsedTicks(now, value.StartTick)));
            return records.ToArray();
        }

        private void RestoreRecords(
            AIDefenseRepairSaveRecord[] records,
            Dictionary<string, TrackedDefense> target,
            int now,
            bool requireStanding)
        {
            records = records ?? Array.Empty<AIDefenseRepairSaveRecord>();
            for (int index = 0; index < records.Length; index++)
            {
                TrackedDefense value = TrackedDefense.FromSaveRecord(records[index], now);
                if (!IsValidRestoredRecord(value, requireStanding))
                    continue;
                string key = requireStanding ? value.Identity : value.RebuildIdentity;
                if (!target.TryGetValue(key, out TrackedDefense existing) ||
                    ElapsedTicks(now, value.StartTick) < ElapsedTicks(now, existing.StartTick))
                {
                    target[key] = value;
                }
            }
        }

        private bool IsValidRestoredRecord(TrackedDefense value, bool requireStanding)
        {
            if (!value.IsValid() || !IsAI(value.PlayerId) || !HasValidCoordinates(value))
                return false;
            if (requireStanding)
                return ElapsedTicks(CurrentTick(), value.StartTick) <= MaximumDamageDelayTicks &&
                    IsExactStandingDefense(value);
            if (value.Kind == KindBuilding &&
                (!IsDefenseBuilding((eStructs)value.BuildingType) || IsTowerRuin((eStructs)value.BuildingType)))
            {
                return false;
            }
            return !IsDefenseRestored(value);
        }

        private static bool HasValidCoordinates(TrackedDefense value)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(value.TileXBegin, value.TileYBegin) ||
                !tileApi.IsTileInsideMapBounds(value.TileXEnd, value.TileYEnd))
            {
                return false;
            }
            return tileApi.IsValidTileId(value.TileId);
        }

        private static bool IsExactStandingDefense(TrackedDefense record)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (record.Kind == KindWallTile)
            {
                return tileApi.IsValidTileId(record.TileId) &&
                    (tileApi.GetTilePropertyFlag(record.TileId) & TilePropertyFlag.IsWall) != 0 &&
                    tileApi.GetTilePlayerOwnerId(record.TileId) == record.PlayerId;
            }

            if (record.GlobalId <= 0 || !IsDefenseBuilding((eStructs)record.BuildingType) ||
                IsTowerRuin((eStructs)record.BuildingType))
            {
                return false;
            }

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_GlobalId == record.GlobalId && building.r_PlayerIdOwner == record.PlayerId &&
                    building.r_BuildingType == (eStructs)record.BuildingType &&
                    building.r_AliveState == AliveState.IsAlive &&
                    building.r_TilePositionXBegin == record.TileXBegin &&
                    building.r_TilePositionYBegin == record.TileYBegin &&
                    building.r_TilePositionXEnd == record.TileXEnd &&
                    building.r_TilePositionYEnd == record.TileYEnd)
                {
                    return true;
                }
            }
            return false;
        }

        private void PruneExpiredDamagedRecords()
        {
            int now = CurrentTick();
            if (ElapsedTicks(now, nextDamagedPruneTick) < TicksPerSecond)
                return;
            nextDamagedPruneTick = now;
            if (damaged.Count == 0)
                return;

            var expired = new List<string>();
            foreach (KeyValuePair<string, TrackedDefense> pair in damaged)
            {
                if (ElapsedTicks(now, pair.Value.StartTick) >= MaximumDamageDelayTicks)
                    expired.Add(pair.Key);
            }
            for (int index = 0; index < expired.Count; index++)
                damaged.Remove(expired[index]);
        }

        private bool TrySetTracked(
            Dictionary<string, TrackedDefense> target,
            string key,
            TrackedDefense record)
        {
            if (target.ContainsKey(key) || damaged.Count + destroyed.Count < MaximumTrackedRecords)
            {
                target[key] = record;
                return true;
            }
            if (!capacityFailureLogged)
            {
                capacityFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"AI defense repair tracking reached its safety limit of {MaximumTrackedRecords} records; additional timers are ignored and Vanilla remains available.");
            }
            return false;
        }

        private static bool IsDefenseMapper(short mapperValue)
        {
            eMappers mapper = (eMappers)mapperValue;
            return mapper == eMappers.MAPPER_WALL || mapper == eMappers.MAPPER_CRENAL ||
                mapper == eMappers.MAPPER_CRENAL2 || mapper == eMappers.MAPPER_STAIR ||
                ((int)mapper >= (int)eMappers.MAPPER_STAIR1 && (int)mapper <= (int)eMappers.MAPPER_STAIR6) ||
                mapper == eMappers.MAPPER_GATEHOUSE || mapper == eMappers.MAPPER_GATE_MAIN ||
                mapper == eMappers.MAPPER_GATE_INNER || mapper == eMappers.MAPPER_GATE_WOOD ||
                mapper == eMappers.MAPPER_GATE_POSTERN || mapper == eMappers.MAPPER_DRAWBRIDGE ||
                ((int)mapper >= (int)eMappers.MAPPER_GATE_WOOD1A && (int)mapper <= (int)eMappers.MAPPER_GATE_STONE2B) ||
                IsTowerMapper(mapperValue);
        }

        private static bool IsTowerMapper(short mapperValue)
        {
            eMappers mapper = (eMappers)mapperValue;
            return mapper == eMappers.MAPPER_TOWER ||
                ((int)mapper >= (int)eMappers.MAPPER_TOWER1 && (int)mapper <= (int)eMappers.MAPPER_TOWER5);
        }

        private static bool IsGateMapper(short mapperValue)
        {
            eMappers mapper = (eMappers)mapperValue;
            return mapper == eMappers.MAPPER_GATEHOUSE || mapper == eMappers.MAPPER_GATE_MAIN ||
                mapper == eMappers.MAPPER_GATE_INNER || mapper == eMappers.MAPPER_GATE_WOOD ||
                mapper == eMappers.MAPPER_GATE_POSTERN || mapper == eMappers.MAPPER_DRAWBRIDGE ||
                ((int)mapper >= (int)eMappers.MAPPER_GATE_WOOD1A && (int)mapper <= (int)eMappers.MAPPER_GATE_STONE2B);
        }

        private static DefenseFamily GetMapperFamily(short mapperValue)
        {
            if (IsTowerMapper(mapperValue))
                return DefenseFamily.Tower;
            if (IsGateMapper(mapperValue))
                return DefenseFamily.Gate;
            return IsDefenseMapper(mapperValue) ? DefenseFamily.Wall : DefenseFamily.None;
        }

        private static DefenseFamily GetRecordFamily(TrackedDefense record)
        {
            if (record.Kind == KindWallTile)
                return DefenseFamily.Wall;
            eStructs type = (eStructs)record.BuildingType;
            if (type == eStructs.STRUCT_TOWER ||
                ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5) ||
                IsTowerRuin(type))
            {
                return DefenseFamily.Tower;
            }
            return IsGateBuilding(type) ? DefenseFamily.Gate : DefenseFamily.None;
        }

        private static int MapperToExpectedStructure(short mapperValue)
        {
            switch ((eMappers)mapperValue)
            {
                case eMappers.MAPPER_TOWER1: return (int)eStructs.STRUCT_TOWER1;
                case eMappers.MAPPER_TOWER2: return (int)eStructs.STRUCT_TOWER2;
                case eMappers.MAPPER_TOWER3: return (int)eStructs.STRUCT_TOWER3;
                case eMappers.MAPPER_TOWER4: return (int)eStructs.STRUCT_TOWER4;
                case eMappers.MAPPER_TOWER5: return (int)eStructs.STRUCT_TOWER5;
                case eMappers.MAPPER_TOWER: return (int)eStructs.STRUCT_TOWER;
                case eMappers.MAPPER_GATEHOUSE: return (int)eStructs.STRUCT_GATEHOUSE;
                case eMappers.MAPPER_GATE_MAIN:
                case eMappers.MAPPER_GATE_STONE2A:
                case eMappers.MAPPER_GATE_STONE2B: return (int)eStructs.STRUCT_GATE_MAIN;
                case eMappers.MAPPER_GATE_INNER:
                case eMappers.MAPPER_GATE_STONE1A:
                case eMappers.MAPPER_GATE_STONE1B: return (int)eStructs.STRUCT_GATE_INNER;
                case eMappers.MAPPER_GATE_WOOD:
                case eMappers.MAPPER_GATE_WOOD1A:
                case eMappers.MAPPER_GATE_WOOD1B:
                case eMappers.MAPPER_GATE_WOOD1C:
                case eMappers.MAPPER_GATE_WOOD1D: return (int)eStructs.STRUCT_GATE_WOOD;
                case eMappers.MAPPER_GATE_POSTERN: return (int)eStructs.STRUCT_GATE_POSTERN;
                case eMappers.MAPPER_DRAWBRIDGE: return (int)eStructs.STRUCT_DRAWBRIDGE;
                default: return 0;
            }
        }

        private static bool IsDefenseBuilding(eStructs type)
        {
            return IsGateBuilding(type) ||
                type == eStructs.STRUCT_TOWER ||
                ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5) ||
                IsTowerRuin(type);
        }

        private static bool IsGateBuilding(eStructs type) =>
            type == eStructs.STRUCT_GATEHOUSE || type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD ||
            type == eStructs.STRUCT_GATE_POSTERN || type == eStructs.STRUCT_DRAWBRIDGE;

        private static bool IsTowerRuin(eStructs type) =>
            type == eStructs.STRUCT_TOWER5_DESTROYED ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1_DESTROYED && (int)type <= (int)eStructs.STRUCT_TOWER4_DESTROYED);

        private static bool IsAI(int playerId) =>
            playerId >= 1 && playerId <= 8 && GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);

        private static int CurrentTick() => GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;

        private static int ElapsedTicks(int now, int start) => unchecked((int)(uint)(now - start));

        private static bool IsDelayActive(int startTick, int seconds)
        {
            if (seconds <= 0)
                return false;
            return ElapsedTicks(CurrentTick(), startTick) < checked(seconds * TicksPerSecond);
        }

        private void LogTimer(string action, TrackedDefense record, int now) =>
            Shared.DebugLogHelper.LogInfo(log, $"AI {action}: {record.Describe()}, tick={now}.");

        private void LogRebuildBlock(string reason, TrackedDefense record)
        {
            int now = CurrentTick();
            if (rebuildDiagnostics.TryGetValue(record.RebuildIdentity, out DiagnosticState previous) &&
                previous.Reason == reason && ElapsedTicks(now, previous.Tick) < DiagnosticRepeatTicks)
            {
                return;
            }
            rebuildDiagnostics[record.RebuildIdentity] = new DiagnosticState(reason, now);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI defense rebuild blocked: reason={reason}, {record.Describe()}, tick={now}.");
        }

        private void LogCallbackFailure(string operation, Exception ex)
        {
            if (callbackFailureLogged)
                return;
            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"AI defense repair {operation} failed; subsequent callback errors are suppressed and Vanilla is retained: {ex}");
        }

        private readonly struct PreparedFrame
        {
            public PreparedFrame(int activeLayout, byte status, short mapper, short positionCount, int firstPositionIndex)
            {
                ActiveLayout = activeLayout;
                Status = status;
                Mapper = mapper;
                PositionCount = positionCount;
                FirstPositionIndex = firstPositionIndex;
            }
            public int ActiveLayout { get; }
            public byte Status { get; }
            public short Mapper { get; }
            public short PositionCount { get; }
            public int FirstPositionIndex { get; }
        }

        private readonly struct DiagnosticState
        {
            public DiagnosticState(string reason, int tick)
            {
                Reason = reason;
                Tick = tick;
            }
            public string Reason { get; }
            public int Tick { get; }
        }

        private enum DefenseFamily
        {
            None,
            Wall,
            Gate,
            Tower
        }

        private sealed class PendingDamage
        {
            public int Kind;
            public int EventTileId;
            public int EventTileX;
            public int EventTileY;
            public int PlayerId;
            public int BuildingId;
            public int BuildingType;
            public int GlobalId;
            public int TileId;
            public int TileXBegin;
            public int TileYBegin;
            public int TileXEnd;
            public int TileYEnd;
            public short Health;
            public byte WallDamage;

            public static PendingDamage ForBuilding(BuildingTileTakeDamageEventArgs args, int buildingId, GameBuilding* building) =>
                new PendingDamage
                {
                    Kind = KindBuilding,
                    EventTileId = args.TileId,
                    EventTileX = args.TileX,
                    EventTileY = args.TileY,
                    PlayerId = building->r_PlayerIdOwner,
                    BuildingId = buildingId,
                    BuildingType = (int)building->r_BuildingType,
                    GlobalId = (int)building->r_GlobalId,
                    TileId = (int)building->r_TileIdBegin,
                    TileXBegin = building->r_TilePositionXBegin,
                    TileYBegin = building->r_TilePositionYBegin,
                    TileXEnd = building->r_TilePositionXEnd,
                    TileYEnd = building->r_TilePositionYEnd,
                    Health = building->r_CurrentHealth
                };

            public static PendingDamage ForWall(BuildingTileTakeDamageEventArgs args, int tileId, int playerId, byte wallDamage) =>
                new PendingDamage
                {
                    Kind = KindWallTile,
                    EventTileId = args.TileId,
                    EventTileX = args.TileX,
                    EventTileY = args.TileY,
                    PlayerId = playerId,
                    TileId = tileId,
                    TileXBegin = args.TileX,
                    TileYBegin = args.TileY,
                    TileXEnd = args.TileX,
                    TileYEnd = args.TileY,
                    WallDamage = wallDamage
                };

            public bool Matches(BuildingTileTakeDamageEventArgs args) =>
                EventTileId == args.TileId && EventTileX == args.TileX && EventTileY == args.TileY;

            public TrackedDefense ToTracked(int tick) => new TrackedDefense
            {
                PlayerId = PlayerId,
                Kind = Kind,
                BuildingType = BuildingType,
                GlobalId = GlobalId,
                TileId = TileId,
                TileXBegin = TileXBegin,
                TileYBegin = TileYBegin,
                TileXEnd = TileXEnd,
                TileYEnd = TileYEnd,
                StartTick = tick
            };
        }

        private sealed class TrackedDefense
        {
            public int PlayerId;
            public int Kind;
            public int BuildingType;
            public int GlobalId;
            public int TileId;
            public int TileXBegin;
            public int TileYBegin;
            public int TileXEnd;
            public int TileYEnd;
            public int StartTick;

            public string Identity => Kind == KindWallTile
                ? BuildWallIdentity(PlayerId, TileId)
                : BuildBuildingIdentity(PlayerId, BuildingType, GlobalId, TileXBegin, TileYBegin);
            public string RebuildIdentity => $"{PlayerId}:{Kind}:{BuildingType}:{TileId}:{TileXBegin},{TileYBegin}-{TileXEnd},{TileYEnd}";
            public static string BuildWallIdentity(int playerId, int tileId) => $"W:{playerId}:{tileId}";
            public static string BuildBuildingIdentity(int playerId, int type, int globalId, int x, int y) =>
                $"B:{playerId}:{type}:{globalId}:{x}:{y}";
            public bool Contains(int x, int y) =>
                x >= TileXBegin && x <= TileXEnd && y >= TileYBegin && y <= TileYEnd;
            public bool IsValid() => PlayerId >= 1 && PlayerId <= 8 &&
                (Kind == KindWallTile || Kind == KindBuilding) &&
                TileXBegin >= 0 && TileYBegin >= 0 && TileXEnd >= TileXBegin && TileYEnd >= TileYBegin &&
                ElapsedTicks(CurrentTick(), StartTick) >= 0;
            public string Describe() =>
                $"player={PlayerId}, kind={Kind}, type={BuildingType}, globalId={GlobalId}, tile={TileId}, footprint={TileXBegin},{TileYBegin}-{TileXEnd},{TileYEnd}";

            public AIDefenseRepairSaveRecord ToSaveRecord(int elapsedTicks) => new AIDefenseRepairSaveRecord
            {
                PlayerId = PlayerId,
                Kind = Kind,
                BuildingType = BuildingType,
                GlobalId = GlobalId,
                TileId = TileId,
                TileXBegin = TileXBegin,
                TileYBegin = TileYBegin,
                TileXEnd = TileXEnd,
                TileYEnd = TileYEnd,
                ElapsedTicks = Math.Max(0, elapsedTicks)
            };

            public static TrackedDefense FromSaveRecord(AIDefenseRepairSaveRecord value, int now) => new TrackedDefense
            {
                PlayerId = value.PlayerId,
                Kind = value.Kind,
                BuildingType = value.BuildingType,
                GlobalId = value.GlobalId,
                TileId = value.TileId,
                TileXBegin = value.TileXBegin,
                TileYBegin = value.TileYBegin,
                TileXEnd = value.TileXEnd,
                TileYEnd = value.TileYEnd,
                StartTick = unchecked(now - Math.Max(0, value.ElapsedTicks))
            };
        }
    }
}
