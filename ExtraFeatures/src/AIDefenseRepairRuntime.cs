// Feature: Configure AI repair proximity and a one-shot tower/gatehouse rebuild delay.
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
        private const int DiagnosticRepeatTicks = 10 * AIDefenseRebuildDelayPolicy.TicksPerSecond;
        private const int MaximumTrackedTargets = 20000;

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
        private readonly Dictionary<TargetKey, TargetState> targets = new Dictionary<TargetKey, TargetState>();
        private readonly Dictionary<TargetKey, TargetState> pendingRestoredTargets = new Dictionary<TargetKey, TargetState>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>> executeBuildStepHook =
            new HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>>();
        private ulong activeLayoutIndexBaseAddress;
        private bool initialized;
        private bool nativeInitialized;
        private bool mapActive;
        private bool callbackFailureLogged;
        private bool capacityFailureLogged;
        private bool disposed;

        public AIDefenseRepairRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized)
                return;

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
                throw new InvalidOperationException("AI defense rebuild save-data registration failed.");
            }

            initialized = true;
            Shared.DebugLogHelper.LogInfo(log, "Extra Features AI defense repair-radius and minimal rebuild-state handling initialized.");
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (nativeInitialized)
                return;

            Shared.NativeResolution executeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory, ExecuteBuildStepPattern, ExecuteBuildStepRva, referenceHashMatches,
                "AI AIV execute-build-step", log);
            Shared.NativeResolution activeLayoutResolution = Shared.NativePatternResolver.ResolveUnique(
                memory, ActiveLayoutIndexReferencePattern, ActiveLayoutIndexReferenceRva, referenceHashMatches,
                "active AIV layout-index table reference", log);

            int leaRva = checked(activeLayoutResolution.Rva + 3);
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, checked(leaRva + 3), checked(leaRva + 7));
            if (targetRva < 0 || targetRva > memory.Length - 9 * PlayerRuntimeStateStride)
                throw new InvalidOperationException("The active AIV layout-index table resolved outside the loaded image.");

            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            activeLayoutIndexBaseAddress = libraryBase + unchecked((ulong)targetRva);
            try
            {
                transaction = new HookTransaction(
                    memory, libraryBase, loggerFactory: null,
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
                    $"Extra Features AI tower/gatehouse rebuild hook installed: executeRva=0x{executeResolution.Rva:X}, activeLayoutTable=0x{activeLayoutIndexBaseAddress:X}.");
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
            targets.Clear();
            pendingRestoredTargets.Clear();
        }

        private void BeginMap()
        {
            mapActive = true;
            capacityFailureLogged = false;
        }

        private void ResetMapState()
        {
            mapActive = false;
            targets.Clear();
            pendingRestoredTargets.Clear();
            capacityFailureLogged = false;
        }

        private void OnAllowRepairInProximity(BuildingAllowRepairInProximityEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || !mapActive || !settings.EnableMod ||
                settings.AIRepairEnemyProximity < 0 || !IsAI(args.PlayerId))
            {
                return;
            }

            try
            {
                if (IsStandingDefenseAt(args.PlayerId, args.TileX, args.TileY))
                    args.Proximity = settings.AIRepairEnemyProximity;
            }
            catch (Exception ex)
            {
                LogCallbackFailure("repair proximity", ex);
            }
        }

        private int ExecuteBuildStep(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced)
        {
            // Keep the completely disabled path outside callback error handling as well as all AIV reads.
            if (!settings.EnableMod ||
                (settings.AITowerGateRebuildDelaySeconds < 0 && settings.AIRepairEnemyProximity < 0))
            {
                return CallOriginal(aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }

            bool originalInvoked = false;
            int originalResult = 0;
            try
            {
                if (!IsAI(playerId) || !TryReadTarget(aivStateAddress, playerId, frameIndex, out TargetKey key))
                {
                    return InvokeOriginal(
                        aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced,
                        ref originalInvoked, ref originalResult);
                }

                PrunePendingForObservedFrame(key);
                bool targetPresent = IsExpectedStructureAt(key, allowNeedsInit: true);
                bool matchingTowerRuinPresent = !targetPresent && IsMatchingTowerRuinAt(key);
                TargetState state = FindOrRestoreTarget(key);

                if (state == null && (targetPresent || matchingTowerRuinPresent))
                {
                    state = AddObservedTarget(key);
                    if (state != null)
                    {
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"AI tower/gatehouse target first observed: {Describe(key)}, evidence={(targetPresent ? "standing" : "tower-ruin")}.");
                    }
                }

                if (state == null)
                {
                    // A target never observed as present is an initial Vanilla placement, regardless of freeOrForced.
                    int initialResult = InvokeOriginal(
                        aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced,
                        ref originalInvoked, ref originalResult);
                    if (IsExpectedStructureAt(key, allowNeedsInit: true) || IsMatchingTowerRuinAt(key))
                    {
                        TargetState observed = AddObservedTarget(key);
                        if (observed != null)
                        {
                            Shared.DebugLogHelper.LogInfo(
                                log,
                                $"AI tower/gatehouse initial placement observed: {Describe(key)}, freeOrForced={freeOrForced}.");
                        }
                    }
                    return initialResult;
                }

                if (targetPresent)
                {
                    if (state.MissingSinceTick.HasValue)
                    {
                        state.MissingSinceTick = null;
                        state.DelayReleaseLogged = false;
                        state.LastBlockReason = null;
                        Shared.DebugLogHelper.LogInfo(log, $"AI tower/gatehouse target restored: {Describe(key)}.");
                    }
                    return InvokeOriginal(
                        aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced,
                        ref originalInvoked, ref originalResult);
                }

                int now = CurrentTick();
                if (!state.MissingSinceTick.HasValue)
                {
                    // Set exactly once per missing episode. Blocked callbacks never modify this timestamp.
                    state.MissingSinceTick = AIDefenseRebuildDelayPolicy.BeginMissing(state.MissingSinceTick, now);
                    state.DelayReleaseLogged = false;
                    state.LastBlockReason = null;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"AI tower/gatehouse absence first observed; one-shot delay started: {Describe(key)}, tick={now}.");
                }

                if (AIDefenseRebuildDelayPolicy.IsBlocked(
                        state.MissingSinceTick.Value,
                        now,
                        settings.AITowerGateRebuildDelaySeconds))
                {
                    LogBlocked(state, "delay", now);
                    return 0;
                }

                if (settings.AIRepairEnemyProximity >= 0 && IsEnemyNear(key))
                {
                    LogBlocked(state, "enemy", now);
                    return 0;
                }

                if (!state.DelayReleaseLogged)
                {
                    state.DelayReleaseLogged = true;
                    state.LastBlockReason = null;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"AI tower/gatehouse one-shot rebuild delay released: {Describe(key)}, elapsedTicks={AIDefenseRebuildDelayPolicy.ElapsedTicks(now, state.MissingSinceTick.Value)}.");
                }

                // Failed attempts do not restart the delay. This missing episode stays released until restoration.
                int result = InvokeOriginal(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced,
                    ref originalInvoked, ref originalResult);
                if (IsExpectedStructureAt(key, allowNeedsInit: true))
                {
                    state.MissingSinceTick = null;
                    state.DelayReleaseLogged = false;
                    state.LastBlockReason = null;
                    Shared.DebugLogHelper.LogInfo(log, $"AI tower/gatehouse rebuild observed: {Describe(key)}.");
                }
                return result;
            }
            catch (Exception ex)
            {
                LogCallbackFailure("execute-build-step", ex);
                return originalInvoked
                    ? originalResult
                    : CallOriginal(aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }
        }

        private int CallOriginal(ulong aivStateAddress, int playerId, int frameIndex, int restrictedMode, byte freeOrForced) =>
            executeBuildStepHook.Value.Hook.Trampoline(
                aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);

        private int InvokeOriginal(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced,
            ref bool originalInvoked,
            ref int originalResult)
        {
            // Mark before entering native code so an exceptional trampoline is never invoked a second time.
            originalInvoked = true;
            originalResult = CallOriginal(aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            return originalResult;
        }

        private bool TryReadTarget(ulong aivStateAddress, int playerId, int frameIndex, out TargetKey key)
        {
            key = default;
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
            short mapper = *(short*)(entry + 2);
            if (!IsTowerMapper(mapper) && !IsGateMapper(mapper))
                return false;

            int targetTileId = *(int*)(entry + 8);
            if (!GameTileManagerAPI.Instance.IsValidTileId(targetTileId))
                return false;

            key = new TargetKey(playerId, activeLayout, frameIndex, mapper, targetTileId);
            return true;
        }

        private TargetState FindOrRestoreTarget(TargetKey key)
        {
            if (targets.TryGetValue(key, out TargetState state))
                return state;
            if (!pendingRestoredTargets.TryGetValue(key, out state))
                return null;

            pendingRestoredTargets.Remove(key);
            targets.Add(key, state);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI tower/gatehouse saved target validated against the active AIV: {Describe(key)}, missing={state.MissingSinceTick.HasValue}.");
            return state;
        }

        private void PrunePendingForObservedFrame(TargetKey current)
        {
            if (pendingRestoredTargets.Count == 0)
                return;

            var invalid = new List<TargetKey>();
            foreach (TargetKey saved in pendingRestoredTargets.Keys)
            {
                if (saved.PlayerId != current.PlayerId)
                    continue;
                if (saved.ActiveLayout != current.ActiveLayout ||
                    (saved.FrameIndex == current.FrameIndex && !saved.Equals(current)))
                {
                    invalid.Add(saved);
                }
            }
            for (int index = 0; index < invalid.Count; index++)
                pendingRestoredTargets.Remove(invalid[index]);
        }

        private TargetState AddObservedTarget(TargetKey key)
        {
            if (targets.TryGetValue(key, out TargetState existing))
                return existing;
            if (targets.Count + pendingRestoredTargets.Count >= MaximumTrackedTargets)
            {
                if (!capacityFailureLogged)
                {
                    capacityFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"AI tower/gatehouse tracking reached its safety limit of {MaximumTrackedTargets} targets; additional targets remain Vanilla.");
                }
                return null;
            }

            var state = new TargetState(key);
            targets.Add(key, state);
            return state;
        }

        private static bool IsExpectedStructureAt(TargetKey key, bool allowNeedsInit)
        {
            int expectedType = MapperToExpectedStructure(key.Mapper);
            if (expectedType == 0)
                return false;

            ushort buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(key.TargetTileId);
            return buildingId != 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) &&
                building->r_PlayerIdOwner == key.PlayerId &&
                building->r_BuildingType == (eStructs)expectedType &&
                (building->r_AliveState == AliveState.IsAlive ||
                 (allowNeedsInit && building->r_AliveState == AliveState.NeedsInit));
        }

        private static bool IsMatchingTowerRuinAt(TargetKey key)
        {
            int expectedRuinType = MapperToExpectedTowerRuin(key.Mapper);
            if (expectedRuinType == 0)
                return false;

            ushort buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(key.TargetTileId);
            return buildingId != 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) &&
                building->r_PlayerIdOwner == key.PlayerId &&
                building->r_AliveState == AliveState.IsAlive &&
                building->r_BuildingType == (eStructs)expectedRuinType;
        }

        private bool IsEnemyNear(TargetKey key)
        {
            UnmanagedVector2<ushort> position = GameTileManagerAPI.Instance.GetTileVectorFromId(key.TargetTileId);
            long result = BulkBuildingDetours.c_game_allow_repair_for_building_proximity_hook_impl(
                IntPtr.Zero, key.PlayerId, position.X, position.Y, settings.AIRepairEnemyProximity, 0);
            return result != 0;
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
                    (IsTowerBuilding(building->r_BuildingType) || IsGateBuilding(building->r_BuildingType));
            }

            return (tileApi.GetTilePropertyFlag(tileId) & TilePropertyFlag.IsWall) != 0 &&
                tileApi.GetTilePlayerOwnerId(tileId) == playerId;
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !mapActive || (targets.Count == 0 && pendingRestoredTargets.Count == 0))
                return null;

            int now = CurrentTick();
            var records = new List<AIDefenseRebuildSaveRecord>(targets.Count + pendingRestoredTargets.Count);
            foreach (TargetState state in targets.Values)
                records.Add(ToSaveRecord(state, now));
            foreach (TargetState state in pendingRestoredTargets.Values)
                records.Add(ToSaveRecord(state, now));

            return MessagePackSerializer.Serialize(new AIDefenseRepairSaveState { Targets = records.ToArray() });
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile)
                return;

            targets.Clear();
            pendingRestoredTargets.Clear();
            AIDefenseRepairSaveState state = MessagePackSerializer.Deserialize<AIDefenseRepairSaveState>(bytes);
            if (state == null || state.Version != AIDefenseRepairSaveState.CurrentVersion)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Discarded an obsolete AI defense repair save state; targets will be observed again from the active AIV.");
                mapActive = true;
                return;
            }

            AIDefenseRebuildSaveRecord[] records = state.Targets ?? Array.Empty<AIDefenseRebuildSaveRecord>();
            if (records.Length > MaximumTrackedTargets)
                throw new InvalidOperationException("AI tower/gatehouse rebuild save state contains too many targets.");

            int now = CurrentTick();
            for (int index = 0; index < records.Length; index++)
            {
                AIDefenseRebuildSaveRecord record = records[index];
                if (!TryRestoreRecord(record, now, out TargetState restored) ||
                    pendingRestoredTargets.ContainsKey(restored.Key))
                {
                    continue;
                }
                pendingRestoredTargets.Add(restored.Key, restored);
            }

            mapActive = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI tower/gatehouse rebuild state loaded pending AIV validation: targets={pendingRestoredTargets.Count}.");
        }

        private static AIDefenseRebuildSaveRecord ToSaveRecord(TargetState state, int now) =>
            new AIDefenseRebuildSaveRecord
            {
                PlayerId = state.Key.PlayerId,
                ActiveLayout = state.Key.ActiveLayout,
                FrameIndex = state.Key.FrameIndex,
                Mapper = state.Key.Mapper,
                TargetTileId = state.Key.TargetTileId,
                MissingElapsedTicks = state.MissingSinceTick.HasValue
                    ? AIDefenseRebuildDelayPolicy.ElapsedTicks(now, state.MissingSinceTick.Value)
                    : -1
            };

        private static bool TryRestoreRecord(AIDefenseRebuildSaveRecord record, int now, out TargetState state)
        {
            state = null;
            if (record == null || record.PlayerId < 1 || record.PlayerId > 8 ||
                !IsAI(record.PlayerId) ||
                record.ActiveLayout < 0 || record.ActiveLayout >= 8 ||
                record.FrameIndex < 0 || record.FrameIndex >= PreparedLayoutFrameCount ||
                (!IsTowerMapper(record.Mapper) && !IsGateMapper(record.Mapper)) ||
                !GameTileManagerAPI.Instance.IsValidTileId(record.TargetTileId) ||
                record.MissingElapsedTicks < -1)
            {
                return false;
            }

            var key = new TargetKey(
                record.PlayerId, record.ActiveLayout, record.FrameIndex, record.Mapper, record.TargetTileId);
            state = new TargetState(key);
            if (record.MissingElapsedTicks >= 0)
                state.MissingSinceTick = unchecked(now - record.MissingElapsedTicks);
            return true;
        }

        private void LogBlocked(TargetState state, string reason, int now)
        {
            bool reasonChanged = !string.Equals(state.LastBlockReason, reason, StringComparison.Ordinal);
            if (!reasonChanged && AIDefenseRebuildDelayPolicy.ElapsedTicks(now, state.LastBlockLogTick) < DiagnosticRepeatTicks)
                return;

            state.LastBlockReason = reason;
            state.LastBlockLogTick = now;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI tower/gatehouse rebuild blocked: reason={reason}, {Describe(state.Key)}, elapsedTicks={AIDefenseRebuildDelayPolicy.ElapsedTicks(now, state.MissingSinceTick.Value)}.");
        }

        private void LogCallbackFailure(string callback, Exception ex)
        {
            if (callbackFailureLogged)
                return;
            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"AI defense {callback} callback failed; further callback errors are suppressed and Vanilla remains active: {ex}");
        }

        private static int CurrentTick() => GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;

        private static bool IsAI(int playerId) =>
            playerId >= 1 && playerId <= 8 && GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);

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

        private static int MapperToExpectedTowerRuin(short mapperValue)
        {
            switch ((eMappers)mapperValue)
            {
                case eMappers.MAPPER_TOWER1: return (int)eStructs.STRUCT_TOWER1_DESTROYED;
                case eMappers.MAPPER_TOWER2: return (int)eStructs.STRUCT_TOWER2_DESTROYED;
                case eMappers.MAPPER_TOWER3: return (int)eStructs.STRUCT_TOWER3_DESTROYED;
                case eMappers.MAPPER_TOWER4: return (int)eStructs.STRUCT_TOWER4_DESTROYED;
                case eMappers.MAPPER_TOWER5: return (int)eStructs.STRUCT_TOWER5_DESTROYED;
                default: return 0;
            }
        }

        private static bool IsTowerBuilding(eStructs type) =>
            type == eStructs.STRUCT_TOWER ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5);

        private static bool IsGateBuilding(eStructs type) =>
            type == eStructs.STRUCT_GATEHOUSE || type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD ||
            type == eStructs.STRUCT_GATE_POSTERN || type == eStructs.STRUCT_DRAWBRIDGE;

        private static string Describe(TargetKey key) =>
            $"player={key.PlayerId}, layout={key.ActiveLayout}, frame={key.FrameIndex}, mapper={key.Mapper}, tile={key.TargetTileId}";

        private readonly struct TargetKey : IEquatable<TargetKey>
        {
            public TargetKey(int playerId, int activeLayout, int frameIndex, short mapper, int targetTileId)
            {
                PlayerId = playerId;
                ActiveLayout = activeLayout;
                FrameIndex = frameIndex;
                Mapper = mapper;
                TargetTileId = targetTileId;
            }

            public int PlayerId { get; }
            public int ActiveLayout { get; }
            public int FrameIndex { get; }
            public short Mapper { get; }
            public int TargetTileId { get; }

            public bool Equals(TargetKey other) =>
                PlayerId == other.PlayerId && ActiveLayout == other.ActiveLayout &&
                FrameIndex == other.FrameIndex && Mapper == other.Mapper &&
                TargetTileId == other.TargetTileId;

            public override bool Equals(object obj) => obj is TargetKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = (hash * 397) ^ ActiveLayout;
                    hash = (hash * 397) ^ FrameIndex;
                    hash = (hash * 397) ^ Mapper;
                    return (hash * 397) ^ TargetTileId;
                }
            }
        }

        private sealed class TargetState
        {
            public TargetState(TargetKey key) => Key = key;

            public TargetKey Key { get; }
            public int? MissingSinceTick { get; set; }
            public int LastBlockLogTick { get; set; }
            public string LastBlockReason { get; set; }
            public bool DelayReleaseLogged { get; set; }
        }
    }
}
