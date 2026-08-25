// Feature: Configure AI repair proximity and diagnose Vanilla's AIV defense rebuild cadence.
// Native ruin audit for CrusaderDE.dll SHA-256
// FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// dispatch table RVA 0x2DEAE0 sends types 79 and 86-89 to the empty updater at
// RVA 0xACE90, so they have no per-building lifetime cleanup timer. One verified
// removal route is destruction processing at RVA 0x7F6FA, which calls BulldozeBuilding
// at RVA 0xC4290. Do not infer that this route caused a runtime removal: the diagnostics
// below correlate damage, mod deletion marks, bulldoze and delete events instead.
// The separate footprint bulldozer at RVA 0x5D3A0 belongs to general BuildStructure
// RVA 0x74DA0 and is not called by the audited AIV placement helper RVA 0x5CD90.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.Detours;
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
        private const int TowerSnapshotIntervalTicks = 10 * TicksPerSecond;

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
        [ThreadStatic] private static RuinDamageObservation pendingRuinDamage;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        // This is diagnostic attempt history, not a rebuild timer. Rejected calls only update
        // LastTick once and can never postpone or otherwise affect a later Vanilla call.
        private readonly Dictionary<BuildStepKey, BuildStepHistory> buildStepHistory =
            new Dictionary<BuildStepKey, BuildStepHistory>();
        private readonly Dictionary<BuildStepKey, RebuildDelayState> rebuildDelays =
            new Dictionary<BuildStepKey, RebuildDelayState>();
        private readonly HashSet<DefenseTargetKey> observedDefenseTargets =
            new HashSet<DefenseTargetKey>();
        private readonly HashSet<string> callbackFailuresLogged = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<uint, StandingDefenseQueryLogState> standingDefenseQueryLogs =
            new Dictionary<uint, StandingDefenseQueryLogState>();
        // Keep standing repairs separate from AIV rebuilds. The native event is shared by both,
        // which made earlier aggregate logs unable to prove the damaged-building case.
        private readonly RepairPlayerDiagnostics[] standingRepairPlayers = new RepairPlayerDiagnostics[9];
        private readonly RepairPlayerDiagnostics[] rebuildRepairPlayers = new RepairPlayerDiagnostics[9];
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
        private bool mapPrepared;
        private bool disposed;
        private int lastTowerSnapshotTick = int.MinValue;

        // Both -1 values are a true Vanilla mode: no subscriptions/detours are installed when
        // starting in that state, and already installed callbacks immediately pass through.
        private bool IsConfigured =>
            settings.EnableMod &&
            (settings.AIRepairEnemyProximity >= 0 || settings.AITowerGateRebuildDelaySeconds >= 0);

        public AIDefenseRepairRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            for (int index = 0; index < standingRepairPlayers.Length; index++)
            {
                standingRepairPlayers[index] = new RepairPlayerDiagnostics();
                rebuildRepairPlayers[index] = new RepairPlayerDiagnostics();
            }
        }

        public void Initialize()
        {
            if (initialized)
                return;
            if (!IsConfigured)
                return;

            subscriptions.Add(BuildingR3EventHooks.OnBuildingAllowRepairInProximity.Observable.Subscribe(OnRepairProximity));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingRepair.Observable.Subscribe(OnBuildingRepair));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnTowerRuinDamage));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => OnTowerRuinRemoval(args.BuildingId, "bulldoze-pre")));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => OnTowerRuinRemoval(args.BuildingId, "delete-pre")));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => ResetMap()));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            initialized = true;
            LogInfo("AI defense repair-radius, per-frame rebuild delay and native AIV branch diagnostics initialized.");
        }

        public void ReconcileConfiguration()
        {
            if (!IsConfigured)
            {
                // Discard diagnostic/timer state when returning to Vanilla. Installed native
                // detours remain process-lifetime pass-throughs and cannot retain an old delay.
                ResetDiagnostics();
                return;
            }

            Initialize();
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (nativeInitialized)
                return;
            if (!IsConfigured)
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
            if (initialized)
                GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            ResetMap();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            if (!IsConfigured)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                mapActive = false;
                ResetDiagnostics();
                mapPrepared = true;
                return;
            }

            if (args.Phase == EventHookPhase.Post)
                BeginMap();
        }

        private void BeginMap()
        {
            // Pre/Post surrounds finished-castle spawning. Keeping those spawn positions is the
            // minimal evidence needed to distinguish an initial placement from a later rebuild.
            if (!mapPrepared)
                ResetDiagnostics();
            mapPrepared = false;
            mapActive = true;
            LogInfo($"AI defense map state started: repairRadius={settings.AIRepairEnemyProximity}, " +
                $"rebuildDelaySeconds={settings.AITowerGateRebuildDelaySeconds}, " +
                $"knownInitialDefenseTargets={observedDefenseTargets.Count}, " +
                "rebuildDelayMode=per-frame-first-detection.");
        }

        private void ResetMap()
        {
            mapActive = false;
            mapPrepared = false;
            ResetDiagnostics();
        }

        private void ResetDiagnostics()
        {
            activeContext = null;
            reusableContext = null;
            pendingRepair = default;
            hasPendingRepair = false;
            pendingRuinDamage = null;
            executeBuildStepConfirmed = false;
            invalidFrameLogged = false;
            lastTowerSnapshotTick = int.MinValue;
            buildStepHistory.Clear();
            rebuildDelays.Clear();
            observedDefenseTargets.Clear();
            standingDefenseQueryLogs.Clear();
            callbackFailuresLogged.Clear();
            Array.Clear(maintenanceLastTicks, 0, maintenanceLastTicks.Length);
            Array.Clear(maintenanceOccurrences, 0, maintenanceOccurrences.Length);
            foreach (RepairPlayerDiagnostics diagnostics in standingRepairPlayers)
                diagnostics.Reset();
            foreach (RepairPlayerDiagnostics diagnostics in rebuildRepairPlayers)
                diagnostics.Reset();
        }

        private void OnRepairProximity(BuildingAllowRepairInProximityEventArgs args)
        {
            if (!IsConfigured || !mapActive)
                return;

            try
            {
                if (!IsAI(args.PlayerId))
                    return;

                if (args.Phase == EventHookPhase.Pre)
                {
                    BuildStepContext context = activeContext;
                    if (context != null && context.PlayerId == args.PlayerId && context.DelayBlocked)
                    {
                        // The measured ExecuteBuildStep path asks this native question after the
                        // placement helper and before spawning. Returning Vanilla's blocked value
                        // leaves the frame scheduler intact, so other frame targets still run.
                        pendingRepair = default;
                        hasPendingRepair = false;
                        context.MarkDelayBlockApplied(args.TileX, args.TileY);
                        args.ReturnValue = 1;
                        args.SkipOriginalFunction = true;
                        return;
                    }

                    // A first AIV placement must retain both of Vanilla's freeOrForced variants.
                    // Only a frame proven to have spawned this defense before receives the shared
                    // mod radius; calls outside an AIV context are standing-building repairs.
                    if (context != null &&
                        (context.Source != "ExecuteBuildStep" || context.History == null ||
                         !context.History.EverSpawnedDefense))
                        return;

                    if (settings.AIRepairEnemyProximity < 0)
                        return;

                    bool isRebuild = context != null;
                    pendingRepair = new RepairObservation(
                        args.PlayerId, args.TileX, args.TileY, args.Proximity,
                        settings.AIRepairEnemyProximity, isRebuild);
                    hasPendingRepair = true;
                    // On the audited DLL, native 0xEE640 returns nonzero when a qualifying
                    // hostile is inside the strict squared-distance check; its repair caller
                    // treats that as denied. Live AI calls supplied Vanilla radii 3, 5 and 15.
                    args.Proximity = settings.AIRepairEnemyProximity;
                    return;
                }

                if (settings.AIRepairEnemyProximity < 0)
                    return;

                RepairObservation observation = pendingRepair;
                bool hadPendingRepair = hasPendingRepair;
                pendingRepair = default;
                hasPendingRepair = false;
                if (args.Phase != EventHookPhase.Post || !hadPendingRepair ||
                    observation.PlayerId != args.PlayerId || observation.X != args.TileX || observation.Y != args.TileY)
                    return;

                int now = CurrentTick();
                RepairPlayerDiagnostics diagnostics = observation.IsRebuild
                    ? rebuildRepairPlayers[args.PlayerId]
                    : standingRepairPlayers[args.PlayerId];
                bool blocked = args.ReturnValue != 0;
                // Record only actual native results. Per-target state transitions avoid repeating
                // an unchanged denial on every Vanilla polling cycle.
                RepairStateTransition transition = diagnostics.Record(observation, blocked, now);
                string source = observation.IsRebuild ? "rebuild" : "standing-repair";
                string targetBuilding = DescribeBuildingAtTile(observation.X, observation.Y);
                if (!observation.IsRebuild)
                    LogDamagedStandingDefenseQuery(observation, blocked, now);
                if (transition != RepairStateTransition.None)
                {
                    LogInfo($"AI repair-proximity target {(transition == RepairStateTransition.Blocked ? "blocked" : "released")}: " +
                        $"source={source}, player={args.PlayerId}, target=({observation.X},{observation.Y}), " +
                        $"vanillaRadius={observation.VanillaRadius}, configuredRadius={observation.ConfiguredRadius}, tick={now}, " +
                        $"targetBuilding={targetBuilding}.");
                }
                if (diagnostics.ShouldWriteSummary(now))
                {
                    LogInfo($"AI repair-proximity summary: source={source}, player={args.PlayerId}, tick={now}, " +
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

        private void OnGameTick(int tick)
        {
            if (!IsConfigured || !mapActive)
                return;

            if (lastTowerSnapshotTick != int.MinValue &&
                ElapsedTicks(tick, lastTowerSnapshotTick) < TowerSnapshotIntervalTicks)
                return;

            // Diagnostic only: polling the native building array avoids inferring a damaged
            // tower from an anonymous proximity coordinate or from Vanilla's supplied radius.
            lastTowerSnapshotTick = tick;
            try
            {
                WriteStandingTowerSnapshots(tick);
            }
            catch (Exception ex)
            {
                LogFailure("standing-tower snapshot", ex);
            }
        }

        private void WriteStandingTowerSnapshots(int tick)
        {
            bool[] aiPlayers = new bool[9];
            StringBuilder[] towersByPlayer = new StringBuilder[9];
            int[] towerCounts = new int[9];
            int[] damagedCounts = new int[9];
            for (int playerId = 1; playerId <= 8; playerId++)
                aiPlayers[playerId] = IsAI(playerId);

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int slot = 0; slot < buildings.Length; slot++)
            {
                ref GameBuilding building = ref buildings[slot];
                int playerId = building.r_PlayerIdOwner;
                if (playerId < 1 || playerId > 8 || !aiPlayers[playerId] ||
                    building.r_AliveState != AliveState.IsAlive || !IsTowerType(building.r_BuildingType))
                    continue;

                towerCounts[playerId]++;
                bool damaged = building.r_CurrentHealth < building.r_MaxHealth;
                if (damaged)
                    damagedCounts[playerId]++;

                StringBuilder details = towersByPlayer[playerId] ??
                    (towersByPlayer[playerId] = new StringBuilder());
                if (details.Length > 0)
                    details.Append(';');
                details.Append("id=").Append(slot + 1)
                    .Append(",globalId=").Append(building.r_GlobalId)
                    .Append(",type=").Append(building.r_BuildingType)
                    .Append(",pos=(").Append(building.r_TilePositionXBegin).Append(',')
                    .Append(building.r_TilePositionYBegin).Append(')')
                    .Append(",health=").Append(building.r_CurrentHealth).Append('/')
                    .Append(building.r_MaxHealth)
                    .Append(",state=").Append(building.r_AliveState)
                    .Append(",damaged=").Append(damaged ? 1 : 0);
            }

            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if (!aiPlayers[playerId] || towerCounts[playerId] == 0)
                    continue;

                int stone = GamePlayerManagerAPI.Instance.GetGoodAmount(
                    playerId, eGoods.STORED_STONE_BLOCKS);
                LogInfo($"AI standing-tower snapshot: player={playerId}, tick={tick}, stone={stone}, " +
                    $"towers={towerCounts[playerId]}, damaged={damagedCounts[playerId]}, " +
                    $"details=[{towersByPlayer[playerId]}].");
            }
        }

        private static string DescribeBuildingAtTile(int tileX, int tileY)
        {
            try
            {
                if (!TryGetBuildingAtTile(tileX, tileY, out int buildingId, out GameBuilding* building))
                    return "none";

                return $"id={buildingId},globalId={building->r_GlobalId},owner={building->r_PlayerIdOwner}," +
                    $"type={building->r_BuildingType},health={building->r_CurrentHealth}/{building->r_MaxHealth}," +
                    $"state={building->r_AliveState},bounds=({building->r_TilePositionXBegin}," +
                    $"{building->r_TilePositionYBegin})-({building->r_TilePositionXEnd}," +
                    $"{building->r_TilePositionYEnd})";
            }
            catch (Exception ex)
            {
                return $"unresolved:{ex.GetType().Name}";
            }
        }

        private void LogDamagedStandingDefenseQuery(RepairObservation observation, bool blocked, int tick)
        {
            if (!TryGetBuildingAtTile(observation.X, observation.Y, out int buildingId, out GameBuilding* building) ||
                building->r_PlayerIdOwner != observation.PlayerId ||
                building->r_AliveState != AliveState.IsAlive ||
                !IsStandingDefenseType(building->r_BuildingType) ||
                building->r_CurrentHealth >= building->r_MaxHealth)
                return;

            bool shouldLog = !standingDefenseQueryLogs.TryGetValue(building->r_GlobalId, out StandingDefenseQueryLogState previous) ||
                previous.Blocked != blocked || previous.Health != building->r_CurrentHealth ||
                ElapsedTicks(tick, previous.Tick) >= TowerSnapshotIntervalTicks;
            if (!shouldLog)
                return;

            standingDefenseQueryLogs[building->r_GlobalId] = new StandingDefenseQueryLogState(
                tick, blocked, building->r_CurrentHealth);
            int stone = GamePlayerManagerAPI.Instance.GetGoodAmount(
                observation.PlayerId, eGoods.STORED_STONE_BLOCKS);
            LogInfo($"AI damaged-standing-defense repair query: player={observation.PlayerId}, tick={tick}, " +
                $"result={(blocked ? "blocked" : "allowed")}, target=({observation.X},{observation.Y}), " +
                $"vanillaRadius={observation.VanillaRadius}, configuredRadius={observation.ConfiguredRadius}, " +
                $"stone={stone}, buildingId={buildingId}, globalId={building->r_GlobalId}, " +
                $"type={building->r_BuildingType}, health={building->r_CurrentHealth}/{building->r_MaxHealth}, " +
                $"bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-" +
                $"({building->r_TilePositionXEnd},{building->r_TilePositionYEnd}).");
        }

        private static bool TryGetBuildingAtTile(
            int tileX, int tileY, out int buildingId, out GameBuilding* building)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            buildingId = tileApi.GetTileBuildingId(tileApi.GetTileId(tileX, tileY));
            building = null;
            return buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out building);
        }

        private void OnBuildingRepair(BuildingRepairEventArgs args)
        {
            // Post proves that the Script Extender actually called Vanilla. Logging Pre here
            // would mislabel a repair that a later subscriber suppresses via SkipOriginalFunction.
            if (!IsConfigured || args.Phase != EventHookPhase.Post || !mapActive || args.BuildingId <= 0)
                return;

            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building) ||
                    building->r_GlobalId != unchecked((uint)args.BuildingGlobalId) ||
                    !IsAI(building->r_PlayerIdOwner) || !IsStandingDefenseType(building->r_BuildingType))
                    return;

                int now = SafeCurrentTick();
                string proximity = standingRepairPlayers[building->r_PlayerIdOwner].DescribeCurrentTickForBounds(
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
            if (!IsConfigured || !mapActive)
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
            if (!IsConfigured || !mapActive)
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
            BuildStepKey key;
            int delta;
            string classification;
            try
            {
                key = new BuildStepKey(playerId, frameIndex);
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
                    history.Occurrences, delta, classification, key, history);
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
                    if (context.DelayBlocked)
                    {
                        Shared.DebugLogHelper.LogError(log,
                            $"AI defense spawned despite an active rebuild-delay block: " +
                            $"player={playerId}, frameIndex={frameIndex}, tick={SafeCurrentTick()}, " +
                            $"delay={context.DescribeDelay()}. The timer is cleared because the target now exists.");
                    }
                    history.EverSpawnedDefense = true;
                    history.ObservedSpawnCount += context.Spawns.Count;
                    if (rebuildDelays.TryGetValue(key, out RebuildDelayState completedDelay))
                    {
                        rebuildDelays.Remove(key);
                        LogInfo($"AI defense rebuild completed: player={playerId}, frameIndex={frameIndex}, " +
                            $"firstDetectedTick={completedDelay.FirstDetectedTick}, completedTick={SafeCurrentTick()}, " +
                            $"delaySeconds={settings.AITowerGateRebuildDelaySeconds}.");
                    }
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
            if (!IsConfigured)
                return CallPlacement(placementStateAddress, playerId, offsetX, offsetY, mapperValue, orientation);

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

            try
            {
                PrepareRebuildDelay(
                    context,
                    mapperValue,
                    offsetX,
                    offsetY,
                    tileX,
                    tileY);
            }
            catch (Exception ex)
            {
                // Delay diagnostics fail open. The placement call below still executes exactly
                // once and no incomplete timer may replace Vanilla behavior.
                context.ClearDelayBlock();
                LogFailure("AIV rebuild-delay preparation", ex);
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
            if (!IsConfigured || args.Phase != EventHookPhase.Post || !IsDefenseType(args.Building))
                return;

            try
            {
                if (!IsAI(args.PlayerId))
                    return;
                if (TryCreateTargetKey(args.PlayerId, args.TileX, args.TileY, args.Building, out DefenseTargetKey target))
                    observedDefenseTargets.Add(target);
                if (!mapActive)
                    return;
                int buildingId = unchecked((int)args.ReturnValue);
                string description = $"type={args.Building},buildingId={buildingId},tile=({args.TileX},{args.TileY}),{DescribeBuilding(buildingId)}";
                BuildStepContext context = activeContext;
                if (context != null && context.PlayerId == args.PlayerId && !IsTowerRuin(args.Building))
                {
                    context.Spawns.Add(description);
                    return;
                }

                // Live defense spawns outside a captured build step are already represented in
                // observedDefenseTargets. Only ruin creation remains useful lifecycle evidence.
                if (IsTowerRuin(args.Building))
                    LogInfo($"AI tower ruin spawned: player={args.PlayerId}, {description}, tick={SafeCurrentTick()}.");
            }
            catch (Exception ex)
            {
                LogFailure("building-spawn diagnostic", ex);
            }
        }

        private void OnTowerRuinRemoval(int buildingId, string source)
        {
            // Installed subscriptions can outlive a live settings change. In full Vanilla mode
            // this callback must neither inspect game state nor produce diagnostic output.
            if (!IsConfigured || !mapActive || buildingId <= 0)
                return;

            try
            {
                GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
                if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    !IsTowerRuin(building->r_BuildingType) ||
                    !IsAI(building->r_PlayerIdOwner))
                {
                    return;
                }

                RuinDamageObservation damage = pendingRuinDamage;
                string damageContext = damage != null && damage.BuildingId == buildingId &&
                    damage.GlobalId == building->r_GlobalId
                    ? $",duringDamage=True,damage={damage.Damage},damageSourcePlayer={damage.SourcePlayerId},healthBefore={damage.HealthBefore}"
                    : ",duringDamage=False";
                LogInfo($"AI tower ruin removal observed: source={source}, player={building->r_PlayerIdOwner}, " +
                    $"type={building->r_BuildingType}, buildingId={buildingId}, globalId={building->r_GlobalId}, " +
                    $"aliveState={building->r_AliveState}, anchor=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin}), " +
                    $"bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-" +
                    $"({building->r_TilePositionXEnd},{building->r_TilePositionYEnd}){damageContext}, tick={SafeCurrentTick()}.");
            }
            catch (Exception ex)
            {
                LogFailure("tower-ruin removal diagnostic", ex);
            }
        }

        private void OnTowerRuinDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                pendingRuinDamage = null;
                if (!IsConfigured || !mapActive || args.Damage <= 0)
                    return;

                try
                {
                    GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                    int buildingId = tileApi.IsValidTileId(args.TileId)
                        ? tileApi.GetTileBuildingId(args.TileId)
                        : 0;
                    if (buildingId <= 0 ||
                        !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                        !IsTowerRuin(building->r_BuildingType) ||
                        !IsAI(building->r_PlayerIdOwner))
                    {
                        return;
                    }

                    pendingRuinDamage = new RuinDamageObservation(
                        buildingId,
                        building->r_GlobalId,
                        building->r_PlayerIdOwner,
                        building->r_BuildingType,
                        args.TileId,
                        args.Damage,
                        args.PlayerIdSource,
                        building->r_CurrentHealth,
                        building->r_MaxHealth);
                }
                catch (Exception ex)
                {
                    LogFailure("tower-ruin damage pre-diagnostic", ex);
                }
                return;
            }

            RuinDamageObservation observation = pendingRuinDamage;
            pendingRuinDamage = null;
            if (observation == null || observation.TileId != args.TileId)
                return;

            try
            {
                string after = "buildingAfter=missing-or-reused";
                if (GameBuildingManagerAPI.Instance.TryGetBuildingById(observation.BuildingId, out GameBuilding* building) &&
                    building->r_GlobalId == observation.GlobalId)
                {
                    after = $"healthAfter={building->r_CurrentHealth},maxHealthAfter={building->r_MaxHealth}," +
                        $"aliveStateAfter={building->r_AliveState},typeAfter={building->r_BuildingType}";
                }

                LogInfo($"AI tower ruin damage processed: player={observation.OwnerId}, type={observation.Type}, " +
                    $"buildingId={observation.BuildingId}, globalId={observation.GlobalId}, tileId={observation.TileId}, " +
                    $"damage={observation.Damage}, damageSourcePlayer={observation.SourcePlayerId}, " +
                    $"healthBefore={observation.HealthBefore}, maxHealthBefore={observation.MaxHealthBefore}, " +
                    $"{after}, tick={SafeCurrentTick()}.");
            }
            catch (Exception ex)
            {
                LogFailure("tower-ruin damage post-diagnostic", ex);
            }
        }

        private static string DescribeBuilding(int buildingId)
        {
            if (buildingId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                return "globalId=unavailable,aliveState=unavailable,bounds=unavailable";
            return $"globalId={building->r_GlobalId},aliveState={building->r_AliveState},bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-({building->r_TilePositionXEnd},{building->r_TilePositionYEnd})";
        }

        private void PrepareRebuildDelay(
            BuildStepContext context,
            short mapperValue,
            int anchorX,
            int anchorY,
            int proximityX,
            int proximityY)
        {
            if (context.Source != "ExecuteBuildStep" || context.History == null ||
                anchorX < 0 || anchorY < 0 || proximityX < 0 || proximityY < 0 ||
                !TryCreateTargetKey(context.PlayerId, anchorX, anchorY, mapperValue, out DefenseTargetKey target))
                return;

            // The live 2026-08-25 trace proved that the placement helper's raw coordinates are
            // the spawn-event anchor: tower spawn (427,119), raw (427,119), origin-adjusted
            // proximity target (428,120). Identity must use the raw anchor, while the native
            // proximity/ruin checks continue to use the validated origin-adjusted position.
            context.SetPlacementTarget(proximityX, proximityY);
            bool observedBefore = observedDefenseTargets.Contains(target);
            if (!observedBefore && (anchorX != proximityX || anchorY != proximityY) &&
                TryCreateTargetKey(
                    context.PlayerId,
                    proximityX,
                    proximityY,
                    mapperValue,
                    out DefenseTargetKey legacyTarget) &&
                observedDefenseTargets.Contains(legacyTarget))
            {
                // Preserve the previously accepted identity for unmeasured mapper/gate variants.
                target = legacyTarget;
                observedBefore = true;
            }

            if (observedBefore)
            {
                context.History.EverSpawnedDefense = true;
                context.MarkKnownRebuild();
            }
            if (!context.History.EverSpawnedDefense)
                return; // The first placement of this AIV frame remains entirely Vanilla.

            if (!settings.EnableMod || settings.AITowerGateRebuildDelaySeconds < 0)
                return;

            int delaySeconds = settings.AITowerGateRebuildDelaySeconds;
            if (delaySeconds == 0)
                return;

            if (!rebuildDelays.TryGetValue(context.Key, out RebuildDelayState state))
            {
                state = new RebuildDelayState(context.Tick, target);
                rebuildDelays.Add(context.Key, state);
                LogInfo($"AI defense rebuild delay started: player={context.PlayerId}, " +
                    $"frameIndex={context.FrameIndex}, target={target}, firstDetectedTick={context.Tick}, " +
                    $"delaySeconds={settings.AITowerGateRebuildDelaySeconds}.");
            }

            int elapsed = ElapsedTicks(context.Tick, state.FirstDetectedTick);
            long requiredTicks = (long)delaySeconds * TicksPerSecond;
            if (elapsed >= requiredTicks)
            {
                context.MarkDelayReleased(target, state.FirstDetectedTick, elapsed, delaySeconds);
                if (!state.ReleaseLogged)
                {
                    state.ReleaseLogged = true;
                    LogInfo($"AI defense rebuild delay released: player={context.PlayerId}, " +
                        $"frameIndex={context.FrameIndex}, target={target}, firstDetectedTick={state.FirstDetectedTick}, " +
                        $"releaseTick={context.Tick}, elapsedTicks={elapsed}, delaySeconds={delaySeconds}.");
                }
                return;
            }

            context.MarkDelayBlocked(target, state.FirstDetectedTick, elapsed, delaySeconds);
        }

        private static bool TryCreateTargetKey(
            int playerId, int tileX, int tileY, short mapperValue, out DefenseTargetKey target)
        {
            eMappers mapper = (eMappers)mapperValue;
            if (mapper == eMappers.MAPPER_TOWER ||
                ((int)mapper >= (int)eMappers.MAPPER_TOWER1 && (int)mapper <= (int)eMappers.MAPPER_TOWER5))
            {
                target = new DefenseTargetKey(playerId, tileX, tileY, DefenseFamily.Tower);
                return true;
            }
            if (mapper == eMappers.MAPPER_GATEHOUSE || mapper == eMappers.MAPPER_GATE_MAIN ||
                mapper == eMappers.MAPPER_GATE_INNER || mapper == eMappers.MAPPER_GATE_WOOD ||
                mapper == eMappers.MAPPER_GATE_POSTERN || mapper == eMappers.MAPPER_DRAWBRIDGE ||
                ((int)mapper >= (int)eMappers.MAPPER_GATE_WOOD1A && (int)mapper <= (int)eMappers.MAPPER_GATE_STONE2B))
            {
                target = new DefenseTargetKey(playerId, tileX, tileY, DefenseFamily.Gate);
                return true;
            }
            target = default;
            return false;
        }

        private static bool TryCreateTargetKey(
            int playerId, int tileX, int tileY, eStructs type, out DefenseTargetKey target)
        {
            if (type == eStructs.STRUCT_TOWER ||
                ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5) ||
                IsTowerRuin(type))
            {
                target = new DefenseTargetKey(playerId, tileX, tileY, DefenseFamily.Tower);
                return true;
            }
            if (type == eStructs.STRUCT_GATEHOUSE || type == eStructs.STRUCT_GATE_MAIN ||
                type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD ||
                type == eStructs.STRUCT_GATE_POSTERN || type == eStructs.STRUCT_DRAWBRIDGE)
            {
                target = new DefenseTargetKey(playerId, tileX, tileY, DefenseFamily.Gate);
                return true;
            }
            target = default;
            return false;
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
                $"targetObservedSpawnTotal={observedSpawnTotal}, delay={context.DescribeDelay()}.");
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

        private static bool IsTowerType(eStructs type) =>
            type == eStructs.STRUCT_TOWER ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5);

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
            internal BuildStepKey Key { get; private set; }
            internal BuildStepHistory History { get; private set; }
            internal bool DelayBlocked { get; private set; }
            internal bool HasPlacementTarget { get; private set; }
            internal int PlacementTargetX { get; private set; }
            internal int PlacementTargetY { get; private set; }
            private bool delayReleased;
            private bool delayBlockApplied;
            private int delayQueryX;
            private int delayQueryY;
            private int delayFirstTick;
            private int delayElapsedTicks;
            private int delaySeconds;
            private DefenseTargetKey delayTarget;
            internal List<PlacementAttempt> Attempts { get; } = new List<PlacementAttempt>(4);
            internal List<string> Spawns { get; } = new List<string>(2);

            internal void Reset(
                string source, int playerId, int frameIndex, int restrictedMode, byte freeOrForced,
                int tick, int occurrence, int deltaTicks, string classification,
                BuildStepKey key = default, BuildStepHistory history = null)
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
                Key = key;
                History = history;
                HasPlacementTarget = false;
                PlacementTargetX = int.MinValue;
                PlacementTargetY = int.MinValue;
                ClearDelayBlock();
                Attempts.Clear();
                Spawns.Clear();
            }

            internal void MarkKnownRebuild()
            {
                if (Classification == "first-observed-attempt" || Classification == "retry-without-observed-spawn")
                    Classification = "rebuild-after-map-observed-defense";
            }

            internal void SetPlacementTarget(int x, int y)
            {
                HasPlacementTarget = true;
                PlacementTargetX = x;
                PlacementTargetY = y;
            }

            internal void MarkDelayBlocked(
                DefenseTargetKey target, int firstTick, int elapsedTicks, int configuredSeconds)
            {
                delayTarget = target;
                delayFirstTick = firstTick;
                delayElapsedTicks = elapsedTicks;
                delaySeconds = configuredSeconds;
                DelayBlocked = true;
                delayReleased = false;
                delayBlockApplied = false;
                delayQueryX = int.MinValue;
                delayQueryY = int.MinValue;
            }

            internal void MarkDelayReleased(
                DefenseTargetKey target, int firstTick, int elapsedTicks, int configuredSeconds)
            {
                delayTarget = target;
                delayFirstTick = firstTick;
                delayElapsedTicks = elapsedTicks;
                delaySeconds = configuredSeconds;
                DelayBlocked = false;
                delayReleased = true;
                delayBlockApplied = false;
            }

            internal void MarkDelayBlockApplied(int queryX, int queryY)
            {
                delayBlockApplied = true;
                delayQueryX = queryX;
                delayQueryY = queryY;
            }

            internal void ClearDelayBlock()
            {
                DelayBlocked = false;
                delayReleased = false;
                delayBlockApplied = false;
                delayQueryX = int.MinValue;
                delayQueryY = int.MinValue;
                delayFirstTick = -1;
                delayElapsedTicks = -1;
                delaySeconds = -1;
                delayTarget = default;
            }

            internal string DescribeDelay()
            {
                if (DelayBlocked)
                {
                    string query = delayBlockApplied ? $"({delayQueryX},{delayQueryY})" : "not-observed";
                    return $"blocked,target={delayTarget},firstTick={delayFirstTick},elapsedTicks={delayElapsedTicks}," +
                        $"seconds={delaySeconds},nativeBlockApplied={delayBlockApplied},query={query}";
                }
                if (delayReleased)
                    return $"released,target={delayTarget},firstTick={delayFirstTick},elapsedTicks={delayElapsedTicks},seconds={delaySeconds}";
                return "vanilla";
            }
        }

        private sealed class BuildStepHistory
        {
            internal int? LastTick;
            internal int Occurrences;
            internal bool EverSpawnedDefense;
            internal int ObservedSpawnCount;
        }

        private sealed class RebuildDelayState
        {
            internal RebuildDelayState(int firstDetectedTick, DefenseTargetKey target)
            { FirstDetectedTick = firstDetectedTick; Target = target; }
            internal int FirstDetectedTick { get; }
            internal DefenseTargetKey Target { get; }
            internal bool ReleaseLogged { get; set; }
        }

        private sealed class RepairPlayerDiagnostics
        {
            private readonly Dictionary<int, int> vanillaRadii = new Dictionary<int, int>();
            private readonly HashSet<long> blockedTargets = new HashSet<long>();
            private readonly List<RepairResultObservation> currentTickQueries =
                new List<RepairResultObservation>(64);
            private int? currentQueryTick;
            private int? lastSummaryTick;
            private int lastBlockedX;
            private int lastBlockedY;
            private bool hasLastBlocked;

            internal int SummaryQueries { get; private set; }
            internal int SummaryBlocked { get; private set; }

            internal RepairStateTransition Record(RepairObservation observation, bool blocked, int now)
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

                long targetKey = unchecked(((long)(uint)observation.X << 32) | (uint)observation.Y);
                bool wasBlocked = blockedTargets.Contains(targetKey);
                if (blocked)
                {
                    if (!wasBlocked)
                    {
                        blockedTargets.Add(targetKey);
                        return RepairStateTransition.Blocked;
                    }
                }
                else if (wasBlocked)
                {
                    blockedTargets.Remove(targetKey);
                    return RepairStateTransition.Released;
                }
                return RepairStateTransition.None;
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
                blockedTargets.Clear();
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

        private enum DefenseFamily : byte
        {
            Tower = 1,
            Gate = 2
        }

        private readonly struct DefenseTargetKey : IEquatable<DefenseTargetKey>
        {
            internal DefenseTargetKey(int playerId, int x, int y, DefenseFamily family)
            { PlayerId = playerId; X = x; Y = y; Family = family; }
            private int PlayerId { get; }
            private int X { get; }
            private int Y { get; }
            private DefenseFamily Family { get; }
            public bool Equals(DefenseTargetKey other) =>
                PlayerId == other.PlayerId && X == other.X && Y == other.Y && Family == other.Family;
            public override bool Equals(object obj) => obj is DefenseTargetKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = (hash * 397) ^ X;
                    hash = (hash * 397) ^ Y;
                    return (hash * 397) ^ (int)Family;
                }
            }
            public override string ToString() => $"{Family}@({X},{Y})";
        }

        private sealed class RuinDamageObservation
        {
            internal RuinDamageObservation(
                int buildingId,
                uint globalId,
                int ownerId,
                eStructs type,
                int tileId,
                int damage,
                int sourcePlayerId,
                short healthBefore,
                ushort maxHealthBefore)
            {
                BuildingId = buildingId;
                GlobalId = globalId;
                OwnerId = ownerId;
                Type = type;
                TileId = tileId;
                Damage = damage;
                SourcePlayerId = sourcePlayerId;
                HealthBefore = healthBefore;
                MaxHealthBefore = maxHealthBefore;
            }

            internal int BuildingId { get; }
            internal uint GlobalId { get; }
            internal int OwnerId { get; }
            internal eStructs Type { get; }
            internal int TileId { get; }
            internal int Damage { get; }
            internal int SourcePlayerId { get; }
            internal short HealthBefore { get; }
            internal ushort MaxHealthBefore { get; }
        }

        private readonly struct RepairObservation
        {
            internal RepairObservation(
                int playerId, int x, int y, int vanillaRadius, int configuredRadius, bool isRebuild)
            {
                PlayerId = playerId;
                X = x;
                Y = y;
                VanillaRadius = vanillaRadius;
                ConfiguredRadius = configuredRadius;
                IsRebuild = isRebuild;
            }
            internal int PlayerId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal int VanillaRadius { get; }
            internal int ConfiguredRadius { get; }
            internal bool IsRebuild { get; }
        }

        private readonly struct StandingDefenseQueryLogState
        {
            internal StandingDefenseQueryLogState(int tick, bool blocked, short health)
            {
                Tick = tick;
                Blocked = blocked;
                Health = health;
            }

            internal int Tick { get; }
            internal bool Blocked { get; }
            internal short Health { get; }
        }

        private enum RepairStateTransition
        {
            None,
            Blocked,
            Released
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
