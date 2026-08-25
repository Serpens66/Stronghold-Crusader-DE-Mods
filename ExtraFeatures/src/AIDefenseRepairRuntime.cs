// Feature: Configure human/AI enemy proximity and damage-anchored AIV defense rebuild timing.
//
// Finished castles repeatedly enter ExecuteBuildStep at RVA 0x51790. The placement helper at
// RVA 0x5CD90 supplies the concrete tower/gate target. Map-start and successful spawn events
// establish that an AIV frame has existed before; its first placement remains entirely Vanilla.
// A rebuild delay starts from the last confirmed damage, or once from the first missing-target
// attempt when no damage event was observed. Later retries never restart or extend that timer.
// Tower-ruin cleanup is deliberately handled by BugfixesAndQoL and is independent of this gate.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
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
        // Dispatcher 0x539B0 uses 0x52270 only for AIV entries whose +0x14 field is zero;
        // otherwise it iterates the finished-castle frames through 0x51790. The 2026-08-24
        // finished-castle trace consequently reached 0x51790 repeatedly and never 0x52270.
        private const string ExecuteBuildStepPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
        private const string PlacementPattern =
            "44 89 4C 24 20 44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 44 8B BC 24 B8 00 00 00";
        private const int ExecuteBuildStepRva = 0x51790;
        private const int PlacementRva = 0x5CD90;
        private const int OriginXOffset = 0x204E760;
        private const int OriginYOffset = 0x204E764;
        private const int MaximumFrameCount = 0x922;
        private const int TicksPerSecond = 40;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ExecuteBuildStepDelegate(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PlacementDelegate(
            ulong placementStateAddress, int playerId, int offsetX, int offsetY,
            short mapperValue, int orientation);

        [ThreadStatic] private static BuildStepContext activeContext;
        [ThreadStatic] private static BuildStepContext reusableContext;
        [ThreadStatic] private static PendingWallRepair pendingWallRepair;
        [ThreadStatic] private static DefenseDamageObservation pendingDefenseDamage;
        [ThreadStatic] private static bool hasPendingDefenseDamage;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        // This records only whether a frame has produced a defense before. It is not a timer;
        // rejected retries cannot postpone a later Vanilla call.
        private readonly Dictionary<BuildStepKey, BuildStepHistory> buildStepHistory =
            new Dictionary<BuildStepKey, BuildStepHistory>();
        private readonly Dictionary<BuildStepKey, RebuildDelayState> rebuildDelays =
            new Dictionary<BuildStepKey, RebuildDelayState>();
        // Damage is the earliest reliable per-target event shared by an intact defense and its
        // later missing/ruined AIV entry. A later placement probe may consume this timestamp,
        // but probes and ruin deletion must never rewrite it or extend the configured delay.
        private readonly Dictionary<DefenseTargetKey, int> lastDefenseDamageTicks =
            new Dictionary<DefenseTargetKey, int>();
        private readonly HashSet<DefenseTargetKey> observedDefenseTargets =
            new HashSet<DefenseTargetKey>();
        private readonly HashSet<string> callbackFailuresLogged = new HashSet<string>(StringComparer.Ordinal);
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>> executeBuildStepHook =
            new HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<PlacementDelegate>> placementHook =
            new HookRef<X64ManagedFunctionDetourAOB<PlacementDelegate>>();
        private bool initialized;
        private bool nativeInitialized;
        private bool mapActive;
        private bool mapPrepared;
        private bool gameModeKnown;
        private bool realMultiplayer;
        private bool gameModeFailureLogged;
        private bool invalidFrameWarningLogged;
        private bool disposed;

        private bool IsConfigured =>
            settings.EnableMod &&
            (settings.HumanEnemyProximitySingleplayer >= 0 ||
             settings.HumanEnemyProximityMultiplayer >= 0 ||
             settings.AIEnemyProximitySingleplayer >= 0 ||
             settings.AIEnemyProximityMultiplayer >= 0 ||
             settings.AITowerGateRebuildDelaySeconds >= 0);

        private bool NeedsNativeRebuildHooks =>
            settings.EnableMod &&
            (settings.AIEnemyProximitySingleplayer >= 0 ||
             settings.AIEnemyProximityMultiplayer >= 0 ||
             settings.AITowerGateRebuildDelaySeconds >= 0);

        public AIDefenseRepairRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized)
                return;

            subscriptions.Add(BuildingR3EventHooks.OnBuildingAllowRepairInProximity.Observable.Subscribe(OnRepairProximity));
            subscriptions.Add(AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(OnAIBuildWall));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnDefenseDamage));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => ResetMap()));
            initialized = true;
            Shared.DebugLogHelper.LogDebug(log, "AI defense repair and rebuild runtime initialized.");
        }

        public void ReconcileConfiguration()
        {
            Initialize();
            ApplyHumanImmediateRanges();

            if (!IsConfigured)
            {
                // Discard timer state when returning to Vanilla. Installed native
                // detours remain process-lifetime pass-throughs and cannot retain an old delay.
                ResetState();
                return;
            }
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (nativeInitialized)
                return;
            if (!NeedsNativeRebuildHooks)
                return;

            // The placement helper's process-state origin fields are fixed-layout data, not
            // proven by either function signature. Fail closed on an unaudited DLL.
            if (!referenceHashMatches)
                throw new InvalidOperationException(
                    "AI defense rebuild timing requires the audited placement-origin layout for this CrusaderDE.dll.");

            Shared.NativeResolution executeBuildStep = Shared.NativePatternResolver.ResolveUnique(
                memory, ExecuteBuildStepPattern, ExecuteBuildStepRva, referenceHashMatches,
                "AI ExecuteBuildStep defense path", log);
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
                transaction.AddDetour(ref placementHook,
                    libraryBase + unchecked((ulong)placement.Rva), ObservePlacement);
                transaction.Commit();
                if (!executeBuildStepHook.Success || !placementHook.Success)
                    throw new InvalidOperationException("One or more AI defense rebuild hooks were not installed.");
                nativeInitialized = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"AI defense rebuild hooks installed at RVAs 0x{executeBuildStep.Rva:X} and 0x{placement.Rva:X}.");
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
            RestoreHumanImmediateRanges();
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            ResetMap();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                mapActive = false;
                ResetState();
                CaptureGameMode(args.bMultiplayerSave != 0);
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
                ResetState();
            mapPrepared = false;
            mapActive = true;
        }

        private void ResetMap()
        {
            mapActive = false;
            mapPrepared = false;
            gameModeKnown = false;
            realMultiplayer = false;
            gameModeFailureLogged = false;
            ResetState();
        }

        private void CaptureGameMode(bool multiplayerSave)
        {
            try
            {
                Shared.GameModeSnapshot snapshot = Shared.GameModeHelper.Capture(multiplayerSave);
                realMultiplayer = snapshot.IsRealMultiplayer;
                gameModeKnown = true;
                gameModeFailureLogged = false;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Enemy-proximity runtime captured map mode: {snapshot.ToDiagnosticString()}.");
            }
            catch (Exception ex)
            {
                gameModeKnown = false;
                realMultiplayer = false;
                LogGameModeFailure("map-start capture", ex);
            }
        }

        private void ResetState()
        {
            activeContext = null;
            reusableContext = null;
            pendingWallRepair = default;
            pendingDefenseDamage = default;
            hasPendingDefenseDamage = false;
            invalidFrameWarningLogged = false;
            buildStepHistory.Clear();
            rebuildDelays.Clear();
            lastDefenseDamageTicks.Clear();
            observedDefenseTargets.Clear();
            callbackFailuresLogged.Clear();
        }

        private void OnRepairProximity(BuildingAllowRepairInProximityEventArgs args)
        {
            if (!settings.EnableMod || !mapActive || args.Phase != EventHookPhase.Pre)
                return;

            try
            {
                bool isAi = IsAI(args.PlayerId);
                if (!isAi)
                {
                    ApplyHumanPlacementProximity(args);
                    return;
                }

                // AI wall repair is not a GameBuilding repair. ExecuteBuildStep calls the
                // dedicated wall writer at RVA 0x6CB20, whose validator RVA 0x77CF0 asks
                // this helper with radius 5 immediately before restoring Height/DamageGrid.
                // Bind only that synchronous, coordinate-identical call. This preserves all
                // first placements while avoiding the old tower-spawn-history heuristic.
                bool isWallRepair = pendingWallRepair.Matches(
                    args.PlayerId, args.TileX, args.TileY, args.Proximity);
                if (pendingWallRepair.IsSet)
                    pendingWallRepair = default;
                if (isWallRepair)
                {
                    if (!TryGetConfiguredAIRadius(out int wallRadius))
                        return;

                    args.Proximity = EnemyProximityPolicy.ApplyAIRadius(
                        args.Proximity, wallRadius, isClassifiedRepairOrRebuild: true);
                    return;
                }

                BuildStepContext context = activeContext;
                if (context != null && context.PlayerId == args.PlayerId && context.DelayBlocked)
                {
                    // The measured ExecuteBuildStep path asks this native question after the
                    // placement helper and before spawning. Returning Vanilla's blocked value
                    // leaves the frame scheduler intact, so other frame targets still run.
                    args.ReturnValue = 1;
                    args.SkipOriginalFunction = true;
                    return;
                }

                // 0xEE640 is a general placement helper, not a standing-building repair API.
                // Calls outside the explicitly classified wall/rebuild contexts remain Vanilla.
                if (context == null || context.History == null ||
                    !context.History.EverSpawnedDefense ||
                    !TryGetConfiguredAIRadius(out int rebuildRadius))
                    return;

                // Live AI calls supplied context-specific Vanilla radii 3, 5 and 15. A custom
                // mode value replaces them only after the call was safely classified as rebuild.
                args.Proximity = EnemyProximityPolicy.ApplyAIRadius(
                    args.Proximity, rebuildRadius, isClassifiedRepairOrRebuild: true);
            }
            catch (Exception ex)
            {
                LogFailure("repair proximity", ex);
            }
        }

        private void OnAIBuildWall(AIBuildWallEventArgs args)
        {
            pendingWallRepair = default;
            if (!settings.EnableMod || !mapActive || !TryGetConfiguredAIRadius(out _) ||
                !IsAI(args.PlayerId))
            {
                return;
            }

            try
            {
                GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                int tileId = tileApi.GetTileId(args.TileX, args.TileY);
                if (!tileApi.IsValidTileId(tileId) ||
                    !tileApi.HasTilePropertyFlag(tileId, TilePropertyFlag.IsWall))
                {
                    return;
                }

                // IsWall is the stable distinction we need: a first placement has no wall bit,
                // while Vanilla may route several damaged/breached wall states through different
                // 0x77CF0 branches before they converge on the radius-5 query. Do not duplicate
                // those evolving health/state rules here. If Vanilla rejects earlier, no matching
                // proximity callback occurs and this one-shot context cannot change game state.
                pendingWallRepair = new PendingWallRepair(args.PlayerId, args.TileX, args.TileY);
            }
            catch (Exception ex)
            {
                pendingWallRepair = default;
                LogFailure("AI damaged-wall repair classification", ex);
            }
        }

        private void ApplyHumanPlacementProximity(BuildingAllowRepairInProximityEventArgs args)
        {
            if (args.PlayerId < 1 || args.PlayerId > 8 || !gameModeKnown)
                return;

            int configuredRadius = EnemyProximityPolicy.SelectConfiguredRadius(
                realMultiplayer,
                settings.HumanEnemyProximitySingleplayer,
                settings.HumanEnemyProximityMultiplayer);
            args.Proximity = EnemyProximityPolicy.ApplyHumanPlacementRadius(
                args.Proximity, configuredRadius);
        }

        private bool TryGetConfiguredAIRadius(out int radius)
        {
            radius = EnemyProximityPolicy.VanillaMode;
            if (!gameModeKnown)
                return false;

            radius = EnemyProximityPolicy.SelectConfiguredRadius(
                realMultiplayer,
                settings.AIEnemyProximitySingleplayer,
                settings.AIEnemyProximityMultiplayer);
            return radius != EnemyProximityPolicy.VanillaMode;
        }

        private void ApplyHumanImmediateRanges()
        {
            int singleplayerConfigured = settings.EnableMod
                ? settings.HumanEnemyProximitySingleplayer
                : EnemyProximityPolicy.VanillaMode;
            int multiplayerConfigured = settings.EnableMod
                ? settings.HumanEnemyProximityMultiplayer
                : EnemyProximityPolicy.VanillaMode;
            SetHumanImmediateRanges(
                EnemyProximityPolicy.ResolveHumanImmediateRadius(
                    singleplayerConfigured, EnemyProximityPolicy.VanillaHumanSingleplayerRadius),
                EnemyProximityPolicy.ResolveHumanImmediateRadius(
                    multiplayerConfigured, EnemyProximityPolicy.VanillaHumanMultiplayerRadius));
        }

        private void RestoreHumanImmediateRanges() => SetHumanImmediateRanges(
            EnemyProximityPolicy.VanillaHumanSingleplayerRadius,
            EnemyProximityPolicy.VanillaHumanMultiplayerRadius);

        private void SetHumanImmediateRanges(int singleplayerRadius, int multiplayerRadius)
        {
            GameGlobalsManager globals = GameGlobalsManager.Instance;
            if (globals?.BuildingRepairProximityCheckRange == null ||
                globals.BuildingRepairProximityCheckExRange == null)
            {
                return;
            }

            ushort normal = checked((ushort)singleplayerRadius);
            ushort reduced = checked((ushort)multiplayerRadius);
            if (globals.BuildingRepairProximityCheckRange.GetValue() != normal)
                globals.BuildingRepairProximityCheckRange.SetValue(normal);
            if (globals.BuildingRepairProximityCheckExRange.GetValue() != reduced)
                globals.BuildingRepairProximityCheckExRange.SetValue(reduced);
        }

        private void LogGameModeFailure(string context, Exception ex)
        {
            if (gameModeFailureLogged)
                return;
            gameModeFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Enemy-proximity game-mode detection failed during {context}; human placement and " +
                $"AI proximity overrides remain Vanilla for this map. Snapshot unavailable: {ex}");
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
                LogFailure("ExecuteBuildStep player classification", ex);
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }
            if (!isAi)
            {
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }

            if (frameIndex < 0 || frameIndex >= MaximumFrameCount)
            {
                if (!invalidFrameWarningLogged)
                {
                    invalidFrameWarningLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"AI defense rebuild received invalid frameIndex={frameIndex}; further invalid-frame " +
                        "warnings are suppressed for this map and affected calls remain Vanilla.");
                }
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
            }

            BuildStepHistory history;
            BuildStepKey key;
            int now = 0;
            try
            {
                if (settings.AITowerGateRebuildDelaySeconds > 0)
                {
                    now = SafeCurrentTick();
                    if (now < 0)
                    {
                        return executeBuildStepHook.Value.Hook.Trampoline(
                            aivStateAddress, playerId, frameIndex, restrictedMode, freeOrForced);
                    }
                }
                key = new BuildStepKey(playerId, frameIndex);
                if (!buildStepHistory.TryGetValue(key, out history))
                {
                    history = new BuildStepHistory();
                    buildStepHistory.Add(key, history);
                }

            }
            catch (Exception ex)
            {
                LogFailure("ExecuteBuildStep preparation", ex);
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
                context.Reset(playerId, frameIndex, now, key, history);
                activeContext = context;
            }
            catch (Exception ex)
            {
                LogFailure("ExecuteBuildStep context preparation", ex);
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
                if (context.DefenseSpawned)
                {
                    if (context.DelayBlocked)
                    {
                        Shared.DebugLogHelper.LogError(log,
                            $"AI defense spawned despite an active rebuild-delay block: " +
                            $"player={playerId}, frameIndex={frameIndex}. The timer is cleared because the target now exists.");
                    }
                    history.EverSpawnedDefense = true;
                    rebuildDelays.Remove(key);
                }
            }
            catch (Exception ex)
            {
                LogFailure("ExecuteBuildStep completion", ex);
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
            try
            {
                if (placementStateAddress != 0)
                {
                    tileX = checked(*(int*)(placementStateAddress + OriginXOffset) + offsetX);
                    tileY = checked(*(int*)(placementStateAddress + OriginYOffset) + offsetY);
                }
            }
            catch (Exception ex)
            {
                LogFailure("AIV placement-coordinate resolution", ex);
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
                // Delay preparation fails open. The placement call below still executes exactly
                // once and no incomplete timer may replace Vanilla behavior.
                context.ClearDelayBlock();
                LogFailure("AIV rebuild-delay preparation", ex);
            }

            return CallPlacement(
                placementStateAddress, playerId, offsetX, offsetY, mapperValue, orientation);
        }

        private int CallPlacement(
            ulong placementStateAddress, int playerId, int offsetX, int offsetY,
            short mapperValue, int orientation) =>
            placementHook.Value.Hook.Trampoline(
                placementStateAddress, playerId, offsetX, offsetY, mapperValue, orientation);

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!IsConfigured || args.Phase != EventHookPhase.Post || !IsTrackedDefenseType(args.Building))
                return;

            try
            {
                if (!IsAI(args.PlayerId))
                    return;
                if (TryCreateTargetKey(args.PlayerId, args.TileX, args.TileY, args.Building, out DefenseTargetKey target))
                {
                    observedDefenseTargets.Add(target);
                    // A successful live spawn closes the previous missing period. Without this
                    // removal, an unrelated later disappearance could inherit an old hit whose
                    // configured maximum delay has long since elapsed.
                    if (!IsTowerRuin(args.Building))
                        lastDefenseDamageTicks.Remove(target);
                }
                if (!mapActive)
                    return;
                BuildStepContext context = activeContext;
                if (context != null && context.PlayerId == args.PlayerId && !IsTowerRuin(args.Building))
                    context.MarkDefenseSpawned();
            }
            catch (Exception ex)
            {
                LogFailure("building-spawn tracking", ex);
            }
        }

        private void OnDefenseDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                pendingDefenseDamage = default;
                hasPendingDefenseDamage = false;
                if (!IsConfigured || !mapActive ||
                    settings.AITowerGateRebuildDelaySeconds <= 0 || args.Damage <= 0)
                    return;

                try
                {
                    GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                    int buildingId = tileApi.IsValidTileId(args.TileId)
                        ? tileApi.GetTileBuildingId(args.TileId)
                        : 0;
                    if (buildingId <= 0 ||
                        !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                        !IsAI(building->r_PlayerIdOwner) ||
                        !TryCreateTargetKey(
                            building->r_PlayerIdOwner,
                            building->r_TilePositionXBegin,
                            building->r_TilePositionYBegin,
                            building->r_BuildingType,
                            out DefenseTargetKey target))
                    {
                        return;
                    }

                    pendingDefenseDamage = new DefenseDamageObservation(
                        buildingId,
                        building->r_GlobalId,
                        args.TileId,
                        building->r_CurrentHealth,
                        target);
                    hasPendingDefenseDamage = true;
                }
                catch (Exception ex)
                {
                    LogFailure("tower/gate damage pre-observation", ex);
                }
                return;
            }

            DefenseDamageObservation observation = pendingDefenseDamage;
            bool hadPendingDamage = hasPendingDefenseDamage;
            pendingDefenseDamage = default;
            hasPendingDefenseDamage = false;
            if (!hadPendingDamage || observation.TileId != args.TileId)
                return;

            try
            {
                bool confirmedDamage = true;
                if (GameBuildingManagerAPI.Instance.TryGetBuildingById(observation.BuildingId, out GameBuilding* building) &&
                    building->r_GlobalId == observation.GlobalId)
                {
                    confirmedDamage = building->r_CurrentHealth < observation.HealthBefore ||
                        building->r_AliveState == AliveState.MarkedForDeletion;
                }

                int now = SafeCurrentTick();
                if (confirmedDamage && now >= 0)
                    lastDefenseDamageTicks[observation.Target] = now;
            }
            catch (Exception ex)
            {
                LogFailure("tower/gate damage post-observation", ex);
            }
        }

        private void PrepareRebuildDelay(
            BuildStepContext context,
            short mapperValue,
            int anchorX,
            int anchorY,
            int proximityX,
            int proximityY)
        {
            if (context.History == null ||
                anchorX < 0 || anchorY < 0 || proximityX < 0 || proximityY < 0 ||
                !TryCreateTargetKey(context.PlayerId, anchorX, anchorY, mapperValue, out DefenseTargetKey target))
                return;

            // The live 2026-08-25 trace proved that the placement helper's raw coordinates are
            // the spawn-event anchor: tower spawn (427,119), raw (427,119), origin-adjusted
            // proximity target (428,120). Identity must use the raw anchor, while the native
            // proximity/ruin checks continue to use the validated origin-adjusted position.
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
                context.History.EverSpawnedDefense = true;
            if (!context.History.EverSpawnedDefense)
                return; // The first placement of this AIV frame remains entirely Vanilla.

            if (!settings.EnableMod || settings.AITowerGateRebuildDelaySeconds < 0)
                return;

            int delaySeconds = settings.AITowerGateRebuildDelaySeconds;
            if (delaySeconds == 0)
                return;

            if (!rebuildDelays.TryGetValue(context.Key, out RebuildDelayState state))
            {
                int firstTick = context.Tick;
                if (lastDefenseDamageTicks.TryGetValue(target, out int damageTick) &&
                    unchecked((uint)(context.Tick - damageTick)) <= int.MaxValue)
                {
                    firstTick = damageTick;
                }

                state = new RebuildDelayState(firstTick);
                rebuildDelays.Add(context.Key, state);
            }

            int elapsed = ElapsedTicks(context.Tick, state.FirstDetectedTick);
            long requiredTicks = (long)delaySeconds * TicksPerSecond;
            if (elapsed >= requiredTicks)
            {
                context.MarkDelayReleased();
                return;
            }

            context.MarkDelayBlocked();
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

        private void LogFailure(string callback, Exception ex)
        {
            if (!callbackFailuresLogged.Add(callback ?? string.Empty))
                return;
            Shared.DebugLogHelper.LogError(log,
                $"AI defense {callback} callback failed; further errors from this callback are suppressed and Vanilla remains active: {ex}");
        }

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

        private static bool IsTrackedDefenseType(eStructs type) =>
            type == eStructs.STRUCT_TOWER ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5) ||
            type == eStructs.STRUCT_GATEHOUSE || type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD ||
            type == eStructs.STRUCT_GATE_POSTERN || type == eStructs.STRUCT_DRAWBRIDGE ||
            IsTowerRuin(type);

        private static bool IsTowerRuin(eStructs type) =>
            type == eStructs.STRUCT_TOWER5_DESTROYED ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1_DESTROYED && (int)type <= (int)eStructs.STRUCT_TOWER4_DESTROYED);

        private sealed class BuildStepContext
        {
            internal int PlayerId { get; private set; }
            internal int FrameIndex { get; private set; }
            internal int Tick { get; private set; }
            internal BuildStepKey Key { get; private set; }
            internal BuildStepHistory History { get; private set; }
            internal bool DelayBlocked { get; private set; }
            internal bool DefenseSpawned { get; private set; }

            internal void Reset(
                int playerId, int frameIndex, int tick,
                BuildStepKey key, BuildStepHistory history)
            {
                PlayerId = playerId;
                FrameIndex = frameIndex;
                Tick = tick;
                Key = key;
                History = history;
                DelayBlocked = false;
                DefenseSpawned = false;
            }

            internal void MarkDelayBlocked() => DelayBlocked = true;
            internal void MarkDelayReleased() => DelayBlocked = false;
            internal void ClearDelayBlock() => DelayBlocked = false;
            internal void MarkDefenseSpawned() => DefenseSpawned = true;
        }

        private sealed class BuildStepHistory
        {
            internal bool EverSpawnedDefense;
        }

        private readonly struct RebuildDelayState
        {
            internal RebuildDelayState(int firstDetectedTick)
            {
                FirstDetectedTick = firstDetectedTick;
            }

            internal int FirstDetectedTick { get; }
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
        }

        private readonly struct DefenseDamageObservation
        {
            internal DefenseDamageObservation(
                int buildingId,
                uint globalId,
                int tileId,
                short healthBefore,
                DefenseTargetKey target)
            {
                BuildingId = buildingId;
                GlobalId = globalId;
                TileId = tileId;
                HealthBefore = healthBefore;
                Target = target;
            }

            internal int BuildingId { get; }
            internal uint GlobalId { get; }
            internal int TileId { get; }
            internal short HealthBefore { get; }
            internal DefenseTargetKey Target { get; }
        }

        private readonly struct PendingWallRepair
        {
            internal PendingWallRepair(int playerId, int x, int y)
            {
                PlayerId = playerId;
                X = x;
                Y = y;
                IsSet = true;
            }

            internal int PlayerId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal bool IsSet { get; }

            internal bool Matches(int playerId, int x, int y, int vanillaRadius) =>
                IsSet && PlayerId == playerId && X == x && Y == y && vanillaRadius == 5;
        }

    }
}
