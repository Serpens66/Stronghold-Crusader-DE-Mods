// Feature: Configure AI repair proximity and diagnose Vanilla's AIV defense rebuild cadence.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace ExtraFeatures
{
    internal sealed unsafe class AIDefenseRepairRuntime : IDisposable
    {
        // Dispatcher 0x539B0 uses 0x52270 only for AIV entries whose +0x14 field is zero;
        // otherwise it iterates the finished-castle frames through 0x51790. The 2026-08-24
        // finished-castle trace consequently reached 0x51790 repeatedly and never 0x52270.
        private const string ExecuteBuildStepPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
        private const string MaintenancePattern =
            "44 89 44 24 18 89 54 24 10 48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC C8 00 00 00";
        private const string PlacementPattern =
            "44 89 4C 24 20 44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 44 8B BC 24 B8 00 00 00";
        private const int ExecuteBuildStepRva = 0x51790;
        private const int MaintenanceRva = 0x52270;
        private const int PlacementRva = 0x5CD90;
        private const int OriginXOffset = 0x204E760;
        private const int OriginYOffset = 0x204E764;
        private const int MaximumFrameCount = 0x922;
        private const int TicksPerSecond = 40;
        private const int RepairSummaryIntervalTicks = 30 * TicksPerSecond;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ExecuteBuildStepDelegate(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MaintenanceDelegate(ulong aivStateAddress, int playerId, int mode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PlacementDelegate(
            ulong placementStateAddress, int playerId, int offsetX, int offsetY,
            short mapperValue, int orientation);

        [ThreadStatic] private static BuildStepContext activeContext;
        [ThreadStatic] private static BuildStepContext reusableContext;
        [ThreadStatic] private static RepairObservation pendingRepair;
        [ThreadStatic] private static bool hasPendingRepair;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        // This is diagnostic attempt history, not a rebuild timer. Rejected calls only update
        // LastTick once and can never postpone or otherwise affect a later Vanilla call.
        private readonly Dictionary<BuildStepKey, BuildStepHistory> buildStepHistory =
            new Dictionary<BuildStepKey, BuildStepHistory>();
        private readonly HashSet<string> callbackFailuresLogged = new HashSet<string>(StringComparer.Ordinal);
        private readonly RepairPlayerDiagnostics[] repairPlayers = new RepairPlayerDiagnostics[9];
        private readonly int?[] maintenanceLastTicks = new int?[9];
        private readonly int[] maintenanceOccurrences = new int[9];
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>> executeBuildStepHook =
            new HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<MaintenanceDelegate>> maintenanceHook =
            new HookRef<X64ManagedFunctionDetourAOB<MaintenanceDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<PlacementDelegate>> placementHook =
            new HookRef<X64ManagedFunctionDetourAOB<PlacementDelegate>>();
        private bool initialized;
        private bool nativeInitialized;
        private bool mapActive;
        private bool executeBuildStepConfirmed;
        private bool invalidFrameLogged;
        private bool disposed;

        public AIDefenseRepairRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            for (int index = 0; index < repairPlayers.Length; index++)
                repairPlayers[index] = new RepairPlayerDiagnostics();
        }

        public void Initialize()
        {
            if (initialized)
                return;

            subscriptions.Add(BuildingR3EventHooks.OnBuildingAllowRepairInProximity.Observable.Subscribe(OnRepairProximity));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingRepair.Observable.Subscribe(OnBuildingRepair));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => BeginMap()));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => ResetMap()));
            initialized = true;
            LogInfo("AI defense repair-radius and both native AIV branch diagnostics initialized; rebuild timing remains observational until the new trace is evaluated.");
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (nativeInitialized)
                return;

            // The placement helper's process-state origin fields are fixed-layout data, not
            // proven by either function signature. Keep only this optional diagnostic inactive
            // on an unaudited DLL; the managed repair-radius override remains independent.
            if (!referenceHashMatches)
                throw new InvalidOperationException(
                    "AI ExecuteBuildStep diagnostics require the audited placement-origin layout for this CrusaderDE.dll.");

            Shared.NativeResolution executeBuildStep = Shared.NativePatternResolver.ResolveUnique(
                memory, ExecuteBuildStepPattern, ExecuteBuildStepRva, referenceHashMatches,
                "AI ExecuteBuildStep defense path", log);
            Shared.NativeResolution maintenance = Shared.NativePatternResolver.ResolveUnique(
                memory, MaintenancePattern, MaintenanceRva, referenceHashMatches,
                "AI alternate castle-maintenance path", log);
            Shared.NativeResolution placement = Shared.NativePatternResolver.ResolveUnique(
                memory, PlacementPattern, PlacementRva, referenceHashMatches,
                "AI AIV placement helper", log);
            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            try
            {
                transaction = new HookTransaction(memory, libraryBase, loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(ref executeBuildStepHook,
                    libraryBase + unchecked((ulong)executeBuildStep.Rva), ObserveExecuteBuildStep);
                transaction.AddDetour(ref maintenanceHook,
                    libraryBase + unchecked((ulong)maintenance.Rva), ObserveMaintenance);
                transaction.AddDetour(ref placementHook,
                    libraryBase + unchecked((ulong)placement.Rva), ObservePlacement);
                transaction.Commit();
                if (!executeBuildStepHook.Success || !maintenanceHook.Success || !placementHook.Success)
                    throw new InvalidOperationException("One or more AI defense-path diagnostic hooks were not installed.");
                nativeInitialized = true;
                LogInfo($"AI defense-path diagnostics installed: executeBuildStepRva=0x{executeBuildStep.Rva:X}, " +
                    $"maintenanceRva=0x{maintenance.Rva:X}, placementRva=0x{placement.Rva:X}.");
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
            ResetMap();
        }

        private void BeginMap()
        {
            ResetDiagnostics();
            mapActive = true;
            LogInfo($"AI defense diagnostic map state started: repairRadius={settings.AIRepairEnemyProximity}, rebuildDelaySeconds={settings.AITowerGateRebuildDelaySeconds}, rebuildDelayMode=observational.");
        }

        private void ResetMap()
        {
            mapActive = false;
            ResetDiagnostics();
        }

        private void ResetDiagnostics()
        {
            activeContext = null;
            reusableContext = null;
            pendingRepair = default;
            hasPendingRepair = false;
            executeBuildStepConfirmed = false;
            invalidFrameLogged = false;
            buildStepHistory.Clear();
            callbackFailuresLogged.Clear();
            Array.Clear(maintenanceLastTicks, 0, maintenanceLastTicks.Length);
            Array.Clear(maintenanceOccurrences, 0, maintenanceOccurrences.Length);
            foreach (RepairPlayerDiagnostics diagnostics in repairPlayers)
                diagnostics.Reset();
        }

        private void OnRepairProximity(BuildingAllowRepairInProximityEventArgs args)
        {
            if (!mapActive || !settings.EnableMod || settings.AIRepairEnemyProximity < 0)
                return;

            try
            {
                if (!IsAI(args.PlayerId))
                    return;

                if (args.Phase == EventHookPhase.Pre)
                {
                    pendingRepair = new RepairObservation(
                        args.PlayerId, args.TileX, args.TileY, args.Proximity, settings.AIRepairEnemyProximity);
                    hasPendingRepair = true;
                    // On the audited DLL, native 0xEE640 returns nonzero when a qualifying
                    // hostile is inside the strict squared-distance check; its repair caller
                    // treats that as denied. Live AI calls supplied Vanilla radii 3, 5 and 15.
                    args.Proximity = settings.AIRepairEnemyProximity;
                    return;
                }

                RepairObservation observation = pendingRepair;
                bool hadPendingRepair = hasPendingRepair;
                pendingRepair = default;
                hasPendingRepair = false;
                if (args.Phase != EventHookPhase.Post || !hadPendingRepair ||
                    observation.PlayerId != args.PlayerId || observation.X != args.TileX || observation.Y != args.TileY)
                    return;

                int now = CurrentTick();
                RepairPlayerDiagnostics diagnostics = repairPlayers[args.PlayerId];
                diagnostics.Record(observation, args.ReturnValue != 0, now);
                if (diagnostics.ShouldWriteSummary(now))
                {
                    LogInfo($"AI repair-proximity summary: player={args.PlayerId}, tick={now}, " +
                        $"queries={diagnostics.SummaryQueries}, blocked={diagnostics.SummaryBlocked}, " +
                        $"allowed={diagnostics.SummaryQueries - diagnostics.SummaryBlocked}, " +
                        $"vanillaRadii={diagnostics.DescribeVanillaRadii()}, configuredRadius={observation.ConfiguredRadius}, " +
                        $"lastBlocked={diagnostics.DescribeLastBlocked()}.");
                    diagnostics.ResetSummary(now);
                }
            }
            catch (Exception ex)
            {
                LogFailure("repair proximity", ex);
            }
        }

        private void OnBuildingRepair(BuildingRepairEventArgs args)
        {
            // Post proves that the Script Extender actually called Vanilla. Logging Pre here
            // would mislabel a repair that a later subscriber suppresses via SkipOriginalFunction.
            if (args.Phase != EventHookPhase.Post || !mapActive || args.BuildingId <= 0)
                return;

            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building) ||
                    building->r_GlobalId != unchecked((uint)args.BuildingGlobalId) ||
                    !IsAI(building->r_PlayerIdOwner) || !IsStandingDefenseType(building->r_BuildingType))
                    return;

                int now = SafeCurrentTick();
                string proximity = repairPlayers[building->r_PlayerIdOwner].DescribeCurrentTickForBounds(
                    now,
                    building->r_TilePositionXBegin,
                    building->r_TilePositionYBegin,
                    building->r_TilePositionXEnd,
                    building->r_TilePositionYEnd);
                LogInfo($"AI standing-defense repair executed: player={building->r_PlayerIdOwner}, " +
                    $"type={building->r_BuildingType}, buildingId={args.BuildingId}, globalId={building->r_GlobalId}, " +
                    $"bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-({building->r_TilePositionXEnd},{building->r_TilePositionYEnd}), " +
                    $"woodCost={args.WoodCost}, stoneCost={args.StoneCost}, tick={now}, proximity={proximity}.");
            }
            catch (Exception ex)
            {
                LogFailure("building-repair diagnostic", ex);
            }
        }

        private int ObserveMaintenance(ulong aivStateAddress, int playerId, int mode)
        {
            if (!mapActive)
                return maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);

            bool isAi;
            try
            {
                isAi = IsAI(playerId);
            }
            catch (Exception ex)
            {
                LogFailure("alternate-maintenance player diagnostic", ex);
                return maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);
            }
            if (!isAi)
                return maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);

            int now = SafeCurrentTick();
            int delta = maintenanceLastTicks[playerId].HasValue
                ? ElapsedTicks(now, maintenanceLastTicks[playerId].Value) : -1;
            maintenanceLastTicks[playerId] = now;
            int occurrence = ++maintenanceOccurrences[playerId];

            BuildStepContext previous;
            BuildStepContext context;
            try
            {
                previous = activeContext;
                context = previous == null
                    ? reusableContext ?? new BuildStepContext()
                    : new BuildStepContext();
                if (previous == null)
                    reusableContext = null;
                context.Reset(
                    "Maintenance52270", playerId, -1, mode, 0, now,
                    occurrence, delta, "alternate-maintenance-path");
                activeContext = context;
            }
            catch (Exception ex)
            {
                LogFailure("alternate-maintenance context diagnostic", ex);
                return maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);
            }

            int result;
            try
            {
                result = maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);
            }
            finally
            {
                activeContext = previous;
                if (previous == null)
                    reusableContext = context;
            }

            try
            {
                if (context.Attempts.Count > 0 || context.Spawns.Count > 0)
                    LogDefenseCall(context, result, context.Spawns.Count);
            }
            catch (Exception ex)
            {
                LogFailure("alternate-maintenance post-diagnostic", ex);
            }
            return result;
        }

        private int ObserveExecuteBuildStep(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced)
        {
            if (!mapActive)
            {
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }

            bool isAi;
            try
            {
                isAi = IsAI(playerId);
            }
            catch (Exception ex)
            {
                LogFailure("ExecuteBuildStep player diagnostic", ex);
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }
            if (!isAi)
            {
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }

            int now = SafeCurrentTick();
            if (!executeBuildStepConfirmed)
            {
                executeBuildStepConfirmed = true;
                try
                {
                    LogInfo($"AI ExecuteBuildStep hook confirmed: player={playerId}, frameIndex={frameIndex}, " +
                        $"restrictedMode={restrictedMode}, freeOrForced={freeOrForced}, tick={now}.");
                }
                catch (Exception ex)
                {
                    LogFailure("ExecuteBuildStep confirmation diagnostic", ex);
                }
            }

            if (frameIndex < 0 || frameIndex >= MaximumFrameCount)
            {
                if (!invalidFrameLogged)
                {
                    invalidFrameLogged = true;
                    try
                    {
                        Shared.DebugLogHelper.LogError(log,
                            $"AI ExecuteBuildStep diagnostic received invalid frameIndex={frameIndex}; " +
                            "this call remains Vanilla and detailed capture is disabled for it.");
                    }
                    catch (Exception ex)
                    {
                        LogFailure("ExecuteBuildStep invalid-frame diagnostic", ex);
                    }
                }
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }

            BuildStepHistory history;
            int delta;
            string classification;
            try
            {
                var key = new BuildStepKey(playerId, frameIndex);
                if (!buildStepHistory.TryGetValue(key, out history))
                {
                    history = new BuildStepHistory();
                    buildStepHistory.Add(key, history);
                }

                delta = history.LastTick.HasValue ? ElapsedTicks(now, history.LastTick.Value) : -1;
                history.LastTick = now;
                history.Occurrences++;
                classification = history.EverSpawnedDefense
                    ? "repeat-after-observed-spawn"
                    : history.Occurrences == 1 ? "first-observed-attempt" : "retry-without-observed-spawn";
            }
            catch (Exception ex)
            {
                LogFailure("ExecuteBuildStep pre-diagnostic", ex);
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }

            BuildStepContext previous;
            BuildStepContext context;
            try
            {
                previous = activeContext;
                context = previous == null
                    ? reusableContext ?? new BuildStepContext()
                    : new BuildStepContext();
                if (previous == null)
                    reusableContext = null;
                context.Reset(
                    "ExecuteBuildStep", playerId, frameIndex, restrictedMode, freeOrForced, now,
                    history.Occurrences, delta, classification);
                activeContext = context;
            }
            catch (Exception ex)
            {
                LogFailure("ExecuteBuildStep context diagnostic", ex);
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }
            int result;
            try
            {
                result = executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }
            finally
            {
                activeContext = previous;
                if (previous == null)
                    reusableContext = context;
            }

            try
            {
                if (context.Spawns.Count > 0)
                {
                    history.EverSpawnedDefense = true;
                    history.ObservedSpawnCount += context.Spawns.Count;
                }
                if (context.Attempts.Count > 0 || context.Spawns.Count > 0)
                    LogDefenseCall(context, result, history.ObservedSpawnCount);
            }
            catch (Exception ex)
            {
                LogFailure("ExecuteBuildStep diagnostic", ex);
            }
            return result;
        }

        private int ObservePlacement(
            ulong placementStateAddress, int playerId, int offsetX, int offsetY,
            short mapperValue, int orientation)
        {
            BuildStepContext context = activeContext;
            if (context == null || context.PlayerId != playerId || !IsDefenseMapper(mapperValue))
                return CallPlacement(placementStateAddress, playerId, offsetX, offsetY, mapperValue, orientation);

            int tileX = int.MinValue;
            int tileY = int.MinValue;
            int tileId = -1;
            try
            {
                if (placementStateAddress != 0)
                {
                    tileX = checked(*(int*)(placementStateAddress + OriginXOffset) + offsetX);
                    tileY = checked(*(int*)(placementStateAddress + OriginYOffset) + offsetY);
                    tileId = GameTileManagerAPI.Instance.GetTileId(tileX, tileY);
                    if (!GameTileManagerAPI.Instance.IsValidTileId(tileId))
                        tileId = -1;
                }
            }
            catch (Exception ex)
            {
                LogFailure("AIV placement-coordinate diagnostic", ex);
            }

            int result = CallPlacement(placementStateAddress, playerId, offsetX, offsetY, mapperValue, orientation);
            try
            {
                context.Attempts.Add(new PlacementAttempt(
                    mapperValue, offsetX, offsetY, tileX, tileY, tileId, orientation, result));
            }
            catch (Exception ex)
            {
                // Vanilla already ran exactly once; a diagnostic allocation failure must not
                // turn into a second placement call or escape across the unmanaged boundary.
                LogFailure("AIV placement post-diagnostic", ex);
            }
            return result;
        }

        private int CallPlacement(
            ulong placementStateAddress, int playerId, int offsetX, int offsetY,
            short mapperValue, int orientation) =>
            placementHook.Value.Hook.Trampoline(
                placementStateAddress, playerId, offsetX, offsetY, mapperValue, orientation);

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || !mapActive || !IsDefenseType(args.Building))
                return;

            try
            {
                if (!IsAI(args.PlayerId))
                    return;
                int buildingId = unchecked((int)args.ReturnValue);
                string description = $"type={args.Building},buildingId={buildingId},tile=({args.TileX},{args.TileY}),{DescribeBuilding(buildingId)}";
                BuildStepContext context = activeContext;
                if (context != null && context.PlayerId == args.PlayerId && !IsTowerRuin(args.Building))
                {
                    context.Spawns.Add(description);
                    return;
                }

                // Combat destruction did not emit OnBuildingDelete in the measured build, but a
                // tower-to-ruin transition did emit this spawn event and is therefore retained.
                LogInfo($"AI defense building spawned outside captured build step: player={args.PlayerId}, {description}, tick={SafeCurrentTick()}.");
            }
            catch (Exception ex)
            {
                LogFailure("building-spawn diagnostic", ex);
            }
        }

        private static string DescribeBuilding(int buildingId)
        {
            if (buildingId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                return "globalId=unavailable,aliveState=unavailable,bounds=unavailable";
            return $"globalId={building->r_GlobalId},aliveState={building->r_AliveState},bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-({building->r_TilePositionXEnd},{building->r_TilePositionYEnd})";
        }

        private void LogDefenseCall(BuildStepContext context, int result, int observedSpawnTotal)
        {
            var text = new StringBuilder();
            for (int index = 0; index < context.Attempts.Count; index++)
            {
                if (index != 0)
                    text.Append("; ");
                PlacementAttempt attempt = context.Attempts[index];
                text.Append('#').Append(index + 1)
                    .Append(" mapper=").Append(attempt.Mapper)
                    .Append(" offset=(").Append(attempt.OffsetX).Append(',').Append(attempt.OffsetY).Append(')')
                    .Append(" target=(").Append(attempt.TileX).Append(',').Append(attempt.TileY).Append(')')
                    .Append(" tile=").Append(attempt.TileId)
                    .Append(" orientation=").Append(attempt.Orientation)
                    .Append(" result=").Append(attempt.Result);
            }
            string spawns = context.Spawns.Count == 0 ? "none" : string.Join("; ", context.Spawns);
            LogInfo($"AI defense native call: source={context.Source}, player={context.PlayerId}, frameIndex={context.FrameIndex}, " +
                $"classification={context.Classification}, occurrence={context.Occurrence}, tick={context.Tick}, " +
                $"targetDeltaTicks={context.DeltaTicks}, restrictedMode={context.RestrictedMode}, " +
                $"freeOrForced={context.FreeOrForced}, buildResult={result}, attempts={context.Attempts.Count}, " +
                $"[{text}], spawns={context.Spawns.Count} [{spawns}], " +
                $"targetObservedSpawnTotal={observedSpawnTotal}.");
        }

        private void LogFailure(string callback, Exception ex)
        {
            if (!callbackFailuresLogged.Add(callback ?? string.Empty))
                return;
            Shared.DebugLogHelper.LogError(log,
                $"AI defense {callback} callback failed; further errors from this callback are suppressed and Vanilla remains active: {ex}");
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);

        private static int CurrentTick() => GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
        private static int SafeCurrentTick() { try { return CurrentTick(); } catch { return -1; } }
        private static int ElapsedTicks(int now, int previous) =>
            unchecked((int)Math.Min((uint)(now - previous), int.MaxValue));
        private static bool IsAI(int playerId) =>
            playerId >= 1 && playerId <= 8 && GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);

        private static bool IsDefenseMapper(short value)
        {
            eMappers mapper = (eMappers)value;
            return mapper == eMappers.MAPPER_TOWER ||
                ((int)mapper >= (int)eMappers.MAPPER_TOWER1 && (int)mapper <= (int)eMappers.MAPPER_TOWER5) ||
                mapper == eMappers.MAPPER_GATEHOUSE || mapper == eMappers.MAPPER_GATE_MAIN ||
                mapper == eMappers.MAPPER_GATE_INNER || mapper == eMappers.MAPPER_GATE_WOOD ||
                mapper == eMappers.MAPPER_GATE_POSTERN || mapper == eMappers.MAPPER_DRAWBRIDGE ||
                ((int)mapper >= (int)eMappers.MAPPER_GATE_WOOD1A && (int)mapper <= (int)eMappers.MAPPER_GATE_STONE2B);
        }

        private static bool IsStandingDefenseType(eStructs type) =>
            type == eStructs.STRUCT_WOOD_WALL || type == eStructs.STRUCT_STONE_WALL ||
            type == eStructs.STRUCT_CRENAL_WALL || type == eStructs.STRUCT_STAIRS ||
            type == eStructs.STRUCT_TOWER ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5) ||
            type == eStructs.STRUCT_GATEHOUSE || type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD ||
            type == eStructs.STRUCT_GATE_POSTERN || type == eStructs.STRUCT_DRAWBRIDGE;

        private static bool IsDefenseType(eStructs type) =>
            IsStandingDefenseType(type) || IsTowerRuin(type);

        private static bool IsTowerRuin(eStructs type) =>
            type == eStructs.STRUCT_TOWER5_DESTROYED ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1_DESTROYED && (int)type <= (int)eStructs.STRUCT_TOWER4_DESTROYED);

        private sealed class BuildStepContext
        {
            internal string Source { get; private set; }
            internal int PlayerId { get; private set; }
            internal int FrameIndex { get; private set; }
            internal int RestrictedMode { get; private set; }
            internal byte FreeOrForced { get; private set; }
            internal int Tick { get; private set; }
            internal int Occurrence { get; private set; }
            internal int DeltaTicks { get; private set; }
            internal string Classification { get; private set; }
            internal List<PlacementAttempt> Attempts { get; } = new List<PlacementAttempt>(4);
            internal List<string> Spawns { get; } = new List<string>(2);

            internal void Reset(
                string source, int playerId, int frameIndex, int restrictedMode, byte freeOrForced,
                int tick, int occurrence, int deltaTicks, string classification)
            {
                Source = source;
                PlayerId = playerId;
                FrameIndex = frameIndex;
                RestrictedMode = restrictedMode;
                FreeOrForced = freeOrForced;
                Tick = tick;
                Occurrence = occurrence;
                DeltaTicks = deltaTicks;
                Classification = classification;
                Attempts.Clear();
                Spawns.Clear();
            }
        }

        private sealed class BuildStepHistory
        {
            internal int? LastTick;
            internal int Occurrences;
            internal bool EverSpawnedDefense;
            internal int ObservedSpawnCount;
        }

        private sealed class RepairPlayerDiagnostics
        {
            private readonly Dictionary<int, int> vanillaRadii = new Dictionary<int, int>();
            private readonly List<RepairResultObservation> currentTickQueries =
                new List<RepairResultObservation>(64);
            private int? currentQueryTick;
            private int? lastSummaryTick;
            private int lastBlockedX;
            private int lastBlockedY;
            private bool hasLastBlocked;

            internal int SummaryQueries { get; private set; }
            internal int SummaryBlocked { get; private set; }

            internal void Record(RepairObservation observation, bool blocked, int now)
            {
                SummaryQueries++;
                if (blocked)
                {
                    SummaryBlocked++;
                    lastBlockedX = observation.X;
                    lastBlockedY = observation.Y;
                    hasLastBlocked = true;
                }
                vanillaRadii.TryGetValue(observation.VanillaRadius, out int radiusCount);
                vanillaRadii[observation.VanillaRadius] = radiusCount + 1;

                if (!currentQueryTick.HasValue || currentQueryTick.Value != now)
                {
                    currentQueryTick = now;
                    currentTickQueries.Clear();
                }
                currentTickQueries.Add(new RepairResultObservation(
                    observation.X, observation.Y, observation.VanillaRadius,
                    observation.ConfiguredRadius, blocked));
            }

            internal bool ShouldWriteSummary(int now) =>
                SummaryQueries > 0 && (!lastSummaryTick.HasValue ||
                    ElapsedTicks(now, lastSummaryTick.Value) >= RepairSummaryIntervalTicks);

            internal void ResetSummary(int now)
            {
                lastSummaryTick = now;
                SummaryQueries = 0;
                SummaryBlocked = 0;
                vanillaRadii.Clear();
                hasLastBlocked = false;
            }

            internal string DescribeVanillaRadii()
            {
                if (vanillaRadii.Count == 0)
                    return "none";
                var values = new List<int>(vanillaRadii.Keys);
                values.Sort();
                var result = new StringBuilder();
                foreach (int radius in values)
                {
                    if (result.Length != 0)
                        result.Append('|');
                    result.Append(radius).Append(':').Append(vanillaRadii[radius]);
                }
                return result.ToString();
            }

            internal string DescribeLastBlocked() =>
                hasLastBlocked ? $"({lastBlockedX},{lastBlockedY})" : "none";

            internal string DescribeCurrentTickForBounds(
                int now, int xBegin, int yBegin, int xEnd, int yEnd)
            {
                if (!currentQueryTick.HasValue || currentQueryTick.Value != now)
                    return "no-same-tick-query";
                bool validBounds = xEnd >= xBegin && yEnd >= yBegin &&
                    xEnd - xBegin <= 20 && yEnd - yBegin <= 20;
                int minX = xBegin;
                int maxX = validBounds ? xEnd : xBegin;
                int minY = yBegin;
                int maxY = validBounds ? yEnd : yBegin;
                int matched = 0;
                int blocked = 0;
                var radii = new HashSet<int>();
                int configuredRadius = int.MinValue;
                foreach (RepairResultObservation query in currentTickQueries)
                {
                    if (query.X < minX || query.X > maxX || query.Y < minY || query.Y > maxY)
                        continue;
                    matched++;
                    if (query.Blocked)
                        blocked++;
                    radii.Add(query.VanillaRadius);
                    configuredRadius = query.ConfiguredRadius;
                }
                var sortedRadii = new List<int>(radii);
                sortedRadii.Sort();
                return $"sameTickQueries={currentTickQueries.Count},matchedBounds={matched}," +
                    $"matchedBlocked={blocked},vanillaRadii={string.Join("|", sortedRadii)}," +
                    $"configuredRadius={(configuredRadius == int.MinValue ? -1 : configuredRadius)}";
            }

            internal void Reset()
            {
                vanillaRadii.Clear();
                currentTickQueries.Clear();
                currentQueryTick = null;
                lastSummaryTick = null;
                SummaryQueries = 0;
                SummaryBlocked = 0;
                hasLastBlocked = false;
            }
        }

        private readonly struct BuildStepKey : IEquatable<BuildStepKey>
        {
            internal BuildStepKey(int playerId, int frameIndex)
            { PlayerId = playerId; FrameIndex = frameIndex; }
            private int PlayerId { get; }
            private int FrameIndex { get; }
            public bool Equals(BuildStepKey other) =>
                PlayerId == other.PlayerId && FrameIndex == other.FrameIndex;
            public override bool Equals(object obj) => obj is BuildStepKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (PlayerId * 397) ^ FrameIndex; }
            }
        }

        private readonly struct RepairObservation
        {
            internal RepairObservation(int playerId, int x, int y, int vanillaRadius, int configuredRadius)
            { PlayerId = playerId; X = x; Y = y; VanillaRadius = vanillaRadius; ConfiguredRadius = configuredRadius; }
            internal int PlayerId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal int VanillaRadius { get; }
            internal int ConfiguredRadius { get; }
        }

        private readonly struct RepairResultObservation
        {
            internal RepairResultObservation(int x, int y, int vanillaRadius, int configuredRadius, bool blocked)
            { X = x; Y = y; VanillaRadius = vanillaRadius; ConfiguredRadius = configuredRadius; Blocked = blocked; }
            internal int X { get; }
            internal int Y { get; }
            internal int VanillaRadius { get; }
            internal int ConfiguredRadius { get; }
            internal bool Blocked { get; }
        }

        private readonly struct PlacementAttempt
        {
            internal PlacementAttempt(short mapper, int offsetX, int offsetY, int tileX, int tileY, int tileId, int orientation, int result)
            { Mapper = mapper; OffsetX = offsetX; OffsetY = offsetY; TileX = tileX; TileY = tileY; TileId = tileId; Orientation = orientation; Result = result; }
            internal short Mapper { get; }
            internal int OffsetX { get; }
            internal int OffsetY { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal int TileId { get; }
            internal int Orientation { get; }
            internal int Result { get; }
        }
    }
}
