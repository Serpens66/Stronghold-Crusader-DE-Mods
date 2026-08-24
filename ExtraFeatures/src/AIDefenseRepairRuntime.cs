// Feature: Configure AI repair proximity and diagnose Vanilla's ongoing AIV rebuild path.
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
        private const string MaintenancePattern =
            "44 89 44 24 18 89 54 24 10 48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC C8 00 00 00";
        private const string PlacementPattern =
            "44 89 4C 24 20 44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 44 8B BC 24 B8 00 00 00";
        private const int MaintenanceRva = 0x52270;
        private const int PlacementRva = 0x5CD90;
        private const int OriginXOffset = 0x204E760;
        private const int OriginYOffset = 0x204E764;
        private const int TicksPerSecond = 40;
        private const int SummaryIntervalTicks = 5 * TicksPerSecond;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MaintenanceDelegate(ulong aivStateAddress, int playerId, int mode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PlacementDelegate(
            ulong placementStateAddress, int playerId, int offsetX, int offsetY,
            short mapperValue, int orientation);

        [ThreadStatic] private static MaintenanceContext activeContext;
        [ThreadStatic] private static MaintenanceContext reusableContext;
        [ThreadStatic] private static RepairObservation pendingRepair;
        [ThreadStatic] private static bool hasPendingRepair;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly PlayerDiagnostics[] players = new PlayerDiagnostics[9];
        private readonly Dictionary<RepairKey, int> repairLogTicks = new Dictionary<RepairKey, int>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<MaintenanceDelegate>> maintenanceHook =
            new HookRef<X64ManagedFunctionDetourAOB<MaintenanceDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<PlacementDelegate>> placementHook =
            new HookRef<X64ManagedFunctionDetourAOB<PlacementDelegate>>();
        private bool initialized;
        private bool nativeInitialized;
        private bool mapActive;
        private bool callbackFailureLogged;
        private bool disposed;

        public AIDefenseRepairRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            for (int index = 0; index < players.Length; index++)
                players[index] = new PlayerDiagnostics();
        }

        public void Initialize()
        {
            if (initialized)
                return;

            subscriptions.Add(BuildingR3EventHooks.OnBuildingAllowRepairInProximity.Observable.Subscribe(OnRepairProximity));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(OnBuildingDelete));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => BeginMap()));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => ResetMap()));
            initialized = true;
            LogInfo("AI defense repair-radius and ongoing AIV rebuild diagnostics initialized; rebuild timing remains observational until the diagnostic test is evaluated.");
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (nativeInitialized)
                return;

            Shared.NativeResolution maintenance = Shared.NativePatternResolver.ResolveUnique(
                memory, MaintenancePattern, MaintenanceRva, referenceHashMatches,
                "AI ongoing castle-maintenance path", log);
            Shared.NativeResolution placement = Shared.NativePatternResolver.ResolveUnique(
                memory, PlacementPattern, PlacementRva, referenceHashMatches,
                "AI AIV placement helper", log);
            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            try
            {
                transaction = new HookTransaction(memory, libraryBase, loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(ref maintenanceHook,
                    libraryBase + unchecked((ulong)maintenance.Rva), ObserveMaintenance);
                transaction.AddDetour(ref placementHook,
                    libraryBase + unchecked((ulong)placement.Rva), ObservePlacement);
                transaction.Commit();
                if (!maintenanceHook.Success || !placementHook.Success)
                    throw new InvalidOperationException("One or more AI castle-maintenance diagnostic hooks were not installed.");
                nativeInitialized = true;
                LogInfo($"Ongoing AI castle-maintenance diagnostics installed: maintenanceRva=0x{maintenance.Rva:X}, placementRva=0x{placement.Rva:X}.");
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
            repairLogTicks.Clear();
            foreach (PlayerDiagnostics diagnostics in players)
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
                    // This hook is repair-specific already; a second tile classification rejected valid wall calls.
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
                var key = new RepairKey(args.PlayerId, args.TileX, args.TileY, args.ReturnValue != 0);
                if (ShouldLog(repairLogTicks, key, now, SummaryIntervalTicks))
                {
                    LogInfo($"AI standing-defense repair proximity evaluated: player={args.PlayerId}, tileX={args.TileX}, tileY={args.TileY}, vanillaRadius={observation.VanillaRadius}, configuredRadius={observation.ConfiguredRadius}, vanillaBlocked={(args.ReturnValue != 0)}, tick={now}.");
                }
            }
            catch (Exception ex)
            {
                LogFailure("repair proximity", ex);
            }
        }

        private int ObserveMaintenance(ulong aivStateAddress, int playerId, int mode)
        {
            if (!mapActive)
                return maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);

            int now;
            int delta;
            try
            {
                if (!IsAI(playerId))
                    return maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);
                now = SafeCurrentTick();
                delta = RecordInvocation(playerId, now);
            }
            catch (Exception ex)
            {
                LogFailure("ongoing-maintenance pre-diagnostic", ex);
                return maintenanceHook.Value.Hook.Trampoline(aivStateAddress, playerId, mode);
            }

            MaintenanceContext previous = activeContext;
            MaintenanceContext context = previous == null
                ? reusableContext ?? new MaintenanceContext()
                : new MaintenanceContext();
            if (previous == null)
                reusableContext = null;
            context.Reset(playerId, mode, now, delta);
            activeContext = context;
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
                if (context.Attempts.Count > 0)
                    LogSequence(context, result);
                LogSummaryIfDue(playerId, now);
            }
            catch (Exception ex)
            {
                LogFailure("ongoing-maintenance diagnostic", ex);
            }
            return result;
        }

        private int ObservePlacement(
            ulong placementStateAddress, int playerId, int offsetX, int offsetY,
            short mapperValue, int orientation)
        {
            MaintenanceContext context = activeContext;
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
            context.Attempts.Add(new PlacementAttempt(
                mapperValue, offsetX, offsetY, tileX, tileY, tileId, orientation, result));
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
                LogInfo($"AI defense building spawned: player={args.PlayerId}, type={args.Building}, buildingId={buildingId}, tileX={args.TileX}, tileY={args.TileY}, {DescribeBuilding(buildingId)}, tick={SafeCurrentTick()}.");
            }
            catch (Exception ex)
            {
                LogFailure("building-spawn diagnostic", ex);
            }
        }

        private void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || !mapActive || args.BuildingId <= 0)
                return;

            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building) ||
                    !IsAI(building->r_PlayerIdOwner) || !IsDefenseType(building->r_BuildingType))
                    return;

                LogInfo($"AI defense building deleting: player={building->r_PlayerIdOwner}, type={building->r_BuildingType}, buildingId={args.BuildingId}, globalId={building->r_GlobalId}, aliveState={building->r_AliveState}, bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-({building->r_TilePositionXEnd},{building->r_TilePositionYEnd}), tick={SafeCurrentTick()}.");
            }
            catch (Exception ex)
            {
                LogFailure("building-delete diagnostic", ex);
            }
        }

        private static string DescribeBuilding(int buildingId)
        {
            if (buildingId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                return "globalId=unavailable, aliveState=unavailable, bounds=unavailable";
            return $"globalId={building->r_GlobalId}, aliveState={building->r_AliveState}, bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-({building->r_TilePositionXEnd},{building->r_TilePositionYEnd})";
        }

        private int RecordInvocation(int playerId, int now)
        {
            PlayerDiagnostics diagnostics = players[playerId];
            int delta = diagnostics.LastTick.HasValue
                ? ElapsedTicks(now, diagnostics.LastTick.Value) : -1;
            diagnostics.LastTick = now;
            diagnostics.Total++;
            diagnostics.SinceSummary++;
            diagnostics.LastDelta = delta;
            if (delta >= 0)
            {
                diagnostics.MinimumDelta = Math.Min(diagnostics.MinimumDelta, delta);
                diagnostics.MaximumDelta = Math.Max(diagnostics.MaximumDelta, delta);
            }
            return delta;
        }

        private void LogSummaryIfDue(int playerId, int now)
        {
            PlayerDiagnostics diagnostics = players[playerId];
            if (diagnostics.LastSummaryTick.HasValue &&
                ElapsedTicks(now, diagnostics.LastSummaryTick.Value) < SummaryIntervalTicks)
                return;

            LogInfo($"AI ongoing castle-maintenance cadence: player={playerId}, tick={now}, callsSinceSummary={diagnostics.SinceSummary}, totalCalls={diagnostics.Total}, lastDeltaTicks={diagnostics.LastDelta}, minDeltaTicks={(diagnostics.MinimumDelta == int.MaxValue ? -1 : diagnostics.MinimumDelta)}, maxDeltaTicks={diagnostics.MaximumDelta}.");
            diagnostics.LastSummaryTick = now;
            diagnostics.SinceSummary = 0;
            diagnostics.MinimumDelta = int.MaxValue;
            diagnostics.MaximumDelta = -1;
        }

        private void LogSequence(MaintenanceContext context, int result)
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
            LogInfo($"AI ongoing defense placement sequence: player={context.PlayerId}, mode={context.Mode}, tick={context.Tick}, invocationDeltaTicks={context.DeltaTicks}, maintenanceResult={result}, attempts={context.Attempts.Count}, [{text}].");
        }

        private void LogFailure(string callback, Exception ex)
        {
            if (callbackFailureLogged)
                return;
            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(log,
                $"AI defense {callback} callback failed; further callback errors are suppressed and Vanilla remains active: {ex}");
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);

        private static bool ShouldLog<TKey>(Dictionary<TKey, int> ticks, TKey key, int now, int interval)
        {
            if (ticks.TryGetValue(key, out int previous) &&
                ElapsedTicks(now, previous) < interval)
                return false;
            ticks[key] = now;
            return true;
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

        private static bool IsDefenseType(eStructs type) =>
            type == eStructs.STRUCT_TOWER ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1 && (int)type <= (int)eStructs.STRUCT_TOWER5) ||
            type == eStructs.STRUCT_GATEHOUSE || type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD ||
            type == eStructs.STRUCT_GATE_POSTERN || type == eStructs.STRUCT_DRAWBRIDGE ||
            type == eStructs.STRUCT_TOWER5_DESTROYED ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1_DESTROYED && (int)type <= (int)eStructs.STRUCT_TOWER4_DESTROYED);

        private sealed class MaintenanceContext
        {
            internal int PlayerId { get; private set; }
            internal int Mode { get; private set; }
            internal int Tick { get; private set; }
            internal int DeltaTicks { get; private set; }
            internal List<PlacementAttempt> Attempts { get; } = new List<PlacementAttempt>(4);

            internal void Reset(int playerId, int mode, int tick, int deltaTicks)
            {
                PlayerId = playerId;
                Mode = mode;
                Tick = tick;
                DeltaTicks = deltaTicks;
                Attempts.Clear();
            }
        }

        private sealed class PlayerDiagnostics
        {
            internal int? LastTick;
            internal int? LastSummaryTick;
            internal int Total;
            internal int SinceSummary;
            internal int LastDelta = -1;
            internal int MinimumDelta = int.MaxValue;
            internal int MaximumDelta = -1;
            internal void Reset()
            {
                LastTick = null; LastSummaryTick = null; Total = 0; SinceSummary = 0;
                LastDelta = -1; MinimumDelta = int.MaxValue; MaximumDelta = -1;
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

        private readonly struct RepairKey : IEquatable<RepairKey>
        {
            internal RepairKey(int playerId, int x, int y, bool blocked)
            { PlayerId = playerId; X = x; Y = y; Blocked = blocked; }
            private int PlayerId { get; }
            private int X { get; }
            private int Y { get; }
            private bool Blocked { get; }
            public bool Equals(RepairKey other) =>
                PlayerId == other.PlayerId && X == other.X && Y == other.Y && Blocked == other.Blocked;
            public override bool Equals(object obj) => obj is RepairKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = (hash * 397) ^ X;
                    hash = (hash * 397) ^ Y;
                    return (hash * 397) ^ Blocked.GetHashCode();
                }
            }
        }
    }
}
