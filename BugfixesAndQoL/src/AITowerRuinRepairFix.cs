// Feature: Diagnose Vanilla replacement of AI tower ruins created during the running match.
// Native ruin audit for CrusaderDE.dll SHA-256
// FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// dispatch table RVA 0x2DEAE0 sends types 79 and 86-89 to the empty updater at
// RVA 0xACE90, so Vanilla has no per-building timed ruin cleanup. Its destruction
// switch routes every ruin type through RVA 0x7F6FA to deletion after further damage.
// A mod-free 2026-08-25 test also strongly indicated that at least one Vanilla AI can
// replace a visible tower ruin atomically. Keep this runtime observational until logs
// distinguish same-placement-call removal from earlier damage removal conclusively;
// deleting every ruin on a timer would hide the native placement decision.
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
using System.Reflection;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AITowerRuinRepairFix : IDisposable
    {
        // Resolve through an interior sequence because ActiveAIVDetector may already have
        // detoured the function prologue by the time this optional fix is initialized.
        private const string BuildingPlacementValidatorInteriorPattern =
            "48 8D 35 ?? ?? ?? ?? 44 8B 81 28 E7 04 02 45 8B F1 " +
            "4C 63 CA 44 8B D0 44 0F 45 94 24 90 00 00 00";
        private const int BuildingPlacementValidatorInteriorRva = 0x7B078;
        private const int BuildingPlacementValidatorInteriorOffset = 0x18;
        private const int DiagnosticRepeatTicks = 30 * 40;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildingPlacementValidatorDelegate(
            ulong placementStateAddress,
            int tileId,
            int playerId,
            int mapperValue,
            int mode);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<uint, RuntimeTowerRuin> runtimeRuins =
            new Dictionary<uint, RuntimeTowerRuin>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>> validatorHook =
            new HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>>();
        private readonly Dictionary<DiagnosticKey, int> diagnosticTicks = new Dictionary<DiagnosticKey, int>();
        private readonly HashSet<ValidatorKey> confirmedValidators = new HashSet<ValidatorKey>();
        private bool callbackFailureLogged;
        private bool mapActive;
        private bool disposed;

        public AITowerRuinRepairFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializeRuntimeRuinDiagnostics();

            try
            {
                if (TryRegisterWithActiveAivDetector())
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Bugfixes and QoL AI tower-ruin repair subscribed to ActiveAIVDetector's " +
                        "placement-validator hook; no overlapping native detour was installed.");
                    return;
                }
            }
            catch (Exception ex)
            {
                // ActiveAIVDetector may already own the native prologue. Do not risk a second
                // hook if its observer contract changed; event diagnostics remain available.
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "AI tower placement diagnostics could not register with ActiveAIVDetector; " +
                    $"event-based ruin diagnostics remain active: {ex}");
                return;
            }

            try
            {
                Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    BuildingPlacementValidatorInteriorPattern,
                    BuildingPlacementValidatorInteriorRva,
                    referenceHashMatches,
                    "AI tower-rebuild placement validator",
                    log);
                if (resolution.Rva < BuildingPlacementValidatorInteriorOffset)
                    throw new InvalidOperationException("The placement-validator signature cannot derive a module RVA.");
                int validatorRva = checked(resolution.Rva - BuildingPlacementValidatorInteriorOffset);
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref validatorHook,
                    libraryBase + unchecked((ulong)validatorRva),
                    ValidateBuildingPlacement);
                transaction.Commit();
                if (!validatorHook.Success)
                    throw new InvalidOperationException("The AI tower-rebuild placement-validator hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL AI tower-ruin repair hook installed: method={resolution.Method}, " +
                    $"signatureRva=0x{resolution.Rva:X}, functionRva=0x{validatorRva:X}.");
            }
            catch (Exception ex)
            {
                transaction?.Unload();
                transaction?.Dispose();
                transaction = null;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "AI tower placement diagnostics could not install their optional native hook; " +
                    $"event-based ruin diagnostics remain active: {ex}");
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
            runtimeRuins.Clear();
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private void InitializeRuntimeRuinDiagnostics()
        {
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => ObserveRuinRemoval(args.BuildingId, "bulldoze-pre")));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => ObserveRuinRemoval(args.BuildingId, "delete-pre")));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnMapStart));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ResetMap()));
            Shared.DebugLogHelper.LogInfo(
                log,
                "AI runtime tower-ruin diagnostics initialized: newly created AI ruins are observed " +
                "without deletion so Vanilla validator, removal and replacement behavior remains measurable.");
        }

        private void OnMapStart(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                ResetMap();
                return;
            }

            if (args.Phase == EventHookPhase.Post)
                mapActive = true;
        }

        private void ResetMap()
        {
            mapActive = false;
            runtimeRuins.Clear();
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!IsEnabled || !mapActive || args.Phase != EventHookPhase.Post ||
                args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
            {
                return;
            }

            try
            {
                if (IsLiveTower(args.Building))
                {
                    ObserveTowerSpawn(args);
                    return;
                }
                if (!IsTowerRuin(args.Building))
                    return;

                int buildingId = unchecked((int)args.ReturnValue);
                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId) ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building->r_GlobalId == 0 || building->r_PlayerIdOwner != args.PlayerId ||
                    building->r_BuildingType != args.Building ||
                    (building->r_AliveState != AliveState.NeedsInit && building->r_AliveState != AliveState.IsAlive))
                {
                    return;
                }

                int now = SafeCurrentTick();
                if (now < 0)
                    return;
                var tracked = new RuntimeTowerRuin(
                    buildingId,
                    building->r_GlobalId,
                    args.PlayerId,
                    args.Building,
                    args.TileX,
                    args.TileY,
                    now);
                runtimeRuins[tracked.GlobalId] = tracked;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AI runtime tower ruin tracked: player={tracked.PlayerId}, type={tracked.Type}, " +
                    $"buildingId={tracked.BuildingId}, globalId={tracked.GlobalId}, " +
                    $"anchor=({tracked.AnchorX},{tracked.AnchorY}), spawnTick={tracked.SpawnTick}.");
            }
            catch (Exception ex)
            {
                LogCallbackFailure("runtime ruin spawn tracking", ex);
            }
        }

        private void ObserveRuinRemoval(int buildingId, string source)
        {
            if (!IsEnabled || !mapActive || buildingId <= 0)
                return;

            try
            {
                foreach (RuntimeTowerRuin tracked in runtimeRuins.Values)
                {
                    if (tracked.BuildingId != buildingId || tracked.HasRemovalSource(source))
                        continue;
                    if (GameBuildingManagerAPI.Instance.TryGetBuildingById(
                            buildingId, out GameBuilding* current) &&
                        current->r_GlobalId != tracked.GlobalId)
                    {
                        continue;
                    }

                    int removalTick = SafeCurrentTick();
                    tracked.RecordRemoval(source, removalTick);
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"AI runtime tower ruin Vanilla removal observed: source={source}, " +
                        $"player={tracked.PlayerId}, ruinType={tracked.Type}, buildingId={tracked.BuildingId}, " +
                        $"globalId={tracked.GlobalId}, anchor=({tracked.AnchorX},{tracked.AnchorY}), " +
                        $"spawnTick={tracked.SpawnTick}, removalTick={removalTick}.");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogCallbackFailure("runtime ruin removal observation", ex);
            }
        }

        private void ObserveTowerSpawn(BuildingSpawnEventArgs args)
        {
            int buildingId = unchecked((int)args.ReturnValue);
            if (!GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* tower) ||
                tower->r_PlayerIdOwner != args.PlayerId ||
                (tower->r_AliveState != AliveState.NeedsInit && tower->r_AliveState != AliveState.IsAlive))
            {
                return;
            }

            int towerSpawnTick = SafeCurrentTick();
            var replaced = new List<uint>();
            foreach (RuntimeTowerRuin tracked in runtimeRuins.Values)
            {
                if (tracked.PlayerId != args.PlayerId ||
                    tracked.AnchorX < tower->r_TilePositionXBegin || tracked.AnchorX > tower->r_TilePositionXEnd ||
                    tracked.AnchorY < tower->r_TilePositionYBegin || tracked.AnchorY > tower->r_TilePositionYEnd)
                {
                    continue;
                }

                bool ruinStillPresent = TryResolveTrackedRuin(tracked, out GameBuilding* ruin);
                string remainingState = ruinStillPresent
                    ? ruin->r_AliveState.ToString()
                    : "no-longer-resolvable";
                string relationship = ruinStillPresent
                    ? "living tracked runtime ruin still present during tower spawn"
                    : tracked.HasRemovalEvidence
                        ? "tower spawned at footprint after tracked runtime ruin removal"
                        : "tower spawned at footprint after unobserved runtime ruin disappearance";
                string removalToSpawnTicks = tracked.LastRemovalTick >= 0 && towerSpawnTick >= 0
                    ? ElapsedTicks(towerSpawnTick, tracked.LastRemovalTick).ToString()
                    : "unknown";
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AI tower/ruin relationship observed: relationship={relationship}, player={args.PlayerId}, " +
                    $"towerType={args.Building}, towerBuildingId={buildingId}, towerGlobalId={tower->r_GlobalId}, " +
                    $"towerBounds=({tower->r_TilePositionXBegin},{tower->r_TilePositionYBegin})-" +
                    $"({tower->r_TilePositionXEnd},{tower->r_TilePositionYEnd}), ruinType={tracked.Type}, " +
                    $"ruinBuildingId={tracked.BuildingId}, ruinGlobalId={tracked.GlobalId}, " +
                    $"ruinAnchor=({tracked.AnchorX},{tracked.AnchorY}), ruinState={remainingState}, " +
                    $"removalSources={tracked.DescribeRemovalSources()}, " +
                    $"lastRemovalTick={tracked.LastRemovalTick}, removalToSpawnTicks={removalToSpawnTicks}, " +
                    $"spawnTick={towerSpawnTick}.");
                replaced.Add(tracked.GlobalId);
            }
            foreach (uint globalId in replaced)
                runtimeRuins.Remove(globalId);
        }

        private bool TryResolveTrackedRuin(RuntimeTowerRuin tracked, out GameBuilding* building)
        {
            building = null;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(tracked.BuildingId, out GameBuilding* candidate) ||
                candidate->r_GlobalId != tracked.GlobalId ||
                candidate->r_PlayerIdOwner != tracked.PlayerId ||
                candidate->r_BuildingType != tracked.Type ||
                (candidate->r_AliveState != AliveState.NeedsInit &&
                 candidate->r_AliveState != AliveState.IsAlive) ||
                candidate->r_TilePositionXBegin != tracked.AnchorX ||
                candidate->r_TilePositionYBegin != tracked.AnchorY)
            {
                return false;
            }

            building = candidate;
            return true;
        }

        private int ValidateBuildingPlacement(
            ulong placementStateAddress,
            int tileId,
            int playerId,
            int mapperValue,
            int mode)
        {
            int result = validatorHook.Value.Hook.Trampoline(
                placementStateAddress,
                tileId,
                playerId,
                mapperValue,
                mode);

            ObserveBuildingPlacement(
                placementStateAddress,
                tileId,
                playerId,
                mapperValue,
                mode,
                result);
            return result;
        }

        private void ObserveBuildingPlacement(
            ulong placementStateAddress,
            int tileId,
            int playerId,
            int mapperValue,
            int mode,
            int result)
        {
            if (!IsEnabled)
                return;

            try
            {
                // Player zero is used by candidate-fit probes; requiring a real AI slot limits
                // diagnostics to live AI tower-placement attempts.
                if (IsLiveTowerMapper(mapperValue) &&
                    playerId >= 1 && playerId <= 8 &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                {
                    string outcome = result == 0
                        ? InspectAllowedTile(tileId, playerId)
                        : InspectBlockedTile(tileId, playerId, mapperValue);
                    LogDiagnostic(playerId, mapperValue, tileId, result, outcome);
                }
            }
            catch (Exception ex)
            {
                LogCallbackFailure("placement-validator callback", ex);
            }

        }

        private string InspectAllowedTile(int tileId, int playerId)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsValidTileId(tileId))
                return "vanilla-allowed-invalid-tile";
            int buildingId = tileApi.GetTileBuildingId(tileId);
            if (buildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building->r_PlayerIdOwner != playerId || !IsTowerRuin(building->r_BuildingType))
            {
                return "vanilla-allowed";
            }

            bool tracked = runtimeRuins.TryGetValue(building->r_GlobalId, out RuntimeTowerRuin runtime) &&
                runtime.BuildingId == buildingId;
            return tracked
                ? $"vanilla-allowed-over-runtime-ruin:buildingId={buildingId},globalId={building->r_GlobalId},type={building->r_BuildingType},aliveState={building->r_AliveState}"
                : $"vanilla-allowed-over-untracked-ruin:buildingId={buildingId},globalId={building->r_GlobalId},type={building->r_BuildingType},aliveState={building->r_AliveState}";
        }

        private bool TryRegisterWithActiveAivDetector()
        {
            Type pluginType = Type.GetType(
                "ActiveAIVDetector.ActiveAIVDetectorPlugin, ActiveAIVDetector",
                throwOnError: false);
            if (pluginType == null)
                return false;

            MethodInfo register = pluginType.GetMethod(
                "TryRegisterPlacementValidatorObserver",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Action<ulong, int, int, int, int, int>) },
                modifiers: null);
            if (register == null)
            {
                throw new MissingMethodException(
                    pluginType.FullName,
                    "TryRegisterPlacementValidatorObserver");
            }

            var observer = new Action<ulong, int, int, int, int, int>(ObserveBuildingPlacement);
            object registrationResult = register.Invoke(null, new object[] { observer });
            return registrationResult is bool registered && registered;
        }

            // The validator is diagnostic only. A mod-free test proved that Vanilla can replace
            // at least some ruins itself; mutating here would hide why another target is blocked.
        private string InspectBlockedTile(int tileId, int playerId, int mapperValue)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsValidTileId(tileId))
                return "blocked-invalid-tile";
            int buildingId = tileApi.GetTileBuildingId(tileId);
            if (buildingId <= 0)
                return "blocked-no-building-on-tile";

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building))
                return $"blocked-building-lookup-failed:buildingId={buildingId}";
            if (building->r_PlayerIdOwner != playerId)
                return $"blocked-owner-mismatch:buildingId={buildingId},owner={building->r_PlayerIdOwner},type={building->r_BuildingType},aliveState={building->r_AliveState}";
            if (building->r_AliveState != AliveState.IsAlive)
                return $"blocked-alive-state-mismatch:buildingId={buildingId},owner={building->r_PlayerIdOwner},type={building->r_BuildingType},aliveState={building->r_AliveState}";
            if (!IsTowerRuin(building->r_BuildingType))
                return $"blocked-not-tower-ruin:buildingId={buildingId},owner={building->r_PlayerIdOwner},type={building->r_BuildingType},aliveState={building->r_AliveState}";

            eStructs ruinType = building->r_BuildingType;
            uint globalId = building->r_GlobalId;
            bool trackedRuntimeRuin = runtimeRuins.TryGetValue(globalId, out RuntimeTowerRuin tracked) &&
                tracked.BuildingId == buildingId;
            return trackedRuntimeRuin
                ? $"blocked-runtime-ruin-left-for-vanilla:buildingId={buildingId},globalId={globalId},type={ruinType},spawnTick={tracked.SpawnTick}"
                : $"blocked-untracked-ruin-left-unchanged:buildingId={buildingId},globalId={globalId},type={ruinType}";
        }

        private void LogDiagnostic(int playerId, int mapperValue, int tileId, int vanillaResult, string outcome)
        {
            int now = SafeCurrentTick();
            var validatorKey = new ValidatorKey(playerId, mapperValue);
            if (vanillaResult == 0 && outcome.IndexOf("ruin", StringComparison.Ordinal) < 0)
            {
                if (confirmedValidators.Add(validatorKey))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"AI tower placement validator hook confirmed: player={playerId}, mapper={mapperValue}, " +
                        $"sampleTileId={tileId}, vanillaResult={vanillaResult}, tick={now}.");
                }
                return;
            }

            string category = outcome;
            int separator = category.IndexOf(':');
            if (separator >= 0)
                category = category.Substring(0, separator);
            // MarkedForDeletion can occur during Vanilla's atomic footprint replacement and is
            // not a separate placement failure worth repeating.
            if (category == "blocked-alive-state-mismatch" &&
                outcome.IndexOf("MarkedForDeletion", StringComparison.Ordinal) >= 0)
                return;

            // Vanilla checks every footprint tile. Grouping by target mapper and outcome keeps
            // one representative line per attempt instead of up to 36 equivalent tile lines.
            // Keep separate evidence for simultaneously blocking ruins, but continue grouping
            // ordinary footprint failures so large tower validators cannot flood the log.
            string diagnosticIdentity = category.IndexOf("ruin", StringComparison.Ordinal) >= 0
                ? outcome
                : category;
            var key = new DiagnosticKey(playerId, mapperValue, diagnosticIdentity);
            if (diagnosticTicks.TryGetValue(key, out int previous) &&
                ElapsedTicks(now, previous) < DiagnosticRepeatTicks)
            {
                return;
            }

            diagnosticTicks[key] = now;
            if (diagnosticTicks.Count > 4096)
            {
                diagnosticTicks.Clear();
                diagnosticTicks[key] = now;
            }
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI tower placement validator sample: player={playerId}, mapper={mapperValue}, " +
                $"tileId={tileId}, vanillaResult={vanillaResult}, outcome={outcome}, tick={now}.");
        }

        private bool IsEnabled =>
            settings.EnableMod && settings.EnableAiFixes && settings.FixAITowerRepair;

        private void LogCallbackFailure(string operation, Exception ex)
        {
            if (callbackFailureLogged)
                return;
            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"AI tower-ruin repair callback failed during {operation}; further callback errors are " +
                $"suppressed and Vanilla remains active: {ex}");
        }

        private static bool IsLiveTowerMapper(int mapperValue)
        {
            eMappers mapper = (eMappers)mapperValue;
            return mapper == eMappers.MAPPER_TOWER ||
                ((int)mapper >= (int)eMappers.MAPPER_TOWER1 && (int)mapper <= (int)eMappers.MAPPER_TOWER5);
        }

        private static bool IsTowerRuin(eStructs type) =>
            type == eStructs.STRUCT_TOWER5_DESTROYED ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1_DESTROYED && (int)type <= (int)eStructs.STRUCT_TOWER4_DESTROYED);

        private static bool IsLiveTower(eStructs type) =>
            type == eStructs.STRUCT_TOWER ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5);

        private static int SafeCurrentTick()
        {
            try { return GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick; }
            catch { return -1; }
        }

        private static int ElapsedTicks(int now, int previous) =>
            unchecked((int)Math.Min((uint)(now - previous), int.MaxValue));

        private sealed class RuntimeTowerRuin
        {
            internal RuntimeTowerRuin(
                int buildingId,
                uint globalId,
                int playerId,
                eStructs type,
                int anchorX,
                int anchorY,
                int spawnTick)
            {
                BuildingId = buildingId;
                GlobalId = globalId;
                PlayerId = playerId;
                Type = type;
                AnchorX = anchorX;
                AnchorY = anchorY;
                SpawnTick = spawnTick;
            }

            internal int BuildingId { get; }
            internal uint GlobalId { get; }
            internal int PlayerId { get; }
            internal eStructs Type { get; }
            internal int AnchorX { get; }
            internal int AnchorY { get; }
            internal int SpawnTick { get; }
            internal int LastRemovalTick { get; private set; } = -1;
            private readonly HashSet<string> removalSources = new HashSet<string>(StringComparer.Ordinal);

            internal bool HasRemovalSource(string source) => removalSources.Contains(source);
            internal bool HasRemovalEvidence => removalSources.Count != 0;
            internal void RecordRemoval(string source, int tick)
            {
                removalSources.Add(source);
                LastRemovalTick = tick;
            }
            internal string DescribeRemovalSources() =>
                removalSources.Count == 0 ? "none" : string.Join("|", removalSources);
        }

        private readonly struct DiagnosticKey : IEquatable<DiagnosticKey>
        {
            internal DiagnosticKey(int playerId, int mapper, string outcome)
            {
                PlayerId = playerId;
                Mapper = mapper;
                Outcome = outcome ?? string.Empty;
            }

            private int PlayerId { get; }
            private int Mapper { get; }
            private string Outcome { get; }

            public bool Equals(DiagnosticKey other) =>
                PlayerId == other.PlayerId && Mapper == other.Mapper &&
                string.Equals(Outcome, other.Outcome, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is DiagnosticKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = (hash * 397) ^ Mapper;
                    return (hash * 397) ^ Outcome.GetHashCode();
                }
            }
        }

        private readonly struct ValidatorKey : IEquatable<ValidatorKey>
        {
            internal ValidatorKey(int playerId, int mapper)
            { PlayerId = playerId; Mapper = mapper; }
            private int PlayerId { get; }
            private int Mapper { get; }
            public bool Equals(ValidatorKey other) =>
                PlayerId == other.PlayerId && Mapper == other.Mapper;
            public override bool Equals(object obj) => obj is ValidatorKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (PlayerId * 397) ^ Mapper; }
            }
        }
    }
}
